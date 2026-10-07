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
        /// <summary>側壁厚 [m]</summary>
        public decimal WallThickness { get; set; } = 0.400m;
        /// <summary>頂版厚 [m]</summary>
        public decimal TopSlabThickness { get; set; } = 0.400m;
        /// <summary>底版厚 [m]</summary>
        public decimal BottomSlabThickness { get; set; } = 0.400m;
        /// <summary>函体延長 L [m]。底版下面に沿う斜距離(勾配がある場合、水平投影長とは異なる)。</summary>
        public decimal BarrelLength { get; set; } = 20.000m;
        /// <summary>底版勾配 i(下流下がりを正)。押出し後の X 軸まわり回転で表現する。</summary>
        public decimal BottomSlope { get; set; } = 0.000m;
        /// <summary>土かぶり(頂版上面〜現地盤)[m]。床掘り数量の算定に使う。</summary>
        public decimal SoilCover { get; set; } = 2.000m;
        /// <summary>ブロック数。形状には反映せず、数量の按分表示にのみ使う。</summary>
        public int BlockCount { get; set; } = 4;
        /// <summary>床掘り余裕幅(片側)[m]</summary>
        public decimal ExcavationMargin { get; set; } = 0.500m;
        /// <summary>床掘り法勾配 1:n の n</summary>
        public decimal ExcavationSlope { get; set; } = 0.500m;
        /// <summary>基礎材厚(均しコン等)[m]</summary>
        public decimal FoundationThickness { get; set; } = 0.100m;

        // --- 胸壁(函体端面の外側に接続する矩形板)。厚 0 で「設置しない」。 ---
        /// <summary>上流胸壁 厚(Y方向)[m]。0 で設置しない。</summary>
        public decimal UpstreamBreastThickness { get; set; } = 0.000m;
        /// <summary>上流胸壁 幅(X方向)[m]。外形幅以上にすること。</summary>
        public decimal UpstreamBreastWidth { get; set; } = 4.000m;
        /// <summary>上流胸壁 高(底版下面からの高さ)[m]。外形高以上にすること。</summary>
        public decimal UpstreamBreastHeight { get; set; } = 4.000m;
        /// <summary>下流胸壁 厚(Y方向)[m]。0 で設置しない。</summary>
        public decimal DownstreamBreastThickness { get; set; } = 0.000m;
        /// <summary>下流胸壁 幅(X方向)[m]</summary>
        public decimal DownstreamBreastWidth { get; set; } = 4.000m;
        /// <summary>下流胸壁 高(底版下面からの高さ)[m]</summary>
        public decimal DownstreamBreastHeight { get; set; } = 4.000m;

        // --- しゃ水壁(函体外周に全周一律で張り出すカラー)。枚数 0 で「設置しない」。 ---
        /// <summary>しゃ水壁 枚数。函体延長を n 等分した各区間の中央に配置する。0 で設置しない。</summary>
        public int CutoffCount { get; set; } = 0;
        /// <summary>しゃ水壁 厚(Y方向)[m]</summary>
        public decimal CutoffThickness { get; set; } = 0.500m;
        /// <summary>しゃ水壁 張出し量(全周一律。下方にも張り出す)[m]</summary>
        public decimal CutoffProjection { get; set; } = 0.500m;
        /// <summary>ソリッド色(ACI 0〜256)</summary>
        public int ColorIndex { get; set; } = 3;

        /// <summary>配置基準点 X [m](Action の同位置再生成用)。函体中心線の位置。</summary>
        public decimal BaseX { get; set; } = 0.000m;
        /// <summary>配置基準点 Y [m]。上流端の位置。</summary>
        public decimal BaseY { get; set; } = 0.000m;
        /// <summary>配置基準点 Z [m]。上流端における底版下面の高さ。</summary>
        public decimal BaseZ { get; set; } = 0.000m;

        /// <summary>外形幅 B_out = B + 2 × 側壁厚 [m]</summary>
        public decimal OuterWidth
        {
            get { return InnerWidth + 2m * WallThickness; }
        }

        /// <summary>外形高 H_out = H + 頂版厚 + 底版厚 [m]</summary>
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
            d["wall_thickness"] = WallThickness.ToString(inv);
            d["top_slab_thickness"] = TopSlabThickness.ToString(inv);
            d["bottom_slab_thickness"] = BottomSlabThickness.ToString(inv);
            d["barrel_length"] = BarrelLength.ToString(inv);
            d["bottom_slope"] = BottomSlope.ToString(inv);
            d["soil_cover"] = SoilCover.ToString(inv);
            d["block_count"] = BlockCount.ToString(inv);
            d["excavation_margin"] = ExcavationMargin.ToString(inv);
            d["excavation_slope"] = ExcavationSlope.ToString(inv);
            d["foundation_thickness"] = FoundationThickness.ToString(inv);
            d["upstream_breast_thickness"] = UpstreamBreastThickness.ToString(inv);
            d["upstream_breast_width"] = UpstreamBreastWidth.ToString(inv);
            d["upstream_breast_height"] = UpstreamBreastHeight.ToString(inv);
            d["downstream_breast_thickness"] = DownstreamBreastThickness.ToString(inv);
            d["downstream_breast_width"] = DownstreamBreastWidth.ToString(inv);
            d["downstream_breast_height"] = DownstreamBreastHeight.ToString(inv);
            d["cutoff_count"] = CutoffCount.ToString(inv);
            d["cutoff_thickness"] = CutoffThickness.ToString(inv);
            d["cutoff_projection"] = CutoffProjection.ToString(inv);
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
            p.BarrelLength = GetDecimal(d, "barrel_length");
            p.BottomSlope = GetDecimal(d, "bottom_slope");
            p.SoilCover = GetDecimal(d, "soil_cover");
            p.BlockCount = GetInt(d, "block_count");
            p.ExcavationMargin = GetDecimal(d, "excavation_margin");
            p.ExcavationSlope = GetDecimal(d, "excavation_slope");
            p.FoundationThickness = GetDecimal(d, "foundation_thickness");

            // 胸壁・しゃ水壁は第2段階で追加したキー。第1段階で生成したソリッドの XData には存在しないため、
            // 欠けている場合は既定値(= 設置しない)で補い、函体だけのモデルとして読めるようにする。
            // キーが在るのに値が不正な場合は従来どおりエラー停止する。
            p.UpstreamBreastThickness = GetDecimalOrDefault(d, "upstream_breast_thickness", p.UpstreamBreastThickness);
            p.UpstreamBreastWidth = GetDecimalOrDefault(d, "upstream_breast_width", p.UpstreamBreastWidth);
            p.UpstreamBreastHeight = GetDecimalOrDefault(d, "upstream_breast_height", p.UpstreamBreastHeight);
            p.DownstreamBreastThickness = GetDecimalOrDefault(d, "downstream_breast_thickness", p.DownstreamBreastThickness);
            p.DownstreamBreastWidth = GetDecimalOrDefault(d, "downstream_breast_width", p.DownstreamBreastWidth);
            p.DownstreamBreastHeight = GetDecimalOrDefault(d, "downstream_breast_height", p.DownstreamBreastHeight);
            p.CutoffCount = GetIntOrDefault(d, "cutoff_count", p.CutoffCount);
            p.CutoffThickness = GetDecimalOrDefault(d, "cutoff_thickness", p.CutoffThickness);
            p.CutoffProjection = GetDecimalOrDefault(d, "cutoff_projection", p.CutoffProjection);

            p.ColorIndex = GetInt(d, "color_index");
            p.BaseX = GetDecimal(d, "base_x");
            p.BaseY = GetDecimal(d, "base_y");
            p.BaseZ = GetDecimal(d, "base_z");
            return p;
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
