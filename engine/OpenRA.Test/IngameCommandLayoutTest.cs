using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public class IngameCommandLayoutTest
	{
		[TestCase("random-soviets", "苏军", "俄罗斯", "苏军")]
		[TestCase("random-soviets", "苏军", "利比亚", "苏军")]
		[TestCase("random-allies", "盟军", "法国", "盟军 (法国)")]
		[TestCase("america", "美国", "美国", "美国")]
		public void StatsFactionNameKeepsSovietIdentityWithoutCountrySuffix(
			string factionId, string displayName, string actualName, string expected)
		{
			var logic = typeof(IngameCommandLayout).Assembly.GetType("OpenRA.Mods.Common.Widgets.Logic.GameInfoStatsLogic");
			Assert.That(logic, Is.Not.Null);
			var method = logic!.GetMethod("FormatFactionName", BindingFlags.Static | BindingFlags.NonPublic);
			Assert.That(method, Is.Not.Null);
			Assert.That(method!.Invoke(null, new object[] { factionId, displayName, actualName }), Is.EqualTo(expected));
		}

		static T Bare<T>(string id) where T : Widget
		{
#pragma warning disable SYSLIB0050
			var widget = (T)FormatterServices.GetUninitializedObject(typeof(T));
#pragma warning restore SYSLIB0050
			typeof(Widget).GetField(nameof(Widget.Children))!.SetValue(widget, new List<Widget>());
			widget.Id = id;
			widget.IsVisible = () => true;
			if (widget is LabelWidget label) { label.Font = "Regular"; label.GetText = () => "Title"; }
			if (widget is ButtonWidget button) button.GetText = () => "Resume";
			return widget;
		}

		[TestCase(667, 320)]
		[TestCase(812, 375)]
		[TestCase(1180, 820)]
		[TestCase(1440, 900)]
		public void WrappedInfoTabsAndCommandsStayInsideTheirParents(int width, int height)
		{
			var root = Bare<ContainerWidget>("INGAME_MENU");
			root.AddChild(Bare<LabelWidget>("SHELL_TITLE"));
			var nav = Bare<ScrollPanelWidget>("MENU_NAV");
			root.AddChild(nav);
			var commands = Bare<ContainerWidget>("MENU_BUTTONS");
			nav.Children.Add(commands);
			for (var i = 0; i < 9; i++) commands.AddChild(Bare<ButtonWidget>(i == 0 ? "RESUME" : "COMMAND_" + i));
			var panel = Bare<ContainerWidget>("PANEL_ROOT");
			root.AddChild(panel);
			var info = Bare<ContainerWidget>("GAME_INFO_PANEL");
			panel.AddChild(info);
			info.AddChild(Bare<LabelWidget>("TITLE"));
			var tabs = Bare<ContainerWidget>("TAB_CONTAINER_5");
			info.AddChild(tabs);
			for (var i = 0; i < 5; i++) tabs.AddChild(Bare<ButtonWidget>("TAB_" + i));
			var body = Bare<ContainerWidget>("STATS_PANEL");
			info.AddChild(body);
			var size = new Size(width, height);
			var layout = new IngameCommandLayout(new IosScreenSnapshot(size, size, default));
			layout.Apply(root, null);
			layout.Apply(root, null);
			Assert.That(body.Bounds.Y, Is.GreaterThan(tabs.Bounds.Bottom));
			Assert.That(body.Bounds.Bottom, Is.LessThanOrEqualTo(info.Bounds.Height));
			foreach (var tab in tabs.Children)
			{
				Assert.That(tab.Bounds.Right, Is.LessThanOrEqualTo(tabs.Bounds.Width));
				Assert.That(tab.Bounds.Bottom, Is.LessThanOrEqualTo(tabs.Bounds.Height));
			}
			Assert.That(commands.Bounds.Right, Is.LessThanOrEqualTo(nav.Bounds.Width - nav.ScrollbarWidth));
			Assert.That(nav.ContentHeight, Is.GreaterThan(commands.Bounds.Bottom));
		}

		[TestCase(300, 48)]
		[TestCase(540, 48)]
		public void StatsReserveActionTargetsBeforeTextColumns(int width, int target)
		{
			var columns = IngameCommandLayout.StatsColumns(width, target);
			Assert.That(columns[3].Width, Is.EqualTo(2 * target));
			Assert.That(columns[2].Right, Is.EqualTo(columns[3].X));
			Assert.That(columns[3].Right, Is.EqualTo(width));
			for (var i = 1; i < columns.Length; i++)
				Assert.That(columns[i - 1].Right, Is.LessThanOrEqualTo(columns[i].X));
		}

		[TestCase(812, 375)]
		[TestCase(1180, 820)]
		[TestCase(1440, 900)]
		public void CommandRailAndInformationStayInsideSafeFrame(int width, int height)
		{
			var screen = new IosScreenSnapshot(new Size(width, height), new Size(width, height), default);
			var layout = new IngameCommandLayout(screen);
			Assert.That(layout.Navigation.Right, Is.LessThan(layout.Information.X));
			Assert.That(layout.Navigation.Y, Is.GreaterThanOrEqualTo(layout.Header.Bottom));
			Assert.That(layout.Information.Bottom, Is.LessThanOrEqualTo(layout.Content.Bottom));
			Assert.That(layout.Information.Right, Is.LessThanOrEqualTo(layout.Content.Right));
			Assert.That(layout.ActionHeight, Is.GreaterThanOrEqualTo(56));
			Assert.That(layout.TabHeight, Is.GreaterThanOrEqualTo(48));
		}

		[Test]
		public void CompactPhoneCommandButtonsUseOneColumnWithoutVerticalOverflow()
		{
			var root = Bare<ContainerWidget>("INGAME_MENU");
			var nav = Bare<ScrollPanelWidget>("MENU_NAV");
			root.AddChild(nav);
			var commands = Bare<ContainerWidget>("MENU_BUTTONS");
			nav.Children.Add(commands);
			for (var i = 0; i < 5; i++)
				commands.AddChild(Bare<ButtonWidget>(i == 0 ? "RESUME" : "COMMAND_" + i));

			var layout = new IngameCommandLayout(new IosScreenSnapshot(
				new Size(1558, 720), new Size(844, 390), new IosSafeAreaInsets(47, 0, 47, 21)));
			layout.Apply(root, null);

			Assert.Multiple(() =>
			{
				Assert.That(commands.Children.All(c => c.Bounds.X == 0), Is.True);
				Assert.That(commands.Children[1].Bounds.Y, Is.GreaterThan(commands.Children[0].Bounds.Bottom));
				Assert.That(commands.Children[2].Bounds.Y, Is.GreaterThan(commands.Children[0].Bounds.Y));
				Assert.That(commands.Bounds.Height, Is.LessThan(nav.Bounds.Height));
				Assert.That(nav.ContentHeight, Is.LessThanOrEqualTo(nav.Bounds.Height));
			});
		}

		[Test]
		public void PhoneInformationTabsShareOneEqualWidthRow()
		{
			var root = Bare<ContainerWidget>("INGAME_MENU");
			var nav = Bare<ScrollPanelWidget>("MENU_NAV");
			root.AddChild(nav);
			nav.Children.Add(Bare<ContainerWidget>("MENU_BUTTONS"));
			var info = Bare<ContainerWidget>("GAME_INFO_PANEL");
			root.AddChild(info);
			info.AddChild(Bare<LabelWidget>("TITLE"));
			var tabs = Bare<ContainerWidget>("TAB_CONTAINER_4");
			info.AddChild(tabs);
			for (var i = 0; i < 4; i++) tabs.AddChild(Bare<ButtonWidget>("TAB_" + i));
			var size = new Size(667, 320);
			new IngameCommandLayout(new IosScreenSnapshot(size, size, default)).Apply(root, null);
			Assert.That(tabs.Children.All(c => c.Bounds.Y == 0), Is.True);
			Assert.That(tabs.Children.Select(c => c.Bounds.Width).Max() - tabs.Children.Select(c => c.Bounds.Width).Min(), Is.LessThanOrEqualTo(1));
			Assert.That(tabs.Children.Last().Bounds.Right, Is.EqualTo(tabs.Bounds.Width));
		}

		[Test]
		public void TabletCommandButtonsKeepTheSingleColumnRail()
		{
			var root = Bare<ContainerWidget>("INGAME_MENU");
			var nav = Bare<ScrollPanelWidget>("MENU_NAV");
			root.AddChild(nav);
			var commands = Bare<ContainerWidget>("MENU_BUTTONS");
			nav.Children.Add(commands);
			for (var i = 0; i < 3; i++)
				commands.AddChild(Bare<ButtonWidget>(i == 0 ? "RESUME" : "COMMAND_" + i));

			var layout = new IngameCommandLayout(new IosScreenSnapshot(
				new Size(1180, 820), new Size(1180, 820), new IosSafeAreaInsets(0, 0, 0, 20)));
			layout.Apply(root, null);

			Assert.That(commands.Children.All(button => button.Bounds.X == 0), Is.True);
			Assert.That(commands.Children[1].Bounds.Y, Is.GreaterThan(commands.Children[0].Bounds.Y));
		}

		[Test]
		public void IngameMenuRelayoutIsChangeDrivenInsteadOfPeriodic()
		{
			var source = System.IO.File.ReadAllText(System.IO.Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Mods.Common", "Widgets", "Logic", "Ingame", "IngameMenuLogic.cs"));
			var tickStart = source.IndexOf("public override void Tick()", System.StringComparison.Ordinal);
			var tickEnd = source.IndexOf("protected override void Dispose", tickStart, System.StringComparison.Ordinal);
			var tick = source.Substring(tickStart, tickEnd - tickStart);

			Assert.That(tick, Does.Contain("CommandStructureSignature"));
			Assert.That(tick, Does.Not.Contain("% 10"),
				"Periodic full-tree layout causes visible stalls while a touch scroll is in progress.");
		}

		static string RepositoryRoot()
		{
			var root = TestContext.CurrentContext.TestDirectory;
			while (root != null && !System.IO.Directory.Exists(System.IO.Path.Combine(root, "engine", "OpenRA.Mods.Common")))
				root = System.IO.Directory.GetParent(root)?.FullName;
			Assert.That(root, Is.Not.Null);
			return root!;
		}

		[TestCase(28)]
		[TestCase(40)]
		public void ChatRowsMeasureTheFinalFontBeforeWrapping(int lineHeight)
		{
			var row = Bare<ContainerWidget>("SYSTEM_LINE_TEMPLATE");
			row.Bounds.Height = 16;
			foreach (var id in new[] { "TIME", "PREFIX", "TEXT" })
			{
				var label = Bare<LabelWidget>(id);
				label.Bounds = new WidgetBounds(5, 0, 37, 16);
				row.AddChild(label);
			}
			var method = typeof(IngameChatLogic).GetMethod("PrepareCommandLineMetrics", BindingFlags.Static | BindingFlags.NonPublic);
			System.Func<LabelWidget, string, int2> measure = (_, text) => new int2(text.Length * 20, lineHeight);
			method.Invoke(null, new object[] { row, measure });
			Assert.That(row.Bounds.Height, Is.EqualTo(lineHeight));
			Assert.That(row.Get<LabelWidget>("TIME").Bounds.Width, Is.EqualTo(100));
			Assert.That(row.Children.Cast<LabelWidget>().All(c => c.VAlign == TextVAlign.Top && c.Bounds.Height == lineHeight), Is.True);
		}

		[Test]
		public void PhoneSettingLabelAndControlUseOneTouchHeight()
		{
			var column = Bare<ContainerWidget>("SETTING");
			column.Bounds = new WidgetBounds(0, 0, 600, 240);
			column.AddChild(Bare<LabelWidget>("LABEL"));
			column.AddChild(Bare<ButtonWidget>("CONTROL"));
			var layout = IosSettingsLayout.ForScreen(true, new Size(1558, 720), new Size(844, 390), default);
			var method = typeof(IosSettingsLayout).Assembly.GetType("OpenRA.Mods.Common.Widgets.Logic.SettingsLogic")
				.GetMethod("CompactPhoneSettingColumn", BindingFlags.Static | BindingFlags.NonPublic);
			method.Invoke(null, new object[] { column, layout });
			Assert.That(column.Bounds.Height, Is.EqualTo(layout.MinimumTarget));
			Assert.That(column.Children[0].Bounds.Right, Is.LessThan(column.Children[1].Bounds.X));
			Assert.That(column.Children[1].Bounds.Right, Is.LessThanOrEqualTo(column.Bounds.Width));
		}

		[Test]
		public void PhoneStandaloneSettingsUseTwoColumnsAndCorrectScrollHeight()
		{
			var scroll = Bare<ScrollPanelWidget>("SETTINGS_SCROLLPANEL");
			scroll.Bounds = new WidgetBounds(0, 0, 1300, 400);
			for (var i = 0; i < 4; i++)
			{
				var row = Bare<ContainerWidget>("ROW" + i);
				row.Bounds = new WidgetBounds(0, i * 180, 1300, 180);
				row.AddChild(Bare<LabelWidget>("LABEL"));
				row.AddChild(Bare<ButtonWidget>("CONTROL"));
				scroll.Children.Add(row);
			}
			var layout = IosSettingsLayout.ForScreen(true, new Size(1558, 720), new Size(844, 390), default);
			var method = typeof(SettingsLogic).GetMethod("PackPhoneSingleRows", BindingFlags.Static | BindingFlags.NonPublic);
			method.Invoke(null, new object[] { scroll, layout });
			Assert.That(scroll.Children[0].Bounds.Y, Is.EqualTo(scroll.Children[1].Bounds.Y));
			Assert.That(scroll.Children[0].Bounds.Right, Is.LessThan(scroll.Children[1].Bounds.X));
			Assert.That(scroll.Children[2].Bounds.Y, Is.GreaterThan(scroll.Children[0].Bounds.Bottom));
			Assert.That(scroll.ContentHeight, Is.LessThan(scroll.Bounds.Height));
			var contentHeight = scroll.ContentHeight;
			scroll.Layout.AdjustChildren();
			scroll.Layout.AdjustChildren();
			Assert.That(scroll.Children[0].Bounds.Y, Is.EqualTo(scroll.Children[1].Bounds.Y));
			Assert.That(scroll.ContentHeight, Is.EqualTo(contentHeight));
		}

	}
}
