using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public struct CombatVisionRecognitionDebugEntry
{
    public string TargetName;
    public bool IsVisible;
    public CombatVisionMemorySource MemorySource;
    public string SharedFromName;
    public float RemainingSeconds;
    public Vector3 DisplayPosition;
}

public sealed class CombatVisionDebugView : MonoBehaviour
{
    private const string GeneratedRootName = "GeneratedVisionDebug";

    [SerializeField] private bool _visible = true;
    [SerializeField] private Camera _cameraTarget;
    [SerializeField] private LayerMask _selectionLayers;
    [SerializeField, Min(0.1f)] private float _maxSelectionDistance = 1000f;
    [SerializeField, Min(0.01f)] private float _lineWidth = 0.08f;
    [SerializeField, Min(0f)] private float _lineHeightOffset = 1.2f;
    [SerializeField] private Color _recognitionLineColor = new Color(1f, 0.9f, 0.2f, 0.95f);

    [Header("Runtime Debug")]
    [SerializeField] private Character _selectedCharacter;
    [SerializeField] private string _selectedCharacterName = "-";
    [SerializeField] private List<CombatVisionRecognitionDebugEntry> _recognizedTargets = new();

    private readonly List<LineRenderer> _linePool = new();

    private CombatVision _selectedVision;
    private Transform _generatedRoot;

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
            ClearRuntimeState();
            return;
        }

        HandleSelectionInput();
        RefreshSelectionState();
    }

    private void LateUpdate()
    {
        if (!_visible || _selectedCharacter == null || _selectedVision == null)
        {
            HideUnusedLines(0);
            return;
        }

        RenderRecognitionLines();
    }

    private void HandleSelectionInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

        Camera camera = ResolveCamera();
        if (camera == null)
        {
            ClearRuntimeState();
            return;
        }

        Ray ray = camera.ScreenPointToRay(mouse.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, _maxSelectionDistance, _selectionLayers))
        {
            ClearRuntimeState();
            return;
        }

        Character character = hit.collider.GetComponentInParent<Character>();
        if (character == null)
        {
            ClearRuntimeState();
            return;
        }

        _selectedCharacter = character;
        _selectedVision = character.Vision;
    }

    private void RefreshSelectionState()
    {
        if (_selectedCharacter == null)
        {
            _selectedVision = null;
            _selectedCharacterName = "-";
            _recognizedTargets.Clear();
            return;
        }

        _selectedVision = _selectedCharacter.Vision;
        _selectedCharacterName = _selectedCharacter.name;
        _recognizedTargets.Clear();

        if (_selectedVision == null) return;

        IReadOnlyList<Character> visibleEnemies = _selectedVision.VisibleEnemies;
        for (int i = 0; i < visibleEnemies.Count; i++)
        {
            Character enemy = visibleEnemies[i];
            if (enemy == null) continue;

            _recognizedTargets.Add(new CombatVisionRecognitionDebugEntry
            {
                TargetName = enemy.name,
                IsVisible = true,
                MemorySource = CombatVisionMemorySource.DirectSight,
                SharedFromName = string.Empty,
                RemainingSeconds = _selectedVision.SearchTimeoutSeconds,
                DisplayPosition = GetCharacterAnchor(enemy),
            });
        }

        IReadOnlyList<CombatVisionDebugMemorySnapshot> memories = _selectedVision.GetDebugMemorySnapshots();
        for (int i = 0; i < memories.Count; i++)
        {
            CombatVisionDebugMemorySnapshot memory = memories[i];
            if (memory.Target == null || !memory.HasPosition) continue;
            if (_selectedVision.IsVisible(memory.Target)) continue;

            _recognizedTargets.Add(new CombatVisionRecognitionDebugEntry
            {
                TargetName = memory.Target.name,
                IsVisible = false,
                MemorySource = memory.Source,
                SharedFromName = memory.SharedFrom != null ? memory.SharedFrom.name : string.Empty,
                RemainingSeconds = memory.RemainingSeconds,
                DisplayPosition = memory.LastSeenPosition + Vector3.up * _lineHeightOffset,
            });
        }
    }

    private void RenderRecognitionLines()
    {
        Vector3 origin = GetCharacterAnchor(_selectedCharacter);
        int lineIndex = 0;

        for (int i = 0; i < _recognizedTargets.Count; i++)
        {
            CombatVisionRecognitionDebugEntry entry = _recognizedTargets[i];
            RenderLine(ref lineIndex, origin, entry.DisplayPosition, _recognitionLineColor);
        }

        HideUnusedLines(lineIndex);
    }

    private void RenderLine(ref int lineIndex, Vector3 start, Vector3 end, Color color)
    {
        LineRenderer line = GetLine(lineIndex);
        line.enabled = true;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.startColor = color;
        line.endColor = color;
        line.widthMultiplier = _lineWidth;
        ApplyMaterialColor(line.sharedMaterial, color);
        lineIndex++;
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
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.sharedMaterial = CreateDefaultMaterial(_recognitionLineColor);
            _linePool.Add(line);
        }

        return _linePool[index];
    }

    private void HideUnusedLines(int usedLines)
    {
        for (int i = usedLines; i < _linePool.Count; i++)
        {
            if (_linePool[i] != null)
            {
                _linePool[i].enabled = false;
            }
        }
    }

    private void ClearRuntimeState()
    {
        _selectedCharacter = null;
        _selectedVision = null;
        _selectedCharacterName = "-";
        _recognizedTargets.Clear();
        HideUnusedLines(0);
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

    private static void ApplyMaterialColor(Material material, Color color)
    {
        if (material == null) return;
        material.color = color;
    }
}
