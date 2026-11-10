from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


PROJECT = Path(__file__).resolve().parents[2]
REPORTS = PROJECT / "Artifacts/CountryComparison"
FONT = PROJECT / "Assets/Fonts/Noto_Sans_JP/static/NotoSansJP-Regular.ttf"
PAPER, INK = "#efe9de", "#242224"
FACILITIES = [
    ("WarriorAcademy_Lv5", "戦士学校 / 深紅"), ("ArcaneAcademy_Lv5", "魔法学校 / 藍"),
    ("Church", "教会 / 青緑"), ("Library", "図書館 / 緑青"),
    ("Tavern", "酒場 / 蜂蜜色"), ("Observatory", "天文台 / 夜空色"),
    ("Museum", "美術館 / 古銅色"), ("Clinic", "診療所 / セージ"),
    ("Casino", "カジノ / 茄子紫"), ("Guardhouse", "衛兵所 / 鋼青"),
    ("SpiritAcademy_Lv5", "精霊使役学校 / 深緑"), ("Blacksmith", "鍛冶場 / 炭色"),
    ("CrimsonRowhouse", "歓楽街・町家 / 桑の実色"), ("VelvetTerrace", "歓楽街・館 / 葡萄色"),
    ("LanternSpire", "歓楽街・塔屋 / プラム"), ("VeiledCourtyard", "歓楽街・中庭 / 紫檀色"),
    ("Plaza", "広場 / 砂岩色の舗装"), ("Arena", "闘技場 / 深紅の幕"),
]


def font(size):
    return ImageFont.truetype(str(FONT), size)


def paste_shot(board, path, x, y, width):
    with Image.open(path) as source:
        shot = source.convert("RGB").resize((width, width * 9 // 16), Image.Resampling.LANCZOS)
        board.paste(shot, (x, y))


def pair(path, left, right, labels, footer):
    board = Image.new("RGB", (1920, 620), PAPER)
    draw = ImageDraw.Draw(board)
    for i, (shot, label) in enumerate(zip((left, right), labels)):
        paste_shot(board, shot, i * 960, 45, 960)
        draw.text((i * 960 + 15, 6), label, font=font(28), fill=INK)
    draw.text((20, 588), footer, font=font(21), fill=INK)
    board.save(path, quality=95)


def main():
    pair(REPORTS / "Comparison.jpg", REPORTS / "Old_Overview.png", REPORTS / "New_Overview.png",
         ("旧モデル 129種類", "新モデル 129種類"),
         "住宅の屋根に12段階の色差 ／ 役割のある施設は専用色 ／ 川は戦闘マップと共通の水面表現")
    pair(REPORTS / "Play_Review.jpg", REPORTS / "Old_Play.png", REPORTS / "New_Play.png",
         ("旧モデル / 再生中", "新モデル / 再生中"), "比較キーと視点保持、固定カメラでの流水を確認")
    pair(REPORTS / "Flow_Review.jpg", REPORTS / "New_Play.png", REPORTS / "RiverFlow_Later.png",
         ("時刻A", "時刻B"), "同じカメラで時間を進め、戦闘マップと同じ水面の変化を確認")
    for name in ("Overview", "Residential"):
        pair(REPORTS / f"Roof_{name}_BeforeAfter.jpg", REPORTS / "BeforeRoofPalette" / f"New_{name}.png",
             REPORTS / f"New_{name}.png", ("変更前", "変更後"),
             "瓦の陰影を残し、赤土・茶・灰色の落ち着いた色差を混ぜる")
    castle_before = REPORTS / "BeforeCastleTextureFix/Old_Palace.png"
    color_before = REPORTS / "BeforeCastleColorMatch/Old_Palace.png"
    if castle_before.exists():
        pair(REPORTS / "CastleTexture_BeforeAfter.jpg", castle_before, color_before if color_before.exists() else REPORTS / "Old_Palace.png",
             ("旧モデルの城 / 修正前", "旧モデルの城 / 修正後"),
             "元の形と配色を保ち、屋根・窓・石壁の黒い欠けを修正")
    if color_before.exists():
        pair(REPORTS / "CastleColor_BeforeAfter.jpg", color_before, REPORTS / "Old_Palace.png",
             ("旧モデルの城 / 色調整前", "旧モデルの城 / 色調整後"),
             "周囲の旧建築と揃えた石壁・青灰色の屋根・木部 ／ 形状は同じ")
    board = Image.new("RGB", (1920, 6 * 392), PAPER)
    draw = ImageDraw.Draw(board)
    for i, (model, label) in enumerate(FACILITIES):
        x, y = i % 3 * 640, i // 3 * 392
        paste_shot(board, REPORTS / f"Roof_{model}.png", x, y + 32, 640)
        draw.text((x + 10, y + 2), label, font=font(21), fill=INK)
    board.save(REPORTS / "Facility_Roofs.jpg", quality=94)
    for page in range(3):
        board = Image.new("RGB", (1440, 3 * 437), PAPER)
        draw = ImageDraw.Draw(board)
        for i, (model, label) in enumerate(FACILITIES[page * 6:page * 6 + 6]):
            x, y = i % 2 * 720, i // 2 * 437
            paste_shot(board, REPORTS / f"Roof_{model}.png", x, y + 32, 720)
            draw.text((x + 10, y + 2), label, font=font(22), fill=INK)
        board.save(REPORTS / f"Roof_Review_{page + 1}.jpg", quality=94)


if __name__ == "__main__":
    main()
