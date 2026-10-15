namespace Hikan.Core
{
    /// <summary>
    /// 数量計算。丸めは「四捨五入」(MidpointRounding.AwayFromZero、decimal)で小数3位止め(= 1 mm 単位)。
    /// ジオメトリから確定的に出せる量(コンクリート・型枠)と、
    /// 施工条件の仮定を要する量(床掘り・埋戻)を区別する。信頼度ラベルは HikanReport で付与する。
    /// </summary>
    public static class HikanEstimator
    {
        private const int Digits = 3;

        private static decimal RoundHalfUp(decimal value, int digits)
        {
            return System.Math.Round(value, digits, System.MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// 胸壁 1 端ぶんの型枠面積 [m2]。
        /// たて壁(左右)は上下流 2 面 + 外側端面 + 天端。函体側面に接する内側端面は計上しない。
        /// 底版は左右つながった 1 枚の版で、左右の端面 + 前後の端面 + 函体の左右のつま先版・かかと版の上面。
        /// 前後の端面のうち函体延長の中にあるものは、函体の底版の中に入る部分(外形幅 × min(底版厚, 敷高))を除く。
        /// 地盤と接する下面、函体端より外の函体幅の上面(翼壁の底版などに覆われる)は計上しない。
        /// </summary>
        private static decimal BreastFormwork(HikanParameters p, HikanBreastWall w, bool upstream)
        {
            if (!w.Exists) { return 0m; }
            decimal top = HikanGeometry.BreastFootingTop(p);
            decimal stemHeight = w.CrownHeight - top;
            decimal stem = 2m * w.Length * stemHeight
                + w.StemThickness * stemHeight
                + w.Length * w.StemThickness;
            decimal width = p.OuterWidth + 2m * w.Length;
            (decimal Min, decimal Max) foot = HikanGeometry.BreastFootingRange(w, upstream);
            decimal hidden = p.OuterWidth * System.Math.Min(w.FootingThickness, top);
            decimal ends = 2m * width * w.FootingThickness;
            if (foot.Min > 0m && foot.Min < p.BarrelLength) { ends -= hidden; }
            if (foot.Max > 0m && foot.Max < p.BarrelLength) { ends -= hidden; }
            decimal footing = 2m * w.FootingLength * w.FootingThickness
                + ends
                + 2m * w.Length * (w.ToeLength + w.HeelLength);
            return 2m * stem + footing;
        }

        /// <summary>しゃ水壁の型枠面積 [m2]。外周面 + 前後の環状面。</summary>
        private static decimal CutoffFormwork(HikanParameters p)
        {
            if (p.CutoffCount <= 0) { return 0m; }
            decimal w = p.OuterWidth + 2m * p.CutoffProjection;
            decimal h = p.OuterHeight + 2m * p.CutoffProjection;
            decimal ring = w * h - p.OuterWidth * p.OuterHeight;
            return p.CutoffCount * (2m * (w + h) * p.CutoffThickness + 2m * ring);
        }

        /// <summary>
        /// 翼壁 1 端ぶん(U 字水路)の型枠面積 [m2]。
        /// 側壁は水路側の面 + 背面(底版の側面まで続く)+ 端部の切断面 + 天端(傾斜面)。
        /// 底版は翼壁端部の切断面のみ(上面は水路床で型枠不要、下面は地盤、側面は側壁の背面に含めた)。
        /// 函体端面に接する始端面は計上しない。
        /// </summary>
        private static decimal WingFormwork(HikanParameters p, HikanWingWall w)
        {
            if (!w.Exists) { return 0m; }
            decimal m = HikanGeometry.WingWidthFactor(p, w);
            decimal wallLength = w.Length * m;
            decimal floorTop = HikanGeometry.WingFloorTop(p);
            decimal averageCrown = (w.CrownStart + w.CrownEnd) / 2m;
            decimal inner = wallLength * (averageCrown - floorTop);
            decimal back = wallLength * (averageCrown - (floorTop - w.FloorThickness));
            decimal endFace = w.StemThickness * m * (w.CrownEnd - floorTop);
            decimal rise = (w.CrownEnd - w.CrownStart) / w.Length;
            decimal top = w.StemThickness * m * w.Length * HikanGeometry.Sqrt(1m + rise * rise);
            decimal floorEnd = (w.EndWidth + 2m * w.StemThickness * m) * w.FloorThickness;
            return 2m * (inner + back + endFace + top) + floorEnd;
        }

        /// <summary>
        /// 門柱・操作台の型枠面積 [m2]。門柱は L 字断面の周囲(= 外接矩形の周長 2·(X4 + X5 + X7 + X8))× 柱高さ × 2 本。
        /// 操作台は 4 側面 + 下面(門柱の天端と接する部分を除く)。管理橋の受け台(切欠き)は 3 つの立ち上がり面を加える。
        /// 操作台の上面と、門柱が頂版に接する下面は計上しない。ハンチは斜面を加え、覆われる面を除く。
        /// 土留壁は前後の面と天端を加え、覆われる門柱の内面(と、届けば操作台の下面)を除く。
        /// </summary>
        private static decimal GateFormwork(HikanParameters p)
        {
            HikanGateTower g = p.Gate;
            if (!g.Exists) { return 0m; }
            // 門柱は胸壁の底版の上面(敷高)から立つ。頂版上面より上は L 字の周長(= 外接矩形の周長)の全面。
            // 頂版上面より下は函体の外へ出た部分(全幅 X7 + X8 − 側壁厚 = e)だけが見え、函体の側面に接する面を除いた
            // 2e + (X4 + X5)。胸壁のたて壁と重なる部分の面は控除しない(概算)。
            decimal above = HikanGeometry.GateColumnTop(p) - p.OuterHeight;
            decimal below = p.OuterHeight - HikanGeometry.BreastFootingTop(p);
            decimal outside = g.ColumnBreadth - p.WallThickness;
            decimal columns = 2m * 2m * (g.ColumnLength + g.ColumnBreadth) * above;
            if (outside > 0m) { columns += 2m * (2m * outside + g.ColumnLength) * below; }
            decimal columnTop = g.TransverseWallThickness * g.ColumnBreadth + g.LongitudinalWallWidth * g.LongitudinalWallThickness;
            decimal deck = 2m * (g.DeckLength + g.DeckWidth) * g.DeckThickness
                + g.DeckLength * g.DeckWidth - 2m * columnTop;
            if (g.Bridge) { deck += (2m * g.SeatLength + g.SeatWidth) * g.SeatDepth; }
            // ハンチは斜面(√2 × H2 × X4)を加え、覆われる縦方向壁の内面と操作台の下面(各 H2 × X4)を除く。左右 2 か所。
            decimal haunch = 0m;
            if (g.HaunchHeight > 0m)
            {
                decimal h = g.HaunchHeight;
                haunch = 2m * (HikanGeometry.Sqrt(2m) * h - 2m * h) * g.LongitudinalWallWidth;
            }
            // 土留壁は前後の面 + 天端(操作台の下面に届けば天端は無く、覆われる操作台の下面を除く)。
            // 左右の端は門柱の横方向壁に接するので、覆われる横方向壁の内面(X5 × WH、左右)を門柱から除く。
            decimal wall = 0m;
            decimal wh = g.RetainingWallHeight;
            if (wh > 0m)
            {
                decimal b = p.InnerWidth;
                wall = 2m * b * wh - 2m * g.TransverseWallThickness * wh;
                wall += wh < above ? b * g.TransverseWallThickness : -b * g.TransverseWallThickness;
            }
            return columns + deck + haunch + wall;
        }

        /// <summary>
        /// 川裏側の土留壁の型枠面積 [m2] = 前後の面 2 × 外形幅 × 高さ + 天端 外形幅 × 厚さ。
        /// 左右の端は胸壁のたて壁の内側の端面(もともと計上しない面)に接する。胸壁の型枠(formwork_breast)に含める。
        /// </summary>
        private static decimal UpstreamRetainingWallFormwork(HikanParameters p)
        {
            HikanRetainingWall r = p.UpstreamRetainingWall;
            if (!r.Exists || !p.UpstreamBreast.Exists) { return 0m; }
            return 2m * p.OuterWidth * r.Height + p.OuterWidth * r.Thickness;
        }

        public static HikanEstimate Calculate(HikanParameters p)
        {
            HikanValidator.EnsureValid(p);

            HikanEstimate e = new HikanEstimate();

            decimal bOut = p.OuterWidth;
            decimal hOut = p.OuterHeight;
            decimal l = p.BarrelLength;

            e.OuterWidth = RoundHalfUp(bOut, Digits);
            e.OuterHeight = RoundHalfUp(hOut, Digits);
            e.InvertLevel = RoundHalfUp(p.InvertLevel, Digits);

            e.OuterSectionArea = RoundHalfUp(HikanGeometry.OuterSectionArea(p), Digits);
            e.InnerSectionArea = RoundHalfUp(HikanGeometry.InnerSectionArea(p), Digits);
            e.ConcreteSectionArea = RoundHalfUp(HikanGeometry.SectionArea(p), Digits);

            // 部位別コンクリート。和は断面積×延長に恒等的に一致する(HikanValidator で検査済み)。
            e.TopSlabVolume = RoundHalfUp(bOut * p.TopSlabThickness * l, Digits);
            e.BottomSlabVolume = RoundHalfUp(bOut * p.BottomSlabThickness * l, Digits);
            e.WallVolume = RoundHalfUp(2m * p.WallThickness * p.InnerHeight * l, Digits);
            e.BarrelConcreteVolume = RoundHalfUp(HikanGeometry.BarrelVolume(p), Digits);
            e.ConcreteVolume = RoundHalfUp(HikanGeometry.ModelVolume(p), Digits);

            // 胸壁は左右 2 基の合計(函体と重ならない)。しゃ水壁は函体と重なる部分を控除した正味。
            e.UpstreamBreastVolume = RoundHalfUp(HikanGeometry.BreastWallVolume(p, p.UpstreamBreast, true), Digits);
            e.DownstreamBreastVolume = RoundHalfUp(HikanGeometry.BreastWallVolume(p, p.DownstreamBreast, false), Digits);
            e.CutoffTotalVolume = RoundHalfUp(HikanGeometry.CutoffVolume(p), Digits);
            e.CutoffSpacing = p.CutoffCount > 0 ? RoundHalfUp(l / p.CutoffCount, Digits) : 0m;

            // 翼壁は左右 2 基の合計。胸壁などと重なる部分は胸壁側に数え、翼壁からは差し引く
            // (部材別の数量を足すと総計に一致する)。開き角は幅の差から決まる派生量(度)。
            e.UpstreamWingVolume = RoundHalfUp(HikanGeometry.WingWallNetVolume(p, true), Digits);
            e.DownstreamWingVolume = RoundHalfUp(HikanGeometry.WingWallNetVolume(p, false), Digits);
            // 堤防保護部は翼壁と重なる部分も含めた総量で数え、その分は翼壁の正味から差し引かれている。
            // 門柱・操作台は胸壁のたて壁と重なる(背面をそろえるため)。重なりは胸壁に含め、門柱・操作台から差し引く。
            // 門柱は胸壁の底版の上面から立つので函体の側壁とも重なる。重なりは函体・胸壁に含め、門柱から差し引く。
            System.Func<string, bool> earlier = m => m == "函体" || m == "川裏側胸壁" || m == "川表側胸壁";
            e.GateColumnVolume = RoundHalfUp(HikanGeometry.GateColumnVolume(p)
                - HikanGeometry.OverlapBetween(p, m => m == "門柱", earlier), Digits);
            e.GateDeckVolume = RoundHalfUp(HikanGeometry.GateDeckVolume(p)
                - HikanGeometry.OverlapBetween(p, m => m == "操作台", earlier), Digits);
            e.GateRetainingWallVolume = RoundHalfUp(HikanGeometry.GateRetainingWallVolume(p), Digits);
            e.UpstreamRetainingWallVolume = RoundHalfUp(HikanGeometry.UpstreamRetainingWallVolume(p), Digits);
            e.UpstreamProtectionVolume = RoundHalfUp(HikanGeometry.ProtectionVolume(p, p.UpstreamWing), Digits);
            e.DownstreamProtectionVolume = RoundHalfUp(HikanGeometry.ProtectionVolume(p, p.DownstreamWing), Digits);
            e.UpstreamWingFlareAngle = RoundHalfUp(HikanGeometry.WingFlareAngle(p, p.UpstreamWing), Digits);
            e.DownstreamWingFlareAngle = RoundHalfUp(HikanGeometry.WingFlareAngle(p, p.DownstreamWing), Digits);

            // 浸透路長: 函体外周に沿う経路。カラー 1 枚につき張出し量の 2 倍(下り + 上り)が加わる。
            // 具体の照査式(レーン則等)は参照文書に無いため信頼度は推定。
            e.SeepagePathLength = RoundHalfUp(
                l + 2m * p.CutoffProjection * p.CutoffCount, Digits);

            // 勾配の回転は体積を変えない。延長 L は斜距離なので水平投影長と落差を出す。
            decimal c = HikanGeometry.SlopeCosine(p);
            e.HorizontalProjection = RoundHalfUp(l * c, Digits);
            e.DropHeight = RoundHalfUp(l * p.BottomSlope * c, Digits);

            // 型枠。面積自体は幾何的に確定だが、どの面を計上するかは実務判断(HikanReport.Notes 参照)。
            e.FormworkInner = RoundHalfUp(2m * (p.InnerWidth + p.InnerHeight) * l, Digits);
            e.FormworkOuterSide = RoundHalfUp(2m * hOut * l, Digits);
            e.FormworkTop = RoundHalfUp(bOut * l, Digits);
            e.FormworkEnd = RoundHalfUp(2m * HikanGeometry.SectionArea(p), Digits);
            e.FormworkBreast = RoundHalfUp(
                BreastFormwork(p, p.UpstreamBreast, true) + BreastFormwork(p, p.DownstreamBreast, false)
                + UpstreamRetainingWallFormwork(p), Digits);
            // しゃ水壁は外周面 + 前後の環状面。
            e.FormworkCutoff = RoundHalfUp(CutoffFormwork(p), Digits);
            e.FormworkWing = RoundHalfUp(
                WingFormwork(p, p.UpstreamWing) + WingFormwork(p, p.DownstreamWing), Digits);
            e.FormworkGate = RoundHalfUp(GateFormwork(p), Digits);

            // ブロック割は函体のみに適用する(胸壁・しゃ水壁は別部材)。
            e.BlockLength = RoundHalfUp(l / p.BlockCount, Digits);
            e.ConcretePerBlock = RoundHalfUp(HikanGeometry.BarrelVolume(p) / p.BlockCount, Digits);

            // 床掘り: 四方に法勾配 1:n を付けた角錐台。∫[0,d] (Wb+2nz)(Lb+2nz) dz の厳密解。
            // 平面と深さは全部材の実際の外接範囲から求める。胸壁の位置は自由なので、
            // つま先版が函体端より外に出るとは限らず、決め打ちの式では過大・過小になる。
            (decimal HalfWidth, decimal VMin, decimal SMin, decimal SMax) range = HikanGeometry.FootprintRange(p);
            decimal planWidth = 2m * range.HalfWidth;
            decimal planLength = range.SMax - range.SMin;
            decimal below = -range.VMin;

            decimal wb = planWidth + 2m * p.ExcavationMargin;
            decimal lb = planLength + 2m * p.ExcavationMargin;
            decimal d = p.SoilCover + hOut + below + p.FoundationThickness;
            decimal n = p.ExcavationSlope;
            decimal exc = wb * lb * d
                + n * d * d * (wb + lb)
                + 4m * n * n * d * d * d / 3m;

            e.ExcavationBottomWidth = RoundHalfUp(wb, Digits);
            e.ExcavationBottomLength = RoundHalfUp(lb, Digits);
            e.ExcavationDepth = RoundHalfUp(d, Digits);
            e.ExcavationVolume = RoundHalfUp(exc, Digits);

            decimal foundation = wb * lb * p.FoundationThickness;
            // 地下占有体積は外形エンベロープ(内空を埋め戻した状態)。
            // 内空は埋戻さないのでコンクリート体積では控除できない。
            // 門柱・操作台・土留壁は現地盤(頂版上面 + 土かぶり)より下の部分だけを数える(上に出た部分を引く)。
            decimal ground = hOut + p.SoilCover;
            decimal occupied = HikanGeometry.EnvelopeVolume(p) - HikanGeometry.SuperstructureVolumeAbove(p, ground);
            e.FoundationVolume = RoundHalfUp(foundation, Digits);
            e.OccupiedVolume = RoundHalfUp(occupied, Digits);
            e.BackfillVolume = RoundHalfUp(exc - occupied - foundation, Digits);
            e.SurplusVolume = RoundHalfUp(occupied + foundation, Digits);

            return e;
        }
    }
}
