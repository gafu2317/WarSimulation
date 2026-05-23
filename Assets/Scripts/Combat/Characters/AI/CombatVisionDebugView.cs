using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class CombatVisionDebugView : MonoBehaviour
{
    private const string GeneratedRootName = "GeneratedVisionDebug";
    private const float Padding = 8f;
    private const float HeaderHeight = 24f;
    private const float MarkerScale = 0.35f;

    [SerializeField] private bool _visible = true;
    [SerializeField] private Camera _cameraTarget;
    [SerializeField] private LayerMask _selectionLayers;
    [SerializeField, Min(0.1f)] private float _maxSelectionDistance = 1000f;
    [SerializeField, Min(120f)] private float _panelWidth = 720f;
    [SerializeField, Min(120f)] private float _panelHeight = 320f;
    [SerializeField, Min(1f)] private float _lineWidth = 0.08f;
    [SerializeField, Min(0f)] private float _lineHeightOffset = 1.2f;
    [SerializeField, Min(0f)] private float _markerHeightOffset = 0.15f;
    [SerializeField, Min(1)] private int _fontSize = 40;
    [SerializeField] private Color _backgroundColor = new Color(0f, 0f, 0f, 0.72f);
    [SerializeField] private Color _textColor = Color.white;
    [SerializeField] private Color _titleColor = new Color(0.9f, 0.95f, 1f, 1f);
    [SerializeField] private Color _visibleEnemyColor = new Color(1f, 0.18f, 0.14f, 0.95f);
    [SerializeField] private Color _rememberedEnemyColor = new Color(1f, 0.85f, 0.12f, 0.95f);
    [SerializeField] private Color _communicableAllyColor = new Color(0.25f, 1f, 0.25f, 0.9f);
    [SerializeField] private Color _blockedAllyColor = new Color(0.45f, 0.45f, 0.45f, 0.75f);

    private readonly List<LineRenderer> _linePool = new();
    private readonly List<GameObject> _markerPool = new();

    private Character _selectedCharacter;
    private CombatVision _selectedVision;
    private Transform _generatedRoot;
    private GUIStyle _labelStyle;
    private GUIStyle _titleStyle;
    private Texture2D _backgroundTexture;
    private Color _cachedBackgroundColor;
    private Vector2 _scroll;

    private void Reset()
    {
        _selectionLayers = LayerMask.GetMask("Character");
    }

    private void Awake()
    {
        if (_selectionLayers.value == 0)
        {
            _selectionLayers = LayerMask.GetMask("Character");
        }

        EnsureGeneratedRoot();
    }

    private void Update()
    {
        if (!_visible)
        {
            HideDebugObjects();
            return;
        }

        HandleSelectionInput();
    }

    private void LateUpdate()
    {
        if (!_visible || _selectedCharacter == null || _selectedVision == null)
        {
            HideDebugObjects();
            return;
        }

        RenderDebugObjects();
    }

    private void OnGUI()
    {
        if (!_visible) return;

        EnsureGuiResources();

        float width = Mathf.Min(_panelWidth, Mathf.Max(1f, Screen.width - Padding * 2f));
        float height = Mathf.Min(_panelHeight, Mathf.Max(1f, Screen.height - Padding * 2f));
        Rect panelRect = new Rect(
            (Screen.width - width) * 0.5f,
            Screen.height - height - Padding,
            width,
            height);

        GUI.DrawTexture(panelRect, _backgroundTexture);

        string title = _selectedCharacter != null
            ? $"視界共有デバッグ: {_selectedCharacter.name}"
            : "視界共有デバッグ: 未選択";
        GUI.Label(
            new Rect(panelRect.x + Padding, panelRect.y + Padding, panelRect.width - Padding * 2f, HeaderHeight),
            title,
            _titleStyle);

        Rect viewRect = new Rect(
            panelRect.x + Padding,
            panelRect.y + Padding + HeaderHeight,
            panelRect.width - Padding * 2f,
            panelRect.height - Padding * 2f - HeaderHeight);
        Rect contentRect = new Rect(0f, 0f, viewRect.width - 16f, Mathf.Max(viewRect.height, EstimateContentHeight()));

        _scroll = GUI.BeginScrollView(viewRect, _scroll, contentRect);
        GUI.Label(new Rect(0f, 0f, contentRect.width, contentRect.height), BuildPanelText(), _labelStyle);
        GUI.EndScrollView();
    }

    private void HandleSelectionInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

        Camera camera = ResolveCamera();
        if (camera == null)
        {
            ClearSelection();
            return;
        }

        Ray ray = camera.ScreenPointToRay(mouse.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, _maxSelectionDistance, _selectionLayers))
        {
            ClearSelection();
            return;
        }

        Character character = hit.collider.GetComponentInParent<Character>();
        if (character == null)
        {
            ClearSelection();
            return;
        }

        _selectedCharacter = character;
        _selectedVision = character.Vision;
        _scroll = Vector2.zero;
    }

    private void ClearSelection()
    {
        _selectedCharacter = null;
        _selectedVision = null;
        HideDebugObjects();
    }

    private string BuildPanelText()
    {
        if (_selectedCharacter == null || _selectedVision == null)
        {
            return "未選択";
        }

        var sb = new StringBuilder(1024);
        IReadOnlyList<Character> visibleEnemies = _selectedVision.VisibleEnemies;
        IReadOnlyList<CombatVisionDebugMemorySnapshot> memories = _selectedVision.GetDebugMemorySnapshots();
        IReadOnlyList<CombatVisionDebugCommunicationSnapshot> communications = _selectedVision.GetDebugCommunicationSnapshots();

        sb.AppendLine($"視認中の敵: {FormatCharacterList(visibleEnemies)}");
        sb.AppendLine("記憶中の敵:");
        AppendMemoryLines(sb, memories);
        sb.AppendLine();
        sb.AppendLine($"通信可能な味方: {FormatCommunicationList(communications, canCommunicate: true)}");
        sb.AppendLine($"通信不可の味方: {FormatCommunicationList(communications, canCommunicate: false)}");
        sb.AppendLine($"最後に送信: {FormatRecentCharacter(_selectedVision.LastSharedTo, _selectedVision.LastSharedAgeSeconds)}");
        sb.AppendLine($"最後に受信: {FormatRecentCharacter(_selectedVision.LastReceivedFrom, _selectedVision.LastReceivedAgeSeconds)}");

        return sb.ToString();
    }

    private static void AppendMemoryLines(StringBuilder sb, IReadOnlyList<CombatVisionDebugMemorySnapshot> memories)
    {
        bool wroteAny = false;
        for (int i = 0; i < memories.Count; i++)
        {
            CombatVisionDebugMemorySnapshot memory = memories[i];
            if (memory.Target == null || !memory.HasPosition) continue;

            wroteAny = true;
            sb.Append("  ");
            sb.Append(memory.Target.name);
            sb.Append("  残り ");
            sb.Append(memory.RemainingSeconds.ToString("0.#"));
            sb.Append("s  ");
            sb.Append(FormatMemorySource(memory));
            sb.AppendLine();
        }

        if (!wroteAny)
        {
            sb.AppendLine("  -");
        }
    }

    private static string FormatMemorySource(CombatVisionDebugMemorySnapshot memory)
    {
        if (memory.Source == CombatVisionMemorySource.Shared)
        {
            string name = memory.SharedFrom != null ? memory.SharedFrom.name : "?";
            return $"共有: {name}";
        }

        return "直接視認";
    }

    private static string FormatCharacterList(IReadOnlyList<Character> characters)
    {
        if (characters == null || characters.Count == 0) return "-";

        var sb = new StringBuilder(128);
        for (int i = 0; i < characters.Count; i++)
        {
            Character character = characters[i];
            if (character == null) continue;
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(character.name);
        }

        return sb.Length > 0 ? sb.ToString() : "-";
    }

    private static string FormatCommunicationList(
        IReadOnlyList<CombatVisionDebugCommunicationSnapshot> communications,
        bool canCommunicate)
    {
        if (communications == null || communications.Count == 0) return "-";

        var sb = new StringBuilder(128);
        for (int i = 0; i < communications.Count; i++)
        {
            CombatVisionDebugCommunicationSnapshot communication = communications[i];
            if (communication.Ally == null || communication.CanCommunicate != canCommunicate) continue;
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(communication.Ally.name);
        }

        return sb.Length > 0 ? sb.ToString() : "-";
    }

    private static string FormatRecentCharacter(Character character, float ageSeconds)
    {
        if (character == null || float.IsInfinity(ageSeconds)) return "-";

        return $"{character.name} {ageSeconds:0.#}s前";
    }

    private float EstimateContentHeight()
    {
        if (_selectedVision == null) return _fontSize * 2f;

        int memoryCount = 0;
        IReadOnlyList<CombatVisionDebugMemorySnapshot> memories = _selectedVision.GetDebugMemorySnapshots();
        for (int i = 0; i < memories.Count; i++)
        {
            if (memories[i].HasPosition) memoryCount++;
        }

        return (_fontSize + 4f) * Mathf.Max(8, memoryCount + 7);
    }

    private void RenderDebugObjects()
    {
        int lineIndex = 0;
        int markerIndex = 0;
        Vector3 origin = GetCharacterAnchor(_selectedCharacter);

        IReadOnlyList<Character> visibleEnemies = _selectedVision.VisibleEnemies;
        for (int i = 0; i < visibleEnemies.Count; i++)
        {
            Character enemy = visibleEnemies[i];
            if (enemy == null) continue;

            RenderLine(ref lineIndex, origin, GetCharacterAnchor(enemy), _visibleEnemyColor);
        }

        IReadOnlyList<CombatVisionDebugMemorySnapshot> memories = _selectedVision.GetDebugMemorySnapshots();
        for (int i = 0; i < memories.Count; i++)
        {
            CombatVisionDebugMemorySnapshot memory = memories[i];
            if (memory.Target == null || !memory.HasPosition) continue;
            if (_selectedVision.IsVisible(memory.Target)) continue;

            Vector3 position = memory.LastSeenPosition + Vector3.up * _markerHeightOffset;
            RenderLine(ref lineIndex, origin, position, _rememberedEnemyColor);
            RenderMarker(ref markerIndex, position, _rememberedEnemyColor);
        }

        IReadOnlyList<CombatVisionDebugCommunicationSnapshot> communications = _selectedVision.GetDebugCommunicationSnapshots();
        for (int i = 0; i < communications.Count; i++)
        {
            CombatVisionDebugCommunicationSnapshot communication = communications[i];
            if (communication.Ally == null) continue;

            Color color = communication.CanCommunicate ? _communicableAllyColor : _blockedAllyColor;
            RenderLine(ref lineIndex, origin, GetCharacterAnchor(communication.Ally), color);
        }

        HideUnusedDebugObjects(lineIndex, markerIndex);
    }

    private void RenderLine(ref int lineIndex, Vector3 start, Vector3 end, Color color)
    {
        LineRenderer line = GetLine(lineIndex);
        line.enabled = true;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        ApplyLineColor(line, color);
        lineIndex++;
    }

    private void RenderMarker(ref int markerIndex, Vector3 position, Color color)
    {
        GameObject marker = GetMarker(markerIndex);
        marker.SetActive(true);
        marker.transform.position = position;
        marker.transform.localScale = Vector3.one * MarkerScale;
        ApplyRendererColor(marker.GetComponent<Renderer>(), color);
        markerIndex++;
    }

    private LineRenderer GetLine(int index)
    {
        EnsureGeneratedRoot();
        while (_linePool.Count <= index)
        {
            var go = new GameObject($"VisionDebugLine{_linePool.Count}", typeof(LineRenderer));
            go.transform.SetParent(_generatedRoot, worldPositionStays: false);
            LineRenderer line = go.GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = _lineWidth;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.sharedMaterial = CreateDefaultMaterial(Color.white);
            _linePool.Add(line);
        }

        return _linePool[index];
    }

    private GameObject GetMarker(int index)
    {
        EnsureGeneratedRoot();
        while (_markerPool.Count <= index)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = $"VisionDebugMarker{_markerPool.Count}";
            marker.transform.SetParent(_generatedRoot, worldPositionStays: false);
            Collider collider = marker.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(collider);
                }
                else
                {
                    DestroyImmediate(collider);
                }
            }

            Renderer renderer = marker.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = CreateDefaultMaterial(Color.white);
            }

            _markerPool.Add(marker);
        }

        return _markerPool[index];
    }

    private void HideUnusedDebugObjects(int usedLines, int usedMarkers)
    {
        for (int i = usedLines; i < _linePool.Count; i++)
        {
            if (_linePool[i] != null)
            {
                _linePool[i].enabled = false;
            }
        }

        for (int i = usedMarkers; i < _markerPool.Count; i++)
        {
            if (_markerPool[i] != null)
            {
                _markerPool[i].SetActive(false);
            }
        }
    }

    private void HideDebugObjects()
    {
        HideUnusedDebugObjects(0, 0);
    }

    private Vector3 GetCharacterAnchor(Character character)
    {
        return character != null
            ? character.transform.position + Vector3.up * _lineHeightOffset
            : Vector3.zero;
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

        var go = new GameObject(GeneratedRootName);
        go.transform.SetParent(transform, worldPositionStays: false);
        _generatedRoot = go.transform;
    }

    private Camera ResolveCamera()
    {
        return _cameraTarget != null ? _cameraTarget : Camera.main;
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
    }

    private static Material CreateDefaultMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;

        var material = new Material(shader) { name = "VisionDebugMaterial" };
        ApplyMaterialColor(material, color);
        return material;
    }

    private void ApplyLineColor(LineRenderer line, Color color)
    {
        if (line == null) return;

        line.widthMultiplier = _lineWidth;
        line.startColor = color;
        line.endColor = color;
        ApplyMaterialColor(line.sharedMaterial, color);
    }

    private static void ApplyRendererColor(Renderer renderer, Color color)
    {
        if (renderer == null) return;

        ApplyMaterialColor(renderer.sharedMaterial, color);
    }

    private static void ApplyMaterialColor(Material material, Color color)
    {
        if (material == null) return;

        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
    }
}
