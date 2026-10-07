namespace Hikan.Core.Tests
{
    public class HikanParametersTests
    {
        // テスト1: XData 往復。負値・小数を含めて全キーが劣化なく戻ること。
        [Xunit.Fact]
        public void Dictionary_RoundTrip()
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.InnerWidth = 2.5m;
            p.InnerHeight = 1.8m;
            p.WallThickness = 0.5m;
            p.TopSlabThickness = 0.6m;
            p.BottomSlabThickness = 0.7m;
            p.BarrelLength = 33.456m;
            p.BottomSlope = 0.015m;
            p.SoilCover = 3.25m;
            p.BlockCount = 7;
            p.ExcavationMargin = 0.75m;
            p.ExcavationSlope = 1.2m;
            p.FoundationThickness = 0.15m;
            p.ColorIndex = 123;
            p.BaseX = -12.5m;
            p.BaseY = 0.125m;
            p.BaseZ = -3.75m;

            System.Collections.Generic.Dictionary<string, string> d = p.ToDictionary();
            Hikan.Core.HikanParameters q = Hikan.Core.HikanParameters.FromDictionary(d);
            System.Collections.Generic.Dictionary<string, string> e = q.ToDictionary();

            Xunit.Assert.Equal(d.Count, e.Count);
            foreach (System.Collections.Generic.KeyValuePair<string, string> kv in d)
            {
                Xunit.Assert.Equal(kv.Value, e[kv.Key]);
            }
        }

        [Xunit.Fact]
        public void FromDictionary_MissingKey_Throws()
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            System.Collections.Generic.Dictionary<string, string> d = p.ToDictionary();
            d.Remove("wall_thickness");
            Xunit.Assert.Throws<Hikan.Core.HikanValidationException>(
                () => Hikan.Core.HikanParameters.FromDictionary(d));
        }

        [Xunit.Fact]
        public void FromDictionary_SchemaVersionMismatch_Throws()
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            System.Collections.Generic.Dictionary<string, string> d = p.ToDictionary();
            d["schema_version"] = "99";
            Xunit.Assert.Throws<Hikan.Core.HikanValidationException>(
                () => Hikan.Core.HikanParameters.FromDictionary(d));
        }

        [Xunit.Fact]
        public void FromDictionary_NotANumber_Throws()
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            System.Collections.Generic.Dictionary<string, string> d = p.ToDictionary();
            d["inner_width"] = "ひろい";
            Xunit.Assert.Throws<Hikan.Core.HikanValidationException>(
                () => Hikan.Core.HikanParameters.FromDictionary(d));
        }

        [Xunit.Fact]
        public void FromDictionary_NotAnInteger_Throws()
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            System.Collections.Generic.Dictionary<string, string> d = p.ToDictionary();
            d["block_count"] = "3.5";
            Xunit.Assert.Throws<Hikan.Core.HikanValidationException>(
                () => Hikan.Core.HikanParameters.FromDictionary(d));
        }

        // テスト6: HikanNames のキー網羅。欠落すると HikanReport.Add が実行時 KeyNotFoundException になる。
        [Xunit.Fact]
        public void Names_CoverAllParametersAndReportItems()
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            foreach (System.Collections.Generic.KeyValuePair<string, string> kv in p.ToDictionary())
            {
                if (kv.Key == "schema_version") { continue; }
                Xunit.Assert.True(Hikan.Core.HikanNames.Japanese.ContainsKey(kv.Key), "日本語名が未定義: " + kv.Key);
            }

            Hikan.Core.HikanEstimate e = Hikan.Core.HikanEstimator.Calculate(p);
            System.Collections.Generic.List<Hikan.Core.HikanItem> items = Hikan.Core.HikanReport.Build(p, e);
            Xunit.Assert.NotEmpty(items);
            foreach (Hikan.Core.HikanItem item in items)
            {
                Xunit.Assert.False(string.IsNullOrWhiteSpace(item.JapaneseName), "日本語名が空: " + item.EnglishName);
                Xunit.Assert.False(string.IsNullOrWhiteSpace(item.Confidence), "信頼度が空: " + item.EnglishName);
            }
        }

        // テスト7: 敷高は底版下面ではなく底版厚ぶん上。基準点との混同を防ぐ。
        [Xunit.Fact]
        public void InvertLevel_IsBaseZPlusBottomSlab()
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.BaseZ = 5.25m;
            p.BottomSlabThickness = 0.6m;
            Xunit.Assert.Equal(5.85m, p.InvertLevel);
        }
    }
}
