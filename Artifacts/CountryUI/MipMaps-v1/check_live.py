import json
import subprocess
import time
from pathlib import Path

from PIL import Image

ROOT = Path('/Users/fukutomi/Unity/WarSimulation')
OUT = ROOT / 'Artifacts/CountryUI/MipMaps-v1'
REQUEST = Path('/private/tmp/country-text-layout.json')
actions = json.loads((OUT / 'actions.json').read_text()) if (OUT / 'actions.json').exists() else []


def command(tool, parameters):
    result = subprocess.run([
        'unity-cli', 'raw', tool, '--host', '127.0.0.1', '--port', '6400',
        '--json', json.dumps(parameters), '--output', 'json', '--timeout-ms', '20000'
    ], cwd=ROOT, capture_output=True, text=True, timeout=25)
    response = json.loads(result.stdout)
    actions.append({'tool': tool, 'parameters': parameters, 'response': response})
    (OUT / 'actions.json').write_text(json.dumps(actions, ensure_ascii=False, indent=2) + '\n')
    assert result.returncode == 0 and response.get('success'), response
    assert not response.get('data', {}).get('error'), response
    return response['data']


def click(name):
    command('click_ui_element', json.loads((OUT / ('click-' + name + '.json')).read_text()))


def resize(width):
    menu = 'PC 1280 x 720' if width == 1280 else 'Phone 844 x 390'
    command('execute_menu_item', {'menuPath': 'WarSim/Country/Government Preview/' + menu})


def capture(name, width, page=None):
    height = 720 if width == 1280 else 390
    REQUEST.write_text(json.dumps({'Scope': 'CountryHud', 'Phase': '../MipMaps-v1/after', 'Name': name}))
    started = time.time()
    command('execute_menu_item', {'menuPath': 'WarSim/Country/Text Layout/Capture'})
    path = OUT / f'after/{name}-{width}x{height}.png'
    deadline = time.monotonic() + 5
    while not path.exists() or path.stat().st_mtime < started:
        assert time.monotonic() < deadline, str(path)
        time.sleep(.05)
    with Image.open(path) as image:
        image.load()
        assert image.size == (width, height)
    report = json.loads(path.with_suffix('.json').read_text())
    if page:
        assert any('/Main/' + page + '/' in text['Path'] for text in report['Texts']), name
    print(str(path.relative_to(ROOT)), flush=True)
    return report


if __name__ == '__main__':
    state = command('get_editor_state', {})['state']
    assert state['isPlaying'] and not state['isCompiling'], state
    assert state['applicationContentsPath'].endswith('/6000.4.3f1/Unity.app/Contents'), state
    scene = command('get_scene_info', {})
    assert scene['scenePath'] == 'Assets/Scenes/Country.unity', scene
    click('budget')
    capture('Budget', 844, 'Budget')
    click('construction')
    capture('Construction', 844, 'Construction')
    resize(1280)
    capture('Construction', 1280, 'Construction')
    click('budget')
    capture('Budget', 1280, 'Budget')
    click('training')
    report = capture('Training-AfterTabs', 1280, 'Training')
    if any(text['Text'] == '選択から外す' for text in report['Texts']):
        click('training-action')
    capture('Training', 1280, 'Training')
