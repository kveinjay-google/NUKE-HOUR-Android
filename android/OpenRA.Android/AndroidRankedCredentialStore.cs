using System;
using System.Text.Json;
using Android.Content;
using Android.Security.Keystore;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;
using OpenRA.Network;

namespace OpenRA.Android;

sealed class AndroidRankedCredentialStore : IRankedCredentialStore
{
    const string ProductionAlias = "com.nukehour.ranked.session-v1";
    readonly string alias;
    readonly string preferenceName;
    const string PreferenceKey = "encrypted-session-v1";
    static readonly object Sync = new();
    static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
    readonly ISharedPreferences preferences;
    public AndroidRankedCredentialStore() : this(null) { }
    AndroidRankedCredentialStore(string? isolatedTestName)
    {
        alias = isolatedTestName == null ? ProductionAlias : ProductionAlias + "." + isolatedTestName;
        preferenceName = isolatedTestName == null ? "ranked-secure" : "ranked-secure-" + isolatedTestName;
        preferences = global::Android.App.Application.Context.GetSharedPreferences(preferenceName, FileCreationMode.Private)!;
    }
    public bool IsAvailable => true;

    IKey GetKey()
    {
        using var store = KeyStore.GetInstance("AndroidKeyStore")!;
        store.Load(null);
        if (store.ContainsAlias(alias))
            return store.GetKey(alias, null)!;
        using var generator = KeyGenerator.GetInstance(KeyProperties.KeyAlgorithmAes, "AndroidKeyStore")!;
        using var specification = new KeyGenParameterSpec.Builder(alias, KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
            .SetBlockModes(KeyProperties.BlockModeGcm)
            .SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone)
            .SetRandomizedEncryptionRequired(true).Build();
        generator.Init(specification);
        return generator.GenerateKey()!;
    }

    public RankedStoredCredential? Load()
    {
        lock (Sync)
        {
            var encoded = preferences.GetString(PreferenceKey, null);
            if (encoded == null) return null;
            if (encoded.Length > 8192) throw new InvalidOperationException("Stored ranked credential exceeded its size limit.");
            var parts = encoded.Split(':');
            if (parts.Length != 2) throw new InvalidOperationException("Invalid encrypted ranked credential.");
            var iv = Convert.FromBase64String(parts[0]);
            if (iv.Length != 12) throw new InvalidOperationException("Invalid ranked credential nonce.");
            using var key = GetKey();
            using var cipher = Cipher.GetInstance("AES/GCM/NoPadding")!;
            using var parameters = new GCMParameterSpec(128, iv);
            cipher.Init(CipherMode.DecryptMode, key, parameters);
            var plaintext = cipher.DoFinal(Convert.FromBase64String(parts[1]))!;
            try
            {
                if (plaintext.Length is < 1 or > 4096) throw new InvalidOperationException("Invalid ranked credential size.");
                return JsonSerializer.Deserialize<RankedStoredCredential>(plaintext, JsonOptions) ?? throw new InvalidOperationException("Invalid ranked credential.");
            }
            finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(plaintext); }
        }
    }

    public void Save(RankedStoredCredential credential)
    {
        ArgumentNullException.ThrowIfNull(credential);
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(credential, JsonOptions);
        try
        {
            if (plaintext.Length > 4096) throw new InvalidOperationException("Ranked credential exceeded its size limit.");
            lock (Sync)
            {
                using var key = GetKey();
                using var cipher = Cipher.GetInstance("AES/GCM/NoPadding")!;
                cipher.Init(CipherMode.EncryptMode, key);
                var encrypted = cipher.DoFinal(plaintext)!;
                var encoded = Convert.ToBase64String(cipher.GetIV()!) + ":" + Convert.ToBase64String(encrypted);
                using var edit = preferences.Edit()!;
                if (!edit.PutString(PreferenceKey, encoded)!.Commit()) throw new InvalidOperationException("Could not save encrypted ranked credential.");
            }
        }
        finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(plaintext); }
    }

    // Explicit opt-in device diagnostic: only random temporary aliases/stores;
    // never reads the player's credential and never contacts a service.
    internal static void RunIsolatedSelfTest()
    {
        var testName = "selftest-" + Guid.NewGuid().ToString("N");
        var test = new AndroidRankedCredentialStore(testName);
        try
        {
            var credential = new RankedStoredCredential { AccountId = "offline-selftest", Username = "offline-selftest",
                RefreshToken = "offline-secret-" + Guid.NewGuid().ToString("N"), RefreshExpiresUtc = DateTime.UtcNow.AddMinutes(1) };
            test.Save(credential);
            var stored = test.preferences.GetString(PreferenceKey, null) ?? throw new InvalidOperationException("Missing encrypted self-test entry.");
            if (stored.Contains(credential.RefreshToken, StringComparison.Ordinal)) throw new InvalidOperationException("Self-test found plaintext storage.");
            var reopened = new AndroidRankedCredentialStore(testName).Load();
            if (reopened?.RefreshToken != credential.RefreshToken || reopened.AccountId != credential.AccountId)
                throw new InvalidOperationException("Keystore roundtrip mismatch.");
            test.Delete();
            if (test.Load() != null) throw new InvalidOperationException("Keystore deletion failed.");
        }
        finally
        {
            global::Android.App.Application.Context.DeleteSharedPreferences(test.preferenceName);
            using var keys = KeyStore.GetInstance("AndroidKeyStore")!;
            keys.Load(null);
            keys.DeleteEntry(test.alias);
        }
        global::Android.Util.Log.Info("OpenRA.Keystore", "SELFTEST PASS encrypted persistence/reopen/delete/cleanup; isolated data only");
    }

    public void Delete()
    {
        lock (Sync)
        {
            using var edit = preferences.Edit()!;
            if (!edit.Remove(PreferenceKey)!.Commit()) throw new InvalidOperationException("Could not remove ranked credential.");
        }
    }
}
