# Change: オープンワールドコアシステムの追加 (UE World Partition 相当)

## Why

Unity には Unreal Engine の World Partition / HLOD / Data Layers に相当する統合的なオープンワールド基盤が標準搭載されていない。大規模ワールドを扱うには、セル分割ストリーミング・HLOD・レイヤー管理・アセットパイプラインを一貫した設計で自前実装する必要がある。本変更は AI 駆動開発の基準となる仕様を定義する。

## What Changes

- **world-streaming**: グリッドセル分割によるワールドの自動分割と、距離ベースの非同期ロード/アンロード(UE World Partition 相当)
- **world-hlod**: セル単位の HLOD (階層的 LOD) のエディタベイクとランタイム切替(UE HLOD 相当)
- **world-data-layers**: セル内オブジェクトの論理レイヤー管理とランタイム有効/無効化(UE Data Layers 相当)
- **world-asset-pipeline**: Addressables を用いたセルアセットのビルド・依存管理・メモリ管理
- **world-debug**: ワールドグリッド可視化・ストリーミング状態モニタ・統計 HUD などのデバッグツール(UE `wp.Runtime` デバッグ表示相当)

## Impact

- Affected specs: 新規 capability 5 件(world-streaming, world-hlod, world-data-layers, world-asset-pipeline, world-debug)
- Affected code: `Assets/OpenWorld/` 以下に新規実装(Runtime / Editor アセンブリ)。既存コードへの破壊的変更なし
- 依存: Unity 6 (6000.x) + URP, Addressables, Burst/Jobs(距離計算最適化), Editor Coroutines(ベイク)
- 前提: シングルプレイヤー優先。ただしストリーミングソースを抽象化し、将来のサーバー権威型マルチプレイヤーに拡張可能な設計とする
