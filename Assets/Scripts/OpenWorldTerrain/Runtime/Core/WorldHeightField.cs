using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenWorldTerrain
{
    /// <summary>
    /// ワールド全体の高さフィールドと名前付き補助フィールド (flow/deposit/moisture/biome 等)。
    /// 全生成ステージの共有中間データ (design.md D3)。値域: height は 0..1 (maxHeight で実寸化)。
    /// </summary>
    public class WorldHeightField
    {
        public readonly int Width;   // px (X)
        public readonly int Height;  // px (Z)
        public readonly float MetersPerPixel;
        public readonly float MaxAltitude; // m
        public readonly Vector2 OriginXZ;  // ワールド原点 (min コーナー)

        public float[] Heights; // Width * Height, row-major (z * Width + x)
        readonly Dictionary<string, float[]> _fields = new Dictionary<string, float[]>();

        public WorldHeightField(int width, int height, float metersPerPixel, float maxAltitude, Vector2 originXZ)
        {
            Width = width;
            Height = height;
            MetersPerPixel = metersPerPixel;
            MaxAltitude = maxAltitude;
            OriginXZ = originXZ;
            Heights = new float[width * height];
        }

        public int Index(int x, int z) => z * Width + x;

        public float Sample(int x, int z) =>
            Heights[Index(Mathf.Clamp(x, 0, Width - 1), Mathf.Clamp(z, 0, Height - 1))];

        /// <summary>バイリニア補間サンプル (px 座標)。</summary>
        public float SampleBilinear(float x, float z)
        {
            int x0 = Mathf.FloorToInt(x), z0 = Mathf.FloorToInt(z);
            float tx = x - x0, tz = z - z0;
            float a = Sample(x0, z0), b = Sample(x0 + 1, z0);
            float c = Sample(x0, z0 + 1), d = Sample(x0 + 1, z0 + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
        }

        /// <summary>名前付き補助フィールドの取得 (無ければ生成)。</summary>
        public float[] GetOrCreateField(string name)
        {
            if (!_fields.TryGetValue(name, out var f))
                _fields[name] = f = new float[Width * Height];
            return f;
        }

        public bool TryGetField(string name, out float[] field) => _fields.TryGetValue(name, out field);
        public IEnumerable<string> FieldNames => _fields.Keys;

        /// <summary>決定論テスト用のコンテンツハッシュ (FNV-1a)。</summary>
        public ulong ComputeHash()
        {
            const ulong prime = 1099511628211UL;
            ulong h = 14695981039346656037UL;
            var bytes = new byte[4];
            foreach (float v in Heights)
            {
                BitConverter.TryWriteBytes(bytes, v);
                for (int i = 0; i < 4; i++) { h ^= bytes[i]; h *= prime; }
            }
            return h;
        }

        // 標準フィールド名
        public const string FieldFlow = "flow";
        public const string FieldDeposit = "deposit";
        public const string FieldMoisture = "moisture";
        public const string FieldBiome = "biome";
        public const string FieldScatterSuppress = "scatterSuppress";
        public const string FieldLock = "lock";
    }
}
