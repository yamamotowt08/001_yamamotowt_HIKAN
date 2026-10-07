namespace Hikan.Core
{
    /// <summary>
    /// 断面座標・体積・生成後検証の期待値。函体は外形矩形から内空矩形を抜いた中空プリズム。
    ///
    /// 断面は局所座標 (U, V) で返す。U = 函体中心線からの横断オフセット、V = 底版下面からの高さ。
    /// AutoCAD 層はこれを作図平面に (U, V) として置き、+Z に BarrelLength だけ押出したあと
    /// X 軸まわり +90 度回転 → (BaseX, BaseY + L, BaseZ) 平行移動 → 底版勾配の回転、の順で配置する。
    /// すなわち (U, V, W) → (BaseX + U, BaseY + L − W, BaseZ + V) を経て勾配回転がかかる。
    /// </summary>
    public static class HikanGeometry
    {
        /// <summary>外形断面(反時計回り、始点 = 左下隅)。</summary>
        public static (decimal U, decimal V)[] GetOuterSectionPoints(HikanParameters p)
        {
            decimal h = p.OuterWidth / 2m;
            return new (decimal U, decimal V)[]
            {
                (-h, 0m),
                (h, 0m),
                (h, p.OuterHeight),
                (-h, p.OuterHeight)
            };
        }

        /// <summary>内空断面(反時計回り、始点 = 左下隅)。外形の厳密な内側にある。</summary>
        public static (decimal U, decimal V)[] GetInnerSectionPoints(HikanParameters p)
        {
            decimal h = p.InnerWidth / 2m;
            decimal v0 = p.BottomSlabThickness;
            decimal v1 = p.BottomSlabThickness + p.InnerHeight;
            return new (decimal U, decimal V)[]
            {
                (-h, v0),
                (h, v0),
                (h, v1),
                (-h, v1)
            };
        }

        public static decimal OuterSectionArea(HikanParameters p)
        {
            return p.OuterWidth * p.OuterHeight;
        }

        public static decimal InnerSectionArea(HikanParameters p)
        {
            return p.InnerWidth * p.InnerHeight;
        }

        /// <summary>コンクリート断面積 = 外形 − 内空 [m2]</summary>
        public static decimal SectionArea(HikanParameters p)
        {
            return OuterSectionArea(p) - InnerSectionArea(p);
        }

        /// <summary>モデル体積(= コンクリート体積)[m3]。勾配の回転は体積を変えない。</summary>
        public static decimal ModelVolume(HikanParameters p)
        {
            return SectionArea(p) * p.BarrelLength;
        }

        /// <summary>勾配 i の回転における cos = 1 / √(1 + i²)。i = 0 のとき厳密に 1。</summary>
        public static decimal SlopeCosine(HikanParameters p)
        {
            if (p.BottomSlope == 0m) { return 1m; }
            return 1m / Sqrt(1m + p.BottomSlope * p.BottomSlope);
        }

        /// <summary>
        /// 生成後の期待エクステント(WCS)。押出し方向・回転符号・基準点オフセットの総合検査に使う。
        /// 底版勾配 i ≥ 0(下流下がり)を前提とする。
        /// </summary>
        public static (decimal MinX, decimal MinY, decimal MinZ, decimal MaxX, decimal MaxY, decimal MaxZ)
            ExpectedExtents(HikanParameters p)
        {
            decimal c = SlopeCosine(p);
            decimal l = p.BarrelLength;
            decimal ho = p.OuterHeight;
            decimal i = p.BottomSlope;
            decimal half = p.OuterWidth / 2m;

            // 局所 (dy, dv) → Y = BaseY + dy·c + dv·i·c, Z = BaseZ − dy·i·c + dv·c
            return (
                p.BaseX - half,
                p.BaseY,
                p.BaseZ - l * i * c,
                p.BaseX + half,
                p.BaseY + l * c + ho * i * c,
                p.BaseZ + ho * c);
        }

        /// <summary>
        /// 生成後の期待重心(WCS)。体積とエクステントだけでは検出できない
        /// 断面の上下反転(頂版厚 ≠ 底版厚 のとき)を検出するために使う。
        /// </summary>
        public static (decimal X, decimal Y, decimal Z) ExpectedCentroid(HikanParameters p)
        {
            decimal ao = OuterSectionArea(p);
            decimal ai = InnerSectionArea(p);
            decimal vo = p.OuterHeight / 2m;
            decimal vi = p.BottomSlabThickness + p.InnerHeight / 2m;
            decimal vc = (ao * vo - ai * vi) / (ao - ai);

            decimal c = SlopeCosine(p);
            decimal i = p.BottomSlope;
            decimal dy = p.BarrelLength / 2m;

            return (
                p.BaseX,
                p.BaseY + dy * c + vc * i * c,
                p.BaseZ - dy * i * c + vc * c);
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
