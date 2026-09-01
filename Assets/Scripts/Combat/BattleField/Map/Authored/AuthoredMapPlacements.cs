using System;
using System.Collections.Generic;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    [Serializable]
    public sealed class AuthoredMountainPlacement
    {
        public HeightStampShape Shape;
        public MountainKind Kind = MountainKind.Small;
        public Vector2 Center;
        public float RotationDeg;
        public Vector2 Scale = Vector2.one;

        public StampPlacement ToStampPlacement() =>
            new StampPlacement(Center, RotationDeg * Mathf.Deg2Rad, Scale == default ? Vector2.one : Scale);
    }

    [Serializable]
    public sealed class AuthoredLakePlacement
    {
        public LakeStampShape Shape;
        public Vector2 Center;
        public float RotationDeg;
        public Vector2 Scale = Vector2.one;
        public bool IsFrozen;

        public StampPlacement ToStampPlacement() =>
            new StampPlacement(Center, RotationDeg * Mathf.Deg2Rad, Scale == default ? Vector2.one : Scale);
    }

    [Serializable]
    public sealed class AuthoredGroundPatchPlacement
    {
        public GroundPatchStampShape Shape;
        public Vector2 Center;
        public float RotationDeg;
        public Vector2 Scale = Vector2.one;

        public StampPlacement ToStampPlacement() =>
            new StampPlacement(Center, RotationDeg * Mathf.Deg2Rad, Scale == default ? Vector2.one : Scale);
    }

    [Serializable]
    public sealed class AuthoredForestPlacement
    {
        public ForestClusterStampShape Shape;
        public Vector2 Center;
        public float RotationDeg;
        public Vector2 Scale = Vector2.one;
        public List<AuthoredPointFeaturePlacement> Trees = new();
        public int TreeLayoutFingerprint;

        public StampPlacement ToStampPlacement() =>
            new StampPlacement(Center, RotationDeg * Mathf.Deg2Rad, Scale == default ? Vector2.one : Scale);
    }

    [Serializable]
    public sealed class AuthoredRiverPlacement
    {
        public RiverShape Shape;
        /// <summary>
        /// [0]=始点, [1]=二次ベジェ制御点, [2]=終点。旧データで2点のみの場合は制御点を弦の中点として扱う。
        /// </summary>
        public List<Vector2> ControlPoints = new();

        public bool TryGetEndpoints(out Vector2 start, out Vector2 end)
        {
            start = default;
            end = default;
            if (ControlPoints == null || ControlPoints.Count < 2) return false;
            start = ControlPoints[0];
            end = ControlPoints[ControlPoints.Count - 1];
            return true;
        }

        public bool TryGetBezier(out Vector2 start, out Vector2 control, out Vector2 end)
        {
            start = default;
            control = default;
            end = default;
            if (!TryGetEndpoints(out start, out end)) return false;
            control = ControlPoints.Count >= 3
                ? ControlPoints[1]
                : (start + end) * 0.5f;
            return true;
        }

        public void SetBezier(Vector2 start, Vector2 control, Vector2 end)
        {
            if (ControlPoints == null)
                ControlPoints = new List<Vector2>(3);
            ControlPoints.Clear();
            ControlPoints.Add(start);
            ControlPoints.Add(control);
            ControlPoints.Add(end);
        }

        public void SetEndpoints(Vector2 start, Vector2 end)
        {
            Vector2 control = ControlPoints != null && ControlPoints.Count >= 3
                ? ControlPoints[1]
                : (start + end) * 0.5f;
            SetBezier(start, control, end);
        }
    }

    [Serializable]
    public sealed class AuthoredBridgePlacement
    {
        public Vector2 Center;
        public float RotationDeg;
        public Vector3 Scale;
    }

    [Serializable]
    public sealed class AuthoredPointFeaturePlacement
    {
        public Vector2 Center;
        public float RotationDeg;
    }

    [Serializable]
    public sealed class AuthoredMagicStonePlacement
    {
        public FeatureType Type = FeatureType.OwnMainStone;
        public Vector2 Center;
    }
}
