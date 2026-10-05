using System;

namespace OpenRA.Mods.RA2.Missions
{
	/// <summary>Presentation-only lookup: UI language takes priority over imported CSF language.</summary>
	public static class RetailMissionText
	{
		public static string Resolve(string key, Func<string, string> localizedMessage, Func<string, string> importedMessage)
		{
			if (string.IsNullOrWhiteSpace(key))
				return "";

			var id = "retail-" + key.ToLowerInvariant().Replace(':', '-');
			var translated = localizedMessage(id);
			if (!string.IsNullOrWhiteSpace(translated))
				return translated;

			// Preserve unsupported/custom mission text instead of silently dropping instructions.
			return (importedMessage(key) ?? key).TrimEnd('\0');
		}
	}
}
