import hashlib
import json
import unittest
import zipfile
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).parents[2]
CATALOG = ROOT / "artsrc/production-cameos-hd/catalog.json"
PUBLIC_MANIFEST = ROOT / "packaging/public-content-manifest.json"
MAC_IOS_ROOT = ROOT.parent / "ra2-mac"


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


class HdProductionCameoAndroidAuditTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = json.loads(CATALOG.read_text(encoding="utf-8"))
        cls.runtime_paths = sorted({
            item["runtime_path"]
            for group in ("actors", "support_powers", "ui_glyphs")
            for item in cls.catalog[group]
        })

    def test_complete_runtime_inventory_is_packaged_source(self):
        self.assertEqual(129, len(self.runtime_paths))
        for relative in self.runtime_paths:
            path = ROOT / relative
            self.assertTrue(path.is_file(), relative)
            with Image.open(path) as image:
                expected = (128, 128) if "/ui/" in relative else (320, 256)
                self.assertEqual(expected, image.size, relative)
                if "/ui/" in relative:
                    self.assertTrue(all(
                        size > 0 and size & (size - 1) == 0
                        for size in image.size), relative)

    def test_public_manifest_hash_locks_every_runtime_asset(self):
        payload = json.loads(PUBLIC_MANIFEST.read_text(encoding="utf-8"))
        entries = {entry["path"]: entry for entry in payload["files"]}
        for relative in self.runtime_paths:
            self.assertIn(relative, entries)
            self.assertTrue(entries[relative]["public"], relative)
            if relative.endswith("/yuri/yacnst.png"):
                self.assertEqual("redistributable-third-party", entries[relative]["category"], relative)
            elif "/allies/" in relative or "/soviets/" in relative or "/yuri/" in relative:
                self.assertEqual("user-reference-restored", entries[relative]["category"], relative)
            else:
                self.assertEqual("project-original", entries[relative]["category"], relative)
            self.assertEqual(digest(ROOT / relative), entries[relative]["sha256"], relative)

    def test_android_runtime_assets_match_mac_ios_byte_for_byte(self):
        if not MAC_IOS_ROOT.is_dir():
            self.skipTest("macOS/iOS sibling repository is unavailable")
        for relative in self.runtime_paths:
            self.assertEqual(digest(ROOT / relative), digest(MAC_IOS_ROOT / relative), relative)

    def test_built_debug_apk_contains_runtime_assets_but_not_design_masters(self):
        apks = sorted((ROOT / "android/OpenRA.Android/bin/Debug").glob("**/*-Signed.apk"))
        if not apks:
            self.skipTest("Debug APK has not been built")
        with zipfile.ZipFile(apks[-1]) as archive:
            names = set(archive.namelist())
        self.assertFalse(any("design-previews/production-cameos-hd" in name for name in names))
        for relative in self.runtime_paths:
            expected = "assets/runtime/" + relative
            self.assertIn(expected, names, relative)


if __name__ == "__main__":
    unittest.main()
