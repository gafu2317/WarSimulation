using System.Reflection;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using WarSimulation.Combat.Map;

public sealed class FeatureRendererPlayModeTests
{
    [UnityTest]
    public IEnumerator CombatCamera_SynchronizesEditorStyleStateFromCurrentMapStonePositions()
    {
        GameObject cameraObject = new GameObject("MainCamera");
        GameObject flowObject = new GameObject("CombatFlow");
        GameObject mapSystemObject = new GameObject("CombatMapSystem");
        try
        {
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            EditorStyleCameraController controller = cameraObject.AddComponent<EditorStyleCameraController>();
            CombatFlow flow = flowObject.AddComponent<CombatFlow>();
            CombatMapSystem mapSystem = mapSystemObject.AddComponent<CombatMapSystem>();
            flow.enabled = false;
            FieldInfo mapSystemField = typeof(CombatFlow).GetField(
                "_mapSystem",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(mapSystemField, Is.Not.Null);
            mapSystemField.SetValue(flow, mapSystem);
            MethodInfo applyCamera = typeof(CombatFlow).GetMethod(
                "ApplyCombatCamera",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(applyCamera, Is.Not.Null);

            mapSystem.SetCurrentMap(CreateMapWithPairedStones(
                new Vector3(1f, 0f, 1f),
                new Vector3(9f, 0f, 9f)));
            yield return null;

            applyCamera.Invoke(flow, null);
            Assert.That(camera.transform.position, Is.EqualTo(new Vector3(30f, 20f, -10f)));
            Assert.That(camera.transform.eulerAngles.y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(GetPrivateField<float>(controller, "yaw"), Is.EqualTo(0f).Within(0.001f));

            mapSystem.SetCurrentMap(CreateMapWithPairedStones(
                new Vector3(9f, 0f, 9f),
                new Vector3(1f, 0f, 1f)));
            applyCamera.Invoke(flow, null);
            Assert.That(camera.transform.position, Is.EqualTo(new Vector3(30f, 20f, 70f)));
            Assert.That(camera.transform.eulerAngles.y, Is.EqualTo(180f).Within(0.001f));
            Assert.That(GetPrivateField<float>(controller, "yaw"), Is.EqualTo(180f).Within(0.001f));
        }
        finally
        {
            Object.Destroy(flowObject);
            Object.Destroy(cameraObject);
            Object.Destroy(mapSystemObject);
        }
    }

    [UnityTest]
    public IEnumerator RenderThenRefreshInSameFrame_MovesReplacementMagicStones()
    {
        GameObject host = new GameObject("FeatureRendererPlayModeHost");
        try
        {
            FeatureRenderer renderer = host.AddComponent<FeatureRenderer>();
            MapData map = CreateMapWithPairedStones();

            renderer.Render(map);
            renderer.Render(map);
            map.Features[0] = new PlacedFeature(
                FeatureType.OwnMainStone,
                new Vector3(9f, 0f, 9f));
            map.Features[1] = new PlacedFeature(
                FeatureType.EnemyMainStone,
                new Vector3(1f, 0f, 1f));

            Assert.That(renderer.TryRefreshMagicStonePositions(map), Is.True);

            MagicStone[] stones = renderer.GetComponentsInChildren<MagicStone>();
            Assert.That(stones, Has.Length.EqualTo(2));
            Assert.That(FindStone(stones, 0).transform.localPosition, Is.EqualTo(new Vector3(9f, 1.65f, 9f)));
            Assert.That(FindStone(stones, 1).transform.localPosition, Is.EqualTo(new Vector3(1f, 1.65f, 1f)));

            yield return null;

            Assert.That(FindStone(renderer.GetComponentsInChildren<MagicStone>(), 0).transform.localPosition,
                Is.EqualTo(new Vector3(9f, 1.65f, 9f)));
            Assert.That(FindStone(renderer.GetComponentsInChildren<MagicStone>(), 1).transform.localPosition,
                Is.EqualTo(new Vector3(1f, 1.65f, 1f)));
        }
        finally
        {
            Object.Destroy(host);
        }
    }

    [UnityTest]
    public IEnumerator RenderMagicStones_UsesRefinedModelAndTeamCoreColors()
    {
        GameObject host = new GameObject("FeatureRendererModelPlayModeHost");
        try
        {
            FeatureRenderer renderer = host.AddComponent<FeatureRenderer>();
            renderer.Render(CreateMapWithPairedStones());

            MagicStone[] stones = renderer.GetComponentsInChildren<MagicStone>();
            Assert.That(stones, Has.Length.EqualTo(2));
            MagicStone own = FindStone(stones, 0);
            MagicStone enemy = FindStone(stones, 1);
            Assert.That(own.GetComponent<MeshFilter>(), Is.Null);
            Assert.That(enemy.GetComponent<MeshFilter>(), Is.Null);
            Assert.That(own.GetComponent<BoxCollider>(), Is.Not.Null);
            Assert.That(enemy.GetComponent<BoxCollider>(), Is.Not.Null);
            Assert.That(own.GetComponent<BoxCollider>().isTrigger, Is.False);
            Assert.That(enemy.GetComponent<BoxCollider>().isTrigger, Is.False);

            Transform ownCore = FindChildByName(own.transform, "Core");
            Transform enemyCore = FindChildByName(enemy.transform, "Core");
            Assert.That(ownCore, Is.Not.Null);
            Assert.That(enemyCore, Is.Not.Null);
            Assert.That(FindChildContainingName(own.transform, "Pedestal"), Is.Not.Null);
            Assert.That(FindChildContainingName(enemy.transform, "Pedestal"), Is.Not.Null);

            Renderer ownCoreRenderer = ownCore.GetComponentInChildren<Renderer>();
            Renderer enemyCoreRenderer = enemyCore.GetComponentInChildren<Renderer>();
            Assert.That(ownCoreRenderer.material, Is.Not.SameAs(enemyCoreRenderer.material));
            Assert.That(ReadMaterialColor(ownCoreRenderer.material).b,
                Is.GreaterThan(ReadMaterialColor(ownCoreRenderer.material).r));
            Assert.That(ReadMaterialColor(enemyCoreRenderer.material).r,
                Is.GreaterThan(ReadMaterialColor(enemyCoreRenderer.material).b));

            Renderer ownPedestalRenderer = FindChildContainingName(own.transform, "Pedestal")
                .GetComponent<Renderer>();
            Renderer enemyPedestalRenderer = FindChildContainingName(enemy.transform, "Pedestal")
                .GetComponent<Renderer>();
            Assert.That(ownPedestalRenderer, Is.Not.Null);
            Assert.That(enemyPedestalRenderer, Is.Not.Null);
            Assert.That(ownPedestalRenderer.sharedMaterial, Is.SameAs(enemyPedestalRenderer.sharedMaterial));

            yield return null;
        }
        finally
        {
            Object.Destroy(host);
        }
    }

    private static MagicStone FindStone(MagicStone[] stones, int featureIndex)
    {
        for (int i = 0; i < stones.Length; i++)
        {
            if (stones[i].FeatureIndex == featureIndex) return stones[i];
        }

        return null;
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        if (root.name == name || root.name.StartsWith(name + ".")) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform match = FindChildByName(root.GetChild(i), name);
            if (match != null) return match;
        }

        return null;
    }

    private static Transform FindChildContainingName(Transform root, string text)
    {
        if (root.name.Contains(text)) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform match = FindChildContainingName(root.GetChild(i), text);
            if (match != null) return match;
        }

        return null;
    }

    private static Color ReadMaterialColor(Material material)
    {
        if (material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
        if (material.HasProperty("_Color")) return material.GetColor("_Color");
        return material.color;
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return (T)field.GetValue(target);
    }

    private static MapData CreateMapWithPairedStones()
    {
        return CreateMapWithPairedStones(
            new Vector3(1f, 0f, 1f),
            new Vector3(9f, 0f, 9f));
    }

    private static MapData CreateMapWithPairedStones(Vector3 ownPosition, Vector3 enemyPosition)
    {
        MapData map = new MapData(
            new HeightMap(12, 12, 1f),
            new GroundStateGrid(12, 12, 1f),
            seed: 1);
        map.AddFeature(new PlacedFeature(FeatureType.OwnMainStone, ownPosition));
        map.AddFeature(new PlacedFeature(FeatureType.EnemyMainStone, enemyPosition));
        return map;
    }
}
