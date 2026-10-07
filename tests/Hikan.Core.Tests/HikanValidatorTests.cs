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

        // テスト10: 胸壁。厚 0 は「設置しない」なので寸法を検査しない。
        [Xunit.Fact]
        public void BreastWall_ZeroThickness_SkipsDimensionChecks()
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.UpstreamBreastThickness = 0m;
            p.UpstreamBreastWidth = 0.1m;   // 外形幅未満だが設置しないので無視される
            p.UpstreamBreastHeight = 0.1m;
            Xunit.Assert.Empty(Hikan.Core.HikanValidator.Validate(p));
        }

        // テスト11: 胸壁の幅・高は函体外形以上であること。
        // これが崩れると内空プリズムが胸壁を貫通しきらず、体積の解析解が実形状と合わなくなる。
        [Xunit.Theory]
        [Xunit.InlineData(4.0, 4.0, true)]
        [Xunit.InlineData(2.8, 2.8, true)]   // 外形ちょうど
        [Xunit.InlineData(2.0, 4.0, false)]  // 幅不足
        [Xunit.InlineData(4.0, 2.0, false)]  // 高不足
        public void BreastWall_MustCoverBarrelOuterSection(double width, double height, bool valid)
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.UpstreamBreastThickness = 0.5m;
            p.UpstreamBreastWidth = (decimal)width;
            p.UpstreamBreastHeight = (decimal)height;
            if (valid) { Xunit.Assert.Empty(Hikan.Core.HikanValidator.Validate(p)); }
            else { AssertInvalid(p); }
        }

        // テスト12: 胸壁厚も最小部材厚 0.40 m・0.10 m ピッチの対象。
        [Xunit.Theory]
        [Xunit.InlineData(0.0, true)]
        [Xunit.InlineData(0.5, true)]
        [Xunit.InlineData(0.3, false)]
        [Xunit.InlineData(0.45, false)]
        [Xunit.InlineData(-0.1, false)]
        public void BreastWall_ThicknessFollowsStandard(double t, bool valid)
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.DownstreamBreastThickness = (decimal)t;
            if (valid) { Xunit.Assert.Empty(Hikan.Core.HikanValidator.Validate(p)); }
            else { AssertInvalid(p); }
        }

        // テスト13: しゃ水壁は厚 × 枚数 < 函体延長。等しいと隣接カラーが接触してブーリアンが退化する。
        [Xunit.Theory]
        [Xunit.InlineData(2, 0.5, true)]
        [Xunit.InlineData(39, 0.5, true)]    // 19.5 < 20.0
        [Xunit.InlineData(40, 0.5, false)]   // 20.0 = 20.0 で接触
        [Xunit.InlineData(50, 0.5, false)]
        public void Cutoff_TotalThicknessMustBeLessThanBarrelLength(int count, double thickness, bool valid)
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.CutoffCount = count;
            p.CutoffThickness = (decimal)thickness;
            if (valid) { Xunit.Assert.Empty(Hikan.Core.HikanValidator.Validate(p)); }
            else { AssertInvalid(p); }
        }

        // テスト14: しゃ水壁は枚数 0 で設置しない。張出し 0 は設置時のみエラー。
        [Xunit.Fact]
        public void Cutoff_ZeroCount_SkipsDimensionChecks()
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.CutoffCount = 0;
            p.CutoffThickness = 0.3m;
            p.CutoffProjection = 0m;
            Xunit.Assert.Empty(Hikan.Core.HikanValidator.Validate(p));

            Hikan.Core.HikanParameters q = new Hikan.Core.HikanParameters();
            q.CutoffCount = 2;
            q.CutoffProjection = 0m;
            AssertInvalid(q);

            Hikan.Core.HikanParameters r = new Hikan.Core.HikanParameters();
            r.CutoffCount = -1;
            AssertInvalid(r);
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
