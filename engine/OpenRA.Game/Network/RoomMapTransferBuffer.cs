using System;
using System.IO;
using System.Linq;

namespace OpenRA.Network
{
	public sealed class RoomMapTransferBuffer
	{
		readonly byte[] bytes;
		public string Uid { get; }
		public string Sha256 { get; }
		public int Offset { get; private set; }
		public int Length => bytes.Length;
		public bool Complete => Offset == Length;

		public RoomMapTransferBuffer(string uid, string sha256, int length)
		{
			if (!RoomMapArchive.ValidUid(uid) || sha256?.Length != 64 ||
				!sha256.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f') || length <= 0 || length > RoomMapArchive.MaxBytes)
				throw new InvalidDataException("Invalid map transfer offer.");
			Uid = uid;
			Sha256 = sha256;
			bytes = new byte[length];
		}

		public void Append(int offset, byte[] chunk)
		{
			if (offset != Offset || chunk == null || chunk.Length <= 0 || chunk.Length > RoomMapArchive.ChunkBytes || chunk.Length > Length - Offset)
				throw new InvalidDataException("Invalid map transfer chunk.");
			Buffer.BlockCopy(chunk, 0, bytes, Offset, chunk.Length);
			Offset += chunk.Length;
		}

		public byte[] Finish()
		{
			if (!Complete || RoomMapArchive.Hash(bytes) != Sha256)
				throw new InvalidDataException("Incomplete or corrupt map transfer.");
			return bytes;
		}
	}
}
