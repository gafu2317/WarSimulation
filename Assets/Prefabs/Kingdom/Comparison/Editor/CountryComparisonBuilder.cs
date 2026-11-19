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
using WarSimulation.Combat.Map;
using Object = UnityEngine.Object;

namespace WarSimulation.CountryComparison.Editor
{
    public static class CountryComparisonBuilder
    {
        const string ScenePath = "Assets/Scenes/Country.unity";
        const string PlanPath = "Assets/Data/CountryComparison/town_plan.json";
        const string OldRoot = "Assets/Prefabs/Kingdom/City/Prefabs";
        const string NewRoot = "Assets/Prefabs/Kingdom/CityV2/Prefabs";
        const string SupportRoot = "Assets/Prefabs/Kingdom/Comparison";
        const string ReportRoot = "Artifacts/CountryComparison";

        [Serializable] class Point { public float x, z; public Vector3 Vector => new(x, 0, z); }
        [Serializable] class Road { public string name; public float width; public bool paved; public Point[] points, outline; }
        [Serializable] class Placement
        {
            public string name, old, zone;
            public float x, y, z, width, depth, yaw, scale;
        }
        [Serializable] class Square { public string name; public float x, z, width, depth, yaw; public Point[] points; public bool fenced,flowers; }
        [Serializable] class Plan
        {
            public bool pavedTown;
            public float cityHalfSize, cityNorth, cityOffset, riverZ, riverWidth;
            public Point[] wallOutline;
            public Road[] roads, accessPaths, frontages;
            public Placement[] buildings, props, watchtowers;
            public Square[] squares, gardens, greens, enclosures;
        }
        [Serializable] class Placed
        {
            public string set, model, zone, prefab;
            public Vector3 position, size;
            public bool isBuilding, prefabConnected;
        }
        [Serializable] class Report
        {
            public string scene = ScenePath;
            public string status;
            public int buildingsPerCity, propsPerCity, streetsPerCity, entrancePathsPerCity;
            public string[] failures;
            public Placed[] placements;
        }

        static readonly List<Placed> Placements = new();
        static readonly Dictionary<string, GameObject> Sources = new();
        static readonly Dictionary<string, Mesh> Paths = new();
        static readonly List<string> Failures = new();
        static Plan plan;

        [MenuItem("WarSim/Country/Build Two Walled Countries")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
            var current = SceneManager.GetActiveScene();
            if (current.isDirty)
                throw new InvalidOperationException("The current scene has unsaved changes: " + current.path);
            Directory.CreateDirectory(SupportRoot + "/Materials");
            Directory.CreateDirectory(SupportRoot + "/Meshes");
            Directory.CreateDirectory(ReportRoot);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            plan = JsonUtility.FromJson<Plan>(File.ReadAllText(PlanPath));
            if(plan.riverWidth<=0)throw new InvalidOperationException("Regenerate the country plan before rebuilding the river.");
            Placements.Clear(); Sources.Clear(); Paths.Clear(); Failures.Clear();
            CountryRoofPalette.BeginBuild();
            if (!File.Exists(ScenePath)) AssetDatabase.CopyAsset("Assets/Scenes/CountryTest.unity", ScenePath);
            var scene = current.path == ScenePath ? current : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Country Comparison") ?? new GameObject("Country Comparison");
            var comparison = root.GetComponent<CountryComparisonView>() ?? root.AddComponent<CountryComparisonView>();
            foreach (var child in root.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            var oldCity = new GameObject("旧モデルの国");
            var newCity = new GameObject("新モデルの国");
            oldCity.transform.SetParent(root.transform, false);
            newCity.transform.SetParent(root.transform, false);
            oldCity.transform.localPosition = Vector3.left * plan.cityOffset;
            newCity.transform.localPosition = Vector3.right * plan.cityOffset;
            BuildLandscape(root.transform);
            root.transform.Find("Old town earth").SetParent(oldCity.transform, true);
            root.transform.Find("New town earth").SetParent(newCity.transform, true);
            var packedEarth = plan.pavedTown ? null : SolidMaterial("Packed earth", new Color(1.22f, 1.32f, 1.42f));
            BuildCity(oldCity.transform, false, packedEarth);
            BuildCity(newCity.transform, true, packedEarth);
            CountryGroundSurfaces.Apply(root.transform, packedEarth);
            var camera = ConfigureScene(scene);
            comparison.viewCamera = camera;
            comparison.cities = new[] { oldCity, newCity };
            comparison.centers = new[] { oldCity.transform.position, newCity.transform.position };
            comparison.hiddenCountryGround = new GameObject[2];
            for (var i=0;i<2;i++)
            {
                var backdrop=new GameObject("Hidden country meadow "+i,typeof(MeshFilter),typeof(MeshRenderer));
                backdrop.transform.SetParent(root.transform,false);
                backdrop.transform.localPosition=comparison.centers[i]+Vector3.down*.004f;
                backdrop.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(SupportRoot+"/Meshes/Town footprint.asset");
                backdrop.GetComponent<MeshRenderer>().sharedMaterial=root.transform.Find("Continuous meadow").GetComponent<MeshRenderer>().sharedMaterial;
                comparison.hiddenCountryGround[i]=backdrop;
            }
            var castle = plan.buildings.Single(b => b.name == "Royal_Castle");
            comparison.initialFocus = new Vector3(castle.x, 6, castle.z);
            comparison.ShowInitialView();
            CountryFacilityBuilder.BindTargets(comparison);
            CountryMinimapBuilder.RefreshLayout();
            foreach (var component in Object.FindObjectsByType<CountryOperationTestUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                component.enabled = false;
            var tester = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "DateManagerTester");
            if (tester != null) tester.SetActive(false);
            Validate(root);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            File.WriteAllText(ReportRoot + "/scene_validation.json", JsonUtility.ToJson(new Report
            {
                status = Failures.Count == 0 ? "PASS" : "FAIL", failures = Failures.ToArray(),
                buildingsPerCity = plan.buildings.Length, propsPerCity = plan.props.Length,
                streetsPerCity = plan.roads.Length, entrancePathsPerCity = plan.accessPaths.Length,
                placements = Placements.ToArray()
            }, true));
            SceneView.lastActiveSceneView?.LookAt(new Vector3(0, 0, 0), Quaternion.Euler(47, 0, 0), 290);
            Selection.activeGameObject = root;
            if (Failures.Count > 0) throw new InvalidOperationException(string.Join("\n", Failures.Take(8)));
            Debug.Log("Country comparison saved: two cities, " + plan.buildings.Length + " buildings each, all entrances connected.");
        }

        static void BuildCity(Transform root, bool modern, Material packedEarth)
        {
            var roadMaterial = Paving(modern);
            var gravel = plan.pavedTown ? roadMaterial : SolidMaterial("Gravel path", new Color(1.3f, 1.6f, 1.9f));
            var courtMaterial = packedEarth ?? roadMaterial;
            var courtHeight = plan.pavedTown ? .029f : .010f;
            var buildings = Child(root, "Buildings and courtyards");
            var streets = Child(root, "Connected streets and entrance paths");
            var life = Child(root, "Market, yards and street life");
            var walls = Child(root, "Complete city wall and gates");
            if (plan.greens is { Length: > 0 })
            {
                var lawn = SolidMaterial("Garden lawn", new Color(.33f,.43f,.23f));
                foreach (var green in plan.greens)
                    GroundPatch(green.name,life,green.x,green.z,green.width,green.depth,.012f,lawn);
            }
            foreach (var enclosure in plan.enclosures ?? plan.greens ?? Array.Empty<Square>())
                GardenEdging(enclosure,life,modern);
            foreach (var record in plan.buildings)
                Place(record.name, record, Child(buildings, record.zone), modern, true);
            foreach (var record in plan.props)
            {
                var furnishing=Place(modern ? record.name : record.old, record, Child(life, record.zone), modern, false);
                if(record.name.StartsWith("GroundPlant_") || record.name.StartsWith("Flower_") && !record.name.EndsWith("_Stall"))
                {
                    var bounds=CountryComparisonImporter.BoundsOf(furnishing);
                    var center=root.InverseTransformPoint(bounds.center);
                    if(plan.gardens.Any(g=>{
                        var local=Quaternion.Euler(0,-g.yaw,0)*new Vector3(record.x-g.x,0,record.z-g.z);
                        return Mathf.Abs(local.x)<g.width/2 && Mathf.Abs(local.z)<g.depth/2;
                    }))continue;
                    GroundPatch("Street planting bed",life,center.x,center.z,bounds.size.x+.12f,bounds.size.z+.12f,.015f,SolidMaterial("Garden soil",new Color(.26f,.21f,.13f)));
                }
            }
            foreach (var road in plan.roads.Concat(plan.frontages))
                PathMesh(road, streets, roadMaterial);
            foreach (var approach in plan.accessPaths)
            {
                var paved = plan.pavedTown || approach.paved;
                var material = paved ? roadMaterial : gravel;
                var height = paved ? .025f : .020f;
                if (approach.outline is { Length: >= 3 })
                    PolygonPatch(new Square { name = approach.name, points = approach.outline }, streets, material, height);
                else
                    PathMesh(approach, streets, material, height);
            }
            foreach (var square in plan.squares)
                if (square.points is { Length: >= 3 }) PolygonPatch(square, streets, courtMaterial, courtHeight);
                else GroundPatch(square.name, streets, square.x, square.z, square.width, square.depth, courtHeight, courtMaterial);
            var soil = SolidMaterial("Garden soil", new Color(.26f, .21f, .13f));
            foreach (var garden in plan.gardens)
            {
                var patch = GroundPatch("Rear kitchen garden", life, garden.x, garden.z, garden.width, garden.depth, .015f, soil);
                patch.localRotation = Quaternion.Euler(0, garden.yaw, 0);
                for(var row=0;row<2;row++)
                    for(var i=0;i<4;i++)
                    {
                        var offset=Quaternion.Euler(0,garden.yaw,0)*new Vector3((i-1.5f)*garden.width/5,0,(row-.5f)*garden.depth*.5f);
                        Place(garden.flowers?"Flower_Border":"GroundPlant_GrassTuft", new Placement { x=garden.x+offset.x,z=garden.z+offset.z,scale=garden.flowers?Mathf.Min(.45f,garden.width/8):.65f,zone="Garden planting" },life,modern,false);
                    }
                if (garden.fenced)
                {
                    var timber=modern?AssetDatabase.LoadAssetAtPath<Material>(CountryComparisonImporter.PrefabRoot+"/Materials/DarkTimber.mat"):SolidMaterial("Old support timber",new Color(.23f,.16f,.1f));
                    var edging=Child(life,"Kitchen garden raised bed "+garden.x+","+garden.z);
                    edging.localPosition=new Vector3(garden.x,0,garden.z);edging.localRotation=Quaternion.Euler(0,garden.yaw,0);
                    foreach(var side in new[]{-1,1})
                    {
                        Block("Planting bed end",edging,new Vector3(0,.12f,side*garden.depth/2),new Vector3(garden.width,.24f,.1f),timber);
                        Block("Planting bed side",edging,new Vector3(side*garden.width/2,.12f,0),new Vector3(.1f,.24f,garden.depth),timber);
                    }
                }
            }
            BuildWalls(walls, modern);
            BuildPublicDetails(life, modern, roadMaterial, packedEarth, courtHeight);
            Place("Paved_Plot",new Placement{x=0,z=-107.5f,width=7.5f,depth=6,zone="Inner south gate approach"},streets,modern,false);
            Place("Paved_Plot",new Placement{x=107.5f,z=-34,width=7,depth=6,yaw=90,zone="Inner east gate approach"},streets,modern,false);
        }

        static GameObject Place(string name, Placement record, Transform parent, bool modern, bool building)
        {
            var path = (modern ? NewRoot : OldRoot) + "/" + name + ".prefab";
            if (!Sources.TryGetValue(path, out var prefab))
                Sources[path] = prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("Missing city model: " + path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            var bounds = CountryComparisonImporter.BoundsOf(instance);
            if (record.width > 0 && record.depth > 0)
            {
                if (name.StartsWith("Road_") || name=="Paved_Plot")
                    instance.transform.localScale = new Vector3(record.width / bounds.size.x, 1, record.depth / bounds.size.z);
                else
                    instance.transform.localScale = Vector3.one * Mathf.Min(record.width / bounds.size.x, record.depth / bounds.size.z);
            }
            else
            {
                var scale=record.scale > 0 ? record.scale : 1;
                if(name.StartsWith("Tree_") || name.StartsWith("NaturalTree_"))
                    scale *= (name=="Tree_Shade"?9f:name=="Tree_AlleyCypress"?8f:7f)/bounds.size.y;
                instance.transform.localScale = Vector3.one * scale;
            }
            instance.transform.localRotation = Quaternion.Euler(0, record.yaw, 0);
            bounds = CountryComparisonImporter.BoundsOf(instance);
            var desired = parent.TransformPoint(new Vector3(record.x, record.y, record.z));
            if(name=="Paved_Plot")desired.y+=.025f-bounds.size.y;
            instance.transform.position += desired - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            bounds = CountryComparisonImporter.BoundsOf(instance);
            Placements.Add(new Placed
            {
                set = modern ? "new" : "old", model = name, zone = record.zone, prefab = path,
                position = instance.transform.position, size = bounds.size, isBuilding = building,
                prefabConnected = PrefabUtility.IsPartOfPrefabInstance(instance)
            });
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            if (modern) CountryRoofPalette.Apply(instance, name, record.x, record.z);
            if (modern && name=="Arena") CountryStructureRepairs.ApplyArenaPassage(instance);
            if (modern && name == "Watermill")
                foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
                    if (renderer.name.StartsWith("Flowing millrace water", StringComparison.Ordinal) || renderer.name.StartsWith("Millrace masonry bank",StringComparison.Ordinal))
                    {
                        renderer.enabled = false;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    }
            return instance;
        }

        static void BuildWalls(Transform root, bool modern)
        {
            var polygon = plan.wallOutline.Select(p=>new Vector2(p.x,p.z)).ToArray();
            for (var i = 0; i < polygon.Length; i++)
            {
                var a = polygon[i]; var b = polygon[(i+1)%polygon.Length];
                if (i == 0)
                {
                    WallRun(a, new Vector2(-5.4f,-112), root, modern);
                    WallRun(new Vector2(5.4f,-112), b, root, modern);
                }
                else if (i == 2)
                {
                    WallRun(a, new Vector2(112,-39.4f), root, modern);
                    WallRun(new Vector2(112,-28.6f), new Vector2(112,plan.riverZ-5.7f), root, modern);
                    WallRun(new Vector2(112,plan.riverZ+5.7f), b, root, modern);
                }
                else if(i==6)
                {
                    WallRun(a,new Vector2(-112,plan.riverZ+5.7f),root,modern);
                    WallRun(new Vector2(-112,plan.riverZ-5.7f),b,root,modern);
                }
                else WallRun(a, b, root, modern);
            }
            Gate(0, -112, 0, root, modern);
            Gate(112, -34, 90, root, modern);
            WaterGate(-112,root,modern);
            WaterGate(112,root,modern);
            foreach (var tower in plan.watchtowers)
                Place("Watchtower", tower, root, modern, false);
        }

        static void WallRun(Vector2 a, Vector2 b, Transform root, bool modern)
        {
            var count = Mathf.CeilToInt(Vector2.Distance(a,b)/6);
            var length = Vector2.Distance(a,b)/count;
            var yaw = Mathf.Atan2(-(b.y-a.y),b.x-a.x)*Mathf.Rad2Deg;
            for (var i=0; i<count; i++)
            {
                var p=Vector2.Lerp(a,b,(i+.5f)/count);
                var wall=Place("Granite_Straight",new Placement { x=p.x, z=p.y, yaw=yaw, zone="City wall" },root,modern,false);
                Fit(wall,length,5.8f,modern?.85f:1.97f);
            }
        }

        static void Gate(float x, float z, float yaw, Transform root, bool modern)
        {
            var gate=Place("Granite_Gate", new Placement { x=x, z=z, width=10.8f, depth=6, yaw=yaw, zone="City gate" }, root, modern, false);
            if(!modern) return;
            foreach(var renderer in gate.GetComponentsInChildren<MeshRenderer>(true))
            {
                var materials=renderer.sharedMaterials;
                var kept=Enumerable.Range(0,materials.Length).Where(i=>materials[i].name!="Iron_Grate").ToArray();
                if(kept.Length==materials.Length)continue;
                var filter=renderer.GetComponent<MeshFilter>();var source=filter.sharedMesh;
                var path=SupportRoot+"/Meshes/Open city gate.asset";
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(mesh==null)
                {
                    mesh=Object.Instantiate(source);mesh.name="City gate with retracted portcullis";
                    mesh.subMeshCount=kept.Length;
                    for(var i=0;i<kept.Length;i++)mesh.SetTriangles(source.GetTriangles(kept[i]),i);
                    AssetDatabase.CreateAsset(mesh,path);
                }
                filter.sharedMesh=mesh;renderer.sharedMaterials=kept.Select(i=>materials[i]).ToArray();
                PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
        }

        static void WaterGate(float x,Transform parent,bool modern)
        {
            var root=Child(parent,x<0?"Western river watergate":"Eastern river watergate");
            root.localPosition=new Vector3(x,0,plan.riverZ);
            root.localRotation=Quaternion.Euler(0,90,0);
            var stone=modern?AssetDatabase.LoadAssetAtPath<Material>(CountryComparisonImporter.PrefabRoot+"/Materials/Limestone.mat"):
                SolidMaterial("Old watergate stone",new Color(.48f,.5f,.46f));
            var path=SupportRoot+"/Meshes/River watergate arch.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){mesh=new Mesh{name="Open river watergate arch"};AssetDatabase.CreateAsset(mesh,path);}
            var vertices=new List<Vector3>();var triangles=new List<int>();
            void Face(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
            {
                var n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});
                triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            for(var i=0;i<24;i++)
            {
                var a=Mathf.PI*i/24;var b=Mathf.PI*(i+1)/24;
                var left=new Vector2(Mathf.Cos(b)*4.7f,1.4f+Mathf.Sin(b)*2.4f);
                var right=new Vector2(Mathf.Cos(a)*4.7f,1.4f+Mathf.Sin(a)*2.4f);
                var lf=new Vector3(left.x,left.y,-1.1f);var rf=new Vector3(right.x,right.y,-1.1f);
                var lt=new Vector3(left.x,5.2f,-1.1f);var rt=new Vector3(right.x,5.2f,-1.1f);
                var back=Vector3.forward*2.2f;
                Face(lf,lt,rt,rf);Face(lf+back,rf+back,rt+back,lt+back);
                Face(lf,rf,rf+back,lf+back);Face(lt,lt+back,rt+back,rt);
            }
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);
            mesh.SetUVs(0,vertices.Select(v=>new Vector2(v.x/2,(v.y+v.z)/2)).ToList());mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.UploadMeshData(false);
            EditorUtility.SetDirty(mesh);
            var arch=new GameObject("Open arch over flowing river",typeof(MeshFilter),typeof(MeshRenderer));
            arch.transform.SetParent(root,false);arch.GetComponent<MeshFilter>().sharedMesh=mesh;arch.GetComponent<MeshRenderer>().sharedMaterial=stone;
            foreach(var side in new[]{-1,1})
                Block("Watergate abutment",root,new Vector3(side*5.2f,2.6f,0),new Vector3(1,5.2f,2.2f),stone);
            for(var i=0;i<8;i++)
                Block("Watergate battlement",root,new Vector3(-5.25f+i*1.5f,5.5f,0),new Vector3(.75f,.6f,2.2f),stone);
        }

        static void GardenEdging(Square garden, Transform parent, bool modern)
        {
            const float thickness = .3f;
            var left = garden.x - garden.width / 2;
            var right = garden.x + garden.width / 2;
            var back = garden.z + garden.depth / 2;
            var start = garden.z - garden.depth / 2 + 8;
            var end = back - thickness / 2;
            var boundary = Child(parent, garden.name + " stone boundary");
            foreach (var x in new[] { left, right })
            {
                var side = Place("Granite_Straight", new Placement { x=x,z=(start+end)/2,yaw=-90,zone="Garden boundary" }, boundary, modern, false);
                Fit(side, end-start, .55f, thickness);
            }
            var rear = Place("Granite_Straight", new Placement { x=garden.x,z=back,zone="Garden boundary" }, boundary, modern, false);
            Fit(rear, garden.width+thickness, .55f, thickness);
        }

        static void BuildPublicDetails(Transform parent,bool modern,Material paving,Material packedEarth,float courtHeight)
        {
            var quay=Child(parent,"Central river, millrace and working quay");
            BuildRiverBanks(quay,modern);
            Bridge(quay,modern,"Stone_Bridge","Royal bridge crossing",6.5f,"Market crossing","Royal ascent");
            Bridge(quay,modern,"Wooden_Bridge","Residential bridge crossing",3.5f);
            Bridge(quay,modern,"Stone_Bridge","Scholars bridge crossing",4.5f);
            foreach(var x in new[]{33f,38f})
            {
                Place("Dock_Section",new Placement{x=x,z=4.5f,zone="Central river cargo landing"},quay,modern,false);
            }
            var stairs=Place("Wooden_Stairs",new Placement{x=35.5f,z=1.15f,scale=.72f,zone="Shared quay access stairs"},quay,modern,false);
            stairs.transform.localScale=Vector3.Scale(stairs.transform.localScale,new Vector3(1.35f,1,1));
            var stairBounds=CountryComparisonImporter.BoundsOf(stairs);
            var top=Enumerable.Range(1,100).Max(i=>WalkHeight(stairs,stairBounds.center.x,Mathf.Lerp(stairBounds.min.z,stairBounds.max.z,i/100f)));
            stairs.transform.localScale=Vector3.Scale(stairs.transform.localScale,new Vector3(1,.9f/(top-stairBounds.min.y),1));
            stairs.transform.position+=Vector3.up*(stairBounds.min.y-CountryComparisonImporter.BoundsOf(stairs).min.y);
            PrefabUtility.RecordPrefabInstancePropertyModifications(stairs.transform);
            GroundPatch("Cargo landing approach",quay,35.5f,-.8f,4,4,.03f,paving);
            var wood=modern?AssetDatabase.LoadAssetAtPath<Material>(CountryComparisonImporter.PrefabRoot+"/Materials/DarkTimber.mat"):SolidMaterial("Old support timber",new Color(.23f,.16f,.1f));
            Block("Connected raised quay walkway",quay,new Vector3(35.5f,.8f,2.4f),new Vector3(8,.2f,.8f),wood);
            foreach(var x in new[]{31.6f,34.4f,36.6f,39.4f})
                Block("Quay walkway timber support",quay,new Vector3(x,.38f,2.4f),new Vector3(.18f,.76f,.18f),wood);
            Place("Barrel",new Placement{x=33,z=5,y=.9f,scale=.65f,zone="Quay cargo"},quay,modern,false);
            Place("Fish_Crate",new Placement{x=38,z=5.1f,y=.9f,scale=.65f,zone="Quay cargo"},quay,modern,false);
            var yard=Child(parent,"Enclosed workers delivery yard");
            yard.localPosition=new Vector3(-62.5f,0,144);
            GroundPatch("Workers service court ground",yard,0,0,4.8f,9,courtHeight,packedEarth ?? paving);
            foreach(var side in new[]{-1,1})
            {
                var fence=Place("Fence_Panel",new Placement{x=0,z=side*4.5f,zone="Service court boundary"},yard,modern,false);
                Fit(fence,4.8f,1.1f,.12f);
            }
            var leftFence=Place("Fence_Panel",new Placement{x=-2.4f,z=0,yaw=90,zone="Service court boundary"},yard,modern,false);Fit(leftFence,9,1.1f,.12f);
            foreach(var span in new[]{(-3.95f,1.1f),(1.45f,6.1f)})
            {
                var fence=Place("Fence_Panel",new Placement{x=2.4f,z=span.Item1,yaw=90,zone="Service court boundary"},yard,modern,false);
                Fit(fence,span.Item2,1.1f,.12f);
            }
            var gate=Place("Fence_Gate",new Placement{x=2.4f,z=-2.5f,yaw=90,zone="Open service court entrance"},yard,modern,false);Fit(gate,1.8f,1.1f,.12f);
            gate.transform.RotateAround(yard.TransformPoint(new Vector3(2.4f,0,-3.4f)),Vector3.up,-70);
            PrefabUtility.RecordPrefabInstancePropertyModifications(gate.transform);
            Place("Sack_Stack",new Placement{x=-1.6f,z=-.5f,scale=.7f,zone="Service court supplies"},yard,modern,false);
            Place("Barrel",new Placement{x=1.6f,z=-.2f,scale=.8f,zone="Service court washing water"},yard,modern,false);
            var courts=Child(parent,"Public courts and lit trading apron");
            var square = plan.squares.Single(s => s.name == "Fountain square");
            var fountain = Place("Plaza",new Placement{x=square.x,z=square.z,width=14,depth=14,zone=square.name},courts,modern,false);
            if (modern && packedEarth != null)
                foreach (var floor in fountain.GetComponentsInChildren<MeshRenderer>().Where(r => r.name.StartsWith("ContinuousFoundation_Cobble")))
                {
                    floor.sharedMaterial = packedEarth;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(floor);
                }
            var post=Place("Fence_Post",new Placement{x=-46.5f,z=-43.3f,zone="Trading apron lantern support"},courts,modern,false);
            var postBounds=CountryComparisonImporter.BoundsOf(post);
            post.transform.localScale*=2.8f/postBounds.size.y;
            postBounds=CountryComparisonImporter.BoundsOf(post);
            post.transform.position+=parent.TransformPoint(new Vector3(-46.5f,0,-43.3f))-new Vector3(postBounds.center.x,postBounds.min.y,postBounds.center.z);
            PrefabUtility.RecordPrefabInstancePropertyModifications(post.transform);
            postBounds=CountryComparisonImporter.BoundsOf(post);
            var lamp=Place("Hanging_Lantern",new Placement{x=-46.5f,z=-43.8f,y=1.5f,zone="Trading apron lantern"},courts,modern,false);
            var lampBounds=CountryComparisonImporter.BoundsOf(lamp);
            lamp.transform.position+=new Vector3(0,2.45f-lampBounds.max.y,postBounds.min.z-lampBounds.max.z+.025f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(lamp.transform);
            var herbs=Place("Hanging_Herbs",new Placement{x=-54,z=33,y=modern?.8f:0,zone="Physicians herb drying rack"},courts,modern,false);
            if(modern)
            {
                var bounds=CountryComparisonImporter.BoundsOf(herbs);
                var local=parent.InverseTransformPoint(bounds.center);
                foreach(var side in new[]{-1,1})
                    Block("Herb drying rack support",courts,new Vector3(local.x+side*1.1f,bounds.max.y/2,local.z+.15f),new Vector3(.1f,bounds.max.y,.1f),wood);
            }
        }

        static void Bridge(Transform parent,bool modern,string name,string crossingName,float width,
            string southApproach=null,string northApproach=null)
        {
            var crossing=plan.roads.Single(r=>r.name==crossingName);
            var x=crossing.points[0].x;
            var z=plan.riverZ;
            var bridge=Place(name,new Placement{x=x,z=z,zone="Central river crossings"},parent,modern,false);
            Fit(bridge,width,name=="Stone_Bridge"?2.7f:1.65f,10);
            if(modern && name=="Stone_Bridge")CountryStructureRepairs.ApplyBridgeOrientation(bridge);
            var landing=Child(parent,name+" landings "+x+","+z);
            var bounds=CountryComparisonImporter.BoundsOf(bridge);
            foreach(var side in new[]{-1,1})
            {
                var edge=0f;var height=float.NegativeInfinity;
                for(var inset=.15f;inset<.5f && float.IsNegativeInfinity(height);inset+=.05f)
                {
                    edge=bounds.center.z+side*(bounds.extents.z-inset);
                    height=WalkHeight(bridge,bounds.center.x,edge);
                }
                if(float.IsNegativeInfinity(height))throw new InvalidOperationException("No bridge deck at bank: "+name);
                var step=width/80;var usable=0f;
                for(var dx=-width/2;dx<=width/2;dx+=step)
                    if(Mathf.Abs(WalkHeight(bridge,bounds.center.x+dx,edge)-height)<.06f)usable+=step;
                var local=parent.InverseTransformPoint(new Vector3(bounds.center.x,height,edge));
                var approachName=side<0?southApproach:northApproach;
                var approachWidth=approachName==null?crossing.width:plan.roads.Single(r=>r.name==approachName).width;
                LandingRamp(landing,local.x,local.z,local.z+side*2,Mathf.Min(width,usable),approachWidth,local.y,Paving(modern));
            }
        }

        public static float WalkHeight(GameObject instance,float x,float z)
        {
            var height=float.NegativeInfinity;
            foreach(var filter in instance.GetComponentsInChildren<MeshFilter>(true))
            {
                var vertices=filter.sharedMesh.vertices;var indices=filter.sharedMesh.triangles;
                for(var i=0;i<indices.Length;i+=3)
                {
                    var a=filter.transform.TransformPoint(vertices[indices[i]]);var b=filter.transform.TransformPoint(vertices[indices[i+1]]);var c=filter.transform.TransformPoint(vertices[indices[i+2]]);
                    var normal=Vector3.Cross(b-a,c-a);
                    if(Mathf.Abs(normal.y)<=.000001f)continue;
                    var u=((b.z-c.z)*(x-c.x)+(c.x-b.x)*(z-c.z))/normal.y;
                    var v=((c.z-a.z)*(x-c.x)+(a.x-c.x)*(z-c.z))/normal.y;
                    // The signed XZ determinant is the opposite of the upward normal.
                    u=-u;v=-v;
                    if(u<-.0001f || v<-.0001f || u+v>1.0001f)continue;
                    height=Mathf.Max(height,u*a.y+v*b.y+(1-u-v)*c.y);
                }
            }
            return height;
        }

        static void BuildRiverBanks(Transform parent,bool modern)
        {
            var mill=parent.parent.parent.Find("Buildings and courtyards/Mill quay/Watermill");
            var half=(modern?6.5f:6f)*mill.localScale.x;
            var gaps=new[]{(-84.1f,-79.9f),(-3.6f,3.6f),(59.4f,64.6f)};
            foreach(var z in new[]{plan.riverZ-plan.riverWidth/2,plan.riverZ+plan.riverWidth/2})
            {
                var cursor=-110.7f;
                var openings=gaps.ToList();
                if(z<plan.riverZ)openings.Add((31.2f,39.8f));
                if(z>plan.riverZ)openings.Add((-75f,-59f));
                foreach(var opening in openings.OrderBy(g=>g.Item1))
                {
                    RiverBank(new Vector2(cursor,z),new Vector2(opening.Item1,z),parent,modern);
                    cursor=opening.Item2;
                }
                RiverBank(new Vector2(cursor,z),new Vector2(110.7f,z),parent,modern);
                if(z>plan.riverZ)
                {
                    RiverBank(new Vector2(-75,z),new Vector2(-67-half,z),parent,modern);
                    RiverBank(new Vector2(-67+half,z),new Vector2(-59,z),parent,modern);
                }
            }
        }

        static void RiverBank(Vector2 a,Vector2 b,Transform parent,bool modern)
        {
            var length=Vector2.Distance(a,b);
            if(length<.1f)return;
            var count=Mathf.Max(1,Mathf.RoundToInt(length/4));
            for(var i=0;i<count;i++)
            {
                var p=Vector2.Lerp(a,b,(i+.5f)/count);
                var bank=Place("Stone_Wall",new Placement{x=p.x,z=p.y,yaw=Mathf.Atan2(-(b.y-a.y),b.x-a.x)*Mathf.Rad2Deg,zone="Central river banks"},parent,modern,false);
                Fit(bank,length/count,.3f,.35f);
            }
            if(length>1.2f)
            {
                var timber=modern?AssetDatabase.LoadAssetAtPath<Material>(CountryComparisonImporter.PrefabRoot+"/Materials/DarkTimber.mat"):SolidMaterial("Old support timber",new Color(.23f,.16f,.1f));
                var posts=Mathf.Max(1,Mathf.CeilToInt(length/2.8f));
                for(var i=0;i<=posts;i++)
                {
                    var point=Vector2.Lerp(a,b,(float)i/posts);
                    Block("Quay railing post",parent,new Vector3(point.x,.78f,point.y),new Vector3(.1f,.96f,.1f),timber);
                }
                var center=(a+b)/2;
                Block("Continuous quay handrail",parent,new Vector3(center.x,1.25f,center.y),new Vector3(length,.1f,.1f),timber);
                Block("Quay lower rail",parent,new Vector3(center.x,.72f,center.y),new Vector3(length,.07f,.07f),timber);
            }
        }

        static void Fit(GameObject instance,float width,float height,float depth)
        {
            var before=CountryComparisonImporter.BoundsOf(instance);
            var rotation=instance.transform.localRotation;
            instance.transform.localRotation=Quaternion.identity;
            var size=CountryComparisonImporter.BoundsOf(instance).size;
            instance.transform.localScale=Vector3.Scale(instance.transform.localScale,new Vector3(width/size.x,height/size.y,depth/size.z));
            instance.transform.localRotation=rotation;
            var after=CountryComparisonImporter.BoundsOf(instance);
            instance.transform.position+=new Vector3(before.center.x-after.center.x,before.min.y-after.min.y,before.center.z-after.center.z);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            Placements[^1].position=instance.transform.position;
            Placements[^1].size=CountryComparisonImporter.BoundsOf(instance).size;
        }

        static void Block(string name,Transform parent,Vector3 position,Vector3 size,Material material)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;
            obj.transform.SetParent(parent,false);obj.transform.localPosition=position;obj.transform.localScale=size;
            Object.DestroyImmediate(obj.GetComponent<Collider>());
            obj.GetComponent<MeshRenderer>().sharedMaterial=material;
        }

        static void LandingRamp(Transform parent,float x,float edgeZ,float footZ,float deckWidth,float footWidth,float height,Material material)
        {
            var path=SupportRoot+"/Meshes/"+Sanitize("Bridge landing "+x.ToString("F2")+"_"+edgeZ.ToString("F2")+"_"+height.ToString("F3")+"_"+deckWidth.ToString("F2"))+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){mesh=new Mesh{name="Fitted bridge bank landing"};AssetDatabase.CreateAsset(mesh,path);}
            var vertices=new List<Vector3>();var triangles=new List<int>();
            void Face(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
            {
                var n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            var a=new Vector3(x-footWidth/2,.025f,footZ);var b=new Vector3(x-deckWidth/2,height,edgeZ);
            var c=new Vector3(x+deckWidth/2,height,edgeZ);var d=new Vector3(x+footWidth/2,.025f,footZ);
            if(edgeZ>footZ)Face(a,b,c,d);else Face(d,c,b,a);
            Face(a,new Vector3(a.x,0,a.z),new Vector3(b.x,0,b.z),b);
            Face(c,new Vector3(c.x,0,c.z),new Vector3(d.x,0,d.z),d);
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,vertices.Select(v=>new Vector2(v.x/2,v.z/2)).ToList());
            mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
            var obj=new GameObject("Bridge bank landing",typeof(MeshFilter),typeof(MeshRenderer));
            obj.transform.SetParent(parent,false);
            obj.GetComponent<MeshFilter>().sharedMesh=mesh;obj.GetComponent<MeshRenderer>().sharedMaterial=material;
        }

        static Transform Child(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing;
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        static Material Paving(bool modern)
        {
            if (modern) return AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefabs/Kingdom/CityV2/Materials/Cobble.mat");
            var path=SupportRoot+"/Materials/Old street paving.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null)
            {
                material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material,path);
            }
            foreach(var suffix in new[]{"BaseColor","Normal"})
            {
                var texturePath=SupportRoot+"/Textures/Old_Paving_"+suffix+".png";
                var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);
                importer.wrapMode=TextureWrapMode.Repeat;
                importer.textureType=suffix=="Normal"?TextureImporterType.NormalMap:TextureImporterType.Default;
                importer.SaveAndReimport();
                material.SetTexture(suffix=="Normal"?"_BumpMap":"_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            }
            material.color=Color.white;material.SetFloat("_Smoothness",.18f);material.SetFloat("_BumpScale",.25f);
            material.EnableKeyword("_NORMALMAP");
            return material;
        }

        static void PathMesh(Road road, Transform parent, Material material, float height = .025f)
        {
            var key = road.name + "_" + road.points[0].x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "_" + road.points[0].z.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            if (!Paths.TryGetValue(key, out var mesh))
            {
                var vertices = new List<Vector3>(); var triangles = new List<int>();
                var points = road.points.Select(p=>p.Vector).ToArray();
                if (points.Length < 2) return;
                for (var i=0;i<points.Length;i++)
                {
                    var before = (points[i]-points[Mathf.Max(0,i-1)]).normalized;
                    var after = (points[Mathf.Min(points.Length-1,i+1)]-points[i]).normalized;
                    var tangent = (before+after).normalized;
                    if (tangent.sqrMagnitude < .1f) tangent=Vector3.forward;
                    var normal = Vector3.Cross(Vector3.up,tangent);
                    var width = road.width*.5f/Mathf.Max(.55f, Vector3.Dot(normal,Vector3.Cross(Vector3.up,(after.sqrMagnitude>.1f?after:before))));
                    vertices.Add(points[i]+normal*width+Vector3.up*height);
                    vertices.Add(points[i]-normal*width+Vector3.up*height);
                    if (i==0) continue;
                    var n=2*i;
                    triangles.AddRange(new[]{n-2,n-1,n,n-1,n+1,n});
                }
                mesh = new Mesh { name=road.name };
                mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0);
                mesh.SetUVs(0,vertices.Select(v=>new Vector2(v.x/2,v.z/2)).ToList());
                if (material == Resources.Load<Material>("Combat/Map/StylizedRiver"))
                {
                    var flowPath = points.Select(p => new Vector2(p.x, p.z)).ToArray();
                    var distances = new float[flowPath.Length];
                    for (var i = 1; i < flowPath.Length; i++)
                        distances[i] = distances[i - 1] + Vector2.Distance(flowPath[i - 1], flowPath[i]);
                    mesh.SetUVs(2, vertices.Select(v => RiverMeshBuilder.GetFlowUv(new Vector2(v.x, v.z), flowPath, distances)).ToList());
                }
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                var path=SupportRoot+"/Meshes/"+Sanitize(key)+".asset";
                var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(saved==null) AssetDatabase.CreateAsset(mesh,path);
                else
                {
                    saved.Clear();saved.vertices=mesh.vertices;saved.triangles=mesh.triangles;saved.uv=mesh.uv;saved.uv3=mesh.uv3;saved.normals=mesh.normals;saved.RecalculateBounds();
                    EditorUtility.SetDirty(saved);Object.DestroyImmediate(mesh);mesh=saved;
                }
                mesh.UploadMeshData(false);
                Paths[key]=mesh;
            }
            var obj=new GameObject(road.name,typeof(MeshFilter),typeof(MeshRenderer));
            obj.transform.SetParent(parent,false);obj.GetComponent<MeshFilter>().sharedMesh=mesh;
            obj.GetComponent<MeshRenderer>().sharedMaterial=material;
        }

        static Transform GroundPatch(string name, Transform parent, float x,float z,float width,float depth,float y,Material material)
        {
            var obj=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
            obj.transform.SetParent(parent,false);obj.transform.localPosition=new Vector3(x,y,z);
            var key="Patch_"+width.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+"_"+depth.ToString("F2",System.Globalization.CultureInfo.InvariantCulture);
            if(!Paths.TryGetValue(key,out var mesh))
            {
                mesh=new Mesh{name=key};
                mesh.vertices=new[]{new Vector3(-width/2,0,-depth/2),new Vector3(-width/2,0,depth/2),new Vector3(width/2,0,depth/2),new Vector3(width/2,0,-depth/2)};
                mesh.triangles=new[]{0,1,2,0,2,3};
                mesh.uv=mesh.vertices.Select(v=>new Vector2(v.x/2,v.z/2)).ToArray();
                mesh.RecalculateNormals();mesh.RecalculateBounds();
                var path=SupportRoot+"/Meshes/"+Sanitize(key)+".asset";
                var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(saved==null)AssetDatabase.CreateAsset(mesh,path);
                else {EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);mesh=saved;}
                Paths[key]=mesh;
            }
            obj.GetComponent<MeshFilter>().sharedMesh=mesh;obj.GetComponent<MeshRenderer>().sharedMaterial=material;
            return obj.transform;
        }

        static void PolygonPatch(Square square, Transform parent, Material material, float height)
        {
            var path=SupportRoot+"/Meshes/"+Sanitize(square.name)+".asset";
            if (!Paths.TryGetValue(path,out var mesh))
            {
                mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh==null) { mesh=new Mesh{name=square.name}; AssetDatabase.CreateAsset(mesh,path); }
                mesh.Clear();
                mesh.vertices=square.points.Select(p=>p.Vector).ToArray();
                mesh.triangles=Enumerable.Range(1,square.points.Length-2).SelectMany(i=>new[]{0,i+1,i}).ToArray();
                mesh.uv=mesh.vertices.Select(v=>new Vector2(v.x/2,v.z/2)).ToArray();
                mesh.RecalculateNormals();mesh.RecalculateBounds();
                EditorUtility.SetDirty(mesh);Paths[path]=mesh;
            }
            var obj=new GameObject(square.name,typeof(MeshFilter),typeof(MeshRenderer));
            obj.transform.SetParent(parent,false);obj.transform.localPosition=Vector3.up*height;
            obj.GetComponent<MeshFilter>().sharedMesh=mesh;obj.GetComponent<MeshRenderer>().sharedMaterial=material;
        }

        static Material SolidMaterial(string name, Color color)
        {
            var path=SupportRoot+"/Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null) { material=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material,path); }
            material.color=color;material.SetFloat("_Smoothness",.05f);
            if(name is "Meadow" or "Garden lawn" or "Town earth" or "Garden soil" or "Packed earth" or "Gravel path")
            {
                var grass=name is "Meadow" or "Garden lawn";
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(CountryComparisonImporter.ModelRoot+"/Textures/"+(grass?"Grass":"Earth")+"_BaseColor.png");
                material.SetTexture("_BaseMap",texture);
                material.SetTextureScale("_BaseMap",grass?Vector2.one*3:Vector2.one);
                material.color = name switch
                {
                    "Meadow" or "Garden lawn" => Color.white,
                    "Garden soil" => new Color(.68f,.63f,.5f),
                    "Town earth" => new Color(1.15f,1.25f,1.3f),
                    _ => color
                };
            }
            EditorUtility.SetDirty(material);return material;
        }

        static void BuildLandscape(Transform root)
        {
            GroundPatch("Continuous meadow",root,0,0,2400,2400,-.004f,SolidMaterial("Meadow",new Color(.31f,.39f,.22f)));
            var water=Resources.Load<Material>("Combat/Map/StylizedRiver");
            if(water==null)throw new InvalidOperationException("Combat river material is missing.");
            var river=Child(root,"River beyond city walls");river.localPosition=Vector3.up*.185f;
            var west=-plan.cityOffset-112;var east=plan.cityOffset+112;
            PathMesh(new Road{name="Continuous central river",width=plan.riverWidth,points=new[]{
                new Point{x=-900,z=-18},new Point{x=west-150,z=18},new Point{x=west-40,z=plan.riverZ},new Point{x=west,z=plan.riverZ},
                new Point{x=-plan.cityOffset+112,z=plan.riverZ},new Point{x=plan.cityOffset-112,z=plan.riverZ},new Point{x=east,z=plan.riverZ},
                new Point{x=east+40,z=plan.riverZ},new Point{x=east+150,z=-4},new Point{x=900,z=30}}},river,water);
            foreach(var offset in new[]{-plan.cityOffset,plan.cityOffset})
            {
                var soil=plan.pavedTown?Paving(offset>0):SolidMaterial("Town earth",new Color(.47f,.43f,.32f));
                var north=plan.cityNorth-1;
                var points=new[]{new Vector3(-92,0,-111),new Vector3(92,0,-111),new Vector3(111,0,-92),new Vector3(111,0,north-19),new Vector3(92,0,north),new Vector3(-92,0,north),new Vector3(-111,0,north-19),new Vector3(-111,0,-92)};
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(SupportRoot+"/Meshes/Town footprint.asset");
                if(mesh==null)
                {
                    mesh=new Mesh{name="Walled town footprint"};
                    AssetDatabase.CreateAsset(mesh,SupportRoot+"/Meshes/Town footprint.asset");
                }
                mesh.vertices=new[]{new Vector3(0,0,36)}.Concat(points).ToArray();
                var triangles=new List<int>();for(var i=0;i<8;i++)triangles.AddRange(new[]{0,(i+1)%8+1,i+1});
                mesh.triangles=triangles.ToArray();mesh.uv=mesh.vertices.Select(v=>new Vector2(v.x/3,v.z/3)).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
                EditorUtility.SetDirty(mesh);
                var obj=new GameObject(offset<0?"Old town earth":"New town earth",typeof(MeshFilter),typeof(MeshRenderer));
                obj.transform.SetParent(root,false);obj.transform.localPosition=new Vector3(offset,plan.pavedTown?0:-.015f,0);
                obj.GetComponent<MeshFilter>().sharedMesh=mesh;obj.GetComponent<MeshRenderer>().sharedMaterial=soil;
            }
        }

        static Camera ConfigureScene(Scene scene)
        {
            var camera=Camera.main;
            if(camera==null) camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();
            camera.gameObject.tag="MainCamera";camera.nearClipPlane=.3f;camera.farClipPlane=1100;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.55f,.66f,.7f);
            camera.allowHDR=true;camera.allowMSAA=false;
            ConfigureCameraControls(camera);
            var data=camera.GetComponent<UniversalAdditionalCameraData>()??camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing=true;data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.64f,.7f,.79f);
            RenderSettings.ambientEquatorColor=new Color(.55f,.57f,.53f);
            RenderSettings.ambientGroundColor=new Color(.34f,.31f,.25f);
            RenderSettings.fog=false;
            var light=Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l=>l.type==LightType.Directional);
            if(light==null) light=new GameObject("Sun",typeof(Light)).GetComponent<Light>();
            light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(48,-30,0);
            light.color=new Color(1,.92f,.8f);light.intensity=1.6f;light.shadows=LightShadows.Soft;light.shadowStrength=.72f;
            light.shadowBias=.03f;light.shadowNormalBias=.05f;light.shadowNearPlane=.1f;
            light.lightmapBakeType=LightmapBakeType.Realtime;RenderSettings.sun=light;
            return camera;
        }

        static void Validate(GameObject root)
        {
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if(renderer.sharedMaterials.Any(m=>m==null || m.shader==null || m.shader.name.Contains("InternalErrorShader"))) Failures.Add("Missing material: "+renderer.name);
                if(!float.IsFinite(renderer.bounds.center.x)||!float.IsFinite(renderer.bounds.size.y)) Failures.Add("Invalid bounds: "+renderer.name);
            }
            if(Placements.Any(p=>!p.prefabConnected)) Failures.Add("A city model lost its prefab connection.");
            foreach(var set in new[]{"old","new"})
            {
                var list=Placements.Where(p=>p.set==set&&p.isBuilding).ToArray();
                if(list.Length!=plan.buildings.Length) Failures.Add("Building count mismatch: "+set);
                for(var i=0;i<list.Length;i++)
                {
                    var a=list[i];
                    var offset=set=="old"?-plan.cityOffset:plan.cityOffset;
                    if(Mathf.Abs(a.position.x-offset)>110||a.position.z < -110||a.position.z>plan.cityNorth-3) Failures.Add("Building outside wall: "+a.model);
                }
            }
        }

        static string Sanitize(string text) => string.Concat(text.Select(c=>char.IsLetterOrDigit(c)?c:'_'));

        static void ConfigureCameraControls(Camera camera)
        {
            var freeCamera = camera.GetComponent<EditorStyleCameraController>();
            if (freeCamera != null) freeCamera.enabled = false;
            if (camera.GetComponent<CountryCameraController>() == null)
                camera.gameObject.AddComponent<CountryCameraController>();
        }

        [MenuItem("WarSim/Country/Set Up Camera Controls")]
        public static void SetUpCameraControls()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != ScenePath)
                throw new InvalidOperationException("Open Country outside Play mode first.");

            var comparison = Object.FindAnyObjectByType<CountryComparisonView>();
            ConfigureCameraControls(comparison.viewCamera);
            comparison.viewCamera.allowMSAA = false;
            comparison.ShowInitialView();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        [MenuItem("WarSim/Country/View Both Countries")]
        public static void ViewBoth() => View(2);
        [MenuItem("WarSim/Country/View Old Country")]
        public static void ViewOld() => View(0);
        [MenuItem("WarSim/Country/View New Country")]
        public static void ViewNew() => View(1);

        static void View(int index)
        {
            Object.FindFirstObjectByType<CountryComparisonView>().Show(index);
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }

        [MenuItem("WarSim/Country/Capture Comparison Views")]
        public static void Capture()
        {
            Directory.CreateDirectory(ReportRoot);
            var comparison=Object.FindFirstObjectByType<CountryComparisonView>();
            for(var index=0;index<3;index++)
            {
                comparison.Show(index);
                CaptureCamera(comparison.viewCamera,ReportRoot+"/"+new[]{"Old_Overview","New_Overview","Both_Overview"}[index]+".png");
            }
            comparison.Show(2);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        [MenuItem("WarSim/Country/Capture District Views")]
        public static void CaptureDistricts()
        {
            var comparison=Object.FindFirstObjectByType<CountryComparisonView>();
            var names=new[]{"Market","Residential","Cathedral","Freight","Gate","Palace","Academies","Riverside","MarketHall","Windmill","Arena","Quay","TradingApron","WarriorAcademy","EasternWatergate","WesternWatergate","RoyalBridge"};
            var targets=new[]{new Vector3(0,0,-27),new Vector3(-62,0,-62),new Vector3(-39,4,33),new Vector3(82,0,-32),new Vector3(0,0,-105),new Vector3(0,5,77),new Vector3(63,0,70),new Vector3(-67,0,14.15f),new Vector3(-38,0,-30),new Vector3(90,0,-88),new Vector3(-85,0,-56),new Vector3(35.5f,0,4.5f),new Vector3(-45,0,-37),new Vector3(-80,4,53),new Vector3(112,0,8),new Vector3(-112,0,8),new Vector3(0,0,8)};
            var positions=new[]{new Vector3(22,28,-39),new Vector3(18,25,-35),new Vector3(29,36,-49),new Vector3(27,30,-40),new Vector3(23,21,-34),new Vector3(36,48,-64),new Vector3(36,36,-48),new Vector3(18,21,-25),new Vector3(23,21,-33),new Vector3(23,23,-32),new Vector3(23,24,-34),new Vector3(-14,9,-18),new Vector3(-7,5,-6),new Vector3(27,33,-45),new Vector3(23,14,-21),new Vector3(-23,14,-21),new Vector3(13,25,-17)};
            for(var index=0;index<2;index++)
            {
                comparison.Show(index);
                for(var shot=0;shot<names.Length;shot++)
                {
                    var target=comparison.centers[index]+targets[shot];
                    comparison.viewCamera.transform.position=target+positions[shot];
                    comparison.viewCamera.transform.LookAt(target+Vector3.up*2);
                    CaptureCamera(comparison.viewCamera,ReportRoot+"/"+(index==0?"Old_":"New_")+names[shot]+".png");
                }
            }
            comparison.Show(2);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        [MenuItem("WarSim/Country/Write Rendering Diagnostics")]
        public static void RenderingDiagnostics()
        {
            var current=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var quality=QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            var sun=RenderSettings.sun;
            File.WriteAllText(ReportRoot+"/rendering_diagnostics.txt",string.Join("\n",new[]{
                "Current pipeline: "+(current==null?"null":current.name+" shadows="+current.shadowDistance),
                "Quality pipeline: "+(quality==null?"null":quality.name+" shadows="+quality.shadowDistance),
                "Sun: "+(sun==null?"null":sun.name+" active="+sun.isActiveAndEnabled+" shadows="+sun.shadows+" intensity="+sun.intensity),
                "Frame: "+Time.frameCount+" Playing: "+Application.isPlaying,
                "Quality shadows: "+QualitySettings.shadows
            }));
        }

        [MenuItem("WarSim/Country/Validate Saved Comparison")]
        public static void ValidateSaved()
        {
            var savedPlan=JsonUtility.FromJson<Plan>(File.ReadAllText(PlanPath));
            var comparison=Object.FindFirstObjectByType<CountryComparisonView>();
            var failures=new List<string>();var checkedBuildings=0;
            foreach(var city in comparison.cities.Where(city => city != null))
            {
                if (CountryLayoutInstaller.IsApproved(city))
                {
                    CountryLayoutInstaller.ValidateModern(comparison);
                    continue;
                }
                var roots=city.transform.Find("Buildings and courtyards");
                var candidates=new List<GameObject>();
                foreach(Transform zone in roots)foreach(Transform item in zone)candidates.Add(item.gameObject);
                foreach(var entry in savedPlan.buildings)
                {
                    var target=city.transform.TransformPoint(new Vector3(entry.x,0,entry.z));
                    var item=candidates.FirstOrDefault(g=>
                    {
                        if(g.name!=entry.name)return false;
                        var b=CountryComparisonImporter.BoundsOf(g);
                        if(entry.name=="Watermill" && g.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="BuildingRiverEdge"))
                            return b.min.x>=target.x-entry.width/2 && b.max.x<=target.x+entry.width/2 &&
                                b.min.z>=target.z-entry.depth/2 && b.max.z<=target.z+entry.depth/2;
                        return Vector2.Distance(new Vector2(b.center.x,b.center.z),new Vector2(target.x,target.z))<.06f;
                    });
                    if(item==null) {failures.Add("Missing saved placement: "+city.name+" "+entry.name+" "+entry.x+","+entry.z);continue;}
                    var bounds=CountryComparisonImporter.BoundsOf(item);
                    var ground=item.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="BuildingRiverEdge");
                    if(Mathf.Abs(ground!=null?ground.position.y:bounds.min.y)>.025f)failures.Add("Not grounded: "+item.name);
                    if(!PrefabUtility.IsPartOfPrefabInstance(item))failures.Add("Prefab disconnected: "+item.name);
                    if(item.GetComponentsInChildren<Renderer>(true).Any(r=>r.sharedMaterials.Any(m=>m==null)))failures.Add("Missing material: "+item.name);
                    checkedBuildings++;
                }
                if(candidates.Count!=savedPlan.buildings.Length)failures.Add("Unexpected building count: "+city.name);
            }
            if(comparison.cities.Length<2 || comparison.centers.Length!=comparison.cities.Length)failures.Add("Camera comparison references are incomplete.");
            var text="Status: "+(failures.Count==0?"PASS":"FAIL")+"\nSaved buildings checked: "+checkedBuildings+"\nEntrances per country: "+savedPlan.accessPaths.Length+"\n"+string.Join("\n",failures);
            File.WriteAllText(ReportRoot+"/saved_scene_validation.txt",text);
            if(failures.Count>0)throw new InvalidOperationException(text);
            Debug.Log(text);
        }

        static double validationUntil;
        [MenuItem("WarSim/Country/Keep Validation Running")]
        public static void KeepValidationRunning()
        {
            validationUntil=EditorApplication.timeSinceStartup+90;
            EditorApplication.update-=ValidationTick;EditorApplication.update+=ValidationTick;
        }

        static void ValidationTick()
        {
            if(!EditorApplication.isPlaying || EditorApplication.timeSinceStartup>validationUntil)
            {EditorApplication.update-=ValidationTick;return;}
            EditorApplication.QueuePlayerLoopUpdate();
        }

        public static void CaptureCamera(Camera camera,string path)
        {
            var previous=camera.targetTexture;var active=RenderTexture.active;
            var target=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32){antiAliasing=4};
            var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();
                File.WriteAllBytes(path,image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=previous;RenderTexture.active=active;
                Object.DestroyImmediate(image);target.Release();Object.DestroyImmediate(target);
            }
        }
    }
}
