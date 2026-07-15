using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CombatCharacterVisibilityDiagnostic
{
    [MenuItem("Tools/War Simulation/Debug/Inspect Character Visibility")]
    public static void Inspect()
    {
        Scene scene = SceneManager.GetActiveScene();
        var characters = FindAllInScene<Character>(scene);
        var systems = FindAllInScene<CombatCharacterSystem>(scene);
        var report = new StringBuilder();

        report.AppendLine($"[キャラクター表示診断] シーン={scene.path}, 読み込み済み={scene.isLoaded}, 変更あり={scene.isDirty}");
        report.AppendLine($"キャラクター数={characters.Count}, 管理システム数={systems.Count}");

        for (int i = 0; i < systems.Count; i++)
        {
            CombatCharacterSystem system = systems[i];
            report.AppendLine(
                $"管理システム[{i}] {GetPath(system.transform)}: " +
                $"味方={system.AllyCharacters.Count}, 敵={system.EnemyCharacters.Count}");
        }

        for (int i = 0; i < characters.Count; i++)
        {
            Character character = characters[i];
            SpriteRenderer[] renderers = character.GetComponentsInChildren<SpriteRenderer>(true);
            int enabled = 0;
            int visibleHierarchy = 0;
            int assignedSprites = 0;
            int transparent = 0;

            for (int j = 0; j < renderers.Length; j++)
            {
                SpriteRenderer renderer = renderers[j];
                if (renderer.enabled) enabled++;
                if (renderer.gameObject.activeInHierarchy) visibleHierarchy++;
                if (renderer.sprite != null) assignedSprites++;
                if (renderer.color.a <= 0.001f) transparent++;
            }

            Transform spriteRoot = character.transform.Find("SpriteRoot");
            report.AppendLine(
                $"キャラクター[{i}] {GetPath(character.transform)}: " +
                $"自身有効={character.gameObject.activeSelf}, 階層有効={character.gameObject.activeInHierarchy}, " +
                $"座標={character.transform.position}, SpriteRoot={Describe(spriteRoot)}, " +
                $"描画部品={renderers.Length}, 有効={enabled}, 階層表示={visibleHierarchy}, " +
                $"画像あり={assignedSprites}, 完全透明={transparent}, " +
                $"味方一覧={Contains(systems, character, true)}, 敵一覧={Contains(systems, character, false)}");
        }

        Debug.Log(report.ToString());
    }

    private static List<T> FindAllInScene<T>(Scene scene) where T : Component
    {
        var found = new List<T>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            found.AddRange(roots[i].GetComponentsInChildren<T>(true));
        }
        return found;
    }

    private static bool Contains(
        IReadOnlyList<CombatCharacterSystem> systems,
        Character character,
        bool allies)
    {
        for (int i = 0; i < systems.Count; i++)
        {
            List<Character> participants = allies
                ? systems[i].AllyCharacters
                : systems[i].EnemyCharacters;
            if (participants.Contains(character)) return true;
        }
        return false;
    }

    private static string Describe(Transform target)
    {
        return target == null
            ? "なし"
            : $"自身有効:{target.gameObject.activeSelf}/階層有効:{target.gameObject.activeInHierarchy}";
    }

    private static string GetPath(Transform target)
    {
        var names = new Stack<string>();
        for (Transform current = target; current != null; current = current.parent)
        {
            names.Push(current.name);
        }
        return string.Join("/", names);
    }
}
