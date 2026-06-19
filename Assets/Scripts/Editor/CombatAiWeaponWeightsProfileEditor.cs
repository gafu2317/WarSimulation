#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CombatAiWeaponWeightsProfile))]
public sealed class CombatAiWeaponWeightsProfileEditor : Editor
{
    private static readonly string[] WeaponPropertyNames =
    {
        "_sword",
        "_shield",
        "_wand",
        "_grimoire",
        "_bible",
        "_rosary",
        "_unarmed",
    };

    private static readonly string[] WeaponLabels =
    {
        "Sword",
        "Shield",
        "Wand",
        "Grimoire",
        "Bible",
        "Rosary",
        "Unarmed",
    };

    private static readonly string[] ObjectiveNames =
    {
        "AttackEnemy",
        "DefendOwnStone",
        "SupportAlly",
        "DestroyEnemyStone",
        "Search",
        "Retreat",
    };

    private static readonly string[] MoveNames =
    {
        CombatAiMoveCode.AdvanceEnemyStone,
        CombatAiMoveCode.ReturnOwnStone,
        CombatAiMoveCode.PursueEnemy,
        CombatAiMoveCode.SupportAlly,
        CombatAiMoveCode.TakeHighGround,
        CombatAiMoveCode.MoveForest,
        CombatAiMoveCode.SearchLastKnown,
        CombatAiMoveCode.HoldPosition,
    };

    private static readonly string[] SkillNames =
    {
        "Damage",
        "Protect",
        "Heal",
        "Buff",
        "Debuff",
        "Stealth",
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawScriptField();
        EditorGUILayout.Space(4f);

        for (int i = 0; i < WeaponPropertyNames.Length; i++)
        {
            DrawWeaponSection(WeaponPropertyNames[i], WeaponLabels[i]);
        }

        EditorGUILayout.Space(8f);
        if (GUILayout.Button("Apply Current Defaults", GUILayout.Height(28f)))
        {
            foreach (Object targetObject in targets)
            {
                var profile = targetObject as CombatAiWeaponWeightsProfile;
                if (profile == null) continue;

                Undo.RecordObject(profile, "Apply AI Weapon Weight Defaults");
                profile.ApplyCurrentDefaults();
                EditorUtility.SetDirty(profile);
            }

            serializedObject.Update();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawScriptField()
    {
        using (new EditorGUI.DisabledScope(true))
        {
            MonoScript script = MonoScript.FromScriptableObject((CombatAiWeaponWeightsProfile)target);
            EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), allowSceneObjects: false);
        }
    }

    private void DrawWeaponSection(string propertyName, string label)
    {
        SerializedProperty weaponProperty = serializedObject.FindProperty(propertyName);
        if (weaponProperty == null)
        {
            return;
        }

        weaponProperty.isExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(weaponProperty.isExpanded, label);
        if (weaponProperty.isExpanded)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawGroup(weaponProperty.FindPropertyRelative("_objectives"), "Objectives", ObjectiveNames);
            EditorGUILayout.Space(2f);
            DrawGroup(weaponProperty.FindPropertyRelative("_moves"), "Moves", MoveNames);
            EditorGUILayout.Space(2f);
            DrawGroup(weaponProperty.FindPropertyRelative("_skills"), "Skills", SkillNames);
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(2f);
    }

    private static void DrawGroup(SerializedProperty groupProperty, string label, string[] fieldNames)
    {
        if (groupProperty == null)
        {
            return;
        }

        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        using (new EditorGUI.IndentLevelScope())
        {
            for (int i = 0; i < fieldNames.Length; i++)
            {
                SerializedProperty valueProperty = groupProperty.FindPropertyRelative(fieldNames[i]);
                if (valueProperty != null)
                {
                    EditorGUILayout.PropertyField(valueProperty);
                }
            }
        }
    }
}
#endif
