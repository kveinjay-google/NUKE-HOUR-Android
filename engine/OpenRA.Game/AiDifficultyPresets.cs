using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenRA
{
	// Local library only. Lobby clients carry independent encoded snapshots.
	public static class AiDifficultyPresets
	{
		public const int MaximumCount = 32;

		public static List<AiDifficultyProfile> Load(IEnumerable<string> encoded)
		{
			var result = new List<AiDifficultyProfile>();
			if (encoded == null)
				return result;
			foreach (var item in encoded.Take(MaximumCount))
				if (AiDifficultyCatalog.TryDecode(item, out var profile) &&
					profile.Id.StartsWith("custom-", StringComparison.Ordinal) &&
					!result.Any(p => p.Id == profile.Id))
					result.Add(profile);
			return result;
		}

		public static bool Save(List<AiDifficultyProfile> profiles, AiDifficultyProfile draft)
		{
			if (draft == null || draft.Id == null || !draft.Id.StartsWith("custom-", StringComparison.Ordinal) ||
				string.IsNullOrWhiteSpace(draft.Name))
				return false;
			var clean = AiDifficultyCatalog.Normalize(draft);
			if (clean.Id != draft.Id || !clean.Id.StartsWith("custom-", StringComparison.Ordinal))
				return false;
			if (profiles.Any(p => p.Id != clean.Id && string.Equals(p.Name, clean.Name, StringComparison.OrdinalIgnoreCase)))
				return false;
			var index = profiles.FindIndex(p => p.Id == clean.Id);
			if (index >= 0)
				profiles[index] = clean;
			else if (profiles.Count < MaximumCount)
				profiles.Add(clean);
			else
				return false;
			return true;
		}

		public static string[] Encode(IEnumerable<AiDifficultyProfile> profiles) =>
			profiles.Take(MaximumCount).Select(AiDifficultyCatalog.Encode).ToArray();

		public static AiDifficultyProfile Find(string id, IEnumerable<AiDifficultyProfile> profiles) =>
			profiles.FirstOrDefault(p => p.Id == id) is AiDifficultyProfile custom ?
				AiDifficultyCatalog.Normalize(custom) : AiDifficultyCatalog.GetOfficial(id);
	}
}
