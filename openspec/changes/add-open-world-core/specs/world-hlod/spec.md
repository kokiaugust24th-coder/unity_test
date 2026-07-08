# world-hlod Spec Delta

## ADDED Requirements

### Requirement: HLOD ベイク

エディタのベイクパイプラインは、セルごとに静的メッシュを結合しマテリアルをアトラス化した HLOD メッシュを生成しなければならない (SHALL)。HLOD アセットは Addressable として登録され、セル本体とは独立にロード可能でなければならない (SHALL)。ベイクは差分実行(ダーティセルのみ)に対応しなければならない (SHALL)。

#### Scenario: セル HLOD の生成
- **WHEN** 静的メッシュを含むセルをベイクする
- **THEN** 結合メッシュとアトラスマテリアルを持つ HLOD プレハブが `Generated/HLOD/` に生成される

#### Scenario: 差分ベイク
- **WHEN** 1 セルのみ内容が変更された状態で差分ベイクを実行する
- **THEN** 変更セルの HLOD のみ再生成される

### Requirement: HLOD ランタイム表示制御

システムは HLOD 表示半径(既定: セルロード半径の 4 倍、設定可能)内かつセルが `Activated` でない領域に HLOD を表示しなければならない (SHALL)。セルが `Activated` に遷移する際は、実セル表示開始と HLOD 非表示化を同期させ、切替中に空白フレームが発生してはならない (MUST)。

#### Scenario: 遠景での HLOD 表示
- **WHEN** セルがロード半径外かつ HLOD 半径内にある
- **THEN** HLOD メッシュのみ表示される

#### Scenario: ポップのないスワップ
- **WHEN** セルが `Activated` に遷移する
- **THEN** 実セルの表示と同一フレーム以降に HLOD が非表示化され、両方欠けるフレームは存在しない

### Requirement: HLOD 品質設定

HLOD ベイクは `WorldPartitionConfig` の品質パラメータ(アトラス解像度、頂点削減率)に従わなければならない (SHALL)。HLOD を持たないセル(静的メッシュなし)はベイク・ランタイムともにスキップされなければならない (SHALL)。

#### Scenario: 空セルのスキップ
- **WHEN** 静的メッシュを含まないセルをベイクする
- **THEN** HLOD アセットは生成されず、ランタイムでも HLOD ロードが要求されない
