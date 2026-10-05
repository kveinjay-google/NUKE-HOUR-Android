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
using System.IO;
using System.Linq;
using System.Net;
using NUnit.Framework;
using OpenRA.Network;
using OpenRA.Server;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class NukeHourDedicatedServerTest
	{
		static RuntimeContractConfiguration PresentationResources() =>
			new(new MiniYaml(null, new[]
			{
				new MiniYamlNode("ResourceCapability", "ra2-presentation-capability-v2"),
				new MiniYamlNode("RequiredResources", "content|ra2.mix, content|language.mix"),
				new MiniYamlNode("OptionalResources", "content|ra2md.mix, content|langmd.mix"),
			}));

		[Test]
		public void DedicatedServerProfileIsExplicitlyDistinctFromClientProfiles()
		{
			var client = new RuntimeProfileIdentity(
				"linux", "x86-64", "release-20250330", "NUKE HOUR macOS 1.0.3 (Build 3)",
				"ra2", "dotnet", "external-import");

			var server = client.AsDedicatedServer();

			Assert.That(server.Platform, Is.EqualTo("linux-server"));
			Assert.That(server.Architecture, Is.EqualTo(client.Architecture));
			Assert.That(server.EngineVersion, Is.EqualTo(client.EngineVersion));
			Assert.That(server.Mod, Is.EqualTo(client.Mod));
			Assert.That(server.BuildVersion, Is.EqualTo("NUKE HOUR Dedicated Server 1.0.3 (Build 3)"));
			Assert.That(server.ImportedResourceMode, Is.EqualTo("headless-server"));
			Assert.That(server.Serialize(), Does.Not.Contain("platform=ios"));
		}

		[Test]
		public void HeadlessPresentationCapabilityMatchesAValidClientContract()
		{
			var configuration = PresentationResources();
			var clientFingerprint = RuntimeResourceFingerprint.Create(
				configuration, _ => new MemoryStream(Array.Empty<byte>()));
			var serverFingerprint = RuntimeResourceFingerprint.CreateHeadlessServer(
				configuration);

			Assert.That(serverFingerprint, Is.EqualTo(clientFingerprint));
		}

		[Test]
		public void HeadlessCapabilityCannotBypassNonPresentationResources()
		{
			var configuration = new RuntimeContractConfiguration(new MiniYaml(null, new[]
			{
				new MiniYamlNode("ResourceCapability", "ra2-gameplay-core-v1"),
				new MiniYamlNode("RequiredResources", "content|rules.bin"),
			}));

			Assert.That(
				() => RuntimeResourceFingerprint.CreateHeadlessServer(configuration),
				Throws.TypeOf<InvalidOperationException>());
		}

		[Test]
		public void HeadlessCapabilityRejectsGameplayResourceDriftUnderThePresentationId()
		{
			var configuration = new RuntimeContractConfiguration(new MiniYaml(null, new[]
			{
				new MiniYamlNode("ResourceCapability", "ra2-presentation-capability-v2"),
				new MiniYamlNode("RequiredResources", "content|ra2.mix, content|language.mix, content|rules.bin"),
				new MiniYamlNode("OptionalResources", "content|ra2md.mix, content|langmd.mix"),
			}));

			Assert.That(
				() => RuntimeResourceFingerprint.CreateHeadlessServer(configuration),
				Throws.TypeOf<InvalidOperationException>()
					.With.Message.Contains("DEDICATED_SERVER_RESOURCE_BLOCKER"));
		}

		[Test]
		public void ClientStillRejectsMissingRequiredPresentationResources()
		{
			Assert.That(
				() => RuntimeResourceFingerprint.Create(PresentationResources(), _ => null),
				Throws.TypeOf<InvalidDataException>());
		}

		[Test]
		public void RuntimeModeSelectsHeadlessExemptionOnlyForDedicatedServer()
		{
			Assert.Multiple(() =>
			{
				Assert.That(
					() => RuntimeResourceFingerprint.CreateForRuntime(
						PresentationResources(), _ => null, true),
					Throws.Nothing);
				Assert.That(
					() => RuntimeResourceFingerprint.CreateForRuntime(
						PresentationResources(), _ => null, false),
					Throws.TypeOf<InvalidDataException>());
			});
		}

		[TestCase(ServerState.WaitingPlayers, false, 0, DedicatedServerOperationalState.WaitingForPlayers)]
		[TestCase(ServerState.WaitingPlayers, false, 1, DedicatedServerOperationalState.InLobby)]
		[TestCase(ServerState.GameStarted, true, 2, DedicatedServerOperationalState.InGame)]
		[TestCase(ServerState.GameStarted, true, 0, DedicatedServerOperationalState.GameFinished)]
		[TestCase(ServerState.ShuttingDown, true, 0, DedicatedServerOperationalState.Stopping)]
		public void LifecycleMapsExistingServerState(
			ServerState state,
			bool gameStarted,
			int validatedClients,
			DedicatedServerOperationalState expected)
		{
			Assert.That(
				DedicatedServerRuntimePolicy.ResolveState(state, gameStarted, validatedClients),
				Is.EqualTo(expected));
		}

		[Test]
		public void ListenAddressAndDynamicPortAreValidated()
		{
			var any = DedicatedServerRuntimePolicy.ResolveEndpoints("0.0.0.0", 12340);
			var loopback = DedicatedServerRuntimePolicy.ResolveEndpoints("127.0.0.1", 12341);

			Assert.Multiple(() =>
			{
				Assert.That(any.Single(), Is.EqualTo(new IPEndPoint(IPAddress.Any, 12340)));
				Assert.That(loopback.Single(), Is.EqualTo(new IPEndPoint(IPAddress.Loopback, 12341)));
				Assert.That(
					() => DedicatedServerRuntimePolicy.ResolveEndpoints("not-an-address", 1234),
					Throws.TypeOf<ArgumentException>());
				Assert.That(
					() => DedicatedServerRuntimePolicy.ResolveEndpoints("0.0.0.0", 0),
					Throws.TypeOf<ArgumentOutOfRangeException>());
			});
		}

		[Test]
		public void RoomExitAndCapacityPoliciesPreserveHostIndependence()
		{
			Assert.Multiple(() =>
			{
				Assert.That(DedicatedServerRuntimePolicy.HasPlayerCapacity(7, 8), Is.True);
				Assert.That(DedicatedServerRuntimePolicy.HasPlayerCapacity(8, 8), Is.False);
				Assert.That(DedicatedServerRuntimePolicy.ShouldExit(
					ServerState.GameStarted, true, 1, TimeSpan.Zero, 900), Is.False,
					"one remaining client keeps the independent server alive");
				Assert.That(DedicatedServerRuntimePolicy.ShouldExit(
					ServerState.GameStarted, true, 0, TimeSpan.Zero, 900), Is.True,
					"the completed empty room exits instead of restarting");
				Assert.That(DedicatedServerRuntimePolicy.ShouldExit(
					ServerState.WaitingPlayers, false, 0, TimeSpan.FromSeconds(899), 900), Is.False);
				Assert.That(DedicatedServerRuntimePolicy.ShouldExit(
					ServerState.WaitingPlayers, false, 0, TimeSpan.FromSeconds(900), 900), Is.True);
			});
		}

		[Test]
		public void IdleDurationUsesMonotonicTimestamps()
		{
			Assert.Multiple(() =>
			{
				Assert.That(
					DedicatedServerRuntimePolicy.ElapsedSince(100, 115, 10),
					Is.EqualTo(TimeSpan.FromSeconds(1.5)));
				Assert.That(
					() => DedicatedServerRuntimePolicy.ElapsedSince(115, 100, 10),
					Throws.TypeOf<ArgumentOutOfRangeException>());
			});
		}

		[Test]
		public void DedicatedOperationalSettingsHaveSafeDefaults()
		{
			var settings = new ServerSettings();

			Assert.Multiple(() =>
			{
				Assert.That(settings.ListenAddress, Is.EqualTo("0.0.0.0"));
				Assert.That(settings.ListenPort, Is.EqualTo(1234));
				Assert.That(settings.MaxPlayers, Is.EqualTo(8));
				Assert.That(settings.IdleTimeoutSeconds, Is.EqualTo(900));
				Assert.That(settings.StatusFile, Is.EqualTo("status/server-status.json"));
			});
		}

		[Test]
		public void StatusDocumentSeparatesAliveFromReadyAndOmitsSecrets()
		{
			var document = DedicatedServerStatusDocument.Serialize(
				DedicatedServerOperationalState.WaitingForPlayers,
				"NUKE HOUR 1.0.3", "release-20250330", "ra2",
				"linux-server", "0.0.0.0", 12340, "map-uid", 0);

			Assert.Multiple(() =>
			{
				Assert.That(document, Does.Contain("\"processAlive\":true"));
				Assert.That(document, Does.Contain("\"ready\":true"));
				Assert.That(document, Does.Contain("\"state\":\"WAITING_FOR_PLAYERS\""));
				Assert.That(document, Does.Contain("\"listenPort\":12340"));
				Assert.That(document, Does.Not.Contain("Password"));
				Assert.That(document, Does.Not.Contain("IPAddress"));
			});
		}

		[Test]
		public void DedicatedServerCanLoadThePinnedEngineVersion()
		{
			var directory = Path.Combine(Path.GetTempPath(), $"nukehour-engine-version-{Guid.NewGuid():N}");
			Directory.CreateDirectory(directory);
			try
			{
				File.WriteAllText(Path.Combine(directory, "VERSION"), "release-20250330\n");
				Assert.That(Game.ReadEngineVersion(directory), Is.EqualTo("release-20250330"));
				Assert.That(Game.ReadEngineVersion(Path.Combine(directory, "missing")), Is.EqualTo("Unknown"));
			}
			finally
			{
				Directory.Delete(directory, true);
			}
		}

		[Test]
		public void DedicatedRuntimeDoesNotInitializeClientPresentationLoaders()
		{
			Assert.Multiple(() =>
			{
				Assert.That(DedicatedServerRuntimePolicy.ShouldInitializeClientPresentation(false), Is.True);
				Assert.That(DedicatedServerRuntimePolicy.ShouldInitializeClientPresentation(true), Is.False);
			});
		}

		[Test]
		public void DedicatedLifecycleExposesThreadSafeObservationAndCompletion()
		{
			Assert.Multiple(() =>
			{
				Assert.That(typeof(OpenRA.Server.Server).GetProperty(
					nameof(OpenRA.Server.Server.ValidatedConnectionCount)), Is.Not.Null);
				Assert.That(typeof(OpenRA.Server.Server).GetMethod(
					nameof(OpenRA.Server.Server.WaitForShutdown)), Is.Not.Null);
			});
		}
	}
}
