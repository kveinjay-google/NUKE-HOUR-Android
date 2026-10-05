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

using System.IO;
using System.Text;
using NUnit.Framework;
using OpenRA.Server;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class DedicatedServerControlTest
	{
		[Test]
		public void ReadsOneNewlineDelimitedFrame()
		{
			using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{\"id\":\"one\"}\nignored"));
			Assert.That(DedicatedServerControlProtocol.ReadFrame(stream), Is.EqualTo("{\"id\":\"one\"}"));
		}

		[Test]
		public void RejectsFramesLargerThan64KiB()
		{
			using var stream = new MemoryStream(Encoding.UTF8.GetBytes(new string('x', 65537) + "\n"));
			Assert.Throws<InvalidDataException>(() => DedicatedServerControlProtocol.ReadFrame(stream));
		}

		[Test]
		public void RejectsIncompleteFrames()
		{
			using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
			Assert.Throws<EndOfStreamException>(() => DedicatedServerControlProtocol.ReadFrame(stream));
		}
	}
}
