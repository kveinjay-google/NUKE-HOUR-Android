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
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OpenRA.Network;
using OpenRA.Server;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class NukeHourNetworkProtocolTest
	{
		const string Engine = "release-20250330";
		const string Mod = "ra2";
		const string CoreA = "nukehour-core-sha256-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
		const string CoreB = "nukehour-core-sha256-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
		const string ResourceA = "1111111111111111111111111111111111111111111111111111111111111111";
		const string ResourceB = "2222222222222222222222222222222222222222222222222222222222222222";

		static RuntimeProfileIdentity Profile(string platform, string architecture, string build) =>
			new(platform, architecture, Engine, build, Mod, "dotnet", "external-import");

		static RuntimeContractIdentity Contract(
			string core = CoreA,
			string resource = ResourceA,
			string localPath = "/ignored") =>
			RuntimeContractIdentity.Create(new RuntimeContractInputs(
				Engine, ProtocolVersion.Orders, Mod, core, "ra2-presentation-capability-v2", resource, localPath));

		static HandshakeRequest Request(RuntimeProfileIdentity profile, RuntimeContractIdentity contract) => new()
		{
			HandshakeSchema = ProtocolVersion.HandshakeSchema,
			OrdersProtocol = ProtocolVersion.Orders,
			EngineCompatibility = Engine,
			Mod = Mod,
			Version = CoreA,
			RuntimeProfile = profile.Serialize(),
			RuntimeContract = contract.Serialize(),
		};

		static HandshakeResponse Response(RuntimeProfileIdentity profile, RuntimeContractIdentity contract) => new()
		{
			HandshakeSchema = ProtocolVersion.HandshakeSchema,
			OrdersProtocol = ProtocolVersion.Orders,
			EngineCompatibility = Engine,
			Mod = Mod,
			Version = CoreA,
			RuntimeProfile = profile.Serialize(),
			RuntimeContract = contract.Serialize(),
			Client = new Session.Client(),
		};

		static NetworkCompatibilityContext Local(RuntimeContractIdentity contract) =>
			new(Engine, Mod, CoreA, contract);

		static RuntimeContractConfiguration Phase11Resources() =>
			new(new MiniYaml(null, new[]
			{
				new MiniYamlNode("ResourceCapability", "ra2-presentation-capability-v2"),
				new MiniYamlNode("RequiredResources", "content|ra2.mix, content|language.mix"),
				new MiniYamlNode("OptionalResources", "content|ra2md.mix, content|langmd.mix"),
			}));

		static string Phase11Fingerprint(IReadOnlyDictionary<string, byte[]> resources) =>
			RuntimeResourceFingerprint.Create(Phase11Resources(), logicalName =>
				resources.TryGetValue(logicalName, out var bytes)
					? new MemoryStream(bytes, writable: false) : null);

		[TestCase("ios", "arm64", "NUKE HOUR iOS 1.0.7 (Build 7)")]
		[TestCase("android", "arm64", "NUKE HOUR Android 0.0.3 (Build 3)")]
		[TestCase("macos", "arm64", "NUKE HOUR macOS 1.0.3 (Build 3)")]
		public void PlatformProfilesUseOneHandshakeAndAreCompatible(
			string platform, string architecture, string build)
		{
			var contract = Contract();
			var request = HandshakeRequest.Deserialize(
				Request(Profile(platform, architecture, build), contract).Serialize(), "request");
			var response = HandshakeResponse.Deserialize(
				Response(Profile(platform, architecture, build), contract).Serialize(), "response");

			Assert.Multiple(() =>
			{
				Assert.That(NetworkCompatibility.Validate(request, Local(contract)),
					Is.EqualTo(NetworkCompatibilityReason.Compatible));
				Assert.That(NetworkCompatibility.Validate(response, Local(contract)),
					Is.EqualTo(NetworkCompatibilityReason.Compatible));
			});
		}

		[Test]
		public void MissingRuntimeContractIsRejected()
		{
			var contract = Contract();
			var request = Request(Profile("ios", "arm64", "build"), contract);
			request.RuntimeContract = null;

			Assert.That(NetworkCompatibility.Validate(request, Local(contract)),
				Is.EqualTo(NetworkCompatibilityReason.RuntimeContractMissing));
		}

		[Test]
		public void InvalidRuntimeContractIsRejected()
		{
			var contract = Contract();
			var request = Request(Profile("android", "arm64", "build"), contract);
			request.RuntimeContract = "v1:ra2-presentation-capability-v2:not-a-digest";

			Assert.That(NetworkCompatibility.Validate(request, Local(contract)),
				Is.EqualTo(NetworkCompatibilityReason.RuntimeContractMismatch));
		}

		[Test]
		public void HandshakeSchemaMismatchIsRejectedBeforeOtherFields()
		{
			var contract = Contract();
			var request = Request(Profile("macos", "arm64", "build"), contract);
			request.HandshakeSchema++;
			request.EngineCompatibility = "different";

			Assert.That(NetworkCompatibility.Validate(request, Local(contract)),
				Is.EqualTo(NetworkCompatibilityReason.ProtocolMismatch));
		}

		[Test]
		public void UnsupportedEnvelopeIsRejectedBeforeModSelection()
		{
			var contract = Contract();
			var request = Request(Profile("ios", "arm64", "build"), contract);
			request.Mod = "external-mod";
			request.HandshakeSchema = 0;

			Assert.That(NetworkCompatibility.ValidateEnvelope(request, Local(contract)),
				Is.EqualTo(NetworkCompatibilityReason.ProtocolMismatch));
		}

		[Test]
		public void OrdersProtocolMismatchIsRejected()
		{
			var contract = Contract();
			var response = Response(Profile("ios", "arm64", "build"), contract);
			response.OrdersProtocol--;

			Assert.That(NetworkCompatibility.Validate(response, Local(contract)),
				Is.EqualTo(NetworkCompatibilityReason.ProtocolMismatch));
		}

		[Test]
		public void RankedAdmissionMetadataRoundTripsOnlyInTheClientResponse()
		{
			var contract = Contract();
			var response = Response(Profile("ios", "arm64", "build"), contract);
			response.RankedMatchId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
			response.RankedAdmissionToken = "single-use-admission-token-that-is-long";
			response.RankedDeviceFingerprint =
				"sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

			var parsed = HandshakeResponse.Deserialize(response.Serialize(), "ranked-response");
			Assert.Multiple(() =>
			{
				Assert.That(parsed.RankedMatchId, Is.EqualTo(response.RankedMatchId));
				Assert.That(parsed.RankedAdmissionToken, Is.EqualTo(response.RankedAdmissionToken));
				Assert.That(parsed.RankedDeviceFingerprint, Is.EqualTo(response.RankedDeviceFingerprint));
				Assert.That(typeof(HandshakeRequest).GetField("RankedAdmissionToken"), Is.Null);
			});
		}

		[Test]
		public void OrdinaryConnectionsCanClearAbandonedRankedAdmission()
		{
			var assignment = new RankedMatchAssignment(
				"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "Opponent",
				new ConnectionTarget("127.0.0.1", 1234),
				"single-use-admission-token-that-is-long", "sha256:device", DateTime.UtcNow.AddMinutes(1));

			CurrentServerSettings.SetRankedAdmission(assignment);
			CurrentServerSettings.ClearRankedAdmission();

			Assert.That(CurrentServerSettings.TakeRankedAdmission(), Is.Null);
		}

		[Test]
		public void SameResourcesAtDifferentLocalPathsHaveSameContract()
		{
			Assert.That(Contract(localPath: "/var/mobile/content"),
				Is.EqualTo(Contract(localPath: "/Users/player/Library/content")));
		}

		[Test]
		public void ReimportingSameResourcesHasSameContract()
		{
			var firstImport = Contract(localPath: "/data/user/0/import-a");
			var secondImport = Contract(localPath: "/data/user/0/import-b");

			Assert.That(secondImport, Is.EqualTo(firstImport));
		}

		[Test]
		public void ResourceFingerprintUsesLogicalContentAndIgnoresSourcePaths()
		{
			var configuration = new RuntimeContractConfiguration(new MiniYaml(null, new[]
			{
				new MiniYamlNode("ResourceCapability", "ra2-presentation-capability-v2"),
				new MiniYamlNode("RequiredResources", "content|ra2.mix, content|language.mix"),
				new MiniYamlNode("OptionalResources", "content|ra2md.mix"),
			}));
			var resources = new Dictionary<string, byte[]>(StringComparer.Ordinal)
			{
				["content|ra2.mix"] = new byte[] { 1, 2, 3 },
				["content|language.mix"] = new byte[] { 4, 5, 6 },
			};
			const string FirstPath = "/var/mobile/import";
			const string SecondPath = "/data/user/0/import";
			Stream Open(string path, string logicalName) =>
				resources.TryGetValue(logicalName, out var bytes) && path.Length > 0
					? new MemoryStream(bytes, writable: false) : null;

			var first = RuntimeResourceFingerprint.Create(
				configuration, logicalName => Open(FirstPath, logicalName));
			var second = RuntimeResourceFingerprint.Create(
				configuration, logicalName => Open(SecondPath, logicalName));
			Assert.That(second, Is.EqualTo(first));
		}

		[Test]
		public void ResourceCapabilityFingerprintRejectsMissingButIgnoresPresentationBytes()
		{
			var configuration = new RuntimeContractConfiguration(new MiniYaml(null, new[]
			{
				new MiniYamlNode("ResourceCapability", "ra2-presentation-capability-v2"),
				new MiniYamlNode("RequiredResources", "content|ra2.mix"),
			}));
			var first = RuntimeResourceFingerprint.Create(
				configuration, _ => new MemoryStream(new byte[] { 1, 2, 3 }, writable: false));
			var changed = RuntimeResourceFingerprint.Create(
				configuration, _ => new MemoryStream(new byte[] { 1, 2, 4 }, writable: false));

			Assert.Multiple(() =>
			{
				Assert.That(changed, Is.EqualTo(first));
				Assert.Throws<InvalidDataException>(() =>
					RuntimeResourceFingerprint.Create(configuration, _ => null));
			});
		}

		[Test]
		public void GameplayCriticalCoreChangeIsRejectedBeforeAdmission()
		{
			var localContract = Contract(CoreA);
			var changedContract = Contract(CoreB);
			var response = Response(Profile("android", "arm64", "build"), changedContract);

			Assert.That(NetworkCompatibility.Validate(response, Local(localContract)),
				Is.EqualTo(NetworkCompatibilityReason.RuntimeContractMismatch));
		}

		[Test]
		public void ResourceCapabilityMismatchIsRejectedBeforeAdmission()
		{
			var localContract = Contract(resource: ResourceA);
			var changedContract = Contract(resource: ResourceB);
			var response = Response(Profile("android", "arm64", "build"), changedContract);

			Assert.That(NetworkCompatibility.Validate(response, Local(localContract)),
				Is.EqualTo(NetworkCompatibilityReason.RuntimeContractMismatch));
		}

		[Test]
		public void FullHandshakeRoundTripPreservesRequiredSchema()
		{
			var contract = Contract();
			var original = Response(Profile("macos", "x64", "NUKE HOUR macOS 1.0.3 (Build 3)"), contract);
			var roundTrip = HandshakeResponse.Deserialize(original.Serialize(), "round trip");

			Assert.Multiple(() =>
			{
				Assert.That(roundTrip.HandshakeSchema, Is.EqualTo(original.HandshakeSchema));
				Assert.That(roundTrip.OrdersProtocol, Is.EqualTo(original.OrdersProtocol));
				Assert.That(roundTrip.EngineCompatibility, Is.EqualTo(original.EngineCompatibility));
				Assert.That(roundTrip.RuntimeProfile, Is.EqualTo(original.RuntimeProfile));
				Assert.That(roundTrip.RuntimeContract, Is.EqualTo(original.RuntimeContract));
			});
		}

		[Test]
		public void FutureOptionalFieldIsIgnoredWithinSupportedSchema()
		{
			var contract = Contract();
			var data = Request(Profile("ios", "arm64", "build"), contract).Serialize()
				.Replace("Handshake:\n", "Handshake:\n\tFutureOptionalField: value\n", StringComparison.Ordinal);
			var request = HandshakeRequest.Deserialize(data, "future field");

			Assert.That(NetworkCompatibility.Validate(request, Local(contract)),
				Is.EqualTo(NetworkCompatibilityReason.Compatible));
		}

		[Test]
		public void MalformedHandshakeHasNonThrowingProtocolRejectionPath()
		{
			Assert.Multiple(() =>
			{
				Assert.That(HandshakeRequest.TryDeserialize(
					"Handshake:\n\tHandshakeSchema: nope\n", "bad request", out _), Is.False);
				Assert.That(HandshakeResponse.TryDeserialize(
					"Handshake:\n\tOrdersProtocol: nope\n", "bad response", out _), Is.False);
			});
		}

		[Test]
		public void EngineAndModFailuresRemainDistinct()
		{
			var contract = Contract();
			var engineMismatch = Request(Profile("ios", "arm64", "build"), contract);
			engineMismatch.EngineCompatibility = "different";
			var modMismatch = Request(Profile("ios", "arm64", "build"), contract);
			modMismatch.Mod = "different";
			modMismatch.RuntimeProfile = new RuntimeProfileIdentity(
				"ios", "arm64", Engine, "build", "different", "dotnet", "external-import").Serialize();

			Assert.Multiple(() =>
			{
				Assert.That(NetworkCompatibility.Validate(engineMismatch, Local(contract)),
					Is.EqualTo(NetworkCompatibilityReason.EngineMismatch));
				Assert.That(NetworkCompatibility.Validate(modMismatch, Local(contract)),
					Is.EqualTo(NetworkCompatibilityReason.ModMismatch));
			});
		}

		[Test]
		public void DebugSummaryOmitsPathsAndFullContractDigest()
		{
			var contract = Contract(localPath: "/private/player/content");
			Assert.That(NetworkCompatibility.TryGetDebugSummary(
				Profile("ios", "arm64", "build").Serialize(), contract.Serialize(), out var summary), Is.True);
			Assert.Multiple(() =>
			{
				Assert.That(summary, Does.Contain("platform=ios"));
				Assert.That(summary, Does.Contain(contract.Digest[..12]));
				Assert.That(summary, Does.Not.Contain(contract.Digest));
				Assert.That(summary, Does.Not.Contain("/private/player/content"));
			});
		}

		[Test]
		public void MapUidMismatchHasExplicitReasonAndDirectPortIsUnchanged()
		{
			Assert.Multiple(() =>
			{
				Assert.That(NetworkCompatibility.ValidateMap("map-a", "map-b"),
					Is.EqualTo(NetworkCompatibilityReason.MapMismatch));
				Assert.That(new ServerSettings().ListenPort, Is.EqualTo(1234));
				Assert.That(ProtocolVersion.Handshake, Is.EqualTo(7));
				Assert.That(ProtocolVersion.Orders, Is.EqualTo(23));
			});
		}

		[Test]
		public void OldWholeArchiveResourceCapabilityIsRejected()
		{
			var localContract = Contract();
			var oldContract = RuntimeContractIdentity.Create(new RuntimeContractInputs(
				Engine, ProtocolVersion.Orders, Mod, CoreA, "ra2-required-v1", ResourceA));
			var remote = Response(Profile("macos", "arm64", "old-build"), oldContract);

			Assert.That(NetworkCompatibility.Validate(remote, Local(localContract)),
				Is.EqualTo(NetworkCompatibilityReason.RuntimeContractMismatch));
		}

		[Test]
		public void RC1_IdenticalResourcesAtDifferentPathsProduceIdenticalContract()
		{
			var resources = new Dictionary<string, byte[]>
			{
				["content|ra2.mix"] = new byte[] { 1, 2, 3 },
				["content|language.mix"] = new byte[] { 4, 5, 6 },
			};

			var fingerprint = Phase11Fingerprint(resources);
			var first = RuntimeContractIdentity.Create(new RuntimeContractInputs(
				Engine, ProtocolVersion.Orders, Mod, CoreA,
				"ra2-presentation-capability-v2", fingerprint, "/first/import"));
			var second = RuntimeContractIdentity.Create(new RuntimeContractInputs(
				Engine, ProtocolVersion.Orders, Mod, CoreA,
				"ra2-presentation-capability-v2", fingerprint, "/second/import"));

			Assert.That(second, Is.EqualTo(first));
		}

		[Test]
		public void RC2_ReimportedResourcesProduceIdenticalFingerprint()
		{
			var first = Phase11Fingerprint(new Dictionary<string, byte[]>
			{
				["content|ra2.mix"] = new byte[] { 1, 2, 3 },
				["content|language.mix"] = new byte[] { 4, 5, 6 },
			});
			var reimported = Phase11Fingerprint(new Dictionary<string, byte[]>
			{
				["content|language.mix"] = new byte[] { 4, 5, 6 },
				["content|ra2.mix"] = new byte[] { 1, 2, 3 },
			});

			Assert.That(reimported, Is.EqualTo(first));
		}

		[Test]
		public void RC3_PresentationOnlyDifferencesRemainGameplayCompatible()
		{
			var first = Phase11Fingerprint(new Dictionary<string, byte[]>
			{
				["content|ra2.mix"] = new byte[] { 1, 2, 3 },
				["content|language.mix"] = new byte[] { 4, 5, 6 },
				["content|langmd.mix"] = new byte[] { 10, 11, 12 },
			});
			var changedPresentation = Phase11Fingerprint(new Dictionary<string, byte[]>
			{
				["content|ra2.mix"] = new byte[] { 1, 2, 3 },
				["content|language.mix"] = new byte[] { 4, 5, 6 },
				["content|langmd.mix"] = new byte[] { 20, 21, 22, 23 },
			});

			Assert.That(changedPresentation, Is.EqualTo(first));
		}

		[Test]
		public void RC4_LocalizationOnlyDifferencesRemainGameplayCompatible()
		{
			var english = Phase11Fingerprint(new Dictionary<string, byte[]>
			{
				["content|ra2.mix"] = new byte[] { 1, 2, 3 },
				["content|language.mix"] = new byte[] { 10, 11, 12 },
			});
			var chinese = Phase11Fingerprint(new Dictionary<string, byte[]>
			{
				["content|ra2.mix"] = new byte[] { 1, 2, 3 },
				["content|language.mix"] = new byte[] { 20, 21, 22, 23 },
			});

			Assert.That(chinese, Is.EqualTo(english));
		}

		[Test]
		public void RC5_GameplayRuleDifferenceIsRejected()
		{
			var localContract = Contract(CoreA);
			var remote = Response(Profile("android", "arm64", "build"), Contract(CoreB));
			remote.Version = CoreB;

			Assert.That(NetworkCompatibility.Validate(remote, Local(localContract)),
				Is.EqualTo(NetworkCompatibilityReason.ModMismatch));
		}

		[Test]
		public void RC6_MissingRequiredGameplayAssetIsRejected()
		{
			var gameplayResources = new RuntimeContractConfiguration(new MiniYaml(null, new[]
			{
				new MiniYamlNode("ResourceCapability", "future-gameplay-capability-v1"),
				new MiniYamlNode("RequiredResources", "content|gameplay-rules.bundle"),
			}));

			Assert.Throws<InvalidDataException>(() =>
				RuntimeResourceFingerprint.Create(gameplayResources, _ => null));
		}

		[Test]
		public void RC7_ModIdentifierMismatchIsRejected()
		{
			var contract = Contract();
			var remote = Request(Profile("ios", "arm64", "build"), contract);
			remote.Mod = "different";
			remote.RuntimeProfile = new RuntimeProfileIdentity(
				"ios", "arm64", Engine, "build", "different", "dotnet", "external-import").Serialize();

			Assert.That(NetworkCompatibility.Validate(remote, Local(contract)),
				Is.EqualTo(NetworkCompatibilityReason.ModMismatch));
		}

		[Test]
		public void RC8_ModGameplayCompatibilityMismatchIsRejected()
		{
			var localContract = Contract(CoreA);
			var remote = Response(Profile("macos", "arm64", "build"), localContract);
			remote.Version = CoreB;

			Assert.That(NetworkCompatibility.Validate(remote, Local(localContract)),
				Is.EqualTo(NetworkCompatibilityReason.ModMismatch));
		}

		[Test]
		public void RC9_EngineCompatibilityMismatchIsRejected()
		{
			var contract = Contract();
			var remote = Request(Profile("ios", "arm64", "build"), contract);
			remote.EngineCompatibility = "different-engine";

			Assert.That(NetworkCompatibility.Validate(remote, Local(contract)),
				Is.EqualTo(NetworkCompatibilityReason.EngineMismatch));
		}
	}
}
