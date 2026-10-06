namespace OpenRA
{
	public static class AiDifficultyLobbyPolicy
	{
		// Called only after the server's host/slot/readiness authorization checks.
		public static bool TrySnapshot(string botType, string supplied, out string snapshot, out string customName)
		{
			snapshot = "";
			customName = null;
			if (AiDifficultyCatalog.IsOfficialBotType(botType))
			{
				if (!string.IsNullOrEmpty(supplied)) return false;
				var official = AiDifficultyCatalog.Resolve(botType, "");
				snapshot = AiDifficultyCatalog.Encode(official);
				return true;
			}
			if (botType == AiDifficultyCatalog.CustomBotType)
			{
				if (!AiDifficultyCatalog.TryDecode(supplied, out var profile) ||
					!profile.Id.StartsWith("custom-", System.StringComparison.Ordinal)) return false;
				snapshot = AiDifficultyCatalog.Encode(profile);
				customName = profile.Name;
				return true;
			}
			return string.IsNullOrEmpty(supplied);
		}
	}
}
