namespace Hikan.Core
{
    /// <summary>
    /// 函体の内空の隅に付くハンチ 1 か所。高さ H(側壁に沿う鉛直の辺)× 幅 W(頂版・底版に沿う水平の辺)の直角三角形。
    /// H・W とも 0 でハンチなし。
    /// </summary>
    public sealed class HikanHaunch
    {
        /// <summary>H: 高さ(鉛直)[m]</summary>
        public decimal Height { get; set; } = 0.000m;
        /// <summary>W: 幅(水平)[m]</summary>
        public decimal Width { get; set; } = 0.000m;

        public bool Exists
        {
            get { return Height > 0m && Width > 0m; }
        }

        /// <summary>断面積 H × W / 2 [m2]</summary>
        public decimal Area
        {
            get { return Exists ? Height * Width / 2m : 0m; }
        }
    }

    /// <summary>
    /// 函体の断面(標準部・川裏側端部・川表側端部のそれぞれ)。内空幅・内空高は函体全体で共通で、
    /// ここでは部材厚と内空の 4 隅のハンチを持つ。記号は手本の樋門設計ソフトの「函体」画面に合わせる。
    ///
    ///        ├WL┤├──── SW ────┤├WR┤        WL = WR = 側壁厚(左右共通)
    ///   ┬    ┌──────────────────────┐       WT = 頂版厚、WB = 底版厚
    ///   WT   │  ┌──────────────┐    │       LT・RT・LB・RB = 左上・右上・左下・右下のハンチ(下流を見て)
    ///   ┴    │  │LT          RT│    │       各ハンチは H(鉛直)× W(水平)の直角三角形
    ///   SH   │  │              │    │
    ///   ┬    │  │LB          RB│    │
    ///   WB   │  └──────────────┘    │
    ///   ┴    └──────────────────────┘
    /// </summary>
    public sealed class HikanBarrelSection
    {
        /// <summary>側壁厚(左右共通)[m]</summary>
        public decimal WallThickness { get; set; } = 0.400m;
        /// <summary>頂版厚 [m]</summary>
        public decimal TopSlabThickness { get; set; } = 0.400m;
        /// <summary>底版厚 [m]</summary>
        public decimal BottomSlabThickness { get; set; } = 0.400m;
        /// <summary>LT: 左上のハンチ</summary>
        public HikanHaunch LeftTop { get; set; } = new HikanHaunch();
        /// <summary>LB: 左下のハンチ</summary>
        public HikanHaunch LeftBottom { get; set; } = new HikanHaunch();
        /// <summary>RT: 右上のハンチ</summary>
        public HikanHaunch RightTop { get; set; } = new HikanHaunch();
        /// <summary>RB: 右下のハンチ</summary>
        public HikanHaunch RightBottom { get; set; } = new HikanHaunch();

        /// <summary>4 隅のハンチの断面積の和 [m2]</summary>
        public decimal HaunchArea
        {
            get { return LeftTop.Area + LeftBottom.Area + RightTop.Area + RightBottom.Area; }
        }

        /// <summary>部材厚(外形)が同じか。ハンチは比べない(外側に付く部材の配置は外形だけで決まる)。</summary>
        public bool SameOuter(HikanBarrelSection other)
        {
            return WallThickness == other.WallThickness
                && TopSlabThickness == other.TopSlabThickness
                && BottomSlabThickness == other.BottomSlabThickness;
        }

        public void Write(System.Collections.Generic.Dictionary<string, string> d, string prefix)
        {
            System.Globalization.CultureInfo inv = System.Globalization.CultureInfo.InvariantCulture;
            d[prefix + "wall_thickness"] = WallThickness.ToString(inv);
            d[prefix + "top_slab_thickness"] = TopSlabThickness.ToString(inv);
            d[prefix + "bottom_slab_thickness"] = BottomSlabThickness.ToString(inv);
            WriteHaunch(d, prefix + "haunch_lt_", LeftTop);
            WriteHaunch(d, prefix + "haunch_lb_", LeftBottom);
            WriteHaunch(d, prefix + "haunch_rt_", RightTop);
            WriteHaunch(d, prefix + "haunch_rb_", RightBottom);
        }

        private static void WriteHaunch(System.Collections.Generic.Dictionary<string, string> d, string prefix, HikanHaunch h)
        {
            System.Globalization.CultureInfo inv = System.Globalization.CultureInfo.InvariantCulture;
            d[prefix + "height"] = h.Height.ToString(inv);
            d[prefix + "width"] = h.Width.ToString(inv);
        }

        /// <summary>
        /// 部材厚とハンチを読む。キーが無い項目は fallback の値を使う(端部の断面を持たない旧 XData は標準部と同じ断面として読む)。
        /// </summary>
        public void Read(
            System.Collections.Generic.IDictionary<string, string> d,
            string prefix,
            System.Func<System.Collections.Generic.IDictionary<string, string>, string, decimal, decimal> get,
            HikanBarrelSection fallback)
        {
            WallThickness = get(d, prefix + "wall_thickness", fallback.WallThickness);
            TopSlabThickness = get(d, prefix + "top_slab_thickness", fallback.TopSlabThickness);
            BottomSlabThickness = get(d, prefix + "bottom_slab_thickness", fallback.BottomSlabThickness);
            ReadHaunches(d, prefix, get, fallback);
        }

        /// <summary>ハンチだけを読む。キーが無い項目は fallback の値を使う(ハンチを持たない旧 XData はハンチなし)。</summary>
        public void ReadHaunches(
            System.Collections.Generic.IDictionary<string, string> d,
            string prefix,
            System.Func<System.Collections.Generic.IDictionary<string, string>, string, decimal, decimal> get,
            HikanBarrelSection fallback)
        {
            ReadHaunch(d, prefix + "haunch_lt_", get, LeftTop, fallback.LeftTop);
            ReadHaunch(d, prefix + "haunch_lb_", get, LeftBottom, fallback.LeftBottom);
            ReadHaunch(d, prefix + "haunch_rt_", get, RightTop, fallback.RightTop);
            ReadHaunch(d, prefix + "haunch_rb_", get, RightBottom, fallback.RightBottom);
        }

        private static void ReadHaunch(
            System.Collections.Generic.IDictionary<string, string> d,
            string prefix,
            System.Func<System.Collections.Generic.IDictionary<string, string>, string, decimal, decimal> get,
            HikanHaunch h,
            HikanHaunch fallback)
        {
            h.Height = get(d, prefix + "height", fallback.Height);
            h.Width = get(d, prefix + "width", fallback.Width);
        }
    }
}
