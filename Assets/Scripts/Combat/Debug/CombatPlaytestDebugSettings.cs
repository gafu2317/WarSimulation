using System;
using UnityEngine;

/// <summary>
/// プレイテスト用デバッグ表示。既定は表示OFF。編成画面のトグル／詳細設定から切り替える。
/// </summary>
public static class CombatPlaytestDebugSettings
{
    public static bool ShowCharacterRoutes { get; private set; }
    public static bool ShowAssaultRoutes { get; private set; }
    public static bool ShowAiLabels { get; private set; }
    public static bool ShowVision { get; private set; }

    public static bool CharacterRoutesShowAlly { get; private set; } = true;
    public static bool CharacterRoutesShowEnemy { get; private set; } = true;

    public static CombatTeam AssaultAttackingTeam { get; private set; } = CombatTeam.Ally;
    public static bool AssaultAllowRiverCrossing { get; private set; }
    public static bool AssaultShowEndpointMarkers { get; private set; } = true;

    public static bool LabelShowObjective { get; private set; } = true;
    public static bool LabelShowWeapon { get; private set; } = true;
    public static bool LabelShowPersonality { get; private set; } = true;

    public static bool VisionShowLines { get; private set; } = true;
    public static bool VisionShowObstructionRays { get; private set; }
    public static bool VisionShowFieldOfView { get; private set; } = true;

    public static event Action Changed;

    public static void SetShowCharacterRoutes(bool value) => Set(ShowCharacterRoutes, v => ShowCharacterRoutes = v, value);
    public static void SetShowAssaultRoutes(bool value) => Set(ShowAssaultRoutes, v => ShowAssaultRoutes = v, value);
    public static void SetShowAiLabels(bool value) => Set(ShowAiLabels, v => ShowAiLabels = v, value);
    public static void SetShowVision(bool value) => Set(ShowVision, v => ShowVision = v, value);

    public static void SetCharacterRoutesShowAlly(bool value) => Set(CharacterRoutesShowAlly, v => CharacterRoutesShowAlly = v, value);
    public static void SetCharacterRoutesShowEnemy(bool value) => Set(CharacterRoutesShowEnemy, v => CharacterRoutesShowEnemy = v, value);

    public static void SetAssaultAttackingTeam(CombatTeam value)
    {
        if (AssaultAttackingTeam == value)
        {
            ApplyToScene();
            return;
        }

        AssaultAttackingTeam = value;
        ApplyToScene();
        Changed?.Invoke();
    }

    public static void SetAssaultAllowRiverCrossing(bool value) =>
        Set(AssaultAllowRiverCrossing, v => AssaultAllowRiverCrossing = v, value);

    public static void SetAssaultShowEndpointMarkers(bool value) =>
        Set(AssaultShowEndpointMarkers, v => AssaultShowEndpointMarkers = v, value);

    public static void SetLabelShowObjective(bool value) => Set(LabelShowObjective, v => LabelShowObjective = v, value);
    public static void SetLabelShowWeapon(bool value) => Set(LabelShowWeapon, v => LabelShowWeapon = v, value);
    public static void SetLabelShowPersonality(bool value) => Set(LabelShowPersonality, v => LabelShowPersonality = v, value);

    public static void SetVisionShowLines(bool value) => Set(VisionShowLines, v => VisionShowLines = v, value);
    public static void SetVisionShowObstructionRays(bool value) =>
        Set(VisionShowObstructionRays, v => VisionShowObstructionRays = v, value);
    public static void SetVisionShowFieldOfView(bool value) =>
        Set(VisionShowFieldOfView, v => VisionShowFieldOfView = v, value);

    public static void ResetCharacterRouteDetailsToDefault()
    {
        CharacterRoutesShowAlly = true;
        CharacterRoutesShowEnemy = true;
        ApplyToScene();
        Changed?.Invoke();
    }

    public static void ResetAssaultRouteDetailsToDefault()
    {
        AssaultAttackingTeam = CombatTeam.Ally;
        AssaultAllowRiverCrossing = false;
        AssaultShowEndpointMarkers = true;
        ApplyToScene();
        Changed?.Invoke();
    }

    public static void ResetLabelDetailsToDefault()
    {
        LabelShowObjective = true;
        LabelShowWeapon = true;
        LabelShowPersonality = true;
        ApplyToScene();
        Changed?.Invoke();
    }

    public static void ResetVisionDetailsToDefault()
    {
        VisionShowLines = true;
        VisionShowObstructionRays = false;
        VisionShowFieldOfView = true;
        ApplyToScene();
        Changed?.Invoke();
    }

    public static void ApplyToScene()
    {
        SetEnabled<CombatCharacterRouteDebugView>(ShowCharacterRoutes);
        SetEnabled<CombatStoneAssaultRouteDebugView>(ShowAssaultRoutes);
        SetEnabled<CombatAiWorldLabelDebugView>(ShowAiLabels);
        SetEnabled<CombatVisionDebugRayView>(ShowVision);

        ApplyDetails<CombatCharacterRouteDebugView>(view => view.ApplyPlaytestSettings());
        ApplyDetails<CombatStoneAssaultRouteDebugView>(view => view.ApplyPlaytestSettings());
        ApplyDetails<CombatAiWorldLabelDebugView>(view => view.ApplyPlaytestSettings());
        ApplyDetails<CombatVisionDebugRayView>(view => view.ApplyPlaytestSettings());

        SetEnabled<CombatTerrainInfoClickDebugger>(false);
        SetEnabled<CombatMagicStoneDebugInput>(false);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        SetEnabled<CombatBattleEventLogger>(false);
#endif
    }

    private static void Set(bool current, Action<bool> assign, bool value)
    {
        if (current == value)
        {
            ApplyToScene();
            return;
        }

        assign(value);
        ApplyToScene();
        Changed?.Invoke();
    }

    private static void SetEnabled<T>(bool enabled) where T : Behaviour
    {
        T[] behaviours = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include);
        for (int i = 0; i < behaviours.Length; i++)
        {
            T behaviour = behaviours[i];
            if (behaviour != null) behaviour.enabled = enabled;
        }
    }

    private static void ApplyDetails<T>(Action<T> apply) where T : Behaviour
    {
        T[] behaviours = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include);
        for (int i = 0; i < behaviours.Length; i++)
        {
            T behaviour = behaviours[i];
            if (behaviour != null) apply(behaviour);
        }
    }
}
