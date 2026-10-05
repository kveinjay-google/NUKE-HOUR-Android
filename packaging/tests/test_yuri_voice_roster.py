import pathlib
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[2]
VOICE_RULES = ROOT / "mods" / "ra2" / "audio" / "voices.yaml"


def top_level_block(path: pathlib.Path, name: str) -> str:
    text = path.read_text(encoding="utf-8")
    marker = f"{name}:\n"
    start = text.find(marker)
    if start < 0:
        return ""

    end = text.find("\n\n", start)
    return text[start:] if end < 0 else text[start:end]


class YuriVoiceRosterTests(unittest.TestCase):
    def test_every_yuri_unit_uses_its_dedicated_voice_set(self):
        expected = {
            "rules/yuri-infantry.yaml": {
                "init": "InitiateVoice",
                "brute": "BruteVoice",
                "virus": "VirusVoice",
                "slav": "SlaveVoice",
            },
            "rules/soviet-infantry.yaml": {
                "yuripr": "YuriPrimeVoice",
            },
            "rules/yuri-vehicles.yaml": {
                "pcv": "YuriConstructionVehicleVoice",
                "smin": "SlaveMinerVoice",
                "ltnk": "LasherTankVoice",
                "ytnk": "GatlingTankVoice",
                "caos": "ChaosDroneVoice",
                "tele": "MagnetronVoice",
                "mind": "MasterMindVoice",
                "disk": "FloatingDiskVoice",
            },
            "rules/yuri-naval.yaml": {
                "yhvr": "YuriAmphibiousTransportVoice",
                "bsub": "BoomerVoice",
            },
        }

        for relative, actors in expected.items():
            path = ROOT / "mods" / "ra2" / relative
            for actor, voice_set in actors.items():
                with self.subTest(actor=actor):
                    self.assertIn(
                        f"\t\tVoiceSet: {voice_set}",
                        top_level_block(path, actor),
                    )

    def test_dedicated_voice_sets_reference_the_retail_yuri_audio_prefixes(self):
        expected = {
            "VirusVoice": ("ivirsea", "ivirmoa", "ivirata", "ivirdia"),
            "YuriPrimeVoice": ("iyupsea", "iyupmoa", "iyupata", "iyupdia"),
            "YuriConstructionVehicleVoice": ("vmcysea", "vmcymoa"),
            "ChaosDroneVoice": ("vchasela", "vchamova", "vchaatca"),
            "MagnetronVoice": ("vmagsea", "vmagmoa", "vmagata"),
            "MasterMindVoice": ("vmassea", "vmasmoa", "vmasata", "vmasdib"),
            "FloatingDiskVoice": ("vflosea", "vflomob", "vfloata", "vflodiea"),
            "YuriAmphibiousTransportVoice": ("vhoysea", "vhoymoa"),
            "BoomerVoice": ("vboosea", "vboomoa", "vbooa1a", "vbooa2a"),
        }

        for voice_set, filenames in expected.items():
            with self.subTest(voice_set=voice_set):
                block = top_level_block(VOICE_RULES, voice_set)
                for filename in filenames:
                    self.assertIn(filename, block)

    def test_units_only_request_voice_actions_present_in_their_retail_sets(self):
        pcv = top_level_block(ROOT / "mods" / "ra2" / "rules" / "yuri-vehicles.yaml", "pcv")
        chaos_drone = top_level_block(ROOT / "mods" / "ra2" / "rules" / "yuri-vehicles.yaml", "caos")
        hover_transport = top_level_block(ROOT / "mods" / "ra2" / "rules" / "yuri-naval.yaml", "yhvr")

        self.assertIn("\tGuard:\n\t\tVoice: Move", pcv)
        self.assertIn("\tGrantConditionOnDeploy:\n", chaos_drone)
        self.assertIn("\t\tVoice: Attack", chaos_drone)
        self.assertIn("\tGuard:\n\t\tVoice: Move", hover_transport)


if __name__ == "__main__":
    unittest.main()
