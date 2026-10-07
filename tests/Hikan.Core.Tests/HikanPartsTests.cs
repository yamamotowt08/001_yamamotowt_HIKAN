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
            p.UpstreamBreastThickness = 0.5m;
            p.UpstreamBreastWidth = 4.0m;
            p.UpstreamBreastHeight = 3.5m;
            p.DownstreamBreastThickness = 0.6m;
            p.DownstreamBreastWidth = 5.0m;
            p.DownstreamBreastHeight = 4.5m;
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
            cutoffOnly.UpstreamBreastThickness = 0m;
            cutoffOnly.DownstreamBreastThickness = 0m;
            Xunit.Assert.Equal(Hikan.Core.HikanGeometry.ModelVolume(cutoffOnly), SlabVolume(cutoffOnly));
        }

        // テスト4: 部材の構成。加算部材が先、内空が最後。内空は上下流の胸壁を貫通する。
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
            // 函体 + 上流胸壁 + 下流胸壁 + しゃ水壁 2 枚 + 内空
            Xunit.Assert.Equal(6, parts.Length);

            Hikan.Core.HikanPart hollow = parts[parts.Length - 1];
            Xunit.Assert.Equal(-0.5m, hollow.SMin);
            Xunit.Assert.Equal(20.6m, hollow.SMax);
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

            // 幅は下流胸壁 5.0 m が最大
            Xunit.Assert.Equal(-2.5m, x.MinX);
            Xunit.Assert.Equal(2.5m, x.MaxX);
            // 上流胸壁が Y<0 に、下流胸壁が Y>L に張り出す
            Xunit.Assert.Equal(-0.5m, x.MinY);
            Xunit.Assert.Equal(20.6m, x.MaxY);
            // しゃ水壁が下方に 0.7 m 張り出し、上端は下流胸壁の 4.5 m
            Xunit.Assert.Equal(-0.7m, x.MinZ);
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

            // 上下流を入れ替えたら鏡像の位置になること
            Hikan.Core.HikanParameters q = Full();
            q.UpstreamBreastThickness = p.DownstreamBreastThickness;
            q.UpstreamBreastWidth = p.DownstreamBreastWidth;
            q.UpstreamBreastHeight = p.DownstreamBreastHeight;
            q.DownstreamBreastThickness = p.UpstreamBreastThickness;
            q.DownstreamBreastWidth = p.UpstreamBreastWidth;
            q.DownstreamBreastHeight = p.UpstreamBreastHeight;
            (decimal X, decimal Y, decimal Z) h = Hikan.Core.HikanGeometry.ExpectedCentroid(q);
            Xunit.Assert.True(h.Y < 10m, "入れ替えると上流寄りになること。実際: " + h.Y);
            Xunit.Assert.Equal(g.Z, h.Z);
        }

        // テスト8: エンベロープ体積 = コンクリート + 内空。埋戻の控除に使う。
        [Xunit.Fact]
        public void EnvelopeVolume_IsConcretePlusVoid()
        {
            Hikan.Core.HikanParameters p = Full();
            decimal expected = Hikan.Core.HikanGeometry.ModelVolume(p)
                + Hikan.Core.HikanGeometry.InnerSectionArea(p) * Hikan.Core.HikanGeometry.VoidLength(p);
            Xunit.Assert.Equal(expected, Hikan.Core.HikanGeometry.EnvelopeVolume(p));
            Xunit.Assert.Equal(21.1m, Hikan.Core.HikanGeometry.VoidLength(p));
        }

        // テスト9: 部材なしのとき GlobalSMax は函体延長に一致する(第1段階と同じ配置になること)。
        [Xunit.Fact]
        public void GlobalSMax_EqualsBarrelLength_WhenNoDownstreamBreast()
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            Xunit.Assert.Equal(p.BarrelLength, Hikan.Core.HikanGeometry.GlobalSMax(p));

            Hikan.Core.HikanParameters q = Full();
            Xunit.Assert.Equal(20.6m, Hikan.Core.HikanGeometry.GlobalSMax(q));
        }
    }
}
