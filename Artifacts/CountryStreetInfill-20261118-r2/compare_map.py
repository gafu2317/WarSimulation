from pathlib import Path
import json
import sys

from PIL import Image, ImageDraw

WORK = Path(__file__).resolve().parent
ROOT = Path.cwd()
sys.path.insert(0, str(ROOT/'Tools/World'))
from draw_country_redesign import draw_plan, font, xy, rectangle, route_boxes

before = json.loads((WORK.with_name('CountryStreetInfill-20261118')/'town_plan.json').read_text())
after = json.loads((WORK/'town_plan.json').read_text())
changes = json.loads((WORK/'layout_changes.json').read_text())
crop = (840,112,1694,1133)
left, right = (62,142), (1008,142)
dark, secondary = '#283832','#4d5c53'
shop, home, lane, garden = '#994b32','#196477','#4b619b','#526f35'
image = Image.new('RGB',(2532,1170),'#f5f1e8')

for plan,origin in ((before,left),(after,right)):
    image.paste(draw_plan(plan).crop(crop),origin)
draw = ImageDraw.Draw(image,'RGBA')

def label(x,y,value,size,weight='Regular',fill=dark,anchor=None):
    draw.text((x,y),value,font=font(size,weight),fill=fill,anchor=anchor)

def map_point(x,z):
    a,b = xy(x,z)
    return right[0]+a-crop[0],right[1]+b-crop[1]

def outline(box,color,fill):
    draw.rectangle((*map_point(box.left,box.north),*map_point(box.right,box.south)),
                   outline=color,fill=fill,width=3)

old_roads = {r['name'] for r in before['roads']}
lanes = [r for r in after['roads'] if r['name'] not in old_roads]
lanes += [r for r in after['amenityPaths'] if r['name']=='Washing' and r['points'][0]['x']==35.93]
for route in lanes:
    for box in route_boxes(route):
        outline(box,lane,(99,119,183,96))
for plot in changes['inserted']:
    work = plot['name']=='WorkshopHouse'
    outline(rectangle(plot),shop if work else home,(194,99,63,112) if work else (56,149,173,115))
for area in changes['areas']:
    outline(rectangle(area),garden,(122,157,83,115))

label(64,14,'残っていた空き地を詰めた配置',50,'Bold')
label(1000,37,'同じ縮尺で比較　／　上が北',25,'Medium',secondary)
draw.line((64,92,2468,92),fill='#c8bfad',width=2)
label(left[0]+427,104,'前案',31,'Bold',anchor='ma')
label(right[0]+427,104,'今回の案',31,'Bold',anchor='ma')
draw.line((1900,148,1900,1110),fill='#c8bfad',width=2)

sx=1938
label(sx,171,'建物',27,'Medium',secondary)
label(sx,216,f'{before["counts"]["plots"]} → {after["counts"]["plots"]} 棟',49,'Bold')
label(sx,301,'建物が占める面積',27,'Medium',secondary)
area = lambda plan: sum(p['width']*p['depth'] for p in plan['buildings'])
label(sx,346,f'+{area(after)-area(before):,.0f} m²',43,'Bold')
label(sx,403,'配置表の幅と奥行で計測',23,fill=secondary)
draw.line((sx,465,2468,465),fill='#c8bfad',width=2)

for i,(color,title,desc) in enumerate((
    (shop,'工房付き住宅','学校の裏や街道沿いに5軒'),
    (home,'住宅','図書館の横や川沿いなどに5軒'),
    (lane,'新しい路地','精霊使役学校と舞踏館の裏手'),
    (garden,'休憩・植栽','闘技場・診療所・美術館のそば'),
)):
    y=505+i*105
    draw.rectangle((sx,y+4,sx+31,y+34),outline=color,width=4)
    label(sx+47,y,title,29,'Bold')
    label(sx+48,y+45,desc,24,fill=secondary)

draw.line((sx,929,2468,929),fill='#c8bfad',width=2)
label(sx,966,f'玄関への経路  {before["counts"]["accessPaths"]} → {after["counts"]["accessPaths"]} 本',26,'Medium')
draw.line((sx,1098,sx+105,1098),fill=dark,width=2)
for dx in (0,52.5,105):
    draw.line((sx+dx,1092,sx+dx,1104),fill=dark,width=2)
label(sx+122,1082,'30 m',22,fill=secondary)
image.save(Path(sys.argv[1]))
