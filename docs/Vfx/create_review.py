"""Build review videos/contact sheets from Unity audit captures (Pillow, ffmpeg)."""
from pathlib import Path
import csv
import subprocess
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[2] / 'Captures/Vfx'
for mode in ('EffectTest', 'Combat'):
    source = root / mode
    if not (source / 'results.csv').exists():
        continue
    rows = list(csv.DictReader((source / 'results.csv').open()))
    for row in rows:
        skill = row['skill']
        frames = sorted((source / skill).glob('*.png'))
        if not frames:
            continue
        video = source / (skill + '.mp4')
        subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-framerate', '10',
                        '-i', str(source / skill / '%04d.png'), '-vf',
                        f"drawtext=text='{skill}':x=18:y=18:fontsize=24:fontcolor=white:box=1:boxcolor=black@0.6",
                        '-c:v', 'libx264', '-preset', 'fast', '-crf', '20',
                        '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(video)], check=True)
        chosen = min(len(frames)-1, 4 if mode == 'EffectTest' else 16)
        Image.open(frames[chosen]).save(source / (skill + '.jpg'), quality=90)
    for page in range((len(rows)+5)//6):
        group = rows[page*6:page*6+6]
        out = Image.new('RGB', (1440, len(group)*226), '#172331')
        draw = ImageDraw.Draw(out)
        for row_index, row in enumerate(group):
            skill = row['skill']
            frames = sorted((source / skill).glob('*.png'))
            for col, fraction in enumerate((.05,.18,.36,.85)):
                index = min(len(frames)-1, max(1,int(len(frames)*fraction)))
                im=Image.open(frames[index]); im.thumbnail((360,202))
                out.paste(im,(col*360,row_index*226+24))
                draw.text((col*360+6,row_index*226+6),f'{skill} | frame {index}',fill='white')
        out.save(source / f'contact-{page+1}.jpg',quality=92)
print('Review media written to', root)
