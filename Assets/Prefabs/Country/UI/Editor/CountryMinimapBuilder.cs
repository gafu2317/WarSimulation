using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CountryMinimapBuilder
{
    private const string Folder = "Assets/Prefabs/Country/UI";
    private const string MinimapPath = Folder + "/CountryMinimap.prefab";
    private const string LayoutPath = Folder + "/Minimap/CountryLayout.asset";
    private const string PlanPath = "Assets/Data/CountryComparison/town_plan.json";

    [Serializable] private sealed class Point { public float x, z; public Vector2 Position => new(x, z); }
    [Serializable] private sealed class Road { public float width; public bool avenue, paved, gravel; public Point[] points, outline; }
    [Serializable] private sealed class Plot { public string name; public float x, z, width, depth, yaw; public Point[] points; }
    [Serializable] private sealed class Plan
    {
        public bool pavedTown;
        public float riverZ, riverWidth;
        public Point[] wallOutline;
        public Road[] roads, accessPaths, frontages, secondaryAccessPaths, amenityPaths;
        public Plot[] buildings, squares, greens;
    }

    [MenuItem("WarSim/Country/Set Up Minimap")]
    public static void Build()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/Country.unity")
            throw new InvalidOperationException("Open the Country scene outside Play mode first.");
        var (saved, bounds) = RefreshLayout();
        BuildPrefab(saved, bounds);
        using (var scope = new PrefabUtility.EditPrefabContentsScope(Folder + "/CountryHud.prefab"))
        {
            var minimap = scope.prefabContentsRoot.transform.Find("Minimap");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MinimapPath);
            if (minimap == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scope.prefabContentsRoot.transform);
                instance.name = "Minimap";
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
                minimap = instance.transform;
            }
            var prefabRect = prefab.GetComponent<RectTransform>();
            ((RectTransform)minimap).anchoredPosition = prefabRect.anchoredPosition;
            ((RectTransform)minimap).sizeDelta = prefabRect.sizeDelta;
            PrefabUtility.RecordPrefabInstancePropertyModifications(minimap);
        }
        AssetDatabase.SaveAssets();
        var country = Object.FindAnyObjectByType<CountryComparisonView>();
        var viewport = Object.FindAnyObjectByType<CountryMinimapViewport>(FindObjectsInactive.Include);
        if (country != null && viewport != null)
        {
            var data = new SerializedObject(viewport);
            var camera = data.FindProperty("_viewCamera");
            var comparison = data.FindProperty("_country");
            if (camera.objectReferenceValue == null) camera.objectReferenceValue = country.viewCamera;
            if (comparison.objectReferenceValue == null) comparison.objectReferenceValue = country;
            data.ApplyModifiedProperties();
        }
        Debug.Log("Country minimap prefab updated.");
    }

    public static (Mesh mesh, Rect bounds) RefreshLayout()
        => RefreshLayout(File.ReadAllText(PlanPath), LayoutPath);

    public static void BindModernLayout(string json, CountryComparisonView view)
    {
        var map = Object.FindAnyObjectByType<CountryMinimapMapGraphic>(FindObjectsInactive.Include);
        if (map == null) throw new InvalidOperationException("Country minimap is missing.");
        var (mesh, _) = RefreshLayout(json, Folder + "/Minimap/ApprovedTownLayout.asset");
        Undo.RecordObject(map, "Bind modern town minimap");
        map.BindModernLayout(mesh, view);
        PrefabUtility.RecordPrefabInstancePropertyModifications(map);
    }

    private static (Mesh mesh, Rect bounds) RefreshLayout(string json, string path)
    {
        var plan = JsonUtility.FromJson<Plan>(json);
        Vector2 min = new(plan.wallOutline.Min(p => p.x) - 8, plan.wallOutline.Min(p => p.z) - 8);
        Vector2 max = new(plan.wallOutline.Max(p => p.x) + 8, plan.wallOutline.Max(p => p.z) + 8);
        Rect bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        var layout = BuildLayout(plan, bounds);
        if (!AssetDatabase.IsValidFolder(Folder + "/Minimap")) AssetDatabase.CreateFolder(Folder, "Minimap");
        var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (saved == null) { AssetDatabase.CreateAsset(layout, path); saved = layout; }
        else { EditorUtility.CopySerialized(layout, saved); Object.DestroyImmediate(layout); }
        EditorUtility.SetDirty(saved);
        return (saved, bounds);
    }

    private static Mesh BuildLayout(Plan plan, Rect bounds)
    {
        var vertices = new List<Vector3>();
        var colors = new List<Color32>();
        var triangles = new List<int>();
        void Polygon(Vector2[] points, Color32 color)
        {
            int start = vertices.Count;
            foreach (Vector2 point in points)
            {
                vertices.Add(new Vector3((point.x - bounds.xMin) / bounds.width, (point.y - bounds.yMin) / bounds.height, 0));
                colors.Add(color);
            }
            for (int i = 1; i < points.Length - 1; i++)
                triangles.AddRange(new[] { start, start + i, start + i + 1 });
        }
        void Segment(Vector2 a, Vector2 b, float width, Color32 color)
        {
            Vector2 direction = (b - a).normalized;
            Vector2 normal = new(-direction.y * width * .5f, direction.x * width * .5f);
            Polygon(new[] { a - normal, a + normal, b + normal, b - normal }, color);
        }
        void Rectangle(Plot plot, float border, Color32 color)
        {
            Vector2 center = new(plot.x, plot.z);
            Quaternion rotation = Quaternion.Euler(0, 0, -plot.yaw);
            Vector2 half = new(plot.width * .5f + border, plot.depth * .5f + border);
            Polygon(new[] { new Vector2(-half.x, -half.y), new Vector2(-half.x, half.y), half,
                new Vector2(half.x, -half.y) }.Select(p => center + (Vector2)(rotation * p)).ToArray(), color);
        }
        Polygon(new[] { bounds.min, new Vector2(bounds.xMin, bounds.yMax), bounds.max, new Vector2(bounds.xMax, bounds.yMin) }, new Color32(46, 60, 55, 255));
        var soil = new Color32(82, 73, 59, 255);
        var cobble = new Color32(122, 139, 130, 255);
        Polygon(plan.wallOutline.Select(p => p.Position).ToArray(), plan.pavedTown ? new Color32(58, 78, 67, 255) : soil);
        var court = plan.pavedTown ? cobble : new Color32(103, 88, 69, 255);
        foreach (var square in plan.squares)
            if (square.points is { Length: >= 3 }) Polygon(square.points.Select(p => p.Position).ToArray(), court);
            else Rectangle(square, 0, court);
        foreach (var green in plan.greens) Rectangle(green, 0, new Color32(77, 94, 70, 255));
        Segment(new Vector2(bounds.xMin, plan.riverZ), new Vector2(bounds.xMax, plan.riverZ), plan.riverWidth, new Color32(70, 117, 130, 255));
        void DrawRoad(Road road, Color32 color)
        {
            if (road.outline is { Length: >= 3 })
            {
                Polygon(road.outline.Select(p => p.Position).ToArray(), color);
                return;
            }
            for (int i = 1; i < road.points.Length; i++)
                Segment(road.points[i - 1].Position, road.points[i].Position, road.width, color);
        }
        foreach (var approach in plan.accessPaths.Concat(plan.secondaryAccessPaths ?? Array.Empty<Road>()))
            DrawRoad(approach, plan.pavedTown || approach.paved ? cobble : new Color32(120, 105, 85, 255));
        foreach (var approach in plan.amenityPaths ?? Array.Empty<Road>())
            DrawRoad(approach, approach.gravel ? new Color32(120, 105, 85, 255) : cobble);
        foreach (var road in plan.frontages) DrawRoad(road, cobble);
        foreach (var road in plan.roads.OrderBy(road => road.avenue))
            DrawRoad(road, road.avenue ? new Color32(171, 175, 160, 255) : cobble);
        foreach (var building in plan.buildings)
        {
            Rectangle(building, .8f, new Color32(39, 53, 57, 255));
            Rectangle(building, 0, building.name == "Royal_Castle" ? new Color32(207, 187, 141, 255) : new Color32(165, 183, 186, 255));
            if (building.name != "Royal_Castle") continue;
            foreach (float x in new[] { -1f, 1f })
                foreach (float z in new[] { -1f, 1f })
                {
                    var tower = new Plot { x = building.x + x * building.width * .48f, z = building.z + z * building.depth * .48f, width = 5, depth = 5 };
                    Rectangle(tower, 1, new Color32(39, 53, 57, 255));
                    Rectangle(tower, 0, new Color32(224, 204, 157, 255));
                }
        }
        for (int i = 0; i < plan.wallOutline.Length; i++)
            Segment(plan.wallOutline[i].Position, plan.wallOutline[(i + 1) % plan.wallOutline.Length].Position, 3, new Color32(139, 157, 150, 255));
        var mesh = new Mesh { name = "Country cartographic layout" };
        mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void BuildPrefab(Mesh layout, Rect bounds)
    {
        bool exists = File.Exists(MinimapPath);
        GameObject root = exists ? PrefabUtility.LoadPrefabContents(MinimapPath) : new GameObject("Minimap", typeof(RectTransform));
        try
        {
            root.layer = LayerMask.NameToLayer("UI");
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(0, -144);
            rect.sizeDelta = new Vector2(264, 316);
            Image(rect, new Color32(105, 123, 135, 235), true);
            Image(Fill(root.transform, "Panel", new Vector2(2, 2), new Vector2(-2, -2)), new Color32(82, 96, 111, 235));
            var frame = Fill(root.transform, "MapFrame", new Vector2(14, 14), new Vector2(-14, -14));
            Image(frame, new Color32(129, 148, 148, 255));
            var mapArea = Fill(frame, "MapArea", Vector2.one * 5, Vector2.one * -5);
            var previousMap = frame.Find("Map");
            if (previousMap != null) previousMap.SetParent(mapArea, false);
            var map = Fill(mapArea, "Map", Vector2.zero, Vector2.zero);
            var aspect = map.GetComponent<UnityEngine.UI.AspectRatioFitter>() ?? map.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
            aspect.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = bounds.width / bounds.height;
            if (!map.TryGetComponent<CanvasRenderer>(out _)) map.gameObject.AddComponent<CanvasRenderer>();
            var graphic = map.GetComponent<CountryMinimapMapGraphic>() ?? map.gameObject.AddComponent<CountryMinimapMapGraphic>();
            graphic.SetLayout(layout); graphic.raycastTarget = false;
            if (!map.TryGetComponent<UnityEngine.UI.RectMask2D>(out _)) map.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var viewportRect = Fill(map, "VisibleArea", Vector2.zero, Vector2.zero);
            if (!viewportRect.TryGetComponent<CanvasRenderer>(out _)) viewportRect.gameObject.AddComponent<CanvasRenderer>();
            var viewport = viewportRect.GetComponent<CountryMinimapViewport>() ?? viewportRect.gameObject.AddComponent<CountryMinimapViewport>();
            viewport.raycastTarget = false;
            viewport.color = new Color32(190, 228, 238, 255);
            var serialized = new SerializedObject(viewport);
            serialized.FindProperty("_countryBounds").rectValue = bounds;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Noto_Sans_JP/static/NotoSansJP-Regular SDF.asset");
            if (!font.HasCharacters("N") && !font.TryAddCharacters("N", out string missing))
                throw new InvalidOperationException("Missing minimap font glyphs: " + missing);
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/NotoSansJP-Regular SDF - Country HUD.mat");
            var caption = root.transform.Find("Caption");
            if (caption != null) caption.gameObject.SetActive(false);
            var north = Text(Child(map, "North"), "N", font, material, 22);
            north.alignment = TextAlignmentOptions.Center;
            north.rectTransform.anchorMin = north.rectTransform.anchorMax = north.rectTransform.pivot = Vector2.one;
            north.rectTransform.anchoredPosition = new Vector2(-6, -4);
            north.rectTransform.sizeDelta = new Vector2(26, 28);
            var arrow = Child(map, "NorthArrow");
            arrow.anchorMin = arrow.anchorMax = Vector2.one;
            arrow.pivot = Vector2.one * .5f;
            arrow.anchoredPosition = new Vector2(-19, -38);
            arrow.sizeDelta = new Vector2(14, 14);
            arrow.localRotation = Quaternion.Euler(0, 0, 90);
            var arrowImage = arrow.GetComponent<UnityEngine.UI.Image>() ?? arrow.gameObject.AddComponent<UnityEngine.UI.Image>();
            arrowImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Icons/Country/Controls/play-v1.png");
            arrowImage.color = new Color32(211, 224, 225, 255); arrowImage.raycastTarget = false;
            CountryTextLayoutBuilder.Minimap(root.transform);
            CountryPaletteBuilder.Apply(root); PrefabUtility.SaveAsPrefabAsset(root, MinimapPath);
        }
        finally
        {
            if (exists) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);
        }
    }

    private static RectTransform Child(Transform parent, string name)
    {
        if (parent.Find(name) is RectTransform existing) return existing;
        var child = new GameObject(name, typeof(RectTransform));
        child.layer = LayerMask.NameToLayer("UI"); child.transform.SetParent(parent, false);
        return child.GetComponent<RectTransform>();
    }

    private static RectTransform Fill(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var rect = Child(parent, name); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = min; rect.offsetMax = max;
        return rect;
    }

    private static void Image(RectTransform rect, Color32 tint, bool blocksInput = false)
    {
        var image = rect.GetComponent<UnityEngine.UI.Image>() ?? rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/Shapes/RoundedPanel.png");
        image.type = UnityEngine.UI.Image.Type.Sliced; image.color = tint; image.raycastTarget = blocksInput;
    }

    private static TextMeshProUGUI Text(RectTransform rect, string text, TMP_FontAsset font, Material material, float size)
    {
        var label = rect.GetComponent<TextMeshProUGUI>() ?? rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text; label.font = font; label.fontSharedMaterial = material;
        label.fontSize = size; label.fontStyle = FontStyles.Normal; label.enableAutoSizing = false;
        label.color = CountryGovernmentTheme.Text; label.alignment = TextAlignmentOptions.MidlineLeft;
        label.raycastTarget = false;
        return label;
    }
}
