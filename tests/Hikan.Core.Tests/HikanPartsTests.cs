namespace Hikan.Core.Tests
{
    /// <summary>
    /// 第2段階(胸壁・しゃ水壁)の部材分解と体積の検証。
    ///
    /// ModelVolume は包除原理(部材の和 − 重なり − 内空)で書いている。
    /// それが正しいかを、まったく別のアルゴリズム = S 方向のスラブ分解 + (U,V) セル被覆判定
    /// で独立に計算して突き合わせる。式の立て方を間違えていても、こちらは幾何から直接積分するので検出できる。
    /// </summary>
    public class HikanPartsTests
    {
        private static Hikan.Core.HikanParameters Full()
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.TopSlabThickness = 0.4m;
            p.BottomSlabThickness = 0.6m;
            // 軸位置は函体端にたて壁面を合わせた値(川裏側 = 厚/2、川表側 = L − 厚/2)。
            p.UpstreamBreast.StemThickness = 0.5m;
            p.UpstreamBreast.Position = 0.25m;
            p.UpstreamBreast.Length = 1.5m;
            p.UpstreamBreast.CrownHeight = 4.0m;
            p.UpstreamBreast.Embedment = 1.0m;
            p.UpstreamBreast.FootingThickness = 0.5m;
            p.UpstreamBreast.ToeLength = 0.8m;
            p.UpstreamBreast.HeelLength = 1.2m;
            p.DownstreamBreast.StemThickness = 0.6m;
            p.DownstreamBreast.Position = 19.7m;
            p.DownstreamBreast.Length = 2.0m;
            p.DownstreamBreast.CrownHeight = 4.5m;
            p.DownstreamBreast.Embedment = 1.2m;
            p.DownstreamBreast.FootingThickness = 0.6m;
            p.DownstreamBreast.ToeLength = 1.0m;
            p.DownstreamBreast.HeelLength = 1.5m;
            p.CutoffCount = 2;
            p.CutoffThickness = 0.5m;
            p.CutoffProjection = 0.7m;
            return p;
        }

        /// <summary>S 方向にスラブ分割し、各スラブで (U,V) をセル分割して被覆判定で面積を出す独立実装。</summary>
        private static decimal SlabVolume(Hikan.Core.HikanParameters p)
        {
            Hikan.Core.HikanPart[] parts = Hikan.Core.HikanGeometry.GetParts(p);

            System.Collections.Generic.List<decimal> sCuts = new System.Collections.Generic.List<decimal>();
            for (int i = 0; i < parts.Length; i++)
            {
                AddUnique(sCuts, parts[i].SMin);
                AddUnique(sCuts, parts[i].SMax);
            }
            sCuts.Sort();

            decimal total = 0m;
            for (int k = 0; k + 1 < sCuts.Count; k++)
            {
                decimal s0 = sCuts[k];
                decimal s1 = sCuts[k + 1];
                if (s1 <= s0) { continue; }
                decimal sMid = (s0 + s1) / 2m;

                System.Collections.Generic.List<decimal> uCuts = new System.Collections.Generic.List<decimal>();
                System.Collections.Generic.List<decimal> vCuts = new System.Collections.Generic.List<decimal>();
                for (int i = 0; i < parts.Length; i++)
                {
                    if (!Covers(parts[i], sMid)) { continue; }
                    AddUnique(uCuts, parts[i].UMin);
                    AddUnique(uCuts, parts[i].UMax);
                    AddUnique(vCuts, parts[i].VMin);
                    AddUnique(vCuts, parts[i].VMax);
                }
                uCuts.Sort();
                vCuts.Sort();

                decimal area = 0m;
                for (int a = 0; a + 1 < uCuts.Count; a++)
                {
                    for (int b = 0; b + 1 < vCuts.Count; b++)
                    {
                        decimal uMid = (uCuts[a] + uCuts[a + 1]) / 2m;
                        decimal vMid = (vCuts[b] + vCuts[b + 1]) / 2m;
                        bool solid = false;
                        bool hollow = false;
                        for (int i = 0; i < parts.Length; i++)
                        {
                            if (!Covers(parts[i], sMid)) { continue; }
                            if (uMid <= parts[i].UMin || uMid >= parts[i].UMax) { continue; }
                            if (vMid <= parts[i].VMin || vMid >= parts[i].VMax) { continue; }
                            if (parts[i].IsVoid) { hollow = true; } else { solid = true; }
                        }
                        if (solid && !hollow)
                        {
                            area += (uCuts[a + 1] - uCuts[a]) * (vCuts[b + 1] - vCuts[b]);
                        }
                    }
                }
                total += area * (s1 - s0);
            }
            return total;
        }

        private static bool Covers(Hikan.Core.HikanPart part, decimal s)
        {
            return s > part.SMin && s < part.SMax;
        }

        private static void AddUnique(System.Collections.Generic.List<decimal> list, decimal value)
        {
            if (!list.Contains(value)) { list.Add(value); }
        }

        // テスト1: 部材なし(第1段階と同一構成)で、独立計算と ModelVolume が一致すること。
        [Xunit.Fact]
        public void SlabVolume_MatchesModelVolume_BarrelOnly()
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            Xunit.Assert.Equal(Hikan.Core.HikanGeometry.ModelVolume(p), SlabVolume(p));
            Xunit.Assert.Equal(76.800m, Hikan.Core.HikanGeometry.ModelVolume(p));
        }

        // テスト2: 胸壁・しゃ水壁ありで、包除原理の式と独立計算が一致すること。
        [Xunit.Theory]
        [Xunit.InlineData(0, 0.0, 0.0)]
        [Xunit.InlineData(1, 0.5, 0.7)]
        [Xunit.InlineData(2, 0.5, 0.7)]
        [Xunit.InlineData(3, 0.4, 1.2)]
        [Xunit.InlineData(5, 0.4, 0.5)]
        public void SlabVolume_MatchesModelVolume_WithExtraParts(int cutoffCount, double thickness, double projection)
        {
            Hikan.Core.HikanParameters p = Full();
            p.CutoffCount = cutoffCount;
            if (cutoffCount > 0)
            {
                p.CutoffThickness = (decimal)thickness;
                p.CutoffProjection = (decimal)projection;
            }
            Xunit.Assert.Equal(Hikan.Core.HikanGeometry.ModelVolume(p), SlabVolume(p));
        }

        // テスト3: 胸壁のみ / しゃ水壁のみ でも一致すること(片方だけ有効な経路の確認)。
        [Xunit.Fact]
        public void SlabVolume_MatchesModelVolume_EachPartAlone()
        {
            Hikan.Core.HikanParameters breastOnly = Full();
            breastOnly.CutoffCount = 0;
            Xunit.Assert.Equal(Hikan.Core.HikanGeometry.ModelVolume(breastOnly), SlabVolume(breastOnly));

            Hikan.Core.HikanParameters cutoffOnly = Full();
            cutoffOnly.UpstreamBreast.StemThickness = 0m;
            cutoffOnly.DownstreamBreast.StemThickness = 0m;
            Xunit.Assert.Equal(Hikan.Core.HikanGeometry.ModelVolume(cutoffOnly), SlabVolume(cutoffOnly));
        }

        // テスト4: 部材の構成。加算部材が先、内空が最後。内空は函体のみを貫通する。
        [Xunit.Fact]
        public void GetParts_OrderAndVoidSpan()
        {
            Hikan.Core.HikanParameters p = Full();
            Hikan.Core.HikanPart[] parts = Hikan.Core.HikanGeometry.GetParts(p);

            Xunit.Assert.Equal("函体", parts[0].Name);
            Xunit.Assert.True(parts[parts.Length - 1].IsVoid, "最後の部材が内空であること");
            for (int i = 0; i < parts.Length - 1; i++)
            {
                Xunit.Assert.False(parts[i].IsVoid, "内空は 1 個だけで最後にあること: " + parts[i].Name);
            }
            // 函体 + 胸壁(たて壁 + 底版)× 左右 × 2 端 + しゃ水壁 2 枚 + 内空
            Xunit.Assert.Equal(12, parts.Length);

            // 内空は函体のみを貫通する(胸壁は開口を塞がないので延長しない)
            Hikan.Core.HikanPart hollow = parts[parts.Length - 1];
            Xunit.Assert.Equal(0m, hollow.SMin);
            Xunit.Assert.Equal(20m, hollow.SMax);
        }

        // テスト4b: 胸壁は左右で同一形状(中心線 U = 0 について鏡映)で、函体側面に接し、函体とは重ならないこと。
        [Xunit.Fact]
        public void BreastWalls_AreMirroredAndTouchBarrelSide()
        {
            Hikan.Core.HikanParameters p = Full();
            decimal half = p.OuterWidth / 2m;
            System.Collections.Generic.Dictionary<string, Hikan.Core.HikanPart> byName =
                new System.Collections.Generic.Dictionary<string, Hikan.Core.HikanPart>();
            foreach (Hikan.Core.HikanPart part in Hikan.Core.HikanGeometry.GetParts(p))
            {
                byName[part.Name] = part;
            }

            string[] bases = new string[] { "川裏側胸壁", "川表側胸壁" };
            string[] pieces = new string[] { "たて壁", "底版" };
            foreach (string b in bases)
            {
                foreach (string piece in pieces)
                {
                    Hikan.Core.HikanPart left = byName[b + "左" + piece];
                    Hikan.Core.HikanPart right = byName[b + "右" + piece];
                    Xunit.Assert.Equal(-right.UMax, left.UMin);
                    Xunit.Assert.Equal(-right.UMin, left.UMax);
                    Xunit.Assert.Equal(right.VMin, left.VMin);
                    Xunit.Assert.Equal(right.VMax, left.VMax);
                    Xunit.Assert.Equal(right.SMin, left.SMin);
                    Xunit.Assert.Equal(right.SMax, left.SMax);
                    // 函体側面(U = ±外形半幅)に接し、函体の内側には入らない
                    Xunit.Assert.Equal(half, right.UMin);
                    Xunit.Assert.Equal(-half, left.UMax);
                }
            }
        }

        // テスト5: しゃ水壁は等間隔で、互いに接触せず函体内に収まること。
        [Xunit.Theory]
        [Xunit.InlineData(1)]
        [Xunit.InlineData(2)]
        [Xunit.InlineData(5)]
        public void CutoffPositions_AreEvenlySpacedAndInsideBarrel(int count)
        {
            Hikan.Core.HikanParameters p = Full();
            p.CutoffCount = count;
            decimal half = p.CutoffThickness / 2m;

            decimal previousMax = 0m;
            for (int i = 1; i <= count; i++)
            {
                decimal s = Hikan.Core.HikanGeometry.CutoffPosition(p, i);
                Xunit.Assert.True(s - half > previousMax, "前のカラーと接触しないこと(i=" + i + ")");
                Xunit.Assert.True(s + half < p.BarrelLength, "函体内に収まること(i=" + i + ")");
                previousMax = s + half;
            }
            if (count == 2)
            {
                Xunit.Assert.Equal(5m, Hikan.Core.HikanGeometry.CutoffPosition(p, 1));
                Xunit.Assert.Equal(15m, Hikan.Core.HikanGeometry.CutoffPosition(p, 2));
            }
        }

        // テスト6: エクステントが胸壁としゃ水壁を含むこと。
        // 極値の隅が存在しない部材構成があるため、S と V の範囲から式で求めてはいけない。
        [Xunit.Fact]
        public void ExpectedExtents_CoversAllParts()
        {
            Hikan.Core.HikanParameters p = Full();
            (decimal MinX, decimal MinY, decimal MinZ, decimal MaxX, decimal MaxY, decimal MaxZ) x =
                Hikan.Core.HikanGeometry.ExpectedExtents(p);

            // 幅は下流胸壁(外形半幅 1.4 + 張出し 2.0)が最大
            Xunit.Assert.Equal(-3.4m, x.MinX);
            Xunit.Assert.Equal(3.4m, x.MaxX);
            // 上流つま先版が Y<0 に、下流つま先版が Y>L に張り出す
            Xunit.Assert.Equal(-0.8m, x.MinY);
            Xunit.Assert.Equal(21.0m, x.MaxY);
            // 下端は下流胸壁の根入れ 1.2 m、上端は下流胸壁の天端 4.5 m
            Xunit.Assert.Equal(-1.2m, x.MinZ);
            Xunit.Assert.Equal(4.5m, x.MaxZ);
        }

        // テスト7: 上下流で胸壁の寸法が違えば重心が中央からずれること。
        // 上下流の取り違えを検出できることの確認。
        [Xunit.Fact]
        public void ExpectedCentroid_ShiftsWhenEndsDiffer()
        {
            Hikan.Core.HikanParameters p = Full();
            (decimal X, decimal Y, decimal Z) g = Hikan.Core.HikanGeometry.ExpectedCentroid(p);
            Xunit.Assert.Equal(0m, g.X);
            // 下流胸壁の方が大きいので重心は函体中央 10.0 より下流側
            Xunit.Assert.True(g.Y > 10m, "重心 Y が下流寄りになること。実際: " + g.Y);

            // 川裏・川表を入れ替えて函体中央 S = 10 について鏡映した配置にすると、重心も鏡映の位置になること。
            // 軸位置は入れ替え後の厚さで函体端に合わせ直す(川裏側 = 厚/2、川表側 = L − 厚/2)。
            Hikan.Core.HikanParameters q = Full();
            Hikan.Core.HikanBreastWall up = q.UpstreamBreast;
            q.UpstreamBreast = q.DownstreamBreast;
            q.DownstreamBreast = up;
            q.UpstreamBreast.Position = q.UpstreamBreast.StemThickness / 2m;
            q.DownstreamBreast.Position = q.BarrelLength - q.DownstreamBreast.StemThickness / 2m;
            (decimal X, decimal Y, decimal Z) h = Hikan.Core.HikanGeometry.ExpectedCentroid(q);
            Xunit.Assert.True(h.Y < 10m, "入れ替えると上流寄りになること。実際: " + h.Y);
            Xunit.Assert.True(System.Math.Abs(h.Y - (20m - g.Y)) < 0.000000001m, "鏡映の位置になること");
            Xunit.Assert.Equal(g.Z, h.Z);
        }

        // テスト7b: 軸位置で胸壁が函体方向に動くこと。たて壁は軸 ± 厚/2、つま先版は近い方の函体端の側。
        [Xunit.Fact]
        public void BreastWall_IsPlacedByAxisPosition()
        {
            Hikan.Core.HikanParameters p = Full();
            p.CutoffCount = 0;
            p.UpstreamBreast.Position = 5.0m;      // 厚 0.5、つま先 0.8、かかと 1.2
            p.DownstreamBreast.Position = 14.0m;   // 厚 0.6、つま先 1.0、かかと 1.5

            System.Collections.Generic.Dictionary<string, Hikan.Core.HikanPart> byName =
                new System.Collections.Generic.Dictionary<string, Hikan.Core.HikanPart>();
            foreach (Hikan.Core.HikanPart part in Hikan.Core.HikanGeometry.GetParts(p))
            {
                byName[part.Name] = part;
            }

            Hikan.Core.HikanPart upStem = byName["川裏側胸壁右たて壁"];
            Xunit.Assert.Equal(4.75m, upStem.SMin);
            Xunit.Assert.Equal(5.25m, upStem.SMax);
            Hikan.Core.HikanPart upFoot = byName["川裏側胸壁右底版"];
            Xunit.Assert.Equal(3.95m, upFoot.SMin);   // つま先は川裏側へ 0.8
            Xunit.Assert.Equal(6.45m, upFoot.SMax);   // かかとは函体中央側へ 1.2

            Hikan.Core.HikanPart downStem = byName["川表側胸壁左たて壁"];
            Xunit.Assert.Equal(13.7m, downStem.SMin);
            Xunit.Assert.Equal(14.3m, downStem.SMax);
            Hikan.Core.HikanPart downFoot = byName["川表側胸壁左底版"];
            Xunit.Assert.Equal(12.2m, downFoot.SMin); // かかとは函体中央側へ 1.5
            Xunit.Assert.Equal(15.3m, downFoot.SMax); // つま先は川表側へ 1.0

            // 胸壁が函体の中に収まるので、配置の基準長は函体延長のまま
            Xunit.Assert.Equal(20m, Hikan.Core.HikanGeometry.GlobalSMax(p));
            Xunit.Assert.Empty(Hikan.Core.HikanValidator.Validate(p));
        }

        // テスト7c: 胸壁を函体中ほどに置いても、包除の体積式と独立検算が一致すること。
        [Xunit.Theory]
        [Xunit.InlineData(0.25, 19.7, 2)]
        [Xunit.InlineData(2.0, 17.5, 2)]
        [Xunit.InlineData(10.0, 17.5, 2)]   // 川裏側胸壁が 2 枚のしゃ水壁の間にある
        [Xunit.InlineData(3.0, 9.0, 0)]
        public void SlabVolume_MatchesModelVolume_AtAnyPosition(double up, double down, int cutoffs)
        {
            Hikan.Core.HikanParameters p = Full();
            p.UpstreamBreast.Position = (decimal)up;
            p.DownstreamBreast.Position = (decimal)down;
            p.CutoffCount = cutoffs;
            Xunit.Assert.Empty(Hikan.Core.HikanValidator.Validate(p));
            Xunit.Assert.Equal(Hikan.Core.HikanGeometry.ModelVolume(p), SlabVolume(p));
        }

        // テスト7d: 床掘りの平面長は実際の部材範囲から求める。
        // 胸壁を函体の中に置けば、つま先版は函体端から出ないので床掘り長は函体延長 + 余裕幅になる。
        [Xunit.Fact]
        public void ExcavationLength_FollowsActualPartRange()
        {
            Hikan.Core.HikanParameters atEnds = Full();
            atEnds.CutoffCount = 0;
            Hikan.Core.HikanEstimate e1 = Hikan.Core.HikanEstimator.Calculate(atEnds);
            // 川裏つま先 0.8 + 函体 20 + 川表つま先 1.0 + 余裕幅 2 × 0.5
            Xunit.Assert.Equal(22.8m, e1.ExcavationBottomLength);

            Hikan.Core.HikanParameters inside = Full();
            inside.CutoffCount = 0;
            inside.UpstreamBreast.Position = 5.0m;
            inside.DownstreamBreast.Position = 14.0m;
            Hikan.Core.HikanEstimate e2 = Hikan.Core.HikanEstimator.Calculate(inside);
            Xunit.Assert.Equal(21.0m, e2.ExcavationBottomLength);
        }

        // テスト8: エンベロープ体積 = コンクリート + 内空。埋戻の控除に使う。
        [Xunit.Fact]
        public void EnvelopeVolume_IsConcretePlusVoid()
        {
            Hikan.Core.HikanParameters p = Full();
            decimal expected = Hikan.Core.HikanGeometry.ModelVolume(p)
                + Hikan.Core.HikanGeometry.InnerSectionArea(p) * Hikan.Core.HikanGeometry.VoidLength(p);
            Xunit.Assert.Equal(expected, Hikan.Core.HikanGeometry.EnvelopeVolume(p));
            Xunit.Assert.Equal(20m, Hikan.Core.HikanGeometry.VoidLength(p));
        }

        // テスト9: 部材なしのとき GlobalSMax は函体延長に一致する(第1段階と同じ配置になること)。
        [Xunit.Fact]
        public void GlobalSMax_EqualsBarrelLength_WhenNoDownstreamBreast()
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            Xunit.Assert.Equal(p.BarrelLength, Hikan.Core.HikanGeometry.GlobalSMax(p));

            Hikan.Core.HikanParameters q = Full();
            Xunit.Assert.Equal(21.0m, Hikan.Core.HikanGeometry.GlobalSMax(q));
        }
    }
}
