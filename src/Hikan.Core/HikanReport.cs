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

            Add(r, "outer_width", F(e.OuterWidth), "m", "確定");
            Add(r, "outer_height", F(e.OuterHeight), "m", "確定");
            Add(r, "invert_level", F(e.InvertLevel), "m", "確定");
            Add(r, "outer_section_area", F(e.OuterSectionArea), "m2", "確定");
            Add(r, "inner_section_area", F(e.InnerSectionArea), "m2", "確定");
            Add(r, "concrete_section_area", F(e.ConcreteSectionArea), "m2", "確定");
            Add(r, "top_slab_volume", F(e.TopSlabVolume), "m3", "確定");
            Add(r, "bottom_slab_volume", F(e.BottomSlabVolume), "m3", "確定");
            Add(r, "wall_volume", F(e.WallVolume), "m3", "確定");
            Add(r, "concrete_volume", F(e.ConcreteVolume), "m3", "確定");
            Add(r, "horizontal_projection", F(e.HorizontalProjection), "m", "確定");
            Add(r, "drop_height", F(e.DropHeight), "m", "確定");

            Add(r, "formwork_inner", F(e.FormworkInner), "m2", "概算");
            Add(r, "formwork_outer_side", F(e.FormworkOuterSide), "m2", "概算");
            Add(r, "formwork_top", F(e.FormworkTop), "m2", "概算");
            Add(r, "formwork_end", F(e.FormworkEnd), "m2", "概算");

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
            "対象範囲: ボックスカルバート型 1 連の函体(頂版・側壁・底版)のみ。胸壁・しゃ水壁・翼壁・門柱・ゲート操作台・水叩き・護床工は対象外。",
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
