import argparse
import hashlib
import json
from datetime import datetime
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile
from zoneinfo import ZoneInfo

from PIL import Image, ImageDraw

from review_country_comparison import INK, PAPER, PROJECT, REPORTS, font


VIEWS = [
    ("Overview", "国の全景", "城壁内の街区構成と、新旧129種類ずつのモデルを比較"),
    ("Palace", "王城", "周囲と配色を揃えた旧城と、新城の塔・屋根・石壁"),
    ("Residential", "住宅街", "住宅の密集度、瓦の色差、窓や木組みの細部"),
    ("Market", "市場広場", "商業地区の街並みと、広場に並ぶ露店"),
    ("TradingApron", "露店・商品のアップ", "果物・瓶・布・看板など、店先の小物を近くから比較"),
    ("Cathedral", "教会", "教会のシルエット、屋根、窓と入口まわり"),
    ("Academies", "魔法学校・精霊使役学校", "Lv5の学校、塔とドーム、専用色の屋根"),
    ("WarriorAcademy", "戦士学校", "Lv5の戦士学校、正面の構えと訓練用の小物"),
    ("Riverside", "川沿い・水車", "本流につながる水車、橋、護岸と川沿いの建物"),
    ("EasternWatergate", "城壁・水門", "城壁を通り抜ける川、水門のアーチと外壁の細部"),
]


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def export(folder=None):
    now = datetime.now(ZoneInfo("Asia/Tokyo"))
    folder = Path(folder) if folder else REPORTS / now.strftime("ComparisonSet_%Y-%m-%d_%H%M%S")
    folder.mkdir(exist_ok=True)
    usage = json.loads((REPORTS / "model_usage_validation.json").read_text())
    records = []
    for index, (name, title, caption) in enumerate(VIEWS, 1):
        board = Image.new("RGB", (3840, 1240), PAPER)
        draw = ImageDraw.Draw(board)
        sources = []
        for side, (prefix, label, count) in enumerate((
                ("Old", "旧モデル", usage["oldTypes"]), ("New", "新モデル", usage["newTypes"]))):
            path = REPORTS / f"{prefix}_{name}.png"
            with Image.open(path) as shot:
                if shot.size != (1920, 1080):
                    raise ValueError(f"Unexpected capture size: {path} {shot.size}")
                board.paste(shot.convert("RGB"), (side * 1920, 90))
            heading = f"{label} {count}種類" if name == "Overview" else f"{label}｜{title}"
            draw.text((side * 1920 + 30, 12), heading, font=font(56), fill=INK)
            sources.append({"path": str(path.relative_to(PROJECT)), "sha256": digest(path)})
        draw.text((40, 1176), f"{index:02d}  {title} ／ {caption}", font=font(40), fill=INK)
        path = folder / f"{index:02d}_{name}.jpg"
        board.save(path, quality=97, subsampling=0)
        records.append({"number": index, "title": title, "file": path.name, "size": [3840, 1240],
                        "sha256": digest(path), "sources": sources})

    overview = Image.new("RGB", (1920, 1780), PAPER)
    draw = ImageDraw.Draw(overview)
    draw.text((25, 12), "新旧モデル比較｜全景1枚＋アップ9枚", font=font(35), fill=INK)
    for i, record in enumerate(records):
        x, y = i % 2 * 960, 65 + i // 2 * 343
        draw.text((x + 12, y + 1), f"{i + 1:02d}  {record['title']}", font=font(24), fill=INK)
        with Image.open(folder / record["file"]) as board:
            overview.paste(board.resize((960, 310), Image.Resampling.LANCZOS), (x, y + 31))
    overview.save(folder / "Index.jpg", quality=95, subsampling=0)
    manifest = {"capturedAt": now.isoformat(), "timezone": "Asia/Tokyo", "scene": "Assets/Scenes/Country.unity",
                "scene_sha256": digest(PROJECT / "Assets/Scenes/Country.unity"),
                "left": "旧モデル", "right": "新モデル", "comparisonImages": len(records),
                "layout": "同じ相対カメラ位置・向き・画角。左右とも1920×1080の撮影画像を縮小せず配置。",
                "images": records}
    (folder / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")
    lines = ["# 新旧モデル比較・10枚", "", "左が旧モデル、右が新モデル。同じ相対視点・倍率で撮影。",
             "比較画像は各3840×1240、左右それぞれ1920×1080。", ""]
    lines += [f"- {r['number']:02d} {r['title']}: {r['file']}" for r in records]
    (folder / "README.md").write_text("\n".join(lines) + "\n")
    with ZipFile(folder / "Country_Comparison_10_Images.zip", "w", ZIP_DEFLATED) as archive:
        for record in records:
            archive.write(folder / record["file"], record["file"])
        for name in ("Index.jpg", "README.md", "manifest.json"):
            archive.write(folder / name, name)
    print(json.dumps({"folder": str(folder), "images": len(records), "size": [3840, 1240]}, ensure_ascii=False))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path)
    export(parser.parse_args().output)
