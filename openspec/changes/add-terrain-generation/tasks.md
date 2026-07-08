# Tasks: add-terrain-generation

## 1. 基盤

- [x] 1.1 パッケージ導入 (com.unity.splines, Burst, Mathematics, Collections)
- [x] 1.2 asmdef 追加 (OpenWorld.Terrain.Runtime / OpenWorld.Terrain.Editor / Tests)
- [x] 1.3 `TerrainGenerationSettings` (ScriptableObject) とバリデーション
- [x] 1.4 `WorldHeightField` (NativeArray ベース、湿度/フロー/バイオーム ID フィールド含む)
- [x] 1.5 決定論 RNG (シード + ステージ ID + セル座標派生) + 同一性テスト

## 2. 高さマップ合成 (内製)

- [x] 2.1 フィールド可視化デバッガ (高さ/フロー/堆積/傾斜ヒートマップ) ※侵食より先に作る
- [x] 2.2 ノイズスタック (fBm / Ridged / Voronoi / Terrace、ブレンドモード、マスク) ※現状 C# 実装。Burst Job 化は最適化課題
- [x] 2.3 大規模構造マスク (大陸形状・台地・崖) の合成
- [x] 2.4 水力侵食 (ドロップレット、決定論的バケット並列) + フロー/堆積フィールド出力
- [x] 2.5 熱侵食 (安息角反復)
- [ ] 2.6 パラメータプリセット (丘陵 / 侵食渓谷 / 岩石高原) と参照比較による調整 ※Unity 上での実測調整が必要
- [x] 2.7 外部ハイトマップ/マスクインポート (RAW16 / EXR / PNG、補助入力)
- [x] 2.8 低解像度プレビューモード (1/4 解像度で全ステージ実行) + 決定論テスト

## 3. Feature (道・川・POI)

- [x] 3.1 RoadFeature / RiverFeature / POIFeature コンポーネント (スプライン・マーカー入力)
- [x] 3.2 道の整地・法面・縦断勾配検証・スプラット上書き・散布抑制マスク
- [x] 3.3 川の河道彫り込み + 水面メッシュ生成
- [x] 3.4 POI 平坦化 + 散布抑制マスク

## 4. バイオームとスプラット

- [x] 4.1 バイオーム分類 (高度・傾斜・湿度) フィールド生成
- [x] 4.2 スプラットマップ生成 (バイオーム別 TerrainLayer ルール、傾斜による岩肌など)

## 5. 散布

- [x] 5.1 散布ルール評価 (密度・傾斜/高度範囲・ノイズ・最小間隔、決定論)
- [x] 5.2 Terrain Detail / Tree への書き出し
- [x] 5.3 プレハブ散布 (OpenWorldRegion 直下へ `GeneratedScatterTag` 付き配置、再散布時の掃除)
- [x] 5.4 Feature 抑制マスク・ロックマスクの反映

## 6. Terrain タイル化とストリーミング統合

- [x] 6.1 HeightField → セル単位 TerrainData/タイルプレハブ書き出し (境界頂点共有)
- [x] 6.2 セルコンテンツ抽象化 (`CellEntry.contents[]` + `ICellContentHandler`、Prefab ハンドラへの既存動作移行)
- [x] 6.3 Terrain コンテンツハンドラ (ロード/アンロード委譲、アクティベート時の `SetNeighbors` 接続)
- [x] 6.4 遠景用低ポリメッシュ生成とコンテンツ HLOD への統合 (セル HLOD プレハブ一本化)
- [ ] 6.5 PlayMode テスト (タイル遷移、シーム、ハンドルリーク)

## 7. オーサリング

- [x] 7.1 生成ウィンドウ (Tools > OpenWorld > Terrain Generator: 生成/プレビュー/進捗)
- [x] 7.2 手動差分レイヤー (Terrain 編集の記録と再生成後の再適用)
- [x] 7.3 ロックマスクペイント (高さ/スプラット/散布の保護)
- [x] 7.4 領域指定の部分再生成
- [x] 7.5 差分適用後の急勾配警告

## 8. 検証・仕上げ

- [x] 8.1 決定論テスト (同一シード → ハイトマップハッシュ一致)
- [ ] 8.2 2km サンプルワールドでの E2E (生成 → 手修正 → 再生成 → ベイク → プレイ)
- [ ] 8.3 パフォーマンス計測 (生成時間、ランタイムのタイルロードヒッチ)
- [x] 8.4 ドキュメント (Docs/TERRAIN.md: 使い方・パラメータリファレンス)
