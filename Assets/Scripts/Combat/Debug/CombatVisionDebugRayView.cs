using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CombatVisionDebugCharacterSetting
{
    [Tooltip("ラインを出す観測者です。")]
    public Character Character;
    [Tooltip("このキャラクターの認識関係ラインを表示します。")]
    public bool ShowLines = true;
    [Tooltip("このキャラクターから全キャラクターの方向へ、視認射程内だけ遮蔽物確認Rayを表示します。初期値はOFFです。")]
    public bool ShowObstructionRays;
    [Tooltip("このキャラクターの水平・垂直視野角ガイドを表示します。初期値はONです。")]
    public bool ShowFieldOfView = true;
}

[DisallowMultipleComponent]
[ExecuteAlways]
public sealed class CombatVisionDebugRayView : CombatDebugBehaviour
{
    private const string GeneratedRootName = "GeneratedVisionRelations";
    private const float DirectLineWidth = 0.08f;
    private const float SharedLineWidth = DirectLineWidth * 0.6f;
    private const float ObstructionRayWidth = 0.04f;
    private const float FieldOfViewLineWidth = 0.03f;
    private const float FieldOfViewDisplayDistance = 2f;
    private const int FieldOfViewArcSegments = 24;

    public override string InspectorDescription => "認識関係を4色で表示します。遮蔽物確認Rayは視野内・遮蔽・視野外を色分けし、水平視野角を足元に表示します。";

    [Tooltip("観測者ごとに認識ラインの表示・非表示を切り替えます。")]
    [SerializeField] private List<CombatVisionDebugCharacterSetting> _characters = new();
    [Tooltip("味方が味方を視認している線の色です。")]
    [SerializeField] private Color _allyToAllyColor = new(0.15f, 0.55f, 1f, 0.95f);
    [Tooltip("味方が敵を視認している線の色です。")]
    [SerializeField] private Color _allyToEnemyColor = new(0.65f, 0.25f, 1f, 0.95f);
    [Tooltip("敵が味方を視認している線の色です。")]
    [SerializeField] private Color _enemyToAllyColor = new(1f, 0.55f, 0.1f, 0.95f);
    [Tooltip("敵が敵を視認している線の色です。")]
    [SerializeField] private Color _enemyToEnemyColor = new(1f, 0.2f, 0.15f, 0.95f);
    [Tooltip("遮られたRayの色です。Rayは最初の遮蔽物で止まります。")]
    [SerializeField] private Color _blockedRayColor = new(1f, 0.85f, 0.1f, 0.8f);
    [Tooltip("遮られず対象まで届いた確認用Rayの色です。")]
    [SerializeField] private Color _clearRayColor = new(0.2f, 1f, 0.45f, 0.55f);
    [Tooltip("物理的には通るものの、キャラクターの向きに対して視野角外にあるRayの色です。")]
    [SerializeField] private Color _outsideFieldOfViewRayColor = new(0.55f, 0.55f, 0.55f, 0.45f);
    [Tooltip("視野角ガイドの塗りつぶし透明度です。")]
    [SerializeField, Range(0.02f, 0.5f)] private float _fieldOfViewFillAlpha = 0.15f;
    [SerializeField, Range(0.05f, 1f)] private float _sharedColorAlpha = 0.3f;
    [SerializeField, Min(0f)] private float _lineHeightOffset = 1.2f;
    [SerializeField, Min(0.1f)] private float _refreshIntervalSeconds = 1f;

    private readonly List<Character> _allCharacters = new();
    private readonly List<LineRenderer> _linePool = new();
    private readonly List<MeshFilter> _fieldOfViewFillPool = new();
    private Transform _generatedRoot;
    private int _usedLineCount;
    private int _usedFieldOfViewFillCount;
    private float _nextRefreshTime;

    private void Reset()
    {
        RefreshCharacters();
    }

    private void OnEnable()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        EnsureGeneratedRoot();
        RefreshCharacters();
#else
        enabled = false;
#endif
    }

    private void Update()
    {
        if (Application.isPlaying && Time.unscaledTime >= _nextRefreshTime)
        {
            RefreshCharacters();
        }

        _usedLineCount = 0;
        _usedFieldOfViewFillCount = 0;
        for (int i = 0; i < _characters.Count; i++)
        {
            CombatVisionDebugCharacterSetting setting = _characters[i];
            if (setting?.Character == null) continue;
            if (setting.ShowLines) DrawRelations(setting.Character);
            if (setting.ShowObstructionRays) DrawObstructionRays(setting.Character);
            if (setting.ShowFieldOfView) DrawFieldOfView(setting.Character);
        }

        HideUnusedLines();
        HideUnusedFieldOfViewFills();
    }

    private void OnDisable()
    {
        for (int i = 0; i < _linePool.Count; i++)
        {
            if (_linePool[i] != null) _linePool[i].enabled = false;
        }
        for (int i = 0; i < _fieldOfViewFillPool.Count; i++)
        {
            if (_fieldOfViewFillPool[i] != null) _fieldOfViewFillPool[i].gameObject.SetActive(false);
        }
    }

    [ContextMenu("キャラクター一覧を更新")]
    private void RefreshCharacters()
    {
        CombatCharacterSystem system = ResolveCharacterSystem();
        if (system == null)
        {
            _allCharacters.Clear();
            _nextRefreshTime = Time.unscaledTime + _refreshIntervalSeconds;
            return;
        }

        _allCharacters.Clear();
        AddCharacters(system.AllyCharacters);
        AddCharacters(system.EnemyCharacters);

        var previousSettings = new Dictionary<Character, bool>();
        var previousObstructionSettings = new Dictionary<Character, bool>();
        var previousFieldOfViewSettings = new Dictionary<Character, bool>();
        for (int i = 0; i < _characters.Count; i++)
        {
            CombatVisionDebugCharacterSetting setting = _characters[i];
            if (setting?.Character != null)
            {
                previousSettings[setting.Character] = setting.ShowLines;
                previousObstructionSettings[setting.Character] = setting.ShowObstructionRays;
                previousFieldOfViewSettings[setting.Character] = setting.ShowFieldOfView;
            }
        }

        _characters.Clear();
        for (int i = 0; i < _allCharacters.Count; i++)
        {
            Character character = _allCharacters[i];
            _characters.Add(new CombatVisionDebugCharacterSetting
            {
                Character = character,
                ShowLines = !previousSettings.TryGetValue(character, out bool showLines) || showLines,
                ShowObstructionRays = previousObstructionSettings.TryGetValue(character, out bool showRays) && showRays,
                ShowFieldOfView = !previousFieldOfViewSettings.TryGetValue(character, out bool showFov) || showFov,
            });
        }

        _nextRefreshTime = Time.unscaledTime + _refreshIntervalSeconds;
    }

    private void AddCharacters(IReadOnlyList<Character> characters)
    {
        if (characters == null) return;

        for (int i = 0; i < characters.Count; i++)
        {
            Character character = characters[i];
            if (character != null && !_allCharacters.Contains(character))
            {
                _allCharacters.Add(character);
            }
        }
    }

    private void DrawRelations(Character observer)
    {
        if (Application.isPlaying && observer.HP <= 0) return;

        CombatVision vision = observer.Vision;
        if (vision == null) return;

        for (int i = 0; i < _allCharacters.Count; i++)
        {
            Character target = _allCharacters[i];
            if (target == null || target == observer) continue;
            if (Application.isPlaying && target.HP <= 0) continue;
            if (!vision.HasLineOfSight(target.transform)) continue;

            DrawLine(observer.transform.position, target.transform.position, ResolveColor(observer, target), DirectLineWidth);
        }

        if (!Application.isPlaying) return;

        IReadOnlyList<CombatVisionDebugMemorySnapshot> memories = vision.GetDebugMemorySnapshots();
        for (int i = 0; i < memories.Count; i++)
        {
            CombatVisionDebugMemorySnapshot memory = memories[i];
            if (!memory.HasPosition || memory.Source != CombatVisionMemorySource.Shared) continue;
            if (memory.Target != null && vision.HasLineOfSight(memory.Target.transform)) continue;

            Color color = Color.Lerp(ResolveColor(observer, memory.Target), Color.white, 0.65f);
            color.a *= _sharedColorAlpha;
            Vector3 targetPosition = memory.Target != null ? memory.Target.transform.position : memory.LastSeenPosition;
            DrawLine(observer.transform.position, targetPosition, color, SharedLineWidth);
        }
    }

    private void DrawObstructionRays(Character observer)
    {
        if (Application.isPlaying && observer.HP <= 0) return;

        CombatVision vision = observer.Vision;
        if (vision == null) return;

        for (int i = 0; i < _allCharacters.Count; i++)
        {
            Character target = _allCharacters[i];
            if (target == null || target == observer) continue;
            if (Application.isPlaying && target.HP <= 0) continue;
            if (!vision.TryGetSightRay(target.transform, out Vector3 origin, out Vector3 end, out bool blocked)) continue;

            bool withinFieldOfView = vision.IsWithinFieldOfView(target.transform);
            Color color = !withinFieldOfView
                ? _outsideFieldOfViewRayColor
                : blocked
                    ? _blockedRayColor
                    : _clearRayColor;
            DrawLine(origin, end, color, ObstructionRayWidth, applyHeightOffset: false);
        }
    }

    private void DrawFieldOfView(Character observer)
    {
        CombatVision vision = observer.Vision;
        if (vision == null) return;

        Vector3 origin = vision.EyePosition;
        Vector3 forward = observer.transform.forward.normalized;
        Vector3 right = observer.transform.right.normalized;
        Color color = observer.Team == CombatTeam.Enemy ? _enemyToEnemyColor : _allyToAllyColor;
        DrawFieldOfViewArc(origin, forward, Vector3.up, vision.HorizontalFovDegrees, color);
        DrawFieldOfViewArc(origin, forward, right, vision.VerticalFovDegrees, color);
    }

    private void DrawFieldOfViewArc(Vector3 origin, Vector3 forward, Vector3 axis, float angleDegrees, Color color)
    {
        DrawFieldOfViewFill(origin, forward, axis, angleDegrees, color);
        float halfAngle = angleDegrees * 0.5f;
        Vector3 firstDirection = Quaternion.AngleAxis(-halfAngle, axis) * forward;
        Vector3 previous = origin + firstDirection * FieldOfViewDisplayDistance;
        DrawLine(origin, previous, color, FieldOfViewLineWidth, applyHeightOffset: false);

        for (int i = 1; i <= FieldOfViewArcSegments; i++)
        {
            float angle = Mathf.Lerp(-halfAngle, halfAngle, i / (float)FieldOfViewArcSegments);
            Vector3 direction = Quaternion.AngleAxis(angle, axis) * forward;
            Vector3 current = origin + direction * FieldOfViewDisplayDistance;
            DrawLine(previous, current, color, FieldOfViewLineWidth, applyHeightOffset: false);
            previous = current;
        }

        DrawLine(origin, previous, color, FieldOfViewLineWidth, applyHeightOffset: false);
    }

    private void DrawFieldOfViewFill(Vector3 origin, Vector3 forward, Vector3 axis, float angleDegrees, Color color)
    {
        MeshFilter meshFilter = GetFieldOfViewFill(_usedFieldOfViewFillCount++);
        meshFilter.transform.position = origin;
        meshFilter.transform.rotation = Quaternion.identity;

        var vertices = new Vector3[FieldOfViewArcSegments + 2];
        var triangles = new int[FieldOfViewArcSegments * 3];
        vertices[0] = Vector3.zero;
        float halfAngle = angleDegrees * 0.5f;
        for (int i = 0; i <= FieldOfViewArcSegments; i++)
        {
            float angle = Mathf.Lerp(-halfAngle, halfAngle, i / (float)FieldOfViewArcSegments);
            vertices[i + 1] = Quaternion.AngleAxis(angle, axis) * forward * FieldOfViewDisplayDistance;
            if (i == FieldOfViewArcSegments) continue;

            int triangleIndex = i * 3;
            triangles[triangleIndex] = 0;
            triangles[triangleIndex + 1] = i + 1;
            triangles[triangleIndex + 2] = i + 2;
        }

        Mesh mesh = meshFilter.sharedMesh;
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        Color fillColor = color;
        fillColor.a = _fieldOfViewFillAlpha;
        ApplyColor(meshFilter.GetComponent<MeshRenderer>().sharedMaterial, fillColor);
    }

    private MeshFilter GetFieldOfViewFill(int index)
    {
        EnsureGeneratedRoot();
        while (_fieldOfViewFillPool.Count <= index)
        {
            var fillObject = new GameObject("FieldOfViewFill_" + _fieldOfViewFillPool.Count, typeof(MeshFilter), typeof(MeshRenderer));
            fillObject.transform.SetParent(_generatedRoot, worldPositionStays: true);
            if (!Application.isPlaying) fillObject.hideFlags = HideFlags.DontSaveInEditor;

            MeshFilter meshFilter = fillObject.GetComponent<MeshFilter>();
            meshFilter.sharedMesh = new Mesh { name = "FieldOfViewDebugMesh" };
            meshFilter.GetComponent<MeshRenderer>().sharedMaterial = CreateFieldOfViewMaterial();
            _fieldOfViewFillPool.Add(meshFilter);
        }

        MeshFilter result = _fieldOfViewFillPool[index];
        result.gameObject.SetActive(true);
        return result;
    }

    private void HideUnusedFieldOfViewFills()
    {
        for (int i = _usedFieldOfViewFillCount; i < _fieldOfViewFillPool.Count; i++)
        {
            if (_fieldOfViewFillPool[i] != null) _fieldOfViewFillPool[i].gameObject.SetActive(false);
        }
    }

    private void DrawLine(Vector3 from, Vector3 to, Color color, float width, bool applyHeightOffset = true)
    {
        LineRenderer line = GetLine(_usedLineCount++);
        line.enabled = true;
        line.widthMultiplier = width;
        line.startColor = color;
        line.endColor = color;
        ApplyColor(line.sharedMaterial, color);
        line.positionCount = 2;
        Vector3 offset = applyHeightOffset ? Vector3.up * _lineHeightOffset : Vector3.zero;
        line.SetPosition(0, from + offset);
        line.SetPosition(1, to + offset);
    }

    private LineRenderer GetLine(int index)
    {
        EnsureGeneratedRoot();
        while (_linePool.Count <= index)
        {
            var lineObject = new GameObject("VisionRelation_" + _linePool.Count, typeof(LineRenderer));
            lineObject.transform.SetParent(_generatedRoot, worldPositionStays: false);
            if (!Application.isPlaying) lineObject.hideFlags = HideFlags.DontSaveInEditor;

            LineRenderer line = lineObject.GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.sharedMaterial = CreateDefaultMaterial();
            _linePool.Add(line);
        }

        return _linePool[index];
    }

    private void HideUnusedLines()
    {
        for (int i = _usedLineCount; i < _linePool.Count; i++)
        {
            if (_linePool[i] == null) continue;
            _linePool[i].enabled = false;
            _linePool[i].positionCount = 0;
        }
    }

    private Color ResolveColor(Character observer, Character target)
    {
        bool observerIsEnemy = observer != null && observer.Team == CombatTeam.Enemy;
        bool targetIsEnemy = target != null && target.Team == CombatTeam.Enemy;
        if (observerIsEnemy) return targetIsEnemy ? _enemyToEnemyColor : _enemyToAllyColor;
        return targetIsEnemy ? _allyToEnemyColor : _allyToAllyColor;
    }

    private static Material CreateDefaultMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");
        return shader != null ? new Material(shader) { name = "VisionRelationDebugMaterial" } : null;
    }

    private static Material CreateFieldOfViewMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");
        return shader != null ? new Material(shader) { name = "FieldOfViewDebugMaterial" } : null;
    }

    private static void ApplyColor(Material material, Color color)
    {
        if (material == null) return;

        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
    }

    private void EnsureGeneratedRoot()
    {
        if (_generatedRoot != null) return;

        Transform existing = transform.Find(GeneratedRootName);
        if (existing != null)
        {
            if (!Application.isPlaying) existing.gameObject.hideFlags = HideFlags.DontSaveInEditor;
            _generatedRoot = existing;
            return;
        }

        var root = new GameObject(GeneratedRootName);
        root.transform.SetParent(transform, worldPositionStays: false);
        if (!Application.isPlaying) root.hideFlags = HideFlags.DontSaveInEditor;
        _generatedRoot = root.transform;
    }

    private static CombatCharacterSystem ResolveCharacterSystem()
    {
        CombatSceneContext context = CombatSceneContext.Instance;
        if (context != null && context.CharacterSystem != null) return context.CharacterSystem;
        return FindAnyObjectByType<CombatCharacterSystem>();
    }
}
