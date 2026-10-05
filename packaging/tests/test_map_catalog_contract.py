import pathlib
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[2]


class MapCatalogContractTest(unittest.TestCase):
	def test_legacy_checkout_maps_are_not_registered_in_the_runtime_catalog(self):
		manifest = (ROOT / "mods" / "ra2" / "mod.yaml").read_text(encoding="utf-8")
		self.assertNotIn("\tra2|maps: System", manifest)
		self.assertNotIn("\t~ra2|maps: System", manifest)
		self.assertIn("\t~^SupportDir|maps/ra2/nukehour-storage-v1: User", manifest)


if __name__ == "__main__":
	unittest.main()
