using System;
using System.IO;
using NUnit.Framework;
using OpenRA.Network;

namespace OpenRA.Test
{
	[TestFixture]
	public class RoomMapTransferBufferTest
	{
		[Test]
		public void RejectsOversizeOutOfOrderAndHashMismatch()
		{
			Assert.Throws<InvalidDataException>(() => new RoomMapTransferBuffer(new string('a', 40), new string('0', 64), RoomMapArchive.MaxBytes + 1));
			var transfer = new RoomMapTransferBuffer(new string('a', 40), RoomMapArchive.Hash(new byte[] { 1, 2 }), 2);
			Assert.Throws<InvalidDataException>(() => transfer.Append(1, new byte[] { 1 }));
			transfer.Append(0, new byte[] { 1 });
			Assert.That(transfer.Complete, Is.False);
			transfer.Append(1, new byte[] { 2 });
			Assert.That(transfer.Finish(), Is.EqualTo(new byte[] { 1, 2 }));
			var corrupt = new RoomMapTransferBuffer(new string('a', 40), new string('0', 64), 1);
			corrupt.Append(0, new byte[] { 1 });
			Assert.Throws<InvalidDataException>(() => corrupt.Finish());
		}
	}
}
