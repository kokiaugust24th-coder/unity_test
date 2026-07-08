# terrain-features Spec Delta

## ADDED Requirements

### Requirement: 道の生成

システムはシーンに配置された RoadFeature (スプライン) に沿って地形を整地しなければならない (SHALL): 中心帯をフラット化し、法面を smoothstep で周囲へ接続し、道用スプラットを上書きし、散布抑制マスクを出力する。縦断勾配が設定上限を超える区間は警告として報告されなければならない (SHALL)。

#### Scenario: スプラインに沿った整地
- **WHEN** 斜面を横切るスプラインに RoadFeature を設定して生成する
- **THEN** スプライン沿いに通行可能な平坦帯と法面が形成され、道テクスチャが適用される

#### Scenario: 急勾配の警告
- **WHEN** 縦断勾配が上限を超える区間を含むスプラインで生成する
- **THEN** 当該区間が警告として報告される

### Requirement: 川の生成

システムは RiverFeature (スプライン) に沿って河道を彫り込み、水面メッシュを生成しなければならない (SHALL)。幅・深さはスプライン上のノット単位で補間可能でなければならない (SHALL)。水面メッシュは OpenWorldRegion 直下に生成物タグ付きで配置され、既存のセルベイク (world-asset-pipeline) の対象としてストリーミングされなければならない (SHALL)。

#### Scenario: 河道と水面
- **WHEN** RiverFeature を配置して生成する
- **THEN** スプライン沿いに彫り込まれた河道と、それに沿う水面メッシュが生成される

### Requirement: POI スタンプ

システムは POIFeature (位置・半径・目標高度) の領域を平坦化し、周囲へ滑らかに接続し、散布抑制マスクを出力しなければならない (SHALL)。

#### Scenario: 拠点用の平坦地
- **WHEN** 斜面上に POIFeature を配置して生成する
- **THEN** 指定半径が平坦化され、建物を配置可能な土台が形成される

### Requirement: Feature の再生成追従

Feature (道・川・POI) の追加・移動・削除後の再生成では、地形が新しい Feature 配置へ追従しなければならない (SHALL)。Feature 由来の彫り込みは手動差分レイヤーより先に適用されなければならない (SHALL)。

#### Scenario: 道の移動
- **WHEN** スプラインを移動して再生成する
- **THEN** 旧経路の整地は消え、新経路に整地が現れる
