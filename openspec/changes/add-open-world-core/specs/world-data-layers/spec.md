# world-data-layers Spec Delta

## ADDED Requirements

### Requirement: データレイヤー定義

システムは `DataLayerAsset`(ScriptableObject)によりデータレイヤーを定義できなければならない (SHALL)。各レイヤーは名前・初期状態(Enabled/Disabled)・説明を持つ。オブジェクトへの割当は `DataLayerTag` コンポーネントで行い、1 オブジェクトに複数レイヤーを割当可能でなければならない (SHALL)。

#### Scenario: レイヤーの作成と割当
- **WHEN** レイヤー `QuestA` を作成しオブジェクトに `DataLayerTag` で割り当ててベイクする
- **THEN** セルプレハブ内で当該オブジェクトが `QuestA` レイヤーのサブツリーに分類される

### Requirement: レイヤーによる生成制御

セルのインスタンス化時、無効なレイヤーのみに属するオブジェクトは生成またはアクティブ化されてはならない (MUST)。複数レイヤーに属するオブジェクトは、いずれか 1 つのレイヤーが有効であれば生成されなければならない (SHALL)。

#### Scenario: 無効レイヤーの抑制
- **WHEN** `QuestA` が Disabled の状態でセルをアクティベートする
- **THEN** `QuestA` のみに属するオブジェクトはワールドに現れない

#### Scenario: 複数レイヤーの OR 評価
- **WHEN** `QuestA`(Disabled) と `Village`(Enabled) の両方に属するオブジェクトを含むセルをアクティベートする
- **THEN** 当該オブジェクトは生成される

### Requirement: ランタイムレイヤー切替

システムはランタイムにレイヤーの有効/無効を切替える API を提供しなければならない (SHALL)。切替はロード済み・アクティブ済みセルへ即時反映されなければならない (SHALL)。切替反映もフレーム予算制御の対象としなければならない (SHALL)。

#### Scenario: ランタイム有効化の即時反映
- **WHEN** アクティブなセル圏内で `QuestA` を Enabled に切替える
- **THEN** 当該レイヤーのオブジェクトが予算内で順次出現する

#### Scenario: ランタイム無効化
- **WHEN** `QuestA` を Disabled に切替える
- **THEN** 当該レイヤーのみに属するオブジェクトが非アクティブ化される
