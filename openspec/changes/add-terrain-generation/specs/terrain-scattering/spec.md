# terrain-scattering Spec Delta

## ADDED Requirements

### Requirement: ルールベース散布

システムはバイオームごとの散布ルール (対象プレハブ/プロトタイプ、密度、傾斜範囲、高度範囲、ノイズマスク、最小間隔) に従い、植生・岩を決定論的に配置しなければならない (SHALL)。同一シードからは同一の配置が再現されなければならない (MUST)。

#### Scenario: バイオーム別の散布
- **WHEN** 草原バイオームに草と樹木、岩場バイオームに岩の散布ルールを定義して散布する
- **THEN** 各バイオーム領域に対応するオブジェクトのみが配置される

#### Scenario: 傾斜制限
- **WHEN** 樹木ルールに傾斜上限 30° を設定して散布する
- **THEN** 崖面には樹木が配置されない

#### Scenario: 散布の決定論
- **WHEN** 同一シードで散布を 2 回実行する
- **THEN** 配置結果が一致する

### Requirement: 出力先の使い分け

散布は対象に応じて出力先を使い分けなければならない (SHALL): 草・小型ディテールは Terrain Detail、樹木は Terrain Tree、大型プレハブ (岩・遺跡等) は OpenWorldRegion 直下への配置 (既存セルベイクの入力)。プレハブ散布物には `GeneratedScatterTag` が付与されなければならない (SHALL)。

#### Scenario: 大岩のプレハブ散布
- **WHEN** 大岩プレハブの散布ルールで散布する
- **THEN** OpenWorldRegion 直下に GeneratedScatterTag 付きで配置され、通常のセルベイク対象になる

### Requirement: 再散布時の掃除

再散布時、システムは `GeneratedScatterTag` 付きオブジェクトのみを削除して再配置しなければならない (MUST)。手動配置されたオブジェクトを削除してはならない (MUST)。

#### Scenario: 手動配置の保護
- **WHEN** 散布結果の隣に手動でオブジェクトを配置し、再散布する
- **THEN** 生成分のみ置き換わり、手動配置は残る

### Requirement: 抑制マスクの尊重

散布は Feature 由来の抑制マスク (道・川・POI) およびロックマスクの領域に配置してはならない (MUST)。

#### Scenario: 道の上に木が生えない
- **WHEN** 道を横切る領域に樹木を散布する
- **THEN** 道幅 + 法面の範囲には樹木が配置されない
