using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(UnityEngine.UI.Button))]
public sealed class CountryButtonTint : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Graphic[] _parts;
    private UnityEngine.UI.Button _button;
    private Color _color;
    private bool _ready;
    private void Awake() => _button = GetComponent<UnityEngine.UI.Button>();
    private void OnEnable() => _ready = false;
    private void LateUpdate()
    {
        var color = _button.targetGraphic.canvasRenderer.GetColor();
        if (_ready && color == _color) return;
        _color = color; _ready = true;
        foreach (var part in _parts) part.canvasRenderer.SetColor(color);
    }
}
