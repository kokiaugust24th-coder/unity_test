using OpenWorld;
using UnityEngine;

namespace OpenWorldTerrain
{
    /// <summary>
    /// Terrain タイルのコンテンツハンドラ (spec: terrain-streaming / Terrain タイルのストリーミング)。
    /// ロード/状態機械はコアに委譲し、ここではアクティベート時の SetNeighbors 接続のみ行う。
    /// </summary>
    public class TerrainContentHandler : ICellContentHandler
    {
        public const string Kind = "terrain";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register() => CellContentHandlers.Register(Kind, new TerrainContentHandler());

        public void OnActivated(CellCoord coord, GameObject instance, WorldStreamingManager mgr)
        {
            var terrain = instance.GetComponentInChildren<UnityEngine.Terrain>();
            if (terrain == null) return;
            Connect(coord, terrain, mgr);
        }

        public void OnDeactivating(CellCoord coord, GameObject instance, WorldStreamingManager mgr)
        {
            // 隣接タイルから自分への参照を切る
            foreach (var (dx, dz) in Offsets)
            {
                var n = GetTerrain(new CellCoord(coord.X + dx, coord.Z + dz), mgr);
                if (n != null) ConnectAround(new CellCoord(coord.X + dx, coord.Z + dz), n, mgr, exclude: coord);
            }
        }

        static readonly (int, int)[] Offsets = { (-1, 0), (1, 0), (0, -1), (0, 1) };

        static void Connect(CellCoord coord, UnityEngine.Terrain terrain, WorldStreamingManager mgr)
        {
            ConnectAround(coord, terrain, mgr, null);
            // 既存の隣接タイル側も再接続
            foreach (var (dx, dz) in Offsets)
            {
                var nc = new CellCoord(coord.X + dx, coord.Z + dz);
                var n = GetTerrain(nc, mgr);
                if (n != null) ConnectAround(nc, n, mgr, null);
            }
        }

        static void ConnectAround(CellCoord coord, UnityEngine.Terrain terrain, WorldStreamingManager mgr, CellCoord? exclude)
        {
            UnityEngine.Terrain Get(int dx, int dz)
            {
                var c = new CellCoord(coord.X + dx, coord.Z + dz);
                if (exclude.HasValue && c == exclude.Value) return null;
                return GetTerrain(c, mgr);
            }
            terrain.SetNeighbors(Get(-1, 0), Get(0, 1), Get(1, 0), Get(0, -1));
        }

        static UnityEngine.Terrain GetTerrain(CellCoord coord, WorldStreamingManager mgr)
        {
            var go = mgr.GetExtraContentInstance(coord, Kind);
            return go == null ? null : go.GetComponentInChildren<UnityEngine.Terrain>();
        }
    }
}
