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
using System.Runtime.InteropServices;
using OpenRA.Support;

namespace OpenRA.Platforms.Default
{
	sealed class VertexBuffer<T> : ThreadAffine, IDisposable, IVertexBuffer<T>
			where T : struct
	{
		static readonly int VertexSize = Marshal.SizeOf<T>();
		readonly IntPtr bufferCapacityBytes;
		uint buffer;
		bool disposed;

		public VertexBuffer(int size)
		{
			bufferCapacityBytes = new IntPtr(VertexSize * size);
			OpenGL.glGenBuffers(1, out buffer);
			OpenGL.CheckGLError();
			Bind();

			// Generates a buffer with uninitialized memory.
			OpenGL.glBufferData(OpenGL.GL_ARRAY_BUFFER,
					new IntPtr(VertexSize * size),
					IntPtr.Zero,
					OpenGL.GL_DYNAMIC_DRAW);
			OpenGL.CheckGLError();

			// We need to zero all the memory. Let's generate a smallish array and copy that over the whole buffer.
			var zeroedArrayElementSize = Math.Min(size, 2048);
			var ptr = GCHandle.Alloc(new T[zeroedArrayElementSize], GCHandleType.Pinned);
			try
			{
				for (var offset = 0; offset < size; offset += zeroedArrayElementSize)
				{
					var length = Math.Min(zeroedArrayElementSize, size - offset);
					OpenGL.glBufferSubData(OpenGL.GL_ARRAY_BUFFER,
						new IntPtr(VertexSize * offset),
						new IntPtr(VertexSize * length),
						ptr.AddrOfPinnedObject());
					OpenGL.CheckGLError();
				}
			}
			finally
			{
				ptr.Free();
			}
		}

		public void SetData(T[] data, int length)
		{
			SetData(data, 0, 0, length);
		}

		public void SetData(ref T[] data, int length)
		{
			#if IOS
			// This overload replaces a streamed batch, unlike partial terrain updates.
			// Orphan the old storage so a following UI batch never waits for the GPU
			// to finish reading vertices from the preceding draw call.
			Bind();
			using (PerformanceCategoryProfiler.Global.Measure("Vertex Buffer Orphan"))
				OpenGL.glBufferData(OpenGL.GL_ARRAY_BUFFER, bufferCapacityBytes,
					IntPtr.Zero, OpenGL.GL_DYNAMIC_DRAW);
			#endif

			SetData(data, 0, 0, length);
		}

		public void SetData(T[] data, int offset, int start, int length)
		{
			Bind();

			var ptr = GCHandle.Alloc(data, GCHandleType.Pinned);
			try
			{
				using (PerformanceCategoryProfiler.Global.Measure("Vertex Upload"))
					OpenGL.glBufferSubData(OpenGL.GL_ARRAY_BUFFER,
						new IntPtr(VertexSize * start),
						new IntPtr(VertexSize * length),
						ptr.AddrOfPinnedObject() + VertexSize * offset);
			}
			finally
			{
				ptr.Free();
			}

			OpenGL.CheckGLError();
		}

		public void Bind()
		{
			VerifyThreadAffinity();
			OpenGL.glBindBuffer(OpenGL.GL_ARRAY_BUFFER, buffer);
		}

		public void Dispose()
		{
			if (disposed)
				return;
			disposed = true;
			OpenGL.glDeleteBuffers(1, ref buffer);
			OpenGL.CheckGLError();
		}
	}
}
