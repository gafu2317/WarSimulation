#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WarSimulation.Combat.Map.EditorOnly
{
    /// <summary>
    /// マップ設定／スタンプ SO の Inspector を日本語ラベルで描画する。
    /// </summary>
    public static class MapJapaneseInspectorDrawer
    {
        private static readonly Dictionary<string, string> Labels = new()
        {
            // StampShape
            ["_displayName"] = "表示名",

            // MapConfig
            ["_worldSize"] = "マップ一辺（m）",
            ["_cellsPerSide"] = "一辺のセル数",
            ["_baseHeight"] = "基準高度",
            ["_riverShape"] = "既定の川断面",
            ["_flatRiverMeanderAmplitude"] = "川の蛇行幅（m）",
            ["_flatRiverMeanderFrequency"] = "川の蛇行周波数",
            ["_bridgeLengthExtraMargin"] = "橋の長さ余白（m）",
            ["_bridgeWidth"] = "橋の幅（m）",
            ["_bridgeThickness"] = "橋の厚み（m）",
            ["_bridgeHeightAboveWater"] = "水面からの橋高さ（m）",
            ["_bridgeFeatureExclusionMargin"] = "橋まわり除外余白（m）",
            ["_scatterTreeCount"] = "散布する木の本数",
            ["_scatterTreeMinDistance"] = "木の最小間隔（m）",
            ["_scatterTreePlacementMargin"] = "木の配置マージン（m）",
            ["_rockCount"] = "岩の個数",
            ["_rockMinDistance"] = "岩の最小間隔（m）",
            ["_rockPlacementMargin"] = "岩の配置マージン（m）",
            ["_rockTopHeightExclusionRatio"] = "高所の岩除外比率",
            ["_mainStonesPerSide"] = "陣営あたりのメイン魔石数",

            // HeightStampShape
            ["_kind"] = "形状",
            ["_radius"] = "半径（m）",
            ["_peakDelta"] = "高度差",
            ["_ridgeLength"] = "尾根の長さ（m）",
            ["_flatTopRatio"] = "平頂の比率",
            ["_noiseAmplitude"] = "輪郭ノイズ強度",
            ["_noiseFrequency"] = "輪郭ノイズ周波数",
            ["_cliffArcDeg"] = "断崖の中心角（度）",
            ["_cliffDirectionDeg"] = "断崖の向き（度）",
            ["_cliffSkirtRatio"] = "断崖スカート比率",
            ["_cliffCutOffsetRatio"] = "断崖カットの外側寄せ",
            ["_cliffBlendDeg"] = "断崖ぼかし角（未使用）",
            ["_blend"] = "合成モード",

            // LakeStampShape
            ["_depthMeters"] = "深さ（m）",
            ["_waterSurfaceRatio"] = "水面の高さ比率",

            // GroundPatchStampShape
            ["_state"] = "地面状態",
            ["_overrideExistingState"] = "既存状態を上書き",
            ["_maxHeight"] = "配置できる最大高度（m）",

            // ForestClusterStampShape
            ["_treeCount"] = "木の本数",
            ["_treeMinDistance"] = "木の最小間隔（m）",
            ["_maxAttemptsPerTree"] = "1本あたりの試行上限",

            // RiverShape
            ["_widthMeters"] = "川幅（m）",
            ["_waterTagRatio"] = "Waterタグ半径比率",
        };

        public static void Draw(SerializedObject serializedObject)
        {
            if (serializedObject == null) return;

            serializedObject.Update();
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyPath == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(iterator, true);
                    continue;
                }

                string label = Labels.TryGetValue(iterator.name, out string japanese)
                    ? japanese
                    : iterator.displayName;
                EditorGUILayout.PropertyField(
                    iterator,
                    new GUIContent(label, iterator.tooltip),
                    includeChildren: true);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }

    public abstract class MapJapaneseEditorBase : Editor
    {
        public override void OnInspectorGUI()
        {
            MapJapaneseInspectorDrawer.Draw(serializedObject);
        }
    }

    [CustomEditor(typeof(MapConfig))]
    public sealed class MapConfigEditor : MapJapaneseEditorBase
    {
    }

    [CustomEditor(typeof(HeightStampShape))]
    public sealed class HeightStampShapeEditor : MapJapaneseEditorBase
    {
    }

    [CustomEditor(typeof(LakeStampShape))]
    public sealed class LakeStampShapeEditor : MapJapaneseEditorBase
    {
    }

    [CustomEditor(typeof(GroundPatchStampShape))]
    public sealed class GroundPatchStampShapeEditor : MapJapaneseEditorBase
    {
    }

    [CustomEditor(typeof(ForestClusterStampShape))]
    public sealed class ForestClusterStampShapeEditor : MapJapaneseEditorBase
    {
    }

    [CustomEditor(typeof(RiverShape))]
    public sealed class RiverShapeEditor : MapJapaneseEditorBase
    {
    }
}
#endif
