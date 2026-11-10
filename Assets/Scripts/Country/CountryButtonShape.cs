using UnityEngine;

[DisallowMultipleComponent]
public sealed class CountryButtonShape : MonoBehaviour, ICanvasRaycastFilter
{
    [SerializeField] private UnityEngine.UI.Image[] _images;
    [SerializeField] private RectTransform _label;

    public bool IsRaycastLocationValid(Vector2 point, Camera eventCamera)
    {
        foreach (var image in _images)
        {
            if (!image.isActiveAndEnabled || image.sprite == null) continue;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(image.rectTransform, point, eventCamera, out var local);
            var rect = image.GetPixelAdjustedRect();
            var sprite = image.sprite.rect;
            if (image.preserveAspect)
            {
                var size = sprite.size * Mathf.Min(rect.width / sprite.width, rect.height / sprite.height);
                rect.position += Vector2.Scale(rect.size - size, image.rectTransform.pivot);
                rect.size = size;
            }
            if (!rect.Contains(local)) continue;
            var pixel = sprite.position + Vector2.Scale((local - rect.position) / rect.size, sprite.size);
            var texture = image.sprite.texture;
            if (texture.GetPixelBilinear(pixel.x / texture.width, pixel.y / texture.height).a >= .12f) return true;
        }
        return _label != null && RectTransformUtility.RectangleContainsScreenPoint(_label, point, eventCamera);
    }
}
