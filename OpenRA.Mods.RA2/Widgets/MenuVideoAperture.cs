using System;

namespace OpenRA.Mods.RA2.Widgets
{
	// Openings are measured in the active chrome regions, not the padded atlas.
	// Separate pipe openings are essential: one exterior polygon leaves frozen sky
	// inside the foreground framework while the rest of the sky moves.
	public static class MenuVideoAperture
	{
		static readonly (double X, double Y)[] Standard = Normalize(1672, 941,
			(756, 0), (1672, 0), (1672, 700), (859, 665), (859, 619),
			(839, 619), (839, 331), (804, 311), (804, 164), (756, 142));
		static readonly (double X, double Y)[] Tablet = Normalize(1536, 1152,
			(684, 0), (1536, 0), (1536, 838), (786, 796), (786, 737),
			(765, 737), (765, 457), (753, 438), (729, 422), (693, 412),
			(714, 400), (729, 383), (729, 283), (718, 272), (684, 261));
		static readonly (double X, double Y)[] Ultrawide = Normalize(1844, 853,
			(656, 0), (1844, 0), (1844, 641), (751, 579), (751, 539),
			(731, 539), (731, 292), (700, 266), (700, 130), (656, 113));

		static readonly (double X, double Y)[][] StandardGaps =
		{
			Normalize(1920, 1080, (714, 0), (741, 0), (742, 26), (713, 21)),
			Normalize(1920, 1080, (773, 0), (815, 0), (813, 26), (805, 34), (772, 30)),
			Normalize(1920, 1080, (839, 0), (851, 0), (852, 105), (824, 66),
				(814, 57), (831, 55), (843, 47), (839, 29)),
			Normalize(1920, 1080, (922, 297), (936, 355), (881, 344), (881, 331), (899, 328))
		};
		static readonly (double X, double Y)[][] TabletGaps =
		{
			// Join the pipe opening to the exterior sky above the lamp cap.
			// The previous 674..684 gap retained a vertical strip of static sky.
			Normalize(1536, 1152, (674, 0), (685, 0), (685, 61), (680, 59), (674, 65)),
			Normalize(1536, 1152, (562, 0), (617, 0), (617, 35), (579, 23), (576, 12), (562, 9)),
			Normalize(1536, 1152, (579, 48), (619, 59), (628, 75), (579, 62)),
			Normalize(1536, 1152, (564, 87), (615, 101), (596, 124), (587, 128),
				(581, 139), (576, 151), (566, 150), (560, 143)),
			Normalize(1536, 1152, (625, 104), (629, 105), (629, 160), (623, 164), (608, 160), (607, 131)),
			Normalize(1536, 1152, (643, 0), (674, 0), (674, 65), (668, 70), (666, 111),
				(670, 116), (670, 221), (645, 190), (632, 183), (650, 180), (660, 165),
				(663, 100), (658, 84), (647, 73), (638, 69), (640, 61), (650, 61), (650, 48), (643, 43)),
			Normalize(1536, 1152, (728, 382), (744, 432), (696, 423), (693, 410), (710, 402))
		};
		static readonly (double X, double Y)[][] UltrawideGaps =
		{
			Normalize(2048, 947, (597, 0), (618, 0), (619, 22), (597, 19)),
			Normalize(2048, 947, (646, 0), (683, 0), (682, 20), (676, 30), (647, 25)),
			Normalize(2048, 947, (706, 0), (714, 0), (714, 88), (691, 57), (681, 51),
				(696, 45), (706, 30)),
			Normalize(2048, 947, (776, 250), (793, 302), (737, 291), (738, 278), (757, 271))
		};

		static (double X, double Y)[] Normalize(int width, int height, params (double X, double Y)[] points)
		{
			for (var i = 0; i < points.Length; i++)
				points[i] = (points[i].X / width, points[i].Y / height);
			return points;
		}

		static (double X, double Y)[] Points(string profile) => profile switch
		{
			"tablet" => Tablet,
			"ultrawide" => Ultrawide,
			_ => Standard
		};

		static (double X, double Y)[][] Gaps(string profile) => profile switch
		{
			"tablet" => TabletGaps,
			"ultrawide" => UltrawideGaps,
			_ => StandardGaps
		};

		public static double Left(string profile)
		{
			var left = Points(profile)[0].X;
			foreach (var gap in Gaps(profile))
				foreach (var point in gap)
					left = Math.Min(left, point.X);
			return left;
		}
		public static double Bottom(string profile) => Points(profile)[2].Y;

		public static bool Contains(string profile, double x, double y)
		{
			if (Contains(Points(profile), x, y))
				return true;
			foreach (var gap in Gaps(profile))
				if (Contains(gap, x, y))
					return true;
			return false;
		}

		static bool Contains((double X, double Y)[] points, double x, double y)
		{
			var inside = false;
			for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
			{
				var a = points[i];
				var b = points[j];
				if ((a.Y > y) != (b.Y > y) && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X)
					inside = !inside;
			}

			return inside;
		}

		public static int FrameAt(double seconds, double fps, double duration)
		{
			if (!double.IsFinite(seconds) || !double.IsFinite(fps) || !double.IsFinite(duration) ||
				seconds < 0 || fps <= 0 || duration <= 0)
				return 0;
			return (int)Math.Floor(seconds % duration * fps);
		}
	}
}
