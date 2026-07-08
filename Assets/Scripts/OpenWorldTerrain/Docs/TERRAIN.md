# OpenWorldTerrain — 地形自動生成ガイド

`add-terrain-generation` の実装。コア (OpenWorld) とはアセンブリ分離されており、
Terrain タイルは「セルコンテンツ」としてコアのストリーミングに乗る。

## ワークフロー

1. `Tools > OpenWorld > Terrain Generator` を開き Settings を作成
2. Settings でシード・ワールドサイズ・ノイズレイヤー・侵食・バイオーム・散布ルールを設定
3. **プレビュー生成** (低解像度) でフィールド表示 (height/flow/slope/biome...) を見ながら調整
4. シーンに道 (`RoadFeature`)・川 (`RiverFeature`)・POI (`POIFeature`) をスプライン/マーカーで配置
5. **フル生成** → **タイル書き出し + ベイク統合**
6. World Baker で全体リベイク → マネージャ配置済みシーンでプレイ

## 手修正サイクル

- Unity Terrain 上でブラシ編集 (標準/Terrain Tools どちらでも可)
- **手動編集をキャプチャ** → 差分レイヤーとして保存
- 以後、パラメータやシードを変えて再生成しても差分が自動で再適用される
- ロックマスク (R>0.5) を指定した領域は高さ・散布とも完全固定
- 部分再生成: セル範囲を指定すると範囲外は前回結果を維持 (境界フェザー接続)

## 実装マップ

| 機能 | ファイル |
|---|---|
| ノイズ/台地/侵食/バイオーム | `Runtime/Pipeline/*Stage.cs` |
| パイプライン実行 | `Runtime/Pipeline/TerrainPipeline.cs` |
| Feature (道・川・POI) | `Runtime/Features/TerrainFeatures.cs`, `FeatureCarveStage.cs` |
| タイル書き出し/HLOD 統合 | `Editor/TerrainTileExporter.cs` |
| 散布 | `Editor/ScatterBaker.cs` |
| ストリーミング統合 | `Runtime/Streaming/TerrainContentHandler.cs` (コア側: `CellContentHandlers`) |
| オーサリング | `Editor/TerrainGeneratorWindow.cs`, `TerrainAuthoringStore.cs` |

## 注意

- `cellSize / metersPerPixel` は 2^n になるように (例: 256m / 1m = 256 → タイル解像度 257)
- 生成は決定論的: 同一シード + 設定 + Feature 配置 → 同一ワールド (テストで保証)
- 侵食は CPU 実装。8k² 級で遅い場合は Burst Job 化が次の最適化候補
- 散布プレハブ・水面メッシュは `GeneratedScatterTag` 付きで Region 直下に置かれ、
  再散布時に生成分のみ削除される (手動配置は保護)
