using System.Reflection;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using WarSimulation.Combat.Map;

public sealed class FeatureRendererPlayModeTests
{
    [UnityTest]
    public IEnumerator CombatCamera_SynchronizesEditorStyleStateWhenSwitchingPositions()
    {
        GameObject cameraObject = new GameObject("MainCamera");
        GameObject flowObject = new GameObject("CombatFlow");
        try
        {
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            EditorStyleCameraController controller = cameraObject.AddComponent<EditorStyleCameraController>();
            CombatFlow flow = flowObject.AddComponent<CombatFlow>();
            flow.enabled = false;
            MethodInfo applyCamera = typeof(CombatFlow).GetMethod(
                "ApplyCombatCamera",
                BindingFlags.Instance | BindingFlags.NonPublic);

            yield return null;

            applyCamera.Invoke(flow, new object[] { true });
            Assert.That(camera.transform.position, Is.EqualTo(new Vector3(30f, 20f, 70f)));
            Assert.That(camera.transform.eulerAngles.y, Is.EqualTo(180f).Within(0.001f));
            Assert.That(GetPrivateField<float>(controller, "yaw"), Is.EqualTo(180f).Within(0.001f));

            applyCamera.Invoke(flow, new object[] { false });
            Assert.That(camera.transform.position, Is.EqualTo(new Vector3(30f, 20f, -10f)));
            Assert.That(camera.transform.eulerAngles.y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(GetPrivateField<float>(controller, "yaw"), Is.EqualTo(0f).Within(0.001f));
        }
        finally
        {
            Object.Destroy(flowObject);
            Object.Destroy(cameraObject);
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

    private static MagicStone FindStone(MagicStone[] stones, int featureIndex)
    {
        for (int i = 0; i < stones.Length; i++)
        {
            if (stones[i].FeatureIndex == featureIndex) return stones[i];
        }

        return null;
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
        MapData map = new MapData(
            new HeightMap(12, 12, 1f),
            new GroundStateGrid(12, 12, 1f),
            seed: 1);
        map.AddFeature(new PlacedFeature(FeatureType.OwnMainStone, new Vector3(1f, 0f, 1f)));
        map.AddFeature(new PlacedFeature(FeatureType.EnemyMainStone, new Vector3(9f, 0f, 9f)));
        return map;
    }
}
