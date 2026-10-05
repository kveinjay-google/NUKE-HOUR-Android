using System.Reflection;
using NUnit.Framework;
using OpenRA.Network;

namespace OpenRA.Test
{
	[TestFixture]
	public class RoomMapTransferClientTest
	{
		[Test]
		public void DelayedErrorForPreviousMapDoesNotCancelNewUpload()
		{
			using var manager = new OrderManager(new EchoConnection());
			manager.LobbyInfo.Clients.Add(new Session.Client { Index = 1, IsAdmin = true });
			var client = manager.RoomMapTransfer;
			client.Supported = true;
			var flags = BindingFlags.Instance | BindingFlags.NonPublic;
			var uid = new string('b', 40);
			var bytes = new byte[] { 1, 2, 3 };
			typeof(RoomMapTransferClient).GetField("uploadUid", flags).SetValue(client, uid);
			typeof(RoomMapTransferClient).GetField("upload", flags).SetValue(client, bytes);
			typeof(RoomMapTransferClient).GetField("pending", flags).SetValue(client, "offer " + uid);
			client.Receive("error " + new string('a', 40));
			Assert.That(typeof(RoomMapTransferClient).GetField("upload", flags).GetValue(client), Is.SameAs(bytes));
			Assert.That(typeof(RoomMapTransferClient).GetField("pending", flags).GetValue(client), Is.EqualTo("offer " + uid));
			client.Cancel();
			Assert.That(typeof(RoomMapTransferClient).GetField("upload", flags).GetValue(client), Is.Null);
		}
	}
}
