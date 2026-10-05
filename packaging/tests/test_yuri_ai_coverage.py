import importlib.util
import pathlib
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[2]
BASE_AI = ROOT / "mods" / "ra2" / "rules" / "ai.yaml"
GENERATED_AI = ROOT / "mods" / "ra2" / "rules" / "ai-profiles.yaml"


def load_ai_profiles_module():
	spec = importlib.util.spec_from_file_location("ai_profiles", ROOT / "ai_profiles.py")
	module = importlib.util.module_from_spec(spec)
	spec.loader.exec_module(module)
	return module


class YuriAiCoverageTest(unittest.TestCase):
	maxDiff = None

	def assert_complete_yuri_ai(self, text, source):
		required_lines = {
			"ConstructionYardTypes": "yacnst",
			"RefineryTypes": "yarefn",
			"PowerTypes": "yapowr",
			"BarracksTypes": "yabrck",
			"VehiclesFactoryTypes": "yaweap",
			"ProductionTypes": "yabrck, yaweap",
			"NavalProductionTypes": "yayard",
			"McvTypes": "pcv",
			"McvFactoryTypes": "yaweap",
			"HarvesterTypes": "smin, slav",
			"DefenseTypes": "yaggun, yapsyt",
		}

		for key, expected in required_lines.items():
			matching = [line.strip() for line in text.splitlines() if line.strip().startswith(f"{key}:")]
			self.assertTrue(matching, f"{source}: missing {key}")
			for line in matching:
				values = {value.strip() for value in line.split(":", 1)[1].split(",")}
				for value in expected.split(", "):
					self.assertIn(value, values, f"{source}: {key} is missing {value}: {line}")

		for actor in (
			"yarefn", "yapowr", "yabrck", "yaweap", "yayard", "yadept", "yadome", "yatech",
			"yaggun", "yapsyt", "init", "brute", "virus", "yurix", "smin", "ltnk", "ytnk",
			"caos", "tele", "mind", "disk", "yhvr", "bsub",
		):
			self.assertIn(f"\n\t\t\t{actor}:", text, f"{source}: AI never schedules {actor}")

		for protected in ("yacnst", "yarefn", "yaweap", "pcv", "smin", "slav"):
			protection_lines = [line for line in text.splitlines() if "ProtectionTypes:" in line]
			self.assertTrue(
				all(protected in line.split(":", 1)[1].replace(" ", "").split(",") for line in protection_lines),
				f"{source}: every AI profile must protect {protected}",
			)

	def test_base_ai_supports_complete_yuri_build_chain(self):
		self.assert_complete_yuri_ai(BASE_AI.read_text(encoding="utf-8"), BASE_AI.name)

	def test_profile_generator_preserves_complete_yuri_build_chain(self):
		ai_profiles = load_ai_profiles_module()
		profile = ai_profiles.normalize_profile(ai_profiles.BUILTIN_PRESETS[0])
		self.assert_complete_yuri_ai(ai_profiles._profile_yaml(profile), "ai_profiles.py")

	def test_checked_in_profiles_support_complete_yuri_build_chain(self):
		self.assert_complete_yuri_ai(GENERATED_AI.read_text(encoding="utf-8"), GENERATED_AI.name)


if __name__ == "__main__":
	unittest.main()
