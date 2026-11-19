"""Draw the verified town plan with labels and a shared metric scale."""

import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

from build_country_redesign import BRIDGES, ROOT, coords, rectangle, route_boxes


COLORS = {
    'Royal precinct':'#3f6683', 'Cathedral close':'#457474', 'Civic quarter':'#5c777a',
    'Academy quarter':'#435b88', 'Entertainment quarter':'#89546a', 'Gate ward':'#6d7463',
    'Tournament quarter':'#8c5b4d', 'Market quarter':'#977448', 'Craft and freight quarter':'#8b745d',
    'Military quarter':'#786a59', 'Residential':'#c6a584', 'Western residential':'#9d8263', 'Mill quay':'#8c7560',
}
FONTS = ROOT / 'Assets/Fonts/Noto_Sans_JP/static'
SCALE = 3.5
ORIGIN = 1266, 118


def font(size, weight='Regular'):
    return ImageFont.truetype(str(FONTS / f'NotoSansJP-{weight}.ttf'),size)


def xy(x,z):
    return ORIGIN[0]+x*SCALE, ORIGIN[1]+(168-z)*SCALE


def draw_plan(plan):
    canvas = Image.new('RGB',(2532,1170),'#f5f1e8')
    d = ImageDraw.Draw(canvas)
    ink, light = '#263a36','#65726b'

    def text(x,y,value,size=26,weight='Regular',fill=ink,anchor=None):
        d.text((x,y),value,font=font(size,weight),fill=fill,anchor=anchor)

    def box(shape,color,outline=None):
        d.rectangle((*xy(shape.left,shape.north),*xy(shape.right,shape.south)),fill=color,outline=outline,width=1)

    def section(x,y,color,title,lines):
        d.line((x,y+15,x+40,y+15),fill=color,width=7)
        text(x+56,y-11,title,31,'Bold')
        for i,line in enumerate(lines):
            text(x+56,y+42+i*43,line,25,fill=light)

    wall = [xy(*p) for p in coords({'points':plan['wallOutline']})]
    d.polygon(wall,fill='#e4d8bf')
    for square in plan['squares']:
        box(rectangle(square),'#ddd1b6')
    for green in plan['greens']:
        box(rectangle(green),'#b4be94')
    half = plan['riverWidth']/2
    z = plan['riverZ']
    d.rectangle((*xy(-120,z+half),*xy(120,z-half)),fill='#81a9ae')
    for frontage in plan['frontages']:
        for shape in route_boxes(frontage):
            box(shape,'#b9ad98')
    for entry in plan['accessPaths']+plan['amenityPaths']+plan.get('secondaryAccessPaths',[]):
        for shape in route_boxes(entry):
            box(shape,'#d1c2a9')
    kinds = {'lane':'#b9ad98','district':'#a89b85','avenue':'#746957','bridge':'#9f8a70'}
    for road in sorted(plan['roads'],key=lambda road:road['avenue']):
        for shape in route_boxes(road):
            box(shape,kinds[road['kind']])
        if road['name'] in BRIDGES:
            x = road['points'][0]['x']
            span = road['width']/2+.55
            d.rectangle((*xy(x-span,z+half+.5),*xy(x+span,z-half-.5)),outline='#645a49',width=2)
    dwellings = ('#c49b78','#bc9b7e','#bfaa91','#c9aa87','#c3a28b')
    for i,plot in enumerate(plan['buildings']):
        shape = rectangle(plot)
        label = plot.get('label')
        color = COLORS[plot['zone']] if label else dwellings[i%len(dwellings)]
        box(shape,color,'#75644f')
        if label:
            threshold = max(12,min(19,int(((shape.right-shape.left)*SCALE-7)/len(label))))
            text(*xy(plot['x'],plot['z']),label,threshold,'Bold','#fffaec','mm')
    for p in plan['props']:
        name = p['name']
        px,py = xy(p['x'],p['z'])
        if name.startswith(('Tree_','NaturalTree_')):
            radius = 4 if name=='Tree_AlleyCypress' else 6
            d.ellipse((px-radius,py-radius,px+radius,py+radius),fill='#728a56')
        elif name=='Well':
            d.ellipse((px-9,py-9,px+9,py+9),fill='#2d6372',outline='#fff9e9',width=2)
        elif name=='Bench':
            d.rectangle((px-5,py-2,px+5,py+2),fill='#825f41')
        elif name.endswith('_Stall'):
            d.rounded_rectangle((px-4,py-5,px+4,py+5),radius=1,fill='#c8874f')
        else:
            d.ellipse((px-1.8,py-1.8,px+1.8,py+1.8),fill='#775f48')
    d.line([*wall,wall[0]],fill='#565a4e',width=4,joint='curve')
    for tower in plan['watchtowers']:
        x,y = xy(tower['x'],tower['z'])
        d.rectangle((x-7,y-7,x+7,y+7),fill='#565a4e')
    for gate,label,offset in zip(plan['gates'],('南門','東門'),((0,26),(38,0))):
        x,y = xy(gate['x'],gate['z'])
        d.rectangle((x-8,y-8,x+8,y+8),fill='#a78853',outline='#f5f1e8',width=2)
        text(x+offset[0],y+offset[1],label,21,'Bold',ink,'mm')
    text(*xy(-15,-28.6),'市場',19,'Bold','#4f452e','mm')

    x,y = 1704,155
    d.polygon(((x,y-26),(x-8,y-5),(x+8,y-5)),fill=ink)
    d.line((x,y-5,x,y+15), fill=ink,width=2)
    text(x,y+32,'北',20,'Bold',ink,'mm')
    x,y = 1520,1119
    d.line((x,y,x+30*SCALE,y),fill=ink,width=2)
    for n in (0,15,30):
        xx = x+n*SCALE
        d.line((xx,y-5,xx,y+5),fill=ink,width=2)
    text(x+15*SCALE,y+21,'30 m',18,fill=light,anchor='mm')

    text(92,90,'城壁都市の新配置',62,'Bold')
    text(96,185,'店先と路地に生活の道具を置く',30,'Medium',light)
    d.line((98,256,723,256),fill='#c8bdaa',width=2)
    section(98,315,kinds['avenue'],'南北大通り 12 m',('南門から市場と中央橋を経て王城へ。',))
    section(98,456,kinds['avenue'],'東門大通り 9 m',('東門と市場を結び、美術館と', '演芸館の正面を通る。'))
    section(98,641,kinds['district'],'扉と階段への道',('戸口・門・階段に舗装をつなぐ。','戦士学校は訓練人形の脇を通る。'))
    section(98,825,kinds['lane'],'住宅の路地と庭',('玄関脇に壺や籠、裏庭に薪や物干し。','井戸とベンチは通り沿いに置く。'))
    d.line((98,1010,723,1010),fill='#c8bdaa',width=2)
    count = plan['counts']
    text(98,1044,f'住宅 {count["houses"]}軒   ／   戸口への道 {count["accessPaths"]}本',25,'Medium')
    kinds = len({p['name'] for p in plan['props']})
    text(98,1087,f'小物 {kinds}種類   ／   {count["props"]:,}点',25,'Medium')

    section(1786,168,COLORS['Academy quarter'],'三つの学校',('戦士学校は西、魔法学校は東。', '精霊使役学校は王城の東の街区。'))
    section(1786,348,COLORS['Entertainment quarter'],'歓楽施設',('舞踏館は西、仮面のサロンは川の北。', '南の街区に演芸館・中庭館・カジノ。'))
    section(1786,529,COLORS['Civic quarter'],'公共施設',('教会は西、診療所は川の北。', '美術館は東門側、図書館は北東。'))
    section(1786,709,COLORS['Market quarter'],'市場と荷捌き',('露店の脇に商品、通路の外に荷車。', '倉庫側に樽・木箱・麻袋をまとめる。'))
    d.line((1786,887,2422,887),fill='#c8bdaa',width=2)
    for row,(color,name) in enumerate((('#2d6372','井戸'),('#728a56','木'),('#825f41','ベンチ'),('#775f48','小物'))):
        x = 1788+row*154
        d.rounded_rectangle((x,933,x+19,952),radius=4,fill=color)
        text(x+32,929,name,24,fill=light)
    return canvas


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('plan',type=Path)
    parser.add_argument('image',type=Path)
    args = parser.parse_args()
    args.image.parent.mkdir(parents=True,exist_ok=True)
    draw_plan(json.loads(args.plan.read_text())).save(args.image)


if __name__ == '__main__':
    main()
