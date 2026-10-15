namespace Hikan.Core.Tests
{
    /// <summary>
    /// 川裏側の土留壁(川裏側胸壁の左右のたて壁の間、頂版の上)の検証。配置・体積・型枠・埋戻しを手計算と突き合わせる。
    /// </summary>
    public class HikanRetainingWallTests
    {
        /// <summary>
        /// 函体(内空 2.0 × 2.0、側壁 0.4、頂版 0.4、底版 0.6 → 外形 2.8 × 3.0、延長 20)に川裏側胸壁
        /// (たて壁厚 0.5・軸位置 2.0 → たて壁 S 1.75〜2.25、張出し 1.5、天端 4.0)と川裏側の土留壁(高さ 1.0・厚さ 0.5)を付ける。
        /// </summary>
        private static Hikan.Core.HikanParameters WithWall()
        {
            Hikan.Core.HikanParameters p = HikanTestData.BarrelOnly();
            p.TopSlabThickness = 0.4m;
            p.BottomSlabThickness = 0.6m;
            p.UpstreamBreast.StemThickness = 0.5m;
            p.UpstreamBreast.Position = 2.0m;
            p.UpstreamBreast.Length = 1.5m;
            p.UpstreamBreast.CrownHeight = 4.0m;
            p.UpstreamRetainingWall.Height = 1.0m;
            p.UpstreamRetainingWall.Thickness = 0.5m;
            return p;
        }

        private static Hikan.Core.HikanParameters WithoutWall()
        {
            Hikan.Core.HikanParameters p = WithWall();
            p.UpstreamRetainingWall.Height = 0m;
            return p;
        }

        private static Hikan.Core.HikanPart Wall(Hikan.Core.HikanParameters p)
        {
            foreach (Hikan.Core.HikanPart part in Hikan.Core.HikanGeometry.GetParts(p))
            {
                if (part.Name == "川裏側土留壁") { return part; }
            }
            return null;
        }

        private static string FirstError(Hikan.Core.HikanParameters p)
        {
            System.Collections.Generic.List<string> errors = Hikan.Core.HikanValidator.Validate(p);
            Xunit.Assert.NotEmpty(errors);
            return errors[0];
        }

        // テスト1: 配置。左右のたて壁の間(U ±1.4 = 函体の外形幅)、下端は頂版上面 V 3.0、高さ 1.0、
        // 背面はたて壁の背面(函体中央側の面 S 2.25)で、厚さ 0.5 は川裏側へ(S 1.75〜2.25)。
        [Xunit.Fact]
        public void Wall_SpansBetweenStemsOnTopSlab()
        {
            Hikan.Core.HikanParameters p = WithWall();
            Xunit.Assert.Empty(Hikan.Core.HikanValidator.Validate(p));
            Hikan.Core.HikanPart w = Wall(p);
            Xunit.Assert.Equal(-1.4m, w.UMin);
            Xunit.Assert.Equal(1.4m, w.UMax);
            Xunit.Assert.Equal(3.0m, w.VMin);
            Xunit.Assert.Equal(4.0m, w.VMax);
            Xunit.Assert.Equal(1.75m, w.SMin);
            Xunit.Assert.Equal(2.25m, w.SMax);
            Xunit.Assert.Equal(p.UpstreamBreast.StemEnd, w.SMax);
            Xunit.Assert.True(w.IsSuperstructure);
            Xunit.Assert.Null(Wall(WithoutWall()));
        }

        // テスト2: 体積 2.8 × 0.5 × 1.0 = 1.4。函体・たて壁とは面で接するだけなので全体が 1.4 増え、
        // 独立のスラブ分解とも一致する。型枠は 前後 2 × 2.8 × 1.0 + 天端 2.8 × 0.5 = 7.0 増える。
        [Xunit.Fact]
        public void Volume_AndFormwork_MatchHandCalculation()
        {
            Hikan.Core.HikanParameters p = WithWall();
            Xunit.Assert.Equal(1.4m, Hikan.Core.HikanGeometry.UpstreamRetainingWallVolume(p));
            Xunit.Assert.Equal(Hikan.Core.HikanGeometry.ModelVolume(WithoutWall()) + 1.4m, Hikan.Core.HikanGeometry.ModelVolume(p));
            Xunit.Assert.Equal(Hikan.Core.HikanGeometry.ModelVolume(p), HikanPartsTests.SlabVolume(p));

            Hikan.Core.HikanEstimate e = Hikan.Core.HikanEstimator.Calculate(p);
            Hikan.Core.HikanEstimate n = Hikan.Core.HikanEstimator.Calculate(WithoutWall());
            Xunit.Assert.Equal(1.400m, e.UpstreamRetainingWallVolume);
            Xunit.Assert.Equal(n.FormworkBreast + 7.0m, e.FormworkBreast);
            Xunit.Assert.Equal(e.BarrelConcreteVolume + e.UpstreamBreastVolume + e.UpstreamRetainingWallVolume, e.ConcreteVolume);
            // 現地盤 V 5.0 より下にあるので埋戻しの控除にそのまま入る。床掘りの平面は広げない。
            Xunit.Assert.Equal(n.OccupiedVolume + 1.4m, e.OccupiedVolume);
            Xunit.Assert.Equal(n.ExcavationBottomWidth, e.ExcavationBottomWidth);
        }

        // テスト3: 検査。厚さは 0.40 m 以上・0.10 m ピッチ、川裏函体端からはみ出さない、しゃ水壁と重ならない。
        [Xunit.Fact]
        public void Validation_RejectsBadWall()
        {
            Hikan.Core.HikanParameters a = WithWall();
            a.UpstreamRetainingWall.Thickness = 0.45m;
            Xunit.Assert.StartsWith("川裏側土留壁の厚さ", FirstError(a));

            Hikan.Core.HikanParameters b = WithWall();
            b.UpstreamRetainingWall.Thickness = 2.5m;   // 背面 2.25 から 2.5 は S < 0 へはみ出す
            Xunit.Assert.StartsWith("川裏側土留壁の厚さ", FirstError(b));

            Hikan.Core.HikanParameters c = WithWall();
            c.UpstreamRetainingWall.Height = -0.1m;
            Xunit.Assert.StartsWith("川裏側土留壁の高さ", FirstError(c));

            Hikan.Core.HikanParameters d = WithWall();
            d.UpstreamBreast.Position = 4.8m;           // たて壁 4.55〜5.05、土留壁 4.55〜5.05
            d.UpstreamBreast.ToeLength = 0m;
            d.UpstreamBreast.HeelLength = 0m;
            d.CutoffCount = 2;                          // しゃ水壁 4.75〜5.25
            d.CutoffThickness = 0.5m;
            Xunit.Assert.Contains(Hikan.Core.HikanValidator.Validate(d),
                m => m.StartsWith("川裏側土留壁(", System.StringComparison.Ordinal) && m.Contains("しゃ水壁"));
        }

        // テスト4: 旧 XData(土留壁のキーなし)は設置しない(高さ 0)として読む。往復で値が戻る。
        [Xunit.Fact]
        public void Legacy_ReadsAsNone_AndRoundTrips()
        {
            System.Collections.Generic.Dictionary<string, string> d = WithWall().ToDictionary();
            Xunit.Assert.Equal(d, Hikan.Core.HikanParameters.FromDictionary(d).ToDictionary());
            d.Remove("upstream_retaining_wall_height");
            d.Remove("upstream_retaining_wall_thickness");
            Xunit.Assert.False(Hikan.Core.HikanParameters.FromDictionary(d).UpstreamRetainingWall.Exists);
        }

        // テスト5: 既定値では川裏側の土留壁を設置する(高さ 0.5・厚さ 0.4)。
        [Xunit.Fact]
        public void Defaults_InstallWall()
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            Xunit.Assert.True(p.UpstreamRetainingWall.Exists);
            Xunit.Assert.NotNull(Wall(p));
            Xunit.Assert.Empty(Hikan.Core.HikanValidator.ValidateInput(p));
        }
    }
}
