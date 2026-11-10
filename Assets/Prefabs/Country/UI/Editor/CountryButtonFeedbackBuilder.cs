using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class CountryButtonFeedbackBuilder
{
    public static void Configure(UnityEngine.UI.Button button)
    {
        if (button.targetGraphic == null || button.targetGraphic.transform == button.transform) return;
        if (!button.TryGetComponent<CountryButtonFeedback>(out var feedback)) feedback = button.gameObject.AddComponent<CountryButtonFeedback>();
        var face = button.targetGraphic.rectTransform;
        var contents = button.name == "ModeButton"
            ? new[] { (RectTransform)button.transform.Find("Icon") }
            : button.GetComponent<CountryRosterRow>() != null
            ? new[] { "Portrait", "Name", "State" }.Select(name => (RectTransform)button.transform.Find(name)).ToArray()
            : button.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)
            .Where(graphic => graphic is TMP_Text || graphic is CountryFacilitySymbol || graphic.name == "Icon")
            .Select(graphic => graphic.rectTransform).ToArray();
        contents = contents.Where(content => content != face).ToArray();
        var data = new SerializedObject(feedback);
        if (button.name == "ModeButton")
        {
            data.FindProperty("_pressDepth").floatValue = 6;
            data.FindProperty("_hoverScale").floatValue = .04f;
        }
        data.FindProperty("_face").objectReferenceValue = face;
        data.FindProperty("_shadow").objectReferenceValue = (button.transform.Find("Shadow") ?? face.parent.Find("Shadow"))?.gameObject;
        data.FindProperty("_focus").objectReferenceValue = button.transform.Find("Focus")?.GetComponent<UnityEngine.UI.Graphic>();
        var items = data.FindProperty("_contents"); items.arraySize = contents.Length;
        for (int i = 0; i < contents.Length; i++) items.GetArrayElementAtIndex(i).objectReferenceValue = contents[i];
        data.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(feedback);
    }
}
