namespace Hikan.Core.Tests
{
    /// <summary>テスト用のパラメータ。</summary>
    internal static class HikanTestData
    {
        /// <summary>
        /// 函体だけの構成(胸壁・翼壁の厚 0 = 旧図面の「設置なし」、しゃ水壁 0 枚)。
        /// 既定値は胸壁・翼壁を設置するので、函体単体の式を確かめるテストはこれを使う。
        /// </summary>
        public static Hikan.Core.HikanParameters BarrelOnly()
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.UpstreamBreast.StemThickness = 0m;
            p.DownstreamBreast.StemThickness = 0m;
            p.UpstreamWing.StemThickness = 0m;
            p.DownstreamWing.StemThickness = 0m;
            return p;
        }
    }
}
