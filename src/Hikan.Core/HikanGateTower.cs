namespace Hikan.Core
{
    /// <summary>
    /// 門柱・ゲート操作台。函体の左右の側壁の上に門柱を 1 本ずつ立て(左右同一)、両柱にまたがる操作台の版を載せる。
    /// 記号は手本の樋門設計ソフトの「形状寸法」画面(操作台 / 門柱タブ)に合わせる。
    /// 門柱形式は 2 本柱、操作台形状形式は前後・左右に張り出す単純な版のみ(他の形式は未対応)。
    ///
    ///   正面(下流を見る)                              側面
    ///   ├──────────── X2 ────────────┤                 ├─────── X1 ───────┤
    ///   ┌────────────┬────┬────────────┐ ↕Y1           ┌────────────┬─W1─┐ ← 受け台(管理橋の桁受け切欠き、
    ///   └──┬──┬──────┴────┴──────┬──┬──┘               └──┬────┬──┬─┴────┘    深さ H1、正面で幅 W2、川裏側の端)
    ///   ├X6┤  │                  │  ├X6┤                ├X3┤    │  ├X3┤
    ///      │  │                  │  │  ↕Y2(柱高さ、頂版上面から)      │縦壁│横│
    ///   ───┴──┴──────────────────┴──┴─── 頂版上面          ├X4──┼X5┤  川表 ←→ 川裏
    ///      ├X7┼X8┤  内空幅 B   ├X8┼X7┤
    ///
    ///   門柱の平面(右の柱、上が川表):         ├X8┼X7┤
    ///                                       切欠き │▓▓│ X4  ← 縦方向壁(厚 X7)
    ///                                       ├──┼──┤
    ///                                       │▓▓│▓▓│ X5  ← 横方向壁(厚 X5、川裏側)
    ///   内面(X8 の内側の端)= 函体の内空側面。切欠き(X8 × X4)はゲートの通る溝。
    ///   ハンチ(H2)は縦方向壁の内面(切欠き側)と操作台下面の隅の三角(1:1)で、縦方向壁の区間(X4、川表側)に付く。
    ///   土留壁は左右の門柱の間(±B/2)をふさぐ壁で、頂版上面から高さ WH。背面は門柱の背面、厚さは X5(横方向壁と同じ)。
    ///   門柱の位置は入力しない。門柱の川裏側の面(背面)を川表側胸壁のたて壁の川裏側の面(背面)にそろえる
    ///   (HikanGeometry.GateColumnRange)。
    /// </summary>
    public sealed class HikanGateTower
    {
        /// <summary>設置するか(既定は設置する)。キーが無い旧 XData は設置しないとして読む。</summary>
        public bool Exists { get; set; } = true;

        /// <summary>X1: 操作台縦方向幅(函体軸方向)[m]。X4 + X5 + 2·X3 と一致すること。</summary>
        public decimal DeckLength { get; set; } = 2.700m;
        /// <summary>X2: 操作台横方向幅 [m]。B + 2·(X7 + X8) + 2·X6 と一致すること。</summary>
        public decimal DeckWidth { get; set; } = 5.800m;
        /// <summary>X3: 操作台縦方向張出長(門柱の川表側・川裏側の面から)[m]</summary>
        public decimal DeckAxialOverhang { get; set; } = 0.700m;
        /// <summary>X6: 操作台横方向張出長(門柱の外面から)[m]</summary>
        public decimal DeckSideOverhang { get; set; } = 1.100m;
        /// <summary>Y1: 操作台高さ(厚さ)[m]</summary>
        public decimal DeckThickness { get; set; } = 0.600m;

        /// <summary>管理橋を設置するか。設置するなら操作台の川裏側の端に桁受けの切欠き(受け台)を作る。管理橋そのものは作らない。</summary>
        public bool Bridge { get; set; } = true;
        /// <summary>W1: 操作台受け台縦方向幅(操作台の川裏側の端から)[m]</summary>
        public decimal SeatLength { get; set; } = 0.400m;
        /// <summary>W2: 操作台受け台横方向幅(中央)[m]</summary>
        public decimal SeatWidth { get; set; } = 1.400m;
        /// <summary>H1: 操作台受け台高さ(操作台上面からの切欠きの深さ)[m]</summary>
        public decimal SeatDepth { get; set; } = 0.300m;

        /// <summary>X4: 縦方向壁幅(切欠きのある区間の函体軸方向の長さ)[m]</summary>
        public decimal LongitudinalWallWidth { get; set; } = 0.900m;
        /// <summary>X5: 横方向壁厚(函体軸方向、川裏側)[m]</summary>
        public decimal TransverseWallThickness { get; set; } = 0.400m;
        /// <summary>X7: 縦方向壁厚(横断方向、外側)[m]</summary>
        public decimal LongitudinalWallThickness { get; set; } = 0.400m;
        /// <summary>X8: 横方向壁幅(横断方向、内側 = 切欠きの幅)[m]</summary>
        public decimal TransverseWallWidth { get; set; } = 0.400m;
        /// <summary>
        /// Y2: 柱高さ(門柱が立つ区間の函体の頂版上面から操作台下面まで)[m]。
        /// 門柱そのものは胸壁の底版の上面(敷高)から立ち、頂版上面より下の部分は函体の側壁・胸壁と一体になる。
        /// </summary>
        public decimal ColumnHeight { get; set; } = 4.000m;
        /// <summary>
        /// H2: ハンチ高 [m]。縦方向壁の内面(切欠き側)と操作台下面の隅に付く、高さ・幅とも H2 の三角(1:1)で、
        /// 縦方向壁の区間(X4、川表側の柱)に付ける。0 でハンチなし。
        /// </summary>
        public decimal HaunchHeight { get; set; } = 0.200m;
        /// <summary>
        /// WH: 土留壁高さ(頂版上面から)[m]。土留壁は左右の門柱の間(±内空幅/2)をふさぐ壁で、下端は頂版上面、
        /// 背面は門柱の背面(川裏側の面)、厚さは横方向壁厚 X5。0 で土留壁なし。上端は川表側胸壁のたて壁の天端以上、操作台の下面以下。
        /// </summary>
        public decimal RetainingWallHeight { get; set; } = 1.200m;

        /// <summary>門柱の函体軸方向の全長 = X4 + X5</summary>
        public decimal ColumnLength
        {
            get { return LongitudinalWallWidth + TransverseWallThickness; }
        }

        /// <summary>門柱の横断方向の全幅 = X7 + X8</summary>
        public decimal ColumnBreadth
        {
            get { return LongitudinalWallThickness + TransverseWallWidth; }
        }

        public void Write(System.Collections.Generic.Dictionary<string, string> d, string prefix)
        {
            System.Globalization.CultureInfo inv = System.Globalization.CultureInfo.InvariantCulture;
            d[prefix + "exists"] = Exists ? "1" : "0";
            d[prefix + "deck_length"] = DeckLength.ToString(inv);
            d[prefix + "deck_width"] = DeckWidth.ToString(inv);
            d[prefix + "deck_axial_overhang"] = DeckAxialOverhang.ToString(inv);
            d[prefix + "deck_side_overhang"] = DeckSideOverhang.ToString(inv);
            d[prefix + "deck_thickness"] = DeckThickness.ToString(inv);
            d[prefix + "bridge"] = Bridge ? "1" : "0";
            d[prefix + "seat_length"] = SeatLength.ToString(inv);
            d[prefix + "seat_width"] = SeatWidth.ToString(inv);
            d[prefix + "seat_depth"] = SeatDepth.ToString(inv);
            d[prefix + "longitudinal_wall_width"] = LongitudinalWallWidth.ToString(inv);
            d[prefix + "transverse_wall_thickness"] = TransverseWallThickness.ToString(inv);
            d[prefix + "longitudinal_wall_thickness"] = LongitudinalWallThickness.ToString(inv);
            d[prefix + "transverse_wall_width"] = TransverseWallWidth.ToString(inv);
            d[prefix + "column_height"] = ColumnHeight.ToString(inv);
            d[prefix + "haunch_height"] = HaunchHeight.ToString(inv);
            d[prefix + "retaining_wall_height"] = RetainingWallHeight.ToString(inv);
        }

        /// <summary>
        /// キーが無い項目は既定値のまま残す。ただし設置有無のキーが無い旧 XData は門柱を持たないので「設置しない」として読む。
        /// 第4段階の初版(門柱厚・門柱幅・天端高で入力していた版)のキーと、門柱の軸位置(gate_position、
        /// 今は川表側胸壁のたて壁から決まる)のキーは使わない。
        /// </summary>
        public void Read(
            System.Collections.Generic.IDictionary<string, string> d,
            string prefix,
            System.Func<System.Collections.Generic.IDictionary<string, string>, string, decimal, decimal> get)
        {
            Exists = get(d, prefix + "exists", 0m) != 0m;
            DeckLength = get(d, prefix + "deck_length", DeckLength);
            DeckWidth = get(d, prefix + "deck_width", DeckWidth);
            DeckAxialOverhang = get(d, prefix + "deck_axial_overhang", DeckAxialOverhang);
            DeckSideOverhang = get(d, prefix + "deck_side_overhang", DeckSideOverhang);
            DeckThickness = get(d, prefix + "deck_thickness", DeckThickness);
            Bridge = get(d, prefix + "bridge", Bridge ? 1m : 0m) != 0m;
            SeatLength = get(d, prefix + "seat_length", SeatLength);
            SeatWidth = get(d, prefix + "seat_width", SeatWidth);
            SeatDepth = get(d, prefix + "seat_depth", SeatDepth);
            LongitudinalWallWidth = get(d, prefix + "longitudinal_wall_width", LongitudinalWallWidth);
            TransverseWallThickness = get(d, prefix + "transverse_wall_thickness", TransverseWallThickness);
            LongitudinalWallThickness = get(d, prefix + "longitudinal_wall_thickness", LongitudinalWallThickness);
            TransverseWallWidth = get(d, prefix + "transverse_wall_width", TransverseWallWidth);
            ColumnHeight = get(d, prefix + "column_height", ColumnHeight);
            HaunchHeight = get(d, prefix + "haunch_height", HaunchHeight);
            RetainingWallHeight = get(d, prefix + "retaining_wall_height", RetainingWallHeight);
        }
    }
}
