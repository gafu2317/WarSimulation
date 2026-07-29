using System.IO;
using UnityEngine;

public static class CombatDebugPaths
{
    private static string _cachedProjectRoot;

    public static string FindProjectRoot()
    {
        if (!string.IsNullOrEmpty(_cachedProjectRoot) && Directory.Exists(Path.Combine(_cachedProjectRoot, "Assets")))
            return _cachedProjectRoot;

        if (CombatAutoBattleConfigLoader.TryGetLastConfigPath(out string configPath))
        {
            string fromConfig = WalkForAssets(Path.GetDirectoryName(configPath));
            if (fromConfig != null)
            {
                _cachedProjectRoot = fromConfig;
                return fromConfig;
            }
        }

        string[] starts =
        {
            Directory.GetCurrentDirectory(),
            Application.dataPath,
            Directory.GetParent(Application.dataPath)?.FullName,
            Directory.GetParent(Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty)?.FullName,
        };

        for (int i = 0; i < starts.Length; i++)
        {
            string root = WalkForAssets(starts[i]);
            if (root == null) continue;
            _cachedProjectRoot = root;
            return root;
        }

        return null;
    }

    public static string GetLogsDirectory(string folderName)
    {
        string projectRoot = FindProjectRoot();
        if (!string.IsNullOrEmpty(projectRoot))
            return Path.Combine(projectRoot, "Logs", folderName);

        return Path.Combine(Application.persistentDataPath, folderName);
    }

    private static string WalkForAssets(string start)
    {
        if (string.IsNullOrEmpty(start)) return null;

        string current = Path.GetFullPath(start);
        for (int i = 0; i < 8; i++)
        {
            if (Directory.Exists(Path.Combine(current, "Assets")))
                return current;

            DirectoryInfo parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }

        return null;
    }
}
