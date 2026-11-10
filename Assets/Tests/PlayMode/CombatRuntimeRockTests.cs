using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WarSimulation.Combat.Map;
using Object = UnityEngine.Object;

[Category("RuntimeRock")]
public sealed class CombatRuntimeRockTests
{
    private GameObject _root;
    private CombatMapSystem _map;
    private CombatNavMeshBuilder _builder;
    private TerrainData _terrainData;
    private readonly List<GameObject> _characters = new();
    private bool _completed;
    private bool _success;
    private bool _loadedBattleScene;

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.timeScale = 1;
        foreach (GameObject character in _characters) if (character != null) Object.Destroy(character);
        _characters.Clear();
        if (_map != null) _map.RemoveRuntimeRock();
        if (_builder != null) _builder.Clear();
        if (_root != null) Object.Destroy(_root);
        yield return null;
        if (_terrainData != null) Object.Destroy(_terrainData);
        yield return null;
        if (_loadedBattleScene)
        {
            Scene cleanup = SceneManager.CreateScene("RuntimeRockCleanup");
            SceneManager.SetActiveScene(cleanup);
            var scenes = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i) != cleanup) scenes.Add(SceneManager.GetSceneAt(i));
            foreach (Scene scene in scenes) yield return SceneManager.UnloadSceneAsync(scene);
            _loadedBattleScene = false;
        }
    }

    [UnityTest]
    public IEnumerator Placement_RemovesTreeFromGeometryAndNavMeshAndRestoresBoth()
    {
        CreateMap();
        Transform features = new GameObject("GeneratedFeatures").transform;
        features.SetParent(_root.transform, false);
        GameObject tree = Box("Tree_0", features, new Vector3(21.6f, 2, 20), new Vector3(2, 4, 1));
        GameObject otherTree = Box("Tree_1", features, new Vector3(30, 2, 30), new Vector3(1, 4, 1));
        SetBlocked(tree);
        SetBlocked(otherTree);
        Assert.That(_builder.Build(_map.CurrentMap), Is.True);
        NavMeshData original = _builder.Surface.navMeshData;
        Assert.That(OnNavMesh(new Vector3(22.5f, 0, 20)), Is.False);

        Time.timeScale = 0;
        yield return Place(CombatRockShape.Straight, new Vector2(20, 20));

        Assert.That(tree.activeSelf, Is.False);
        Assert.That(otherTree.activeSelf, Is.True);
        Assert.That(_builder.Surface.navMeshData, Is.Not.SameAs(original));
        Assert.That(OnNavMesh(new Vector3(22.5f, 0, 20)), Is.True, "The old tree's NavMesh hole must reopen.");
        Assert.That(OnNavMesh(new Vector3(20, 0, 20)), Is.False);
        Assert.That(_map.CanStandAt(new Vector3(20, 0, 20)), Is.False);
        Assert.That(NavMesh.Raycast(new Vector3(15, 0, 20), new Vector3(25, 0, 20), out _, NavMesh.AllAreas), Is.True);
        var path = new NavMeshPath();
        Assert.That(NavMesh.CalculatePath(new Vector3(15, 0, 20), new Vector3(25, 0, 20), NavMesh.AllAreas, path), Is.True);
        Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
        Assert.That(path.corners.Length, Is.GreaterThan(2));

        _map.RemoveRuntimeRock();
        yield return null;
        Assert.That(tree.activeSelf, Is.True);
        Assert.That(_builder.Surface.navMeshData, Is.SameAs(original));
        Assert.That(OnNavMesh(new Vector3(22.5f, 0, 20)), Is.False);
        Assert.That(OnNavMesh(new Vector3(20, 0, 20)), Is.True);
        Assert.That(_map.CanStandAt(new Vector3(20, 0, 20)), Is.True);
        Assert.That(_root.transform.Find("RuntimeRock"), Is.Null);
    }

    [UnityTest]
    public IEnumerator AllShapes_HaveGroundedCollidersAndOnlyOneRockSurvivesReplacement()
    {
        CreateMap();
        Assert.That(_builder.Build(_map.CurrentMap), Is.True);
        foreach (CombatRockShape shape in new[] { CombatRockShape.Straight, CombatRockShape.Bent, CombatRockShape.U, CombatRockShape.Circle })
        {
            yield return Place(shape, new Vector2(20, 20), 1);
            yield return null;
            CombatRuntimeRock rock = _root.GetComponent<CombatRuntimeRock>();
            Assert.That(rock.Placement.Value.Shape, Is.EqualTo(shape));
            Assert.That(rock.Rock.transform.localPosition.y, Is.EqualTo(-0.02f).Within(0.005f));
            int roots = 0;
            foreach (Transform child in _root.transform) if (child.name == "RuntimeRock") roots++;
            Assert.That(roots, Is.EqualTo(1));
            foreach (BoxCollider collider in rock.Rock.GetComponentsInChildren<BoxCollider>())
            {
                Assert.That(collider.enabled, Is.True);
                Assert.That(collider.bounds.size.y, Is.EqualTo(3).Within(0.001));
                Assert.That(collider.bounds.min.y, Is.LessThanOrEqualTo(0));
                Assert.That(collider.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("VisionObstacle")));
            }
        }
    }

    [UnityTest]
    public IEnumerator Slope_RockSinksOnlyEnoughToMeetLowestGroundUnderItsFootprint()
    {
        CreateMap(slope: 0.1f);
        Assert.That(_builder.Build(_map.CurrentMap), Is.True);
        yield return Place(CombatRockShape.U, new Vector2(20, 20));
        CombatRuntimeRock rock = _root.GetComponent<CombatRuntimeRock>();
        float bottom = rock.Rock.transform.localPosition.y;
        Assert.That(bottom, Is.EqualTo(1.7f).Within(0.03f));
        TerrainCollider terrain = _root.GetComponentInChildren<TerrainCollider>();
        foreach (BoxCollider part in rock.Rock.GetComponentsInChildren<BoxCollider>())
        {
            for (int x = 0; x <= 10; x++)
            for (int z = 0; z <= 10; z++)
            {
                Vector3 point = part.transform.TransformPoint(new Vector3(x / 10f - 0.5f, -0.5f, z / 10f - 0.5f));
                Assert.That(terrain.Raycast(new Ray(point + Vector3.up * 10, Vector3.down), out RaycastHit hit, 20), Is.True);
                Assert.That(point.y, Is.LessThanOrEqualTo(hit.point.y + 0.005f), "No floating bottom sample.");
            }
        }
    }

    [UnityTest]
    public IEnumerator Ring_LeavesItsCenterWalkableButAiCannotCrossTheRockWall()
    {
        CreateMap();
        var characters = new GameObject("Participants").AddComponent<CombatCharacterSystem>();
        characters.transform.SetParent(_root.transform, false);
        Character owner = CreateCharacter(new Vector3(20, 0, 20));
        characters.AllyCharacters.Add(owner);
        Set(owner.GetComponent<CombatCharacterBody>(), "_mapSystem", _map);
        Assert.That(_builder.Build(_map.CurrentMap), Is.True);
        owner.GetComponent<NavMeshAgent>().enabled = true;
        yield return null;
        yield return Place(CombatRockShape.Circle, new Vector2(20, 20), characters: characters);
        Assert.That(_map.CanStandAt(owner.transform.position), Is.True);
        Assert.That(_map.CanStandAt(new Vector3(22.25f, 0, 20)), Is.False);
        Assert.That(owner.GetComponent<NavMeshAgent>().isOnNavMesh, Is.True);
        Assert.That(CombatAiNavigation.IsReachable(owner, new Vector3(20, 0, 28)), Is.False);
        _map.RemoveRuntimeRock();
        yield return null;
        Assert.That(CombatAiNavigation.IsReachable(owner, new Vector3(20, 0, 28)), Is.True);
    }

    [UnityTest]
    public IEnumerator Placement_UnderACharacterRelocatesItAndMapChangeRestoresTrees()
    {
        CreateMap();
        Transform features = new GameObject("GeneratedFeatures").transform;
        features.SetParent(_root.transform, false);
        GameObject tree = Box("Tree_0", features, new Vector3(20, 2, 22), new Vector3(1, 4, 1));
        SetBlocked(tree);
        Assert.That(_builder.Build(_map.CurrentMap), Is.True);
        var characters = new GameObject("Participants").AddComponent<CombatCharacterSystem>();
        characters.transform.SetParent(_root.transform, false);
        Character owner = CreateCharacter(new Vector3(20, 0, 20));
        characters.AllyCharacters.Add(owner);
        owner.GetComponent<NavMeshAgent>().enabled = true;
        yield return null;
        yield return Place(CombatRockShape.Straight, new Vector2(20, 20), characters: characters);
        Assert.That(_map.CanStandAt(owner.transform.position), Is.True);
        Assert.That(owner.GetComponent<NavMeshAgent>().isOnNavMesh, Is.True);
        Assert.That(tree.activeSelf, Is.False);
        _map.SetCurrentMap(new MapData(new HeightMap(40, 40, 1), new GroundStateGrid(40, 40, 1), 2));
        yield return null;
        Assert.That(tree.activeSelf, Is.True);
        Assert.That(_map.RuntimeRockPlacement, Is.Null);
        Assert.That(_map.IsRuntimeRockUpdating, Is.False);
    }

    [UnityTest]
    public IEnumerator InvalidPlacementKeepsExistingRockAndCancellingAnUpdateRestoresOriginalNavMesh()
    {
        CreateMap();
        Assert.That(_builder.Build(_map.CurrentMap), Is.True);
        NavMeshData original = _builder.Surface.navMeshData;
        yield return Place(CombatRockShape.Straight, new Vector2(20, 20));
        Assert.That(_map.TryPlaceRuntimeRock(new CombatRockPlacement(CombatRockShape.U, Vector2.zero, 0),
            null, null, out string error), Is.False);
        Assert.That(error, Is.Not.Empty);
        Assert.That(_map.RuntimeRockPlacement.Value.Shape, Is.EqualTo(CombatRockShape.Straight));
        bool cancelled = false;
        Assert.That(_map.TryPlaceRuntimeRock(new CombatRockPlacement(CombatRockShape.U, new Vector2(20, 20), 0),
            null, (success, _) => cancelled = !success, out error), Is.True, error);
        _map.RemoveRuntimeRock();
        yield return null;
        Assert.That(cancelled, Is.True);
        Assert.That(_map.RuntimeRockPlacement, Is.Null);
        Assert.That(_map.IsRuntimeRockUpdating, Is.False);
        Assert.That(_builder.Surface.navMeshData, Is.SameAs(original));
    }

    [UnityTest]
    public IEnumerator DisablingTheRockRestoresTreesAndTheOriginalNavMesh()
    {
        CreateMap();
        Transform features = new GameObject("GeneratedFeatures").transform;
        features.SetParent(_root.transform, false);
        GameObject tree = Box("Tree_0", features, new Vector3(20, 2, 20), new Vector3(1, 4, 1));
        SetBlocked(tree);
        Assert.That(_builder.Build(_map.CurrentMap), Is.True);
        NavMeshData original = _builder.Surface.navMeshData;
        yield return Place(CombatRockShape.Straight, new Vector2(20, 20));
        Assert.That(tree.activeSelf, Is.False);
        _root.GetComponent<CombatRuntimeRock>().Rock.SetActive(false);
        yield return null;
        Assert.That(tree.activeSelf, Is.True);
        Assert.That(_map.RuntimeRockPlacement, Is.Null);
        Assert.That(_builder.Surface.navMeshData, Is.SameAs(original));
    }

    [UnityTest]
    public IEnumerator RuntimeBattleUi_CreatesAndRemovesRockOnTheLoadedBakedMap()
    {
        _root = null;
        _terrainData = null;
        _loadedBattleScene = true;
        yield return SceneManager.LoadSceneAsync("GafuTest", LoadSceneMode.Single);
        var flow = Object.FindAnyObjectByType<CombatFlow>(FindObjectsInactive.Include);
        Canvas canvas = flow.GetComponentInParent<Canvas>(true);
        canvas.gameObject.SetActive(true);
        canvas.enabled = true;
        flow.gameObject.SetActive(true);
        yield return null;
        _map = Object.FindAnyObjectByType<CombatMapSystem>();
        Assert.That(_map, Is.Not.Null);
        float started = Time.realtimeSinceStartup;
        while (!_map.IsMapReady(_map.AuthoredMap) &&
               Time.realtimeSinceStartup - started < 30) yield return null;
        Assert.That(_map.IsMapReady(_map.AuthoredMap), Is.True,
            $"Map: {_map.AuthoredMap}, preparation: {_map.PreparationState}, failure: {_map.PreparationFailure}");
        _builder = _map.MapOrigin.GetComponent<CombatNavMeshBuilder>();
        NavMeshData original = _builder.Surface.navMeshData;
        var selection = Object.FindAnyObjectByType<CombatCharacterSelection>(FindObjectsInactive.Include);
        var startButton = (Button)selection.GetType().GetField("_startBattleButton",
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(selection);
        Assert.That(startButton.interactable, Is.True);
        startButton.onClick.Invoke();
        yield return null;
        Assert.That(CombatBattleFlow.IsRunning, Is.True);
        var hud = Object.FindAnyObjectByType<CombatBattleHudView>(FindObjectsInactive.Include);
        Transform controls = hud.transform.Find("DebugBattlePanel/RockPlacementControls");
        Assert.That(controls.gameObject.activeInHierarchy, Is.True);
        controls.Find("RockStraight").GetComponent<Button>().onClick.Invoke();
        var view = hud.GetComponentInChildren<CombatRockPlacementView>(true);
        Assert.That(view.IsOpen, Is.True);
        Assert.That(Time.timeScale, Is.Zero);
        view.SelectPosition(new Vector2(0.5f, 0.5f));
        view.Confirm();
        started = Time.realtimeSinceStartup;
        while (view.IsOpen && Time.realtimeSinceStartup - started < 30) yield return null;
        Assert.That(view.IsOpen, Is.False, "Placement must complete on the authored map.");
        Assert.That(_map.RuntimeRockPlacement.HasValue, Is.True);
        Assert.That(Time.timeScale, Is.EqualTo(1));
        CombatRuntimeRock rock = _map.MapOrigin.GetComponent<CombatRuntimeRock>();
        Assert.That(rock.Rock.activeInHierarchy, Is.True);
        Assert.That(_map.CanStandAt(rock.Rock.transform.position), Is.False);
        Camera camera = Camera.main;
        if (camera != null)
        {
            var controller = camera.GetComponent<EditorStyleCameraController>();
            if (controller != null) controller.enabled = false;
            camera.transform.position = rock.Rock.transform.position + new Vector3(10, 9, -10);
            camera.transform.LookAt(rock.Rock.transform.position + Vector3.up);
            System.IO.Directory.CreateDirectory("/tmp/WarSimulation-rock-ui");
            ScreenCapture.CaptureScreenshot("/tmp/WarSimulation-rock-ui/runtime-rock.png");
            yield return null;
            yield return null;
        }
        controls.Find("RockU").GetComponent<Button>().onClick.Invoke();
        Assert.That(view.IsOpen, Is.True);
        view.Remove();
        yield return null;
        Assert.That(view.IsOpen, Is.False);
        Assert.That(_map.RuntimeRockPlacement, Is.Null);
        Assert.That(hud.ConfirmedRockPlacement, Is.Null);
        Assert.That(_builder.Surface.navMeshData, Is.SameAs(original));
        Assert.That(Time.timeScale, Is.EqualTo(1));
    }

    private void CreateMap(float slope = 0)
    {
        _root = new GameObject("RuntimeRockTestMap");
        _map = _root.AddComponent<CombatMapSystem>();
        Set(_map, "_buildMapOnStart", false);
        _builder = _root.AddComponent<CombatNavMeshBuilder>();
        var map = new MapData(new HeightMap(40, 40, 1), new GroundStateGrid(40, 40, 1), 1);
        for (int x = 0; x < 40; x++)
        for (int z = 0; z < 40; z++) map.Height.SetHeight(x, z, x * slope);
        _map.SetCurrentMap(map);
        _terrainData = new TerrainData { heightmapResolution = 65, size = new Vector3(40, 8, 40) };
        var heights = new float[65, 65];
        for (int z = 0; z < 65; z++)
        for (int x = 0; x < 65; x++) heights[z, x] = x / 64f * 40 * slope / 8;
        _terrainData.SetHeights(0, 0, heights);
        GameObject terrain = Terrain.CreateTerrainGameObject(_terrainData);
        terrain.transform.SetParent(_root.transform, false);
        Physics.SyncTransforms();
    }

    private IEnumerator Place(CombatRockShape shape, Vector2 point, int rotation = 0, CombatCharacterSystem characters = null)
    {
        _completed = _success = false;
        Assert.That(_map.TryPlaceRuntimeRock(new CombatRockPlacement(shape, point, rotation), characters,
            (success, error) => { _completed = true; _success = success; Assert.That(error, Is.Null); }, out string failure), Is.True, failure);
        float started = Time.realtimeSinceStartup;
        while (!_completed && Time.realtimeSinceStartup - started < 30) yield return null;
        Assert.That(_completed, Is.True, "NavMesh update timed out.");
        Assert.That(_success, Is.True);
    }

    private Character CreateCharacter(Vector3 position)
    {
        var go = new GameObject("RockTestCharacter");
        go.SetActive(false);
        var character = go.AddComponent<Character>();
        go.GetComponent<NavMeshAgent>().enabled = false;
        go.transform.position = position;
        _characters.Add(go);
        go.SetActive(true);
        character.Health.Initialize(30, 30);
        return character;
    }

    private static bool OnNavMesh(Vector3 point) => NavMesh.SamplePosition(point, out _, 0.2f, NavMesh.AllAreas);
    private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = position;
        box.transform.localScale = size;
        return box;
    }
    private static void SetBlocked(GameObject go)
    {
        var modifier = go.AddComponent<NavMeshModifier>();
        modifier.overrideArea = true;
        modifier.area = NavMesh.GetAreaFromName("Not Walkable");
    }
    private static void Set(object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}
