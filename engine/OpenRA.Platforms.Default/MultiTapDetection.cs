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
using OpenRA.Primitives;

namespace OpenRA.Platforms.Default
{
	static class MultiTapDetection
	{
		const int TouchDistance = 28;
		static readonly TimeSpan TouchInterval = TimeSpan.FromMilliseconds(400);

		static readonly Cache<(Keycode Key, Modifiers Mods), TapHistory> KeyHistoryCache =
			new(_ => new TapHistory(DateTime.Now - TimeSpan.FromSeconds(1)));
		static readonly Cache<byte, TapHistory> ClickHistoryCache =
			new(_ => new TapHistory(DateTime.Now - TimeSpan.FromSeconds(1)));
		static readonly Cache<byte, TapHistory> TouchHistoryCache =
			new(_ => new TapHistory(DateTime.Now - TimeSpan.FromSeconds(1)));

		public static int DetectFromMouse(byte button, int2 xy)
		{
			return ClickHistoryCache[button].GetTapCount(xy);
		}

		public static int InfoFromMouse(byte button)
		{
			return ClickHistoryCache[button].LastTapCount();
		}

		public static void CancelFromMouse(byte button)
		{
			ClickHistoryCache[button].Reset(DateTime.Now - TimeSpan.FromSeconds(1));
		}

		public static int DetectFromTouch(byte button, int2 xy)
		{
			return TouchHistoryCache[button].GetTapCount(xy, TouchInterval, TouchDistance);
		}

		public static int InfoFromTouch(byte button)
		{
			return TouchHistoryCache[button].LastTapCount(TouchInterval, TouchDistance);
		}

		public static void CancelFromTouch(byte button)
		{
			TouchHistoryCache[button].Reset(DateTime.Now - TimeSpan.FromSeconds(1));
		}

		public static int DetectFromKeyboard(Keycode key, Modifiers mods)
		{
			return KeyHistoryCache[(key, mods)].GetTapCount(int2.Zero);
		}

		public static int InfoFromKeyboard(Keycode key, Modifiers mods)
		{
			return KeyHistoryCache[(key, mods)].LastTapCount();
		}
	}

	sealed class TapHistory
	{
		public (DateTime Time, int2 Location) FirstRelease, SecondRelease, ThirdRelease;

		public TapHistory(DateTime now)
		{
			Reset(now);
		}

		public void Reset(DateTime now)
		{
			FirstRelease = SecondRelease = ThirdRelease = (now, int2.Zero);
		}

		static bool CloseEnough(
			(DateTime Time, int2 Location) a, (DateTime Time, int2 Location) b,
			TimeSpan interval, int distance)
		{
			return a.Time - b.Time < interval && (a.Location - b.Location).Length < distance;
		}

		public int GetTapCount(int2 xy) =>
			GetTapCount(xy, TimeSpan.FromMilliseconds(250), 4);

		public int GetTapCount(int2 xy, TimeSpan interval, int distance)
		{
			FirstRelease = SecondRelease;
			SecondRelease = ThirdRelease;
			ThirdRelease = (DateTime.Now, xy);

			if (!CloseEnough(ThirdRelease, SecondRelease, interval, distance))
				return 1;
			if (!CloseEnough(SecondRelease, FirstRelease, interval, distance))
				return 2;

			return 3;
		}

		public int LastTapCount() =>
			LastTapCount(TimeSpan.FromMilliseconds(250), 4);

		public int LastTapCount(TimeSpan interval, int distance)
		{
			if (!CloseEnough(ThirdRelease, SecondRelease, interval, distance))
				return 1;
			if (!CloseEnough(SecondRelease, FirstRelease, interval, distance))
				return 2;

			return 3;
		}
	}
}
