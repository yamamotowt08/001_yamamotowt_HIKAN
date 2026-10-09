namespace Hikan.Core
{
    /// <summary>
    /// 数量計算。丸めは「四捨五入」(MidpointRounding.AwayFromZero、decimal)で小数3位止め(= 1 mm 単位)。
    /// ジオメトリから確定的に出せる量(コンクリート・型枠)と、
    /// 施工条件の仮定を要する量(床掘り・埋戻)を区別する。信頼度ラベルは HikanReport で付与する。
    /// </summary>
    public static class HikanEstimator
    {
        private const int Digits = 3;

        private static decimal RoundHalfUp(decimal value, int digits)
        {
            return System.Math.Round(value, digits, System.MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// 胸壁 1 端ぶん(左右 2 基)の型枠面積 [m2]。
        /// たて壁は上下流 2 面 + 外側端面 + 天端。函体側面に接する内側端面は計上しない。
        /// 底版は内外の側面 + 両端面 + つま先版・かかと版の上面。地盤と接する下面は計上しない。
        /// </summary>
        private static decimal BreastFormwork(HikanBreastWall w)
        {
            if (!w.Exists) { return 0m; }
            decimal stemHeight = w.CrownHeight - w.FootingTop;
            decimal stem = 2m * w.Length * stemHeight
                + w.StemThickness * stemHeight
                + w.Length * w.StemThickness;
            decimal footing = 2m * w.FootingLength * w.FootingThickness
                + 2m * w.Length * w.FootingThickness
                + w.Length * (w.ToeLength + w.HeelLength);
            return 2m * (stem + footing);
        }

        /// <summary>しゃ水壁の型枠面積 [m2]。外周面 + 前後の環状面。</summary>
        private static decimal CutoffFormwork(HikanParameters p)
        {
            if (p.CutoffCount <= 0) { return 0m; }
            decimal w = p.OuterWidth + 2m * p.CutoffProjection;
            decimal h = p.OuterHeight + 2m * p.CutoffProjection;
            decimal ring = w * h - p.OuterWidth * p.OuterHeight;
            return p.CutoffCount * (2m * (w + h) * p.CutoffThickness + 2m * ring);
        }

        public static HikanEstimate Calculate(HikanParameters p)
        {
            HikanValidator.EnsureValid(p);

            HikanEstimate e = new HikanEstimate();

            decimal bOut = p.OuterWidth;
            decimal hOut = p.OuterHeight;
            decimal l = p.BarrelLength;

            e.OuterWidth = RoundHalfUp(bOut, Digits);
            e.OuterHeight = RoundHalfUp(hOut, Digits);
            e.InvertLevel = RoundHalfUp(p.InvertLevel, Digits);

            e.OuterSectionArea = RoundHalfUp(HikanGeometry.OuterSectionArea(p), Digits);
            e.InnerSectionArea = RoundHalfUp(HikanGeometry.InnerSectionArea(p), Digits);
            e.ConcreteSectionArea = RoundHalfUp(HikanGeometry.SectionArea(p), Digits);

            // 部位別コンクリート。和は断面積×延長に恒等的に一致する(HikanValidator で検査済み)。
            e.TopSlabVolume = RoundHalfUp(bOut * p.TopSlabThickness * l, Digits);
            e.BottomSlabVolume = RoundHalfUp(bOut * p.BottomSlabThickness * l, Digits);
            e.WallVolume = RoundHalfUp(2m * p.WallThickness * p.InnerHeight * l, Digits);
            e.BarrelConcreteVolume = RoundHalfUp(HikanGeometry.BarrelVolume(p), Digits);
            e.ConcreteVolume = RoundHalfUp(HikanGeometry.ModelVolume(p), Digits);

            // 胸壁は左右 2 基の合計(函体と重ならない)。しゃ水壁は函体と重なる部分を控除した正味。
            e.UpstreamBreastVolume = RoundHalfUp(HikanGeometry.BreastWallVolume(p.UpstreamBreast), Digits);
            e.DownstreamBreastVolume = RoundHalfUp(HikanGeometry.BreastWallVolume(p.DownstreamBreast), Digits);
            e.CutoffTotalVolume = RoundHalfUp(HikanGeometry.CutoffVolume(p), Digits);
            e.CutoffSpacing = p.CutoffCount > 0 ? RoundHalfUp(l / p.CutoffCount, Digits) : 0m;

            // 浸透路長: 函体外周に沿う経路。カラー 1 枚につき張出し量の 2 倍(下り + 上り)が加わる。
            // 具体の照査式(レーン則等)は参照文書に無いため信頼度は推定。
            e.SeepagePathLength = RoundHalfUp(
                l + 2m * p.CutoffProjection * p.CutoffCount, Digits);

            // 勾配の回転は体積を変えない。延長 L は斜距離なので水平投影長と落差を出す。
            decimal c = HikanGeometry.SlopeCosine(p);
            e.HorizontalProjection = RoundHalfUp(l * c, Digits);
            e.DropHeight = RoundHalfUp(l * p.BottomSlope * c, Digits);

            // 型枠。面積自体は幾何的に確定だが、どの面を計上するかは実務判断(HikanReport.Notes 参照)。
            e.FormworkInner = RoundHalfUp(2m * (p.InnerWidth + p.InnerHeight) * l, Digits);
            e.FormworkOuterSide = RoundHalfUp(2m * hOut * l, Digits);
            e.FormworkTop = RoundHalfUp(bOut * l, Digits);
            e.FormworkEnd = RoundHalfUp(2m * HikanGeometry.SectionArea(p), Digits);
            e.FormworkBreast = RoundHalfUp(
                BreastFormwork(p.UpstreamBreast) + BreastFormwork(p.DownstreamBreast), Digits);
            // しゃ水壁は外周面 + 前後の環状面。
            e.FormworkCutoff = RoundHalfUp(CutoffFormwork(p), Digits);

            // ブロック割は函体のみに適用する(胸壁・しゃ水壁は別部材)。
            e.BlockLength = RoundHalfUp(l / p.BlockCount, Digits);
            e.ConcretePerBlock = RoundHalfUp(HikanGeometry.BarrelVolume(p) / p.BlockCount, Digits);

            // 床掘り: 四方に法勾配 1:n を付けた角錐台。∫[0,d] (Wb+2nz)(Lb+2nz) dz の厳密解。
            // 平面と深さは全部材の実際の外接範囲から求める。胸壁の位置は自由なので、
            // つま先版が函体端より外に出るとは限らず、決め打ちの式では過大・過小になる。
            (decimal HalfWidth, decimal VMin, decimal SMin, decimal SMax) range = HikanGeometry.SolidRange(p);
            decimal planWidth = 2m * range.HalfWidth;
            decimal planLength = range.SMax - range.SMin;
            decimal below = -range.VMin;

            decimal wb = planWidth + 2m * p.ExcavationMargin;
            decimal lb = planLength + 2m * p.ExcavationMargin;
            decimal d = p.SoilCover + hOut + below + p.FoundationThickness;
            decimal n = p.ExcavationSlope;
            decimal exc = wb * lb * d
                + n * d * d * (wb + lb)
                + 4m * n * n * d * d * d / 3m;

            e.ExcavationBottomWidth = RoundHalfUp(wb, Digits);
            e.ExcavationBottomLength = RoundHalfUp(lb, Digits);
            e.ExcavationDepth = RoundHalfUp(d, Digits);
            e.ExcavationVolume = RoundHalfUp(exc, Digits);

            decimal foundation = wb * lb * p.FoundationThickness;
            // 地下占有体積は外形エンベロープ(内空を埋め戻した状態)。
            // 内空は埋戻さないのでコンクリート体積では控除できない。
            decimal occupied = HikanGeometry.EnvelopeVolume(p);
            e.FoundationVolume = RoundHalfUp(foundation, Digits);
            e.OccupiedVolume = RoundHalfUp(occupied, Digits);
            e.BackfillVolume = RoundHalfUp(exc - occupied - foundation, Digits);
            e.SurplusVolume = RoundHalfUp(occupied + foundation, Digits);

            return e;
        }
    }
}
