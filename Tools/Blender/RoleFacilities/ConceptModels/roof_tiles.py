"""Tile scale and roof shading from the roofs already placed in Country."""

from dataclasses import dataclass
from pathlib import Path
import re

from materials import image_node, linear_color, surface


PALETTES = Path(__file__).resolve().parents[4]/"Assets/Prefabs/Kingdom/Comparison/Materials/Roofs"


@dataclass(frozen=True)
class RoofTiles:
    repeat_m: float
    palette: str | tuple[str, str, str]


TILE_ROOFS = {
    "Library": RoofTiles(2.75, "Library_Verdigris"),
    "Museum": RoofTiles(2.75, "Museum_Bronze"),
    "Clinic": RoofTiles(2.9364, "Clinic_Sage"),
    "WarriorAcademy_Lv5": RoofTiles(2.75, "Warrior_Oxblood"),
    "ArcaneAcademy_Lv5": RoofTiles(2.64, "Arcane_Indigo"),
    "SpiritAcademy_Lv5": RoofTiles(2.75, "Spirit_Forest"),
    "Guardhouse": RoofTiles(3.52, "Guard_Steel"),
    "Blacksmith": RoofTiles(2.64, "Smith_Graphite"),
    "Tavern": RoofTiles(2.5919, "Tavern_Honey"),
    "Casino": RoofTiles(2.64, "Casino_Aubergine"),
    "CrimsonRowhouse": RoofTiles(2.20, "Nightlife_Mulberry"),
    "VeiledCourtyard": RoofTiles(2.75, ("49281f", "9a573b", "d68d65")),
    "MasqueradeSalon": RoofTiles(2.75, ("181f25", "35424b", "717b80")),
}


def build_tile_material(name, tiles):
    if isinstance(tiles.palette, str):
        settings = (PALETTES/f"{tiles.palette}_Roof_Slate.mat").read_text()
        colors = dict(re.findall(r"- (_\w+): \{r: ([^}]+)\}", settings))
        shade, body, edge = [tuple(map(float, re.split(r", [gba]: ", colors[key])[:3]))
                             for key in ("_SpecColor", "_BaseColor", "_EmissionColor")]
        relief = float(re.search(r"- _BumpScale: (\S+)", settings)[1])
        smoothness = float(re.search(r"- _Smoothness: (\S+)", settings)[1])
    else:
        shade, body, edge = map(linear_color, tiles.palette)
        relief, smoothness = .24, .4

    material = surface(name, source="Roof_Slate", relief=relief, roughness=1-smoothness)
    material.diffuse_color = (*body, 1)
    nodes, links = material.node_tree.nodes, material.node_tree.links
    shader = nodes["Principled BSDF"]
    base_color = shader.inputs["Base Color"]
    rgb = nodes.new("ShaderNodeSeparateColor")
    links.new(base_color.links[0].from_socket, rgb.inputs[0])
    value = rgb.outputs[0]
    for channel in (rgb.outputs[1], rgb.outputs[2]):
        maximum = nodes.new("ShaderNodeMath")
        maximum.operation = "MAXIMUM"
        links.new(value, maximum.inputs[0])
        links.new(channel, maximum.inputs[1])
        value = maximum.outputs[0]

    # CityRoofPalette.shader assigns separate colors to recesses, the body, and the tile edges.
    for lower, upper, start, end in ((.025, .24, shade, body), (.24, .85, None, edge)):
        ramp = nodes.new("ShaderNodeMapRange")
        ramp.interpolation_type = "SMOOTHSTEP"
        ramp.inputs["From Min"].default_value = lower
        ramp.inputs["From Max"].default_value = upper
        links.new(value, ramp.inputs["Value"])
        mix = nodes.new("ShaderNodeMixRGB")
        links.new(ramp.outputs[0], mix.inputs[0])
        if start is None:
            links.new(albedo, mix.inputs[1])
        else:
            mix.inputs[1].default_value = (*start, 1)
        mix.inputs[2].default_value = (*end, 1)
        albedo = mix.outputs[0]
    links.new(albedo, base_color)

    roughness = image_node(nodes, "Roof_Slate", "Roughness", data=True)
    if roughness:
        gloss = nodes.new("ShaderNodeMath")
        gloss.operation = "MULTIPLY_ADD"
        links.new(roughness.outputs["Color"], gloss.inputs[0])
        gloss.inputs[1].default_value = smoothness
        gloss.inputs[2].default_value = 1-smoothness
        links.new(gloss.outputs[0], shader.inputs["Roughness"])
    material["tile_repeat_metres"] = tiles.repeat_m
    return material
