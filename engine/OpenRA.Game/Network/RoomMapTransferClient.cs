using System;
using System.IO;
using OpenRA.Widgets;

namespace OpenRA.Network
{
	public sealed class RoomMapTransferClient
	{
		readonly OrderManager manager;
		RoomMapTransferBuffer download;
		byte[] upload;
		string uploadUid;
		int uploadOffset;
		string requestedUid;
		string failedUid;
		string pending;
		long nextSend;
		long expires;
		public bool Supported { get; set; }

		public RoomMapTransferClient(OrderManager manager) { this.manager = manager; }
		void Queue(string message)
		{
			pending = message;
			nextSend = Environment.TickCount64 + 100;
		}

		public void Select(string uid)
		{
			if (!Supported || Game.ModData.MapCache[uid].Status != MapStatus.Available)
			{
				manager.IssueOrder(Order.Command("map " + uid));
				return;
			}
			if (manager.LocalClient?.IsAdmin != true || manager.LocalClient.IsReady || manager.GameStarted || upload != null)
				return;
			try
			{
				if (requestedUid != null)
				{
					Game.ModData.MapCache[requestedUid].Invalidate();
					requestedUid = null;
					download = null;
					pending = null;
				}
				upload = RoomMapArchive.Export(Game.ModData.MapCache[uid].Package);
				uploadUid = uid;
				uploadOffset = 0;
				expires = Environment.TickCount64 + 120000;
				Queue($"offer {uid} {RoomMapArchive.Hash(upload)} {upload.Length}");
			}
			catch (Exception)
			{
				upload = null;
				uploadUid = null;
				// Maps with custom scripts/assets still work when preinstalled by the administrator.
				manager.IssueOrder(Order.Command("map " + uid));
				TextNotificationsManager.AddSystemLine("notification-room-map-manual-install");
			}
		}

		void Fail()
		{
			if (requestedUid != null)
				Game.ModData.MapCache[requestedUid].RoomDownloadFailed();
			failedUid = requestedUid;
			requestedUid = null;
			upload = null;
			uploadUid = null;
			download = null;
			pending = null;
			TextNotificationsManager.AddSystemLine("notification-room-map-transfer-failed");
		}

		public void Cancel()
		{
			if (requestedUid != null && Game.ModData?.MapCache[requestedUid].Status == MapStatus.Downloading)
				Game.ModData.MapCache[requestedUid].Invalidate();
			requestedUid = null;
			uploadUid = null;
			upload = null;
			download = null;
			pending = null;
		}

		public bool Retry(string uid)
		{
			if (!Supported || manager.GameStarted || uid != manager.LobbyInfo.GlobalSettings.Map || upload != null)
				return false;
			failedUid = null;
			requestedUid = null;
			download = null;
			pending = null;
			return true;
		}

		public void Tick()
		{
			if (manager.GameStarted)
			{
				Cancel();
				return;
			}
			if (!Supported || manager.GameStarted || manager.LocalClient == null || Game.ModData?.Manifest.Id != "ra2")
				return;
			var uid = manager.LobbyInfo.GlobalSettings.Map;
			if (requestedUid != null && requestedUid != uid)
			{
				Game.ModData.MapCache[requestedUid].Invalidate();
				requestedUid = null;
				download = null;
				pending = null;
			}
			if ((requestedUid != null || upload != null) && Environment.TickCount64 > expires)
			{
				Fail();
				return;
			}
			if (upload == null && requestedUid == null && RoomMapArchive.ValidUid(uid) && uid != failedUid &&
				Game.ModData.MapCache[uid].Status != MapStatus.Available)
			{
				requestedUid = uid;
				expires = Environment.TickCount64 + 120000;
				Game.ModData.MapCache[uid].BeginDownloadAttempt();
				Queue("get " + uid + " 0");
			}
			if (pending != null && Environment.TickCount64 >= nextSend)
			{
				manager.IssueOrder(Order.FromTargetString("RoomMapTransfer", pending, true));
				pending = null;
			}
		}

		public void Receive(string message)
		{
			if (!Supported || manager.GameStarted || manager.LocalClient == null)
				return;
			try
			{
				if (message == null || message.Length > 18000)
					throw new InvalidDataException();
				var parts = message.Split(' ');
				if (parts.Length == 2 && parts[0] == "error")
				{
					if (parts[1] == requestedUid || parts[1] == uploadUid)
						Fail();
					return;
				}
				if (parts.Length == 2 && parts[0] == "done" && parts[1] == uploadUid) { upload = null; uploadUid = null; return; }
				if (parts.Length == 3 && parts[0] == "ack" && upload != null && parts[1] == uploadUid)
				{
					if (!int.TryParse(parts[2], out var offset) || offset != uploadOffset || offset >= upload.Length)
						throw new InvalidDataException();
					var count = Math.Min(RoomMapArchive.ChunkBytes, upload.Length - offset);
					Queue($"put {uploadUid} {offset} {Convert.ToBase64String(upload, offset, count)}");
					uploadOffset += count;
				}
				else if (parts.Length == 6 && parts[0] == "data" && parts[1] == requestedUid &&
					requestedUid == manager.LobbyInfo.GlobalSettings.Map)
				{
					if (!int.TryParse(parts[3], out var total) || !int.TryParse(parts[4], out var offset))
						throw new InvalidDataException();
					download ??= new RoomMapTransferBuffer(requestedUid, parts[2], total);
					if (download.Length != total || download.Sha256 != parts[2])
						throw new InvalidDataException();
					download.Append(offset, Convert.FromBase64String(parts[5]));
					Game.ModData.MapCache[requestedUid].RoomDownloadProgress(download.Offset, total);
					if (download.Complete)
					{
						RoomMapArchive.Install(Game.ModData, download.Finish(), download.Uid, download.Sha256);
						Log.Write("client", "[MAP] Room map verified and installed: " + requestedUid);
						download = null;
						requestedUid = null;
					}
					else
						Queue($"get {requestedUid} {download.Offset}");
				}
			}
			catch (Exception) { Fail(); }
		}
	}
}
