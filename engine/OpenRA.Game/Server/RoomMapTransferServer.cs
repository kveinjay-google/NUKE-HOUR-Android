using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenRA.Network;

namespace OpenRA.Server
{
	// Runs on the server event thread, after connection authentication, only in the lobby.
	sealed class RoomMapTransferServer
	{
		readonly Dictionary<int, long> lastRequest = new();
		RoomMapTransferBuffer upload;
		int owner = -1;
		long expires;
		string sourceMap;
		string cachedUid;
		byte[] cachedBytes;
		string cachedHash;
		int uploads;

		public void Drop(int player)
		{
			lastRequest.Remove(player);
			if (owner == player)
				ClearUpload();
		}

		void ClearUpload() { upload = null; owner = -1; }

		public void Tick(Server server)
		{
			if (upload != null && (Environment.TickCount64 > expires || server.State != ServerState.WaitingPlayers ||
				server.LobbyInfo.GlobalSettings.Map != sourceMap || server.LobbyInfo.ClientWithIndex(owner)?.IsAdmin != true))
				ClearUpload();
			if (cachedUid != server.LobbyInfo.GlobalSettings.Map || server.State != ServerState.WaitingPlayers)
			{
				cachedBytes = null;
				cachedUid = null;
			}
		}

		public void Receive(Server server, Connection conn, string message)
		{
			if (server.ModData.Manifest.Id != "ra2" || server.State != ServerState.WaitingPlayers)
				return;
			Tick(server);
			void Send(string data) => server.SendOrderTo(conn, "RoomMapTransfer", data);
			var requestUid = "invalid";
			try
			{
				if (message == null || message.Length > 18000)
					throw new InvalidDataException();
				var now = Environment.TickCount64;
				if (lastRequest.TryGetValue(conn.PlayerIndex, out var last) && now - last < 50)
					throw new InvalidDataException("Transfer rate exceeded.");
				lastRequest[conn.PlayerIndex] = now;
				var parts = message.Split(' ');
				if (parts.Length < 2 || !RoomMapArchive.ValidUid(parts[1]))
					throw new InvalidDataException();
				var uid = parts[1];
				requestUid = uid;
				if (parts[0] == "get" && parts.Length == 3)
				{
					if (uid != server.LobbyInfo.GlobalSettings.Map || !int.TryParse(parts[2], out var offset) || offset < 0)
						throw new InvalidDataException();
					if (cachedUid != uid)
					{
						cachedBytes = RoomMapArchive.Export(server.ModData.MapCache[uid].Package);
						cachedHash = RoomMapArchive.Hash(cachedBytes);
						cachedUid = uid;
					}
					if (offset >= cachedBytes.Length || offset % RoomMapArchive.ChunkBytes != 0)
						throw new InvalidDataException();
					var data = Convert.ToBase64String(cachedBytes, offset, Math.Min(RoomMapArchive.ChunkBytes, cachedBytes.Length - offset));
					Send($"data {uid} {cachedHash} {cachedBytes.Length} {offset} {data}");
					return;
				}

				var client = server.LobbyInfo.ClientWithIndex(conn.PlayerIndex);
				if (client?.IsAdmin != true || client.IsReady || (server.MapPool != null && !server.MapPool.Contains(uid)))
					throw new InvalidDataException("Map selection is not permitted.");
				if (parts[0] == "offer" && parts.Length == 4)
				{
					if (server.ModData.MapCache[uid].Status == MapStatus.Available)
					{
						ClearUpload();
						server.InterpretCommand("map " + uid, conn);
						Send("done " + uid);
						return;
					}
					if (upload != null || uploads >= 16 || !int.TryParse(parts[3], out var total))
						throw new InvalidDataException("Upload quota exceeded.");
					upload = new RoomMapTransferBuffer(uid, parts[2], total);
					owner = conn.PlayerIndex;
					sourceMap = server.LobbyInfo.GlobalSettings.Map;
					expires = now + 120000;
					Send("ack " + uid + " 0");
				}
				else if (parts[0] == "put" && parts.Length == 4 && upload != null && owner == conn.PlayerIndex && upload.Uid == uid)
				{
					if (!int.TryParse(parts[2], out var offset))
						throw new InvalidDataException();
					upload.Append(offset, Convert.FromBase64String(parts[3]));
					if (!upload.Complete)
						Send($"ack {uid} {upload.Offset}");
					else
					{
						RoomMapArchive.Install(server.ModData, upload.Finish(), uid, upload.Sha256);
						uploads++;
						ClearUpload();
						server.InterpretCommand("map " + uid, conn);
						Send("done " + uid);
					}
				}
				else
					throw new InvalidDataException();
			}
			catch (Exception e)
			{
				if (owner == conn.PlayerIndex)
					ClearUpload();
				Log.Write("server", "[MAP] Room map transfer rejected: " + e.GetType().Name);
				Send("error " + requestUid);
			}
		}
	}
}
