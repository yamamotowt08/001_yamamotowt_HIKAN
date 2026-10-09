namespace Hikan.Core
{
    /// <summary>
    /// 胸壁 1 端ぶんの寸法(左右 2 基で同一寸法)。函体の左右側面にそれぞれ取り付く逆 T 字擁壁。
    /// たて壁は函体方向に垂直な板(厚さは Y 方向)で、函体側面から外側へ張り出す。
    /// 底版は Y 方向に伸び、つま先版長・かかと版長のどちらかを 0 にすれば L 字になる。
    /// 函体とは側面で接するだけで重ならない(函体の上は跨がない)。
    ///
    ///   断面(XZ・下流を見る)                   側面(YZ・片側)
    ///   ┌──┐              ┌──┐                     ┌──┐   ← たて壁(厚 = StemThickness)
    ///   │  │ ┌──────────┐ │  │                     │  │
    ///   │  │ │   函体   │ │  │             ┌───────┴──┴───────┐
    ///  ─┴──┴─┴──────────┴─┴──┴─ Z = 0       └────── 底版 ──────┘
    ///   └──┘              └──┘              ├つま先┤      ├かかと┤
    ///   ├Lw┤              ├Lw┤  Lw = Length(函体側面から外向きの張出し長)
    ///                              底版は Embedment ぶん Z = 0 より下
    ///
    /// 函体方向の位置は Position(川裏函体端 = 上流端 S = 0 から、たて壁の軸 = 厚さの中心までの距離)で決める。
    /// つま先版は近い方の函体端の側、かかと版は函体中央の側に伸びる。
    /// </summary>
    public sealed class HikanBreastWall
    {
        /// <summary>たて壁厚(Y方向)[m]。0 で設置しない。</summary>
        public decimal StemThickness { get; set; } = 0.000m;
        /// <summary>たて壁の軸位置(川裏函体端からの距離、底版下面に沿う斜距離)[m]</summary>
        public decimal Position { get; set; } = 1.000m;
        /// <summary>張出し長(X方向、函体側面から外向き。左右それぞれ)[m]</summary>
        public decimal Length { get; set; } = 2.000m;
        /// <summary>天端高(函体底版下面 Z = 0 からの高さ)[m]</summary>
        public decimal CrownHeight { get; set; } = 4.000m;
        /// <summary>根入れ深さ(Z = 0 から下げる量)[m]。底版厚以上にすること。</summary>
        public decimal Embedment { get; set; } = 1.000m;
        /// <summary>底版厚 [m]</summary>
        public decimal FootingThickness { get; set; } = 0.500m;
        /// <summary>つま先版長(Y方向、近い方の函体端の側)[m]。0 で L 字になる。</summary>
        public decimal ToeLength { get; set; } = 0.800m;
        /// <summary>かかと版長(Y方向、函体中央の側)[m]。0 で L 字になる。</summary>
        public decimal HeelLength { get; set; } = 1.200m;

        /// <summary>たて壁の川裏側の面の位置 S = 軸位置 − たて壁厚/2 [m]</summary>
        public decimal StemStart
        {
            get { return Position - StemThickness / 2m; }
        }

        /// <summary>たて壁の川表側の面の位置 S = 軸位置 + たて壁厚/2 [m]</summary>
        public decimal StemEnd
        {
            get { return Position + StemThickness / 2m; }
        }

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

        /// <summary>底版の Y 方向全長 = つま先版長 + たて壁厚 + かかと版長 [m]</summary>
        public decimal FootingLength
        {
            get { return ToeLength + StemThickness + HeelLength; }
        }

        /// <summary>たて壁 1 基の体積 [m3]</summary>
        public decimal StemVolume
        {
            get { return Length * (CrownHeight - FootingTop) * StemThickness; }
        }

        /// <summary>底版 1 基の体積 [m3]</summary>
        public decimal FootingVolume
        {
            get { return Length * FootingThickness * FootingLength; }
        }

        public void Write(System.Collections.Generic.Dictionary<string, string> d, string prefix)
        {
            System.Globalization.CultureInfo inv = System.Globalization.CultureInfo.InvariantCulture;
            d[prefix + "stem_thickness"] = StemThickness.ToString(inv);
            d[prefix + "position"] = Position.ToString(inv);
            d[prefix + "length"] = Length.ToString(inv);
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
            Position = get(d, prefix + "position", Position);
            Length = get(d, prefix + "length", Length);
            CrownHeight = get(d, prefix + "crown_height", CrownHeight);
            Embedment = get(d, prefix + "embedment", Embedment);
            FootingThickness = get(d, prefix + "footing_thickness", FootingThickness);
            ToeLength = get(d, prefix + "toe_length", ToeLength);
            HeelLength = get(d, prefix + "heel_length", HeelLength);
        }
    }
}
