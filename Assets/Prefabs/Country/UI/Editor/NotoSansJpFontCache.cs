using TMPro;
using UnityEditor;

[InitializeOnLoad]
internal static class NotoSansJpFontCache
{
    private const string FontPath = "Assets/Fonts/Noto_Sans_JP/static/NotoSansJP-Regular SDF.asset";

    static NotoSansJpFontCache() => EditorApplication.playModeStateChanged += RefreshBeforePlay;

    private static void RefreshBeforePlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode) return;

        // Reimport can replace the atlas while its character lookup survives with domain reload disabled.
        AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath)?.ReadFontAssetDefinition();
    }
}
