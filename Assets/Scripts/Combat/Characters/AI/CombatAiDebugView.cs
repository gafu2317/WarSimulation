using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SimpleCombatBrain))]
public sealed class CombatAiDebugView : MonoBehaviour
{
    [SerializeField] private bool _visible = true;
    [SerializeField] private Camera _cameraTarget = null;
    [SerializeField] private Vector3 _healthBarWorldOffset = new Vector3(0f, 2.25f, 0f);
    [SerializeField, Min(80f)] private float _panelWidth = 320f;
    [SerializeField, Min(80f)] private float _panelHeight = 0f;
    [SerializeField, Min(0f)] private float _panelMargin = 12f;
    [SerializeField, Min(1f)] private float _lineHeight = 24f;
    [SerializeField, Min(1)] private int _fontSize = 16;
    [SerializeField] private Font _font;
    [SerializeField] private Color _textColor = Color.white;
    [SerializeField] private Color _titleColor = new Color(0.9f, 0.95f, 1f, 1f);
    [SerializeField] private Color _backgroundColor = new Color(0f, 0f, 0f, 0.65f);
    [SerializeField, Min(1f)] private float _healthBarWidth = 72f;
    [SerializeField, Min(1f)] private float _healthBarHeight = 7f;
    [SerializeField] private Color _maxHealthColor = new Color(0.35f, 0.35f, 0.35f, 1f);
    [SerializeField] private Color _currentHealthColor = new Color(0.65f, 1f, 0.2f, 1f);

    private const int LineCount = 5;
    private const string DefaultFontAssetPath = "Assets/Fonts/Noto_Sans_JP/static/NotoSansJP-Regular.ttf";
    private const float Padding = 6f;
    private const float EntryGap = 8f;
    private const float HeaderHeight = 22f;

    private static readonly List<CombatAiDebugView> s_views = new();
    private static Font s_sharedFont;
    private static Vector2 s_allyScroll;
    private static Vector2 s_enemyScroll;
    private static int s_renderedFrame = -1;
    private static EventType s_renderedEventType = EventType.Ignore;

    private SimpleCombatBrain _brain;
    private Character _character;
    private CombatHealth _health;
    private CombatVision _vision;
    private GUIStyle _labelStyle;
    private GUIStyle _titleStyle;
    private Texture2D _backgroundTexture;
    private Texture2D _maxHealthTexture;
    private Texture2D _currentHealthTexture;
    private Color _cachedBackgroundColor;
    private Color _cachedMaxHealthColor;
    private Color _cachedCurrentHealthColor;
    private Font _cachedFont;

    private void Awake()
    {
        ResolveComponents();
    }

    private void OnEnable()
    {
        if (!s_views.Contains(this))
        {
            s_views.Add(this);
        }
    }

    private void OnDisable()
    {
        s_views.Remove(this);
    }

    private void OnDestroy()
    {
        s_views.Remove(this);
        DestroyCachedTexture(_backgroundTexture);
        DestroyCachedTexture(_maxHealthTexture);
        DestroyCachedTexture(_currentHealthTexture);
    }

    private void OnGUI()
    {
        if (!_visible) return;
        EventType eventType = Event.current != null ? Event.current.type : EventType.Repaint;
        if (s_renderedFrame == Time.frameCount && s_renderedEventType == eventType) return;

        ResolveComponents();
        if (_brain == null) return;

        s_renderedFrame = Time.frameCount;
        s_renderedEventType = eventType;
        EnsureGuiResources();
        DrawWorldHealthBars();
        DrawTeamPanels();
    }

    private void DrawWorldHealthBars()
    {
        Camera camera = ResolveCamera();
        if (camera == null) return;

        for (int i = 0; i < s_views.Count; i++)
        {
            CombatAiDebugView view = s_views[i];
            if (view == null || !view._visible) continue;
            view.ResolveComponents();
            if (view._health == null || view._health.MaxHP <= 0) continue;

            view.EnsureGuiResources();
            view.DrawWorldHealthBar(camera);
        }
    }

    private void DrawTeamPanels()
    {
        float height = _panelHeight > 0f
            ? Mathf.Min(_panelHeight, Mathf.Max(1f, Screen.height - _panelMargin * 2f))
            : Mathf.Max(1f, Screen.height - _panelMargin * 2f);
        float width = Mathf.Min(_panelWidth, Mathf.Max(1f, Screen.width * 0.5f - _panelMargin * 1.5f));

        Rect enemyPanel = new Rect(_panelMargin, _panelMargin, width, height);
        Rect allyPanel = new Rect(Screen.width - width - _panelMargin, _panelMargin, width, height);

        s_enemyScroll = DrawPanel(enemyPanel, "敵", CombatTeam.Enemy, s_enemyScroll);
        s_allyScroll = DrawPanel(allyPanel, "味方", CombatTeam.Ally, s_allyScroll);
    }

    private Vector2 DrawPanel(Rect panelRect, string title, CombatTeam team, Vector2 scroll)
    {
        GUI.DrawTexture(panelRect, _backgroundTexture);
        GUI.Label(
            new Rect(panelRect.x + Padding, panelRect.y + Padding, panelRect.width - Padding * 2f, HeaderHeight),
            title,
            _titleStyle);

        float contentHeight = CalculateContentHeight(team);
        Rect viewRect = new Rect(
            panelRect.x + Padding,
            panelRect.y + Padding + HeaderHeight,
            panelRect.width - Padding * 2f,
            panelRect.height - Padding * 2f - HeaderHeight);
        Rect contentRect = new Rect(0f, 0f, viewRect.width - 16f, Mathf.Max(viewRect.height, contentHeight));

        scroll = GUI.BeginScrollView(viewRect, scroll, contentRect);
        float y = 0f;
        for (int i = 0; i < s_views.Count; i++)
        {
            CombatAiDebugView view = s_views[i];
            if (view == null || !view._visible) continue;
            view.ResolveComponents();
            if (view._character == null || view._brain == null || view._character.Team != team) continue;

            y = view.DrawCharacterEntry(contentRect.width, y);
        }

        GUI.EndScrollView();
        return scroll;
    }

    private float DrawCharacterEntry(float width, float y)
    {
        EnsureGuiResources();

        float entryHeight = GetEntryHeight();
        Rect entryRect = new Rect(0f, y, width, entryHeight);

        string characterName = _character != null ? _character.name : name;
        GUI.Label(
            new Rect(entryRect.x + Padding, entryRect.y + Padding, entryRect.width - Padding * 2f, _lineHeight),
            characterName,
            _titleStyle);

        string text = BuildCurrentDebugText();
        GUI.Label(
            new Rect(
                entryRect.x + Padding,
                entryRect.y + Padding + _lineHeight + Padding,
                entryRect.width - Padding * 2f,
                _lineHeight * LineCount),
            text,
            _labelStyle);

        return y + entryHeight + EntryGap;
    }

    public static string BuildDebugText(
        SimpleCombatBrain.Decision decision,
        string targetName,
        LifeState lifeState,
        int visibleEnemyCount)
    {
        if (string.IsNullOrEmpty(targetName))
        {
            targetName = "-";
        }

        return
            $"移動: {FormatMoveKind(decision.Move.Kind)} {decision.Move.Score:0.#}\n" +
            $"行動: {FormatActionKind(decision.Action.Kind)} {decision.Action.Score:0.#}\n" +
            $"対象: {targetName}\n" +
            $"状態: {FormatLifeState(lifeState)}\n" +
            $"視認: {visibleEnemyCount}";
    }

    public static string FormatMoveKind(SimpleCombatBrain.MoveKind kind) => kind switch
    {
        SimpleCombatBrain.MoveKind.Idle => "待機",
        SimpleCombatBrain.MoveKind.Patrol => "巡回",
        SimpleCombatBrain.MoveKind.AssaultEnemyBase => "敵拠点突撃",
        SimpleCombatBrain.MoveKind.DefendHomeBase => "拠点防衛",
        SimpleCombatBrain.MoveKind.FollowAlly => "味方追従",
        SimpleCombatBrain.MoveKind.ChaseEnemy => "敵追跡",
        SimpleCombatBrain.MoveKind.MoveToLastKnownEnemyPosition => "最終目撃地点へ",
        SimpleCombatBrain.MoveKind.RetreatToHome => "退却",
        SimpleCombatBrain.MoveKind.MoveToHighGround => "高所へ",
        SimpleCombatBrain.MoveKind.HideInForest => "森に潜む",
        _ => kind.ToString(),
    };

    public static string FormatActionKind(SimpleCombatBrain.ActionKind kind) => kind switch
    {
        SimpleCombatBrain.ActionKind.None => "なし",
        SimpleCombatBrain.ActionKind.AttackEnemy => "攻撃",
        _ => kind.ToString(),
    };

    public static string FormatLifeState(LifeState lifeState) => lifeState switch
    {
        LifeState.Active => "戦闘中",
        LifeState.Retreating => "退却中",
        _ => lifeState.ToString(),
    };

    private string BuildCurrentDebugText()
    {
        SimpleCombatBrain.Decision decision = _brain.GetLastDecision();
        Character target = ResolveDisplayTarget(decision);
        string targetName = target != null ? target.name : "-";
        LifeState state = _health != null ? _health.LifeState : LifeState.Active;
        int visibleCount = _vision != null ? _vision.VisibleEnemies.Count : 0;

        return BuildDebugText(decision, targetName, state, visibleCount);
    }

    private Character ResolveDisplayTarget(SimpleCombatBrain.Decision decision)
    {
        if (_brain != null && _brain.CurrentTarget != null) return _brain.CurrentTarget;
        if (decision.Action.Target != null) return decision.Action.Target;
        if (decision.Move.Target != null) return decision.Move.Target;
        return null;
    }

    private float CalculateContentHeight(CombatTeam team)
    {
        float height = 0f;
        for (int i = 0; i < s_views.Count; i++)
        {
            CombatAiDebugView view = s_views[i];
            if (view == null || !view._visible) continue;
            view.ResolveComponents();
            if (view._character == null || view._brain == null || view._character.Team != team) continue;

            height += view.GetEntryHeight() + EntryGap;
        }

        return height;
    }

    private float GetEntryHeight()
    {
        return Padding * 3f + _lineHeight + _lineHeight * LineCount;
    }

    private void DrawWorldHealthBar(Camera camera)
    {
        if (_health == null || _health.MaxHP <= 0) return;

        Vector3 screenPoint = camera.WorldToScreenPoint(transform.position + _healthBarWorldOffset);
        if (screenPoint.z <= 0f) return;

        float width = _healthBarWidth;
        float x = screenPoint.x - width * 0.5f;
        float y = Screen.height - screenPoint.y;
        x = Mathf.Clamp(x, 0f, Mathf.Max(0f, Screen.width - width));
        y = Mathf.Clamp(y, 0f, Mathf.Max(0f, Screen.height - _healthBarHeight));

        float hpRatio = Mathf.Clamp01(_health.HP / (float)_health.MaxHP);

        GUI.DrawTexture(new Rect(x, y, width, _healthBarHeight), _maxHealthTexture);
        GUI.DrawTexture(new Rect(x, y, width * hpRatio, _healthBarHeight), _currentHealthTexture);
    }

    private Camera ResolveCamera()
    {
        if (_cameraTarget != null) return _cameraTarget;

        return Camera.main;
    }

    private void EnsureGuiResources()
    {
        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = _fontSize,
                normal = { textColor = _textColor },
                alignment = TextAnchor.UpperLeft,
                wordWrap = false,
                clipping = TextClipping.Clip,
            };
        }
        else
        {
            _labelStyle.fontSize = _fontSize;
            _labelStyle.normal.textColor = _textColor;
        }

        if (_titleStyle == null)
        {
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = _fontSize,
                normal = { textColor = _titleColor },
                alignment = TextAnchor.UpperLeft,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                clipping = TextClipping.Clip,
            };
        }
        else
        {
            _titleStyle.fontSize = _fontSize;
            _titleStyle.normal.textColor = _titleColor;
        }

        ApplyFontToStyles();

        if (_backgroundTexture == null)
        {
            _backgroundTexture = new Texture2D(1, 1)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        if (_cachedBackgroundColor != _backgroundColor)
        {
            _cachedBackgroundColor = _backgroundColor;
            _backgroundTexture.SetPixel(0, 0, _backgroundColor);
            _backgroundTexture.Apply();
        }

        EnsureColorTexture(ref _maxHealthTexture, ref _cachedMaxHealthColor, _maxHealthColor);
        EnsureColorTexture(ref _currentHealthTexture, ref _cachedCurrentHealthColor, _currentHealthColor);
    }

    private void ApplyFontToStyles()
    {
        Font font = ResolveFont();
        if (font == null || font == _cachedFont) return;

        _cachedFont = font;
        if (_labelStyle != null) _labelStyle.font = font;
        if (_titleStyle != null) _titleStyle.font = font;
    }

    private Font ResolveFont()
    {
        if (_font != null) return _font;
        if (s_sharedFont != null) return s_sharedFont;

        s_sharedFont = Resources.Load<Font>("NotoSansJP-Regular");
#if UNITY_EDITOR
        if (s_sharedFont == null)
        {
            s_sharedFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(DefaultFontAssetPath);
        }
#endif
        return s_sharedFont;
    }

    private static void EnsureColorTexture(ref Texture2D texture, ref Color cachedColor, Color color)
    {
        if (texture == null)
        {
            texture = new Texture2D(1, 1)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
        }
        else if (cachedColor == color)
        {
            return;
        }

        cachedColor = color;
        texture.SetPixel(0, 0, color);
        texture.Apply();
    }

    private static void DestroyCachedTexture(Texture2D texture)
    {
        if (texture == null) return;

        if (Application.isPlaying)
        {
            Destroy(texture);
        }
        else
        {
            DestroyImmediate(texture);
        }
    }

    private void ResolveComponents()
    {
        _brain ??= GetComponent<SimpleCombatBrain>();
        _character ??= GetComponent<Character>();
        _health ??= _character != null ? _character.Health : GetComponent<CombatHealth>();
        _vision ??= _character != null ? _character.Vision : GetComponent<CombatVision>();
    }
}
