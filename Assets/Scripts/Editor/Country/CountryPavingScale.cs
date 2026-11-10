using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace WarSimulation.CountryComparison.Editor
{
    public static class CountryPavingScale
    {
        public static Mesh Project(Mesh source, Matrix4x4 toCity,
            Func<int,Vector3,Vector3,Vector3,bool> select, bool separateMaterial = false, bool masonry = false)
        {
            var positions=source.vertices; var sourceUv=source.uv;
            if(masonry && sourceUv.Length!=positions.Length) return null;
            var points=positions.Select(toCity.MultiplyPoint3x4).ToArray();
            var originals=new List<int>(); var mappedUv=new List<Vector2>();
            var selected=new Dictionary<(int,float,float),int>();
            var submeshes=new List<int[]>(); var paving=new List<int>();
            for(var sub=0;sub<source.subMeshCount;sub++)
            {
                var indices=source.GetTriangles(sub); var kept=new List<int>();
                for(var i=0;i<indices.Length;i+=3)
                {
                    var a=indices[i]; var b=indices[i+1]; var c=indices[i+2];
                    if(!select(sub,points[a],points[b],points[c]))
                    {
                        kept.AddRange(new[]{a,b,c}); continue;
                    }
                    var scale=masonry?WorldUnitsPerUv(points[a],points[b],points[c],sourceUv[a],sourceUv[b],sourceUv[c])/2:Vector2.one;
                    if(scale.x<.00001f || scale.y<.00001f) { kept.AddRange(new[]{a,b,c}); continue; }
                    var target=separateMaterial?paving:kept;
                    foreach(var index in new[]{a,b,c})
                    {
                        var key=(index,masonry?scale.x:0,masonry?scale.y:0);
                        if(!selected.TryGetValue(key,out var mapped))
                        {
                            selected[key]=mapped=positions.Length+originals.Count;
                            originals.Add(index);
                            mappedUv.Add(masonry?Vector2.Scale(sourceUv[index],scale):new Vector2(points[index].x/2,points[index].z/2));
                        }
                        target.Add(mapped);
                    }
                }
                submeshes.Add(kept.ToArray());
            }
            if(originals.Count==0) return null;
            if(!separateMaterial && sourceUv.Length==positions.Length && originals.Select((i,j)=>
                Vector2.SqrMagnitude(sourceUv[i]-mappedUv[j])).All(error=>error<.00000001f)) return null;
            var mesh=UnityEngine.Object.Instantiate(source);
            if(positions.Length+originals.Count>65535) mesh.indexFormat=IndexFormat.UInt32;
            mesh.SetVertices(positions.Concat(originals.Select(i=>positions[i])).ToList());
            var normals=source.normals;
            if(normals.Length==positions.Length) mesh.SetNormals(normals.Concat(originals.Select(i=>normals[i])).ToList());
            var colors=source.colors32;
            if(colors.Length==positions.Length) mesh.SetColors(colors.Concat(originals.Select(i=>colors[i])).ToList());
            for(var channel=0;channel<8;channel++)
            {
                var uv=new List<Vector4>(); source.GetUVs(channel,uv);
                if(uv.Count!=positions.Length) continue;
                uv.AddRange(originals.Select((i,j)=>channel==0?new Vector4(mappedUv[j].x,mappedUv[j].y,0,0):uv[i]));
                mesh.SetUVs(channel,uv);
            }
            if(!source.HasVertexAttribute(VertexAttribute.TexCoord0))
                mesh.SetUVs(0,positions.Select(_=>Vector2.zero).Concat(mappedUv).ToList());
            if(separateMaterial) submeshes.Add(paving.ToArray());
            mesh.subMeshCount=submeshes.Count;
            for(var sub=0;sub<submeshes.Count;sub++) mesh.SetTriangles(submeshes[sub],sub);
            if(normals.Length!=positions.Length) mesh.RecalculateNormals();
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
            return mesh;
        }

        public static Vector2 WorldUnitsPerUv(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc)
        {
            var u=ub-ua; var v=uc-ua; var determinant=u.x*v.y-u.y*v.x;
            if(Mathf.Abs(determinant)<.0000001f) return Vector2.zero;
            return new Vector2(((b-a)*v.y-(c-a)*u.y).magnitude,((c-a)*u.x-(b-a)*v.x).magnitude)/Mathf.Abs(determinant);
        }
    }
}
