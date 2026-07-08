using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OpenWorldTerrain.EditorTools
{
    /// <summary>
    /// 外部ハイトマップ/マスクのインポート (spec: 外部ハイトマップのインポート)。
    /// RAW16 (リトルエンディアン・正方) / EXR / PNG に対応。
    /// </summary>
    public static class HeightFieldImporter
    {
        /// <summary>ファイルから 0..1 の float 配列を読む。戻り値は (heights, width)。</summary>
        public static (float[] data, int width) Load(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".raw":
                case ".r16":
                    return LoadRaw16(path);
                case ".exr":
                case ".png":
                    return LoadTexture(path);
                default:
                    throw new NotSupportedException($"未対応の形式: {ext} (raw/r16/exr/png)");
            }
        }

        static (float[], int) LoadRaw16(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            int count = bytes.Length / 2;
            int width = Mathf.RoundToInt(Mathf.Sqrt(count));
            if (width * width != count)
                throw new InvalidDataException($"RAW16 が正方ではありません ({count} px)");
            var data = new float[count];
            for (int i = 0; i < count; i++)
                data[i] = BitConverter.ToUInt16(bytes, i * 2) / 65535f;
            // RAW は上下反転していることが多い (行順をそのまま採用し、必要なら外部で反転)
            return (data, width);
        }

        static (float[], int) LoadTexture(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RFloat, false);
            if (!tex.LoadImage(bytes)) // PNG/EXR (EXR は Unity 2022+ で LoadImage 可)
                throw new InvalidDataException($"画像の読み込みに失敗: {path}");
            int w = tex.width, h = tex.height;
            if (w != h) Debug.LogWarning($"[Terrain] 非正方画像 ({w}x{h})。幅を基準にリサンプルされます");
            var pixels = tex.GetPixels();
            var data = new float[w * h];
            for (int i = 0; i < data.Length; i++)
                data[i] = pixels[i].r;
            UnityEngine.Object.DestroyImmediate(tex);
            return (data, w);
        }

        /// <summary>インポートして WorldHeightField の名前付きフィールドに書き込む。</summary>
        public static void LoadMaskInto(string path, WorldHeightField field, string fieldName)
        {
            var (data, width) = Load(path);
            var dst = field.GetOrCreateField(fieldName);
            var tmp = new WorldHeightField(field.Width, field.Height, field.MetersPerPixel,
                field.MaxAltitude, field.OriginXZ);
            TerrainPipeline.Resample(data, width, tmp);
            Array.Copy(tmp.Heights, dst, dst.Length);
        }
    }
}
