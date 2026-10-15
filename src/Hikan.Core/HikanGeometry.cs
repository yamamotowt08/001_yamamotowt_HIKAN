namespace Hikan.Core
{
    /// <summary>
    /// 部材 1 個分の直方体。局所座標で持つ。
    /// U = 函体中心線からの横断オフセット、V = 標準部の底版下面からの高さ、S = 上流端からの下流向き距離。
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
        /// <summary>
        /// 部材の区分(函体 / 川裏側胸壁 / 川表側胸壁 / しゃ水壁 / 川裏側堤防保護部 / 川表側堤防保護部 / 門柱 / 操作台 / 内空)。
        /// 部材どうしの重なりを、どの部材の数量から差し引くかを決めるのに使う。
        /// </summary>
        public string Member { get; set; }

        public decimal Volume
        {
            get { return (UMax - UMin) * (VMax - VMin) * (SMax - SMin); }
        }
    }

    /// <summary>
    /// 断面 (U, V) が多角形で S 方向に一定のプリズム(門柱のハンチなど、直方体でない部材)。
    /// 直方体を前提にした計算(HikanPart の Volume・隅の座標・重なり)に紛れ込まないよう、HikanPart とは別の型にする。
    /// </summary>
    public sealed class HikanPrismPart
    {
        public string Name { get; set; }
        /// <summary>断面の頂点(反時計回り)</summary>
        public (decimal U, decimal V)[] Section { get; set; }
        public decimal SMin { get; set; }
        public decimal SMax { get; set; }

        /// <summary>断面積(靴紐公式)</summary>
        public decimal Area
        {
            get
            {
                decimal a = 0m;
                for (int i = 0; i < Section.Length; i++)
                {
                    (decimal U, decimal V) p = Section[i];
                    (decimal U, decimal V) q = Section[(i + 1) % Section.Length];
                    a += p.U * q.V - q.U * p.V;
                }
                return a / 2m;
            }
        }

        public decimal Volume
        {
            get { return Area * (SMax - SMin); }
        }

        /// <summary>断面の図心(三角形なら頂点の平均。ハンチは三角形のみ)</summary>
        public (decimal U, decimal V) Centroid
        {
            get
            {
                decimal u = 0m, v = 0m;
                foreach ((decimal U, decimal V) p in Section) { u += p.U; v += p.V; }
                return (u / Section.Length, v / Section.Length);
            }
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
        /// <summary>
        /// 構成部材の一覧。加算部材が先、内空が最後。BuildSolid と期待値計算が共有する。
        /// 函体は区間(川裏側端部 / 標準部 / 川表側端部)ごとの外形の直方体で、先頭に並ぶ(区間が 1 つなら名前は「函体」)。
        /// 内空は全長で 1 個の直方体で、内空の隅のハンチは GetBarrelHaunchParts が別に返す。
        /// </summary>
        public static HikanPart[] GetParts(HikanParameters p)
        {
            System.Collections.Generic.List<HikanPart> parts = new System.Collections.Generic.List<HikanPart>();

            decimal l = p.BarrelLength;

            (string Name, decimal SMin, decimal SMax, HikanBarrelSection Section)[] zones = BarrelZones(p);
            foreach ((string Name, decimal SMin, decimal SMax, HikanBarrelSection Section) z in zones)
            {
                decimal half = OuterHalfWidth(p, z.Section);
                string name = zones.Length > 1 ? "函体(" + z.Name + ")" : "函体";
                parts.Add(Tag(Box(name, -half, half, BarrelBottom(p, z.Section), SlabTop(p, z.Section), z.SMin, z.SMax, false), "函体"));
            }

            AddBreast(parts, p, p.UpstreamBreast, true);
            AddBreast(parts, p, p.DownstreamBreast, false);

            decimal a = p.CutoffProjection;
            decimal tc = p.CutoffThickness;
            for (int i = 1; i <= p.CutoffCount; i++)
            {
                decimal s = CutoffPosition(p, i);
                HikanBarrelSection z = SectionOver(p, s - tc / 2m, s + tc / 2m);
                decimal half = OuterHalfWidth(p, z) + a;
                parts.Add(Tag(Box("しゃ水壁" + i, -half, half, BarrelBottom(p, z) - a, SlabTop(p, z) + a,
                    s - tc / 2m, s + tc / 2m, false), "しゃ水壁"));
            }

            AddProtection(parts, p, p.UpstreamWing, true);
            AddProtection(parts, p, p.DownstreamWing, false);
            AddGate(parts, p);
            AddUpstreamRetainingWall(parts, p);

            // 内空は函体のみを貫通する。胸壁は函体側面より外側にあり開口を塞がないため関与しない。
            parts.Add(Tag(Centred("内空", p.InnerWidth, p.BottomSlabThickness, p.BottomSlabThickness + p.InnerHeight,
                0m, l, true), "内空"));

            return parts.ToArray();
        }

        /// <summary>
        /// 函体の区間。川裏側端部 [0, 川裏側端部長]、標準部、川表側端部 [L − 川表側端部長, L] の順。長さ 0 の区間は含めない。
        /// 内空(幅・高さ・敷高)は全区間で共通で、部材厚とハンチが区間ごとに変わる。
        /// </summary>
        public static (string Name, decimal SMin, decimal SMax, HikanBarrelSection Section)[] BarrelZones(HikanParameters p)
        {
            System.Collections.Generic.List<(string Name, decimal SMin, decimal SMax, HikanBarrelSection Section)> zones =
                new System.Collections.Generic.List<(string Name, decimal SMin, decimal SMax, HikanBarrelSection Section)>();
            decimal l = p.BarrelLength;
            decimal a = p.UpstreamEndLength > 0m ? p.UpstreamEndLength : 0m;
            decimal b = p.DownstreamEndLength > 0m ? l - p.DownstreamEndLength : l;
            if (a > 0m) { zones.Add(("川裏側端部", 0m, a, p.UpstreamEnd)); }
            if (b > a) { zones.Add(("標準部", a, b, p.Standard)); }
            if (b < l) { zones.Add(("川表側端部", b, l, p.DownstreamEnd)); }
            return zones.ToArray();
        }

        /// <summary>位置 S の函体の断面。区間の境目は川裏側の区間とする。函体の外は近い方の端の区間。</summary>
        public static HikanBarrelSection SectionAt(HikanParameters p, decimal s)
        {
            (string Name, decimal SMin, decimal SMax, HikanBarrelSection Section)[] zones = BarrelZones(p);
            if (zones.Length == 0) { return p.Standard; }
            if (s <= zones[0].SMin) { return zones[0].Section; }
            foreach ((string Name, decimal SMin, decimal SMax, HikanBarrelSection Section) z in zones)
            {
                if (s <= z.SMax) { return z.Section; }
            }
            return zones[zones.Length - 1].Section;
        }

        /// <summary>
        /// 函体方向の範囲 [a, b] に置く部材(胸壁のたて壁・しゃ水壁・門柱・土留壁)が取り付く函体の断面(範囲の中央の区間)。
        /// 範囲の中で外形が変わらないことは HikanValidator が保証する(OuterStep)。
        /// </summary>
        public static HikanBarrelSection SectionOver(HikanParameters p, decimal a, decimal b)
        {
            return SectionAt(p, (a + b) / 2m);
        }

        /// <summary>範囲 (a, b) の中で函体の外形(部材厚)が変わる境目の S。無ければ null。ハンチの違いは問わない。</summary>
        public static decimal? OuterStep(HikanParameters p, decimal a, decimal b)
        {
            (string Name, decimal SMin, decimal SMax, HikanBarrelSection Section)[] zones = BarrelZones(p);
            for (int i = 1; i < zones.Length; i++)
            {
                decimal s = zones[i].SMin;
                if (s > a && s < b && !zones[i].Section.SameOuter(zones[i - 1].Section)) { return s; }
            }
            return null;
        }

        /// <summary>断面 z の外形の半幅 = B/2 + 側壁厚 [m]</summary>
        public static decimal OuterHalfWidth(HikanParameters p, HikanBarrelSection z)
        {
            return p.InnerWidth / 2m + z.WallThickness;
        }

        /// <summary>断面 z の頂版上面の高さ V = 敷高 + H + 頂版厚 [m]</summary>
        public static decimal SlabTop(HikanParameters p, HikanBarrelSection z)
        {
            return p.BottomSlabThickness + p.InnerHeight + z.TopSlabThickness;
        }

        /// <summary>断面 z の底版下面の高さ V = 敷高 − 底版厚 [m](標準部では 0)</summary>
        public static decimal BarrelBottom(HikanParameters p, HikanBarrelSection z)
        {
            return p.BottomSlabThickness - z.BottomSlabThickness;
        }

        /// <summary>
        /// 函体の内空の隅のハンチ(区間ごと・4 隅)。高さ H(側壁に沿う)× 幅 W(頂版・底版に沿う)の直角三角形の柱で、
        /// 内空の中にあって側壁・頂版・底版の内面と面で接するだけ。他の部材は内空に入らないので重ならない。
        /// 生成では内空を抜いたあとに BoolUnite する。
        /// </summary>
        public static HikanPrismPart[] GetBarrelHaunchParts(HikanParameters p)
        {
            System.Collections.Generic.List<HikanPrismPart> parts = new System.Collections.Generic.List<HikanPrismPart>();
            decimal u = p.InnerWidth / 2m;
            decimal bottom = p.BottomSlabThickness;
            decimal top = bottom + p.InnerHeight;
            (string Name, decimal SMin, decimal SMax, HikanBarrelSection Section)[] zones = BarrelZones(p);
            foreach ((string Name, decimal SMin, decimal SMax, HikanBarrelSection Section) z in zones)
            {
                string name = "函体ハンチ" + (zones.Length > 1 ? "(" + z.Name + ")" : "");
                HikanBarrelSection c = z.Section;
                // 頂点は反時計回り(断面積が正)。
                AddBarrelHaunch(parts, name + "LT", c.LeftTop, z.SMin, z.SMax,
                    (-u, top - c.LeftTop.Height), (-u + c.LeftTop.Width, top), (-u, top));
                AddBarrelHaunch(parts, name + "RT", c.RightTop, z.SMin, z.SMax,
                    (u - c.RightTop.Width, top), (u, top - c.RightTop.Height), (u, top));
                AddBarrelHaunch(parts, name + "LB", c.LeftBottom, z.SMin, z.SMax,
                    (-u, bottom), (-u + c.LeftBottom.Width, bottom), (-u, bottom + c.LeftBottom.Height));
                AddBarrelHaunch(parts, name + "RB", c.RightBottom, z.SMin, z.SMax,
                    (u - c.RightBottom.Width, bottom), (u, bottom), (u, bottom + c.RightBottom.Height));
            }
            return parts.ToArray();
        }

        private static void AddBarrelHaunch(
            System.Collections.Generic.List<HikanPrismPart> parts, string name, HikanHaunch h, decimal sMin, decimal sMax,
            (decimal U, decimal V) a, (decimal U, decimal V) b, (decimal U, decimal V) c)
        {
            if (!h.Exists) { return; }
            HikanPrismPart part = new HikanPrismPart();
            part.Name = name;
            part.Section = new (decimal U, decimal V)[] { a, b, c };
            part.SMin = sMin;
            part.SMax = sMax;
            parts.Add(part);
        }

        /// <summary>函体の内空のハンチの体積の合計 [m3] = Σ 区間長 × その区間のハンチ断面積。</summary>
        public static decimal BarrelHaunchVolume(HikanParameters p)
        {
            decimal v = 0m;
            foreach ((string Name, decimal SMin, decimal SMax, HikanBarrelSection Section) z in BarrelZones(p))
            {
                v += z.Section.HaunchArea * (z.SMax - z.SMin);
            }
            return v;
        }

        private static HikanPart Tag(HikanPart part, string member)
        {
            part.Member = member;
            return part;
        }

        /// <summary>
        /// 胸壁 1 基ぶん(左右のたて壁 2 ボックス + 左右つながった底版 1 ボックス)を追加する。
        /// たて壁は函体方向に垂直な板で、軸位置(川裏函体端からの距離)を中心に置き、
        /// 函体の左右側面に接して外側へ張り出す(函体とは側面で接するだけ)。
        /// 底版は左右の張出し端から端まで 1 枚で、函体の下を通って左右がつながる(上面 = 敷高)。
        /// 函体の範囲では函体の底版と重なり、その分は BoxOverlapVolume で 1 回だけ差し引く。
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

            // たて壁は取り付く区間の函体の側面に接する。底版の左右の幅も同じ外形幅から決める。
            decimal half = OuterHalfWidth(p, SectionOver(p, w.StemStart, w.StemEnd));
            decimal top = BreastFootingTop(p);
            (decimal Min, decimal Max) foot = BreastFootingRange(w, upstream);

            string member = (upstream ? "川裏側" : "川表側") + "胸壁";
            for (int k = 0; k < 2; k++)
            {
                bool right = k == 1;
                decimal uMin = right ? half : -half - w.Length;
                decimal uMax = right ? half + w.Length : -half;
                parts.Add(Tag(Box(member + (right ? "右" : "左") + "たて壁", uMin, uMax, top, w.CrownHeight,
                    w.StemStart, w.StemEnd, false), member));
            }
            parts.Add(Tag(Box(member + "底版", -half - w.Length, half + w.Length, top - w.FootingThickness, top,
                foot.Min, foot.Max, false), member));
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
                parts.Add(Tag(Box(side + "たて壁", uMin, uMax, top, w.CrownEnd, stem.Min, stem.Max, false), name));
                parts.Add(Tag(Box(side + "底版", uMin, uMax, bottom, top, footing.Min, footing.Max, false), name));
            }
            if (g.ToeLength > 0m)
            {
                parts.Add(Tag(Box(name + "つま先版", -outer, outer, bottom, top, toe.Min, toe.Max, false), name));
            }
        }

        /// <summary>
        /// 門柱・操作台。門柱は函体の左右の側壁の上(頂版上面 V = 外形高)から柱高さ Y2 まで立つ L 字の柱で、
        /// 内面(横方向壁の内側の端)を内空側面(±B/2)にそろえる。重ならない直方体に分けて作る:
        ///   横方向壁 … U [B/2, B/2 + X8 + X7] × S [柱の川裏側の面, + X5]
        ///   縦方向壁 … U [B/2 + X8, B/2 + X8 + X7] × S [横方向壁の川表側の面, + X4](内側の X8 × X4 は切欠き)
        /// 操作台は V = 外形高 + Y2 から厚 Y1、U は ±X2/2、S は門柱の範囲 ± X3。管理橋を設置するなら、
        /// 川裏側の端の上面を W1 × W2(中央)× 深さ H1 で欠く(切欠きを避けて直方体に分ける)。
        /// 門柱は胸壁の底版の上面(敷高)から立つので、函体の側壁と重なる(函体の幅の内側、敷高〜頂版上面)。
        /// 胸壁のたて壁とも重なり得る。重なりは BoxOverlapVolume で 1 回だけ差し引き、数量では函体・胸壁に含める。
        /// </summary>
        private static void AddGate(System.Collections.Generic.List<HikanPart> parts, HikanParameters p)
        {
            HikanGateTower g = p.Gate;
            if (!g.Exists) { return; }
            decimal inner = p.InnerWidth / 2m;
            decimal bottom = BreastFootingTop(p);   // 門柱は胸壁の底版の上面(敷高)から立つ
            decimal top = GateColumnTop(p);
            (decimal Min, decimal Max) column = GateColumnRange(p);
            decimal slab = GateSlabTop(p);          // 土留壁の下端 = 頂版上面
            decimal split = column.Min + g.TransverseWallThickness;

            System.Collections.Generic.List<HikanPart> gate = new System.Collections.Generic.List<HikanPart>();
            for (int k = 0; k < 2; k++)
            {
                bool right = k == 1;
                string side = "門柱" + (right ? "右" : "左");
                gate.Add(Mirrored(side + "横方向壁", right, inner, inner + g.ColumnBreadth,
                    bottom, top, column.Min, split));
                gate.Add(Mirrored(side + "縦方向壁", right, inner + g.TransverseWallWidth, inner + g.ColumnBreadth,
                    bottom, top, split, column.Max));
            }
            // 土留壁: 左右の門柱(横方向壁)の間をふさぐ。下端 = 頂版上面、背面 = 門柱の背面、厚さ = X5。
            gate.Add(Box("土留壁", -inner, inner, slab, slab + g.RetainingWallHeight, column.Min, split, false));

            (decimal Min, decimal Max) deck = GateDeckRange(p);
            decimal half = g.DeckWidth / 2m;
            decimal deckTop = top + g.DeckThickness;
            if (!g.Bridge)
            {
                gate.Add(Box("操作台", -half, half, top, deckTop, deck.Min, deck.Max, false));
            }
            else
            {
                // 受け台(桁受けの切欠き)は川裏側(S の小さい側)の端。切欠きの周りを 4 つの直方体に分ける。
                decimal seatEnd = deck.Min + g.SeatLength;
                decimal seatBottom = deckTop - g.SeatDepth;
                decimal seatHalf = g.SeatWidth / 2m;
                gate.Add(Box("操作台", -half, half, top, deckTop, seatEnd, deck.Max, false));
                gate.Add(Box("操作台受け台下", -half, half, top, seatBottom, deck.Min, seatEnd, false));
                gate.Add(Box("操作台受け台左", -half, -seatHalf, seatBottom, deckTop, deck.Min, seatEnd, false));
                gate.Add(Box("操作台受け台右", seatHalf, half, seatBottom, deckTop, deck.Min, seatEnd, false));
            }

            foreach (HikanPart part in gate)
            {
                // 縦方向壁幅 X4 や横方向壁幅 X8 が 0 の場合など、厚さ 0 の直方体は作らない(矩形の柱になる)。
                if (part.UMax <= part.UMin || part.VMax <= part.VMin || part.SMax <= part.SMin) { continue; }
                part.IsSuperstructure = true;
                part.Member = part.Name.StartsWith("門柱", System.StringComparison.Ordinal) ? "門柱"
                    : part.Name == "土留壁" ? "土留壁" : "操作台";
                parts.Add(part);
            }
        }

        /// <summary>
        /// 門柱の S 範囲。門柱の川裏側の面(横方向壁の面 = 背面)を川表側胸壁のたて壁の川裏側の面(背面)にそろえるので、
        /// [川表側胸壁のたて壁の川裏側の面, + X4 + X5]。門柱の位置は入力しない。
        /// </summary>
        public static (decimal Min, decimal Max) GateColumnRange(HikanParameters p)
        {
            decimal start = p.DownstreamBreast.StemStart;
            return (start, start + p.Gate.ColumnLength);
        }

        /// <summary>操作台の S 範囲 = 門柱の範囲 ± X3。門柱を含むので、門柱・操作台全体の S 範囲でもある。</summary>
        public static (decimal Min, decimal Max) GateDeckRange(HikanParameters p)
        {
            (decimal Min, decimal Max) c = GateColumnRange(p);
            return (c.Min - p.Gate.DeckAxialOverhang, c.Max + p.Gate.DeckAxialOverhang);
        }

        /// <summary>門柱が立つ区間の函体の頂版上面の高さ(土留壁の下端)。</summary>
        public static decimal GateSlabTop(HikanParameters p)
        {
            (decimal Min, decimal Max) column = GateColumnRange(p);
            return SlabTop(p, SectionOver(p, column.Min, column.Max));
        }

        /// <summary>門柱の天端(= 操作台の下面)の高さ = 胸壁の底版上面(敷高)+ 柱高さ Y2。</summary>
        public static decimal GateColumnTop(HikanParameters p)
        {
            return BreastFootingTop(p) + p.Gate.ColumnHeight;
        }

        /// <summary>
        /// 川裏側の土留壁(川裏側胸壁の左右のたて壁の間、頂版の上)。U は函体の外形幅、V は頂版上面から高さぶん、
        /// S は川裏側胸壁のたて壁の背面(函体中央側の面)から川裏側へ厚さぶん。函体・たて壁とは面で接するだけ。
        /// </summary>
        private static void AddUpstreamRetainingWall(System.Collections.Generic.List<HikanPart> parts, HikanParameters p)
        {
            HikanRetainingWall r = p.UpstreamRetainingWall;
            if (!r.Exists || !p.UpstreamBreast.Exists) { return; }
            decimal back = p.UpstreamBreast.StemEnd;
            HikanBarrelSection z = SectionOver(p, back - r.Thickness, back);
            decimal top = SlabTop(p, z);
            HikanPart wall = Centred("川裏側土留壁", 2m * OuterHalfWidth(p, z), top, top + r.Height,
                back - r.Thickness, back, false);
            wall.Member = "川裏側土留壁";
            wall.IsSuperstructure = true;
            parts.Add(wall);
        }

        /// <summary>川裏側の土留壁の体積 [m3] = 外形幅(取り付く区間)× 厚さ × 高さ。</summary>
        public static decimal UpstreamRetainingWallVolume(HikanParameters p)
        {
            HikanRetainingWall r = p.UpstreamRetainingWall;
            if (!r.Exists || !p.UpstreamBreast.Exists) { return 0m; }
            return 2m * OuterHalfWidth(p, UpstreamRetainingWallSection(p)) * r.Thickness * r.Height;
        }

        /// <summary>川裏側の土留壁が載る区間の函体の断面。</summary>
        public static HikanBarrelSection UpstreamRetainingWallSection(HikanParameters p)
        {
            decimal back = p.UpstreamBreast.StemEnd;
            return SectionOver(p, back - p.UpstreamRetainingWall.Thickness, back);
        }

        /// <summary>右側(U ≥ 0)の範囲 [a, b] で直方体を作り、左側なら中心線について鏡映する。</summary>
        private static HikanPart Mirrored(string name, bool right, decimal a, decimal b,
            decimal vMin, decimal vMax, decimal sMin, decimal sMax)
        {
            return right
                ? Box(name, a, b, vMin, vMax, sMin, sMax, false)
                : Box(name, -b, -a, vMin, vMax, sMin, sMax, false);
        }

        /// <summary>
        /// 門柱 2 本の体積 [m3] = 2 × Y2 × (X5 × (X7 + X8) + X4 × X7)(L 字の断面積 × 高さ)+ ハンチ。
        /// </summary>
        public static decimal GateColumnVolume(HikanParameters p)
        {
            HikanGateTower g = p.Gate;
            if (!g.Exists) { return 0m; }
            decimal area = g.TransverseWallThickness * g.ColumnBreadth + g.LongitudinalWallWidth * g.LongitudinalWallThickness;
            return 2m * g.ColumnHeight * area + GateHaunchVolume(p);
        }

        /// <summary>門柱のハンチ 2 か所の体積 [m3] = 2 × H2² / 2 × X4 = H2² × X4。</summary>
        public static decimal GateHaunchVolume(HikanParameters p)
        {
            HikanGateTower g = p.Gate;
            if (!g.Exists || g.HaunchHeight <= 0m) { return 0m; }
            return g.HaunchHeight * g.HaunchHeight * g.LongitudinalWallWidth;
        }

        /// <summary>土留壁の体積 [m3] = 内空幅 B × X5 × WH。</summary>
        public static decimal GateRetainingWallVolume(HikanParameters p)
        {
            HikanGateTower g = p.Gate;
            if (!g.Exists || g.RetainingWallHeight <= 0m) { return 0m; }
            return p.InnerWidth * g.TransverseWallThickness * g.RetainingWallHeight;
        }

        /// <summary>
        /// 門柱のハンチ(左右 2 か所)。縦方向壁の内面(切欠き側、U = ±(B/2 + X8))と操作台下面の隅に付く、
        /// 高さ・幅とも H2 の三角(1:1)で、縦方向壁の区間(S = 横方向壁の川表側の面 〜 門柱の川表側の面、X4)に付ける。
        /// 縦方向壁の内面・操作台の下面と面で接するだけで、他の部材とは重ならない(HikanValidator が H2 &lt; B/2 + X8 を保証)。
        /// </summary>
        public static HikanPrismPart[] GetHaunchParts(HikanParameters p)
        {
            HikanGateTower g = p.Gate;
            if (!g.Exists || g.HaunchHeight <= 0m) { return new HikanPrismPart[0]; }
            decimal inner = p.InnerWidth / 2m + g.TransverseWallWidth;   // 縦方向壁の内面
            decimal top = GateColumnTop(p);
            decimal h = g.HaunchHeight;
            (decimal Min, decimal Max) column = GateColumnRange(p);
            decimal start = column.Min + g.TransverseWallThickness;

            HikanPrismPart right = new HikanPrismPart();
            right.Name = "門柱右ハンチ";
            right.Section = new (decimal U, decimal V)[] { (inner - h, top), (inner, top - h), (inner, top) };
            right.SMin = start;
            right.SMax = column.Max;
            HikanPrismPart left = new HikanPrismPart();
            left.Name = "門柱左ハンチ";
            left.Section = new (decimal U, decimal V)[] { (-inner, top - h), (-inner + h, top), (-inner, top) };
            left.SMin = start;
            left.SMax = column.Max;
            return new HikanPrismPart[] { right, left };
        }

        /// <summary>操作台の体積 [m3] = Y1 × X1 × X2 − (管理橋ありなら)W1 × W2 × H1。</summary>
        public static decimal GateDeckVolume(HikanParameters p)
        {
            HikanGateTower g = p.Gate;
            if (!g.Exists) { return 0m; }
            decimal v = g.DeckThickness * g.DeckLength * g.DeckWidth;
            if (g.Bridge) { v -= g.SeatLength * g.SeatWidth * g.SeatDepth; }
            return v;
        }

        /// <summary>
        /// 上部工(門柱・操作台・川表側と川裏側の土留壁)のうち高さ V ≥ ground の部分の体積 [m3](他の部材との重なりは除く)。
        /// 床掘りの地下占有体積は、外形エンベロープからこれを引いて求める(現地盤より上の上部工は埋め戻す空間を占めない)。
        /// 上部工が函体・胸壁のたて壁と重なる部分は函体・胸壁に数えるので、ここには含めない。
        /// </summary>
        public static decimal SuperstructureVolumeAbove(HikanParameters p, decimal ground)
        {
            HikanPart[] parts = GetParts(p);
            decimal v = 0m;
            foreach (HikanPart part in parts)
            {
                if (!part.IsSuperstructure) { continue; }
                HikanPart above = ClipAbove(part, ground);
                if (above == null) { continue; }
                v += above.Volume;
                foreach (HikanPart other in parts)
                {
                    if (other.IsVoid || other.IsSuperstructure) { continue; }
                    v -= BoxOverlap(above, other).Volume;
                }
            }
            v += GateHaunchVolume(p);
            HikanGateTower g = p.Gate;
            foreach (HikanPrismPart part in GetHaunchParts(p))
            {
                decimal h = ground - (GateColumnTop(p) - g.HaunchHeight);
                if (h <= 0m) { continue; }
                if (h > g.HaunchHeight) { h = g.HaunchHeight; }
                v -= h * h / 2m * (part.SMax - part.SMin);
            }
            return v;
        }

        /// <summary>直方体の V ≥ ground の部分(無ければ null)。</summary>
        private static HikanPart ClipAbove(HikanPart part, decimal ground)
        {
            if (part.VMax <= ground) { return null; }
            HikanPart c = Box(part.Name, part.UMin, part.UMax, part.VMin > ground ? part.VMin : ground, part.VMax,
                part.SMin, part.SMax, false);
            c.Member = part.Member;
            return c;
        }

        private static (decimal Min, decimal Max) Span(decimal a, decimal b)
        {
            return a < b ? (a, b) : (b, a);
        }

        /// <summary>堤防保護部のたて壁高 Y3 = 翼壁端部の天端 H1 − 敷高(たて壁の上面を H1 にそろえる)[m]。</summary>
        public static decimal ProtectionStemHeight(HikanParameters p, HikanWingWall w)
        {
            return w.CrownEnd - WingFloorTop(p);
        }

        /// <summary>
        /// 堤防保護部 1 端ぶんの体積 [m3](翼壁との重なりを含む総量)。部材の和とは別の式で、テストで突き合わせる。
        /// 左右の たて壁 X4·Y3·L3 と 底版 X4·tf·(L3 + L4)、つま先版 (端部水路幅 + 2·X4)·tf·L2。Y3 = H1 − 敷高。
        /// </summary>
        public static decimal ProtectionVolume(HikanParameters p, HikanWingWall w)
        {
            if (!w.Exists || !w.Protection.Exists) { return 0m; }
            HikanLeveeProtection g = w.Protection;
            decimal stems = 2m * g.Width * ProtectionStemHeight(p, w) * g.StemThickness;
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

        /// <summary>標準部の外形断面積 [m2]</summary>
        public static decimal OuterSectionArea(HikanParameters p)
        {
            return p.OuterWidth * p.OuterHeight;
        }

        /// <summary>標準部の内空断面積 = B × H − ハンチ [m2]</summary>
        public static decimal InnerSectionArea(HikanParameters p)
        {
            return p.InnerWidth * p.InnerHeight - p.Standard.HaunchArea;
        }

        /// <summary>標準部のコンクリート断面積 = 外形 − 内空 [m2]</summary>
        public static decimal SectionArea(HikanParameters p)
        {
            return OuterSectionArea(p) - InnerSectionArea(p);
        }

        /// <summary>断面 z のコンクリート断面積 = 外形 − (B × H − ハンチ) [m2]</summary>
        public static decimal SectionArea(HikanParameters p, HikanBarrelSection z)
        {
            decimal outer = 2m * OuterHalfWidth(p, z) * (SlabTop(p, z) - BarrelBottom(p, z));
            return outer - (p.InnerWidth * p.InnerHeight - z.HaunchArea);
        }

        /// <summary>函体のみのコンクリート体積 [m3](胸壁・しゃ水壁を含まない)= Σ 区間長 × 区間の断面積(ハンチを含む)。</summary>
        public static decimal BarrelVolume(HikanParameters p)
        {
            decimal v = 0m;
            foreach ((string Name, decimal SMin, decimal SMax, HikanBarrelSection Section) z in BarrelZones(p))
            {
                v += SectionArea(p, z.Section) * (z.SMax - z.SMin);
            }
            return v;
        }

        /// <summary>
        /// 胸壁 1 端ぶんの体積 [m3](正味)。左右のたて壁 + 左右つながった底版 − 函体との重なり。
        /// たて壁は底版上面(敷高)から天端まで。底版は (外形幅 + 2 × 張出し長) × 底版厚 × (つま先 + 厚 + かかと)。
        /// 函体の範囲の底版は函体の底版と重なる(外形幅 × min(底版厚, 敷高) × 函体延長内の長さ)ので、
        /// その分は函体の数量に含め、胸壁からは差し引く。たて壁厚 0 のときは 0。
        /// </summary>
        public static decimal BreastWallVolume(HikanParameters p, HikanBreastWall w, bool upstream)
        {
            if (!w.Exists) { return 0m; }
            decimal top = BreastFootingTop(p);
            decimal width = 2m * OuterHalfWidth(p, SectionOver(p, w.StemStart, w.StemEnd));
            decimal stems = 2m * w.Length * (w.CrownHeight - top) * w.StemThickness;
            decimal footing = (width + 2m * w.Length) * w.FootingThickness * w.FootingLength;
            // 函体の底版との重なり(区間ごとに外形幅 × min(底版厚, 胸壁の底版厚) × 区間内の長さ)を差し引く。
            (decimal Min, decimal Max) foot = BreastFootingRange(w, upstream);
            decimal overlap = 0m;
            foreach ((string Name, decimal SMin, decimal SMax, HikanBarrelSection Section) z in BarrelZones(p))
            {
                decimal inside = System.Math.Min(foot.Max, z.SMax) - System.Math.Max(foot.Min, z.SMin);
                if (inside <= 0m) { continue; }
                decimal u = System.Math.Min(OuterHalfWidth(p, z.Section), width / 2m + w.Length);
                overlap += 2m * u * System.Math.Min(w.FootingThickness, z.Section.BottomSlabThickness) * inside;
            }
            return stems + footing - overlap;
        }

        /// <summary>直方体 2 個の共通部分(体積と 1 次モーメント)。</summary>
        public static (decimal Volume, decimal MomentU, decimal MomentV, decimal MomentS) BoxOverlap(HikanPart a, HikanPart b)
        {
            decimal u0 = System.Math.Max(a.UMin, b.UMin), u1 = System.Math.Min(a.UMax, b.UMax);
            decimal v0 = System.Math.Max(a.VMin, b.VMin), v1 = System.Math.Min(a.VMax, b.VMax);
            decimal s0 = System.Math.Max(a.SMin, b.SMin), s1 = System.Math.Min(a.SMax, b.SMax);
            if (u1 <= u0 || v1 <= v0 || s1 <= s0) { return (0m, 0m, 0m, 0m); }
            decimal vol = (u1 - u0) * (v1 - v0) * (s1 - s0);
            return (vol, vol * (u0 + u1) / 2m, vol * (v0 + v1) / 2m, vol * (s0 + s1) / 2m);
        }

        /// <summary>
        /// 加算部材(直方体)どうしの重なりの合計体積 [m3]。しゃ水壁 ∩ 函体、胸壁の底版 ∩ 函体、門柱 ∩ 胸壁のたて壁など。
        /// 3 個以上が重なる点は無い(しゃ水壁と胸壁・門柱が函体方向に重ならないことを HikanValidator が保証し、
        /// 門柱と函体・操作台、たて壁と底版は面で接するだけ)ので、対ごとの和で厳密。
        /// </summary>
        public static decimal BoxOverlapVolume(HikanParameters p)
        {
            return OverlapBetween(p, m => true, m => true);
        }

        /// <summary>区分 a の部材と区分 b の部材の重なりの合計体積 [m3](同じ部材どうし・内空は数えない)。</summary>
        public static decimal OverlapBetween(HikanParameters p, System.Func<string, bool> a, System.Func<string, bool> b)
        {
            HikanPart[] parts = GetParts(p);
            decimal v = 0m;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].IsVoid) { continue; }
                for (int j = i + 1; j < parts.Length; j++)
                {
                    if (parts[j].IsVoid) { continue; }
                    bool pair = (a(parts[i].Member) && b(parts[j].Member)) || (a(parts[j].Member) && b(parts[i].Member));
                    if (pair) { v += BoxOverlap(parts[i], parts[j]).Volume; }
                }
            }
            return v;
        }

        /// <summary>しゃ水壁の合計体積 [m3]。函体と重なる部分を控除した正味(取り付く区間の外形の周りの環)。</summary>
        public static decimal CutoffVolume(HikanParameters p)
        {
            decimal v = 0m;
            decimal a = p.CutoffProjection;
            decimal tc = p.CutoffThickness;
            for (int i = 1; i <= p.CutoffCount; i++)
            {
                decimal s = CutoffPosition(p, i);
                HikanBarrelSection z = SectionOver(p, s - tc / 2m, s + tc / 2m);
                decimal w = 2m * OuterHalfWidth(p, z);
                decimal h = SlabTop(p, z) - BarrelBottom(p, z);
                v += ((w + 2m * a) * (h + 2m * a) - w * h) * tc;
            }
            return v;
        }

        /// <summary>
        /// モデル全体のコンクリート体積 [m3]。勾配の回転は体積を変えない。
        /// 直方体部材の和 − 内空 − 直方体どうしの重なり(BoxOverlapVolume)+ 翼壁 − 翼壁と直方体の重なり
        /// + 門柱のハンチ + 函体の内空のハンチ(内空の中にあるので、内空を引いたあとに足す)。
        /// 3 個以上が重なる点は無い(HikanValidator が保証)ので、重なりは対ごとに 1 回引けば厳密。
        /// </summary>
        public static decimal ModelVolume(HikanParameters p)
        {
            decimal v = 0m;
            foreach (HikanPart part in GetParts(p))
            {
                v += part.IsVoid ? -part.Volume : part.Volume;
            }
            v -= BoxOverlapVolume(p);
            v += WingWallVolume(p, p.UpstreamWing) + WingWallVolume(p, p.DownstreamWing);
            v -= WingOverlapVolume(p, null);
            foreach (HikanPrismPart part in GetHaunchParts(p))
            {
                v += part.Volume;
            }
            v += BarrelHaunchVolume(p);
            return v;
        }

        /// <summary>内空の全長 [m]。胸壁は開口を塞がないので函体延長に等しい。</summary>
        public static decimal VoidLength(HikanParameters p)
        {
            return p.BarrelLength;
        }

        /// <summary>内空の体積 [m3] = B × H × L − 函体のハンチ。</summary>
        public static decimal VoidVolume(HikanParameters p)
        {
            return p.InnerWidth * p.InnerHeight * VoidLength(p) - BarrelHaunchVolume(p);
        }

        /// <summary>
        /// 外形エンベロープ体積 [m3] = コンクリート + 内空。
        /// 内空は埋戻せないので、床掘りからの控除(地下占有体積)にはこちらを使う。
        /// </summary>
        public static decimal EnvelopeVolume(HikanParameters p)
        {
            return ModelVolume(p) + VoidVolume(p);
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
            // ハンチは門柱と操作台の内側・函体の内空にあるので実際には範囲を広げないが、頂点で確かめておく。
            foreach (HikanPrismPart part in AllHaunchParts(p))
            {
                foreach ((decimal U, decimal V) c in part.Section)
                {
                    Extend(ref first, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ, ToWorld(p, c.U, c.V, part.SMin));
                    Extend(ref first, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ, ToWorld(p, c.U, c.V, part.SMax));
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

            // 重なり補正: 直方体どうしの重なり(しゃ水壁 ∩ 函体、胸壁の底版 ∩ 函体、門柱 ∩ 胸壁など)を 1 回引く。
            HikanPart[] all = GetParts(p);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].IsVoid) { continue; }
                for (int j = i + 1; j < all.Length; j++)
                {
                    if (all[j].IsVoid) { continue; }
                    (decimal Volume, decimal MomentU, decimal MomentV, decimal MomentS) o = BoxOverlap(all[i], all[j]);
                    mass -= o.Volume;
                    mu -= o.MomentU;
                    mv -= o.MomentV;
                    ms -= o.MomentS;
                }
            }

            // 門柱のハンチと函体の内空のハンチ(三角柱)。他の部材と重ならない。
            foreach (HikanPrismPart part in AllHaunchParts(p))
            {
                (decimal U, decimal V) c = part.Centroid;
                Accumulate(ref mass, ref mu, ref mv, ref ms, part.Volume, c.U, c.V, (part.SMin + part.SMax) / 2m);
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

        /// <summary>門柱のハンチと函体の内空のハンチ。</summary>
        private static System.Collections.Generic.IEnumerable<HikanPrismPart> AllHaunchParts(HikanParameters p)
        {
            foreach (HikanPrismPart part in GetHaunchParts(p)) { yield return part; }
            foreach (HikanPrismPart part in GetBarrelHaunchParts(p)) { yield return part; }
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
