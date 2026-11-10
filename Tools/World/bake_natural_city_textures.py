"""Bake a quieter material palette without changing the source textures or alpha."""
import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image


SOURCE = Path("Assets/Models/Kingdom/CityV2/Textures")
OUTPUT = Path("Artifacts/CountryComparison/NaturalThird/BakedTextures")
TONES = {
    "Limestone": ("474B46", "8E8D7E", "BAB7A5"),
    "Fieldstone": ("424740", "818477", "B0AE9D"),
    "Cobble": ("3E433F", "7C7E72", "AAA697"),
    "Rock": ("3C413D", "777C71", "A8ABA0"),
    "Plaster": ("6A665B", "B4AE9B", "D1CBB9"),
    "HalfTimber_White": ("6A665B", "B4AE9B", "D1CBB9"),
    "Plaster_Rose": ("66504A", "A08777", "C3B29E"),
    "Brick": ("4C3B32", "85634C", "AD8E70"),
    "Oak": ("302D24", "69543E", "A08B68"),
    "Door_Oak": ("2E2B24", "67513A", "A18A64"),
    "DarkTimber": ("252721", "51483B", "81745F"),
    "HalfTimber": ("252721", "51483B", "81745F"),
    "Bark": ("2B2C23", "5D5844", "8D8668"),
    "Roof_Clay": ("342C27", "765443", "AA8B6C"),
    "Roof_Slate": ("272E31", "52616A", "8F9B9B"),
    "Roof_Moss": ("29312A", "52604A", "899272"),
    "Thatch": ("444030", "8B7E56", "B7AA7C"),
    "Earth": ("3D3B30", "766D52", "A29778"),
    "Terracotta": ("45372D", "88654A", "B49A77"),
    "Linen": ("686452", "AAA38A", "D0CAB3"),
    "Copper": ("263D34", "4C7060", "8DA595"),
    "Brass": ("443D2A", "897849", "BCA670"),
    "Iron": ("202829", "505A59", "8D9590"),
    "Iron_Grate": ("202829", "505A59", "8D9590"),
}


def color(value):
    return np.array([int(value[i:i + 2], 16) for i in (0, 2, 4)], dtype=np.float32) / 255


def bake(name, rgba):
    rgb = rgba[..., :3].astype(np.float32) / 255
    light = rgb @ np.array([.2126, .7152, .0722], dtype=np.float32)
    visible = rgba[..., 3] > 32
    if name in TONES:
        low, middle, high = np.quantile(light[visible], [.01, .5, .99])
        middle = max(middle, low + .001)
        high = max(high, middle + .001)
        shade, body, edge = map(color, TONES[name])
        # Narrow source ranges are intentional: stretching them turns fine grain into stains.
        below = np.clip(1 + (light - middle) / max(middle - low, .18), 0, 1)[..., None]
        above = np.clip((light - middle) / max(high - middle, .18), 0, 1)[..., None]
        result = (shade + (body - shade) * below) * (1 - above) + edge * above
        # A little source hue keeps individual stones and wood grain distinct.
        result += (rgb - light[..., None]) * .12
    else:
        foliage = name in {"Foliage", "Cypress_Foliage", "Cypress_Card", "Leaf_Card",
                           "Grass", "Grass_Tuft", "Fern_Frond", "Canopy_Card"}
        detail = name.startswith(("Sign_", "Window_", "Glass_", "Flower_"))
        saturation, brightness = (.62, .82) if foliage else ((.78, .91) if detail else (.65, .86))
        result = (light[..., None] + (rgb - light[..., None]) * saturation) * brightness
    output = rgba.copy()
    output[..., :3] = np.round(np.clip(result, 0, 1) * 255).astype(np.uint8)
    return output


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, default=OUTPUT)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    records = []
    for source in sorted(SOURCE.glob("*_BaseColor.png")):
        name = source.name.removesuffix("_BaseColor.png")
        pixels = np.array(Image.open(source).convert("RGBA"))
        natural = bake(name, pixels)
        assert np.array_equal(pixels[..., 3], natural[..., 3])
        Image.fromarray(natural).save(args.output / source.name)
        visible = pixels[..., 3] > 32
        records.append({"source": str(source), "name": name,
                        "beforeMeanRgb": pixels[..., :3][visible].mean(axis=0).round(2).tolist(),
                        "afterMeanRgb": natural[..., :3][visible].mean(axis=0).round(2).tolist(),
                        "width": pixels.shape[1], "height": pixels.shape[0], "alphaPreserved": True})
    (args.output.parent / "texture_bake_manifest.json").write_text(json.dumps(records, indent=2) + "\n")
    print(f"Baked {len(records)} natural albedo textures into {args.output}")


if __name__ == "__main__":
    main()
