# Change: WebGLビルド・デプロイ対応の追加

## Why

現状このプロジェクトは PC/コンソール向け(Unity 6 + URP、Addressables によるセルストリーミング、HLOD、Terrain タイル、Jobs/Burst のホットパス)を前提に設計されている(`add-open-world-core`, `add-terrain-generation`)。WebGL は固定的なメモリヒープ・IL2CPP 必須・特別なサーバーヘッダーなしではマルチスレッド不可、といった制約があり、既存のビルド設定のままではブラウザで動作しない、または起動時にクラッシュする可能性が高い。無料かつ手軽な手段(itch.io 等)でブラウザ実行可能な形にデプロイできるようにする。

## What Changes

- **webgl-build**: WebGL 向け Player Settings(IL2CPP、Decompression Fallback、メモリサイズ/Memory Growth)、Jobs/Burst ホットパスのシングルスレッド互換確認、WebGL 専用 Addressables プロファイル、バッチモードでの自動ビルドスクリプトを追加
- **webgl-hosting**: ビルド成果物を Cloudflare Pages へ公開する手順(既定・無料・帯域無制限・将来のカスタムヘッダー拡張が可能)と、itch.io / GitHub Pages 等の代替静的ホスティング手順(Decompression Fallback により圧縮ヘッダー設定なしでも動作)、公開前のローカル検証チェックリストを整備
- **webgl-ci-cd**: GitHub Actions によるビルド・テストの自動化(PR ごとに EditMode テスト + WebGL ビルド)と、main ブランチへのマージ時の Cloudflare Pages への自動デプロイ。Unity ライセンス/Cloudflare API トークンは CI シークレットで管理し、テスト・ビルド失敗時はデプロイをブロックする

## Impact

- Affected specs: 新規 capability 3 件(webgl-build, webgl-hosting, webgl-ci-cd)
- Affected code: `ProjectSettings/`(Player Settings, WebGL 用 Addressables プロファイル)、Addressables 設定(`Assets/AddressableAssetsData/`)、新規 Editor ビルドスクリプト(`Assets/Editor/` 等)、新規 CI 設定(`.github/workflows/`)。既存の Standalone/コンソール向けビルド設定への破壊的変更なし(WebGL は追加のビルドターゲット/プロファイルとして扱う)
- 依存: WebGL Build Support モジュール(導入済み: Unity 6000.5.2f1)、既存の world-streaming / world-asset-pipeline / terrain-streaming(いずれも `add-open-world-core`, `add-terrain-generation` で提案済み)、GitHub Actions(`game-ci/unity-builder`, `cloudflare/pages-action`)、Cloudflare アカウント + Pages プロジェクト、Unity ライセンス(CI 用のシリアル/ライセンスファイルが別途必要)
- 前提: 初回リリースはワールド規模(セル数・Terrain タイル数)を絞った「WebGL向け縮小スコープ」から開始し、メモリ実測を踏まえて拡張する。リポジトリに GitHub リモートがまだ設定されていないため、CI/CD 導入時にリモート作成が別途必要
