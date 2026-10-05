using System;
using System.IO;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public class ReadAllBytesMemoryTest
	{
		sealed class SequentialStream : Stream
		{
			readonly int length;
			readonly int maxRead;
			readonly int failAt;
			int position;
			public bool Disposed;
			public SequentialStream(int length, int maxRead = int.MaxValue, int failAt = int.MaxValue)
			{
				this.length = length;
				this.maxRead = maxRead;
				this.failAt = failAt;
			}

			public override bool CanRead => !Disposed;
			public override bool CanSeek => false;
			public override bool CanWrite => false;
			public override long Length => throw new NotSupportedException();
			public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
			public override int Read(byte[] buffer, int offset, int count)
			{
				if (position >= failAt)
					throw new IOException("Synthetic decoder failure");
				count = Math.Min(count, Math.Min(maxRead, length - position));
				for (var i = 0; i < count; i++)
					buffer[offset + i] = (byte)((position + i) % 251);
				position += count;
				return count;
			}

			protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
			public override void Flush() => throw new NotSupportedException();
			public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
			public override void SetLength(long value) => throw new NotSupportedException();
			public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
		}

		[TestCase(0, 1)]
		[TestCase(1, 1)]
		[TestCase(65536, 257)]
		[TestCase(65537, 7)]
		[TestCase(200003, 65536)]
		public void SequentialReadPreservesBytesAcrossShortReadsAndDisposesInput(int length, int maxRead)
		{
			var stream = new SequentialStream(length, maxRead);
			var bytes = stream.ReadAllBytes();
			Assert.That(bytes.Length, Is.EqualTo(length));
			for (var i = 0; i < bytes.Length; i++)
				if (bytes[i] != i % 251)
					Assert.Fail($"Incorrect byte at {i}");
			Assert.That(stream.Disposed, Is.True);
		}

		[Test]
		public void SequentialReadDisposesInputOnDecoderFailure()
		{
			var stream = new SequentialStream(200000, 1024, 70000);
			Assert.Throws<IOException>(() => stream.ReadAllBytes());
			Assert.That(stream.Disposed, Is.True);
		}

		[Test]
		public void LargeSequentialReadAvoidsRepeatedGrowingCopiesAndPerReadIterators()
		{
			const int length = 4 * 1024 * 1024;
			var stream = new SequentialStream(length);
			var before = GC.GetAllocatedBytesForCurrentThread();
			var bytes = stream.ReadAllBytes();
			var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
			TestContext.WriteLine($"decodedBytes={bytes.Length}, allocatedBytes={allocated}");
			Assert.That(bytes.Length, Is.EqualTo(length));
			Assert.That(allocated, Is.LessThan(2L * length + 1024 * 1024));
		}

		[Test]
		public void SeekableReadStartsAtCurrentPosition()
		{
			var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
			stream.Position = 2;
			Assert.That(stream.ReadAllBytes(), Is.EqualTo(new byte[] { 3, 4 }));
			Assert.That(stream.CanRead, Is.False);
		}
	}
}
