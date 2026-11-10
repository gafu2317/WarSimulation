using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static CombatBattleMapView;

public sealed class CombatPlayerMagicCutInView : MonoBehaviour
{
    private CanvasGroup _group;
    private RectTransform _band;
    private TMP_Text _label;
    private Image _accent;
    private float _age = -1;
    private const float Duration = 1.2f;
    public bool IsVisible => gameObject.activeSelf && _age >= 0;
    public string Caption => _label.text;

    public static CombatPlayerMagicCutInView Create(Transform parent, TMP_FontAsset font)
    {
        var root = Rect("PlayerMagicCutIn", parent);
        Stretch(root, Vector2.zero, Vector2.one);
        var view = root.gameObject.AddComponent<CombatPlayerMagicCutInView>();
        view._group = root.gameObject.AddComponent<CanvasGroup>();
        view._group.blocksRaycasts = view._group.interactable = false;
        view._band = Rect("Band", root);
        Stretch(view._band, new Vector2(.08f, .5f), new Vector2(.92f, .5f));
        view._band.sizeDelta = new Vector2(0, 150);
        var background = view._band.gameObject.AddComponent<Image>();
        background.color = new Color(.015f, .025f, .05f, .9f); background.raycastTarget = false;
        view._accent = Rect("Accent", view._band).gameObject.AddComponent<Image>();
        Stretch(view._accent.rectTransform, new Vector2(0, 1), Vector2.one);
        view._accent.rectTransform.sizeDelta = new Vector2(0, 4);
        view._accent.raycastTarget = false;
        view._label = Label(view._band, "Caption", "", font, 52);
        Stretch(view._label.rectTransform, Vector2.zero, Vector2.one);
        view.Clear();
        return view;
    }

    public void Show(SkillId id)
    {
        if (!CombatPlayerMagicSettings.IsPlayerMagic(id)) return;
        _label.text = CombatPlayerMagicSettings.Label(id) + "発動";
        _accent.color = _label.color = SkillVfxArt.ColorFor(id);
        _age = 0; _group.alpha = 0;
        gameObject.SetActive(true); transform.SetAsLastSibling();
        Animate();
    }
    public void Clear()
    {
        _age = -1; _group.alpha = 0; gameObject.SetActive(false);
    }
    private void OnDisable() => _age = -1;
    private void Update()
    {
        _age += Time.unscaledDeltaTime;
        if (_age >= Duration) { Clear(); return; }
        Animate();
    }
    private void Animate()
    {
        float enter = Mathf.SmoothStep(0, 1, Mathf.Clamp01(_age / .15f));
        _group.alpha = enter * Mathf.Clamp01((Duration - _age) / .2f);
        _band.anchoredPosition = new Vector2((1 - enter) * ((RectTransform)transform).rect.width * .12f, 0);
    }
}
