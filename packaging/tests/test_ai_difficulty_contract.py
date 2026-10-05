import importlib.util
import json
import pathlib
import tempfile
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[2]


def load_ai_profiles_module():
	spec = importlib.util.spec_from_file_location("ai_profiles_difficulty", ROOT / "ai_profiles.py")
	module = importlib.util.module_from_spec(spec)
	spec.loader.exec_module(module)
	return module


class AiDifficultyContractTest(unittest.TestCase):
	OLD_CADENCE = {
		"profturtle": (1.2, 6, 60),
		"profbalanced": (1.0, 5, 24),
		"profrush": (0.6, 2, 6),
	}

	def test_every_builtin_profile_is_at_least_two_and_a_half_times_more_responsive(self):
		ai_profiles = load_ai_profiles_module()
		profiles = {profile["id"]: ai_profiles.normalize_profile(profile)
			for profile in ai_profiles.BUILTIN_PRESETS}

		self.assertEqual(set(profiles), set(self.OLD_CADENCE))
		for profile_id, (old_active, old_inactive, old_rush) in self.OLD_CADENCE.items():
			with self.subTest(profile=profile_id):
				profile = profiles[profile_id]
				self.assertLessEqual(profile["build_active_delay_s"], old_active * 0.4)
				self.assertLessEqual(profile["build_inactive_delay_s"], old_inactive * 0.4)
				self.assertLessEqual(profile["rush_interval_s"], old_rush * 0.4)
				self.assertLessEqual(profile["unit_prod_interval_s"], 0.4)
				self.assertGreaterEqual(profile["barracks_limit"], 4)
				self.assertGreaterEqual(profile["weap_limit"], 4)
				self.assertGreaterEqual(profile["airpad_limit"], 3)
				self.assertGreaterEqual(profile["naval_limit"], 2)

	def test_generator_emits_the_complete_strength_contract(self):
		ai_profiles = load_ai_profiles_module()
		for profile in ai_profiles.BUILTIN_PRESETS:
			profile = ai_profiles.normalize_profile(profile)
			yaml = ai_profiles._profile_yaml(profile)
			with self.subTest(profile=profile["id"]):
				self.assertIn("\t\tMinOrderQuotientPerTick: 2\n", yaml)
				self.assertEqual(yaml.count("MinimumScanTimeInterval: 100"), 2)
				self.assertEqual(yaml.count("MaximumScanTimeInterval: 105"), 2)
				self.assertIn("\t\tStructureProductionRandomBonusDelay: 4\n", yaml)
				self.assertIn(
					f"\t\tNewProductionCashThreshold: {profile['new_production_cash_threshold']}\n", yaml)
				self.assertIn(f"\t\t\tgapile: {profile['barracks_limit']}\n", yaml)
				self.assertIn(f"\t\t\tgaweap: {profile['weap_limit']}\n", yaml)
				self.assertIn(f"\t\t\tgaairc: {profile['airpad_limit']}\n", yaml)
				self.assertIn(f"\t\t\tnayard: {profile['naval_limit']}\n", yaml)
				self.assertIn("\t\tAssignRolesInterval: 20\n", yaml)
				self.assertIn("\t\tAttackForceInterval: 30\n", yaml)
				self.assertIn(
					f"\t\tUnitProductionInterval: {ai_profiles._ticks(profile['unit_prod_interval_s'])}\n", yaml)

	def test_version_one_builtin_profiles_are_migrated_without_touching_custom_profiles(self):
		ai_profiles = load_ai_profiles_module()
		with tempfile.TemporaryDirectory() as temp_dir:
			profile_path = pathlib.Path(temp_dir) / "profiles.json"
			output_path = pathlib.Path(temp_dir) / "ai-profiles.yaml"
			profile_path.write_text(json.dumps({
				"version": 1,
				"profiles": [
					{"id": "profbalanced", "name": "旧均衡", "builtin": True,
					 "build_active_delay_s": 1.0, "build_inactive_delay_s": 5,
					 "rush_interval_s": 24},
					{"id": "profcustom", "name": "我的自定义", "builtin": False,
					 "build_active_delay_s": 3.0, "rush_interval_s": 90},
				],
			}), encoding="utf-8")

			old_profile_path = ai_profiles.PROFILES_JSON
			old_output_path = ai_profiles.OUTPUT_YAML
			try:
				ai_profiles.PROFILES_JSON = str(profile_path)
				ai_profiles.OUTPUT_YAML = str(output_path)
				profiles = ai_profiles.load_profiles()
			finally:
				ai_profiles.PROFILES_JSON = old_profile_path
				ai_profiles.OUTPUT_YAML = old_output_path

			by_id = {profile["id"]: profile for profile in profiles}
			self.assertEqual(by_id["profbalanced"]["build_active_delay_s"], 0.4)
			self.assertEqual(by_id["profcustom"]["build_active_delay_s"], 3.0)
			self.assertEqual(by_id["profcustom"]["rush_interval_s"], 90)
			self.assertEqual(json.loads(profile_path.read_text(encoding="utf-8"))["version"], 2)

	def test_standalone_bot_matches_the_balanced_strength_baseline(self):
		yaml = (ROOT / "mods" / "ra2" / "rules" / "ai.yaml").read_text(encoding="utf-8")
		for required in (
			"MinOrderQuotientPerTick: 2",
			"MinimumScanTimeInterval: 100",
			"MaximumScanTimeInterval: 105",
			"StructureProductionActiveDelay: 10",
			"StructureProductionInactiveDelay: 50",
			"StructureProductionRandomBonusDelay: 4",
			"NewProductionCashThreshold: 2000",
			"AssignRolesInterval: 20",
			"RushInterval: 240",
			"AttackForceInterval: 30",
			"UnitProductionInterval: 10",
			"MinimumConstructionYardCount: 2",
		):
			with self.subTest(required=required):
				self.assertIn(required, yaml)


if __name__ == "__main__":
	unittest.main()
