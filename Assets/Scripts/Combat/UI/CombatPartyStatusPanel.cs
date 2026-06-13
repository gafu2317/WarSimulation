using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-80)]
public sealed class CombatPartyStatusPanel : MonoBehaviour
{
    private const string CanvasName = "CombatPartyStatusCanvas";

    [SerializeField, Min(0.05f)] private float _syncIntervalSeconds = 0.2f;

    private readonly List<CombatPartyMemberView> _allyViews = new();
    private readonly List<CombatPartyMemberView> _enemyViews = new();
    private CombatCharacterSystem _characterSystem;
    private Canvas _canvas;
    private RectTransform _canvasRect;
    private RectTransform _allyColumn;
    private RectTransform _enemyColumn;
    private float _nextSyncTime;
    private bool _ownsCanvas;

    public int AllyViewCount => _allyViews.Count;
    public int EnemyViewCount => _enemyViews.Count;

    private void Awake()
    {
        EnsureBuilt();
        TryResolveCharacterSystem();
    }

    private void OnEnable()
    {
        CombatSkillUseEvents.SkillUsed += OnSkillUsed;
    }

    private void Start()
    {
        ForceSyncNow();
    }

    private void Update()
    {
        float currentTime = Time.unscaledTime;
        if (currentTime >= _nextSyncTime)
        {
            ForceSyncNow();
            _nextSyncTime = currentTime + _syncIntervalSeconds;
        }

        TickNow(currentTime);
    }

    private void OnDisable()
    {
        CombatSkillUseEvents.SkillUsed -= OnSkillUsed;
    }

    private void OnDestroy()
    {
        if (!_ownsCanvas || _canvas == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(_canvas.gameObject);
        }
        else
        {
            DestroyImmediate(_canvas.gameObject);
        }
    }

    public void Initialize(CombatCharacterSystem characterSystem)
    {
        _characterSystem = characterSystem;
        ForceSyncNow();
    }

    public void ForceSyncNow()
    {
        EnsureBuilt();
        TryResolveCharacterSystem();
        if (_characterSystem == null)
        {
            return;
        }

        SyncTeam(_characterSystem.AllyCharacters, _allyViews, _allyColumn, isAlly: true);
        SyncTeam(_characterSystem.EnemyCharacters, _enemyViews, _enemyColumn, isAlly: false);
    }

    public void TickNow(float currentTime)
    {
        TickList(_allyViews, currentTime);
        TickList(_enemyViews, currentTime);
    }

    public CombatPartyMemberView FindView(Character character)
    {
        CombatPartyMemberView view = FindView(_allyViews, character);
        return view != null ? view : FindView(_enemyViews, character);
    }

    private void EnsureBuilt()
    {
        if (_canvas != null && _canvasRect != null && _allyColumn != null && _enemyColumn != null)
        {
            return;
        }

        GameObject existingCanvas = GameObject.Find(CanvasName);
        if (existingCanvas != null)
        {
            _canvas = existingCanvas.GetComponent<Canvas>();
            _canvasRect = existingCanvas.GetComponent<RectTransform>();
        }

        if (_canvas == null || _canvasRect == null)
        {
            var canvasObject = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = short.MaxValue - 1;
            _ownsCanvas = true;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _canvasRect = canvasObject.GetComponent<RectTransform>();
            _canvasRect.anchorMin = Vector2.zero;
            _canvasRect.anchorMax = Vector2.one;
            _canvasRect.offsetMin = Vector2.zero;
            _canvasRect.offsetMax = Vector2.zero;
        }

        _allyColumn = EnsureColumn("AlliesColumn", new Vector2(0f, 0.5f), new Vector2(16f, 0f));
        _enemyColumn = EnsureColumn("EnemiesColumn", new Vector2(1f, 0.5f), new Vector2(-16f, 0f));
    }

    private RectTransform EnsureColumn(string name, Vector2 anchor, Vector2 anchoredPosition)
    {
        Transform existing = _canvasRect.Find(name);
        RectTransform columnRect;
        if (existing != null)
        {
            columnRect = existing.GetComponent<RectTransform>();
        }
        else
        {
            var columnObject = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            columnObject.transform.SetParent(_canvasRect, false);
            columnRect = columnObject.GetComponent<RectTransform>();

            VerticalLayoutGroup layout = columnObject.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = anchor.x < 0.5f ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            layout.spacing = 8f;
            layout.padding = new RectOffset(0, 0, 12, 12);

            ContentSizeFitter fitter = columnObject.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        columnRect.anchorMin = anchor;
        columnRect.anchorMax = anchor;
        columnRect.pivot = new Vector2(anchor.x, 0.5f);
        columnRect.anchoredPosition = anchoredPosition;
        columnRect.sizeDelta = new Vector2(0f, 0f);
        return columnRect;
    }

    private void TryResolveCharacterSystem()
    {
        if (_characterSystem != null)
        {
            return;
        }

        CombatSceneContext context = CombatSceneContext.Instance;
        if (context != null && context.CharacterSystem != null)
        {
            _characterSystem = context.CharacterSystem;
            return;
        }

        _characterSystem = FindAnyObjectByType<CombatCharacterSystem>();
    }

    private void SyncTeam(
        List<Character> characters,
        List<CombatPartyMemberView> views,
        RectTransform column,
        bool isAlly)
    {
        int targetCount = characters != null ? characters.Count : 0;
        while (views.Count > targetCount)
        {
            int lastIndex = views.Count - 1;
            DestroyView(views[lastIndex]);
            views.RemoveAt(lastIndex);
        }

        for (int i = 0; i < targetCount; i++)
        {
            Character character = characters[i];
            if (character == null)
            {
                continue;
            }

            CombatPartyMemberView view = i < views.Count ? views[i] : null;
            if (view == null)
            {
                view = CreateView(column, isAlly);
                if (i < views.Count)
                {
                    views[i] = view;
                }
                else
                {
                    views.Add(view);
                }
            }

            if (view.BoundCharacter != character)
            {
                view.Bind(character, isAlly ? CombatCharacterAppearanceView.Facing.FrontLeft : CombatCharacterAppearanceView.Facing.FrontRight);
            }

            view.transform.SetSiblingIndex(i);
        }
    }

    private CombatPartyMemberView CreateView(Transform parent, bool isAlly)
    {
        var rowObject = new GameObject(isAlly ? "AllyMemberView" : "EnemyMemberView", typeof(RectTransform), typeof(LayoutElement), typeof(CombatPartyMemberView));
        rowObject.transform.SetParent(parent, false);
        RectTransform rect = rowObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(280f, 132f);

        LayoutElement layout = rowObject.GetComponent<LayoutElement>();
        layout.preferredWidth = 280f;
        layout.preferredHeight = 132f;

        return rowObject.GetComponent<CombatPartyMemberView>();
    }

    private void OnSkillUsed(Character user, string skillName)
    {
        CombatPartyMemberView view = FindView(user);
        if (view != null)
        {
            view.ShowSkill(skillName, Time.unscaledTime);
        }
    }

    private static void TickList(List<CombatPartyMemberView> views, float currentTime)
    {
        for (int i = 0; i < views.Count; i++)
        {
            views[i]?.Tick(currentTime);
        }
    }

    private static CombatPartyMemberView FindView(List<CombatPartyMemberView> views, Character character)
    {
        for (int i = 0; i < views.Count; i++)
        {
            CombatPartyMemberView view = views[i];
            if (view != null && view.BoundCharacter == character)
            {
                return view;
            }
        }

        return null;
    }

    private static void DestroyView(CombatPartyMemberView view)
    {
        if (view == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(view.gameObject);
        }
        else
        {
            DestroyImmediate(view.gameObject);
        }
    }
}
