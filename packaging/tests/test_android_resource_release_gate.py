import contextlib
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest
import zipfile

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('clean_runtime_assets', ROOT / 'packaging/android/clean_runtime_assets.py')
GATE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(GATE)


class AndroidResourceReleaseGateTest(unittest.TestCase):
    def audit_case(self, expected_name, expected_data, actual, category='project-original'):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            apk = root / 'fixture.apk'
            with zipfile.ZipFile(apk, 'w') as archive:
                for name, data in actual.items():
                    archive.writestr(name, data)
            manifest = root / 'manifest.json'
            manifest.write_text(json.dumps({'files': [{'apk_path': expected_name,
                'sha256': hashlib.sha256(expected_data).hexdigest(), 'size': len(expected_data),
                'category': category}]}))
            report = root / 'report.json'
            with contextlib.redirect_stdout(io.StringIO()):
                status = GATE.audit(apk, manifest, report)
            return status, json.loads(report.read_text())

    def test_exact_resource_inventory_passes(self):
        name = 'assets/runtime/mods/ra2/chrome.yaml'
        status, report = self.audit_case(name, b'owned config', {name: b'owned config'})
        self.assertEqual(0, status)
        self.assertTrue(report['passed'])

    def test_unlisted_file_blocks_release(self):
        name = 'assets/runtime/mods/ra2/chrome.yaml'
        status, report = self.audit_case(name, b'ok', {name: b'ok', 'assets/runtime/unreviewed.png': b'new'})
        self.assertEqual(1, status)
        self.assertTrue(any('Unlisted resource' in e for e in report['errors']))

    def test_missing_or_changed_file_blocks_release(self):
        name = 'assets/runtime/mods/ra2/chrome.yaml'
        for actual in ({}, {name: b'changed'}):
            with self.subTest(actual=actual):
                status, report = self.audit_case(name, b'expected', actual)
                self.assertEqual(1, status)
                self.assertFalse(report['passed'])

    def test_retail_archive_is_denied_even_if_added_to_inventory(self):
        name = 'assets/runtime/mods/ra2/ra2.mix'
        status, report = self.audit_case(name, b'retail', {name: b'retail'})
        self.assertEqual(1, status)
        self.assertTrue(any('Forbidden resource' in e for e in report['errors']))

    def test_unreviewed_category_is_denied(self):
        name = 'assets/runtime/mods/ra2/uibits/new.png'
        status, report = self.audit_case(name, b'unknown', {name: b'unknown'}, 'unknown-or-derivative')
        self.assertEqual(1, status)
        self.assertFalse(report['passed'])


if __name__ == '__main__':
    unittest.main()
