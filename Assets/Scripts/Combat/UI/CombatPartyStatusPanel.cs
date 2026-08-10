using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-80)]
public sealed class CombatPartyStatusPanel : MonoBehaviour
{
    [SerializeField, Min(0.05f)] private float _syncIntervalSeconds = 0.2f;
    [SerializeField] private RectTransform _allyColumn;
    [SerializeField] private RectTransform _enemyColumn;
    [SerializeField] private CombatPartyMemberView _allyTemplate;
    [SerializeField] private CombatPartyMemberView _enemyTemplate;

    private readonly List<CombatPartyMemberView> _allyViews = new();
    private readonly List<CombatPartyMemberView> _enemyViews = new();
    private CombatCharacterSystem _characterSystem;
    private ScrollRect _allyScrollRect;
    private float _nextSyncTime;

    public int AllyViewCount => CountActiveViews(_allyViews);
    public int EnemyViewCount => CountActiveViews(_enemyViews);

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
        CombatSkillUseEvents.SkillUsed -= OnSkillUsed;
    }

    public void Initialize(CombatCharacterSystem characterSystem)
    {
        CombatSkillUseEvents.SkillUsed -= OnSkillUsed;
        CombatSkillUseEvents.SkillUsed += OnSkillUsed;
        _characterSystem = characterSystem;
        ForceSyncNow();
    }

    public void ForceSyncNow()
    {
        EnsureBuilt();
        TryResolveCharacterSystem();
        List<Character> allies = CollectTeamCharacters(CombatTeam.Ally);
        List<Character> enemies = CollectTeamCharacters(CombatTeam.Enemy);
        if (allies == null && enemies == null)
        {
            return;
        }

        SyncTeam(allies, _allyViews, isAlly: true);
        SyncTeam(enemies, _enemyViews, isAlly: false);
        ClearFocusIfMissing(allies, enemies);
    }

    private static void ClearFocusIfMissing(List<Character> allies, List<Character> enemies)
    {
        Character selected = CombatPartyFocus.Selected;
        if (ReferenceEquals(selected, null))
        {
            return;
        }

        // Unity fake-null after destroy, or character left the synced teams.
        if (selected == null
            || (!ContainsCharacter(allies, selected) && !ContainsCharacter(enemies, selected)))
        {
            CombatPartyFocus.Clear();
        }
    }

    private static bool ContainsCharacter(List<Character> characters, Character character)
    {
        if (characters == null)
        {
            return false;
        }

        for (int i = 0; i < characters.Count; i++)
        {
            if (characters[i] == character)
            {
                return true;
            }
        }

        return false;
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
        if (_allyColumn == null)
        {
            _allyColumn = transform.Find("AlliesColumn") as RectTransform;
        }

        if (_enemyColumn == null)
        {
            _enemyColumn = transform.Find("EnemiesColumn") as RectTransform;
        }

        EnsureTemplates();
        EnsureAllyScrollView();
        RehydrateExistingViews(_allyColumn, _allyTemplate, _allyViews);
        RehydrateExistingViews(_enemyColumn, _enemyTemplate, _enemyViews);
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

    private List<Character> CollectTeamCharacters(CombatTeam team)
    {
        if (_characterSystem != null)
        {
            List<Character> configured = team == CombatTeam.Ally
                ? _characterSystem.AllyCharacters
                : _characterSystem.EnemyCharacters;
            List<Character> filtered = FilterCharacters(configured, team);
            if (filtered.Count > 0)
            {
                return filtered;
            }
        }

        Character[] allCharacters = FindObjectsByType<Character>(FindObjectsInactive.Exclude);
        if (allCharacters == null || allCharacters.Length == 0)
        {
            return null;
        }

        var discovered = new List<Character>();
        for (int i = 0; i < allCharacters.Length; i++)
        {
            Character character = allCharacters[i];
            if (character == null || character.Team != team)
            {
                continue;
            }

            discovered.Add(character);
        }

        return discovered;
    }

    private static List<Character> FilterCharacters(List<Character> source, CombatTeam team)
    {
        var filtered = new List<Character>();
        if (source == null)
        {
            return filtered;
        }

        for (int i = 0; i < source.Count; i++)
        {
            Character character = source[i];
            if (character == null || character.Team != team)
            {
                continue;
            }

            filtered.Add(character);
        }

        return filtered;
    }

    private void SyncTeam(
        List<Character> characters,
        List<CombatPartyMemberView> views,
        bool isAlly)
    {
        int targetCount = characters != null ? characters.Count : 0;
        EnsureViewCapacity(
            isAlly ? _allyColumn : _enemyColumn,
            isAlly ? _allyTemplate : _enemyTemplate,
            views,
            targetCount,
            isAlly);

        for (int i = 0; i < views.Count; i++)
        {
            CombatPartyMemberView view = views[i];
            if (view == null)
            {
                continue;
            }

            if (i >= targetCount)
            {
                view.gameObject.SetActive(false);
                continue;
            }

            Character character = characters[i];
            if (character == null)
            {
                view.gameObject.SetActive(false);
                continue;
            }

            if (view.BoundCharacter != character)
            {
                view.Bind(character, isAlly ? CombatCharacterAppearanceView.Facing.FrontLeft : CombatCharacterAppearanceView.Facing.FrontRight);
            }

            view.gameObject.SetActive(true);
            view.transform.SetSiblingIndex(i);
        }
    }

    private void EnsureTemplates()
    {
        if (_allyTemplate == null && _allyColumn != null)
        {
            _allyTemplate = FindDirectChildTemplate(_allyColumn);
        }

        if (_enemyTemplate == null && _enemyColumn != null)
        {
            _enemyTemplate = FindDirectChildTemplate(_enemyColumn);
        }

        if (_allyTemplate != null)
        {
            _allyTemplate.gameObject.SetActive(false);
        }

        if (_enemyTemplate != null)
        {
            _enemyTemplate.gameObject.SetActive(false);
        }
    }

    private void EnsureAllyScrollView()
    {
        if (_allyColumn == null || _allyScrollRect != null)
        {
            return;
        }

        _allyScrollRect = _allyColumn.GetComponentInParent<ScrollRect>();
        if (_allyScrollRect != null)
        {
            return;
        }

        HorizontalLayoutGroup layout = _allyColumn.GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
        {
            layout.childControlWidth = true;
            layout.childControlHeight = true;
        }

        float preferredHeight = GetPreferredViewportHeight(_allyColumn);
        if (preferredHeight <= 0f)
        {
            return;
        }

        Transform originalParent = _allyColumn.parent;
        int siblingIndex = _allyColumn.GetSiblingIndex();
        var viewportObject = new GameObject("AlliesViewport", typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect));
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        viewport.SetParent(originalParent, false);
        viewport.SetSiblingIndex(siblingIndex);
        viewport.anchorMin = _allyColumn.anchorMin;
        viewport.anchorMax = _allyColumn.anchorMax;
        viewport.anchoredPosition = new Vector2(0f, _allyColumn.anchoredPosition.y);
        viewport.sizeDelta = new Vector2(-Mathf.Abs(_allyColumn.anchoredPosition.x) * 2f, preferredHeight);
        viewport.pivot = _allyColumn.pivot;

        _allyColumn.SetParent(viewport, false);
        _allyColumn.anchorMin = new Vector2(0.5f, 0.5f);
        _allyColumn.anchorMax = new Vector2(0.5f, 0.5f);
        _allyColumn.anchoredPosition = Vector2.zero;
        _allyColumn.pivot = new Vector2(0.5f, 0.5f);

        _allyScrollRect = viewportObject.GetComponent<ScrollRect>();
        _allyScrollRect.content = _allyColumn;
        _allyScrollRect.viewport = viewport;
        _allyScrollRect.horizontal = true;
        _allyScrollRect.vertical = false;
        _allyScrollRect.movementType = ScrollRect.MovementType.Clamped;
    }

    private static float GetPreferredViewportHeight(RectTransform column)
    {
        float preferredHeight = LayoutUtility.GetPreferredHeight(column);
        for (int i = 0; i < column.childCount && preferredHeight <= 0f; i++)
        {
            preferredHeight = Mathf.Max(
                preferredHeight,
                LayoutUtility.GetPreferredHeight(column.GetChild(i) as RectTransform));
        }

        return preferredHeight;
    }

    private static CombatPartyMemberView FindDirectChildTemplate(RectTransform column)
    {
        if (column == null)
        {
            return null;
        }

        for (int i = 0; i < column.childCount; i++)
        {
            CombatPartyMemberView view = column.GetChild(i).GetComponent<CombatPartyMemberView>();
            if (view != null)
            {
                return view;
            }
        }

        return null;
    }

    private static void RehydrateExistingViews(
        RectTransform column,
        CombatPartyMemberView template,
        List<CombatPartyMemberView> views)
    {
        views.Clear();
        if (column == null)
        {
            return;
        }

        for (int i = 0; i < column.childCount; i++)
        {
            CombatPartyMemberView view = column.GetChild(i).GetComponent<CombatPartyMemberView>();
            if (view != null && view != template)
            {
                views.Add(view);
            }
        }
    }

    private static void EnsureViewCapacity(
        RectTransform column,
        CombatPartyMemberView template,
        List<CombatPartyMemberView> views,
        int targetCount,
        bool isAlly)
    {
        if (column == null || template == null)
        {
            return;
        }

        while (views.Count < targetCount)
        {
            GameObject cloneObject = Object.Instantiate(template.gameObject, column, false);
            cloneObject.name = isAlly ? $"AllyMemberView_{views.Count}" : $"EnemyMemberView_{views.Count}";
            cloneObject.SetActive(true);
            CombatPartyMemberView cloneView = cloneObject.GetComponent<CombatPartyMemberView>();
            views.Add(cloneView);
        }
    }

    private void OnSkillUsed(Character user, string skillName)
    {
        CombatPartyMemberView view = FindView(user);
        if (view == null)
        {
            ForceSyncNow();
            view = FindView(user);
        }

        if (view != null)
        {
            view.ShowSkill(skillName, Time.unscaledTime);
        }
    }

    private static int CountActiveViews(List<CombatPartyMemberView> views)
    {
        int count = 0;
        for (int i = 0; i < views.Count; i++)
        {
            CombatPartyMemberView view = views[i];
            if (view != null && view.gameObject.activeSelf)
            {
                count++;
            }
        }

        return count;
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
}
