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
using System.Text.RegularExpressions;

namespace OpenRA.Server
{
	public static class RankedServerPolicy
	{
		public const string CredentialEnvironmentVariable = "NUKEHOUR_RANKED_SERVER_REGISTRATION_CREDENTIAL";
		public const string SpoolKeyEnvironmentVariable = "NUKEHOUR_RANKED_RESULT_SPOOL_KEY";
		static readonly Regex SafeIdentifier = new("^[A-Za-z0-9._-]{1,64}$", RegexOptions.CultureInvariant);
		static readonly Regex SafeRegion = new("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant);

		public static byte[] ParseSpoolKey(string value)
		{
			try
			{
				var key = Convert.FromBase64String(value ?? "");
				if (key.Length == 32)
					return key;
			}
			catch (FormatException) { }
			throw new ArgumentException($"{SpoolKeyEnvironmentVariable} must be a base64-encoded 32-byte key.");
		}

		public static void Validate(ServerSettings settings, string registrationCredential, string spoolKey = null)
		{
			ArgumentNullException.ThrowIfNull(settings);
			if (!settings.RankedServerEnabled)
				return;

			Network.RankedLobbyClient.ParseBaseUri(settings.RankedLobbyUrl);
			if (!SafeIdentifier.IsMatch(settings.RankedServerId ?? ""))
				throw new ArgumentException("RankedServerId must use 1-64 safe identifier characters.");
			if (string.IsNullOrEmpty(registrationCredential) || registrationCredential.Length < 32)
				throw new ArgumentException($"{CredentialEnvironmentVariable} must contain at least 32 characters.");
			ParseSpoolKey(spoolKey);
			if (!IPAddress.TryParse(settings.RankedPublicEndpoint, out _))
				throw new ArgumentException("RankedPublicEndpoint must be a numeric IPv4 or IPv6 address.");
			var port = settings.RankedPublicPort == 0 ? settings.ListenPort : settings.RankedPublicPort;
			if (port < 1 || port > IPEndPoint.MaxPort)
				throw new ArgumentException("RankedPublicPort must be a valid TCP port.");
			if (!SafeRegion.IsMatch(settings.RankedRegion ?? "") || settings.RankedRegion.Length > 32)
				throw new ArgumentException("RankedRegion must be a lower-case region identifier.");
			if (settings.RankedPollSeconds < 1 || settings.RankedPollSeconds > 30)
				throw new ArgumentException("RankedPollSeconds must be between 1 and 30.");
			if (settings.MaxPlayers != 2 || settings.EnableSingleplayer)
				throw new ArgumentException("Ranked servers require exactly two human players.");
			if (settings.AdvertiseOnline || !string.IsNullOrWhiteSpace(settings.OnlineLobbyUrl))
				throw new ArgumentException("Ranked servers cannot publish as ordinary Online rooms.");
			if (!string.IsNullOrEmpty(settings.Password))
				throw new ArgumentException("Ranked servers cannot use an ordinary room password.");
			if (string.IsNullOrWhiteSpace(settings.RankedResultSpoolFile) ||
				Path.IsPathRooted(settings.RankedResultSpoolFile) ||
				settings.RankedResultSpoolFile.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains(".."))
				throw new ArgumentException("RankedResultSpoolFile must stay inside the support directory.");
		}

		public static bool IsAllowedLobbyCommand(string command) => command is
			"state" or "map_status" or "faction" or "spawn" or "color";
	}
}
