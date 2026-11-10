using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class CountryRosterConfirm : MonoBehaviour
{
    [SerializeField] private GameObject _window;
    [SerializeField] private CanvasGroup _blocked;
    [SerializeField] private TextMeshProUGUI _title, _body, _confirmLabel;
    [SerializeField] private UnityEngine.UI.Button _confirm, _cancel;
    [SerializeField] private UnityEngine.UI.ScrollRect _bodyScroll;
    private Action _accept;
    private GameObject _returnFocus;
    public bool IsOpen => _window.activeSelf;
    private void Awake()
    {
        _confirm.onClick.AddListener(() => { var action = _accept; Close(); action?.Invoke(); });
        _cancel.onClick.AddListener(Close);
    }
    public void Show(string title, string body, string confirmLabel, Action accept)
    {
        _returnFocus = EventSystem.current?.currentSelectedGameObject;
        _title.text = title; _body.text = body; _confirmLabel.text = confirmLabel; _accept = accept;
        _confirm.gameObject.SetActive(accept != null);
        _cancel.GetComponentInChildren<TextMeshProUGUI>().text = accept == null ? "閉じる" : "戻る";
        _blocked.interactable = _blocked.blocksRaycasts = false;
        _window.SetActive(true); _cancel.Select();
        var canvas = _window.GetComponent<Canvas>(); canvas.overrideSorting = true; canvas.sortingOrder = 80;
        if (_bodyScroll != null) { Canvas.ForceUpdateCanvases(); _bodyScroll.verticalNormalizedPosition = 1; }
    }
    public void ShowDetails(string title, string body) => Show(title, body, "", null);
    public void Close()
    {
        if (!IsOpen) return;
        _accept = null; _window.SetActive(false);
        _blocked.interactable = _blocked.blocksRaycasts = true;
        if (EventSystem.current != null && _returnFocus != null && _returnFocus.activeInHierarchy)
            EventSystem.current.SetSelectedGameObject(_returnFocus);
    }
    private void OnDisable() => Close();
}
