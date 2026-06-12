using System.Collections.Generic;
using System.Text;
using UnityEngine;

public sealed class CombatAiDecisionDebugView : MonoBehaviour
{
    private const float WindowWidth = 1760f;
    private const float WindowHeight = 980f;
    private const float SidebarWidth = 300f;
    private const float ColumnWidth = 250f;
    private const float ColumnHeight = 840f;
    private const float Padding = 12f;

    [SerializeField] private bool _visible = true;
    [SerializeField] private bool _autoRefresh = true;
    [SerializeField] private CombatAiContextCollector _contextCollector;
    [SerializeField] private List<Character> _characters = new();
    [SerializeField] private int _selectedCharacterIndex;
    [SerializeField] private Rect _windowRect = new Rect(16f, 16f, WindowWidth, WindowHeight);
    [SerializeField] private Color _moveLineColor = new Color(0.2f, 1f, 0.35f, 0.95f);
    [SerializeField] private Color _visionLineColor = new Color(0.3f, 0.85f, 1f, 0.95f);
    [SerializeField] private Color _blockedLineColor = new Color(1f, 0.3f, 0.3f, 0.95f);
    [SerializeField] private Color _skillLineColor = new Color(1f, 0.85f, 0.2f, 0.95f);
    [SerializeField] private Color _areaColor = new Color(1f, 0.55f, 0.15f, 0.92f);

    private Vector2 _sidebarScroll;
    private Vector2 _flowScroll;
    private GUIStyle _titleStyle;
    private GUIStyle _sectionStyle;
    private GUIStyle _textStyle;
    private GUIStyle _selectedTextStyle;
    private Texture2D _backgroundTexture;
    private CombatAiDebugSnapshot _snapshot;

    private void Awake()
    {
        if (!IsDebugAllowed())
        {
            enabled = false;
            return;
        }

        _contextCollector ??= GetComponent<CombatAiContextCollector>();
        _contextCollector ??= gameObject.AddComponent<CombatAiContextCollector>();
        AutoPopulateCharactersIfNeeded();
        RefreshSnapshot();
    }

    private void Update()
    {
        if (!IsDebugAllowed()) return;

        if (_autoRefresh)
        {
            RefreshSnapshot();
        }
    }

    private void OnGUI()
    {
        if (!IsDebugAllowed() || !_visible) return;

        EnsureStyles();
        _windowRect = GUI.Window(GetHashCode(), _windowRect, DrawWindow, "AI Decision Debug");
    }

    private void OnDrawGizmos()
    {
        if (!_visible || _snapshot == null || _snapshot.Owner == null) return;

        DrawMoveGizmo();
        DrawVisionGizmos();
        DrawSkillGizmos();
    }

    private void DrawWindow(int windowId)
    {
        AutoPopulateCharactersIfNeeded();
        ClampSelection();

        GUILayout.BeginHorizontal();
        DrawSidebar();
        DrawFlow();
        GUILayout.EndHorizontal();

        GUI.DragWindow(new Rect(0f, 0f, _windowRect.width, 28f));
    }

    private void DrawSidebar()
    {
        GUILayout.BeginVertical(GUILayout.Width(SidebarWidth));
        GUILayout.Label("Characters", _titleStyle);

        if (GUILayout.Button("Refresh Characters", GUILayout.Height(36f)))
        {
            RefreshCharacters();
        }

        if (GUILayout.Button("Refresh Snapshot", GUILayout.Height(36f)))
        {
            RefreshSnapshot();
        }

        _sidebarScroll = GUILayout.BeginScrollView(_sidebarScroll, GUILayout.Width(SidebarWidth), GUILayout.Height(ColumnHeight - 120f));
        for (int i = 0; i < _characters.Count; i++)
        {
            Character character = _characters[i];
            if (character == null) continue;

            GUIStyle style = i == _selectedCharacterIndex ? _selectedTextStyle : _textStyle;
            if (GUILayout.Button(BuildCharacterButtonLabel(character), style, GUILayout.Height(56f)))
            {
                _selectedCharacterIndex = i;
                RefreshSnapshot();
            }
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    private void DrawFlow()
    {
        GUILayout.BeginVertical();
        if (_snapshot == null)
        {
            GUILayout.Label("Snapshot not available", _titleStyle);
            GUILayout.EndVertical();
            return;
        }

        GUILayout.Label("Flow", _titleStyle);
        _flowScroll = GUILayout.BeginScrollView(_flowScroll, GUILayout.Width(_windowRect.width - SidebarWidth - Padding * 3f), GUILayout.Height(ColumnHeight + 40f));
        GUILayout.BeginHorizontal();
        DrawColumn("CombatAiContext", BuildContextText());
        DrawColumn("AiAssessment", BuildAssessmentText());
        DrawColumn("Objectives", BuildObjectiveText());
        DrawColumn("Selected Objective", BuildSelectedObjectiveText());
        DrawColumn("Move Candidates", BuildMoveText());
        DrawColumn("Selected Move", BuildSelectedMoveText());
        DrawColumn("Skill Candidates", BuildSkillText());
        DrawColumn("Selected Skill", BuildSelectedSkillText());
        DrawColumn("CombatAiPlan", BuildPlanText());
        GUILayout.EndHorizontal();
        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    private void DrawColumn(string title, string body)
    {
        GUILayout.BeginVertical("box", GUILayout.Width(ColumnWidth), GUILayout.Height(ColumnHeight));
        GUILayout.Label(title, _sectionStyle);
        GUILayout.TextArea(body, _textStyle, GUILayout.ExpandHeight(true));
        GUILayout.EndVertical();
    }

    private string BuildCharacterButtonLabel(Character character)
    {
        if (character == null) return "null";
        string weapon = CombatAiDebugLabels.Weapon(character.EquippedWeapon);
        return character.name + "\n" + weapon;
    }

    private string BuildContextText()
    {
        var sb = new StringBuilder(512);
        CombatAiContextSummary summary = _snapshot.ContextSummary;
        sb.AppendLine(summary.WeaponLabel);
        sb.AppendLine(summary.PersonalityLabel);
        sb.AppendLine(summary.WeaponWeightsLabel);
        sb.AppendLine(summary.WeatherLabel);
        sb.AppendLine("VisibleEnemies: " + summary.VisibleEnemyCount);
        sb.AppendLine("RememberedEnemies: " + summary.RememberedEnemyCount);
        sb.AppendLine("KnownEnemies: " + summary.KnownEnemyCount);
        sb.AppendLine("Allies: " + summary.AllyCount);
        sb.AppendLine("LowHpAllies: " + summary.LowHpAllyCount);
        sb.AppendLine("EnemyStoneKnown: " + _snapshot.Context.HasEnemyStonePosition);
        sb.AppendLine("OwnStoneKnown: " + _snapshot.Context.HasOwnStonePosition);
        return sb.ToString();
    }

    private string BuildAssessmentText()
    {
        var sb = new StringBuilder(1024);
        List<CombatAiMetric> metrics = _snapshot.Assessment.Metrics;
        for (int i = 0; i < metrics.Count; i++)
        {
            CombatAiMetric metric = metrics[i];
            sb.AppendLine(metric.Label + " = " + metric.Value.ToString("0.0"));
            for (int j = 0; j < metric.ReasonCodes.Count; j++)
            {
                sb.Append("  - ");
                sb.AppendLine(CombatAiDebugLabels.Reason(metric.ReasonCodes[j]));
            }

            if (i < metrics.Count - 1)
            {
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private string BuildObjectiveText()
    {
        var sb = new StringBuilder(2048);
        for (int i = 0; i < _snapshot.ObjectiveEntries.Count; i++)
        {
            AppendBreakdown(sb, _snapshot.ObjectiveEntries[i].Label, _snapshot.ObjectiveEntries[i].Breakdown);
        }

        return sb.ToString();
    }

    private string BuildSelectedObjectiveText()
    {
        if (_snapshot.SelectedObjective == null) return "None";
        var sb = new StringBuilder(512);
        AppendBreakdown(sb, _snapshot.SelectedObjective.Label, _snapshot.SelectedObjective.Breakdown);
        return sb.ToString();
    }

    private string BuildMoveText()
    {
        var sb = new StringBuilder(2048);
        for (int i = 0; i < _snapshot.MoveEntries.Count; i++)
        {
            CombatAiMoveCandidateEntry entry = _snapshot.MoveEntries[i];
            AppendBreakdown(sb, entry.Label, entry.Breakdown);
            sb.AppendLine("  Target: " + FormatMoveTarget(entry.Target));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private string BuildSelectedMoveText()
    {
        if (_snapshot.SelectedMove == null) return "None";
        var sb = new StringBuilder(512);
        AppendBreakdown(sb, _snapshot.SelectedMove.Label, _snapshot.SelectedMove.Breakdown);
        sb.AppendLine("Target: " + FormatMoveTarget(_snapshot.SelectedMove.Target));
        return sb.ToString();
    }

    private string BuildSkillText()
    {
        var sb = new StringBuilder(3072);
        for (int i = 0; i < _snapshot.SkillEntries.Count; i++)
        {
            CombatAiSkillCandidateEntry entry = _snapshot.SkillEntries[i];
            AppendBreakdown(sb, entry.Label, entry.Breakdown);
            if (entry.Skill != null)
            {
                sb.AppendLine("  CanUse: " + entry.Evaluation.CanUse);
                if (!entry.Evaluation.CanUse)
                {
                    sb.AppendLine("  Failure: " + entry.Evaluation.FailureReason);
                }

                sb.AppendLine("  Target: " + FormatSkillContext(entry.SkillContext));
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private string BuildSelectedSkillText()
    {
        if (_snapshot.SelectedSkill == null) return "None";
        var sb = new StringBuilder(512);
        AppendBreakdown(sb, _snapshot.SelectedSkill.Label, _snapshot.SelectedSkill.Breakdown);
        sb.AppendLine("CanUse: " + _snapshot.SelectedSkill.Evaluation.CanUse);
        sb.AppendLine("Target: " + FormatSkillContext(_snapshot.SelectedSkill.SkillContext));
        return sb.ToString();
    }

    private string BuildPlanText()
    {
        var sb = new StringBuilder(512);
        sb.AppendLine("Objective: " + (_snapshot.SelectedObjective != null ? _snapshot.SelectedObjective.Label : "None"));
        sb.AppendLine("Move: " + (_snapshot.SelectedMove != null ? _snapshot.SelectedMove.Label : "None"));
        sb.AppendLine("MoveTarget: " + (_snapshot.SelectedMove != null ? FormatMoveTarget(_snapshot.SelectedMove.Target) : "None"));
        sb.AppendLine("Skill: " + (_snapshot.SelectedSkill != null ? _snapshot.SelectedSkill.Label : "None"));
        sb.AppendLine("SkillTarget: " + (_snapshot.SelectedSkill != null ? FormatSkillContext(_snapshot.SelectedSkill.SkillContext) : "None"));
        return sb.ToString();
    }

    private static void AppendBreakdown(StringBuilder sb, string label, CombatAiScoreBreakdown breakdown)
    {
        sb.AppendLine(label + " = " + breakdown.Total.ToString("0.0"));
        sb.AppendLine("  Base: " + breakdown.BaseScore.ToString("0.0"));
        sb.AppendLine("  Weapon: " + breakdown.WeaponScore.ToString("0.0"));
        sb.AppendLine("  Personality: " + breakdown.PersonalityScore.ToString("0.0"));
        sb.AppendLine("  Situation: " + breakdown.SituationScore.ToString("0.0"));
        for (int i = 0; i < breakdown.ReasonCodes.Count; i++)
        {
            sb.Append("  - ");
            sb.AppendLine(CombatAiDebugLabels.Reason(breakdown.ReasonCodes[i]));
        }
    }

    private string FormatMoveTarget(CombatMoveTarget target)
    {
        if (target.Kind == CombatMoveTargetKind.None) return "None";
        if (target.Kind == CombatMoveTargetKind.Character)
        {
            return target.TargetCharacter != null ? target.TargetCharacter.name : "Character";
        }

        return target.Destination.ToString("F1");
    }

    private string FormatSkillContext(SkillExecutionContext context)
    {
        if (context.PrimaryTarget != null)
        {
            return context.PrimaryTarget.name;
        }

        if (context.HasTargetPoint)
        {
            return context.TargetPoint.ToString("F1");
        }

        return "None";
    }

    private void DrawMoveGizmo()
    {
        if (_snapshot.SelectedMove == null || !_snapshot.SelectedMove.Target.HasDestination) return;
        Character owner = _snapshot.Owner;
        if (owner == null) return;

        Gizmos.color = _moveLineColor;
        Vector3 start = owner.transform.position + Vector3.up * 1.2f;
        Vector3 end = _snapshot.SelectedMove.Target.Kind == CombatMoveTargetKind.Character && _snapshot.SelectedMove.Target.TargetCharacter != null
            ? _snapshot.SelectedMove.Target.TargetCharacter.transform.position + Vector3.up * 1.2f
            : _snapshot.SelectedMove.Target.Destination + Vector3.up * 0.2f;
        Gizmos.DrawLine(start, end);
        Gizmos.DrawSphere(end, 0.25f);
    }

    private void DrawVisionGizmos()
    {
        Character owner = _snapshot.Owner;
        if (owner == null || owner.Vision == null) return;

        Vector3 start = owner.transform.position + Vector3.up * 1.4f;
        for (int i = 0; i < _snapshot.Context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel intel = _snapshot.Context.EnemyIntel[i];
            if (intel.Character == null) continue;

            Gizmos.color = intel.HasDirectSight ? _visionLineColor : _blockedLineColor;
            Vector3 end = intel.Character.transform.position + Vector3.up * 1.4f;
            Gizmos.DrawLine(start, end);
        }
    }

    private void DrawSkillGizmos()
    {
        CombatAiSkillCandidateEntry selectedSkill = _snapshot.SelectedSkill;
        if (selectedSkill == null || selectedSkill.Skill == null) return;
        Character owner = _snapshot.Owner;
        if (owner == null) return;

        Gizmos.color = _skillLineColor;
        Vector3 start = owner.transform.position + Vector3.up * 1.0f;
        if (selectedSkill.SkillContext.PrimaryTarget != null)
        {
            Vector3 end = selectedSkill.SkillContext.PrimaryTarget.transform.position + Vector3.up * 1.0f;
            Gizmos.DrawLine(start, end);
            Gizmos.DrawSphere(end, 0.2f);
        }
        else if (selectedSkill.SkillContext.HasTargetPoint)
        {
            Vector3 end = selectedSkill.SkillContext.TargetPoint + Vector3.up * 0.1f;
            Gizmos.DrawLine(start, end);
            Gizmos.DrawSphere(end, 0.2f);
        }

        if (selectedSkill.Evaluation.HasAreaPreview)
        {
            Gizmos.color = _areaColor;
            DrawCircle(selectedSkill.Evaluation.AreaCenter, selectedSkill.Evaluation.AreaRadius);
        }
    }

    private void DrawCircle(Vector3 center, float radius)
    {
        const int segments = 40;
        Vector3 prev = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }

    private void RefreshSnapshot()
    {
        ClampSelection();
        Character selected = GetSelectedCharacter();
        if (selected == null || _contextCollector == null)
        {
            _snapshot = null;
            return;
        }

        CombatAiContext context = _contextCollector.Collect(selected);
        CombatAiBrain brain = selected.GetComponent<CombatAiBrain>();
        CombatAiWeaponWeightsProfile weaponWeightsProfile = brain != null
            ? brain.WeaponWeightsProfile
            : CombatSceneContext.Instance != null
                ? CombatSceneContext.Instance.AiWeaponWeightsProfile
                : null;
        _snapshot = CombatAiPlanner.BuildDebugSnapshot(context, selected.PersonalityProfile, weaponWeightsProfile);
    }

    private Character GetSelectedCharacter()
    {
        return _selectedCharacterIndex >= 0 && _selectedCharacterIndex < _characters.Count ? _characters[_selectedCharacterIndex] : null;
    }

    private void RefreshCharacters()
    {
        _characters.Clear();
        AutoPopulateCharactersIfNeeded(force: true);
        ClampSelection();
        RefreshSnapshot();
    }

    private void AutoPopulateCharactersIfNeeded(bool force = false)
    {
        if (!force && _characters.Count > 0) return;

        _characters.Clear();
        Character[] characters = FindObjectsByType<Character>(FindObjectsInactive.Exclude);
        for (int i = 0; i < characters.Length; i++)
        {
            if (characters[i] != null)
            {
                _characters.Add(characters[i]);
            }
        }
    }

    private void ClampSelection()
    {
        if (_characters.Count == 0)
        {
            _selectedCharacterIndex = 0;
            return;
        }

        _selectedCharacterIndex = Mathf.Clamp(_selectedCharacterIndex, 0, _characters.Count - 1);
    }

    private void EnsureStyles()
    {
        if (_backgroundTexture == null)
        {
            _backgroundTexture = new Texture2D(1, 1);
            _backgroundTexture.SetPixel(0, 0, new Color(0.06f, 0.07f, 0.09f, 0.94f));
            _backgroundTexture.Apply();
        }

        if (_titleStyle == null)
        {
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        }

        if (_sectionStyle == null)
        {
            _sectionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = new Color(0.9f, 0.95f, 1f, 1f) }
            };
        }

        if (_textStyle == null)
        {
            _textStyle = new GUIStyle(GUI.skin.textArea)
            {
                fontSize = 12,
                wordWrap = true,
                richText = false
            };
        }

        if (_selectedTextStyle == null)
        {
            _selectedTextStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
        }
    }

    private static bool IsDebugAllowed()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        return true;
#else
        return false;
#endif
    }
}
