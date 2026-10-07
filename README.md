# 樋管(ボックスカルバート型 函体)パラメトリックモデル + 数量

『土木構造物設計マニュアル(案)－樋門編－(平成13年12月)』に基づく場所打ち鉄筋コンクリート樋管の
**函体**(頂版・側壁・底版)の 3D モデル(Solid3d 1個)生成と、ジオメトリから確定的に出せる数量の計算を行う DLL 群。
単位はメートル統一。

> **状態**: Core(ジオメトリ・整合性・数量)は **xunit 53 ケース全合格**。
> AutoCAD / Dynamo 層は **参照DLLバージョン未検証**(Windows 実機未確認)。
> ただし Autodesk 公式 NuGet(AutoCAD.NET 25.0.1 / DynamoVisualProgramming.DynamoServices 3.3.1)の
> 参照アセンブリに対して **コンパイル検証済み**(0 エラー・0 警告)なので、API の存在とシグネチャは確認できている。

## 1. 概略図

```
   断面(XZ 平面)                               延長方向(+Y = 上流 → 下流)
   ┌─────────────────┐  ← 頂版厚              ┌──────────────────────┐
   │ ╔═════════════╗ │                        │                      │  ← 土かぶり
   │ ║             ║ │                        │  ╔════════════════╗  │
   │ ║  内空 B × H ║ │  ← 側壁厚(両側)       │  ║                ║  │
   │ ║             ║ │                        │  ╚════════════════╝  │
   │ ╚═════════════╝ │                        └──────────────────────┘
   └─────────────────┘  ← 底版厚               ├── 函体延長 L(斜距離)──┤
   ├─ 外形幅 B_out ──┤                         底版勾配 i で下流が落差ぶん下がる
   ●(0,0) = 底版下面 かつ 函体中心線

        +Z (鉛直上向き)
         │
         │     X = 横断方向。X = 0 が函体中心線(左右対称軸)。下流を向いて右が +X。
   基準点 ●────── +Y (施設延長方向。上流 → 下流)
        ╱        Y = X × Z の右手系。
      +X
   基準点 (base_x, base_y, base_z) = 上流端・底版下面・函体中心線上の点。
   底版勾配の回転軸もこの点を通る X 軸なので、上流端の高さが不動点になる。
```

生成手順: **外形/内空の断面ポリライン → Region 2 個 → `BoolSubtract` で穴あき Region 1 個 → `Extrude` 1 回**
→ X 軸まわり +90 度回転 → 基準点へ平行移動 → 底版勾配の回転。
Region 段階で内空を抜くため、**Solid3d は構成上最初から 1 個**で、集約忘れが原理的に起こらない。

## 2. 参照アセンブリ

| プロジェクト | 参照 | 期待バージョン | Private |
|---|---|---|---|
| Hikan.Core | なし(net8.0) | ― | ― |
| Hikan.AutoCAD | AcCoreMgd.dll / AcDbMgd.dll / AcMgd.dll | 25.x(**未検証**) | False |
| Hikan.AutoCAD | AeccDbMgd.dll | 第1段階では不使用(参照しない) | ― |
| Hikan.Dynamo | DynamoServices.dll | 3.3.x(**未検証**) | False |
| Hikan.Core.Tests | xunit 2.9.2 / Microsoft.NET.Test.Sdk 17.11.1 / xunit.runner.visualstudio 2.8.2 | ― | ― |

検証: Windows 側で `scripts/verify-dll-versions.ps1` を実行。**NETLOAD の前に必ず実行すること**(バージョン不一致は
`TypeLoadException` / `MissingMethodException` を実行時に起こし、ビルド成功では検出できない)。

## 3. AutoCAD / Civil 3D コマンド(`NETLOAD` で `Hikan.AutoCAD.dll` を読み込む)

| コマンド | 説明 |
|---|---|
| `HIKAN_Create` | 基準点・パラメータ入力 → Solid3d 1個生成 → XData(`HIKAN_PARAM`)記録 → レイヤー「樋管函体」・色設定 |
| `HIKAN_Action` | 既存ソリッド選択 → XData から現在値復元 → 再入力(基準点は聞かない)→ 同位置再生成(旧ソリッドは消去、ハンドルは変わる) |
| `HIKAN_Query` | XData 読取 → 諸元・派生量・数量を信頼度ラベル付きで出力 → 根拠不足項目の注記を出力 |
| `HIKAN_SelfTest` | 頂版厚 0.4 / 底版厚 0.6、基準点 (1.5, 2.5, 0.75) で勾配 0 と 0.02 の 2 ケースを生成 → 下記を確認 → 消去 |

`HIKAN_SelfTest` が確認すること:

1. **体積** = (外形 − 内空) × L(±0.001 m3)
2. **内空控除** = 体積が外形プリズム体積より小さい(内空が実際に抜けている)
3. **3軸エクステント** = `HikanGeometry.ExpectedExtents` と一致(押出し方向・回転符号・基準点オフセットの検査)
4. **3成分重心** = `HikanGeometry.ExpectedCentroid` と一致
5. XData 往復(全キー一致)・レイヤー名・色

**4 が必須である理由**: 体積と 3 軸エクステントは**断面の上下反転では変化しない**(頂版厚 ≠ 底版厚 の函体を
上下反転してもバウンディングボックスと体積は完全に一致する)。回転符号を誤ったときに起きるのがまさにこの誤りなので、
重心の照合がないと検出できない。SelfTest が頂版厚 ≠ 底版厚 かつ基準点 Z ≠ 0 で走るのも同じ理由。

生成時検証は `tr.Commit()` の前に実行するので、**不一致の場合は図面に何も残らない**。

## 4. Civil 3D Dynamo ノード

`Hikan.Dynamo.dll` を Dynamo のパッケージ/ライブラリに追加。ノード: **`Hikan.Dynamo.HikanNodes.Estimate`**

グラフ配線イメージ:

```
[Number Slider ×11]──┐                          外形幅_m / 外形高_m / 敷高_m
[Integer Slider]─────┼──▶ HikanNodes.Estimate ──▶ コンクリート計_m3 / 頂版_m3 / 底版_m3 / 側壁_m3
                     │                          型枠_内空_m2 / 型枠_外側面_m2 / 型枠_頂版上面_m2 / 型枠_端面_m2
                     └                          水平投影長_m / 落差_m / ブロック長_m / ブロック当りコンクリート_m3
                                                床掘り_m3 / 埋戻_m3 / 残土_m3
```

| 入力 | 既定値 | 備考 |
|---|---|---|
| innerWidth / innerHeight | 2.0 / 2.0 | 内空 B / H [m] |
| wallThickness / topSlabThickness / bottomSlabThickness | 0.4 / 0.4 / 0.4 | 部材厚 [m] |
| barrelLength | 20.0 | 函体延長 L(斜距離)[m] |
| bottomSlope | 0.0 | 底版勾配 i(下流下がり) |
| soilCover | 2.0 | 土かぶり [m] |
| blockCount | 4 | ブロック数(按分表示のみ) |
| excavationMargin / excavationSlope | 0.5 / 0.5 | 床掘り余裕幅 [m] / 法勾配 1:n の n |
| foundationThickness | 0.1 | 基礎材厚 [m] |

モデルを作らず数量だけ返すノードなので `color_index` / `base_*` は引数に持たない。

## 5. 入力パラメータ

「基準」= 文書由来でエラー停止に使う条件。「目安」= 実務上の目安で強制しない。

| 英語名 | 日本語名 | 単位 | デフォルト値 | 範囲 |
|---|---|---|---|---|
| inner_width | 内空幅 B | m | 2.000 | > 0、≤ 3.0(基準: 適用範囲 内空断面 3.0 m 程度以下) |
| inner_height | 内空高 H | m | 2.000 | > 0、≤ 3.0(基準) |
| wall_thickness | 側壁厚 | m | 0.400 | ≥ 0.40 かつ 0.10 の倍数(基準) |
| top_slab_thickness | 頂版厚 | m | 0.400 | ≥ 0.40 かつ 0.10 の倍数(基準) |
| bottom_slab_thickness | 底版厚 | m | 0.400 | ≥ 0.40 かつ 0.10 の倍数(基準) |
| barrel_length | 函体延長 L(斜距離) | m | 20.000 | > 0(目安 5〜100) |
| bottom_slope | 底版勾配 i(下流下がり) | ― | 0.000 | ≥ 0(目安 0〜0.05) |
| soil_cover | 土かぶり | m | 2.000 | ≥ 0、≤ 10.0(基準: 適用範囲 土かぶり 10 m 程度以下) |
| block_count | ブロック数 | ― | 4 | ≥ 1 の整数(形状には反映しない) |
| excavation_margin | 床掘り余裕幅(片側) | m | 0.500 | ≥ 0(目安 0〜2) |
| excavation_slope | 床掘り法勾配 1:n の n | ― | 0.500 | ≥ 0(目安 0〜1.5) |
| foundation_thickness | 基礎材厚(均しコン等) | m | 0.100 | ≥ 0(目安 0〜0.5) |
| color_index | ソリッド色(ACI) | ― | 3 | 0〜256 |
| base_x / base_y / base_z | 基準点(上流端・底版下面・中心線) | m | 0 / 0 / 0 | `HIKAN_Create` で指定 |

**参照文書から基準として強制できるのは上表の「基準」印の 3 種のみ**。内空寸法・部材厚・函体延長・勾配・
ブロック割の算定式や標準値表はルート直下の参照文書(いずれも要約)に記載がないため、すべて入力パラメータとしている。

## 6. 計算値(自動算出)

| 英語名 | 計算式 | デフォルト値 | 信頼度 |
|---|---|---|---|
| outer_width | `B + 2 × 側壁厚` | 2.800 m | 確定 |
| outer_height | `H + 頂版厚 + 底版厚` | 2.800 m | 確定 |
| invert_level | `base_z + 底版厚`(敷高。基準点は底版**下面**なので混同しない) | 0.400 m | 確定 |
| outer_section_area | `outer_width × outer_height` | 7.840 m2 | 確定 |
| inner_section_area | `B × H` | 4.000 m2 | 確定 |
| concrete_section_area | 外形 − 内空 | 3.840 m2 | 確定 |
| top_slab_volume | `outer_width × 頂版厚 × L` | 22.400 m3 | 確定 |
| bottom_slab_volume | `outer_width × 底版厚 × L` | 22.400 m3 | 確定 |
| wall_volume | `2 × 側壁厚 × H × L` | 32.000 m3 | 確定 |
| concrete_volume | `concrete_section_area × L`(= 部位別の和) | 76.800 m3 | 確定 |
| horizontal_projection | `L / √(1 + i²)` | 20.000 m | 確定 |
| drop_height | `L × i / √(1 + i²)` | 0.000 m | 確定 |
| formwork_inner | `2 × (B + H) × L` | 160.000 m2 | 概算 |
| formwork_outer_side | `2 × outer_height × L` | 112.000 m2 | 概算 |
| formwork_top | `outer_width × L` | 56.000 m2 | 概算 |
| formwork_end | `2 × concrete_section_area` | 7.680 m2 | 概算 |
| block_length | `L / block_count` | 5.000 m | 確定 |
| concrete_per_block | `concrete_volume / block_count` | 19.200 m3 | 確定 |
| excavation_bottom_width | `outer_width + 2 × 余裕幅` | 3.800 m | 概算 |
| excavation_bottom_length | `L + 2 × 余裕幅` | 21.000 m | 概算 |
| excavation_depth `d` | `土かぶり + outer_height + 基礎材厚` | 4.900 m | 概算 |
| excavation_volume | `W_b·L_b·d + n·d²·(W_b+L_b) + (4/3)·n²·d³` | 727.960 m3 | 推定 |
| foundation_volume | `W_b × L_b × 基礎材厚` | 7.980 m3 | 推定 |
| occupied_volume | `outer_width × outer_height × L`(地下占有) | 156.800 m3 | 確定 |
| backfill_volume | `excavation_volume − occupied_volume − foundation_volume` | 563.180 m3 | 推定 |
| surplus_volume | `occupied_volume + foundation_volume` | 164.780 m3 | 推定 |

- 丸めは `decimal` + `MidpointRounding.AwayFromZero`(四捨五入)で小数3位止め(= 1 mm 単位)。
- 床掘りは**四方に法勾配 1:n を付けた角錐台の厳密積分** `∫₀^d (W_b+2nz)(L_b+2nz) dz` の展開。
  `n = 0` で直方体 `W_b·L_b·d` に退化することをテストで確認している(391.020 m3)。
- 埋戻は**外形プリズム**を控除する。コンクリート体積で控除すると内空を埋戻し可能な空間として数えてしまう。
- デフォルト値は Python の `Decimal` で独立計算してクロスチェックし、xunit にハードコードしている。

## 7. ビルド方法

`Hikan.Core` と `Hikan.Core.Tests` は AutoCAD 非依存(`net8.0`)なので Linux / WSL でもテストまで実行できる。

```bash
dotnet test tests/Hikan.Core.Tests -c Release
```

AutoCAD / Dynamo 層は Autodesk 製品のインストール先を参照するため Windows 側でビルドする。

```powershell
# 1. 参照DLLバージョン検証(不一致ならここで停止)
powershell -File scripts\verify-dll-versions.ps1
# 2. AutoCAD / Dynamo 層(インストール先が異なる場合は -p:AcadDir / -p:DynamoCoreDir で上書き)
dotnet build src\Hikan.AutoCAD -c Release
dotnet build src\Hikan.Dynamo -c Release
```

配布時は `Hikan.AutoCAD.dll` / `Hikan.Dynamo.dll` と `Hikan.Core.dll` を同じフォルダに置く(AutoCAD 本体 DLL は含めない)。

## 8. 規約・制約

- `using` ディレクティブ不使用(完全修飾名)、mm 不使用(メートル統一)、Z は上向き、AutoCAD 系 DLL は Private=False。
- 整合性チェック違反は `HikanValidationException` でエラー停止し、再生成しない(許容誤差 1 mm = 0.001 m)。
  長さ・面積・体積で許容誤差の定数を分けている。
- **前提**: ボックスカルバート型 **1 連**の**函体のみ**。胸壁・しゃ水壁・翼壁・門柱・ゲート操作台・水叩き・護床工は対象外。
- **既知の制約(底版勾配)**: 勾配は押出し後の剛体回転で表現するため、**上下流の端面が鉛直にならない**。
  勾配が 1/100 程度までなら実用上の影響は小さい。回転は体積を保存するのでコンクリート数量には影響しない。
  端面を鉛直に保つには**せん断変換**(`z' = z + i·y`、行列式 1 なので同じく体積保存)が幾何的に正しいが、
  `Solid3d.TransformBy` に非直交行列を渡したときの ACIS の挙動が未確認のため第2段階の課題とする。
- **既知の制約(ブロック割)**: `block_count` は形状に反映せず数量の按分表示のみ。継手の目地幅は控除していない。
- **既知の制約(型枠)**: 面積の内訳のみを出し合計は出さない。頂版上面・底版下面は計上せず、端面は両端に型枠が立つ前提。
  支保工は算定しない。どの面を計上するかは実務判断のため、文書に根拠がない合計の自動化はしない。
- **既知の制約(床掘り)**: 勾配 0 の水平床掘りとして概算する。土留め工(自立・切梁)を用いる場合は鉛直掘削になり
  角錐台の式が成立しない。土量換算係数(ほぐし・締固め)、流用土/購入土の区分、裏込め材の区分は未対応。
- **対象外**(第2段階以降): 鉄筋・ユニット鉄筋、目地・止水板、足場・支保工、基礎杭、残土処理、
  しゃ水工・グラウトホール、管理橋、ゲート設備、プレキャスト函渠、円形管(樋管)、多連ボックス、歩掛・金額。
- **参照文書の根拠不足**: ルート直下の参照文書 4 本はいずれも**原典ではなく要約**で、
  『数量集計表様式(樋門・樋管)』には本体工のコンクリート・型枠・鉄筋の**細別名・規格・積算単位が収録されていない**。
  本 DLL の出力は自前の項目名・単位による数量であり、数量集計表の様式にそのまま貼れる保証はない。
  様式に合わせるには原典 Excel(平成20年4月版)が必要。
- 座標系は `CLAUDE.PRIVATE.md` §2.2 の定義に従う(上記 1. と同一)。同表には X 軸の定義行が欠落し、
  Y 行の備考が「法線平行方向」のままだったため、X 軸の行の追加と Y 行の「法線直交方向」への修正を行っている。

## 9. 既存 README.md との差分

- 樋管(`Hikan.*`)のみを扱う構成に全面刷新した。
- 以前の README.md に記載されていた別構造物の節は、対象外となったため記載・プロジェクト一式ともに全削除した。
- ソリューションファイルは `Hikan.sln`(新規作成)。旧ソリューションは行末が `CR CR LF` で
  MSBuild が `error MSB5010` を出す状態だったため、作り直しにより解消している。
