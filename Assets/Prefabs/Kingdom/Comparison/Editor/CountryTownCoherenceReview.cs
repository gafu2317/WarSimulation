using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WarSimulation.CountryComparison.Editor
{
    public static class CountryTownCoherenceReview
    {
        [MenuItem("WarSim/Country/City Coherence/Validate Structures")]
        public static void Validate()
        {
            var view=Object.FindAnyObjectByType<CountryComparisonView>();
            var failures=new List<string>();var ramps=new List<object>();var stairs=new List<object>();var arenas=new List<object>();
            foreach(var city in view.cities)
            {
                var quay=city.transform.Find("Market, yards and street life/Central river, millrace and working quay");
                foreach(var bridge in quay.Cast<Transform>().Where(t=>t.name is "Stone_Bridge" or "Wooden_Bridge"))
                {
                    var bounds=CountryComparisonImporter.BoundsOf(bridge.gameObject);
                    if(city==view.cities[1] && bridge.name=="Stone_Bridge")
                    {
                        var deck=bridge.GetComponentsInChildren<MeshFilter>().Single(f=>f.name.StartsWith("Continuous cambered bridge paving"));
                        var points=deck.sharedMesh.vertices.Select(deck.transform.TransformPoint).ToArray();var indices=deck.sharedMesh.triangles;
                        for(var i=0;i<indices.Length;i+=3)
                            if(Vector3.Cross(points[indices[i+1]]-points[indices[i]],points[indices[i+2]]-points[indices[i]]).y<=0)failures.Add(city.name+": stone bridge deck faces downwards.");
                    }
                    var landings=quay.Cast<Transform>().Single(t=>t.name==bridge.name+" landings "+Mathf.RoundToInt(city.transform.InverseTransformPoint(bounds.center).x)+",8");
                    foreach(var filter in landings.GetComponentsInChildren<MeshFilter>())
                    {
                        var vertices=filter.sharedMesh.vertices.Select(filter.transform.TransformPoint).ToArray();
                        var high=vertices.Max(p=>p.y);var edge=vertices.Where(p=>Mathf.Abs(p.y-high)<.0001f).ToArray();
                        var point=edge.Aggregate(Vector3.zero,(a,b)=>a+b)/edge.Length;
                        var deck=CountryComparisonBuilder.WalkHeight(bridge.gameObject,point.x,point.z);
                        var error=Mathf.Abs(high-deck);
                        if(error>.015f)failures.Add(city.name+" / "+bridge.name+" bank height mismatch "+error);
                        ramps.Add(new{city=city.name,bridge=bridge.name,deckHeight=deck,rampHeight=high,error});
                    }
                }
                var stair=quay.Find("Wooden_Stairs");var sb=CountryComparisonImporter.BoundsOf(stair.gameObject);
                var top=Enumerable.Range(1,100).Max(i=>CountryComparisonBuilder.WalkHeight(stair.gameObject,sb.center.x,Mathf.Lerp(sb.min.z,sb.max.z,i/100f)));
                var platform=quay.Find("Connected raised quay walkway").GetComponent<MeshRenderer>().bounds.max.y;
                if(Mathf.Abs(top-platform)>.04f)failures.Add(city.name+": quay stairs do not meet the landing.");
                stairs.Add(new{city=city.name,top,platform,error=Mathf.Abs(top-platform)});
                var arena=city.transform.Find("Buildings and courtyards/Tournament quarter/Arena");
                var ab=CountryComparisonImporter.BoundsOf(arena.gameObject);
                var origin=new Vector3(ab.center.x,1.5f,ab.min.z-.5f);
                var blocked=arena.GetComponentsInChildren<MeshFilter>().Any(f=>Hits(f,origin,Vector3.forward,ab.extents.z+.5f));
                if(blocked)failures.Add(city.name+": arena entrance is blocked at walking height.");
                arenas.Add(new{city=city.name,passageClear=!blocked});
                var yard=city.transform.Find("Market, yards and street life/Enclosed workers delivery yard");
                var upperFence=yard.Cast<Transform>().Where(t=>t.name=="Fence_Panel")
                    .Select(t=>CountryComparisonImporter.BoundsOf(t.gameObject)).Single(b=>yard.InverseTransformPoint(b.center).x>2 && yard.InverseTransformPoint(b.center).z>0);
                if(Mathf.Abs(yard.InverseTransformPoint(upperFence.min).z+1.6f)>.015f || Mathf.Abs(yard.InverseTransformPoint(upperFence.max).z-4.5f)>.015f)
                    failures.Add(city.name+": service court fence does not join its corner or gate.");
            }
            var folder="Artifacts/CityCoherence20261102/After";Directory.CreateDirectory(folder);
            File.WriteAllText(folder+"/structure_validation.json",JsonConvert.SerializeObject(new{status=failures.Count==0?"PASS":"FAIL",ramps,stairs,arenas,failures},Formatting.Indented));
            if(failures.Count>0)throw new InvalidOperationException(string.Join("\n",failures));
            Debug.Log("City structures: fitted bridge landings, connected quay steps, and open arena passages checked.");
        }

        static bool Hits(MeshFilter filter,Vector3 origin,Vector3 direction,float distance)
        {
            var vertices=filter.sharedMesh.vertices.Select(filter.transform.TransformPoint).ToArray();var indices=filter.sharedMesh.triangles;
            for(var i=0;i<indices.Length;i+=3)
            {
                var a=vertices[indices[i]];var e1=vertices[indices[i+1]]-a;var e2=vertices[indices[i+2]]-a;
                var cross=Vector3.Cross(direction,e2);var determinant=Vector3.Dot(e1,cross);
                if(Mathf.Abs(determinant)<.000001f)continue;
                var offset=origin-a;var u=Vector3.Dot(offset,cross)/determinant;if(u<0 || u>1)continue;
                var q=Vector3.Cross(offset,e1);var v=Vector3.Dot(direction,q)/determinant;if(v<0 || u+v>1)continue;
                var t=Vector3.Dot(e2,q)/determinant;if(t>=0 && t<=distance)return true;
            }
            return false;
        }
    }
}
