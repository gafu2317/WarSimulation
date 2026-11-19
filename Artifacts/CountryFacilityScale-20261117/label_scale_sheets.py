import json
from pathlib import Path
import shutil
import sys

from PIL import Image, ImageDraw, ImageFont

ROOT = Path.cwd()
WORK = ROOT/'Artifacts/CountryFacilityScale-20261117'
OUT = Path('/Users/fukutomi/.codex/visualizations/2026/11/16/01a1e27b-83d1-7fa0-a1cf-a38d39658bb0/CountryFacilityScale')
FONT = ROOT/'Assets/Fonts/Noto_Sans_JP/static'

SHEETS = {
    '01_storeys': ('住宅と二階建ての施設', '住宅の二階の境目と、酒場・診療所の一階の高さを見比べる。',
                   ('Fantasy_House', 'Tavern', 'Clinic')),
    '02_schools': ('学校と講堂', '回廊は住宅に近い階高、講堂や研究塔は用途に合わせた高さ。',
                   ('WarriorAcademy_Lv5', 'ArcaneAcademy_Lv5', 'SpiritAcademy_Lv5')),
    '03_landmarks': ('教会・天文台・衛兵所', '教会は19.34 m。望遠鏡と見張り塔の高さを見比べる。',
                     ('Church', 'Observatory', 'Guardhouse')),
    '04_public': ('図書館・美術館・闘技場', '閲覧室、展示室、客席の用途が分かるように幅と高さに差をつけた。',
                  ('Library', 'Museum', 'Arena')),
    '05_venues': ('舞踏館・カジノ・仮面のサロン', '舞踏館は吹き抜け、カジノは集会室、サロンは小部屋のある建物。',
                  ('Ballroom', 'Casino', 'MasqueradeSalon')),
    '06_small_scale': ('鍛冶場・演芸館・中庭館', '鍛冶場は作業床、演芸館は舞台、中庭館は二階の回廊に合わせた。',
                       ('Blacksmith', 'CrimsonRowhouse', 'VeiledCourtyard')),
}
LABELS = dict(zip(
    ('Fantasy_House','Tavern','Clinic','WarriorAcademy_Lv5','ArcaneAcademy_Lv5','SpiritAcademy_Lv5',
     'Church','Observatory','Guardhouse','Library','Museum','Arena','Ballroom','Casino','MasqueradeSalon',
     'Blacksmith','CrimsonRowhouse','VeiledCourtyard'),
    ('二階建て住宅','酒場','診療所','戦士学校','魔法学校','精霊使役学校',
     '教会','天文台','衛兵所','図書館','美術館','闘技場','舞踏館','カジノ','仮面のサロン',
     '鍛冶場','演芸館','中庭館')
))
MEASURES = json.loads((WORK/'scale_measurements.json').read_text())
house = [v*.98 for v in (7.378, 8.578, 8.353)]
MEASURES['Fantasy_House'] = {'before':house,'after':house}
f_title = ImageFont.truetype(str(FONT/'NotoSansJP-Bold.ttf'), 46)
f_body = ImageFont.truetype(str(FONT/'NotoSansJP-Regular.ttf'), 25)
f_caption = ImageFont.truetype(str(FONT/'NotoSansJP-Bold.ttf'), 29)
f_detail = ImageFont.truetype(str(FONT/'NotoSansJP-Regular.ttf'), 23)
f_row = ImageFont.truetype(str(FONT/'NotoSansJP-Bold.ttf'), 27)
INK, SUB, ACCENT = '#182b2e', '#4c5859', '#215f78'


def title_bars(draw, title, note):
    draw.rectangle((0,0,2532,110),fill='#f3f0ea')
    draw.text((68,17),title,font=f_title,fill=INK)
    draw.text((70,75),note,font=f_body,fill=SUB)
    draw.text((2462,26),'同じ縮尺  ／  青い人型 1.70 m',font=f_body,fill=ACCENT,anchor='ra')
    draw.line((2268,78,2348,78),fill=ACCENT,width=3)
    draw.line((2268,72,2268,85),fill=ACCENT,width=3)
    draw.line((2348,72,2348,85),fill=ACCENT,width=3)
    draw.text((2360,67),'5 m',font=f_detail,fill=ACCENT)


def caption(draw, name, x, y, stage):
    m = MEASURES[name]
    sizes = ' × '.join(f'{n:.1f}' for n in m['before' if stage==0 else 'after'])
    storey = None
    if name=='Fantasy_House':
        storey='二階の境目 2.55 m'
    elif 'old_levels' in m:
        floor, upper, _ = m['old_levels' if stage==0 else 'new_levels']
        part={'CrimsonRowhouse':'玄関棟の一階','VeiledCourtyard':'回廊の一階'}.get(name,'一階の高さ')
        storey=f'{part} {upper-floor:.2f} m'
    draw.text((x,y),LABELS[name],font=f_caption,fill=INK,anchor='ma')
    draw.text((x,y+39),f'{sizes} m' + (f'  ／  {storey}' if storey else ''),font=f_detail,fill=SUB,anchor='ma')


def render(stem, data):
    title,note,names = data
    source=WORK/f'{stem}_raw.png'
    image=Image.open(source).convert('RGB')
    assert image.size==(2532,1170)
    image.paste(image.crop((0,0,2532,529)), (0,17))
    draw=ImageDraw.Draw(image)
    title_bars(draw,title,note)
    for stage, (y, head, badge) in enumerate(((546,'現行','#f6f3ed'),(1051,'調整案','#e1eef1'))):
        color='#f5f2ec' if stage==0 else '#eef4f3'
        draw.rectangle((0,y,2532,618 if stage==0 else 1170),fill=color)
        draw.rounded_rectangle((60,y+14,170,y+62),radius=8,fill=badge,outline='#c3cecb',width=1)
        draw.text((115,y+19),head,font=f_row,fill=INK if stage==0 else ACCENT,anchor='ma')
        for x,name in zip((438,1266,2094),names):
            caption(draw,name,x,y+2 if stage==0 else y+7,stage)
    draw.text((2464,1130),'幅 × 奥行 × 高さ',font=f_detail,fill=SUB,anchor='ra')
    image.save(OUT/f'{stem}.png')


OUT.mkdir(parents=True,exist_ok=True)
for stem,data in SHEETS.items():
    if len(sys.argv)==1 or stem in sys.argv[1:]:
        render(stem,data)
street=WORK/'02_avenue.png'
if street.exists() and len(sys.argv)==1:
    shutil.copyfile(street,OUT/'07_town.png')
