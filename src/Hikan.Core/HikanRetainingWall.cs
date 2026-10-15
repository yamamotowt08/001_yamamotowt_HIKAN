namespace Hikan.Core
{
    /// <summary>
    /// 川裏側の土留壁。函体の頂版の上で、川裏側胸壁の左右のたて壁の間(函体の外形幅)をふさぐ壁。
    /// 下端 = 頂版上面、背面 = 川裏側胸壁のたて壁の背面(函体中央側の面)、厚さは背面から川裏側へ取る。
    /// 川表側の土留壁は門柱・操作台の一部(HikanGateTower.RetainingWallHeight)で、厚さは横方向壁厚 X5。
    ///
    ///   正面(川裏側胸壁の位置)                       側面
    ///   ┌──┐                ┌──┐ ← たて壁            │たて壁│
    ///   │  ├────────────────┤  │ ↑                ┌──┤    │ ↑
    ///   │  │     土留壁     │  │ │高さ(頂版上面から)│土│    │ │高さ
    ///   │  ├────────────────┤  │ ↓                └──┤    │ ↓
    ///   │  │ ┌────────────┐ │  │ ← 函体           ├厚┤ ← 背面 = たて壁の背面(函体中央側)
    /// </summary>
    public sealed class HikanRetainingWall
    {
        /// <summary>高さ(頂版上面から)[m]。0 で設置しない。上端は川裏側胸壁のたて壁の天端以上(既定 1.2 = 天端 4.0 − 頂版上面 2.8)。</summary>
        public decimal Height { get; set; } = 1.200m;
        /// <summary>厚さ(函体軸方向、背面から川裏側へ)[m]</summary>
        public decimal Thickness { get; set; } = 0.400m;

        /// <summary>設置するか(高さ 0 は設置しない)。</summary>
        public bool Exists
        {
            get { return Height > 0m; }
        }

        public void Write(System.Collections.Generic.Dictionary<string, string> d, string prefix)
        {
            System.Globalization.CultureInfo inv = System.Globalization.CultureInfo.InvariantCulture;
            d[prefix + "height"] = Height.ToString(inv);
            d[prefix + "thickness"] = Thickness.ToString(inv);
        }

        /// <summary>キーが無い旧 XData は川裏側の土留壁を持たないので、高さ 0(設置しない)として読む。</summary>
        public void Read(
            System.Collections.Generic.IDictionary<string, string> d,
            string prefix,
            System.Func<System.Collections.Generic.IDictionary<string, string>, string, decimal, decimal> get)
        {
            Height = get(d, prefix + "height", 0m);
            Thickness = get(d, prefix + "thickness", Thickness);
        }
    }
}
