using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WarSimulation.CountryComparison.Editor;

public sealed class CountryPavingCutoutTests
{
    static Vector2[] Square(float x, float z, float half) => new[] {
        new Vector2(x-half,z-half),new Vector2(x+half,z-half),new Vector2(x+half,z+half),new Vector2(x-half,z+half) };

    static Mesh Paving()
    {
        var mesh=new Mesh();
        mesh.vertices=new[] {new Vector3(-2,.025f,-2),new Vector3(-2,.025f,2),new Vector3(2,.025f,2),new Vector3(2,.025f,-2)};
        mesh.uv=mesh.vertices.Select(v=>new Vector2(v.x/2,v.z/2)).ToArray();
        mesh.triangles=new[] {0,1,2,0,2,3}; mesh.RecalculateNormals();
        return mesh;
    }

    [Test]
    public void CentralCutout_LeavesRingWithoutCoveringThePlot()
    {
        var source=Paving(); var result=CountryPavingCutout.Subtract(source,Matrix4x4.identity,new[] {Square(0,0,1)});
        try
        {
            Assert.That(Area(result),Is.EqualTo(12).Within(.0001));
            var vertices=result.vertices; var indices=result.triangles;
            for(var i=0;i<indices.Length;i+=3)
            {
                var center=(vertices[indices[i]]+vertices[indices[i+1]]+vertices[indices[i+2]])/3;
                Assert.That(Mathf.Abs(center.x)>=.9999f || Mathf.Abs(center.z)>=.9999f,Is.True);
            }
            Assert.That(source.vertexCount,Is.EqualTo(4));
            Assert.That(source.triangles.Length,Is.EqualTo(6));
        }
        finally { Object.DestroyImmediate(source); Object.DestroyImmediate(result); }
    }

    [Test]
    public void WorldSpaceCutout_KeepsStreetHeightAndTextureCoordinates()
    {
        var source=Paving();
        var matrix=Matrix4x4.TRS(new Vector3(154,0,-106),Quaternion.identity,Vector3.one);
        var result=CountryPavingCutout.Subtract(source,matrix,new[] {Square(154,-106,1)});
        try
        {
            Assert.That(Area(result),Is.EqualTo(12).Within(.001));
            for(var i=0;i<result.vertexCount;i++)
            {
                var vertex=result.vertices[i];
                Assert.That(vertex.y,Is.EqualTo(.025f).Within(.00001));
                Assert.That(result.uv[i].x,Is.EqualTo(vertex.x/2).Within(.0001));
                Assert.That(result.uv[i].y,Is.EqualTo(vertex.z/2).Within(.0001));
            }
            Assert.That(result.normals.All(n=>n.y>.999f),Is.True);
        }
        finally { Object.DestroyImmediate(source); Object.DestroyImmediate(result); }
    }

    [Test]
    public void OverlappingCutouts_RemoveTheirUnionOnce()
    {
        var source=Paving();
        var result=CountryPavingCutout.Subtract(source,Matrix4x4.identity,new[] {Square(-.5f,0,1),Square(.5f,0,1)});
        try { Assert.That(Area(result),Is.EqualTo(10).Within(.0001)); }
        finally { Object.DestroyImmediate(source); Object.DestroyImmediate(result); }
    }

    [Test]
    public void RepeatedCutout_PreservesAreaAndGeometry()
    {
        var source=Paving(); var cutouts=new[] {Square(0,0,1)};
        var once=CountryPavingCutout.Subtract(source,Matrix4x4.identity,cutouts);
        var twice=CountryPavingCutout.Subtract(once,Matrix4x4.identity,cutouts);
        try
        {
            Assert.That(Area(twice),Is.EqualTo(Area(once)).Within(.0001));
            Assert.That(twice.vertexCount,Is.EqualTo(once.vertexCount));
        }
        finally { Object.DestroyImmediate(source); Object.DestroyImmediate(once); Object.DestroyImmediate(twice); }
    }

    [Test]
    public void DisjointCutout_LeavesTheWholeStreet()
    {
        var source=Paving(); var result=CountryPavingCutout.Subtract(source,Matrix4x4.identity,new[] {Square(10,10,1)});
        try { Assert.That(Area(result),Is.EqualTo(16).Within(.0001)); }
        finally { Object.DestroyImmediate(source); Object.DestroyImmediate(result); }
    }

    [Test]
    public void EnlargedRotatedCourtyard_UsesTheStreetStoneScale()
    {
        var source=Paving();
        var matrix=Matrix4x4.TRS(new Vector3(28,0,45),Quaternion.Euler(0,90,0),new Vector3(2.4f,1,1.7f));
        var result=CountryPavingScale.Project(source,matrix,(_,a,b,c)=>true);
        try
        {
            foreach(var index in result.triangles.Distinct())
            {
                var point=matrix.MultiplyPoint3x4(result.vertices[index]);
                Assert.That(result.uv[index].x,Is.EqualTo(point.x/2).Within(.00001));
                Assert.That(result.uv[index].y,Is.EqualTo(point.z/2).Within(.00001));
            }
            Assert.That(source.vertexCount,Is.EqualTo(4));
            Assert.That(source.uv[0],Is.EqualTo(new Vector2(-1,-1)));
            Assert.That(CountryPavingScale.Project(result,matrix,(_,a,b,c)=>true),Is.Null);
        }
        finally { Object.DestroyImmediate(source); Object.DestroyImmediate(result); }
    }

    [Test]
    public void AtlasFloorProjection_PreservesTheWallMaterialAndUvs()
    {
        var source=Paving();
        source.subMeshCount=2;
        source.SetTriangles(new[]{0,1,2},0);
        source.SetTriangles(new[]{0,2,3},1);
        var matrix=Matrix4x4.Translate(new Vector3(37,0,50));
        var result=CountryPavingScale.Project(source,matrix,(sub,a,b,c)=>sub==0,true);
        try
        {
            Assert.That(result.subMeshCount,Is.EqualTo(3));
            Assert.That(result.GetTriangles(0),Is.Empty);
            Assert.That(result.GetTriangles(1),Is.EqualTo(source.GetTriangles(1)));
            foreach(var index in result.GetTriangles(1)) Assert.That(result.uv[index],Is.EqualTo(source.uv[index]));
            Assert.That(result.GetTriangles(2).All(i=>i>=source.vertexCount),Is.True);
            foreach(var index in result.GetTriangles(2))
            {
                var point=matrix.MultiplyPoint3x4(result.vertices[index]);
                Assert.That(result.uv[index],Is.EqualTo(new Vector2(point.x/2,point.z/2)));
            }
        }
        finally { Object.DestroyImmediate(source); Object.DestroyImmediate(result); }
    }

    [Test]
    public void SlopedBridgeProjection_KeepsTheDeckShapeAndMatchesTheBank()
    {
        var source=Paving(); var vertices=source.vertices;
        vertices[1].y=.4f; vertices[2].y=.4f; source.vertices=vertices; source.RecalculateNormals();source.RecalculateBounds();
        var matrix=Matrix4x4.TRS(new Vector3(62,0,8),Quaternion.identity,new Vector3(1.4f,2,2.5f));
        var result=CountryPavingScale.Project(source,matrix,(_,a,b,c)=>true);
        try
        {
            foreach(var index in result.triangles.Distinct())
            {
                var vertex=result.vertices[index]; var point=matrix.MultiplyPoint3x4(vertex);
                Assert.That(vertex.y,Is.EqualTo(vertex.z>0?.4f:.025f));
                Assert.That(result.uv[index],Is.EqualTo(new Vector2(point.x/2,point.z/2)));
            }
            Assert.That(result.bounds,Is.EqualTo(source.bounds));
        }
        finally { Object.DestroyImmediate(source); Object.DestroyImmediate(result); }
    }

    [Test]
    public void ScaledMasonry_KeepsItsUvAxesAndTwoMetresPerTile()
    {
        var source=Paving();
        source.uv=source.vertices.Select(v=>new Vector2(v.x/2.4f+.3f,v.z/2.4f+.8f)).ToArray();
        var matrix=Matrix4x4.TRS(new Vector3(28,3,45),Quaternion.Euler(90,35,0),new Vector3(1.6f,1,2.4f));
        var result=CountryPavingScale.Project(source,matrix,(_,a,b,c)=>true,masonry:true);
        try
        {
            var indices=result.GetTriangles(0);
            var a=indices[0]; var b=indices[1]; var c=indices[2];
            Assert.That(result.uv[a].x,Is.EqualTo(result.uv[b].x).Within(.00001));
            Assert.That(result.uv[b].y-result.uv[a].y,Is.EqualTo(4.8f).Within(.00001));
            Assert.That(result.uv[c].x-result.uv[b].x,Is.EqualTo(3.2f).Within(.00001));
            Assert.That(result.bounds,Is.EqualTo(source.bounds));
            Assert.That(source.uv[0].x,Is.EqualTo(-2/2.4f+.3f).Within(.00001));
        }
        finally { Object.DestroyImmediate(source); Object.DestroyImmediate(result); }
    }

    [Test]
    public void SmallBevelsWithHighUvOffsets_KeepTheirOwnMasonryScale()
    {
        var source=new Mesh();
        source.vertices=new[]{Vector3.zero,new Vector3(.01f,0,0),new Vector3(0,.01f,0),
            new Vector3(-.010002f,0,0),new Vector3(0,-.01f,0)};
        source.uv=new[]{new Vector2(100,80),new Vector2(100.01f,80),new Vector2(100,80.01f),
            new Vector2(99.99f,80),new Vector2(100,79.99f)};
        source.triangles=new[]{0,1,2,0,3,4}; source.RecalculateNormals(); source.RecalculateBounds();
        var matrix=Matrix4x4.TRS(new Vector3(28,3,45),Quaternion.Euler(0,35,0),new Vector3(2.4f,1.7f,1));
        var result=CountryPavingScale.Project(source,matrix,(_,a,b,c)=>true,masonry:true);
        try
        {
            var indices=result.triangles; var vertices=result.vertices; var uv=result.uv;
            for(var i=0;i<indices.Length;i+=3)
            {
                var a=indices[i]; var b=indices[i+1]; var c=indices[i+2];
                Assert.That(uv[a].y,Is.EqualTo(uv[b].y).Within(.00001));
                Assert.That(uv[a].x,Is.EqualTo(uv[c].x).Within(.00001));
                var width=Vector3.Distance(matrix.MultiplyPoint3x4(vertices[a]),matrix.MultiplyPoint3x4(vertices[b]));
                var height=Vector3.Distance(matrix.MultiplyPoint3x4(vertices[a]),matrix.MultiplyPoint3x4(vertices[c]));
                Assert.That(width/Mathf.Abs(uv[b].x-uv[a].x),Is.EqualTo(2).Within(.02));
                Assert.That(height/Mathf.Abs(uv[c].y-uv[a].y),Is.EqualTo(2).Within(.02));
            }
            Assert.That(source.uv[0],Is.EqualTo(new Vector2(100,80)));
        }
        finally { Object.DestroyImmediate(source); Object.DestroyImmediate(result); }
    }

    static float Area(Mesh mesh)
    {
        var vertices=mesh.vertices; var indices=mesh.triangles; var area=0f;
        for(var i=0;i<indices.Length;i+=3)
            area+=Vector3.Cross(vertices[indices[i+1]]-vertices[indices[i]],vertices[indices[i+2]]-vertices[indices[i]]).magnitude/2;
        return area;
    }
}
