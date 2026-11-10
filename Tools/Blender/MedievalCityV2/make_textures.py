"""Author reusable part textures; no existing models or images are read."""
from pathlib import Path
import json
import math

import numpy as np
from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'ArtSource/Deliveries/MedievalCity_2026-10-22'
SIZE = 1024
RNG = np.random.default_rng(221026)
RECORDS = []


def surface(name, color, kind, scale=1.0):
    size = SIZE
    y, x = np.mgrid[0:size, 0:size]
    fine = RNG.normal(0, 1, (size, size))
    cloud = np.asarray(Image.fromarray(RNG.integers(0, 256, (32, 32), dtype='uint8')).resize((size, size), Image.Resampling.BICUBIC), dtype=float) / 255 - .5
    grain = .35 * fine + 12 * cloud
    rgb = np.clip(np.asarray(color)[None, None, :] + grain[:, :, None], 0, 255).astype('uint8')
    image = Image.fromarray(rgb)
    height = Image.new('L', (size, size), 128)
    draw, hd = ImageDraw.Draw(image), ImageDraw.Draw(height)
    if kind in ('stone', 'cobble', 'brick'):
        rows = 8 if kind != 'brick' else 12
        step = size // rows
        for row in range(rows + 1):
            cy = row * step
            w = int(step * (1.6 if kind == 'stone' else 1.1 if kind == 'cobble' else 2.4))
            offset = (row % 2) * w // 2
            for left in range(-w + offset, size, w):
                v = RNG.integers(-13, 14)
                col = tuple(int(np.clip(c + v, 0, 255)) for c in color)
                bounds = (left + 5, cy + 5, left + w - 5, cy + step - 5)
                draw.rounded_rectangle(bounds, radius=10 if kind == 'cobble' else 4, fill=col, outline=tuple(max(c - 22, 0) for c in col), width=3)
                hd.rounded_rectangle(bounds, radius=9, fill=int(190 + v))
                draw.line((left + 13, cy + 10, left + w - 14, cy + 10), fill=tuple(min(c + 15, 255) for c in col), width=3)
    elif kind == 'rock':
        for i in range(26):
            xx, yy = RNG.integers(-150, size + 100, 2)
            points = [(int(xx + math.cos(a) * RNG.uniform(70, 190)),
                       int(yy + math.sin(a) * RNG.uniform(45, 145))) for a in np.linspace(0, 2 * math.pi, 7)[:-1]]
            shade = int(RNG.integers(-9, 10))
            draw.polygon(points, fill=tuple(int(np.clip(c + shade, 0, 255)) for c in color))
            hd.polygon(points, fill=128 + shade)
        image = image.filter(ImageFilter.GaussianBlur(5))
        draw = ImageDraw.Draw(image)
        for i in range(5):
            start = int(RNG.integers(0, size))
            points = [(xx, start + 35 * math.sin(xx / 170 + i)) for xx in range(-30, size + 30, 25)]
            draw.line(points, fill=tuple(min(c + 12, 255) for c in color), width=4)
    elif kind == 'plaster':
        for i in range(45):
            xx,yy=RNG.integers(-160,size+100,2)
            shade=int(RNG.integers(-6,7))
            col=tuple(int(np.clip(c+shade,0,255)) for c in color)
            points=[(int(xx),int(yy)),(int(xx+220),int(yy+35)),(int(xx+160),int(yy+90)),(int(xx-50),int(yy+55))]
            draw.polygon(points,fill=col)
        image=image.filter(ImageFilter.GaussianBlur(9));draw=ImageDraw.Draw(image)
    elif kind == 'copper':
        for i in range(4):
            shade = int(RNG.integers(-7, 8))
            col = tuple(int(np.clip(c + shade, 0, 255)) for c in color)
            draw.rectangle((i * 256 + 3, 0, (i + 1) * 256 - 3, size), fill=col)
            draw.line((i * 256, 0, i * 256, size), fill=tuple(max(c - 19, 0) for c in color), width=6)
            draw.line((i * 256 + 8, 0, i * 256 + 8, size), fill=tuple(min(c + 13, 255) for c in color), width=3)
            hd.line((i * 256, 0, i * 256, size), fill=100, width=6)
    elif kind == 'bark':
        for i in range(34):
            start=i*32
            points=[(start+12*math.sin(yy/135+i)+5*math.sin(yy/40),yy) for yy in range(-10,size+20,15)]
            draw.line(points,fill=(56+i%3*6,46+i%4*3,29),width=7+i%4)
            hd.line(points,fill=80+i%5*8,width=8)
            draw.line([(xx+10,yy) for xx,yy in points],fill=(119,99,61),width=3)
    elif kind == 'wood':
        for i in range(8):
            left = i * 128
            draw.line((left, 0, left, size), fill=(34, 24, 18), width=6)
            hd.line((left, 0, left, size), fill=50, width=6)
            for j in range(13):
                start = left + 9 + j * 8
                points = [(start + 4 * math.sin(k * .3 + i + j), k * 16) for k in range(65)]
                draw.line(points, fill=tuple(max(c - 15 - j % 3 * 3, 0) for c in color), width=2)
                hd.line(points, fill=105 + j % 5 * 3, width=2)
            if i % 2:
                yy = int(RNG.integers(100, 880))
                for r in range(2, 20, 4):
                    draw.ellipse((left + 52 - r, yy - r * 2, left + 52 + r, yy + r * 2), outline=tuple(max(c - 25, 0) for c in color), width=2)
    elif kind == 'roof':
        for row in range(12):
            top = row * 86 - 22
            for col in range(-1, 9):
                left = col * 128 + (row % 2) * 64
                shade = int(RNG.integers(-16, 17))
                color2 = tuple(int(np.clip(c + shade, 0, 255)) for c in color)
                draw.rounded_rectangle((left + 2, top, left + 126, top + 119), radius=24, fill=color2, outline=tuple(max(c - 27, 0) for c in color2), width=5)
                hd.rounded_rectangle((left + 2, top, left + 126, top + 119), radius=24, fill=180 + shade, outline=75, width=5)
                draw.line((left + 14, top + 104, left + 113, top + 104), fill=tuple(min(c + 14, 255) for c in color2), width=2)
    elif kind == 'timber':
        light = (67, 44, 28)
        for pts, width in [((0, 0, size, 0), 38), ((0, size // 2, size, size // 2), 30), ((0, size, size, size), 38), ((0, 0, 0, size), 38), ((size, 0, size, size), 38), ((size // 2, 0, size // 2, size), 32), ((24, 28, 495, 490), 26), ((535, 490, 1000, 28), 26), ((24, 990, 490, 535), 26), ((535, 535, 998, 990), 26)]:
            draw.line(pts, fill=light, width=width)
            hd.line(pts, fill=193, width=width)
        for yy in (26, 504, 997):
            draw.line((0, yy, size, yy), fill=(110, 77, 46), width=3)
    elif kind == 'cloth':
        for yy in range(0, size, 6):
            draw.line((0, yy, size, yy), fill=tuple(max(c - 8, 0) for c in color), width=1)
        for xx in range(0, size, 8):
            draw.line((xx, 0, xx, size), fill=tuple(min(c + 8, 255) for c in color), width=1)
        if 'Stripe' in name:
            for xx in range(0, size, 256):
                draw.rectangle((xx, 0, xx + 126, size), fill=(216, 201, 161))
    elif kind == 'potion_glass':
        for yy in range(size):
            shade=.72+.28*math.sin(yy/size*math.pi)
            draw.line((0,yy,size,yy),fill=tuple(int(c*shade) for c in color))
        draw.polygon([(150,130),(225,170),(278,750),(225,850)],fill=tuple(min(255,int(c*.55+110)) for c in color))
        draw.line([(183,190),(215,430),(230,670)],fill=tuple(min(255,c+105) for c in color),width=14)
        draw.polygon([(650,250),(680,230),(730,650),(700,690)],fill=tuple(min(255,c+35) for c in color))
    elif kind == 'metal':
        for i in range(100):
            xx, yy = RNG.integers(0, size, 2)
            draw.line((int(xx), int(yy), int(xx + RNG.integers(5, 30)), int(yy + 1)), fill=tuple(min(c + 13, 255) for c in color), width=1)
    elif kind == 'thatch':
        for xx in range(0, size, 7):
            shift = int(RNG.integers(-5, 6))
            draw.line((xx, 0, xx + shift, size), fill=(148 + xx % 20, 112 + xx % 20, 52), width=3)
            hd.line((xx, 0, xx + shift, size), fill=95 + xx % 45, width=2)
    elif kind == 'leaf':
        for i in range(330):
            xx, yy = RNG.integers(-10, size, 2)
            v = int(RNG.integers(-22, 24))
            draw.ellipse((int(xx), int(yy), int(xx + 30), int(yy + 49)), fill=tuple(int(np.clip(c + v, 0, 255)) for c in color))
            hd.ellipse((int(xx), int(yy), int(xx + 30), int(yy + 49)), fill=160 + v)
    elif kind == 'water':
        for i in range(32):
            yy = i * 32
            draw.arc((yy % 200 - 100, yy - 20, size - yy % 180, yy + 40), 0, 160, fill=(91, 145, 140), width=3)
    elif kind=='grain':
        for i in range(1600):
            xx,yy=RNG.integers(-10,size,2)
            shade=int(RNG.integers(-18,19))
            col=tuple(int(np.clip(c+shade,0,255)) for c in color)
            draw.ellipse((int(xx),int(yy),int(xx+12),int(yy+25)),fill=col)
            draw.line((int(xx+6),int(yy+5),int(xx+6),int(yy+20)),fill=tuple(max(c-24,0) for c in col),width=2)
    elif kind=='bread':
        for yy in (250,500,750):
            draw.line((280,yy+75,730,yy-70),fill=(220,169,96),width=46)
            draw.line((285,yy+56,720,yy-85),fill=(145,86,35),width=9)
            hd.line((280,yy+75,730,yy-70),fill=70,width=35)
    elif kind=='fish_skin':
        for yy in range(size):
            t=yy/size
            shade=.66+.3*math.sin(t*math.pi)**2
            draw.line((0,yy,size,yy),fill=tuple(int(c*shade) for c in color))
        for row in range(9):
            for col in range(7):
                xx=340+col*88+(row%2)*35;yy=100+row*96
                draw.arc((xx,yy,xx+78,yy+120),-70,65,fill=(105,139,144),width=3)
        for cy in (24,512,1000):
            draw.arc((110,cy-125,370,cy+125),-65,65,fill=(65,98,102),width=14)
            draw.ellipse((138,cy-20,179,cy+20),fill=(25,40,40))
            draw.ellipse((144,cy-17,153,cy-8),fill=(215,223,204))
    # Surface wear is reusable material information, never a photograph of a building.
    arr = np.asarray(image).astype(float)
    arr += fine[:, :, None] * .3 + cloud[:, :, None] * 5
    image = Image.fromarray(np.clip(arr, 0, 255).astype('uint8'))
    rough=.24 if kind=='potion_glass' else .18 if name.startswith(('Window_','Glass_','Liquid_')) else .63 if kind=='copper' else .88 if kind not in ('metal','water') else .36
    save(name, image, height, rough, scale, kind)


def save(name, image, height, rough, scale, kind):
    directory = OUT / 'Textures'
    directory.mkdir(parents=True, exist_ok=True)
    image.save(directory / f'{name}_BaseColor.png')
    h = np.asarray(height.filter(ImageFilter.GaussianBlur(1.8)), dtype=float) / 255
    dy, dx = np.gradient(h)
    n = np.dstack((-dx * 11, dy * 11, np.ones(h.shape)))
    n /= np.linalg.norm(n, axis=2)[:, :, None]
    Image.fromarray(np.clip((n * .5 + .5) * 255, 0, 255).astype('uint8')).save(directory / f'{name}_Normal.png')
    roughness = np.clip(rough * 255 + (h - .5) * 18, 0, 255).astype('uint8')
    Image.fromarray(roughness).save(directory / f'{name}_Roughness.png')
    RECORDS.append({'name': name, 'part': kind, 'tile_meters': scale, 'basecolor': f'Textures/{name}_BaseColor.png', 'normal': f'Textures/{name}_Normal.png', 'roughness': f'Textures/{name}_Roughness.png', 'resolution': list(image.size)})


def panel(name, kind, color=(110, 67, 34)):
    im = Image.new('RGB', (SIZE, SIZE), color)
    d = ImageDraw.Draw(im)
    h = Image.new('L', (SIZE, SIZE), 128)
    hd = ImageDraw.Draw(h)
    if kind == 'door':
        for xx in range(0, SIZE, 128):
            d.line((xx, 0, xx, SIZE), fill=(45, 28, 19), width=9)
            d.line((xx + 11, 0, xx + 11, SIZE), fill=(137, 91, 48), width=4)
            hd.line((xx, 0, xx, SIZE), fill=40, width=9)
            for g in range(8):
                d.line([(xx + g * 14 + 6 * math.sin(y / 95 + xx), y) for y in range(0, SIZE, 12)], fill=(89, 53, 28), width=2)
        for yy in (280, 740):
            d.rounded_rectangle((60, yy, 960, yy + 44), radius=16, fill=(46, 48, 44), outline=(102, 100, 79), width=4)
            hd.rectangle((60, yy, 960, yy + 44), fill=192)
            for xx in (90, 290, 510, 730, 920):
                d.ellipse((xx, yy + 12, xx + 13, yy + 25), fill=(165, 153, 111))
        d.ellipse((750, 493, 838, 581), outline=(180, 147, 66), width=17)
        d.ellipse((773, 469, 815, 511), fill=(193, 155, 74))
    elif kind == 'window':
        yy,xx=np.mgrid[0:SIZE,0:SIZE]
        value=np.asarray(color)[None,None,:]*(.65+.35*(1-yy/SIZE))[:,:,None]
        reflection=np.clip(1-np.abs((xx-yy*.44-SIZE*.12)/(SIZE*.12)),0,1)*.22
        value+=reflection[:,:,None]*np.asarray((110,135,155))[None,None,:]
        im=Image.fromarray(np.clip(value,0,255).astype('uint8'))
    elif kind == 'grate':
        d.rectangle((0, 0, SIZE, SIZE), fill=(25, 29, 27))
        for xx in range(40, SIZE, 120):
            d.rectangle((xx, 25, xx + 38, SIZE - 25), fill=(96, 97, 81), outline=(137, 133, 100), width=4)
        for yy in (70, 480, 920):
            d.rectangle((20, yy, SIZE - 20, yy + 32), fill=(80, 85, 75))
    elif kind == 'books':
        for xx in range(0, SIZE, 76):
            cc = [(87, 45, 34), (35, 69, 64), (55, 61, 93), (138, 111, 58)][xx // 76 % 4]
            d.rectangle((xx + 3, 0, xx + 70, SIZE), fill=cc, outline=(29, 24, 19), width=6)
            for yy in (110, 180, 780, 850):
                d.line((xx + 8, yy, xx + 63, yy), fill=(193, 159, 82), width=10)
    elif kind == 'produce':
        d.rectangle((0, 0, SIZE, SIZE), fill=(61, 56, 29))
        for yy in range(-40, SIZE, 110):
            for xx in range(-40, SIZE, 100):
                r = int(RNG.integers(29, 46))
                c = [(161, 47, 31), (157, 142, 50), (57, 102, 48)][int(RNG.integers(0, 3))]
                d.ellipse((xx, yy, xx + r * 2, yy + r * 2), fill=c, outline=tuple(int(v * .65) for v in c), width=6)
                d.ellipse((xx + 16, yy + 9, xx + 29, yy + 18), fill=tuple(min(v + 35, 255) for v in c))
    elif kind == 'fish':
        d.rectangle((0, 0, SIZE, SIZE), fill=(77, 74, 55))
        for yy in range(40, SIZE, 190):
            for xx in range(30, SIZE, 330):
                d.ellipse((xx, yy, xx + 230, yy + 115), fill=(118, 153, 143), outline=(37, 67, 63), width=7)
                d.polygon([(xx + 215, yy + 56), (xx + 289, yy + 5), (xx + 289, yy + 108)], fill=(88, 123, 117))
                d.ellipse((xx + 26, yy + 35, xx + 38, yy + 47), fill=(17, 33, 32))
    elif kind == 'bread':
        d.rectangle((0, 0, SIZE, SIZE), fill=(65, 44, 25))
        for yy in range(30, SIZE, 185):
            for xx in range(20, SIZE, 245):
                d.rounded_rectangle((xx, yy, xx + 200, yy + 130), radius=62, fill=(190, 135, 65), outline=(120, 75, 34), width=8)
                for i in range(3):
                    d.line((xx + 48 + i * 44, yy + 24, xx + 28 + i * 44, yy + 77), fill=(229, 186, 115), width=12)
    if kind=='grate':
        rgba=im.convert('RGBA')
        rgba.putalpha(im.convert('L').point(lambda v:255 if v>55 else 0))
        im=rgba
    elif kind=='parchment':
        for yy in range(160,880,66):
            d.line((120,yy,820+(yy%3)*25,yy+8),fill=(90,65,37),width=12)
        d.ellipse((140,80,260,135),outline=(126,72,43),width=10)
        d.ellipse((750,840,880,970),fill=(133,63,43))
    save(name, im, h, .18 if kind=='window' else .68, 1, kind)


def sign(name, motif):
    s = 512
    color=(38,65,119) if motif in ('Mage','Star','Book','Potion') else (37,83,63) if motif in ('Spirit','Herb','Flower') else (107,42,50) if motif in ('Warrior','Weapon','Guard','Crown') else (86,48,31)
    image = Image.new('RGB', (s, s), color)
    d = ImageDraw.Draw(image)
    for x in range(0, s, 64):
        d.line((x, 0, x, s), fill=tuple(int(c*.83) for c in color), width=3)
    gold = (233, 188, 91)
    d.rounded_rectangle((15, 15, 497, 497), radius=35, outline=gold, width=12)
    d.rounded_rectangle((32,32,480,480),radius=25,outline=(149,106,49),width=3)
    for x in (48,464):
        for y in (48,464):d.ellipse((x-7,y-7,x+7,y+7),fill=(249,218,137))
    if motif=='Guard':
        d.polygon([(126,100),(386,100),(369,286),(256,416),(143,286)],outline=gold,width=22)
        d.line((256,130,256,351),fill=gold,width=20)
        d.line((181,228,331,228),fill=gold,width=20)
    elif motif in ('Warrior', 'Weapon'):
        d.polygon([(256, 70), (283, 127), (271, 322), (241, 322), (229, 127)], fill=gold)
        d.line((180, 327, 332, 327), fill=gold, width=18)
        d.line((256, 327, 256, 412), fill=gold, width=22)
    elif motif=='Mage':
        points=[]
        for i in range(16):
            a=i*math.pi/8;r=170 if i%2==0 else 70
            points.append((256+math.cos(a)*r,256+math.sin(a)*r))
        d.polygon(points,fill=gold)
    elif motif=='Book':
        d.polygon([(90, 120), (242, 149), (256, 395), (105, 363)], fill=gold)
        d.polygon([(270, 149), (424, 120), (407, 363), (270, 395)], fill=gold)
        for yy in (190, 235, 280):
            d.line((130, yy, 215, yy + 18), fill=(109, 65, 32), width=7)
    elif motif=='Spirit':
        d.line((256,410,256,130),fill=gold,width=24)
        for yy,spread in ((190,125),(270,155),(340,110)):
            for direction in (-1,1):
                d.line((256,yy+55,256+direction*spread,yy-30),fill=gold,width=18)
                d.ellipse((256+direction*spread-37,yy-65,256+direction*spread+37,yy+6),fill=gold)
        d.arc((143,326,369,464),180,360,fill=gold,width=18)
    elif motif=='Herb':
        d.pieslice((111,220,401,440),0,180,fill=gold)
        d.line((270,295,360,133),fill=gold,width=31)
        d.ellipse((145,128,244,238),fill=gold)
        d.line((197,240,232,315),fill=gold,width=15)
    elif motif=='Flower':
        for i in range(8):
            a=i*math.pi/4;x=256+math.cos(a)*105;y=221+math.sin(a)*105
            d.ellipse((x-59,y-59,x+59,y+59),fill=gold)
        d.ellipse((201,166,311,276),fill=(106,67,32),outline=gold,width=13)
        d.line((256,317,256,429),fill=gold,width=17)
        d.ellipse((265,349,365,392),fill=gold)
    elif motif=='Bath':
        d.pieslice((100,210,412,430),0,180,fill=gold)
        d.line((105,310,407,310),fill=gold,width=18)
        for xx in (180,256,332):d.arc((xx-27,93,xx+27,243),70,285,fill=gold,width=13)
    elif motif in ('Tavern', 'Wine'):
        d.polygon([(150, 130), (355, 130), (332, 360), (173, 360)], fill=gold)
        d.arc((310, 165, 435, 312), 270, 90, fill=gold, width=22)
        d.rectangle((160, 95, 345, 139), fill=(236, 219, 165))
    elif motif=='Tools':
        d.line((148,399,336,129),fill=gold,width=26)
        d.polygon([(265,112),(315,76),(393,130),(348,177)],fill=gold)
        d.line((362,395,169,137),fill=gold,width=18)
        d.polygon([(111,118),(146,91),(220,185),(186,210)],fill=gold)
    elif motif=='Smith':
        d.polygon([(105, 272), (400, 272), (340, 329), (280, 344), (272, 398), (170, 398), (178, 337)], fill=gold)
        d.line((228, 105, 310, 237), fill=gold, width=30)
        d.polygon([(159, 126), (208, 87), (290, 138), (250, 180)], fill=gold)
    elif motif == 'Stable':
        d.arc((130, 90, 382, 418), 0, 180, fill=gold, width=50)
        d.line((130, 254, 130, 143), fill=gold, width=50)
        d.line((382, 254, 382, 143), fill=gold, width=50)
    elif motif == 'Bread':
        d.rounded_rectangle((90, 205, 420, 333), radius=64, fill=gold)
        for x in (165, 245, 325):
            d.line((x, 218, x - 25, 285), fill=(95, 53, 26), width=15)
    elif motif=='Grain':
        for x in (180,256,332):
            d.line((x,410,x,120),fill=gold,width=12)
            for yy in (160,220,280):
                d.ellipse((x-58,yy-43,x,yy),fill=gold);d.ellipse((x,yy-60,x+58,yy-17),fill=gold)
    elif motif=='Storage':
        for x,y in [(110,256),(270,256),(190,104)]:
            d.rectangle((x,y,x+133,y+130),outline=gold,width=17)
            d.line((x+10,y+10,x+123,y+120),fill=gold,width=12)
    elif motif=='Crown':
        d.polygon([(110,160),(175,222),(256,93),(336,222),(404,160),(362,367),(150,367)],fill=gold)
        d.rectangle((148,377,365,410),fill=gold)
    elif motif=='Museum':
        d.ellipse((195,91,318,230),fill=gold)
        d.polygon([(220,212),(290,212),(375,328),(136,328)],fill=gold)
        d.rectangle((165,337,347,410),fill=gold)
    elif motif=='Casino':
        d.rounded_rectangle((122,122,390,390),radius=34,outline=gold,width=22)
        for x,y in [(190,190),(322,190),(256,256),(190,322),(322,322)]: d.ellipse((x-19,y-19,x+19,y+19),fill=gold)
    elif motif in ('Church', 'Chapel'):
        d.line((256, 95, 256, 417), fill=gold, width=35)
        d.line((143, 205, 369, 205), fill=gold, width=35)
        d.ellipse((175, 124, 337, 286), outline=gold, width=12)
    elif motif == 'Fish':
        d.ellipse((90, 166, 333, 330), fill=gold)
        d.polygon([(300, 249), (421, 157), (421, 339)], fill=gold)
        d.ellipse((131, 212, 151, 232), fill=(55, 39, 24))
    elif motif=='Fruit':
        d.ellipse((118,165,295,379),fill=gold)
        d.ellipse((220,165,397,379),fill=gold)
        d.line((254,192,273,101),fill=gold,width=17)
        d.ellipse((275,102,358,147),fill=gold)
    elif motif=='Meat':
        d.ellipse((115,172,338,391),fill=gold)
        d.line((294,214,375,121),fill=gold,width=41)
        d.ellipse((346,87,391,139),fill=gold);d.ellipse((378,115,421,162),fill=gold)
    elif motif=='Spice':
        d.pieslice((105,180,407,400),0,180,fill=gold)
        d.line((262,230,355,105),fill=gold,width=36)
    elif motif=='Trade':
        d.line((256,107,256,399),fill=gold,width=20)
        d.line((117,180,395,180),fill=gold,width=20)
        for xx in (139,373):
            d.line((xx,180,xx-53,306),fill=gold,width=10)
            d.line((xx,180,xx+53,306),fill=gold,width=10)
            d.pieslice((xx-64,246,xx+64,356),0,180,fill=gold)
        d.line((170,405,342,405),fill=gold,width=22)
    elif motif == 'Potion':
        d.rectangle((215, 89, 296, 185), fill=gold)
        d.ellipse((143, 151, 369, 404), fill=gold)
        d.ellipse((183, 210, 328, 360), fill=(64, 101, 91))
    elif motif == 'Star':
        pts=[]
        for i in range(10):
            a=i*math.pi/5-math.pi/2;r=170 if i%2==0 else 75
            pts.append((256+math.cos(a)*r,256+math.sin(a)*r))
        d.polygon(pts, fill=gold)
    else:
        d.polygon([(256, 89), (405, 256), (256, 423), (107, 256)], fill=gold)
        d.ellipse((202, 202, 310, 310), fill=(95, 53, 26))
    save(name, image, Image.new('L', (s,s), 128), .8, 1, 'sign')


def main():
    surfaces = [
        ('Limestone', (162,153,130), 'stone', 2.4), ('Fieldstone', (114,122,112), 'stone', 2.4),
        ('Brick', (143,93,66), 'brick', 2), ('Cobble', (120,123,111), 'cobble', 2),
        ('Plaster', (224,202,162), 'plaster', 2), ('Plaster_Rose', (195,128,113), 'plaster', 2),
        ('HalfTimber', (203,183,140), 'timber', 3), ('HalfTimber_White', (209,209,184), 'timber', 3),
        ('Oak', (145,84,38), 'wood', 2), ('DarkTimber', (81,48,47), 'wood', 2),
        ('Bark', (90,72,46), 'bark', 2), ('Roof_Slate', (60,83,92), 'roof', 2),
        ('Roof_Clay', (153,77,48), 'roof', 2), ('Roof_Moss', (67,101,77), 'roof', 2),
        ('Thatch', (190,145,64), 'thatch', 2), ('Copper', (52,137,127), 'copper', 2),
        ('Rock', (125,130,120), 'rock', 2),
        ('Grain', (187,146,75), 'grain', 1), ('Paper', (218,202,165), 'paper', 1),
        ('Iron', (58,70,91), 'metal', 1), ('Brass', (196,146,54), 'metal', 1),
        ('Canvas_Stripe', (168,52,60), 'cloth', 2), ('Canvas_Blue', (45,87,157), 'cloth', 2),
        ('Canvas_Green', (44,118,88), 'cloth', 2), ('Canvas_Gold', (201,149,49), 'cloth', 2),
        ('Canvas_Red', (153,43,61), 'cloth', 2), ('Linen', (218,199,157), 'cloth', 1),
        ('Foliage', (65,103,58), 'leaf', 2), ('Foliage_Gold', (135,127,55), 'leaf', 2),
        ('Grass', (83,105,58), 'leaf', 1), ('Earth', (111,91,61), 'plaster', 2),
        ('Terracotta', (157,92,57), 'plaster', 1), ('Water', (51,99,99), 'water', 2),
        ('Apple_Red',(159,45,29),'organic',1), ('Apple_Green',(115,145,47),'organic',1),
        ('Carrot',(207,105,27),'organic',1), ('Cabbage',(88,123,57),'leaf',1),
        ('Bread_Crust',(187,128,59),'bread',1), ('Fish_Skin',(145,174,178),'fish_skin',1),
        ('Meat',(146,63,55),'organic',1), ('Cork',(154,116,65),'organic',1),
        ('Spice_Ochre',(201,145,47),'organic',1), ('Spice_Red',(157,57,34),'organic',1),
        ('Spice_Salt',(211,204,183),'organic',1), ('Glass_Clear',(155,185,180),'organic',1),
        ('Liquid_Green',(35,164,104),'organic',1), ('Liquid_Purple',(127,62,180),'organic',1),
        ('Glass_Green',(35,151,120),'potion_glass',1), ('Glass_Purple',(120,68,175),'potion_glass',1),
    ]
    for args in surfaces:
        surface(*args)
    for name, kind, col in [('Door_Oak','door',(110,67,34)), ('Window_Amber','window',(198,154,73)), ('Window_Blue','window',(76,142,196)), ('Window_Green','window',(85,154,139)), ('Iron_Grate','grate',(0,0,0)), ('Books','books',(0,0,0)), ('Produce','produce',(0,0,0)), ('Fish','fish',(0,0,0)), ('Bread','bread',(0,0,0))]:
        panel(name,kind,col)
    panel('Parchment','parchment',(211,187,137))
    for motif in ('Warrior','Weapon','Guard','Book','Mage','Spirit','Herb','Flower','Tavern','Wine','Bath','Smith','Tools','Stable','Bread','Grain','Church','Chapel','Fish','Potion','Star','Trade','Fruit','Meat','Spice','Storage','Crown','Museum','Casino'):
        sign('Sign_'+motif,motif)
    y,x=np.mgrid[0:SIZE,0:SIZE]
    noise=np.asarray(Image.fromarray(RNG.integers(0,256,(40,48),dtype='uint8')).resize((SIZE,SIZE),Image.Resampling.BICUBIC),dtype=float)/255
    alpha=np.clip((y/SIZE-.22)*noise*150,0,110).astype('uint8')
    color=np.zeros((SIZE,SIZE,4),dtype='uint8');color[:,:,:3]=(69,74,43);color[:,:,3]=alpha
    save('Weathering',Image.fromarray(color),Image.new('L',(SIZE,SIZE),128),.95,2,'dirt_decal')
    frond=Image.new('RGBA',(SIZE,SIZE),(0,0,0,0));fd=ImageDraw.Draw(frond)
    fd.line((512,995,512,60),fill=(112,139,65,255),width=13)
    for row in range(15):
        yy=140+row*51;length=45+row*13
        for direction in (-1,1):
            fd.polygon([(512,yy+45),(512+direction*length,yy-50),(512+direction*(length-15),yy+9),(512,yy+70)],fill=(49+row*2,98+row*2,45,255))
    save('Fern_Frond',frond,Image.new('L',(SIZE,SIZE),128),.88,1,'plant_frond')
    for name, petal_color in [('Flower_Card_Gold',(225,181,74)), ('Flower_Card_Pink',(192,100,139))]:
        bloom = Image.new('RGBA',(SIZE,SIZE),(0,0,0,0));bd=ImageDraw.Draw(bloom)
        for i in range(9):
            a=i*2*math.pi/9
            tangent=np.array([-math.sin(a),math.cos(a)]);radial=np.array([math.cos(a),math.sin(a)])
            center=np.array([512,512])+radial*220
            points=[tuple(center+radial*math.cos(t)*205+tangent*math.sin(t)*113) for t in np.linspace(0,2*math.pi,28)]
            col=tuple(min(255,c+(i%3-1)*10) for c in petal_color)+(255,)
            bd.polygon(points,fill=col)
            bd.line([tuple(np.array([512,512])+radial*140),tuple(np.array([512,512])+radial*365)],fill=tuple(max(0,c-22) for c in petal_color)+(255,),width=8)
        bd.ellipse((392,392,632,632),fill=(129,89,36,255))
        for i in range(25):
            a=i*2.4;r=12*math.sqrt(i)
            x=512+math.cos(a)*r;y=512+math.sin(a)*r
            bd.ellipse((x-8,y-8,x+8,y+8),fill=(213,158,56,255))
        save(name,bloom,Image.new('L',(SIZE,SIZE),128),.86,1,'flower_petals')
    leaves=Image.new('RGBA',(SIZE,SIZE),(0,0,0,0));ld=ImageDraw.Draw(leaves)
    ld.line((512,995,512,90),fill=(104,129,55,255),width=16)
    for i in range(7):
        yy=180+i*103
        for direction in (-1,1):
            base=np.array([512,yy+80]);tip=np.array([512+direction*(220+i*13),yy-105])
            middle=(base+tip)/2;offset=np.array([60,75])
            ld.polygon([tuple(base),tuple(middle+offset),tuple(tip),tuple(middle-offset)],fill=(64+i*3,109+i*3,52,255))
            ld.line([tuple(base),tuple(tip)],fill=(117,150,70,255),width=6)
    save('Leaf_Card',leaves,Image.new('L',(SIZE,SIZE),128),.9,1,'leaf_sprig')
    grass=Image.new('RGBA',(SIZE,SIZE),(0,0,0,0));gd=ImageDraw.Draw(grass)
    for i in range(17):
        xx=110+i*49;top=int(RNG.integers(90,470));lean=int(RNG.integers(-120,121))
        gd.polygon([(xx-15,1024),(xx+20,1024),(xx+lean,top)],fill=(65+i%4*12,104+i%3*10,46,255))
        gd.line((xx,1024,xx+lean,top+55),fill=(125,146,65,255),width=3)
    save('Grass_Tuft',grass,Image.new('L',(SIZE,SIZE),128),.92,1,'grass_cluster')
    (OUT/'texture_manifest.json').write_text(json.dumps(RECORDS,indent=2),encoding='utf-8')
    print('TEXTURES_AUTHORED',len(RECORDS),'materials',len(RECORDS)*3,'PNG maps',flush=True)


if __name__ == '__main__':
    main()
