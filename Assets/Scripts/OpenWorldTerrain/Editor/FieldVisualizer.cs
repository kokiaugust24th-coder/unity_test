using System.Collections.Generic;
using UnityEngine;

namespace OpenWorldTerrain.EditorTools
{
    /// <summary>
    /// フィールドのヒートマップテクスチャ化 (spec: フィールド可視化デバッガ)。
    /// TerrainGeneratorWindow に表示される。
    /// </summary>
    public static class FieldVisualizer
    {
        public const string ViewHeight = "height";
        public const string ViewSlope = "slope";

        public static readonly string[] Views =
        {
            ViewHeight, ViewSlope,
            WorldHeightField.FieldFlow, WorldHeightField.FieldDeposit,
            WorldHeightField.FieldMoisture, WorldHeightField.FieldBiome,
            WorldHeightField.FieldScatterSuppress,
        };

        public static Texture2D Render(WorldHeightField f, string view, int maxSize = 512)
        {
            int step = Mathf.Max(1, Mathf.Max(f.Width, f.Height) / maxSize);
            int w = f.Width / step, h = f.Height / step;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false) { name = $"field_{view}" };
            var pixels = new Color[w * h];

            float[] data = null;
            if (view != ViewHeight && view != ViewSlope)
                f.TryGetField(view, out data);

            for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
            {
                int sx = x * step, sz = z * step;
                float v;
                if (view == ViewHeight) v = f.Sample(sx, sz);
                else if (view == ViewSlope) v = BiomeStage.SlopeDeg(f, sx, sz, f.MaxAltitude) / 90f;
                else v = data != null ? data[f.Index(sx, sz)] : 0f;

                pixels[z * w + x] = view == WorldHeightField.FieldBiome
                    ? BiomeColor((int)v)
                    : Heat(Mathf.Clamp01(v));
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        static Color Heat(float v) =>
            v < 0.5f ? Color.Lerp(new Color(0.05f, 0.1f, 0.4f), new Color(0.1f, 0.8f, 0.3f), v * 2f)
                     : Color.Lerp(new Color(0.1f, 0.8f, 0.3f), new Color(0.95f, 0.2f, 0.1f), v * 2f - 1f);

        static readonly List<Color> _biomePalette = new List<Color>
        {
            new Color(0.4f, 0.7f, 0.3f), new Color(0.8f, 0.75f, 0.5f), new Color(0.5f, 0.5f, 0.55f),
            new Color(0.2f, 0.5f, 0.7f), new Color(0.85f, 0.6f, 0.3f), new Color(0.6f, 0.3f, 0.6f),
        };

        static Color BiomeColor(int id) => _biomePalette[Mathf.Abs(id) % _biomePalette.Count];
    }
}
