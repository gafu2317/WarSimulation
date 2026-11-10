"""Render disjoint asset groups with independent journals and shared PNG output."""
from pathlib import Path
import argparse
import json
import shutil
import sys

sys.path.insert(0,str(Path(__file__).resolve().parent))
import render_review


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--job',type=int,required=True)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    if args.job not in (0,1):raise ValueError('Expected one of two render groups')
    source=render_review.OUT
    assets=json.loads((source/'asset_manifest.json').read_text())['assets']
    work=render_review.ROOT/f'Artifacts/MedievalCity_2026-10-22/RenderJobs/{args.job}'
    (work/'Reports').mkdir(parents=True,exist_ok=True)
    for name in ('Blender','Textures','Previews','asset_manifest.json'):
        link=work/name
        if not link.exists():link.symlink_to(source/name)
        if link.resolve()!=(source/name).resolve():raise ValueError('Unexpected output link')
    shutil.copyfile(source/'Reports/render_progress.json',work/'Reports/render_progress.json')
    names=[a['name'] for i,a in enumerate(assets) if i%2==args.job]
    render_review.OUT=work
    sys.argv=['blender','--','--device','METAL','--names',*names]
    render_review.main()


if __name__=='__main__':main()
