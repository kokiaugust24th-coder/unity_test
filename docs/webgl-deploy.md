# WebGL デプロイ手順

仕様: `openspec/changes/add-webgl-deployment/`(承認済み実装分)。設計判断の根拠は同ディレクトリの `design.md` を参照。

## ローカルビルド

```
"<Unity Editor パス>\Unity.exe" -batchmode -quit -projectPath "." ^
  -buildTarget WebGL -executeMethod WebGLDeploy.EditorTools.WebGLBuildScript.Build
```

- 出力先は既定で `Builds/WebGL/`(`-buildOutput <path>` で変更可)
- Player Settings(IL2CPP、Decompression Fallback、メモリ設定)は `WebGLBuildScript.Build()` 内で毎回自動適用されるため、Editor 上で事前に手動設定する必要はない
- 実行前に Unity Editor で同じプロジェクトを開いたままにしない(ロック競合のため)

### ローカル動作確認

`Builds/WebGL/` はブラウザの `file://` では動作しないため、簡易 HTTP サーバーで配信して確認する。

```
cd Builds/WebGL
python -m http.server 8000
```

`http://localhost:8000` を開き、初期ワールドセルがロードされフレーム予算内で操作できることを確認する(タスク 4 参照)。

## Cloudflare Pages への公開(既定・無料・帯域無制限)

1. https://dash.cloudflare.com でアカウント作成、Workers & Pages → Create → Pages → Upload assets でプロジェクト作成(初回は手動アップロードで OK)
2. `Builds/WebGL/` フォルダの中身をそのままアップロード(zip 化不要。ディレクトリごとドラッグ&ドロップ)
3. 発行された `*.pages.dev` の URL で実際に読み込み・操作できることを確認する
4. CI から自動デプロイする場合は API トークンを発行する(Cloudflare ダッシュボード → My Profile → API Tokens → Create Token → "Cloudflare Pages" 編集権限のみのスコープに絞る)

Cloudflare Pages は圧縮配信・HTTPS 配信を自動処理するため追加のサーバー設定は不要。加えてカスタムレスポンスヘッダー(`_headers` ファイル)にも対応しているため、将来 COOP/COEP ヘッダーを追加して WebGL Threading Support を有効化する場合もホスティングの変更なしで対応できる(design.md D5/D2 Open Questions 参照)。

## 代替ホスティング

### itch.io

1. https://itch.io でアカウント作成、ダッシュボードから新規プロジェクト作成(kind of project = HTML5)
2. `Builds/WebGL/` フォルダを zip 化してアップロード、「This file will be played in the browser」を有効にする

ゲーム配信プラットフォームとしての発見性を重視する場合や、zip アップロードのみで完結させたい場合に選択する。

### GitHub Pages

1. `Builds/WebGL/` の内容を `gh-pages` ブランチ、または `docs/` 配下に配置してプッシュ
2. リポジトリの Settings → Pages で公開元ブランチ/フォルダを設定

GitHub Pages はカスタムレスポンスヘッダーを設定できないが、ビルド時に Decompression Fallback を有効化しているため(`WebGLPlayerSettingsSetup.Apply()`)、圧縮ヘッダー未設定でも問題なく動作する。

## CI/CD(GitHub Actions)

`.github/workflows/webgl-ci-cd.yml` が以下を自動化する。

- **PR / push(全ブランチ→main向けPRを含む)**: EditMode テスト実行 → WebGL ビルド。失敗時はチェックが red になる
- **main への push/merge のみ**: テスト・ビルド成功を条件に Cloudflare Pages へ自動公開

### 必要な GitHub Secrets / Variables

| 種類 | 名前 | 用途 |
|---|---|---|
| Secret | `UNITY_LICENSE` | Unity Personal ライセンスの `.ulf` ファイル内容(下記手順で取得) |
| Secret | `UNITY_EMAIL` | Unity アカウントのメールアドレス |
| Secret | `UNITY_PASSWORD` | Unity アカウントのパスワード |
| Secret | `CLOUDFLARE_API_TOKEN` | Cloudflare の [API トークン](https://dash.cloudflare.com/profile/api-tokens)(Pages 編集権限のみ) |
| Secret | `CLOUDFLARE_ACCOUNT_ID` | Cloudflare ダッシュボード右側に表示される Account ID |
| Secret | `CLOUDFLARE_PROJECT_NAME` | Cloudflare Pages のプロジェクト名 |

Pro ライセンス(シリアル)を使う場合は `UNITY_LICENSE` の代わりに `UNITY_SERIAL` を使うようワークフローを変更する([game-ci/unity-builder](https://game.ci/docs/github/getting-started) 参照)。

### `.ulf` ライセンスファイルの取得(Personal ライセンスの場合)

`game-ci/unity-request-activation-file@v2` アクションで activation file を発行し、[license.unity3d.com](https://license.unity3d.com/manual) でアクティベートして `.ulf` を取得する。手順は https://game.ci/docs/github/activation を参照。

## 前提条件・未確定事項

- リポジトリに GitHub リモートが未設定の場合は先に作成し、上記 Secrets/Variables を登録する
- Unity ライセンス種別(Personal/Pro)によって CI 設定が変わるため、実施前に確定させる
- 初回リリースはワールド規模(セル数・Terrain タイル数)を絞った縮小スコープを想定(`design.md` の Open Questions 参照)
