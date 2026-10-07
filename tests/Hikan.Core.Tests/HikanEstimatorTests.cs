// InlineData に decimal は使えないため double で受けて decimal に変換する。
namespace Hikan.Core.Tests
{
    public class HikanEstimatorTests
    {
        // 既定値の全派生量。期待値は Python の Decimal で独立計算したもの(クロスチェック)。
        [Xunit.Fact]
        public void Default_Values()
        {
            Hikan.Core.HikanEstimate e = Hikan.Core.HikanEstimator.Calculate(new Hikan.Core.HikanParameters());

            Xunit.Assert.Equal(2.800m, e.OuterWidth);
            Xunit.Assert.Equal(2.800m, e.OuterHeight);
            Xunit.Assert.Equal(0.400m, e.InvertLevel);

            Xunit.Assert.Equal(7.840m, e.OuterSectionArea);
            Xunit.Assert.Equal(4.000m, e.InnerSectionArea);
            Xunit.Assert.Equal(3.840m, e.ConcreteSectionArea);

            Xunit.Assert.Equal(22.400m, e.TopSlabVolume);
            Xunit.Assert.Equal(22.400m, e.BottomSlabVolume);
            Xunit.Assert.Equal(32.000m, e.WallVolume);
            Xunit.Assert.Equal(76.800m, e.ConcreteVolume);

            Xunit.Assert.Equal(20.000m, e.HorizontalProjection);
            Xunit.Assert.Equal(0.000m, e.DropHeight);

            Xunit.Assert.Equal(160.000m, e.FormworkInner);
            Xunit.Assert.Equal(112.000m, e.FormworkOuterSide);
            Xunit.Assert.Equal(56.000m, e.FormworkTop);
            Xunit.Assert.Equal(7.680m, e.FormworkEnd);

            Xunit.Assert.Equal(5.000m, e.BlockLength);
            Xunit.Assert.Equal(19.200m, e.ConcretePerBlock);

            Xunit.Assert.Equal(3.800m, e.ExcavationBottomWidth);
            Xunit.Assert.Equal(21.000m, e.ExcavationBottomLength);
            Xunit.Assert.Equal(4.900m, e.ExcavationDepth);
            Xunit.Assert.Equal(727.960m, e.ExcavationVolume);
            Xunit.Assert.Equal(7.980m, e.FoundationVolume);
            Xunit.Assert.Equal(156.800m, e.OccupiedVolume);
            Xunit.Assert.Equal(563.180m, e.BackfillVolume);
            Xunit.Assert.Equal(164.780m, e.SurplusVolume);
        }

        // 部位別コンクリートの和が合計と一致すること(丸め後も 1 mm 以内)。
        [Xunit.Theory]
        [Xunit.InlineData(0.4, 0.4, 0.4)]
        [Xunit.InlineData(0.4, 0.4, 0.6)]
        [Xunit.InlineData(0.6, 0.5, 0.9)]
        public void PartVolumes_SumTo_Total(double wall, double top, double bottom)
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.WallThickness = (decimal)wall;
            p.TopSlabThickness = (decimal)top;
            p.BottomSlabThickness = (decimal)bottom;
            Hikan.Core.HikanEstimate e = Hikan.Core.HikanEstimator.Calculate(p);
            decimal sum = e.TopSlabVolume + e.BottomSlabVolume + e.WallVolume;
            Xunit.Assert.True(System.Math.Abs(sum - e.ConcreteVolume) <= 0.001m,
                "部位別の和 " + sum + " と合計 " + e.ConcreteVolume + " が一致しません。");
        }

        // 床掘りの角錐台式。n = 0 のとき直方体 Wb × Lb × d に退化すること。
        [Xunit.Fact]
        public void Excavation_DegeneratesToBox_WhenSlopeIsZero()
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.ExcavationSlope = 0m;
            Hikan.Core.HikanEstimate e = Hikan.Core.HikanEstimator.Calculate(p);
            Xunit.Assert.Equal(391.020m, e.ExcavationVolume);
            Xunit.Assert.Equal(e.ExcavationBottomWidth * e.ExcavationBottomLength * e.ExcavationDepth,
                e.ExcavationVolume);
            Xunit.Assert.Equal(226.240m, e.BackfillVolume);
        }

        // 法勾配を付けると掘削が増える(単調性)。
        [Xunit.Theory]
        [Xunit.InlineData(0.0)]
        [Xunit.InlineData(0.5)]
        [Xunit.InlineData(1.0)]
        [Xunit.InlineData(1.5)]
        public void Excavation_IncreasesWithSlope(double n)
        {
            Hikan.Core.HikanParameters zero = new Hikan.Core.HikanParameters();
            zero.ExcavationSlope = 0m;
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.ExcavationSlope = (decimal)n;
            Xunit.Assert.True(Hikan.Core.HikanEstimator.Calculate(p).ExcavationVolume
                >= Hikan.Core.HikanEstimator.Calculate(zero).ExcavationVolume);
        }

        // 埋戻は内空を埋戻し可能な空間として数えないこと(外形プリズムで控除する)。
        [Xunit.Fact]
        public void Backfill_DeductsOuterPrism_NotConcreteOnly()
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            Hikan.Core.HikanEstimate e = Hikan.Core.HikanEstimator.Calculate(p);
            Xunit.Assert.Equal(156.800m, e.OccupiedVolume);
            Xunit.Assert.True(e.OccupiedVolume > e.ConcreteVolume, "控除は外形体積であること");
            Xunit.Assert.Equal(e.ExcavationVolume - e.OccupiedVolume - e.FoundationVolume, e.BackfillVolume);
        }

        // 勾配は体積を変えず、水平投影長と落差にのみ現れる。
        [Xunit.Theory]
        [Xunit.InlineData(0.0, 20.000, 0.000)]
        [Xunit.InlineData(0.01, 19.999, 0.200)]
        [Xunit.InlineData(0.02, 19.996, 0.400)]
        [Xunit.InlineData(0.05, 19.975, 0.999)]
        public void Slope_AffectsProjectionNotVolume(double i, double projection, double drop)
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.BottomSlope = (decimal)i;
            Hikan.Core.HikanEstimate e = Hikan.Core.HikanEstimator.Calculate(p);
            Xunit.Assert.Equal((decimal)projection, e.HorizontalProjection);
            Xunit.Assert.Equal((decimal)drop, e.DropHeight);
            Xunit.Assert.Equal(76.800m, e.ConcreteVolume);
        }

        // 丸めは四捨五入(銀行家丸めだと 0.1235 → 0.123 になる)。
        [Xunit.Fact]
        public void Rounding_IsHalfUp()
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.BarrelLength = 3.2175m;
            Hikan.Core.HikanEstimate e = Hikan.Core.HikanEstimator.Calculate(p);
            // 3.840 × 3.2175 = 12.35520 → そのまま 3 位
            Xunit.Assert.Equal(12.355m, e.ConcreteVolume);
            // ブロック長 3.2175 / 4 = 0.804375 → 0.804
            Xunit.Assert.Equal(0.804m, e.BlockLength);
        }

        // ブロック按分。目地は控除していない(Notes に明記)。
        [Xunit.Fact]
        public void BlockDivision_IsPlainDivision()
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.BlockCount = 5;
            Hikan.Core.HikanEstimate e = Hikan.Core.HikanEstimator.Calculate(p);
            Xunit.Assert.Equal(4.000m, e.BlockLength);
            Xunit.Assert.Equal(15.360m, e.ConcretePerBlock);
            Xunit.Assert.Equal(76.800m, e.ConcreteVolume);
        }
    }
}
