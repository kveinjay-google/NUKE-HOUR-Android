"""Check the publication inventory from Git, without reading local credentials."""
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[2]
paths = subprocess.check_output(['git', 'ls-files', '-z'], cwd=ROOT).decode().split('\0')
paths = [p for p in paths if p]
assert paths, 'The publication set must be staged or committed before auditing.'
for name in paths:
    path = ROOT / name
    assert not path.is_symlink(), f'Unexpected symlink: {name}'
    assert path.stat().st_size < 50 * 1024 * 1024, f'File exceeds publication ceiling: {name}'
    assert not {'bin', 'obj', 'artifacts', '__pycache__', '.git'}.intersection(path.relative_to(ROOT).parts), name
    assert path.suffix.lower() not in {'.mix', '.bag', '.bik', '.vqa', '.vxl', '.hva', '.apk', '.aab',
                                      '.ipa', '.p12', '.pfx', '.jks', '.keystore', '.pem', '.key',
                                      '.so', '.dylib', '.dll', '.pdb'}, name
    assert not path.name.startswith('.env'), name

# Resource checks alone are insufficient: an ignore rule must not hide source namespaces.
tracked = set(paths)
source_extensions = {'.cs', '.csproj', '.c', '.h', '.java', '.props', '.targets', '.resx'}
for directory in ('engine', 'android', 'OpenRA.Mods.RA2', 'third_party'):
    for source in (ROOT / directory).rglob('*'):
        relative = source.relative_to(ROOT)
        if {'bin', 'obj', 'artifacts', '__pycache__'}.intersection(relative.parts):
            continue
        if source.is_file() and source.suffix in source_extensions:
            assert relative.as_posix() in tracked, f'Required source file not in Git: {relative}'

entries = json.loads((ROOT / 'packaging/public-content-manifest.json').read_text())['files']
for entry in entries:
    name = entry['path']
    source = ROOT / name
    if name in ('VERSION', 'global mix database.dat') or name.startswith(('glsl/', 'mods/common/', 'mods/common-content/')):
        source = ROOT / 'engine' / name
    assert source.relative_to(ROOT).as_posix() in paths, f'Required runtime resource not in Git: {name}'
    data = source.read_bytes()
    assert len(data) == entry['size'] and hashlib.sha256(data).hexdigest() == entry['sha256'], name
print(f'PASS: {len(paths)} source files; {len(entries)} hash-locked runtime resources; no build/game archives or oversized files.')
