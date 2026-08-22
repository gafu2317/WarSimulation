using System.Collections.Generic;

namespace WarSimulation.Combat.Map
{
    public enum CombatMapUnavailableReason
    {
        None,
        NoMapSelected,
        MissingSharedConfig,
        InvalidAuthoredDefinition,
        MissingOwnMainStone,
        MissingEnemyMainStone,
        StonePairCountMismatch,
        MissingBakedMapData,
        StaleBakedMapData,
        MissingInitialSpawnPositions,
        MissingBakedNavMesh,
        StaleBakedNavMesh,
        MissingBakedRuntimeScene,
        MissingAssaultRoutes,
        StaleAssaultRoutes,
        MissingPreview,
        StalePreview,
    }

    public readonly struct CombatMapAvailability
    {
        public bool CanStartBattle => Reason == CombatMapUnavailableReason.None;
        public CombatMapUnavailableReason Reason { get; }
        public string Message { get; }

        private CombatMapAvailability(CombatMapUnavailableReason reason, string message)
        {
            Reason = reason;
            Message = message;
        }

        public static CombatMapAvailability Evaluate(
            AuthoredMapDefinition definition,
            bool stonePositionsReversed)
        {
            if (definition == null)
                return Unavailable(CombatMapUnavailableReason.NoMapSelected, "マップが登録されていません");
            if (definition.SharedConfig == null)
                return Unavailable(CombatMapUnavailableReason.MissingSharedConfig, "共通マップ設定がありません");

            List<AuthoredMapValidationIssue> issues = AuthoredMapValidator.Validate(definition);
            if (AuthoredMapValidator.HasErrors(issues))
                return Unavailable(CombatMapUnavailableReason.InvalidAuthoredDefinition, "マップ定義にエラーがあります");

            CountMainStones(definition, out int ownCount, out int enemyCount);
            int requiredCount = System.Math.Max(1, definition.SharedConfig.MainStonesPerSide);
            if (ownCount < requiredCount)
                return Unavailable(CombatMapUnavailableReason.MissingOwnMainStone, "自軍の主魔石が不足しています");
            if (enemyCount < requiredCount)
                return Unavailable(CombatMapUnavailableReason.MissingEnemyMainStone, "敵軍の主魔石が不足しています");
            if (stonePositionsReversed && ownCount != enemyCount)
            {
                return Unavailable(
                    CombatMapUnavailableReason.StonePairCountMismatch,
                    "魔石反転に必要な自軍・敵軍の数が一致しません");
            }

            int fingerprint = definition.ComputeGeometryFingerprint();
            if (definition.BakedMapData == null)
                return Unavailable(CombatMapUnavailableReason.MissingBakedMapData, "MapDataが未ベイクです");
            if (!definition.BakedMapData.IsValidFor(fingerprint))
                return Unavailable(CombatMapUnavailableReason.StaleBakedMapData, "マップ変更後にMapDataが再ベイクされていません");
            if (!definition.HasValidBakedInitialSpawnPositions)
                return Unavailable(
                    CombatMapUnavailableReason.MissingInitialSpawnPositions,
                    "初期配置データの再ベイクが必要です");
            if (definition.BakedNavMesh == null)
                return Unavailable(CombatMapUnavailableReason.MissingBakedNavMesh, "NavMeshが未ベイクです");
            if (definition.NavMeshBakeFingerprint != fingerprint)
                return Unavailable(CombatMapUnavailableReason.StaleBakedNavMesh, "マップ変更後にNavMeshが再ベイクされていません");
            if ((definition.AssaultRoutes == null || definition.AssaultRoutes.Count == 0) &&
                !definition.HasValidLegacyBakedAssaultRoutes)
                return Unavailable(CombatMapUnavailableReason.MissingAssaultRoutes, "侵攻ルートが設定されていません");
            if (!definition.HasBakedAssaultRoutesData)
                return Unavailable(CombatMapUnavailableReason.MissingAssaultRoutes, "侵攻ルートが未ベイクです");
            if (!definition.HasValidBakedAssaultRoutes)
                return Unavailable(CombatMapUnavailableReason.StaleAssaultRoutes, "マップ変更後に侵攻ルートが再ベイクされていません");
            if (definition.BakedPreview == null)
                return Unavailable(CombatMapUnavailableReason.MissingPreview, "プレビューが未生成です");
            int previewFingerprint = definition.AssaultRoutes != null && definition.AssaultRoutes.Count > 0
                ? definition.ComputeAssaultRouteFingerprint()
                : fingerprint;
            if (definition.PreviewBakeFingerprint != previewFingerprint)
                return Unavailable(CombatMapUnavailableReason.StalePreview, "マップ変更後にプレビューが再生成されていません");
            if (!definition.HasValidBakedRuntimeScene)
                return Unavailable(
                    CombatMapUnavailableReason.MissingBakedRuntimeScene,
                    "ランタイム用マップSceneの再ベイクが必要です");

            return new CombatMapAvailability(CombatMapUnavailableReason.None, string.Empty);
        }

        private static CombatMapAvailability Unavailable(CombatMapUnavailableReason reason, string message) =>
            new CombatMapAvailability(reason, message);

        private static void CountMainStones(
            AuthoredMapDefinition definition,
            out int ownCount,
            out int enemyCount)
        {
            ownCount = 0;
            enemyCount = 0;
            List<AuthoredMagicStonePlacement> stones = definition.MagicStones;
            if (stones == null) return;

            for (int i = 0; i < stones.Count; i++)
            {
                AuthoredMagicStonePlacement stone = stones[i];
                if (stone == null) continue;
                if (stone.Type == FeatureType.OwnMainStone) ownCount++;
                if (stone.Type == FeatureType.EnemyMainStone) enemyCount++;
            }
        }
    }
}
