#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WarSimulation.Combat.Map;

namespace WarSimulation.Combat.Map.EditorOnly
{
    public enum MapAuthoringTool
    {
        Select = 0,
        Mountain,
        Lake,
        GroundPatch,
        Forest,
        River,
        Bridge,
        MagicStone,
        AssaultRoute,
    }

    public enum MapAuthoringSelectionKind
    {
        None = 0,
        Mountain,
        Lake,
        GroundPatch,
        Forest,
        River,
        Bridge,
        MagicStone,
        AssaultRoute,
        Rock,
        ScatterTree,
        ForestTree,
    }

    public enum MapAuthoringRightTab
    {
        Placement = 0,
        Shared = 1,
        Stamps = 2,
    }

    public enum MapAuthoringStampKind
    {
        Mountain = 0,
        Lake = 1,
        GroundPatch = 2,
        Forest = 3,
        River = 4,
    }

    public sealed class MapAuthoringWindow : EditorWindow
    {
        private const string DefaultFolder = "Assets/Data/Map/Map/Authored";
        private const string StampRoot = "Assets/Data/Map/Map/Stamps";
        private const string StampHeightFolder = "Assets/Data/Map/Map/Stamps/Height";
        private const string StampLakeFolder = "Assets/Data/Map/Map/Stamps/Lake";
        private const string StampBiomeFolder = "Assets/Data/Map/Map/Stamps/Biome";
        private const string StampRiverFolder = "Assets/Data/Map/Map/Stamps/River";
        private const string DefaultConfigPath = "Assets/Data/Map/Map/Configs/MapGenerationConfig.asset";
        private const float RightPanelWidth = 300f;
        private const float PickRadiusMeters = 3f;
        private const float BridgeSnapRadiusMeters = 8f;
        private const double PreviewDebounceSeconds = 0.2;

        private AuthoredMapDefinition _definition;
        private MapAuthoringTool _tool = MapAuthoringTool.Select;
        private MapAuthoringSelectionKind _selectionKind;
        private int _selectionIndex = -1;
        private int _selectionForestIndex = -1;
        private bool _pickFixedFeatures;
        private MapAuthoringRightTab _rightTab = MapAuthoringRightTab.Placement;
        private MapAuthoringStampKind _stampKind = MapAuthoringStampKind.Mountain;

        private HeightStampShape _selectedMountain;
        private LakeStampShape _selectedLake;
        private GroundPatchStampShape _selectedGround;
        private ForestClusterStampShape _selectedForest;
        private RiverShape _selectedRiver;
        private FeatureType _selectedMagicStoneType = FeatureType.OwnMainStone;
        private bool _lakeFrozen;
        private MountainKind _mountainKind = MountainKind.Small;
        private bool _hasPendingRiverStart;
        private Vector2 _pendingRiverStart;
        private int _selectionEndpoint = -1;
        private int _selectedAssaultRoute = -1;
        private int _selectedAssaultWaypoint = -1;
        private List<CombatAssaultRouteValidationFailure> _assaultRouteFailures = new();
        private List<AssaultRoute> _validatedPreviewRoutes = new();

        private Texture2D _previewTex;
        private MapData _lastPreviewMap;
        private double _rebuildAt;
        private bool _rebuildQueued;
        private bool _assaultRouteValidationQueued;
        private string _status;
        private Vector2 _paletteScroll;
        private Vector2 _listScroll;
        private Vector2 _panelScroll;
        private Vector2 _sharedScroll;
        private Vector2 _stampDetailScroll;
        private bool _dragging;
        private bool _assaultWaypointInsertedDuringDrag;
        private Vector2 _assaultWaypointPositionBeforeDrag;
        private AuthoredAssaultRouteSource _assaultRouteSourceBeforeDrag;
        private string _assaultRouteIdBeforeDrag;
        private AuthoredMapBakeStatus _bakeStatus;
        private Editor _sharedConfigEditor;
        private Editor _stampEditor;

        private List<HeightStampShape> _mountainStamps = new();
        private List<LakeStampShape> _lakeStamps = new();
        private List<GroundPatchStampShape> _groundStamps = new();
        private List<ForestClusterStampShape> _forestStamps = new();
        private List<RiverShape> _riverStamps = new();

        [MenuItem("WarSim/Map/マップ編集")]
        public static void Open()
        {
            var window = GetWindow<MapAuthoringWindow>("マップ編集");
            window.minSize = new Vector2(900f, 600f);
            window.Show();
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.update += OnEditorUpdate;
            ReloadStampPalette();
            QueuePreviewRebuild();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.update -= OnEditorUpdate;
            DestroyPreview();
            _lastPreviewMap = null;
            DestroyCachedEditors();
        }

        private void OnUndoRedo()
        {
            _status = null;
            _assaultRouteFailures.Clear();
            if (_definition != null && _definition.AssaultRoutes.Count > 0)
                _assaultRouteValidationQueued = true;
            QueuePreviewRebuild();
            Repaint();
        }

        private void OnEditorUpdate()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (AuthoredMapBuilder.RegenerateChangedForestTrees(_definition))
            {
                MarkDirty();
                QueuePreviewRebuild(immediate: true);
            }
            if (_assaultRouteValidationQueued)
            {
                _assaultRouteValidationQueued = false;
                ValidateAssaultRoutes();
            }
            if (!_rebuildQueued) return;
            if (EditorApplication.timeSinceStartup < _rebuildAt) return;
            _rebuildQueued = false;
            RebuildPreviewNow();
            Repaint();
        }

        private void OnGUI()
        {
            _bakeStatus = AuthoredMapBakeStatus.Evaluate(
                _definition,
                Object.FindAnyObjectByType<MapSceneHost>());
            DrawToolbar();
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawMapCanvas();
                DrawRightPanel();
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                AuthoredMapDefinition next = (AuthoredMapDefinition)EditorGUILayout.ObjectField(
                    _definition, typeof(AuthoredMapDefinition), false, GUILayout.MinWidth(220f));
                if (EditorGUI.EndChangeCheck())
                {
                    _definition = next;
                    ClearSelection();
                    ClearPendingRiverStart();
                    DestroyCachedEditors();
                    QueuePreviewRebuild(immediate: true);
                    if (_tool == MapAuthoringTool.AssaultRoute &&
                        _definition != null && _definition.AssaultRoutes.Count > 0)
                    {
                        _assaultRouteValidationQueued = true;
                    }
                }

                if (GUILayout.Button("新規", EditorStyles.toolbarButton, GUILayout.Width(44f)))
                    CreateNewAsset();
                if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(44f)))
                    SaveAsset();
                if (GUILayout.Button("スタンプ再読込", EditorStyles.toolbarButton, GUILayout.Width(100f)))
                    ReloadStampPalette();

                GUILayout.FlexibleSpace();
                Color previousBackground = GUI.backgroundColor;
                if (RequiresGeometryBake()) GUI.backgroundColor = new Color(1f, 0.72f, 0.25f);
                if (GUILayout.Button("シーンへ3D反映", EditorStyles.toolbarButton, GUILayout.Width(110f)))
                    ApplyToScene3D();
                GUI.backgroundColor = previousBackground;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                DrawToolToggle(MapAuthoringTool.Select, "選択");
                DrawToolToggle(MapAuthoringTool.Mountain, "山");
                DrawToolToggle(MapAuthoringTool.Lake, "湖");
                DrawToolToggle(MapAuthoringTool.GroundPatch, "沼・雪");
                DrawToolToggle(MapAuthoringTool.Forest, "森");
                DrawToolToggle(MapAuthoringTool.River, "川");
                DrawToolToggle(MapAuthoringTool.Bridge, "橋");
                DrawToolToggle(MapAuthoringTool.MagicStone, "魔石");
                DrawToolToggle(MapAuthoringTool.AssaultRoute, "侵攻ルート");
                if (_tool == MapAuthoringTool.Select && _definition?.HasFixedFeaturePlacements == true)
                {
                    bool features = GUILayout.Toggle(
                        _pickFixedFeatures, "岩・木", EditorStyles.toolbarButton, GUILayout.Width(58f));
                    if (features != _pickFixedFeatures)
                    {
                        _pickFixedFeatures = features;
                        ClearSelection();
                    }
                }
                GUILayout.FlexibleSpace();
                if (!string.IsNullOrEmpty(_status))
                    GUILayout.Label(_status, EditorStyles.miniLabel);
            }
        }

        private void DrawToolToggle(MapAuthoringTool tool, string label)
        {
            bool on = _tool == tool;
            if (GUILayout.Toggle(on, label, EditorStyles.toolbarButton, GUILayout.Width(72f)) && !on)
            {
                _tool = tool;
                ClearPendingRiverStart();
                SyncStampKindFromTool();
                if (tool == MapAuthoringTool.AssaultRoute &&
                    _definition != null && _definition.AssaultRoutes.Count > 0)
                {
                    _assaultRouteValidationQueued = true;
                }
            }
        }

        private void DrawMapCanvas()
        {
            Rect rect = GUILayoutUtility.GetRect(
                GUIContent.none, GUIStyle.none,
                GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f, 1f));
            if (rect.width < 32f || rect.height < 32f)
                return;

            float world = GetWorldSize();
            // 上下の説明ラベル分も空けて、マップ周りに余白を残す。
            const float pad = 36f;
            const float topLabelReserve = 44f;
            float availW = Mathf.Max(1f, rect.width - pad * 2f);
            float availH = Mathf.Max(1f, rect.height - pad * 2f - topLabelReserve);
            float side = Mathf.Min(availW, availH);
            var drawRect = new Rect(
                rect.x + (rect.width - side) * 0.5f,
                rect.y + topLabelReserve + (rect.height - topLabelReserve - side) * 0.5f,
                side,
                side);

            // はみ出し描画を防ぐ
            GUI.BeginClip(rect);
            var localDraw = new Rect(
                drawRect.x - rect.x,
                drawRect.y - rect.y,
                drawRect.width,
                drawRect.height);

            if (_previewTex != null)
                GUI.DrawTexture(localDraw, _previewTex, ScaleMode.ScaleToFit, false);
            else
                EditorGUI.DrawRect(localDraw, new Color(0.25f, 0.45f, 0.28f, 1f));

            DrawPlacementMarkersLocal(localDraw, world);
            DrawAssaultRoutesLocal(localDraw, world);
            DrawPendingRiverLocal(localDraw, world, rect);
            GUI.EndClip();

            // 入力はクリップ前の GUI 座標（drawRect）で扱う
            HandleCanvasInput(drawRect, world);

            GUI.Label(
                new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, 40f),
                "真上からの2D編集。スタンプ配置／川は端＋ベジェ制御点／橋は川付近クリック（自動スナップ）／魔石は手動／散布木・岩は自動／選択で移動／Delete削除",
                EditorStyles.whiteLabel);
        }

        private void DrawPendingRiverLocal(Rect localDraw, float world, Rect clipRect)
        {
            if (!_hasPendingRiverStart) return;
            Vector2 startGui = MapAuthoringPreview2D.MapToGui(localDraw, _pendingRiverStart, world);
            EditorGUI.DrawRect(new Rect(startGui.x - 6f, startGui.y - 6f, 12f, 12f), Color.yellow);

            Event e = Event.current;
            Vector2 localMouse = e.mousePosition - new Vector2(clipRect.x, clipRect.y);
            if (!MapAuthoringPreview2D.TryMapPointNearEdge(
                    localDraw, localMouse, world, edgeSlopGui: 24f, out Vector2 hover))
                return;

            hover = MapAuthoringPreview2D.SnapToNearestEdge(hover, world);
            Handles.BeginGUI();
            Handles.color = new Color(0.3f, 0.7f, 1f, 0.9f);
            if (!TryDrawMeanderGuide(localDraw, world, _pendingRiverStart, hover, (_pendingRiverStart + hover) * 0.5f))
            {
                Vector2 endGui = MapAuthoringPreview2D.MapToGui(localDraw, hover, world);
                Handles.DrawLine(startGui, endGui, 2f);
            }

            Handles.EndGUI();
        }

        private bool TryDrawMeanderGuide(
            Rect localDraw, float world, Vector2 start, Vector2 end, Vector2 control)
        {
            if (_definition?.SharedConfig == null || _lastPreviewMap?.Height == null)
                return false;

            HeightMap height = _lastPreviewMap.Height;
            Vector2Int startCell = RiverPathRasterizer.WorldToCell(height, start);
            Vector2Int endCell = RiverPathRasterizer.WorldToCell(height, end);
            MapConfig config = _definition.SharedConfig;
            int riverIndex = _definition.Rivers != null ? _definition.Rivers.Count : 0;
            float noiseSeed = AuthoredMapBuilder.HashRiverNoiseSeed(
                _definition.BuildSeed, riverIndex, startCell, endCell);
            List<Vector2Int> path = new FlatRiverPathBuilder().BuildWithBezierControl(
                height,
                startCell,
                endCell,
                control,
                config.FlatRiverMeanderAmplitude,
                config.FlatRiverMeanderFrequency,
                noiseSeed);
            if (path == null || path.Count < 2) return false;

            path[0] = startCell;
            path[path.Count - 1] = endCell;

            float cs = height.CellSize;
            Vector3 prev = default;
            bool hasPrev = false;
            for (int c = 0; c < path.Count; c++)
            {
                Vector2Int cell = path[c];
                Vector2 worldXZ = new Vector2((cell.x + 0.5f) * cs, (cell.y + 0.5f) * cs);
                Vector2 gui = MapAuthoringPreview2D.MapToGui(localDraw, worldXZ, world);
                var p = new Vector3(gui.x, gui.y, 0f);
                if (hasPrev)
                    Handles.DrawLine(prev, p, 2f);
                prev = p;
                hasPrev = true;
            }

            return true;
        }

        private void DrawPlacementMarkersLocal(Rect localDraw, float world)
        {
            if (_definition == null) return;

            void Mark(Vector2 center, Color color, bool selected)
            {
                Vector2 p = MapAuthoringPreview2D.MapToGui(localDraw, center, world);
                float size = selected ? 12f : 8f;
                EditorGUI.DrawRect(new Rect(p.x - size * 0.5f, p.y - size * 0.5f, size, size), color);
            }

            for (int i = 0; i < _definition.Mountains.Count; i++)
                Mark(_definition.Mountains[i].Center, new Color(0.55f, 0.35f, 0.15f),
                    _selectionKind == MapAuthoringSelectionKind.Mountain && _selectionIndex == i);
            for (int i = 0; i < _definition.Lakes.Count; i++)
                Mark(_definition.Lakes[i].Center, new Color(0.2f, 0.55f, 1f),
                    _selectionKind == MapAuthoringSelectionKind.Lake && _selectionIndex == i);
            for (int i = 0; i < _definition.GroundPatches.Count; i++)
                Mark(_definition.GroundPatches[i].Center, new Color(0.55f, 0.7f, 0.2f),
                    _selectionKind == MapAuthoringSelectionKind.GroundPatch && _selectionIndex == i);
            for (int i = 0; i < _definition.Forests.Count; i++)
                Mark(_definition.Forests[i].Center, new Color(0.1f, 0.45f, 0.15f),
                    _selectionKind == MapAuthoringSelectionKind.Forest && _selectionIndex == i);

            for (int i = 0; i < _definition.Rivers.Count; i++)
            {
                AuthoredRiverPlacement river = _definition.Rivers[i];
                if (!river.TryGetBezier(out Vector2 start, out Vector2 control, out Vector2 end)) continue;
                bool selectedRiver = _selectionKind == MapAuthoringSelectionKind.River && _selectionIndex == i;
                Mark(start, new Color(0.15f, 0.45f, 1f), selectedRiver && _selectionEndpoint == 0);
                Mark(control, new Color(1f, 0.75f, 0.15f), selectedRiver && _selectionEndpoint == 2);
                Mark(end, new Color(0.05f, 0.25f, 0.85f), selectedRiver && _selectionEndpoint == 1);
                if (selectedRiver)
                {
                    Vector2 a = MapAuthoringPreview2D.MapToGui(localDraw, start, world);
                    Vector2 c = MapAuthoringPreview2D.MapToGui(localDraw, control, world);
                    Vector2 b = MapAuthoringPreview2D.MapToGui(localDraw, end, world);
                    Handles.BeginGUI();
                    Handles.color = new Color(1f, 0.85f, 0.3f, 0.55f);
                    Handles.DrawDottedLine(a, c, 4f);
                    Handles.DrawDottedLine(c, b, 4f);
                    Handles.EndGUI();
                }

                DrawRiverPathOverlay(localDraw, world, i, selectedRiver);
            }

            for (int i = 0; i < _definition.Bridges.Count; i++)
            {
                AuthoredBridgePlacement bridge = _definition.Bridges[i];
                bool selected = _selectionKind == MapAuthoringSelectionKind.Bridge && _selectionIndex == i;
                Mark(bridge.Center, new Color(0.55f, 0.35f, 0.15f), selected);
                DrawBridgeOrientation(localDraw, world, bridge, selected);
            }

            for (int i = 0; i < _definition.MagicStones.Count; i++)
            {
                AuthoredMagicStonePlacement stone = _definition.MagicStones[i];
                Mark(stone.Center, MagicStoneColor(stone.Type),
                    _selectionKind == MapAuthoringSelectionKind.MagicStone && _selectionIndex == i);
            }

            if (_definition.HasFixedFeaturePlacements)
            {
                for (int i = 0; i < _definition.Rocks.Count; i++)
                    DrawFeatureMarker(localDraw, world, _definition.Rocks[i].Center, new Color(0.45f, 0.45f, 0.45f),
                        _selectionKind == MapAuthoringSelectionKind.Rock && _selectionIndex == i);
                for (int i = 0; i < _definition.Trees.Count; i++)
                    DrawFeatureMarker(localDraw, world, _definition.Trees[i].Center, new Color(0.08f, 0.62f, 0.18f),
                        _selectionKind == MapAuthoringSelectionKind.ScatterTree && _selectionIndex == i);
                for (int forest = 0; forest < _definition.Forests.Count; forest++)
                {
                    List<AuthoredPointFeaturePlacement> trees = _definition.Forests[forest].Trees;
                    if (trees == null) continue;
                    for (int i = 0; i < trees.Count; i++)
                        DrawFeatureMarker(localDraw, world, trees[i].Center, new Color(0.04f, 0.42f, 0.12f),
                            _selectionKind == MapAuthoringSelectionKind.ForestTree &&
                            _selectionForestIndex == forest && _selectionIndex == i);
                    }
                }
            else if (_lastPreviewMap != null)
            {
                for (int i = 0; i < _lastPreviewMap.Features.Count; i++)
                {
                    PlacedFeature feature = _lastPreviewMap.Features[i];
                    if (feature.Type != FeatureType.Tree && feature.Type != FeatureType.Rock) continue;
                    Color color = feature.Type == FeatureType.Rock
                        ? new Color(0.45f, 0.45f, 0.45f)
                        : new Color(0.08f, 0.62f, 0.18f);
                    DrawFeatureMarker(
                        localDraw,
                        world,
                        new Vector2(feature.WorldPosition.x, feature.WorldPosition.z),
                        color,
                        selected: false);
                }
            }
        }

        private static void DrawFeatureMarker(
            Rect localDraw,
            float world,
            Vector2 center,
            Color color,
            bool selected)
        {
            Vector2 point = MapAuthoringPreview2D.MapToGui(localDraw, center, world);
            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawSolidDisc(new Vector3(point.x, point.y), Vector3.forward, selected ? 6f : 4f);
            if (selected)
            {
                Handles.color = Color.yellow;
                Handles.DrawWireDisc(new Vector3(point.x, point.y), Vector3.forward, 7f, 2f);
            }
            Handles.EndGUI();
        }

        private void DrawAssaultRoutesLocal(Rect localDraw, float world)
        {
            if (_definition == null) return;
            var colors = new[]
            {
                new Color(1f, 0.85f, 0.2f),
                new Color(0.2f, 0.85f, 1f),
                new Color(1f, 0.35f, 0.85f),
                new Color(0.5f, 1f, 0.3f),
            };

            for (int i = 0; i < _validatedPreviewRoutes.Count; i++)
            {
                AssaultRoute route = _validatedPreviewRoutes[i];
                Color routeColor = colors[i % colors.Length];
                DrawRoutePolyline(
                    localDraw, world, route.Corners, width: 10f, new Color(0f, 0f, 0f, 0.75f));
                DrawRoutePolyline(localDraw, world, route.Corners, width: 6f, routeColor);
                if (_tool == MapAuthoringTool.AssaultRoute &&
                    _selectedAssaultRoute >= 0 &&
                    _selectedAssaultRoute < _definition.AssaultRoutes.Count &&
                    _definition.AssaultRoutes[_selectedAssaultRoute].RouteId == route.RouteId)
                {
                    DrawRouteArrow(localDraw, world, route.Corners, routeColor);
                }
            }

            if (TryGetAuthoredStoneCenter(FeatureType.OwnMainStone, out Vector2 own) &&
                TryGetAuthoredStoneCenter(FeatureType.EnemyMainStone, out Vector2 enemy))
            {
                for (int i = 0; i < _definition.AssaultRoutes.Count; i++)
                {
                    AuthoredAssaultRoute route = _definition.AssaultRoutes[i];
                    if (route == null) continue;
                    var points = new List<Vector2> { own };
                    if (route.Waypoints != null) points.AddRange(route.Waypoints);
                    points.Add(enemy);
                    CombatAssaultRouteValidationFailure? failure = FindFailure(route.RouteId);
                    if (failure.HasValue && failure.Value.SegmentIndex >= 0 &&
                        failure.Value.SegmentIndex + 1 < points.Count)
                    {
                        int segment = failure.Value.SegmentIndex;
                        Vector2 a = MapAuthoringPreview2D.MapToGui(localDraw, points[segment], world);
                        Vector2 b = MapAuthoringPreview2D.MapToGui(localDraw, points[segment + 1], world);
                        DrawGuiLine(a, b, 10f, new Color(0f, 0f, 0f, 0.75f));
                        DrawGuiLine(a, b, 6f, Color.red);
                    }

                    if (_tool != MapAuthoringTool.AssaultRoute || _selectedAssaultRoute != i) continue;
                    Color waypointColor = colors[i % colors.Length];
                    for (int p = 0; p < route.Waypoints.Count; p++)
                    {
                        Vector2 gui = MapAuthoringPreview2D.MapToGui(localDraw, route.Waypoints[p], world);
                        float size = _selectedAssaultWaypoint == p ? 12f : 8f;
                        EditorGUI.DrawRect(
                            new Rect(gui.x - size * 0.5f, gui.y - size * 0.5f, size, size),
                            _selectedAssaultWaypoint == p ? Color.white : waypointColor);
                    }

                }
            }
        }

        private static void DrawRoutePolyline(
            Rect localDraw,
            float world,
            IReadOnlyList<Vector3> corners,
            float width,
            Color color)
        {
            if (corners == null) return;
            for (int i = 0; i + 1 < corners.Count; i++)
            {
                Vector2 a = MapAuthoringPreview2D.MapToGui(
                    localDraw, new Vector2(corners[i].x, corners[i].z), world);
                Vector2 b = MapAuthoringPreview2D.MapToGui(
                    localDraw, new Vector2(corners[i + 1].x, corners[i + 1].z), world);
                DrawGuiLine(a, b, width, color);
            }
        }

        private static void DrawRouteArrow(
            Rect localDraw,
            float world,
            IReadOnlyList<Vector3> points,
            Color color)
        {
            if (points == null || points.Count < 2) return;
            Vector3 end = points[points.Count - 1];
            Vector3 beforeEnd = points[points.Count - 2];
            Vector2 tip = MapAuthoringPreview2D.MapToGui(localDraw, new Vector2(end.x, end.z), world);
            Vector2 previous = MapAuthoringPreview2D.MapToGui(
                localDraw, new Vector2(beforeEnd.x, beforeEnd.z), world);
            Vector2 direction = (tip - previous).normalized;
            Vector2 side = new(-direction.y, direction.x);
            DrawGuiLine(tip, tip - direction * 12f + side * 6f, 3f, color);
            DrawGuiLine(tip, tip - direction * 12f - side * 6f, 3f, color);
        }

        private static void DrawGuiLine(Vector2 start, Vector2 end, float width, Color color)
        {
            Vector2 delta = end - start;
            if (delta.sqrMagnitude <= Mathf.Epsilon) return;

            Matrix4x4 previousMatrix = GUI.matrix;
            try
            {
                float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                GUIUtility.RotateAroundPivot(angle, start);
                EditorGUI.DrawRect(
                    new Rect(start.x, start.y - width * 0.5f, delta.magnitude, width),
                    color);
            }
            finally
            {
                GUI.matrix = previousMatrix;
            }
        }

        private CombatAssaultRouteValidationFailure? FindFailure(string routeId)
        {
            for (int i = 0; i < _assaultRouteFailures.Count; i++)
            {
                if (_assaultRouteFailures[i].RouteId == routeId) return _assaultRouteFailures[i];
            }
            return null;
        }

        private bool TryGetAuthoredStoneCenter(FeatureType type, out Vector2 center)
        {
            center = default;
            for (int i = 0; i < _definition.MagicStones.Count; i++)
            {
                AuthoredMagicStonePlacement stone = _definition.MagicStones[i];
                if (stone == null || stone.Type != type) continue;
                center = stone.Center;
                return true;
            }
            return false;
        }

        private void HandleAssaultRouteInput(Rect drawRect, float world)
        {
            Event e = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive);
            if (e.type == EventType.KeyDown &&
                (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace))
            {
                DeleteSelectedAssaultWaypoint();
                e.Use();
                return;
            }

            if (_selectedAssaultRoute < 0 || _selectedAssaultRoute >= _definition.AssaultRoutes.Count)
                return;
            AuthoredAssaultRoute route = _definition.AssaultRoutes[_selectedAssaultRoute];
            if (route == null) return;

            if (e.type == EventType.MouseDown && e.button == 0 &&
                MapAuthoringPreview2D.TryMapPoint(drawRect, e.mousePosition, world, out Vector2 mapXZ))
            {
                int waypoint = FindNearestWaypoint(route, mapXZ);
                _assaultWaypointInsertedDuringDrag = waypoint < 0;
                _assaultWaypointPositionBeforeDrag = waypoint >= 0
                    ? route.Waypoints[waypoint]
                    : default;
                _assaultRouteSourceBeforeDrag = route.Source;
                _assaultRouteIdBeforeDrag = route.RouteId;
                RecordUndo(waypoint >= 0 ? "侵攻ルート経由点を移動" : "侵攻ルート経由点を追加");
                if (waypoint < 0)
                {
                    waypoint = FindRouteInsertionIndex(route, mapXZ);
                    route.Waypoints.Insert(waypoint, mapXZ);
                    ManualizeRoute(route);
                }
                _selectedAssaultWaypoint = waypoint;
                _dragging = true;
                GUIUtility.hotControl = id;
                MarkDirty();
                e.Use();
                return;
            }

            if (e.type == EventType.MouseDrag && _dragging && GUIUtility.hotControl == id &&
                MapAuthoringPreview2D.TryMapPoint(drawRect, e.mousePosition, world, out Vector2 dragXZ))
            {
                route.Waypoints[_selectedAssaultWaypoint] = ClampToMap(dragXZ);
                ManualizeRoute(route);
                MarkDirty();
                e.Use();
                return;
            }

            if (e.type == EventType.MouseUp && GUIUtility.hotControl == id)
            {
                _dragging = false;
                GUIUtility.hotControl = 0;
                ValidateAssaultRoutes();
                if (DidSelectedAssaultWaypointFailPlacement(route))
                {
                    RestoreAssaultWaypointBeforeDrag(route);
                    ValidateAssaultRoutes();
                    _status = "経由点をNavMesh上へ配置できないため、変更を取り消しました";
                }
                _assaultWaypointInsertedDuringDrag = false;
                e.Use();
            }
        }

        private bool DidSelectedAssaultWaypointFailPlacement(AuthoredAssaultRoute route)
        {
            if (_selectedAssaultWaypoint < 0 ||
                _selectedAssaultWaypoint >= route.Waypoints.Count)
                return false;
            CombatAssaultRouteValidationFailure? failure = FindFailure(route.RouteId);
            if (failure.HasValue && failure.Value.WaypointIndex == _selectedAssaultWaypoint)
                return true;
            if (_assaultRouteFailures.Count == 0) return false;

            MapSceneHost host = Object.FindAnyObjectByType<MapSceneHost>();
            return host != null && !CombatAssaultRouteBaker.CanPlaceWaypoint(
                host.transform,
                route.Waypoints[_selectedAssaultWaypoint]);
        }

        private void RestoreAssaultWaypointBeforeDrag(AuthoredAssaultRoute route)
        {
            if (_assaultWaypointInsertedDuringDrag)
            {
                route.Waypoints.RemoveAt(_selectedAssaultWaypoint);
                _selectedAssaultWaypoint = -1;
            }
            else
            {
                route.Waypoints[_selectedAssaultWaypoint] = _assaultWaypointPositionBeforeDrag;
            }

            route.Source = _assaultRouteSourceBeforeDrag;
            route.RouteId = _assaultRouteIdBeforeDrag;
            MarkDirty();
        }

        private int FindNearestWaypoint(AuthoredAssaultRoute route, Vector2 mapXZ)
        {
            float best = PickRadiusMeters * PickRadiusMeters;
            int result = -1;
            for (int i = 0; i < route.Waypoints.Count; i++)
            {
                float sq = (route.Waypoints[i] - mapXZ).sqrMagnitude;
                if (sq >= best) continue;
                best = sq;
                result = i;
            }
            return result;
        }

        private int FindRouteInsertionIndex(AuthoredAssaultRoute route, Vector2 point)
        {
            if (!TryGetAuthoredStoneCenter(FeatureType.OwnMainStone, out Vector2 own) ||
                !TryGetAuthoredStoneCenter(FeatureType.EnemyMainStone, out Vector2 enemy))
                return route.Waypoints.Count;
            var points = new List<Vector2> { own };
            points.AddRange(route.Waypoints);
            points.Add(enemy);
            float best = float.PositiveInfinity;
            int result = route.Waypoints.Count;
            for (int i = 0; i + 1 < points.Count; i++)
            {
                float distance = DistanceToSegment(point, points[i], points[i + 1]);
                if (distance >= best) continue;
                best = distance;
                result = i;
            }
            return result;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            if (ab.sqrMagnitude <= 0.0001f) return Vector2.Distance(point, a);
            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(point, a + ab * t);
        }

        private static void DrawBridgeOrientation(
            Rect localDraw, float world, AuthoredBridgePlacement bridge, bool selected)
        {
            float length = bridge.Scale.z > 0.01f ? bridge.Scale.z * 0.5f : 2f;
            float rad = bridge.RotationDeg * Mathf.Deg2Rad;
            // Bridge local +Z = 渡り方向。2D では XZ 平面で RotationDeg 周り。
            var along = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * length;
            Vector2 a = MapAuthoringPreview2D.MapToGui(localDraw, bridge.Center - along, world);
            Vector2 b = MapAuthoringPreview2D.MapToGui(localDraw, bridge.Center + along, world);
            Handles.BeginGUI();
            Handles.color = selected ? new Color(1f, 0.75f, 0.2f) : new Color(0.7f, 0.45f, 0.2f, 0.9f);
            Handles.DrawLine(a, b, selected ? 4f : 3f);
            Handles.EndGUI();
        }

        private static Color MagicStoneColor(FeatureType type) => type switch
        {
            FeatureType.OwnMainStone => new Color(0.25f, 0.55f, 1f),
            FeatureType.EnemyMainStone => new Color(1f, 0.3f, 0.3f),
            _ => Color.magenta,
        };

        private void DrawRiverPathOverlay(Rect localDraw, float world, int riverIndex, bool selected)
        {
            if (_lastPreviewMap == null
                || riverIndex < 0
                || riverIndex >= _lastPreviewMap.Rivers.Count)
                return;

            RiverPath path = _lastPreviewMap.Rivers[riverIndex];
            if (path.Cells == null || path.Cells.Count < 2) return;

            float cs = _lastPreviewMap.Height.CellSize;
            Handles.BeginGUI();
            Handles.color = selected ? Color.cyan : new Color(0.25f, 0.55f, 1f, 0.85f);
            Vector3 prev = default;
            bool hasPrev = false;
            for (int c = 0; c < path.Cells.Count; c++)
            {
                Vector2Int cell = path.Cells[c];
                Vector2 worldXZ = new Vector2((cell.x + 0.5f) * cs, (cell.y + 0.5f) * cs);
                Vector2 gui = MapAuthoringPreview2D.MapToGui(localDraw, worldXZ, world);
                var p = new Vector3(gui.x, gui.y, 0f);
                if (hasPrev)
                    Handles.DrawLine(prev, p, selected ? 3f : 2f);
                prev = p;
                hasPrev = true;
            }

            Handles.EndGUI();
        }

        private void HandleCanvasInput(Rect drawRect, float world)
        {
            if (_tool == MapAuthoringTool.AssaultRoute)
            {
                HandleAssaultRouteInput(drawRect, world);
                return;
            }

            Event e = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive);

            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Escape && _dragging)
                {
                    _dragging = false;
                    GUIUtility.hotControl = 0;
                    Undo.PerformUndo();
                    e.Use();
                    return;
                }
                if (e.keyCode == KeyCode.Escape && _hasPendingRiverStart)
                {
                    ClearPendingRiverStart();
                    _status = "川の始点を取り消しました";
                    e.Use();
                    return;
                }

                if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
                {
                    DeleteSelection();
                    e.Use();
                    return;
                }
            }

            if (e.type != EventType.MouseDrag
                && e.type != EventType.MouseUp)
            {
                bool inHit = drawRect.Contains(e.mousePosition);
                if (_tool == MapAuthoringTool.River || (_dragging && _selectionKind == MapAuthoringSelectionKind.River))
                {
                    var expanded = Rect.MinMaxRect(
                        drawRect.xMin - 28f,
                        drawRect.yMin - 28f,
                        drawRect.xMax + 28f,
                        drawRect.yMax + 28f);
                    inHit = expanded.Contains(e.mousePosition);
                }

                if (!inHit) return;
            }

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button != 0) break;
                    bool mapped = _tool == MapAuthoringTool.River
                        ? MapAuthoringPreview2D.TryMapPointNearEdge(
                            drawRect, e.mousePosition, world, edgeSlopGui: 28f, out Vector2 mapXZ)
                        : MapAuthoringPreview2D.TryMapPoint(drawRect, e.mousePosition, world, out mapXZ);
                    if (!mapped)
                        break;

                    if (_tool == MapAuthoringTool.Select)
                    {
                        if (TryPick(mapXZ))
                        {
                            RecordUndo("配置を移動");
                            _dragging = true;
                            GUIUtility.hotControl = id;
                        }
                    }
                    else
                    {
                        PlaceAt(mapXZ);
                    }

                    e.Use();
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != id || !_dragging) break;
                    bool dragRiverEndpoint = _selectionKind == MapAuthoringSelectionKind.River
                        && _selectionEndpoint >= 0
                        && _selectionEndpoint <= 1;
                    bool dragMapped = dragRiverEndpoint
                        ? MapAuthoringPreview2D.TryMapPointNearEdge(
                            drawRect, e.mousePosition, world, edgeSlopGui: 28f, out Vector2 dragXZ)
                        : MapAuthoringPreview2D.TryMapPoint(drawRect, e.mousePosition, world, out dragXZ);
                    if (dragMapped)
                    {
                        MoveSelection(dragXZ);
                        e.Use();
                    }

                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl != id) break;
                    _dragging = false;
                    GUIUtility.hotControl = 0;
                    e.Use();
                    break;
            }
        }

        private void DrawRightPanel()
        {
            // Width だけでは子コントロールが親を押し広げられるので Max/Min も固定する。
            using (new EditorGUILayout.VerticalScope(
                       GUILayout.Width(RightPanelWidth),
                       GUILayout.MinWidth(RightPanelWidth),
                       GUILayout.MaxWidth(RightPanelWidth),
                       GUILayout.ExpandWidth(false),
                       GUILayout.ExpandHeight(true)))
            {
                if (_definition == null)
                {
                    EditorGUILayout.HelpBox("マップを選ぶか「新規」で作成してください。", MessageType.Info);
                    return;
                }

                DrawBakeStatusPanel();

                if (_tool == MapAuthoringTool.AssaultRoute)
                {
                    DrawAssaultRoutePanel();
                    return;
                }

                _rightTab = (MapAuthoringRightTab)GUILayout.Toolbar(
                    (int)_rightTab,
                    new[] { "配置", "共通", "スタンプ" });

                EditorGUILayout.Space(4f);
                switch (_rightTab)
                {
                    case MapAuthoringRightTab.Shared:
                        DrawSharedTab();
                        break;
                    case MapAuthoringRightTab.Stamps:
                        DrawStampsTab();
                        break;
                    default:
                        DrawPlacementTab();
                        break;
                }
            }
        }

        private void DrawBakeStatusPanel()
        {
            EditorGUILayout.LabelField("ベイク状態", EditorStyles.boldLabel);
            DrawBakeStage("MapData", _bakeStatus.MapData);
            DrawBakeStage("NavMesh", _bakeStatus.NavMesh);
            DrawBakeStage("侵攻ルート", _bakeStatus.AssaultRoutes);
            DrawBakeStage("プレビュー", _bakeStatus.Preview);
            DrawBakeStage("シーン3D", _bakeStatus.Scene3D);

            EditorGUILayout.HelpBox(
                _bakeStatus.AllCurrent ? "ベイク済み" : "未ベイク項目があります",
                _bakeStatus.AllCurrent ? MessageType.Info : MessageType.Warning);

            if (RequiresGeometryBake())
            {
                EditorGUILayout.HelpBox(
                    "MapData・NavMeshを更新するには「シーンへ3D反映」を実行してください。",
                    MessageType.Warning);
            }
            else if (_bakeStatus.AssaultRoutes != AuthoredMapBakeStageState.Current)
            {
                string guidance = _bakeStatus.AssaultRoutes == AuthoredMapBakeStageState.NotConfigured
                    ? "侵攻ルートを追加するか、自動ルートを更新してください。"
                    : "最新NavMeshで侵攻ルートを自動検証するか、「全ルートを今すぐ再検証」を実行してください。";
                EditorGUILayout.HelpBox(guidance, MessageType.Info);
            }
            else if (_bakeStatus.Preview != AuthoredMapBakeStageState.Current)
            {
                EditorGUILayout.HelpBox(
                    "侵攻ルートを再検証してプレビューを更新してください。",
                    MessageType.Info);
            }
            else if (_bakeStatus.Scene3D != AuthoredMapBakeStageState.Current)
            {
                EditorGUILayout.HelpBox(
                    "シーン3Dは最新ではありませんが、保存済みNavMeshによる侵攻ルート検証は可能です。",
                    MessageType.Info);
            }

            EditorGUILayout.Space(6f);
        }

        private static void DrawBakeStage(string label, AuthoredMapBakeStageState state)
        {
            GUIStyle style = new(EditorStyles.label);
            style.normal.textColor = state == AuthoredMapBakeStageState.Current
                ? new Color(0.35f, 0.8f, 0.45f)
                : new Color(1f, 0.68f, 0.25f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(90f));
                EditorGUILayout.LabelField(GetBakeStageLabel(label, state), style);
            }
        }

        private static string GetBakeStageLabel(string label, AuthoredMapBakeStageState state)
        {
            return state switch
            {
                AuthoredMapBakeStageState.Current => "最新",
                AuthoredMapBakeStageState.NotConfigured => "未設定",
                AuthoredMapBakeStageState.Deferred => "判定保留",
                AuthoredMapBakeStageState.MissingSceneData => "生成物不足",
                AuthoredMapBakeStageState.Stale when label == "侵攻ルート" => "要再検証",
                AuthoredMapBakeStageState.Stale when label == "プレビュー" => "要再生成",
                AuthoredMapBakeStageState.Stale when label == "シーン3D" => "未反映",
                AuthoredMapBakeStageState.Stale => "要再ベイク",
                AuthoredMapBakeStageState.Missing when label == "侵攻ルート" => "未ベイク",
                _ => "未生成",
            };
        }

        private bool RequiresGeometryBake() =>
            _bakeStatus.MapData != AuthoredMapBakeStageState.Current ||
            _bakeStatus.NavMesh != AuthoredMapBakeStageState.Current;

        private void DrawPlacementTab()
        {
            _panelScroll = EditorGUILayout.BeginScrollView(_panelScroll);
            try
            {
                string assetPath = AssetDatabase.GetAssetPath(_definition);
                if (!string.IsNullOrEmpty(assetPath))
                    EditorGUILayout.HelpBox($"保存先\n{assetPath}", MessageType.None);

                if (_definition.SharedConfig == null)
                    EditorGUILayout.HelpBox("共通設定が必要です。「共通」タブで割り当ててください。", MessageType.Warning);

                if (_definition.SharedConfig != null)
                {
                    if (!_definition.HasFixedFeaturePlacements)
                    {
                        if (GUILayout.Button("岩・木の配置を確定")) CaptureFeaturePlacements();
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("岩・木の配置は確定済みです。選択ツールの「岩・木」で移動できます。", MessageType.None);
                        if (GUILayout.Button("自動配置に戻す（手動調整を破棄）")) ResetFeaturePlacements();
                    }
                }

                EditorGUILayout.Space(6f);
                DrawPalette();
                EditorGUILayout.Space(6f);
                DrawSelectionInspector();
                EditorGUILayout.Space(6f);
                DrawPlacementList();

                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("状態", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    string.IsNullOrEmpty(_status) ? "準備完了" : _status,
                    MessageType.Info);
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawAssaultRoutePanel()
        {
            _panelScroll = EditorGUILayout.BeginScrollView(_panelScroll);
            try
            {
                EditorGUILayout.LabelField("侵攻ルート", EditorStyles.boldLabel);
                if (!TryGetAuthoredStoneCenter(FeatureType.OwnMainStone, out _) ||
                    !TryGetAuthoredStoneCenter(FeatureType.EnemyMainStone, out _))
                {
                    EditorGUILayout.HelpBox("自軍・敵軍の主魔石を1つずつ配置してください。", MessageType.Warning);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("手動ルートを追加")) AddManualAssaultRoute();
                    if (GUILayout.Button("自動ルートを更新")) AutoGenerateAssaultRoutes();
                }
                if (GUILayout.Button("全ルートを今すぐ再検証")) ValidateAssaultRoutes();

                EditorGUILayout.Space(6f);
                for (int i = 0; i < _definition.AssaultRoutes.Count; i++)
                {
                    AuthoredAssaultRoute route = _definition.AssaultRoutes[i];
                    if (route == null) continue;
                    bool selected = i == _selectedAssaultRoute;
                    string state = FindFailure(route.RouteId).HasValue
                        ? "エラー"
                        : IsValidatedRoute(route.RouteId)
                            ? "検証済み"
                            : "未検証";
                    if (GUILayout.Toggle(
                            selected,
                            $"{route.DisplayName}  [{route.Source}]  {state}",
                            EditorStyles.miniButton) && !selected)
                    {
                        _selectedAssaultRoute = i;
                        _selectedAssaultWaypoint = -1;
                    }
                }

                if (_selectedAssaultRoute >= 0 && _selectedAssaultRoute < _definition.AssaultRoutes.Count)
                {
                    AuthoredAssaultRoute selected = _definition.AssaultRoutes[_selectedAssaultRoute];
                    EditorGUILayout.Space(8f);
                    EditorGUI.BeginChangeCheck();
                    string displayName = EditorGUILayout.DelayedTextField("表示名", selected.DisplayName);
                    if (EditorGUI.EndChangeCheck())
                    {
                        RecordUndo("侵攻ルート名を変更");
                        selected.DisplayName = displayName;
                        ManualizeRoute(selected);
                        MarkDirty();
                        ValidateAssaultRoutes();
                    }

                    EditorGUILayout.LabelField("Route ID", selected.RouteId, EditorStyles.miniLabel);
                    EditorGUILayout.LabelField("経由点", selected.Waypoints.Count.ToString());
                    CombatAssaultRouteValidationFailure? failure = FindFailure(selected.RouteId);
                    if (failure.HasValue)
                        EditorGUILayout.HelpBox(failure.Value.Message, MessageType.Error);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(_selectedAssaultWaypoint < 0))
                        {
                            if (GUILayout.Button("選択経由点を削除")) DeleteSelectedAssaultWaypoint();
                        }
                        if (GUILayout.Button("ルートを削除")) DeleteSelectedAssaultRoute();
                    }
                }

                EditorGUILayout.Space(8f);
                EditorGUILayout.HelpBox(
                    string.IsNullOrEmpty(_status)
                        ? "表示される線はNavMesh検証済み経路です。経由点はクリックで追加、ドラッグ終了時に自動再検証します。"
                        : _status,
                    MessageType.Info);
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        private void AddManualAssaultRoute()
        {
            if (!TryGetAuthoredStoneCenter(FeatureType.OwnMainStone, out _) ||
                !TryGetAuthoredStoneCenter(FeatureType.EnemyMainStone, out _))
            {
                _status = "自軍・敵軍の主魔石を配置してください";
                return;
            }
            RecordUndo("手動侵攻ルートを追加");
            _definition.AssaultRoutes.Add(new AuthoredAssaultRoute(
                System.Guid.NewGuid().ToString("N"),
                $"手動ルート {_definition.AssaultRoutes.Count + 1}",
                AuthoredAssaultRouteSource.Manual));
            _selectedAssaultRoute = _definition.AssaultRoutes.Count - 1;
            _selectedAssaultWaypoint = -1;
            _assaultRouteFailures.Clear();
            MarkDirty();
            ValidateAssaultRoutes();
        }

        private void AutoGenerateAssaultRoutes()
        {
            MapSceneHost host = Object.FindAnyObjectByType<MapSceneHost>();
            if (AuthoredMapNavBake.AutoGenerateAndSave(_definition, host, out string status))
            {
                _selectedAssaultRoute = _definition.AssaultRoutes.Count > 0 ? 0 : -1;
                _selectedAssaultWaypoint = -1;
                _assaultRouteFailures.Clear();
                QueuePreviewRebuild(immediate: true);
            }
            _status = status;
        }

        private void ValidateAssaultRoutes()
        {
            if (_definition == null || _definition.AssaultRoutes.Count == 0)
            {
                _assaultRouteFailures.Clear();
                _status = "侵攻ルートがありません";
                QueuePreviewRebuild(immediate: true);
                return;
            }
            MapSceneHost host = Object.FindAnyObjectByType<MapSceneHost>();
            if (AuthoredMapNavBake.ValidateAndSave(
                    _definition,
                    host,
                    _definition.AssaultRoutes,
                    out List<CombatAssaultRouteValidationFailure> failures,
                    out string status))
            {
                _assaultRouteFailures = failures;
                QueuePreviewRebuild(immediate: true);
            }
            else
            {
                _assaultRouteFailures = failures;
                QueuePreviewRebuild(immediate: true);
            }
            _status = status;
        }

        private bool IsValidatedRoute(string routeId)
        {
            if (!_definition.HasValidBakedAssaultRoutes) return false;
            for (int i = 0; i < _validatedPreviewRoutes.Count; i++)
            {
                if (_validatedPreviewRoutes[i].RouteId == routeId) return true;
            }
            return false;
        }

        private void DeleteSelectedAssaultWaypoint()
        {
            if (_selectedAssaultRoute < 0 || _selectedAssaultRoute >= _definition.AssaultRoutes.Count)
                return;
            AuthoredAssaultRoute route = _definition.AssaultRoutes[_selectedAssaultRoute];
            if (_selectedAssaultWaypoint < 0 || _selectedAssaultWaypoint >= route.Waypoints.Count) return;
            RecordUndo("侵攻ルート経由点を削除");
            route.Waypoints.RemoveAt(_selectedAssaultWaypoint);
            _selectedAssaultWaypoint = -1;
            ManualizeRoute(route);
            MarkDirty();
            ValidateAssaultRoutes();
        }

        private void DeleteSelectedAssaultRoute()
        {
            if (_selectedAssaultRoute < 0 || _selectedAssaultRoute >= _definition.AssaultRoutes.Count)
                return;
            RecordUndo("侵攻ルートを削除");
            _definition.AssaultRoutes.RemoveAt(_selectedAssaultRoute);
            _selectedAssaultRoute = Mathf.Min(_selectedAssaultRoute, _definition.AssaultRoutes.Count - 1);
            _selectedAssaultWaypoint = -1;
            _assaultRouteFailures.Clear();
            MarkDirty();
            if (_definition.AssaultRoutes.Count > 0)
                ValidateAssaultRoutes();
            else
                QueuePreviewRebuild(immediate: true);
        }

        private static void ManualizeRoute(AuthoredAssaultRoute route)
        {
            if (route.Source == AuthoredAssaultRouteSource.Manual) return;
            route.Source = AuthoredAssaultRouteSource.Manual;
            route.RouteId = System.Guid.NewGuid().ToString("N");
        }

        private void DrawSharedTab()
        {
            _sharedScroll = EditorGUILayout.BeginScrollView(_sharedScroll);
            try
            {
                EditorGUI.BeginChangeCheck();
                MapConfig config = (MapConfig)EditorGUILayout.ObjectField(
                    "共通設定", _definition.SharedConfig, typeof(MapConfig), false);
                if (EditorGUI.EndChangeCheck())
                {
                    RecordUndo("共通設定を変更");
                    _definition.SharedConfig = config;
                    MarkDirty();
                    DestroyCachedEditors();
                    QueuePreviewRebuild();
                }

                if (_definition.SharedConfig == null)
                {
                    EditorGUILayout.HelpBox("共通設定が未設定です。", MessageType.Warning);
                    if (GUILayout.Button("デフォルト設定を割り当て"))
                        AssignDefaultSharedConfig();
                    return;
                }

                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("パラメータ", EditorStyles.boldLabel);
                DrawCachedInspector(_definition.SharedConfig, ref _sharedConfigEditor, rebuildPreviewOnChange: true);
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawStampsTab()
        {
            EditorGUI.BeginChangeCheck();
            _stampKind = (MapAuthoringStampKind)EditorGUILayout.Popup(
                "種類",
                (int)_stampKind,
                new[] { "山", "湖", "沼・雪", "森", "川" });
            if (EditorGUI.EndChangeCheck())
                SyncToolFromStampKind();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("新規"))
                    CreateStampAsset(_stampKind);
                using (new EditorGUI.DisabledScope(GetSelectedStampObject() == null))
                {
                    if (GUILayout.Button("削除"))
                        DeleteSelectedStampAsset();
                }
            }

            EditorGUILayout.Space(4f);
            DrawStampKindPalette();

            Object selected = GetSelectedStampObject();
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("選択中のスタンプ", EditorStyles.boldLabel);
            if (selected == null)
            {
                EditorGUILayout.HelpBox("一覧からスタンプを選ぶか「新規」で作成してください。", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(selected), EditorStyles.miniLabel);
            _stampDetailScroll = EditorGUILayout.BeginScrollView(_stampDetailScroll);
            try
            {
                DrawCachedInspector(selected, ref _stampEditor, rebuildPreviewOnChange: true);
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawStampKindPalette()
        {
            switch (_stampKind)
            {
                case MapAuthoringStampKind.Mountain:
                    _selectedMountain = DrawStampList(_mountainStamps, _selectedMountain, s => s.DisplayName);
                    break;
                case MapAuthoringStampKind.Lake:
                    _selectedLake = DrawStampList(_lakeStamps, _selectedLake, s => s.DisplayName);
                    break;
                case MapAuthoringStampKind.GroundPatch:
                    _selectedGround = DrawStampList(_groundStamps, _selectedGround, s => s.DisplayName);
                    break;
                case MapAuthoringStampKind.Forest:
                    _selectedForest = DrawStampList(_forestStamps, _selectedForest, s => s.DisplayName);
                    break;
                case MapAuthoringStampKind.River:
                    _selectedRiver = DrawStampList(_riverStamps, _selectedRiver, s => s.name);
                    break;
            }
        }

        private void DrawCachedInspector(Object target, ref Editor editor, bool rebuildPreviewOnChange)
        {
            if (target == null) return;

            if (editor == null || editor.target != target)
            {
                if (editor != null)
                    DestroyImmediate(editor);
                editor = Editor.CreateEditor(target);
            }

            if (editor == null) return;

            EditorGUI.BeginChangeCheck();
            editor.OnInspectorGUI();
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(target);
                if (target is ForestClusterStampShape forestShape &&
                    _definition?.HasFixedFeaturePlacements == true)
                {
                    RecordUndo("森の木を再生成");
                    for (int i = 0; i < _definition.Forests.Count; i++)
                    {
                        if (_definition.Forests[i].Shape == forestShape)
                            AuthoredMapBuilder.RegenerateForestTrees(_definition, i);
                    }
                    MarkDirty();
                }
                if (rebuildPreviewOnChange)
                    QueuePreviewRebuild();
            }
        }

        private void AssignDefaultSharedConfig()
        {
            MapConfig config = AssetDatabase.LoadAssetAtPath<MapConfig>(DefaultConfigPath);
            if (config == null)
            {
                _status = $"デフォルト設定が見つかりません\n{DefaultConfigPath}";
                return;
            }

            RecordUndo("デフォルト共通設定を割り当て");
            _definition.SharedConfig = config;
            MarkDirty();
            DestroyCachedEditors();
            QueuePreviewRebuild();
            _status = "デフォルト共通設定を割り当てました";
        }

        private void SyncStampKindFromTool()
        {
            switch (_tool)
            {
                case MapAuthoringTool.Mountain:
                    _stampKind = MapAuthoringStampKind.Mountain;
                    break;
                case MapAuthoringTool.Lake:
                    _stampKind = MapAuthoringStampKind.Lake;
                    break;
                case MapAuthoringTool.GroundPatch:
                    _stampKind = MapAuthoringStampKind.GroundPatch;
                    break;
                case MapAuthoringTool.Forest:
                    _stampKind = MapAuthoringStampKind.Forest;
                    break;
                case MapAuthoringTool.River:
                    _stampKind = MapAuthoringStampKind.River;
                    break;
            }
        }

        private void SyncToolFromStampKind()
        {
            _tool = _stampKind switch
            {
                MapAuthoringStampKind.Mountain => MapAuthoringTool.Mountain,
                MapAuthoringStampKind.Lake => MapAuthoringTool.Lake,
                MapAuthoringStampKind.GroundPatch => MapAuthoringTool.GroundPatch,
                MapAuthoringStampKind.Forest => MapAuthoringTool.Forest,
                MapAuthoringStampKind.River => MapAuthoringTool.River,
                _ => _tool,
            };
            ClearPendingRiverStart();
        }

        private Object GetSelectedStampObject()
        {
            return _stampKind switch
            {
                MapAuthoringStampKind.Mountain => _selectedMountain,
                MapAuthoringStampKind.Lake => _selectedLake,
                MapAuthoringStampKind.GroundPatch => _selectedGround,
                MapAuthoringStampKind.Forest => _selectedForest,
                MapAuthoringStampKind.River => _selectedRiver,
                _ => null,
            };
        }

        private void ClearSelectedStampObject()
        {
            switch (_stampKind)
            {
                case MapAuthoringStampKind.Mountain:
                    _selectedMountain = null;
                    break;
                case MapAuthoringStampKind.Lake:
                    _selectedLake = null;
                    break;
                case MapAuthoringStampKind.GroundPatch:
                    _selectedGround = null;
                    break;
                case MapAuthoringStampKind.Forest:
                    _selectedForest = null;
                    break;
                case MapAuthoringStampKind.River:
                    _selectedRiver = null;
                    break;
            }

            DestroyCachedEditors();
        }

        private void CreateStampAsset(MapAuthoringStampKind kind)
        {
            EnsureStampFolders();
            ScriptableObject asset;
            string folder;
            string fileName;
            switch (kind)
            {
                case MapAuthoringStampKind.Mountain:
                    asset = CreateInstance<HeightStampShape>();
                    folder = StampHeightFolder;
                    fileName = "HeightStamp";
                    break;
                case MapAuthoringStampKind.Lake:
                    asset = CreateInstance<LakeStampShape>();
                    folder = StampLakeFolder;
                    fileName = "LakeStamp";
                    break;
                case MapAuthoringStampKind.GroundPatch:
                    asset = CreateInstance<GroundPatchStampShape>();
                    folder = StampBiomeFolder;
                    fileName = "GroundPatchStamp";
                    break;
                case MapAuthoringStampKind.Forest:
                    asset = CreateInstance<ForestClusterStampShape>();
                    folder = StampBiomeFolder;
                    fileName = "ForestClusterStamp";
                    break;
                case MapAuthoringStampKind.River:
                    asset = CreateInstance<RiverShape>();
                    folder = StampRiverFolder;
                    fileName = "RiverShape";
                    break;
                default:
                    return;
            }

            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ReloadStampPalette();
            SelectCreatedStamp(kind, asset);
            _status = $"スタンプを作成しました\n{path}";
        }

        private void SelectCreatedStamp(MapAuthoringStampKind kind, ScriptableObject asset)
        {
            switch (kind)
            {
                case MapAuthoringStampKind.Mountain:
                    _selectedMountain = asset as HeightStampShape;
                    break;
                case MapAuthoringStampKind.Lake:
                    _selectedLake = asset as LakeStampShape;
                    break;
                case MapAuthoringStampKind.GroundPatch:
                    _selectedGround = asset as GroundPatchStampShape;
                    break;
                case MapAuthoringStampKind.Forest:
                    _selectedForest = asset as ForestClusterStampShape;
                    break;
                case MapAuthoringStampKind.River:
                    _selectedRiver = asset as RiverShape;
                    break;
            }

            DestroyCachedEditors();
        }

        private void DeleteSelectedStampAsset()
        {
            Object stamp = GetSelectedStampObject();
            if (stamp == null) return;

            string path = AssetDatabase.GetAssetPath(stamp);
            if (string.IsNullOrEmpty(path))
            {
                _status = "スタンプのアセットパスが取得できません";
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "スタンプ削除",
                    $"次のスタンプを削除しますか？\n{path}\n\n手作りマップから参照されている場合は削除できません。",
                    "削除",
                    "キャンセル"))
            {
                return;
            }

            if (MapAuthoringStampUsage.TryFindUsers(stamp, out List<string> users))
            {
                _status = $"削除できません。次のマップが参照中です:\n{string.Join("\n", users)}";
                return;
            }

            DestroyCachedEditors();
            ClearSelectedStampObject();
            if (!AssetDatabase.DeleteAsset(path))
            {
                _status = $"削除に失敗しました\n{path}";
                ReloadStampPalette();
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ReloadStampPalette();
            QueuePreviewRebuild();
            _status = $"スタンプを削除しました\n{path}";
        }

        private static void EnsureStampFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder("Assets/Data/Map")) AssetDatabase.CreateFolder("Assets/Data", "Map");
            if (!AssetDatabase.IsValidFolder("Assets/Data/Map/Map")) AssetDatabase.CreateFolder("Assets/Data/Map", "Map");
            if (!AssetDatabase.IsValidFolder(StampRoot)) AssetDatabase.CreateFolder("Assets/Data/Map/Map", "Stamps");
            if (!AssetDatabase.IsValidFolder(StampHeightFolder)) AssetDatabase.CreateFolder(StampRoot, "Height");
            if (!AssetDatabase.IsValidFolder(StampLakeFolder)) AssetDatabase.CreateFolder(StampRoot, "Lake");
            if (!AssetDatabase.IsValidFolder(StampBiomeFolder)) AssetDatabase.CreateFolder(StampRoot, "Biome");
            if (!AssetDatabase.IsValidFolder(StampRiverFolder)) AssetDatabase.CreateFolder(StampRoot, "River");
        }

        private void DestroyCachedEditors()
        {
            if (_sharedConfigEditor != null)
            {
                DestroyImmediate(_sharedConfigEditor);
                _sharedConfigEditor = null;
            }

            if (_stampEditor != null)
            {
                DestroyImmediate(_stampEditor);
                _stampEditor = null;
            }
        }

        private void DrawPalette()
        {
            EditorGUILayout.LabelField("スタンプ一覧", EditorStyles.boldLabel);
            switch (_tool)
            {
                case MapAuthoringTool.Mountain:
                    _mountainKind = DrawMountainKindPopup(_mountainKind);
                    _selectedMountain = DrawStampList(_mountainStamps, _selectedMountain, s => s.DisplayName);
                    break;
                case MapAuthoringTool.Lake:
                    _lakeFrozen = EditorGUILayout.Toggle("凍結", _lakeFrozen);
                    _selectedLake = DrawStampList(_lakeStamps, _selectedLake, s => s.DisplayName);
                    break;
                case MapAuthoringTool.GroundPatch:
                    _selectedGround = DrawStampList(_groundStamps, _selectedGround, s => s.DisplayName);
                    break;
                case MapAuthoringTool.Forest:
                    _selectedForest = DrawStampList(_forestStamps, _selectedForest, s => s.DisplayName);
                    break;
                case MapAuthoringTool.River:
                    _selectedRiver = DrawStampList(_riverStamps, _selectedRiver, s => s.name);
                    EditorGUILayout.HelpBox(
                        _hasPendingRiverStart
                            ? "終点側のマップ端をクリックしてください（Escで取消）"
                            : "始点→終点をクリック。作成後、黄色いベジェ制御点をドラッグして弧を調整できます。",
                        MessageType.None);
                    break;
                case MapAuthoringTool.Bridge:
                    EditorGUILayout.HelpBox(
                        "川の近くをクリックするとセルへスナップし、向き・長さを自動設定します。選択後に回転・拡縮を調整できます。",
                        MessageType.None);
                    break;
                case MapAuthoringTool.MagicStone:
                    _selectedMagicStoneType = DrawMagicStoneTypePopup(_selectedMagicStoneType);
                    EditorGUILayout.HelpBox("クリックで魔石を配置。散布木・岩は SharedConfig の自動配置です。", MessageType.None);
                    break;
                default:
                    EditorGUILayout.HelpBox("選択ツール: マーカーをクリックして選択・移動。", MessageType.None);
                    break;
            }
        }

        private static FeatureType DrawMagicStoneTypePopup(FeatureType current)
        {
            int index = current switch
            {
                FeatureType.OwnMainStone => 0,
                FeatureType.EnemyMainStone => 1,
                _ => 0,
            };
            index = EditorGUILayout.Popup("種類", index, new[]
            {
                "自軍メイン",
                "敵軍メイン",
            });
            return index switch
            {
                1 => FeatureType.EnemyMainStone,
                _ => FeatureType.OwnMainStone,
            };
        }

        private T DrawStampList<T>(List<T> stamps, T selected, System.Func<T, string> nameOf)
            where T : Object
        {
            _paletteScroll = EditorGUILayout.BeginScrollView(_paletteScroll, GUILayout.Height(180f));
            if (stamps.Count == 0)
                EditorGUILayout.LabelField("スタンプが見つかりません");

            for (int i = 0; i < stamps.Count; i++)
            {
                T stamp = stamps[i];
                if (stamp == null) continue;
                string label = string.IsNullOrEmpty(nameOf(stamp)) ? stamp.name : nameOf(stamp);
                bool on = selected == stamp;
                if (GUILayout.Toggle(on, label, "Button") && !on)
                    selected = stamp;
            }

            EditorGUILayout.EndScrollView();
            return selected;
        }

        private void DrawSelectionInspector()
        {
            EditorGUILayout.LabelField("選択中", EditorStyles.boldLabel);
            if (_selectionKind == MapAuthoringSelectionKind.None || _selectionIndex < 0)
            {
                EditorGUILayout.LabelField("なし");
                return;
            }

            bool fixedFeature = _selectionKind == MapAuthoringSelectionKind.Rock ||
                _selectionKind == MapAuthoringSelectionKind.ScatterTree ||
                _selectionKind == MapAuthoringSelectionKind.ForestTree;
            if (!fixedFeature && GUILayout.Button("削除"))
            {
                DeleteSelection();
                return;
            }

            EditorGUI.BeginChangeCheck();
            switch (_selectionKind)
            {
                case MapAuthoringSelectionKind.Mountain:
                {
                    AuthoredMountainPlacement e = _definition.Mountains[_selectionIndex];
                    HeightStampShape shape = (HeightStampShape)EditorGUILayout.ObjectField("スタンプ", e.Shape, typeof(HeightStampShape), false);
                    MountainKind kind = DrawMountainKindPopup(e.Kind);
                    Vector2 center = EditorGUILayout.Vector2Field("位置", e.Center);
                    float rotation = EditorGUILayout.FloatField("回転", e.RotationDeg);
                    Vector2 scale = EditorGUILayout.Vector2Field("拡縮", e.Scale);
                    if (EditorGUI.EndChangeCheck())
                    {
                        RecordUndo("山を編集");
                        e.Shape = shape;
                        e.Kind = kind;
                        e.Center = ClampToMap(center);
                        e.RotationDeg = rotation;
                        e.Scale = scale;
                        MarkDirty();
                        QueuePreviewRebuild();
                    }

                    return;
                }
                case MapAuthoringSelectionKind.Lake:
                {
                    AuthoredLakePlacement e = _definition.Lakes[_selectionIndex];
                    LakeStampShape shape = (LakeStampShape)EditorGUILayout.ObjectField("スタンプ", e.Shape, typeof(LakeStampShape), false);
                    Vector2 center = EditorGUILayout.Vector2Field("位置", e.Center);
                    float rotation = EditorGUILayout.FloatField("回転", e.RotationDeg);
                    Vector2 scale = EditorGUILayout.Vector2Field("拡縮", e.Scale);
                    bool frozen = EditorGUILayout.Toggle("凍結", e.IsFrozen);
                    if (EditorGUI.EndChangeCheck())
                    {
                        RecordUndo("湖を編集");
                        e.Shape = shape;
                        e.Center = ClampToMap(center);
                        e.RotationDeg = rotation;
                        e.Scale = scale;
                        e.IsFrozen = frozen;
                        MarkDirty();
                        QueuePreviewRebuild();
                    }

                    return;
                }
                case MapAuthoringSelectionKind.GroundPatch:
                {
                    AuthoredGroundPatchPlacement e = _definition.GroundPatches[_selectionIndex];
                    GroundPatchStampShape shape = (GroundPatchStampShape)EditorGUILayout.ObjectField("スタンプ", e.Shape, typeof(GroundPatchStampShape), false);
                    Vector2 center = EditorGUILayout.Vector2Field("位置", e.Center);
                    float rotation = EditorGUILayout.FloatField("回転", e.RotationDeg);
                    Vector2 scale = EditorGUILayout.Vector2Field("拡縮", e.Scale);
                    if (EditorGUI.EndChangeCheck())
                    {
                        RecordUndo("沼・雪を編集");
                        e.Shape = shape;
                        e.Center = ClampToMap(center);
                        e.RotationDeg = rotation;
                        e.Scale = scale;
                        MarkDirty();
                        QueuePreviewRebuild();
                    }

                    return;
                }
                case MapAuthoringSelectionKind.Forest:
                {
                    AuthoredForestPlacement e = _definition.Forests[_selectionIndex];
                    ForestClusterStampShape shape = (ForestClusterStampShape)EditorGUILayout.ObjectField("スタンプ", e.Shape, typeof(ForestClusterStampShape), false);
                    Vector2 center = EditorGUILayout.Vector2Field("位置", e.Center);
                    float rotation = EditorGUILayout.FloatField("回転", e.RotationDeg);
                    Vector2 scale = EditorGUILayout.Vector2Field("拡縮", e.Scale);
                    if (EditorGUI.EndChangeCheck())
                    {
                        RecordUndo("森を編集");
                        bool regenerate = _definition.HasFixedFeaturePlacements && e.Shape != shape;
                        e.Shape = shape;
                        TryMoveForest(e, ClampToMap(center));
                        e.RotationDeg = rotation;
                        e.Scale = scale;
                        if (regenerate) AuthoredMapBuilder.RegenerateForestTrees(_definition, _selectionIndex);
                        MarkDirty();
                        QueuePreviewRebuild();
                    }

                    return;
                }
                case MapAuthoringSelectionKind.Rock:
                case MapAuthoringSelectionKind.ScatterTree:
                case MapAuthoringSelectionKind.ForestTree:
                {
                    AuthoredPointFeaturePlacement placement = GetSelectedPointFeature();
                    Vector2 center = EditorGUILayout.Vector2Field("位置", placement.Center);
                    if (EditorGUI.EndChangeCheck())
                    {
                        RecordUndo("岩・木を移動");
                        FeatureType type = _selectionKind == MapAuthoringSelectionKind.Rock
                            ? FeatureType.Rock
                            : FeatureType.Tree;
                        if (TryMoveFeature(placement, type, ClampToMap(center)))
                        {
                            MarkDirty();
                            QueuePreviewRebuild();
                        }
                    }
                    return;
                }
                case MapAuthoringSelectionKind.River:
                {
                    AuthoredRiverPlacement e = _definition.Rivers[_selectionIndex];
                    if (!e.TryGetBezier(out Vector2 start, out Vector2 control, out Vector2 end))
                    {
                        EditorGUILayout.HelpBox("始点・終点が不足しています。", MessageType.Warning);
                        EditorGUI.EndChangeCheck();
                        return;
                    }

                    RiverShape shape = (RiverShape)EditorGUILayout.ObjectField("断面", e.Shape, typeof(RiverShape), false);
                    start = EditorGUILayout.Vector2Field("始点", start);
                    control = EditorGUILayout.Vector2Field("ベジェ制御点", control);
                    end = EditorGUILayout.Vector2Field("終点", end);
                    if (EditorGUI.EndChangeCheck())
                    {
                        RecordUndo("川を編集");
                        e.Shape = shape;
                        float world = GetWorldSize();
                        e.SetBezier(
                            MapAuthoringPreview2D.SnapToNearestEdge(start, world),
                            ClampToMap(control),
                            MapAuthoringPreview2D.SnapToNearestEdge(end, world));
                        MarkDirty();
                        QueuePreviewRebuild();
                    }

                    return;
                }
                case MapAuthoringSelectionKind.Bridge:
                {
                    AuthoredBridgePlacement e = _definition.Bridges[_selectionIndex];
                    Vector2 center = EditorGUILayout.Vector2Field("位置", e.Center);
                    float rotation = EditorGUILayout.FloatField("回転", e.RotationDeg);
                    Vector3 scale = EditorGUILayout.Vector3Field("拡縮 (幅/厚/長さ)", e.Scale);
                    if (GUILayout.Button("川に合わせて再スナップ"))
                    {
                        RecordUndo("橋を再スナップ");
                        ApplyBridgeSnap(e, e.Center, updateScale: true);
                        MarkDirty();
                        QueuePreviewRebuild();
                        EditorGUI.EndChangeCheck();
                        return;
                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        RecordUndo("橋を編集");
                        e.Center = ClampToMap(center);
                        e.RotationDeg = rotation;
                        e.Scale = scale;
                        MarkDirty();
                        QueuePreviewRebuild();
                    }

                    return;
                }
                case MapAuthoringSelectionKind.MagicStone:
                {
                    AuthoredMagicStonePlacement e = _definition.MagicStones[_selectionIndex];
                    FeatureType type = DrawMagicStoneTypePopup(e.Type);
                    Vector2 center = EditorGUILayout.Vector2Field("位置", e.Center);
                    if (EditorGUI.EndChangeCheck())
                    {
                        RecordUndo("魔石を編集");
                        e.Type = type;
                        e.Center = ClampToMap(center);
                        MarkDirty();
                        QueuePreviewRebuild();
                    }

                    return;
                }
            }

            EditorGUI.EndChangeCheck();
        }

        private void DrawPlacementList()
        {
            EditorGUILayout.LabelField("配置一覧", EditorStyles.boldLabel);
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.Height(160f));
            DrawIndexRow("山", _definition.Mountains.Count, MapAuthoringSelectionKind.Mountain);
            DrawIndexRow("湖", _definition.Lakes.Count, MapAuthoringSelectionKind.Lake);
            DrawIndexRow("沼・雪", _definition.GroundPatches.Count, MapAuthoringSelectionKind.GroundPatch);
            DrawIndexRow("森", _definition.Forests.Count, MapAuthoringSelectionKind.Forest);
            DrawIndexRow("川", _definition.Rivers.Count, MapAuthoringSelectionKind.River);
            DrawIndexRow("橋", _definition.Bridges.Count, MapAuthoringSelectionKind.Bridge);
            DrawIndexRow("魔石", _definition.MagicStones.Count, MapAuthoringSelectionKind.MagicStone);
            EditorGUILayout.EndScrollView();
        }

        private void DrawIndexRow(string label, int count, MapAuthoringSelectionKind kind)
        {
            EditorGUILayout.LabelField($"{label} ({count})");
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < count; i++)
                {
                    bool on = _selectionKind == kind && _selectionIndex == i;
                    if (GUILayout.Toggle(on, i.ToString(), EditorStyles.miniButton, GUILayout.Width(28f)) && !on)
                        SetSelection(kind, i, endpoint: -1);
                }
            }
        }

        private void PlaceAt(Vector2 mapXZ)
        {
            if (_definition == null || _definition.SharedConfig == null)
            {
                _status = "共通設定を設定してください";
                return;
            }

            mapXZ = ClampToMap(mapXZ);
            switch (_tool)
            {
                case MapAuthoringTool.Mountain:
                    if (_selectedMountain == null)
                    {
                        _status = "山スタンプを選んでください";
                        return;
                    }

                    RecordUndo("山を配置");
                    _definition.Mountains.Add(new AuthoredMountainPlacement
                    {
                        Shape = _selectedMountain,
                        Kind = _mountainKind,
                        Center = mapXZ,
                        Scale = Vector2.one,
                    });
                    SetSelection(MapAuthoringSelectionKind.Mountain, _definition.Mountains.Count - 1);
                    break;

                case MapAuthoringTool.Lake:
                    if (_selectedLake == null)
                    {
                        _status = "湖スタンプを選んでください";
                        return;
                    }

                    RecordUndo("湖を配置");
                    _definition.Lakes.Add(new AuthoredLakePlacement
                    {
                        Shape = _selectedLake,
                        Center = mapXZ,
                        Scale = Vector2.one,
                        IsFrozen = _lakeFrozen,
                    });
                    SetSelection(MapAuthoringSelectionKind.Lake, _definition.Lakes.Count - 1);
                    break;

                case MapAuthoringTool.GroundPatch:
                    if (_selectedGround == null)
                    {
                        _status = "沼・雪スタンプを選んでください";
                        return;
                    }

                    RecordUndo("沼・雪を配置");
                    _definition.GroundPatches.Add(new AuthoredGroundPatchPlacement
                    {
                        Shape = _selectedGround,
                        Center = mapXZ,
                        Scale = Vector2.one,
                    });
                    SetSelection(MapAuthoringSelectionKind.GroundPatch, _definition.GroundPatches.Count - 1);
                    break;

                case MapAuthoringTool.Forest:
                    if (_selectedForest == null)
                    {
                        _status = "森スタンプを選んでください";
                        return;
                    }

                    RecordUndo("森を配置");
                    _definition.Forests.Add(new AuthoredForestPlacement
                    {
                        Shape = _selectedForest,
                        Center = mapXZ,
                        Scale = Vector2.one,
                    });
                    SetSelection(MapAuthoringSelectionKind.Forest, _definition.Forests.Count - 1);
                    if (_definition.HasFixedFeaturePlacements)
                        AuthoredMapBuilder.RegenerateForestTrees(_definition, _definition.Forests.Count - 1);
                    break;

                case MapAuthoringTool.River:
                    PlaceRiverEndpoint(mapXZ);
                    return;

                case MapAuthoringTool.Bridge:
                {
                    RecordUndo("橋を配置");
                    var bridge = new AuthoredBridgePlacement();
                    ApplyBridgeSnap(bridge, mapXZ);
                    _definition.Bridges.Add(bridge);
                    SetSelection(MapAuthoringSelectionKind.Bridge, _definition.Bridges.Count - 1);
                    break;
                }

                case MapAuthoringTool.MagicStone:
                    if (!AuthoredMapBuilder.IsMagicStoneType(_selectedMagicStoneType))
                    {
                        _status = "魔石の種類を選んでください";
                        return;
                    }

                    RecordUndo("魔石を配置");
                    _definition.MagicStones.Add(new AuthoredMagicStonePlacement
                    {
                        Type = _selectedMagicStoneType,
                        Center = mapXZ,
                    });
                    SetSelection(MapAuthoringSelectionKind.MagicStone, _definition.MagicStones.Count - 1);
                    break;

                default:
                    return;
            }

            MarkDirty();
            _status = null;
            QueuePreviewRebuild();
        }

        private void PlaceRiverEndpoint(Vector2 mapXZ)
        {
            RiverShape shape = _selectedRiver;
            if (shape == null && _definition.SharedConfig != null)
                shape = _definition.SharedConfig.RiverShape;
            if (shape == null)
            {
                _status = "川の断面スタンプを選んでください";
                return;
            }

            float world = GetWorldSize();
            mapXZ = MapAuthoringPreview2D.SnapToNearestEdge(mapXZ, world);

            if (!_hasPendingRiverStart)
            {
                _hasPendingRiverStart = true;
                _pendingRiverStart = mapXZ;
                _status = "始点を端に設定しました。反対側の端をクリックしてください";
                Repaint();
                return;
            }

            Vector2 start = MapAuthoringPreview2D.SnapToNearestEdge(_pendingRiverStart, world);
            Vector2 end = mapXZ;
            if ((end - start).sqrMagnitude < 1f)
            {
                _status = "始点と終点が近すぎます。別の辺か離れた位置をクリックしてください";
                return;
            }

            RecordUndo("川を配置");
            var river = new AuthoredRiverPlacement { Shape = shape };
            river.SetBezier(start, (start + end) * 0.5f, end);
            _definition.Rivers.Add(river);
            SetSelection(MapAuthoringSelectionKind.River, _definition.Rivers.Count - 1, endpoint: 2);
            ClearPendingRiverStart();
            MarkDirty();
            _status = "川を作成しました。黄色い制御点を動かして弧を調整できます";
            QueuePreviewRebuild();
        }

        private bool TryPick(Vector2 mapXZ)
        {
            float best = PickRadiusMeters * PickRadiusMeters;
            MapAuthoringSelectionKind kind = MapAuthoringSelectionKind.None;
            int index = -1;
            int endpoint = -1;
            int forestIndex = -1;

            void Consider(Vector2 center, MapAuthoringSelectionKind k, int i, int ep = -1, int forest = -1)
            {
                float sq = (center - mapXZ).sqrMagnitude;
                if (sq >= best) return;
                best = sq;
                kind = k;
                index = i;
                endpoint = ep;
                forestIndex = forest;
            }

            if (_pickFixedFeatures && _definition.HasFixedFeaturePlacements)
            {
                for (int i = 0; i < _definition.Rocks.Count; i++)
                    Consider(_definition.Rocks[i].Center, MapAuthoringSelectionKind.Rock, i);
                for (int i = 0; i < _definition.Trees.Count; i++)
                    Consider(_definition.Trees[i].Center, MapAuthoringSelectionKind.ScatterTree, i);
                for (int forest = 0; forest < _definition.Forests.Count; forest++)
                {
                    List<AuthoredPointFeaturePlacement> trees = _definition.Forests[forest].Trees;
                    if (trees == null) continue;
                    for (int i = 0; i < trees.Count; i++)
                        Consider(trees[i].Center, MapAuthoringSelectionKind.ForestTree, i, forest: forest);
                }
            }
            else
            {
                for (int i = 0; i < _definition.Mountains.Count; i++)
                    Consider(_definition.Mountains[i].Center, MapAuthoringSelectionKind.Mountain, i);
                for (int i = 0; i < _definition.Lakes.Count; i++)
                    Consider(_definition.Lakes[i].Center, MapAuthoringSelectionKind.Lake, i);
                for (int i = 0; i < _definition.GroundPatches.Count; i++)
                    Consider(_definition.GroundPatches[i].Center, MapAuthoringSelectionKind.GroundPatch, i);
                for (int i = 0; i < _definition.Forests.Count; i++)
                    Consider(_definition.Forests[i].Center, MapAuthoringSelectionKind.Forest, i);
                for (int i = 0; i < _definition.Rivers.Count; i++)
                {
                    AuthoredRiverPlacement river = _definition.Rivers[i];
                    if (!river.TryGetBezier(out Vector2 start, out Vector2 control, out Vector2 end)) continue;
                    Consider(start, MapAuthoringSelectionKind.River, i, 0);
                    Consider(end, MapAuthoringSelectionKind.River, i, 1);
                    Consider(control, MapAuthoringSelectionKind.River, i, 2);
                }
                for (int i = 0; i < _definition.Bridges.Count; i++)
                    Consider(_definition.Bridges[i].Center, MapAuthoringSelectionKind.Bridge, i);
                for (int i = 0; i < _definition.MagicStones.Count; i++)
                    Consider(_definition.MagicStones[i].Center, MapAuthoringSelectionKind.MagicStone, i);
            }

            if (kind == MapAuthoringSelectionKind.None)
            {
                ClearSelection();
                return false;
            }

            SetSelection(kind, index, endpoint, forestIndex);
            return true;
        }

        private void MoveSelection(Vector2 mapXZ)
        {
            if (_definition == null || _selectionIndex < 0) return;
            mapXZ = ClampToMap(mapXZ);
            switch (_selectionKind)
            {
                case MapAuthoringSelectionKind.Mountain:
                    _definition.Mountains[_selectionIndex].Center = mapXZ;
                    break;
                case MapAuthoringSelectionKind.Lake:
                    _definition.Lakes[_selectionIndex].Center = mapXZ;
                    break;
                case MapAuthoringSelectionKind.GroundPatch:
                    _definition.GroundPatches[_selectionIndex].Center = mapXZ;
                    break;
                case MapAuthoringSelectionKind.Forest:
                    if (!TryMoveForest(_definition.Forests[_selectionIndex], mapXZ)) return;
                    break;
                case MapAuthoringSelectionKind.River:
                {
                    AuthoredRiverPlacement river = _definition.Rivers[_selectionIndex];
                    if (!river.TryGetBezier(out Vector2 start, out Vector2 control, out Vector2 end)) return;
                    float world = GetWorldSize();
                    if (_selectionEndpoint == 2)
                    {
                        control = ClampToMap(mapXZ);
                    }
                    else
                    {
                        mapXZ = MapAuthoringPreview2D.SnapToNearestEdge(mapXZ, world);
                        if (_selectionEndpoint <= 0) start = mapXZ;
                        else end = mapXZ;
                    }

                    river.SetBezier(start, control, end);
                    break;
                }
                case MapAuthoringSelectionKind.Bridge:
                {
                    AuthoredBridgePlacement bridge = _definition.Bridges[_selectionIndex];
                    ApplyBridgeSnap(bridge, mapXZ, updateScale: false);
                    break;
                }
                case MapAuthoringSelectionKind.MagicStone:
                    _definition.MagicStones[_selectionIndex].Center = mapXZ;
                    break;
                case MapAuthoringSelectionKind.Rock:
                    if (!TryMoveFeature(_definition.Rocks[_selectionIndex], FeatureType.Rock, mapXZ)) return;
                    break;
                case MapAuthoringSelectionKind.ScatterTree:
                    if (!TryMoveFeature(_definition.Trees[_selectionIndex], FeatureType.Tree, mapXZ)) return;
                    break;
                case MapAuthoringSelectionKind.ForestTree:
                    if (!TryMoveFeature(
                            _definition.Forests[_selectionForestIndex].Trees[_selectionIndex],
                            FeatureType.Tree,
                            mapXZ)) return;
                    break;
                default:
                    return;
            }

            MarkDirty();
            QueuePreviewRebuild();
        }

        private bool TryMoveFeature(
            AuthoredPointFeaturePlacement placement,
            FeatureType type,
            Vector2 candidate)
        {
            MapData map = AuthoredMapBuilder.Build(_definition);
            if (!AuthoredFeaturePlacementValidator.TryValidate(
                    map, type, placement.Center, candidate, null, out string reason))
            {
                _status = reason;
                return false;
            }
            placement.Center = candidate;
            _status = null;
            return true;
        }

        private bool TryMoveForest(AuthoredForestPlacement forest, Vector2 candidate)
        {
            Vector2 delta = candidate - forest.Center;
            if (!_definition.HasFixedFeaturePlacements || forest.Trees == null || forest.Trees.Count == 0)
            {
                forest.Center = candidate;
                return true;
            }

            MapData map = AuthoredMapBuilder.Build(_definition);
            var excluded = new List<Vector2>(forest.Trees.Count);
            for (int i = 0; i < forest.Trees.Count; i++) excluded.Add(forest.Trees[i].Center);
            for (int i = 0; i < forest.Trees.Count; i++)
            {
                Vector2 next = forest.Trees[i].Center + delta;
                if (!AuthoredFeaturePlacementValidator.TryValidate(
                        map, FeatureType.Tree, forest.Trees[i].Center, next, excluded, out string reason))
                {
                    _status = reason;
                    return false;
                }
            }
            forest.Center = candidate;
            for (int i = 0; i < forest.Trees.Count; i++) forest.Trees[i].Center += delta;
            _status = null;
            return true;
        }

        private void DeleteSelection()
        {
            if (_definition == null || _selectionIndex < 0) return;
            if (_selectionKind == MapAuthoringSelectionKind.Rock ||
                _selectionKind == MapAuthoringSelectionKind.ScatterTree ||
                _selectionKind == MapAuthoringSelectionKind.ForestTree)
            {
                _status = "確定した岩・木は移動できます。個別削除は未対応です";
                return;
            }
            RecordUndo("配置を削除");
            switch (_selectionKind)
            {
                case MapAuthoringSelectionKind.Mountain:
                    _definition.Mountains.RemoveAt(_selectionIndex);
                    break;
                case MapAuthoringSelectionKind.Lake:
                    _definition.Lakes.RemoveAt(_selectionIndex);
                    break;
                case MapAuthoringSelectionKind.GroundPatch:
                    _definition.GroundPatches.RemoveAt(_selectionIndex);
                    break;
                case MapAuthoringSelectionKind.Forest:
                    _definition.Forests.RemoveAt(_selectionIndex);
                    break;
                case MapAuthoringSelectionKind.River:
                    _definition.Rivers.RemoveAt(_selectionIndex);
                    break;
                case MapAuthoringSelectionKind.Bridge:
                    _definition.Bridges.RemoveAt(_selectionIndex);
                    break;
                case MapAuthoringSelectionKind.MagicStone:
                    _definition.MagicStones.RemoveAt(_selectionIndex);
                    break;
                default:
                    return;
            }

            ClearSelection();
            MarkDirty();
            QueuePreviewRebuild();
        }

        private AuthoredPointFeaturePlacement GetSelectedPointFeature()
        {
            return _selectionKind switch
            {
                MapAuthoringSelectionKind.Rock => _definition.Rocks[_selectionIndex],
                MapAuthoringSelectionKind.ScatterTree => _definition.Trees[_selectionIndex],
                MapAuthoringSelectionKind.ForestTree =>
                    _definition.Forests[_selectionForestIndex].Trees[_selectionIndex],
                _ => null,
            };
        }

        private void ApplyToScene3D()
        {
            if (_definition == null || _definition.SharedConfig == null)
            {
                _status = "マップと共通設定が必要です";
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                _status = "コンパイル中です。完了後に再実行してください";
                return;
            }

            MapSceneHost host = Object.FindAnyObjectByType<MapSceneHost>();
            if (host == null)
            {
                _status = "シーンに MapSceneHost がありません（旧 MapGenerator 参照が切れていないか確認）";
                return;
            }

            try
            {
                _status = "シーンへ反映中…（3D描画 → NavMesh/ルート保存）";
                Repaint();
                bool baked = AuthoredMapNavBake.BakeAndSave(_definition, host, out string status);
                _status = status;
                if (baked && _definition.AssaultRoutes.Count > 0)
                    ValidateAssaultRoutes();
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                _status = "シーン反映に失敗しました（Consoleを確認）";
            }
        }

        private void ReloadStampPalette()
        {
            _mountainStamps = LoadAssets<HeightStampShape>();
            _lakeStamps = LoadAssets<LakeStampShape>();
            _groundStamps = LoadAssets<GroundPatchStampShape>();
            _forestStamps = LoadAssets<ForestClusterStampShape>();
            _riverStamps = LoadAssets<RiverShape>();

            if (_selectedMountain == null && _mountainStamps.Count > 0) _selectedMountain = _mountainStamps[0];
            if (_selectedLake == null && _lakeStamps.Count > 0) _selectedLake = _lakeStamps[0];
            if (_selectedGround == null && _groundStamps.Count > 0) _selectedGround = _groundStamps[0];
            if (_selectedForest == null && _forestStamps.Count > 0) _selectedForest = _forestStamps[0];
            if (_selectedRiver == null && _riverStamps.Count > 0) _selectedRiver = _riverStamps[0];
        }

        private static List<T> LoadAssets<T>() where T : Object
        {
            var list = new List<T>();
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { StampRoot });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) list.Add(asset);
            }

            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return list;
        }

        private void CreateNewAsset()
        {
            EnsureAuthoredFolder();
            string path = AssetDatabase.GenerateUniqueAssetPath($"{DefaultFolder}/AuthoredMap.asset");
            var asset = CreateInstance<AuthoredMapDefinition>();
            asset.SharedConfig = AssetDatabase.LoadAssetAtPath<MapConfig>(DefaultConfigPath);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            _definition = asset;
            ClearSelection();
            QueuePreviewRebuild(immediate: true);
            _status = $"新規作成しました: {path}";
        }

        private static void EnsureAuthoredFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder("Assets/Data/Map")) AssetDatabase.CreateFolder("Assets/Data", "Map");
            if (!AssetDatabase.IsValidFolder("Assets/Data/Map/Map")) AssetDatabase.CreateFolder("Assets/Data/Map", "Map");
            if (!AssetDatabase.IsValidFolder(DefaultFolder)) AssetDatabase.CreateFolder("Assets/Data/Map/Map", "Authored");
        }

        private void SaveAsset()
        {
            if (_definition == null) return;
            EditorUtility.SetDirty(_definition);
            AssetDatabase.SaveAssets();
            _status = GetSaveStatusMessage();
        }

        private void CaptureFeaturePlacements()
        {
            RecordUndo("岩・木の配置を確定");
            _lastPreviewMap = AuthoredMapBuilder.CaptureFeaturePlacements(_definition);
            MarkDirty();
            ClearSelection();
            _pickFixedFeatures = true;
            _status = $"岩 {_definition.Rocks.Count} 個・木 {CountFixedTrees()} 本の配置を確定しました";
            QueuePreviewRebuild(immediate: true);
        }

        private void ResetFeaturePlacements()
        {
            RecordUndo("岩・木を自動配置に戻す");
            _definition.HasFixedFeaturePlacements = false;
            _definition.Rocks.Clear();
            _definition.Trees.Clear();
            for (int i = 0; i < _definition.Forests.Count; i++)
            {
                _definition.Forests[i].Trees?.Clear();
                _definition.Forests[i].TreeLayoutFingerprint = 0;
            }
            MarkDirty();
            ClearSelection();
            _pickFixedFeatures = false;
            _status = "岩・木を自動配置に戻しました";
            QueuePreviewRebuild(immediate: true);
        }

        private int CountFixedTrees()
        {
            int count = _definition.Trees.Count;
            for (int i = 0; i < _definition.Forests.Count; i++)
                count += _definition.Forests[i].Trees?.Count ?? 0;
            return count;
        }

        private string GetSaveStatusMessage()
        {
            if (_definition == null) return "保存するマップがありません";
            string path = AssetDatabase.GetAssetPath(_definition);
            return string.IsNullOrEmpty(path)
                ? "保存しました（パス不明）"
                : $"保存しました\n{path}";
        }

        private static MountainKind DrawMountainKindPopup(MountainKind current)
        {
            int index = current == MountainKind.Large ? 0 : 1;
            index = EditorGUILayout.Popup("種類", index, new[] { "大山", "小山" });
            return index == 0 ? MountainKind.Large : MountainKind.Small;
        }

        private void QueuePreviewRebuild(bool immediate = false)
        {
            if (immediate)
            {
                _rebuildQueued = false;
                RebuildPreviewNow();
                return;
            }

            _rebuildQueued = true;
            _rebuildAt = EditorApplication.timeSinceStartup + PreviewDebounceSeconds;
        }

        private void RebuildPreviewNow()
        {
            DestroyPreview();
            _lastPreviewMap = null;
            _validatedPreviewRoutes.Clear();
            if (_definition == null || _definition.SharedConfig == null) return;

            try
            {
                _lastPreviewMap = AuthoredMapBuilder.Build(_definition);
                _previewTex = MapAuthoringPreview2D.BuildBackground(
                    _lastPreviewMap,
                    includeTreesAndRocks: false);
                if (_definition.HasValidBakedAssaultRoutes)
                {
                    MapData baked = _definition.BakedMapData.CreateRuntimeMap();
                    _validatedPreviewRoutes.AddRange(baked.AssaultRoutes);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                _status = "2D プレビューの構築に失敗";
                _lastPreviewMap = null;
            }
        }

        private void DestroyPreview()
        {
            if (_previewTex == null) return;
            DestroyImmediate(_previewTex);
            _previewTex = null;
        }

        private float GetWorldSize() =>
            _definition?.SharedConfig != null ? _definition.SharedConfig.WorldSize : 60f;

        private void ApplyBridgeSnap(AuthoredBridgePlacement bridge, Vector2 desiredCenter, bool updateScale = true)
        {
            desiredCenter = ClampToMap(desiredCenter);
            BridgeBuildUtility.ResolveAuthoredPlacement(
                _lastPreviewMap,
                _definition != null ? _definition.SharedConfig : null,
                desiredCenter,
                BridgeSnapRadiusMeters,
                out Vector2 center,
                out float rotationDeg,
                out Vector3 scale);
            bridge.Center = ClampToMap(center);
            bridge.RotationDeg = rotationDeg;
            if (updateScale
                || bridge.Scale.x <= 0.0001f
                || bridge.Scale.y <= 0.0001f
                || bridge.Scale.z <= 0.0001f)
                bridge.Scale = scale;
        }

        private Vector2 ClampToMap(Vector2 mapXZ)
        {
            float world = GetWorldSize();
            return new Vector2(Mathf.Clamp(mapXZ.x, 0f, world), Mathf.Clamp(mapXZ.y, 0f, world));
        }

        private void SetSelection(
            MapAuthoringSelectionKind kind,
            int index,
            int endpoint = -1,
            int forestIndex = -1)
        {
            _selectionKind = kind;
            _selectionIndex = index;
            _selectionEndpoint = endpoint;
            _selectionForestIndex = forestIndex;
        }

        private void ClearSelection()
        {
            _selectionKind = MapAuthoringSelectionKind.None;
            _selectionIndex = -1;
            _selectionEndpoint = -1;
            _selectionForestIndex = -1;
        }

        private void ClearPendingRiverStart()
        {
            _hasPendingRiverStart = false;
            _pendingRiverStart = default;
        }

        private void RecordUndo(string label)
        {
            if (_definition != null)
                Undo.RecordObject(_definition, label);
        }

        private void MarkDirty()
        {
            if (_definition != null)
                EditorUtility.SetDirty(_definition);
            _status = null;
            _assaultRouteFailures.Clear();
        }
    }
}
#endif
