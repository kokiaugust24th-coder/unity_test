# Tasks: add-open-world-core

## 1. 基盤

- [x] 1.1 asmdef 3 件作成 (OpenWorld.Runtime / OpenWorld.Editor / OpenWorld.Tests)
- [x] 1.2 Addressables パッケージ導入と初期設定 (グループ構成、ビルドパス)
- [x] 1.3 `WorldPartitionConfig` (ScriptableObject: セルサイズ、各半径、フレーム予算、in-flight 上限) 実装
- [x] 1.4 グリッド座標系 (`CellCoord`, ワールド座標⇔セル変換, AABB 最近点距離) 実装 + EditMode テスト

## 2. ベイクパイプライン (Editor)

- [x] 2.1 オーサリングシーン走査と静的オブジェクトのセル分類 (`WorldBaker`)
- [x] 2.2 セルプレハブ生成と Addressables 登録 (`Generated/Cells/`)
- [x] 2.3 Always Loaded セル対応 (セル境界を跨ぐ大型オブジェクトの分類)
- [x] 2.4 差分ベイク (セル単位ダーティ判定) と全体リベイク
- [x] 2.5 ベイク検証 (階層深さ警告、非静的オブジェクト混入警告、依存重複 Analyze)

## 3. ランタイムストリーミング

- [x] 3.1 `IStreamingSource` と `StreamingSourceRegistry` (複数ソース、優先度)
- [x] 3.2 セル状態機械 (Unloaded/Loading/Loaded/Activated) とヒステリシス判定
- [x] 3.3 優先度付きロードキュー、in-flight 制限、フレーム予算制御
- [x] 3.4 Addressables ハンドル管理とアンロード (Release、インスタンスプール)
- [x] 3.5 PlayMode テスト (ロード/アンロード遷移、予算遵守、ソース複数)

## 4. HLOD

- [x] 4.1 セル単位メッシュ結合ベイク (`HLODBaker`: CombineMeshes + マテリアルアトラス)
- [x] 4.2 HLOD ランタイム表示制御 (HLOD 半径、セル状態連動スワップ)
- [x] 4.3 スワップ時の 1 フレーム重複表示によるポップ抑制
- [x] 4.4 PlayMode テスト (スワップ整合性、HLOD のみ表示域)

## 5. Data Layers

- [x] 5.1 `DataLayerAsset` / `DataLayerTag` 実装
- [x] 5.2 ベイク時のレイヤー別分類 (セルプレハブ内サブツリー)
- [x] 5.3 ランタイム API (`DataLayerManager.SetLayerEnabled`) とロード済みセルへの即時反映
- [x] 5.4 テスト (無効レイヤーの非生成、ランタイム切替)

## 6. デバッグツール

- [x] 6.1 Scene ビューのグリッド/セル状態オーバーレイ (Editor)
- [x] 6.2 ランタイム HUD (セル状態数、ロードキュー、メモリ、フレーム予算消費)
- [x] 6.3 ストリーミングソースのギズモ表示 (各半径リング)
- [x] 6.4 デバッグコマンド (全ロード、指定セルロード、レイヤー切替)

## 7. 検証・仕上げ

- [ ] 7.1 サンプルワールド (2km 四方、セル 256m) で E2E 動作確認 ※Unity Editor 上での確認が必要
- [ ] 7.2 プロファイリング (ヒッチ計測、メモリ推移) と既定値の調整 ※Unity Editor 上での確認が必要
- [x] 7.3 ドキュメント (セットアップ手順、ベイク運用、API リファレンス) → `Assets/OpenWorld/README.md`
- [ ] 7.4 `openspec archive add-open-world-core` 前の全タスク完了確認
