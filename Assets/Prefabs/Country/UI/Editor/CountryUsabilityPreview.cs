using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class CountryUsabilityPreview
{
    private const string Folder = "Assets/Prefabs/Country/UI/";
    private const string Output = CountryPhonePreview.Folder + "/Usability";
    private static readonly List<string> Checks = new();
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    private static void Check(bool passed, string name)
    { if (!passed) throw new InvalidOperationException(name); Checks.Add(name); }

    [MenuItem("WarSim/Country/Verify Usability In Isolated Preview")]
    public static void Capture()
    {
        if (EditorApplication.isPlaying || PlayDataManager.Instance != null) throw new InvalidOperationException("Use Edit mode without a live game session.");
        Directory.CreateDirectory(Output); Checks.Clear();
        Capture(CountryPhonePreview.Size);
        File.WriteAllText(Output + "/checks.json", Newtonsoft.Json.JsonConvert.SerializeObject(Checks, Newtonsoft.Json.Formatting.Indented));
        Debug.Log($"Usability preview: {Checks.Count} checks passed. The open scene was not saved or reloaded.");
    }

    private static void Capture(Vector2Int size)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var target = RenderTexture.GetTemporary(size.x, size.y, 24, RenderTextureFormat.ARGB32);
        try
        {
            var cameraObject = new GameObject("UI Camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>(); camera.scene = scene; camera.targetTexture = target;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = CountryGovernmentTheme.Background;
            camera.transform.position = new Vector3(0, 0, -10); camera.aspect = (float)size.x / size.y;
            var canvasObject = new GameObject("UI Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 5;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var managers = new GameObject("Preview State"); SceneManager.MoveGameObjectToScene(managers, scene);
            var date = managers.AddComponent<DateManager>(); var resources = managers.AddComponent<PlayDataManager>(); resources.SetResources(8500, 1000, 1200, 800);
            var root = Instance("CountryGovernment"); var government = root.GetComponent<CountryGovernmentPanel>();
            var data = Field<CountryGovernmentData>(government, "_data");
            Set(government, "_date", date); Set(government, "_resources", resources); Set(government, "_draft", resources.GetOrCreateGovernmentDraft(data));
            Call(government, "Awake"); Call(government, "CreateRows");
            var training = Field<CountryTrainingView>(government, "_training"); var view = Field<CountryRosterView>(training, "_view");
            var roster = resources.GetOrCreateRoster(view.Data, new CountryDate(0, 1, 1)); training.Initialize(roster);
            var confirmation = Field<CountryRosterConfirm>(training, "_confirmation"); Call(confirmation, "Awake");
            government.OpenTraining(); Shot("Training-Learning", root);

            var skillText = Field<TextMeshProUGUI>(view, "_skills");
            Check(skillText.text.Contains("単体ダメージ"), "Training shows the combat skill effect");
            Field<Button>(training, "_learning").onClick.Invoke();
            Check(Field<TextMeshProUGUI>(confirmation, "_body").text.Contains("肩代わり"), "Both weapon courses are available from the top learning control");
            Shot("Training-Learning-Details", root); Field<Button>(confirmation, "_cancel").onClick.Invoke();
            skillText.ForceMeshUpdate();
            var link = skillText.textInfo.linkInfo[0]; var character = skillText.textInfo.characterInfo[link.linkTextfirstCharacterIndex];
            var point = camera.WorldToScreenPoint(skillText.transform.TransformPoint((character.bottomLeft + character.topRight) * .5f));
            var pointer = new PointerEventData(null) { position = point, pointerPressRaycast = new RaycastResult { module = canvasObject.GetComponent<GraphicRaycaster>() } };
            ExecuteEvents.ExecuteHierarchy(skillText.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Check(skillText.text.Contains("標準CT"), "Clicking a skill expands details"); Shot("Training-Skill-Details", root);
            var details = Field<ScrollRect>(view, "_details"); details.verticalNormalizedPosition = 0;
            float y = details.content.anchoredPosition.y; view.SelectSoldier(1);
            Check(Mathf.Abs(details.content.anchoredPosition.y - y) < .1f, "Soldier comparison preserves detail position");
            var list = Field<ScrollRect>(view, "_list"); list.verticalNormalizedPosition = 0;
            var action = Field<Button>(view, "_action");
            foreach (int id in new[] { 25, 24, 23 }) { view.SelectSoldier(id); action.onClick.Invoke(); }
            Canvas.ForceUpdateCanvases();
            var row = list.content.Find("Row24"); var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(list.viewport, row);
            Shot("Training-Capacity", root);
            File.WriteAllText(Output + "/capacity-" + size.x + ".json", Newtonsoft.Json.JsonConvert.SerializeObject(new { selected = view.SelectedSoldier, rowY = row.localPosition.y, sibling = row.GetSiblingIndex(), contentY = list.content.anchoredPosition.y, contentHeight = list.content.rect.height, viewportYMin = list.viewport.rect.yMin, viewportYMax = list.viewport.rect.yMax, rowMin = bounds.min.y, rowMax = bounds.max.y }));
            Check(bounds.max.y <= list.viewport.rect.yMax + .5f && bounds.min.y >= list.viewport.rect.yMin - .5f, "Selected row remains fully visible at capacity");
            Set(date, "_currentDay", 21); roster.AdvanceTo(new CountryDate(0, 1, 21));
            government.OpenTraining(); training.ConfirmStart();
            Check(Field<TextMeshProUGUI>(confirmation, "_body").text.Contains("開始前でも変更・キャンセルはできない"), "Reservation confirmation states the immediate restriction");
            Shot("Training-Reservation", root); Field<Button>(confirmation, "_confirm").onClick.Invoke();
            Check(roster.Soldiers[23].Training != null && !roster.IsTraining(23), "Reservation commits before training starts");
            government.Close();

            var personnelRoot = Instance("CountryPersonnel"); var personnel = personnelRoot.GetComponent<CountryPersonnelPanel>();
            var personnelView = Field<CountryRosterView>(personnel, "_view");
            Set(personnel, "_roster", roster); Call(personnel, "Awake"); personnelView.Initialize(roster);
            Field<GameObject>(personnel, "_window").SetActive(true); Set(personnel, "_equipWeapon", 0);
            personnelView.SelectSoldier(1); personnelView.ShowSoldiers(); Call(personnel, "RefreshAction");
            int original = roster.EquippedWeapon(1);
            Check(Field<TextMeshProUGUI[]>(personnelView, "_stats").All(t => t.text.Contains("→")), "Equipment previews all five resulting stats");
            Check(roster.EquippedWeapon(1) == original, "Equipment preview leaves the actual loadout unchanged");
            Shot("Equipment-Comparison", personnelRoot);
            var equipConfirmation = Field<CountryRosterConfirm>(personnel, "_confirmation"); Call(equipConfirmation, "Awake"); Call(personnel, "Action");
            Check(Field<TextMeshProUGUI>(equipConfirmation, "_body").text.Contains("使用可能な技"), "Equipment confirmation contains the skill comparison");
            Shot("Equipment-Confirmation", personnelRoot);
            var confirmScroll = Field<ScrollRect>(equipConfirmation, "_bodyScroll");
            confirmScroll.OnScroll(new PointerEventData(null) { scrollDelta = new Vector2(0, -1) });
            Check(confirmScroll.content.anchoredPosition.y > 0, "Equipment confirmation scrolls within the card");
            confirmScroll.verticalNormalizedPosition = 0; Shot("Equipment-Confirmation-Scrolled", personnelRoot);
            Field<Button>(equipConfirmation, "_cancel").onClick.Invoke();
            Check(roster.EquippedWeapon(1) == original, "Cancelling equipment change preserves the loadout");
            Field<GameObject>(personnel, "_window").SetActive(false);

            government.OpenBudget(); Shot("Budget-Tax", root);
            Check(Field<TextMeshProUGUI>(government, "_fundingBasis").text.Contains("税収"), "Budget states the percent basis");
            Field<Toggle>(government, "_useTreasury").isOn = true; Shot("Budget-Treasury", root);
            Check(Field<TextMeshProUGUI>(government, "_fundingBasis").text.Contains("税収＋国庫"), "Treasury toggle updates the basis description");
            government.OpenConstruction(); Shot("Construction-Upgrade", root);
            Call(government, "SetConstructionMode", false); Shot("Construction-Library", root);
            Set(government, "_building", 6); Call(government, "Refresh"); Shot("Construction-Observatory", root);
            Check(Field<TextMeshProUGUI>(government, "_constructionEffect").text.Contains("性格"), "Construction explains the agreed facility role");
            government.OpenIncome(); Shot("Report-Treasury", root);
            Check(Field<TextMeshProUGUI>(government, "_treasuryCaption").text == "国庫" && Field<TextMeshProUGUI>(government, "_treasuryText").text == "1,000", "Report header shows actual treasury");
            government.Close();
            var hud = Instance("CountryHud"); var expeditions = hud.GetComponentInChildren<CountryExpeditionPanel>(true);
            var mapData = Field<CountryWorldMapData>(expeditions, "_data"); var state = resources.GetOrCreateExpeditions(mapData, view.Data, roster.Today);
            Set(expeditions, "_state", state); Set(expeditions, "_roster", roster); Set(expeditions, "_date", date); Call(expeditions, "Awake");
            var expeditionView = Field<CountryRosterView>(expeditions, "_view"); expeditionView.Initialize(roster);
            var tokens = new CountryRosterRow[26]; var slots = new GameObject[26]; var memberRoot = Field<RectTransform>(expeditions, "_members");
            for (int i = 0; i < tokens.Length; i++)
            {
                tokens[i] = Object.Instantiate(Field<CountryRosterRow>(expeditions, "_tokenPrefab"), memberRoot); tokens[i].SetCaptionVisible(true);
                slots[i] = Object.Instantiate(Field<GameObject>(expeditions, "_emptySlotPrefab"), memberRoot);
            }
            Set(expeditions, "_tokens", tokens); Set(expeditions, "_emptySlots", slots);
            Field<CountryWorldMapView>(expeditions, "_map").Initialize(state, expeditions.SelectRegion);
            state.Changed += () => Call(expeditions, "StateChanged");
            var departure = Field<CountryRosterConfirm>(expeditions, "_confirmation"); Call(departure, "Awake");
            expeditions.Open();
            int adjacent = Enumerable.Range(0, mapData.Regions.Length).First(state.CanTarget);
            expeditions.SelectRegion(adjacent); expeditions.Draft.Toggle(0); Call(expeditions, "Refresh"); Shot("Map-Available", hud);
            int distant = Enumerable.Range(0, mapData.Regions.Length).First(i => state.Owner(i) != 0 && !state.CanTarget(i));
            expeditions.SelectRegion(distant); Shot("Map-Unavailable", hud);
            Check(!Field<CountryWorldRouteGraphic>(Field<CountryWorldMapView>(expeditions, "_map"), "_route").gameObject.activeSelf, "Unavailable destinations do not show a departure route");
            Check(Field<TextMeshProUGUI>(expeditions, "_targetNotice").text.Contains("遠征不可"), "Unavailable destinations state the reason");
            expeditions.SelectRegion(adjacent); Call(expeditions, "ConfirmDeparture"); Field<Button>(departure, "_confirm").onClick.Invoke();
            Shot("HUD-Departure", hud);
            Check(state.Active.Count == 1 && Field<TextMeshProUGUI>(expeditions, "_hudCount").transform.parent.gameObject.activeSelf, "Departure updates the HUD unit badge");
            Check(Field<TextMeshProUGUI>(expeditions, "_departureNotice").transform.parent.gameObject.activeSelf && !date.IsRunning, "Departure feedback is visible while the clock stays paused");
            Field<TextMeshProUGUI>(expeditions, "_departureNotice").transform.parent.gameObject.SetActive(false); Shot("HUD-Active", hud);
            state.AdvanceTo(state.Active[0].Return);
            Check(state.Active.Count == 0 && !Field<TextMeshProUGUI>(expeditions, "_hudCount").transform.parent.gameObject.activeSelf, "Returning clears the active unit badge");
            Field<TextMeshProUGUI>(expeditions, "_departureNotice").transform.parent.gameObject.SetActive(false);
            var settingsIcon = hud.GetComponentsInChildren<Image>(true).First(i => i.sprite != null && i.sprite.texture.name == "settings-v1");
            Check(settingsIcon.color == CountryGovernmentTheme.Ink, "Settings gear uses the ink foreground");
            hud.SetActive(false);
            government.OpenIncome();
            var report = Field<CountryExpeditionReportView>(government, "_expeditionReport");
            var returned = Object.Instantiate(Field<CountryExpeditionRow>(report, "_rowPrefab"), Field<RectTransform>(report, "_content"));
            Field<TextMeshProUGUI>(report, "_empty").gameObject.SetActive(false); returned.Bind(state, state.Returned[0], true, () => { });
            Check(Field<TextMeshProUGUI>(returned, "_status").color == CountryGovernmentTheme.Ink, "Return result uses the row ink foreground");
            Shot("Report-Return", root);
            Check(PlayDataManager.Instance == null && DateManager.Instance == null, "Preview does not create live singleton state");

            GameObject Instance(string name)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab"), scene);
                var instanceCanvas = instance.GetComponent<Canvas>();
                if (instanceCanvas != null) { instanceCanvas.renderMode = RenderMode.ScreenSpaceCamera; instanceCanvas.worldCamera = camera; instanceCanvas.planeDistance = 5; }
                else
                {
                    instance.transform.SetParent(canvasObject.transform, false);
                    var rect = (RectTransform)instance.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                }
                return instance;
            }
            void Shot(string name, GameObject scope)
            {
                Canvas.ForceUpdateCanvases();
                foreach (var scale in scope.GetComponentsInChildren<CountrySliderScale>()) Call(scale, "LateUpdate");
                camera.Render();
                var previous = RenderTexture.active; RenderTexture.active = target;
                var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); image.Apply();
                string path = $"{Output}/{name}-{size.x}x{size.y}";
                File.WriteAllBytes(path + ".png", image.EncodeToPNG()); Object.DestroyImmediate(image); RenderTexture.active = previous;
                File.WriteAllText(path + ".json", Newtonsoft.Json.JsonConvert.SerializeObject(scope.GetComponentsInChildren<TextMeshProUGUI>().Where(t => !string.IsNullOrWhiteSpace(t.text)).Select(t => new { path = AnimationUtility.CalculateTransformPath(t.transform, scope.transform), text = t.text, overflow = t.isTextOverflowing }), Newtonsoft.Json.Formatting.Indented));
            }
        }
        finally { RenderTexture.ReleaseTemporary(target); EditorSceneManager.ClosePreviewScene(scene); }
    }
}
