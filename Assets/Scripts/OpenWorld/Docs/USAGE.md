# OpenWorld 使い方ガイド

対象: Unity 6 (6000.x) + URP + Addressables 2.x

## 1. 基本概念

| 用語 | 意味 |
|---|---|
| オーサリングシーン | レベルを自由に配置する作業用シーン。ベイクで変更されることはない |
| OpenWorldRegion | ワールドコンテンツのルート。**直下の各子 = 1 ストリーミング単位** |
| セル | XZ 平面の正方グリッド (既定 256m)。ロード/アンロードの最小単位 |
| ベイク | Region 配下をセル別プレハブ + HLOD に変換し Addressables 登録する処理 |
| HLOD | セル単位の結合メッシュ。実セルがアンロードされた遠景で表示される |
| Data Layer | セル内コンテンツの論理グループ。クエスト単位などでランタイム切替できる |

## 2. メニュー一覧 (Tools > OpenWorld)

| メニュー | 用途 |
|---|---|
| World Baker | ベイク実行・警告確認・ベイク済みセル一覧 (日常操作) |
| シーン変換ウィザード | 既存シーンのオープンワールド化。仕上げボタン付き |
| Grid Overlay | Scene ビューのセル状態表示の切替 |
| 変換ツール / 選択グループを展開 | コンテナの子をリージョン直下へ移動 (プレハブは自動アンパック) |
| 変換ツール / 選択メッシュをセル境界で分割 | 大きな床・地形メッシュをセル単位に切断 |
| 変換ツール / マネージャと HUD をシーンに配置 | ランタイム必須オブジェクトの自動配置 |
| サンプル / ... | デモワールドの生成・再生成 |
| メンテナンス / 旧生成物と重複登録を掃除 | フォルダ移動などで生じた重複の除去 |

## 3. 既存シーンをオープンワールド化する

1. **シーン変換ウィザード** を開く → ルートオブジェクトが自動分類される
   - 静的候補: チェック済み / 動的 (Player・Rigidbody・Animator): 除外 / カメラ・ライト等: 非表示
2. **選択したオブジェクトを変換** → WorldRegion 直下へ移動 + Static 化 (Undo 可)
3. ベイクして World Baker の警告を確認:
   - 「N×M セルに跨るため AlwaysLoaded」→ コンテナなら **グループを展開**、
     1 枚メッシュの床なら **メッシュをセル分割**
4. **マネージャと HUD を配置** (Player に StreamingSource / StreamingSpawnGate も自動追加)
5. World Baker で **全体リベイク** → プレイ

### マップ規模に合わせた設定の目安

| ワールドサイズ | cellSize | activationRadius | loadRadius |
|---|---|---|---|
| 〜100m (テスト用) | 32 | 32 | 64 |
| 〜1km | 64–128 | 128 | 256 |
| 2km〜 | 256 (既定) | 256 | 512 |

半径系・予算系はランタイム設定なので**リベイク不要**。cellSize の変更は**全体リベイク必須**。

## 4. コンポーネントリファレンス

### ランタイム

| コンポーネント | 付ける場所 | 役割 |
|---|---|---|
| `WorldStreamingManager` | シーンに 1 つ | ストリーミングの中核。Config と WorldManifest を割当てる |
| `StreamingSource` | プレイヤー / カメラ | ストリーミング判定の基準点。複数可。radiusMultiplier で先読み拡大 |
| `StreamingSpawnGate` | 物理で動く Player | 足元セルのロード完了まで移動・重力を凍結し落下を防ぐ |
| `OpenWorldRegion` | オーサリングルート | ベイク対象のマーカー。プレイ時は自動無効化 |
| `AlwaysLoadedTag` | 巨大オブジェクト | セル分割せず常時ロードに分類する |
| `DataLayerTag` | ストリーミング単位 | DataLayerAsset を割当 (複数可) |
| `StreamingDebugHUD` | マネージャと同居 | F3 で統計 HUD (開発ビルドのみ) |

### WorldPartitionConfig 全パラメータ

| 項目 | 既定値 | 説明 |
|---|---|---|
| cellSize | 256 | セル一辺 (m)。変更時は全体リベイク |
| activationRadius | 256 | この距離内のセルを表示・有効化 |
| loadRadius | 512 | この距離内のセルをメモリ常駐 (非表示) |
| hlodRadiusMultiplier | 4 | HLOD 表示半径 = loadRadius × この値 |
| hysteresis | 32 | アンロード境界の緩衝幅。境界での振動防止 |
| maxInFlightLoads | 4 | 同時非同期ロード数上限 |
| frameBudgetMs | 2 | 1 フレームのインスタンス化等の時間予算 |
| evaluationInterval | 0.1 | 距離判定の実行間隔 (秒) |
| staticBatchOnActivate | true | アクティベート時に静的バッチングを適用 |
| disableAuthoringRegionOnPlay | true | プレイ開始時に OpenWorldRegion を自動無効化 |
| hlodAtlasSize | 2048 | HLOD テクスチャアトラス解像度 |
| hlodCastShadows | false | HLOD の影 |

## 5. ランタイム API

```csharp
// データレイヤー切替 (アクティブなセルへ即時反映)
DataLayerManager.SetLayerEnabled("QuestA", true);

var mgr = WorldStreamingManager.Instance;
mgr.SetForceLoadAll(true);              // 全セル強制ロード (デバッグ)
mgr.SetPaused(true);                    // ストリーミング一時停止
mgr.SetCellOverride(coord, CellState.Activated); // 指定セルの状態を強制
mgr.TryGetCellState(coord, out var s);  // セル状態の取得
StreamingStats stats = mgr.GetStats();  // HUD と同じ統計

// 独自のストリーミングソース (例: 高速移動体の先読み)
public class MySource : MonoBehaviour, IStreamingSource
{
    public Vector3 Position => transform.position;
    public float RadiusMultiplier => 2f;
    public int Priority => 1;
    void OnEnable() => StreamingSourceRegistry.Register(this);
    void OnDisable() => StreamingSourceRegistry.Unregister(this);
}
```

## 6. トラブルシューティング

| 症状 | 原因と対処 |
|---|---|
| セルが 1 個 (AlwaysLoaded) しかできない | 全コンテンツが 1 コンテナに入っている → **グループを展開** |
| 床が AlwaysLoaded のまま | 1 枚の大きなメッシュ → **メッシュをセル境界で分割** |
| プレイしても色 (状態) が変わらない | マネージャ未配置 → **マネージャと HUD を配置**。または半径がマップ全体を覆っている → Config の半径を縮小 |
| 移動してもロード/アンロードされない | StreamingSource が動くオブジェクトに付いていない (Console に警告が出ます) |
| 遠くの床が消えない | 正常。アンロード済みセルの HLOD が表示されている。確認は hlodRadiusMultiplier=1 |
| プレイ開始直後に Player が落下する | 初期ロード完了前に重力が効いている → **StreamingSpawnGate** を Player に付ける |
| ロード失敗 / 同一アドレス重複エラー | 生成物フォルダを手動移動した → **メンテナンス > 旧生成物と重複登録を掃除** → 全体リベイク |
| 「テクスチャアトラス化不可」警告 | 無害。テクスチャが Read/Write 無効のためマテリアル別結合にフォールバックしただけ |
| ベイクしても変化しない | 差分ベイクは変更セルのみ。設定変更後は **全体リベイク** |

## 7. 既知の制約

- グリッドは単一レイヤー。セル境界を跨ぐ単位は AlwaysLoaded になる (分割ツールで対処)
- ベイク済みライトマップはセルに引き継がれない → リアルタイムライト + APV を推奨
- Unity Terrain の分割は未対応 (別プロポーザル予定)
- セーブ/永続化、ネットワーク同期は未対応 (ICellLoader / IStreamingSource が拡張点)
