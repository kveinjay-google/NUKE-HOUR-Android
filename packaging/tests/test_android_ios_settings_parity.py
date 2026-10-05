from pathlib import Path
import hashlib
import json
import unittest

ROOT = Path(__file__).resolve().parents[2]

class AndroidIosSettingsParityTest(unittest.TestCase):
    def test_settings_match_verified_ios_reference_with_mobile_platform_entry(self):
        reference = json.loads((ROOT / 'packaging/tests/fixtures/ios_phone_settings_reference.json').read_text())
        for path, expected in reference['files'].items():
            with self.subTest(path=path):
                content = (ROOT / path).read_bytes().replace(b'Platform.IsIOS', b'Platform.UsesMobileLayout')
                self.assertEqual(hashlib.sha256(content).hexdigest(), expected,
                    'Refresh the iOS reference after verifying a deliberate layout change.')
