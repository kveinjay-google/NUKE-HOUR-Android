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
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class LogSafetyTest
	{
		[Test]
		public void WritingBeforeChannelRegistrationIsIgnored()
		{
			var channel = "not-registered-" + Guid.NewGuid().ToString("N");
			Assert.That(Log.TryWrite(channel, "startup diagnostic"), Is.False);
		}
	}
}
