namespace Hikan.Core
{
    /// <summary>
    /// 門柱・ゲート操作台。函体の左右の側壁の上に門柱を 1 本ずつ立て(左右同一)、両柱にまたがる操作台の版を載せる。
    /// ゲート溝(戸当り)・頂版の開口は作らない。
    ///
    ///   正面(下流を見る)                          側面
    ///   ┌───────────────────┐ ← 操作台(厚 td)        ├as┤      ├as┤
    ///   └─┬──┬───────────┬──┬─┘                       ┌──────────────┐ ← 操作台
    ///     │  │           │  │  ↑                      └──┬────────┬──┘
    ///     │門│           │門│  Hc(門柱天端高 =          │  門柱  │  Hc
    ///     │柱│           │柱│     操作台下面、Z=0 から)  │        │
    ///   ┌─┴──┴───────────┴──┴─┐ ← 頂版上面          ────┴────────┴──── 頂版上面
    ///   │ ┌───────────────┐ │                            ├── Wc ──┤
    ///   │ │      内空      │ │                        d = 川裏函体端から門柱の中心まで
    ///   │ └───────────────┘ │
    ///   └───────────────────┘
    ///     ├Tc┤ 門柱厚(内面 = 内空側面)    操作台は門柱の外面から左右に ax 張り出す
    /// </summary>
    public sealed class HikanGateTower
    {
        /// <summary>設置するか(既定は設置する)。キーが無い旧 XData は設置しないとして読む。</summary>
        public bool Exists { get; set; } = true;
        /// <summary>d: 川裏函体端から門柱の中心までの距離(底版下面に沿う斜距離)[m]</summary>
        public decimal Position { get; set; } = 17.000m;
        /// <summary>Tc: 門柱厚(横断方向、内面 = 函体の内空側面)[m]</summary>
        public decimal ColumnThickness { get; set; } = 0.600m;
        /// <summary>Wc: 門柱幅(函体軸方向)[m]</summary>
        public decimal ColumnWidth { get; set; } = 1.000m;
        /// <summary>Hc: 門柱天端高 = 操作台下面の高さ(函体底版下面 Z = 0 から)[m]</summary>
        public decimal CrownHeight { get; set; } = 7.000m;
        /// <summary>td: 操作台厚 [m]</summary>
        public decimal DeckThickness { get; set; } = 0.400m;
        /// <summary>ax: 操作台の左右の張出し(門柱の外面から)[m]</summary>
        public decimal DeckSideOverhang { get; set; } = 0.500m;
        /// <summary>as: 操作台の前後の張出し(門柱の川裏側・川表側の面から)[m]</summary>
        public decimal DeckAxialOverhang { get; set; } = 0.500m;

        /// <summary>門柱の S 範囲 = 軸位置 ± 門柱幅/2</summary>
        public (decimal Min, decimal Max) ColumnRange
        {
            get { return (Position - ColumnWidth / 2m, Position + ColumnWidth / 2m); }
        }

        /// <summary>操作台の S 範囲 = 門柱の範囲 ± 前後の張出し。門柱を含むので、門柱・操作台全体の S 範囲でもある。</summary>
        public (decimal Min, decimal Max) DeckRange
        {
            get
            {
                (decimal Min, decimal Max) c = ColumnRange;
                return (c.Min - DeckAxialOverhang, c.Max + DeckAxialOverhang);
            }
        }

        public void Write(System.Collections.Generic.Dictionary<string, string> d, string prefix)
        {
            System.Globalization.CultureInfo inv = System.Globalization.CultureInfo.InvariantCulture;
            d[prefix + "exists"] = Exists ? "1" : "0";
            d[prefix + "position"] = Position.ToString(inv);
            d[prefix + "column_thickness"] = ColumnThickness.ToString(inv);
            d[prefix + "column_width"] = ColumnWidth.ToString(inv);
            d[prefix + "crown_height"] = CrownHeight.ToString(inv);
            d[prefix + "deck_thickness"] = DeckThickness.ToString(inv);
            d[prefix + "deck_side_overhang"] = DeckSideOverhang.ToString(inv);
            d[prefix + "deck_axial_overhang"] = DeckAxialOverhang.ToString(inv);
        }

        /// <summary>キーが無い項目は既定値のまま残す。ただし設置有無のキーが無い旧 XData は門柱を持たないので「設置しない」として読む。</summary>
        public void Read(
            System.Collections.Generic.IDictionary<string, string> d,
            string prefix,
            System.Func<System.Collections.Generic.IDictionary<string, string>, string, decimal, decimal> get)
        {
            Exists = get(d, prefix + "exists", 0m) != 0m;
            Position = get(d, prefix + "position", Position);
            ColumnThickness = get(d, prefix + "column_thickness", ColumnThickness);
            ColumnWidth = get(d, prefix + "column_width", ColumnWidth);
            CrownHeight = get(d, prefix + "crown_height", CrownHeight);
            DeckThickness = get(d, prefix + "deck_thickness", DeckThickness);
            DeckSideOverhang = get(d, prefix + "deck_side_overhang", DeckSideOverhang);
            DeckAxialOverhang = get(d, prefix + "deck_axial_overhang", DeckAxialOverhang);
        }
    }
}
