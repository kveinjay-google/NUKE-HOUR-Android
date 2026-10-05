using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA2.Widgets
{
	// Desktop branding occupies the existing top frame, not the lobby content area.
	public sealed class MacBrandTitleWidget : LabelWidget
	{
		[ObjectCreator.UseCtor]
		public MacBrandTitleWidget(ModData modData) : base(modData)
		{
			IsVisible = () => Platform.CurrentPlatform == PlatformType.OSX;
			GetText = () => "NUKE HOUR";
		}

		MacBrandTitleWidget(MacBrandTitleWidget other) : base(other) { }
		public override Widget Clone() => new MacBrandTitleWidget(this);
	}
}
