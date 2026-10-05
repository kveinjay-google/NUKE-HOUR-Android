#!/usr/bin/env python3
"""Prepare a reviewed Android runtime allowlist and audit the actual final APK.

Source assets remain untouched. PublicClean builds must consume generated props.
"""
import argparse
import hashlib
import json
import re
import sys
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PUBLIC_CATEGORIES = {'engine-source', 'project-original', 'redistributable-third-party', 'user-reference-restored'}
VIDEO_NAMES = ('menu-background.mp4', 'ios-home-background.mp4')
FORBIDDEN = ('.mix', '.bag', '.idx', '.bik', '.vqa', '.vxl', '.hva', '.aud')


def sha(data):
    return hashlib.sha256(data).hexdigest()


def source_for(path):
    if path in ('VERSION', 'global mix database.dat') or path.startswith(('glsl/', 'mods/common/', 'mods/common-content/')):
        return ROOT / 'engine' / path
    return ROOT / path


def apk_path(path):
    if path == 'VERSION' or path.startswith(('glsl/', 'mods/common/', 'mods/common-content/')):
        return 'assets/runtime/engine/' + path
    return 'assets/runtime/' + path


def legacy_touch(path):
    name = Path(path).name
    return bool(re.fullmatch(r'ios-touch-.+-(?:allies|soviets|yuri)(?:-2x)?\.png', name)
                or re.fullmatch(r'ios-touch-.+-v3(?:-2x)?\.png', name)
                or re.fullmatch(r'ios-commandbar-glyphs-.+-v3\.png', name))


def filter_chrome(text, removed):
    blocks = []
    for match in re.finditer(r'(?m)^([^\s#][^\n]*):[^\n]*\n', text):
        blocks.append((match.start(), match.group(1)))
    dropped = set()
    for index, (start, name) in enumerate(blocks):
        end = blocks[index + 1][0] if index + 1 < len(blocks) else len(text)
        block = text[start:end]
        for image in re.findall(r'(?m)^\s+Image(?:2x|3x)?:\s*(.+)$', block):
            canonical = 'mods/ra2/uibits/' + image.strip().split('|')[-1]
            if image.startswith('uibits/'):
                canonical = 'mods/ra2/' + image.strip()
            if canonical in removed:
                dropped.add(name)
    changed = True
    while changed:
        changed = False
        for index, (start, name) in enumerate(blocks):
            end = blocks[index + 1][0] if index + 1 < len(blocks) else len(text)
            parents = re.findall(r'(?m)^\s+Inherits(?:@[^:]*)?:\s*(.+)$', text[start:end])
            if name not in dropped and any(p.strip() in dropped for p in parents):
                dropped.add(name)
                changed = True
    result = text[:blocks[0][0]] if blocks else text
    for index, (start, name) in enumerate(blocks):
        end = blocks[index + 1][0] if index + 1 < len(blocks) else len(text)
        if name not in dropped:
            result += text[start:end]
    return result, sorted(dropped)


def prepare(out, shared_manifest):
    out.mkdir(parents=True, exist_ok=True)
    inactive = json.loads((ROOT / 'packaging/android/inactive_ui_assets.json').read_text())
    removed = set(inactive['exclude_from_package'])
    manifests = [json.loads((ROOT / 'packaging/public-content-manifest.json').read_text())]
    if shared_manifest.resolve() != (ROOT / 'packaging/public-content-manifest.json').resolve():
        manifests.append(json.loads(shared_manifest.read_text()))
    approvals = {}
    for manifest in manifests:
        for e in manifest['files']:
            if e.get('public') and e.get('category') in PUBLIC_CATEGORIES:
                approvals.setdefault(e['path'], []).append(e)
    allowed_prefixes = ('glsl/', 'mods/common/', 'mods/common-content/', 'mods/ra2-content/', 'mods/ra2/')
    entries = []
    for path in sorted(approvals):
        if path not in ('VERSION', 'global mix database.dat') and not path.startswith(allowed_prefixes):
            continue
        if path.startswith(('mods/ra2/maps/', 'mods/ra2/chrome/')) and not path.endswith('.yaml'):
            continue
        if path.startswith('mods/ra2/media/') or path.endswith(('.svg', '.DS_Store')) or path in removed or legacy_touch(path):
            removed.add(path)
            continue
        source = source_for(path)
        if not source.is_file():
            continue
        data = source.read_bytes()
        approved = next((e for e in approvals[path] if e['sha256'] == sha(data)), None)
        if approved is None:
            # Source-authored rules/layout/localization are editable GPL source;
            # raster/support/audio/font data requires an exact provenance hash.
            if source.suffix not in ('.yaml', '.ftl') and path not in ('VERSION', 'mods/ra2/AUTHORS'):
                raise ValueError('Unapproved changed resource: ' + path)
            approved = approvals[path][-1]
        entries.append({'path': path, 'apk_path': apk_path(path), 'source': str(source),
                        'source_sha256': sha(data), 'sha256': sha(data), 'size': len(data),
                        'category': approved['category'], 'license': approved.get('license', ''),
                        'provenance': 'Existing approved Android/iOS manifest'})
    for name in VIDEO_NAMES:
        path = 'mods/ra2/media/' + name
        source = ROOT / path
        data = source.read_bytes()
        entries.append({'path': path, 'apk_path': apk_path(path), 'source': str(source),
                        'source_sha256': sha(data), 'sha256': sha(data), 'size': len(data),
                        'category': 'project-original', 'license': 'Project original',
                        'provenance': 'User explicitly confirmed both background videos are original project assets on 2026-10-05'})
    # Legacy touch files absent from public manifests are intentionally excluded;
    # current V4 bindings and every selectable style are checked independently.
    for source in (ROOT / 'mods/ra2/uibits').rglob('*'):
        if source.is_file() and legacy_touch(source.as_posix()):
            removed.add(source.relative_to(ROOT).as_posix())
    chrome_source = ROOT / 'mods/ra2/chrome.yaml'
    chrome, dropped = filter_chrome(chrome_source.read_text(), removed)
    staged = out / 'staged/mods/ra2/chrome.yaml'
    staged.parent.mkdir(parents=True, exist_ok=True)
    staged.write_text(chrome)
    for e in entries:
        if e['path'] == 'mods/ra2/chrome.yaml':
            e.update(source=str(staged), sha256=sha(staged.read_bytes()), size=staged.stat().st_size,
                     generated_from=str(chrome_source), provenance='Approved Chrome definitions minus reviewed inactive collections')
    retained = {e['path'] for e in entries}
    # Every retained collection must resolve its own registered PNG image.
    for image in re.findall(r'(?m)^\s+Image(?:2x|3x)?:\s*(.+)$', chrome):
        image = image.strip()
        if not image.endswith('.png'):
            continue
        canonical = 'mods/ra2/' + image if image.startswith('uibits/') else 'mods/ra2/uibits/' + image
        if canonical not in retained:
            raise ValueError('Retained Chrome image missing from whitelist: ' + canonical)
    props = ET.Element('Project')
    group = ET.SubElement(props, 'ItemGroup')
    for e in entries:
        item = ET.SubElement(group, 'AndroidAsset', Include=e['source'])
        ET.SubElement(item, 'Link').text = e['apk_path'].removeprefix('assets/')
    ET.indent(props)
    ET.ElementTree(props).write(out / 'runtime-assets.props', encoding='utf-8', xml_declaration=True)
    payload = {'schemaVersion': 1, 'source_root': str(ROOT), 'files': entries,
               'excluded_ui_files': sorted(removed), 'excluded_chrome_collections': dropped,
               'preserved_interface_styles': ['Classic', 'ClassicHD'],
               'preserved_factions': ['allies', 'soviets', 'yuri']}
    (out / 'runtime-assets.json').write_text(json.dumps(payload, ensure_ascii=False, indent=2) + '\n')
    print(f'Prepared {len(entries)} hash-locked resources; {len(dropped)} inactive Chrome collections omitted.')


def audit(apk, manifest, report):
    payload = json.loads(manifest.read_text())
    expected = {e['apk_path']: e for e in payload['files']}
    errors = []
    with zipfile.ZipFile(apk) as archive:
        actual = {n for n in archive.namelist() if n.startswith('assets/runtime/') and not n.endswith('/')}
        for name in sorted(actual - expected.keys()):
            errors.append('Unlisted resource: ' + name)
        for name in sorted(expected.keys() - actual):
            errors.append('Missing resource: ' + name)
        for name in sorted(actual & expected.keys()):
            entry = expected[name]
            data = archive.read(name)
            if sha(data) != entry['sha256'] or len(data) != entry['size']:
                errors.append('Hash or size mismatch: ' + name)
            if entry['category'] not in PUBLIC_CATEGORIES or name.lower().endswith(FORBIDDEN):
                errors.append('Forbidden resource: ' + name)
            if name.endswith('.mp4') and archive.getinfo(name).compress_type != zipfile.ZIP_STORED:
                errors.append('Video cannot be opened using Android asset FD: ' + name)
        if archive.testzip():
            errors.append('ZIP CRC check failed')
    result = {'passed': not errors, 'errors': errors, 'apk': str(apk),
              'apk_bytes': apk.stat().st_size, 'apk_sha256': sha(apk.read_bytes()),
              'expected_resource_files': len(expected), 'actual_resource_files': len(actual)}
    report.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps({k: v for k, v in result.items() if k != 'apk_sha256'}, ensure_ascii=False))
    return 0 if not errors else 1


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    sub = parser.add_subparsers(dest='mode', required=True)
    build = sub.add_parser('prepare')
    build.add_argument('--out', type=Path, required=True)
    build.add_argument('--shared-manifest', type=Path, required=True)
    check = sub.add_parser('audit')
    check.add_argument('--apk', type=Path, required=True)
    check.add_argument('--manifest', type=Path, required=True)
    check.add_argument('--report', type=Path, required=True)
    args = parser.parse_args()
    if args.mode == 'prepare':
        prepare(args.out.resolve(), args.shared_manifest.resolve())
    else:
        sys.exit(audit(args.apk.resolve(), args.manifest.resolve(), args.report.resolve()))
