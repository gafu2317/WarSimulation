import json
from pathlib import Path
import sys

from PIL import Image, ImageDraw

ROOT = Path.cwd()
sys.path.insert(0, str(ROOT/'Tools/World'))
from draw_country_redesign import draw_plan, font, xy, rectangle, route_boxes

WORK = ROOT/'Artifacts/CountryStreetInfill-20261118'
OUT = Path('/Users/fukutomi/.codex/visualizations/2026/11/16/01a1e27b-83d1-7fa0-a1cf-a38d39658bb0/CountryStreetInfill')
OUT.mkdir(parents=True,exist_ok=True)
BEFORE = json.loads((ROOT/'Art/CountryLayout/town_plan.json').read_text())
AFTER = json.loads((WORK/'town_plan.json').read_text())
CHANGES = json.loads((WORK/'layout_changes.json').read_text())
CROP = (840,112,1694,1133)
left, right = (62,142), (1008,142)
ink, secondary = '#263632','#52615a'
new, moved, lane = '#27716c','#9b5473','#597bad'
image = Image.new('RGB',(2532,1170),'#f5f1e8')

for p,origin in ((BEFORE,left),(AFTER,right)):
    crop=draw_plan(p).crop(CROP)
    image.paste(crop,origin)
draw=ImageDraw.Draw(image,'RGBA')

def text(at,label,size,weight='Regular',fill=ink,anchor=None):
    draw.text(at,label,font=font(size,weight),fill=fill,anchor=anchor)

def map_xy(x,z):
    a,b=xy(x,z)
    return right[0]+a-CROP[0],right[1]+b-CROP[1]

def outline(box,color,width=3,fill=None):
    draw.rectangle((*map_xy(box.left,box.north),*map_xy(box.right,box.south)),
                   outline=color,fill=fill,width=width)

new_roads={road['name'] for road in AFTER['roads']}-{road['name'] for road in BEFORE['roads']}
for road in AFTER['roads']:
    if road['name'] in new_roads:
        for box in route_boxes(road):
            outline(box,lane,2,(125,157,198,84))

for entry in CHANGES['inserted']:
    outline(rectangle(entry),new,3,(70,135,124,70))
moved_at={tuple(v['after']) for key in ('facilities','relocated') for v in CHANGES[key]}
for entry in AFTER['buildings']:
    if (entry['x'],entry['z']) in moved_at:
        outline(rectangle(entry),moved,3)

text((64,14),'施設縮小後の空きを詰めた配置',52,'Bold')
text((1000,35),'同じ縮尺で比較  ／  上が北',25,'Medium',secondary)
draw.line((64,92,2468,92),fill='#c8bfad',width=2)
text((left[0]+427,103),'現行',31,'Bold',anchor='ma')
text((right[0]+427,103),'詰め直し案',31,'Bold',anchor='ma')
draw.line((1900,148,1900,1110),fill='#c8bfad',width=2)

x=1938
text((x,171),'建物',27,'Medium',secondary)
text((x,214),f'{BEFORE["counts"]["plots"]} → {AFTER["counts"]["plots"]} 棟',49,'Bold')
before_area=sum(p['width']*p['depth'] for p in BEFORE['buildings'])
after_area=sum(p['width']*p['depth'] for p in AFTER['buildings'])
text((x,301),'建物の外形面積',27,'Medium',secondary)
text((x,346),f'+{after_area-before_area:,.0f} m²',43,'Bold')
text((x,403),'配置表の幅と奥行で計測',23,fill=secondary)
draw.line((x,465,2468,465),fill='#c8bfad',width=2)

for i,(color,title,desc) in enumerate((
    (new,'新しく置いた建物','住宅14・商家4・工房付き住宅5'),
    (moved,'動かした建物','施設8棟、住宅4軒を寄せ直した'),
    (lane,'通り抜けられる路地','診療所の裏手と鍛冶場の裏手'),
)):
    y=512+i*131
    draw.rectangle((x,y+4,x+31,y+34),outline=color,width=4)
    text((x+47,y),title,29,'Bold')
    text((x+48,y+49),desc,24,fill=secondary)

draw.line((x,927,2468,927),fill='#c8bfad',width=2)
text((x,962),f'玄関への経路  {BEFORE["counts"]["accessPaths"]} → {AFTER["counts"]["accessPaths"]} 本',26,'Medium')
text((x,1008),'大通り沿いの市場と井戸へ通じる',23,fill=secondary)
draw.line((x,1098,x+105,1098),fill=ink,width=2)
for dx in (0,52.5,105):
    draw.line((x+dx,1092,x+dx,1104),fill=ink,width=2)
text((x+122,1082),'30 m',22,fill=secondary)
image.save(OUT/'00_comparison.png')
