namespace Hikan.Core
{
    /// <summary>
    /// 樋管(ボックスカルバート型・1連)函体の入力パラメータ。単位はすべてメートル(m)。mm は混入させない。
    /// 座標系: X = 横断方向(X = 0 が函体中心線、下流を向いて右が +X)、
    ///         Y = 施設延長方向(上流 → 下流が +Y)、Z = 鉛直上向き。
    /// 基準点(BaseX/Y/Z)は「上流端・底版下面・函体中心線上」の点。
    /// </summary>
    public sealed class HikanParameters
    {
        public const string SchemaVersion = "1";

        /// <summary>内空幅 B [m]</summary>
        public decimal InnerWidth { get; set; } = 2.000m;
        /// <summary>内空高 H [m]</summary>
        public decimal InnerHeight { get; set; } = 2.000m;
        /// <summary>スパン数の上限(手本の画面のスパン 1〜5)。</summary>
        public const int MaxSpans = 5;

        /// <summary>標準部の断面(部材厚・ハンチ)。端部の長さを除いた函体中央の区間。</summary>
        public HikanBarrelSection Standard { get; set; } = new HikanBarrelSection();
        /// <summary>川裏側端部の断面。川裏函体端(S = 0)から UpstreamEndLength の区間。</summary>
        public HikanBarrelSection UpstreamEnd { get; set; } = new HikanBarrelSection();
        /// <summary>川表側端部の断面。川表函体端(S = L)から DownstreamEndLength の区間。</summary>
        public HikanBarrelSection DownstreamEnd { get; set; } = new HikanBarrelSection();
        /// <summary>川裏側端部の長さ [m]。0 で端部なし(全長が標準部の断面)。</summary>
        public decimal UpstreamEndLength { get; set; } = 0.000m;
        /// <summary>川表側端部の長さ [m]。0 で端部なし。</summary>
        public decimal DownstreamEndLength { get; set; } = 0.000m;
        /// <summary>
        /// 各スパン長 [m](川裏側から順に、1〜5 個)。合計が函体延長 L、個数がブロック数、境目が継手の位置。
        /// 形状は継手で切らず 1 つのソリッドにする。
        /// </summary>
        public decimal[] Spans { get; set; } = new decimal[] { 5.000m, 5.000m, 5.000m, 5.000m };

        /// <summary>標準部の側壁厚 [m]</summary>
        public decimal WallThickness
        {
            get { return Standard.WallThickness; }
            set { Standard.WallThickness = value; }
        }

        /// <summary>標準部の頂版厚 [m]</summary>
        public decimal TopSlabThickness
        {
            get { return Standard.TopSlabThickness; }
            set { Standard.TopSlabThickness = value; }
        }

        /// <summary>
        /// 標準部の底版厚 [m]。Z = 0 は標準部の底版下面の高さなので、敷高(内空底面、函体全長で一定)は Z = この値。
        /// 端部の底版厚が違うと、端部の底版下面は Z = 0 から段違いになる。
        /// </summary>
        public decimal BottomSlabThickness
        {
            get { return Standard.BottomSlabThickness; }
            set { Standard.BottomSlabThickness = value; }
        }

        /// <summary>函体延長 L = スパン長の合計 [m]。底版下面に沿う斜距離(勾配がある場合、水平投影長とは異なる)。</summary>
        public decimal BarrelLength
        {
            get
            {
                decimal l = 0m;
                foreach (decimal s in Spans) { l += s; }
                return l;
            }
        }
        /// <summary>底版勾配 i(下流下がりを正)。押出し後の X 軸まわり回転で表現する。</summary>
        public decimal BottomSlope { get; set; } = 0.000m;
        /// <summary>土かぶり(頂版上面〜現地盤)[m]。床掘り数量の算定に使う。</summary>
        public decimal SoilCover { get; set; } = 2.000m;
        /// <summary>ブロック数 = スパン数。形状には反映せず、数量の按分表示にのみ使う。</summary>
        public int BlockCount
        {
            get { return Spans.Length; }
        }
        /// <summary>床掘り余裕幅(片側)[m]</summary>
        public decimal ExcavationMargin { get; set; } = 0.500m;
        /// <summary>床掘り法勾配 1:n の n</summary>
        public decimal ExcavationSlope { get; set; } = 0.500m;
        /// <summary>基礎材厚(均しコン等)[m]</summary>
        public decimal FoundationThickness { get; set; } = 0.100m;

        // --- 胸壁(逆T字 / L字)。函体の左右側面に 1 基ずつ取り付く。必ず設置する(たて壁厚 0 は旧図面の「設置なし」)。 ---
        // 上流端 = 川裏 とする(排水樋管の向き)。位置はどちらも川裏函体端からたて壁の軸までの距離で指定する。
        /// <summary>川裏側胸壁(左右に 1 基ずつ)。つま先版は川裏側に伸びる。</summary>
        public HikanBreastWall UpstreamBreast { get; set; } = new HikanBreastWall { Position = 1.000m };
        /// <summary>川表側胸壁(左右に 1 基ずつ)。つま先版は川表側に伸びる。</summary>
        public HikanBreastWall DownstreamBreast { get; set; } = new HikanBreastWall { Position = 18.000m };

        // --- 翼壁(U 字の 3 面張り水路)。函体端から外へ。必ず設置する(側壁厚 0 は旧図面の「設置なし」)。 ---
        /// <summary>川裏側翼壁(左右に 1 基ずつ)。川裏函体端(S = 0)から川裏へ伸びる。</summary>
        public HikanWingWall UpstreamWing { get; set; } = new HikanWingWall();
        /// <summary>川裏側の土留壁(川裏側胸壁の左右のたて壁の間、頂版の上)。</summary>
        public HikanRetainingWall UpstreamRetainingWall { get; set; } = new HikanRetainingWall();
        /// <summary>川表側翼壁(左右に 1 基ずつ)。川表函体端(S = L)から川表へ伸びる。</summary>
        public HikanWingWall DownstreamWing { get; set; } = new HikanWingWall();

        // --- しゃ水壁(函体外周に全周一律で張り出すカラー)。枚数 0 で「設置しない」。 ---
        /// <summary>しゃ水壁 枚数。函体延長を n 等分した各区間の中央に配置する。0 で設置しない。</summary>
        public int CutoffCount { get; set; } = 0;
        /// <summary>しゃ水壁 厚(Y方向)[m]</summary>
        public decimal CutoffThickness { get; set; } = 0.500m;
        /// <summary>しゃ水壁 張出し量(全周一律。下方にも張り出す)[m]</summary>
        public decimal CutoffProjection { get; set; } = 0.500m;
        /// <summary>門柱・ゲート操作台(函体の側壁の上に立つ左右の門柱 + 操作台の版)。</summary>
        public HikanGateTower Gate { get; set; } = new HikanGateTower();
        /// <summary>ソリッド色(ACI 0〜256)</summary>
        public int ColorIndex { get; set; } = 3;

        /// <summary>配置基準点 X [m](Action の同位置再生成用)。函体中心線の位置。</summary>
        public decimal BaseX { get; set; } = 0.000m;
        /// <summary>配置基準点 Y [m]。上流端の位置。</summary>
        public decimal BaseY { get; set; } = 0.000m;
        /// <summary>配置基準点 Z [m]。上流端における標準部の底版下面の高さ(端部の底版厚が違っても動かない)。</summary>
        public decimal BaseZ { get; set; } = 0.000m;

        /// <summary>標準部の外形幅 B_out = B + 2 × 側壁厚 [m]</summary>
        public decimal OuterWidth
        {
            get { return InnerWidth + 2m * WallThickness; }
        }

        /// <summary>標準部の外形高 H_out = H + 頂版厚 + 底版厚 [m](= 標準部の頂版上面の Z)</summary>
        public decimal OuterHeight
        {
            get { return InnerHeight + TopSlabThickness + BottomSlabThickness; }
        }

        /// <summary>敷高(内空底面高)= BaseZ + 底版厚 [m]。基準点は底版下面なので混同しないこと。</summary>
        public decimal InvertLevel
        {
            get { return BaseZ + BottomSlabThickness; }
        }

        /// <summary>英語名 → 文字列。XData 保存に使う(実数は不変カルチャの文字列)。</summary>
        public System.Collections.Generic.Dictionary<string, string> ToDictionary()
        {
            System.Globalization.CultureInfo inv = System.Globalization.CultureInfo.InvariantCulture;
            System.Collections.Generic.Dictionary<string, string> d = new System.Collections.Generic.Dictionary<string, string>();
            d["schema_version"] = SchemaVersion;
            d["inner_width"] = InnerWidth.ToString(inv);
            d["inner_height"] = InnerHeight.ToString(inv);
            Standard.Write(d, "");
            UpstreamEnd.Write(d, "upstream_end_");
            DownstreamEnd.Write(d, "downstream_end_");
            d["upstream_end_length"] = UpstreamEndLength.ToString(inv);
            d["downstream_end_length"] = DownstreamEndLength.ToString(inv);
            d["span_count"] = Spans.Length.ToString(inv);
            for (int i = 0; i < Spans.Length; i++)
            {
                d["span_" + (i + 1)] = Spans[i].ToString(inv);
            }
            d["bottom_slope"] = BottomSlope.ToString(inv);
            d["soil_cover"] = SoilCover.ToString(inv);
            d["excavation_margin"] = ExcavationMargin.ToString(inv);
            d["excavation_slope"] = ExcavationSlope.ToString(inv);
            d["foundation_thickness"] = FoundationThickness.ToString(inv);
            UpstreamBreast.Write(d, "upstream_breast_");
            UpstreamRetainingWall.Write(d, "upstream_retaining_wall_");
            DownstreamBreast.Write(d, "downstream_breast_");
            UpstreamWing.Write(d, "upstream_wing_");
            DownstreamWing.Write(d, "downstream_wing_");
            d["cutoff_count"] = CutoffCount.ToString(inv);
            d["cutoff_thickness"] = CutoffThickness.ToString(inv);
            d["cutoff_projection"] = CutoffProjection.ToString(inv);
            Gate.Write(d, "gate_");
            d["color_index"] = ColorIndex.ToString(inv);
            d["base_x"] = BaseX.ToString(inv);
            d["base_y"] = BaseY.ToString(inv);
            d["base_z"] = BaseZ.ToString(inv);
            return d;
        }

        /// <summary>ToDictionary の逆変換。キー欠損・不正値・版数不一致は HikanValidationException。</summary>
        public static HikanParameters FromDictionary(System.Collections.Generic.IDictionary<string, string> d)
        {
            if (Get(d, "schema_version") != SchemaVersion)
            {
                throw new HikanValidationException("XData の schema_version が未対応です: " + Get(d, "schema_version"));
            }

            HikanParameters p = new HikanParameters();
            p.InnerWidth = GetDecimal(d, "inner_width");
            p.InnerHeight = GetDecimal(d, "inner_height");
            p.WallThickness = GetDecimal(d, "wall_thickness");
            p.TopSlabThickness = GetDecimal(d, "top_slab_thickness");
            p.BottomSlabThickness = GetDecimal(d, "bottom_slab_thickness");
            p.Standard.ReadHaunches(d, "", GetDecimalOrDefault, new HikanBarrelSection());
            // 端部の断面・長さとハンチは後から追加したキー。無ければ端部なし(長さ 0)、断面は標準部と同じとして読む。
            p.UpstreamEnd.Read(d, "upstream_end_", GetDecimalOrDefault, p.Standard);
            p.DownstreamEnd.Read(d, "downstream_end_", GetDecimalOrDefault, p.Standard);
            p.UpstreamEndLength = GetDecimalOrDefault(d, "upstream_end_length", 0m);
            p.DownstreamEndLength = GetDecimalOrDefault(d, "downstream_end_length", 0m);
            p.Spans = ReadSpans(d);
            p.BottomSlope = GetDecimal(d, "bottom_slope");
            p.SoilCover = GetDecimal(d, "soil_cover");
            p.ExcavationMargin = GetDecimal(d, "excavation_margin");
            p.ExcavationSlope = GetDecimal(d, "excavation_slope");
            p.FoundationThickness = GetDecimal(d, "foundation_thickness");

            // 胸壁・しゃ水壁は第2段階で追加したキー。第1段階で生成したソリッドの XData には存在しないため、
            // 欠けている場合は「設置なし」(厚 0)で補い、函体だけのモデルとして読めるようにする。
            // キーが在るのに値が不正な場合は従来どおりエラー停止する。
            p.UpstreamBreast.Read(d, "upstream_breast_", GetDecimalOrDefault);
            p.UpstreamRetainingWall.Read(d, "upstream_retaining_wall_", GetDecimalOrDefault);
            p.DownstreamBreast.Read(d, "downstream_breast_", GetDecimalOrDefault);
            p.UpstreamWing.Read(d, "upstream_wing_", GetDecimalOrDefault);
            p.DownstreamWing.Read(d, "downstream_wing_", GetDecimalOrDefault);
            p.CutoffCount = GetIntOrDefault(d, "cutoff_count", p.CutoffCount);
            p.CutoffThickness = GetDecimalOrDefault(d, "cutoff_thickness", p.CutoffThickness);
            p.CutoffProjection = GetDecimalOrDefault(d, "cutoff_projection", p.CutoffProjection);
            p.Gate.Read(d, "gate_", GetDecimalOrDefault);

            p.ColorIndex = GetInt(d, "color_index");
            p.BaseX = GetDecimal(d, "base_x");
            p.BaseY = GetDecimal(d, "base_y");
            p.BaseZ = GetDecimal(d, "base_z");
            return p;
        }

        /// <summary>
        /// スパン長を読む。スパンを持たない旧 XData は 函体延長 L とブロック数 n から作る:
        /// L / n が 1 mm 単位で割り切れて n が上限以下なら n 等分、そうでなければ 1 スパン(= L)。
        /// </summary>
        private static decimal[] ReadSpans(System.Collections.Generic.IDictionary<string, string> d)
        {
            if (d.ContainsKey("span_count"))
            {
                int count = GetInt(d, "span_count");
                if (count < 0)
                {
                    throw new HikanValidationException("パラメータ span_count が負です: " + count);
                }
                decimal[] spans = new decimal[count];
                for (int i = 0; i < count; i++)
                {
                    spans[i] = GetDecimal(d, "span_" + (i + 1));
                }
                return spans;
            }
            decimal l = GetDecimal(d, "barrel_length");
            int n = GetInt(d, "block_count");
            // 等分の長さが 1 mm 単位で割り切れるときだけ等分する(20 / 3 などは 1 スパン)。
            if (n >= 1 && n <= MaxSpans && System.Math.Round(l / n, 3) * n == l)
            {
                decimal[] equal = new decimal[n];
                for (int i = 0; i < n; i++) { equal[i] = l / n; }
                return equal;
            }
            return new decimal[] { l };
        }

        private static string Get(System.Collections.Generic.IDictionary<string, string> d, string key)
        {
            string v;
            if (!d.TryGetValue(key, out v))
            {
                throw new HikanValidationException("パラメータ欠損: " + key);
            }
            return v;
        }

        private static decimal GetDecimal(System.Collections.Generic.IDictionary<string, string> d, string key)
        {
            decimal v;
            if (!decimal.TryParse(Get(d, key), System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out v))
            {
                throw new HikanValidationException("パラメータ " + key + " が数値ではありません: " + Get(d, key));
            }
            return v;
        }

        /// <summary>キーが無ければ既定値を返す。在るのに数値でなければエラー停止する。</summary>
        private static decimal GetDecimalOrDefault(System.Collections.Generic.IDictionary<string, string> d, string key, decimal fallback)
        {
            if (!d.ContainsKey(key)) { return fallback; }
            return GetDecimal(d, key);
        }

        /// <summary>キーが無ければ既定値を返す。在るのに整数でなければエラー停止する。</summary>
        private static int GetIntOrDefault(System.Collections.Generic.IDictionary<string, string> d, string key, int fallback)
        {
            if (!d.ContainsKey(key)) { return fallback; }
            return GetInt(d, key);
        }

        private static int GetInt(System.Collections.Generic.IDictionary<string, string> d, string key)
        {
            int v;
            if (!int.TryParse(Get(d, key), System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out v))
            {
                throw new HikanValidationException("パラメータ " + key + " が整数ではありません: " + Get(d, key));
            }
            return v;
        }
    }
}
