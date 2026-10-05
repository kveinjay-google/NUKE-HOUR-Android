#region Copyright & License Information
/*
 * This file is part of OpenRA, which is free software. It is made
 * available under the terms of the GNU General Public License as published by
 * the Free Software Foundation, version 3 or any later version.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA2.Widgets
{
	public static class MainMenuSlideSelectionPolicy
	{
		public static int Resolve(
			IReadOnlyList<Rectangle> targets, IReadOnlyList<bool> enabled,
			int2 location, int currentIndex)
		{
			if (targets == null || enabled == null || targets.Count != enabled.Count)
				throw new ArgumentException("Slide-selection targets and enabled states must have matching lengths.");

			for (var i = 0; i < targets.Count; i++)
				if (enabled[i] && targets[i].Contains(location))
					return i;

			return currentIndex;
		}
	}

	/// <summary>
	/// Captures a primary touch above the main-menu buttons and moves the active
	/// selection as the finger slides. Releasing activates the last valid target.
	/// Mouse input keeps using the underlying buttons directly.
	/// </summary>
	public sealed class MainMenuSlideSelectorWidget : Widget
	{
		public bool TouchOnly = true;

		readonly List<ButtonWidget> targets = new();
		readonly List<Rectangle> targetBounds = new();
		readonly List<bool> targetEnabled = new();
		readonly List<(bool Depressed, bool Highlighted)> originalStates = new();
		int selectedIndex = -1;

		public MainMenuSlideSelectorWidget()
		{
			IgnoreMouseOver = true;
		}

		MainMenuSlideSelectorWidget(MainMenuSlideSelectorWidget other)
			: base(other)
		{
			TouchOnly = other.TouchOnly;
			IgnoreMouseOver = true;
		}

		void BeginGesture(MouseInput mi)
		{
			targets.Clear();
			targetBounds.Clear();
			targetEnabled.Clear();
			originalStates.Clear();
			targets.AddRange(Parent.Children.OfType<ButtonWidget>().Where(button => button.IsVisible()));
			foreach (var button in targets)
			{
				targetBounds.Add(button.EventBounds);
				targetEnabled.Add(!button.IsDisabled());
				originalStates.Add((button.Depressed, button.Highlighted));
			}

			selectedIndex = ResolveSelection(mi.Location, -1);
			ApplySelection();
		}

		int ResolveSelection(int2 location, int currentIndex)
		{
			return MainMenuSlideSelectionPolicy.Resolve(targetBounds, targetEnabled, location, currentIndex);
		}

		void ApplySelection()
		{
			for (var i = 0; i < targets.Count; i++)
			{
				var selected = i == selectedIndex;
				targets[i].Depressed = originalStates[i].Depressed || selected;
				targets[i].Highlighted = originalStates[i].Highlighted || selected;
			}
		}

		void ClearSelection()
		{
			for (var i = 0; i < targets.Count && i < originalStates.Count; i++)
			{
				targets[i].Depressed = originalStates[i].Depressed;
				targets[i].Highlighted = originalStates[i].Highlighted;
			}

			targets.Clear();
			targetBounds.Clear();
			targetEnabled.Clear();
			originalStates.Clear();
			selectedIndex = -1;
		}

		static void Activate(ButtonWidget button, MouseInput source)
		{
			var bounds = button.EventBounds;
			var location = new int2(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
			var down = new MouseInput(
				MouseInputEvent.Down, MouseButton.Left, location, int2.Zero, source.Modifiers, source.MultiTapCount);
			var up = new MouseInput(
				MouseInputEvent.Up, MouseButton.Left, location, int2.Zero, source.Modifiers, source.MultiTapCount);
			button.HandleMouseInput(down);
			button.HandleMouseInput(up);
		}

		public override bool YieldMouseFocus(MouseInput mi)
		{
			ClearSelection();
			return base.YieldMouseFocus(mi);
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			if (TouchOnly && !Platform.UsesMobileLayout)
				return false;

			if (!HasMouseFocus)
			{
				if (mi.Event != MouseInputEvent.Down || mi.Button != MouseButton.Left)
					return false;

				BeginGesture(mi);
				if (selectedIndex < 0 || !TakeMouseFocus(mi))
				{
					ClearSelection();
					return false;
				}

				return true;
			}

			if (mi.Event == MouseInputEvent.Cancel)
				return YieldMouseFocus(mi);

			if (mi.Event == MouseInputEvent.Move)
			{
				var next = ResolveSelection(mi.Location, selectedIndex);
				if (next != selectedIndex)
				{
					selectedIndex = next;
					ApplySelection();
				}

				return true;
			}

			if (mi.Event == MouseInputEvent.Up)
			{
				selectedIndex = ResolveSelection(mi.Location, selectedIndex);
				var selected = selectedIndex >= 0 && selectedIndex < targets.Count ? targets[selectedIndex] : null;
				ClearSelection();
				base.YieldMouseFocus(mi);
				if (selected != null && selected.IsVisible() && !selected.IsDisabled())
					Activate(selected, mi);

				return true;
			}

			return true;
		}

		public override Widget Clone() => new MainMenuSlideSelectorWidget(this);
	}
}
