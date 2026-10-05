#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class IosProductionCancellationTest
	{
		const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		Player localPlayer;
		ProductionPaletteWidget palette;

		static T Uninitialized<T>() where T : class => (T)FormatterServices.GetUninitializedObject(typeof(T));

		static void SetField(object instance, string name, object value) =>
			instance.GetType().GetField(name, Fields).SetValue(instance, value);

		static void SetProperty(object instance, string name, object value) =>
			SetField(instance, $"<{name}>k__BackingField", value);

		[SetUp]
		public void SetUp()
		{
			localPlayer = Uninitialized<Player>();
			var world = Uninitialized<World>();
			SetProperty(world, "LocalPlayer", localPlayer);
			palette = Uninitialized<ProductionPaletteWidget>();
			SetField(palette, "World", world);
			SetField(palette, "currentQueues", new List<ProductionQueue>());
		}

		ProductionQueue Queue(params string[] names)
		{
			var actor = Uninitialized<Actor>();
			SetProperty(actor, "Owner", localPlayer);
			SetProperty(actor, "IsInWorld", true);
			var queue = Uninitialized<ProductionQueue>();
			SetProperty(queue, "Actor", actor);
			var items = new List<ProductionItem>();
			foreach (var name in names)
			{
				var item = Uninitialized<ProductionItem>();
				SetField(item, "Item", name);
				SetField(item, "Queue", queue);
				items.Add(item);
			}

			SetField(queue, "Queue", items);
			return queue;
		}

		void Category(params ProductionQueue[] queues) =>
			SetField(palette, "currentQueues", new List<ProductionQueue>(queues));

		void Select(ProductionQueue queue, string name)
		{
			SetField(palette, "selectedTouchQueue", queue);
			SetField(palette, "selectedTouchItem", name);
		}

		(ProductionQueue Queue, string Item) Target() =>
			((ProductionQueue Queue, string Item))typeof(ProductionPaletteWidget)
				.GetMethod("CancellationTarget", Fields).Invoke(palette, Array.Empty<object>());

		Order CancellationOrder(ProductionQueue queue, string name)
		{
			var method = typeof(ProductionPaletteWidget).GetMethod("CreateCancellationOrder", Fields);
			Assert.That(method, Is.Not.Null, "Cancellation must validate live queue state before creating an order.");
			return (Order)method.Invoke(palette, new object[] { queue, name });
		}

		[Test]
		public void FooterCancelsFirstCurrentCategoryQueueRegardlessOfLastCameoSelection()
		{
			var vehicle = Queue("tank");
			var aircraft = Queue("plane", "helicopter", "helicopter");
			Category(vehicle, aircraft);
			Select(vehicle, "tank");
			Select(aircraft, "helicopter");
			Assert.That(Target(), Is.EqualTo((vehicle, "tank")));
		}

		[Test]
		public void SelectedQueueOutsideCurrentCategoryCannotBeCancelled()
		{
			var previous = Queue("tank");
			var current = Queue("soldier");
			Category(current);
			Select(previous, "tank");
			Assert.That(Target(), Is.EqualTo((current, "soldier")));
			Assert.That(CancellationOrder(previous, "tank"), Is.Null);
		}

		[Test]
		public void StaleSelectedTypeFallsBackToFirstCurrentQueueItem()
		{
			var empty = Queue();
			var vehicle = Queue("tank", "artillery");
			var aircraft = Queue("plane");
			Category(empty, vehicle, aircraft);
			Select(aircraft, "helicopter");
			Assert.That(Target(), Is.EqualTo((vehicle, "tank")));
			Assert.That(CancellationOrder(aircraft, "helicopter"), Is.Null);
		}

		[TestCase(true)]
		[TestCase(false)]
		public void ForeignOrRemovedQueueCannotBeSelectedOrCancelled(bool foreign)
		{
			var invalid = Queue("tank");
			var current = Queue("plane");
			if (foreign)
				SetProperty(invalid.Actor, "Owner", Uninitialized<Player>());
			else
				SetProperty(invalid.Actor, "IsInWorld", false);
			Category(invalid, current);
			Select(invalid, "tank");
			Assert.That(Target(), Is.EqualTo((current, "plane")));
			Assert.That(CancellationOrder(invalid, "tank"), Is.Null);
		}

		[Test]
		public void EmptyCategoryAndAbsentTypeProduceNoCancellation()
		{
			Assert.That(Target().Queue, Is.Null);
			Assert.That(CancellationOrder(null, "tank"), Is.Null);
			var queue = Queue();
			Category(queue);
			Select(queue, "tank");
			Assert.That(Target().Queue, Is.Null);
			Assert.That(CancellationOrder(queue, "tank"), Is.Null);
		}

		[TestCase(false, false, false)]
		[TestCase(true, false, false)]
		[TestCase(true, true, false)]
		[TestCase(true, false, true)]
		public void QueuedRunningPausedAndCompletedItemsCancelExactlyOne(bool started, bool paused, bool done)
		{
			var queue = Queue("tank", "tank", "tank");
			Category(queue);
			SetProperty(queue.CurrentItem(), "Started", started);
			SetProperty(queue.CurrentItem(), "Paused", paused);
			SetProperty(queue.CurrentItem(), "Done", done);
			var order = CancellationOrder(queue, "tank");
			Assert.That(order.OrderString, Is.EqualTo("CancelProduction"));
			Assert.That(order.Subject, Is.SameAs(queue.Actor));
			Assert.That(order.TargetString, Is.EqualTo("tank"));
			Assert.That(order.ExtraData, Is.EqualTo(1u));
			Assert.That(order.Queued, Is.False);
			Assert.That(queue.AllQueued(), Has.Exactly(3).Items,
				"Creating a synchronized order must not mutate the local production queue.");
		}

		[Test]
		public void OrderCreationRevalidatesSelectionAfterItemDisappears()
		{
			var queue = Queue("tank");
			Category(queue);
			Select(queue, "tank");
			var target = Target();
			((List<ProductionItem>)queue.AllQueued()).Clear();
			Assert.That(CancellationOrder(target.Queue, target.Item), Is.Null);
		}

		[TestCase(false, false, true)]
		[TestCase(true, false, false)]
		[TestCase(false, true, false)]
		public void LongPressRetainsOriginalTypeAndQueueAcrossIconRefresh(bool differentType, bool differentQueue, bool valid)
		{
			var queue = Queue("tank", "plane");
			var other = Queue("tank");
			Category(queue, other);
			var bounds = new Rectangle(100, 100, 50, 50);
			var original = new ProductionIcon { Name = "tank", ProductionQueue = queue };
			var icons = new Dictionary<Rectangle, ProductionIcon> { [bounds] = original };
			SetField(palette, "icons", icons);
			var begin = typeof(ProductionPaletteWidget).GetMethod("BeginTouch");
			Assert.That(begin, Is.Not.Null, "Remember the pressed cameo before producer changes can reflow the grid.");
			begin.Invoke(palette, new object[] { new int2(110, 110) });
			icons[bounds] = new ProductionIcon
			{
				Name = differentType ? "plane" : "tank",
				ProductionQueue = differentQueue ? other : queue
			};
			var target = typeof(ProductionPaletteWidget).GetMethod("LongPressCancellationTarget", Fields);
			Assert.That(target, Is.Not.Null);
			var result = target.Invoke(palette, new object[] { new int2(110, 110), new int2(115, 115) });
			Assert.That(result != null, Is.EqualTo(valid));
		}

		[Test]
		public void LongPressFromEmptyCellOrIntoDifferentCameoCannotCancel()
		{
			var queue = Queue("tank", "plane");
			Category(queue);
			var icons = new Dictionary<Rectangle, ProductionIcon>
			{
				[new Rectangle(100, 100, 50, 50)] = new ProductionIcon { Name = "tank", ProductionQueue = queue },
				[new Rectangle(150, 100, 50, 50)] = new ProductionIcon { Name = "plane", ProductionQueue = queue }
			};
			SetField(palette, "icons", icons);
			var begin = typeof(ProductionPaletteWidget).GetMethod("BeginTouch");
			Assert.That(begin, Is.Not.Null);
			var target = typeof(ProductionPaletteWidget).GetMethod("LongPressCancellationTarget", Fields);
			Assert.That(target, Is.Not.Null);
			begin.Invoke(palette, new object[] { new int2(110, 110) });
			Assert.That(target.Invoke(palette, new object[] { new int2(110, 110), new int2(160, 110) }), Is.Null);
			begin.Invoke(palette, new object[] { new int2(210, 110) });
			icons[new Rectangle(200, 100, 50, 50)] = new ProductionIcon { Name = "tank", ProductionQueue = queue };
			Assert.That(target.Invoke(palette, new object[] { new int2(210, 110), new int2(210, 110) }), Is.Null);
		}
	}
}
