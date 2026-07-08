using System;
using System.IO;
using UnityEngine;

namespace OpenWorldTerrain.EditorTools
{
    /// <summary>
    /// ベースライン (純生成結果) と手動差分レイヤーの保存 (spec: terrain-authoring / 手動差分レイヤー)。
    /// 形式: [int32 width][int32 height][float...] の .bytes。
    /// 保存先は Assets 外 (プロジェクトルート/TerrainAuthoring) — 大きなバイナリを
    /// Unity にインポートさせない (インポートループ防止)。
    /// </summary>
    public static class TerrainAuthoringStore
    {
        public const string Folder = "TerrainAuthoring"; // プロジェクトルート直下 (Assets 外)
        public static string BaselinePath => Folder + "/baseline.bytes";
        public static string ManualDiffPath => Folder + "/manualdiff.bytes";

        public static void Save(string path, float[] data, int width, int height)
        {
            string full = ToFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            using var bw = new BinaryWriter(File.Open(full, FileMode.Create));
            bw.Write(width);
            bw.Write(height);
            var bytes = new byte[data.Length * 4];
            Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
            bw.Write(bytes);
            // Assets 外なので AssetDatabase.Refresh は不要
        }

        public static bool TryLoad(string path, out float[] data, out int width, out int height)
        {
            data = null; width = height = 0;
            string full = ToFullPath(path);
            if (!File.Exists(full)) return false;
            using var br = new BinaryReader(File.OpenRead(full));
            width = br.ReadInt32();
            height = br.ReadInt32();
            var bytes = br.ReadBytes(width * height * 4);
            data = new float[width * height];
  