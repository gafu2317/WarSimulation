using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace WarSimulation.CountryComparison.Editor
{
    public static class CountryRoleFacilities
    {
        const string Report = "Artifacts/RoleFacilityRebuild20261024";
        const string Design = "Tools/Blender/RoleFacilities/design.json";
        [Serializable] class Catalog { public Spec[] assets; }
        [Serializable] class Spec { public string name, label, identity; public bool placed, preserveIdentity; public Plot plot; }
        [Serializable] class Plot { public float x, z, width, depth, yaw; }
        [Serializable] class Generated { public Entry[] assets; }
        [Serializable] class Entry { public string style, name, staged, fbx; public string[] materials, features, landmarks; public float[] min, max; }

        static CountryComparisonView View()
        {
            var view = Object.FindAnyObjectByType<CountryComparisonView>();
            if (EditorApplication.isPlaying || view == null || view.gameObject.scene.path != "Assets/Scenes/Country.unity")
                throw new InvalidOperationException("Open this project's Country scene in Edit mode.");
            if (view.cities.Length != 2) throw new InvalidOperationException("Expected the old/new two-country comparison.");
            return view;
        }

        static Spec[] Specs() => JsonUtility.FromJson<Catalog>(File.ReadAllText(Design)).assets;

        static Entry[] Entries() => JsonUtility.FromJson<Generated>(File.ReadAllText(Report + "/generation_CityV2.json")).assets;

        public static void RemoveHousePlacards(GameObject instance, string model)
        {
            if (model is not ("MerchantHouse" or "WorkshopHouse")) return;
            foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                var materials=renderer.sharedMaterials;
                if (!materials.Any(IsPlacard)) continue;
                Undo.RecordObject(renderer,"Remove oversized house placard");
                if (materials.All(IsPlacard)) renderer.enabled=false;
                else
                {
                    for (var i=0;i<materials.Length;i++) if (IsPlacard(materials[i])) materials[i]=InvisiblePlacard();
                    renderer.sharedMaterials=materials;
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
        }

        static bool IsPlacard(Material material) => material != null && material.name.StartsWith("Sign_",StringComparison.Ordinal);

        static Material InvisiblePlacard()
        {
            const string path="Assets/Prefabs/Kingdom/Comparison/Materials/Removed house placard.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material=new Material(Shader.Find("Universal Render Pipeline/Lit")) { name="Removed house placard", renderQueue=3000 };
            material.SetColor("_BaseColor",new Color(0,0,0,0));material.SetFloat("_Surface",1);material.SetFloat("_ZWrite",0);
            material.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.SetOverrideTag("RenderType","Transparent");
            material.SetShaderPassEnabled("ShadowCaster",false);material.SetShaderPassEnabled("DepthOnly",false);material.SetShaderPassEnabled("DepthNormals",false);
            AssetDatabase.CreateAsset(material,path);return material;
        }

        [MenuItem("WarSim/Country/Role Facilities/Finalize Appearance")]
        public static void FinalizeAppearance()
        {
            var view=View();
            foreach (var t in view.cities[1].GetComponentsInChildren<Transform>(true).Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)))
                RemoveHousePlacards(t.gameObject,t.name);
            var signs=view.cities[1].GetComponentsInChildren<MeshRenderer>(true).Where(r => r.enabled && r.sharedMaterials.Any(IsPlacard))
                .Select(r => new { root=PrefabUtility.GetNearestPrefabInstanceRoot(r.gameObject)?.name, renderer=r.name, materials=r.sharedMaterials.Select(m=>m?.name).ToArray() }).ToArray();
            File.WriteAllText(Report+"/remaining_signage.json",JsonConvert.SerializeObject(signs,Formatting.Indented));
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(view.gameObject.scene);EditorSceneManager.SaveScene(view.gameObject.scene);
        }

        [MenuItem("WarSim/Country/Role Facilities/Capture Before")]
        public static void CaptureBefore() => Capture("Before", false);

        [MenuItem("WarSim/Country/Role Facilities/Capture After")]
        public static void CaptureAfter() => Capture("After", true);

        [MenuItem("WarSim/Country/Role Facilities/Install Rebuilt Facilities")]
        public static void Install()
        {
            if (EditorApplication.isCompiling) throw new InvalidOperationException("Wait for script compilation before importing models.");
            EditorApplication.LockReloadAssemblies();
            AssetDatabase.DisallowAutoRefresh();
            try { InstallModels(); }
            finally
            {
                AssetDatabase.AllowAutoRefresh();
                EditorApplication.UnlockReloadAssemblies();
            }
        }

        static void InstallModels()
        {
            var view = View();
            CityV2MaterialQuality.Apply();
            var specs = Specs().ToDictionary(s => s.name);
            var entries = Entries();
            if (entries.Length != 40 || entries.Any(e => !File.Exists(e.staged))) throw new InvalidOperationException("All forty generated models must be ready before installation.");
            var existing = new List<(int city, GameObject root, Transform parent, Vector3 center, Vector3 size, Quaternion rotation)>();
            for (var set = 1; set < 2; set++)
                foreach (var t in view.cities[set].GetComponentsInChildren<Transform>(true).Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && specs.ContainsKey(t.name)))
                {
                    var bounds = CountryComparisonImporter.BoundsOf(t.gameObject);
                    existing.Add((set, t.gameObject, t.parent, new Vector3(bounds.center.x,bounds.min.y,bounds.center.z), bounds.size, t.rotation));
                }
            File.WriteAllText(Report + "/replacement_targets.json", JsonConvert.SerializeObject(existing.Select(e => new {
                city=e.city, name=e.root.name, center=new[] { e.center.x,e.center.y,e.center.z }, size=new[] { e.size.x,e.size.y,e.size.z }
            }), Formatting.Indented));
            foreach (var entry in entries)
            {
                File.Copy(entry.staged, entry.fbx, true);
                AssetDatabase.ImportAsset(entry.fbx, ImportAssetOptions.ForceSynchronousImport);
                var importer = (ModelImporter)AssetImporter.GetAtPath(entry.fbx);
                importer.importAnimation = false; importer.importBlendShapes = false;
                importer.bakeAxisConversion = true; importer.globalScale = 1; importer.useFileScale = true;
                importer.importNormals = ModelImporterNormals.Import; importer.importTangents = ModelImporterTangents.CalculateMikk;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.SaveAndReimport();
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(entry.fbx);
                var root = new GameObject(entry.name);
                try
                {
                    var geometry = Object.Instantiate(source,root.transform); geometry.name = "Purpose-built facility geometry";
                    foreach (var marker in geometry.GetComponentsInChildren<Transform>(true).Where(t => t.name.EndsWith("_Entrance",StringComparison.Ordinal))) marker.name="Entrance";
                    foreach (var renderer in geometry.GetComponentsInChildren<MeshRenderer>(true))
                        renderer.sharedMaterials = renderer.sharedMaterials.Select(m => AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefabs/Kingdom/" + entry.style + "/Materials/" + m.name + ".mat")
                            ?? throw new InvalidOperationException("Missing material: " + m.name)).ToArray();
                    var bounds = CountryComparisonImporter.BoundsOf(root);
                    geometry.transform.localPosition -= new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
                    PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefabs/Kingdom/" + entry.style + "/Prefabs/" + entry.name + ".prefab");
                }
                finally { Object.DestroyImmediate(root); }
            }
            foreach (var style in new[] { "CityV2" })
            {
                var path = "Assets/Models/Kingdom/" + style + "/export_manifest.json";
                var manifest = JObject.Parse(File.ReadAllText(path));
                foreach (var entry in entries.Where(e => e.style == style))
                {
                    var target = manifest["assets"].OfType<JObject>().Single(e => (string)e["name"] == entry.name);
                    var detailed = JObject.Parse(File.ReadAllText(Report + "/generation_" + style + ".json"))["assets"].OfType<JObject>().Single(e => (string)e["name"] == entry.name);
                    foreach (var property in detailed.Properties()) target[property.Name] = property.Value.DeepClone();
                    target["role_identity"] = string.Join("; ",entry.features);
                    var model=AssetDatabase.LoadAssetAtPath<GameObject>(entry.fbx);
                    var meshes=model.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh).ToArray();
                    target["triangles"]=meshes.Sum(m => Enumerable.Range(0,m.subMeshCount).Sum(i => (long)m.GetIndexCount(i)/3));
                    target["mesh_parts"]=meshes.Length;
                    target["minimum"]=new JArray(entry.min);target["maximum"]=new JArray(entry.max);
                    target.Remove("top");
                }
                File.WriteAllText(path,manifest.ToString(Formatting.Indented));
                AssetDatabase.ImportAsset(path);
            }
            CountryRoofPalette.BeginBuild();
            foreach (var old in existing)
            {
                var name = old.root.name; var spec = specs[name];
                const string style = "CityV2";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Kingdom/" + style + "/Prefabs/" + name + ".prefab");
                var replacement = (GameObject)PrefabUtility.InstantiatePrefab(prefab,old.parent);
                replacement.name = name;
                replacement.transform.localPosition = Vector3.zero;
                replacement.transform.localRotation = Quaternion.identity;
                var bounds = CountryComparisonImporter.BoundsOf(replacement);
                replacement.transform.localScale = Vector3.one * Mathf.Min(spec.plot.width/bounds.size.x,spec.plot.depth/bounds.size.z);
                replacement.transform.rotation = old.rotation;
                bounds = CountryComparisonImporter.BoundsOf(replacement);
                replacement.transform.position += old.center - new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
                PrefabUtility.RecordPrefabInstancePropertyModifications(replacement.transform);
                if (old.city == 1) CountryRoofPalette.Apply(replacement,name,spec.plot.x,spec.plot.z);
                Undo.RegisterCreatedObjectUndo(replacement,"Rebuild role facility architecture");
                Undo.DestroyObjectImmediate(old.root);
            }
            foreach (var t in view.cities[1].GetComponentsInChildren<Transform>(true).Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)))
                RemoveHousePlacards(t.gameObject,t.name);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            EditorSceneManager.SaveScene(view.gameObject.scene);
            File.WriteAllText(Report + "/installed.json",JsonConvert.SerializeObject(new { status="PASS", assets=entries.Length, sceneInstances=existing.Count, utc=DateTime.UtcNow },Formatting.Indented));
            Validate();
        }

        [MenuItem("WarSim/Country/Role Facilities/Validate Rebuilt Facilities")]
        public static void Validate()
        {
            var view = View(); var entries = Entries(); var failures = new List<string>();
            var specs = Specs().ToDictionary(s => s.name);
            foreach (var entry in entries)
            {
                var path = "Assets/Prefabs/Kingdom/" + entry.style + "/Prefabs/" + entry.name + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) { failures.Add("Missing prefab: " + path); continue; }
                if (PrefabUtility.GetPrefabAssetType(prefab) != PrefabAssetType.Regular) failures.Add("Not a regular purpose-built prefab: " + entry.name);
                if (!specs[entry.name].preserveIdentity && !string.IsNullOrEmpty(specs[entry.name].identity) && (entry.landmarks == null || entry.landmarks.Length == 0))
                    failures.Add("Missing facility landmark: " + entry.name);
                foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (renderer.sharedMaterials.Any(m => m == null || ShaderUtil.ShaderHasError(m.shader))) failures.Add("Material/shader: " + entry.name);
                    // The retired atlas put the same rectangular placard on every facility.
                    if (renderer.sharedMaterials.Where(m => m != null).Any(IsPlacard)) failures.Add("Legacy role sign atlas: " + entry.name);
                }
                foreach (var f in prefab.GetComponentsInChildren<MeshFilter>(true))
                    if (f.sharedMesh == null || AssetDatabase.GetAssetPath(f.sharedMesh) != entry.fbx) failures.Add("Foreign or missing mesh: " + entry.name);
                var bounds = CountryComparisonImporter.BoundsOf(prefab);
                var expected = new Vector3(entry.max[0]-entry.min[0],entry.max[1]-entry.min[1],entry.max[2]-entry.min[2]);
                if (Vector3.Distance(bounds.size,expected) > .04f) failures.Add("Wrong axes/scale: " + entry.name);
                var entrance = prefab.GetComponentsInChildren<Transform>(true).SingleOrDefault(t => t.name == "Entrance");
                if (entrance == null || prefab.transform.InverseTransformPoint(entrance.position).z >= 0) failures.Add("Front entrance missing: " + entry.name);
            }
            var names = Specs().Where(s => s.placed).Select(s => s.name).ToArray();
            for (var set = 1; set < 2; set++)
            {
                var placed = view.cities[set].GetComponentsInChildren<Transform>(true).Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && names.Contains(t.name)).ToArray();
                foreach (var name in names)
                    if (placed.Count(t => t.name == name) != 1) failures.Add("Expected one facility: " + set + "/" + name);
                foreach (var t in placed)
                    if (Vector3.Dot(t.forward,Vector3.forward) < .999f) failures.Add("Facility faces away: " + set + "/" + t.name);
            }
            if (failures.Count==0)
                try
                {
                    CountryComparisonBuilder.ValidateSaved();
                    CountryRoofPalette.Validate();
                    CountryComparisonAudit.Audit(); CountryComparisonAudit.ValidateInsideWalls(); CountryComparisonAudit.ValidateRiver();
                }
                catch (Exception error) { failures.Add("Scene integration: " + error.Message); }
            File.WriteAllText(Report + "/validation.json",JsonConvert.SerializeObject(new { status=failures.Count==0?"PASS":"FAIL", rebuiltAssets=entries.Length, placedFacilityTypes=names.Length,
                landmarkAssets=entries.Count(e => e.landmarks != null && e.landmarks.Length > 0),
                legacyRoleSigns=failures.Count(f => f.StartsWith("Legacy role sign atlas:")), failures },Formatting.Indented));
            if (failures.Count > 0) throw new InvalidOperationException(string.Join("\n",failures.Take(12)));
        }

        static void Capture(string phase, bool frontViews)
        {
            var view = View(); var specs = Specs().Where(s => s.placed).ToArray();
            var folder = Report + "/" + phase + "/Captures"; Directory.CreateDirectory(folder);
            var temp = new GameObject("Temporary role architecture review camera") { hideFlags=HideFlags.HideAndDontSave };
            var camera=temp.AddComponent<Camera>();camera.CopyFrom(view.viewCamera);camera.enabled=false;camera.aspect=16f/9;camera.fieldOfView=40;
            var data=temp.AddComponent<UniversalAdditionalCameraData>();EditorUtility.CopySerialized(view.viewCamera.GetComponent<UniversalAdditionalCameraData>(),data);
            var active=view.cities.Select(c => c.activeSelf).ToArray();
            try
            {
                for (var set=1;set<2;set++)
                {
                    for (var i=0;i<view.cities.Length;i++) view.SetCityVisible(i,i==set);
                    var center=view.centers[set];const string prefix="New_";
                    camera.transform.position=center+new Vector3(0,215,-245);camera.transform.LookAt(center+new Vector3(0,5,-3));
                    CountryComparisonBuilder.CaptureCamera(camera,folder+"/"+prefix+"Overview.png");
                    foreach (var spec in specs)
                    {
                        var size=Mathf.Max(spec.plot.width,spec.plot.depth);
                        var target=center+new Vector3(spec.plot.x,Mathf.Max(2,size*.22f),spec.plot.z);
                        camera.transform.position=target+new Vector3(.78f,.72f,-1.25f)*size*1.65f;camera.transform.LookAt(target);
                        CountryComparisonBuilder.CaptureCamera(camera,folder+"/"+prefix+spec.name+".png");
                        if (!frontViews || !new[] { "Library","Church","Blacksmith","Clinic","ArcaneAcademy_Lv5","WarriorAcademy_Lv5","SpiritAcademy_Lv5","Tavern","Observatory" }.Contains(spec.name))continue;
                        camera.transform.position=target+new Vector3(.13f,.65f,-1.4f)*size;camera.transform.LookAt(target);
                        var subject=view.cities[set].GetComponentsInChildren<Transform>(true).First(t => t.name==spec.name && PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject));
                        var position=camera.transform.position;
                        foreach (var renderer in view.cities[set].GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && !r.transform.IsChildOf(subject)))
                        {
                            var bounds=renderer.bounds;
                            if (position.x>=bounds.min.x && position.x<=bounds.max.x && position.z>=bounds.min.z && position.z<=bounds.max.z)
                                position.y=Mathf.Max(position.y,bounds.max.y+1);
                        }
                        camera.transform.position=position;camera.transform.LookAt(target);
                        CountryComparisonBuilder.CaptureCamera(camera,folder+"/"+prefix+spec.name+"_Front.png");
                    }
                }
            }
            finally
            {
                for (var i=0;i<view.cities.Length;i++)view.SetCityVisible(i,active[i]);
                Object.DestroyImmediate(temp);
            }
            File.WriteAllText(Report+"/capture_"+phase.ToLowerInvariant()+".json",JsonConvert.SerializeObject(new { status="PASS", phase, images=Directory.GetFiles(folder,"New_*.png").Length },Formatting.Indented));
        }
    }
}
