#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Mods.RA2.Widgets;
using OpenRA.Mods.RA2.Widgets.Logic;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class IosProductionBatchToggleTest
	{
		static readonly (Size Renderer, Size Native, IosSafeAreaInsets Insets)[] Devices =
		{
			(new Size(1558, 720), new Size(844, 390), new IosSafeAreaInsets(47, 0, 47, 21)),
			(new Size(1560, 720), new Size(932, 430), new IosSafeAreaInsets(59, 0, 59, 21)),
			(new Size(1558, 720), new Size(844, 390), new IosSafeAreaInsets(55, 0, 39, 21)),
			(new Size(1558, 720), new Size(844, 390), new IosSafeAreaInsets(39, 0, 55, 21)),
			(new Size(1133, 744), new Size(1133, 744), new IosSafeAreaInsets(0, 0, 0, 20)),
			(new Size(1180, 820), new Size(1180, 820), new IosSafeAreaInsets(0, 0, 0, 20)),
			(new Size(1366, 1024), new Size(1366, 1024), new IosSafeAreaInsets(0, 0, 0, 20)),
		};

		static bool Calls(MethodInfo caller, MethodInfo callee)
		{
			var il = caller.GetMethodBody()?.GetILAsByteArray();
			if (il == null)
				return false;

			for (var i = 0; i + sizeof(int) < il.Length; i++)
				if ((il[i] == 0x28 || il[i] == 0x6F) && BitConverter.ToInt32(il, i + 1) == callee.MetadataToken)
					return true;

			return false;
		}

		static T Uninitialized<T>() where T : class =>
			(T)FormatterServices.GetUninitializedObject(typeof(T));

		static void InitializeWidget(Widget widget, string id)
		{
			typeof(Widget).GetField(nameof(Widget.Children))!.SetValue(widget, new List<Widget>());
			widget.Id = id;
			widget.IsVisible = () => true;
		}

		static ButtonWidget CreateSlot(string id, out LabelWidget label, out ImageWidget icon)
		{
			var button = Uninitialized<ButtonWidget>();
			InitializeWidget(button, id);
			label = Uninitialized<LabelWidget>();
			InitializeWidget(label, "LABEL");
			icon = new ImageWidget { Id = "ICON" };
			button.AddChild(icon);
			button.AddChild(label);
			return button;
		}

		static void SetPrivateField<T>(object target, string name, T value)
		{
			target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
		}

		static (int Width, int Height) PngSize(string path)
		{
			using var stream = File.OpenRead(path);
			using var reader = new BinaryReader(stream);
			Assert.That(reader.ReadBytes(8), Is.EqualTo(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }));
			Assert.That(reader.ReadBytes(4), Is.EqualTo(new byte[] { 0, 0, 0, 13 }));
			Assert.That(reader.ReadBytes(4), Is.EqualTo(new byte[] { 73, 72, 68, 82 }));

			static int ReadBigEndianInt32(BinaryReader input)
			{
				var bytes = input.ReadBytes(4);
				return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
			}

			return (ReadBigEndianInt32(reader), ReadBigEndianInt32(reader));
		}

		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null, "Unable to locate the repository root.");
			return directory!.FullName;
		}

		[TestCase("Building")]
		[TestCase("Support")]
		public void StructureQueuesAllowOnlyOneTotalItem(string queue)
		{
			var path = Path.Combine(RepositoryRoot(), "mods", "ra2", "rules", "player.yaml");
			var player = MiniYaml.FromString(File.ReadAllText(path), path)
				.Single(node => node.Key == "Player");
			var productionQueue = player.Value.NodeWithKey($"ClassicProductionQueue@{queue}");

			Assert.That(productionQueue.Value.NodeWithKey("ItemLimit").Value.Value, Is.EqualTo("1"));
			Assert.That(productionQueue.Value.NodeWithKey("QueueLimit").Value.Value, Is.EqualTo("1"));
		}

		[TestCase("Infantry")]
		[TestCase("Vehicle")]
		[TestCase("Aircraft")]
		[TestCase("Ship")]
		public void UnitQueuesKeepTheirDefaultBatchLimit(string queue)
		{
			var path = Path.Combine(RepositoryRoot(), "mods", "ra2", "rules", "player.yaml");
			var player = MiniYaml.FromString(File.ReadAllText(path), path)
				.Single(node => node.Key == "Player");
			var productionQueue = player.Value.NodeWithKey($"ClassicProductionQueue@{queue}");

			Assert.That(productionQueue.Value.NodeWithKeyOrDefault("ItemLimit"), Is.Null);
		}

		[TestCase(Modifiers.None, 1, 1)]
		[TestCase(Modifiers.None, 5, 5)]
		[TestCase(Modifiers.Shift, 1, 5)]
		[TestCase(Modifiers.Shift, 5, 5)]
		[TestCase(Modifiers.None, 0, 1)]
		public void ProductionCountUsesEitherShiftOrTouchMultiplier(Modifiers modifiers, int multiplier, int expected)
		{
			Assert.That(ProductionBatchPolicy.ResolveStartCount(modifiers, multiplier), Is.EqualTo(expected));
		}

		[TestCase(0, 0, 0, 999, 0, 0, 5)]
		[TestCase(3, 0, 5, 999, 0, 0, 2)]
		[TestCase(2, 2, 0, 3, 0, 0, 1)]
		[TestCase(0, 0, 0, 999, 1, 0, 1)]
		[TestCase(0, 1, 0, 999, 1, 0, 0)]
		public void ProductionQueueAcceptsFiveOrdinaryBuildingsButPreservesLimits(
			int queueCount, int sameItemQueued, int queueLimit, int itemLimit,
			int buildLimit, int owned, int expected)
		{
			Assert.That(ProductionBatchPolicy.ResolveAcceptedCount(
				5, queueCount, sameItemQueued, queueLimit, itemLimit, buildLimit, owned), Is.EqualTo(expected));
		}

		[TestCase(0, 0, 5, 1)]
		[TestCase(1, 0, 1, 0)]
		[TestCase(1, 1, 5, 0)]
		public void StructureQueueAcceptsAtMostOneTotalItem(
			int queueCount, int sameItemQueued, int requested, int expected)
		{
			Assert.That(ProductionBatchPolicy.ResolveAcceptedCount(
				(uint)requested, queueCount, sameItemQueued, 1, 1, 0, 0), Is.EqualTo(expected));
		}

		[TestCase(5, true, true, 0, 1)]
		[TestCase(1, true, false, 1, 0)]
		[TestCase(5, false, true, 0, 5)]
		[TestCase(5, false, false, 3, 2)]
		public void AuthoritativeProductionCountNeverLetsStructuresBypassTheSingleSlot(
			int requested, bool isStructure, bool bypassLimits, int queueCount, int expected)
		{
			Assert.That(ProductionBatchPolicy.ResolveOrderCount(
				(uint)requested, isStructure, bypassLimits, queueCount, 0, 5, 999, 0, 0),
				Is.EqualTo(expected));
		}

		[Test]
		public void ProductionQueueUsesTheSharedBatchLimitPolicy()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "engine", "OpenRA.Mods.Common",
				"Traits", "Player", "ProductionQueue.cs"));

			StringAssert.Contains("ProductionBatchPolicy.ResolveOrderCount", source);
			StringAssert.Contains("unit.HasTraitInfo<BuildingInfo>()", source);
			StringAssert.Contains("developerMode.AllTech", source);
			StringAssert.Contains("Queue.Count, sameItemCount", source);
			StringAssert.Contains("Info.QueueLimit, Info.ItemLimit, bi.BuildLimit, ownedCount", source);
			StringAssert.Contains("actor.HasTraitInfo<BuildingInfo>() && Queue.Count > 0", source);
		}

		[Test]
		public void TouchCommandBarKeepsFourGroupsRequiredCommandsAndRemovesJoystickDuplicates()
		{
			var visible = CommandBarLayoutPolicy.AvailableIds(CommandBarCatalog.AllIds, true).ToArray();

			Assert.That(visible.First(), Is.EqualTo("CTRL_TOGGLE"));
			Assert.That(visible, Does.Contain("CTRL_TOGGLE"));
			Assert.That(visible, Does.Contain("GROUP_01"));
			Assert.That(visible, Does.Not.Contain("GROUP_05"));
			Assert.That(visible, Does.Not.Contain("GROUP_06"));
			Assert.That(visible, Does.Not.Contain("GROUP_10"));
			Assert.That(visible, Does.Not.Contain("SELECT_BY_TYPE"));
			Assert.That(visible, Does.Contain("CYCLE_BASE"));
			Assert.That(visible, Does.Not.Contain("FORCE_ATTACK"));
			Assert.That(visible, Does.Not.Contain("DEPLOY"));
			Assert.That(visible, Does.Not.Contain("FORCE_MOVE"));
			Assert.That(visible, Does.Not.Contain("GUARD"));
			Assert.That(visible, Does.Not.Contain("STANCE_ATTACKANYTHING"));
			Assert.That(visible, Does.Not.Contain("STANCE_DEFEND"));
			Assert.That(visible, Does.Not.Contain("STANCE_RETURNFIRE"));
			Assert.That(visible, Does.Contain("STOP"));
			Assert.That(visible, Does.Contain("PRODUCTION_X5"));
		}

		[Test]
		public void DesktopCommandBarKeepsCommandsThatHaveNoVirtualJoystickDuplicate()
		{
			var visible = CommandBarLayoutPolicy.AvailableIds(CommandBarCatalog.AllIds, false).ToArray();

			Assert.That(visible, Does.Contain("GROUP_10"));
			Assert.That(visible, Does.Contain("SELECT_BY_TYPE"));
			Assert.That(visible, Does.Contain("FORCE_ATTACK"));
			Assert.That(visible, Does.Contain("DEPLOY"));
			Assert.That(visible, Does.Contain("STOP"));
			Assert.That(visible, Does.Not.Contain("PRODUCTION_X5"));
			Assert.That(visible, Does.Not.Contain("CTRL_TOGGLE"));
		}

		[Test]
		public void DesktopPreferencesGainRepairAndSellAcrossLegacyMigrationVersions()
		{
			var savedOrder = new[] { "ATTACK_MOVE", "GROUP_01" };
			foreach (var version in new[] { 1, 2, 3, 4, 5 })
			{
				var normalized = CommandBarLayoutPolicy.NormalizeVisibleOrder(
					savedOrder, false, version).ToArray();
				Assert.Multiple(() =>
				{
					Assert.That(normalized, Is.EqualTo(new[]
					{
						"ATTACK_MOVE", "GROUP_01", "REPAIR", "SELL", "AUTO_REPAIR", "BEACON"
					}), $"v{version}");
					Assert.That(normalized, Does.Not.Contain("STOP"), $"v{version}");
					Assert.That(normalized, Does.Not.Contain("CYCLE_BASE"), $"v{version}");
					Assert.That(normalized, Does.Not.Contain("PRODUCTION_X5"), $"v{version}");
				});
			}

			var desktopCompact = (string[])typeof(CustomCommandBarWidget).GetField(
				"DesktopCompactIds", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
				Assert.That(desktopCompact, Is.EqualTo(new[]
				{
					"ATTACK_MOVE", "FORCE_MOVE", "FORCE_ATTACK", "GUARD",
					"DEPLOY", "SCATTER", "REPAIR", "AUTO_REPAIR", "SELL", "BEACON", "STOP", "QUEUE_ORDERS",
					"STANCE_ATTACKANYTHING", "STANCE_DEFEND", "STANCE_RETURNFIRE", "STANCE_HOLDFIRE",
			}));
		}

		[Test]
		public void TouchPreferenceMigrationKeepsLegacyX5ThresholdAndAppendsRequiredCommandsOnce()
		{
			Assert.That(CommandBarLayoutPolicy.CurrentVersion, Is.EqualTo(7));
			foreach (var version in new[] { 1, 2 })
			{
				var upgraded = CommandBarLayoutPolicy.NormalizeVisibleOrder(
					new[] { "GROUP_01", "STOP", "STOP" }, true, version).ToArray();
				Assert.Multiple(() =>
				{
					Assert.That(upgraded.Count(id => id == "PRODUCTION_X5"), Is.EqualTo(1), $"v{version}");
					Assert.That(upgraded.Count(id => id == "STOP"), Is.EqualTo(1), $"v{version}");
					Assert.That(upgraded.Count(id => id == "CYCLE_BASE"), Is.EqualTo(1), $"v{version}");
				});
			}

			foreach (var version in new[] { 3, 4, 5 })
			{
				var customized = CommandBarLayoutPolicy.NormalizeVisibleOrder(
					new[] { "GROUP_01", "STOP", "STOP" }, true, version).ToArray();
				Assert.Multiple(() =>
				{
					Assert.That(customized, Does.Not.Contain("PRODUCTION_X5"), $"v{version}");
					Assert.That(customized.Count(id => id == "STOP"), Is.EqualTo(1), $"v{version}");
					Assert.That(customized.Count(id => id == "CYCLE_BASE"), Is.EqualTo(1), $"v{version}");
				});
			}

			var newInstall = CommandBarLayoutPolicy.NormalizeVisibleOrder(
				CommandBarCatalog.DefaultVisibleIds(), true, 1).ToArray();
			Assert.Multiple(() =>
			{
				Assert.That(newInstall.First(), Is.EqualTo("CTRL_TOGGLE"));
				Assert.That(newInstall.Count(id => id == "CTRL_TOGGLE"), Is.EqualTo(1));
				Assert.That(newInstall, Does.Not.Contain("STANCE_HOLDFIRE"));
				Assert.That(newInstall, Does.Contain("STOP"));
				Assert.That(newInstall, Does.Contain("CYCLE_BASE"));
			});
		}

		[Test]
		public void TouchQuickbarMigrationAddsRepairAndSellAsPermanentActions()
		{
			var upgraded = CommandBarLayoutPolicy.NormalizeVisibleOrder(
				new[] { "GROUP_01", "STOP" }, true, 5).ToArray();
			var upgradedDesktop = CommandBarLayoutPolicy.NormalizeVisibleOrder(
				new[] { "ATTACK_MOVE", "STOP" }, false, 5).ToArray();
			var defaults = CommandBarCatalog.DefaultVisibleIds().ToArray();
			var compact = (string[])typeof(CustomCommandBarWidget).GetField(
				"TouchCompactIds", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
			var desktopCompact = (string[])typeof(CustomCommandBarWidget).GetField(
				"DesktopCompactIds", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;

			Assert.Multiple(() =>
			{
				Assert.That(upgraded.Count(id => id == "REPAIR"), Is.EqualTo(1));
				Assert.That(upgraded.Count(id => id == "SELL"), Is.EqualTo(1));
				Assert.That(defaults, Does.Contain("REPAIR"));
				Assert.That(defaults, Does.Contain("SELL"));
				Assert.That(compact, Does.Contain("REPAIR"));
				Assert.That(compact, Does.Contain("SELL"));
				Assert.That(upgradedDesktop.Count(id => id == "REPAIR"), Is.EqualTo(1));
				Assert.That(upgradedDesktop.Count(id => id == "SELL"), Is.EqualTo(1));
				Assert.That(desktopCompact, Does.Contain("REPAIR"));
				Assert.That(desktopCompact, Does.Contain("SELL"));
				Assert.That(CommandBarLayoutPolicy.IsRequiredTouchId("REPAIR", true), Is.True);
				Assert.That(CommandBarLayoutPolicy.IsRequiredTouchId("SELL", true), Is.True);
			});
		}

		[Test]
		public void TouchCompactOrderEndsWithQueueStopAndCycleBase()
		{
			var compact = (string[])typeof(CustomCommandBarWidget).GetField(
				"TouchCompactIds", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;

			Assert.That(compact.TakeLast(6), Is.EqualTo(new[]
			{
				"REPAIR", "AUTO_REPAIR", "SELL", "BEACON", "STOP", "CYCLE_BASE"
			}));
			Assert.That(compact.First(), Is.EqualTo("CTRL_TOGGLE"));
		}

		[Test]
		public void RequiredTouchCommandsCannotBeHiddenByAnyEditPath()
		{
			Assert.Multiple(() =>
			{
				Assert.That(CommandBarLayoutPolicy.IsRequiredTouchId("CTRL_TOGGLE", true), Is.True);
				Assert.That(CommandBarLayoutPolicy.IsRequiredTouchId("STOP", true), Is.True);
				Assert.That(CommandBarLayoutPolicy.IsRequiredTouchId("CYCLE_BASE", true), Is.True);
				Assert.That(CommandBarLayoutPolicy.IsRequiredTouchId("STOP", false), Is.False);
				Assert.That(CommandBarLayoutPolicy.CanHide("CTRL_TOGGLE", true), Is.False);
				Assert.That(CommandBarLayoutPolicy.CanHide("STOP", true), Is.False);
				Assert.That(CommandBarLayoutPolicy.CanHide("CYCLE_BASE", true), Is.False);
				Assert.That(CommandBarLayoutPolicy.CanHide("STOP", false), Is.True);
				Assert.That(CommandBarLayoutPolicy.CanHide("SCATTER", true), Is.True);
			});

			var widget = typeof(CustomCommandBarWidget);
			var canHide = typeof(CommandBarLayoutPolicy).GetMethod(nameof(CommandBarLayoutPolicy.CanHide))!;
			var toggle = widget.GetMethod("ToggleSlot", BindingFlags.Instance | BindingFlags.NonPublic)!;
			var tick = widget.GetMethod(nameof(CustomCommandBarWidget.Tick), BindingFlags.Instance | BindingFlags.Public)!;
			var context = widget.GetMethod(
				nameof(CustomCommandBarWidget.HandleMouseInput), BindingFlags.Instance | BindingFlags.Public)!;
			var leftClick = widget.GetNestedTypes(BindingFlags.NonPublic)
				.SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
				.FirstOrDefault(method => method.Name.Contains("EnterEditMode", StringComparison.Ordinal) && Calls(method, toggle));
			Assert.Multiple(() =>
			{
				Assert.That(Calls(toggle, canHide), Is.True, "Click toggles must reject required IDs immediately.");
				Assert.That(Calls(tick, canHide), Is.True, "Dragging a required ID to the hidden row must snap back.");
				Assert.That(Calls(context, toggle), Is.True, "The right-click edit path must share ToggleSlot.");
				Assert.That(leftClick, Is.Not.Null, "The left-click edit path must share ToggleSlot.");
			});
		}

		[Test]
		public void TouchCtrlSlotUsesSameCenteredIconAsGroupButtons()
		{
			var snapshot = new IosScreenSnapshot(
				new Size(1564, 720), new Size(956, 440), new IosSafeAreaInsets(62, 0, 62, 21));
			var slot = TouchCommandBarSlotPolicy.Create(snapshot, "CTRL_TOGGLE", false);
			var group = TouchCommandBarSlotPolicy.Create(snapshot, "GROUP_01", false);

			Assert.Multiple(() =>
			{
				Assert.That(slot.ShowLabel, Is.False);
				Assert.That(slot.IconBounds, Is.EqualTo(group.IconBounds));
				Assert.That(slot.ButtonSize, Is.EqualTo(group.ButtonSize));
			});
		}

		[TestCase(false)]
		[TestCase(true)]
		public void RequiredTouchSlotsKeepStableChromeSizeWithoutRedundantLabels(bool expanded)
		{
			foreach (var device in Devices)
			{
				var snapshot = new IosScreenSnapshot(device.Renderer, device.Native, device.Insets);
				var buttonPoints = snapshot.IsCompactPhone ? expanded ? 46 : 50 : 56;
				var iconPoints = snapshot.IsCompactPhone ? expanded ? 34 : 40 : 42;
				foreach (var id in new[] { "STOP", "CYCLE_BASE" })
				{
					var layout = TouchCommandBarSlotPolicy.Create(snapshot, id, expanded);
					var buttonBounds = new Rectangle(0, 0, layout.ButtonSize.Width, layout.ButtonSize.Height);
					Assert.Multiple(() =>
					{
						Assert.That(layout.ButtonSize,
							Is.EqualTo(new Size(snapshot.LogicalPoints(buttonPoints), snapshot.LogicalPoints(buttonPoints))), id);
						Assert.That(layout.ShowLabel, Is.False, id);
						Assert.That(layout.Font, Is.EqualTo("TinyBold"), id);
						Assert.That(layout.LabelBounds.IsEmpty, Is.True, id);
						Assert.That(buttonBounds.Contains(layout.IconBounds), Is.True, id);
						Assert.That(layout.IconBounds.Size,
							Is.EqualTo(new Size(snapshot.LogicalPoints(iconPoints), snapshot.LogicalPoints(iconPoints))), id);
					});
				}
			}
		}

		[Test]
		public void TouchCommandBarUsesPhysicalTouchPointsInsteadOfRawRendererPixels()
		{
			var snapshot = new IosScreenSnapshot(
				new Size(1564, 720), new Size(956, 440), new IosSafeAreaInsets(62, 0, 62, 21));

			foreach (var expanded in new[] { false, true })
			{
				var layout = TouchCommandBarSlotPolicy.Create(snapshot, "SCATTER", expanded);
				Assert.Multiple(() =>
				{
					var button = snapshot.LogicalPoints(expanded ? 46 : 50);
					var icon = snapshot.LogicalPoints(expanded ? 34 : 40);
					Assert.That(layout.ButtonSize,
						Is.EqualTo(new Size(button, button)), $"button expanded={expanded}");
					Assert.That(layout.IconBounds.Size,
						Is.EqualTo(new Size(icon, icon)),
						$"icon expanded={expanded}");
				});
			}
		}

		[Test]
		public void IosProductionSidebarUsesFullViewportAndSeparatesPaletteFromBackgroundFillRows()
		{
			var snapshot = new IosScreenSnapshot(
				new Size(1564, 720), new Size(956, 440), new IosSafeAreaInsets(62, 0, 62, 21));
			var layout = IosIngameSidebarLayoutPolicy.Create(snapshot);

			Assert.Multiple(() =>
			{
				Assert.That(layout.TopBounds, Is.EqualTo(new Rectangle(1564 - 234, 0, 234, 303)));
				Assert.That(layout.ProductionBounds.Top, Is.EqualTo(layout.TopBounds.Bottom));
				Assert.That(layout.ProductionBounds.Bottom, Is.EqualTo(snapshot.EffectiveSize.Height));
				Assert.That(layout.MaximumRows, Is.EqualTo(6), "Only complete icon rows may appear above the bottom cap.");
				Assert.That(layout.BackgroundRows, Is.EqualTo(7), "Only complete background rows may be emitted.");
				Assert.That(layout.BackgroundRemainderHeight, Is.EqualTo(1),
					"A separately sized final fill must close the fractional gap without overlapping the bottom cap.");
				Assert.That(layout.BottomCapY, Is.EqualTo(layout.ProductionBounds.Height - 66));
				Assert.That(layout.ScrollButtonsY, Is.EqualTo(layout.BottomCapY + 7),
					"The scroll buttons must overlay the authored button slot seven pixels inside the bottom cap.");
				Assert.That(layout.ScrollButtonLocalY(-30) - 30, Is.EqualTo(layout.BottomCapY + 7),
					"The PRODUCTION_TYPES parent offset must be applied exactly once.");
				Assert.That(layout.ScrollButtonLocalBounds(-30, true),
					Is.EqualTo(new WidgetBounds(89, layout.BottomCapY + 37, 77, 27)));
				Assert.That(layout.ScrollButtonLocalBounds(-30, false),
					Is.EqualTo(new WidgetBounds(12, layout.BottomCapY + 37, 77, 27)));
				Assert.That(layout.BackgroundRows * 50 + layout.BackgroundRemainderHeight,
					Is.EqualTo(layout.BottomCapY));
				Assert.That(IosIngameSidebarLayoutPolicy.BackgroundRowHeights(layout),
					Is.EqualTo(new[] { 50, 50, 50, 50, 50, 50, 50, 1 }));
			});
		}

		[Test]
		public void AutomaticProductionPaletteUsesOneLargeColumnOnPhonesAndThreeColumnsOnTablets()
		{
			var phone = new IosScreenSnapshot(
				new Size(1564, 720), new Size(956, 440), new IosSafeAreaInsets(62, 0, 62, 21));
			var tablet = new IosScreenSnapshot(
				new Size(1180, 820), new Size(1180, 820), new IosSafeAreaInsets(0, 0, 0, 20));
			var phoneShell = IosIngameSidebarLayoutPolicy.Create(phone);
			var tabletShell = IosIngameSidebarLayoutPolicy.Create(tablet);
			var phoneLayout = IosProductionPaletteLayoutPolicy.Create(
				phone, phoneShell, IosProductionPaletteMode.Automatic);
			var tabletLayout = IosProductionPaletteLayoutPolicy.Create(
				tablet, tabletShell, IosProductionPaletteMode.Automatic);

			Assert.Multiple(() =>
			{
				Assert.That(phoneLayout.Columns, Is.EqualTo(1));
				Assert.That(phoneLayout.IconSize.X / phone.LogicalPerPoint, Is.GreaterThanOrEqualTo(44));
				Assert.That(phoneLayout.IconSize.Y / phone.LogicalPerPoint, Is.GreaterThanOrEqualTo(44));
				Assert.That(phoneLayout.MaximumRows, Is.GreaterThanOrEqualTo(2));
				Assert.That(phoneLayout.PaletteX, Is.GreaterThanOrEqualTo(0));
				Assert.That(phoneLayout.PaletteX + phoneLayout.GridWidth,
					Is.LessThanOrEqualTo(phoneShell.ProductionBounds.Width));

				Assert.That(tabletLayout.Columns, Is.EqualTo(3));
				Assert.That(tabletLayout.IconSize, Is.EqualTo(new int2(60, 48)));
				Assert.That(tabletLayout.IconMargin, Is.EqualTo(new int2(2, 2)));
			});
		}

		[Test]
		public void CompactPhoneProductionCategoriesUseFourLargeButtonsAndMergeMobileQueues()
		{
			var phoneGroups = IosProductionCategoryPolicy.VisibleGroups(true);
			var tabletGroups = IosProductionCategoryPolicy.VisibleGroups(false);
			var phoneBounds = IosProductionCategoryPolicy.TouchButtonBounds(234, true);

			Assert.Multiple(() =>
			{
				Assert.That(phoneGroups, Is.EqualTo(new[] { "Building", "Support", "Infantry", "Vehicle" }));
				Assert.That(tabletGroups, Is.EqualTo(
					new[] { "Building", "Support", "Infantry", "Vehicle", "Aircraft", "Ship" }));
				Assert.That(IosProductionCategoryPolicy.SourceGroups("Vehicle", true),
					Is.EqualTo(new[] { "Vehicle", "Aircraft", "Ship" }));
				Assert.That(IosProductionCategoryPolicy.SourceGroups("Aircraft", true), Is.Empty);
				Assert.That(IosProductionCategoryPolicy.SourceGroups("Ship", true), Is.Empty);
				Assert.That(IosProductionCategoryPolicy.VisibleGroups(false, true),
					Is.EqualTo(new[] { "Building", "Support", "Infantry", "Vehicle" }));
				Assert.That(IosProductionCategoryPolicy.SourceGroups("Vehicle", false, true),
					Is.EqualTo(new[] { "Vehicle", "Aircraft", "Ship" }));
				Assert.That(IosProductionCategoryPolicy.SourceGroups("Aircraft", false, true), Is.Empty);
				Assert.That(IosProductionCategoryPolicy.SourceGroups("Ship", false, true), Is.Empty);
				Assert.That(phoneBounds, Has.Length.EqualTo(4));
				Assert.That(phoneBounds.All(bounds => bounds.Width >= 48 && bounds.Height >= 48), Is.True);
				Assert.That(phoneBounds[0].X, Is.EqualTo(0));
				Assert.That(phoneBounds[^1].Right, Is.EqualTo(234));
			});
		}

		[Test]
		public void MergedProductionPaletteKeepsTheOwningQueueForEveryUniqueItem()
		{
			var queues = new[]
			{
				(Group: "Vehicle", Items: new[] { (Name: "tank", Order: 20), (Name: "ifv", Order: 10) }),
				(Group: "Aircraft", Items: new[] { (Name: "harrier", Order: 10), (Name: "ifv", Order: 99) }),
				(Group: "Ship", Items: new[] { (Name: "destroyer", Order: 10) }),
			};
			var merged = ProductionPaletteMergePolicy.MergeDistinct(
				queues,
				queue => queue.Items,
				item => item.Name,
				item => item.Order);

			Assert.That(merged.Select(entry => (entry.Item.Name, entry.Queue.Group)), Is.EqualTo(new[]
			{
				("ifv", "Vehicle"),
				("tank", "Vehicle"),
				("harrier", "Aircraft"),
				("destroyer", "Ship"),
			}));
		}

		[Test]
		public void SingleColumnProductionBackgroundUsesOneMatchingFramePerTouchCell()
		{
			var snapshot = new IosScreenSnapshot(
				new Size(1564, 720), new Size(956, 440), new IosSafeAreaInsets(62, 0, 62, 21));
			var shell = IosIngameSidebarLayoutPolicy.Create(snapshot);
			var palette = IosProductionPaletteLayoutPolicy.Create(
				snapshot, shell, IosProductionPaletteMode.LargeSingleColumn);
			var heights = IosProductionPaletteLayoutPolicy.BackgroundRowHeights(palette, shell.BottomCapY);

			Assert.Multiple(() =>
			{
				Assert.That(palette.Columns, Is.EqualTo(1));
				Assert.That(heights.Length, Is.EqualTo((shell.BottomCapY + 115) / 116));
				Assert.That(heights, Has.All.LessThanOrEqualTo(116));
				Assert.That(heights.Sum(), Is.EqualTo(shell.BottomCapY));
				Assert.That(heights, Has.None.EqualTo(50),
					"The legacy 50px chrome rows create the repeated silver half-round seams.");
			});
		}

		[Test]
		public void ProductionPaletteLayoutModesRemainExplicitlySelectable()
		{
			var settings = new GameSettings();
			Assert.That(settings.IosProductionPaletteMode, Is.EqualTo(IosProductionPaletteMode.Automatic));
			Assert.That(InputSettingsLogic.IosProductionPaletteModes(), Is.EqualTo(new[]
			{
				IosProductionPaletteMode.Automatic,
				IosProductionPaletteMode.LargeSingleColumn,
				IosProductionPaletteMode.DoubleColumn,
				IosProductionPaletteMode.CompactThreeColumns
			}));

			InputSettingsLogic.SelectIosProductionPaletteMode(settings, IosProductionPaletteMode.LargeSingleColumn);
			Assert.That(settings.IosProductionPaletteMode, Is.EqualTo(IosProductionPaletteMode.LargeSingleColumn));
			InputSettingsLogic.ResetIosProductionPaletteMode(settings);
			Assert.That(settings.IosProductionPaletteMode, Is.EqualTo(IosProductionPaletteMode.Automatic));
		}

		[TestCase(IosProductionPaletteMode.LargeSingleColumn, 1)]
		[TestCase(IosProductionPaletteMode.DoubleColumn, 2)]
		[TestCase(IosProductionPaletteMode.CompactThreeColumns, 3)]
		public void ProductionPaletteManualModeOverridesDeviceClass(
			IosProductionPaletteMode mode, int expectedColumns)
		{
			foreach (var device in Devices)
			{
				var snapshot = new IosScreenSnapshot(device.Renderer, device.Native, device.Insets);
				var shell = IosIngameSidebarLayoutPolicy.Create(snapshot);
				var layout = IosProductionPaletteLayoutPolicy.Create(snapshot, shell, mode);
				Assert.That(layout.Columns, Is.EqualTo(expectedColumns), device.Native.ToString());
				Assert.That(layout.MaximumRows, Is.GreaterThanOrEqualTo(1), device.Native.ToString());
				Assert.That(layout.PaletteX + layout.GridWidth,
					Is.LessThanOrEqualTo(shell.ProductionBounds.Width), device.Native.ToString());
			}
		}

		[Test]
		public void ProductionPaletteSettingIsExposedByTouchSettings()
		{
			var root = RepositoryRoot();
			var layout = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome", "settings-input.yaml"));
			var logic = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets",
				"Logic", "Settings", "InputSettingsLogic.cs"));
			var production = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets",
				"Logic", "Ingame", "ClassicProductionLogic.cs"));

			StringAssert.Contains("TOUCH_PRODUCTION_LAYOUT_DROPDOWN", layout);
			StringAssert.Contains("BindIosProductionPaletteModeDropdown", logic);
			StringAssert.Contains("Game.Settings.Game.IosProductionPaletteMode", production);
			StringAssert.Contains("productionPalette.ApplyGridLayout", production);
		}

		[Test]
		public void TouchCommandBarUsesOneSlotAndGlyphCanvasForEveryCompactCommand()
		{
			var snapshot = new IosScreenSnapshot(
				new Size(1564, 720), new Size(956, 440), new IosSafeAreaInsets(62, 0, 62, 21));
			var ids = new[]
			{
				"GROUP_01", "GROUP_02", "GROUP_03", "GROUP_04", "GROUP_05", "PRODUCTION_X5",
				"ATTACK_MOVE", "SCATTER", "QUEUE_ORDERS", "STOP", "CYCLE_BASE"
			};

			foreach (var expanded in new[] { false, true })
			{
				var layouts = ids.Select(id => TouchCommandBarSlotPolicy.Create(snapshot, id, expanded)).ToArray();
				Assert.Multiple(() =>
				{
					Assert.That(layouts.Select(layout => layout.ButtonSize).Distinct().ToArray(), Has.Length.EqualTo(1),
						$"Every touch command must occupy the same outer slot when expanded={expanded}.");
					Assert.That(layouts.Select(layout => layout.IconBounds).Distinct().ToArray(), Has.Length.EqualTo(1),
						$"Every touch glyph must use the same centered canvas when expanded={expanded}.");
					Assert.That(layouts.Select(layout => layout.Font).Distinct().ToArray(), Has.Length.EqualTo(1),
						$"Command labels must use one visual style when expanded={expanded}.");
					Assert.That(layouts.All(layout => !layout.ShowLabel && layout.LabelBounds.IsEmpty), Is.True,
						$"Touch command glyphs must communicate their action without duplicated labels when expanded={expanded}.");
				});
			}
		}

		[Test]
		public void FingerSizedDefaultTouchCommandBarFitsEverySupportedSafeWidth()
		{
			var expandedIds = CommandBarLayoutPolicy.NormalizeVisibleOrder(
				CommandBarCatalog.DefaultVisibleIds(), true, 1).ToArray();
			const int CompactSlotCount = 13;

			foreach (var device in Devices)
			{
				var snapshot = new IosScreenSnapshot(device.Renderer, device.Native, device.Insets);
				foreach (var expanded in new[] { false, true })
				{
					var slotCount = expanded ? expandedIds.Length : CompactSlotCount;
					var slot = TouchCommandBarSlotPolicy.Create(snapshot, "STOP", expanded).ButtonSize.Width;
					var pad = TouchCommandBarSlotPolicy.Metric(snapshot, 6);
					var gap = TouchCommandBarSlotPolicy.Metric(snapshot, 2);
					var edit = TouchCommandBarSlotPolicy.Metric(snapshot, snapshot.IsCompactPhone ? 48 : 72);
					var width = 2 * pad + slotCount * (slot + gap) + slot + gap +
						(expanded ? edit + gap : 0);

					Assert.That(width, Is.LessThanOrEqualTo(snapshot.SafeBounds.Width),
						$"Default bar must fit the safe width when expanded={expanded} on {device.Native}.");
				}
			}
		}

		[Test]
		public void ClassicProductionRelayoutReanchorsBothScrollButtons()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "engine", "OpenRA.Mods.Common", "Widgets",
				"Logic", "Ingame", "ClassicProductionLogic.cs"));

			StringAssert.Contains("RefreshIosSidebarLayout(widget, palette, typesContainer, scrollUp, scrollDown);", source,
				"The live layout ticker must re-anchor both arrows after a renderer or widget relayout.");
			StringAssert.Contains("PositionIosScrollButton(typesContainer, scrollUp, layout);", source);
			StringAssert.Contains("PositionIosScrollButton(typesContainer, scrollDown, layout);", source);
			StringAssert.Contains("button.Bounds = layout.ScrollButtonLocalBounds", source,
				"Relayout must restore the complete authored slot, not only its vertical coordinate.");
		}

		[TestCase(false)]
		[TestCase(true)]
		public void TouchCommandBarPlacementIsCenteredBottomInsetAndSafeContained(bool expanded)
		{
			foreach (var device in Devices)
			{
				var snapshot = new IosScreenSnapshot(device.Renderer, device.Native, device.Insets);
				var safe = snapshot.SafeBounds;
				var size = new Size(
					Math.Min(safe.Width, snapshot.LogicalPoints(640)),
					snapshot.LogicalPoints(expanded ? 120 : 56));
				var placed = TouchCommandBarSlotPolicy.PlaceBar(snapshot, size);
				Assert.Multiple(() =>
				{
					Assert.That(safe.Contains(placed), Is.True);
					Assert.That(Math.Abs(2 * placed.X + placed.Width - (2 * safe.X + safe.Width)), Is.LessThanOrEqualTo(1));
					Assert.That(safe.Bottom - placed.Bottom, Is.EqualTo(snapshot.LogicalPoints(12)));
				});
			}
		}

		[Test]
		public void TouchCommandBarCanBePlacedInsideAreaLeftOfProductionSidebar()
		{
			var boundedPlace = typeof(TouchCommandBarSlotPolicy).GetMethod(
				nameof(TouchCommandBarSlotPolicy.PlaceBar),
				new[] { typeof(IosScreenSnapshot), typeof(Size), typeof(Rectangle) });
			Assert.That(boundedPlace, Is.Not.Null,
				"Touch placement needs an explicit available area so the production sidebar is not treated as free space.");

			var snapshots = Devices
				.Select(device => new IosScreenSnapshot(device.Renderer, device.Native, device.Insets))
				.Append(new IosScreenSnapshot(
					new Size(2360, 1640), new Size(1180, 820), new IosSafeAreaInsets(0, 0, 0, 20)));
			foreach (var snapshot in snapshots)
			{
				var safe = snapshot.SafeBounds;
				var sidebar = IosIngameSidebarLayoutPolicy.Create(snapshot).TopBounds;
				var available = Rectangle.FromLTRB(
					safe.Left, safe.Top,
					Math.Max(safe.Left + 1, sidebar.Left - snapshot.LogicalPoints(8)),
					safe.Bottom);
				var size = new Size(
					Math.Min(available.Width, snapshot.LogicalPoints(760)),
					snapshot.LogicalPoints(56));
				var placed = (Rectangle)boundedPlace!.Invoke(null, new object[] { snapshot, size, available })!;

				Assert.Multiple(() =>
				{
					Assert.That(available.Contains(placed), Is.True, snapshot.NativePointSize.ToString());
					Assert.That(placed.Right, Is.LessThan(sidebar.Left), snapshot.NativePointSize.ToString());
					Assert.That(
						Math.Abs(2 * placed.X + placed.Width - (2 * available.X + available.Width)),
						Is.LessThanOrEqualTo(1), snapshot.NativePointSize.ToString());
				});
			}
		}

		[Test]
		public void TouchCommandBarRefreshSignatureIncludesRendererNativeAndAsymmetricSafeArea()
		{
			var baseline = new IosScreenSnapshot(
				new Size(1558, 720), new Size(844, 390), new IosSafeAreaInsets(55, 0, 39, 21));
			var rendererChanged = new IosScreenSnapshot(
				new Size(1559, 720), new Size(844, 390), new IosSafeAreaInsets(55, 0, 39, 21));
			var nativeChanged = new IosScreenSnapshot(
				new Size(1558, 720), new Size(845, 390), new IosSafeAreaInsets(55, 0, 39, 21));
			var safeAreaSwapped = new IosScreenSnapshot(
				new Size(1558, 720), new Size(844, 390), new IosSafeAreaInsets(39, 0, 55, 21));

			Assert.Multiple(() =>
			{
				Assert.That(CustomCommandBarWidget.ShouldRefreshTouchLayout(
					true, false, default, default, default, baseline), Is.True);
				Assert.That(CustomCommandBarWidget.ShouldRefreshTouchLayout(
					true, true, baseline.EffectiveSize, baseline.NativePointSize, baseline.SafeBounds, baseline), Is.False);
				Assert.That(CustomCommandBarWidget.ShouldRefreshTouchLayout(
					true, true, baseline.EffectiveSize, baseline.NativePointSize, baseline.SafeBounds, rendererChanged), Is.True);
				Assert.That(CustomCommandBarWidget.ShouldRefreshTouchLayout(
					true, true, baseline.EffectiveSize, baseline.NativePointSize, baseline.SafeBounds, nativeChanged), Is.True);
				Assert.That(CustomCommandBarWidget.ShouldRefreshTouchLayout(
					true, true, baseline.EffectiveSize, baseline.NativePointSize, baseline.SafeBounds, safeAreaSwapped), Is.True);
				Assert.That(CustomCommandBarWidget.ShouldRefreshTouchLayout(
					false, false, default, default, default, baseline), Is.False);
			});

			var tick = typeof(CustomCommandBarWidget).GetMethod(
				nameof(CustomCommandBarWidget.Tick), BindingFlags.Instance | BindingFlags.Public)!;
			var refresh = typeof(CustomCommandBarWidget).GetMethod(
				nameof(CustomCommandBarWidget.ShouldRefreshTouchLayout), BindingFlags.Static | BindingFlags.Public)!;
			Assert.That(Calls(tick, refresh), Is.True, "The live widget must reflow on the complete screen signature.");
		}

		[Test]
		public void ExpandedTouchCommandBarKeepsCollapseToggleOutsideRightSidebar()
		{
			var snapshot = new IosScreenSnapshot(
				new Size(1558, 720), new Size(844, 390), new IosSafeAreaInsets(47, 0, 47, 21));
			var commandBar = new CustomCommandBarWidget();
			var slots = new ContainerWidget { Id = "COMMAND_SLOTS" };
			commandBar.AddChild(slots);
			var buttons = (Dictionary<string, ButtonWidget>)typeof(CustomCommandBarWidget)
				.GetField("buttons", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(commandBar)!;
			var labels = (Dictionary<string, LabelWidget>)typeof(CustomCommandBarWidget)
				.GetField("labels", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(commandBar)!;
			var icons = (Dictionary<string, ImageWidget>)typeof(CustomCommandBarWidget)
				.GetField("icons", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(commandBar)!;
			var visibleOrder = CommandBarLayoutPolicy.NormalizeVisibleOrder(
				CommandBarCatalog.DefaultVisibleIds(), true, 1).ToList();

			foreach (var id in visibleOrder)
			{
				var button = CreateSlot(id, out var label, out var icon);
				slots.AddChild(button);
				buttons.Add(id, button);
				labels.Add(id, label);
				icons.Add(id, icon);
			}

			var edit = CreateSlot("EDIT_TOGGLE", out _, out _);
			var collapse = CreateSlot("COLLAPSE_TOGGLE", out _, out var collapseIcon);
			commandBar.AddChild(edit);
			commandBar.AddChild(collapse);
			SetPrivateField(commandBar, "editButton", edit);
			SetPrivateField(commandBar, "collapseButton", collapse);
			SetPrivateField(commandBar, "collapseIcon", collapseIcon);
			SetPrivateField(commandBar, "visibleOrder", visibleOrder);
			SetPrivateField(commandBar, "expanded", true);

			var apply = typeof(CustomCommandBarWidget).GetMethod(
				"ApplyTouchLayout", BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.That(apply, Is.Not.Null);
			apply!.Invoke(commandBar, new object[] { snapshot, false, null });

			var sidebar = IosIngameSidebarLayoutPolicy.Create(snapshot).TopBounds;
			Assert.Multiple(() =>
			{
				Assert.That(commandBar.RenderBounds.Right, Is.LessThanOrEqualTo(sidebar.Left),
					"The complete quickbar background must stop before the production sidebar.");
				Assert.That(collapse.RenderBounds.Right, Is.LessThanOrEqualTo(sidebar.Left),
					"The later-rendered sidebar must never cover or capture the collapse toggle.");
				Assert.That(collapse.RenderBounds.Right,
					Is.LessThanOrEqualTo(buttons[visibleOrder[0]].RenderBounds.Left),
					"Touch commands must start after the pinned collapse control.");
			});
		}

		[TestCase(0, false, false)]
		[TestCase(0, true, true)]
		[TestCase(1, false, false)]
		[TestCase(1, true, true)]
		[TestCase(2, false, false)]
		[TestCase(2, true, true)]
		[TestCase(3, false, false)]
		[TestCase(3, true, true)]
		[TestCase(4, false, false)]
		[TestCase(4, true, true)]
		[TestCase(5, false, false)]
		[TestCase(5, true, true)]
		[TestCase(6, false, false)]
		[TestCase(6, true, true)]
		public void LiveTouchLayoutUsesOnePhysicalRowAndKeepsRequiredControlsInsideSafeArea(
			int deviceIndex, bool expanded, bool editing)
		{
			var device = Devices[deviceIndex];
			var snapshot = new IosScreenSnapshot(device.Renderer, device.Native, device.Insets);
			var commandBar = new CustomCommandBarWidget();
			var slots = new ContainerWidget { Id = "COMMAND_SLOTS" };
			commandBar.AddChild(slots);
			var buttons = (Dictionary<string, ButtonWidget>)typeof(CustomCommandBarWidget)
				.GetField("buttons", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(commandBar)!;
			var labels = (Dictionary<string, LabelWidget>)typeof(CustomCommandBarWidget)
				.GetField("labels", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(commandBar)!;
			var icons = (Dictionary<string, ImageWidget>)typeof(CustomCommandBarWidget)
				.GetField("icons", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(commandBar)!;

			foreach (var id in new[] { "STOP", "CYCLE_BASE", "SCATTER", "REPAIR", "SELL" })
			{
				var button = CreateSlot(id, out var label, out var icon);
				slots.AddChild(button);
				buttons.Add(id, button);
				labels.Add(id, label);
				icons.Add(id, icon);
			}

			var edit = CreateSlot("EDIT_TOGGLE", out _, out _);
			var collapse = CreateSlot("COLLAPSE_TOGGLE", out _, out var collapseIcon);
			commandBar.AddChild(edit);
			commandBar.AddChild(collapse);
			SetPrivateField(commandBar, "editButton", edit);
			SetPrivateField(commandBar, "collapseButton", collapse);
			SetPrivateField(commandBar, "collapseIcon", collapseIcon);
			var visibleOrder = new List<string> { "STOP", "CYCLE_BASE" };
			if (!expanded && !editing)
				visibleOrder.AddRange(new[] { "REPAIR", "SELL" });
			SetPrivateField(commandBar, "visibleOrder", visibleOrder);
			SetPrivateField(commandBar, "expanded", expanded);

			var apply = typeof(CustomCommandBarWidget).GetMethod(
				"ApplyTouchLayout", BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.That(apply, Is.Not.Null, "The production widget needs an executable touch-layout seam.");
			apply!.Invoke(commandBar, new object[] { snapshot, editing, null });

			var buttonPoints = snapshot.IsCompactPhone ? 46 : 56;
			var rowHeight = snapshot.LogicalPoints(buttonPoints);
			Assert.Multiple(() =>
			{
				Assert.That(snapshot.SafeBounds.Contains(commandBar.RenderBounds), Is.True);
				Assert.That(collapse.Bounds.Height, Is.EqualTo(rowHeight));
				Assert.That(snapshot.SafeBounds.Contains(collapse.RenderBounds), Is.True);
				Assert.That(edit.Bounds.Height, Is.EqualTo(rowHeight));
				Assert.That(edit.IsVisible(), Is.EqualTo(expanded));
				if (expanded)
					Assert.That(snapshot.SafeBounds.Contains(edit.RenderBounds), Is.True);
			});

			if (!expanded)
			{
				Assert.That(buttons.Values.All(button => !button.IsVisible()), Is.True);
				Assert.That(slots.IsVisible(), Is.False);
				Assert.That(collapse.IsVisible(), Is.True);
				Assert.That(commandBar.Bounds.Width, Is.EqualTo(collapse.Bounds.Width + 2 * snapshot.LogicalPoints(6)));
				return;
			}

			foreach (var id in new[] { "STOP", "CYCLE_BASE" })
			{
				var button = buttons[id];
				Assert.Multiple(() =>
				{
					Assert.That(button.IsVisible(), Is.True, id);
					Assert.That(button.Bounds.Height, Is.EqualTo(rowHeight), id);
					Assert.That(snapshot.SafeBounds.Contains(button.RenderBounds), Is.True, id);
					Assert.That(labels[id].IsVisible(), Is.False, id);
					Assert.That(labels[id].Font, Is.EqualTo("TinyBold"), id);
					Assert.That(button.RenderBounds.Contains(icons[id].RenderBounds), Is.True, id);
					Assert.That(icons[id].StretchToFit, Is.True, id);
				});
			}

			if (editing)
			{
				Assert.Multiple(() =>
				{
					Assert.That(buttons["SCATTER"].IsVisible(), Is.True);
					Assert.That(buttons["SCATTER"].Bounds.Height, Is.EqualTo(rowHeight));
					Assert.That(buttons["SCATTER"].Bounds.Y, Is.LessThan(buttons["STOP"].Bounds.Y));
					Assert.That(snapshot.SafeBounds.Contains(buttons["SCATTER"].RenderBounds), Is.True);
				});
			}

		}

		[Test]
		public void RequiredTouchSlotsKeepTheirExistingLabelsAndIconBindings()
		{
			var layout = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "mods", "ra2", "chrome", "ingame-player.yaml"));
			foreach (var (id, collection, icon, label) in new[]
			{
				("STOP", "ios-commandbar-glyphs", "stop", "button-command-bar-stop.label"),
				("CYCLE_BASE", "ios-commandbar-glyphs", "cycle-base", "button-selection-bar-cycle-base.label"),
			})
			{
				var start = layout.IndexOf($"Button@{id}:", StringComparison.Ordinal);
				var end = layout.IndexOf("\n\t\t\t\t\t\tButton@", start + 1, StringComparison.Ordinal);
				var block = layout.Substring(start, end - start);
				Assert.Multiple(() =>
				{
					Assert.That(start, Is.GreaterThanOrEqualTo(0));
					StringAssert.Contains("ImageCollection: " + collection, block);
					StringAssert.Contains("ImageName: " + icon, block);
					StringAssert.Contains("Text: " + label, block);
				});
			}
		}

		[Test]
		public void CycleBasePolicyIsStatelessPrimaryFirstThenActorIdAndWraps()
		{
			var candidates = new[] { (ActorId: 9u, IsPrimary: false), (ActorId: 7u, IsPrimary: true), (ActorId: 3u, IsPrimary: false) };
			var ordered = BaseCyclePolicy.Order(candidates, candidate => candidate.ActorId, candidate => candidate.IsPrimary);

			Assert.Multiple(() =>
			{
				Assert.That(ordered.Select(candidate => candidate.ActorId), Is.EqualTo(new[] { 7u, 3u, 9u }));
				Assert.That(BaseCyclePolicy.Next(ordered, _ => false).ActorId, Is.EqualTo(7u));
				Assert.That(BaseCyclePolicy.Next(ordered, candidate => candidate.ActorId == 7u).ActorId, Is.EqualTo(3u));
				Assert.That(BaseCyclePolicy.Next(ordered, candidate => candidate.ActorId == 9u).ActorId, Is.EqualTo(7u));
			});
		}

		[Test]
		public void CycleBaseResolveUsesOrderedCycleBeforeFallback()
		{
			var firstBase = new object();
			var secondBase = new object();
			var fallback = new object();

			var resolved = BaseCyclePolicy.Resolve(
				new[] { firstBase, secondBase }, new[] { fallback }, candidate => candidate == firstBase);

			Assert.That(resolved, Is.SameAs(secondBase));
		}

		[Test]
		public void CycleBaseResolveReturnsFirstFallbackWhenNoBases()
		{
			var firstFallback = new object();
			var secondFallback = new object();

			var resolved = BaseCyclePolicy.Resolve(
				Array.Empty<object>(), new[] { firstFallback, secondFallback }, _ => false);

			Assert.That(resolved, Is.SameAs(firstFallback));
		}

		[Test]
		public void CycleBaseResolveReturnsNullWhenNoCandidateExists()
		{
			var resolved = BaseCyclePolicy.Resolve(
				Array.Empty<object>(), Array.Empty<object>(), _ => false);

			Assert.That(resolved, Is.Null);
		}

		[Test]
		public void CycleBaseExecuteRunsSelectCenterAndClickOnceInOrder()
		{
			var candidate = new object();
			var calls = new List<string>();
			var selectCount = 0;
			var centerCount = 0;
			var clickCount = 0;

			var executed = BaseCyclePolicy.Execute(
				candidate,
				selected =>
				{
					Assert.That(selected, Is.SameAs(candidate));
					selectCount++;
					calls.Add("select");
				},
				() =>
				{
					centerCount++;
					calls.Add("center");
				},
				() =>
				{
					clickCount++;
					calls.Add("click");
				});

			Assert.Multiple(() =>
			{
				Assert.That(executed, Is.True);
				Assert.That(selectCount, Is.EqualTo(1));
				Assert.That(centerCount, Is.EqualTo(1));
				Assert.That(clickCount, Is.EqualTo(1));
				Assert.That(calls, Is.EqualTo(new[] { "select", "center", "click" }));
			});
		}

		[Test]
		public void CycleBaseExecuteHasNoSideEffectsWhenCandidateIsNull()
		{
			var selectCount = 0;
			var centerCount = 0;
			var clickCount = 0;

			var executed = BaseCyclePolicy.Execute<object>(
				null, _ => selectCount++, () => centerCount++, () => clickCount++);

			Assert.Multiple(() =>
			{
				Assert.That(executed, Is.False);
				Assert.That(selectCount, Is.Zero);
				Assert.That(centerCount, Is.Zero);
				Assert.That(clickCount, Is.Zero);
			});
		}

		[Test]
		public void CycleBaseProductionPathConsumesTheStatelessPolicyAndPrimaryTrait()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "OpenRA.Mods.RA2", "Widgets", "Logic",
				"SelectionCommandBarLogic.cs"));

			StringAssert.Contains("BaseCyclePolicy.Order(", source);
			StringAssert.Contains("var nextBase = BaseCyclePolicy.Resolve(", source);
			StringAssert.Contains("BaseCyclePolicy.Execute(nextBase,", source);
			StringAssert.Contains("a => a.ActorID", source);
			StringAssert.Contains("TraitOrDefault<PrimaryBuilding>()?.IsPrimary == true", source);
			StringAssert.DoesNotContain("lastBase", source);
		}

		[Test]
		public void BottomCommandBarExposesProductionBatchToggle()
		{
			var root = RepositoryRoot();
			var layout = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome", "ingame-player.yaml"));
			var chrome = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome.yaml"));
			var catalog = File.ReadAllText(Path.Combine(root, "OpenRA.Mods.RA2", "Widgets", "CommandBarPreferences.cs"));
			var logic = File.ReadAllText(Path.Combine(root, "OpenRA.Mods.RA2", "Widgets", "Logic", "ProductionBatchToggleLogic.cs"));

			StringAssert.Contains("Button@PRODUCTION_X5:", layout);
			StringAssert.Contains("Logic: ProductionBatchToggleLogic", layout);
			StringAssert.Contains("Text: button-command-bar-production-x5.short", layout);
			StringAssert.Contains("ImageCollection: ios-commandbar-glyphs", layout);
			StringAssert.Contains("ImageName: production-x5", layout);
			StringAssert.Contains("production-x5: 163, 3, 26, 26", chrome);
			StringAssert.Contains("new(\"PRODUCTION_X5\", 48, true)", catalog);
			StringAssert.Contains("BuildCountMultiplier = enabled ? 5 : 1", logic);
			Assert.That(File.Exists(Path.Combine(root, "mods", "ra2", "uibits", "ios-commandbar-glyphs-unified.png")), Is.True);
		}

		[Test]
		public void BottomCommandBarExposesTouchCtrlToggleAndScopesItToBattlefieldInput()
		{
			var root = RepositoryRoot();
			var layout = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome", "ingame-player.yaml"));
			var catalog = File.ReadAllText(Path.Combine(root, "OpenRA.Mods.RA2", "Widgets", "CommandBarPreferences.cs"));
			var selection = File.ReadAllText(Path.Combine(root, "OpenRA.Mods.RA2", "Widgets", "Logic",
				"SelectionCommandBarLogic.cs"));
			var battlefield = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets",
				"WorldInteractionControllerWidget.cs"));
			var production = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets",
				"ProductionPaletteWidget.cs"));

			StringAssert.Contains("Button@CTRL_TOGGLE:", layout);
			StringAssert.Contains("Text: button-command-bar-touch-ctrl.short", layout);
			StringAssert.Contains("new(\"CTRL_TOGGLE\", 48, false)", catalog);
			StringAssert.Contains("TouchModifierOverride.Toggle()", selection);
			StringAssert.Contains("TouchModifierOverride.Apply(mi.Modifiers", battlefield);
			StringAssert.Contains("TouchModifierOverride.Apply(Game.GetModifierKeys()", battlefield);
			StringAssert.DoesNotContain("TouchModifierOverride", production);
		}

		[Test]
		public void ProductionBatchToggleDefersPaletteLookupUntilTheWidgetTreeIsAttached()
		{
			var root = RepositoryRoot();
			var logic = File.ReadAllText(Path.Combine(root, "OpenRA.Mods.RA2", "Widgets", "Logic", "ProductionBatchToggleLogic.cs"));

			StringAssert.DoesNotContain("var palette = Ui.Root.Get<ProductionPaletteWidget>", logic);
			StringAssert.Contains("Ui.Root.GetOrNull<ProductionPaletteWidget>", logic);
		}

		[Test]
		public void BottomCommandBarUsesGeneratedCollapseAndExpandIcons()
		{
			var root = RepositoryRoot();
			var layout = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome", "ingame-player.yaml"));
			var chrome = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome.yaml"));
			var widget = File.ReadAllText(Path.Combine(root, "OpenRA.Mods.RA2", "Widgets", "CustomCommandBarWidget.cs"));
			var image = Path.Combine(root, "mods", "ra2", "uibits", "commandbar-toggle-icons.png");

			StringAssert.Contains("ImageCollection: commandbar-toggle-icons", layout);
			StringAssert.Contains("ImageName: collapse", layout);
			StringAssert.Contains("collapse: 3, 3, 26, 26", chrome);
			StringAssert.Contains("expand: 35, 3, 26, 26", chrome);
			StringAssert.Contains("expanded ? \"collapse\" : \"expand\"", widget);
			StringAssert.Contains("collapseIcon.Bounds = new WidgetBounds", widget);
			Assert.That(File.Exists(image), Is.True);
			Assert.That(PngSize(image), Is.EqualTo((64, 32)));
		}

		[Test]
		public void ProductionBatchToggleUsesTheSameFactionChromeAsItsNeighbours()
		{
			var root = RepositoryRoot();
			var layout = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome", "ingame-player.yaml"));

			StringAssert.Contains("Logic: ProductionBatchToggleLogic, AddFactionSuffixLogic", layout,
				"The X5 slot must use the same faction button chrome as its neighbours.");
		}

		[Test]
		public void BottomCommandBarUsesOneGeneratedArmoredTray()
		{
			var root = RepositoryRoot();
			var chrome = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome.yaml"));
			var atlas = Path.Combine(root, "mods", "ra2", "uibits", "commandbar-soviet-panel.png");

			StringAssert.Contains("commandbar-panel:\n\tImage: commandbar-soviet-panel.png", chrome);
			StringAssert.Contains("commandbar-panel-compact:\n\tImage: commandbar-soviet-panel.png", chrome);
			Assert.That(File.Exists(atlas), Is.True);
			Assert.That(PngSize(atlas), Is.EqualTo((1024, 256)),
				"The generated tray atlas must remain power-of-two for iOS/OpenGL uploads.");
		}

		[Test]
		public void UnexploredTerrainDoesNotOpenAWorldTooltip()
		{
			Assert.That(WorldTooltipVisibilityPolicy.ShouldShow(WorldTooltipType.Unexplored), Is.False);
			Assert.That(WorldTooltipVisibilityPolicy.ShouldShow(WorldTooltipType.Actor), Is.True);
			Assert.That(WorldTooltipVisibilityPolicy.ShouldShow(WorldTooltipType.Resource), Is.True);
		}
	}
}
