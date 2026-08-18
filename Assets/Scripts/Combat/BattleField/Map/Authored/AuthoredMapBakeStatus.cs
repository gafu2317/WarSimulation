namespace WarSimulation.Combat.Map
{
    public enum AuthoredMapBakeStageState
    {
        Current = 0,
        Missing,
        Stale,
        NotConfigured,
        Deferred,
        MissingSceneData,
    }

    /// <summary>現在のfingerprintと保存物を比較し、工程ごとのベイク状態を算出する。</summary>
    public readonly struct AuthoredMapBakeStatus
    {
        public AuthoredMapBakeStageState MapData { get; }
        public AuthoredMapBakeStageState NavMesh { get; }
        public AuthoredMapBakeStageState AssaultRoutes { get; }
        public AuthoredMapBakeStageState Preview { get; }
        public AuthoredMapBakeStageState Scene3D { get; }

        public bool AllCurrent =>
            MapData == AuthoredMapBakeStageState.Current &&
            NavMesh == AuthoredMapBakeStageState.Current &&
            AssaultRoutes == AuthoredMapBakeStageState.Current &&
            Preview == AuthoredMapBakeStageState.Current &&
            Scene3D == AuthoredMapBakeStageState.Current;

        private AuthoredMapBakeStatus(
            AuthoredMapBakeStageState mapData,
            AuthoredMapBakeStageState navMesh,
            AuthoredMapBakeStageState assaultRoutes,
            AuthoredMapBakeStageState preview,
            AuthoredMapBakeStageState scene3D)
        {
            MapData = mapData;
            NavMesh = navMesh;
            AssaultRoutes = assaultRoutes;
            Preview = preview;
            Scene3D = scene3D;
        }

        public static AuthoredMapBakeStatus Evaluate(
            AuthoredMapDefinition definition,
            MapSceneHost sceneHost)
        {
            if (definition == null)
            {
                return new AuthoredMapBakeStatus(
                    AuthoredMapBakeStageState.Missing,
                    AuthoredMapBakeStageState.Missing,
                    AuthoredMapBakeStageState.NotConfigured,
                    AuthoredMapBakeStageState.Missing,
                    AuthoredMapBakeStageState.Deferred);
            }

            int geometryFingerprint = definition.ComputeGeometryFingerprint();
            AuthoredMapBakeStageState mapData = definition.BakedMapData == null
                ? AuthoredMapBakeStageState.Missing
                : definition.BakedMapData.IsValidFor(geometryFingerprint)
                    ? AuthoredMapBakeStageState.Current
                    : AuthoredMapBakeStageState.Stale;
            AuthoredMapBakeStageState navMesh = definition.BakedNavMesh == null
                ? AuthoredMapBakeStageState.Missing
                : definition.NavMeshBakeFingerprint == geometryFingerprint
                    ? AuthoredMapBakeStageState.Current
                    : AuthoredMapBakeStageState.Stale;
            AuthoredMapBakeStageState assaultRoutes = EvaluateAssaultRoutes(definition);
            AuthoredMapBakeStageState preview = definition.BakedPreview == null
                ? AuthoredMapBakeStageState.Missing
                : definition.HasValidBakedPreview
                    ? AuthoredMapBakeStageState.Current
                    : AuthoredMapBakeStageState.Stale;
            AuthoredMapBakeStageState scene3D = EvaluateScene3D(
                definition,
                sceneHost,
                geometryFingerprint,
                mapData);

            return new AuthoredMapBakeStatus(mapData, navMesh, assaultRoutes, preview, scene3D);
        }

        private static AuthoredMapBakeStageState EvaluateAssaultRoutes(AuthoredMapDefinition definition)
        {
            if (definition.HasValidBakedAssaultRoutes) return AuthoredMapBakeStageState.Current;
            if (definition.AssaultRoutes == null || definition.AssaultRoutes.Count == 0)
                return AuthoredMapBakeStageState.NotConfigured;
            return definition.HasBakedAssaultRoutesData
                ? AuthoredMapBakeStageState.Stale
                : AuthoredMapBakeStageState.Missing;
        }

        private static AuthoredMapBakeStageState EvaluateScene3D(
            AuthoredMapDefinition definition,
            MapSceneHost sceneHost,
            int geometryFingerprint,
            AuthoredMapBakeStageState mapDataState)
        {
            if (mapDataState != AuthoredMapBakeStageState.Current)
                return AuthoredMapBakeStageState.Deferred;
            if (sceneHost == null) return AuthoredMapBakeStageState.MissingSceneData;
            if (!sceneHost.HasBakedRenderFingerprint ||
                sceneHost.BakedRenderFingerprint != geometryFingerprint)
                return AuthoredMapBakeStageState.Stale;

            MapData map = definition.BakedMapData.CreateRuntimeMap();
            return sceneHost.HasBakedRenderDataFor(map, geometryFingerprint)
                ? AuthoredMapBakeStageState.Current
                : AuthoredMapBakeStageState.MissingSceneData;
        }
    }
}
