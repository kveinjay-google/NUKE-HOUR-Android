from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[2]
AUTHORIZED_SUPPORTERS = ("www", "阿顿", "1***r@163.com", "Yung")


class SupporterCreditsTest(unittest.TestCase):
    def test_authorized_supporters_are_in_shared_game_credits(self):
        authors = (ROOT / "mods/ra2/AUTHORS").read_text(encoding="utf-8")

        self.assertIn("NUKE HOUR supporters", authors)
        for supporter in AUTHORIZED_SUPPORTERS:
            self.assertEqual(1, authors.splitlines().count(f"* {supporter}"))

        supporter_block = authors.split(
            "NUKE HOUR supporters (publication permission confirmed):\n", 1
        )[1].split("\n\n", 1)[0]
        self.assertEqual(
            [f"* {supporter}" for supporter in AUTHORIZED_SUPPORTERS],
            supporter_block.splitlines(),
        )


if __name__ == "__main__":
    unittest.main()
