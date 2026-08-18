using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using WarSimulation.Combat.Map;

public sealed class CombatMapSelectionTests
{
    private const string PrefabPath = "Assets/Prefabs/Combat/BattleFlow/CharacterSelectionPanel.prefab";
    private const string FontPath = "Assets/Fonts/Noto_Sans_JP/static/NotoSansJP-Regular SDF.asset";

    [Test]
    public void CombatMapAvailability_ReportsRequiredBakeAndStoneFailures()
    {
        using var fixture = new MapFixture();

        fixture.Definition.SharedConfig = null;
        AssertReason(fixture.Definition, false, CombatMapUnavailableReason.MissingSharedConfig);
        fixture.Definition.SharedConfig = fixture.Config;

        fixture.Definition.MagicStones.Clear();
        AssertReason(fixture.Definition, false, CombatMapUnavailableReason.MissingOwnMainStone);
        fixture.AddStone(FeatureType.OwnMainStone, new Vector2(1f, 1f));
        AssertReason(fixture.Definition, false, CombatMapUnavailableReason.MissingEnemyMainStone);
        fixture.AddStone(FeatureType.EnemyMainStone, new Vector2(6f, 6f));
        AssertReason(fixture.Definition, false, CombatMapUnavailableReason.MissingBakedMapData);

        fixture.CaptureMap();
        AssertReason(fixture.Definition, false, CombatMapUnavailableReason.MissingBakedNavMesh);
        fixture.SetNavMesh();
        AssertReason(fixture.Definition, false, CombatMapUnavailableReason.MissingAssaultRoutes);
        fixture.SetRoutes();
        AssertReason(fixture.Definition, false, CombatMapUnavailableReason.MissingPreview);
        fixture.SetPreview();
        Assert.That(CombatMapAvailability.Evaluate(fixture.Definition, false).CanStartBattle, Is.True);

        fixture.AddStone(FeatureType.OwnMainStone, new Vector2(2f, 2f));
        fixture.CaptureMap();
        fixture.SetNavMesh();
        fixture.SetRoutes();
        fixture.SetPreview();
        AssertReason(fixture.Definition, true, CombatMapUnavailableReason.StonePairCountMismatch);
    }

    [Test]
    public void CombatMapAvailability_ReportsStaleBakeInDependencyOrder()
    {
        using var fixture = new MapFixture();
        fixture.AddPairedStones();
        fixture.CaptureMap();
        fixture.SetNavMesh();
        fixture.SetRoutes();
        fixture.SetPreview();

        fixture.Definition.BuildSeed++;
        AssertReason(fixture.Definition, false, CombatMapUnavailableReason.StaleBakedMapData);

        fixture.CaptureMap();
        AssertReason(fixture.Definition, false, CombatMapUnavailableReason.StaleBakedNavMesh);
        fixture.SetNavMesh();
        AssertReason(fixture.Definition, false, CombatMapUnavailableReason.StaleAssaultRoutes);
        fixture.SetRoutes();
        AssertReason(fixture.Definition, false, CombatMapUnavailableReason.StalePreview);
        fixture.SetPreview();
        Assert.That(CombatMapAvailability.Evaluate(fixture.Definition, false).CanStartBattle, Is.True);
    }

    [Test]
    public void CharacterSelectionPrefab_ContainsConfiguredMapSelectionHierarchyAndMaps()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        Transform featureRoot = prefab.transform.Find("MapSelectionFeatureRoot");
        Assert.That(featureRoot, Is.Not.Null);
        Assert.That(featureRoot.Find("MapSummaryBar/OpenSelectionButton/MapNameText"), Is.Not.Null);
        Assert.That(featureRoot.Find("MapSelectionOverlay/SelectionPanel/PreviewFrame/PreviewImage"), Is.Not.Null);
        Assert.That(featureRoot.Find("MapSelectionOverlay/SelectionPanel/PreviewFrame/MagicStoneMarkerLayer"), Is.Not.Null);

        CombatMapSelectionView view = featureRoot.GetComponent<CombatMapSelectionView>();
        List<AuthoredMapDefinition> maps = GetPrivateField<List<AuthoredMapDefinition>>(view, "_mapOptions");
        Assert.That(maps, Has.Count.EqualTo(10));
        for (int i = 0; i < maps.Count; i++)
        {
            Assert.That(maps[i], Is.Not.Null);
            Assert.That(CombatMapAvailability.Evaluate(maps[i], false).CanStartBattle, Is.True, maps[i].name);
        }

        TMP_Text[] labels = featureRoot.GetComponentsInChildren<TMP_Text>(includeInactive: true);
        Assert.That(labels, Is.Not.Empty);
        for (int i = 0; i < labels.Length; i++)
            Assert.That(AssetDatabase.GetAssetPath(labels[i].font), Is.EqualTo(FontPath));
    }

    [Test]
    public void CharacterSelectionPrefab_MapSummaryIsLabeledAndDoesNotOverlapTitleOrStatus()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        RectTransform title = prefab.transform.Find("Title").GetComponent<RectTransform>();
        RectTransform summary = prefab.transform.Find("MapSelectionFeatureRoot/MapSummaryBar")
            .GetComponent<RectTransform>();
        RectTransform openButton = summary.Find("OpenSelectionButton").GetComponent<RectTransform>();
        RectTransform status = summary.Find("AvailabilityText").GetComponent<RectTransform>();

        float titleRight = title.anchoredPosition.x + title.rect.width * (1f - title.pivot.x);
        float summaryLeft = summary.anchoredPosition.x - summary.rect.width * summary.pivot.x;
        float buttonBottom = openButton.anchoredPosition.y - openButton.rect.height * openButton.pivot.y;
        float statusTop = status.anchoredPosition.y + status.rect.height * (1f - status.pivot.y);

        Assert.That(summaryLeft, Is.GreaterThanOrEqualTo(titleRight));
        Assert.That(statusTop, Is.LessThanOrEqualTo(buttonBottom));

        GameObject instance = Object.Instantiate(prefab);
        try
        {
            CombatMapSelectionView view = instance.GetComponentInChildren<CombatMapSelectionView>(true);
            List<AuthoredMapDefinition> maps = GetPrivateField<List<AuthoredMapDefinition>>(view, "_mapOptions");
            view.Initialize(maps[0], false);
            TMP_Text label = instance.transform
                .Find("MapSelectionFeatureRoot/MapSummaryBar/OpenSelectionButton/MapNameText")
                .GetComponent<TMP_Text>();
            Assert.That(label.text, Is.EqualTo($"マップ選択：{maps[0].name}"));
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void MapSelectionView_PrefersCurrentMapCyclesAndSwapsPreviewMarkers()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject instance = Object.Instantiate(prefab);
        try
        {
            CombatMapSelectionView view = instance.GetComponentInChildren<CombatMapSelectionView>(true);
            List<AuthoredMapDefinition> maps = GetPrivateField<List<AuthoredMapDefinition>>(view, "_mapOptions");
            AuthoredMapDefinition preferred = maps[3];
            view.Initialize(preferred, false);
            Assert.That(view.SelectedMap, Is.SameAs(preferred));
            Assert.That(view.CanStartBattle, Is.True);

            InvokePrivate(view, "SelectNext");
            Assert.That(view.SelectedMap, Is.SameAs(maps[4]));

            view.Initialize(maps[maps.Count - 1], false);
            InvokePrivate(view, "SelectNext");
            Assert.That(view.SelectedMap, Is.SameAs(maps[0]));

            AuthoredMapDefinition selected = view.SelectedMap;
            List<Vector2> ownCenters = Centers(selected, FeatureType.OwnMainStone);
            List<Vector2> enemyCenters = Centers(selected, FeatureType.EnemyMainStone);
            Assert.That(ownCenters.Count, Is.EqualTo(enemyCenters.Count));
            Assert.That(ownCenters, Is.Not.Empty);

            List<Image> markers = GetPrivateField<List<Image>>(view, "_markers");
            float worldSize = selected.SharedConfig.WorldSize;
            Assert.That(markers[0].rectTransform.anchorMin, Is.EqualTo(ownCenters[0] / worldSize));
            view.SetStonePositionsReversed(true);
            Assert.That(markers[0].rectTransform.anchorMin, Is.EqualTo(enemyCenters[0] / worldSize));
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void CombatFlow_ShowSelectionPreservesSelectedMapAndReversedMarkers()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject panel = Object.Instantiate(prefab);
        GameObject flowObject = new GameObject("CombatFlow");
        GameObject systemsObject = new GameObject("CombatSystems");
        try
        {
            CombatMapSelectionView view = panel.GetComponentInChildren<CombatMapSelectionView>(true);
            List<AuthoredMapDefinition> maps = GetPrivateField<List<AuthoredMapDefinition>>(view, "_mapOptions");
            view.Initialize(maps[5], false);
            view.SetStonePositionsReversed(true);
            Vector2 markerPosition = GetPrivateField<List<Image>>(view, "_markers")[0].rectTransform.anchorMin;

            CombatFlow flow = flowObject.AddComponent<CombatFlow>();
            SetPrivateField(flow, "_battleFlow", systemsObject.AddComponent<CombatBattleFlow>());
            SetPrivateField(flow, "_characterSystem", systemsObject.AddComponent<CombatCharacterSystem>());
            SetPrivateField(flow, "_mapSelectionView", view);
            SetPrivateField(flow, "_characterSelectionPanel", panel);

            InvokePrivate(flow, "ShowSelection");

            Assert.That(view.SelectedMap, Is.SameAs(maps[5]));
            Assert.That(GetPrivateField<List<Image>>(view, "_markers")[0].rectTransform.anchorMin,
                Is.EqualTo(markerPosition));
        }
        finally
        {
            Object.DestroyImmediate(systemsObject);
            Object.DestroyImmediate(flowObject);
            Object.DestroyImmediate(panel);
        }
    }

    private static List<Vector2> Centers(AuthoredMapDefinition definition, FeatureType type)
    {
        var centers = new List<Vector2>();
        for (int i = 0; i < definition.MagicStones.Count; i++)
        {
            AuthoredMagicStonePlacement stone = definition.MagicStones[i];
            if (stone != null && stone.Type == type) centers.Add(stone.Center);
        }

        return centers;
    }

    private static void AssertReason(
        AuthoredMapDefinition definition,
        bool reversed,
        CombatMapUnavailableReason expected)
    {
        Assert.That(CombatMapAvailability.Evaluate(definition, reversed).Reason, Is.EqualTo(expected));
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        return (T)target.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(target);
    }

    private static void InvokePrivate(object target, string methodName)
    {
        target.GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(target, null);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        target.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(target, value);
    }

    private sealed class MapFixture : System.IDisposable
    {
        public MapConfig Config { get; }
        public AuthoredMapDefinition Definition { get; }

        private BakedMapData _bakedMap;
        private NavMeshData _navMesh;
        private Texture2D _preview;

        public MapFixture()
        {
            Config = ScriptableObject.CreateInstance<MapConfig>();
            SetPrivateField(Config, "_worldSize", 8f);
            SetPrivateField(Config, "_cellsPerSide", 8);
            SetPrivateField(Config, "_rockCount", 0);
            Definition = ScriptableObject.CreateInstance<AuthoredMapDefinition>();
            Definition.SharedConfig = Config;
        }

        public void AddPairedStones()
        {
            AddStone(FeatureType.OwnMainStone, new Vector2(1f, 1f));
            AddStone(FeatureType.EnemyMainStone, new Vector2(6f, 6f));
        }

        public void AddStone(FeatureType type, Vector2 center)
        {
            Definition.MagicStones.Add(new AuthoredMagicStonePlacement { Type = type, Center = center });
        }

        public void CaptureMap()
        {
            if (_bakedMap == null) _bakedMap = ScriptableObject.CreateInstance<BakedMapData>();
            _bakedMap.Capture(AuthoredMapBuilder.Build(Definition), Definition.ComputeBakeFingerprint());
            Definition.SetBakedMapData(_bakedMap);
        }

        public void SetNavMesh()
        {
            if (_navMesh == null) _navMesh = new NavMeshData();
            Definition.SetBakedNavMesh(_navMesh, Definition.ComputeBakeFingerprint());
        }

        public void SetRoutes()
        {
            Definition.SetBakedAssaultRoutes(
                new List<AuthoredBakedAssaultRoute>(),
                new List<AuthoredBakedAssaultRoute>(),
                Definition.ComputeBakeFingerprint());
        }

        public void SetPreview()
        {
            if (_preview == null) _preview = new Texture2D(2, 2);
            Definition.SetBakedPreview(_preview, Definition.ComputeBakeFingerprint());
        }

        public void Dispose()
        {
            Object.DestroyImmediate(_preview);
            Object.DestroyImmediate(_navMesh);
            Object.DestroyImmediate(_bakedMap);
            Object.DestroyImmediate(Definition);
            Object.DestroyImmediate(Config);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }
    }
}
