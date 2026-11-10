using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WarSimulation.CountryComparison.Editor
{
    public static class CountryComparisonAudit
    {
        const string ReportRoot = "Artifacts/CountryComparison";
        [Serializable] class Dimension { public string set, name; public Vector3 size; }
        [Serializable] class Dimensions { public Dimension[] models; }
        [Serializable] class Usage { public string name; public bool includedInComparison; public int oldCount, newCount; }
        [Serializable] class Report { public string status; public int catalogTypes, expectedTypes, oldTypes, newTypes; public string[] failures; public Usage[] models; }
        [Serializable] class Point { public float x,z; }
        [Serializable] class Plan { public Point[] wallOutline; public float riverZ,riverWidth; }
        [Serializable] class ContentReport { public string status; public int checkedRenderers, checkedVertices, outsidePoints; public string[] failures; }
        [Serializable] class RiverReport { public string status; public float riverZ,riverWidth; public int checkedWatergates,checkedRiverJoins,checkedMillMouths; public string[] failures; }

        [MenuItem("WarSim/Country/Write Model Dimensions")]
        public static void WriteDimensions()
        {
            var models = new List<Dimension>();
            foreach (var set in new[] { "old", "new" })
            {
                var folder = "Assets/Prefabs/Kingdom/" + (set == "old" ? "City" : "CityV2") + "/Prefabs";
                foreach (var path in Directory.GetFiles(folder, "*.prefab"))
                {
                    var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                    try { models.Add(new Dimension { set = set, name = Path.GetFileNameWithoutExtension(path), size = CountryComparisonImporter.BoundsOf(instance).size }); }
                    finally { Object.DestroyImmediate(instance); }
                }
            }
            Directory.CreateDirectory(ReportRoot);
            File.WriteAllText("Assets/Data/CountryComparison/model_dimensions.json", JsonUtility.ToJson(new Dimensions { models = models.ToArray() }, true));
        }

        [MenuItem("WarSim/Country/Audit All Model Usage")]
        public static void Audit()
        {
            Directory.CreateDirectory(ReportRoot);
            var comparison = Object.FindFirstObjectByType<CountryComparisonView>();
            var names = Directory.GetFiles("Assets/Prefabs/Kingdom/CityV2/Prefabs", "*.prefab").Select(Path.GetFileNameWithoutExtension).OrderBy(n => n).ToArray();
            var counts = new List<Dictionary<string,int>>();
            foreach (var city in comparison.cities)
            {
                var count = names.ToDictionary(n => n, _ => 0);
                foreach (var transform in city.GetComponentsInChildren<Transform>(true))
                {
                    var obj = transform.gameObject;
                    if (!PrefabUtility.IsAnyPrefabInstanceRoot(obj)) continue;
                    var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(obj);
                    if (!path.Contains("/Prefabs/Kingdom/City") || !path.Contains("/Prefabs/")) continue;
                    var name = Path.GetFileNameWithoutExtension(path);
                    if (count.ContainsKey(name)) count[name]++;
                }
                counts.Add(count);
            }
            var unused=new[]{"Road_Corner","Road_Cross","Road_End","Road_Straight","Road_T","Stone_Wall_Corner","Stone_Stairs"};
            var usage = names.Select(n => new Usage { name = n, includedInComparison = (!n.Contains("Academy_Lv") || n.EndsWith("_Lv5")) && !unused.Contains(n), oldCount = counts[0][n], newCount = counts[1][n] }).ToArray();
            var failures = usage.Where(u => u.includedInComparison ? counts.Any(c => c[u.name] == 0 || c[u.name] != u.oldCount) : counts.Any(c => c[u.name] != 0))
                .Select(u => u.name + ": old=" + u.oldCount + ", new=" + u.newCount).ToArray();
            var report = new Report { status = failures.Length == 0 ? "PASS" : "FAIL", catalogTypes = names.Length, expectedTypes = usage.Count(u=>u.includedInComparison),
                oldTypes = usage.Count(u => u.oldCount > 0), newTypes = usage.Count(u => u.newCount > 0), failures = failures, models = usage };
            File.WriteAllText(ReportRoot + "/model_usage_validation.json", JsonUtility.ToJson(report, true));
            if (failures.Length > 0) throw new InvalidOperationException(string.Join("\n", failures));
            Debug.Log(comparison.cities.Length + " countries use " + report.expectedTypes + " model types with equal counts; academies show level five only.");
        }

        [MenuItem("WarSim/Country/Validate Inside Walls")]
        public static void ValidateInsideWalls()
        {
            var comparison=Object.FindFirstObjectByType<CountryComparisonView>();
            var plan=JsonUtility.FromJson<Plan>(File.ReadAllText("Assets/Data/CountryComparison/town_plan.json"));
            var failures=new List<string>();var renderers=0;var vertices=0;var outside=0;
            foreach(var city in comparison.cities)
            {
                var wall=city.transform.Find("Complete city wall and gates");
                foreach(var renderer in city.GetComponentsInChildren<Renderer>(true))
                {
                    if(renderer.transform.IsChildOf(wall) || renderer.name is "Old town earth" or "New town earth")continue;
                    renderers++;
                    var filter=renderer.GetComponent<MeshFilter>();
                    var generated=filter!=null && filter.sharedMesh!=null && AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith("Assets/Prefabs/Kingdom/Comparison/Meshes/");
                    var bounds=renderer.bounds;
                    var points=generated?filter.sharedMesh.vertices.Select(v=>filter.transform.TransformPoint(v)):
                        new[]{new Vector3(bounds.min.x,0,bounds.min.z),new Vector3(bounds.max.x,0,bounds.min.z),new Vector3(bounds.max.x,0,bounds.max.z),new Vector3(bounds.min.x,0,bounds.max.z)};
                    foreach(var point in points)
                    {
                        vertices++;var p=city.transform.InverseTransformPoint(point);
                        var inside=true;
                        for(var i=0;i<plan.wallOutline.Length;i++)
                        {
                            var a=plan.wallOutline[i];var b=plan.wallOutline[(i+1)%plan.wallOutline.Length];
                            var dx=b.x-a.x;var dz=b.z-a.z;
                            if(dx*(p.z-a.z)-dz*(p.x-a.x)<Mathf.Sqrt(dx*dx+dz*dz)*.95f){inside=false;break;}
                        }
                        if(inside)continue;
                        outside++;
                        if(failures.Count<60)failures.Add(city.name+" / "+renderer.name+" / "+p.x.ToString("F2")+","+p.z.ToString("F2"));
                    }
                }
            }
            Directory.CreateDirectory(ReportRoot);
            var report=new ContentReport{status=outside==0?"PASS":"FAIL",checkedRenderers=renderers,checkedVertices=vertices,outsidePoints=outside,failures=failures.ToArray()};
            File.WriteAllText(ReportRoot+"/inside_walls_validation.json",JsonUtility.ToJson(report,true));
            if(outside>0)throw new InvalidOperationException(string.Join("\n",failures.Take(12)));
            Debug.Log("All buildings, paths and props lie inside the walls; the river passes through the watergates.");
        }

        [MenuItem("WarSim/Country/Validate River Connections")]
        public static void ValidateRiver()
        {
            var comparison=Object.FindFirstObjectByType<CountryComparisonView>();
            var plan=JsonUtility.FromJson<Plan>(File.ReadAllText("Assets/Data/CountryComparison/town_plan.json"));
            var failures=new List<string>();var gates=0;var joins=0;var millMouths=0;
            var exterior=comparison.transform.Find("River beyond city walls");
            var exteriorPoints=exterior.GetComponentsInChildren<MeshFilter>().SelectMany(f=>f.sharedMesh.vertices.Select(v=>f.transform.TransformPoint(v))).ToArray();
            if(exteriorPoints.Any(p=>Mathf.Abs(p.y-.21f)>.01f))failures.Add("The shared river water surface is not continuous in height.");
            foreach(var city in comparison.cities)
            {
                var walls=city.transform.Find("Complete city wall and gates");
                foreach(var side in new[]{-1,1})
                {
                    var gate=walls.Find(side<0?"Western river watergate":"Eastern river watergate");
                    if(gate==null){failures.Add(city.name+": missing watergate.");continue;}
                    if(Vector3.Distance(gate.localPosition,new Vector3(side*112,0,plan.riverZ))>.01f)failures.Add(city.name+": watergate is off the river.");
                    var arch=gate.Find("Open arch over flowing river").GetComponent<MeshFilter>();
                    if(arch.sharedMesh.vertices.Min(v=>v.y)<1.39f)failures.Add(city.name+": arch blocks the water surface.");
                    foreach(Transform pier in gate)
                        if(pier.name=="Watergate abutment" && Mathf.Abs(pier.localPosition.x)-pier.localScale.x/2<=plan.riverWidth/2)failures.Add(city.name+": abutment blocks the river.");
                    var opening=city.transform.TransformPoint(new Vector3(side*112,.21f,plan.riverZ));
                    foreach(var renderer in walls.GetComponentsInChildren<Renderer>(true))
                        if(renderer.name=="Granite_Straight" && renderer.bounds.Contains(opening))failures.Add(city.name+": wall crosses the river.");
                    foreach(var bank in new[]{-1,1})
                    {
                        var edge=opening+Vector3.forward*bank*plan.riverWidth/2;
                        if(!exteriorPoints.Any(p=>Vector3.Distance(p,edge)<.03f))failures.Add(city.name+": exterior river is disconnected.");
                        joins++;
                    }
                    gates++;
                }
                var mill=city.transform.Find("Buildings and courtyards/Mill quay/Watermill");
                var modern=city!=comparison.cities[0];
                var millCenter=city.transform.InverseTransformPoint(CountryComparisonImporter.BoundsOf(mill.gameObject).center);
                var wheelContact=mill.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="WaterWheelContact");
                var channelZ=wheelContact!=null?city.transform.InverseTransformPoint(wheelContact.position).z:millCenter.z-((modern?4.8f:4.9f)-(modern?1.335f:1.4975f))*mill.localScale.x;
                if(Vector3.Dot(mill.forward,Vector3.right)<.999f)failures.Add(city.name+": the waterwheel channel does not follow the river current.");
                if(channelZ<plan.riverZ-plan.riverWidth/2+.2f || channelZ>plan.riverZ+plan.riverWidth/2-.2f)failures.Add(city.name+": the waterwheel is outside the flowing river.");
                millMouths+=2;
            }
            var report=new RiverReport{status=failures.Count==0?"PASS":"FAIL",riverZ=plan.riverZ,riverWidth=plan.riverWidth,checkedWatergates=gates,checkedRiverJoins=joins,checkedMillMouths=millMouths,failures=failures.ToArray()};
            File.WriteAllText(ReportRoot+"/river_validation.json",JsonUtility.ToJson(report,true));
            if(failures.Count>0)throw new InvalidOperationException(string.Join("\n",failures));
            Debug.Log("The central river passes through " + gates + " open watergates and joins the exterior reaches.");
        }
    }
}
