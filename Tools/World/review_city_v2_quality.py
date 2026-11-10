"""Lay out native Unity captures without changing their rendering."""
import json
import re
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
REPORT = ROOT / 'Artifacts/CityV2Quality20261030'
FONT = str(ROOT / 'Assets/Fonts/Noto_Sans_JP/static/NotoSansJP-Regular.ttf')


def comparison(left, right, output, labels, title):
    with Image.open(left) as source:
        height = round(960 * source.height / source.width)
    canvas = Image.new('RGB', (1920, height + 90), '#eeece5')
    draw = ImageDraw.Draw(canvas)
    for i, path in enumerate((left, right)):
        with Image.open(path) as source:
            canvas.paste(source.convert('RGB').resize((960, height), Image.Resampling.LANCZOS), (i * 960, 45))
        draw.text((i * 960 + 15, 5), labels[i], font=ImageFont.truetype(FONT, 26), fill='#222222')
    draw.text((15, height + 51), title, font=ImageFont.truetype(FONT, 22), fill='#222222')
    canvas.save(output, quality=95)


def sheets(phase):
    folder = REPORT / phase
    records = json.loads((folder / 'inventory.json').read_text())
    out = folder / 'Sheets'
    out.mkdir(exist_ok=True)
    font = ImageFont.truetype(FONT, 16)
    for group in sorted({r['group'] for r in records}):
        members = [r for r in records if r['group'] == group]
        for rear in (False, True) if group in ('Buildings', 'Academies') else (False,):
            for start in range(0, len(members), 12):
                page = members[start:start + 12]
                image = Image.new('RGB', (1440, 304 * ((len(page) + 3) // 4)), '#eeece5')
                draw = ImageDraw.Draw(image)
                for i, item in enumerate(page):
                    name = item['name'] + ('_Rear' if rear else '')
                    shot = Image.open(folder / 'Models' / (name + '.png')).resize((360, 270), Image.Resampling.LANCZOS)
                    x, y = i % 4 * 360, i // 4 * 304
                    image.paste(shot, (x, y))
                    draw.text((x + 5, y + 275), item['name'], font=font, fill='#222222')
                image.save(out / f'{group}_{start // 12 + 1}{"_Rear" if rear else ""}.jpg', quality=94)


def gallery():
    records = json.loads((REPORT / 'After/inventory.json').read_text())
    cards = []
    for item in records:
        name = item['name']
        rear = f'<a href="After/Models/{name}_Rear.png">背面</a>' if item['group'] in ('Buildings', 'Academies') else ''
        cards.append(f'<article><h2>{item.get("label") or name} <small>{name}</small></h2><div class="pair"><a href="Before/Models/{name}.png"><img loading="lazy" src="Before/Models/{name}.png"><span>変更前</span></a><a href="After/Models/{name}.png"><img loading="lazy" src="After/Models/{name}.png"><span>変更後</span></a></div>{rear}</article>')
    street_cards = []
    for name, label in [('Overview','国の全景'),('Market','市場'),('Residential','住宅街'),('Church','教会'),('Palace','城'),('Library','図書館'),('Observatory','天文台'),('Workshop','鍛冶場'),('River','川と水車'),('MarketHall','市場会館')]:
        if not (REPORT / f'Streets/New_{name}.png').exists():continue
        file = f'Comparison_{name}.jpg'
        comparison(REPORT / f'Streets/Old_{name}.png', REPORT / f'Streets/New_{name}.png', REPORT / file,
                   ('旧モデル', '新モデル・修正後'), label + ' ／ 同じカメラ・照明による実物比較')
        street_cards.append(f'<article><h2>{label}</h2><a href="{file}"><img style="width:100%" loading="lazy" src="{file}"></a></article>')
    for name, label in [('Road_Cross','石畳の彩度と明るさ'),('Observatory','天文台のガラス屋根・観測口・望遠鏡'),('Museum','美術館の屋根・柱・彫刻'),('Bathhouse','浴場のドームと採光'),('Blacksmith','鍛冶場の支柱・炉・作業道具'),('Covered_Wagon','荷車の支持材と幌'),('Weapon_Rack','剣の形と保持具'),('LanternSpire','上階への階段・開口・手すり')]:
        comparison(REPORT / f'Before/Models/{name}.png', REPORT / f'After/Models/{name}.png', REPORT / f'BeforeAfter_{name}.jpg', ('新モデル・変更前','新モデル・変更後'),label)
    html = '<!doctype html><html lang="ja"><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>新モデルの材質・構造の確認</title><style>body{margin:32px;background:#eeece5;color:#292b29;font:16px system-ui}main{max-width:1440px;margin:auto}h1{font-size:28px}h2{font-size:20px}small{font-size:14px;font-weight:400;color:#555}.pair{display:grid;grid-template-columns:1fr 1fr;gap:12px}.pair img{width:100%;display:block}.pair a{position:relative;color:inherit}.pair span{position:absolute;top:12px;left:12px;background:#eee;padding:6px 10px}article{margin:30px 0 45px}a{color:#315e71}</style><main><h1>新モデルの材質・構造の確認</h1><p>Unityの実際のモデルを同じカメラ・照明で撮影。全141種類。建物と学校は背面も確認できる。</p><nav><a href="#streets">街並み10か所の新旧比較</a> ／ <a href="#models">全141種類の変更前後</a></nav><h2 id="streets">街並みの撮影記録</h2>' + ''.join(street_cards) + '<h2 id="models">全モデルの変更前後</h2>' + ''.join(cards) + '</main></html>'
    if (REPORT / 'Layout/index.html').exists():
        html = html.replace('<nav>', '<p><a href="Layout/">現在の街区配置：全景と9地区の比較を見る</a></p><nav>', 1)
    if (REPORT / 'Identity/index.html').exists():
        html = html.replace('<nav>', '<p><a href="Identity/">施設の大きな目印：19建築の中距離・近距離の比較を見る</a></p><nav>', 1)
    revision = str((REPORT / 'After/inventory.json').stat().st_mtime_ns)
    html = re.sub(r'((?:src|href)="[^"]+\.(?:png|jpg))"', lambda m: f'{m[1]}?v={revision}"', html)
    (REPORT / 'index.html').write_text(html)


if __name__ == '__main__':
    for phase in ('Before', 'After'):
        sheets(phase)
    gallery()
