using System.Net;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Network;

namespace OpenRA.Test
{
	[TestFixture]
	public class ConnectionTargetPolicyTest
	{
		[TestCase("127.0.0.1")]
		[TestCase("127.23.45.67")]
		[TestCase("::1")]
		[TestCase("localhost")]
		[TestCase("LOCALHOST")]
		public void LoopbackTargetsSkipTheConnectingPanel(string host)
		{
			var target = new ConnectionTarget(host, 1234);

			Assert.That(target.IsLoopback, Is.True);
			Assert.That(ConnectionLogic.ShouldShowConnectingPanel(target), Is.False);
		}

		[TestCase("192.168.1.20")]
		[TestCase("10.0.0.5")]
		[TestCase("play.example.com")]
		public void RemoteTargetsKeepTheConnectingPanel(string host)
		{
			var target = new ConnectionTarget(host, 1234);

			Assert.That(target.IsLoopback, Is.False);
			Assert.That(ConnectionLogic.ShouldShowConnectingPanel(target), Is.True);
		}

		[Test]
		public void MixedTargetsAreNotTreatedAsLoopbackOnly()
		{
			var target = new ConnectionTarget(new[]
			{
				new DnsEndPoint("localhost", 1234),
				new DnsEndPoint("192.168.1.20", 1234)
			});

			Assert.That(target.IsLoopback, Is.False);
			Assert.That(ConnectionLogic.ShouldShowConnectingPanel(target), Is.True);
		}
	}
}
