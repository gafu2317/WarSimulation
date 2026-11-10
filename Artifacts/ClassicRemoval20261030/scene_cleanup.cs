using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WarSimulation.CountryComparison.Editor
{
    public static class CountryClassicRemoval
    {
        const string Report = "Artifacts/ClassicRemoval20261030";
        const string MeshRoot = "Assets/Prefabs/Kingdom/Comparison/Meshes/";
        const string Classic = "Assets/Prefabs/Kingdom/CityV3Classic";

        [MenuItem("WarSim/Country/Validate Retained Countries")]
        public static void ValidateRetained()
        {
            var view=Object.FindAnyObjectByType<CountryComparisonView>(FindObjectsInactive.Include);
            if (EditorApplication.isPlaying || view==null || view.cities.Length!=2 || view.centers.Length!=2 || view.hiddenCountryGround.Length!=2)
                throw new InvalidOperationException("Expected two retained countries in Edit mode.");
            var missingMeshes=view.GetComponentsInChildren<MeshFilter>(true).Count(f=>f.sharedMesh==null);
            var missingMaterials=view.GetComponentsInChildren<MeshRenderer>(true).Count(r=>r.sharedMaterials.Any(m=>m==null));
            var missingScripts=view.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            var dependencies=AssetDatabase.GetDependencies(view.gameObject.scene.path,true).Where(p=>p.Contains("CityV3Classic")).ToArray();
            if (missingMeshes+missingMaterials+missingScripts+dependencies.Length!=0)throw new InvalidOperationException("Missing or removed scene references remain.");
            CountryRoleFacilities.Validate();
            File.WriteAllText(Report+"/scene_validation.json",JsonConvert.SerializeObject(new {status="PASS",countries=2,missingMeshes,missingMaterials,missingScripts,removedDependencies=dependencies.Length,sceneSaved=!view.gameObject.scene.isDirty},Formatting.Indented));
        }

        [MenuItem("WarSim/Country/Capture Retained Countries")]
        public static void CaptureRetained()
        {
            var view=Object.FindAnyObjectByType<CountryComparisonView>(FindObjectsInactive.Include);
            var temp=new GameObject("Temporary two-country review camera") {hideFlags=HideFlags.HideAndDontSave};
            var camera=temp.AddComponent<Camera>();camera.CopyFrom(view.viewCamera);camera.enabled=false;camera.aspect=16f/9;camera.fieldOfView=40;
            var data=temp.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            EditorUtility.CopySerialized(view.viewCamera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(),data);
            var active=view.cities.Select(c=>c.activeSelf).ToArray();
            try
            {
                for(var set=0;set<2;set++)
                {
                    for(var i=0;i<2;i++)view.SetCityVisible(i,i==set);
                    camera.transform.position=view.centers[set]+new Vector3(0,215,-245);camera.transform.LookAt(view.centers[set]+new Vector3(0,5,-3));
                    CountryComparisonBuilder.CaptureCamera(camera,Report+"/"+(set==0?"Old_":"New_")+"Overview.png");
                }
            }
            finally {for(var i=0;i<2;i++)view.SetCityVisible(i,active[i]);Object.DestroyImmediate(temp);}
        }

        [MenuItem("WarSim/Country/Remove Classic Country")]
        public static void Remove()
        {
            var view = Object.FindAnyObjectByType<CountryComparisonView>(FindObjectsInactive.Include);
            if (EditorApplication.isPlaying || view == null || view.gameObject.scene.path != "Assets/Scenes/Country.unity" || view.gameObject.scene.isDirty)
                throw new InvalidOperationException("Use the saved Country scene in Edit mode.");
            if (view.cities.Length != 3 || view.cities[2].name != "古風なファンタジーの国")
                throw new InvalidOperationException("Expected the classic third country.");
            var before = view.cities.Take(2).Select(Snapshot).ToArray();
            var index = view.viewIndex;
            var root = view.transform;
            var footprint = AssetDatabase.LoadAssetAtPath<Mesh>(MeshRoot + "Town footprint.asset");
            var meadow = root.Find("Continuous meadow").GetComponent<MeshFilter>();
            var outlines = view.cities.Take(2).Select(c => footprint.vertices.Skip(1).Select(v => c.transform.TransformPoint(v)).Select(v => new Vector2(v.x,v.z)).ToArray()).ToArray();
            var baseMeadow = AssetDatabase.LoadAssetAtPath<Mesh>(MeshRoot + "Patch_2400_00_2400_00.asset");
            Undo.RecordObject(meadow,"Restore two-country meadow");
            meadow.sharedMesh = Store(CountryPavingCutout.Subtract(baseMeadow,meadow.transform.localToWorldMatrix,outlines),"Meadow outside two countries");
            var river = root.Find("River beyond city walls/Continuous central river").GetComponent<MeshFilter>();
            Undo.RecordObject(river,"Restore two-country river");
            river.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshRoot + "Continuous_central_river__900_00__18_00.asset");
            for (var i=0;i<2;i++)
            {
                var filter=view.hiddenCountryGround[i].GetComponent<MeshFilter>();
                Undo.RecordObject(filter,"Retain hidden-country ground");
                filter.sharedMesh=Store(Object.Instantiate(filter.sharedMesh),"Hidden country meadow "+i);
            }
            Undo.RecordObject(view,"Remove third comparison country");
            Undo.DestroyObjectImmediate(view.cities[2]);
            Undo.DestroyObjectImmediate(view.hiddenCountryGround[2]);
            view.cities=view.cities.Take(2).ToArray();
            view.centers=view.centers.Take(2).ToArray();
            view.hiddenCountryGround=view.hiddenCountryGround.Take(2).ToArray();
            if (index>2) view.Show(0);
            else for(var i=0;i<2;i++)view.SetCityVisible(i,index==2 || i==index);
            EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(view.gameObject.scene);
            var remaining=AssetDatabase.GetDependencies(view.gameObject.scene.path,true).Where(p => p.StartsWith(Classic+"/") || p.StartsWith("Assets/Models/Kingdom/CityV3Classic/")).ToArray();
            if (remaining.Length>0) throw new InvalidOperationException("Remaining classic dependencies: "+string.Join(", ",remaining.Take(8)));
            var after=view.cities.Select(Snapshot).ToArray();
            if (!before.SequenceEqual(after)) throw new InvalidOperationException("Old/new country content changed.");
            var folders=new[]{Classic,"Assets/Models/Kingdom/CityV3Classic"};
            foreach(var folder in folders)
                if (!AssetDatabase.DeleteAsset(folder)) throw new InvalidOperationException("Could not delete "+folder);
            File.WriteAllText(Report+"/scene_removal.json",JsonConvert.SerializeObject(new {status="PASS",cities=view.cities.Select(c=>c.name).ToArray(),oldNewContentPreserved=true,classicDependencies=remaining,deletedFolders=folders},Formatting.Indented));
        }

        static string Snapshot(GameObject city) => JsonConvert.SerializeObject(city.GetComponentsInChildren<Transform>(true).Select(t=> {
            var filter=t.GetComponent<MeshFilter>();var renderer=t.GetComponent<MeshRenderer>();
            return new {
            path=AnimationUtility.CalculateTransformPath(t,city.transform),
            position=new[]{t.localPosition.x,t.localPosition.y,t.localPosition.z},
            rotation=new[]{t.localRotation.x,t.localRotation.y,t.localRotation.z,t.localRotation.w},
            scale=new[]{t.localScale.x,t.localScale.y,t.localScale.z},
            mesh=filter != null ? AssetDatabase.GetAssetPath(filter.sharedMesh) : "",
            materials=renderer != null ? renderer.sharedMaterials.Select(AssetDatabase.GetAssetPath).ToArray() : null
        };}));

        static Mesh Store(Mesh mesh,string name)
        {
            mesh.name=name;
            var path=MeshRoot+name+".asset";
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved==null) {AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,saved);EditorUtility.SetDirty(saved);Object.DestroyImmediate(mesh);return saved;
        }
    }
}
