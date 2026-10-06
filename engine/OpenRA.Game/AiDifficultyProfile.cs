using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace OpenRA
{
	// Editable DTO used by settings and synchronized lobby snapshots. Catalog entries are always copied.
	public sealed class AiDifficultyProfile
	{
		public string Id { get; set; } = "normal";
		public string Name { get; set; } = "普通";
		public string BaseDifficulty { get; set; } = "normal";
		public string Style { get; set; } = "balanced";
		public int AttackIntervalSeconds { get; set; } = 60;
		public int WaveSize { get; set; } = 10;
		public int Expansion { get; set; } = 2;
		public int IncomePercent { get; set; } = 100;
		public int ProductionSpeedPercent { get; set; } = 100;
	}

	public static class AiDifficultyCatalog
	{
		public const string CustomBotType = "difficulty-custom";
		public const int MaximumPayloadLength = 4096;
		static readonly string[] Ids = { "beginner", "easy", "normal", "hard", "brutal", "expert", "master", "nightmare" };
		static readonly string[] Names = { "新手", "简单", "普通", "困难", "冷酷", "专家", "大师", "噩梦" };
		static readonly int[] Intervals = { 120, 90, 60, 45, 30, 22, 15, 10 };
		static readonly int[] Waves = { 4, 6, 10, 14, 18, 22, 26, 30 };
		static readonly int[] Expansions = { 1, 1, 2, 2, 3, 3, 4, 4 };

		public static IReadOnlyList<AiDifficultyProfile> OfficialProfiles => Ids.Select(GetOfficial).ToArray();
		public static string BotType(string officialId) => "difficulty-" + GetOfficial(officialId).Id;
		public static bool IsOfficialBotType(string type) => type != null && Ids.Any(id => type == "difficulty-" + id);
		public static int TierIndex(string id)
		{
			var index = Array.IndexOf(Ids, id);
			return index < 0 ? 2 : index;
		}

		public static AiDifficultyProfile GetOfficial(string id)
		{
			var index = Array.IndexOf(Ids, id);
			if (index < 0)
				index = 2;

			return new AiDifficultyProfile
			{
				Id = Ids[index], Name = Names[index], BaseDifficulty = Ids[index],
				AttackIntervalSeconds = Intervals[index], WaveSize = Waves[index], Expansion = Expansions[index],
				IncomePercent = index == 7 ? 200 : 100, ProductionSpeedPercent = index == 7 ? 150 : 100
			};
		}

		public static AiDifficultyProfile CreateCustom(string baseId)
		{
			var profile = GetOfficial(baseId);
			profile.Id = "custom-" + Guid.NewGuid().ToString("N");
			profile.Name += " 自定义";
			return profile;
		}

		static bool IsCustomId(string id) => id != null && id.Length == 39 && id.StartsWith("custom-", StringComparison.Ordinal)
			&& Guid.TryParseExact(id.Substring(7), "N", out _);

		static string SanitizeName(string value, string fallback)
		{
			var result = new StringBuilder();
			foreach (var c in (value ?? "").Take(256))
			{
				var category = char.GetUnicodeCategory(c);
				if (char.IsControl(c) || char.IsSurrogate(c) || category == UnicodeCategory.Format ||
					category == UnicodeCategory.LineSeparator || category == UnicodeCategory.ParagraphSeparator ||
					c == '{' || c == '}' || c == '$')
					continue;
				if (result.Length == 24)
					break;
				result.Append(c);
			}

			var name = result.ToString().Trim();
			return name.Length == 0 ? fallback : name;
		}

		public static AiDifficultyProfile Normalize(AiDifficultyProfile profile)
		{
			if (profile == null)
				return GetOfficial("normal");
			if (Ids.Contains(profile.Id))
				return GetOfficial(profile.Id);
			if (!IsCustomId(profile.Id))
				return GetOfficial("normal");

			var basis = GetOfficial(profile.BaseDifficulty);
			return new AiDifficultyProfile
			{
				Id = profile.Id, Name = SanitizeName(profile.Name, basis.Name + " 自定义"), BaseDifficulty = basis.Id,
				Style = profile.Style == "defensive" || profile.Style == "rush" ? profile.Style : "balanced",
				AttackIntervalSeconds = Math.Clamp(profile.AttackIntervalSeconds, 2, 120),
				WaveSize = Math.Clamp(profile.WaveSize, 3, 60), Expansion = Math.Clamp(profile.Expansion, 1, 4),
				IncomePercent = Math.Clamp(profile.IncomePercent, 100, 300),
				ProductionSpeedPercent = Math.Clamp(profile.ProductionSpeedPercent, 100, 200)
			};
		}

		public static string Encode(AiDifficultyProfile profile) => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(Normalize(profile)));

		public static bool TryDecode(string encoded, out AiDifficultyProfile profile)
		{
			profile = GetOfficial("normal");
			if (string.IsNullOrEmpty(encoded) || encoded.Length > MaximumPayloadLength || encoded.Any(char.IsWhiteSpace))
				return false;
			try
			{
				var decoded = JsonSerializer.Deserialize<AiDifficultyProfile>(Convert.FromBase64String(encoded));
				if (decoded == null || (!Ids.Contains(decoded.Id) && !IsCustomId(decoded.Id)))
					return false;
				profile = Normalize(decoded);
				return true;
			}
			catch (Exception e) when (e is JsonException || e is FormatException || e is ArgumentException)
			{
				return false;
			}
		}

		public static AiDifficultyProfile Resolve(string botType, string encoded)
		{
			if (IsOfficialBotType(botType))
				return GetOfficial(botType.Substring("difficulty-".Length));
			if (botType == CustomBotType)
				return TryDecode(encoded, out var profile) ? profile : GetOfficial("normal");
			return null;
		}
	}

	// Pure arithmetic shared by the simulation and tests. Callers supply only the synchronized player snapshot.
	public static class AiDifficultyRuntime
	{
		public static int ProductionTime(int ticks, AiDifficultyProfile profile) => profile == null || ticks <= 0 ? ticks
			: (int)Math.Max(1L, ((long)ticks * 100 + Math.Clamp(profile.ProductionSpeedPercent, 100, 200) - 1) /
				Math.Clamp(profile.ProductionSpeedPercent, 100, 200));

		public static int MinedIncome(int value, AiDifficultyProfile profile) => profile == null || value <= 0 ? value
			: (int)Math.Min(int.MaxValue, (long)value * Math.Clamp(profile.IncomePercent, 100, 300) / 100);

		public static int AttackIntervalTicks(AiDifficultyProfile profile, int timestep, int legacy)
		{
			if (profile == null)
				return legacy;
			var style = profile.Style == "defensive" ? 125 : profile.Style == "rush" ? 75 : 100;
			return Math.Max(5, Math.Clamp(profile.AttackIntervalSeconds, 2, 120) * 10 * style / Math.Max(1, timestep));
		}

		public static int DecisionInterval(AiDifficultyProfile profile, int legacy) => profile == null ? Math.Max(5, legacy)
			: Math.Max(5, 60 - 7 * AiDifficultyCatalog.TierIndex(profile.BaseDifficulty));

		public static int DefenseReserve(AiDifficultyProfile profile) => profile?.Style == "defensive" ? Math.Max(1, profile.WaveSize / 3) : 0;

		public static int UnitShare(AiDifficultyProfile profile, int share, int cost)
		{
			if (profile == null)
				return share;
			var tier = AiDifficultyCatalog.TierIndex(profile.BaseDifficulty);
			var modifier = cost >= 1000 ? 60 + tier * 15 : 100;
			if (profile.Style == "rush")
				modifier = cost < 1000 ? modifier + 35 : Math.Max(25, modifier - 25);
			return (int)Math.Clamp((long)share * modifier / 100, 1, 100);
		}
	}
}
