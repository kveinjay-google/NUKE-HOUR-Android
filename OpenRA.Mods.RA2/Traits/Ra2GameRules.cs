using System.Collections.Generic;
using System.Linq;
using OpenRA.FileSystem;
using OpenRA.Mods.Common.Traits;
using OpenRA.Network;
using OpenRA.Traits;

namespace OpenRA.Mods.RA2.Traits
{
	[TraitLocation(SystemActors.Player)]
	[Desc("Selects RA2 or Yuri's Revenge Tanya demolition rules for the entire match.")]
	public sealed class Ra2GameRulesInfo : TraitInfo, ILobbyOptions, ITechTreePrerequisiteInfo
	{
		public const string OptionId = "ra2-game-rules";
		public const string YuriPrerequisite = "ruleset.yuris-revenge";

		[FluentReference]
		public readonly string Label = "dropdown-ra2-game-rules.label";

		[FluentReference]
		public readonly string Description = "dropdown-ra2-game-rules.description";

		[FluentReference]
		public readonly string OriginalLabel = "dropdown-ra2-game-rules.original";

		[FluentReference]
		public readonly string YuriLabel = "dropdown-ra2-game-rules.yuri";

		public readonly int DisplayOrder = 5;

		public static (string DefaultValue, bool Locked) LobbyDefaults(IReadOnlyFileSystem files)
		{
			var hasBase = files.Exists("ra2.mix") && files.Exists("language.mix");
			var hasYuri = hasBase && files.Exists("ra2md.mix") && files.Exists("langmd.mix");
			// Content-free dedicated servers can host either rule choice. Local hosts
			// with base-only content are locked to RA2; incomplete YR imports do not unlock it.
			return (hasYuri ? "yr" : "ra2", hasBase && !hasYuri);
		}

		IEnumerable<LobbyOption> ILobbyOptions.LobbyOptions(MapPreview map)
		{
			var (defaultValue, locked) = LobbyDefaults(map);
			// Keep both labels available so a base-only client can display the host's choice.
			yield return new LobbyOption(map, OptionId, Label, Description, true, DisplayOrder,
				new Dictionary<string, string> { ["ra2"] = OriginalLabel, ["yr"] = YuriLabel }, defaultValue, locked);
		}

		IEnumerable<string> ITechTreePrerequisiteInfo.Prerequisites(ActorInfo info)
		{
			yield return YuriPrerequisite;
		}

		public override object Create(ActorInitializer init) => new Ra2GameRules(init.World.LobbyInfo.GlobalSettings);
	}

	public sealed class Ra2GameRules : ITechTreePrerequisite
	{
		readonly bool yuriRules;

		public Ra2GameRules(Session.Global settings)
		{
			// Simulation never inspects local retail files: the room's serialized choice
			// is authoritative for clients, replays, and saves. Missing choices use RA2.
			yuriRules = settings.OptionOrDefault(Ra2GameRulesInfo.OptionId, "ra2") == "yr";
		}

		public IEnumerable<string> ProvidesPrerequisites => yuriRules ?
			new[] { Ra2GameRulesInfo.YuriPrerequisite } : Enumerable.Empty<string>();
	}
}
