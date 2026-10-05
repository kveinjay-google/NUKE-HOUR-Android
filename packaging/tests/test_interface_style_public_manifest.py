import hashlib
import json
import unittest
from pathlib import Path


ROOT = Path(__file__).parents[2]
RUNTIME_FILES = (
    "mods/ra2/chrome/mainmenu.yaml",
    "mods/ra2/fluent/chrome.ftl",
    "mods/ra2/fluent/zh-CN/chrome.ftl",
)
MANIFESTS = (
    "packaging/public-content-manifest.json",
)


class InterfaceStylePublicManifestTest(unittest.TestCase):
    def test_menu_and_localization_hashes_match_every_public_manifest(self):
        for manifest_name in MANIFESTS:
            payload = json.loads((ROOT / manifest_name).read_text(encoding="utf-8"))
            entries = {entry["path"]: entry for entry in payload["files"]}
            for relative in RUNTIME_FILES:
                expected = hashlib.sha256((ROOT / relative).read_bytes()).hexdigest()
                self.assertIn(relative, entries, f"{manifest_name}: {relative}")
                self.assertEqual(expected, entries[relative]["sha256"],
                                 f"{manifest_name}: {relative}")


if __name__ == "__main__":
    unittest.main()
