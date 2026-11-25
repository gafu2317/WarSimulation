using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class CountryGovernmentTheme
{
    public const string CountryReturnText = "‹ 国へ";
    public const float PageBarHeight = 96;
    public const float PageBarButtonHeight = 88;
    public const float PageBarNoticeHeight = 36;
    public const float PageBarInset = 32;
    public const float PageBarBackWidth = 144;
    public const float PageBarActionWidth = 380;

    public static void PlacePageItem(RectTransform rect, float anchor, float x, float width,
        float y = PageBarHeight / 2, float height = PageBarButtonHeight)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(anchor, 0);
        rect.anchoredPosition = new Vector2(x, y - height / 2);
        rect.sizeDelta = new Vector2(width, height);
    }

    public static readonly Color Background = new Color32(65, 76, 91, 255);
    public static readonly Color Panel = new Color32(82, 96, 113, 255);
    public static readonly Color Face = new Color32(147, 164, 183, 255);
    public static readonly Color Selected = new Color32(89, 106, 126, 255);
    public static readonly Color Text = new Color32(244, 240, 230, 255);
    public static readonly Color Muted = new Color32(224, 231, 238, 255);
    public static readonly Color Ink = new Color32(37, 51, 68, 255);
    public static readonly Color Paper = new Color32(233, 221, 194, 255);
    public static readonly Color PaperMuted = new Color32(79, 94, 112, 255);
    public static readonly Color Accent = new Color32(226, 189, 104, 255);
    public static readonly Color AccentText = new Color32(246, 220, 157, 255);
    public static readonly Color Border = new Color32(145, 164, 184, 255);
    public static readonly Color Line = new Color32(126, 142, 161, 110);
    public static readonly Color Recess = new Color32(52, 65, 80, 255);
    public static readonly Color Positive = new Color32(194, 228, 200, 255);
    public static readonly Color PositiveInk = new Color32(53, 99, 63, 255);
    public static readonly Color Error = new Color32(255, 218, 207, 255);
    public static readonly Color ErrorInk = new Color32(143, 63, 54, 255);
    public static readonly Color PositiveFace = new Color32(47, 120, 84, 255);
    public static readonly Color Play = PositiveFace;
    public static readonly Color Stop = new Color32(153, 81, 77, 255);
    public static readonly Color TimeStop = new Color32(255, 107, 107, 255);
    public static readonly Color TimePlay = Positive;
    public static readonly Color DoubleSpeed = new Color32(111, 199, 142, 255);
    public static readonly Color DisabledFace = new Color32(89, 97, 107, 255);
    public static readonly Color DisabledText = new Color32(185, 195, 207, 255);

    public static void SelectTab(Button button, bool selected)
    {
        Colors(button, selected ? Accent : Face);
        var marker = button.transform.Find("Selected").GetComponent<Graphic>();
        marker.gameObject.SetActive(false);
        foreach (var label in button.GetComponentsInChildren<TextMeshProUGUI>()) label.color = Ink;
    }

    public static void Colors(Selectable control, Color normal)
    {
        var colors = control.colors;
        var tint = SurfaceTint(control, normal);
        colors.normalColor = tint;
        colors.highlightedColor = normal == Face || normal == Accent ? Color.Lerp(tint, Color.white, .05f) : tint;
        colors.selectedColor = control is Button ? tint : colors.highlightedColor;
        colors.pressedColor = tint;
        colors.disabledColor = control is Button ? SurfaceTint(control, DisabledFace) : new Color32(43, 50, 61, 255);
        colors.fadeDuration = .09f;
        control.colors = colors;
    }

    private static Color SurfaceTint(Selectable control, Color color)
    {
        // 生成りの原画をそのまま乗算すると、濃紺文字との明暗差が不足するため。
        if (control.targetGraphic is Image image && image.sprite != null && image.sprite.texture.name == "GameTab-v1")
            return new Color(Mathf.Min(1, color.r * 255 / 239), Mathf.Min(1, color.g * 255 / 234), Mathf.Min(1, color.b * 255 / 225), color.a);
        return color;
    }
}
