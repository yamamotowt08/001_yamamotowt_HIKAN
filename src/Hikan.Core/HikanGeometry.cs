namespace Hikan.Core
{
    /// <summary>
    /// 部材 1 個分の直方体。局所座標で持つ。
    /// U = 函体中心線からの横断オフセット、V = 底版下面からの高さ、S = 上流端からの下流向き距離。
    /// IsVoid = true のものは内空で、合成の最後に BoolSubtract する。
    /// </summary>
    public sealed class HikanPart
    {
        public string Name { get; set; }
        public decimal UMin { get; set; }
        public decimal UMax { get; set; }
        public decimal VMin { get; set; }
        public decimal VMax { get; set; }
        public decimal SMin { get; set; }
        public decimal SMax { get; set; }
        public bool IsVoid { get; set; }

        public decimal Volume
        {
            get { return (UMax - UMin) * (VMax - VMin) * (SMax - SMin); }
        }
    }

    /// <summary>
    /// 断面座標・体積・生成後検証の期待値。
    ///
    /// 構造は「中実の直方体部材を BoolUnite し、最後に内空プリズムを 1 回 BoolSubtract」で作る。
    /// 薄いシェル同士を同一平面でブーリアンしないので、部材を増やしても退化しにくい。
    ///
    /// 断面は局所座標 (U, V) で返す。AutoCAD 層はこれを作図平面に置いて +Z に (SMax - SMin) 押出し、
    /// X 軸まわり +90 度回転 → (BaseX, BaseY + SMax, BaseZ) 平行移動 → 底版勾配の回転、の順で配置する。
    /// すなわち (U, V, W) → (BaseX + U, BaseY + SMax − W, BaseZ + V) を経て勾配回転がかかる。
    /// W ∈ [0, SMax − SMin] なので S ∈ [SMin, SMax] に正しく収まる。
    /// </summary>
    public static class HikanGeometry
    {
        /// <summary>構成部材の一覧。加算部材が先、内空が最後。BuildSolid と期待値計算が共有する。</summary>
        public static HikanPart[] GetParts(HikanParameters p)
        {
            System.Collections.Generic.List<HikanPart> parts = new System.Collections.Generic.List<HikanPart>();

            decimal bo = p.OuterWidth;
            decimal ho = p.OuterHeight;
            decimal l = p.BarrelLength;

            parts.Add(Centred("函体", bo, 0m, ho, 0m, l, false));

            AddBreast(parts, p, p.UpstreamBreast, true);
            AddBreast(parts, p, p.DownstreamBreast, false);

            decimal a = p.CutoffProjection;
            decimal tc = p.CutoffThickness;
            for (int i = 1; i <= p.CutoffCount; i++)
            {
                decimal s = CutoffPosition(p, i);
                parts.Add(Centred("しゃ水壁" + i, bo + 2m * a, -a, ho + a, s - tc / 2m, s + tc / 2m, false));
            }

            // 内空は函体のみを貫通する。胸壁は函体側面より外側にあり開口を塞がないため関与しない。
            parts.Add(Centred("内空", p.InnerWidth, p.BottomSlabThickness, p.BottomSlabThickness + p.InnerHeight,
                0m, l, true));

            return parts.ToArray();
        }

        /// <summary>
        /// 胸壁 1 端ぶん(たて壁 + 底版の 2 ボックス)を追加する。
        /// たて壁は函体方向に垂直な 1 枚板で、函体がこれを貫通する(体積は包除で控除する)。
        /// 底版は Y 方向に伸び、つま先版は函体から遠い側、かかと版は近い側。
        /// 底版は根入れにより Z = 0 以下に収まるので函体とは重ならない。
        /// </summary>
        private static void AddBreast(
            System.Collections.Generic.List<HikanPart> parts,
            HikanParameters p,
            HikanBreastWall w,
            bool upstream)
        {
            if (!w.Exists) { return; }

            decimal l = p.BarrelLength;
            decimal stemMin = upstream ? 0m : l - w.StemThickness;
            decimal stemMax = upstream ? w.StemThickness : l;
            decimal footMin = upstream ? -w.ToeLength : l - w.StemThickness - w.HeelLength;
            decimal footMax = upstream ? w.StemThickness + w.HeelLength : l + w.ToeLength;

            string side = upstream ? "上流" : "下流";
            parts.Add(Centred(side + "胸壁たて壁", w.Width, w.FootingTop, w.CrownHeight, stemMin, stemMax, false));
            parts.Add(Centred(side + "胸壁底版", w.Width, -w.Embedment, w.FootingTop, footMin, footMax, false));
        }

        /// <summary>たて壁と函体の重なり体積 [m3]。たて壁が函体外形を完全に含むことを検証済み。</summary>
        public static decimal BreastStemOverlap(HikanParameters p, HikanBreastWall w)
        {
            if (!w.Exists) { return 0m; }
            return p.OuterWidth * p.OuterHeight * w.StemThickness;
        }

        /// <summary>
        /// 全部材を通した S の最大値(= 下流端)。作図平面での押出し座標 W と S の対応 S = GlobalSMax − W に使う。
        /// 配置変換がこの値ぶん +Y に平行移動するので、胸壁が無ければ従来どおり函体延長に一致する。
        /// </summary>
        public static decimal GlobalSMax(HikanParameters p)
        {
            if (p.DownstreamBreast.Exists)
            {
                return p.BarrelLength + p.DownstreamBreast.ToeLength;
            }
            return p.BarrelLength;
        }

        /// <summary>i 枚目(1 始まり)のしゃ水壁の中心位置 S。函体延長を n 等分した各区間の中央。</summary>
        public static decimal CutoffPosition(HikanParameters p, int index)
        {
            return p.BarrelLength * (2m * index - 1m) / (2m * p.CutoffCount);
        }

        private static HikanPart Box(
            string name, decimal uMin, decimal uMax, decimal vMin, decimal vMax, decimal sMin, decimal sMax, bool isVoid)
        {
            HikanPart part = new HikanPart();
            part.Name = name;
            part.UMin = uMin;
            part.UMax = uMax;
            part.VMin = vMin;
            part.VMax = vMax;
            part.SMin = sMin;
            part.SMax = sMax;
            part.IsVoid = isVoid;
            return part;
        }

        /// <summary>中心線 U = 0 に対称なボックス。</summary>
        private static HikanPart Centred(string name, decimal width, decimal vMin, decimal vMax, decimal sMin, decimal sMax, bool isVoid)
        {
            return Box(name, -width / 2m, width / 2m, vMin, vMax, sMin, sMax, isVoid);
        }

        /// <summary>部材断面(反時計回り、始点 = 左下隅)。</summary>
        public static (decimal U, decimal V)[] GetSectionPoints(HikanPart part)
        {
            return new (decimal U, decimal V)[]
            {
                (part.UMin, part.VMin),
                (part.UMax, part.VMin),
                (part.UMax, part.VMax),
                (part.UMin, part.VMax)
            };
        }

        /// <summary>函体の外形断面(反時計回り、始点 = 左下隅)。</summary>
        public static (decimal U, decimal V)[] GetOuterSectionPoints(HikanParameters p)
        {
            return GetSectionPoints(Centred("函体", p.OuterWidth, 0m, p.OuterHeight, 0m, p.BarrelLength, false));
        }

        /// <summary>内空断面(反時計回り、始点 = 左下隅)。外形の厳密な内側にある。</summary>
        public static (decimal U, decimal V)[] GetInnerSectionPoints(HikanParameters p)
        {
            return GetSectionPoints(Centred("内空", p.InnerWidth, p.BottomSlabThickness,
                p.BottomSlabThickness + p.InnerHeight, 0m, p.BarrelLength, true));
        }

        public static decimal OuterSectionArea(HikanParameters p)
        {
            return p.OuterWidth * p.OuterHeight;
        }

        public static decimal InnerSectionArea(HikanParameters p)
        {
            return p.InnerWidth * p.InnerHeight;
        }

        /// <summary>函体のコンクリート断面積 = 外形 − 内空 [m2]</summary>
        public static decimal SectionArea(HikanParameters p)
        {
            return OuterSectionArea(p) - InnerSectionArea(p);
        }

        /// <summary>函体のみのコンクリート体積 [m3](胸壁・しゃ水壁を含まない)。</summary>
        public static decimal BarrelVolume(HikanParameters p)
        {
            return SectionArea(p) * p.BarrelLength;
        }

        /// <summary>
        /// 胸壁 1 端ぶんの体積 [m3]。たて壁(函体との重なりを控除)+ 底版。たて壁厚 0 のときは 0。
        /// </summary>
        public static decimal BreastWallVolume(HikanParameters p, HikanBreastWall w)
        {
            if (!w.Exists) { return 0m; }
            return w.StemGrossVolume - BreastStemOverlap(p, w) + w.FootingVolume;
        }

        /// <summary>しゃ水壁の合計体積 [m3]。函体と重なる部分を控除した正味。</summary>
        public static decimal CutoffVolume(HikanParameters p)
        {
            if (p.CutoffCount <= 0) { return 0m; }
            decimal a = p.CutoffProjection;
            decimal outerArea = (p.OuterWidth + 2m * a) * (p.OuterHeight + 2m * a);
            return p.CutoffCount * (outerArea - OuterSectionArea(p)) * p.CutoffThickness;
        }

        /// <summary>
        /// モデル全体のコンクリート体積 [m3]。勾配の回転は体積を変えない。
        /// 部材の重なりは「しゃ水壁 ∩ 函体」のみで、CutoffVolume が控除済み。
        /// 内空は函体と上下流胸壁を貫通する分を 1 本で控除する。
        /// </summary>
        public static decimal ModelVolume(HikanParameters p)
        {
            return BarrelVolume(p)
                + BreastWallVolume(p, p.UpstreamBreast)
                + BreastWallVolume(p, p.DownstreamBreast)
                + CutoffVolume(p);
        }

        /// <summary>内空の全長 [m]。胸壁は開口を塞がないので函体延長に等しい。</summary>
        public static decimal VoidLength(HikanParameters p)
        {
            return p.BarrelLength;
        }

        /// <summary>
        /// 外形エンベロープ体積 [m3] = コンクリート + 内空。
        /// 内空は埋戻せないので、床掘りからの控除(地下占有体積)にはこちらを使う。
        /// </summary>
        public static decimal EnvelopeVolume(HikanParameters p)
        {
            return ModelVolume(p) + InnerSectionArea(p) * VoidLength(p);
        }

        /// <summary>勾配 i の回転における cos = 1 / √(1 + i²)。i = 0 のとき厳密に 1。</summary>
        public static decimal SlopeCosine(HikanParameters p)
        {
            if (p.BottomSlope == 0m) { return 1m; }
            return 1m / Sqrt(1m + p.BottomSlope * p.BottomSlope);
        }

        /// <summary>局所座標 (U, V, S) を WCS へ写す(基準点オフセット + 底版勾配の回転)。</summary>
        public static (decimal X, decimal Y, decimal Z) ToWorld(HikanParameters p, decimal u, decimal v, decimal s)
        {
            decimal c = SlopeCosine(p);
            decimal i = p.BottomSlope;
            return (
                p.BaseX + u,
                p.BaseY + s * c + v * i * c,
                p.BaseZ - s * i * c + v * c);
        }

        /// <summary>
        /// 生成後の期待エクステント(WCS)。押出し方向・回転符号・基準点オフセットの総合検査に使う。
        /// 加算部材それぞれの 8 隅を写して min/max を取る(極値の隅が存在しない部材構成があるため、
        /// S と V の範囲から式で求めてはいけない)。
        /// </summary>
        public static (decimal MinX, decimal MinY, decimal MinZ, decimal MaxX, decimal MaxY, decimal MaxZ)
            ExpectedExtents(HikanParameters p)
        {
            bool first = true;
            decimal minX = 0m, minY = 0m, minZ = 0m, maxX = 0m, maxY = 0m, maxZ = 0m;

            foreach (HikanPart part in GetParts(p))
            {
                if (part.IsVoid) { continue; }
                for (int k = 0; k < 8; k++)
                {
                    decimal u = ((k & 1) == 0) ? part.UMin : part.UMax;
                    decimal v = ((k & 2) == 0) ? part.VMin : part.VMax;
                    decimal s = ((k & 4) == 0) ? part.SMin : part.SMax;
                    (decimal X, decimal Y, decimal Z) w = ToWorld(p, u, v, s);
                    if (first)
                    {
                        minX = maxX = w.X; minY = maxY = w.Y; minZ = maxZ = w.Z;
                        first = false;
                        continue;
                    }
                    if (w.X < minX) { minX = w.X; }
                    if (w.X > maxX) { maxX = w.X; }
                    if (w.Y < minY) { minY = w.Y; }
                    if (w.Y > maxY) { maxY = w.Y; }
                    if (w.Z < minZ) { minZ = w.Z; }
                    if (w.Z > maxZ) { maxZ = w.Z; }
                }
            }
            return (minX, minY, minZ, maxX, maxY, maxZ);
        }

        /// <summary>
        /// 生成後の期待重心(WCS)。体積とエクステントだけでは検出できない
        /// 断面の上下反転(頂版厚 ≠ 底版厚 のとき)や上下流の取り違えを検出するために使う。
        /// 部材は左右対称に配置されるので X は厳密に BaseX になる。
        /// </summary>
        public static (decimal X, decimal Y, decimal Z) ExpectedCentroid(HikanParameters p)
        {
            decimal mass = 0m;
            decimal mu = 0m;
            decimal mv = 0m;
            decimal ms = 0m;

            foreach (HikanPart part in GetParts(p))
            {
                decimal sign = part.IsVoid ? -1m : 1m;
                Accumulate(ref mass, ref mu, ref mv, ref ms, sign * part.Volume,
                    (part.UMin + part.UMax) / 2m, (part.VMin + part.VMax) / 2m, (part.SMin + part.SMax) / 2m);
            }

            // 重なり補正: しゃ水壁と胸壁たて壁のボックスは函体と重なるので、その分を 1 回引く。
            // 胸壁底版は根入れにより Z = 0 以下に収まるため函体とは重ならない。
            decimal collarOverlap = p.OuterWidth * p.OuterHeight * p.CutoffThickness;
            for (int k = 1; k <= p.CutoffCount; k++)
            {
                Accumulate(ref mass, ref mu, ref mv, ref ms, -collarOverlap,
                    0m, p.OuterHeight / 2m, CutoffPosition(p, k));
            }
            SubtractStemOverlap(ref mass, ref mu, ref mv, ref ms, p, p.UpstreamBreast, true);
            SubtractStemOverlap(ref mass, ref mu, ref mv, ref ms, p, p.DownstreamBreast, false);

            return ToWorld(p, mu / mass, mv / mass, ms / mass);
        }

        private static void SubtractStemOverlap(
            ref decimal mass, ref decimal mu, ref decimal mv, ref decimal ms,
            HikanParameters p, HikanBreastWall w, bool upstream)
        {
            if (!w.Exists) { return; }
            decimal centre = upstream
                ? w.StemThickness / 2m
                : p.BarrelLength - w.StemThickness / 2m;
            Accumulate(ref mass, ref mu, ref mv, ref ms, -BreastStemOverlap(p, w),
                0m, p.OuterHeight / 2m, centre);
        }

        private static void Accumulate(
            ref decimal mass, ref decimal mu, ref decimal mv, ref decimal ms,
            decimal volume, decimal u, decimal v, decimal s)
        {
            mass += volume;
            mu += volume * u;
            mv += volume * v;
            ms += volume * s;
        }

        /// <summary>
        /// decimal の平方根(ニュートン法)。double 経由の初期値を decimal で収束させる。
        /// 丸めを double に依存させないことで、派生量の期待値が環境に依らず再現する。
        /// </summary>
        public static decimal Sqrt(decimal value)
        {
            if (value < 0m)
            {
                throw new HikanValidationException("平方根の引数が負です: " + value);
            }
            if (value == 0m) { return 0m; }

            decimal x = (decimal)System.Math.Sqrt((double)value);
            for (int k = 0; k < 5; k++)
            {
                decimal next = (x + value / x) / 2m;
                if (next == x) { break; }
                x = next;
            }
            return x;
        }
    }
}
