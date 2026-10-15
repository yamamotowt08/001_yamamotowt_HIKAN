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
        /// <summary>頂版より上の上部工(門柱・操作台)。床掘りの平面範囲には含めない。</summary>
        public bool IsSuperstructure { get; set; }

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

            AddProtection(parts, p, p.UpstreamWing, true);
            AddProtection(parts, p, p.DownstreamWing, false);
            AddGate(parts, p);

            // 内空は函体のみを貫通する。胸壁は函体側面より外側にあり開口を塞がないため関与しない。
            parts.Add(Centred("内空", p.InnerWidth, p.BottomSlabThickness, p.BottomSlabThickness + p.InnerHeight,
                0m, l, true));

            return parts.ToArray();
        }

        /// <summary>
        /// 胸壁 1 基ぶん(左右 2 基 × たて壁 + 底版 = 4 ボックス)を追加する。
        /// たて壁は函体方向に垂直な板で、軸位置(川裏函体端からの距離)を中心に置き、
        /// 函体の左右側面に接して外側へ張り出す。函体とは側面で接するだけで重ならない。
        /// 底版のつま先版は近い方の函体端の側(川裏側胸壁なら川裏、川表側胸壁なら川表)、
        /// かかと版は函体中央の側に伸びる。
        /// </summary>
        private static void AddBreast(
            System.Collections.Generic.List<HikanPart> parts,
            HikanParameters p,
            HikanBreastWall w,
            bool upstream)
        {
            if (!w.Exists) { return; }

            decimal half = p.OuterWidth / 2m;
            decimal top = BreastFootingTop(p);
            (decimal Min, decimal Max) foot = BreastFootingRange(w, upstream);

            string end = upstream ? "川裏側" : "川表側";
            for (int k = 0; k < 2; k++)
            {
                bool right = k == 1;
                decimal uMin = right ? half : -half - w.Length;
                decimal uMax = right ? half + w.Length : -half;
                string name = end + "胸壁" + (right ? "右" : "左");

                parts.Add(Box(name + "たて壁", uMin, uMax, top, w.CrownHeight, w.StemStart, w.StemEnd, false));
                parts.Add(Box(name + "底版", uMin, uMax, top - w.FootingThickness, top, foot.Min, foot.Max, false));
            }
        }

        /// <summary>胸壁の底版上面(= たて壁の下端)の高さ。函体の敷高(内空底面 V = 底版厚)にそろえる。</summary>
        public static decimal BreastFootingTop(HikanParameters p)
        {
            return p.BottomSlabThickness;
        }

        /// <summary>
        /// 胸壁底版の S 範囲。底版はたて壁を含むので、これが胸壁全体の S 範囲になる。
        /// 川裏側胸壁(upstream)はつま先版が川裏(−S)側、川表側胸壁はつま先版が川表(+S)側。
        /// </summary>
        public static (decimal Min, decimal Max) BreastFootingRange(HikanBreastWall w, bool upstream)
        {
            return upstream
                ? (w.StemStart - w.ToeLength, w.StemEnd + w.HeelLength)
                : (w.StemStart - w.HeelLength, w.StemEnd + w.ToeLength);
        }

        /// <summary>翼壁の片側の開き tanθ = (端部水路幅 − 内空幅) / (2 × 翼壁長)。端部幅 = 内空幅で 0(平行)。</summary>
        public static decimal WingSlope(HikanParameters p, HikanWingWall w)
        {
            return (w.EndWidth - p.InnerWidth) / (2m * w.Length);
        }

        /// <summary>
        /// 壁面に直交する長さを横断方向 U の長さに直す係数 1 / cosθ = √(1 + tan²θ)。
        /// 始端・終端を S 一定の面で切るので、壁厚 t の U 方向の幅は t / cosθ になる。
        /// </summary>
        public static decimal WingWidthFactor(HikanParameters p, HikanWingWall w)
        {
            decimal k = WingSlope(p, w);
            if (k == 0m) { return 1m; }
            return Sqrt(1m + k * k);
        }

        /// <summary>翼壁の開き角 θ [度](表示用。幅の差から求める派生量)。</summary>
        public static decimal WingFlareAngle(HikanParameters p, HikanWingWall w)
        {
            if (!w.Exists) { return 0m; }
            double rad = System.Math.Atan((double)WingSlope(p, w));
            return (decimal)(rad * 180.0 / System.Math.PI);
        }

        /// <summary>
        /// 翼壁(U 字の 3 面張り水路 = 左右の側壁 2 部材 + 底版 1 部材)。
        /// 川表側は S = L から +S へ、川裏側は S = 0 から −S へ伸びる。
        /// 側壁の内面は函体端で内空側面(U = ±B/2)、翼壁端部で ±EndWidth/2。側壁は内面から背面側へ厚 t(壁面に直交)。
        /// 底版は左右の側壁の外面まで一体で、上面は函体の敷高(V = 函体の底版厚)。側壁は底版の上に立つので重ならない。
        /// </summary>
        public static HikanTaperedPart[] GetWingParts(HikanParameters p)
        {
            System.Collections.Generic.List<HikanTaperedPart> parts = new System.Collections.Generic.List<HikanTaperedPart>();
            AddWing(parts, p, p.UpstreamWing, true);
            AddWing(parts, p, p.DownstreamWing, false);
            return parts.ToArray();
        }

        private static void AddWing(
            System.Collections.Generic.List<HikanTaperedPart> parts,
            HikanParameters p,
            HikanWingWall w,
            bool upstream)
        {
            if (!w.Exists) { return; }

            decimal m = WingWidthFactor(p, w);
            decimal innerStart = p.InnerWidth / 2m;   // 函体端での内面の U(右側)
            decimal innerEnd = w.EndWidth / 2m;       // 翼壁端部での内面の U(右側)

            // S0 < S1 になるよう、川裏側は端部が S0、川表側は函体端が S0。
            decimal sStart = upstream ? 0m : p.BarrelLength;
            decimal sEnd = upstream ? -w.Length : p.BarrelLength + w.Length;
            decimal s0 = upstream ? sEnd : sStart;
            decimal s1 = upstream ? sStart : sEnd;
            decimal in0 = upstream ? innerEnd : innerStart;
            decimal in1 = upstream ? innerStart : innerEnd;
            decimal top0 = upstream ? w.CrownEnd : w.CrownStart;
            decimal top1 = upstream ? w.CrownStart : w.CrownEnd;

            decimal floorTop = WingFloorTop(p);
            decimal outer = w.StemThickness * m;   // 側壁の U 方向の幅

            string end = upstream ? "川裏側" : "川表側";
            for (int k = 0; k < 2; k++)
            {
                bool right = k == 1;
                // 右側の内面からの距離 [0, 側壁幅] で部材を作り、左側は鏡映する。側壁は底版の上に立つ。
                parts.Add(WingBand(end + "翼壁" + (right ? "右" : "左") + "側壁", upstream, right,
                    s0, s1, in0, in1, 0m, outer, floorTop, top0, top1));
            }

            // 底版は左右の側壁の外面まで一体。U は [−(内面 + 側壁幅), +(内面 + 側壁幅)]。
            HikanTaperedPart floor = new HikanTaperedPart();
            floor.Name = end + "翼壁底版";
            floor.Upstream = upstream;
            floor.S0 = s0;
            floor.S1 = s1;
            floor.UL0 = -(in0 + outer);
            floor.UR0 = in0 + outer;
            floor.UL1 = -(in1 + outer);
            floor.UR1 = in1 + outer;
            floor.VBottom = floorTop - w.FloorThickness;
            floor.VTop0 = floorTop;
            floor.VTop1 = floorTop;
            parts.Add(floor);
        }

        /// <summary>
        /// 堤防保護部(翼壁の端部から左右へ張り出す逆 T 字擁壁)。左右 × (たて壁 + 底版) + 左右つながるつま先版。
        /// たて壁は外側の面を翼壁端部にそろえ、U 方向は端部の側壁内面(±端部水路幅/2)から外向きに張出し幅まで。
        /// 底版(たて壁の下 + かかと版)は同じ U 範囲で函体側へ伸び、つま先版は翼壁端部より外側で水路床の下も含めて左右つながる。
        /// 底版は上面 = 敷高、厚さ = 翼壁の底版厚。たて壁・底版は翼壁の側壁・底版と重なり、その分は WingOverlapVolume で差し引く。
        /// </summary>
        private static void AddProtection(
            System.Collections.Generic.List<HikanPart> parts,
            HikanParameters p,
            HikanWingWall w,
            bool upstream)
        {
            if (!w.Exists || !w.Protection.Exists) { return; }
            HikanLeveeProtection g = w.Protection;

            decimal top = WingFloorTop(p);
            decimal bottom = top - w.FloorThickness;
            decimal inner = w.EndWidth / 2m;
            decimal outer = inner + g.Width;
            // 翼壁端部の S と、函体へ向かう向き(川裏側は +S、川表側は −S)。
            decimal end = upstream ? -w.Length : p.BarrelLength + w.Length;
            decimal inward = upstream ? 1m : -1m;
            (decimal Min, decimal Max) stem = Span(end, end + inward * g.StemThickness);
            (decimal Min, decimal Max) footing = Span(end, end + inward * (g.StemThickness + g.HeelLength));
            (decimal Min, decimal Max) toe = Span(end, end - inward * g.ToeLength);

            string name = (upstream ? "川裏側" : "川表側") + "堤防保護部";
            for (int k = 0; k < 2; k++)
            {
                bool right = k == 1;
                decimal uMin = right ? inner : -outer;
                decimal uMax = right ? outer : -inner;
                string side = name + (right ? "右" : "左");
                parts.Add(Box(side + "たて壁", uMin, uMax, top, top + g.StemHeight, stem.Min, stem.Max, false));
                parts.Add(Box(side + "底版", uMin, uMax, bottom, top, footing.Min, footing.Max, false));
            }
            if (g.ToeLength > 0m)
            {
                parts.Add(Box(name + "つま先版", -outer, outer, bottom, top, toe.Min, toe.Max, false));
            }
        }

        /// <summary>
        /// 門柱・操作台。門柱は函体の左右の側壁の上(頂版上面 V = 外形高)から天端 Hc まで立ち、内面を内空側面(±B/2)にそろえる。
        /// 操作台は V = Hc から厚 td、U は門柱の外面から左右に ax、S は門柱の範囲から前後に as 張り出す。
        /// 門柱と頂版、門柱と操作台は面で接するだけで重ならない。他の部材と重ならないことは HikanValidator が保証する。
        /// </summary>
        private static void AddGate(System.Collections.Generic.List<HikanPart> parts, HikanParameters p)
        {
            HikanGateTower g = p.Gate;
            if (!g.Exists) { return; }
            decimal inner = p.InnerWidth / 2m;
            decimal outer = inner + g.ColumnThickness;
            (decimal Min, decimal Max) column = g.ColumnRange;
            (decimal Min, decimal Max) deck = g.DeckRange;

            HikanPart right = Box("門柱右", inner, outer, p.OuterHeight, g.CrownHeight, column.Min, column.Max, false);
            HikanPart left = Box("門柱左", -outer, -inner, p.OuterHeight, g.CrownHeight, column.Min, column.Max, false);
            decimal half = outer + g.DeckSideOverhang;
            HikanPart top = Box("操作台", -half, half, g.CrownHeight, g.CrownHeight + g.DeckThickness, deck.Min, deck.Max, false);
            foreach (HikanPart part in new HikanPart[] { right, left, top })
            {
                part.IsSuperstructure = true;
                parts.Add(part);
            }
        }

        /// <summary>門柱 2 本の体積 [m3] = 2 × Tc × Wc × (Hc − 外形高)。</summary>
        public static decimal GateColumnVolume(HikanParameters p)
        {
            HikanGateTower g = p.Gate;
            if (!g.Exists) { return 0m; }
            return 2m * g.ColumnThickness * g.ColumnWidth * (g.CrownHeight - p.OuterHeight);
        }

        /// <summary>操作台の体積 [m3] = td × (B + 2·Tc + 2·ax) × (Wc + 2·as)。</summary>
        public static decimal GateDeckVolume(HikanParameters p)
        {
            HikanGateTower g = p.Gate;
            if (!g.Exists) { return 0m; }
            return g.DeckThickness
                * (p.InnerWidth + 2m * g.ColumnThickness + 2m * g.DeckSideOverhang)
                * (g.ColumnWidth + 2m * g.DeckAxialOverhang);
        }

        /// <summary>
        /// 門柱・操作台のうち高さ V ≤ ground の部分の体積 [m3]。床掘りの埋戻しから控除する地下占有分に使う
        /// (現地盤より上に出た部分は埋め戻す空間を占めない)。
        /// </summary>
        public static decimal GateVolumeBelow(HikanParameters p, decimal ground)
        {
            decimal v = 0m;
            foreach (HikanPart part in GetParts(p))
            {
                if (!part.IsSuperstructure) { continue; }
                decimal top = part.VMax < ground ? part.VMax : ground;
                if (top > part.VMin)
                {
                    v += (part.UMax - part.UMin) * (top - part.VMin) * (part.SMax - part.SMin);
                }
            }
            return v;
        }

        private static (decimal Min, decimal Max) Span(decimal a, decimal b)
        {
            return a < b ? (a, b) : (b, a);
        }

        /// <summary>
        /// 堤防保護部 1 端ぶんの体積 [m3](翼壁との重なりを含む総量)。部材の和とは別の式で、テストで突き合わせる。
        /// 左右の たて壁 X4·Y3·L3 と 底版 X4·tf·(L3 + L4)、つま先版 (端部水路幅 + 2·X4)·tf·L2。
        /// </summary>
        public static decimal ProtectionVolume(HikanWingWall w)
        {
            if (!w.Exists || !w.Protection.Exists) { return 0m; }
            HikanLeveeProtection g = w.Protection;
            decimal stems = 2m * g.Width * g.StemHeight * g.StemThickness;
            decimal footings = 2m * g.Width * w.FloorThickness * (g.StemThickness + g.HeelLength);
            decimal toe = (w.EndWidth + 2m * g.Width) * w.FloorThickness * g.ToeLength;
            return stems + footings + toe;
        }

        /// <summary>翼壁の底版上面の高さ = 函体の敷高(局所座標では函体の底版厚)。水路床が函体の内空底面と段差なくつながる。</summary>
        public static decimal WingFloorTop(HikanParameters p)
        {
            return p.BottomSlabThickness;
        }

        private static HikanTaperedPart WingBand(
            string name, bool upstream, bool right, decimal s0, decimal s1, decimal in0, decimal in1,
            decimal a, decimal b, decimal vBottom, decimal top0, decimal top1)
        {
            HikanTaperedPart part = new HikanTaperedPart();
            part.Name = name;
            part.Upstream = upstream;
            part.S0 = s0;
            part.S1 = s1;
            if (right)
            {
                part.UL0 = in0 + a;
                part.UR0 = in0 + b;
                part.UL1 = in1 + a;
                part.UR1 = in1 + b;
            }
            else
            {
                part.UL0 = -(in0 + b);
                part.UR0 = -(in0 + a);
                part.UL1 = -(in1 + b);
                part.UR1 = -(in1 + a);
            }
            part.VBottom = vBottom;
            part.VTop0 = top0;
            part.VTop1 = top1;
            return part;
        }

        /// <summary>
        /// 翼壁 1 端ぶん(U 字水路)の体積 [m3]。
        /// 側壁 = 2 × 厚 × 壁面長 × (平均天端高 − 底版上面高)(壁面長 = 翼壁長 / cosθ、開き角によらない形)、
        /// 底版 = 底版厚 × 翼壁長 × 平均全幅(全幅 = 水路幅 + 2 × 側壁幅、水路幅は B から EndWidth へ直線)。
        /// HikanTaperedPart のシンプソン積分とは別の式で、テストで突き合わせる。
        /// </summary>
        public static decimal WingWallVolume(HikanParameters p, HikanWingWall w)
        {
            if (!w.Exists) { return 0m; }
            decimal m = WingWidthFactor(p, w);
            decimal wallLength = w.Length * m;
            decimal walls = 2m * w.StemThickness * wallLength * ((w.CrownStart + w.CrownEnd) / 2m - WingFloorTop(p));
            decimal averageWidth = (p.InnerWidth + w.EndWidth) / 2m + 2m * w.StemThickness * m;
            decimal floor = w.FloorThickness * w.Length * averageWidth;
            return walls + floor;
        }

        /// <summary>
        /// 直方体部材と翼壁部材の重なり(体積と 1 次モーメント)。
        /// 函体端近くに置いた胸壁の底版(つま先版)が函体端より外へ出ると、翼壁の底版(厚い場合)などと重なる。
        /// 実形状は BoolUnite で一体化するので、解析解ではこの重なりを 1 回だけ差し引く。
        ///
        /// 重なりの S 断面は矩形 [max(UMin, UL), min(UMax, UR)] × [max(VMin, VBottom), min(VMax, VTop)] で、
        /// 各辺は S の折れ線になる。折れ点(翼壁の辺が直方体の面を横切る位置)で S 区間を分割すれば、
        /// 各小区間では幅も高さも 1 次式なので、シンプソン則で厳密に積分できる。
        /// </summary>
        public static (decimal Volume, decimal MomentU, decimal MomentV, decimal MomentS) Overlap(
            HikanPart box, HikanTaperedPart t)
        {
            decimal a = box.SMin > t.S0 ? box.SMin : t.S0;
            decimal b = box.SMax < t.S1 ? box.SMax : t.S1;
            if (b <= a) { return (0m, 0m, 0m, 0m); }

            System.Collections.Generic.List<decimal> cuts = new System.Collections.Generic.List<decimal>();
            cuts.Add(a);
            cuts.Add(b);
            AddCrossing(cuts, t, t.UL0, t.UL1, box.UMin, a, b);
            AddCrossing(cuts, t, t.UL0, t.UL1, box.UMax, a, b);
            AddCrossing(cuts, t, t.UR0, t.UR1, box.UMin, a, b);
            AddCrossing(cuts, t, t.UR0, t.UR1, box.UMax, a, b);
            AddCrossing(cuts, t, t.VTop0, t.VTop1, box.VMin, a, b);
            AddCrossing(cuts, t, t.VTop0, t.VTop1, box.VMax, a, b);
            AddCrossing(cuts, t, t.VTop0, t.VTop1, t.VBottom, a, b);
            cuts.Sort();

            decimal volume = 0m, mu = 0m, mv = 0m, ms = 0m;
            decimal[] weights = new decimal[] { 1m, 4m, 1m };
            for (int i = 0; i + 1 < cuts.Count; i++)
            {
                decimal c0 = cuts[i];
                decimal c1 = cuts[i + 1];
                if (c1 <= c0) { continue; }
                decimal k = (c1 - c0) / 6m;
                for (int j = 0; j < 3; j++)
                {
                    decimal s = c0 + (c1 - c0) * j / 2m;
                    decimal ul = t.ULAt(s) > box.UMin ? t.ULAt(s) : box.UMin;
                    decimal ur = t.URAt(s) < box.UMax ? t.URAt(s) : box.UMax;
                    decimal vb = t.VBottom > box.VMin ? t.VBottom : box.VMin;
                    decimal vt = t.TopAt(s) < box.VMax ? t.TopAt(s) : box.VMax;
                    decimal area = (ur > ul ? ur - ul : 0m) * (vt > vb ? vt - vb : 0m);
                    volume += k * weights[j] * area;
                    mu += k * weights[j] * area * (ul + ur) / 2m;
                    mv += k * weights[j] * area * (vb + vt) / 2m;
                    ms += k * weights[j] * area * s;
                }
            }
            return (volume, mu, mv, ms);
        }

        /// <summary>翼壁部材の辺 f(S)(S0 で f0、S1 で f1 の 1 次式)が値 target をとる位置を (a, b) 内なら追加する。</summary>
        private static void AddCrossing(
            System.Collections.Generic.List<decimal> cuts, HikanTaperedPart t,
            decimal f0, decimal f1, decimal target, decimal a, decimal b)
        {
            if (f0 == f1 || t.S1 == t.S0) { return; }
            decimal s = t.S0 + (target - f0) * (t.S1 - t.S0) / (f1 - f0);
            if (s > a && s < b) { cuts.Add(s); }
        }

        /// <summary>
        /// 直方体部材(内空を除く)と翼壁部材の重なりの合計体積 [m3]。
        /// upstream が null なら両端、true なら川裏側翼壁、false なら川表側翼壁との重なりだけを数える。
        /// 3 部材以上が重なる点は無い(直方体どうし・翼壁部材どうしの重なりは
        /// 「しゃ水壁 ∩ 函体」以外に無く、それは函体端より内側にしか無い。胸壁と堤防保護部が重ならないことは
        /// HikanValidator が保証する)ので、対ごとの和で厳密。
        /// </summary>
        public static decimal WingOverlapVolume(HikanParameters p, bool? upstream)
        {
            decimal v = 0m;
            HikanPart[] boxes = GetParts(p);
            foreach (HikanTaperedPart t in GetWingParts(p))
            {
                if (upstream.HasValue && t.Upstream != upstream.Value) { continue; }
                foreach (HikanPart box in boxes)
                {
                    if (box.IsVoid) { continue; }
                    v += Overlap(box, t).Volume;
                }
            }
            return v;
        }

        /// <summary>
        /// 翼壁 1 端ぶんの正味体積 [m3]。胸壁などと重なる部分は胸壁側に数え、翼壁からは差し引く。
        /// 部材別の数量を足すとモデル全体の体積に一致する。
        /// </summary>
        public static decimal WingWallNetVolume(HikanParameters p, bool upstream)
        {
            HikanWingWall w = upstream ? p.UpstreamWing : p.DownstreamWing;
            return WingWallVolume(p, w) - WingOverlapVolume(p, upstream);
        }

        /// <summary>
        /// 加算部材(内空を除く)の外接範囲。U は中心線対称なので半幅で返す。
        /// 胸壁の位置が自由で翼壁も外へ伸びるので、床掘り範囲や配置の基準は
        /// 決め打ちの式ではなく、実際の部材範囲(翼壁は頂点)から求める。
        /// </summary>
        public static (decimal HalfWidth, decimal VMin, decimal SMin, decimal SMax) SolidRange(HikanParameters p)
        {
            return Range(p, true);
        }

        /// <summary>
        /// 床掘りの平面・深さに使う外接範囲。SolidRange から上部工(門柱・操作台)を除いたもの。
        /// 操作台は頂版より高い所にあるので、左右に張り出しても床掘りの底幅は広げない。
        /// </summary>
        public static (decimal HalfWidth, decimal VMin, decimal SMin, decimal SMax) FootprintRange(HikanParameters p)
        {
            return Range(p, false);
        }

        private static (decimal HalfWidth, decimal VMin, decimal SMin, decimal SMax) Range(HikanParameters p, bool superstructure)
        {
            decimal half = 0m;
            decimal vMin = 0m;
            decimal sMin = 0m;
            decimal sMax = p.BarrelLength;
            foreach (HikanPart part in GetParts(p))
            {
                if (part.IsVoid) { continue; }
                if (part.IsSuperstructure && !superstructure) { continue; }
                if (part.UMax > half) { half = part.UMax; }
                if (-part.UMin > half) { half = -part.UMin; }
                if (part.VMin < vMin) { vMin = part.VMin; }
                if (part.SMin < sMin) { sMin = part.SMin; }
                if (part.SMax > sMax) { sMax = part.SMax; }
            }
            foreach (HikanTaperedPart part in GetWingParts(p))
            {
                foreach ((decimal U, decimal V, decimal S) v in part.Vertices())
                {
                    if (v.U > half) { half = v.U; }
                    if (-v.U > half) { half = -v.U; }
                    if (v.V < vMin) { vMin = v.V; }
                    if (v.S < sMin) { sMin = v.S; }
                    if (v.S > sMax) { sMax = v.S; }
                }
            }
            return (half, vMin, sMin, sMax);
        }

        /// <summary>
        /// 全部材を通した S の最大値(= 最も川表側)。作図平面での押出し座標 W と S の対応 S = GlobalSMax − W に使う。
        /// 配置変換がこの値ぶん +Y に平行移動するので、函体より川表へ出る部材が無ければ函体延長に一致する。
        /// </summary>
        public static decimal GlobalSMax(HikanParameters p)
        {
            return SolidRange(p).SMax;
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
        /// 胸壁 1 端ぶん(左右 2 基)の体積 [m3]。たて壁 + 底版。
        /// 函体とは側面で接するだけで重ならないので控除は不要。たて壁厚 0 のときは 0。
        /// たて壁は底版上面(敷高)から天端まで。
        /// </summary>
        public static decimal BreastWallVolume(HikanParameters p, HikanBreastWall w)
        {
            if (!w.Exists) { return 0m; }
            decimal stem = w.Length * (w.CrownHeight - BreastFootingTop(p)) * w.StemThickness;
            return 2m * (stem + w.FootingVolume);
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
        /// 胸壁は函体と側面で接するだけ、内空は函体のみを貫通する(BarrelVolume が控除済み)。
        /// 翼壁は函体端より外(川裏 S ≤ 0 / 川表 S ≥ L)にあり、函体とは端面で接するだけだが、
        /// 函体端近くの胸壁の底版が函体端より外へ出ていると重なる。その重なりは WingOverlapVolume で 1 回差し引く。
        /// 堤防保護部は翼壁の側壁・底版と重なり、同じく WingOverlapVolume で差し引く(保護部は総量で数え、翼壁を正味にする)。
        /// </summary>
        public static decimal ModelVolume(HikanParameters p)
        {
            return BarrelVolume(p)
                + BreastWallVolume(p, p.UpstreamBreast)
                + BreastWallVolume(p, p.DownstreamBreast)
                + CutoffVolume(p)
                + WingWallVolume(p, p.UpstreamWing)
                + WingWallVolume(p, p.DownstreamWing)
                + ProtectionVolume(p.UpstreamWing)
                + ProtectionVolume(p.DownstreamWing)
                + GateColumnVolume(p)
                + GateDeckVolume(p)
                - WingOverlapVolume(p, null);
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
                    Extend(ref first, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ, ToWorld(p, u, v, s));
                }
            }
            // 翼壁は凸多面体なので、頂点の min/max がエクステントになる。
            foreach (HikanTaperedPart part in GetWingParts(p))
            {
                foreach ((decimal U, decimal V, decimal S) v in part.Vertices())
                {
                    Extend(ref first, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ, ToWorld(p, v.U, v.V, v.S));
                }
            }
            return (minX, minY, minZ, maxX, maxY, maxZ);
        }

        private static void Extend(
            ref bool first,
            ref decimal minX, ref decimal minY, ref decimal minZ,
            ref decimal maxX, ref decimal maxY, ref decimal maxZ,
            (decimal X, decimal Y, decimal Z) w)
        {
            if (first)
            {
                minX = maxX = w.X; minY = maxY = w.Y; minZ = maxZ = w.Z;
                first = false;
                return;
            }
            if (w.X < minX) { minX = w.X; }
            if (w.X > maxX) { maxX = w.X; }
            if (w.Y < minY) { minY = w.Y; }
            if (w.Y > maxY) { maxY = w.Y; }
            if (w.Z < minZ) { minZ = w.Z; }
            if (w.Z > maxZ) { maxZ = w.Z; }
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

            // 重なり補正: しゃ水壁のボックスは函体と重なるので、その分を 1 回引く。
            // 胸壁は函体と側面で接するだけで重ならないため補正不要。
            decimal collarOverlap = p.OuterWidth * p.OuterHeight * p.CutoffThickness;
            for (int k = 1; k <= p.CutoffCount; k++)
            {
                Accumulate(ref mass, ref mu, ref mv, ref ms, -collarOverlap,
                    0m, p.OuterHeight / 2m, CutoffPosition(p, k));
            }

            // 翼壁はシンプソン積分した体積とモーメントを足し、胸壁などとの重なりを 1 回引く。
            HikanPart[] boxes = GetParts(p);
            foreach (HikanTaperedPart part in GetWingParts(p))
            {
                (decimal Volume, decimal MomentU, decimal MomentV, decimal MomentS) m = part.Moments();
                mass += m.Volume;
                mu += m.MomentU;
                mv += m.MomentV;
                ms += m.MomentS;
                foreach (HikanPart box in boxes)
                {
                    if (box.IsVoid) { continue; }
                    (decimal Volume, decimal MomentU, decimal MomentV, decimal MomentS) o = Overlap(box, part);
                    mass -= o.Volume;
                    mu -= o.MomentU;
                    mv -= o.MomentV;
                    ms -= o.MomentS;
                }
            }

            return ToWorld(p, mu / mass, mv / mass, ms / mass);
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
