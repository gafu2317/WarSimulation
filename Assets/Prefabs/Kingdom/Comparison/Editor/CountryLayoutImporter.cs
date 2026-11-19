using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Rendering;

namespace WarSimulation.CountryComparison.Editor
{
    [ScriptedImporter(2, "townlayout")]
    public sealed class CountryLayoutImporter : ScriptedImporter
    {
        const int Magic = 20261117;

        [Serializable] sealed class Town
        {
            public Surface[] materials;
            public Picture[] textures;
            public Geometry[] geometry;
            public Prototype[] prototypes;
            public Placement[] placements;
            public string plan;
        }

        [Serializable] sealed class Surface
        {
            public string name, @base, normal;
            public float[] color, roofShade, roofEdge;
            public float bump, roughness, metallic, opacity, emission;
            public bool roof, tone, cutout;
        }

        [Serializable] sealed class Picture { public string key; public int size; }
        [Serializable] sealed class Geometry { public int vertices; public int[] indices; }
        [Serializable] sealed class Prototype
        {
            public string key;
            public int geometry;
            public int[] materials;
            public float[] entrance;
        }

        [Serializable] sealed class Placement
        {
            public string name, key, group, kind;
            public float[] position, scale;
            public float yaw;
        }

        public override void OnImportAsset(AssetImportContext context)
        {
            using var file = File.OpenRead(context.assetPath);
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            using var reader = new BinaryReader(gzip, Encoding.UTF8);
            if (reader.ReadInt32() != Magic) throw new InvalidDataException("Invalid Country town geometry version.");
            var town = JsonUtility.FromJson<Town>(Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadInt32())));
            var pictures = new Dictionary<string, Texture2D>();
            foreach (var picture in town.textures)
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = picture.key };
                texture.wrapMode = TextureWrapMode.Clamp;
                if (!ImageConversion.LoadImage(texture, reader.ReadBytes(picture.size), true))
                    throw new InvalidDataException("Invalid town texture " + picture.key);
                context.AddObjectToAsset(picture.key, texture);
                pictures.Add(picture.key, texture);
            }

            Texture2D GetTexture(string path)
            {
                if (string.IsNullOrEmpty(path)) return null;
                if (pictures.TryGetValue(path, out var texture)) return texture;
                context.DependsOnSourceAsset(path);
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null) throw new InvalidDataException("Missing town texture " + path);
                pictures[path] = texture;
                return texture;
            }

            var materials = town.materials.Select((source, index) =>
            {
                if (string.IsNullOrEmpty(source.@base) && CityV2MaterialQuality.IsDedicated(source.name))
                {
                    // FBX round-trips can leave the dedicated CityV2 materials with a default Blender surface.
                    var path = CityV2MaterialQuality.Root + source.name + ".mat";
                    context.DependsOnSourceAsset(path);
                    var authored = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (authored != null) return authored;
                }
                var material = CreateSurface(source, GetTexture(source.@base), GetTexture(source.normal));
                context.AddObjectToAsset("material/" + index, material);
                return material;
            }).ToArray();
            var meshes = town.geometry.Select((geometry, index) =>
            {
                var mesh = ReadMesh(reader, geometry, "Town mesh " + index);
                context.AddObjectToAsset("mesh/" + index, mesh);
                return mesh;
            }).ToArray();
            if (gzip.ReadByte() >= 0) throw new InvalidDataException("Unexpected data after the town meshes.");

            var root = new GameObject("新モデルの国");
            var groups = new Dictionary<string, Transform> { [""] = root.transform };
            var prototypes = town.prototypes.ToDictionary(model => model.key);
            var repeated = town.placements.GroupBy(p => (p.group, p.name)).Where(g => g.Count() > 1)
                .ToDictionary(g => g.Key, _ => 0);
            foreach (var placement in town.placements)
            {
                var parent = Group(placement.group, groups);
                var model = prototypes[placement.key];
                var id = (placement.group, placement.name);
                string name = repeated.TryGetValue(id, out int number)
                    ? placement.name + " (" + (repeated[id] = number + 1) + ")" : placement.name;
                var instance = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                instance.transform.SetParent(parent, false);
                instance.transform.SetLocalPositionAndRotation(Vector(placement.position), Quaternion.Euler(0, placement.yaw, 0));
                instance.transform.localScale = Vector(placement.scale);
                instance.GetComponent<MeshFilter>().sharedMesh = meshes[model.geometry];
                var renderer = instance.GetComponent<MeshRenderer>();
                renderer.sharedMaterials = model.materials.Select(i => materials[i]).ToArray();
                if (model.entrance is { Length: 3 })
                {
                    var entrance = new GameObject("Entrance").transform;
                    entrance.SetParent(instance.transform, false);
                    entrance.localPosition = Vector(model.entrance);
                }
            }
            context.AddObjectToAsset("town", root);
            context.AddObjectToAsset("plan", new TextAsset(town.plan) { name = "Town layout plan" });
            context.SetMainObject(root);
        }

        static Transform Group(string path, Dictionary<string, Transform> groups)
        {
            if (groups.TryGetValue(path, out var existing)) return existing;
            int slash = path.LastIndexOf('/');
            var group = new GameObject(path[(slash + 1)..]).transform;
            group.SetParent(Group(slash < 0 ? "" : path[..slash], groups), false);
            groups[path] = group;
            return group;
        }

        static Vector3 Vector(float[] values) => new(values[0], values[1], values[2]);
        static Color ColorValue(float[] values) => new(values[0], values[1], values[2], values[3]);

        static Mesh ReadMesh(BinaryReader reader, Geometry geometry, string name)
        {
            var vertices = new Vector3[geometry.vertices];
            var normals = new Vector3[geometry.vertices];
            var uv = new Vector2[geometry.vertices];
            for (int i = 0; i < vertices.Length; ++i)
            {
                vertices[i] = new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                normals[i] = new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                uv[i] = new(reader.ReadSingle(), reader.ReadSingle());
            }
            var mesh = new Mesh
            {
                name = name,
                indexFormat = vertices.Length > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16,
                vertices = vertices,
                normals = normals,
                uv = uv,
                subMeshCount = geometry.indices.Length
            };
            for (int i = 0; i < geometry.indices.Length; ++i)
            {
                var indices = new int[geometry.indices[i]];
                for (int j = 0; j < indices.Length; ++j) indices[j] = reader.ReadInt32();
                mesh.SetTriangles(indices, i, false);
            }
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        static Material CreateSurface(Surface source, Texture2D albedo, Texture2D normal)
        {
            bool transparent = source.opacity < .99f;
            var shaderName = source.roof ? "WarSimulation/City Roof Palette" :
                source.tone && !transparent && !source.cutout ? "WarSimulation/City Surface Tone" :
                "Universal Render Pipeline/Lit";
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidDataException("Missing town shader " + shaderName);
            var material = new Material(shader) { name = source.name, enableInstancing = true };
            var color = ColorValue(source.color);
            color.a = source.opacity;
            material.SetColor("_BaseColor", color);
            material.SetTexture("_BaseMap", albedo);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetFloat("_Smoothness", Mathf.Clamp01(1 - source.roughness));
            material.SetFloat("_Metallic", source.metallic);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", source.bump);
                material.EnableKeyword("_NORMALMAP");
            }
            if (source.roof)
            {
                material.SetColor("_SpecColor", ColorValue(source.roofShade));
                material.SetColor("_EmissionColor", ColorValue(source.roofEdge));
            }
            else if (shaderName == "WarSimulation/City Surface Tone")
            {
                material.EnableKeyword("_TOWN_LAYOUT_TONE");
            }
            else if (source.emission > 0)
            {
                material.EnableKeyword("_EMISSION");
                material.SetTexture("_EmissionMap", albedo);
                material.SetColor("_EmissionColor", color * source.emission);
            }
            if (source.cutout)
            {
                material.SetFloat("_AlphaClip", 1);
                material.SetFloat("_Cutoff", .42f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.renderQueue = (int)RenderQueue.AlphaTest;
            }
            if (transparent)
            {
                material.SetFloat("_Surface", 1);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetShaderPassEnabled("ShadowCaster", false);
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            return material;
        }
    }
}
