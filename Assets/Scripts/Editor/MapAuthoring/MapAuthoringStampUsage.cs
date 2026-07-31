#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WarSimulation.Combat.Map.EditorOnly
{
    /// <summary>
    /// スタンプ SO が AuthoredMapDefinition から参照されているかを調べる。
    /// </summary>
    public static class MapAuthoringStampUsage
    {
        private const string AuthoredFolder = "Assets/Data/Map/Map/Authored";

        /// <returns>参照しているマップがあれば true。users にマップ名を入れる。</returns>
        public static bool TryFindUsers(Object stamp, out List<string> users)
        {
            users = new List<string>();
            if (stamp == null) return false;

            string[] guids = AssetDatabase.FindAssets(
                $"t:{nameof(AuthoredMapDefinition)}",
                new[] { AuthoredFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                AuthoredMapDefinition map = AssetDatabase.LoadAssetAtPath<AuthoredMapDefinition>(path);
                if (map == null || !IsReferencedBy(map, stamp)) continue;
                users.Add(string.IsNullOrEmpty(map.name) ? path : map.name);
            }

            return users.Count > 0;
        }

        private static bool IsReferencedBy(AuthoredMapDefinition map, Object stamp)
        {
            if (stamp is HeightStampShape height)
            {
                for (int i = 0; i < map.Mountains.Count; i++)
                {
                    if (map.Mountains[i]?.Shape == height) return true;
                }

                return false;
            }

            if (stamp is LakeStampShape lake)
            {
                for (int i = 0; i < map.Lakes.Count; i++)
                {
                    if (map.Lakes[i]?.Shape == lake) return true;
                }

                return false;
            }

            if (stamp is GroundPatchStampShape ground)
            {
                for (int i = 0; i < map.GroundPatches.Count; i++)
                {
                    if (map.GroundPatches[i]?.Shape == ground) return true;
                }

                return false;
            }

            if (stamp is ForestClusterStampShape forest)
            {
                for (int i = 0; i < map.Forests.Count; i++)
                {
                    if (map.Forests[i]?.Shape == forest) return true;
                }

                return false;
            }

            if (stamp is RiverShape river)
            {
                for (int i = 0; i < map.Rivers.Count; i++)
                {
                    if (map.Rivers[i]?.Shape == river) return true;
                }
            }

            return false;
        }
    }
}
#endif
