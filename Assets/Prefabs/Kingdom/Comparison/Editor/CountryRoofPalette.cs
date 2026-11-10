using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WarSimulation.CountryComparison.Editor
{
    public static class CountryRoofPalette
    {
        const string Root = "Assets/Prefabs/Kingdom/Comparison/Materials/Roofs";
        const string Reports = "Artifacts/CountryComparison";
        sealed class Palette
        {
            public readonly string Name, Body, Edge;
            public Palette(string name, string body, string edge) { Name = name; Body = body; Edge = edge; }
        }
        static readonly Palette[] Homes =
        {
            new("Terracotta", "#91543D", "#C58C64"), new("FiredClay", "#814D3C", "#B27C60"),
            new("RoseClay", "#8B594E", "#BF8F7B"), new("Russet", "#704433", "#A87955"),
            new("Sienna", "#8A623F", "#C0996C"), new("WeatheredClay", "#8B7060", "#B9A08A"),
            new("MutedBrick", "#80504A", "#B88472"), new("WarmUmber", "#705A49", "#A89278"),
            new("SmokedClay", "#766558", "#A89883"), new("SmokeSlate", "#59625F", "#929B92"),
            new("DustSlate", "#616975", "#99A2AD"), new("StoneGrey", "#74736A", "#AAA99A")
        };
        static readonly Dictionary<string, Palette> Facilities = new()
        {
            ["WarriorAcademy_Lv5"] = new("Warrior_Oxblood", "#782F34", "#B96B60"),
            ["ArcaneAcademy_Lv5"] = new("Arcane_Indigo", "#3D3D74", "#8A88BD"),
            ["Church"] = new("Church_PatinaBlue", "#356C82", "#8EBAC3"),
            ["Library"] = new("Library_Verdigris", "#386D66", "#83B2A0"),
            ["Tavern"] = new("Tavern_Honey", "#98682D", "#D5B477"),
            ["Observatory"] = new("Observatory_Midnight", "#293852", "#758CB3"),
            ["Museum"] = new("Museum_Bronze", "#7D7158", "#CCB689"),
            ["Clinic"] = new("Clinic_Sage", "#729376", "#C1D2AF"),
            ["Casino"] = new("Casino_Aubergine", "#633E6B", "#B18EAC"),
            ["Guardhouse"] = new("Guard_Steel", "#3F586E", "#8FA9BA"),
            ["CrimsonRowhouse"] = new("Nightlife_Mulberry", "#713E55", "#B17D90"),
            ["VelvetTerrace"] = new("Nightlife_Wine", "#663F53", "#A77E91"),
            ["LanternSpire"] = new("Nightlife_Plum", "#61425C", "#A786A1"),
            ["VeiledCourtyard"] = new("Nightlife_Rosewood", "#754D60", "#B08A98"),
            ["SpiritAcademy_Lv5"] = new("Spirit_Forest", "#2F6249", "#83AF7D"),
            ["Blacksmith"] = new("Smith_Graphite", "#3C4349", "#87918F"),
            ["Plaza"] = new("Plaza_Sandstone", "#8D897B", "#B2AEA0"),
            ["Arena"] = new("Arena_Crimson", "#843C32", "#BC8261"),
            ["Chapel"] = new("Chapel_Pewter", "#697E86", "#ACBAB9"),
            ["Bathhouse"] = new("Bathhouse_Copper", "#657F78", "#A8B9A2"),
            ["Barracks"] = new("Barracks_Brick", "#795447", "#B99070"),
            ["Guildhall"] = new("Guildhall_Bronze", "#786849", "#BEB18B"),
            ["Bakery"] = new("Bakery_Ochre", "#9A7147", "#D0AC76"),
            ["Warehouse"] = new("Warehouse_Tin", "#5C656D", "#9FA6A6"),
            ["Stable"] = new("Stable_Walnut", "#6D5C40", "#AC9A6D"),
            ["Granary"] = new("Granary_Clay", "#846449", "#BD9B76"),
            ["Market_Hall"] = new("MarketHall_Moss", "#5D755D", "#A4B79A")
        };
        static readonly Palette Castle = new("Castle_RoyalBlue", "#3B6084", "#8BAAC4");
        static readonly Dictionary<string, Material> Materials = new();

        public static void BeginBuild()
        {
            Directory.CreateDirectory(Root);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Materials.Clear();
        }

        public static void Apply(GameObject instance, string model, float x, float z)
        {
            CountryRoleFacilities.RemoveHousePlacards(instance,model);
            var facility = Facilities.TryGetValue(model, out var palette);
            uint hash = 2166136261;
            foreach (var c in model) hash = (hash ^ c) * 16777619;
            hash = (hash ^ (uint)Mathf.RoundToInt(x * 10)) * 16777619;
            hash = (hash ^ (uint)Mathf.RoundToInt(z * 10)) * 16777619;
            palette ??= model == "Royal_Castle" ? Castle : Homes[hash % (uint)Homes.Length];
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var slots = renderer.sharedMaterials;
                var changed = false;
                for (var i = 0; i < slots.Length; i++)
                {
                    var source = slots[i];
                    if (source == null) continue;
                    var roof = source.name.StartsWith("Roof_") || facility && source.name == "Copper";
                    var court = model == "Plaza" && source.name == "Cobble" || model == "Arena" && source.name == "Canvas_Red";
                    if (!roof && !court) continue;
                    slots[i] = GetMaterial(source, palette);
                    changed = true;
                }
                if (!changed) continue;
                renderer.sharedMaterials = slots;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
        }

        static Material GetMaterial(Material source, Palette palette)
        {
            var key = palette.Name + "_" + source.name;
            if (Materials.TryGetValue(key, out var cached)) return cached;
            var path = Root + "/" + key + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(source); AssetDatabase.CreateAsset(material, path); }
            else material.CopyPropertiesFromMaterial(source);
            material.shader = Shader.Find("WarSimulation/City Roof Palette");
            if (material.shader == null) throw new InvalidOperationException("City roof palette shader is missing.");
            ColorUtility.TryParseHtmlString(palette.Body, out var body);
            ColorUtility.TryParseHtmlString(palette.Edge, out var edge);
            material.SetColor("_BaseColor", body.linear);
            material.SetColor("_SpecColor", (body * .47f).linear);
            material.SetColor("_EmissionColor", edge.linear);
            material.DisableKeyword("_EMISSION");
            material.DisableKeyword("_SPECULAR_SETUP");
            if (source.name == "Copper")
            {
                material.EnableKeyword("_ROOF_PATINA");
                material.DisableKeyword("_METALLICSPECGLOSSMAP");
                material.SetFloat("_Metallic", .15f);
            }
            material.SetFloat("_Smoothness", .4f);
            EditorUtility.SetDirty(material);
            return Materials[key] = material;
        }

        [MenuItem("WarSim/Country/Validate Roof Palette")]
        public static void Validate()
        {
            var comparison = Object.FindFirstObjectByType<CountryComparisonView>();
            var city = comparison.cities[1];
            var failures = new List<string>();
            var roofs = city.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
                .Where(m => m != null && AssetDatabase.GetAssetPath(m).StartsWith(Root + "/")).ToArray();
            var facilities = new List<object>();
            foreach (var entry in Facilities)
            {
                var models = city.GetComponentsInChildren<Transform>(true)
                    .Where(t => t.name == entry.Key && PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)).ToArray();
                if (models.Length == 0) failures.Add("Missing facility: " + entry.Key);
                foreach (var model in models)
                {
                    var names = model.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
                        .Where(m => m != null && m.name.StartsWith(entry.Value.Name + "_")).Select(m => m.name).Distinct().ToArray();
                    if (names.Length == 0) failures.Add("Facility has no dedicated palette: " + entry.Key);
                    facilities.Add(new { model = entry.Key, palette = entry.Value.Name, materials = names, roofless = entry.Key is "Plaza" or "Arena" });
                }
            }
            var homes = Homes.Select(p => new { palette = p.Name, uses = roofs.Count(m => m.name.StartsWith(p.Name + "_")) }).ToArray();
            if (homes.Any(p => p.uses == 0)) failures.Add("A residential palette is unused.");
            foreach (var material in roofs.Distinct())
                if (ShaderUtil.ShaderHasError(material.shader)) failures.Add("Shader error: " + material.name);
            if (comparison.cities[0].GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
                .Any(m => m != null && AssetDatabase.GetAssetPath(m).StartsWith(Root + "/"))) failures.Add("New palettes leaked into the old city.");
            var river = comparison.transform.Find("River beyond city walls/Continuous central river");
            var mesh = river.GetComponent<MeshFilter>().sharedMesh;
            var uvs = new List<Vector2>(); mesh.GetUVs(2, uvs);
            var combatWater = Resources.Load<Material>("Combat/Map/StylizedRiver");
            if (river.GetComponent<MeshRenderer>().sharedMaterial != combatWater) failures.Add("River does not share the combat material.");
            if (uvs.Count != mesh.vertexCount || uvs.Max(v => v.x) - uvs.Min(v => v.x) < 1000)
                failures.Add("River flow coordinates are incomplete.");
            File.WriteAllText(Reports + "/roof_palette_validation.json", Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                status = failures.Count == 0 ? "PASS" : "FAIL", failures, residentialPalettes = homes,
                facilities, sharedRiverMaterial = AssetDatabase.GetAssetPath(combatWater), riverFlowVertices = uvs.Count
            }, Newtonsoft.Json.Formatting.Indented));
            if (failures.Count > 0) throw new InvalidOperationException(string.Join("\n", failures));
        }

        [MenuItem("WarSim/Country/Capture Facility Roof Views")]
        public static void CaptureFacilities()
        {
            var comparison = Object.FindFirstObjectByType<CountryComparisonView>();
            comparison.Show(1);
            foreach (var entry in Facilities)
            {
                var model = comparison.cities[1].GetComponentsInChildren<Transform>(true).First(t => t.name == entry.Key);
                var bounds = CountryComparisonImporter.BoundsOf(model.gameObject);
                var distance = Mathf.Max(bounds.size.x, bounds.size.z, bounds.size.y) * 1.75f;
                var target = bounds.center + Vector3.up * bounds.size.y * .08f;
                comparison.viewCamera.transform.position = target + new Vector3(.42f, .68f, -1) * distance;
                comparison.viewCamera.transform.LookAt(target);
                CountryComparisonBuilder.CaptureCamera(comparison.viewCamera, Reports + "/Roof_" + entry.Key + ".png");
            }
            comparison.Show(2);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
    }
}
