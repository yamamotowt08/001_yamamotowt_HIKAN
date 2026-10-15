namespace Hikan.Core
{
    /// <summary>出力1行分。信頼度は 入力 / 確定 / 概算 / 推定。</summary>
    public sealed class HikanItem
    {
        public string EnglishName { get; set; }
        public string JapaneseName { get; set; }
        public string Value { get; set; }
        public string Unit { get; set; }
        public string Confidence { get; set; }
    }

    /// <summary>諸元・派生量の一覧を作る(HIKAN_Query 出力、README 表の根拠)。</summary>
    public static class HikanReport
    {
        private static string F(decimal v)
        {
            return v.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void Add(System.Collections.Generic.List<HikanItem> list, string name, string value, string unit, string confidence)
        {
            HikanItem item = new HikanItem();
            item.EnglishName = name;
            item.JapaneseName = HikanNames.Japanese[name];
            item.Value = value;
            item.Unit = unit;
            item.Confidence = confidence;
            list.Add(item);
        }

        private static void AddBreast(System.Collections.Generic.List<HikanItem> list, HikanBreastWall w, string prefix)
        {
            Add(list, prefix + "stem_thickness", F(w.StemThickness), "m", "入力");
            Add(list, prefix + "position", F(w.Position), "m", "入力");
            Add(list, prefix + "length", F(w.Length), "m", "入力");
            Add(list, prefix + "crown_height", F(w.CrownHeight), "m", "入力");
            Add(list, prefix + "footing_thickness", F(w.FootingThickness), "m", "入力");
            Add(list, prefix + "toe_length", F(w.ToeLength), "m", "入力");
            Add(list, prefix + "heel_length", F(w.HeelLength), "m", "入力");
        }

        private static void AddGate(System.Collections.Generic.List<HikanItem> list, HikanGateTower g)
        {
            Add(list, "gate_exists", g.Exists ? "設置する" : "設置しない", "", "入力");
            if (!g.Exists) { return; }
            Add(list, "gate_position", F(g.Position), "m", "入力");
            Add(list, "gate_deck_length", F(g.DeckLength), "m", "入力");
            Add(list, "gate_deck_width", F(g.DeckWidth), "m", "入力");
            Add(list, "gate_deck_axial_overhang", F(g.DeckAxialOverhang), "m", "入力");
            Add(list, "gate_deck_side_overhang", F(g.DeckSideOverhang), "m", "入力");
            Add(list, "gate_deck_thickness", F(g.DeckThickness), "m", "入力");
            Add(list, "gate_bridge", g.Bridge ? "する" : "しない", "", "入力");
            if (g.Bridge)
            {
                Add(list, "gate_seat_length", F(g.SeatLength), "m", "入力");
                Add(list, "gate_seat_width", F(g.SeatWidth), "m", "入力");
                Add(list, "gate_seat_depth", F(g.SeatDepth), "m", "入力");
            }
            Add(list, "gate_longitudinal_wall_width", F(g.LongitudinalWallWidth), "m", "入力");
            Add(list, "gate_transverse_wall_thickness", F(g.TransverseWallThickness), "m", "入力");
            Add(list, "gate_longitudinal_wall_thickness", F(g.LongitudinalWallThickness), "m", "入力");
            Add(list, "gate_transverse_wall_width", F(g.TransverseWallWidth), "m", "入力");
            Add(list, "gate_column_height", F(g.ColumnHeight), "m", "入力");
            Add(list, "gate_haunch_height", F(g.HaunchHeight), "m", "入力(形状・数量には未反映)");
        }

        private static void AddWing(System.Collections.Generic.List<HikanItem> list, HikanWingWall w, string prefix)
        {
            Add(list, prefix + "stem_thickness", F(w.StemThickness), "m", "入力");
            Add(list, prefix + "length", F(w.Length), "m", "入力");
            Add(list, prefix + "end_width", F(w.EndWidth), "m", "入力");
            Add(list, prefix + "crown_start", F(w.CrownStart), "m", "入力");
            Add(list, prefix + "crown_end", F(w.CrownEnd), "m", "入力");
            Add(list, prefix + "floor_thickness", F(w.FloorThickness), "m", "入力");
            HikanLeveeProtection g = w.Protection;
            Add(list, prefix + "protection_exists", g.Exists ? "設置する" : "設置しない", "", "入力");
            if (!g.Exists) { return; }
            Add(list, prefix + "protection_width", F(g.Width), "m", "入力");
            Add(list, prefix + "protection_stem_thickness", F(g.StemThickness), "m", "入力");
            Add(list, prefix + "protection_stem_height", F(g.StemHeight), "m", "入力");
            Add(list, prefix + "protection_toe_length", F(g.ToeLength), "m", "入力");
            Add(list, prefix + "protection_heel_length", F(g.HeelLength), "m", "入力");
        }

        public static System.Collections.Generic.List<HikanItem> Build(HikanParameters p, HikanEstimate e)
        {
            System.Collections.Generic.List<HikanItem> r = new System.Collections.Generic.List<HikanItem>();

            Add(r, "inner_width", F(p.InnerWidth), "m", "入力");
            Add(r, "inner_height", F(p.InnerHeight), "m", "入力");
            Add(r, "wall_thickness", F(p.WallThickness), "m", "入力");
            Add(r, "top_slab_thickness", F(p.TopSlabThickness), "m", "入力");
            Add(r, "bottom_slab_thickness", F(p.BottomSlabThickness), "m", "入力");
            Add(r, "barrel_length", F(p.BarrelLength), "m", "入力");
            Add(r, "bottom_slope", F(p.BottomSlope), "", "入力");
            Add(r, "soil_cover", F(p.SoilCover), "m", "入力");
            Add(r, "block_count", p.BlockCount.ToString(), "ブロック", "入力");
            Add(r, "excavation_margin", F(p.ExcavationMargin), "m", "入力");
            Add(r, "excavation_slope", F(p.ExcavationSlope), "", "入力");
            Add(r, "foundation_thickness", F(p.FoundationThickness), "m", "入力");
            AddBreast(r, p.UpstreamBreast, "upstream_breast_");
            AddBreast(r, p.DownstreamBreast, "downstream_breast_");
            AddWing(r, p.UpstreamWing, "upstream_wing_");
            AddWing(r, p.DownstreamWing, "downstream_wing_");
            Add(r, "cutoff_count", p.CutoffCount.ToString(), "枚", "入力");
            Add(r, "cutoff_thickness", F(p.CutoffThickness), "m", "入力");
            Add(r, "cutoff_projection", F(p.CutoffProjection), "m", "入力");
            AddGate(r, p.Gate);

            Add(r, "outer_width", F(e.OuterWidth), "m", "確定");
            Add(r, "outer_height", F(e.OuterHeight), "m", "確定");
            Add(r, "invert_level", F(e.InvertLevel), "m", "確定");
            Add(r, "outer_section_area", F(e.OuterSectionArea), "m2", "確定");
            Add(r, "inner_section_area", F(e.InnerSectionArea), "m2", "確定");
            Add(r, "concrete_section_area", F(e.ConcreteSectionArea), "m2", "確定");
            Add(r, "top_slab_volume", F(e.TopSlabVolume), "m3", "確定");
            Add(r, "bottom_slab_volume", F(e.BottomSlabVolume), "m3", "確定");
            Add(r, "wall_volume", F(e.WallVolume), "m3", "確定");
            Add(r, "barrel_concrete_volume", F(e.BarrelConcreteVolume), "m3", "確定");
            Add(r, "upstream_breast_volume", F(e.UpstreamBreastVolume), "m3", "確定");
            Add(r, "downstream_breast_volume", F(e.DownstreamBreastVolume), "m3", "確定");
            Add(r, "upstream_wing_volume", F(e.UpstreamWingVolume), "m3", "確定");
            Add(r, "downstream_wing_volume", F(e.DownstreamWingVolume), "m3", "確定");
            Add(r, "gate_column_volume", F(e.GateColumnVolume), "m3", "確定");
            Add(r, "gate_deck_volume", F(e.GateDeckVolume), "m3", "確定");
            Add(r, "upstream_protection_volume", F(e.UpstreamProtectionVolume), "m3", "確定");
            Add(r, "downstream_protection_volume", F(e.DownstreamProtectionVolume), "m3", "確定");
            Add(r, "upstream_wing_flare_angle", F(e.UpstreamWingFlareAngle), "度", "確定");
            Add(r, "downstream_wing_flare_angle", F(e.DownstreamWingFlareAngle), "度", "確定");
            Add(r, "cutoff_total_volume", F(e.CutoffTotalVolume), "m3", "確定");
            Add(r, "cutoff_spacing", F(e.CutoffSpacing), "m", "確定");
            Add(r, "seepage_path_length", F(e.SeepagePathLength), "m", "推定");
            Add(r, "concrete_volume", F(e.ConcreteVolume), "m3", "確定");
            Add(r, "horizontal_projection", F(e.HorizontalProjection), "m", "確定");
            Add(r, "drop_height", F(e.DropHeight), "m", "確定");

            Add(r, "formwork_inner", F(e.FormworkInner), "m2", "概算");
            Add(r, "formwork_outer_side", F(e.FormworkOuterSide), "m2", "概算");
            Add(r, "formwork_top", F(e.FormworkTop), "m2", "概算");
            Add(r, "formwork_end", F(e.FormworkEnd), "m2", "概算");
            Add(r, "formwork_breast", F(e.FormworkBreast), "m2", "概算");
            Add(r, "formwork_cutoff", F(e.FormworkCutoff), "m2", "概算");
            Add(r, "formwork_wing", F(e.FormworkWing), "m2", "概算");
            Add(r, "formwork_gate", F(e.FormworkGate), "m2", "概算");

            Add(r, "block_length", F(e.BlockLength), "m", "確定");
            Add(r, "concrete_per_block", F(e.ConcretePerBlock), "m3", "確定");

            Add(r, "excavation_bottom_width", F(e.ExcavationBottomWidth), "m", "概算");
            Add(r, "excavation_bottom_length", F(e.ExcavationBottomLength), "m", "概算");
            Add(r, "excavation_depth", F(e.ExcavationDepth), "m", "概算");
            Add(r, "excavation_volume", F(e.ExcavationVolume), "m3", "推定");
            Add(r, "foundation_volume", F(e.FoundationVolume), "m3", "推定");
            Add(r, "occupied_volume", F(e.OccupiedVolume), "m3", "確定");
            Add(r, "backfill_volume", F(e.BackfillVolume), "m3", "推定");
            Add(r, "surplus_volume", F(e.SurplusVolume), "m3", "推定");
            return r;
        }

        /// <summary>数量化できない前提・対象外を注記として出す(金額は算出しない)。</summary>
        public static readonly string[] Notes = new string[]
        {
            "対象範囲: ボックスカルバート型 1 連の函体(頂版・側壁・底版)+ 胸壁 + しゃ水壁 + 翼壁(+ 堤防保護部)+ 門柱・ゲート操作台。水叩き・護床工・ゲート設備・管理橋は対象外。",
            "翼壁は左右の側壁と底版(水路床)が一体になった U 字の 3 面張り水路で、函体端から外へ伸びます。側壁の内面は函体端で内空側面に、底版上面は函体の敷高に一致し、水路が函体の内空と段差なくつながります。開き角は翼壁端部の水路幅と内空幅の差と翼壁長(函体軸方向)から決まります(端部水路幅 = 内空幅で平行)。",
            "翼壁の側壁の天端は函体端から翼壁端部まで直線で変わり、壁の厚さ方向には水平です。側壁厚は壁面に直交する方向で測ります。底版は側壁の外面まで(張出しなし)です。始端・終端は函体端と平行な面で切っています。",
            "函体端近くの胸壁の底版が函体端より外へ出て翼壁(厚い底版など)と重なる場合、重なった部分は一体のコンクリートとして 1 回だけ数え、胸壁の数量に含めて翼壁の数量から差し引きます。",
            "胸壁は函体の左右側面にそれぞれ取り付く逆T字擁壁(たて壁 + 底版)としてモデル化しています。左右は同一形状で、入力は片側分です。底版上面は函体の敷高(内空底面)にそろえ、たて壁は敷高から立ち上がります。つま先版長またはかかと版長を 0 にすれば L 字になります。",
            "堤防保護部は翼壁の端部から左右へ張り出す逆T字擁壁(たて壁 + 底版、つま先版は水路床の下で左右つながる)としてモデル化しています。底版は上面 = 敷高、厚さ = 翼壁の底版厚です。翼壁と重なる部分は堤防保護部の数量に含め、翼壁の数量から差し引きます。堤防保護部の型枠は計上していません。",
            "門柱・ゲート操作台は、函体の左右の側壁の上に立つ L 字断面の門柱 2 本(内側の切欠きがゲートの溝、横方向壁は川裏側)と、両柱にまたがる操作台の版としてモデル化しています。門柱形式は 2 本柱、操作台形状形式は前後・左右に張り出す単純な版のみです。管理橋を設置する場合は操作台の川裏側の端に桁受けの切欠き(受け台)を作り、管理橋そのものは作りません。左柱ハンチ高 H2 は形と位置が未確定のため、形状・数量に入れていません。床掘りの平面範囲には含めず、埋戻しの控除は現地盤(頂版上面 + 土かぶり)より下の部分だけです。",
            "胸壁・翼壁は必ず設置します(たて壁厚・側壁厚 0.40 m 以上)。厚 0 は旧版で作った図面の「設置なし」としてのみ読み込みます。",
            "上流端を川裏とします。胸壁のたて壁は函体方向に垂直で、軸位置(川裏函体端からたて壁厚の中心までの斜距離)を中心に置き、函体延長の内側に収めます。函体の上は跨ぎません。",
            "胸壁のつま先版は近い方の函体端の側(川裏側胸壁は川裏、川表側胸壁は川表)、かかと版は函体中央の側に伸びます。つま先版は函体端より外へ出てもかまいません。",
            "しゃ水壁は函体外周に全周一律(下方にも)張り出すカラーとしてモデル化し、函体延長を枚数で等分した各区間の中央に配置します。",
            "胸壁・しゃ水壁の寸法・張出し量・設置位置は参照文書に規定がないため、全て入力値です。形状は『全部材が等厚矩形』(基準の形状単純化方針)のみを根拠にしています。",
            "浸透路長は函体外周に沿う経路長(延長 + 張出し量 × 2 × 枚数)です。レーン則等による必要浸透路長の照査は行いません(照査式が参照文書にないため)。",
            "基準点は上流端・底版下面・函体中心線上。敷高(内空底面高)は invert_level を参照(底版厚ぶん上)。",
            "底版勾配は押出し後の回転で表現するため、上下流の端面が鉛直になりません。コンクリート体積は回転で変わりません。",
            "ブロック数は形状に反映せず数量の按分表示のみです。継手の目地幅は控除していません(目地材・止水板も対象外)。",
            "型枠は面積の内訳のみを出し合計は出しません。頂版上面と底版下面は計上せず、端面は両端に型枠が立つ前提です。支保工は算定しません。",
            "床掘りは勾配 0 の水平床掘りとして四方に法勾配 1:n を付けた角錐台で概算します。土留め工を用いる場合この式は成立しません。",
            "埋戻は床掘りから外形プリズムの地下占有分と基礎材を控除した値です。土量換算係数(ほぐし・締固め)、流用土/購入土の区分、裏込め材の区分は未対応。",
            "鉄筋・目地・止水板・足場・基礎杭・残土処理の細別は算定しません。『数量集計表様式(樋門・樋管)』の原本(平成20年4月版 Excel)がないと細別名・規格・積算単位を確定できません。",
            "材料は σck = 24 N/mm2・SD345(基準)を前提としますが、材料規格の区分は数量に反映していません。"
        };
    }
}
