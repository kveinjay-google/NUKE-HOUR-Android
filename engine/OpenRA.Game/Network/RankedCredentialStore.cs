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
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace OpenRA.Network
{
	public sealed class RankedStoredCredential
	{
		public string AccountId { get; init; }
		public string Username { get; init; }
		public string RefreshToken { get; init; }
		public DateTime RefreshExpiresUtc { get; init; }

		public override string ToString() => $"RankedStoredCredential({Username}, expires {RefreshExpiresUtc:O})";
	}

	public interface IRankedCredentialStore
	{
		bool IsAvailable { get; }
		RankedStoredCredential Load();
		void Save(RankedStoredCredential credential);
		void Delete();
	}

	public sealed class UnavailableRankedCredentialStore : IRankedCredentialStore
	{
		public bool IsAvailable => false;
		public RankedStoredCredential Load() => null;
		public void Save(RankedStoredCredential credential) =>
			throw new PlatformNotSupportedException("A secure ranked credential store is not available.");
		public void Delete() { }
	}

	public static class RankedCredentialStoreFactory
	{
		static Func<IRankedCredentialStore> platformFactory;

		public static void RegisterPlatformFactory(Func<IRankedCredentialStore> factory)
		{
			platformFactory = factory ?? throw new ArgumentNullException(nameof(factory));
		}

		public static IRankedCredentialStore Create()
		{
			if (platformFactory != null)
				return platformFactory();
			if (Platform.CurrentPlatform == PlatformType.OSX)
				return new AppleKeychainRankedCredentialStore();
			return new UnavailableRankedCredentialStore();
		}
	}

	public sealed class AppleKeychainRankedCredentialStore : IRankedCredentialStore
	{
		const string SecurityLibrary = "/System/Library/Frameworks/Security.framework/Security";
		const string CoreFoundationLibrary = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
		const uint Utf8Encoding = 0x08000100;
		const int Success = 0;
		const int ItemNotFound = -25300;
		const string Service = "com.nukehour.ranked";
		const string Account = "session-v1";
		static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			PropertyNameCaseInsensitive = true,
		};

		readonly IntPtr kSecClass;
		readonly IntPtr kSecClassGenericPassword;
		readonly IntPtr kSecAttrService;
		readonly IntPtr kSecAttrAccount;
		readonly IntPtr kSecAttrAccessible;
		readonly IntPtr kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly;
		readonly IntPtr kSecValueData;
		readonly IntPtr kSecReturnData;
		readonly IntPtr kSecMatchLimit;
		readonly IntPtr kSecMatchLimitOne;
		readonly IntPtr kCFBooleanTrue;

		public bool IsAvailable { get; }

		public AppleKeychainRankedCredentialStore()
		{
			if (Platform.CurrentPlatform != PlatformType.OSX)
				return;

			try
			{
				var security = NativeLibrary.Load(SecurityLibrary);
				var coreFoundation = NativeLibrary.Load(CoreFoundationLibrary);
				kSecClass = Constant(security, "kSecClass");
				kSecClassGenericPassword = Constant(security, "kSecClassGenericPassword");
				kSecAttrService = Constant(security, "kSecAttrService");
				kSecAttrAccount = Constant(security, "kSecAttrAccount");
				kSecAttrAccessible = Constant(security, "kSecAttrAccessible");
				kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly =
					Constant(security, "kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly");
				kSecValueData = Constant(security, "kSecValueData");
				kSecReturnData = Constant(security, "kSecReturnData");
				kSecMatchLimit = Constant(security, "kSecMatchLimit");
				kSecMatchLimitOne = Constant(security, "kSecMatchLimitOne");
				kCFBooleanTrue = Constant(coreFoundation, "kCFBooleanTrue");
				IsAvailable = true;
			}
			catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
			{
				Log.Write("debug", "Apple Keychain is unavailable for ranked credentials.");
			}
		}

		static IntPtr Constant(IntPtr library, string name) =>
			Marshal.ReadIntPtr(NativeLibrary.GetExport(library, name));

		static IntPtr String(string value) =>
			CFStringCreateWithCString(IntPtr.Zero, value, Utf8Encoding);

		IntPtr Query(bool returnData, List<IntPtr> owned)
		{
			var dictionary = CFDictionaryCreateMutable(IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero);
			if (dictionary == IntPtr.Zero)
				throw new InvalidOperationException("Could not create an Apple Keychain query.");

			var service = String(Service);
			var account = String(Account);
			owned.Add(service);
			owned.Add(account);
			CFDictionarySetValue(dictionary, kSecClass, kSecClassGenericPassword);
			CFDictionarySetValue(dictionary, kSecAttrService, service);
			CFDictionarySetValue(dictionary, kSecAttrAccount, account);
			if (returnData)
			{
				CFDictionarySetValue(dictionary, kSecReturnData, kCFBooleanTrue);
				CFDictionarySetValue(dictionary, kSecMatchLimit, kSecMatchLimitOne);
			}

			return dictionary;
		}

		static void Release(IEnumerable<IntPtr> values)
		{
			foreach (var value in values)
				if (value != IntPtr.Zero)
					CFRelease(value);
		}

		void RequireAvailable()
		{
			if (!IsAvailable)
				throw new PlatformNotSupportedException("Apple Keychain is unavailable.");
		}

		public RankedStoredCredential Load()
		{
			RequireAvailable();
			var owned = new List<IntPtr>();
			var query = Query(true, owned);
			try
			{
				var status = SecItemCopyMatching(query, out var result);
				if (status == ItemNotFound)
					return null;
				if (status != Success || result == IntPtr.Zero)
					throw new InvalidOperationException($"Apple Keychain read failed with status {status}.");
				try
				{
					var length = checked((int)CFDataGetLength(result));
					if (length < 1 || length > 4096)
						throw new InvalidOperationException("Stored ranked credential had an invalid size.");
					var bytes = new byte[length];
					Marshal.Copy(CFDataGetBytePtr(result), bytes, 0, length);
					var credential = JsonSerializer.Deserialize<RankedStoredCredential>(bytes, JsonOptions);
					Validate(credential);
					return credential;
				}
				finally
				{
					CFRelease(result);
				}
			}
			finally
			{
				CFRelease(query);
				Release(owned);
			}
		}

		public void Save(RankedStoredCredential credential)
		{
			RequireAvailable();
			Validate(credential);
			Delete();
			var bytes = JsonSerializer.SerializeToUtf8Bytes(credential, JsonOptions);
			if (bytes.Length > 4096)
				throw new InvalidOperationException("Ranked credential exceeded the Keychain size policy.");

			var owned = new List<IntPtr>();
			var query = Query(false, owned);
			try
			{
				var data = CFDataCreate(IntPtr.Zero, bytes, bytes.Length);
				owned.Add(data);
				CFDictionarySetValue(query, kSecValueData, data);
				CFDictionarySetValue(query, kSecAttrAccessible, kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly);
				var status = SecItemAdd(query, IntPtr.Zero);
				if (status != Success)
					throw new InvalidOperationException($"Apple Keychain write failed with status {status}.");
			}
			finally
			{
				CFRelease(query);
				Release(owned);
			}
		}

		public void Delete()
		{
			RequireAvailable();
			var owned = new List<IntPtr>();
			var query = Query(false, owned);
			try
			{
				var status = SecItemDelete(query);
				if (status != Success && status != ItemNotFound)
					throw new InvalidOperationException($"Apple Keychain delete failed with status {status}.");
			}
			finally
			{
				CFRelease(query);
				Release(owned);
			}
		}

		static void Validate(RankedStoredCredential credential)
		{
			if (credential == null || !Guid.TryParse(credential.AccountId, out _) ||
				string.IsNullOrWhiteSpace(credential.Username) || credential.Username.Length > 96 ||
				credential.RefreshToken?.Length < 32 || credential.RefreshToken.Length > 256 ||
				credential.RefreshExpiresUtc.Kind == DateTimeKind.Unspecified)
				throw new InvalidOperationException("Invalid ranked credential.");
		}

		[DllImport(CoreFoundationLibrary)]
		static extern IntPtr CFStringCreateWithCString(IntPtr allocator, string value, uint encoding);

		[DllImport(CoreFoundationLibrary)]
		static extern IntPtr CFDataCreate(IntPtr allocator, byte[] bytes, nint length);

		[DllImport(CoreFoundationLibrary)]
		static extern nint CFDataGetLength(IntPtr data);

		[DllImport(CoreFoundationLibrary)]
		static extern IntPtr CFDataGetBytePtr(IntPtr data);

		[DllImport(CoreFoundationLibrary)]
		static extern IntPtr CFDictionaryCreateMutable(
			IntPtr allocator, nint capacity, IntPtr keyCallbacks, IntPtr valueCallbacks);

		[DllImport(CoreFoundationLibrary)]
		static extern void CFDictionarySetValue(IntPtr dictionary, IntPtr key, IntPtr value);

		[DllImport(CoreFoundationLibrary)]
		static extern void CFRelease(IntPtr value);

		[DllImport(SecurityLibrary)]
		static extern int SecItemAdd(IntPtr attributes, IntPtr result);

		[DllImport(SecurityLibrary)]
		static extern int SecItemCopyMatching(IntPtr query, out IntPtr result);

		[DllImport(SecurityLibrary)]
		static extern int SecItemDelete(IntPtr query);
	}
}
