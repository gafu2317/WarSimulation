using System;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// <see cref="GroundState"/> を 2D セル配列で保持する純粋データクラス。
    /// 見た目は連続 3D 地形でも、ゲームロジック（移動コスト・視界判定など）は
    /// このグリッドへの問い合わせで行う。
    /// </summary>
    public class GroundStateGrid
    {
        private readonly GroundState[,] _cells;

        public int Width { get; }
        public int Height { get; }
        public float CellSize { get; }

        public GroundStateGrid(int width, int height, float cellSize, GroundState defaultState = GroundState.Normal)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize));

            Width = width;
            Height = height;
            CellSize = cellSize;
            _cells = new GroundState[width, height];

            if (defaultState != GroundState.Normal)
            {
                for (int z = 0; z < height; z++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        _cells[x, z] = defaultState;
                    }
                }
            }
        }

        public GroundState GetCell(int x, int z) => _cells[x, z];

        public void SetCell(int x, int z, GroundState state) => _cells[x, z] = state;

        /// <summary>
        /// 「上書きしてもよい状態」ルールに従ってセルに書き込む。
        /// 優先度: Water > Snow > Swamp > Normal。低優先は高優先を上書きしない。
        /// </summary>
        public void PaintCell(int x, int z, GroundState state)
        {
            GroundState current = _cells[x, z];
            if ((int)state >= (int)current)
            {
                _cells[x, z] = state;
            }
        }

        public bool IsInBounds(int x, int z) =>
            x >= 0 && x < Width && z >= 0 && z < Height;

        public Vector2 WorldSize => new Vector2(Width * CellSize, Height * CellSize);

        /// <summary>
        /// ワールド XZ 座標からグリッドセルインデックスを返す（範囲外は端にクランプ）。
        /// </summary>
        public Vector2Int WorldToCell(Vector3 worldPos)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt(worldPos.x / CellSize), 0, Width - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(worldPos.z / CellSize), 0, Height - 1);
            return new Vector2Int(x, z);
        }

        public GroundState SampleAt(Vector3 worldPos)
        {
            Vector2Int c = WorldToCell(worldPos);
            return _cells[c.x, c.y];
        }

        /// <summary>
        /// ワールド座標 (<paramref name="center"/>) を中心とする半径 <paramref name="radius"/> の円内に、
        /// 指定 <paramref name="state"/> のセルが 1 つでも存在するか。
        /// 山が川・湖に被らないか、湖が川に被らないか、などの配置バリデーション用の共通ヘルパ。
        /// </summary>
        public bool HasAnyCellInCircle(Vector2 center, float radius, GroundState state)
        {
            if (radius <= 0f) return false;

            int cx = Mathf.FloorToInt(center.x / CellSize);
            int cy = Mathf.FloorToInt(center.y / CellSize);
            int r = Mathf.CeilToInt(radius / CellSize);
            float rSqr = radius * radius;

            for (int y = cy - r; y <= cy + r; y++)
            {
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (!IsInBounds(x, y)) continue;
                    if (_cells[x, y] != state) continue;

                    float wx = (x + 0.5f) * CellSize - center.x;
                    float wy = (y + 0.5f) * CellSize - center.y;
                    if (wx * wx + wy * wy <= rSqr) return true;
                }
            }
            return false;
        }
    }
}
