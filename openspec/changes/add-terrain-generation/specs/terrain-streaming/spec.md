# terrain-streaming Spec Delta

## ADDED Requirements

### Requirement: セル単位の Terrain タイル書き出し

システムはワールド高さマップをストリーミングセルと同一のグリッドで Unity Terrain タイル (TerrainData + タイルプレハブ) に分割し、Addressables 登録しなければならない (SHALL)。隣接タイルは境界頂点を共有し、隙間や段差が生じてはならない (MUST)。生成物は `Generated/Terrain/` 配下に置かれ、削除のみで完全にロールバックできなければならない (SHALL)。

#### Scenario: タイル分割
- **WHEN** 2km 四方 (セル 256m) のワールドを書き出す
- **THEN** 8x8 の TerrainData タイルが生成され、それぞれ `ow_terrain_{x}_{z}` で Addressables 登録される

#### Scenario: シームレスな境界
- **WHEN** 隣接する 2 タイルをロードして境界を確認する
- **THEN** 高さの不一致や隙間が存在しない

### Requirement: Terrain タイルのストリーミング

Terrain タイルは既存のセル状態機械 (Unloaded/Loading/Loaded/Activated) に従ってロード/アンロードされなければならない (SHALL)。アクティベート時、システムはロード済みの隣接タイルと `SetNeighbors` で接続し LOD 継ぎ目を解消しなければならない (SHALL)。

#### Scenario: タイルのロードと接続
- **WHEN** ソースが移動して新しいタイルが Activated になる
- **THEN** タイルが表示され、既にロード済みの隣接タイルと SetNeighbors で接続される

#### Scenario: タイルのアンロード
- **WHEN** タイルがソース圏外になる
- **THEN** Terrain と TerrainData のメモリが解放される

### Requirement: 遠景 Terrain HLOD

システムはタイルごとに低ポリゴンの遠景メッシュを生成しなければならない (SHALL)。遠景メッシュはベイク時にセルのコンテンツ HLOD と 1 つの HLOD プレハブへ統合され、既存の HLOD 機構 (HLOD 半径・スワップ規則) で表示されなければならない (SHALL)。セルごとの HLOD スロットは 1 つを維持し、機構を二重化してはならない (MUST)。

#### Scenario: 遠景の表示
- **WHEN** タイルがロード半径外かつ HLOD 半径内にある
- **THEN** 低ポリ地形メッシュが表示され、タイルのアクティベートでポップなくスワップされる

### Requirement: セルコンテンツ抽象化

WorldManifest のセルエントリは複数のコンテンツ (種別 + アドレスの組: Prefab / Terrain) を保持できなければならない (SHALL)。ランタイムは種別ごとのコンテンツハンドラへロード/アクティベートを委譲しなければならず (SHALL)、Terrain 側が独自のストリーミング判定・状態機械を持ってはならない (MUST)。Terrain を持たないセルは従来どおり動作しなければならない (SHALL)。

#### Scenario: 混在ワールド
- **WHEN** Terrain タイルとメッシュコンテンツの両方を持つセルをロードする
- **THEN** 両方が同じセル状態機械で一緒にロード/アンロードされる
