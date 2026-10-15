namespace Hikan.Core.Tests
{
    /// <summary>
    /// 堤防保護部(翼壁の端部から左右へ張り出す逆 T 字擁壁)の検証。
    /// 部材配置・閉じた式の体積・翼壁との重なりを手計算と突き合わせ、全体の体積は独立の柱積分で確かめる。
    /// </summary>
    public class HikanProtectionTests
    {
        /// <summary>
        /// 函体の底版厚 0.6(敷高 0.6)。川裏側翼壁は八の字(内空幅 2.0 → 端部 3.0、翼壁長 3.0、tanθ = 1/6)、
        /// 川表側翼壁は平行(翼壁長 4.0)。胸壁は既定位置(函体の中)。しゃ水壁なし。
        /// 川裏側保護部: X4 1.2 / L3 0.4 / L2 0.5 / L4 1.0、川表側: X4 1.5 / L3 0.5 / L2 0.6 / L4 1.2。
        /// たて壁高 Y3 は入力せず、上面を翼壁端部の天端 H1 にそろえる: 川裏側 2.0 − 0.6 = 1.4、川表側 2.5 − 0.6 = 1.9。
        /// </summary>
        private static Hikan.Core.HikanParameters WithProtection()
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.BottomSlabThickness = 0.6m;
            p.Gate.Exists = false;   // 門柱・操作台は HikanGateTests で確かめる
            p.UpstreamRetainingWall.Height = 0m;   // 川裏側の土留壁は HikanRetainingWallTests で確かめる

            p.UpstreamWing.StemThickness = 0.5m;
            p.UpstreamWing.Length = 3.0m;
            p.UpstreamWing.EndWidth = 3.0m;
            p.UpstreamWing.CrownStart = 3.5m;
            p.UpstreamWing.CrownEnd = 2.0m;
            p.UpstreamWing.FloorThickness = 0.6m;
            p.DownstreamWing.StemThickness = 0.6m;
            p.DownstreamWing.Length = 4.0m;
            p.DownstreamWing.EndWidth = 2.0m;
            p.DownstreamWing.CrownStart = 4.0m;
            p.DownstreamWing.CrownEnd = 2.5m;
            p.DownstreamWing.FloorThickness = 0.8m;

            Hikan.Core.HikanLeveeProtection up = p.UpstreamWing.Protection;
            up.Exists = true;
            up.Width = 1.2m;
            up.StemThickness = 0.4m;
            up.ToeLength = 0.5m;
            up.HeelLength = 1.0m;
            Hikan.Core.HikanLeveeProtection down = p.DownstreamWing.Protection;
            down.Exists = true;
            down.Width = 1.5m;
            down.StemThickness = 0.5m;
            down.ToeLength = 0.6m;
            down.HeelLength = 1.2m;
            return p;
        }

        private static System.Collections.Generic.Dictionary<string, Hikan.Core.HikanPart> ByName(Hikan.Core.HikanParameters p)
        {
            System.Collections.Generic.Dictionary<string, Hikan.Core.HikanPart> byName =
                new System.Collections.Generic.Dictionary<string, Hikan.Core.HikanPart>();
            foreach (Hikan.Core.HikanPart part in Hikan.Core.HikanGeometry.GetParts(p))
            {
                byName[part.Name] = part;
            }
            return byName;
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
        }

        private static void AssertClose(decimal expected, decimal actual, decimal tolerance, string label)
        {
            Xunit.Assert.True(System.Math.Abs(expected - actual) <= tolerance,
                label + ": 期待 " + expected + " / 実際 " + actual + " / 許容 " + tolerance);
        }

        [Xunit.Fact]
        public void WithProtection_IsValidInput()
        {
            Xunit.Assert.Empty(Hikan.Core.HikanValidator.ValidateInput(WithProtection()));
        }

        // テスト1: 配置。たて壁は外側の面が翼壁端部、U は端部の側壁内面(±端部水路幅/2)から外向きに X4。
        // 底版は上面 = 敷高 0.6、厚さ = 翼壁の底版厚。かかと版は函体側、つま先版は端部より外側で左右つながる。
        [Xunit.Fact]
        public void Parts_ArePlacedAtWingEnd()
        {
            System.Collections.Generic.Dictionary<string, Hikan.Core.HikanPart> b = ByName(WithProtection());

            // 川表側: 翼壁端部 S = 24、内面 U = 1.0、外端 U = 2.5、底版 V −0.2〜0.6
            AssertBox(b["川表側堤防保護部右たて壁"], 1.0m, 2.5m, 0.6m, 2.5m, 23.5m, 24.0m);
            AssertBox(b["川表側堤防保護部右底版"], 1.0m, 2.5m, -0.2m, 0.6m, 22.3m, 24.0m);
            AssertBox(b["川表側堤防保護部左たて壁"], -2.5m, -1.0m, 0.6m, 2.5m, 23.5m, 24.0m);
            AssertBox(b["川表側堤防保護部つま先版"], -2.5m, 2.5m, -0.2m, 0.6m, 24.0m, 24.6m);

            // 川裏側: 翼壁端部 S = −3、内面 U = 1.5、外端 U = 2.7、底版 V 0.0〜0.6、函体側は +S
            AssertBox(b["川裏側堤防保護部右たて壁"], 1.5m, 2.7m, 0.6m, 2.0m, -3.0m, -2.6m);
            AssertBox(b["川裏側堤防保護部右底版"], 1.5m, 2.7m, 0.0m, 0.6m, -3.0m, -1.6m);
            AssertBox(b["川裏側堤防保護部つま先版"], -2.7m, 2.7m, 0.0m, 0.6m, -3.5m, -3.0m);
        }

        // テスト2: 体積の閉じた式 = 部材の和 = 手計算。
        // 川表側: たて壁 2×1.5×1.9×0.5 = 2.85、底版 2×1.5×0.8×1.7 = 4.08、つま先版 (2.0 + 3.0)×0.8×0.6 = 2.4 → 9.33
        // 川裏側: たて壁 2×1.2×1.4×0.4 = 1.344、底版 2×1.2×0.6×1.4 = 2.016、つま先版 (3.0 + 2.4)×0.6×0.5 = 1.62 → 4.98
        [Xunit.Fact]
        public void Volume_MatchesPartsAndHandCalculation()
        {
            Hikan.Core.HikanParameters p = WithProtection();
            Xunit.Assert.Equal(9.33m, Hikan.Core.HikanGeometry.ProtectionVolume(p, p.DownstreamWing));
            Xunit.Assert.Equal(4.98m, Hikan.Core.HikanGeometry.ProtectionVolume(p, p.UpstreamWing));

            decimal up = 0m, down = 0m;
            foreach (Hikan.Core.HikanPart part in Hikan.Core.HikanGeometry.GetParts(p))
            {
                if (part.Name.StartsWith("川裏側堤防保護部", System.StringComparison.Ordinal)) { up += part.Volume; }
                if (part.Name.StartsWith("川表側堤防保護部", System.StringComparison.Ordinal)) { down += part.Volume; }
            }
            Xunit.Assert.Equal(4.98m, up);
            Xunit.Assert.Equal(9.33m, down);
        }

        // テスト3: 翼壁との重なり(手計算)。川表側(平行)は
        // たて壁 ∩ 側壁: U 1.0〜1.6(0.6)× V 0.6〜2.5(たて壁の上面 = H1 2.5。側壁の天端は 2.5 以上)× S 0.5 = 0.57 / 片側、
        // 底版 ∩ 翼壁底版: U 1.0〜1.6(0.6)× V −0.2〜0.6(0.8)× S 1.7 = 0.816 / 片側 → 2 × 1.386 = 2.772
        [Xunit.Fact]
        public void OverlapWithWing_MatchesHandCalculation()
        {
            Hikan.Core.HikanParameters p = WithProtection();
            AssertClose(2.772m, Hikan.Core.HikanGeometry.WingOverlapVolume(p, false), 0.000000001m, "川表側の重なり");

            // 翼壁の数量は重なりを差し引いた正味、保護部は総量。部材別の和が総計に一致する。
            Hikan.Core.HikanEstimate e = Hikan.Core.HikanEstimator.Calculate(p);
            AssertClose(Hikan.Core.HikanGeometry.WingWallVolume(p, p.DownstreamWing) - 2.772m,
                e.DownstreamWingVolume, 0.0005m, "川表側翼壁の正味");
            Xunit.Assert.Equal(9.330m, e.DownstreamProtectionVolume);
            Xunit.Assert.Equal(4.98m, e.UpstreamProtectionVolume);
            decimal parts = e.BarrelConcreteVolume + e.UpstreamBreastVolume + e.DownstreamBreastVolume
                + e.CutoffTotalVolume + e.UpstreamWingVolume + e.DownstreamWingVolume
                + e.UpstreamProtectionVolume + e.DownstreamProtectionVolume;
            AssertClose(parts, e.ConcreteVolume, 0.004m, "部材別の和 = 総計(丸め誤差のみ)");
        }

        // テスト4: 独立の柱積分で、合併体積が ModelVolume に一致し、測った重なりが解析的な重なりに一致すること。
        [Xunit.Fact]
        public void ColumnIntegral_MatchesModelVolume()
        {
            Hikan.Core.HikanParameters p = WithProtection();
            (decimal Union, decimal Overlap) c = HikanWingTests.ColumnIntegral(p, 0.05m);
            decimal model = Hikan.Core.HikanGeometry.ModelVolume(p);
            AssertClose(model, c.Union, model * 0.002m, "合併体積");
            AssertClose(Hikan.Core.HikanGeometry.WingOverlapVolume(p, null) + Hikan.Core.HikanGeometry.BoxOverlapVolume(p),
                c.Overlap, 0.01m, "重なり体積");
        }

        // テスト5: エクステント・床掘り範囲に保護部が入ること。
        [Xunit.Fact]
        public void Extents_IncludeProtection()
        {
            Hikan.Core.HikanParameters p = WithProtection();
            p.UpstreamBreast.Length = 1.0m;     // 胸壁の外端 1.4 + 1.0 = 2.4 を保護部の外端より内側にする
            p.DownstreamBreast.Length = 1.0m;
            (decimal MinX, decimal MinY, decimal MinZ, decimal MaxX, decimal MaxY, decimal MaxZ) x =
                Hikan.Core.HikanGeometry.ExpectedExtents(p);
            Xunit.Assert.Equal(-2.7m, x.MinX);   // 川裏側保護部の外端 1.5 + 1.2
            Xunit.Assert.Equal(2.7m, x.MaxX);
            Xunit.Assert.Equal(-3.5m, x.MinY);   // 川裏側つま先版
            Xunit.Assert.Equal(24.6m, x.MaxY);   // 川表側つま先版
            Xunit.Assert.Equal(24.6m, Hikan.Core.HikanGeometry.GlobalSMax(p));
        }

        // テスト6: 設置しない・翼壁が無い場合は部材を作らない。旧 XData(保護部のキーなし)は設置しないとして読む。
        [Xunit.Fact]
        public void NotInstalled_HasNoParts_AndLegacyReadsAsNone()
        {
            Hikan.Core.HikanParameters p = WithProtection();
            p.UpstreamWing.Protection.Exists = false;
            p.DownstreamWing.StemThickness = 0m;   // 旧図面の「翼壁なし」なら保護部も作らない
            foreach (Hikan.Core.HikanPart part in Hikan.Core.HikanGeometry.GetParts(p))
            {
                Xunit.Assert.DoesNotContain("堤防保護部", part.Name);
            }
            Xunit.Assert.Equal(0m, Hikan.Core.HikanGeometry.ProtectionVolume(p, p.UpstreamWing));

            System.Collections.Generic.Dictionary<string, string> d = WithProtection().ToDictionary();
            Xunit.Assert.Equal("1", d["upstream_wing_protection_exists"]);
            foreach (string key in new System.Collections.Generic.List<string>(d.Keys))
            {
                if (key.Contains("_protection_")) { d.Remove(key); }
            }
            Hikan.Core.HikanParameters q = Hikan.Core.HikanParameters.FromDictionary(d);
            Xunit.Assert.False(q.UpstreamWing.Protection.Exists);
            Xunit.Assert.False(q.DownstreamWing.Protection.Exists);
        }

        // テスト7: XData 往復で保護部の値が戻ること。
        [Xunit.Fact]
        public void Dictionary_RoundTrip()
        {
            Hikan.Core.HikanParameters p = WithProtection();
            Hikan.Core.HikanParameters q = Hikan.Core.HikanParameters.FromDictionary(p.ToDictionary());
            Xunit.Assert.Equal(p.ToDictionary(), q.ToDictionary());
            Xunit.Assert.True(q.DownstreamWing.Protection.Exists);
            Xunit.Assert.Equal(1.2m, q.DownstreamWing.Protection.HeelLength);
        }

        private static string FirstError(Hikan.Core.HikanParameters p)
        {
            System.Collections.Generic.List<string> errors = Hikan.Core.HikanValidator.ValidateInput(p);
            Xunit.Assert.NotEmpty(errors);
            return errors[0];
        }

        // テスト8: 寸法の検査。メッセージは「<端>堤防保護部の<項目>」で始まる(入力画面が該当欄を探すのに使う)。
        [Xunit.Fact]
        public void Validation_RejectsBadDimensions()
        {
            Hikan.Core.HikanParameters a = WithProtection();
            a.DownstreamWing.Protection.StemThickness = 0.45m;
            Xunit.Assert.StartsWith("川表側堤防保護部のたて壁厚", FirstError(a));

            Hikan.Core.HikanParameters b = WithProtection();
            b.DownstreamWing.Protection.Width = 0.6m;   // 側壁の幅 0.6 と同じ = 側壁の外へ出ない
            Xunit.Assert.StartsWith("川表側堤防保護部の張出し幅", FirstError(b));

            Hikan.Core.HikanParameters c = WithProtection();
            c.DownstreamWing.Protection.HeelLength = 3.6m;   // 0.5 + 3.6 > 翼壁長 4.0
            Xunit.Assert.StartsWith("川表側堤防保護部のたて壁厚 + かかと版長", FirstError(c));


            Hikan.Core.HikanParameters e = WithProtection();
            e.UpstreamWing.Protection.ToeLength = -0.1m;
            Xunit.Assert.StartsWith("川裏側堤防保護部のつま先版長", FirstError(e));
        }

        // テスト9: 八の字に開く翼壁では、保護部が函体側へ長いと側壁から離れる。
        // 側壁の U 方向の幅 0.5 × √(1 + 1/36) = 0.5069 を tanθ = 1/6 で割った 3.04 m までは許す。
        [Xunit.Theory]
        [Xunit.InlineData(2.6, true)]    // たて壁厚 + かかと版長 = 3.0
        [Xunit.InlineData(2.8, false)]   // 3.2
        public void FlaredWing_LimitsProtectionDepth(double heel, bool valid)
        {
            Hikan.Core.HikanParameters p = WithProtection();
            p.UpstreamWing.Length = 6.0m;
            p.UpstreamWing.EndWidth = 4.0m;   // tanθ = 2 / 12 = 1/6
            p.UpstreamWing.Protection.HeelLength = (decimal)heel;
            if (valid) { Xunit.Assert.Empty(Hikan.Core.HikanValidator.ValidateInput(p)); }
            else { Xunit.Assert.StartsWith("川裏側堤防保護部のたて壁厚 + かかと版長", FirstError(p)); }
        }

        // テスト10: 胸壁の底版と重なる配置は弾く(3 部材の重なりで体積の解析解が崩れるため)。
        [Xunit.Fact]
        public void Validation_RejectsOverlapWithBreast()
        {
            Hikan.Core.HikanParameters p = WithProtection();
            p.DownstreamBreast.Position = 19.8m;   // 底版は S 20.8 まで(厚 0.4 / 2 + つま先 0.8)
            p.DownstreamWing.Length = 2.0m;        // かかと版は S 22.0 − 1.7 = 20.3 から
            Xunit.Assert.Contains(Hikan.Core.HikanValidator.ValidateInput(p),
                m => m.StartsWith("川表側堤防保護部のかかと版が川表側胸壁の底版と重なります", System.StringComparison.Ordinal));
        }
    }
}
