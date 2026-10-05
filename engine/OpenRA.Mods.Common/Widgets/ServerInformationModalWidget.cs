using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets
{
	// The window stack keeps the browser alive but hidden. Draw it without routing
	// input or ticking it, so this remains a modal and CloseWindow restores it once.
	public class ServerInformationModalWidget : Widget
	{
		public Widget Backdrop;

		public override void PrepareRenderables()
		{
			Backdrop?.PrepareRenderablesOuter();
		}

		public override void Draw()
		{
			Backdrop?.DrawOuter();
			WidgetUtils.FillRectWithColor(RenderBounds, Color.FromArgb(140, 0, 0, 0));
		}

		public override bool HandleMouseInput(MouseInput mi) => true;
		public override bool HandleKeyPress(KeyInput e) => true;
	}
}
