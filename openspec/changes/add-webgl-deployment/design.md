## Context

- 既存プロジェクトは PC/コンソールを主対象に、Addressables セルストリーミング・HLOD・Terrain タイル・Jobs/Burst ホットパスで構成されている(`add-open-world-core` design.md, `add-terrain-generation` proposal.md)
- WebGL はブラウザの WebAssembly サンドボックス上で動作し、以下の制約がある:
  - スクリプティングバックエンドは IL2CPP 固定
  - メモリは明示的に確保したヒープ内(Memory Growth 未設定だと固定サイズ)に収まる必要があり、OOM は即クラッシュにつながる
  - `SharedArrayBuffer` を使うマルチスレッド(Web Worker)実行には `Cross-Origin-Opener-Policy` / `Cross-Origin-Embedder-Policy` レスポンスヘッダーが必須。itch.io を含む多くの無料静的ホストはカスタムヘッダーを設定できない
  - Brotli 圧縮ビルドはサーバー側で正しい `Content-Encoding` を返す必要があり、対応していないホストでは読み込み失敗する(Decompression Fallback で回避可能)
- 公開先は「無料かつ手軽」を優先し、itch.io を既定とする

## Goals / Non-Goals

- Goals:
  - 既存の Standalone/コンソール向けビルド設定・仕様(world-streaming, world-asset-pipeline, terrain-streaming)を変更せず、WebGL を追加ビルドターゲットとして両立させる
  - サーバー設定(圧縮ヘッダー、CORS ヘッダー)に依存せずに動作する構成をデフォルトにする(ホスティング先を選ばない)
  - itch.io への公開をワンステップに近い手順に落とし込む
- Non-Goals:
  - WebGL 向けタッチ操作/モバイルブラウザ最適化(別提案)
  - COOP/COEP 対応ホスティングによる真のマルチスレッド実行(将来拡張の余地は残すが本変更のスコープ外)
  - デスクトップ版と同等のワールド規模・描画品質のブラウザでの実現(初回は縮小スコープ)

## Decisions

### D1: 圧縮設定 — Decompression Fallback を有効化

- Brotli/gzip の `Content-Encoding` 設定をサーバーに要求しない代わりに、Unity の Decompression Fallback(クライアント側で解凍)を有効にする
- 代替案: サーバー側で正しい圧縮ヘッダーを返す → ホスティング先ごとに設定が異なり、itch.io ではカスタムヘッダーが使えないため不採用。GitHub Pages 等でも同様の理由でこちらを既定とする

### D2: スレッディング — シングルスレッド実行を既定にする

- WebGL Threading Support(Web Worker + `SharedArrayBuffer`)は有効化しない。world-streaming のホットパス Jobs はシングルスレッド Burst 実行で動作させる
- 代替案: WebGL Threading Support を有効化 → itch.io 等の COOP/COEP 非対応ホストでは動作せず、ホスティング選択肢を著しく狭めるため不採用。将来 COOP/COEP 対応ホストに移行する場合は ADDED 要件として拡張

### D3: メモリ設定 — 明示的な上限 + Memory Growth 有効化

- WebGL の初期メモリサイズを設定し、Memory Growth(段階的にヒープを拡張)を有効化した上で、上限(実測に基づき決定)を設定する
- Terrain/HLOD のロード半径・同時ロードセル数は既存の world-asset-pipeline のパラメータを WebGL 用に別途チューニングする(縮小スコープ)。QualitySettings の既存 "Mobile" ティアを WebGL 向けの出発点として流用検討する
- 代替案: デスクトップと同一パラメータのまま公開 → 実測なしに OOM リスクが高く不採用

### D4: Addressables — 既存 Default プロファイルの `[BuildTarget]` トークンを流用(専用プロファイルは追加しない)

- 実装時に確認した結果、`Assets/AddressableAssetsData/AddressableAssetSettings.asset` の `Default` プロファイルは既に `Local.BuildPath` / `Local.LoadPath` に `[BuildTarget]` トークンを使っており、アクティブビルドターゲットごとに自動的にパスが分離される(`m_BuildRemoteCatalog: 0` のためリモートカタログは未使用、コンテンツは StreamingAssets に同梱される)
- そのため WebGL 専用の Addressables プロファイルを別途追加すると、既存の仕組みと重複するだけで実質的な差分がない。**当初案(専用プロファイル追加)を撤回し、ビルドターゲットを WebGL にした状態で Addressables コンテンツをビルドする**運用に変更する(自動ビルドスクリプトがこれを保証する)
- ロードは `UnityWebRequest` ベース(WebGL では標準)。コンテンツはビルド成果物(StreamingAssets)と同一ホストから配信される(初回。将来 CDN 分離は拡張余地)

### D5: ホスティング — Cloudflare Pages を既定、itch.io / GitHub Pages を代替として明記

- **変更(当初 itch.io を既定としていたが Cloudflare Pages に変更)**: Cloudflare Pages は帯域無制限の無料枠、Git 連携デプロイ(`wrangler pages deploy`)、そして `_headers` ファイルによるカスタムレスポンスヘッダー設定に対応している。カスタムヘッダーに対応していることで、design.md の Open Questions に残していた「将来 COOP/COEP 対応ホストへ移行してマルチスレッド化する」という選択肢を、ホスティングを変更することなく将来的に選べるようになる(本変更ではスレッディング設定自体は変更しない。D2 は維持)
- itch.io はゲーム配信プラットフォームとしての発見性・zip アップロードのみで完結する手軽さがあるため、代替ホスティングとして引き続き文書化する
- GitHub Pages も同様に代替として維持する。カスタムヘッダー非対応だが Decompression Fallback(D1)により追加設定なしで動作する
- 代替案: itch.io を既定のまま → 無料・手軽だがカスタムヘッダーが使えず将来の拡張性がない。GitHub Pages を既定に → 帯域のソフト上限(目安 100GB/月)がオープンワールドの大容量アセットと相性が悪い可能性があるため不採用

### D6: CI/CD プラットフォーム — GitHub Actions + game-ci/unity-builder + Cloudflare Pages 公式アクション

- GitHub Actions を採用する。Unity のビルドは実績のある `game-ci/unity-builder` アクションを利用し、Docker イメージの取得・Unity ライセンス活性化・ビルド実行を任せる
- Cloudflare Pages へのデプロイは公式の `cloudflare/pages-action` を CI 上で実行する専用ジョブで行う(itch.io へのデプロイが必要な場合は `butler` を使った同様のジョブを追加可能。design.md D5 で代替ホスティングとして維持)
- パイプラインは 2 段階: (1) PR / push 時に EditMode テスト + WebGL ビルドを実行して検証、(2) main ブランチへの merge/push 時のみ Cloudflare Pages への自動デプロイを実行(PR からはデプロイしない)
- 代替案: Jenkins/GitLab CI 等の自前 CI → リポジトリが GitHub 前提でセットアップコストが増えるため不採用。手動ビルド+手動アップロードのみ → 反復デプロイの手間が大きく、テスト未実施のままの公開を防げないため不採用
- Unity ライセンス: Personal ライセンスは CI での自動アクティベーションに `.ulf` ファイルの事前生成が必要(手動で一度取得し GitHub Secrets に保存)。Pro ライセンスがある場合はシリアルを直接 Secrets に保存する

## Risks / Trade-offs

- WebGL ヒープの OOM → 初回はセル数/Terrain タイル数を絞った縮小スコープで検証し、実測してから拡張(Open Questions 参照)
- マルチスレッド不可によるロード時のヒッチ → 既存のフレーム予算制御(1フレームあたりのインスタンス化予算)を WebGL ではより保守的な値に調整
- ビルドサイズ(Terrain/HLOD アセット)が無料ホストの実用上の制限に達する可能性 → 初回リリースは縮小スコープ、必要に応じてテクスチャ解像度/HLOD 品質を WebGL 専用ティアで下げる
- OneDrive 同期下のプロジェクト(`add-open-world-core` design.md でも既知のリスク)→ WebGL ビルド出力先も OneDrive 同期対象外のパスを推奨
- CI 実行時間・コスト → オープンワールド規模のインポート(Terrain/HLOD ベイク成果物)は初回インポートが重い。`Library/` キャッシュ(D6 のパイプライン内)で 2 回目以降を短縮するが、GitHub Actions の無料枠(分単位課金)を圧迫する可能性があるため、CI は PR ごとの毎回フルビルドではなく差分の少ない範囲に絞ることを検討する
- Cloudflare API トークン・Unity ライセンスの漏洩 → GitHub Secrets 管理を必須とし、ログへの出力を避ける(`cloudflare/pages-action` 等の標準的なシークレット渡し方に従う)。トークンは Cloudflare 側で Pages 編集権限のみに絞ったスコープ限定トークンを発行する

## Migration Plan

新規ビルドターゲットの追加であり、既存の Standalone/コンソール向け仕様・ビルド設定への移行作業はなし。ロールバックは追加した WebGL 用 Player Settings プロファイル・Addressables プロファイル・ビルドスクリプトの削除で完結する。

## Open Questions

- WebGL 版の初期公開スコープ(セル数・Terrain タイル数)をどこまで絞るか(実測して決定)
- WebGL 向けメモリ上限の具体値(実機ブラウザでの実測が必要)
- Cloudflare Pages はカスタムヘッダーに対応済みのため、将来 COOP/COEP ヘッダーを設定してマルチスレッド(D2)を有効化するかどうかは、ホスティング移行なしで判断可能になった。本変更のスコープでは未対応のまま据え置く
- GitHub リモートリポジトリの作成タイミング・可視性(public/private)。private の場合 GitHub Actions の無料枠(分単位)が Actions minutes の消費対象になる点に留意
- Unity のライセンス種別(Personal/Pro)。CI ライセンス活性化の手順が変わるため実装前に確定させる必要がある
