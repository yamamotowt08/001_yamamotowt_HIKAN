namespace Hikan.Core
{
    /// <summary>
    /// 翼壁 1 端ぶんの寸法(左右 2 基で同一寸法)。函体端から外へ伸びる逆 T 字擁壁で、
    /// 内面は函体の内空側面の延長から始まる(取付部の水路幅 = 内空幅 B)。
    /// 開きは入力しない。翼壁端部の水路幅 EndWidth と内空幅 B の差から
    /// tanθ = (EndWidth − B) / (2 × Length) で決まる(EndWidth = B で平行翼壁)。
    ///
    ///   平面(右側・川表側の例)                       側面(翼壁に沿って)
    ///   函体端 S = L                                     H0 ┌───╲____
    ///    │ ┌──────────────────────┐                       │        ╲_____ H1
    ///    │ │ たて壁(厚は壁に直交)│ ← 始端・終端は        │  たて壁       │
    ///    │ └──────────────────────┘   函体端と平行に切る ─┴───────────────┴─ Z = 0
    ///    ↑ 内面の始点 = 内空側面(X = +B/2)                  ┌──── 底版 ────┐ ← 根入れぶん下
    ///    ├────── Length(函体軸方向)──────┤                 └──────────────┘
    ///
    ///   断面(翼壁に直交)  水路側 │ 背面側
    ///                          ┌──┐
    ///                   ┌──────┴──┴──────┐
    ///                   └──── 底 版 ─────┘
    ///                   ├つま先┤  ├かかと┤
    /// </summary>
    public sealed class HikanWingWall
    {
        /// <summary>たて壁厚(壁面に直交する方向)[m]。0 で設置しない。</summary>
        public decimal StemThickness { get; set; } = 0.000m;
        /// <summary>翼壁長(函体軸方向、函体端から翼壁端部まで)[m]</summary>
        public decimal Length { get; set; } = 3.000m;
        /// <summary>翼壁端部の水路幅(左右の翼壁内面間)[m]。内空幅以上。内空幅と同じで平行翼壁。</summary>
        public decimal EndWidth { get; set; } = 3.000m;
        /// <summary>函体端での天端高(函体底版下面 Z = 0 から)[m]</summary>
        public decimal CrownStart { get; set; } = 3.000m;
        /// <summary>翼壁端部での天端高(Z = 0 から)[m]。間は直線。</summary>
        public decimal CrownEnd { get; set; } = 2.000m;
        /// <summary>根入れ深さ(Z = 0 から下げる量)[m]。底版厚以上にすること。</summary>
        public decimal Embedment { get; set; } = 1.000m;
        /// <summary>底版厚 [m]</summary>
        public decimal FootingThickness { get; set; } = 0.500m;
        /// <summary>つま先版長(壁面に直交、水路側)[m]。0 で L 字になる。</summary>
        public decimal ToeLength { get; set; } = 0.500m;
        /// <summary>かかと版長(壁面に直交、背面側)[m]。0 で L 字になる。</summary>
        public decimal HeelLength { get; set; } = 1.000m;

        /// <summary>設置するか。たて壁厚 0 は「設置しない」。</summary>
        public bool Exists
        {
            get { return StemThickness > 0m; }
        }

        /// <summary>底版上面の高さ(= たて壁の下端)。</summary>
        public decimal FootingTop
        {
            get { return FootingThickness - Embedment; }
        }

        public void Write(System.Collections.Generic.Dictionary<string, string> d, string prefix)
        {
            System.Globalization.CultureInfo inv = System.Globalization.CultureInfo.InvariantCulture;
            d[prefix + "stem_thickness"] = StemThickness.ToString(inv);
            d[prefix + "length"] = Length.ToString(inv);
            d[prefix + "end_width"] = EndWidth.ToString(inv);
            d[prefix + "crown_start"] = CrownStart.ToString(inv);
            d[prefix + "crown_end"] = CrownEnd.ToString(inv);
            d[prefix + "embedment"] = Embedment.ToString(inv);
            d[prefix + "footing_thickness"] = FootingThickness.ToString(inv);
            d[prefix + "toe_length"] = ToeLength.ToString(inv);
            d[prefix + "heel_length"] = HeelLength.ToString(inv);
        }

        /// <summary>キーが無い項目は既定値のまま残す(翼壁を持たない旧 XData を読むため)。</summary>
        public void Read(
            System.Collections.Generic.IDictionary<string, string> d,
            string prefix,
            System.Func<System.Collections.Generic.IDictionary<string, string>, string, decimal, decimal> get)
        {
            StemThickness = get(d, prefix + "stem_thickness", StemThickness);
            Length = get(d, prefix + "length", Length);
            EndWidth = get(d, prefix + "end_width", EndWidth);
            CrownStart = get(d, prefix + "crown_start", CrownStart);
            CrownEnd = get(d, prefix + "crown_end", CrownEnd);
            Embedment = get(d, prefix + "embedment", Embedment);
            FootingThickness = get(d, prefix + "footing_thickness", FootingThickness);
            ToeLength = get(d, prefix + "toe_length", ToeLength);
            HeelLength = get(d, prefix + "heel_length", HeelLength);
        }
    }
}
