"""Export role facilities from the bundle used by the Country scene."""

import gzip
import hashlib
import json
from pathlib import Path
import re
import struct
import subprocess
import sys


ROOT = Path(sys.argv[1]).resolve()
SITE = Path(__file__).resolve().parent

GROUPS = [
    ('職業施設', [
        ('WarriorAcademy_Lv5', '戦士学校', '剣・盾の職業スキルを習得する訓練施設。正面の訓練門には交差した大剣が掲げられている。'),
        ('ArcaneAcademy_Lv5', '魔法学校', '杖・魔導書の職業スキルを習得する訓練施設。研究塔の上には大きな魔力結晶がある。'),
        ('Church', '教会', '聖書・ロザリオの職業スキルを習得する訓練施設。開放鐘楼と薔薇窓が目印。'),
    ]),
    ('性格施設', [
        ('Library', '図書館', '蔵書内容によって、CP0から復帰するときの性格のランダム性を調整する。正面には開いた本のレリーフがある。'),
        ('Plaza', '広場', '飾り付けによって、CP0から復帰するときの性格のランダム性を調整する。中央には三段の大噴水がある。'),
        ('Tavern', '酒場', '木製ジョッキの吊り看板と醸造設備を備える。'),
        ('Observatory', '天文台', 'ガラスの観測ドームと、その内側の望遠鏡が目印。'),
        ('Arena', '闘技場', '高い大会門と深紅の幕、競技場を囲む客席を備える。'),
        ('Museum', '美術館', '正面の柱廊に、翼を広げた彫刻を掲げる。'),
        ('Clinic', '診療所', '緑の薬師十字と乳鉢の目印、薬草温室を備える。'),
        ('Casino', 'カジノ', '扇状に並んだトランプと入口の賽子が目印。'),
        ('Guardhouse', '衛兵所', '門の上の盾と長槍が目印。'),
        ('LanternSpire', '仮面のサロン', '歓楽街の一棟。'),
        ('CrimsonRowhouse', '演芸館', '歓楽街の一棟。正面の竪琴が目印。'),
        ('VeiledCourtyard', '中庭館', '歓楽街の一棟。'),
        ('VelvetTerrace', '舞踏館', '歓楽街の一棟。'),
    ]),
    ('精霊施設', [
        ('SpiritAcademy_Lv5', '精霊使役学校', 'ステータスを上昇させる施設。回廊を超えて伸びる大樹と精霊の灯が目印。'),
    ]),
    ('武器製作施設', [
        ('Blacksmith', '鍛冶場', '施設レベルに応じてより良い武器を製作する。開いた作業屋根、炉、金床を備える。'),
    ]),
]


def read_bundle(path):
    with gzip.open(path, 'rb') as stream:
        magic, length = struct.unpack('<II', stream.read(8))
        if magic != 20261117:
            raise ValueError('Unexpected TownLayout version')
        town = json.loads(stream.read(length))
        pictures = {item['key']: stream.read(item['size']) for item in town['textures']}
        meshes = [
            stream.read(32 * mesh['vertices'] + 4 * sum(mesh['indices']))
            for mesh in town['geometry']
        ]
        if stream.read(1):
            raise ValueError('Unexpected data after the town meshes')
    return town, pictures, meshes


def surface_for(surface, material_dir, textures):
    surface = dict(surface)
    material_path = material_dir / (surface['name'] + '.mat')
    dedicated = {'Carved_Limestone', 'Bronze_Fittings', 'Glazing_Blue', 'Glazing_Amber',
                 'Glazing_Sage', 'Glazing_Plum', 'Coal', 'Glowing_Coals', 'Leather',
                 'Wicker', 'Architectural_Glass'}
    if not surface['base'] and material_path.is_file() and (
        surface['name'] in dedicated or surface['name'].startswith('Identity_')
    ):
        material = material_path.read_text()
        color = re.search(r'^    - _BaseColor: \{([^}]+)\}', material, re.M)
        if color:
            surface['color'] = [float(part.split(':')[1]) for part in color[1].split(',')]
        for prop, guid in re.findall(r'^    - (_BaseMap|_BumpMap):\n +m_Texture: \{fileID: \d+, guid: (\w+),', material, re.M):
            if guid in textures:
                surface['base' if prop == '_BaseMap' else 'normal'] = textures[guid]
        for field, key in [('_BumpScale', 'bump'), ('_Metallic', 'metallic'), ('_Smoothness', 'roughness')]:
            match = re.search(r'^    - ' + field + r': ([\d.]+)', material, re.M)
            if match:
                value = float(match[1])
                surface[key] = 1 - value if field == '_Smoothness' else value
        if re.search(r'^    - _Surface: 1$', material, re.M):
            surface['opacity'] = surface['color'][3]
        if '_TOWN_LAYOUT_TONE' in material or '_TONE_PALETTE' in material:
            surface['tone'] = True
    return surface


def export():
    city = ROOT / 'Assets/Models/Kingdom/CityV2'
    bundle = city / 'TownLayout.townlayout'
    town, pictures, meshes = read_bundle(bundle)
    output = SITE / 'dist'
    media = output / 'models'
    media.mkdir(parents=True, exist_ok=True)
    materials_dir = ROOT / 'Assets/Prefabs/Kingdom/CityV2/Materials'

    textures = {}
    for meta in city.rglob('*.png.meta'):
        guid = re.search(r'^guid: (\w+)', meta.read_text(), re.M)
        if guid:
            textures[guid[1]] = meta.with_suffix('').relative_to(ROOT).as_posix()

    texture_files = {}
    def copy_texture(key):
        if not key:
            return ''
        if key not in texture_files:
            raw = pictures[key] if key in pictures else (ROOT / key).read_bytes()
            extension = '.png' if raw.startswith(b'\x89PNG') else '.jpg'
            name = hashlib.sha256(raw).hexdigest()[:20] + extension
            target = media / name
            target.write_bytes(raw)
            subprocess.run(['sips', '-Z', '512', str(target)], check=True, stdout=subprocess.DEVNULL)
            texture_files[key] = name
        return texture_files[key]

    prototypes = {p['key']: p for p in town['prototypes']}
    placements = {p['name']: p for p in town['placements'] if p['kind'] == 'building'}
    scene = (ROOT / 'Assets/Scenes/Country.unity').read_text()
    guid = re.search(r'^guid: (\w+)', bundle.with_suffix('.townlayout.meta').read_text(), re.M)[1]
    if guid not in scene:
        raise ValueError('Country scene does not reference the exported TownLayout')
    surfaces = {}
    catalog = []
    for group, entries in GROUPS:
        for key, label, description in entries:
            place = placements[key]
            prototype = prototypes[place['key']]
            geometry = town['geometry'][prototype['geometry']]
            raw = meshes[prototype['geometry']]
            if len(raw) != 32 * geometry['vertices'] + 4 * sum(geometry['indices']):
                raise ValueError(f'Truncated mesh: {key}')
            xyz = [vertex[:3] for vertex in struct.iter_unpack('<8f', raw[:32 * geometry['vertices']])]
            low, high = zip(*[(min(axis), max(axis)) for axis in zip(*xyz)])
            for index in prototype['materials']:
                if index not in surfaces:
                    surface = surface_for(town['materials'][index], materials_dir, textures)
                    surfaces[index] = {**surface, 'base': copy_texture(surface['base']), 'normal': copy_texture(surface['normal'])}
            (media / (key + '.bin')).write_bytes(raw)
            catalog.append(dict(id=key, name=label, group=group, description=description,
                                prototype=place['key'], scale=place['scale'], low=low, high=high,
                                materials=prototype['materials'], **geometry))
    data = dict(models=catalog, surfaces=surfaces, source=hashlib.sha256(bundle.read_bytes()).hexdigest())
    (output / 'catalog.json').write_text(json.dumps(data, ensure_ascii=False, separators=(',', ':')) + '\n')


if __name__ == '__main__':
    export()
