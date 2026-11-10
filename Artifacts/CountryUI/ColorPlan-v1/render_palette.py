from pathlib import Path
import hashlib
import json
import os
import re

os.environ.setdefault("MPLCONFIGDIR", "/private/tmp/country-color-matplotlib")
import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.font_manager import FontProperties


def luminance(color):
    values = [int(color[i:i + 2], 16) / 255 for i in (1, 3, 5)]
    linear = [v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4 for v in values]
    return sum(v * w for v, w in zip(linear, (.2126, .7152, .0722)))


def contrast(first, second):
    high, low = sorted((luminance(first), luminance(second)), reverse=True)
    return (high + .05) / (low + .05)


folder = Path(__file__).resolve().parent
root = folder.parents[2]
source = root / "docs/Country/UI.md"
document = source.read_text()
section = document.split("## 配色変更の計画\n", 1)[1].split("### 選択・主操作・操作状態", 1)[0]
pattern = r"^\| ([^|]+) \| `(#\w{6})` \| `(#\w{6})` \| ([\d.]+):1 \|$"
rows = []
for role, surface, foreground, declared in re.findall(pattern, section, re.M):
    ratio = contrast(surface, foreground)
    target = 3 if role == "操作不可の面" else 4.5
    assert round(ratio, 2) == float(declared), (role, ratio, declared)
    assert ratio >= target, (role, ratio, target)
    rows.append(dict(role=role, surface=surface, foreground=foreground,
                     contrast=round(ratio, 4), minimum=target))
assert len(rows) == 10, len(rows)

signal_line = next(line for line in section.splitlines() if line.startswith("- 紺灰の面に使う増加"))
signal_colors = re.findall(r"`(#[A-F0-9]{6})`", signal_line)
secondary_line = next(line for line in section.splitlines() if line.startswith("- 紙面の補足文字は"))
paper_secondary = re.findall(r"`(#[A-F0-9]{6})`", secondary_line)[0]
extra_pairs = []
for role, surface, foreground in [
    ("紺灰上の増加", "#526071", signal_colors[0]),
    ("紺灰上の不足", "#526071", signal_colors[1]),
    ("紙上の増加", "#E9DDC2", signal_colors[2]),
    ("紙上の不足", "#E9DDC2", signal_colors[3]),
    ("紙上の補足", "#E9DDC2", paper_secondary),
]:
    ratio = contrast(surface, foreground)
    assert ratio >= 4.5, (role, ratio)
    extra_pairs.append(dict(role=role, surface=surface, foreground=foreground,
                            contrast=round(ratio, 4), minimum=4.5))

record = dict(stage="plan_only", unityApplied=False, visualAccepted=False,
              measurement="Opaque sRGB reference pairs; excludes textures, tint and opacity",
              source="docs/Country/UI.md#配色変更の計画",
              sourceSHA256=hashlib.sha256(source.read_bytes()).hexdigest(),
              pairs=rows, textPairs=extra_pairs)
(folder / "palette-check.json").write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n")

font = FontProperties(fname=str(root / "Assets/Fonts/Noto_Sans_JP/static/NotoSansJP-Regular.ttf"))
shown = [row for row in rows if row["role"] not in ("パネル上の補足", "入力欄・凹面")]
fig, ax = plt.subplots(figsize=(10.4, 8.4), dpi=140)
fig.patch.set_facecolor(rows[0]["surface"])
ax.set_xlim(0, 100)
ax.set_ylim(-1.1, len(shown) + 1.9)
ax.axis("off")


def label(x, y, value, size=15, color="#F4F0E6", **kwargs):
    ax.text(x, y, value, fontproperties=font, fontsize=size,
            color=color, va="center", **kwargs)


label(0, len(shown) + 1.4, "国家UI 配色計画", size=23)
label(0, len(shown) + .7, "基準色と文字の組み合わせ", size=14, color="#D6DEE7")
label(0, len(shown) + .05, "用途", size=12, color="#D6DEE7")
label(36, len(shown) + .05, "面と文字", size=12, color="#D6DEE7")
label(72, len(shown) + .05, "面 / 文字・明暗差", size=12, color="#D6DEE7")

for i, row in enumerate(shown):
    y = len(shown) - 1 - i
    label(0, y, row["role"])
    ax.barh(y, 33, height=.66, left=35, color=row["surface"],
            edgecolor="#93A4B7", linewidth=.6)
    label(51.5, y, "文字の見え方", color=row["foreground"], ha="center")
    label(72, y + .14, f'{row["surface"]} / {row["foreground"]}', size=11.5)
    label(72, y - .17, f'{row["contrast"]:.2f} : 1', size=11.5, color="#D6DEE7")

label(0, -.83, "比率は不透明な基準色同士の計算。素材を合成した表示は実装時に確認する。", size=11.5)
fig.subplots_adjust(left=.045, right=.985, top=.98, bottom=.03)
fig.savefig(folder / "palette.png", facecolor=fig.get_facecolor())
fig.savefig(folder / "palette.svg", facecolor=fig.get_facecolor())
plt.close(fig)
print(json.dumps(dict(referencePairs=len(rows) + len(extra_pairs), shownPairs=len(shown),
                      palette=str(folder / "palette.png")), ensure_ascii=False))
