#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CombatBattleFlow))]
public sealed class CombatBattleFlowEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        CombatBattleFlow battleFlow = (CombatBattleFlow)target;
        EditorGUILayout.LabelField("Battle State", battleFlow.State.ToString());

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Start Battle On Current Map", GUILayout.Height(28f)))
            {
                battleFlow.StartBattleOnCurrentMap();
                EditorUtility.SetDirty(battleFlow);
            }

            if (GUILayout.Button("Restart Battle On Current Map", GUILayout.Height(28f)))
            {
                battleFlow.RestartBattleOnCurrentMap();
                EditorUtility.SetDirty(battleFlow);
            }
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Play Mode 中のみ実行できます。", MessageType.Info);
        }
    }
}
#endif
