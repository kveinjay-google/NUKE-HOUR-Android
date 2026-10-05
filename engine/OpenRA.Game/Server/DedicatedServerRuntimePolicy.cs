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
using System.Net;
using System.Text.Json;
using OpenRA.Network;

namespace OpenRA.Server
{
	public enum DedicatedServerOperationalState
	{
		Starting,
		WaitingForPlayers,
		InLobby,
		InGame,
		GameFinished,
		Stopping,
		Stopped,
	}

	public static class DedicatedServerRuntimePolicy
	{
		public static bool ShouldInitializeClientPresentation(bool dedicatedServer) => !dedicatedServer;

		public static TimeSpan ElapsedSince(long startTimestamp, long currentTimestamp, long timestampFrequency)
		{
			if (startTimestamp < 0)
				throw new ArgumentOutOfRangeException(nameof(startTimestamp));
			if (currentTimestamp < startTimestamp)
				throw new ArgumentOutOfRangeException(nameof(currentTimestamp));
			if (timestampFrequency <= 0)
				throw new ArgumentOutOfRangeException(nameof(timestampFrequency));

			return TimeSpan.FromSeconds((currentTimestamp - startTimestamp) / (double)timestampFrequency);
		}

		public static DedicatedServerOperationalState ResolveState(
			ServerState state,
			bool gameStarted,
			int validatedClients)
		{
			if (validatedClients < 0)
				throw new ArgumentOutOfRangeException(nameof(validatedClients));

			return state switch
			{
				ServerState.ShuttingDown => DedicatedServerOperationalState.Stopping,
				ServerState.GameStarted when gameStarted && validatedClients == 0 =>
					DedicatedServerOperationalState.GameFinished,
				ServerState.GameStarted => DedicatedServerOperationalState.InGame,
				ServerState.WaitingPlayers when validatedClients > 0 => DedicatedServerOperationalState.InLobby,
				_ => DedicatedServerOperationalState.WaitingForPlayers,
			};
		}

		public static IReadOnlyList<IPEndPoint> ResolveEndpoints(string listenAddress, int listenPort)
		{
			if (listenPort < 1 || listenPort > IPEndPoint.MaxPort)
				throw new ArgumentOutOfRangeException(nameof(listenPort));
			if (!IPAddress.TryParse(listenAddress, out var address))
				throw new ArgumentException("Listen address must be a numeric IPv4 or IPv6 address.", nameof(listenAddress));

			return new[] { new IPEndPoint(address, listenPort) };
		}

		public static bool HasPlayerCapacity(int validatedClients, int maxPlayers)
		{
			if (validatedClients < 0)
				throw new ArgumentOutOfRangeException(nameof(validatedClients));
			if (maxPlayers < 1 || maxPlayers > 64)
				throw new ArgumentOutOfRangeException(nameof(maxPlayers));

			return validatedClients < maxPlayers;
		}

		public static bool ShouldExit(
			ServerState state,
			bool gameStarted,
			int validatedClients,
			TimeSpan emptyDuration,
			int idleTimeoutSeconds)
		{
			if (emptyDuration < TimeSpan.Zero)
				throw new ArgumentOutOfRangeException(nameof(emptyDuration));
			if (idleTimeoutSeconds < 0)
				throw new ArgumentOutOfRangeException(nameof(idleTimeoutSeconds));

			if (state == ServerState.GameStarted && gameStarted && validatedClients == 0)
				return true;

			return state == ServerState.WaitingPlayers && validatedClients == 0 &&
				idleTimeoutSeconds > 0 && emptyDuration >= TimeSpan.FromSeconds(idleTimeoutSeconds);
		}
	}

	public static class DedicatedServerStatusDocument
	{
		public static string Serialize(
			DedicatedServerOperationalState state,
			string productVersion,
			string engineVersion,
			string mod,
			string platform,
			string listenAddress,
			int listenPort,
			string map,
			int validatedClients)
		{
			if (validatedClients < 0)
				throw new ArgumentOutOfRangeException(nameof(validatedClients));
			DedicatedServerRuntimePolicy.ResolveEndpoints(listenAddress, listenPort);

			var processAlive = state != DedicatedServerOperationalState.Stopped;
			var ready = state == DedicatedServerOperationalState.WaitingForPlayers ||
				state == DedicatedServerOperationalState.InLobby;
			var document = new Dictionary<string, object>
			{
				["product"] = "NUKE HOUR Dedicated Server",
				["productVersion"] = productVersion,
				["engineVersion"] = engineVersion,
				["mod"] = mod,
				["platform"] = platform,
				["transportHandshake"] = ProtocolVersion.Handshake,
				["handshakeSchema"] = ProtocolVersion.HandshakeSchema,
				["ordersProtocol"] = ProtocolVersion.Orders,
				["runtimeContractSchema"] = RuntimeContractIdentity.SchemaVersion,
				["listenAddress"] = listenAddress,
				["listenPort"] = listenPort,
				["map"] = map ?? string.Empty,
				["validatedClients"] = validatedClients,
				["processAlive"] = processAlive,
				["ready"] = ready,
				["state"] = ToWireState(state),
				["updatedUtc"] = DateTime.UtcNow.ToString("O"),
			};

			return JsonSerializer.Serialize(document);
		}

		static string ToWireState(DedicatedServerOperationalState state) => state switch
		{
			DedicatedServerOperationalState.WaitingForPlayers => "WAITING_FOR_PLAYERS",
			DedicatedServerOperationalState.InLobby => "IN_LOBBY",
			DedicatedServerOperationalState.InGame => "IN_GAME",
			DedicatedServerOperationalState.GameFinished => "GAME_FINISHED",
			DedicatedServerOperationalState.Stopping => "STOPPING",
			DedicatedServerOperationalState.Stopped => "STOPPED",
			_ => "STARTING",
		};
	}
}
