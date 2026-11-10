import json
import subprocess
import time
from pathlib import Path

ROOT = Path('/Users/fukutomi/Unity/WarSimulation')
OUT = ROOT / 'Artifacts/CountryUI/ColorPass-v1'
REQUEST = Path('/private/tmp/country-text-layout.json')


def command(tool, parameters):
    process = subprocess.run([
        'unity-cli', 'raw', tool, '--host', '127.0.0.1', '--port', '6400',
        '--json', json.dumps(parameters), '--output', 'json', '--timeout-ms', '25000'
    ], cwd=ROOT, capture_output=True, text=True, timeout=30)
    response = json.loads(process.stdout)
    log = OUT / 'actions.json'
    actions = json.loads(log.read_text()) if log.exists() else []
    actions.append(dict(tool=tool, parameters=parameters, response=response))
    log.write_text(json.dumps(actions, ensure_ascii=False, indent=2) + '\n')
    assert process.returncode == 0 and response.get('success'), response
    assert not response.get('data', {}).get('error'), response
    return response['data']


def menu(path):
    return command('execute_menu_item', dict(menuPath=path))


def click(path):
    return command('click_ui_element', dict(elementPath=path))


def capture(name, width=1280):
    time.sleep(.2)
    REQUEST.write_text(json.dumps(dict(Scope='CountryHud', Phase='../ColorPass-v1/after', Name=name)))
    started = time.time()
    menu('WarSim/Country/Text Layout/Capture')
    path = OUT / f'after/{name}-{width}x{720 if width == 1280 else 390}.png'
    deadline = time.monotonic() + 6
    while not path.exists() or path.stat().st_mtime < started:
        assert time.monotonic() < deadline, path
        time.sleep(.05)
    print(str(path), flush=True)
    return json.loads(path.with_suffix('.json').read_text())
