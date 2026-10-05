// Copyright (c) The OpenRA Developers and Contributors
// Licensed under the GNU General Public License v3.
using System;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Widgets.Logic;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class PerformanceUnitCountTest
	{
		const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		static T Empty<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
		static void Set(object target, string name, object value) => target.GetType().GetField(name, Fields).SetValue(target, value);

		[Test]
		public void PerformanceTextIncludesCurrentTotalSeparatelyFromQueues()
		{
			var method = typeof(PerfDebugLogic).GetMethod("FormatPerformanceText");
			var args = new object[] { 60d, 5d, "100 MB", "20 MB", 1, 1d, 1, 1d, 1000, 500, 42 };
			if (method.GetParameters().Length == 10) Array.Resize(ref args, 10);
			Assert.That((string)method.Invoke(null, args), Does.Contain("当前单位总数：42"));
		}

		[Test]
		public void CurrentCountIncludesCargoAndBothOwnersButExcludesDisposedBuildingsAndEffects()
		{
			var world = Empty<World>();
			var traits = typeof(World).GetField("TraitDict", Fields);
			traits.SetValue(world, Activator.CreateInstance(traits.FieldType, true));
			var a = Empty<Player>(); var b = Empty<Player>(); var neutral = Empty<Player>();
			Set(neutral, "NonCombatant", true);
			uint id = 0;
			Actor Add(Player owner, bool counts = true)
			{
				var actor = Empty<Actor>(); Set(actor, "World", world); Set(actor, "ActorID", ++id);
				Set(actor, "<Owner>k__BackingField", owner);
				if (counts) actor.AddTrait(new Population());
				return actor;
			}

			Add(a); var cargo = Add(b); Set(cargo, "<IsInWorld>k__BackingField", false);
			var dead = Add(a); Set(dead, "<Disposed>k__BackingField", true);
			var transforming = Add(b); Set(transforming, "<WillDispose>k__BackingField", true);
			Add(a, false); Add(neutral);
			var count = typeof(PopulationLimits).GetMethod("CountCurrentUnits", BindingFlags.Public | BindingFlags.Static);
			Assert.That(count, Is.Not.Null, "Performance must have a queue-free live unit count.");
			Assert.That(count.Invoke(null, new object[] { world }), Is.EqualTo(2));
		}

		[Test]
		public void CountingIsLimitedToTwicePerSecondAndRefreshesWhenClockRestarts()
		{
			var type = typeof(PerfDebugLogic).Assembly.GetType("OpenRA.Mods.Common.Widgets.Logic.PerformanceUnitCountCache");
			Assert.That(type, Is.Not.Null, "Counting on every rendered frame would add avoidable world scans.");
			var calls = 0;
			var cache = Activator.CreateInstance(type, new object[] { (Func<int>)(() => ++calls) });
			var sample = type.GetMethod("Sample");
			Assert.That(sample.Invoke(cache, new object[] { 0L }), Is.EqualTo(1));
			for (var i = 1; i < 500; i++) Assert.That(sample.Invoke(cache, new object[] { (long)i }), Is.EqualTo(1));
			Assert.That(sample.Invoke(cache, new object[] { 500L }), Is.EqualTo(2));
			Assert.That(sample.Invoke(cache, new object[] { 100L }), Is.EqualTo(3));
		}
	}
}
