using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace WarSimulation.CountryComparison.Editor
{
    public static class CountryTownLayoutReview
    {
        const string Report = "Artifacts/TownLayout20261031";
        const string DensityReport = "Artifacts/TownDensity20261101";
        const string ScaleReport = "Artifacts/TownScale20261101";
        const string StreetLifeReport = "Artifacts/StreetLife20261101";
        const string CoherenceReport = "Artifacts/CityCoherence20261102";

        [Serializable] class UserView
        {
            public Vector3 position;
            public Quaternion rotation;
            public float fieldOfView, orthographicSize, aspect, nearClipPlane, farClipPlane;
            public bool orthographic;
        }

        [MenuItem("WarSim/Country/Town Layout/Capture Before")]
        public static void Before() => Capture(Report, "Before");

        [MenuItem("WarSim/Country/Town Layout/Capture After")]
        public static void After() => Capture(Report, "After");

        [MenuItem("WarSim/Country/Town Density/Capture Before")]
        public static void DensityBefore() => Capture(DensityReport, "Before");

        [MenuItem("WarSim/Country/Town Density/Capture After")]
        public static void DensityAfter() => Capture(DensityReport, "After");

        [MenuItem("WarSim/Country/Town Scale/Capture Before")]
        public static void ScaleBefore() => Capture(ScaleReport, "Before");

        [MenuItem("WarSim/Country/Town Scale/Capture After")]
        public static void ScaleAfter() => Capture(ScaleReport, "After");

        [MenuItem("WarSim/Country/Street Life/Capture Before")]
        public static void StreetLifeBefore() => Capture(StreetLifeReport, "Before");

        [MenuItem("WarSim/Country/Street Life/Capture After")]
        public static void StreetLifeAfter() => Capture(StreetLifeReport, "After");

        [MenuItem("WarSim/Country/City Coherence/Capture Before")]
        public static void CoherenceBefore() => Capture(CoherenceReport, "Before");

        [MenuItem("WarSim/Country/City Coherence/Capture After")]
        public static void CoherenceAfter() => Capture(CoherenceReport, "After");

        static void Capture(string report, string phase)
        {
            var view = Object.FindAnyObjectByType<CountryComparisonView>();
            if (EditorApplication.isPlaying || view == null || view.gameObject.scene.path != "Assets/Scenes/Country.unity")
                throw new InvalidOperationException("Open the saved Country scene in Edit mode.");
            var folder = report + "/" + phase + "/Streets";
            Directory.CreateDirectory(folder);
            UserView userView = null;
            if (report == StreetLifeReport)
            {
                var posePath = report + "/user_view.json";
                if (phase == "Before")
                {
                    var source = SceneView.lastActiveSceneView?.camera;
                    if (source == null) throw new InvalidOperationException("Open a Scene View before capturing the current street.");
                    var selected = view.viewIndex is 0 or 1 ? view.viewIndex : 1;
                    File.WriteAllText(posePath, JsonUtility.ToJson(new UserView {
                        position=source.transform.position-view.centers[selected], rotation=source.transform.rotation,
                        fieldOfView=source.fieldOfView, orthographic=source.orthographic,
                        orthographicSize=source.orthographicSize, aspect=source.aspect,
                        nearClipPlane=source.nearClipPlane, farClipPlane=source.farClipPlane
                    }, true));
                }
                userView = JsonUtility.FromJson<UserView>(File.ReadAllText(posePath));
            }
            var temp = new GameObject("Temporary town layout review") { hideFlags = HideFlags.HideAndDontSave };
            var camera = temp.AddComponent<Camera>();
            camera.CopyFrom(view.viewCamera);camera.enabled=false;camera.aspect=16f/9;camera.fieldOfView=40;
            camera.orthographic=false;
            EditorUtility.CopySerialized(view.viewCamera.GetComponent<UniversalAdditionalCameraData>(),temp.AddComponent<UniversalAdditionalCameraData>());
            var visible=view.cities.Select(c=>c.activeSelf).ToArray();
            var shots=new[] {
                ("Overview",new Vector3(0,6,-5),new Vector3(0,209,-240)),
                ("Market",new Vector3(-3,3,-30),new Vector3(28,42,-52)),
                ("Residential",new Vector3(-63,3,-48),new Vector3(23,37,-48)),
                ("Cathedral",new Vector3(-44,5,37),new Vector3(26,45,-56)),
                ("Palace",new Vector3(0,7,123),new Vector3(27,59,-71)),
                ("Fountain",new Vector3(7,3,77),new Vector3(28,36,-47)),
                ("Colleges",new Vector3(65,6,107),new Vector3(32,53,-61)),
                ("Freight",new Vector3(77,3,-37),new Vector3(24,43,-50)),
                ("River",new Vector3(-67,2,14.15f),new Vector3(18,24,-30)),
                ("Gate",new Vector3(-2,4,-83),new Vector3(28,40,-52))
            };
            if (report == ScaleReport)
                shots = shots.Concat(new[] {
                    ("Guardhouse", new Vector3(-15,4,-89.6f), new Vector3(17,18,-25)),
                    ("Arena", new Vector3(-80.4f,3,-79.3f), new Vector3(15,34,-42)),
                    ("Watchtower", new Vector3(106.2724f,4,145.6275f), new Vector3(-13,10,-17)),
                    ("MarketStalls", new Vector3(0,1,-30), new Vector3(12,24,-28)),
                    ("MarketPlan", new Vector3(0,1,-30), new Vector3(0,42,-16))
                }).ToArray();
            if (report == StreetLifeReport)
                shots = shots.Concat(new[] {
                    ("ShopLane", new Vector3(43,2,-12), new Vector3(3,11,-22)),
                    ("BackCourts", new Vector3(-14,3,96), new Vector3(22,23,-30))
                }).ToArray();
            if (report == CoherenceReport)
                shots = shots.Concat(new[] {
                    ("QuayLanding", new Vector3(35,1,2), new Vector3(9,10,-14)),
                    ("BridgeApproach", new Vector3(0,1,8), new Vector3(0,18,-17)),
                    ("Guardhouse", new Vector3(-15,2,-89.6f), new Vector3(10,12,-18)),
                    ("ArenaEntrance", new Vector3(-80,1,-94), new Vector3(10,12,-18)),
                    ("DeliveryYard", new Vector3(-62,2,145), new Vector3(10,17,-20)),
                    ("WallYards", new Vector3(80,2,144), new Vector3(-10,18,-22))
                }).ToArray();
            try
            {
                for (var set=0;set<2;set++)
                {
                    for(var i=0;i<view.cities.Length;i++)view.SetCityVisible(i,i==set);
                    camera.aspect=16f/9;camera.fieldOfView=40;camera.orthographic=false;
                    camera.nearClipPlane=view.viewCamera.nearClipPlane;camera.farClipPlane=view.viewCamera.farClipPlane;
                    foreach(var shot in shots)
                    {
                        var target=view.centers[set]+shot.Item2;
                        camera.transform.position=target+shot.Item3;camera.transform.LookAt(target);
                        CountryComparisonBuilder.CaptureCamera(camera,folder+"/"+(set==0?"Old_":"New_")+shot.Item1+".png");
                    }
                    if (report == CoherenceReport && set == 1)
                    {
                        foreach(var z in new[]{-84,-28,28,84,140})
                            foreach(var x in new[]{-84,-28,28,84})
                            {
                                var target=view.centers[set]+new Vector3(x,2,z);
                                camera.transform.position=target+new Vector3(18,52,-46);camera.transform.LookAt(target);
                                CountryComparisonBuilder.CaptureCamera(camera,folder+"/Grid_"+x+"_"+z+".png");
                            }
                        var data=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText("Assets/Data/CountryComparison/town_plan.json"));
                        camera.fieldOfView=65;camera.nearClipPlane=.08f;
                        var index=0;
                        foreach(var road in data["roads"])
                        {
                            var points=road["points"].Select(p=>new Vector3((float)p["x"],0,(float)p["z"])).ToArray();
                            var segment=Enumerable.Range(0,points.Length-1).OrderByDescending(i=>Vector3.Distance(points[i],points[i+1])).First();
                            var middle=(points[segment]+points[segment+1])/2;var forward=(points[segment+1]-points[segment]).normalized;
                            camera.transform.position=view.centers[set]+middle+Vector3.up*2.5f;
                            camera.transform.LookAt(camera.transform.position+forward*20-Vector3.up*.5f);
                            CountryComparisonBuilder.CaptureCamera(camera,folder+"/Walk_"+(index++).ToString("D2")+".png");
                        }
                    }
                    if (userView != null)
                    {
                        camera.transform.SetPositionAndRotation(view.centers[set]+userView.position, userView.rotation);
                        camera.fieldOfView=userView.fieldOfView;camera.orthographic=userView.orthographic;
                        camera.orthographicSize=userView.orthographicSize;
                        camera.nearClipPlane=userView.nearClipPlane;camera.farClipPlane=userView.farClipPlane;
                        var width = Mathf.Max(1, Mathf.RoundToInt(1080*userView.aspect));
                        var previous = camera.targetTexture;var active = RenderTexture.active;
                        var target = new RenderTexture(width,1080,24,RenderTextureFormat.ARGB32) { antiAliasing=4 };
                        var image = new Texture2D(width,1080,TextureFormat.RGB24,false);
                        try
                        {
                            camera.targetTexture=target;camera.aspect=userView.aspect;camera.Render();RenderTexture.active=target;
                            image.ReadPixels(new Rect(0,0,width,1080),0,0);image.Apply();
                            File.WriteAllBytes(folder+"/"+(set==0?"Old_":"New_")+"UserStreet.png",image.EncodeToPNG());
                        }
                        finally
                        {
                            camera.targetTexture=previous;RenderTexture.active=active;
                            Object.DestroyImmediate(image);target.Release();Object.DestroyImmediate(target);
                        }
                    }
                }
            }
            finally
            {
                for(var i=0;i<view.cities.Length;i++)view.SetCityVisible(i,visible[i]);
                Object.DestroyImmediate(temp);
            }
        }

        [MenuItem("WarSim/Country/Town Density/Save New Country View")]
        public static void SaveNewCountryView()
        {
            var view = Object.FindAnyObjectByType<CountryComparisonView>();
            if (EditorApplication.isPlaying || view == null || view.gameObject.scene.path != "Assets/Scenes/Country.unity")
                throw new InvalidOperationException("Open the saved Country scene in Edit mode.");
            view.ShowInitialView();
            SceneView.lastActiveSceneView?.LookAt(view.centers[1] + new Vector3(0, 0, 28), Quaternion.Euler(60, 0, 0), 190);
            Selection.activeGameObject = view.cities[1];
            EditorUtility.SetDirty(view);
            EditorSceneManager.SaveScene(view.gameObject.scene);
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }

        [MenuItem("WarSim/Country/Town Layout/Validate Scene Links")]
        public static void ValidateSceneLinks()
        {
            var view=Object.FindAnyObjectByType<CountryComparisonView>();
            var failures=new System.Collections.Generic.List<string>();
            var layer=Object.FindFirstObjectByType<CountryFacilityLayer>(FindObjectsInactive.Include);
            var targets=layer.Targets;
            foreach(var target in targets)
                if(target.building==null || !view.cities.Any(c=>target.building.IsChildOf(c.transform))) failures.Add("Stale facility target: "+target.name);
            foreach(var type in new[]{typeof(CountryFacilityLayer),typeof(CountryMinimapViewport),typeof(CountryGovernmentPanel)})
                foreach(var component in Object.FindObjectsByType(type,FindObjectsInactive.Include,FindObjectsSortMode.None))
                {
                    var data=new SerializedObject(component);
                    var reference=data.FindProperty("_country")??data.FindProperty("_comparison");
                    if(reference==null || reference.objectReferenceValue!=view) failures.Add("Stale comparison reference: "+type.Name);
                }
            if(view.hiddenCountryGround.Length!=2 || view.hiddenCountryGround.Any(g=>g==null))failures.Add("Missing hidden-country meadow.");
            if(targets.Count!=30) failures.Add("Expected 30 facility targets, found "+targets.Count);
            var plan = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(File.ReadAllText("Assets/Data/CountryComparison/town_plan.json"));
            var castle = plan["buildings"].First(b => (string)b["name"] == "Royal_Castle");
            if(Mathf.Abs(view.initialFocus.x-(float)castle["x"])>.01f || Mathf.Abs(view.initialFocus.z-(float)castle["z"])>.01f)
                failures.Add("Initial camera does not target the relocated palace.");
            Directory.CreateDirectory(Report);
            File.WriteAllText(Report+"/scene_links.json",JsonConvert.SerializeObject(new{status=failures.Count==0?"PASS":"FAIL",facilityTargets=targets.Count,failures},Formatting.Indented));
            if(failures.Count>0)throw new InvalidOperationException(string.Join("\n",failures));
        }
    }
}
