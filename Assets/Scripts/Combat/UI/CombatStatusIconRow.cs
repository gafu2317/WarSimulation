using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class CombatStatusIconRow
{
    private readonly RectTransform _root;
    private readonly List<Image> _images = new();
    private readonly List<CombatStatusIconKind> _kinds = new();
    private readonly List<Vector2> _sizes = new();
    private readonly float _size;
    private readonly float _gap;
    private readonly TextAnchor _alignment;

    public int ActiveCount => _kinds.Count;

    public CombatStatusIconRow(RectTransform root, float size, float gap, TextAnchor alignment)
    {
        _root = root;
        _size = size;
        _gap = gap;
        _alignment = alignment;
        HorizontalLayoutGroup layout = root.GetComponent<HorizontalLayoutGroup>();
        if (layout != null) layout.enabled = false;
        for (int i = 0; i < root.childCount; i++)
        {
            Image image = root.GetChild(i).GetComponent<Image>();
            if (image != null) _images.Add(image);
        }
    }

    public void Refresh(Character character)
    {
        CombatStatusIconSource.Collect(character, _kinds);
        while (_images.Count < _kinds.Count)
        {
            var child = new GameObject("StatusIcon", typeof(RectTransform), typeof(Image));
            child.transform.SetParent(_root, false);
            _images.Add(child.GetComponent<Image>());
        }

        CombatStatusEffectIconCatalog catalog = CombatStatusEffectIconCatalog.Default;
        _sizes.Clear();
        float iconWidth = 0f;
        for (int i = 0; i < _kinds.Count; i++)
        {
            Sprite sprite = catalog != null ? catalog.GetSprite(_kinds[i]) : null;
            Vector2 size = GetIconSize(sprite);
            _sizes.Add(size);
            iconWidth += size.x;
        }

        float gap = GetGap(_root.rect.width, iconWidth, _gap, _kinds.Count);
        float width = _kinds.Count == 0 ? 0f : iconWidth + gap * (_kinds.Count - 1);
        float start = _alignment switch
        {
            TextAnchor.MiddleLeft => _root.rect.xMin,
            TextAnchor.MiddleRight => _root.rect.xMax - width,
            _ => -width / 2f,
        };
        float cursor = start;
        for (int i = 0; i < _images.Count; i++)
        {
            Image image = _images[i];
            image.gameObject.SetActive(i < _kinds.Count);
            if (i >= _kinds.Count) continue;
            image.sprite = catalog != null ? catalog.GetSprite(_kinds[i]) : null;
            image.enabled = image.sprite != null;
            image.color = Color.white;
            image.preserveAspect = false;
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            Vector2 iconSize = _sizes[i];
            rect.sizeDelta = iconSize;
            rect.anchoredPosition = new Vector2(cursor + iconSize.x / 2f, 0f);
            cursor += iconSize.x + gap;
            // 左側を前面にすることで、右端に描かれた上下矢印を隠さない。
            rect.SetAsFirstSibling();
        }
        _root.gameObject.SetActive(_kinds.Count > 0);
    }

    public static float GetStep(float width, float size, float gap, int count)
    {
        return count <= 1 ? 0f : Mathf.Max(0f, Mathf.Min(size + gap, (width - size) / (count - 1)));
    }

    private Vector2 GetIconSize(Sprite sprite)
    {
        if (sprite == null || sprite.rect.width <= 0f || sprite.rect.height <= 0f)
            return Vector2.one * _size;

        float scale = _size / Mathf.Max(sprite.rect.width, sprite.rect.height);
        return sprite.rect.size * scale;
    }

    private static float GetGap(float width, float iconWidth, float gap, int count)
    {
        return count <= 1 ? 0f : Mathf.Min(gap, (width - iconWidth) / (count - 1));
    }
}
