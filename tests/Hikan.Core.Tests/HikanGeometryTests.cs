namespace Hikan.Core.Tests
{
    public class HikanGeometryTests
    {
        private static decimal TwiceArea((decimal U, decimal V)[] pts)
        {
            decimal t = 0m;
            for (int i = 0; i < pts.Length; i++)
            {
                (decimal U, decimal V) a = pts[i];
                (decimal U, decimal V) b = pts[(i + 1) % pts.Length];
                t += a.U * b.V - b.U * a.V;
            }
            return t;
        }

        // テスト1: 外形断面は反時計回り、始点 = 左下隅、中心線(U=0)対称。
        [Xunit.Fact]
        public void OuterSectionPoints_CounterClockwise_Symmetric()
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            (decimal U, decimal V)[] pts = Hikan.Core.HikanGeometry.GetOuterSectionPoints(p);
            Xunit.Assert.Equal(4, pts.Length);
            Xunit.Assert.Equal((-1.4m, 0m), pts[0]);
            Xunit.Assert.Equal((1.4m, 0m), pts[1]);
            Xunit.Assert.Equal((1.4m, 2.8m), pts[2]);
            Xunit.Assert.Equal((-1.4m, 2.8m), pts[3]);
            Xunit.Assert.True(TwiceArea(pts) > 0m, "反時計回りであること");
        }

        // テスト2: 内空断面も反時計回り。底版厚ぶん持ち上がっている。
        [Xunit.Fact]
        public void InnerSectionPoints_CounterClockwise_LiftedByBottomSlab()
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            (decimal U, decimal V)[] pts = Hikan.Core.HikanGeometry.GetInnerSectionPoints(p);
            Xunit.Assert.Equal((-1.0m, 0.4m), pts[0]);
            Xunit.Assert.Equal((1.0m, 0.4m), pts[1]);
            Xunit.Assert.Equal((1.0m, 2.4m), pts[2]);
            Xunit.Assert.Equal((-1.0m, 2.4m), pts[3]);
            Xunit.Assert.True(TwiceArea(pts) > 0m, "反時計回りであること");
        }

        // テスト3: 内空は外形の厳密な内側(辺を共有しない)。Region ブーリアンが退化しない前提条件。
        [Xunit.Theory]
        [Xunit.InlineData(0.4, 0.4, 0.4)]
        [Xunit.InlineData(0.4, 0.4, 0.6)]
        [Xunit.InlineData(1.0, 0.5, 0.7)]
        public void InnerLoop_StrictlyInsideOuterLoop(double wall, double top, double bottom)
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.WallThickness = (decimal)wall;
            p.TopSlabThickness = (decimal)top;
            p.BottomSlabThickness = (decimal)bottom;

            (decimal U, decimal V)[] outer = Hikan.Core.HikanGeometry.GetOuterSectionPoints(p);
            (decimal U, decimal V)[] inner = Hikan.Core.HikanGeometry.GetInnerSectionPoints(p);
            decimal minU = outer[0].U;
            decimal maxU = outer[1].U;
            decimal minV = outer[0].V;
            decimal maxV = outer[2].V;

            for (int i = 0; i < inner.Length; i++)
            {
                Xunit.Assert.True(inner[i].U > minU && inner[i].U < maxU, "U が外形の開区間内であること");
                Xunit.Assert.True(inner[i].V > minV && inner[i].V < maxV, "V が外形の開区間内であること");
            }
        }

        // テスト4: 靴紐公式の差 × L = モデル体積(Solid3d の体積検証と同じ式)。
        [Xunit.Fact]
        public void SectionAreaDifference_TimesLength_EqualsModelVolume()
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            decimal area = TwiceArea(Hikan.Core.HikanGeometry.GetOuterSectionPoints(p)) / 2m
                - TwiceArea(Hikan.Core.HikanGeometry.GetInnerSectionPoints(p)) / 2m;
            Xunit.Assert.Equal(Hikan.Core.HikanGeometry.SectionArea(p), area);
            Xunit.Assert.Equal(Hikan.Core.HikanGeometry.ModelVolume(p), area * p.BarrelLength);
        }

        // テスト5: 部位別体積の和 == 全体体積。数量の内訳と幾何の整合を同時に保証する。
        [Xunit.Theory]
        [Xunit.InlineData(0.4, 0.4, 0.4)]
        [Xunit.InlineData(0.4, 0.4, 0.6)]
        [Xunit.InlineData(0.6, 0.5, 0.9)]
        public void PartVolumes_SumTo_ModelVolume(double wall, double top, double bottom)
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.WallThickness = (decimal)wall;
            p.TopSlabThickness = (decimal)top;
            p.BottomSlabThickness = (decimal)bottom;

            decimal parts = p.OuterWidth * p.TopSlabThickness * p.BarrelLength
                + p.OuterWidth * p.BottomSlabThickness * p.BarrelLength
                + 2m * p.WallThickness * p.InnerHeight * p.BarrelLength;
            Xunit.Assert.Equal(Hikan.Core.HikanGeometry.ModelVolume(p), parts);
        }

        // テスト6: 勾配 0 のエクステント。押出し方向と基準点オフセットの期待値。
        [Xunit.Fact]
        public void ExpectedExtents_NoSlope()
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.BaseX = 1.5m;
            p.BaseY = 2.5m;
            p.BaseZ = 0.75m;
            (decimal MinX, decimal MinY, decimal MinZ, decimal MaxX, decimal MaxY, decimal MaxZ) x =
                Hikan.Core.HikanGeometry.ExpectedExtents(p);
            Xunit.Assert.Equal(0.1m, x.MinX);
            Xunit.Assert.Equal(2.9m, x.MaxX);
            Xunit.Assert.Equal(2.5m, x.MinY);
            Xunit.Assert.Equal(22.5m, x.MaxY);
            Xunit.Assert.Equal(0.75m, x.MinZ);
            Xunit.Assert.Equal(3.55m, x.MaxZ);
        }

        // テスト7: 勾配ありのエクステント。下流端が落差ぶん下がる。
        [Xunit.Fact]
        public void ExpectedExtents_WithSlope_DownstreamDrops()
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.BottomSlope = 0.02m;
            (decimal MinX, decimal MinY, decimal MinZ, decimal MaxX, decimal MaxY, decimal MaxZ) x =
                Hikan.Core.HikanGeometry.ExpectedExtents(p);
            decimal c = Hikan.Core.HikanGeometry.SlopeCosine(p);
            Xunit.Assert.Equal(0m, x.MinY);
            Xunit.Assert.Equal(20m * c + 2.8m * 0.02m * c, x.MaxY);
            Xunit.Assert.Equal(-20m * 0.02m * c, x.MinZ);
            Xunit.Assert.Equal(2.8m * c, x.MaxZ);
            Xunit.Assert.True(x.MinZ < 0m, "下流端が基準面より下がること");
        }

        // テスト8: 重心。左右対称なので X は厳密に BaseX、Y は延長中央。
        [Xunit.Fact]
        public void ExpectedCentroid_SymmetricCase()
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.BaseX = 1.5m;
            p.BaseY = 2.5m;
            p.BaseZ = 0.75m;
            (decimal X, decimal Y, decimal Z) g = Hikan.Core.HikanGeometry.ExpectedCentroid(p);
            Xunit.Assert.Equal(1.5m, g.X);
            Xunit.Assert.Equal(12.5m, g.Y);
            Xunit.Assert.Equal(0.75m + 1.4m, g.Z);
        }

        // テスト9: 頂版厚 ≠ 底版厚 のとき重心 Z は外形高の中央からずれる。
        // これが成立しないと Verify で断面の上下反転を検出できない(体積とエクステントは反転でも一致する)。
        [Xunit.Fact]
        public void ExpectedCentroid_DetectsUpsideDown()
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.TopSlabThickness = 0.4m;
            p.BottomSlabThickness = 0.6m;
            (decimal X, decimal Y, decimal Z) g = Hikan.Core.HikanGeometry.ExpectedCentroid(p);
            decimal half = p.OuterHeight / 2m;
            Xunit.Assert.NotEqual(half, g.Z);

            // 上下反転した断面(頂版 0.6 / 底版 0.4)の重心とも異なること
            Hikan.Core.HikanParameters flipped = new Hikan.Core.HikanParameters();
            flipped.TopSlabThickness = 0.6m;
            flipped.BottomSlabThickness = 0.4m;
            (decimal X, decimal Y, decimal Z) gf = Hikan.Core.HikanGeometry.ExpectedCentroid(flipped);
            Xunit.Assert.NotEqual(gf.Z, g.Z);
            Xunit.Assert.Equal(Hikan.Core.HikanGeometry.ModelVolume(flipped), Hikan.Core.HikanGeometry.ModelVolume(p));
        }

        // テスト10: decimal 平方根。勾配 0 では厳密に 1 を返す。
        [Xunit.Fact]
        public void Sqrt_And_SlopeCosine()
        {
            Xunit.Assert.Equal(2m, Hikan.Core.HikanGeometry.Sqrt(4m));
            Xunit.Assert.Equal(0m, Hikan.Core.HikanGeometry.Sqrt(0m));
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            Xunit.Assert.Equal(1m, Hikan.Core.HikanGeometry.SlopeCosine(p));
            decimal r = Hikan.Core.HikanGeometry.Sqrt(2m);
            Xunit.Assert.True(System.Math.Abs(r * r - 2m) < 0.0000000001m, "ニュートン法が収束すること");
        }
    }
}
