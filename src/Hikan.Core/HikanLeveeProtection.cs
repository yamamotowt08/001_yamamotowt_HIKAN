namespace Hikan.Core
{
    /// <summary>
    /// 翼壁 1 端ぶんの堤防保護部。翼壁の端部から左右(堤防法線の方向)へ張り出す逆 T 字擁壁で、左右同一形状。
    /// 記号は手本の樋門設計ソフトの「形状寸法」画面に合わせる(X4 / Y3 / L2 / L3 / L4)。
    ///
    ///   平面(川表側の例、上が函体)                       側面(保護部を横から)
    ///             │ │  水路  │ │  ← 翼壁の側壁                  ┌──┐ ↑
    ///   ┌────────╱ ╱            ╲ ╲────────┐ ┐                 │  │ Y3      翼壁 → 函体
    ///   │ かかと版│              │ かかと版 │ │L4          ┌──────┤  ├────────┐ ↓ ← 底版上面 = 敷高
    ///   ├────────┤              ├────────┤ ┤            └──────┴──┴────────┘
    ///   │▓たて壁▓│              │▓たて壁▓│ │L3          ├─ L2 ─┼ L3┼── L4 ──┤
    ///   ├────────┴──────────────┴────────┤ ┤ ← 翼壁端部
    ///   │        つま先版(左右つながる)    │ │L2
    ///   └────────────────────────────────┘ ┘
    ///   ├── X4 ──┤              ├── X4 ──┤   X4 は翼壁端部の側壁内面から外向き(側壁厚を含む)
    ///
    /// たて壁は外側(函体から遠い側)の面を翼壁端部にそろえ、かかと版は函体側、つま先版は翼壁端部より外側へ伸びる。
    /// つま先版は水路床の下も含めて左右つながる。底版は上面 = 敷高、厚さ = 翼壁の底版厚。
    /// たて壁高 Y3 は入力しない: たて壁の上面を翼壁端部の天端 H1 にそろえる(Y3 = H1 − 敷高、HikanGeometry.ProtectionStemHeight)。
    /// </summary>
    public sealed class HikanLeveeProtection
    {
        /// <summary>設置するか(既定は設置しない)。</summary>
        public bool Exists { get; set; } = false;
        /// <summary>X4: 張出し幅 [m]。翼壁端部の側壁内面から外向き(側壁厚を含む)。</summary>
        public decimal Width { get; set; } = 1.000m;
        /// <summary>L3: たて壁厚(函体軸方向)[m]</summary>
        public decimal StemThickness { get; set; } = 0.400m;
        /// <summary>L2: つま先版長(翼壁端部より外側)[m]。0 でつま先版なし。</summary>
        public decimal ToeLength { get; set; } = 0.500m;
        /// <summary>L4: かかと版長(函体側)[m]。0 でかかと版なし。</summary>
        public decimal HeelLength { get; set; } = 1.000m;

        public void Write(System.Collections.Generic.Dictionary<string, string> d, string prefix)
        {
            System.Globalization.CultureInfo inv = System.Globalization.CultureInfo.InvariantCulture;
            d[prefix + "exists"] = Exists ? "1" : "0";
            d[prefix + "width"] = Width.ToString(inv);
            d[prefix + "stem_thickness"] = StemThickness.ToString(inv);
            d[prefix + "toe_length"] = ToeLength.ToString(inv);
            d[prefix + "heel_length"] = HeelLength.ToString(inv);
        }

        /// <summary>キーが無い項目は既定値のまま残す(堤防保護部を持たない旧 XData は「設置しない」として読む)。</summary>
        public void Read(
            System.Collections.Generic.IDictionary<string, string> d,
            string prefix,
            System.Func<System.Collections.Generic.IDictionary<string, string>, string, decimal, decimal> get)
        {
            Exists = get(d, prefix + "exists", Exists ? 1m : 0m) != 0m;
            Width = get(d, prefix + "width", Width);
            StemThickness = get(d, prefix + "stem_thickness", StemThickness);
            ToeLength = get(d, prefix + "toe_length", ToeLength);
            HeelLength = get(d, prefix + "heel_length", HeelLength);
        }
    }
}
