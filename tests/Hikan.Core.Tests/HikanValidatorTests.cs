namespace Hikan.Core.Tests
{
    public class HikanValidatorTests
    {
        private static void AssertInvalid(Hikan.Core.HikanParameters p)
        {
            Xunit.Assert.Throws<Hikan.Core.HikanValidationException>(() => Hikan.Core.HikanEstimator.Calculate(p));
            Xunit.Assert.NotEmpty(Hikan.Core.HikanValidator.Validate(p));
        }

        [Xunit.Fact]
        public void Default_IsValid()
        {
            Xunit.Assert.Empty(Hikan.Core.HikanValidator.Validate(new Hikan.Core.HikanParameters()));
        }

        // テスト2: 最小部材厚 0.40 m(基準)。0.40 は可、0.35 は不可。
        [Xunit.Theory]
        [Xunit.InlineData(0.40, true)]
        [Xunit.InlineData(0.35, false)]
        [Xunit.InlineData(0.30, false)]
        public void WallThickness_MinimumIsFortyCentimetres(double t, bool valid)
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.WallThickness = (decimal)t;
            if (valid) { Xunit.Assert.Empty(Hikan.Core.HikanValidator.Validate(p)); }
            else { AssertInvalid(p); }
        }

        // テスト3: 部材厚は 0.10 m ピッチ(基準)。0.45 や 0.42 は不可。
        [Xunit.Theory]
        [Xunit.InlineData(0.50, true)]
        [Xunit.InlineData(0.45, false)]
        [Xunit.InlineData(0.42, false)]
        public void SlabThickness_IsStandardisedInTenCentimetrePitch(double t, bool valid)
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.TopSlabThickness = (decimal)t;
            if (valid) { Xunit.Assert.Empty(Hikan.Core.HikanValidator.Validate(p)); }
            else { AssertInvalid(p); }
        }

        // テスト4: 適用範囲の境界。内空 3.0 m は可、3.5 m は不可。
        [Xunit.Theory]
        [Xunit.InlineData(3.0, true)]
        [Xunit.InlineData(3.5, false)]
        public void InnerDimension_ApplicableRangeBoundary(double v, bool valid)
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.InnerWidth = (decimal)v;
            if (valid) { Xunit.Assert.Empty(Hikan.Core.HikanValidator.Validate(p)); }
            else { AssertInvalid(p); }

            Hikan.Core.HikanParameters q = new Hikan.Core.HikanParameters();
            q.InnerHeight = (decimal)v;
            if (valid) { Xunit.Assert.Empty(Hikan.Core.HikanValidator.Validate(q)); }
            else { AssertInvalid(q); }
        }

        // テスト5: 土かぶり 10.0 m は可、10.5 m は不可(適用範囲)。
        [Xunit.Theory]
        [Xunit.InlineData(10.0, true)]
        [Xunit.InlineData(10.5, false)]
        public void SoilCover_ApplicableRangeBoundary(double v, bool valid)
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.SoilCover = (decimal)v;
            if (valid) { Xunit.Assert.Empty(Hikan.Core.HikanValidator.Validate(p)); }
            else { AssertInvalid(p); }
        }

        [Xunit.Fact]
        public void NonPositiveDimensions_AreRejected()
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.InnerWidth = 0m;
            AssertInvalid(p);

            Hikan.Core.HikanParameters q = new Hikan.Core.HikanParameters();
            q.BarrelLength = 0m;
            AssertInvalid(q);
        }

        [Xunit.Fact]
        public void NegativeSlope_And_ZeroBlockCount_AreRejected()
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.BottomSlope = -0.01m;
            AssertInvalid(p);

            Hikan.Core.HikanParameters q = new Hikan.Core.HikanParameters();
            q.BlockCount = 0;
            AssertInvalid(q);
        }

        [Xunit.Theory]
        [Xunit.InlineData(-1)]
        [Xunit.InlineData(257)]
        public void ColorIndex_OutOfRange_IsRejected(int c)
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.ColorIndex = c;
            AssertInvalid(p);
        }

        // テスト9: 複数のエラーが同時に収集されること(1 件ずつ止めない)。
        [Xunit.Fact]
        public void MultipleErrors_AreCollected()
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.InnerWidth = 0m;
            p.WallThickness = 0.25m;
            p.SoilCover = 12m;
            p.BlockCount = 0;
            System.Collections.Generic.List<string> errors = Hikan.Core.HikanValidator.Validate(p);
            Xunit.Assert.True(errors.Count >= 3, "複数エラーが収集されること。実際: " + errors.Count);
        }
    }
}
