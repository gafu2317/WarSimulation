using UnityEditor;

[CustomEditor(typeof(CombatDebugBehaviour), true)]
public sealed class CombatDebugBehaviourEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var debugBehaviour = (CombatDebugBehaviour)target;
        EditorGUILayout.HelpBox(debugBehaviour.InspectorDescription, MessageType.Info);
        EditorGUILayout.Space();
        DrawDefaultInspector();
    }
}
