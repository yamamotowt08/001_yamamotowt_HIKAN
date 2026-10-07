namespace Hikan.Core
{
    /// <summary>
    /// 胸壁 1 端ぶんの寸法。たて壁 + 底版からなる 1 枚の逆 T 字擁壁で、
    /// たて壁は函体方向に垂直な板(厚さは Y 方向)。函体はたて壁を貫通する。
    /// つま先版長・かかと版長のどちらかを 0 にすれば L 字になる。
    ///
    ///   断面(XZ・下流を見る)                 側面(YZ)
    ///   ┌────────────────────┐                   ┌──┐   ← たて壁(厚 = StemThickness)
    ///   │      たて壁        │                   │  │
    ///   │   ┌────────────┐   │           ┌───────┴──┴───────┐
    ///   │   │ 函体(貫通) │   │           └────── 底版 ──────┘
    ///   │   └────────────┘   │            ├つま先┤  ├かかと┤
    ///   └─┬────────────────┬─┘            ↑ Embedment ぶん Z = 0 より下
    ///   ┌─┴────────────────┴─┐
    ///   └──── 底 版 ─────────┘
    ///   ├──── Width ────────┤  (たて壁・底版とも同幅、中心線 X = 0 に対称)
    /// </summary>
    public sealed class HikanBreastWall
    {
        /// <summary>たて壁厚(Y方向)[m]。0 で設置しない。</summary>
        public decimal StemThickness { get; set; } = 0.000m;
        /// <summary>たて壁・底版の幅(X方向の全幅、中心線に対称)[m]。函体外形幅以上にすること。</summary>
        public decimal Width { get; set; } = 6.000m;
        /// <summary>天端高(函体底版下面 Z = 0 からの高さ)[m]。函体外形高以上にすること。</summary>
        public decimal CrownHeight { get; set; } = 4.000m;
        /// <summary>根入れ深さ(Z = 0 から下げる量)[m]。底版厚以上にすること。</summary>
        public decimal Embedment { get; set; } = 1.000m;
        /// <summary>底版厚 [m]</summary>
        public decimal FootingThickness { get; set; } = 0.500m;
        /// <summary>つま先版長(Y方向、函体から遠い側)[m]。0 で L 字になる。</summary>
        public decimal ToeLength { get; set; } = 0.800m;
        /// <summary>かかと版長(Y方向、函体に近い側)[m]。0 で L 字になる。</summary>
        public decimal HeelLength { get; set; } = 1.200m;

        /// <summary>設置するか。たて壁厚 0 は「設置しない」。</summary>
        public bool Exists
        {
            get { return StemThickness > 0m; }
        }

        /// <summary>底版上面の高さ(= たて壁の下端)。根入れ深さぶん下げた位置から底版厚ぶん上。</summary>
        public decimal FootingTop
        {
            get { return FootingThickness - Embedment; }
        }

        /// <summary>たて壁の体積 [m3](函体との重なりを含む総量。控除は HikanGeometry 側で行う)。</summary>
        public decimal StemGrossVolume
        {
            get { return Width * (CrownHeight - FootingTop) * StemThickness; }
        }

        /// <summary>底版の体積 [m3]。函体とは重ならない(根入れにより Z = 0 以下に収まるため)。</summary>
        public decimal FootingVolume
        {
            get { return Width * FootingThickness * (ToeLength + StemThickness + HeelLength); }
        }

        public void Write(System.Collections.Generic.Dictionary<string, string> d, string prefix)
        {
            System.Globalization.CultureInfo inv = System.Globalization.CultureInfo.InvariantCulture;
            d[prefix + "stem_thickness"] = StemThickness.ToString(inv);
            d[prefix + "width"] = Width.ToString(inv);
            d[prefix + "crown_height"] = CrownHeight.ToString(inv);
            d[prefix + "embedment"] = Embedment.ToString(inv);
            d[prefix + "footing_thickness"] = FootingThickness.ToString(inv);
            d[prefix + "toe_length"] = ToeLength.ToString(inv);
            d[prefix + "heel_length"] = HeelLength.ToString(inv);
        }

        /// <summary>キーが無い項目は既定値のまま残す(第1段階の XData を読むため)。</summary>
        public void Read(
            System.Collections.Generic.IDictionary<string, string> d,
            string prefix,
            System.Func<System.Collections.Generic.IDictionary<string, string>, string, decimal, decimal> get)
        {
            StemThickness = get(d, prefix + "stem_thickness", StemThickness);
            Width = get(d, prefix + "width", Width);
            CrownHeight = get(d, prefix + "crown_height", CrownHeight);
            Embedment = get(d, prefix + "embedment", Embedment);
            FootingThickness = get(d, prefix + "footing_thickness", FootingThickness);
            ToeLength = get(d, prefix + "toe_length", ToeLength);
            HeelLength = get(d, prefix + "heel_length", HeelLength);
        }
    }
}
