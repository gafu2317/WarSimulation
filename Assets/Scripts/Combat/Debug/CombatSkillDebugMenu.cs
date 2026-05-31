using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public sealed class CombatSkillDebugMenu : MonoBehaviour
{
    private const float WindowWidth = 860f;
    private const float WindowHeight = 1080f;
    private const float ScrollWidth = 820f;
    private const float ScrollHeight = 980f;
    private const float ButtonHeight = 56f;
    private const int RingSegments = 48;
    private static readonly Color OwnerRingColor = new Color(0.2f, 1f, 0.35f, 0.95f);
    private static readonly Color TargetRingColor = new Color(1f, 0.2f, 0.2f, 0.95f);
    private static readonly Color SharedRingColor = new Color(1f, 0.85f, 0.2f, 0.95f);
    private static readonly Color RangeRingColor = new Color(0.25f, 0.85f, 1f, 0.9f);
    private static readonly Color AreaRingColor = new Color(1f, 0.55f, 0.15f, 0.9f);
    private static readonly Color ValidTargetLineColor = new Color(0.3f, 1f, 1f, 0.95f);
    private static readonly Color InvalidTargetLineColor = new Color(1f, 0.25f, 0.25f, 0.95f);

    [SerializeField] private List<Character> _characters = new();
    [SerializeField] private Transform _pointTargetMarker;
    [SerializeField] private bool _showMenu = true;
    [SerializeField, Min(0.2f)] private float _selectionRingRadius = 0.9f;
    [SerializeField, Min(0.01f)] private float _selectionRingWidth = 0.08f;
    [SerializeField] private float _selectionRingYOffset = 0.05f;

    private readonly Dictionary<Character, Vector3> _initialPositions = new();
    private Rect _windowRect = new Rect(16f, 16f, WindowWidth, WindowHeight);
    private Vector2 _scrollPosition;
    private int _selectedOwnerIndex;
    private int _selectedTargetIndex;
    private int _selectedSkillIndex;
    private string _lastMessage = "Ready";
    private GUIStyle _labelStyle;
    private GUIStyle _buttonStyle;
    private GUIStyle _headerStyle;
    private LineRenderer _ownerRing;
    private LineRenderer _targetRing;
    private LineRenderer _rangeRing;
    private LineRenderer _areaRing;
    private readonly List<LineRenderer> _targetLines = new();

    private void Awake()
    {
        AutoPopulateCharactersIfNeeded();
        CaptureInitialPositions();
        EnsureSelectionRings();
        EnsureSkillVisuals();
    }

    private void Update()
    {
        UpdateSelectionRings();
        UpdateSkillVisuals();
    }

    private void OnDestroy()
    {
        DestroyRing(_ownerRing);
        DestroyRing(_targetRing);
        DestroyRing(_rangeRing);
        DestroyRing(_areaRing);
        DestroyTargetLines();
    }

    private void OnGUI()
    {
        if (!_showMenu) return;

        EnsureStyles();
        _windowRect = GUI.Window(GetInstanceID(), _windowRect, DrawWindow, "Skill Debug Menu");
    }

    private void DrawWindow(int windowId)
    {
        AutoPopulateCharactersIfNeeded();
        ClampSelections();

        GUILayout.BeginVertical();
        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Width(ScrollWidth), GUILayout.Height(ScrollHeight));

        DrawCharacterSelectors();
        GUILayout.Space(8f);
        DrawActionButtons();
        GUILayout.Space(8f);
        DrawSkillSection();
        GUILayout.Space(8f);
        DrawStatusSection();
        GUILayout.Space(8f);
        GUILayout.Label("Last: " + _lastMessage);

        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUI.DragWindow(new Rect(0f, 0f, WindowWidth, 40f));
    }

    private void DrawCharacterSelectors()
    {
        GUILayout.Label("Characters", _headerStyle);

        if (_characters.Count == 0)
        {
            GUILayout.Label("No Character found.", _labelStyle);
            if (GUILayout.Button("Refresh", _buttonStyle, GUILayout.Height(ButtonHeight)))
            {
                RefreshCharacters();
            }

            return;
        }

        if (GUILayout.Button("Refresh", _buttonStyle, GUILayout.Height(ButtonHeight)))
        {
            RefreshCharacters();
        }

        GUILayout.Label("Owner", _headerStyle);
        _selectedOwnerIndex = GUILayout.SelectionGrid(_selectedOwnerIndex, BuildCharacterLabels(), 1, _buttonStyle);

        GUILayout.Space(4f);
        GUILayout.Label("Target", _headerStyle);
        _selectedTargetIndex = GUILayout.SelectionGrid(_selectedTargetIndex, BuildCharacterLabels(), 1, _buttonStyle);

        if (_pointTargetMarker != null)
        {
            GUILayout.Label("Point Marker: " + FormatVector(_pointTargetMarker.position), _labelStyle);
        }
        else
        {
            GUILayout.Label("Point Marker: none (target position fallback)", _labelStyle);
        }
    }

    private void DrawActionButtons()
    {
        Character owner = GetSelectedOwner();
        Character target = GetSelectedTarget();

        GUILayout.Label("Actions", _headerStyle);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Heal Owner", _buttonStyle, GUILayout.Height(ButtonHeight)))
        {
            RestoreFull(owner);
        }

        if (GUILayout.Button("Heal Target", _buttonStyle, GUILayout.Height(ButtonHeight)))
        {
            RestoreFull(target);
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset Owner Pos", _buttonStyle, GUILayout.Height(ButtonHeight)))
        {
            ResetPosition(owner);
        }

        if (GUILayout.Button("Reset Target Pos", _buttonStyle, GUILayout.Height(ButtonHeight)))
        {
            ResetPosition(target);
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset Owner CD", _buttonStyle, GUILayout.Height(ButtonHeight)))
        {
            ClearCooldowns(owner);
        }

        if (GUILayout.Button("Reset All", _buttonStyle, GUILayout.Height(ButtonHeight)))
        {
            ResetAllCharacters();
        }
        GUILayout.EndHorizontal();
    }

    private void DrawSkillSection()
    {
        Character owner = GetSelectedOwner();
        IReadOnlyList<SkillBase> skills = owner != null ? owner.AvailableCombatSkills : System.Array.Empty<SkillBase>();

        GUILayout.Label("Skills", _headerStyle);
        if (owner == null)
        {
            GUILayout.Label("Owner not selected.", _labelStyle);
            return;
        }

        if (skills.Count == 0)
        {
            GUILayout.Label(owner.name + " has no available skills.", _labelStyle);
            return;
        }

        _selectedSkillIndex = Mathf.Clamp(_selectedSkillIndex, 0, skills.Count - 1);
        _selectedSkillIndex = GUILayout.SelectionGrid(_selectedSkillIndex, BuildSkillLabels(owner, skills), 1, _buttonStyle);

        SkillBase skill = skills[_selectedSkillIndex];
        GUILayout.Label("TargetKind: " + skill.TargetKind, _labelStyle);
        GUILayout.Label("MaxRange: " + skill.MaxRange.ToString("0.0"), _labelStyle);
        if (skill.AreaRadius > 0f)
        {
            GUILayout.Label("AreaRadius: " + skill.AreaRadius.ToString("0.0"), _labelStyle);
        }

        CombatSkillEvaluationResult result = EvaluateSelectedSkill(owner, skill);
        using (new GUIEnabledScope(result.CanUse))
        {
            if (GUILayout.Button("Use Skill", _buttonStyle, GUILayout.Height(ButtonHeight)))
            {
                skill.Execute(owner, result.Context);
                owner.SkillCooldowns?.StartCooldown(skill);
                _lastMessage = "Used " + skill.Name;
            }
        }

        if (!result.CanUse)
        {
            GUILayout.Label("Cannot use: " + result.FailureReason, _labelStyle);
        }
    }

    private void DrawStatusSection()
    {
        Character owner = GetSelectedOwner();
        Character target = GetSelectedTarget();

        GUILayout.Label("Status", _headerStyle);
        DrawCharacterSummary("Owner", owner);
        GUILayout.Space(4f);
        DrawCharacterSummary("Target", target);
    }

    private void DrawCharacterSummary(string label, Character character)
    {
        if (character == null)
        {
            GUILayout.Label(label + ": none", _labelStyle);
            return;
        }

        CombatHealth health = character.Health;
        GUILayout.Label(label + ": " + character.name, _labelStyle);
        GUILayout.Label("HP " + (health != null ? health.HP : 0) + "/" + (health != null ? health.MaxHP : 0), _labelStyle);
        GUILayout.Label("Alive " + (health != null && health.IsAlive) + " / CanAct " + (health != null && health.CanAct), _labelStyle);

        IReadOnlyList<CombatStatusEffectSnapshot> effects = character.StatusEffects != null
            ? character.StatusEffects.GetActiveEffectSnapshots()
            : System.Array.Empty<CombatStatusEffectSnapshot>();

        if (effects.Count == 0)
        {
            GUILayout.Label("Effects: none", _labelStyle);
            return;
        }

        for (int i = 0; i < effects.Count; i++)
        {
            CombatStatusEffectSnapshot effect = effects[i];
            GUILayout.Label("- " + effect.Type + " " + effect.RemainingSeconds.ToString("0.0") + "s", _labelStyle);
        }
    }

    private void RestoreFull(Character character)
    {
        if (character == null || character.Health == null)
        {
            _lastMessage = "Character missing.";
            return;
        }

        character.Health.RestoreFull();
        _lastMessage = "Healed " + character.name;
    }

    private void ResetPosition(Character character)
    {
        if (character == null)
        {
            _lastMessage = "Character missing.";
            return;
        }

        if (!_initialPositions.TryGetValue(character, out Vector3 initialPosition))
        {
            _initialPositions[character] = character.transform.position;
            initialPosition = character.transform.position;
        }

        CombatCharacterBody body = character.GetComponent<CombatCharacterBody>();
        body?.Stop();

        NavMeshAgent agent = character.GetComponent<NavMeshAgent>();
        if (agent != null && agent.isOnNavMesh)
        {
            agent.Warp(initialPosition);
        }
        else
        {
            character.transform.position = initialPosition;
        }

        _lastMessage = "Reset position: " + character.name;
    }

    private void ClearCooldowns(Character character)
    {
        if (character == null || character.SkillCooldowns == null)
        {
            _lastMessage = "Cooldown target missing.";
            return;
        }

        character.SkillCooldowns.ClearAll();
        _lastMessage = "Reset cooldowns: " + character.name;
    }

    private void ResetAllCharacters()
    {
        for (int i = 0; i < _characters.Count; i++)
        {
            Character character = _characters[i];
            if (character == null) continue;

            character.Health?.RestoreFull();
            character.SkillCooldowns?.ClearAll();
            ResetPosition(character);
        }

        _lastMessage = "Reset all characters";
    }

    private Vector3 ResolvePoint(Character owner, Character target, SkillBase skill)
    {
        if (_pointTargetMarker != null)
        {
            return _pointTargetMarker.position;
        }

        if (target != null)
        {
            return target.transform.position;
        }

        float fallbackDistance = skill != null && !float.IsPositiveInfinity(skill.MaxRange)
            ? Mathf.Min(skill.MaxRange, 3f)
            : 3f;
        return owner.transform.position + owner.transform.forward * fallbackDistance;
    }

    private string[] BuildCharacterLabels()
    {
        var labels = new string[_characters.Count];
        for (int i = 0; i < _characters.Count; i++)
        {
            Character character = _characters[i];
            if (character == null)
            {
                labels[i] = "(null)";
                continue;
            }

            CombatHealth health = character.Health;
            labels[i] = character.name +
                " [" + character.Team + "] " +
                (health != null ? health.HP + "/" + health.MaxHP : "no hp");
        }

        return labels;
    }

    private string[] BuildSkillLabels(Character owner, IReadOnlyList<SkillBase> skills)
    {
        var labels = new string[skills.Count];
        for (int i = 0; i < skills.Count; i++)
        {
            SkillBase skill = skills[i];
            float remaining = owner.SkillCooldowns != null ? owner.SkillCooldowns.GetRemainingSeconds(skill) : 0f;
            labels[i] = remaining > 0f
                ? skill.Name + " (" + remaining.ToString("0.0") + "s)"
                : skill.Name;
        }

        return labels;
    }

    private void RefreshCharacters()
    {
        _characters.Clear();
        Character[] found = FindObjectsByType<Character>(FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            Character character = found[i];
            if (character == null) continue;
            _characters.Add(character);
        }

        CaptureInitialPositions();
        ClampSelections();
        _lastMessage = "Refreshed characters";
    }

    private void AutoPopulateCharactersIfNeeded()
    {
        bool hasCharacter = false;
        for (int i = 0; i < _characters.Count; i++)
        {
            if (_characters[i] != null)
            {
                hasCharacter = true;
                break;
            }
        }

        if (!hasCharacter)
        {
            RefreshCharacters();
        }
    }

    private void CaptureInitialPositions()
    {
        for (int i = 0; i < _characters.Count; i++)
        {
            Character character = _characters[i];
            if (character == null || _initialPositions.ContainsKey(character)) continue;

            _initialPositions.Add(character, character.transform.position);
        }
    }

    private void ClampSelections()
    {
        if (_characters.Count == 0)
        {
            _selectedOwnerIndex = 0;
            _selectedTargetIndex = 0;
            _selectedSkillIndex = 0;
            return;
        }

        _selectedOwnerIndex = Mathf.Clamp(_selectedOwnerIndex, 0, _characters.Count - 1);
        _selectedTargetIndex = Mathf.Clamp(_selectedTargetIndex, 0, _characters.Count - 1);
    }

    private Character GetSelectedOwner()
    {
        return _characters.Count == 0 ? null : _characters[_selectedOwnerIndex];
    }

    private Character GetSelectedTarget()
    {
        return _characters.Count == 0 ? null : _characters[_selectedTargetIndex];
    }

    private static string FormatVector(Vector3 value)
    {
        return value.x.ToString("0.0") + ", " + value.y.ToString("0.0") + ", " + value.z.ToString("0.0");
    }

    private void EnsureSelectionRings()
    {
        _ownerRing = EnsureRing(_ownerRing, "DebugOwnerSelectionRing", owner: true);
        _targetRing = EnsureRing(_targetRing, "DebugTargetSelectionRing", owner: false);
        RebuildRingShape(_ownerRing);
        RebuildRingShape(_targetRing);
    }

    private void EnsureSkillVisuals()
    {
        _rangeRing = EnsureRing(_rangeRing, "DebugSkillRangeRing", owner: true);
        _areaRing = EnsureRing(_areaRing, "DebugSkillAreaRing", owner: false);
        RebuildRingShape(_rangeRing);
        RebuildRingShape(_areaRing);
    }

    private void UpdateSelectionRings()
    {
        EnsureSelectionRings();

        Character owner = GetSelectedOwner();
        Character target = GetSelectedTarget();
        bool isSharedSelection = owner != null && owner == target;

        UpdateRing(_ownerRing, owner, isSharedSelection ? SharedRingColor : OwnerRingColor, scaleMultiplier: isSharedSelection ? 1.15f : 1f);
        UpdateRing(_targetRing, target, isSharedSelection ? SharedRingColor : TargetRingColor, scaleMultiplier: isSharedSelection ? 0.8f : 1f);
    }

    private void UpdateSkillVisuals()
    {
        EnsureSkillVisuals();

        Character owner = GetSelectedOwner();
        SkillBase skill = GetSelectedSkill(owner);

        if (owner == null || skill == null)
        {
            HideSkillVisuals();
            return;
        }

        CombatSkillEvaluationResult result = EvaluateSelectedSkill(owner, skill);
        UpdateRangeRing(result);
        UpdateAreaRing(result);
        UpdateTargetLines(result);
    }

    private LineRenderer EnsureRing(LineRenderer current, string ringName, bool owner)
    {
        if (current != null) return current;

        var ringObject = new GameObject(ringName);
        ringObject.transform.SetParent(transform, false);

        var line = ringObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = RingSegments;
        line.widthMultiplier = _selectionRingWidth;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.textureMode = LineTextureMode.Stretch;
        line.alignment = LineAlignment.View;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = owner ? OwnerRingColor : TargetRingColor;
        line.endColor = owner ? OwnerRingColor : TargetRingColor;
        line.enabled = false;
        return line;
    }

    private LineRenderer CreateTargetLine(int index)
    {
        var lineObject = new GameObject("DebugSkillTargetLine_" + index);
        lineObject.transform.SetParent(transform, false);

        var line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = false;
        line.positionCount = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.textureMode = LineTextureMode.Stretch;
        line.alignment = LineAlignment.View;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.widthMultiplier = _selectionRingWidth;
        line.enabled = false;
        return line;
    }

    private void RebuildRingShape(LineRenderer ring)
    {
        if (ring == null) return;

        Vector3[] points = new Vector3[RingSegments];
        float step = Mathf.PI * 2f / RingSegments;
        for (int i = 0; i < RingSegments; i++)
        {
            float angle = step * i;
            points[i] = new Vector3(Mathf.Cos(angle) * _selectionRingRadius, 0f, Mathf.Sin(angle) * _selectionRingRadius);
        }

        ring.SetPositions(points);
    }

    private void UpdateRing(LineRenderer ring, Character character, Color color, float scaleMultiplier)
    {
        if (ring == null) return;

        if (character == null)
        {
            ring.enabled = false;
            return;
        }

        ring.enabled = true;
        ring.startColor = color;
        ring.endColor = color;
        ring.widthMultiplier = _selectionRingWidth;

        Vector3 center = character.transform.position + Vector3.up * _selectionRingYOffset;
        float step = Mathf.PI * 2f / RingSegments;
        for (int i = 0; i < RingSegments; i++)
        {
            float angle = step * i;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (_selectionRingRadius * scaleMultiplier);
            ring.SetPosition(i, center + offset);
        }
    }

    private void DestroyRing(LineRenderer ring)
    {
        if (ring == null) return;

        if (Application.isPlaying)
        {
            Destroy(ring.gameObject);
            return;
        }

        DestroyImmediate(ring.gameObject);
    }

    private void HideSkillVisuals()
    {
        if (_rangeRing != null) _rangeRing.enabled = false;
        if (_areaRing != null) _areaRing.enabled = false;
        HideTargetLines();
    }

    private SkillBase GetSelectedSkill(Character owner)
    {
        if (owner == null) return null;

        IReadOnlyList<SkillBase> skills = owner.AvailableCombatSkills;
        if (skills == null || skills.Count == 0) return null;

        _selectedSkillIndex = Mathf.Clamp(_selectedSkillIndex, 0, skills.Count - 1);
        return skills[_selectedSkillIndex];
    }

    private void UpdateRangeRing(CombatSkillEvaluationResult result)
    {
        if (_rangeRing == null) return;

        if (!result.HasRangePreview)
        {
            _rangeRing.enabled = false;
            return;
        }

        _rangeRing.enabled = true;
        _rangeRing.startColor = RangeRingColor;
        _rangeRing.endColor = RangeRingColor;
        SetRingPositions(_rangeRing, result.OriginPoint + Vector3.up * (_selectionRingYOffset * 0.5f), result.RangeRadius);
    }

    private void UpdateAreaRing(CombatSkillEvaluationResult result)
    {
        if (_areaRing == null) return;

        if (!result.HasAreaPreview)
        {
            _areaRing.enabled = false;
            return;
        }

        _areaRing.enabled = true;
        _areaRing.startColor = AreaRingColor;
        _areaRing.endColor = AreaRingColor;
        SetRingPositions(_areaRing, result.AreaCenter + Vector3.up * (_selectionRingYOffset * 0.4f), result.AreaRadius);
    }

    private void UpdateTargetLines(CombatSkillEvaluationResult result)
    {
        if (result.ResolvedTargets == null || result.ResolvedTargets.Count == 0)
        {
            HideTargetLines();
            return;
        }

        EnsureTargetLineCapacity(result.ResolvedTargets.Count);

        Vector3 origin = result.OriginPoint + Vector3.up * _selectionRingYOffset;
        for (int i = 0; i < _targetLines.Count; i++)
        {
            LineRenderer line = _targetLines[i];
            if (i >= result.ResolvedTargets.Count)
            {
                line.enabled = false;
                continue;
            }

            Character resolvedTarget = result.ResolvedTargets[i];
            if (resolvedTarget == null)
            {
                line.enabled = false;
                continue;
            }

            line.enabled = true;
            Color lineColor = result.CanUse ? ValidTargetLineColor : InvalidTargetLineColor;
            line.startColor = lineColor;
            line.endColor = lineColor;
            line.widthMultiplier = _selectionRingWidth;
            line.SetPosition(0, origin);
            line.SetPosition(1, resolvedTarget.transform.position + Vector3.up * _selectionRingYOffset);
        }
    }

    private CombatSkillEvaluationResult EvaluateSelectedSkill(Character owner, SkillBase skill)
    {
        Character target = GetSelectedTarget();
        Vector3 point = ResolvePoint(owner, target, skill);

        CombatSkillEvaluationRequest request = skill != null &&
            (skill.TargetKind == SkillTargetKind.Point || skill.TargetKind == SkillTargetKind.Area)
            ? CombatSkillEvaluationRequest.ForPoint(owner, point)
            : CombatSkillEvaluationRequest.ForTarget(owner, target);

        return CombatSkillEvaluator.Evaluate(skill, request);
    }

    private void EnsureTargetLineCapacity(int count)
    {
        while (_targetLines.Count < count)
        {
            _targetLines.Add(CreateTargetLine(_targetLines.Count));
        }
    }

    private void HideTargetLines()
    {
        for (int i = 0; i < _targetLines.Count; i++)
        {
            if (_targetLines[i] != null)
            {
                _targetLines[i].enabled = false;
            }
        }
    }

    private void DestroyTargetLines()
    {
        for (int i = 0; i < _targetLines.Count; i++)
        {
            DestroyRing(_targetLines[i]);
        }

        _targetLines.Clear();
    }

    private void SetRingPositions(LineRenderer ring, Vector3 center, float radius)
    {
        float step = Mathf.PI * 2f / RingSegments;
        for (int i = 0; i < RingSegments; i++)
        {
            float angle = step * i;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            ring.SetPosition(i, center + offset);
        }
    }

    private void EnsureStyles()
    {
        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                wordWrap = true
            };
        }

        if (_buttonStyle == null)
        {
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 26,
                fixedHeight = ButtonHeight,
                wordWrap = true
            };
        }

        if (_headerStyle == null)
        {
            _headerStyle = new GUIStyle(_labelStyle)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold
            };
        }
    }

    private readonly struct GUIEnabledScope : System.IDisposable
    {
        private readonly bool _previous;

        public GUIEnabledScope(bool enabled)
        {
            _previous = GUI.enabled;
            GUI.enabled = enabled;
        }

        public void Dispose()
        {
            GUI.enabled = _previous;
        }
    }
}
