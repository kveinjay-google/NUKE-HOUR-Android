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
using OpenRA.FileFormats;
using OpenRA.Primitives;

namespace OpenRA.Graphics
{
	public sealed class Sheet : IDisposable
	{
		public static Func<Stream, Png> PlatformPngDecoder;
		// Returns an exclusively owned, premultiplied BGRA buffer, ready for upload.
		public static Func<Stream, (byte[] Pixels, Size Size)> PlatformBgraDecoder;
		bool dirty;
		bool releaseBufferOnCommit;
		ITexture texture;
		byte[] data;
		Func<ITexture> reloadTexture;
		bool disposed;
		long lastTextureUse;

		public bool HasResidentTexture => texture != null;

		// Only immutable, file-backed sheets may opt into eviction. The factory transfers ownership.
		public void SetTextureReload(Func<ITexture> reload)
		{
			if (disposed) throw new ObjectDisposedException(nameof(Sheet));
			if (data != null || dirty || texture == null)
				throw new InvalidOperationException("Only committed, unbuffered sheets may be reloadable.");
			reloadTexture = reload ?? throw new ArgumentNullException(nameof(reload));
			lastTextureUse = Game.RunTime;
		}

		// Call on the render thread, after flushing pending batches.
		public bool TryEvictTexture(long unusedBefore)
		{
			if (disposed || reloadTexture == null || texture == null || data != null || dirty || lastTextureUse > unusedBefore)
				return false;
			texture.Dispose();
			texture = null;
			return true;
		}

		public readonly Size Size;
		public readonly SheetType Type;

		public byte[] GetData()
		{
			CreateBuffer();
			return data;
		}

		public bool Buffered => data != null || texture == null;

		public Sheet(SheetType type, Size size)
		{
			Type = type;
			Size = size;
		}

		public Sheet(SheetType type, ITexture texture)
		{
			Type = type;
			this.texture = texture;
			Size = texture.Size;
		}

		public Sheet(SheetType type, Stream stream)
		{
			if (PlatformBgraDecoder != null && type == SheetType.BGRA)
			{
				var decoded = PlatformBgraDecoder(stream);
				if (decoded.Size.Width <= 0 || decoded.Size.Height <= 0 || decoded.Pixels == null ||
					(long)decoded.Size.Width * decoded.Size.Height > int.MaxValue / 4 ||
					decoded.Pixels.Length != 4L * decoded.Size.Width * decoded.Size.Height)
					throw new InvalidDataException("Invalid platform BGRA image buffer.");
				Size = decoded.Size;
				Type = type;
				data = decoded.Pixels;
				ReleaseBuffer();
				return;
			}

			var png = PlatformPngDecoder != null && type == SheetType.BGRA ? PlatformPngDecoder(stream) : new Png(stream);
			Size = new Size(png.Width, png.Height);
			data = new byte[4 * Size.Width * Size.Height];
			Util.FastCopyIntoSprite(new Sprite(this, new Rectangle(0, 0, png.Width, png.Height), TextureChannel.Red), png);

			Type = type;
			ReleaseBuffer();
		}

		public ITexture GetTexture()
		{
			if (disposed) throw new ObjectDisposedException(nameof(Sheet));
			if (reloadTexture != null)
			{
				lastTextureUse = Game.RunTime;
				if (texture == null)
				{
					var replacement = reloadTexture();
					if (replacement == null || replacement.Size != Size)
					{
						replacement?.Dispose();
						throw new InvalidDataException("Reloaded texture dimensions changed.");
					}
					texture = replacement;
				}
			}

			if (texture == null)
			{
				texture = Game.Renderer.Context.CreateTexture();
				dirty = true;
			}

			if (data != null && dirty)
			{
				texture.SetData(data, Size.Width, Size.Height);
				dirty = false;
				if (releaseBufferOnCommit)
					data = null;
			}

			return texture;
		}

		public Png AsPng()
		{
			if (Type == SheetType.Indexed)
				throw new InvalidOperationException("AsPng() cannot be called on Indexed sheets.");

			return new Png(GetData(), SpriteFrameType.Bgra32, Size.Width, Size.Height);
		}

		public Png AsPng(TextureChannel channel, IPalette pal)
		{
			if (Type != SheetType.Indexed)
				throw new InvalidOperationException("AsPng(TextureChannel, IPalette) can only be called on Indexed sheets.");

			var d = GetData();
			var plane = new byte[Size.Width * Size.Height];
			var dataStride = 4 * Size.Width;
			var channelOffset = (int)channel;

			for (var y = 0; y < Size.Height; y++)
				for (var x = 0; x < Size.Width; x++)
					plane[y * Size.Width + x] = d[y * dataStride + channelOffset + 4 * x];

			var palColors = new Color[Palette.Size];
			for (var i = 0; i < Palette.Size; i++)
				palColors[i] = pal.GetColor(i);

			return new Png(plane, SpriteFrameType.Indexed8, Size.Width, Size.Height, palColors);
		}

		public void CreateBuffer()
		{
			if (disposed) throw new ObjectDisposedException(nameof(Sheet));
			if (texture == null && reloadTexture != null)
				GetTexture();
			// A mutable buffer must never be discarded in favor of the original file.
			reloadTexture = null;
			if (data != null)
				return;
			if (texture == null)
				data = new byte[4 * Size.Width * Size.Height];
			else
				data = texture.GetData();
			releaseBufferOnCommit = false;
		}

		public void CommitBufferedData()
		{
			if (!Buffered)
				throw new InvalidOperationException(
					"This sheet is unbuffered. You cannot call CommitBufferedData on an unbuffered sheet. " +
					"If you need to completely replace the texture data you should set data into the texture directly. " +
					"If you need to make only small changes to the texture data consider creating a buffered sheet instead.");

			dirty = true;
		}

		public void ReleaseBuffer()
		{
			if (!Buffered)
				return;

			dirty = true;
			releaseBufferOnCommit = true;

			// Commit data from the buffer to the texture, allowing the buffer to be released and reclaimed by GC.
			if (Game.Renderer != null)
				GetTexture();
		}

		public bool ReleaseBufferAndTryTransferTo(Sheet destination)
		{
			if (Size != destination.Size)
				throw new ArgumentException("Destination sheet does not have the same size", nameof(destination));

			var buffer = data;
			ReleaseBuffer();

			// We aren't commiting data to the GPU, so let's not delete our data.
			if (Game.Renderer == null)
				return false;

			// Only transfer if the destination has no data that would be lost by overwriting.
			if (buffer != null && destination.data == null && destination.texture == null)
			{
				Array.Clear(buffer, 0, buffer.Length);
				destination.data = buffer;
				return true;
			}

			return false;
		}

		public void Dispose()
		{
			if (disposed) return;
			disposed = true;
			texture?.Dispose();
			texture = null;
			data = null;
			reloadTexture = null;
		}
	}
}
