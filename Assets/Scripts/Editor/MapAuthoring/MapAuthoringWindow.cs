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
        MagicStone,
    }

    public enum MapAuthoringSelectionKind
    {
        None = 0,
        Mountain,
        Lake,
        GroundPatch,
        Forest,
        River,
        MagicStone,
    }

    public sealed class MapAuthoringWindow : EditorWindow
    {
        private const string DefaultFolder = "Assets/Data/Map/Map/Authored";
        private const string StampRoot = "Assets/Data/Map/Map/Stamps";
        private const string DefaultConfigPath = "Assets/Data/Map/Map/Configs/MapGenerationConfig.asset";
        private const float RightPanelWidth = 300f;
        private const float PickRadiusMeters = 3f;
        private const double PreviewDebounceSeconds = 0.2;

        private AuthoredMapDefinition _definition;
        private MapAuthoringTool _tool = MapAuthoringTool.Select;
        private MapAuthoringSelectionKind _selectionKind;
        private int _selectionIndex = -1;

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

        private Texture2D _previewTex;
        private MapData _lastPreviewMap;
        private double _rebuildAt;
        private bool _rebuildQueued;
        private string _status;
        private Vector2 _paletteScroll;
        private Vector2 _listScroll;
        private bool _dragging;
        private bool _bakeNavMeshOnApply;

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
        }

        private void OnUndoRedo()
        {
            QueuePreviewRebuild();
            Repaint();
        }

        private void OnEditorUpdate()
        {
            if (!_rebuildQueued) return;
            if (EditorApplication.timeSinceStartup < _rebuildAt) return;
            _rebuildQueued = false;
            RebuildPreviewNow();
            Repaint();
        }

        private void OnGUI()
        {
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
                    QueuePreviewRebuild(immediate: true);
                }

                if (GUILayout.Button("新規", EditorStyles.toolbarButton, GUILayout.Width(44f)))
                    CreateNewAsset();
                if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(44f)))
                    SaveAsset();
                if (GUILayout.Button("スタンプ再読込", EditorStyles.toolbarButton, GUILayout.Width(100f)))
                    ReloadStampPalette();

                GUILayout.FlexibleSpace();
                _bakeNavMeshOnApply = GUILayout.Toggle(
                    _bakeNavMeshOnApply, "NavMeshも焼く", EditorStyles.toolbarButton, GUILayout.Width(100f));
                if (GUILayout.Button("シーンへ3D反映", EditorStyles.toolbarButton, GUILayout.Width(110f)))
                    ApplyToScene3D();
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                DrawToolToggle(MapAuthoringTool.Select, "選択");
                DrawToolToggle(MapAuthoringTool.Mountain, "山");
                DrawToolToggle(MapAuthoringTool.Lake, "湖");
                DrawToolToggle(MapAuthoringTool.GroundPatch, "沼・雪");
                DrawToolToggle(MapAuthoringTool.Forest, "森");
                DrawToolToggle(MapAuthoringTool.River, "川");
                DrawToolToggle(MapAuthoringTool.MagicStone, "魔石");
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
            DrawPendingRiverLocal(localDraw, world, rect);
            GUI.EndClip();

            // 入力はクリップ前の GUI 座標（drawRect）で扱う
            HandleCanvasInput(drawRect, world);

            GUI.Label(
                new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, 40f),
                "真上からの2D編集。スタンプ配置／川は端＋ベジェ制御点／魔石は手動／橋・散布木・岩は SharedConfig 自動／選択で移動／Delete削除",
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
            MapGenerationConfig config = _definition.SharedConfig;
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

            for (int i = 0; i < _definition.MagicStones.Count; i++)
            {
                AuthoredMagicStonePlacement stone = _definition.MagicStones[i];
                Mark(stone.Center, MagicStoneColor(stone.Type),
                    _selectionKind == MapAuthoringSelectionKind.MagicStone && _selectionIndex == i);
            }
        }

        private static Color MagicStoneColor(FeatureType type) => type switch
        {
            FeatureType.OwnMainStone => new Color(0.25f, 0.55f, 1f),
            FeatureType.OwnSubStone => new Color(0.45f, 0.7f, 1f),
            FeatureType.EnemyMainStone => new Color(1f, 0.3f, 0.3f),
            FeatureType.EnemySubStone => new Color(1f, 0.55f, 0.4f),
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
            Event e = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive);

            if (e.type == EventType.KeyDown)
            {
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
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(RightPanelWidth)))
            {
                if (_definition == null)
                {
                    EditorGUILayout.HelpBox("マップを選ぶか「新規」で作成してください。", MessageType.Info);
                    return;
                }

                EditorGUI.BeginChangeCheck();
                MapGenerationConfig config = (MapGenerationConfig)EditorGUILayout.ObjectField(
                    "共通設定", _definition.SharedConfig, typeof(MapGenerationConfig), false);
                if (EditorGUI.EndChangeCheck())
                {
                    RecordUndo("共通設定を変更");
                    _definition.SharedConfig = config;
                    MarkDirty();
                    QueuePreviewRebuild();
                }

                if (_definition.SharedConfig == null)
                    EditorGUILayout.HelpBox("共通設定が必要です。", MessageType.Warning);

                string assetPath = AssetDatabase.GetAssetPath(_definition);
                if (!string.IsNullOrEmpty(assetPath))
                    EditorGUILayout.HelpBox($"保存先\n{assetPath}", MessageType.None);

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
                case MapAuthoringTool.MagicStone:
                    _selectedMagicStoneType = DrawMagicStoneTypePopup(_selectedMagicStoneType);
                    EditorGUILayout.HelpBox("クリックで魔石を配置。橋・散布木・岩は SharedConfig の自動配置です。", MessageType.None);
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
                FeatureType.OwnSubStone => 1,
                FeatureType.EnemyMainStone => 2,
                FeatureType.EnemySubStone => 3,
                _ => 0,
            };
            index = EditorGUILayout.Popup("種類", index, new[]
            {
                "自軍メイン",
                "自軍サブ",
                "敵軍メイン",
                "敵軍サブ",
            });
            return index switch
            {
                1 => FeatureType.OwnSubStone,
                2 => FeatureType.EnemyMainStone,
                3 => FeatureType.EnemySubStone,
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

            if (GUILayout.Button("削除"))
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
                        e.Shape = shape;
                        e.Center = ClampToMap(center);
                        e.RotationDeg = rotation;
                        e.Scale = scale;
                        MarkDirty();
                        QueuePreviewRebuild();
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
                    break;

                case MapAuthoringTool.River:
                    PlaceRiverEndpoint(mapXZ);
                    return;

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

            void Consider(Vector2 center, MapAuthoringSelectionKind k, int i, int ep = -1)
            {
                float sq = (center - mapXZ).sqrMagnitude;
                if (sq >= best) return;
                best = sq;
                kind = k;
                index = i;
                endpoint = ep;
            }

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

            for (int i = 0; i < _definition.MagicStones.Count; i++)
                Consider(_definition.MagicStones[i].Center, MapAuthoringSelectionKind.MagicStone, i);

            if (kind == MapAuthoringSelectionKind.None)
            {
                ClearSelection();
                return false;
            }

            SetSelection(kind, index, endpoint);
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
                    _definition.Forests[_selectionIndex].Center = mapXZ;
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
                case MapAuthoringSelectionKind.MagicStone:
                    _definition.MagicStones[_selectionIndex].Center = mapXZ;
                    break;
                default:
                    return;
            }

            MarkDirty();
            QueuePreviewRebuild();
        }

        private void DeleteSelection()
        {
            if (_definition == null || _selectionIndex < 0) return;
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

        private void ApplyToScene3D()
        {
            if (_definition == null || _definition.SharedConfig == null)
            {
                _status = "マップと共通設定が必要です";
                return;
            }

            MapGenerator generator = Object.FindAnyObjectByType<MapGenerator>();
            if (generator == null)
            {
                _status = "シーンに MapGenerator がありません";
                return;
            }

            try
            {
                if (generator.Config == null)
                    generator.Config = _definition.SharedConfig;

                _status = _bakeNavMeshOnApply
                    ? "シーンへ反映中…（3D描画 → NavMeshベイク）"
                    : "シーンへ反映中…（3D描画のみ）";
                Repaint();

                MapData map = AuthoredMapBuilder.Build(_definition);
                EnsureRenderComponents(generator);
                bool ok = generator.ApplyMapData(map, render3D: true, bakeNavMesh: _bakeNavMeshOnApply);

                if (!_bakeNavMeshOnApply)
                {
                    _status = "シーンへ3D反映完了（NavMeshは焼いていません）";
                }
                else if (ok)
                {
                    _status = "シーンへ3D反映完了 / NavMeshベイク完了";
                }
                else
                {
                    _status = "シーンへ3D反映はしたが、NavMeshベイクに失敗しました";
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                _status = "シーン反映に失敗しました（Consoleを確認）";
            }
        }

        private static void EnsureRenderComponents(MapGenerator gen)
        {
            if (gen.GetComponent<TerrainRenderer>() == null) Undo.AddComponent<TerrainRenderer>(gen.gameObject);
            if (gen.GetComponent<TerrainSkirtRenderer>() == null) Undo.AddComponent<TerrainSkirtRenderer>(gen.gameObject);
            if (gen.GetComponent<RiverRenderer>() == null) Undo.AddComponent<RiverRenderer>(gen.gameObject);
            if (gen.GetComponent<LakeRenderer>() == null) Undo.AddComponent<LakeRenderer>(gen.gameObject);
            if (gen.GetComponent<BridgeRenderer>() == null) Undo.AddComponent<BridgeRenderer>(gen.gameObject);
            if (gen.GetComponent<FeatureRenderer>() == null) Undo.AddComponent<FeatureRenderer>(gen.gameObject);
            if (gen.GetComponent<CombatNavMeshBuilder>() == null) Undo.AddComponent<CombatNavMeshBuilder>(gen.gameObject);
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
            asset.SharedConfig = AssetDatabase.LoadAssetAtPath<MapGenerationConfig>(DefaultConfigPath);
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
            if (_definition == null || _definition.SharedConfig == null) return;

            try
            {
                _lastPreviewMap = AuthoredMapBuilder.Build(_definition);
                _previewTex = MapAuthoringPreview2D.Build(_lastPreviewMap);
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

        private Vector2 ClampToMap(Vector2 mapXZ)
        {
            float world = GetWorldSize();
            return new Vector2(Mathf.Clamp(mapXZ.x, 0f, world), Mathf.Clamp(mapXZ.y, 0f, world));
        }

        private void SetSelection(MapAuthoringSelectionKind kind, int index, int endpoint = -1)
        {
            _selectionKind = kind;
            _selectionIndex = index;
            _selectionEndpoint = endpoint;
        }

        private void ClearSelection()
        {
            _selectionKind = MapAuthoringSelectionKind.None;
            _selectionIndex = -1;
            _selectionEndpoint = -1;
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
        }
    }
}
#endif
