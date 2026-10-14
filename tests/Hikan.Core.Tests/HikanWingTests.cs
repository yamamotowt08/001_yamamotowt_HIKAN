namespace Hikan.Core.Tests
{
    /// <summary>
    /// 第3段階(翼壁)の検証。
    ///
    /// 翼壁は軸にそろった直方体ではない(八の字に開き、天端が傾く)ので、第2段階のスラブ分解は使えない。
    /// 代わりに次の 3 つを独立に突き合わせる。
    /// 1. 閉じた式(HikanGeometry.WingWallVolume)
    /// 2. 部材ごとのシンプソン積分(HikanTaperedPart.Moments)
    /// 3. (U, S) の柱ごとに V 方向の区間を厳密に重ね合わせる数値積分(このファイルの ColumnIntegral)。
    ///    同じ柱で部材の区間が重なった長さも数えるので、「部材は重ならない」という前提が崩れていれば検出できる。
    /// </summary>
    public class HikanWingTests
    {
        /// <summary>胸壁・しゃ水壁・翼壁をすべて持つ構成(HIKAN_SelfTest と同じ寸法)。</summary>
        private static Hikan.Core.HikanParameters WithWings()
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.TopSlabThickness = 0.4m;
            p.BottomSlabThickness = 0.6m;

            p.UpstreamBreast.StemThickness = 0.5m;
            p.UpstreamBreast.Position = 2.0m;
            p.UpstreamBreast.Length = 1.5m;
            p.UpstreamBreast.CrownHeight = 4.0m;
            p.UpstreamBreast.Embedment = 1.0m;
            p.UpstreamBreast.FootingThickness = 0.5m;
            p.UpstreamBreast.ToeLength = 0.8m;
            p.UpstreamBreast.HeelLength = 1.2m;
            p.DownstreamBreast.StemThickness = 0.6m;
            p.DownstreamBreast.Position = 17.5m;
            p.DownstreamBreast.Length = 2.0m;
            p.DownstreamBreast.CrownHeight = 4.5m;
            p.DownstreamBreast.Embedment = 1.2m;
            p.DownstreamBreast.FootingThickness = 0.6m;
            p.DownstreamBreast.ToeLength = 1.0m;
            p.DownstreamBreast.HeelLength = 1.5m;

            p.CutoffCount = 2;
            p.CutoffThickness = 0.5m;
            p.CutoffProjection = 0.7m;

            // 川裏側は八の字(内空幅 2.0 → 端部 3.0、翼壁長 3.0 → tanθ = 1/6)、川表側は平行。
            p.UpstreamWing.StemThickness = 0.5m;
            p.UpstreamWing.Length = 3.0m;
            p.UpstreamWing.EndWidth = 3.0m;
            p.UpstreamWing.CrownStart = 3.5m;
            p.UpstreamWing.CrownEnd = 2.0m;
            p.UpstreamWing.Embedment = 1.0m;
            p.UpstreamWing.FootingThickness = 0.5m;
            p.UpstreamWing.ToeLength = 0.4m;
            p.UpstreamWing.HeelLength = 1.0m;
            p.DownstreamWing.StemThickness = 0.6m;
            p.DownstreamWing.Length = 4.0m;
            p.DownstreamWing.EndWidth = 2.0m;
            p.DownstreamWing.CrownStart = 4.0m;
            p.DownstreamWing.CrownEnd = 2.5m;
            p.DownstreamWing.Embedment = 1.2m;
            p.DownstreamWing.FootingThickness = 0.6m;
            p.DownstreamWing.ToeLength = 0.5m;
            p.DownstreamWing.HeelLength = 1.2m;
            return p;
        }

        private static decimal WingPartsVolume(Hikan.Core.HikanParameters p, string end)
        {
            decimal v = 0m;
            foreach (Hikan.Core.HikanTaperedPart part in Hikan.Core.HikanGeometry.GetWingParts(p))
            {
                if (part.Name.StartsWith(end, System.StringComparison.Ordinal)) { v += part.Volume; }
            }
            return v;
        }

        private static void AssertClose(decimal expected, decimal actual, decimal tolerance, string label)
        {
            Xunit.Assert.True(System.Math.Abs(expected - actual) <= tolerance,
                label + ": 期待 " + expected + " / 実際 " + actual + " / 許容 " + tolerance);
        }

        /// <summary>
        /// (U, S) の柱ごとに、柱を通る部材の V 区間を集めて厳密に合併・控除する数値積分。
        /// 戻り値: 合併体積、部材区間の重なり体積(同じ柱で 2 部材以上が重なった長さの合計)。
        /// </summary>
        private static (decimal Union, decimal Overlap) ColumnIntegral(Hikan.Core.HikanParameters p, decimal cell)
        {
            Hikan.Core.HikanPart[] boxes = Hikan.Core.HikanGeometry.GetParts(p);
            Hikan.Core.HikanTaperedPart[] wings = Hikan.Core.HikanGeometry.GetWingParts(p);
            (decimal HalfWidth, decimal VMin, decimal SMin, decimal SMax) r = Hikan.Core.HikanGeometry.SolidRange(p);

            decimal union = 0m;
            decimal overlap = 0m;
            int nu = (int)System.Math.Ceiling(2m * r.HalfWidth / cell);
            int ns = (int)System.Math.Ceiling((r.SMax - r.SMin) / cell);
            System.Collections.Generic.List<(decimal Lo, decimal Hi)> solid = new System.Collections.Generic.List<(decimal Lo, decimal Hi)>();
            System.Collections.Generic.List<(decimal Lo, decimal Hi)> hollow = new System.Collections.Generic.List<(decimal Lo, decimal Hi)>();

            for (int i = 0; i < nu; i++)
            {
                decimal u = -r.HalfWidth + (i + 0.5m) * cell;
                for (int j = 0; j < ns; j++)
                {
                    decimal s = r.SMin + (j + 0.5m) * cell;
                    solid.Clear();
                    hollow.Clear();
                    foreach (Hikan.Core.HikanPart b in boxes)
                    {
                        if (u <= b.UMin || u >= b.UMax || s <= b.SMin || s >= b.SMax) { continue; }
                        if (b.IsVoid) { hollow.Add((b.VMin, b.VMax)); } else { solid.Add((b.VMin, b.VMax)); }
                    }
                    foreach (Hikan.Core.HikanTaperedPart w in wings)
                    {
                        if (s <= w.S0 || s >= w.S1) { continue; }
                        decimal t = (s - w.S0) / (w.S1 - w.S0);
                        decimal ul = w.UL0 + (w.UL1 - w.UL0) * t;
                        decimal ur = w.UR0 + (w.UR1 - w.UR0) * t;
                        if (u <= ul || u >= ur) { continue; }
                        solid.Add((w.VBottom, w.VTop0 + (w.VTop1 - w.VTop0) * t));
                    }
                    if (solid.Count == 0) { continue; }

                    decimal sum = 0m;
                    foreach ((decimal Lo, decimal Hi) iv in solid) { sum += iv.Hi - iv.Lo; }
                    System.Collections.Generic.List<(decimal Lo, decimal Hi)> merged = Merge(solid);
                    decimal mergedLength = 0m;
                    foreach ((decimal Lo, decimal Hi) iv in merged) { mergedLength += iv.Hi - iv.Lo; }
                    overlap += (sum - mergedLength) * cell * cell;

                    // 内空を控除(内空は合併区間の内側にあることが前提だが、交差長で厳密に引く)
                    decimal length = mergedLength;
                    foreach ((decimal Lo, decimal Hi) h in Merge(hollow))
                    {
                        foreach ((decimal Lo, decimal Hi) iv in merged)
                        {
                            decimal lo = System.Math.Max(h.Lo, iv.Lo);
                            decimal hi = System.Math.Min(h.Hi, iv.Hi);
                            if (hi > lo) { length -= hi - lo; }
                        }
                    }
                    union += length * cell * cell;
                }
            }
            return (union, overlap);
        }

        private static System.Collections.Generic.List<(decimal Lo, decimal Hi)> Merge(
            System.Collections.Generic.List<(decimal Lo, decimal Hi)> intervals)
        {
            System.Collections.Generic.List<(decimal Lo, decimal Hi)> sorted =
                new System.Collections.Generic.List<(decimal Lo, decimal Hi)>(intervals);
            sorted.Sort((a, b) => a.Lo.CompareTo(b.Lo));
            System.Collections.Generic.List<(decimal Lo, decimal Hi)> result = new System.Collections.Generic.List<(decimal Lo, decimal Hi)>();
            foreach ((decimal Lo, decimal Hi) iv in sorted)
            {
                if (result.Count > 0 && iv.Lo <= result[result.Count - 1].Hi)
                {
                    (decimal Lo, decimal Hi) last = result[result.Count - 1];
                    result[result.Count - 1] = (last.Lo, System.Math.Max(last.Hi, iv.Hi));
                }
                else
                {
                    result.Add(iv);
                }
            }
            return result;
        }

        // テスト1: 翼壁の体積は「閉じた式」と「部材ごとのシンプソン積分」が一致する(八の字・天端傾斜あり)。
        [Xunit.Fact]
        public void WingVolume_ClosedFormMatchesSimpson()
        {
            Hikan.Core.HikanParameters p = WithWings();
            AssertClose(Hikan.Core.HikanGeometry.WingWallVolume(p, p.UpstreamWing), WingPartsVolume(p, "川裏側"),
                0.000000001m, "川裏側(八の字)");
            AssertClose(Hikan.Core.HikanGeometry.WingWallVolume(p, p.DownstreamWing), WingPartsVolume(p, "川表側"),
                0.000000001m, "川表側(平行)");
        }

        // テスト2: 平行翼壁(端部水路幅 = 内空幅)は開き 0、体積は 2 × {厚 × 長さ × (平均天端 − 底版上面) + 底版} で厳密。
        [Xunit.Fact]
        public void ParallelWing_HasNoFlareAndExactVolume()
        {
            Hikan.Core.HikanParameters p = WithWings();
            Hikan.Core.HikanWingWall w = p.DownstreamWing;
            Xunit.Assert.Equal(0m, Hikan.Core.HikanGeometry.WingSlope(p, w));
            Xunit.Assert.Equal(1m, Hikan.Core.HikanGeometry.WingWidthFactor(p, w));
            Xunit.Assert.Equal(0m, Hikan.Core.HikanGeometry.WingFlareAngle(p, w));
            // 底版上面 = 0.6 − 1.2 = −0.6。たて壁 0.6 × 4.0 × (3.25 + 0.6) = 9.24、底版 2.3 × 4.0 × 0.6 = 5.52
            Xunit.Assert.Equal(2m * (9.24m + 5.52m), Hikan.Core.HikanGeometry.WingWallVolume(p, w));
        }

        // テスト3: 開き角は幅の差と翼壁長から決まる。tanθ = (3.0 − 2.0) / (2 × 3.0) = 1/6 → 9.462°
        [Xunit.Fact]
        public void FlareAngle_IsDeterminedByWidthDifference()
        {
            Hikan.Core.HikanParameters p = WithWings();
            Hikan.Core.HikanEstimate e = Hikan.Core.HikanEstimator.Calculate(p);
            Xunit.Assert.Equal(9.462m, e.UpstreamWingFlareAngle);
            Xunit.Assert.Equal(0.000m, e.DownstreamWingFlareAngle);

            // 端部の水路幅を広げると開き角も大きくなる
            p.UpstreamWing.EndWidth = 4.0m;
            Xunit.Assert.True(Hikan.Core.HikanEstimator.Calculate(p).UpstreamWingFlareAngle > 9.462m);
        }

        // テスト4: 翼壁の平面形。内面は函体端で内空側面(±B/2)、翼壁端部で ±端部水路幅/2。左右は鏡映。
        [Xunit.Fact]
        public void WingPlan_StartsAtInnerSideAndEndsAtEndWidth()
        {
            Hikan.Core.HikanParameters p = WithWings();
            System.Collections.Generic.Dictionary<string, Hikan.Core.HikanTaperedPart> byName =
                new System.Collections.Generic.Dictionary<string, Hikan.Core.HikanTaperedPart>();
            foreach (Hikan.Core.HikanTaperedPart part in Hikan.Core.HikanGeometry.GetWingParts(p))
            {
                byName[part.Name] = part;
            }
            Xunit.Assert.Equal(8, byName.Count);

            // 川表側(平行)右: S = 20 → 24、内面 U = 1.0 のまま
            Hikan.Core.HikanTaperedPart down = byName["川表側翼壁右たて壁"];
            Xunit.Assert.Equal(20m, down.S0);
            Xunit.Assert.Equal(24m, down.S1);
            Xunit.Assert.Equal(1.0m, down.UL0);
            Xunit.Assert.Equal(1.0m, down.UL1);
            Xunit.Assert.Equal(1.6m, down.UR0);
            Xunit.Assert.Equal(4.0m, down.VTop0);   // 函体端で天端 4.0
            Xunit.Assert.Equal(2.5m, down.VTop1);   // 端部で天端 2.5

            // 川裏側(八の字)右: S = −3 → 0。端部(S = −3)で内面 U = 1.5、函体端(S = 0)で 1.0
            Hikan.Core.HikanTaperedPart up = byName["川裏側翼壁右たて壁"];
            Xunit.Assert.Equal(-3m, up.S0);
            Xunit.Assert.Equal(0m, up.S1);
            Xunit.Assert.Equal(1.5m, up.UL0);
            Xunit.Assert.Equal(1.0m, up.UL1);
            Xunit.Assert.Equal(2.0m, up.VTop0);     // 端部で天端 2.0
            Xunit.Assert.Equal(3.5m, up.VTop1);     // 函体端で天端 3.5

            // 左右は鏡映
            Hikan.Core.HikanTaperedPart left = byName["川裏側翼壁左底版"];
            Hikan.Core.HikanTaperedPart right = byName["川裏側翼壁右底版"];
            Xunit.Assert.Equal(-right.UR0, left.UL0);
            Xunit.Assert.Equal(-right.UL1, left.UR1);
            Xunit.Assert.Equal(right.VBottom, left.VBottom);
        }

        // テスト5: 独立した柱積分で、全部材の合併体積が ModelVolume と一致し、部材どうしの重なりが
        // 「しゃ水壁 ∩ 函体」(既知・解析的に控除済み)以外に存在しないこと。
        [Xunit.Fact]
        public void ColumnIntegral_MatchesModelVolume_AndNoUnexpectedOverlap()
        {
            Hikan.Core.HikanParameters p = WithWings();
            (decimal Union, decimal Overlap) c = ColumnIntegral(p, 0.05m);
            decimal model = Hikan.Core.HikanGeometry.ModelVolume(p);

            // 斜めの辺をセルで近似するぶんの誤差だけを許す(0.2 %)
            AssertClose(model, c.Union, model * 0.002m, "合併体積");

            // 既知の重なりはしゃ水壁と函体のみ(外形断面 × 厚 × 枚数)。それ以外の重なりは 0。
            decimal knownOverlap = p.OuterWidth * p.OuterHeight * p.CutoffThickness * p.CutoffCount;
            AssertClose(knownOverlap, c.Overlap, 0.000001m, "重なり体積");
        }

        // テスト6: エクステントが翼壁の頂点を含むこと。
        [Xunit.Fact]
        public void ExpectedExtents_IncludeWings()
        {
            Hikan.Core.HikanParameters p = WithWings();
            (decimal MinX, decimal MinY, decimal MinZ, decimal MaxX, decimal MaxY, decimal MaxZ) x =
                Hikan.Core.HikanGeometry.ExpectedExtents(p);
            Xunit.Assert.Equal(-3.0m, x.MinY);   // 川裏側翼壁の端部
            Xunit.Assert.Equal(24.0m, x.MaxY);   // 川表側翼壁の端部
            Xunit.Assert.Equal(-1.2m, x.MinZ);   // 根入れ 1.2(川表側胸壁・翼壁)
            Xunit.Assert.Equal(4.5m, x.MaxZ);    // 川表側胸壁の天端
            Xunit.Assert.Equal(24.0m, Hikan.Core.HikanGeometry.GlobalSMax(p));
        }

        // テスト7: 重心。左右対称なので X は 0。川表側だけに翼壁を付けると重心は川表へ寄る。
        [Xunit.Fact]
        public void ExpectedCentroid_ShiftsTowardWing()
        {
            Hikan.Core.HikanParameters p = WithWings();
            Xunit.Assert.True(System.Math.Abs(Hikan.Core.HikanGeometry.ExpectedCentroid(p).X) < 0.000000001m);

            Hikan.Core.HikanParameters without = WithWings();
            without.UpstreamWing.StemThickness = 0m;
            without.DownstreamWing.StemThickness = 0m;
            Hikan.Core.HikanParameters downOnly = WithWings();
            downOnly.UpstreamWing.StemThickness = 0m;
            Xunit.Assert.True(Hikan.Core.HikanGeometry.ExpectedCentroid(downOnly).Y
                > Hikan.Core.HikanGeometry.ExpectedCentroid(without).Y);
        }

        // テスト8: 総コンクリート量と床掘り範囲に翼壁が含まれること。
        [Xunit.Fact]
        public void Estimate_IncludesWings()
        {
            Hikan.Core.HikanParameters p = WithWings();
            Hikan.Core.HikanEstimate e = Hikan.Core.HikanEstimator.Calculate(p);
            decimal parts = e.BarrelConcreteVolume + e.UpstreamBreastVolume + e.DownstreamBreastVolume
                + e.CutoffTotalVolume + e.UpstreamWingVolume + e.DownstreamWingVolume;
            AssertClose(parts, e.ConcreteVolume, 0.003m, "部材別の和 = 総計(丸め誤差のみ)");
            // 床掘りの平面長は 川裏翼壁端部 −3 から 川表翼壁端部 24 まで + 余裕幅 2 × 0.5
            Xunit.Assert.Equal(28.0m, e.ExcavationBottomLength);
            Xunit.Assert.True(e.FormworkWing > 0m);
        }

        private static void AssertInvalid(Hikan.Core.HikanParameters p)
        {
            Xunit.Assert.NotEmpty(Hikan.Core.HikanValidator.Validate(p));
            Xunit.Assert.Throws<Hikan.Core.HikanValidationException>(() => Hikan.Core.HikanEstimator.Calculate(p));
        }

        [Xunit.Fact]
        public void WithWings_IsValid()
        {
            Xunit.Assert.Empty(Hikan.Core.HikanValidator.Validate(WithWings()));
        }

        // テスト9: 端部水路幅は内空幅以上(開く向きのみ)。
        [Xunit.Fact]
        public void EndWidth_MustNotBeNarrowerThanInnerWidth()
        {
            Hikan.Core.HikanParameters p = WithWings();
            p.DownstreamWing.EndWidth = 1.5m;
            AssertInvalid(p);
        }

        // テスト10: 左右のつま先版が水路中央で接触しないこと(函体端で つま先版の横断幅 < 内空幅/2)。
        [Xunit.Theory]
        [Xunit.InlineData(0.9, true)]
        [Xunit.InlineData(1.0, false)]
        public void Toes_MustNotMeetAtChannelCentre(double toe, bool valid)
        {
            Hikan.Core.HikanParameters p = WithWings();
            p.DownstreamWing.ToeLength = (decimal)toe;
            if (valid) { Xunit.Assert.Empty(Hikan.Core.HikanValidator.Validate(p)); }
            else { AssertInvalid(p); }
        }

        // テスト11: 翼壁を付ける端で、胸壁の底版が函体端より外へ出ていると重なるのでエラー。
        [Xunit.Fact]
        public void BreastBeyondBarrelEnd_ConflictsWithWing()
        {
            Hikan.Core.HikanParameters p = WithWings();
            p.DownstreamBreast.Position = 19.7m;   // たて壁 19.4〜20.0、つま先版で 21.0 まで出る
            AssertInvalid(p);

            // 翼壁を付けなければ同じ胸壁は可
            p.DownstreamWing.StemThickness = 0m;
            Xunit.Assert.Empty(Hikan.Core.HikanValidator.Validate(p));
        }

        // テスト12: たて壁厚・底版厚は最小部材厚 0.40 m・0.10 m ピッチ(H13 Ⅴ章)。天端・根入れの条件。
        [Xunit.Fact]
        public void WingDimensions_FollowStandardAndConnect()
        {
            Hikan.Core.HikanParameters a = WithWings();
            a.UpstreamWing.StemThickness = 0.35m;
            AssertInvalid(a);

            Hikan.Core.HikanParameters b = WithWings();
            b.UpstreamWing.FootingThickness = 0.45m;
            AssertInvalid(b);

            Hikan.Core.HikanParameters c = WithWings();
            c.UpstreamWing.CrownStart = 0m;          // 函体端面に接しない
            AssertInvalid(c);

            Hikan.Core.HikanParameters d = WithWings();
            d.UpstreamWing.Embedment = 0.3m;         // 根入れ < 底版厚
            AssertInvalid(d);

            Hikan.Core.HikanParameters f = WithWings();
            f.UpstreamWing.Length = 0m;
            AssertInvalid(f);
        }

        // テスト13: 翼壁を付けない(たて壁厚 0)なら他の寸法は検査しない。
        [Xunit.Fact]
        public void NoWing_SkipsChecks()
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.UpstreamWing.StemThickness = 0m;
            p.UpstreamWing.EndWidth = 0.1m;
            p.UpstreamWing.Length = 0m;
            Xunit.Assert.Empty(Hikan.Core.HikanValidator.Validate(p));
            Xunit.Assert.Empty(Hikan.Core.HikanGeometry.GetWingParts(p));
        }
    }
}
