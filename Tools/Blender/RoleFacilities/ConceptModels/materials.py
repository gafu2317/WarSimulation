"""CityV2 texture maps in self-contained Blender materials."""

from array import array
from pathlib import Path

import bpy


TEXTURES = Path(__file__).resolve().parents[4]/"Assets/Models/Kingdom/CityV2/Textures"

ALIASES = {
    "stone":"Fieldstone", "trim":"Limestone", "wall":"Plaster", "brick":"Brick",
    "roof":"Roof_Slate", "wood":"Oak", "timber":"DarkTimber", "door":"Door_Oak",
    "glass":"Window_Blue", "warm_glass":"Window_Amber", "clear_glass":"Clear_Glazing",
    "paving":"Cobble", "soil":"Earth", "water":"Water", "metal":"Iron",
    "bronze":"Brass", "gold":"Brass", "steel":"Steel", "books":"Books",
    "ivory":"Linen", "ink":"Ink", "leather":"Leather", "red":"Canvas_Red",
    "blue":"Canvas_Blue", "green":"Canvas_Green", "canvas":"Canvas_Gold",
    "bark":"Bark", "leaf":"Foliage", "leaflight":"Foliage_Gold",
    "crystal":"Crystal", "spiritlight":"Spirit_Light",
}


def linear_color(hex_rgb):
    srgb = [int(hex_rgb[i:i+2],16)/255 for i in (0,2,4)]
    return tuple(v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in srgb)


def image_node(nodes, name, label, data=False):
    path = TEXTURES/f"{name}_{label}.png"
    if not path.is_file():
        return None
    texture = nodes.new("ShaderNodeTexImage")
    texture.label = label
    texture.image = bpy.data.images.load(str(path),check_existing=True)
    texture.image.colorspace_settings.name = "Non-Color" if data else "sRGB"
    return texture


def surface(name, source=None, color=None, roughness=.64, metallic=0,
            relief=.23, emittance=0, transmission=0, thin_glass=0):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes,links = material.node_tree.nodes,material.node_tree.links
    shader = nodes.get("Principled BSDF")
    tint = linear_color(color) if color else (.55,.48,.40)
    material.diffuse_color = (*tint,1)
    shader.inputs["Base Color"].default_value = material.diffuse_color
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metallic
    shader.inputs["Transmission Weight"].default_value = transmission
    if transmission:
        shader.inputs["IOR"].default_value = 1.40

    base = image_node(nodes,source,"BaseColor") if source else None
    if base:
        albedo = base.outputs["Color"]
        if color:
            gray = nodes.new("ShaderNodeRGBToBW")
            links.new(albedo,gray.inputs[0])
            remap = nodes.new("ShaderNodeMapRange")
            remap.inputs["From Min"].default_value = .036
            remap.inputs["From Max"].default_value = .64
            links.new(gray.outputs[0],remap.inputs["Value"])
            palette = nodes.new("ShaderNodeMixRGB")
            palette.inputs[1].default_value = (*(v*.62 for v in tint),1)
            palette.inputs[2].default_value = (*(min(1,v*1.52) for v in tint),1)
            links.new(remap.outputs[0],palette.inputs[0])
            albedo = palette.outputs[0]
        links.new(albedo,shader.inputs["Base Color"])
    if source and relief:
        normal = image_node(nodes,source,"Normal",data=True)
        if normal:
            node = nodes.new("ShaderNodeNormalMap")
            node.inputs["Strength"].default_value = relief
            links.new(normal.outputs["Color"],node.inputs["Color"])
            links.new(node.outputs[0],shader.inputs["Normal"])
    if emittance:
        shader.inputs["Emission Color"].default_value = (*tint,1)
        shader.inputs["Emission Strength"].default_value = emittance
        if base:
            links.new(albedo,shader.inputs["Emission Color"])
    if thin_glass:
        transparent = nodes.new("ShaderNodeBsdfTransparent")
        glazing = nodes.new("ShaderNodeMixShader")
        glazing.inputs[0].default_value = thin_glass
        links.new(transparent.outputs[0],glazing.inputs[1])
        links.new(shader.outputs[0],glazing.inputs[2])
        links.new(glazing.outputs[0],nodes["Material Output"].inputs["Surface"])
    return material


DEFAULTS = {
    "Roof_Slate": dict(source="Roof_Slate",color="4d5b65",roughness=.71),
    "Fieldstone": dict(source="Fieldstone",color="888b87"),
    "Limestone": dict(source="Limestone",color="baa583",relief=.14),
    "Plaster": dict(source="Plaster",color="d6c6a6",relief=.17),
    "Oak": dict(source="Oak",color="99724e",relief=.19),
    "DarkTimber": dict(source="DarkTimber",color="64503d",relief=.18),
    "Door_Oak": dict(source="Door_Oak",color="705239",relief=.19),
    "Window_Blue": dict(source="Window_Blue",color="466575",roughness=.29,relief=0),
    "Window_Amber": dict(source="Window_Amber",color="c38a51",roughness=.35,relief=0,emittance=.42),
    "Clear_Glazing": dict(color="e1e9e8",roughness=.16,thin_glass=.08),
    "Iron": dict(source="Iron",color="41454c",metallic=.46,relief=.08),
    "Brass": dict(source="Brass",color="b49855",roughness=.31,metallic=.54,relief=.08),
    "Steel": dict(source="Iron",color="b1b5b7",roughness=.34,metallic=.54,relief=.07),
    "Water": dict(source="Water",color="3d8c88",roughness=.26,relief=.18),
    "Ink": dict(color="4b392e",relief=0),
    "Crystal": dict(color="287d99",roughness=.30,metallic=.12,emittance=.40),
    "Spirit_Light": dict(color="9bdae4",roughness=.24,emittance=.60),
    "Canvas_Gold": dict(source="Canvas_Gold",color="b79953",roughness=.82),
    "Canvas_Red": dict(source="Canvas_Red",color="893e44",roughness=.81),
    "Brick": dict(source="Brick",color="a36953",roughness=.73),
    "Cobble": dict(source="Cobble",color="898175",roughness=.80),
    "Earth": dict(source="Earth",color="aa9068",roughness=.85),
}


def city_surface(name, fallback):
    if (TEXTURES/f"{name}_BaseColor.png").is_file():
        return surface(f"CityV2 · {name}", **dict(DEFAULTS.get(name, {}), source=name))
    return fallback


def build_materials(model, overrides):
    surfaces = {}
    for _,name in model.parts:
        if name in surfaces:
            continue
        params = overrides.get(name,DEFAULTS.get(name,dict(source=name)))
        if isinstance(params,bpy.types.Material):
            surfaces[name] = params
        else:
            surfaces[name] = surface(name,**params)
    return surfaces


def artwork(name,pixel,colors,width=256,height=256,emittance=0):
    """Bake small purpose-made artwork into a packed diffuse texture."""
    palette = [(*linear_color(hex_rgb),1) for hex_rgb in colors]
    image = bpy.data.images.new(name,width=width,height=height,alpha=False)
    values = array("f")
    for y in range(height):
        for x in range(width):
            values.extend(palette[pixel(x/width,y/height)])
    image.pixels.foreach_set(values)
    image.pack()
    material = surface(name,roughness=.84,emittance=emittance)
    nodes,links = material.node_tree.nodes,material.node_tree.links
    texture = nodes.new("ShaderNodeTexImage")
    texture.image=image
    texture.extension="EXTEND"
    shader = nodes["Principled BSDF"]
    links.new(texture.outputs["Color"],shader.inputs["Base Color"])
    if emittance:
        links.new(texture.outputs["Color"],shader.inputs["Emission Color"])
    return material
