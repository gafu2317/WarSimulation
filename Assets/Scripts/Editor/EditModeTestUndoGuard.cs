#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>
/// EditMode TestRunner restores the previously open scene, then undoes the test
/// undo group. Undo records still target the unloaded bootstrap scene and hit
/// Unity's native assert: targetScene != nullptr.
/// Skip that undo pass and clear the poisoned undo stack; scene restore already
/// discarded the bootstrap scene.
/// </summary>
[InitializeOnLoad]
static class EditModeTestUndoGuard
{
    private static TestRunnerApi s_api;

    static EditModeTestUndoGuard()
    {
        s_api = ScriptableObject.CreateInstance<TestRunnerApi>();
        s_api.hideFlags = HideFlags.HideAndDontSave;
        s_api.RegisterCallbacks(new Callbacks());
    }

    private sealed class Callbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }

        public void RunFinished(ITestResultAdaptor result)
        {
            DisablePendingTestUndo();
            Undo.ClearAll();
        }

        public void TestStarted(ITestAdaptor test) { }

        public void TestFinished(ITestResultAdaptor result) { }
    }

    private static void DisablePendingTestUndo()
    {
        try
        {
            Type holderType = Type.GetType(
                "UnityEditor.TestTools.TestRunner.TestRun.TestJobDataHolder, UnityEditor.TestRunner",
                throwOnError: false);
            if (holderType == null)
            {
                Debug.LogWarning("[EditModeTestUndoGuard] TestJobDataHolder type not found.");
                return;
            }

            PropertyInfo instanceProperty = holderType.GetProperty(
                "instance",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            object holder = instanceProperty?.GetValue(null);
            if (holder == null)
            {
                Debug.LogWarning("[EditModeTestUndoGuard] TestJobDataHolder.instance is null.");
                return;
            }

            FieldInfo testRunsField = holderType.GetField(
                "TestRuns",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (testRunsField?.GetValue(holder) is not IList testRuns)
            {
                Debug.LogWarning("[EditModeTestUndoGuard] TestRuns list not found.");
                return;
            }

            int patched = 0;
            foreach (object job in testRuns)
            {
                if (job == null) continue;
                FieldInfo undoGroupField = job.GetType().GetField(
                    "undoGroup",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (undoGroupField == null || undoGroupField.FieldType != typeof(int)) continue;
                undoGroupField.SetValue(job, -1);
                patched++;
            }

            if (patched == 0)
            {
                Debug.LogWarning("[EditModeTestUndoGuard] No running TestJobData.undoGroup was patched.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[EditModeTestUndoGuard] Failed to disable test undo: {ex.Message}");
        }
    }
}
#endif
