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
            e.ConcreteVolume = RoundHalfUp(HikanGeometry.ModelVolume(p), Digits);

            // 勾配の回転は体積を変えない。延長 L は斜距離なので水平投影長と落差を出す。
            decimal c = HikanGeometry.SlopeCosine(p);
            e.HorizontalProjection = RoundHalfUp(l * c, Digits);
            e.DropHeight = RoundHalfUp(l * p.BottomSlope * c, Digits);

            // 型枠。面積自体は幾何的に確定だが、どの面を計上するかは実務判断(HikanReport.Notes 参照)。
            e.FormworkInner = RoundHalfUp(2m * (p.InnerWidth + p.InnerHeight) * l, Digits);
            e.FormworkOuterSide = RoundHalfUp(2m * hOut * l, Digits);
            e.FormworkTop = RoundHalfUp(bOut * l, Digits);
            e.FormworkEnd = RoundHalfUp(2m * HikanGeometry.SectionArea(p), Digits);

            e.BlockLength = RoundHalfUp(l / p.BlockCount, Digits);
            e.ConcretePerBlock = RoundHalfUp(HikanGeometry.ModelVolume(p) / p.BlockCount, Digits);

            // 床掘り: 四方に法勾配 1:n を付けた角錐台。∫[0,d] (Wb+2nz)(Lb+2nz) dz の厳密解。
            decimal wb = bOut + 2m * p.ExcavationMargin;
            decimal lb = l + 2m * p.ExcavationMargin;
            decimal d = p.SoilCover + hOut + p.FoundationThickness;
            decimal n = p.ExcavationSlope;
            decimal exc = wb * lb * d
                + n * d * d * (wb + lb)
                + 4m * n * n * d * d * d / 3m;

            e.ExcavationBottomWidth = RoundHalfUp(wb, Digits);
            e.ExcavationBottomLength = RoundHalfUp(lb, Digits);
            e.ExcavationDepth = RoundHalfUp(d, Digits);
            e.ExcavationVolume = RoundHalfUp(exc, Digits);

            decimal foundation = wb * lb * p.FoundationThickness;
            // 地下占有体積は外形プリズム。内空は埋戻さないので断面積ではなく外形で控除する。
            decimal occupied = bOut * hOut * l;
            e.FoundationVolume = RoundHalfUp(foundation, Digits);
            e.OccupiedVolume = RoundHalfUp(occupied, Digits);
            e.BackfillVolume = RoundHalfUp(exc - occupied - foundation, Digits);
            e.SurplusVolume = RoundHalfUp(occupied + foundation, Digits);

            return e;
        }
    }
}
