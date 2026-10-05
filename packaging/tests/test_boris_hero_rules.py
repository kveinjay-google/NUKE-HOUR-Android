import pathlib
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[2]


def text(relative: str) -> str:
    return (ROOT / relative).read_text(encoding="utf-8")


def top_level_block(relative: str, name: str) -> str:
    source = text(relative)
    marker = f"{name}:\n"
    start = source.find(marker)
    if start < 0:
        return ""

    end = source.find("\n\n", start)
    return source[start:] if end < 0 else source[start:end]


class BorisHeroRulesTests(unittest.TestCase):
    def test_boris_is_a_complete_single_limit_soviet_hero(self):
        boris = top_level_block("mods/ra2/rules/soviet-infantry.yaml", "boris")
        self.assertIn("\t\tBuildLimit: 1", boris)
        self.assertIn("\t\tPrerequisites: natech, ~nahand", boris)
        self.assertIn("\t\tWeapon: AKM", boris)
        self.assertIn("\t\tWeapon: AKME", boris)
        self.assertIn("\t\tWeapon: BorisFlare", boris)
        self.assertIn("\t\tVoiceSet: BorisVoice", boris)
        self.assertIn("\t\t\tsecondary: shoot-flare", boris)

    def test_boris_uses_retail_yr_assets(self):
        sequence = top_level_block("mods/ra2/sequences/soviet-infantry.yaml", "boris")
        self.assertIn("Filename: conqmd|boris.shp", sequence)
        self.assertIn("Filename: cameomd|brisicon.shp", sequence)

        voice = top_level_block("mods/ra2/audio/voices.yaml", "BorisVoice")
        for sample in ("iborcra", "iborsea", "ibormoa", "iborata", "iborfea", "ibordia"):
            self.assertIn(sample, voice)

        voxel = top_level_block("mods/ra2/sequences/voxels.yaml", "bpln")
        self.assertIn("localmd|bpln", voxel)

    def test_airstrike_and_custom_runtime_are_present(self):
        flare = top_level_block("mods/ra2/weapons/misc.yaml", "BorisFlare")
        self.assertIn("Warhead@Airstrike: SendAirstrike", flare)
        self.assertIn("\t\tUnitType: bpln", flare)
        self.assertIn("\t\tSquadSize: 2", flare)

        mig = top_level_block("mods/ra2/rules/aircraft.yaml", "bpln")
        self.assertIn("\tAttackBomber:", mig)
        self.assertIn("\t\tWeapon: BorisMaverick", mig)
        self.assertIn("\t\tAmmo: 1", mig)
        missile = top_level_block("mods/ra2/weapons/missiles.yaml", "BorisMaverick")
        self.assertIn("\tValidTargets: Ground, Water", missile)
        self.assertIn("Warhead@1Dam: SpreadDamage", missile)
        self.assertIn("class SendAirstrikeWarhead", text("OpenRA.Mods.RA2/Warheads/SendAirstrikeWarhead.cs"))


if __name__ == "__main__":
    unittest.main()
