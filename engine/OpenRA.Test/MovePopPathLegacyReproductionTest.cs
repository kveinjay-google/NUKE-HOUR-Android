// Copyright (c) The OpenRA Developers and Contributors
// This file is part of OpenRA, licensed under the GNU General Public License v3.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.HitShapes;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class MovePopPathLegacyReproductionTest
	{
		const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
		static T Empty<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
		static void Set(object target, string field, object value) => target.GetType().GetField(field, Fields).SetValue(target, value);
		delegate ((CPos Cell, SubCell SubCell)? Next, bool ShouldTryAgain) Pop(Actor self);

		[Test, Explicit("Known legacy Move.PopPath bug: after repathing it asks for the old blocked cell's subcell. No Move change is authorized in this performance stage.")]
		public void RepathChoosesTheReplacementCellsAvailableSubCell()
		{
			// Execute real Move, Mobile, Locomotor, ActorMap influence, and cell blocking logic.
			// Supply only the state otherwise populated by the full game/world loader.
			var world = Empty<World>();
			var map = Empty<Map>();
			Set(map, "Grid", new MapGrid(new MiniYaml(null)));
			Set(map, "<MapSize>k__BackingField", new int2(10, 10));
			Set(map, "Bounds", new Rectangle(0, 0, 10, 10));
			var shape = new HitShapeInfo();
			Set(shape, "Type", new CircleShape());
			var rules = Empty<Ruleset>();
			Set(rules, "Actors", new ActorInfoDictionary(new Dictionary<string, ActorInfo>
			{
				{ "unit", new ActorInfo("unit", shape) }
			}));
			Set(map, "<Rules>k__BackingField", rules);
			Set(world, "Map", map);
			var traitsField = typeof(World).GetField("TraitDict", Fields);
			traitsField.SetValue(world, Activator.CreateInstance(traitsField.FieldType, true));
			var actorMap = new ActorMap(world, new ActorMapInfo());
			Set(world, "ActorMap", actorMap);
			var owner = Empty<Player>();
			owner.PlayerMask = owner.AlliedPlayersMask = new LongBitSet<PlayerBitMask>("move-repath-test-owner");
			world.AllPlayersMask = owner.PlayerMask;

			var locomotorInfo = new LocomotorInfo();
			var locomotor = Empty<Locomotor>();
			Set(locomotor, "Info", locomotorInfo);
			Set(locomotor, "world", world);
			Set(locomotor, "actorMap", actorMap);
			Set(locomotor, "dirtyCells", new HashSet<CPos>());
			var costs = new CellLayer<short>(map);
			costs.Clear(100);
			Set(locomotor, "cellsCost", new[] { costs });
			var blockingField = typeof(Locomotor).GetField("blockingCache", Fields);
			var layerType = blockingField.FieldType.GetElementType();
			var blockingLayers = Array.CreateInstance(layerType, 1);
			blockingLayers.SetValue(Activator.CreateInstance(layerType, new object[] { map }), 0);
			blockingField.SetValue(locomotor, blockingLayers);
			var mobileInfo = new MobileInfo();
			Set(mobileInfo, "<LocomotorInfo>k__BackingField", locomotorInfo);
			Set(mobileInfo, "locomotor", locomotor);

			Actor Unit(uint id, CPos cell)
			{
				var actor = Empty<Actor>();
				Set(actor, "World", world);
				Set(actor, "ActorID", id);
				Set(actor, "Info", new ActorInfo("unit"));
				Set(actor, "<Owner>k__BackingField", owner);
				Set(actor, "<IsInWorld>k__BackingField", true);
				Set(actor, "crushables", Array.Empty<ICrushable>());
				var mobile = Empty<Mobile>();
				Set(mobile, "Info", mobileInfo);
				Set(mobile, "self", actor);
				Set(mobile, "<FromCell>k__BackingField", cell);
				Set(mobile, "<ToCell>k__BackingField", cell);
				Set(mobile, "<Locomotor>k__BackingField", locomotor);
				mobile.FromSubCell = mobile.ToSubCell = SubCell.FullCell;
				Set(mobile, "<CenterPosition>k__BackingField", new WPos(cell.X * 1024 + 512, cell.Y * 1024 + 512, 0));
				Set(actor, "<OccupiesSpace>k__BackingField", mobile);
				actorMap.AddInfluence(actor, mobile);
				return actor;
			}

			var oldNext = new CPos(5, 4);
			var newNext = new CPos(4, 5);
			var destination = new CPos(7, 7);
			var self = Unit(1, new CPos(4, 4));
			var blocker = Unit(2, oldNext);
			Assert.That(((Mobile)blocker.OccupiesSpace).IsLeaving(), Is.False);
			var updateBlocking = typeof(Locomotor).GetMethod("UpdateCellBlocking", Fields);
			updateBlocking.Invoke(locomotor, new object[] { oldNext });
			updateBlocking.Invoke(locomotor, new object[] { newNext });
			var mobileSelf = (Mobile)self.OccupiesSpace;
			Assert.That(mobileSelf.CanEnterCell(oldNext, check: BlockedByActor.Immovable), Is.True);
			Assert.That(mobileSelf.CanEnterCell(oldNext, check: BlockedByActor.All), Is.False);
			Assert.That(mobileSelf.CanEnterCell(newNext), Is.True);
			Assert.That(mobileSelf.GetAvailableSubCell(newNext), Is.EqualTo(SubCell.FullCell));

			var pathCalls = 0;
			var move = new Move(self, check =>
			{
				pathCalls++;
				Assert.That(check, Is.EqualTo(BlockedByActor.All));
				return (false, new List<CPos> { destination, newNext });
			});
			Set(move, "path", new List<CPos> { destination, oldNext });
			Set(move, "destination", (CPos?)destination);
			Set(move, "hasWaited", true);
			Set(move, "waitTicksRemaining", 0);
			var pop = (Pop)typeof(Move).GetMethod("PopPath", Fields).CreateDelegate(typeof(Pop), move);
			var result = pop(self);
			Assert.That(pathCalls, Is.EqualTo(1));
			Assert.That(result.ShouldTryAgain, Is.True);
			Assert.That(result.Next.HasValue, Is.True);
			Assert.That(result.Next.Value.Cell, Is.EqualTo(newNext));
			Assert.That(result.Next.Value.SubCell, Is.EqualTo(SubCell.FullCell),
				"The replacement cell is free, but legacy PopPath asks oldNext (blocked), returning SubCell.Invalid.");
		}
	}
}
