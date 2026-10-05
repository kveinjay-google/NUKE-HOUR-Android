#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenRA.Network;
using OpenRA.Server;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class NukeHourDirectTcpHandshakeTest
	{
		const string Engine = "release-20250330";
		const string Mod = "ra2";
		const string Core = "nukehour-core-sha256-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
		const string Resources = "1111111111111111111111111111111111111111111111111111111111111111";

		static RuntimeContractIdentity Contract(string resources = Resources) => RuntimeContractIdentity.Create(
			new RuntimeContractInputs(
				Engine, ProtocolVersion.Orders, Mod, Core, "ra2-presentation-capability-v2", resources));

		static RuntimeProfileIdentity Profile(string platform) =>
			new(platform, "arm64", Engine, $"NUKE HOUR {platform}", Mod, "dotnet", "external-import");

		static NetworkCompatibilityContext Context(RuntimeContractIdentity contract) =>
			new(Engine, Mod, Core, contract);

		static HandshakeRequest Request(string platform, RuntimeContractIdentity contract) => new()
		{
			HandshakeSchema = ProtocolVersion.HandshakeSchema,
			OrdersProtocol = ProtocolVersion.Orders,
			EngineCompatibility = Engine,
			Mod = Mod,
			Version = Core,
			RuntimeProfile = Profile(platform).Serialize(),
			RuntimeContract = contract.Serialize(),
		};

		static HandshakeResponse Response(string platform, RuntimeContractIdentity contract) => new()
		{
			HandshakeSchema = ProtocolVersion.HandshakeSchema,
			OrdersProtocol = ProtocolVersion.Orders,
			EngineCompatibility = Engine,
			Mod = Mod,
			Version = Core,
			RuntimeProfile = Profile(platform).Serialize(),
			RuntimeContract = contract.Serialize(),
			Client = new Session.Client { Name = "Cross-platform client" },
		};

		[TestCase("ios", "android")]
		[TestCase("android", "macos")]
		[TestCase("macos", "ios")]
		public async Task ProductionTcpFramingRoundTripsCrossPlatformHandshake(
			string serverPlatform, string clientPlatform)
		{
			var contract = Contract();
			var reason = await ExchangeHandshake(
				serverPlatform, Response(clientPlatform, contract), Context(contract));

			Assert.That(reason, Is.EqualTo(NetworkCompatibilityReason.Compatible));
		}

		[TestCase("missing-contract", NetworkCompatibilityReason.RuntimeContractMissing)]
		[TestCase("changed-resources", NetworkCompatibilityReason.RuntimeContractMismatch)]
		[TestCase("legacy-schema", NetworkCompatibilityReason.ProtocolMismatch)]
		public async Task ProductionServerAdmissionRejectsInvalidTcpHandshake(
			string scenario,
			NetworkCompatibilityReason expected)
		{
			var contract = Contract();
			var response = Response("android", contract);
			switch (scenario)
			{
				case "missing-contract":
					response.RuntimeContract = null;
					break;
				case "changed-resources":
					response.RuntimeContract = Contract(new string('2', 64)).Serialize();
					break;
				case "legacy-schema":
					response.HandshakeSchema = 0;
					break;
				default:
					Assert.Fail($"Unknown test scenario: {scenario}");
					break;
			}

			var reason = await ExchangeHandshake("macos", response, Context(contract));
			Assert.That(reason, Is.EqualTo(expected));
		}

		static async Task<NetworkCompatibilityReason> ExchangeHandshake(
			string serverPlatform,
			HandshakeResponse clientResponse,
			NetworkCompatibilityContext context)
		{
			Log.AddChannel("client", null);
			var contract = context.RuntimeContract;
			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			var endpoint = (IPEndPoint)listener.LocalEndpoint;
			var serverReason = NetworkCompatibilityReason.ProtocolMismatch;

			var serverTask = Task.Run(async () =>
			{
				using var client = await listener.AcceptTcpClientAsync();
				using var stream = client.GetStream();
				using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
				using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);

				writer.Write(ProtocolVersion.Handshake);
				writer.Write(1);
				WriteServerFrame(writer, HandshakeOrder(
					"HandshakeRequest", Request(serverPlatform, contract).Serialize()));
				writer.Flush();

				var packetLength = reader.ReadInt32();
				Assert.That(packetLength, Is.GreaterThan(4));
				Assert.That(reader.ReadInt32(), Is.EqualTo(0), "Immediate client response must use frame zero.");
				var orderBytes = reader.ReadBytes(packetLength - 4);
				Assert.That(orderBytes, Has.Length.EqualTo(packetLength - 4));
				var responseOrder = DeserializeOrder(orderBytes);
				Assert.That(responseOrder.OrderString, Is.EqualTo("HandshakeResponse"));
				NetworkCompatibility.TryValidateResponsePayload(
					responseOrder.TargetString, "TCP response", context, out _, out serverReason);
			});

			using var connection = new NetworkConnection(new ConnectionTarget("127.0.0.1", endpoint.Port));
			var packets = ReceivedPackets(connection);
			Assert.That(SpinWait.SpinUntil(() => !packets.IsEmpty, TimeSpan.FromSeconds(5)), Is.True,
				"The production NetworkConnection did not receive the handshake frame.");
			Assert.That(packets.TryDequeue(out var packet), Is.True);
			Assert.That(packet.FromClient, Is.EqualTo(0));
			Assert.That(BitConverter.ToInt32(packet.Data, 0), Is.EqualTo(0));
			var requestOrder = DeserializeOrder(packet.Data[4..]);
			Assert.That(requestOrder.OrderString, Is.EqualTo("HandshakeRequest"));
			var request = HandshakeRequest.Deserialize(requestOrder.TargetString, "TCP request");
			Assert.That(NetworkCompatibility.ValidateEnvelope(request, context),
				Is.EqualTo(NetworkCompatibilityReason.Compatible));
			Assert.That(NetworkCompatibility.Validate(request, Context(contract)),
				Is.EqualTo(NetworkCompatibilityReason.Compatible));

			((IConnection)connection).SendImmediate(new[]
			{
				HandshakeOrder("HandshakeResponse", clientResponse.Serialize())
			});

			var completed = await Task.WhenAny(serverTask, Task.Delay(TimeSpan.FromSeconds(5)));
			Assert.That(completed, Is.SameAs(serverTask), "TCP handshake response timed out.");
			await serverTask;
			listener.Stop();
			return serverReason;
		}

		static Order HandshakeOrder(string name, string payload) => new(name, null, false)
		{
			Type = OrderType.Handshake,
			IsImmediate = true,
			TargetString = payload,
		};

		static void WriteServerFrame(BinaryWriter writer, Order order)
		{
			var orderBytes = order.Serialize();
			writer.Write(orderBytes.Length + 4);
			writer.Write(0);
			writer.Write(0);
			writer.Write(orderBytes);
		}

		static Order DeserializeOrder(byte[] data)
		{
			using var stream = new MemoryStream(data, writable: false);
			using var reader = new BinaryReader(stream);
			return Order.Deserialize(null, reader);
		}

		static ConcurrentQueue<(int FromClient, byte[] Data)> ReceivedPackets(NetworkConnection connection)
		{
			var field = typeof(NetworkConnection).GetField(
				"receivedPackets", BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.That(field, Is.Not.Null);
			return (ConcurrentQueue<(int FromClient, byte[] Data)>)field.GetValue(connection);
		}
	}
}
