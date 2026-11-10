using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class CombatReleaseSceneProcessor : IProcessSceneWithReport
{
    public int callbackOrder => 0;

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report == null || (report.summary.options & BuildOptions.Development) != 0) return;
        // Releaseでは型が存在しないため、ビルドするシーンへ保存しない。
        foreach (var root in scene.GetRootGameObjects())
            foreach (var logger in root.GetComponentsInChildren<CombatBattleEventLogger>(true))
                Object.DestroyImmediate(logger);
    }
}
