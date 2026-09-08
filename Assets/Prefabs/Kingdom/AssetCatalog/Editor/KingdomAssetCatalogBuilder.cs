#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace WarSimulation.Kingdom.EditorOnly
{
    public static class KingdomAssetCatalogBuilder
    {
        const string ScenePath = "Assets/Scenes/KingdomAssetCatalog.unity";
        const string MaterialRoot = "Assets/Images/Materials";
        const string ValidationPath = "docs/Art/KingdomAssetCatalog/validation.json";
        const string FontPath = "Assets/Fonts/Noto_Sans_JP/static/NotoSansJP-Regular SDF.asset";

        static readonly string[] PrefabRoots =
        {
            "Assets/Prefabs/Kingdom/City/Prefabs",
            "Assets/Prefabs/Kingdom/NewFantasyAssets/Prefabs",
            "Assets/Prototypes/TownBlock/Prefabs",
            "Assets/Prefabs/Environment/NaturalRocks",
            "Assets/Prefabs/Environment/NaturalTrees"
        };

        static readonly string[] CategoryOrder =
        {
            "01 Buildings",
            "02 Props and Stalls",
            "03 Roads and Walls",
            "04 Vegetation and Rocks"
        };

        static readonly HashSet<string> PropNames = new HashSet<string>
        {
            "Anvil", "Clothesline", "Firewood_Rack", "Handcart", "Hay_Bale", "Forge",
            "Noticeboard", "Signpost", "Water_Trough", "Well", "Barrel", "Bench", "Cloth_Stall",
            "Crate_Closed", "Produce_Stall", "Streetlamp"
        };

        static readonly string[] DisplayOrder =
        {
            "Fantasy_House", "MerchantHouse", "WorkshopHouse", "Bakery", "Tavern",
            "Blacksmith", "Stable", "Warehouse", "Granary", "Guildhall", "Bathhouse", "Clinic",
            "Chapel", "Church", "Guardhouse", "Barracks",
            "WarriorAcademy", "ArcaneAcademy", "SpiritAcademy",
            "CrimsonRowhouse", "VelvetTerrace", "LanternSpire", "VeiledCourtyard",
            "Casino", "Museum", "Library", "Observatory", "Arena", "Royal_Castle",
            "Produce_Stall", "Cloth_Stall", "Barrel", "Crate_Closed", "Handcart",
            "Forge", "Anvil", "Firewood_Rack", "Hay_Bale", "Water_Trough", "Well",
            "Bench", "Streetlamp", "Signpost", "Noticeboard", "Clothesline",
            "Road_Straight", "Road_Corner", "Road_T", "Road_Cross", "Road_End",
            "Paved_Plot", "Plaza", "Granite_Straight", "Granite_Corner", "Granite_Gate",
            "GroundPlant_GrassShort", "GroundPlant_GrassTuft", "GroundPlant_GrassTall",
            "GroundPlant_FernPatch", "Flower_WildPatch", "Flower_Border",
            "Tree_Street", "Tree_Shade", "Tree_AlleyCypress",
            "NaturalTree_01", "NaturalTree_02", "NaturalTree_03", "NaturalTree_04", "NaturalTree_05",
            "NaturalTree_06", "NaturalTree_07", "NaturalTree_08", "NaturalTree_09", "NaturalTree_10",
            "NaturalRock_02", "NaturalRock_04", "NaturalRock_07", "NaturalRock_08", "NaturalRock_11"
        };

        static int DisplayIndex(string name)
        {
            var index = Array.IndexOf(DisplayOrder, name);
            return index < 0 ? DisplayOrder.Length : index;
        }

        static readonly List<PlacementRecord> Placements = new List<PlacementRecord>();
        static readonly List<Transform> Labels = new List<Transform>();
        static readonly List<Bounds> Pads = new List<Bounds>();

        [Serializable]
        class PlacementRecord
        {
            public string name;
            public string category;
            public string prefab;
            public Vector3 position;
            public Vector3 size;
            public float groundHeight;
            public bool prefabConnection;
        }

        [Serializable]
        class ValidationReport
        {
            public string scene;
            public string unityVersion;
            public int sourcePrefabs;
            public int placedPrefabs;
            public int itemLabels;
            public int buildingCount;
            public int propCount;
            public int roadAndWallCount;
            public int vegetationAndRockCount;
            public bool allSourcesPlaced;
            public bool allGrounded;
            public bool allPrefabConnectionsPreserved;
            public bool noDisplayPadOverlap;
            public bool allModelsInsideDisplayPads;
            public bool mainCameraConfigured;
            public bool notoSansLabels;
            public PlacementRecord[] placements;
            public string status;
        }

        class CatalogEntry
        {
            public string Path;
            public GameObject Prefab;
            public string Category;
            public GameObject Instance;
            public Vector3 Size;
            public Vector2 PadSize;
        }

        [MenuItem("WarSim/Kingdom/Build Asset Catalog Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Exit Play mode before building the asset catalog.");
            for (var index = 0; index < SceneManager.sceneCount; index++)
                if (SceneManager.GetSceneAt(index).isDirty)
                    throw new InvalidOperationException("Save the current scene before building the asset catalog.");

            var entries = DiscoverEntries();
            if (entries.Count == 0)
                throw new InvalidOperationException("No kingdom prefabs were found.");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
                throw new InvalidOperationException("Noto Sans JP font asset was not found at " + FontPath);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            Directory.CreateDirectory(MaterialRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(ValidationPath));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "KingdomAssetCatalog";
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            Placements.Clear();
            Labels.Clear();
            Pads.Clear();

            var root = new GameObject("Kingdom Asset Catalog").transform;
            var displayRoot = new GameObject("Displays").transform;
            displayRoot.SetParent(root, false);
            var labelRoot = new GameObject("Labels").transform;
            labelRoot.SetParent(root, false);
            var platformRoot = new GameObject("Platforms").transform;
            platformRoot.SetParent(root, false);

            var cursorZ = 0f;
            foreach (var category in CategoryOrder)
            {
                var categoryEntries = entries.Where(entry => entry.Category == category).OrderBy(entry => DisplayIndex(entry.Prefab.name)).ThenBy(entry => entry.Prefab.name, StringComparer.Ordinal).ToList();
                if (categoryEntries.Count == 0) continue;
                cursorZ = BuildCategory(category, categoryEntries, cursorZ, displayRoot, labelRoot, platformRoot, font);
            }

            var assetBounds = CombinedPlacedBounds();
            BuildFloor(assetBounds, platformRoot);
            var camera = BuildLightingAndCamera(assetBounds, root);
            FaceLabelsTowards(camera.transform);
            EditorSceneManager.SaveScene(scene, ScenePath);
            var report = Validate(entries, font, camera);
            File.WriteAllText(ValidationPath, JsonUtility.ToJson(report, true));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (report.status != "PASS")
                throw new InvalidOperationException("Asset catalog validation failed. See " + ValidationPath);

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(assetBounds.center, Quaternion.Euler(48, 180, 0), assetBounds.extents.magnitude);
            Debug.Log($"Kingdom asset catalog saved with {report.placedPrefabs} prefabs: {ScenePath}");
        }

        static List<CatalogEntry> DiscoverEntries()
        {
            var paths = new HashSet<string>();
            foreach (var root in PrefabRoots)
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { root }))
                    paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            return paths.OrderBy(path => path).Select(path =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                return new CatalogEntry { Path = path, Prefab = prefab, Category = CategoryFor(path, prefab.name) };
            }).ToList();
        }

        static string CategoryFor(string path, string name)
        {
            if (path.Contains("/Environment/")
                || name.StartsWith("Tree_", StringComparison.Ordinal)
                || name.StartsWith("GroundPlant_", StringComparison.Ordinal)
                || name.StartsWith("Flower_", StringComparison.Ordinal))
                return "04 Vegetation and Rocks";
            if (name.StartsWith("Road_", StringComparison.Ordinal)
                || name.StartsWith("Granite_", StringComparison.Ordinal)
                || name == "Paved_Plot"
                || name == "Plaza")
                return "03 Roads and Walls";
            if (PropNames.Contains(name)) return "02 Props and Stalls";
            return "01 Buildings";
        }

        static float BuildCategory(
            string category,
            List<CatalogEntry> entries,
            float startZ,
            Transform displayRoot,
            Transform labelRoot,
            Transform platformRoot,
            TMP_FontAsset font)
        {
            var categoryRoot = new GameObject(category).transform;
            categoryRoot.SetParent(displayRoot, false);
            foreach (var entry in entries)
            {
                entry.Instance = PrefabUtility.InstantiatePrefab(entry.Prefab) as GameObject;
                if (entry.Instance == null) throw new InvalidOperationException("Could not instantiate " + entry.Path);
                entry.Instance.name = entry.Prefab.name;
                entry.Instance.transform.SetParent(categoryRoot, false);
                var bounds = RendererBounds(entry.Instance);
                entry.Size = bounds.size;
                var minimumPad = category == "01 Buildings" ? 6f : 4f;
                entry.PadSize = new Vector2(Mathf.Max(bounds.size.x + 2, minimumPad), Mathf.Max(bounds.size.z + 2, minimumPad));
            }

            var columns = Mathf.CeilToInt(Mathf.Sqrt(entries.Count));
            var rows = Mathf.CeilToInt(entries.Count / (float)columns);
            var columnWidths = new float[columns];
            var rowDepths = new float[rows];
            for (var index = 0; index < entries.Count; index++)
            {
                var column = index % columns;
                var row = index / columns;
                columnWidths[column] = Mathf.Max(columnWidths[column], entries[index].PadSize.x);
                rowDepths[row] = Mathf.Max(rowDepths[row], entries[index].PadSize.y);
            }

            var gap = 3f;
            var totalWidth = columnWidths.Sum() + gap * (columns - 1);
            var columnCenters = Centers(columnWidths, gap, -totalWidth * 0.5f);
            var firstRowFront = startZ + 6f;
            var rowCenters = Centers(rowDepths, gap, firstRowFront);
            CreateLabel(category, new Vector3(0, 4, startZ), new Vector2(totalWidth, 3), 2.2f, labelRoot, font, true);

            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                var column = index % columns;
                var row = index / columns;
                var x = columnCenters[column];
                var z = rowCenters[row];
                var before = RendererBounds(entry.Instance);
                entry.Instance.transform.position = new Vector3(x - before.center.x, -before.min.y, z - before.center.z);
                var after = RendererBounds(entry.Instance);
                var pad = CreatePad(entry, new Vector3(x, -0.1f, z), platformRoot);
                Pads.Add(pad.GetComponent<Renderer>().bounds);
                CreateLabel(entry.Prefab.name, new Vector3(x, Mathf.Max(1.2f, after.size.y * 0.08f), z - entry.PadSize.y * 0.5f),
                    new Vector2(entry.PadSize.x, 1.4f), 0.72f, labelRoot, font, false);
                Placements.Add(new PlacementRecord
                {
                    name = entry.Prefab.name,
                    category = category,
                    prefab = entry.Path,
                    position = entry.Instance.transform.position,
                    size = after.size,
                    groundHeight = after.min.y,
                    prefabConnection = PrefabUtility.GetCorrespondingObjectFromSource(entry.Instance) != null
                });
            }

            return firstRowFront + rowDepths.Sum() + gap * (rows - 1) + 10f;
        }

        static float[] Centers(float[] sizes, float gap, float start)
        {
            var centers = new float[sizes.Length];
            var cursor = start;
            for (var index = 0; index < sizes.Length; index++)
            {
                centers[index] = cursor + sizes[index] * 0.5f;
                cursor += sizes[index] + gap;
            }
            return centers;
        }

        static GameObject CreatePad(CatalogEntry entry, Vector3 position, Transform parent)
        {
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = entry.Prefab.name + " Display Pad";
            pad.transform.SetParent(parent, false);
            pad.transform.position = position;
            pad.transform.localScale = new Vector3(entry.PadSize.x, 0.2f, entry.PadSize.y);
            pad.GetComponent<Renderer>().sharedMaterial = MaterialForCategory(entry.Category);
            UnityEngine.Object.DestroyImmediate(pad.GetComponent<Collider>());
            return pad;
        }

        static void CreateLabel(
            string value,
            Vector3 position,
            Vector2 size,
            float fontSize,
            Transform parent,
            TMP_FontAsset font,
            bool header)
        {
            var labelObject = new GameObject((header ? "Section - " : "Label - ") + value, typeof(RectTransform), typeof(TextMeshPro));
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.position = position;
            var rect = (RectTransform)labelObject.transform;
            rect.sizeDelta = size;
            var label = labelObject.GetComponent<TextMeshPro>();
            label.ForceMeshUpdate();
            label.text = value;
            label.font = font;
            label.fontSharedMaterial = font.material;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.color = header ? new Color(1f, 0.82f, 0.38f) : Color.white;
            Labels.Add(labelObject.transform);
        }

        static Material MaterialForCategory(string category)
        {
            var color = category switch
            {
                "01 Buildings" => new Color(0.23f, 0.27f, 0.32f),
                "02 Props and Stalls" => new Color(0.31f, 0.25f, 0.18f),
                "03 Roads and Walls" => new Color(0.28f, 0.29f, 0.28f),
                _ => new Color(0.18f, 0.29f, 0.19f)
            };
            return CatalogMaterial(category.Replace(" ", "_"), color);
        }

        static Material CatalogMaterial(string name, Color color)
        {
            var path = MaterialRoot + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("Universal Render Pipeline/Lit shader is unavailable.");
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", 0);
            material.SetFloat("_Smoothness", 0.18f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        static void BuildFloor(Bounds bounds, Transform parent)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Catalog Ground";
            floor.transform.SetParent(parent, false);
            floor.transform.position = new Vector3(bounds.center.x, -0.22f, bounds.center.z);
            floor.transform.localScale = new Vector3(bounds.size.x + 12, 0.2f, bounds.size.z + 12);
            floor.GetComponent<Renderer>().sharedMaterial = CatalogMaterial("Catalog_Ground", new Color(0.105f, 0.12f, 0.14f));
            UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());
        }

        static Camera BuildLightingAndCamera(Bounds bounds, Transform root)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.52f, 0.55f, 0.61f);
            RenderSettings.fog = false;

            var lightObject = new GameObject("Catalog Sun", typeof(Light));
            lightObject.transform.SetParent(root, false);
            lightObject.transform.rotation = Quaternion.Euler(48, -32, 0);
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.92f, 0.79f);
            light.intensity = 1.25f;
            light.shadows = LightShadows.Soft;

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(root, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.fieldOfView = 42;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.07f, 0.09f);
            var radius = bounds.extents.magnitude;
            var viewDirection = new Vector3(0, -0.58f, 1).normalized;
            var distance = radius / Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * 1.2f;
            cameraObject.transform.position = bounds.center - viewDirection * distance;
            cameraObject.transform.LookAt(bounds.center + Vector3.up * bounds.extents.y * 0.15f);
            camera.nearClipPlane = Mathf.Max(0.1f, distance - radius * 1.5f);
            camera.farClipPlane = distance + radius * 2.5f;
            return camera;
        }

        static void FaceLabelsTowards(Transform camera)
        {
            foreach (var label in Labels)
                label.rotation = Quaternion.LookRotation(label.position - camera.position, Vector3.up);
        }

        static ValidationReport Validate(List<CatalogEntry> entries, TMP_FontAsset font, Camera camera)
        {
            var noOverlap = true;
            for (var left = 0; left < Pads.Count; left++)
                for (var right = left + 1; right < Pads.Count; right++)
                    if (Pads[left].Intersects(Pads[right])) noOverlap = false;
            var itemLabels = Labels.Count - CategoryOrder.Count(category => entries.Any(entry => entry.Category == category));
            var report = new ValidationReport
            {
                scene = ScenePath,
                unityVersion = Application.unityVersion,
                sourcePrefabs = entries.Count,
                placedPrefabs = Placements.Count,
                itemLabels = itemLabels,
                buildingCount = Placements.Count(record => record.category == "01 Buildings"),
                propCount = Placements.Count(record => record.category == "02 Props and Stalls"),
                roadAndWallCount = Placements.Count(record => record.category == "03 Roads and Walls"),
                vegetationAndRockCount = Placements.Count(record => record.category == "04 Vegetation and Rocks"),
                allSourcesPlaced = Placements.Select(record => record.prefab).Distinct().Count() == entries.Count,
                allGrounded = Placements.All(record =>
                    Mathf.Abs(record.groundHeight) <= Mathf.Max(1, record.size.y) * 0.00001f),
                allPrefabConnectionsPreserved = Placements.All(record => record.prefabConnection),
                noDisplayPadOverlap = noOverlap,
                allModelsInsideDisplayPads = entries.All(entry =>
                {
                    var model = RendererBounds(entry.Instance);
                    var pad = GameObject.Find("Kingdom Asset Catalog/Platforms/" + entry.Prefab.name + " Display Pad").GetComponent<Renderer>().bounds;
                    return model.min.x >= pad.min.x && model.max.x <= pad.max.x
                        && model.min.z >= pad.min.z && model.max.z <= pad.max.z;
                }),
                mainCameraConfigured = camera != null && camera.CompareTag("MainCamera"),
                notoSansLabels = Labels.All(label => label.GetComponent<TextMeshPro>().font == font),
                placements = Placements.ToArray()
            };
            report.status = report.placedPrefabs == report.sourcePrefabs
                && report.itemLabels == report.sourcePrefabs
                && report.allSourcesPlaced
                && report.allGrounded
                && report.allPrefabConnectionsPreserved
                && report.noDisplayPadOverlap
                && report.allModelsInsideDisplayPads
                && report.mainCameraConfigured
                && report.notoSansLabels ? "PASS" : "FAIL";
            return report;
        }

        static Bounds CombinedPlacedBounds()
        {
            var renderers = Placements.SelectMany(record =>
                GameObject.Find("Kingdom Asset Catalog/Displays/" + record.category + "/" + record.name)
                    .GetComponentsInChildren<Renderer>(true));
            return CombinedBounds(renderers);
        }

        static Bounds RendererBounds(GameObject root)
        {
            return CombinedBounds(root.GetComponentsInChildren<Renderer>(true));
        }

        static Bounds CombinedBounds(IEnumerable<Renderer> renderers)
        {
            using var enumerator = renderers.GetEnumerator();
            if (!enumerator.MoveNext()) throw new InvalidOperationException("A catalog prefab has no renderer.");
            var bounds = enumerator.Current.bounds;
            while (enumerator.MoveNext()) bounds.Encapsulate(enumerator.Current.bounds);
            return bounds;
        }
    }
}
#endif
