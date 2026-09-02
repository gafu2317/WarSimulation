#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace WarSimulation.Kingdom.City
{
    public static class KingdomCityBuilder
    {
        const string ScenePath = "Assets/Scenes/Country.unity";
        const string ModelRoot = "Assets/Models/Kingdom/City";
        const string PrefabRoot = "Assets/Prefabs/Kingdom/City";
        const string TownBlockRoot = "Assets/Prototypes/TownBlock";
        const string EnvironmentPrefabRoot = "Assets/Prefabs/Environment/NaturalTrees";
        const string ReviewRoot = "docs/Art/KingdomCity";

        [Serializable] class ExportModel { public string name; public string fbx; public float[] minimum; public float[] maximum; public int triangles; public int atlas_size; }
        [Serializable] class ExportManifest { public ExportModel[] models; }
        [Serializable] class PlacementRecord { public string name; public string district; public Vector3 position; public float rotationY; public Vector3 size; }
        [Serializable] class CityReport
        {
            public string scene;
            public string unityVersion;
            public int importedPrefabTypes;
            public int townBlockPrefabTypes;
            public int prefabInstances;
            public int wallModules;
            public int houses;
            public int facilities;
            public int roads;
            public int props;
            public int entranceConnections;
            public int entranceTargetCount;
            public int entranceTargetsOnRoad;
            public int importantFacilityCount;
            public int enlargedImportantFacilityCount;
            public int buildingCount;
            public float walledArea;
            public float buildingFootprintArea;
            public float occupiedFootprintRatio;
            public float managedOpenSpaceArea;
            public float developedAreaRatio;
            public int coplanarCourtyardPads;
            public bool allBuildingsInsideWalls;
            public bool noBuildingOverlap;
            public bool southernGateToCastleRoute;
            public bool allFacilityEntrancesConnected;
            public bool noDuplicateRoadTiles;
            public bool noCoplanarTerrainRoads;
            public float minimumTerrainRoadClearance;
            public float minimumBuildingGap;
            public PlacementRecord[] placements;
            public string[] districts;
            public string[] limitations;
        }

        static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();
        static readonly List<GameObject> Instances = new List<GameObject>();
        static readonly List<GameObject> Buildings = new List<GameObject>();
        static readonly List<GameObject> Roads = new List<GameObject>();
        static readonly List<GameObject> Walkways = new List<GameObject>();
        static readonly List<GameObject> ImportantFacilities = new List<GameObject>();
        static readonly List<Vector3> EntranceRoadTargets = new List<Vector3>();
        static readonly List<Bounds> ReservedAreas = new List<Bounds>();
        static readonly List<PlacementRecord> Records = new List<PlacementRecord>();
        static Transform kingdom;
        static Transform walkwayRoot;
        static Material groundMaterial;
        static Material lotMaterial;
        static Material dirtMaterial;
        static Material gardenMaterial;
        static float managedOpenSpaceArea;

        [MenuItem("WarSim/Kingdom/Build Country City")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before building Country.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException("Open Assets/Scenes/Country.unity before building the kingdom.");
            if (scene.isDirty) throw new InvalidOperationException("Save unrelated Country changes before rebuilding the kingdom.");
            var manifest = JsonUtility.FromJson<ExportManifest>(File.ReadAllText(ReviewRoot + "/export_manifest.json"));
            Directory.CreateDirectory(PrefabRoot + "/Materials");
            Directory.CreateDirectory(PrefabRoot + "/Prefabs");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            BuildImportedPrefabs(manifest);
            LoadPrefabs();
            var previous = GameObject.Find("Kingdom");
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous);
            Prefabs.Clear();
            LoadPrefabs();
            Instances.Clear(); Buildings.Clear(); Roads.Clear(); Walkways.Clear(); ImportantFacilities.Clear(); EntranceRoadTargets.Clear(); ReservedAreas.Clear(); Records.Clear(); managedOpenSpaceArea = 0;
            kingdom = new GameObject("Kingdom").transform;
            Undo.RegisterCreatedObjectUndo(kingdom.gameObject, "Build Country kingdom");
            BuildMaterials();
            BuildLand();
            BuildWalls();
            BuildRoads();
            walkwayRoot = District("03 Entrance Connections");
            BuildRoyalDistrict();
            BuildCivicDistrict();
            BuildMarketAndCraftDistrict();
            BuildNightlifeDistrict();
            BuildOpenSpaces();
            BuildResidentialDistricts();
            BuildInfillDetails();
            BuildStreetFurniture();
            ConfigureLightingAndCamera();
            Physics.SyncTransforms();
            ValidateAndWriteReport(manifest);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = kingdom.gameObject;
            Debug.Log("Country kingdom built and saved: " + ScenePath);
        }

        static void BuildImportedPrefabs(ExportManifest manifest)
        {
            foreach (var model in manifest.models)
            {
                ImportTexture(model.name + "_BaseColor.png", true, false, model.atlas_size);
                ImportTexture(model.name + "_Normal.png", false, true, model.atlas_size);
                ImportTexture(model.name + "_MetallicSmoothness.png", false, false, model.atlas_size);
                AssetDatabase.ImportAsset(model.fbx, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(model.fbx) as ModelImporter;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importAnimation = false;
                importer.globalScale = 1;
                importer.useFileScale = true;
                importer.bakeAxisConversion = true;
                importer.importNormals = ModelImporterNormals.Import;
                importer.importTangents = ModelImporterTangents.CalculateMikk;
                importer.isReadable = true;
                importer.SaveAndReimport();
                var material = BuildMaterial(model.name);
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(model.fbx);
                var root = new GameObject(model.name);
                var visual = PrefabUtility.InstantiatePrefab(source) as GameObject;
                visual.name = "Visual";
                visual.transform.SetParent(root.transform, false);
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = material;
                foreach (var filter in visual.GetComponentsInChildren<MeshFilter>())
                {
                    var collider = filter.gameObject.GetComponent<MeshCollider>();
                    if (collider == null) collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    collider.convex = false;
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabRoot + "/Prefabs/" + model.name + ".prefab");
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static void ImportTexture(string file, bool srgb, bool normal, int size)
        {
            string path = ModelRoot + "/Textures/" + file;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            importer.sRGBTexture = srgb;
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.alphaSource = normal || srgb ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.maxTextureSize = size;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        static Material BuildMaterial(string name)
        {
            string path = PrefabRoot + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = Shader.Find("Universal Render Pipeline/Lit");
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ModelRoot + "/Textures/" + name + "_BaseColor.png"));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ModelRoot + "/Textures/" + name + "_Normal.png"));
            material.SetFloat("_BumpScale", 1);
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ModelRoot + "/Textures/" + name + "_MetallicSmoothness.png"));
            material.SetFloat("_Metallic", 1);
            material.SetFloat("_Smoothness", 1);
            material.SetFloat("_SmoothnessTextureChannel", 0);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        static void LoadPrefabs()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot + "/Prefabs", TownBlockRoot + "/Prefabs", EnvironmentPrefabRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) Prefabs[prefab.name] = prefab;
            }
        }

        static void BuildMaterials()
        {
            groundMaterial = SimpleMaterial("Kingdom Ground", new Color(0.24f, 0.31f, 0.19f), 0, 0.2f);
            lotMaterial = SimpleMaterial("Courtyard Stone", new Color(0.57f, 0.56f, 0.51f), 0, 0.22f);
            dirtMaterial = SimpleMaterial("Managed Earth", new Color(0.32f, 0.25f, 0.16f), 0, 0.08f);
            gardenMaterial = SimpleMaterial("Kitchen Garden", new Color(0.24f, 0.38f, 0.16f), 0, 0.06f);
        }

        static Material SimpleMaterial(string name, Color color, float metallic, float smoothness)
        {
            string path = PrefabRoot + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            return material;
        }

        static Transform District(string name)
        {
            var root = new GameObject(name).transform;
            root.SetParent(kingdom, false);
            return root;
        }

        static GameObject Place(string name, string district, Vector3 position, float yaw = 0, Transform parent = null, bool building = false, float scale = 1)
        {
            var instance = PrefabUtility.InstantiatePrefab(Prefabs[name]) as GameObject;
            instance.name = name;
            instance.transform.SetParent(parent == null ? kingdom : parent, false);
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.Euler(0, yaw, 0);
            instance.transform.localScale = Vector3.one * scale;
            Instances.Add(instance);
            if (building) Buildings.Add(instance);
            if (name.StartsWith("Road_")) Roads.Add(instance);
            var bounds = BoundsOf(instance);
            Records.Add(new PlacementRecord { name = name, district = district, position = position, rotationY = yaw, size = bounds.size });
            return instance;
        }

        static GameObject PlaceFacing(string name, string district, Vector3 position, Vector3 entranceDirection, Transform parent, bool building = true, float scale = 1)
        {
            float yaw = Quaternion.LookRotation(-entranceDirection.normalized, Vector3.up).eulerAngles.y;
            return Place(name, district, position, yaw, parent, building, scale);
        }

        static Bounds BoundsOf(GameObject obj)
        {
            var renderers = obj.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        static void BuildLand()
        {
            var root = District("00 Land");
            var land = GameObject.CreatePrimitive(PrimitiveType.Cube);
            land.name = "Kingdom Ground";
            land.transform.SetParent(root, false);
            land.transform.position = new Vector3(0, -0.55f, 0);
            land.transform.localScale = new Vector3(240, 1, 200);
            land.GetComponent<Renderer>().sharedMaterial = groundMaterial;
        }

        static void BuildWalls()
        {
            var root = District("01 Fortifications");
            int[] horizontal = Enumerable.Range(-12, 25).Select(i => i * 8).ToArray();
            foreach (float x in horizontal)
            {
                Place(x == 0 ? "Granite_Gate" : "Granite_Straight", "Fortifications", new Vector3(x, 0, -84), 0, root);
                Place("Granite_Straight", "Fortifications", new Vector3(x, 0, 84), 180, root);
            }
            foreach (float z in Enumerable.Range(0, 20).Select(i => -76 + i * 8))
            {
                bool gate = z == -28;
                Place(gate ? "Granite_Gate" : "Granite_Straight", "Fortifications", new Vector3(-104, 0, z), 90, root);
                Place(gate ? "Granite_Gate" : "Granite_Straight", "Fortifications", new Vector3(104, 0, z), 270, root);
            }
            Place("Granite_Corner", "Fortifications", new Vector3(-104, 0, -84), 0, root);
            Place("Granite_Corner", "Fortifications", new Vector3(104, 0, -84), 90, root);
            Place("Granite_Corner", "Fortifications", new Vector3(104, 0, 84), 180, root);
            Place("Granite_Corner", "Fortifications", new Vector3(-104, 0, 84), 270, root);
        }

        static readonly Vector2Int[] RoadDirections = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        static bool[] RoadPorts(GameObject obj)
        {
            Physics.SyncTransforms();
            var collider = obj.GetComponentInChildren<MeshCollider>();
            var ports = new bool[4];
            for (int i = 0; i < RoadDirections.Length; i++)
            {
                var direction = RoadDirections[i];
                Vector3 probe = obj.transform.TransformPoint(new Vector3(direction.x * 3.5f, 2, direction.y * 3.5f));
                RaycastHit hit;
                if (!collider.Raycast(new Ray(probe, Vector3.down), out hit, 4)) throw new InvalidOperationException("Missing road surface: " + obj.name);
                ports[i] = Mathf.Abs(hit.point.y - obj.transform.position.y) < 0.001f;
            }
            return ports;
        }

        static void BuildRoads()
        {
            var root = District("02 Roads");
            var cells = new HashSet<Vector2Int>();
            Func<int, int, Vector2Int> cell = (x, z) => new Vector2Int(x / 8, (z - 4) / 8);
            for (int z = -92; z <= 28; z += 8) if (z != -12 && z != -4 && z != 4) cells.Add(cell(0, z));
            for (int x = -24; x <= 24; x += 8) cells.Add(cell(x, -76));
            foreach (int z in new[] { -28, 12, 28 })
                for (int x = z == -28 ? -112 : -88; x <= (z == -28 ? 112 : 88); x += 8) cells.Add(cell(x, z));
            foreach (int start in new[] { -88, 32 })
                for (int x = start; x <= (start < 0 ? -32 : 88); x += 8) cells.Add(cell(x, 44));
            foreach (int x in new[] { -72, -40, 40, 72 })
                for (int z = -76; z <= 60; z += 8) cells.Add(cell(x, z));
            var basePorts = new Dictionary<string, bool[]>();
            foreach (string name in new[] { "Road_Straight", "Road_Corner", "Road_T", "Road_Cross", "Road_End" })
            {
                var probe = PrefabUtility.InstantiatePrefab(Prefabs[name]) as GameObject;
                basePorts[name] = RoadPorts(probe);
                UnityEngine.Object.DestroyImmediate(probe);
            }
            foreach (var current in cells.OrderBy(value => value.x).ThenBy(value => value.y))
            {
                bool placed = false;
                foreach (var type in basePorts)
                {
                    for (int turn = 0; turn < 4 && !placed; turn++)
                    {
                        bool fits = true;
                        for (int i = 0; i < RoadDirections.Length; i++)
                            if (type.Value[(i - turn + 4) % 4] != cells.Contains(current + RoadDirections[i])) fits = false;
                        if (!fits) continue;
                        Place(type.Key, "Roads", new Vector3(current.x * 8, 0, current.y * 8 + 4), turn * 90, root);
                        placed = true;
                    }
                    if (placed) break;
                }
                if (!placed) throw new InvalidOperationException("No road module fits " + current);
            }
        }

        static Bounds Expanded(Bounds bounds, float amount)
        {
            bounds.Expand(new Vector3(amount * 2, 0, amount * 2));
            return bounds;
        }

        static void PathSegment(Vector3 start, Vector3 end, float width)
        {
            Vector3 delta = end - start;
            float length = new Vector2(delta.x, delta.z).magnitude;
            if (length < 0.05f) return;
            var path = GameObject.CreatePrimitive(PrimitiveType.Cube);
            path.name = "Entrance Walkway";
            path.transform.SetParent(walkwayRoot, false);
            path.transform.position = (start + end) * 0.5f + Vector3.up * 0.04f;
            path.transform.rotation = Quaternion.LookRotation(new Vector3(delta.x, 0, delta.z), Vector3.up);
            path.transform.localScale = new Vector3(width, 0.08f, length);
            path.GetComponent<Renderer>().sharedMaterial = lotMaterial;
            Walkways.Add(path);
        }

        static void ConnectEntrance(GameObject building, Vector3 direction, Vector3 roadEdge, float width = 2)
        {
            var bounds = BoundsOf(building);
            Vector3 entrance = bounds.center;
            entrance.y = 0;
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.z)) entrance.x = direction.x > 0 ? bounds.max.x : bounds.min.x;
            else entrance.z = direction.z > 0 ? bounds.max.z : bounds.min.z;
            Vector3 bend = Mathf.Abs(direction.x) > Mathf.Abs(direction.z)
                ? new Vector3(roadEdge.x, 0, entrance.z)
                : new Vector3(entrance.x, 0, roadEdge.z);
            PathSegment(entrance, bend, width);
            PathSegment(bend, new Vector3(roadEdge.x, 0, roadEdge.z), width);
            EntranceRoadTargets.Add(roadEdge);
        }

        static GameObject Facility(string name, string district, Vector3 position, Vector3 entranceDirection, Vector3 roadEdge, Transform parent, float scale = 1, bool important = false)
        {
            var building = PlaceFacing(name, district, position, entranceDirection, parent, true, scale);
            ConnectEntrance(building, entranceDirection, roadEdge, important ? 2.8f : 2);
            if (important) ImportantFacilities.Add(building);
            return building;
        }

        static void BuildRoyalDistrict()
        {
            var root = District("04 Royal and Military");
            Facility("Royal_Castle", "Royal District", new Vector3(0, 0, 58), Vector3.back, new Vector3(0, 0, 32), root, 1.35f, true);
            Facility("Barracks", "Military Quarter", new Vector3(52, 0, 58), Vector3.back, new Vector3(52, 0, 48), root, 1.18f, true);
            Facility("Guardhouse", "South Gate", new Vector3(15, 0, -74), Vector3.back, new Vector3(15, 0, -80), root, 1.08f);
            Facility("Guardhouse", "South Gate", new Vector3(-15, 0, -74), Vector3.back, new Vector3(-15, 0, -80), root, 1.08f);
        }

        static void BuildCivicDistrict()
        {
            var root = District("05 Civic Culture and Faith");
            var plaza = PlaceFacing("Plaza", "Civic Centre", new Vector3(0, 0, -4), Vector3.back, root, false, 1.25f);
            var plazaBounds = BoundsOf(plaza);
            ReservedAreas.Add(Expanded(plazaBounds, 0.8f));
            PathSegment(new Vector3(0, 0, -16), new Vector3(0, 0, plazaBounds.min.z), 4);
            PathSegment(new Vector3(0, 0, plazaBounds.max.z), new Vector3(0, 0, 8), 4);
            Facility("Guildhall", "Civic Centre", new Vector3(-24, 0, 20), Vector3.back, new Vector3(-24, 0, 16), root, 1.2f, true);
            Facility("Bathhouse", "Civic Centre", new Vector3(24, 0, 20), Vector3.back, new Vector3(24, 0, 16), root, 1.2f, true);
            Facility("Library", "Civic and Culture", new Vector3(-32, 0, 39), Vector3.back, new Vector3(-32, 0, 32), root, 1.25f, true);
            Facility("Museum", "Civic and Culture", new Vector3(32, 0, 39), Vector3.back, new Vector3(32, 0, 32), root, 1.25f, true);
            Facility("Chapel", "Faith Quarter", new Vector3(-56, 0, 58), Vector3.back, new Vector3(-56, 0, 48), root, 1.15f, true);
            Facility("Observatory", "Scholars Quarter", new Vector3(-88, 0, 61), Vector3.right, new Vector3(-76, 0, 61), root, 1.25f, true);
            Facility("Arena", "Arena Quarter", new Vector3(82, 0, 58), Vector3.left, new Vector3(76, 0, 58), root, 1.3f, true);
            Facility("Clinic", "East Residential", new Vector3(56, 0, -16), Vector3.left, new Vector3(44, 0, -16), root, 1.12f);
        }

        static void BuildResidentialDistricts()
        {
            var west = District("06 Dense Residential West");
            var east = District("07 Dense Residential East");
            string[] homes = { "Fantasy_House", "MerchantHouse", "WorkshopHouse" };
            int index = 0;
            foreach (float street in new[] { -72f, -40f, 0f, 40f, 72f })
                foreach (float z in new[] { -68f, -56f, -44f, -16f, 0f, 20f, 38f })
                {
                    Transform leftRoot = street <= 0 ? west : east;
                    Transform rightRoot = street < 0 ? west : east;
                    TryResidence(homes[index++ % homes.Length], new Vector3(street - 10, 0, z), Vector3.right, new Vector3(street - 4, 0, z), leftRoot, street <= 0 ? "West Residential" : "East Residential");
                    TryResidence(homes[index++ % homes.Length], new Vector3(street + 10, 0, z), Vector3.left, new Vector3(street + 4, 0, z), rightRoot, street < 0 ? "West Residential" : "East Residential");
                }
            foreach (float x in new[] { -88f, -56f, -24f, 24f, 56f, 88f })
            {
                TryResidence(homes[index++ % homes.Length], new Vector3(x, 0, -18), Vector3.back, new Vector3(x, 0, -24), x < 0 ? west : east, x < 0 ? "West Residential" : "East Residential");
                TryResidence(homes[index++ % homes.Length], new Vector3(x, 0, 2), Vector3.forward, new Vector3(x, 0, 8), x < 0 ? west : east, x < 0 ? "West Residential" : "East Residential");
                TryResidence(homes[index++ % homes.Length], new Vector3(x, 0, 38), Vector3.back, new Vector3(x, 0, 32), x < 0 ? west : east, x < 0 ? "West Residential" : "East Residential");
            }
            foreach (float x in new[] { -76f, -44f, -12f, 12f, 44f, 76f })
            {
                TryResidence(homes[index++ % homes.Length], new Vector3(x, 0, -18), Vector3.back, new Vector3(x, 0, -24), x < 0 ? west : east, x < 0 ? "West Residential" : "East Residential");
                TryResidence(homes[index++ % homes.Length], new Vector3(x, 0, 2), Vector3.forward, new Vector3(x, 0, 8), x < 0 ? west : east, x < 0 ? "West Residential" : "East Residential");
                TryResidence(homes[index++ % homes.Length], new Vector3(x, 0, 38), Vector3.back, new Vector3(x, 0, 32), x < 0 ? west : east, x < 0 ? "West Residential" : "East Residential");
            }
        }

        static bool TryResidence(string name, Vector3 position, Vector3 entrance, Vector3 roadEdge, Transform parent, string district)
        {
            int recordIndex = Records.Count;
            var house = PlaceFacing(name, district, position, entrance, parent, true);
            var bounds = Expanded(BoundsOf(house), 0.6f);
            bool invalid = bounds.min.x <= -101 || bounds.max.x >= 101 || bounds.min.z <= -81 || bounds.max.z >= 81 ||
                           Buildings.Take(Buildings.Count - 1).Any(other => Expanded(BoundsOf(other), 0.6f).Intersects(bounds)) ||
                           ReservedAreas.Any(area => area.Intersects(bounds));
            if (invalid)
            {
                Buildings.Remove(house);
                Instances.Remove(house);
                Records.RemoveAt(recordIndex);
                UnityEngine.Object.DestroyImmediate(house);
                return false;
            }
            ConnectEntrance(house, entrance, roadEdge, 1.25f);
            return true;
        }

        static void BuildMarketAndCraftDistrict()
        {
            var market = District("08 Market Logistics and Crafts");
            PlaceFacing("Produce_Stall", "Market", new Vector3(-6, 0.14f, -4), Vector3.back, market, false);
            PlaceFacing("Cloth_Stall", "Market", new Vector3(6, 0.14f, -4), Vector3.back, market, false);
            Facility("Tavern", "West Market", new Vector3(-56, 0, -16), Vector3.right, new Vector3(-44, 0, -16), market, 1.08f);
            Facility("Bakery", "West Market", new Vector3(-24, 0, -16), Vector3.left, new Vector3(-36, 0, -16), market);
            Facility("Warehouse", "East Logistics", new Vector3(88, 0, -16), Vector3.left, new Vector3(76, 0, -16), market, 1.08f);
            Facility("Stable", "South Logistics", new Vector3(88, 0, -48), Vector3.left, new Vector3(76, 0, -48), market, 1.05f);
            Facility("Granary", "South Logistics", new Vector3(60, 0, -60), Vector3.right, new Vector3(68, 0, -60), market, 1.08f);
            Facility("Forge", "Craft Quarter", new Vector3(56, 0, -44), Vector3.left, new Vector3(44, 0, -44), market);
            Place("Anvil", "Craft Quarter", new Vector3(52, 0.14f, -42), 0, market);
            Place("Firewood_Rack", "Craft Quarter", new Vector3(59, 0.14f, -40), 90, market);
            Place("Water_Trough", "South Logistics", new Vector3(76, 0.14f, -56), 90, market);
            Place("Handcart", "Market", new Vector3(10, 0.14f, -18), 15, market);
        }

        static void BuildNightlifeDistrict()
        {
            var root = District("09 West Gate and Nightlife");
            Facility("Casino", "Nightlife", new Vector3(-88, 0, 34), Vector3.right, new Vector3(-76, 0, 34), root, 1.12f, true);
            Facility("CrimsonRowhouse", "Nightlife", new Vector3(-86, 0, -60), Vector3.right, new Vector3(-76, 0, -60), root);
            Facility("VelvetTerrace", "Nightlife", new Vector3(-86, 0, -45), Vector3.right, new Vector3(-76, 0, -45), root);
            Facility("LanternSpire", "Nightlife", new Vector3(-86, 0, -14), Vector3.right, new Vector3(-76, 0, -14), root);
            Facility("VeiledCourtyard", "Nightlife", new Vector3(-86, 0, 2), Vector3.right, new Vector3(-76, 0, 2), root);
        }

        static GameObject Block(string name, Vector3 position, Vector3 size, Material material, Transform parent)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.position = position;
            block.transform.localScale = size;
            block.GetComponent<Renderer>().sharedMaterial = material;
            return block;
        }

        static Transform ManagedArea(string name, Vector3 centre, Vector2 size, Material material, Transform parent)
        {
            var area = new GameObject(name).transform;
            area.SetParent(parent, false);
            Block(name + " Surface", new Vector3(centre.x, -0.005f, centre.z), new Vector3(size.x, 0.08f, size.y), material, area);
            managedOpenSpaceArea += size.x * size.y;
            ReservedAreas.Add(new Bounds(new Vector3(centre.x, 0, centre.z), new Vector3(size.x + 1, 2, size.y + 1)));
            return area;
        }

        static void BuildOpenSpaces()
        {
            var root = District("10 Managed Open Spaces");
            var training = ManagedArea("Barracks Training Yard", new Vector3(56, 0, 36), new Vector2(20, 8), dirtMaterial, root);
            foreach (float x in new[] { 50f, 56f, 62f })
            {
                Block("Training Post", new Vector3(x, 0.92f, 36), new Vector3(0.22f, 1.76f, 0.22f), lotMaterial, training);
                Block("Training Crossbar", new Vector3(x, 1.42f, 36), new Vector3(1.4f, 0.16f, 0.16f), lotMaterial, training);
            }
            Place("Hay_Bale", "Training Yard", new Vector3(64, 0.04f, 34), 0, training);

            var garden = ManagedArea("Northern Kitchen Gardens", new Vector3(-32, 0, 75), new Vector2(14, 8), dirtMaterial, root);
            foreach (float x in new[] { -36.5f, -33.5f, -30.5f, -27.5f })
            {
                Block("Raised Garden Bed", new Vector3(x, 0.12f, 75), new Vector3(2.3f, 0.16f, 6.2f), dirtMaterial, garden);
                Block("Vegetable Crop", new Vector3(x, 0.225f, 75), new Vector3(1.95f, 0.05f, 5.8f), gardenMaterial, garden);
            }

            var cemetery = ManagedArea("Chapel Cemetery", new Vector3(-56, 0, 75), new Vector2(16, 8), groundMaterial, root);
            foreach (float x in new[] { -61f, -57.5f, -54f, -50.5f })
                foreach (float z in new[] { 73f, 77f })
                {
                    Block("Memorial Stone", new Vector3(x, 0.55f, z), new Vector3(0.55f, 1.05f, 0.25f), lotMaterial, cemetery);
                    Block("Memorial Base", new Vector3(x, 0.11f, z), new Vector3(0.9f, 0.18f, 0.55f), lotMaterial, cemetery);
                }

            var caravan = ManagedArea("Caravan Service Yard", new Vector3(84, 0, -68), new Vector2(20, 8), dirtMaterial, root);
            Place("Handcart", "Caravan Yard", new Vector3(80, 0.04f, -68), 90, caravan);
            foreach (float x in new[] { 86f, 89f, 92f }) Place("Hay_Bale", "Caravan Yard", new Vector3(x, 0.04f, -68), 0, caravan);
        }

        static void BuildInfillDetails()
        {
            var root = District("11 Household Yards and Trees");
            string[] trees = { "NaturalTree_01", "NaturalTree_03", "NaturalTree_07", "NaturalTree_09" };
            Vector3[] candidates =
            {
                new Vector3(-94, 0, -72), new Vector3(-94, 0, -36), new Vector3(-94, 0, 18), new Vector3(-94, 0, 70),
                new Vector3(94, 0, -72), new Vector3(94, 0, -36), new Vector3(94, 0, 18), new Vector3(94, 0, 70),
                new Vector3(-20, 0, -68), new Vector3(20, 0, -68), new Vector3(-20, 0, 36), new Vector3(20, 0, 36)
            };
            for (int i = 0; i < candidates.Length; i++) TryPlaceOpenProp(trees[i % trees.Length], "Household Trees", candidates[i], i * 37, root, 3);

            Place("Barrel", "Tavern Yard", new Vector3(-51, 0.08f, -12), 0, root);
            Place("Crate_Closed", "Tavern Yard", new Vector3(-50, 0.08f, -20), 15, root);
            Place("Crate_Closed", "Warehouse Yard", new Vector3(80, 0.08f, -12), 0, root);
            Place("Barrel", "Warehouse Yard", new Vector3(81.5f, 0.08f, -12), 0, root);
            Place("Firewood_Rack", "Bakery Yard", new Vector3(-24, 0.08f, -10), 90, root);
        }

        static bool TryPlaceOpenProp(string name, string district, Vector3 position, float yaw, Transform parent, float scale)
        {
            int recordIndex = Records.Count;
            var prop = Place(name, district, position, yaw, parent, false, scale);
            var bounds = Expanded(BoundsOf(prop), 0.5f);
            bool invalid = Buildings.Any(building => Expanded(BoundsOf(building), 0.5f).Intersects(bounds)) ||
                           Roads.Any(road => BoundsOf(road).Intersects(bounds)) ||
                           ReservedAreas.Any(area => area.Intersects(bounds));
            if (!invalid) return true;
            Instances.Remove(prop);
            Records.RemoveAt(recordIndex);
            UnityEngine.Object.DestroyImmediate(prop);
            return false;
        }

        static void BuildStreetFurniture()
        {
            var root = District("12 Public Amenities");
            Place("Well", "Civic Centre", new Vector3(0, 0.14f, 5), 0, root);
            foreach (float x in new[] { -7f, 7f }) Place("Bench", "Civic Centre", new Vector3(x, 0.14f, 6), x < 0 ? 90 : 270, root);
            foreach (float x in new[] { -5f, 5f })
                foreach (float z in new[] { -20f, 9f, 25f }) Place("Streetlamp", "Main Street", new Vector3(x, 0.14f, z), 0, root);
            foreach (float z in new[] { -68f, -52f, -36f, -20f, 8f, 24f, 40f })
                foreach (float x in new[] { -76f, -68f, -44f, -36f, 36f, 44f, 68f, 76f }) Place("Streetlamp", "District Roads", new Vector3(x, 0.14f, z), 0, root);
            Place("Noticeboard", "South Gate", new Vector3(-6, 0.14f, -78), 0, root);
            Place("Signpost", "South Gate", new Vector3(6, 0.14f, -78), 0, root);
            Place("Clothesline", "West Residential", new Vector3(-50, 0.14f, -65), 0, root);
            Place("Clothesline", "East Residential", new Vector3(50, 0.14f, -65), 0, root);
            foreach (float x in new[] { -12f, 12f }) Place("Hay_Bale", "Gate Services", new Vector3(x, 0.14f, -80), 0, root);
        }

        static void ConfigureLightingAndCamera()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.54f, 0.60f, 0.68f);
            RenderSettings.ambientEquatorColor = new Color(0.39f, 0.42f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.24f, 0.22f);
            var light = GameObject.Find("Directional Light").GetComponent<Light>();
            light.transform.rotation = Quaternion.Euler(52, -32, 0);
            light.color = new Color(1, 0.94f, 0.84f);
            light.intensity = 1.45f;
            light.shadows = LightShadows.Soft;
            var camera = GameObject.Find("Main Camera").GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 116;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 400;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.27f, 0.33f, 0.37f);
            camera.transform.position = new Vector3(78, 90, -108);
            camera.transform.LookAt(new Vector3(0, 7, 0));
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        }

        static void ValidateAndWriteReport(ExportManifest manifest)
        {
            bool inside = Buildings.All(b => { var bounds = BoundsOf(b); return bounds.min.x > -101 && bounds.max.x < 101 && bounds.min.z > -81 && bounds.max.z < 81; });
            float minimumGap = float.MaxValue;
            bool overlap = false;
            float footprint = 0;
            foreach (var building in Buildings)
            {
                var bounds = BoundsOf(building);
                footprint += bounds.size.x * bounds.size.z;
            }
            for (int i = 0; i < Buildings.Count; i++)
                for (int j = i + 1; j < Buildings.Count; j++)
                {
                    var a = BoundsOf(Buildings[i]); var b = BoundsOf(Buildings[j]);
                    float dx = Mathf.Max(0, Mathf.Max(a.min.x - b.max.x, b.min.x - a.max.x));
                    float dz = Mathf.Max(0, Mathf.Max(a.min.z - b.max.z, b.min.z - a.max.z));
                    float gap = Mathf.Sqrt(dx * dx + dz * dz);
                    if (gap == 0 && a.Intersects(b)) overlap = true;
                    if (gap > 0) minimumGap = Mathf.Min(minimumGap, gap);
                }
            int[] routeZ = Enumerable.Range(0, 10).Select(i => -92 + i * 8).Concat(new[] { -20, 12, 20, 28 }).Distinct().ToArray();
            bool route = routeZ.All(z => Roads.Any(r => Vector3.Distance(new Vector3(0, 0, z), new Vector3(r.transform.position.x, 0, r.transform.position.z)) < 0.1f));
            bool targetsMeetRoads = EntranceRoadTargets.All(point => Roads.Any(road => {
                var bounds = BoundsOf(road);
                return point.x >= bounds.min.x - 0.05f && point.x <= bounds.max.x + 0.05f && point.z >= bounds.min.z - 0.05f && point.z <= bounds.max.z + 0.05f;
            }));
            bool entranceRoutes = EntranceRoadTargets.Count == Buildings.Count && targetsMeetRoads && ImportantFacilities.All(f => f.transform.localScale.x > 1);
            bool uniqueRoads = Roads.Select(road => road.transform.position.x.ToString("F3") + "|" + road.transform.position.z.ToString("F3")).Distinct().Count() == Roads.Count;
            float groundTop = GameObject.Find("Kingdom/00 Land/Kingdom Ground").GetComponent<Renderer>().bounds.max.y;
            float minimumRoadClearance = float.MaxValue;
            foreach (var road in Roads)
            {
                var collider = road.GetComponentInChildren<MeshCollider>();
                RaycastHit hit;
                if (collider.Raycast(new Ray(new Vector3(road.transform.position.x, 2, road.transform.position.z), Vector3.down), out hit, 4))
                    minimumRoadClearance = Mathf.Min(minimumRoadClearance, hit.point.y - groundTop);
            }
            bool separatedRoadSurface = minimumRoadClearance > 0.001f;
            int courtyardPads = kingdom.GetComponentsInChildren<Transform>().Count(t => t.name.EndsWith(" Courtyard"));
            var report = new CityReport {
                scene = ScenePath,
                unityVersion = Application.unityVersion,
                importedPrefabTypes = manifest.models.Length,
                townBlockPrefabTypes = Prefabs.Keys.Count(k => AssetDatabase.GetAssetPath(Prefabs[k]).StartsWith(TownBlockRoot)),
                prefabInstances = Instances.Count,
                wallModules = Records.Count(r => r.district == "Fortifications"),
                houses = Records.Count(r => r.district.Contains("Residential")),
                facilities = Buildings.Count - Records.Count(r => r.district.Contains("Residential")),
                roads = Roads.Count,
                props = Instances.Count - Roads.Count - Buildings.Count - Records.Count(r => r.district == "Fortifications"),
                entranceConnections = Walkways.Count,
                entranceTargetCount = EntranceRoadTargets.Count,
                entranceTargetsOnRoad = EntranceRoadTargets.Count(point => Roads.Any(road => {
                    var bounds = BoundsOf(road);
                    return point.x >= bounds.min.x - 0.05f && point.x <= bounds.max.x + 0.05f && point.z >= bounds.min.z - 0.05f && point.z <= bounds.max.z + 0.05f;
                })),
                importantFacilityCount = ImportantFacilities.Count,
                enlargedImportantFacilityCount = ImportantFacilities.Count(f => f.transform.localScale.x > 1),
                buildingCount = Buildings.Count,
                walledArea = 208 * 168,
                buildingFootprintArea = footprint,
                occupiedFootprintRatio = footprint / (208 * 168),
                managedOpenSpaceArea = managedOpenSpaceArea,
                developedAreaRatio = (footprint + Roads.Count * 64 + managedOpenSpaceArea) / (208 * 168),
                coplanarCourtyardPads = courtyardPads,
                allBuildingsInsideWalls = inside,
                noBuildingOverlap = !overlap,
                southernGateToCastleRoute = route,
                allFacilityEntrancesConnected = entranceRoutes,
                noDuplicateRoadTiles = uniqueRoads,
                noCoplanarTerrainRoads = separatedRoadSurface,
                minimumTerrainRoadClearance = minimumRoadClearance,
                minimumBuildingGap = minimumGap,
                placements = Records.ToArray(),
                districts = kingdom.Cast<Transform>().Select(t => t.name).ToArray(),
                limitations = new[] { "Exterior city layout only", "No NavMesh or inhabitants", "No target-device performance validation" }
            };
            File.WriteAllText(ReviewRoot + "/unity_validation.json", JsonUtility.ToJson(report, true));
            if (!inside || overlap || !route || !entranceRoutes || !uniqueRoads || !separatedRoadSurface || courtyardPads != 0) throw new InvalidOperationException("Kingdom layout validation failed; inspect unity_validation.json.");
        }
    }
}
#endif
