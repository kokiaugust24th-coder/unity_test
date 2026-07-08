## 1. WebGL Build Configuration
- [x] 1.1 Player Settings を WebGL 向けに設定する(IL2CPP、WASM Code Optimization = Speed、Decompression Fallback = 有効) → `Assets/Scripts/WebGLDeploy/Editor/WebGLPlayerSettingsSetup.cs`
- [x] 1.2 WebGL の初期メモリサイズと Memory Growth(上限付き)を設定し、選定値と根拠を記録する → 同上スクリプト(初期256MB/上限1024MB/Geometric)。実測に基づく調整は未実施(design.md Open Questions)
- [x] 1.3 world-streaming の Jobs/Burst ホットパスが WebGL Threading Support 無効(シングルスレッド)で正しく動作することを確認する → 実ビルドで検証済み。コンソールに `[Physics::Module] Threading Mode: Single-Threaded` と表示され、`[OpenWorld] オーサリングリージョン 'WorldRegion' を無効化しました。` 等のワールド初期化ログもエラーなく出力された

## 2. Addressables WebGL パス分離
- [x] 2.1 ~~Standalone とは別に WebGL 用 Build/Load パスを持つ Addressables プロファイルを追加する~~ → 実装時に Default プロファイルが既に `[BuildTarget]` トークンで分離済みと判明したため撤回(design.md D4 参照)。ビルドターゲットが WebGL の状態でコンテンツビルドすることを自動ビルドスクリプトで保証する方式に変更
- [x] 2.2 WebGL ターゲットでコンテンツをビルドし、ローカル WebGL ビルドでセル/HLOD/Terrain タイルの Addressable バンドルが UnityWebRequest 経由で読み込めることを確認する → ブラウザコンソールで `openworld-hlod_assets_all_*.bundle` / `openworld-cells_assets_all_*.bundle` が HTTP 経由でダウンロード・キャッシュされたことを確認

## 3. 自動ビルドスクリプト
- [x] 3.1 `-executeMethod` から呼び出せる WebGL 用 Editor ビルドスクリプトを追加する → `Assets/Scripts/WebGLDeploy/Editor/WebGLBuildScript.cs`(`WebGLDeploy.EditorTools.WebGLBuildScript.Build`)
- [x] 3.2 バッチモードコマンドでエディタを開かずにビルドが完走することを確認する → `Unity.exe -batchmode -quit -buildTarget WebGL -executeMethod WebGLDeploy.EditorTools.WebGLBuildScript.Build` を実行し exit code 0、`Builds/WebGL`(約16.6MB)を生成。初回は `geometricMemoryGrowthCap` という存在しない API を呼んでいたコンパイルエラーがあり、`maximumMemorySize` が上限を兼ねる仕様と判明したため修正して再実行し成功

## 4. ローカル検証
- [x] 4.1 WebGL ビルドをローカルサーバーで配信し、デスクトップブラウザで起動することを確認する → `python -m http.server` + ブラウザで起動確認。WebGL2 コンテキスト生成、コンソールエラーなし
- [x] 4.2 初期ワールドセルがロードされ、フレーム予算内でプレイヤーが移動できる(OOM・致命的エラーが発生しない)ことを確認する → セルコンテンツ(プレースホルダーキューブ/HLOD メッシュ)が描画され、WASD 入力でカメラ/プレイヤーが移動。コンソールエラー・クラッシュなし

## 5. 公開(手動)
- [ ] 5.1 `Builds/WebGL/` を Cloudflare Pages へ公開(手動アップロードまたは `wrangler pages deploy`)、`*.pages.dev` の URL で動作を確認する → **未実施**(Cloudflare アカウントでの手動作業が必要)
- [x] 5.2 Cloudflare Pages 公開手順と itch.io / GitHub Pages 等の代替ホスティング手順(Decompression Fallback の位置づけを含む)をドキュメント化する → `docs/webgl-deploy.md`

## 6. CI/CD
- [ ] 6.1 GitHub リモートリポジトリを用意する(未設定の場合) → **未実施**(現在リモート未設定。ユーザー側での作成が必要)
- [ ] 6.2 Unity ライセンス(Personal の `.ulf` または Pro シリアル)と Cloudflare の `CLOUDFLARE_API_TOKEN`/`CLOUDFLARE_ACCOUNT_ID` を GitHub Secrets に登録する → **未実施**(アカウント・認証情報が必要なため手動作業)
- [x] 6.3 PR/push 時に EditMode テスト + WebGL ビルドを実行する GitHub Actions ワークフローを追加する(`game-ci/unity-builder` を使用) → `.github/workflows/webgl-ci-cd.yml`
- [x] 6.4 `Library/` フォルダを `Assets/`・`Packages/` の内容でキーイングしてキャッシュするステップを追加する → 同ワークフロー内 `actions/cache@v4`
- [x] 6.5 main ブランチへの push/merge 時のみ、テスト・ビルド成功を条件に Cloudflare Pages へ自動デプロイするジョブを追加する → 同ワークフロー `deploy-cloudflare` ジョブ(`cloudflare/pages-action@v1`、`needs: test-and-build` + `if: push to main`)。当初は `deploy-itch`(butler/itch.io)だったが、ホスティングを Cloudflare Pages に変更したため置き換え
- [ ] 6.6 テスト失敗時・ビルド失敗時にデプロイジョブがスキップされることを確認する(意図的に失敗させて検証) → **未実施**(6.1/6.2 未完了のため CI 実行不可)
- [ ] 6.7 CI ログにライセンス/認証情報が出力されていないことを確認する → **未実施**(同上)

## 残タスク(ユーザー側の手動作業が必要)

- 5.1 / 6.1 / 6.2 / 6.6 / 6.7: Cloudflare アカウント作成、GitHub リモート作成、Secrets/Variables 登録はエージェントが代行できないため、ユーザー側での対応が必要。登録後に 6.6/6.7 は実際に CI を走らせて確認する
