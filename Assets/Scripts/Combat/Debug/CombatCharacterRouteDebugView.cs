using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatCharacterRouteDebugView : CombatDebugBehaviour
{
    private const string GeneratedRootName = "GeneratedCharacterRoutes";
    private const float LineWidth = 0.16f;

    public override string InspectorDescription => "全キャラクターの現在の移動経路を、地面上の色付きラインで表示します。";

    [SerializeField, Min(0f)] private float _surfaceOffset = 0.08f;
    [SerializeField] private Color _allyRouteColor = new(0.15f, 0.75f, 1f, 0.9f);
    [SerializeField] private Color _enemyRouteColor = new(1f, 0.25f, 0.2f, 0.9f);
    [SerializeField, Min(0.1f)] private float _refreshIntervalSeconds = 1f;

    private readonly Dictionary<CombatCharacterBody, LineRenderer> _lines = new();
    private Transform _generatedRoot;
    private float _nextRefreshTime;

    private void OnEnable()
    {
        EnsureGeneratedRoot();
        RefreshCharacters();
    }

    private void Update()
    {
        if (Time.unscaledTime >= _nextRefreshTime)
        {
            RefreshCharacters();
        }

        foreach (KeyValuePair<CombatCharacterBody, LineRenderer> pair in _lines)
        {
            RenderRoute(pair.Key, pair.Value);
        }
    }

    private void OnDisable()
    {
        foreach (LineRenderer line in _lines.Values)
        {
            if (line != null) line.enabled = false;
        }
    }

    private void OnValidate()
    {
        foreach (KeyValuePair<CombatCharacterBody, LineRenderer> pair in _lines)
        {
            if (pair.Value == null) continue;

            Character character = pair.Key != null ? pair.Key.GetComponent<Character>() : null;
            ApplyStyle(pair.Value, ResolveColor(character));
        }
    }

    private void RefreshCharacters()
    {
        CombatCharacterSystem system = ResolveCharacterSystem();
        if (system == null)
        {
            foreach (LineRenderer line in _lines.Values)
            {
                if (line != null) line.enabled = false;
            }

            _nextRefreshTime = Time.unscaledTime + _refreshIntervalSeconds;
            return;
        }

        var activeBodies = new HashSet<CombatCharacterBody>();
        CollectBodies(system.AllyCharacters, activeBodies);
        CollectBodies(system.EnemyCharacters, activeBodies);

        var staleBodies = new List<CombatCharacterBody>();
        foreach (KeyValuePair<CombatCharacterBody, LineRenderer> pair in _lines)
        {
            if (pair.Key == null || !activeBodies.Contains(pair.Key))
            {
                if (pair.Value != null) Destroy(pair.Value.gameObject);
                staleBodies.Add(pair.Key);
            }
        }

        for (int i = 0; i < staleBodies.Count; i++)
        {
            _lines.Remove(staleBodies[i]);
        }

        _nextRefreshTime = Time.unscaledTime + _refreshIntervalSeconds;
    }

    private void CollectBodies(IReadOnlyList<Character> characters, HashSet<CombatCharacterBody> activeBodies)
    {
        if (characters == null) return;

        for (int i = 0; i < characters.Count; i++)
        {
            Character character = characters[i];
            if (character == null) continue;

            CombatCharacterBody body = character.GetComponent<CombatCharacterBody>();
            if (body == null) continue;

            activeBodies.Add(body);
            if (!_lines.ContainsKey(body))
            {
                _lines.Add(body, CreateLine(character));
            }
            else
            {
                ApplyStyle(_lines[body], ResolveColor(character));
            }
        }
    }

    private void RenderRoute(CombatCharacterBody body, LineRenderer line)
    {
        if (body == null || line == null) return;

        Vector3[] corners = body.CurrentRouteCorners;
        if (!body.isActiveAndEnabled || corners == null || corners.Length < 2)
        {
            line.enabled = false;
            line.positionCount = 0;
            return;
        }

        line.enabled = true;
        line.positionCount = corners.Length;
        for (int i = 0; i < corners.Length; i++)
        {
            line.SetPosition(i, corners[i] + Vector3.up * _surfaceOffset);
        }
    }

    private LineRenderer CreateLine(Character character)
    {
        EnsureGeneratedRoot();
        var lineObject = new GameObject("Route_" + character.name, typeof(LineRenderer));
        lineObject.transform.SetParent(_generatedRoot, worldPositionStays: false);

        LineRenderer line = lineObject.GetComponent<LineRenderer>();
        ApplyStyle(line, ResolveColor(character));
        line.enabled = false;
        return line;
    }

    private void ApplyStyle(LineRenderer line, Color color)
    {
        if (line == null) return;

        line.useWorldSpace = true;
        line.widthMultiplier = LineWidth;
        line.numCornerVertices = 3;
        line.numCapVertices = 3;
        line.textureMode = LineTextureMode.Stretch;
        line.startColor = color;
        line.endColor = color;
        line.sharedMaterial ??= CreateDefaultMaterial(color);
        ApplyColor(line.sharedMaterial, color);
    }

    private static Material CreateDefaultMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;

        var material = new Material(shader) { name = "CharacterRouteDebugMaterial" };
        ApplyColor(material, color);
        return material;
    }

    private static void ApplyColor(Material material, Color color)
    {
        if (material == null) return;

        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
    }

    private Color ResolveColor(Character character)
    {
        return character != null && character.Team == CombatTeam.Enemy
            ? _enemyRouteColor
            : _allyRouteColor;
    }

    private void EnsureGeneratedRoot()
    {
        if (_generatedRoot != null) return;

        Transform existing = transform.Find(GeneratedRootName);
        if (existing != null)
        {
            _generatedRoot = existing;
            return;
        }

        var root = new GameObject(GeneratedRootName);
        root.transform.SetParent(transform, worldPositionStays: false);
        _generatedRoot = root.transform;
    }

    private static CombatCharacterSystem ResolveCharacterSystem()
    {
        CombatSceneContext context = CombatSceneContext.Instance;
        if (context != null && context.CharacterSystem != null) return context.CharacterSystem;
        return FindAnyObjectByType<CombatCharacterSystem>();
    }
}
