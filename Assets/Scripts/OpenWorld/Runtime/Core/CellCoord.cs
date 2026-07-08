using System;
using UnityEngine;

namespace OpenWorld
{
    /// <summary>XZ 平面グリッド上のセル座標。</summary>
    [Serializable]
    public struct CellCoord : IEquatable<CellCoord>
    {
        public int X;
        public int Z;

        public CellCoord(int x, int z)
        {
            X = x;
            Z = z;
        }

        /// <summary>ワールド座標を含むセルを返す。</summary>
        public static CellCoord FromWorld(Vector3 worldPos, float cellSize)
        {
            return new CellCoord(
                Mathf.FloorToInt(worldPos.x / cellSize),
                Mathf.FloorToInt(worldPos.z / cellSize));
        }

        /// <summary>セルの XZ 最小コーナー。</summary>
        public Vector3 MinCorner(float cellSize) => new Vector3(X * cellSize, 0f, Z * cellSize);

        /// <summary>セル中心 (Y=0)。</summary>
        public Vector3 Center(float cellSize) =>
            new Vector3((X + 0.5f) * cellSize, 0f, (Z + 0.5f) * cellSize);

        /// <summary>
        /// 点からセル XZ AABB への最近点距離。セル内なら 0。
        /// ストリーミング判定はセル中心距離ではなくこの値を使う (spec: world-streaming)。
        /// </summary>
        public float DistanceXZ(Vector3 point, float cellSize)
        {
            float minX = X * cellSize;
            float minZ = Z * cellSize;
            float cx = Mathf.Clamp(point.x, minX, minX + cellSize);
            float cz = Mathf.Clamp(point.z, minZ, minZ + cellSize);
            float dx = point.x - cx;
            float dz = point.z - cz;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public bool Equals(CellCoord other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is CellCoord c && Equals(c);
        public override int GetHashCode() => unchecked(X * 73856093 ^ Z * 19349663);
        public override string ToString() => $"Cell({X},{Z})";

        public static bool operator ==(CellCoord a, CellCoord b) => a.Equals(b);
        public static bool operator !=(CellCoord a, CellCoord b) => !a.Equals(b);

        /// <summary>Always Loaded セルを表す特殊座標。</summary>
        public static readonly CellCoord AlwaysLoaded = new CellCoord(int.MinValue, int.MinValue);
    }
}
