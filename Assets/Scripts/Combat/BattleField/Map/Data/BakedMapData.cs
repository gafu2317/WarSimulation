using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

namespace WarSimulation.Combat.Map
{
    [CreateAssetMenu(menuName = "WarSim/Map/Baked Map Data", fileName = "BakedMapData")]
    public sealed class BakedMapData : ScriptableObject
    {
        private static readonly ProfilerMarker CreateRuntimeMapMarker =
            new("CombatLoading.CreateRuntimeMap");

        [Serializable]
        private struct RiverRecord
        {
            public Vector2Int[] Cells;
            public float WidthMeters;
            public float DepthMeters;
            public float WaterTagRatio;
        }

        [Serializable]
        private struct LakeRecord
        {
            public Vector2 Center;
            public float Radius;
            public float WaterY;
            public bool IsFrozen;
            public float WaterTaggedRadius;
            public float NoiseAmplitude;
            public float NoiseFrequency;
        }

        [Serializable]
        private struct MountainRecord
        {
            public MountainKind Kind;
            public Vector2 Center;
            public float Extent;
            public Vector2 Scale;
            public float RotationRad;
            public HeightStampShape Shape;
        }

        [Serializable]
        private struct ForestRecord
        {
            public Vector2 Center;
            public float Radius;
            public float NoiseAmplitude;
            public float NoiseFrequency;
        }

        [Serializable]
        private struct FeatureRecord
        {
            public FeatureType Type;
            public Vector3 WorldPosition;
            public Quaternion Rotation;
            public Vector3 Scale;
        }

        [Serializable]
        private struct AssaultRouteRecord
        {
            public string RouteId;
            public string DisplayName;
            public Vector3[] Corners;
        }

        [SerializeField] private int _width;
        [SerializeField] private int _height;
        [SerializeField] private float _cellSize;
        [SerializeField] private int _seed;
        [SerializeField] private float _bridgeFeatureExclusionMargin;
        [SerializeField] private int _bakeFingerprint;
        [SerializeField] private float[] _heights;
        [SerializeField] private GroundState[] _groundStates;
        [SerializeField] private bool[] _cliffFaces;
        [SerializeField] private string[] _biomeIds;
        [SerializeField] private FeatureRecord[] _features;
        [SerializeField] private RiverRecord[] _rivers;
        [SerializeField] private LakeRecord[] _lakes;
        [SerializeField] private MountainRecord[] _mountains;
        [SerializeField] private ForestRecord[] _forests;
        [SerializeField] private int _assaultRouteFingerprint;
        [SerializeField] private AssaultRouteRecord[] _assaultRoutes;
        [SerializeField] private bool _assaultRoutesValidated;
        [SerializeField] private int _initialSpawnPositionFingerprint;
        [SerializeField] private Vector3[] _ownMainSpawnPositions;
        [SerializeField] private Vector3[] _enemyMainSpawnPositions;

        public int BakeFingerprint => _bakeFingerprint;
        public int AssaultRouteFingerprint => _assaultRouteFingerprint;
        public bool HasAssaultRoutesData => _assaultRoutes != null && _assaultRoutes.Length > 0;
        public bool HasInitialSpawnPositions =>
            _ownMainSpawnPositions != null && _ownMainSpawnPositions.Length > 0 &&
            _enemyMainSpawnPositions != null && _enemyMainSpawnPositions.Length > 0;

        public bool HasValidAssaultRoutes(int fingerprint) =>
            _assaultRoutesValidated && _assaultRouteFingerprint == fingerprint && HasAssaultRoutesData;

        public bool HasValidInitialSpawnPositions(int fingerprint) =>
            _initialSpawnPositionFingerprint == fingerprint && HasInitialSpawnPositions;

        public bool TryGetInitialSpawnPositions(
            FeatureType anchorType,
            int fingerprint,
            out IReadOnlyList<Vector3> positions)
        {
            positions = Array.Empty<Vector3>();
            if (!HasValidInitialSpawnPositions(fingerprint)) return false;
            positions = anchorType == FeatureType.OwnMainStone
                ? _ownMainSpawnPositions
                : anchorType == FeatureType.EnemyMainStone
                    ? _enemyMainSpawnPositions
                    : Array.Empty<Vector3>();
            return positions.Count > 0;
        }

        public bool IsValidFor(int fingerprint)
        {
            if (_bakeFingerprint != fingerprint || _width <= 0 || _height <= 0 || _cellSize <= 0f)
                return false;

            if (_width > int.MaxValue / _height)
                return false;

            int cellCount = _width * _height;
            return _heights != null && _heights.Length == cellCount &&
                _groundStates != null && _groundStates.Length == cellCount &&
                _cliffFaces != null && _cliffFaces.Length == cellCount;
        }

        public MapData CreateRuntimeMap()
        {
            using var _ = CreateRuntimeMapMarker.Auto();
            if (!IsStructurallyValid())
                throw new InvalidOperationException($"{nameof(BakedMapData)} '{name}' is incomplete.");

            var height = new HeightMap(_width, _height, _cellSize);
            var ground = new GroundStateGrid(_width, _height, _cellSize);
            for (int z = 0; z < _height; z++)
            {
                for (int x = 0; x < _width; x++)
                {
                    int index = z * _width + x;
                    height.SetHeight(x, z, _heights[index]);
                    ground.SetCell(x, z, _groundStates[index]);
                    if (_cliffFaces[index]) height.CliffFaces.MarkCliff(x, z);
                }
            }

            var map = new MapData(height, ground, _seed)
            {
                BridgeFeatureExclusionMargin = _bridgeFeatureExclusionMargin,
            };

            if (_biomeIds != null && _biomeIds.Length == _width * _height)
            {
                for (int z = 0; z < _height; z++)
                {
                    for (int x = 0; x < _width; x++)
                    {
                        string biomeId = _biomeIds[z * _width + x];
                        if (!string.IsNullOrEmpty(biomeId)) map.SetBiomeId(x, z, biomeId);
                    }
                }
            }

            AddFeatures(map);
            AddRivers(map);
            AddLakes(map);
            AddMountains(map);
            AddForests(map);
            AddAssaultRoutes(map);
            return map;
        }

        public void Capture(MapData map, int fingerprint)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));

            HeightMap height = map.Height;
            GroundStateGrid ground = map.GroundStates;
            if (height.Width != ground.Width || height.Height != ground.Height)
                throw new InvalidOperationException("MapData height and ground dimensions do not match.");

            _width = height.Width;
            _height = height.Height;
            _cellSize = height.CellSize;
            _seed = map.Seed;
            _bridgeFeatureExclusionMargin = map.BridgeFeatureExclusionMargin;
            _bakeFingerprint = fingerprint;

            int cellCount = _width * _height;
            _heights = new float[cellCount];
            _groundStates = new GroundState[cellCount];
            _cliffFaces = new bool[cellCount];

            bool hasBiome = false;
            for (int z = 0; z < _height; z++)
            {
                for (int x = 0; x < _width; x++)
                {
                    int index = z * _width + x;
                    _heights[index] = height.GetHeight(x, z);
                    _groundStates[index] = ground.GetCell(x, z);
                    _cliffFaces[index] = height.IsCliffFaceCell(x, z);
                    hasBiome |= !string.IsNullOrEmpty(map.GetBiomeId(x, z));
                }
            }

            _biomeIds = hasBiome ? new string[cellCount] : null;
            if (_biomeIds != null)
            {
                for (int z = 0; z < _height; z++)
                {
                    for (int x = 0; x < _width; x++)
                        _biomeIds[z * _width + x] = map.GetBiomeId(x, z);
                }
            }

            CaptureFeatures(map.Features);
            CaptureRivers(map.Rivers);
            CaptureLakes(map.Lakes);
            CaptureMountains(map.Mountains);
            CaptureForests(map.ForestRegions);
            CaptureInitialSpawnPositions(map, fingerprint);
        }

        public void CaptureInitialSpawnPositions(MapData map, int fingerprint)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            _ownMainSpawnPositions = InitialSpawnPositionBaker.Build(map, FeatureType.OwnMainStone);
            _enemyMainSpawnPositions = InitialSpawnPositionBaker.Build(map, FeatureType.EnemyMainStone);
            _initialSpawnPositionFingerprint = fingerprint;
        }

        public bool RestoreRuntimeState(
            MapData map,
            IReadOnlyCollection<Vector2Int> dirtyGroundCells,
            IReadOnlyCollection<Vector2Int> dirtyBiomeCells)
        {
            if (map == null || !IsStructurallyValid()) return false;
            FeatureRecord[] features = _features ?? Array.Empty<FeatureRecord>();
            if (map.Features.Count != features.Length) return false;

            if (dirtyGroundCells != null)
            {
                foreach (Vector2Int cell in dirtyGroundCells)
                {
                    if (!map.GroundStates.IsInBounds(cell.x, cell.y)) continue;
                    map.GroundStates.SetCell(cell.x, cell.y, _groundStates[cell.y * _width + cell.x]);
                }
            }

            if (dirtyBiomeCells != null)
            {
                foreach (Vector2Int cell in dirtyBiomeCells)
                {
                    if (!map.GroundStates.IsInBounds(cell.x, cell.y)) continue;
                    string biome = _biomeIds != null && _biomeIds.Length == _width * _height
                        ? _biomeIds[cell.y * _width + cell.x]
                        : MapData.UnsetBiomeId;
                    map.SetBiomeId(cell.x, cell.y, biome);
                }
            }

            for (int i = 0; i < features.Length; i++)
            {
                FeatureRecord feature = features[i];
                map.Features[i] = new PlacedFeature(
                    feature.Type,
                    feature.WorldPosition,
                    feature.Rotation,
                    feature.Scale);
            }

            return true;
        }

        public void CaptureAssaultRoutes(IReadOnlyList<AssaultRoute> routes, int fingerprint)
        {
            _assaultRoutes = new AssaultRouteRecord[routes != null ? routes.Count : 0];
            if (routes != null)
            {
                for (int i = 0; i < routes.Count; i++)
                {
                    AssaultRoute route = routes[i];
                    var corners = new Vector3[route?.Corners.Count ?? 0];
                    for (int c = 0; c < corners.Length; c++) corners[c] = route.Corners[c];
                    _assaultRoutes[i] = new AssaultRouteRecord
                    {
                        RouteId = route?.RouteId ?? string.Empty,
                        DisplayName = route?.DisplayName ?? string.Empty,
                        Corners = corners,
                    };
                }
            }

            _assaultRouteFingerprint = fingerprint;
            _assaultRoutesValidated = true;
        }

        public void InvalidateAssaultRoutes()
        {
            _assaultRoutesValidated = false;
        }

        private bool IsStructurallyValid()
        {
            if (_width <= 0 || _height <= 0 || _cellSize <= 0f || _width > int.MaxValue / _height)
                return false;

            int cellCount = _width * _height;
            return _heights != null && _heights.Length == cellCount &&
                _groundStates != null && _groundStates.Length == cellCount &&
                _cliffFaces != null && _cliffFaces.Length == cellCount &&
                (_biomeIds == null || _biomeIds.Length == 0 || _biomeIds.Length == cellCount);
        }

        private void AddFeatures(MapData map)
        {
            if (_features == null) return;
            for (int i = 0; i < _features.Length; i++)
            {
                FeatureRecord feature = _features[i];
                map.AddFeature(new PlacedFeature(
                    feature.Type,
                    feature.WorldPosition,
                    feature.Rotation,
                    feature.Scale));
            }
        }

        private void AddRivers(MapData map)
        {
            if (_rivers == null) return;
            for (int i = 0; i < _rivers.Length; i++)
            {
                RiverRecord river = _rivers[i];
                map.AddRiver(new RiverPath(
                    river.Cells ?? Array.Empty<Vector2Int>(),
                    river.WidthMeters,
                    river.DepthMeters,
                    river.WaterTagRatio));
            }
        }

        private void AddLakes(MapData map)
        {
            if (_lakes == null) return;
            for (int i = 0; i < _lakes.Length; i++)
            {
                LakeRecord lake = _lakes[i];
                map.AddLake(new LakeRegion(
                    lake.Center,
                    lake.Radius,
                    lake.WaterY,
                    lake.IsFrozen,
                    lake.WaterTaggedRadius,
                    lake.NoiseAmplitude,
                    lake.NoiseFrequency));
            }
        }

        private void AddMountains(MapData map)
        {
            if (_mountains == null) return;
            for (int i = 0; i < _mountains.Length; i++)
            {
                MountainRecord mountain = _mountains[i];
                map.AddMountain(new MountainRegion(
                    mountain.Kind,
                    mountain.Center,
                    mountain.Extent,
                    mountain.Scale,
                    mountain.RotationRad,
                    mountain.Shape));
            }
        }

        private void AddForests(MapData map)
        {
            if (_forests == null) return;
            for (int i = 0; i < _forests.Length; i++)
            {
                ForestRecord forest = _forests[i];
                map.AddForestRegion(new ForestRegion(
                    forest.Center,
                    forest.Radius,
                    forest.NoiseAmplitude,
                    forest.NoiseFrequency));
            }
        }

        private void AddAssaultRoutes(MapData map)
        {
            if (!_assaultRoutesValidated || _assaultRoutes == null) return;
            for (int i = 0; i < _assaultRoutes.Length; i++)
            {
                AssaultRouteRecord route = _assaultRoutes[i];
                map.AddAssaultRoute(new AssaultRoute(
                    route.RouteId,
                    route.DisplayName,
                    route.Corners ?? Array.Empty<Vector3>()));
            }
        }

        private void CaptureFeatures(IReadOnlyList<PlacedFeature> features)
        {
            _features = new FeatureRecord[features != null ? features.Count : 0];
            if (features == null) return;
            for (int i = 0; i < features.Count; i++)
            {
                PlacedFeature feature = features[i];
                _features[i] = new FeatureRecord
                {
                    Type = feature.Type,
                    WorldPosition = feature.WorldPosition,
                    Rotation = feature.Rotation,
                    Scale = feature.Scale,
                };
            }
        }

        private void CaptureRivers(IReadOnlyList<RiverPath> rivers)
        {
            _rivers = new RiverRecord[rivers != null ? rivers.Count : 0];
            if (rivers == null) return;
            for (int i = 0; i < rivers.Count; i++)
            {
                RiverPath river = rivers[i];
                var cells = new Vector2Int[river.Cells.Count];
                for (int c = 0; c < river.Cells.Count; c++) cells[c] = river.Cells[c];
                _rivers[i] = new RiverRecord
                {
                    Cells = cells,
                    WidthMeters = river.WidthMeters,
                    DepthMeters = river.DepthMeters,
                    WaterTagRatio = river.WaterTagRatio,
                };
            }
        }

        private void CaptureLakes(IReadOnlyList<LakeRegion> lakes)
        {
            _lakes = new LakeRecord[lakes != null ? lakes.Count : 0];
            if (lakes == null) return;
            for (int i = 0; i < lakes.Count; i++)
            {
                LakeRegion lake = lakes[i];
                _lakes[i] = new LakeRecord
                {
                    Center = lake.Center,
                    Radius = lake.Radius,
                    WaterY = lake.WaterY,
                    IsFrozen = lake.IsFrozen,
                    WaterTaggedRadius = lake.WaterTaggedRadius,
                    NoiseAmplitude = lake.NoiseAmplitude,
                    NoiseFrequency = lake.NoiseFrequency,
                };
            }
        }

        private void CaptureMountains(IReadOnlyList<MountainRegion> mountains)
        {
            _mountains = new MountainRecord[mountains != null ? mountains.Count : 0];
            if (mountains == null) return;
            for (int i = 0; i < mountains.Count; i++)
            {
                MountainRegion mountain = mountains[i];
                _mountains[i] = new MountainRecord
                {
                    Kind = mountain.Kind,
                    Center = mountain.Center,
                    Extent = mountain.Extent,
                    Scale = mountain.Scale,
                    RotationRad = mountain.RotationRad,
                    Shape = mountain.Shape,
                };
            }
        }

        private void CaptureForests(IReadOnlyList<ForestRegion> forests)
        {
            _forests = new ForestRecord[forests != null ? forests.Count : 0];
            if (forests == null) return;
            for (int i = 0; i < forests.Count; i++)
            {
                ForestRegion forest = forests[i];
                _forests[i] = new ForestRecord
                {
                    Center = forest.Center,
                    Radius = forest.Radius,
                    NoiseAmplitude = forest.NoiseAmplitude,
                    NoiseFrequency = forest.NoiseFrequency,
                };
            }
        }
    }
}
