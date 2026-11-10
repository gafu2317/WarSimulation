import json
import sys
from pathlib import Path

import yaml

sys.path.insert(0, str(Path(__file__).resolve().parent))
from check_live import command, menu, click, capture, REQUEST, ROOT

NAV = '/CountryHud/Government/Window/Shell/Header/Navigation/'
PERSONNEL = '/CountryHud/Personnel/Window/Shell/Header/'


def back(report):
    paths = [text['Button'] for text in report['Texts'] if '国へ戻る' in text['Text'] and text['Button']]
    assert paths, 'No back button'
    click(paths[-1])


def caption(report, label):
    paths = [text['Button'] for text in report['Texts'] if label == text['Text'] and text['Button']]
    assert paths, label
    click(paths[-1])


def region():
    text = (ROOT / 'Assets/Data/Country/CountryWorldMap.asset').read_text()
    data = yaml.safe_load(text[text.index('MonoBehaviour:'):])['MonoBehaviour']
    def edges(polygon):
        points = [(round(v['x'], 6), round(v['y'], 6)) for v in polygon]
        return set(zip(points, points[1:] + points[:1]))
    home = edges(data['Regions'][data['HomeRegion']]['Polygon'])
    return next(i for i, value in enumerate(data['Regions']) if i != data['HomeRegion']
                and any((end, start) in home for start, end in edges(value['Polygon'])))


def review(width):
    menu('WarSim/Country/Government Preview/' + ('PC 1280 x 720' if width == 1280 else 'Phone 844 x 390'))
    capture('HUD', width)
    click('/CountryHud/BottomLeftButtons/Buttons/Button2')
    action = '/CountryHud/Government/Window/Shell/Main/Training/Content/Selection/Body/Roster/Right/Actions/Action'
    report = capture('Training-Opened', width)
    if any(text['Text'] == '選択から外す' for text in report['Texts']):
        click(action)
    capture('Training', width)
    click(action)
    capture('Training-Selected', width)
    REQUEST.write_text(json.dumps(dict(Scope=action)))
    menu('WarSim/Country/Text Layout/Hold')
    capture('Training-Pressed', width)
    REQUEST.write_text(json.dumps(dict(Scope=action)))
    menu('WarSim/Country/Text Layout/Release')
    for name in ['Budget', 'Construction', 'Income']:
        click(NAV + name)
        report = capture(name, width)
        if name == 'Budget':
            switch = '/CountryHud/Government/Window/Shell/Main/Budget/Summary/UseTreasury'
            click(switch)
            capture('Budget-On', width)
            click(switch)
    back(report)
    click('/CountryHud/BottomLeftButtons/Buttons/Button4')
    report = capture('Soldiers', width)
    details = next(scroll['Path'] for scroll in report['Scrolls'] if '/Personnel/' in scroll['Path'] and scroll['Path'].endswith('/Details'))
    REQUEST.write_text(json.dumps(dict(Scope=details, X=0, Y=0)))
    menu('WarSim/Country/Text Layout/Scroll')
    capture('Soldier-Skills', width)
    REQUEST.write_text(json.dumps(dict(Scope=details, X=0, Y=1)))
    menu('WarSim/Country/Text Layout/Scroll')
    click(PERSONNEL + 'Weapons')
    report = capture('Weapons', width)
    click(PERSONNEL + 'Defense')
    report = capture('Defense', width)
    back(report)
    click('/CountryHud/TopBar/Content/SettingsButton')
    report = capture('Settings', width)
    caption(report, 'キャンセル')
    click('/CountryHud/BottomRightButton')
    REQUEST.write_text(json.dumps(dict(Index=region())))
    menu('WarSim/Country/Text Layout/Select Region')
    report = capture('Expedition', width)
    click('/CountryHud/Expeditions/Window/Shell/Header/Formation')
    report = capture('Formation', width)
    caption(report, 'メンバーに加える')
    report = capture('Formation-Selected', width)
    back(report)


if __name__ == '__main__':
    review(int(sys.argv[1]))
