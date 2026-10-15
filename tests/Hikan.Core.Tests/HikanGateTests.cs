namespace Hikan.Core.Tests
{
    /// <summary>
    /// 門柱・ゲート操作台の検証。部材配置・体積を手計算と突き合わせ、全体の体積は独立のスラブ分解で確かめる。
    /// 床掘りの平面範囲に入らないこと、埋戻しの控除が現地盤より下の部分だけであることも確かめる。
    /// </summary>
    public class HikanGateTests
    {
        /// <summary>
        /// 函体だけ(内空 2.0 × 2.0、側壁 0.4、頂版 0.4、底版 0.6 → 外形高 3.0、延長 20)に門柱・操作台を付ける。
        /// d 10.0 / Tc 0.6 / Wc 1.0 / Hc 7.0 / td 0.4 / ax 0.5 / as 0.5。土かぶり 2.0 → 現地盤 V = 5.0。
        /// </summary>
        private static Hikan.Core.HikanParameters WithGate()
        {
            Hikan.Core.HikanParameters p = HikanTestData.BarrelOnly();
            p.TopSlabThickness = 0.4m;
            p.BottomSlabThickness = 0.6m;
            Hikan.Core.HikanGateTower g = p.Gate;
            g.Exists = true;
            g.Position = 10.0m;
            g.ColumnThickness = 0.6m;
            g.ColumnWidth = 1.0m;
            g.CrownHeight = 7.0m;
            g.DeckThickness = 0.4m;
            g.DeckSideOverhang = 0.5m;
            g.DeckAxialOverhang = 0.5m;
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

        [Xunit.Fact]
        public void WithGate_IsValid()
        {
            Xunit.Assert.Empty(Hikan.Core.HikanValidator.Validate(WithGate()));
        }

        // テスト1: 配置。門柱は頂版上面(V 3.0)から天端 7.0 まで、内面 = 内空側面(U 1.0)、S = 10 ± 0.5。
        // 操作台は V 7.0〜7.4、U = ±(1.0 + 0.6 + 0.5)、S = 9.5 − 0.5 〜 10.5 + 0.5。
        [Xunit.Fact]
        public void Parts_StandOnSideWalls()
        {
            System.Collections.Generic.Dictionary<string, Hikan.Core.HikanPart> b =
                new System.Collections.Generic.Dictionary<string, Hikan.Core.HikanPart>();
            foreach (Hikan.Core.HikanPart part in Hikan.Core.HikanGeometry.GetParts(WithGate()))
            {
                b[part.Name] = part;
            }
            Hikan.Core.HikanPart right = b["門柱右"];
            Xunit.Assert.Equal(1.0m, right.UMin);
            Xunit.Assert.Equal(1.6m, right.UMax);
            Xunit.Assert.Equal(3.0m, right.VMin);
            Xunit.Assert.Equal(7.0m, right.VMax);
            Xunit.Assert.Equal(9.5m, right.SMin);
            Xunit.Assert.Equal(10.5m, right.SMax);
            Hikan.Core.HikanPart left = b["門柱左"];
            Xunit.Assert.Equal(-1.6m, left.UMin);
            Xunit.Assert.Equal(-1.0m, left.UMax);
            Hikan.Core.HikanPart deck = b["操作台"];
            Xunit.Assert.Equal(-2.1m, deck.UMin);
            Xunit.Assert.Equal(2.1m, deck.UMax);
            Xunit.Assert.Equal(7.0m, deck.VMin);
            Xunit.Assert.Equal(7.4m, deck.VMax);
            Xunit.Assert.Equal(9.0m, deck.SMin);
            Xunit.Assert.Equal(11.0m, deck.SMax);
            Xunit.Assert.True(right.IsSuperstructure && left.IsSuperstructure && deck.IsSuperstructure);
        }

        // テスト2: 体積。門柱 2 × 0.6 × 1.0 × (7.0 − 3.0) = 4.8、操作台 0.4 × (2.0 + 1.2 + 1.0) × (1.0 + 1.0) = 3.36。
        // 他の部材と重ならないので、全体の体積は単純に 8.16 増える。独立のスラブ分解とも一致する。
        [Xunit.Fact]
        public void Volume_MatchesHandCalculationAndSlabIntegral()
        {
            Hikan.Core.HikanParameters p = WithGate();
            Xunit.Assert.Equal(4.8m, Hikan.Core.HikanGeometry.GateColumnVolume(p));
            Xunit.Assert.Equal(3.36m, Hikan.Core.HikanGeometry.GateDeckVolume(p));
            Xunit.Assert.Equal(Hikan.Core.HikanGeometry.ModelVolume(WithoutGate()) + 8.16m, Hikan.Core.HikanGeometry.ModelVolume(p));
            Xunit.Assert.Equal(Hikan.Core.HikanGeometry.ModelVolume(p), HikanPartsTests.SlabVolume(p));

            Hikan.Core.HikanEstimate e = Hikan.Core.HikanEstimator.Calculate(p);
            Xunit.Assert.Equal(4.800m, e.GateColumnVolume);
            Xunit.Assert.Equal(3.360m, e.GateDeckVolume);
            Xunit.Assert.Equal(e.BarrelConcreteVolume + e.GateColumnVolume + e.GateDeckVolume, e.ConcreteVolume);
        }

        // テスト3: 型枠。門柱 2 × 2 × (0.6 + 1.0) × 4.0 = 25.6、
        // 操作台 側面 2 × (4.2 + 2.0) × 0.4 = 4.96 + 下面 4.2 × 2.0 − 2 × 0.6 × 1.0 = 7.2 → 37.76
        [Xunit.Fact]
        public void Formwork_MatchesHandCalculation()
        {
            Xunit.Assert.Equal(37.760m, Hikan.Core.HikanEstimator.Calculate(WithGate()).FormworkGate);
            Xunit.Assert.Equal(0.000m, Hikan.Core.HikanEstimator.Calculate(WithoutGate()).FormworkGate);
        }

        // テスト4: 床掘りの平面は操作台の張出し(U ±2.1)で広がらない。
        // 埋戻しの控除は現地盤 V 5.0 より下の門柱だけ: 2 × 0.6 × 1.0 × (5.0 − 3.0) = 2.4(操作台は地上)。
        [Xunit.Fact]
        public void Excavation_IgnoresSuperstructure_AndBackfillDeductsOnlyBelowGround()
        {
            Hikan.Core.HikanEstimate with = Hikan.Core.HikanEstimator.Calculate(WithGate());
            Hikan.Core.HikanEstimate without = Hikan.Core.HikanEstimator.Calculate(WithoutGate());
            Xunit.Assert.Equal(without.ExcavationBottomWidth, with.ExcavationBottomWidth);
            Xunit.Assert.Equal(without.ExcavationVolume, with.ExcavationVolume);
            Xunit.Assert.Equal(without.OccupiedVolume + 2.4m, with.OccupiedVolume);
            Xunit.Assert.Equal(without.BackfillVolume - 2.4m, with.BackfillVolume);
            Xunit.Assert.Equal(2.4m, Hikan.Core.HikanGeometry.GateVolumeBelow(WithGate(), 5.0m));
            Xunit.Assert.Equal(4.8m + 3.36m, Hikan.Core.HikanGeometry.GateVolumeBelow(WithGate(), 100m));
        }

        // テスト5: エクステント・重心。上端は操作台の上面 7.4、横幅は操作台の ±2.1。中央に置けば重心の Y は動かず、Z は上がる。
        [Xunit.Fact]
        public void ExtentsAndCentroid_IncludeGate()
        {
            Hikan.Core.HikanParameters p = WithGate();
            (decimal MinX, decimal MinY, decimal MinZ, decimal MaxX, decimal MaxY, decimal MaxZ) x =
                Hikan.Core.HikanGeometry.ExpectedExtents(p);
            Xunit.Assert.Equal(7.4m, x.MaxZ);
            Xunit.Assert.Equal(2.1m, x.MaxX);
            Xunit.Assert.Equal(-2.1m, x.MinX);
            Xunit.Assert.Equal(20m, Hikan.Core.HikanGeometry.GlobalSMax(p));

            (decimal X, decimal Y, decimal Z) g = Hikan.Core.HikanGeometry.ExpectedCentroid(p);
            (decimal X, decimal Y, decimal Z) h = Hikan.Core.HikanGeometry.ExpectedCentroid(WithoutGate());
            Xunit.Assert.Equal(h.Y, g.Y);
            Xunit.Assert.True(g.Z > h.Z, "門柱・操作台の分だけ重心が上がること");
        }

        // テスト6: 旧 XData(門柱のキーなし)は設置しないとして読む。往復で値が戻る。
        [Xunit.Fact]
        public void Legacy_ReadsAsNone_AndRoundTrips()
        {
            System.Collections.Generic.Dictionary<string, string> d = WithGate().ToDictionary();
            Hikan.Core.HikanParameters q = Hikan.Core.HikanParameters.FromDictionary(d);
            Xunit.Assert.Equal(d, q.ToDictionary());
            Xunit.Assert.True(q.Gate.Exists);

            foreach (string key in new System.Collections.Generic.List<string>(d.Keys))
            {
                if (key.StartsWith("gate_", System.StringComparison.Ordinal)) { d.Remove(key); }
            }
            Xunit.Assert.False(Hikan.Core.HikanParameters.FromDictionary(d).Gate.Exists);
        }

        // テスト7: 寸法の検査。メッセージは「門柱・操作台の<項目>」で始まる(入力画面が該当欄を探すのに使う)。
        [Xunit.Fact]
        public void Validation_RejectsBadDimensions()
        {
            Hikan.Core.HikanParameters a = WithGate();
            a.Gate.ColumnThickness = 0.45m;
            Xunit.Assert.StartsWith("門柱・操作台の門柱厚", FirstError(a));

            Hikan.Core.HikanParameters b = WithGate();
            b.Gate.CrownHeight = 3.0m;   // 頂版上面と同じ
            Xunit.Assert.StartsWith("門柱・操作台の天端高", FirstError(b));

            Hikan.Core.HikanParameters c = WithGate();
            c.Gate.Position = 0.3m;      // 門柱 −0.2〜0.8 が函体からはみ出す
            Xunit.Assert.StartsWith("門柱・操作台の軸位置", FirstError(c));

            Hikan.Core.HikanParameters d = WithGate();
            d.Gate.DeckSideOverhang = -0.1m;
            Xunit.Assert.StartsWith("門柱・操作台の左右の張出し", FirstError(d));

            Hikan.Core.HikanParameters e = WithGate();
            e.Gate.DeckThickness = 0.3m;
            Xunit.Assert.StartsWith("門柱・操作台の操作台厚", FirstError(e));
        }

        // テスト8: しゃ水壁・胸壁のたて壁と S 方向で重なる配置は弾く(接するのは可)。
        [Xunit.Fact]
        public void Validation_RejectsOverlapWithCutoffAndBreast()
        {
            Hikan.Core.HikanParameters a = WithGate();
            a.CutoffCount = 2;             // しゃ水壁 5 ± 0.25、15 ± 0.25
            a.Gate.Position = 6.0m;        // 操作台 5.0〜7.0
            Xunit.Assert.StartsWith("門柱・操作台の軸位置", FirstError(a));
            a.Gate.Position = 6.25m;       // 操作台 5.25〜7.25(しゃ水壁に接するだけ)
            Xunit.Assert.Empty(Hikan.Core.HikanValidator.Validate(a));

            Hikan.Core.HikanParameters b = WithGate();
            b.DownstreamBreast.StemThickness = 0.4m;
            b.DownstreamBreast.Position = 19.0m;   // たて壁 18.8〜19.2
            b.Gate.Position = 18.0m;               // 操作台 17.0〜19.0
            Xunit.Assert.StartsWith("門柱・操作台の軸位置", FirstError(b));
        }

        // テスト9: 函体端より外へ張り出した操作台は、その側の翼壁(堤防保護部を含む)より高くなければならない。
        [Xunit.Theory]
        [Xunit.InlineData(3.0, true)]    // 翼壁の天端 3.0 < 門柱天端 7.0
        [Xunit.InlineData(7.5, false)]   // 翼壁の天端が操作台より高い
        public void DeckBeyondBarrelEnd_MustBeAboveWing(double crown, bool valid)
        {
            Hikan.Core.HikanParameters p = WithGate();
            p.Gate.Position = 19.5m;       // 門柱 19.0〜20.0、操作台 18.5〜20.5
            p.DownstreamWing.StemThickness = 0.4m;
            p.DownstreamWing.EndWidth = 2.0m;
            p.DownstreamWing.CrownStart = (decimal)crown;
            p.DownstreamWing.CrownEnd = 2.0m;
            if (valid) { Xunit.Assert.Empty(Hikan.Core.HikanValidator.Validate(p)); }
            else { Xunit.Assert.StartsWith("門柱・操作台の天端高", FirstError(p)); }
        }
    }
}
