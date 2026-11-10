import gzip
import hashlib
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
OUT = Path(__file__).resolve().parent
BUILD = ROOT / '.unity/CombatPlaytestWebGL'


def digest(paths, compressed=False):
    result = hashlib.sha256()
    size = 0
    for path in paths:
        opener = gzip.open if compressed else open
        with opener(path, 'rb') as stream:
            while block := stream.read(4 * 1024 * 1024):
                result.update(block)
                size += len(block)
    return dict(bytes=size, sha256=result.hexdigest())


if sys.argv[1] == 'before':
    files = list((BUILD / 'Build').glob('*.data.unityweb'))
    assert len(files) == 1, files
    data = dict(path=str(files[0].relative_to(BUILD)), expanded=digest(files, True))
    (OUT / 'data-before-packaging.json').write_text(json.dumps(data, indent=2) + '\n')
    print(json.dumps(data))
else:
    before = json.loads((OUT / 'data-before-packaging.json').read_text())
    names = json.loads((BUILD / 'Build/PlaytestData.json').read_text())
    assert names and all(name.startswith('Build/PlaytestData.part') and '..' not in name for name in names)
    parts = [BUILD / name for name in names]
    combined = digest(parts)
    assert combined == before['expanded'], (combined, before)
    with parts[0].open('rb') as stream:
        assert stream.read(len(b'UnityWebData1.0')) == b'UnityWebData1.0', 'Unexpected WebGL data signature'
    html = (BUILD / 'index.html').read_text()
    assert 'PlaytestData.json' in html and 'URL.createObjectURL' in html
    files = [p for p in BUILD.rglob('*') if p.is_file()]
    assert all(p.stat().st_size <= 200 * 1024 * 1024 for p in files)
    assert not list((BUILD / 'Build').glob('*.data.unityweb'))
    summary = dict(parts=[dict(path=str(p.relative_to(BUILD)), **digest([p])) for p in parts], combined=combined,
                   maxFileBytes=max(p.stat().st_size for p in files), fileCount=len(files), matched=True)
    (OUT / 'packaging.json').write_text(json.dumps(summary, indent=2) + '\n')
    print(json.dumps(summary))
