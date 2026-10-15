namespace Hikan.Core
{
    /// <summary>
    /// 翼壁 1 端ぶんの寸法。左右の側壁と、その間をつなぐ底版(水路床)が一体になった
    /// U 字の 3 面張り水路で、函体端から外へ伸びる。左右の側壁は同一形状。
    ///
    /// 水路の内面は函体端で函体の内空にそろう(水路幅 = 内空幅 B、底版上面 = 敷高)。
    /// 開きは入力しない。翼壁端部の水路幅 EndWidth と内空幅 B の差から
    /// tanθ = (EndWidth − B) / (2 × Length) で決まる(EndWidth = B で平行)。
    ///
    ///   平面(川表側の例)                              断面(翼壁の位置で見る)
    ///   函体端 S = L                                     ┌──┐                  ┌──┐ ← 側壁(天端 H0 → H1)
    ///    │ ┌──────────────────────┐                       │  │      水 路       │  │
    ///    │ │▓▓▓▓▓ 側壁(右) ▓▓▓▓▓▓│                       │  └──────────────────┘  │ ← 底版上面 = 敷高
    ///    │ │      底 版(水路床)    │                       └────── 底 版 ─────────┘ ← 底版厚
    ///    │ │▓▓▓▓▓ 側壁(左) ▓▓▓▓▓▓│                            ├─ 水路幅 ─┤
    ///    │ └──────────────────────┘                    底版は側壁の外面まで(U 字)
    ///    ├────── Length(函体軸方向)──────┤
    /// </summary>
    public sealed class HikanWingWall
    {
        /// <summary>側壁厚(壁面に直交する方向)[m]。0.40 m 以上。0 は旧図面の「設置なし」(新規入力では使わない)。</summary>
        public decimal StemThickness { get; set; } = 0.400m;
        /// <summary>翼壁長(函体軸方向、函体端から翼壁端部まで)[m]</summary>
        public decimal Length { get; set; } = 3.000m;
        /// <summary>翼壁端部の水路幅(左右の側壁内面間)[m]。内空幅以上。内空幅と同じで平行。</summary>
        public decimal EndWidth { get; set; } = 3.000m;
        /// <summary>函体端での側壁の天端高(函体底版下面 Z = 0 から)[m]</summary>
        public decimal CrownStart { get; set; } = 3.000m;
        /// <summary>翼壁端部での側壁の天端高(Z = 0 から)[m]。間は直線。</summary>
        public decimal CrownEnd { get; set; } = 2.000m;
        /// <summary>底版厚 [m]。底版上面は函体の敷高に合わせるので、函体の底版厚と同じなら底版下面も Z = 0 になる。</summary>
        public decimal FloorThickness { get; set; } = 0.400m;

        /// <summary>設置するか。側壁厚 0 は旧図面の「設置なし」。</summary>
        public bool Exists
        {
            get { return StemThickness > 0m; }
        }

        public void Write(System.Collections.Generic.Dictionary<string, string> d, string prefix)
        {
            System.Globalization.CultureInfo inv = System.Globalization.CultureInfo.InvariantCulture;
            d[prefix + "stem_thickness"] = StemThickness.ToString(inv);
            d[prefix + "length"] = Length.ToString(inv);
            d[prefix + "end_width"] = EndWidth.ToString(inv);
            d[prefix + "crown_start"] = CrownStart.ToString(inv);
            d[prefix + "crown_end"] = CrownEnd.ToString(inv);
            d[prefix + "floor_thickness"] = FloorThickness.ToString(inv);
        }

        /// <summary>キーが無い項目は既定値のまま残す。ただし側壁厚のキーが無い旧 XData は翼壁を持たないので 0(設置なし)として読む。</summary>
        public void Read(
            System.Collections.Generic.IDictionary<string, string> d,
            string prefix,
            System.Func<System.Collections.Generic.IDictionary<string, string>, string, decimal, decimal> get)
        {
            StemThickness = get(d, prefix + "stem_thickness", 0m);
            Length = get(d, prefix + "length", Length);
            EndWidth = get(d, prefix + "end_width", EndWidth);
            CrownStart = get(d, prefix + "crown_start", CrownStart);
            CrownEnd = get(d, prefix + "crown_end", CrownEnd);
            FloorThickness = get(d, prefix + "floor_thickness", FloorThickness);
        }
    }
}
