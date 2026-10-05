"""Regression checks against a real APK; all selectable UI styles must survive."""
import json
import os
import unittest
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
APK = Path(os.environ.get('NUKEHOUR_ANDROID_AUDIT_APK',
    str(ROOT / 'artifacts/android-public-clean/build/Release/net8.0-android/android-arm64/com.openra.android.personal-Signed.apk')))
PREFIX = 'assets/runtime/mods/ra2/uibits/'


class AndroidCleanUiApkTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.archive = zipfile.ZipFile(APK)
        cls.names = set(cls.archive.namelist())

    @classmethod
    def tearDownClass(cls):
        cls.archive.close()

    def test_no_duplicate_raster_directory_is_packaged(self):
        copies = [n for n in self.names if n.startswith('assets/runtime/mods/ra2/chrome/') and n.endswith('.png')]
        self.assertEqual([], copies, 'PNG loading uses uibits; duplicated chrome copies must not ship')

    def test_inactive_menu_backgrounds_do_not_ship(self):
        for name in ('menubg1.png', 'menubg2.png', 'menubg3.png', 'menubg4.png', 'NUCLEAR-CRISIS-BG-04.png'):
            self.assertFalse(PREFIX + name in self.names, name)
        self.assertIn(PREFIX + 'NUCLEAR-CRISIS-BG-06.png', self.names)
        self.assertIn(PREFIX + 'NUCLEAR-CRISIS-BG-05.png', self.names)
        for i in range(1, 10):
            self.assertIn(PREFIX + f'startup-wallpapers/wallpaper-{i:02d}.png', self.names)

    def test_classic_hd_and_faction_resources_all_survive(self):
        for path in (ROOT / 'mods/ra2/uibits').rglob('*.png'):
            relative = path.relative_to(ROOT / 'mods/ra2/uibits').as_posix()
            if relative.startswith(('classic-', 'hd-sidebar-', 'production-', 'commandbar-', 'mac-dock-', 'mobile-')):
                self.assertIn(PREFIX + relative, self.names, relative)
        for faction in ('allies', 'soviets', 'yuri'):
            self.assertIn(PREFIX + f'ios-commandbar-glyphs-{faction}-v4.png', self.names)
            for kind in ('actions', 'joystick', 'quickbar'):
                for density in ('', '-2x'):
                    self.assertIn(PREFIX + f'ios-touch-{kind}-{faction}-v4{density}.png', self.names)

    def test_every_hd_production_icon_survives(self):
        catalog = json.loads((ROOT / 'artsrc/production-cameos-hd/catalog.json').read_text())
        for group in ('actors', 'support_powers', 'ui_glyphs'):
            for item in catalog[group]:
                self.assertIn('assets/runtime/' + item['runtime_path'], self.names)

    def test_original_background_videos_remain_seekable(self):
        for name in ('menu-background.mp4', 'ios-home-background.mp4'):
            entry = self.archive.getinfo('assets/runtime/mods/ra2/media/' + name)
            self.assertEqual(zipfile.ZIP_STORED, entry.compress_type, name)

    def test_retail_archives_are_never_packaged(self):
        self.assertFalse(any(n.lower().endswith(('.mix', '.bag', '.bik', '.vqa', '.vxl', '.hva')) for n in self.names))
        self.assertFalse(any(n.startswith('assets/runtime/mods/ra2/maps/') for n in self.names))


if __name__ == '__main__':
    unittest.main()
