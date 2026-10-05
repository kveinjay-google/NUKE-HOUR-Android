import pathlib
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[2]
VOXEL_SEQUENCES = ROOT / "mods" / "ra2" / "sequences" / "voxels.yaml"


def actor_sequence(text: str, actor: str) -> str:
    marker = f"{actor}:\n"
    start = text.index(marker)
    end = text.find("\n\n", start)
    return text[start:] if end < 0 else text[start:end]


class LasherTankVoxelSourceTests(unittest.TestCase):
    def test_lasher_tank_uses_yuris_revenge_voxels(self):
        sequence = actor_sequence(VOXEL_SEQUENCES.read_text(encoding="utf-8"), "ltnk")

        self.assertIn("\tidle: localmd|ltnk", sequence)
        self.assertIn("\tturret: localmd|ltnktur", sequence)
        self.assertNotIn("ltnkbarl", sequence)


if __name__ == "__main__":
    unittest.main()
