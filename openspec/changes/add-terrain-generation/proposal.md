# Change: 高品質地形・マップ自動生成システムの追加 (内製フル)

## Why

デスストランディングやエルデンリング級の地形は「プロシージャル下地 + 手作業の作り込み」で成立する。AI 駆動開発では実装工数の経済性が従来と逆転するため、侵食を含む生成パイプラインを内製する: 外部 GUI ツール (Gaea 等) は人間の手作業が必須で AI が自動操作できず、再現性がパイプラインの外に漏れ、設計が張りぼてになる。内製ならシードから最終タイルまで全工程が決定論的・自動実行可能で、AI による反復チューニングの対象にできる。

## What Changes

- **terrain-generation**: シード決定論的な高さマップ合成 (ノイズスタック、台地・崖の大規模構造、水力/熱侵食シミュレーション)、バイオーム分類、スプラットマップ生成。補助入力として外部ハイトマップ (RAW16/EXR/PNG) のインポートも提供
- **terrain-features**: スプラインベースの道・川の彫り込みと整地、POI スタンプ配置
- **terrain-scattering**: バイオーム・傾斜・高度ルールによる植生/岩の決定論的散布 (Terrain ディテール + プレハブ)
- **terrain-authoring**: 生成ウィンドウ、手動編集の保護 (ロックマスク + 差分レイヤー)、領域単位の部分再生成
- **terrain-streaming**: セル単位の Unity Terrain タイル書き出し、Addressables 化、隣接シーム解決、遠景 HLOD メッシュ

## Impact

- Affected specs: 新規 capability 5 件。add-open-world-core の world-streaming / world-asset-pipeline を拡張
- Affected code: `Assets/Scripts/OpenWorld/Terrain/` 以下に新規 (Runtime / Editor)。既存コードへの破壊的変更なし
- 依存: add-open-world-core (実装済み)、Unity Terrain、com.unity.splines、Burst + Mathematics + Collections。**外部有料ツールへ