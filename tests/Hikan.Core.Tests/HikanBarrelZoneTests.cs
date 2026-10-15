namespace Hikan.Core.Tests
{
    /// <summary>
    /// 函体の区間(川裏側端部 / 標準部 / 川表側端部)・内空の隅のハンチ・スパン長の検証。
    /// 区間ごとの部材厚・ハンチを手計算と突き合わせ、重なりの扱いは独立のスラブ分解(HikanPartsTests.SlabVolume)で確かめる。
    /// </summary>
    public class HikanBarrelZoneTests
    {
        /// <summary>
        /// 内空 2.0 × 2.0、標準部 側壁・頂版・底版 0.4(敷高 0.4)、延長 20(5 × 4 スパン)。
        /// 川裏側端部 3.0: 側壁 0.5 / 頂版 0.5 / 底版 0.6、ハンチ LT・RT 0.3 × 0.3。
        /// 標準部 3.0〜16.0: ハンチ LT・RT 0.2 × 0.2。
        /// 川表側端部 4.0: 側壁 0.6 / 頂版・底版 0.4、ハンチ LB・RB H 0.2 × W 0.4。
        /// 区間ごとに部材厚・ハンチを変えて、区間の取り違えを数量で検出できるようにする。
        /// </summary>
        internal static Hikan.Core.HikanParameters Zoned()
        {
            Hikan.Core.HikanParameters p = HikanTestData.BarrelOnly();
            p.UpstreamEndLength = 3.0m;
            p.UpstreamEnd.WallThickness = 0.5m;
            p.UpstreamEnd.TopSlabThickness = 0.5m;
            p.UpstreamEnd.BottomSlabThickness = 0.6m;
            Haunch(p.UpstreamEnd.LeftTop, 0.3m, 0.3m);
            Haunch(p.UpstreamEnd.RightTop, 0.3m, 0.3m);
            Haunch(p.Standard.LeftTop, 0.2m, 0.2m);
            Haunch(p.Standard.RightTop, 0.2m, 0.2m);
            p.DownstreamEndLength = 4.0m;
            p.DownstreamEnd.WallThickness = 0.6m;
            Haunch(p.DownstreamEnd.LeftBottom, 0.2m, 0.4m);
            Haunch(p.DownstreamEnd.RightBottom, 0.2m, 0.4m);
            return p;
        }

        private static void Haunch(Hikan.Core.HikanHaunch h, decimal height, decimal width)
        {
            h.Height = height;
            h.Width = width;
        }

        private static string FirstError(Hikan.Core.HikanParameters p)
        {
            System.Collections.Generic.List<string> errors = Hikan.Core.HikanValidator.Validate(p);
            Xunit.Assert.NotEmpty(errors);
            return errors[0];
        }

        private static void AssertBox(Hikan.Core.HikanPart part, decimal u, decimal vMin, decimal vMax, decimal sMin, decimal sMax)
        {
            Xunit.Assert.Equal(-u, part.UMin);
            Xunit.Assert.Equal(u, part.UMax);
            Xunit.Assert.Equal(vMin, part.VMin);
            Xunit.Assert.Equal(vMax, part.VMax);
            Xunit.Assert.Equal(sMin, part.SMin);
            Xunit.Assert.Equal(sMax, part.SMax);
        }

        // テスト1: 区間は川裏側端部 [0, 3]・標準部 [3, 16]・川表側端部 [16, 20]。函体の外形は区間ごとの直方体で、
        // 内空(幅・高さ・敷高 0.4)は共通。端部の底版が厚いと底版下面が Z = 0 より下がる(川裏側端部は −0.2)。
        [Xunit.Fact]
        public void Zones_SplitBarrelIntoBoxes()
        {
            Hikan.Core.HikanParameters p = Zoned();
            (string Name, decimal SMin, decimal SMax, Hikan.Core.HikanBarrelSection Section)[] zones =
                Hikan.Core.HikanGeometry.BarrelZones(p);
            Xunit.Assert.Equal(3, zones.Length);
            Xunit.Assert.Equal("川裏側端部", zones[0].Name);
            Xunit.Assert.Equal("標準部", zones[1].Name);
            Xunit.Assert.Equal("川表側端部", zones[2].Name);

            Hikan.Core.HikanPart[] parts = Hikan.Core.HikanGeometry.GetParts(p);
            AssertBox(parts[0], 1.5m, -0.2m, 2.9m, 0m, 3m);
            AssertBox(parts[1], 1.4m, 0m, 2.8m, 3m, 16m);
            AssertBox(parts[2], 1.6m, 0m, 2.8m, 16m, 20m);
            Hikan.Core.HikanPart hollow = System.Array.Find(parts, x => x.IsVoid);
            AssertBox(hollow, 1.0m, 0.4m, 2.4m, 0m, 20m);
        }

        // テスト2: 端部の長さ 0(既定)は区間を作らず、全長が標準部の 1 個の直方体(名前は「函体」)。
        [Xunit.Fact]
        public void NoEnds_IsSingleBarrel()
        {
            Hikan.Core.HikanParameters p = HikanTestData.BarrelOnly();
            Xunit.Assert.Single(Hikan.Core.HikanGeometry.BarrelZones(p));
            Xunit.Assert.Equal("函体", Hikan.Core.HikanGeometry.GetParts(p)[0].Name);
            Xunit.Assert.Empty(Hikan.Core.HikanGeometry.GetBarrelHaunchParts(p));
        }

        // テスト3: 体積。川裏側端部 (3.0 × 3.1 − (4 − 2 × 0.045)) × 3 = 16.17、標準部 (7.84 − (4 − 2 × 0.02)) × 13 = 50.44、
        // 川表側端部 (3.2 × 2.8 − (4 − 2 × 0.04)) × 4 = 20.16、計 86.77。部位別(頂版 24.18 + 底版 25.08 + 側壁 36.4
        // + ハンチ 0.27 + 0.52 + 0.32 = 1.11)の和とも一致する。重なりの独立計算(スラブ分解 + ハンチ)とも一致する。
        [Xunit.Fact]
        public void Volume_ByZone()
        {
            Hikan.Core.HikanParameters p = Zoned();
            Xunit.Assert.Empty(Hikan.Core.HikanValidator.Validate(p));
            Xunit.Assert.Equal(1.11m, Hikan.Core.HikanGeometry.BarrelHaunchVolume(p));
            Xunit.Assert.Equal(86.77m, Hikan.Core.HikanGeometry.BarrelVolume(p));
            Xunit.Assert.Equal(86.77m, Hikan.Core.HikanGeometry.ModelVolume(p));
            Xunit.Assert.Equal(Hikan.Core.HikanGeometry.ModelVolume(p),
                HikanPartsTests.SlabVolume(p) + Hikan.Core.HikanGeometry.BarrelHaunchVolume(p));

            Hikan.Core.HikanEstimate e = Hikan.Core.HikanEstimator.Calculate(p);
            Xunit.Assert.Equal(24.180m, e.TopSlabVolume);
            Xunit.Assert.Equal(25.080m, e.BottomSlabVolume);
            Xunit.Assert.Equal(36.400m, e.WallVolume);
            Xunit.Assert.Equal(1.110m, e.BarrelHaunchVolume);
            Xunit.Assert.Equal(86.770m, e.BarrelConcreteVolume);
            Xunit.Assert.Equal(86.770m, e.ConcreteVolume);
            // 外形・断面積の欄は標準部の値(内空断面積はハンチを控除)。
            Xunit.Assert.Equal(2.800m, e.OuterWidth);
            Xunit.Assert.Equal(3.960m, e.InnerSectionArea);
            Xunit.Assert.Equal(3.880m, e.ConcreteSectionArea);
        }

        // テスト4: 重心。区間ごとの外形 − 内空 + ハンチ三角形のモーメントを Python の Decimal で独立に計算した値
        // (V 1.388874803…、S 9.984038262…)。左右対称なので U は 0。範囲は最も外へ出る区間で決まる。
        [Xunit.Fact]
        public void CentroidAndExtents_ByZone()
        {
            Hikan.Core.HikanParameters p = Zoned();
            (decimal X, decimal Y, decimal Z) c = Hikan.Core.HikanGeometry.ExpectedCentroid(p);
            Xunit.Assert.Equal(0m, System.Math.Round(c.X, 9));
            Xunit.Assert.Equal(9.984038262m, System.Math.Round(c.Y, 9));
            Xunit.Assert.Equal(1.388874803m, System.Math.Round(c.Z, 9));

            (decimal MinX, decimal MinY, decimal MinZ, decimal MaxX, decimal MaxY, decimal MaxZ) x =
                Hikan.Core.HikanGeometry.ExpectedExtents(p);
            Xunit.Assert.Equal(-1.6m, x.MinX);
            Xunit.Assert.Equal(1.6m, x.MaxX);
            Xunit.Assert.Equal(-0.2m, x.MinZ);
            Xunit.Assert.Equal(2.9m, x.MaxZ);
            Xunit.Assert.Equal(0m, x.MinY);
            Xunit.Assert.Equal(20m, x.MaxY);
        }

        // テスト5: ハンチの三角形は内空の隅にあり、反時計回り(面積が正)。左上は (−1.0, 2.1)・(−0.7, 2.4)・(−1.0, 2.4)。
        [Xunit.Fact]
        public void HaunchParts_SitInVoidCorners()
        {
            Hikan.Core.HikanPrismPart[] parts = Hikan.Core.HikanGeometry.GetBarrelHaunchParts(Zoned());
            Xunit.Assert.Equal(6, parts.Length);
            foreach (Hikan.Core.HikanPrismPart part in parts)
            {
                Xunit.Assert.True(part.Area > 0m, part.Name);
                foreach ((decimal U, decimal V) q in part.Section)
                {
                    Xunit.Assert.InRange(q.U, -1.0m, 1.0m);
                    Xunit.Assert.InRange(q.V, 0.4m, 2.4m);
                }
            }
            Hikan.Core.HikanPrismPart lt = System.Array.Find(parts, x => x.Name == "函体ハンチ(川裏側端部)LT");
            Xunit.Assert.Equal(new (decimal U, decimal V)[] { (-1.0m, 2.1m), (-0.7m, 2.4m), (-1.0m, 2.4m) }, lt.Section);
            Xunit.Assert.Equal(0m, lt.SMin);
            Xunit.Assert.Equal(3m, lt.SMax);
            Hikan.Core.HikanPrismPart rb = System.Array.Find(parts, x => x.Name == "函体ハンチ(川表側端部)RB");
            Xunit.Assert.Equal(new (decimal U, decimal V)[] { (0.6m, 0.4m), (1.0m, 0.4m), (1.0m, 0.6m) }, rb.Section);
        }

        // テスト6: 型枠。内空面は区間長 × 周長(ハンチの隅は 2 辺の代わりに斜辺)= 154.677(Python で独立計算)。
        // 外側面 2 × 3.1 × 3 + 2 × 2.8 × 13 + 2 × 2.8 × 4 = 113.8、頂版上面 3.0 × 3 + 2.8 × 13 + 3.2 × 4 = 58.2。
        // 端面・段差面: 両端面 5.39 + 5.04、段差面 (9.3 + 7.84 − 2 × 7.84) + ハンチの差 0.05 = 1.51、
        // (8.96 + 7.84 − 2 × 7.84) + 0.12 = 1.24、計 13.18。
        [Xunit.Fact]
        public void Formwork_ByZone()
        {
            Hikan.Core.HikanEstimate e = Hikan.Core.HikanEstimator.Calculate(Zoned());
            Xunit.Assert.Equal(154.677m, e.FormworkInner);
            Xunit.Assert.Equal(113.800m, e.FormworkOuterSide);
            Xunit.Assert.Equal(58.200m, e.FormworkTop);
            Xunit.Assert.Equal(13.180m, e.FormworkEnd);
        }

        // テスト7: 地下占有体積 = 外形の直方体の和(内空を埋め戻した状態)= 27.9 + 101.92 + 35.84 = 165.66。
        // 床掘りの底は最も深い底版下面(川裏側端部 −0.2)、幅は最も広い区間(3.2)から決まる。
        [Xunit.Fact]
        public void Envelope_And_Excavation_ByZone()
        {
            Hikan.Core.HikanParameters p = Zoned();
            Xunit.Assert.Equal(165.66m, Hikan.Core.HikanGeometry.EnvelopeVolume(p));
            Hikan.Core.HikanEstimate e = Hikan.Core.HikanEstimator.Calculate(p);
            Xunit.Assert.Equal(165.660m, e.OccupiedVolume);
            Xunit.Assert.Equal(4.200m, e.ExcavationBottomWidth);      // 3.2 + 2 × 0.5
            Xunit.Assert.Equal(5.100m, e.ExcavationDepth);            // 土かぶり 2.0 + 標準部の頂版上面 2.8 + 0.2 + 基礎 0.1
        }

        // テスト8: 胸壁・しゃ水壁は取り付く区間の外形に付く。胸壁のたて壁は川裏側端部(外形幅 3.0)の側面から張り出し、
        // 底版(かかと版が標準部へまたがる)と函体の底版との重なりは区間ごとに控除する。
        // しゃ水壁 4 枚(2.5・7.5・12.5・17.5)は、1 枚目が川裏側端部、4 枚目が川表側端部の外形の周りの環になる。
        // 部材を足した分の体積の増え方が、胸壁・しゃ水壁の数量の式と一致する(胸壁としゃ水壁は別々に確かめる)。
        [Xunit.Fact]
        public void BreastAndCutoff_FollowZoneOuter()
        {
            Hikan.Core.HikanParameters p = Zoned();
            Hikan.Core.HikanBreastWall w = p.UpstreamBreast;
            w.StemThickness = 0.4m;
            w.Position = 1.5m;             // たて壁 1.3〜1.7(川裏側端部)
            w.Length = 1.0m;
            w.CrownHeight = 3.5m;
            w.FootingThickness = 0.5m;
            w.ToeLength = 0.5m;
            w.HeelLength = 2.0m;           // 底版 0.8〜3.7(標準部へまたがる)
            Xunit.Assert.Empty(Hikan.Core.HikanValidator.Validate(p));

            Hikan.Core.HikanPart stem = System.Array.Find(Hikan.Core.HikanGeometry.GetParts(p), x => x.Name == "川裏側胸壁右たて壁");
            Xunit.Assert.Equal(1.5m, stem.UMin);
            Xunit.Assert.Equal(2.5m, stem.UMax);
            // たて壁 2 × 1.0 × 3.1 × 0.4 = 2.48、底版 5.0 × 0.5 × 2.9 = 7.25、
            // 函体との重なり 3.0 × 0.5 × 2.2(0.8〜3.0)+ 2.8 × 0.4 × 0.7(3.0〜3.7)= 3.3 + 0.784 → 正味 5.646。
            Xunit.Assert.Equal(5.646m, Hikan.Core.HikanGeometry.BreastWallVolume(p, w, true));
            Hikan.Core.HikanParameters bare = Zoned();
            Xunit.Assert.Equal(Hikan.Core.HikanGeometry.ModelVolume(bare) + 5.646m, Hikan.Core.HikanGeometry.ModelVolume(p));
            Xunit.Assert.Equal(Hikan.Core.HikanGeometry.ModelVolume(p),
                HikanPartsTests.SlabVolume(p) + Hikan.Core.HikanGeometry.BarrelHaunchVolume(p));

            Hikan.Core.HikanParameters q = Zoned();
            q.CutoffCount = 4;
            q.CutoffThickness = 0.4m;
            q.CutoffProjection = 0.5m;
            Xunit.Assert.Empty(Hikan.Core.HikanValidator.Validate(q));
            Hikan.Core.HikanPart[] parts = Hikan.Core.HikanGeometry.GetParts(q);
            AssertBox(System.Array.Find(parts, x => x.Name == "しゃ水壁1"), 2.0m, -0.7m, 3.4m, 2.3m, 2.7m);
            AssertBox(System.Array.Find(parts, x => x.Name == "しゃ水壁4"), 2.1m, -0.5m, 3.3m, 17.3m, 17.7m);
            // しゃ水壁: (4.0 × 4.1 − 9.3) × 0.4 = 2.84、(3.8 × 3.8 − 7.84) × 0.4 = 2.64 × 2 枚、(4.2 × 3.8 − 8.96) × 0.4 = 2.8。
            Xunit.Assert.Equal(10.92m, Hikan.Core.HikanGeometry.CutoffVolume(q));
            Xunit.Assert.Equal(Hikan.Core.HikanGeometry.ModelVolume(bare) + 10.92m, Hikan.Core.HikanGeometry.ModelVolume(q));
            Xunit.Assert.Equal(Hikan.Core.HikanGeometry.ModelVolume(q),
                HikanPartsTests.SlabVolume(q) + Hikan.Core.HikanGeometry.BarrelHaunchVolume(q));
        }

        // テスト9: 胸壁のたて壁・しゃ水壁は部材厚が変わる境目をまたげない。ハンチだけが違う境目はまたいでよい。
        [Xunit.Fact]
        public void Members_MustNotStraddleOuterStep()
        {
            Hikan.Core.HikanParameters a = Zoned();
            a.UpstreamBreast.StemThickness = 0.4m;
            a.UpstreamBreast.Position = 3.0m;   // たて壁 2.8〜3.2 が境目 3.0 をまたぐ
            string m = FirstError(a);
            Xunit.Assert.StartsWith("川裏側胸壁のたて壁(", m);
            Xunit.Assert.Contains("S = 3.0", m);

            Hikan.Core.HikanParameters b = Zoned();
            b.UpstreamEnd.WallThickness = 0.4m;  // 外形は標準部と同じでハンチだけが違う
            b.UpstreamEnd.TopSlabThickness = 0.4m;
            b.UpstreamEnd.BottomSlabThickness = 0.4m;
            b.UpstreamBreast.StemThickness = 0.4m;
            b.UpstreamBreast.Position = 3.0m;
            Xunit.Assert.Empty(Hikan.Core.HikanValidator.Validate(b));

            Hikan.Core.HikanParameters c = Zoned();
            c.UpstreamEndLength = 2.5m;          // 1 枚目のしゃ水壁 2.3〜2.7 が境目 2.5 をまたぐ
            c.CutoffCount = 4;
            c.CutoffThickness = 0.4m;
            Xunit.Assert.StartsWith("しゃ水壁 1 枚目", FirstError(c));
        }

        // テスト10: 端部の検査。長さは 0 以上で両端部の和は延長以下、部材厚は 0.40 m 以上・0.10 m ピッチ(長さ 0 なら検査しない)。
        [Xunit.Fact]
        public void Ends_AreValidated()
        {
            Hikan.Core.HikanParameters a = Zoned();
            a.UpstreamEnd.WallThickness = 0.45m;
            Xunit.Assert.StartsWith("川裏側端部の側壁厚", FirstError(a));

            Hikan.Core.HikanParameters b = Zoned();
            b.UpstreamEndLength = 0m;
            b.UpstreamEnd.WallThickness = 0.45m;  // 端部なしなら使わないので検査しない
            Xunit.Assert.Empty(Hikan.Core.HikanValidator.Validate(b));

            Hikan.Core.HikanParameters c = Zoned();
            c.DownstreamEndLength = 17.5m;        // 3.0 + 17.5 > 20
            Xunit.Assert.StartsWith("川裏側端部の長さ + 川表側端部の長さ", FirstError(c));

            Hikan.Core.HikanParameters d = Zoned();
            d.DownstreamEndLength = -1m;
            Xunit.Assert.StartsWith("川表側端部の長さ", FirstError(d));

            Hikan.Core.HikanParameters e = Zoned();
            e.DownstreamEndLength = 17.0m;        // 両端部で全長(標準部なし)は可
            Xunit.Assert.Empty(Hikan.Core.HikanValidator.Validate(e));
            Xunit.Assert.Equal(2, Hikan.Core.HikanGeometry.BarrelZones(e).Length);
        }

        // テスト11: ハンチの検査。H・W は両方正か両方 0、上下・左右のハンチの和は内空高・内空幅より小さい。
        [Xunit.Fact]
        public void Haunches_AreValidated()
        {
            Hikan.Core.HikanParameters a = Zoned();
            a.Standard.LeftBottom.Height = 0.2m;
            Xunit.Assert.StartsWith("標準部のハンチ LB の W", FirstError(a));

            Hikan.Core.HikanParameters b = Zoned();
            b.Standard.RightTop.Width = -0.1m;
            Xunit.Assert.StartsWith("標準部のハンチ RT の W", FirstError(b));

            Hikan.Core.HikanParameters c = Zoned();
            Haunch(c.UpstreamEnd.LeftBottom, 1.7m, 0.2m);   // 0.3 + 1.7 = 2.0 = 内空高
            Xunit.Assert.StartsWith("川裏側端部のハンチ LT の H", FirstError(c));

            Hikan.Core.HikanParameters d = Zoned();
            Haunch(d.DownstreamEnd.LeftBottom, 0.2m, 1.0m);  // W の和 1.0 + 1.0 = 2.0 = 内空幅
            Haunch(d.DownstreamEnd.RightBottom, 0.2m, 1.0m);
            Xunit.Assert.StartsWith("川表側端部のハンチ LB の W", FirstError(d));
        }

        // テスト12: スパン。合計が函体延長、個数がブロック数。0 や負のスパン、0 個・6 個以上はエラー。
        [Xunit.Fact]
        public void Spans_DefineLengthAndBlocks()
        {
            Hikan.Core.HikanParameters p = HikanTestData.BarrelOnly();
            p.Spans = new decimal[] { 4.5m, 15.0m, 4.5m };
            Xunit.Assert.Equal(24.0m, p.BarrelLength);
            Xunit.Assert.Equal(3, p.BlockCount);
            Hikan.Core.HikanEstimate e = Hikan.Core.HikanEstimator.Calculate(p);
            Xunit.Assert.Equal(8.000m, e.BlockLength);
            Xunit.Assert.Equal(92.160m, e.BarrelConcreteVolume);   // 3.84 × 24

            Hikan.Core.HikanParameters a = HikanTestData.BarrelOnly();
            a.Spans = new decimal[] { 5m, 0m, 5m };
            Xunit.Assert.StartsWith("スパン2 の長さ", FirstError(a));

            Hikan.Core.HikanParameters b = HikanTestData.BarrelOnly();
            b.Spans = new decimal[0];
            Xunit.Assert.StartsWith("スパン1 の長さ", FirstError(b));

            Hikan.Core.HikanParameters c = HikanTestData.BarrelOnly();
            c.Spans = new decimal[] { 3m, 3m, 3m, 3m, 3m, 3m };
            Xunit.Assert.StartsWith("スパンの数", FirstError(c));
        }

        // テスト13: XData 往復で端部・ハンチ・スパンが戻る。旧 XData(函体延長とブロック数)は等分できれば等分、
        // できなければ 1 スパンとして読む。端部の断面のキーが無ければ標準部と同じ断面(長さ 0)。
        [Xunit.Fact]
        public void XData_RoundTrip_And_Legacy()
        {
            Hikan.Core.HikanParameters p = Zoned();
            p.Spans = new decimal[] { 4.5m, 11.0m, 4.5m };
            Hikan.Core.HikanParameters q = Hikan.Core.HikanParameters.FromDictionary(p.ToDictionary());
            Xunit.Assert.Equal(p.ToDictionary(), q.ToDictionary());
            Xunit.Assert.Equal(Hikan.Core.HikanGeometry.ModelVolume(p), Hikan.Core.HikanGeometry.ModelVolume(q));

            System.Collections.Generic.Dictionary<string, string> d = HikanTestData.BarrelOnly().ToDictionary();
            foreach (string key in new System.Collections.Generic.List<string>(d.Keys))
            {
                if (key.StartsWith("span_", System.StringComparison.Ordinal)
                    || key.StartsWith("upstream_end_", System.StringComparison.Ordinal)
                    || key.StartsWith("downstream_end_", System.StringComparison.Ordinal))
                {
                    d.Remove(key);
                }
            }
            d["wall_thickness"] = "0.5";
            d["barrel_length"] = "20";
            d["block_count"] = "3";                // 20 / 3 は割り切れない → 1 スパン
            Hikan.Core.HikanParameters r = Hikan.Core.HikanParameters.FromDictionary(d);
            Xunit.Assert.Equal(new decimal[] { 20m }, r.Spans);
            Xunit.Assert.Equal(0m, r.UpstreamEndLength);
            Xunit.Assert.Equal(0.5m, r.UpstreamEnd.WallThickness);   // 標準部と同じ
            d["block_count"] = "5";                // 20 / 5 = 4 → 5 スパン
            Xunit.Assert.Equal(new decimal[] { 4m, 4m, 4m, 4m, 4m }, Hikan.Core.HikanParameters.FromDictionary(d).Spans);
            d["block_count"] = "8";                // 上限 5 を超える → 1 スパン
            Xunit.Assert.Equal(new decimal[] { 20m }, Hikan.Core.HikanParameters.FromDictionary(d).Spans);
        }

        // テスト14: 門柱は立つ区間(川表側端部)の頂版上面を土留壁の下端・柱高さの判定に使う。
        // 川表側端部の頂版を 0.6 にすると頂版上面は 3.0(標準部は 2.8)、土留壁は V 3.0 から。
        [Xunit.Fact]
        public void Gate_UsesZoneSlabTop()
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.DownstreamEndLength = 4.0m;               // 16〜20(門柱 17.8〜19.1 を含む)
            p.DownstreamEnd.TopSlabThickness = 0.6m;
            p.Gate.RetainingWallHeight = 1.0m;          // 上端 4.0 = 川表側胸壁の天端
            Xunit.Assert.Empty(Hikan.Core.HikanValidator.ValidateInput(p));
            Xunit.Assert.Equal(3.0m, Hikan.Core.HikanGeometry.GateSlabTop(p));
            Hikan.Core.HikanPart wall = System.Array.Find(Hikan.Core.HikanGeometry.GetParts(p), x => x.Name == "土留壁");
            Xunit.Assert.Equal(3.0m, wall.VMin);
            Xunit.Assert.Equal(4.0m, wall.VMax);

            p.Gate.RetainingWallHeight = 0.9m;          // 上端 3.9 < 天端 4.0
            Xunit.Assert.Contains(Hikan.Core.HikanValidator.Validate(p), x => x.StartsWith("門柱・操作台の土留壁高さ WH", System.StringComparison.Ordinal));
        }
    }
}
