namespace Hikan.Core
{
    /// <summary>
    /// 翼壁用の部材。S 一定の面で始端・終端を切った柱状体で、
    /// 横断方向 U の範囲 [UL, UR] と天端高 VTop が S に対して直線的に変わり、下面 VBottom は水平。
    ///   S ∈ [S0, S1]、U ∈ [UL(S), UR(S)]、V ∈ [VBottom, VTop(S)]
    /// 平面は台形(開いた翼壁なら平行四辺形)、側面は天端が傾いた台形になる。
    ///
    /// AutoCAD 層は「平面形を鉛直に押出したプリズム」と「側面形(台形)を横断方向に押出したプリズム」の
    /// 共通部分(BoolIntersect)として作る。どちらも 断面ポリライン → Region → Extrude で作れる。
    ///
    /// S 断面は矩形 [UL, UR] × [VBottom, VTop] で、幅も高さも S の 1 次式なので、
    /// 体積(2 次式)と 1 次モーメント(3 次式)の積分はシンプソン則で厳密に求まる。
    /// </summary>
    public sealed class HikanTaperedPart
    {
        public string Name { get; set; }
        public decimal S0 { get; set; }
        public decimal S1 { get; set; }
        public decimal UL0 { get; set; }
        public decimal UR0 { get; set; }
        public decimal UL1 { get; set; }
        public decimal UR1 { get; set; }
        public decimal VBottom { get; set; }
        public decimal VTop0 { get; set; }
        public decimal VTop1 { get; set; }

        private decimal Lerp(decimal a, decimal b, decimal t)
        {
            return a + (b - a) * t;
        }

        /// <summary>
        /// 体積と 1 次モーメント(U・V・S)。シンプソン則(t = 0, 1/2, 1 の重み 1:4:1)で、
        /// 3 次以下の多項式の積分として厳密。
        /// </summary>
        public (decimal Volume, decimal MomentU, decimal MomentV, decimal MomentS) Moments()
        {
            decimal d = S1 - S0;
            decimal volume = 0m, mu = 0m, mv = 0m, ms = 0m;
            decimal[] ts = new decimal[] { 0m, 0.5m, 1m };
            decimal[] weights = new decimal[] { 1m, 4m, 1m };
            for (int i = 0; i < 3; i++)
            {
                decimal t = ts[i];
                decimal ul = Lerp(UL0, UL1, t);
                decimal ur = Lerp(UR0, UR1, t);
                decimal top = Lerp(VTop0, VTop1, t);
                decimal s = Lerp(S0, S1, t);
                decimal area = (ur - ul) * (top - VBottom);
                volume += weights[i] * area;
                mu += weights[i] * area * (ul + ur) / 2m;
                mv += weights[i] * area * (VBottom + top) / 2m;
                ms += weights[i] * area * s;
            }
            decimal k = d / 6m;
            return (volume * k, mu * k, mv * k, ms * k);
        }

        public decimal Volume
        {
            get { return Moments().Volume; }
        }

        /// <summary>頂点 8 個(凸多面体なのでエクステントはこれらの min/max)。</summary>
        public (decimal U, decimal V, decimal S)[] Vertices()
        {
            return new (decimal U, decimal V, decimal S)[]
            {
                (UL0, VBottom, S0), (UR0, VBottom, S0), (UL0, VTop0, S0), (UR0, VTop0, S0),
                (UL1, VBottom, S1), (UR1, VBottom, S1), (UL1, VTop1, S1), (UR1, VTop1, S1)
            };
        }

        /// <summary>平面形(U, S)。反時計回り、始点 = S0 側の左隅。</summary>
        public (decimal U, decimal S)[] PlanPoints()
        {
            return new (decimal U, decimal S)[]
            {
                (UL0, S0), (UR0, S0), (UR1, S1), (UL1, S1)
            };
        }

        /// <summary>平面形の面積(U 方向の幅が S の 1 次式なので台形公式で厳密)。</summary>
        public decimal PlanArea
        {
            get { return ((UR0 - UL0) + (UR1 - UL1)) / 2m * (S1 - S0); }
        }

        /// <summary>天端が水平か(水平なら平面プリズムだけで作れ、側面プリズムとの共通部分は不要)。</summary>
        public bool FlatTop
        {
            get { return VTop0 == VTop1; }
        }

        /// <summary>天端高 VTop を S の 1 次式で S の任意位置に延長した値(側面プリズムの作図用)。</summary>
        public decimal TopAt(decimal s)
        {
            if (S1 == S0) { return VTop0; }
            return VTop0 + (VTop1 - VTop0) * (s - S0) / (S1 - S0);
        }
    }
}
