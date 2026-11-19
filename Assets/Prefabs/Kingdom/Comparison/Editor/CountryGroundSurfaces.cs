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
    public static class CountryGroundSurfaces
    {
        const string MeshRoot = "Assets/Prefabs/Kingdom/Comparison/Meshes/GroundSurfaces";

        [Serializable] class PavingReport
        {
            public string status;
            public int surfaces, upwardTriangles, checkedVertices, mismatchedVertices;
            public float maximumUvError;
            public int masonrySurfaces, masonryTriangles, masonryScaleFailures;
            public float maximumMasonryScaleError;
            public string[] failures;
        }

        [MenuItem("WarSim/Country/Validate Paving Scale")]
        public static void ValidatePavingScale()
        {
            var view=Object.FindAnyObjectByType<CountryComparisonView>();
            if(view==null) throw new InvalidOperationException("Open the Country comparison scene.");
            var report=new PavingReport(); var failures=new List<string>();
            foreach(var city in view.transform.Cast<Transform>().Where(t=>t.name is "旧モデルの国" or "新モデルの国" or "自然色モデルの国"))
                foreach(var filter in city.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer=filter.GetComponent<MeshRenderer>(); var mesh=filter.sharedMesh;
                    if(renderer==null || mesh==null) continue;
                    var slots=renderer.sharedMaterials; var matrix=city.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                    var points=mesh.vertices.Select(matrix.MultiplyPoint3x4).ToArray();
                    var uv=mesh.uv;
                    var vertices=new HashSet<int>();
                    for(var sub=0;sub<Mathf.Min(slots.Length,mesh.subMeshCount);sub++)
                    {
                        if(city.name!="旧モデルの国" && slots[sub]!=null && IsMasonry(slots[sub]))
                        {
                            var masonryIndices=mesh.GetTriangles(sub); var count=0; var before=report.masonryScaleFailures;
                            for(var i=0;i<masonryIndices.Length;i+=3)
                            {
                                var a=masonryIndices[i]; var b=masonryIndices[i+1]; var c=masonryIndices[i+2];
                                if(uv.Length!=points.Length) { report.masonryScaleFailures++; continue; }
                                var scale=CountryPavingScale.WorldUnitsPerUv(points[a],points[b],points[c],uv[a],uv[b],uv[c]);
                                if(scale.x==0 || scale.y==0) continue;
                                count++; report.masonryTriangles++;
                                var error=Mathf.Max(Mathf.Abs(scale.x-2),Mathf.Abs(scale.y-2));
                                report.maximumMasonryScaleError=Mathf.Max(report.maximumMasonryScaleError,error);
                                if(error>.02f) report.masonryScaleFailures++;
                            }
                            if(count>0) report.masonrySurfaces++;
                            if(report.masonryScaleFailures>before && failures.Count<40)
                                failures.Add(city.name+"/"+filter.name+": masonry scale requires review");
                        }
                        if(slots[sub]==null || slots[sub].name is not ("Cobble" or "Old street paving")) continue;
                        var indices=mesh.GetTriangles(sub);
                        for(var i=0;i<indices.Length;i+=3)
                        {
                            if(Vector3.Cross(points[indices[i+1]]-points[indices[i]],points[indices[i+2]]-points[indices[i]]).normalized.y<.55f) continue;
                            report.upwardTriangles++;
                            vertices.UnionWith(new[]{indices[i],indices[i+1],indices[i+2]});
                        }
                    }
                    if(vertices.Count==0) continue;
                    report.surfaces++;
                    var mismatches=0;
                    foreach(var index in vertices)
                    {
                        report.checkedVertices++;
                        var expected=new Vector2(points[index].x/2,points[index].z/2);
                        var error=index<uv.Length?Vector2.Distance(uv[index],expected):float.PositiveInfinity;
                        report.maximumUvError=Mathf.Max(report.maximumUvError,error);
                        if(error>.001f) { report.mismatchedVertices++; mismatches++; }
                    }
                    if(mismatches>0) failures.Add(city.name+"/"+filter.name+": "+mismatches+" vertices");
                }
            report.status=report.mismatchedVertices==0 && report.masonryScaleFailures==0 && report.surfaces>0?"PASS":"FAIL";
            report.failures=failures.ToArray();
            const string output="Artifacts/CityCoherence20261102";
            Directory.CreateDirectory(output);
            File.WriteAllText(output+"/paving_scale_validation.json",JsonUtility.ToJson(report,true));
            Debug.Log("Country paving scale: "+report.status+", "+report.surfaces+" surfaces, "+report.mismatchedVertices+" mismatched vertices.");
        }

        [MenuItem("WarSim/Country/Repair Ground Surface Overlaps")]
        public static void Repair()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
            var view = Object.FindAnyObjectByType<CountryComparisonView>();
            if (view == null) throw new InvalidOperationException("Open the Country comparison scene.");
            Apply(view.transform);
            Undo.FlushUndoRecordObjects();
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            EditorSceneManager.SaveScene(view.gameObject.scene);
        }

        public static void Apply(Transform root, Material packedEarth = null)
        {
            if (!AssetDatabase.IsValidFolder(MeshRoot))
                AssetDatabase.CreateFolder("Assets/Prefabs/Kingdom/Comparison/Meshes","GroundSurfaces");
            var countries = root.Cast<Transform>().Where(t=>t.name is "旧モデルの国" or "新モデルの国" or "自然色モデルの国").ToArray();
            foreach (var city in countries) CountryMeshBatchBuilder.Restore(city);
            var footprints = new List<Vector2[]>();
            foreach (var city in countries)
            {
                var label = city.name == "旧モデルの国" ? "Old" : city.name == "新モデルの国" ? "New" : "Natural";
                var streets = city.Find("Connected streets and entrance paths");
                var filters = city.GetComponentsInChildren<MeshFilter>(true);
                var earth = city.Find(label=="Old" ? "Old town earth" : "New town earth").GetComponent<MeshFilter>();
                if (CountryLayoutInstaller.IsApproved(city.gameObject))
                {
                    footprints.Add(Outline(earth));
                    continue;
                }
                var plots = streets.Cast<Transform>().Where(t=>t.name=="Paved_Plot").ToArray();
                var plotCuts = plots.Select(t=>Rectangle(CountryComparisonImporter.BoundsOf(t.gameObject))).ToList();
                var paving = filters.Where(f=>f!=earth && !PrefabUtility.IsPartOfPrefabInstance(f) && f.GetComponent<MeshRenderer>() is { } r &&
                    r.sharedMaterials.Length==1 && (r.sharedMaterial.name is "Cobble" or "Old street paving") && f.sharedMesh.bounds.size.y<.0001f).ToArray();
                var raised = paving.Where(f=>f.transform.TransformPoint(f.sharedMesh.bounds.center).y>.028f).ToArray();
                var courtCuts = raised.Select(f=>f.sharedMesh.vertices.Select(v=>f.transform.TransformPoint(v)).Select(v=>new Vector2(v.x,v.z)).ToArray()).ToArray();
                var gardenCuts = filters.Where(f=>f.GetComponent<MeshRenderer>() is { } r &&
                    r.sharedMaterial != null && r.sharedMaterial.name is "Garden soil" or "Garden lawn")
                    .Select(f=>f.sharedMesh.vertices.Select(v=>f.transform.TransformPoint(v)).Select(v=>new Vector2(v.x,v.z)).ToArray()).ToArray();
                foreach (var filter in paving)
                {
                    var cuts = new List<Vector2[]>(plotCuts);
                    if (!raised.Contains(filter))
                    {
                        cuts.AddRange(courtCuts);
                        if (filter.name.StartsWith("Connected street edge ") || filter.name.StartsWith("Street side "))
                            cuts.AddRange(gardenCuts);
                    }
                    Clip(filter,cuts,label);
                }
                if (label!="Old")
                    foreach (var tile in city.Find("Market, yards and street life/Public courts and lit trading apron").Cast<Transform>().Where(t=>t.name.StartsWith("Road_")))
                        foreach (var renderer in tile.GetComponentsInChildren<MeshRenderer>(true)) RemoveRoadSubstrate(renderer);

                footprints.Add(Outline(earth));
                if (Mathf.Approximately(earth.transform.localPosition.y, 0))
                {
                    var groundCuts = new List<Vector2[]>(gardenCuts);
                    foreach (var filter in filters.Where(f=>PrefabUtility.IsPartOfPrefabInstance(f)))
                    {
                        var renderer = filter.GetComponent<MeshRenderer>();
                        var mesh = filter.sharedMesh; var vertices = mesh.vertices;
                        for (var sub=0;sub<mesh.subMeshCount;sub++)
                        {
                            var material = renderer.sharedMaterials[sub];
                            if (material.renderQueue>=2450 || material.HasProperty("_AlphaClip") && material.GetFloat("_AlphaClip")>.5f) continue;
                            var indices = mesh.GetTriangles(sub);
                            for (var i=0;i<indices.Length;i+=3)
                            {
                                var a=filter.transform.TransformPoint(vertices[indices[i]]);
                                var b=filter.transform.TransformPoint(vertices[indices[i+1]]);
                                var c=filter.transform.TransformPoint(vertices[indices[i+2]]);
                                if (Mathf.Abs(a.y)>.0001f || Mathf.Abs(b.y)>.0001f || Mathf.Abs(c.y)>.0001f || Vector3.Cross(b-a,c-a).y<.00001f) continue;
                                groundCuts.Add(new[] {new Vector2(a.x,a.z),new Vector2(b.x,b.z),new Vector2(c.x,c.z)});
                            }
                        }
                    }
                    Clip(earth,groundCuts,label);
                }
                NormalizePaving(city,filters,label,packedEarth);
                if(label!="Old") NormalizeMasonry(city,filters,label);
            }
            Clip(root.Find("Continuous meadow").GetComponent<MeshFilter>(),footprints,"Shared");
            foreach (var city in countries) CountryMeshBatchBuilder.Bake(city);
            Debug.Log("Country ground surfaces repaired: pavement retained, covered ground faces removed.");
        }

        static bool IsMasonry(Material material) => material.name is "Fieldstone" or "Limestone" or "Carved_Limestone" or "Brick";

        static void NormalizeMasonry(Transform city, MeshFilter[] filters, string label)
        {
            foreach(var filter in filters)
            {
                if(AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith(MeshRoot+"/Masonry_",StringComparison.Ordinal)) continue;
                var renderer=filter.GetComponent<MeshRenderer>();
                if(renderer==null) continue;
                var slots=renderer.sharedMaterials;
                var allowed=Enumerable.Range(0,Mathf.Min(slots.Length,filter.sharedMesh.subMeshCount))
                    .Where(i=>slots[i]!=null && IsMasonry(slots[i])).ToHashSet();
                if(allowed.Count==0) continue;
                var mesh=CountryPavingScale.Project(filter.sharedMesh,city.worldToLocalMatrix*filter.transform.localToWorldMatrix,
                    (sub,a,b,c)=>allowed.Contains(sub),masonry:true);
                if(mesh!=null) Store(filter,mesh,label,"Masonry_");
            }
        }

        static void NormalizePaving(Transform city, MeshFilter[] filters, string label, Material packedEarth)
        {
            var streetMaterial=city.Find("Connected streets and entrance paths").GetComponentsInChildren<MeshRenderer>(true)
                .Select(r=>r.sharedMaterial).First(m=>m.name is "Cobble" or "Old street paving");
            foreach(var filter in filters)
            {
                var path=AssetDatabase.GetAssetPath(filter.sharedMesh);
                if(path.StartsWith(MeshRoot+"/Paving_",StringComparison.Ordinal)) continue;
                var renderer=filter.GetComponent<MeshRenderer>();
                if(renderer==null) continue;
                var slots=renderer.sharedMaterials;
                var allowed=Enumerable.Range(0,Mathf.Min(slots.Length,filter.sharedMesh.subMeshCount))
                    .Where(i=>slots[i]!=null && slots[i].name is "Cobble" or "Old street paving").ToHashSet();
                Transform legacy=null;
                if(label=="Old" && allowed.Count==0)
                {
                    for(var t=filter.transform;t!=city;t=t.parent)
                        if(t.name is "Paved_Plot" or "Plaza" or "Stone_Bridge" || t.name.StartsWith("Road_",StringComparison.Ordinal))
                        { legacy=t; break; }
                    if(legacy==null) continue;
                    allowed.UnionWith(Enumerable.Range(0,filter.sharedMesh.subMeshCount));
                }
                if(allowed.Count==0) continue;
                var ceiling=legacy!=null?CountryComparisonImporter.BoundsOf(legacy.gameObject).min.y+.45f:float.PositiveInfinity;
                var bridge=legacy!=null && legacy.name=="Stone_Bridge";
                var bridgeBounds=bridge?CountryComparisonImporter.BoundsOf(legacy.gameObject):default;
                var matrix=city.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                var mesh=CountryPavingScale.Project(filter.sharedMesh,matrix,(sub,a,b,c)=>
                {
                    if(!allowed.Contains(sub)) return false;
                    var normal=Vector3.Cross(b-a,c-a).normalized;
                    if(normal.y<.55f) return false;
                    if(legacy==null) return true;
                    if(!bridge) return Mathf.Max(a.y,Mathf.Max(b.y,c.y))+city.position.y<=ceiling;
                    var center=city.InverseTransformPoint(bridgeBounds.center).x;
                    return Mathf.Max(Mathf.Abs(a.x-center),Mathf.Max(Mathf.Abs(b.x-center),Mathf.Abs(c.x-center)))<bridgeBounds.extents.x*.87f;
                },legacy!=null);
                if(mesh==null) continue;
                Store(filter,mesh,label,"Paving_");
                if(legacy!=null)
                {
                    var surface = legacy.name == "Plaza" && packedEarth != null ? packedEarth : streetMaterial;
                    renderer.sharedMaterials=slots.Append(surface).ToArray();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
            }
        }

        static void RemoveRoadSubstrate(MeshRenderer renderer)
        {
            var filter=renderer.GetComponent<MeshFilter>(); var source=filter.sharedMesh;
            if (AssetDatabase.GetAssetPath(source).StartsWith(MeshRoot+"/")) return;
            var slots=renderer.sharedMaterials;
            var removed=Enumerable.Range(0,slots.Length).Where(i=>slots[i].name=="Earth" && source.GetIndexCount(i)>0).ToArray();
            if (removed.Length==0) return;
            var mesh=Object.Instantiate(source);
            foreach (var index in removed) mesh.SetTriangles(Array.Empty<int>(),index);
            Store(filter,mesh,"New");
        }

        static void Clip(MeshFilter filter, IReadOnlyList<Vector2[]> cutouts, string label)
        {
            if (cutouts.Count==0 || AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith(MeshRoot+"/")) return;
            var bounds=filter.GetComponent<MeshRenderer>().bounds;
            var rectangle=Rect.MinMaxRect(bounds.min.x,bounds.min.z,bounds.max.x,bounds.max.z);
            var relevant=cutouts.Where(p=>rectangle.Overlaps(Rect.MinMaxRect(p.Min(v=>v.x),p.Min(v=>v.y),p.Max(v=>v.x),p.Max(v=>v.y)))).ToArray();
            if (relevant.Length==0) return;
            Store(filter,CountryPavingCutout.Subtract(filter.sharedMesh,filter.transform.localToWorldMatrix,relevant),label);
        }

        static void Store(MeshFilter filter, Mesh mesh, string label, string prefix="")
        {
            var sourcePath=AssetDatabase.GetAssetPath(filter.sharedMesh);
            var local=filter.transform.position;
            var key=label+"_"+filter.name+"_"+AssetDatabase.AssetPathToGUID(sourcePath)+"_"+local.x.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+"_"+local.z.ToString("F2",System.Globalization.CultureInfo.InvariantCulture);
            var path=MeshRoot+"/"+prefix+string.Concat(key.Select(c=>char.IsLetterOrDigit(c)?c:'_'))+".asset";
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved==null) { AssetDatabase.CreateAsset(mesh,path); saved=mesh; }
            else { EditorUtility.CopySerialized(mesh,saved); EditorUtility.SetDirty(saved); Object.DestroyImmediate(mesh); }
            Undo.RecordObject(filter,"Repair country ground surfaces");
            filter.sharedMesh=saved;
            if (PrefabUtility.IsPartOfPrefabInstance(filter)) PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
        }

        static Vector2[] Rectangle(Bounds bounds) => new[] {
            new Vector2(bounds.min.x,bounds.min.z),new Vector2(bounds.max.x,bounds.min.z),
            new Vector2(bounds.max.x,bounds.max.z),new Vector2(bounds.min.x,bounds.max.z) };

        static Vector2[] Outline(MeshFilter filter)
        {
            // Repaired ground has cutouts, so it cannot supply the original meadow boundary.
            var source=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Prefabs/Kingdom/Comparison/Meshes/Town footprint.asset");
            return source.vertices.Skip(1).Select(v=>filter.transform.TransformPoint(v)).Select(v=>new Vector2(v.x,v.z)).ToArray();
        }
    }
}
