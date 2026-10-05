using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public class CargoSoundsTest
	{
		const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
		static void Set(object target, string name, object value) => target.GetType().GetField(name, Fields).SetValue(target, value);
		sealed class RecordingCargo : Cargo
		{
			public readonly List<string> Sounds = new();
			public RecordingCargo(Actor actor, CargoInfo info) : base(new ActorInitializer(actor, new TypeDictionary()), info) { }
			protected override void PlayCargoSound(Actor actor, string[] sounds) => Sounds.AddRange(sounds);
		}

		[TestCase("allied-naval", "lcrf", "genter1a.wav", "gexit1a.wav")]
		[TestCase("soviet-naval", "sapc", "genter1a.wav", "gexit1a.wav")]
		[TestCase("yuri-naval", "yhvr", "genter1a.wav", "gexit1a.wav")]
		[TestCase("allied-vehicles", "fv", "vifvtran.wav", "vifvtran.wav")]
		[TestCase("allied-vehicles", "bfrt", "genter1a.wav", "gexit1a.wav")]
		[TestCase("soviet-vehicles", "htk", "genter1a.wav", "gexit1a.wav")]
		[TestCase("aircraft", "shad", "genter1a.wav", "gexit1a.wav")]
		[TestCase("civilian-vehicles", "bus", "genter1a.wav", "gexit1a.wav")]
		[TestCase("civilian-vehicles", "limo", "genter1a.wav", "gexit1a.wav")]
		public void TransportUsesRetailTransferSounds(string file, string actor, string enter, string exit)
		{
			var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (root != null && !File.Exists(Path.Combine(root.FullName, "packaging", "nukehour-version.json")))
				root = root.Parent;
			Assert.That(root, Is.Not.Null);
			var nodes = MiniYaml.FromFile(Path.Combine(root.FullName, "mods", "ra2", "rules", file + ".yaml"));
			var unit = nodes.Single(n => n.Key == actor);
			var cargo = unit.Value.Nodes.Single(n => n.Key == "Cargo");
			Assert.That(cargo.Value.Nodes.Single(n => n.Key == "LoadSounds").Value.Value, Is.EqualTo(enter));
			Assert.That(cargo.Value.Nodes.Single(n => n.Key == "UnloadSounds").Value.Value, Is.EqualTo(exit));
		}

		[TestCase(true, true, false, 2)]
		[TestCase(false, true, false, 0)]
		[TestCase(true, false, false, 0)]
		[TestCase(true, true, true, 0)]
		public void OnlySuccessfulLiveTransfersPlay(bool initialized, bool inWorld, bool disposing, int count)
		{
			var world = (World)FormatterServices.GetUninitializedObject(typeof(World));
			var traits = typeof(World).GetField("TraitDict", Fields);
			traits.SetValue(world, Activator.CreateInstance(traits.FieldType, true));
			Actor Make(uint id)
			{
				var actor = (Actor)FormatterServices.GetUninitializedObject(typeof(Actor));
				Set(actor, "World", world);
				Set(actor, "ActorID", id);
				Set(actor, "Info", new ActorInfo("unit", new MobileInfo(), new PassengerInfo()));
				Set(actor, "<IsInWorld>k__BackingField", inWorld);
				actor.AddTrait(new PassengerInfo().Create(new ActorInitializer(actor, new TypeDictionary())));
				return actor;
			}

			var carrier = Make(1);
			var passenger = Make(2);
			var info = new CargoInfo();
			Set(info, "MaxWeight", 5);
			Set(info, "LoadSounds", new[] { "enter.wav" });
			Set(info, "UnloadSounds", new[] { "exit.wav" });
			var cargo = new RecordingCargo(carrier, info);
			carrier.AddTrait(cargo);
			typeof(Cargo).GetField("initialised", Fields).SetValue(cargo, initialized);
			Assert.That(cargo.Load(carrier, passenger), Is.True);
			Assert.That(cargo.Load(carrier, passenger), Is.False);
			Assert.That(cargo.Load(carrier, carrier), Is.False);
			if (disposing)
			{
				cargo.Sounds.Clear();
				Set(carrier, "<WillDispose>k__BackingField", true);
			}

			cargo.Unload(carrier);
			Assert.That(cargo.Sounds.Count, Is.EqualTo(count));
			if (count == 2)
				Assert.That(cargo.Sounds, Is.EqualTo(new[] { "enter.wav", "exit.wav" }));
		}
	}
}
