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
using System.Security.Cryptography;
using System.Text;
using OpenRA.Server;

namespace OpenRA.Network
{
	public sealed class RuntimeContractConfiguration : IGlobalModData
	{
		public readonly string ResourceCapability;
		public readonly string[] RequiredResources = Array.Empty<string>();
		public readonly string[] OptionalResources = Array.Empty<string>();

		public RuntimeContractConfiguration() { }

		public RuntimeContractConfiguration(MiniYaml yaml)
		{
			FieldLoader.Load(this, yaml);
		}
	}

	public enum NetworkCompatibilityReason
	{
		Compatible = 0,
		ProtocolMismatch = 1,
		RuntimeContractMissing = 2,
		RuntimeContractMismatch = 3,
		ModMismatch = 4,
		MapMismatch = 5,
		EngineMismatch = 6,
	}

	public static class NetworkCompatibilityReasonExts
	{
		public static string Code(this NetworkCompatibilityReason reason) => reason switch
		{
			NetworkCompatibilityReason.Compatible => "PASS",
			NetworkCompatibilityReason.ProtocolMismatch => "PROTOCOL_MISMATCH",
			NetworkCompatibilityReason.RuntimeContractMissing => "RUNTIME_CONTRACT_MISSING",
			NetworkCompatibilityReason.RuntimeContractMismatch => "RUNTIME_CONTRACT_MISMATCH",
			NetworkCompatibilityReason.ModMismatch => "MOD_MISMATCH",
			NetworkCompatibilityReason.MapMismatch => "MAP_MISMATCH",
			NetworkCompatibilityReason.EngineMismatch => "ENGINE_MISMATCH",
			_ => "PROTOCOL_MISMATCH",
		};

		public static string MessageKey(this NetworkCompatibilityReason reason) => reason switch
		{
			NetworkCompatibilityReason.RuntimeContractMissing =>
				"notification-runtime-contract-missing",
			NetworkCompatibilityReason.RuntimeContractMismatch =>
				"notification-runtime-contract-mismatch",
			NetworkCompatibilityReason.ModMismatch => "notification-incompatible-mod",
			NetworkCompatibilityReason.MapMismatch => "notification-map-mismatch",
			NetworkCompatibilityReason.EngineMismatch => "notification-engine-mismatch",
			_ => "notification-incompatible-protocol",
		};
	}

	public sealed class RuntimeProfileIdentity : IEquatable<RuntimeProfileIdentity>
	{
		const int SchemaVersion = 1;
		const int MaxValueLength = 192;
		static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

		public string Platform { get; }
		public string Architecture { get; }
		public string EngineVersion { get; }
		public string BuildVersion { get; }
		public string Mod { get; }
		public string RuntimeType { get; }
		public string ImportedResourceMode { get; }

		public RuntimeProfileIdentity(
			string platform,
			string architecture,
			string engineVersion,
			string buildVersion,
			string mod,
			string runtimeType,
			string importedResourceMode)
		{
			Platform = ValidateToken(platform, nameof(platform));
			Architecture = ValidateToken(architecture, nameof(architecture));
			EngineVersion = ValidateValue(engineVersion, nameof(engineVersion));
			BuildVersion = ValidateValue(buildVersion, nameof(buildVersion));
			Mod = ValidateToken(mod, nameof(mod));
			RuntimeType = ValidateToken(runtimeType, nameof(runtimeType));
			ImportedResourceMode = ValidateToken(importedResourceMode, nameof(importedResourceMode));
		}

		public static RuntimeProfileIdentity CreateCurrent(Manifest manifest)
		{
			if (manifest == null)
				throw new ArgumentNullException(nameof(manifest));

			var platform = OpenRA.Platform.CurrentPlatform switch
			{
				PlatformType.OSX => "macos",
				PlatformType.iOS => "ios",
				_ => OpenRA.Platform.CurrentPlatform.ToString().ToLowerInvariant(),
			};
			var runtimeType = OpenRA.Platform.RuntimeVersion.StartsWith("Mono", StringComparison.Ordinal)
				? "mono" : "dotnet";

			return new RuntimeProfileIdentity(
				platform,
				OpenRA.Platform.CurrentArchitecture.ToString().ToLowerInvariant(),
				Game.EngineVersion ?? "Unknown",
				manifest.Metadata.DisplayVersionOrVersion,
				manifest.Id,
				runtimeType,
				"external-import");
		}

		public string Serialize() => string.Join(";", new[]
		{
			$"v{SchemaVersion}",
			$"platform={Encode(Platform)}",
			$"architecture={Encode(Architecture)}",
			$"engine={Encode(EngineVersion)}",
			$"build={Encode(BuildVersion)}",
			$"mod={Encode(Mod)}",
			$"runtime={Encode(RuntimeType)}",
			$"resources={Encode(ImportedResourceMode)}",
		});

		public RuntimeProfileIdentity AsDedicatedServer()
		{
			var platform = Platform.EndsWith("-server", StringComparison.Ordinal)
				? Platform : $"{Platform}-server";
			var buildVersion = BuildVersion;
			const string brandPrefix = "NUKE HOUR ";
			if (buildVersion.StartsWith(brandPrefix, StringComparison.Ordinal))
				buildVersion = buildVersion[brandPrefix.Length..];
			foreach (var clientPlatform in new[] { "macOS ", "iOS ", "Android " })
				if (buildVersion.StartsWith(clientPlatform, StringComparison.Ordinal))
				{
					buildVersion = buildVersion[clientPlatform.Length..];
					break;
				}

			buildVersion = $"{brandPrefix}Dedicated Server {buildVersion}";
			return new RuntimeProfileIdentity(
				platform, Architecture, EngineVersion, buildVersion, Mod,
				RuntimeType, "headless-server");
		}

		public static bool TryParse(string value, out RuntimeProfileIdentity profile)
		{
			profile = null;
			if (string.IsNullOrEmpty(value) || value.Length > 1536)
				return false;

			var fields = value.Split(';');
			if (fields.Length != 8 || fields[0] != $"v{SchemaVersion}")
				return false;

			var names = new[] { "platform", "architecture", "engine", "build", "mod", "runtime", "resources" };
			var decoded = new string[names.Length];
			try
			{
				for (var i = 0; i < names.Length; i++)
				{
					var prefix = names[i] + "=";
					if (!fields[i + 1].StartsWith(prefix, StringComparison.Ordinal))
						return false;

					decoded[i] = Uri.UnescapeDataString(fields[i + 1][prefix.Length..]);
				}

				profile = new RuntimeProfileIdentity(
					decoded[0], decoded[1], decoded[2], decoded[3],
					decoded[4], decoded[5], decoded[6]);
				if (profile.Serialize() != value)
				{
					profile = null;
					return false;
				}

				return true;
			}
			catch (ArgumentException)
			{
				profile = null;
				return false;
			}
		}

		static string Encode(string value) => Uri.EscapeDataString(value);

		static string ValidateToken(string value, string parameterName)
		{
			ValidateValue(value, parameterName);
			if (value.Length > 64 || value.Any(character =>
				!(character is >= 'a' and <= 'z') &&
				!(character is >= '0' and <= '9') && character != '-'))
				throw new ArgumentException("Runtime profile token is not canonical.", parameterName);

			return value;
		}

		static string ValidateValue(string value, string parameterName)
		{
			if (string.IsNullOrEmpty(value) || value.Length > MaxValueLength ||
				!value.IsNormalized(NormalizationForm.FormC) ||
				value.Any(character => char.IsControl(character)))
				throw new ArgumentException("Runtime profile value is invalid.", parameterName);

			try
			{
				if (StrictUtf8.GetByteCount(value) > MaxValueLength * 4)
					throw new ArgumentException("Runtime profile value is too large.", parameterName);
			}
			catch (EncoderFallbackException exception)
			{
				throw new ArgumentException("Runtime profile value is not valid UTF-8.", parameterName, exception);
			}

			return value;
		}

		public bool Equals(RuntimeProfileIdentity other) => other != null && Serialize() == other.Serialize();
		public override bool Equals(object obj) => Equals(obj as RuntimeProfileIdentity);
		public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Serialize());
	}

	public sealed class RuntimeContractInputs
	{
		static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

		public string EngineCompatibility { get; }
		public int OrdersProtocol { get; }
		public string Mod { get; }
		public string CoreCompatibility { get; }
		public string ResourceCapability { get; }
		public string ResourceFingerprint { get; }

		// Diagnostic-only input. It is intentionally never serialized or hashed.
		public string LocalContentRoot { get; }

		public RuntimeContractInputs(
			string engineCompatibility,
			int ordersProtocol,
			string mod,
			string coreCompatibility,
			string resourceCapability,
			string resourceFingerprint,
			string localContentRoot = null)
		{
			EngineCompatibility = Require(engineCompatibility, nameof(engineCompatibility));
			if (ordersProtocol <= 0)
				throw new ArgumentOutOfRangeException(nameof(ordersProtocol));

			OrdersProtocol = ordersProtocol;
			Mod = Require(mod, nameof(mod));
			CoreCompatibility = Require(coreCompatibility, nameof(coreCompatibility));
			ResourceCapability = Require(resourceCapability, nameof(resourceCapability));
			ResourceFingerprint = RequireLowerSha256(resourceFingerprint, nameof(resourceFingerprint));
			LocalContentRoot = localContentRoot;
		}

		static string RequireLowerSha256(string value, string parameterName)
		{
			if (value == null || value.Length != 64 || value.Any(character =>
				!(character is >= '0' and <= '9') && !(character is >= 'a' and <= 'f')))
				throw new ArgumentException("Runtime resource fingerprint is not lowercase SHA-256.", parameterName);

			return value;
		}

		static string Require(string value, string parameterName)
		{
			if (string.IsNullOrEmpty(value) || value.Length > 512 ||
				!value.IsNormalized(NormalizationForm.FormC) ||
				value.Any(character => char.IsControl(character)))
				throw new ArgumentException("Runtime contract input is invalid.", parameterName);

			try
			{
				if (StrictUtf8.GetByteCount(value) > 2048)
					throw new ArgumentException("Runtime contract input is too large.", parameterName);
			}
			catch (EncoderFallbackException exception)
			{
				throw new ArgumentException("Runtime contract input is not valid UTF-8.", parameterName, exception);
			}

			return value;
		}
	}

	public static class RuntimeResourceFingerprint
	{
		const int MaxResources = 32;
		const string HeadlessPresentationCapability = "ra2-presentation-capability-v2";
		static readonly string[] HeadlessRequiredPresentationResources =
		{
			"content|language.mix",
			"content|ra2.mix",
		};
		static readonly string[] HeadlessOptionalPresentationResources =
		{
			"content|langmd.mix",
			"content|ra2md.mix",
		};

		public static string Create(
			RuntimeContractConfiguration configuration,
			Func<string, Stream> openResource)
		{
			if (configuration == null)
				throw new ArgumentNullException(nameof(configuration));
			if (openResource == null)
				throw new ArgumentNullException(nameof(openResource));

			var resources = configuration.RequiredResources
				.Select(name => (Name: ValidateLogicalName(name), Required: true))
				.Concat(configuration.OptionalResources
					.Select(name => (Name: ValidateLogicalName(name), Required: false)))
				.OrderBy(resource => resource.Name, StringComparer.Ordinal)
				.ToArray();
			if (resources.Length > MaxResources ||
				resources.Select(resource => resource.Name).Distinct(StringComparer.Ordinal).Count() != resources.Length)
				throw new ArgumentException("Runtime resources must be bounded and unique.", nameof(configuration));

			using var framed = new MemoryStream();
			var requiredResources = resources.Where(resource => resource.Required).ToArray();
			Write(framed, "NUKEHOUR-RUNTIME-RESOURCE-CAPABILITIES-V2");
			Write(framed, requiredResources.Length.ToStringInvariant());
			foreach (var resource in requiredResources)
			{
				Write(framed, resource.Name);
				using var stream = openResource(resource.Name);
				if (stream == null)
					throw new InvalidDataException($"Required runtime resource is missing: {resource.Name}");

				Write(framed, "present");
			}

			return Convert.ToHexString(SHA256.HashData(framed.ToArray())).ToLowerInvariant();
		}

		public static string CreateHeadlessServer(RuntimeContractConfiguration configuration)
		{
			if (configuration == null)
				throw new ArgumentNullException(nameof(configuration));
			var required = configuration.RequiredResources
				.Select(ValidateLogicalName).OrderBy(name => name, StringComparer.Ordinal).ToArray();
			var optional = configuration.OptionalResources
				.Select(ValidateLogicalName).OrderBy(name => name, StringComparer.Ordinal).ToArray();
			if (!string.Equals(configuration.ResourceCapability, HeadlessPresentationCapability, StringComparison.Ordinal) ||
				!required.SequenceEqual(HeadlessRequiredPresentationResources, StringComparer.Ordinal) ||
				!optional.SequenceEqual(HeadlessOptionalPresentationResources, StringComparer.Ordinal))
				throw new InvalidOperationException(
					"DEDICATED_SERVER_RESOURCE_BLOCKER: the audited presentation resource set changed.");

			return Create(configuration, _ => new MemoryStream(Array.Empty<byte>(), false));
		}

		public static string CreateForRuntime(
			RuntimeContractConfiguration configuration,
			Func<string, Stream> openResource,
			bool dedicatedServer) => dedicatedServer
			? CreateHeadlessServer(configuration)
			: Create(configuration, openResource);

		static string ValidateLogicalName(string value)
		{
			if (string.IsNullOrEmpty(value) || value.Length > 192 ||
				!value.IsNormalized(NormalizationForm.FormC) || value.StartsWith('/') ||
				value.Contains('\\') || value.Split('/').Contains("..") ||
				value.Any(character => char.IsControl(character)))
				throw new ArgumentException("Runtime resource name is not canonical.", nameof(value));

			return value;
		}

		static void Write(Stream stream, string value)
		{
			var bytes = Encoding.UTF8.GetBytes(value);
			stream.WriteByte((byte)bytes.Length);
			stream.WriteByte((byte)(bytes.Length >> 8));
			stream.WriteByte((byte)(bytes.Length >> 16));
			stream.WriteByte((byte)(bytes.Length >> 24));
			stream.Write(bytes, 0, bytes.Length);
		}
	}

	public readonly struct RuntimeContractIdentity : IEquatable<RuntimeContractIdentity>
	{
		public const int SchemaVersion = 1;

		public string ResourceCapability { get; }
		public string Digest { get; }

		RuntimeContractIdentity(string resourceCapability, string digest)
		{
			ResourceCapability = ValidateCapability(resourceCapability);
			Digest = ValidateDigest(digest);
		}

		public static RuntimeContractIdentity Create(RuntimeContractInputs inputs)
		{
			if (inputs == null)
				throw new ArgumentNullException(nameof(inputs));

			using var framed = new MemoryStream();
			Write(framed, "NUKEHOUR-RUNTIME-CONTRACT");
			Write(framed, SchemaVersion.ToStringInvariant());
			Write(framed, inputs.EngineCompatibility);
			Write(framed, inputs.OrdersProtocol.ToStringInvariant());
			Write(framed, inputs.Mod);
			Write(framed, inputs.CoreCompatibility);
			Write(framed, inputs.ResourceCapability);
			Write(framed, inputs.ResourceFingerprint);
			var digest = Convert.ToHexString(SHA256.HashData(framed.ToArray())).ToLowerInvariant();
			return new RuntimeContractIdentity(inputs.ResourceCapability, digest);
		}

		public string Serialize() => $"v{SchemaVersion}:{ResourceCapability}:{Digest}";

		public static bool TryParse(string value, out RuntimeContractIdentity contract)
		{
			contract = default;
			if (string.IsNullOrEmpty(value))
				return false;

			var fields = value.Split(':');
			if (fields.Length != 3 || fields[0] != $"v{SchemaVersion}")
				return false;

			try
			{
				contract = new RuntimeContractIdentity(fields[1], fields[2]);
				return contract.Serialize() == value;
			}
			catch (ArgumentException)
			{
				contract = default;
				return false;
			}
		}

		static void Write(Stream stream, string value)
		{
			var bytes = Encoding.UTF8.GetBytes(value);
			stream.WriteByte((byte)bytes.Length);
			stream.WriteByte((byte)(bytes.Length >> 8));
			stream.WriteByte((byte)(bytes.Length >> 16));
			stream.WriteByte((byte)(bytes.Length >> 24));
			stream.Write(bytes, 0, bytes.Length);
		}

		static string ValidateCapability(string value)
		{
			if (string.IsNullOrEmpty(value) || value.Length > 64 || value.Any(character =>
				!(character is >= 'a' and <= 'z') &&
				!(character is >= '0' and <= '9') && character != '-'))
				throw new ArgumentException("Runtime resource capability is not canonical.", nameof(value));

			return value;
		}

		static string ValidateDigest(string value)
		{
			if (value == null || value.Length != 64 || value.Any(character =>
				!(character is >= '0' and <= '9') && !(character is >= 'a' and <= 'f')))
				throw new ArgumentException("Runtime contract digest is not lowercase SHA-256.", nameof(value));

			return value;
		}

		public bool Equals(RuntimeContractIdentity other) =>
			StringComparer.Ordinal.Equals(ResourceCapability, other.ResourceCapability) &&
			StringComparer.Ordinal.Equals(Digest, other.Digest);

		public override bool Equals(object obj) => obj is RuntimeContractIdentity other && Equals(other);
		public override int GetHashCode() => HashCode.Combine(ResourceCapability, Digest);
		public static bool operator ==(RuntimeContractIdentity left, RuntimeContractIdentity right) => left.Equals(right);
		public static bool operator !=(RuntimeContractIdentity left, RuntimeContractIdentity right) => !left.Equals(right);
	}

	public readonly struct NetworkCompatibilityContext
	{
		public string EngineCompatibility { get; }
		public string Mod { get; }
		public string ModCompatibility { get; }
		public RuntimeContractIdentity RuntimeContract { get; }

		public NetworkCompatibilityContext(
			string engineCompatibility,
			string mod,
			string modCompatibility,
			RuntimeContractIdentity runtimeContract)
		{
			EngineCompatibility = engineCompatibility;
			Mod = mod;
			ModCompatibility = modCompatibility;
			RuntimeContract = runtimeContract;
		}
	}

	public static class NetworkCompatibility
	{
		public static NetworkCompatibilityReason ValidateEnvelope(
			HandshakeRequest remote,
			NetworkCompatibilityContext local) => remote == null
			? NetworkCompatibilityReason.ProtocolMismatch
			: ValidateEnvelope(remote.HandshakeSchema, remote.OrdersProtocol,
				remote.EngineCompatibility, remote.Mod, remote.RuntimeProfile, local);

		public static bool TryValidateResponsePayload(
			string data,
			string name,
			NetworkCompatibilityContext local,
			out HandshakeResponse response,
			out NetworkCompatibilityReason reason)
		{
			if (!HandshakeResponse.TryDeserialize(data, name, out response))
			{
				reason = NetworkCompatibilityReason.ProtocolMismatch;
				return false;
			}

			reason = Validate(response, local);
			return reason == NetworkCompatibilityReason.Compatible;
		}

		public static bool TryGetDebugSummary(
			string runtimeProfile,
			string runtimeContract,
			out string summary)
		{
			summary = null;
			if (!RuntimeProfileIdentity.TryParse(runtimeProfile, out var profile) ||
				!RuntimeContractIdentity.TryParse(runtimeContract, out var contract))
				return false;

			summary = $"platform={profile.Platform}; architecture={profile.Architecture}; " +
				$"resources={contract.ResourceCapability}; contract={contract.Digest[..12]}";
			return true;
		}

		public static NetworkCompatibilityReason Validate(
			HandshakeRequest remote,
			NetworkCompatibilityContext local) => remote == null
			? NetworkCompatibilityReason.ProtocolMismatch
			: Validate(remote.HandshakeSchema, remote.OrdersProtocol,
				remote.EngineCompatibility, remote.Mod, remote.Version,
				remote.RuntimeProfile, remote.RuntimeContract, local);

		public static NetworkCompatibilityReason Validate(
			HandshakeResponse remote,
			NetworkCompatibilityContext local) => remote == null
			? NetworkCompatibilityReason.ProtocolMismatch
			: Validate(remote.HandshakeSchema, remote.OrdersProtocol,
				remote.EngineCompatibility, remote.Mod, remote.Version,
				remote.RuntimeProfile, remote.RuntimeContract, local);

		static NetworkCompatibilityReason Validate(
			int handshakeSchema,
			int ordersProtocol,
			string engineCompatibility,
			string mod,
			string modCompatibility,
			string runtimeProfile,
			string runtimeContract,
			NetworkCompatibilityContext local)
		{
			var envelope = ValidateEnvelope(
				handshakeSchema, ordersProtocol, engineCompatibility, mod, runtimeProfile, local);
			if (envelope != NetworkCompatibilityReason.Compatible)
				return envelope;

			if (!StringComparer.Ordinal.Equals(mod, local.Mod) ||
				!StringComparer.Ordinal.Equals(modCompatibility, local.ModCompatibility))
				return NetworkCompatibilityReason.ModMismatch;

			if (string.IsNullOrEmpty(runtimeContract))
				return NetworkCompatibilityReason.RuntimeContractMissing;
			if (!RuntimeContractIdentity.TryParse(runtimeContract, out var contract) ||
				contract != local.RuntimeContract)
				return NetworkCompatibilityReason.RuntimeContractMismatch;

			return NetworkCompatibilityReason.Compatible;
		}

		static NetworkCompatibilityReason ValidateEnvelope(
			int handshakeSchema,
			int ordersProtocol,
			string engineCompatibility,
			string mod,
			string runtimeProfile,
			NetworkCompatibilityContext local)
		{
			if (handshakeSchema != ProtocolVersion.HandshakeSchema ||
				ordersProtocol != ProtocolVersion.Orders)
				return NetworkCompatibilityReason.ProtocolMismatch;

			if (!StringComparer.Ordinal.Equals(engineCompatibility, local.EngineCompatibility))
				return NetworkCompatibilityReason.EngineMismatch;

			if (!RuntimeProfileIdentity.TryParse(runtimeProfile, out var profile) ||
				!StringComparer.Ordinal.Equals(profile.EngineVersion, engineCompatibility) ||
				!StringComparer.Ordinal.Equals(profile.Mod, mod))
				return NetworkCompatibilityReason.ProtocolMismatch;

			return NetworkCompatibilityReason.Compatible;
		}

		public static NetworkCompatibilityReason ValidateMap(string expectedUid, string actualUid) =>
			!string.IsNullOrEmpty(expectedUid) &&
			StringComparer.Ordinal.Equals(expectedUid, actualUid)
				? NetworkCompatibilityReason.Compatible
				: NetworkCompatibilityReason.MapMismatch;
	}
}
