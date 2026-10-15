namespace Hikan.Core.Tests
{
    /// <summary>
    /// 門柱・ゲート操作台の検証。L 字の門柱(横方向壁は川裏側)と、管理橋の桁受け切欠きのある操作台を、
    /// 部材配置・体積・型枠の手計算と突き合わせ、全体の体積は独立のスラブ分解で確かめる。
    /// 床掘りの平面範囲に入らないこと、埋戻しの控除が現地盤より下の部分だけであることも確かめる。
    /// </summary>
    public class HikanGateTests
    {
        /// <summary>
        /// 函体だけ(内空 2.0 × 2.0、側壁 0.4、頂版 0.4、底版 0.6 → 外形高 3.0、延長 20)に門柱・操作台を付ける。
        /// 門柱: d 10.0 / X4 0.9 / X5 0.5 / X7 0.5 / X8 0.4 / Y2 6.0(X7 ≠ X8、X4 ≠ X5 にして向きの取り違えを検出する)。
        /// 操作台: X3 0.7 / X6 1.1 / Y1 0.6 → X1 = 1.4 + 1.4 = 2.8、X2 = 2.0 + 2 × 0.9 + 2 × 1.1 = 6.0。
        /// 管理橋あり: W1 0.4 / W2 1.4 / H1 0.3。ハンチ H2 0.2。土かぶり 2.0 → 現地盤 V = 5.0。
        /// </summary>
        private static Hikan.Core.HikanParameters WithGate()
        {
            Hikan.Core.HikanParameters p = HikanTestData.BarrelOnly();
            p.TopSlabThickness = 0.4m;
            p.BottomSlabThickness = 0.6m;
            Hikan.Core.HikanGateTower g = p.Gate;
            g.Exists = true;
            g.Position = 10.0m;
            g.LongitudinalWallWidth = 0.9m;
            g.TransverseWallThickness = 0.5m;
            g.LongitudinalWallThickness = 0.5m;
            g.TransverseWallWidth = 0.4m;
            g.ColumnHeight = 6.0m;
            g.DeckAxialOverhang = 0.7m;
            g.DeckSideOverhang = 1.1m;
            g.DeckLength = 2.8m;
            g.DeckWidth = 6.0m;
            g.DeckThickness = 0.6m;
            g.Bridge = true;
            g.SeatLength = 0.4m;
            g.SeatWidth = 1.4m;
            g.SeatDepth = 0.3m;
            g.HaunchHeight = 0.2m;
            return p;
        }

        private static Hikan.Core.HikanParameters WithoutGate()
        {
            Hikan.Core.HikanParameters p = WithGate();
            p.Gate.Exists = false;
            return p;
        }

        /// <summary>函体だけの構成なので、胸壁・翼壁の設置を求める ValidateInput ではなく Validate で検査する。</summary>
        private static string FirstError(Hikan.Core.HikanParameters p)
        {
            System.Collections.Generic.List<string> errors = Hikan.Core.HikanValidator.Validate(p);
            Xunit.Assert.NotEmpty(errors);
            return errors[0];
        }

        private static System.Collections.Generic.Dictionary<string, Hikan.Core.HikanPart> ByName(Hikan.Core.HikanParameters p)
        {
            System.Collections.Generic.Dictionary<string, Hikan.Core.HikanPart> b =
                new System.Collections.Generic.Dictionary<string, Hikan.Core.HikanPart>();
            foreach (Hikan.Core.HikanPart part in Hikan.Core.HikanGeometry.GetParts(p))
            {
                b[part.Name] = part;
            }
            return b;
        }

        private static void AssertBox(Hikan.Core.HikanPart b,
            decimal uMin, decimal uMax, decimal vMin, decimal vMax, decimal sMin, decimal sMax)
        {
            Xunit.Assert.Equal(uMin, b.UMin);
            Xunit.Assert.Equal(uMax, b.UMax);
            Xunit.Assert.Equal(vMin, b.VMin);
            Xunit.Assert.Equal(vMax, b.VMax);
            Xunit.Assert.Equal(sMin, b.SMin);
            Xunit.Assert.Equal(sMax, b.SMax);
            Xunit.Assert.True(b.IsSuperstructure);
        }

        [Xunit.Fact]
        public void WithGate_IsValid()
        {
            Xunit.Assert.Empty(Hikan.Core.HikanValidator.Validate(WithGate()));
        }

        // テスト1: 配置。門柱は頂版上面 V 3.0 から 9.0 まで、S = 10 ± 0.7。横方向壁は川裏側(S 9.3〜9.8)で
        // 内空側面 U 1.0 から外へ X8 + X7 = 0.9、縦方向壁は S 9.8〜10.7 で外側の X7(U 1.4〜1.9)だけ(内側は切欠き)。
        // 操作台は V 9.0〜9.6、U ±3.0、S 8.6〜11.4。受け台(切欠き)は川裏側の端 S 8.6〜9.0、U ±0.7、V 9.3〜9.6。
        [Xunit.Fact]
        public void Parts_AreLShapedColumnsAndNotchedDeck()
        {
            System.Collections.Generic.Dictionary<string, Hikan.Core.HikanPart> b = ByName(WithGate());
            AssertBox(b["門柱右横方向壁"], 1.0m, 1.9m, 3.0m, 9.0m, 9.3m, 9.8m);
            AssertBox(b["門柱右縦方向壁"], 1.4m, 1.9m, 3.0m, 9.0m, 9.8m, 10.7m);
            AssertBox(b["門柱左横方向壁"], -1.9m, -1.0m, 3.0m, 9.0m, 9.3m, 9.8m);
            AssertBox(b["門柱左縦方向壁"], -1.9m, -1.4m, 3.0m, 9.0m, 9.8m, 10.7m);
            AssertBox(b["操作台"], -3.0m, 3.0m, 9.0m, 9.6m, 9.0m, 11.4m);
            AssertBox(b["操作台受け台下"], -3.0m, 3.0m, 9.0m, 9.3m, 8.6m, 9.0m);
            AssertBox(b["操作台受け台左"], -3.0m, -0.7m, 9.3m, 9.6m, 8.6m, 9.0m);
            AssertBox(b["操作台受け台右"], 0.7m, 3.0m, 9.3m, 9.6m, 8.6m, 9.0m);
        }

        // テスト2: 体積。門柱 L 字断面 0.5 × 0.9 + 0.9 × 0.5 = 0.9 × 高さ 6.0 × 2 本 = 10.8 + ハンチ 2 × 0.2² / 2 × 0.5 = 0.02、
        // 操作台 0.6 × 2.8 × 6.0 = 10.08 − 受け台 0.4 × 1.4 × 0.3 = 0.168 → 9.912。
        // 他の部材と重ならないので全体の体積は 20.732 増える。直方体部分は独立のスラブ分解と一致する。
        [Xunit.Fact]
        public void Volume_MatchesHandCalculationAndSlabIntegral()
        {
            Hikan.Core.HikanParameters p = WithGate();
            Xunit.Assert.Equal(0.02m, Hikan.Core.HikanGeometry.GateHaunchVolume(p));
            Xunit.Assert.Equal(10.82m, Hikan.Core.HikanGeometry.GateColumnVolume(p));
            Xunit.Assert.Equal(9.912m, Hikan.Core.HikanGeometry.GateDeckVolume(p));
            Xunit.Assert.Equal(Hikan.Core.HikanGeometry.ModelVolume(WithoutGate()) + 20.732m, Hikan.Core.HikanGeometry.ModelVolume(p));
            Xunit.Assert.Equal(Hikan.Core.HikanGeometry.ModelVolume(p),
                HikanPartsTests.SlabVolume(p) + Hikan.Core.HikanGeometry.GateHaunchVolume(p));

            Hikan.Core.HikanEstimate e = Hikan.Core.HikanEstimator.Calculate(p);
            Xunit.Assert.Equal(10.820m, e.GateColumnVolume);
            Xunit.Assert.Equal(9.912m, e.GateDeckVolume);
            Xunit.Assert.Equal(e.BarrelConcreteVolume + e.GateColumnVolume + e.GateDeckVolume, e.ConcreteVolume);
        }

        // テスト3: 管理橋なしなら受け台(切欠き)を作らず、操作台は 1 枚の版。
        [Xunit.Fact]
        public void NoBridge_HasNoSeat()
        {
            Hikan.Core.HikanParameters p = WithGate();
            p.Gate.Bridge = false;
            System.Collections.Generic.Dictionary<string, Hikan.Core.HikanPart> b = ByName(p);
            Xunit.Assert.False(b.ContainsKey("操作台受け台下"));
            AssertBox(b["操作台"], -3.0m, 3.0m, 9.0m, 9.6m, 8.6m, 11.4m);
            Xunit.Assert.Equal(10.08m, Hikan.Core.HikanGeometry.GateDeckVolume(p));
            Xunit.Assert.Equal(Hikan.Core.HikanGeometry.ModelVolume(p),
                HikanPartsTests.SlabVolume(p) + Hikan.Core.HikanGeometry.GateHaunchVolume(p));
        }

        // テスト4: 横方向壁幅 X8 = 0 なら切欠きのない矩形の柱(X4 + X5)× X7 になる。
        [Xunit.Fact]
        public void ZeroNotch_GivesRectangularColumn()
        {
            Hikan.Core.HikanParameters p = WithGate();
            p.Gate.TransverseWallWidth = 0m;
            p.Gate.DeckWidth = 2.0m + 2m * 0.5m + 2m * 1.1m;   // X2 を合わせ直す
            Xunit.Assert.Empty(Hikan.Core.HikanValidator.Validate(p));
            Xunit.Assert.Equal(2m * 6.0m * 1.4m * 0.5m + 0.02m, Hikan.Core.HikanGeometry.GateColumnVolume(p));
            Xunit.Assert.Equal(Hikan.Core.HikanGeometry.ModelVolume(p),
                HikanPartsTests.SlabVolume(p) + Hikan.Core.HikanGeometry.GateHaunchVolume(p));
        }

        // テスト5: 横方向壁は川裏側(S の小さい側)なので、門柱の重心は軸位置 d = 10 より川裏側に寄る。
        [Xunit.Fact]
        public void TransverseWall_IsOnLandSide()
        {
            decimal volume = 0m, moment = 0m;
            foreach (Hikan.Core.HikanPart part in Hikan.Core.HikanGeometry.GetParts(WithGate()))
            {
                if (!part.Name.StartsWith("門柱", System.StringComparison.Ordinal)) { continue; }
                volume += part.Volume;
                moment += part.Volume * (part.SMin + part.SMax) / 2m;
            }
            // 横方向壁 0.45 × S 9.55、縦方向壁 0.45 × S 10.25 → 9.9
            Xunit.Assert.Equal(9.9m, moment / volume);
        }

        // テスト6: 型枠。門柱 2 × 2 × (1.4 + 0.9) × 6.0 = 55.2、
        // 操作台 側面 2 × (2.8 + 6.0) × 0.6 = 10.56 + 下面 2.8 × 6.0 − 2 × 0.9 = 15.0 + 受け台 (2 × 0.4 + 1.4) × 0.3 = 0.66 → 81.42、
        // ハンチ 2 × (√2 × 0.2 − 2 × 0.2) × 0.5 = −0.117157 → 81.302843
        [Xunit.Fact]
        public void Formwork_MatchesHandCalculation()
        {
            Xunit.Assert.Equal(81.303m, Hikan.Core.HikanEstimator.Calculate(WithGate()).FormworkGate);
            Xunit.Assert.Equal(0.000m, Hikan.Core.HikanEstimator.Calculate(WithoutGate()).FormworkGate);
        }

        // テスト7: 床掘りの平面は操作台の張出し(U ±3.0)で広がらない。
        // 埋戻しの控除は現地盤 V 5.0 より下の門柱だけ: 0.9 × (5.0 − 3.0) × 2 本 = 3.6(操作台は地上)。
        [Xunit.Fact]
        public void Excavation_IgnoresSuperstructure_AndBackfillDeductsOnlyBelowGround()
        {
            Hikan.Core.HikanEstimate with = Hikan.Core.HikanEstimator.Calculate(WithGate());
            Hikan.Core.HikanEstimate without = Hikan.Core.HikanEstimator.Calculate(WithoutGate());
            Xunit.Assert.Equal(without.ExcavationBottomWidth, with.ExcavationBottomWidth);
            Xunit.Assert.Equal(without.ExcavationVolume, with.ExcavationVolume);
            Xunit.Assert.Equal(without.OccupiedVolume + 3.6m, with.OccupiedVolume);
            Xunit.Assert.Equal(without.BackfillVolume - 3.6m, with.BackfillVolume);
            Xunit.Assert.Equal(3.6m, Hikan.Core.HikanGeometry.GateVolumeBelow(WithGate(), 5.0m));
            Xunit.Assert.Equal(20.732m, Hikan.Core.HikanGeometry.GateVolumeBelow(WithGate(), 100m));
            // 現地盤がハンチの途中(V 8.9、ハンチは 8.8〜9.0)なら、地盤より下のハンチは 2 × 0.1² / 2 × 0.5 = 0.005
            Xunit.Assert.Equal(10.8m * (8.9m - 3.0m) / 6.0m + 0.005m, Hikan.Core.HikanGeometry.GateVolumeBelow(WithGate(), 8.9m));
        }

        // テスト8: エクステント。上端は操作台の上面 9.6、横幅は操作台の ±3.0。
        [Xunit.Fact]
        public void Extents_IncludeGate()
        {
            Hikan.Core.HikanParameters p = WithGate();
            (decimal MinX, decimal MinY, decimal MinZ, decimal MaxX, decimal MaxY, decimal MaxZ) x =
                Hikan.Core.HikanGeometry.ExpectedExtents(p);
            Xunit.Assert.Equal(9.6m, x.MaxZ);
            Xunit.Assert.Equal(3.0m, x.MaxX);
            Xunit.Assert.Equal(-3.0m, x.MinX);
            Xunit.Assert.Equal(20m, Hikan.Core.HikanGeometry.GlobalSMax(p));
        }

        // テスト8b: ハンチは門柱の内面(U = ±1.0)と操作台下面(V = 9.0)の隅の三角(1:1、H2 = 0.2)で、
        // 横方向壁の区間 S 9.3〜9.8 に付く(切欠きの区間には付かない)。左右対称なので重心の X は 0 のまま。
        [Xunit.Fact]
        public void Haunch_IsTriangleAtInnerFaceUnderDeck()
        {
            Hikan.Core.HikanPrismPart[] h = Hikan.Core.HikanGeometry.GetHaunchParts(WithGate());
            Xunit.Assert.Equal(2, h.Length);
            Hikan.Core.HikanPrismPart right = h[0];
            Xunit.Assert.Equal("門柱右ハンチ", right.Name);
            Xunit.Assert.Equal(new (decimal U, decimal V)[] { (0.8m, 9.0m), (1.0m, 8.8m), (1.0m, 9.0m) }, right.Section);
            Xunit.Assert.Equal(9.3m, right.SMin);
            Xunit.Assert.Equal(9.8m, right.SMax);
            Xunit.Assert.Equal(0.02m, right.Area);
            Hikan.Core.HikanPrismPart left = h[1];
            Xunit.Assert.Equal(0.02m, left.Area);
            Xunit.Assert.Equal(-right.Centroid.U, left.Centroid.U);
            Xunit.Assert.Equal(right.Centroid.V, left.Centroid.V);
            Xunit.Assert.Equal(0m, Hikan.Core.HikanGeometry.ExpectedCentroid(WithGate()).X);

            Hikan.Core.HikanParameters none = WithGate();
            none.Gate.HaunchHeight = 0m;
            Xunit.Assert.Empty(Hikan.Core.HikanGeometry.GetHaunchParts(none));
            Xunit.Assert.Equal(10.8m, Hikan.Core.HikanGeometry.GateColumnVolume(none));
        }

        // テスト8c: ハンチは左右で重ならないこと(H2 < 内空幅 / 2)。
        [Xunit.Theory]
        [Xunit.InlineData(0.9, true)]
        [Xunit.InlineData(1.0, false)]
        public void Haunch_MustBeSmallerThanHalfInnerWidth(double h, bool valid)
        {
            Hikan.Core.HikanParameters p = WithGate();
            p.Gate.HaunchHeight = (decimal)h;
            if (valid) { Xunit.Assert.Empty(Hikan.Core.HikanValidator.Validate(p)); }
            else { Xunit.Assert.StartsWith("門柱・操作台のハンチ高 H2", FirstError(p)); }
        }

        // テスト9: 旧 XData(門柱のキーなし)は設置しないとして読む。往復で値が戻る。
        [Xunit.Fact]
        public void Legacy_ReadsAsNone_AndRoundTrips()
        {
            System.Collections.Generic.Dictionary<string, string> d = WithGate().ToDictionary();
            Hikan.Core.HikanParameters q = Hikan.Core.HikanParameters.FromDictionary(d);
            Xunit.Assert.Equal(d, q.ToDictionary());
            Xunit.Assert.True(q.Gate.Exists);
            Xunit.Assert.True(q.Gate.Bridge);

            foreach (string key in new System.Collections.Generic.List<string>(d.Keys))
            {
                if (key.StartsWith("gate_", System.StringComparison.Ordinal)) { d.Remove(key); }
            }
            Xunit.Assert.False(Hikan.Core.HikanParameters.FromDictionary(d).Gate.Exists);
        }

        // テスト10: 整合性チェック(許容 1 mm)。X1 = X4 + X5 + 2·X3、X2 = B + 2·(X7 + X8) + 2·X6。
        [Xunit.Fact]
        public void Consistency_DeckSizeMustMatchColumnsAndOverhangs()
        {
            Hikan.Core.HikanParameters a = WithGate();
            a.Gate.DeckLength = 2.7m;
            Xunit.Assert.StartsWith("門柱・操作台の操作台縦方向幅 X1", FirstError(a));

            Hikan.Core.HikanParameters b = WithGate();
            b.InnerWidth = 2.5m;   // 内空幅を変えたら X2 も合わせる必要がある
            Xunit.Assert.StartsWith("門柱・操作台の操作台横方向幅 X2", FirstError(b));

            Hikan.Core.HikanParameters c = WithGate();
            c.Gate.DeckWidth = 6.0009m;   // 許容 1 mm 以内
            Xunit.Assert.Empty(Hikan.Core.HikanValidator.Validate(c));
        }

        // テスト11: 寸法の検査。メッセージは「門柱・操作台の<項目> <記号>」で始まる(入力画面が該当欄を探すのに使う)。
        [Xunit.Theory]
        [Xunit.InlineData("X5", "門柱・操作台の横方向壁厚 X5")]
        [Xunit.InlineData("X7", "門柱・操作台の縦方向壁厚 X7")]
        [Xunit.InlineData("Y1", "門柱・操作台の操作台高さ Y1")]
        [Xunit.InlineData("Y2", "門柱・操作台の柱高さ Y2")]
        [Xunit.InlineData("W2", "門柱・操作台の操作台受け台横方向幅 W2")]
        [Xunit.InlineData("H1", "門柱・操作台の操作台受け台高さ H1")]
        [Xunit.InlineData("d", "門柱・操作台の軸位置")]
        public void Validation_RejectsBadDimensions(string item, string expected)
        {
            Hikan.Core.HikanParameters p = WithGate();
            Hikan.Core.HikanGateTower g = p.Gate;
            switch (item)
            {
                case "X5": g.TransverseWallThickness = 0.45m; g.DeckLength = 2.75m; break;
                case "X7": g.LongitudinalWallThickness = 0.3m; g.DeckWidth = 5.8m; break;
                case "Y1": g.DeckThickness = 0.35m; break;
                case "Y2": g.ColumnHeight = 0m; break;
                case "W2": g.SeatWidth = 6.0m; break;
                case "H1": g.SeatDepth = 0.6m; break;
                case "d": g.Position = 0.5m; break;   // 門柱 −0.2〜1.2 が函体からはみ出す
            }
            Xunit.Assert.StartsWith(expected, FirstError(p));
        }

        // テスト12: しゃ水壁・胸壁のたて壁と S 方向で重なる配置は弾く(接するのは可)。
        [Xunit.Fact]
        public void Validation_RejectsOverlapWithCutoffAndBreast()
        {
            Hikan.Core.HikanParameters a = WithGate();
            a.CutoffCount = 2;             // しゃ水壁 5 ± 0.25、15 ± 0.25
            a.Gate.Position = 6.0m;        // 操作台 4.6〜7.4
            Xunit.Assert.StartsWith("門柱・操作台の軸位置", FirstError(a));
            a.Gate.Position = 6.65m;       // 操作台 5.25〜8.05(しゃ水壁に接するだけ)
            Xunit.Assert.Empty(Hikan.Core.HikanValidator.Validate(a));

            Hikan.Core.HikanParameters b = WithGate();
            b.DownstreamBreast.StemThickness = 0.4m;
            b.DownstreamBreast.Position = 19.0m;   // たて壁 18.8〜19.2
            b.Gate.Position = 18.0m;               // 操作台 16.6〜19.4
            Xunit.Assert.StartsWith("門柱・操作台の軸位置", FirstError(b));
        }

        // テスト13: 函体端より外へ張り出した操作台は、その側の翼壁(堤防保護部を含む)より高くなければならない。
        [Xunit.Theory]
        [Xunit.InlineData(3.0, true)]    // 翼壁の天端 3.0 < 操作台下面 9.0
        [Xunit.InlineData(9.5, false)]   // 翼壁の天端が操作台下面より高い
        public void DeckBeyondBarrelEnd_MustBeAboveWing(double crown, bool valid)
        {
            Hikan.Core.HikanParameters p = WithGate();
            p.Gate.Position = 19.3m;       // 門柱 18.6〜20.0、操作台 17.9〜20.7
            p.DownstreamWing.StemThickness = 0.4m;
            p.DownstreamWing.EndWidth = 2.0m;
            p.DownstreamWing.CrownStart = (decimal)crown;
            p.DownstreamWing.CrownEnd = 2.0m;
            if (valid) { Xunit.Assert.Empty(Hikan.Core.HikanValidator.Validate(p)); }
            else { Xunit.Assert.StartsWith("門柱・操作台の柱高さ Y2", FirstError(p)); }
        }
    }
}
