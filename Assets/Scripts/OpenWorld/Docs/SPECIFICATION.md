# OpenWorld 技術仕様

正式な要件 (SHALL/MUST + シナリオ) は `openspec/changes/add-open-world-core/specs/` を参照。
本書は実装レベルの仕様をまとめたもの。

## 1. アーキテクチャ

```
[オーサリング]                [ビルド]                      [ランタイム]
OpenWorldRegion   --bake-->  Cell prefabs (Addressables)   WorldStreamingManager
  └ 静的オブジェクト           HLOD prefabs (Addressables)     ├ StreamingSource (複数)
    (DataLayerTag /           WorldManifest.asset             ├ ICellLoader (Addressables/Fake)
     AlwaysLoadedTag)         BakeCache.asset (差分用)        └ DataLayerManager
```

アセンブリ: `OpenWorld.Runtime` / `OpenWorld.Editor` (Editor 専用) / `OpenWorld.Tests.*`。
Editor → Runtime の一方向参照のみ。

## 2. グリッドとセル分類

- XZ 平面の均一グリッド。`CellCoord.FromWorld(pos, cellSize)` = floor 分割
- 距離判定はセル中心ではなく **AABB 最近点距離** (`CellCoord.DistanceXZ`)
- ストリーミング単位 = OpenWorldRegion **直下の各子** (アクティブかつ static のもの)
- 分類規則 (WorldBaker.ClassifyCells):
  1. バウンズを境界イプシロン (0.05m) 縮小してから所属セルを計算
     (境界にぴったり接する床タイルの誤分類防止)
  2. 縮小後も複数セルに跨る、または `AlwaysLoadedTag` 付き → **AlwaysLoaded セル**
  3. それ以外 → バウンズ min が属するセル
- 非 static の子はベイク対象外 (警告してシーンに残す)

## 3. セル状態機械

```
Unloaded → Loading → Loaded → Activated
    ↑___release___|     ↑________|   (降格は隣接状態へのみ)
```

| 状態 | 意味 |
|---|---|
| Unloaded | 何もメモリにない |
| Loading | Addressables 非同期ロード中 (in-flight) |
| Loaded | プレハブアセットがメモリ常駐。未インスタンス化 |
| Activated | インスタンス化され表示・有効 |

要求状態の決定 (`StreamingEvaluation.DesiredForSource`):

```
actR  = activationRadius * source.RadiusMultiplier  (+ hysteresis ※現在 Activated の場合)
loadR = loadRadius       * source.RadiusMultiplier  (+ hysteresis ※現在 Loading 以上の場合)
dist <= actR  → Activated
dist <= loadR → Loaded
else          → Unloaded
```

- 複数ソースは **最大値合成**。AlwaysLoaded セル / ForceLoadAll は常に Activated
- ヒステリシスにより境界での状態振動を防止
- ロード中にソース圏外へ出た場合、完了後にインスタンス化せず即 Release (キャンセル)

## 4. 非同期処理とフレーム予算

- 評価は `evaluationInterval` 秒ごと。ロードキューは (ソース優先度 desc, 距離 asc) でソート
- 同時 in-flight ロード数 ≤ `maxInFlightLoads`
- メインスレッド処理 (インスタンス化 / 破棄 / 解放 / レイヤー再適用) は
  1 フレームあたり `frameBudgetMs` を上限に分割実行 (Stopwatch 計測)
- インスタンス化時: `staticBatchOnActivate` なら StaticBatchingUtility.Combine 適用

## 5. HLOD

- ベイク時にセルごとの静的メッシュを結合 (`HLODBaker`)
  - 全マテリアルのベーステクスチャが Readable → アトラス化して単一マテリアル
    (UV は frac で再マップ。タイリング UV は歪む可能性 → 警告)
  - 不可 → マテリアル別サブメッシュ結合にフォールバック (警告、動作は正常)
  - 静的メッシュのないセル / AlwaysLoaded セル / レイヤー付きコンテンツは HLOD 対象外
- ランタイム表示規則: `hasHlod && state != Activated && dist <= HlodRadius(+hyst)`
- スワップ: セルが Activated になったフレーム +1 まで HLOD を残す (1 フレーム重複でポップ防止)

## 6. Data Layers

- `DataLayerAsset` (ScriptableObject) で定義、`DataLayerTag` で単位に割当 (複数可)
- ベイク時、セルプレハブ内でレイヤーセット別サブツリー (`DataLayerSubtree`) に分類。
  タグコンポーネント自体はベイク成果物から除去される
- 可視判定 = **OR**: 所属レイヤーのいずれかが有効なら表示。未所属・未定義レイヤーは常に表示
- `DataLayerManager.SetLayerEnabled` はアクティブなセルに予算内で即時反映

## 7. ベイクパイプライン

- **非破壊**: オーサリングシーンは一切変更しない。生成物は `Generated/` のみ
- **冪等**: 同一入力での再ベイクは同一出力。Addressables 登録は CreateOrMoveEntry で重複しない
- **差分ベイク**: セル内容 (名前・変換行列・メッシュ/マテリアルパス・レイヤー) の SHA1 を
  BakeCache に保存し、一致セルをスキップ
- **検証**: 非静的混入 / 階層深さ > 8 / セル跨ぎ / Addressables 依存重複 (>1 セル) を警告
- **エラー時**: その実行で作成したアセットを全削除して中断 (部分成果物を残さない)
- **掃除**: 消滅したセルのプレハブ・HLOD・Addressables エントリを自動削除。
  Generated 外を指す古いエントリも毎回パージ

### 生成物とアドレス命名

| 生成物 | パス | アドレス |
|---|---|---|
| セルプレハブ | `Generated/Cells/Cell_{x}_{z}.prefab` | `ow_cell_{x}_{z}` |
| AlwaysLoaded セル | `Generated/Cells/Cell_always.prefab` | `ow_cell_always` |
| HLOD | `Generated/HLOD/HLOD_{x}_{z}.prefab` ほか mesh/mat/atlas | `ow_hlod_{x}_{z}` |
| マニフェスト | `Generated/WorldManifest.asset` | (直接参照) |

Addressables グループ: `OpenWorld-Cells` / `OpenWorld-HLOD`。
パス定義はすべて `OpenWorldPaths` (Editor) に集約。

## 8. メッシュ分割ツール (MeshCellSplitter)

- セル境界の XZ 平面で三角形を Sutherland–Hodgman クリップし、セルごとのピースを生成
- 頂点属性 (位置・法線・UV0) は線形補間。ピースはワールド座標で焼き込み (transform = identity)
- ピースメッシュは `SplitMeshes/` にアセット保存 (プレハブ化に必須)
- 元にコライダーがあればピースに MeshCollider を付与。元オブジェクトは無効化

## 9. 拡張ポイント

| 抽象 | 用途 |
|---|---|
| `ICellLoader` | ロード実装の差替。本番 = AddressablesCellLoader、テスト = FakeCellLoader。独自バンドル/サーバー配信への拡張点 |
| `IStreamingSource` | 判定基準の追加。マルチプレイヤー (サーバー上の全プレイヤー分登録)、カットシーンカメラ、先読みポイント |
| `WorldStreamingManager.Configure` | コードからの初期化 (テスト・動的ワールド生成) |
| `SetCellOverride` | スクリプトによるセル状態の強制 (イベント演出等) |

将来のサーバー権威型 MP は「サーバーで全プレイヤーを IStreamingSource として登録し、
サーバー側はコリジョンのみのセルをロードする」構成を想定 (design.md D4)。

## 10. テスト

- EditMode: グリッド座標変換 / ヒステリシス評価 / データレイヤー OR 論理 (純粋関数)
- PlayMode: FakeCellLoader (同期完了・Addressables 不要) でロード/アンロード遷移、
  ハンドルリーク 0 件、HLOD スワップ、レイヤー切替、ForceLoadAll / Pause を検証
- 実行: Window > General > Test Runner → `OpenWorld.Tests.*`
