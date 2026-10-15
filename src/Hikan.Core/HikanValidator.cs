namespace Hikan.Core
{
    /// <summary>
    /// パラメータ整合性チェック。不一致は HikanValidationException でエラー停止(再生成しない)。
    /// エラー停止に使うのは「正値条件」と「基準由来の条件」のみ。実務上の目安範囲は強制しない。
    /// 基準由来の条件は『土木構造物設計マニュアル(案)樋門編 H13』の
    /// 最小部材厚 40cm / 10cm ピッチ規格化 / 適用範囲(内空断面 3.0m 程度以下・土かぶり 10m 程度以下)。
    /// </summary>
    public static class HikanValidator
    {
        /// <summary>許容誤差 1 mm = 0.001 m。</summary>
        private const decimal Tolerance = 0.001m;

        /// <summary>基準: 最小部材厚 [m]</summary>
        private const decimal MinMemberThickness = 0.40m;
        /// <summary>基準: 部材厚の規格化ピッチ [m]</summary>
        private const decimal ThicknessPitch = 0.10m;
        /// <summary>基準: 適用範囲の内空断面寸法上限 [m]</summary>
        private const decimal MaxInnerDimension = 3.0m;
        /// <summary>基準: 適用範囲の土かぶり上限 [m]</summary>
        private const decimal MaxSoilCover = 10.0m;

        public static System.Collections.Generic.List<string> Validate(HikanParameters p)
        {
            System.Collections.Generic.List<string> errors = new System.Collections.Generic.List<string>();

            if (p.InnerWidth <= 0m) { errors.Add("内空幅 B は正の値にしてください。"); }
            if (p.InnerHeight <= 0m) { errors.Add("内空高 H は正の値にしてください。"); }
            if (p.BarrelLength <= 0m) { errors.Add("函体延長 L は正の値にしてください。"); }
            if (p.SoilCover < 0m) { errors.Add("土かぶりは 0 以上にしてください。"); }
            if (p.BottomSlope < 0m) { errors.Add("底版勾配 i は 0 以上(下流下がり)にしてください。"); }
            if (p.BlockCount < 1) { errors.Add("ブロック数は 1 以上にしてください。"); }
            if (p.ExcavationMargin < 0m) { errors.Add("床掘り余裕幅は 0 以上にしてください。"); }
            if (p.ExcavationSlope < 0m) { errors.Add("床掘り法勾配 n は 0 以上にしてください。"); }
            if (p.FoundationThickness < 0m) { errors.Add("基礎材厚は 0 以上にしてください。"); }
            if (p.ColorIndex < 0 || p.ColorIndex > 256) { errors.Add("色番号(ACI)は 0〜256 にしてください。"); }

            CheckThickness(errors, p.WallThickness, "側壁厚");
            CheckThickness(errors, p.TopSlabThickness, "頂版厚");
            CheckThickness(errors, p.BottomSlabThickness, "底版厚");

            CheckBreastWall(errors, p, p.UpstreamBreast, "川裏側胸壁");
            CheckBreastWall(errors, p, p.DownstreamBreast, "川表側胸壁");
            CheckBreastClearance(errors, p);
            CheckCutoff(errors, p);
            CheckWingWall(errors, p, p.UpstreamWing, "川裏側翼壁");
            CheckWingWall(errors, p, p.DownstreamWing, "川表側翼壁");
            CheckProtection(errors, p, p.UpstreamWing, true, "川裏側堤防保護部");
            CheckProtection(errors, p, p.DownstreamWing, false, "川表側堤防保護部");
            CheckGate(errors, p);

            if (p.InnerWidth > MaxInnerDimension + Tolerance)
            {
                errors.Add("内空幅 B が適用範囲(" + MaxInnerDimension + " m 程度以下)を超えています。");
            }
            if (p.InnerHeight > MaxInnerDimension + Tolerance)
            {
                errors.Add("内空高 H が適用範囲(" + MaxInnerDimension + " m 程度以下)を超えています。");
            }
            if (p.SoilCover > MaxSoilCover + Tolerance)
            {
                errors.Add("土かぶりが適用範囲(" + MaxSoilCover + " m 程度以下)を超えています。");
            }

            // 整合性チェック: 函体の部位別体積の和 == 函体断面積 × 延長。断面の分解と全体が食い違う入力を弾く。
            // 比較相手は BarrelVolume(函体のみ)であって ModelVolume(胸壁・しゃ水壁を含む総計)ではない。
            if (p.InnerWidth > 0m && p.InnerHeight > 0m && p.BarrelLength > 0m)
            {
                decimal parts = p.OuterWidth * p.TopSlabThickness * p.BarrelLength
                    + p.OuterWidth * p.BottomSlabThickness * p.BarrelLength
                    + 2m * p.WallThickness * p.InnerHeight * p.BarrelLength;
                decimal whole = HikanGeometry.BarrelVolume(p);
                if (System.Math.Abs(parts - whole) > Tolerance)
                {
                    errors.Add("函体の部位別体積の和 " + parts + " m3 が断面積×延長 " + whole + " m3 と一致しません。");
                }
            }

            return errors;
        }

        /// <summary>
        /// 胸壁(左右それぞれの逆 T 字擁壁)。たて壁厚 0 は旧図面の「設置なし」なので寸法を検査しない
        /// (新規入力では ValidateInput が 0 を弾く)。
        /// 底版上面は函体の敷高にそろえるので、たて壁は敷高から立ち上がる。天端はそれより高いこと。
        /// 底版は左右つながって函体の下を通る(函体の底版との重なりは体積の解析解で 1 回だけ差し引く)。
        /// </summary>
        private static void CheckBreastWall(
            System.Collections.Generic.List<string> errors,
            HikanParameters p,
            HikanBreastWall w,
            string name)
        {
            if (w.StemThickness == 0m) { return; }

            CheckThickness(errors, w.StemThickness, name + "のたて壁厚");
            CheckThickness(errors, w.FootingThickness, name + "の底版厚");

            if (w.Length <= 0m)
            {
                errors.Add(name + "の張出し長は正の値にしてください。");
            }
            decimal footingTop = HikanGeometry.BreastFootingTop(p);
            if (w.CrownHeight <= footingTop)
            {
                errors.Add(name + "の天端高は底版上面(敷高 " + footingTop + " m)より高くしてください。");
            }
            if (w.ToeLength < 0m) { errors.Add(name + "のつま先版長は 0 以上にしてください。"); }
            if (w.HeelLength < 0m) { errors.Add(name + "のかかと版長は 0 以上にしてください。"); }
            // たて壁が函体側面に全厚で接するには、たて壁が函体延長の中に収まっている必要がある。
            if (w.StemStart < -Tolerance || w.StemEnd > p.BarrelLength + Tolerance)
            {
                errors.Add(name + "のたて壁(軸位置 " + w.Position + " m ± 厚/2 = " + w.StemStart + "〜" + w.StemEnd
                    + " m)が函体延長 0〜" + p.BarrelLength + " m からはみ出しています。");
            }
        }

        /// <summary>
        /// 川裏側・川表側の胸壁どうしが干渉せず、川裏側が川表側より川裏にあること。
        /// 底版どうしが重なる(接する場合も含む)と体積の解析解が崩れ、ブーリアンも退化する。
        /// </summary>
        private static void CheckBreastClearance(System.Collections.Generic.List<string> errors, HikanParameters p)
        {
            if (!p.UpstreamBreast.Exists || !p.DownstreamBreast.Exists) { return; }
            (decimal Min, decimal Max) up = HikanGeometry.BreastFootingRange(p.UpstreamBreast, true);
            (decimal Min, decimal Max) down = HikanGeometry.BreastFootingRange(p.DownstreamBreast, false);
            if (up.Max >= down.Min)
            {
                errors.Add("川裏側胸壁(" + up.Min + "〜" + up.Max + " m)と川表側胸壁(" + down.Min + "〜" + down.Max
                    + " m)が干渉するか順序が逆です。軸位置・たて壁厚・かかと版長を見直してください。");
            }
        }

        /// <summary>
        /// しゃ水壁。枚数 0 は「設置しない」なので寸法を検査しない。
        /// 厚 × 枚数 &lt; 函体延長 は、隣り合うカラー同士が接触せず、かつ両端が函体内に収まる条件
        /// (等間隔配置なので間隔 = 延長 / 枚数)。接触するとブーリアンが同一平面で退化する。
        /// </summary>
        private static void CheckCutoff(System.Collections.Generic.List<string> errors, HikanParameters p)
        {
            if (p.CutoffCount < 0)
            {
                errors.Add("しゃ水壁の枚数は 0 以上にしてください(0 で設置しない)。");
                return;
            }
            if (p.CutoffCount == 0) { return; }

            CheckThickness(errors, p.CutoffThickness, "しゃ水壁厚");
            if (p.CutoffProjection <= 0m)
            {
                errors.Add("しゃ水壁の張出し量は正の値にしてください。");
            }
            if (p.BarrelLength > 0m && p.CutoffThickness * p.CutoffCount >= p.BarrelLength)
            {
                errors.Add("しゃ水壁厚 × 枚数 = " + (p.CutoffThickness * p.CutoffCount)
                    + " m が函体延長 " + p.BarrelLength + " m 以上です。カラーが互いに接触するか函体からはみ出します。");
                return;
            }

            // 胸壁とカラーは X・Z で重なるので、Y(S)で離れていないと体積の解析解が崩れる。
            // 胸壁の位置は自由なので、全カラーとの区間の重なりを個別に調べる(接する場合も不可)。
            CheckBreastAgainstCutoffs(errors, p, p.UpstreamBreast, true, "川裏側胸壁");
            CheckBreastAgainstCutoffs(errors, p, p.DownstreamBreast, false, "川表側胸壁");
        }

        /// <summary>
        /// 翼壁(U 字の 3 面張り水路)。側壁厚 0 は旧図面の「設置なし」なので寸法を検査しない
        /// (新規入力では ValidateInput が 0 を弾く)。
        /// 側壁厚・底版厚は H13 マニュアル Ⅴ章(翼壁)の最小部材厚 0.40 m・0.10 m ピッチの対象。
        /// 胸壁などとの重なりは許す(解析解では HikanGeometry.WingOverlapVolume で差し引く)。
        /// </summary>
        private static void CheckWingWall(
            System.Collections.Generic.List<string> errors,
            HikanParameters p,
            HikanWingWall w,
            string name)
        {
            if (w.StemThickness == 0m) { return; }

            CheckThickness(errors, w.StemThickness, name + "の側壁厚");
            CheckThickness(errors, w.FloorThickness, name + "の底版厚");

            if (w.Length <= 0m)
            {
                errors.Add(name + "の翼壁長は正の値にしてください。");
                return;
            }
            if (w.EndWidth < p.InnerWidth - Tolerance)
            {
                errors.Add(name + "の端部水路幅 " + w.EndWidth + " m は内空幅 " + p.InnerWidth
                    + " m 以上にしてください(内空幅と同じで平行、広げると八の字に開く)。");
            }
            // 側壁は底版上面(= 函体の敷高)から立ち上がるので、天端はそれより高くなければならない。
            decimal floorTop = HikanGeometry.WingFloorTop(p);
            if (w.CrownStart <= floorTop)
            {
                errors.Add(name + "の函体端での天端高は底版上面(敷高 " + floorTop
                    + " m)より高くしてください(側壁が函体端面に接しません)。");
            }
            if (w.CrownEnd <= floorTop)
            {
                errors.Add(name + "の端部での天端高は底版上面(敷高 " + floorTop + " m)より高くしてください。");
            }

            // 函体端近くの胸壁の底版が函体端より外へ出て翼壁と重なるのは、実務で普通の配置なので許す。
            // 重なりは BoolUnite で一体化し、解析解では HikanGeometry.WingOverlapVolume で 1 回差し引く。
        }

        /// <summary>
        /// 堤防保護部(翼壁の端部から左右へ張り出す逆 T 字擁壁)。翼壁が無い・保護部を設置しない場合は検査しない。
        /// - たて壁厚は最小部材厚 0.40 m・0.10 m ピッチ。たて壁高は正、つま先版長・かかと版長は 0 以上。
        /// - 張出し幅は側壁の U 方向の幅(側壁厚 / cosθ)より大きいこと(側壁の外へ出ないと保護部にならない)。
        /// - たて壁厚 + かかと版長 ≤ 翼壁長(保護部が函体端を越えて函体に食い込まない)。
        /// - (たて壁厚 + かかと版長) × tanθ ≤ 側壁の U 方向の幅。八の字に開くと函体側ほど側壁が内へ寄るので、
        ///   これを超えると保護部(U は端部の側壁内面から外向き)が側壁から離れて浮く。
        /// - 胸壁の底版と S 方向で重ならないこと(3 部材が重なると体積の解析解が崩れる)。
        /// </summary>
        private static void CheckProtection(
            System.Collections.Generic.List<string> errors,
            HikanParameters p,
            HikanWingWall w,
            bool upstream,
            string name)
        {
            if (!w.Exists || !w.Protection.Exists || w.Length <= 0m) { return; }
            HikanLeveeProtection g = w.Protection;

            CheckThickness(errors, g.StemThickness, name + "のたて壁厚");
            if (g.StemHeight <= 0m) { errors.Add(name + "のたて壁高は正の値にしてください。"); }
            if (g.ToeLength < 0m) { errors.Add(name + "のつま先版長は 0 以上にしてください。"); }
            if (g.HeelLength < 0m) { errors.Add(name + "のかかと版長は 0 以上にしてください。"); }

            decimal wallWidth = w.StemThickness * HikanGeometry.WingWidthFactor(p, w);
            if (g.Width <= wallWidth)
            {
                errors.Add(name + "の張出し幅 " + g.Width + " m は、翼壁の側壁の幅 " + System.Math.Round(wallWidth, 3)
                    + " m(側壁内面から外面まで)より大きくしてください。");
            }
            decimal depth = g.StemThickness + g.HeelLength;
            if (depth > w.Length + Tolerance)
            {
                errors.Add(name + "のたて壁厚 + かかと版長 = " + depth + " m が翼壁長 " + w.Length
                    + " m を超えています(函体に食い込みます)。");
            }
            else if (depth * HikanGeometry.WingSlope(p, w) > wallWidth + Tolerance)
            {
                errors.Add(name + "のたて壁厚 + かかと版長 = " + depth + " m が長すぎます。八の字に開いた側壁から"
                    + "函体側で離れます(" + System.Math.Round(wallWidth / HikanGeometry.WingSlope(p, w), 3) + " m 以下)。");
            }

            HikanBreastWall b = upstream ? p.UpstreamBreast : p.DownstreamBreast;
            if (b.Exists)
            {
                (decimal Min, decimal Max) foot = HikanGeometry.BreastFootingRange(b, upstream);
                decimal reach = upstream ? -w.Length + depth : p.BarrelLength + w.Length - depth;
                bool overlap = upstream ? foot.Min < reach : foot.Max > reach;
                if (overlap)
                {
                    errors.Add(name + "のかかと版が" + (upstream ? "川裏側" : "川表側") + "胸壁の底版と重なります。"
                        + "翼壁長を延ばすか、かかと版長・胸壁のつま先版長を短くしてください。");
                }
            }
        }

        /// <summary>
        /// 門柱・操作台。設置しない場合は検査しない。他の部材と重ならないことをここで保証する
        /// (体積の解析解は「門柱・操作台は他と重ならない」前提で単純和にしている)。
        /// - 横方向壁厚 X5・縦方向壁厚 X7・操作台高さ Y1 は最小部材厚 0.40 m・0.10 m ピッチ。柱高さは正、張出し・壁幅は 0 以上。
        /// - 整合性: X4 + X5 + 2·X3 = X1、B + 2·(X7 + X8) + 2·X6 = X2(許容 1 mm)。
        /// - 管理橋の受け台(切欠き)は操作台の中に収まる(W1 &lt; X1、W2 &lt; X2、H1 &lt; Y1)。
        /// - 門柱は函体延長の中に収まる。門柱・操作台の S 範囲は、しゃ水壁・胸壁のたて壁の S 範囲と重ならない(接するのは可)。
        /// - 操作台が函体端より外へ張り出すなら、その側の翼壁・堤防保護部の天端より高い所にある。
        /// </summary>
        private static void CheckGate(System.Collections.Generic.List<string> errors, HikanParameters p)
        {
            HikanGateTower g = p.Gate;
            if (!g.Exists) { return; }
            const string name = "門柱・操作台";

            CheckThickness(errors, g.TransverseWallThickness, name + "の横方向壁厚 X5");
            CheckThickness(errors, g.LongitudinalWallThickness, name + "の縦方向壁厚 X7");
            CheckThickness(errors, g.DeckThickness, name + "の操作台高さ Y1");
            if (g.LongitudinalWallWidth < 0m) { errors.Add(name + "の縦方向壁幅 X4 は 0 以上にしてください。"); }
            if (g.TransverseWallWidth < 0m) { errors.Add(name + "の横方向壁幅 X8 は 0 以上にしてください。"); }
            if (g.ColumnHeight <= 0m) { errors.Add(name + "の柱高さ Y2 は正の値にしてください。"); }
            if (g.RetainingWallHeight < 0m) { errors.Add(name + "の土留壁高さ WH は 0 以上にしてください(0 で土留壁なし)。"); }
            else if (g.RetainingWallHeight > g.ColumnHeight + Tolerance)
            {
                errors.Add(name + "の土留壁高さ WH は柱高さ Y2 以下にしてください(操作台に食い込みます)。");
            }
            if (g.HaunchHeight < 0m) { errors.Add(name + "のハンチ高 H2 は 0 以上にしてください。"); }
            else if (g.HaunchHeight >= p.InnerWidth / 2m + g.TransverseWallWidth)
            {
                errors.Add(name + "のハンチ高 H2 は 内空幅の半分 + X8(" + (p.InnerWidth / 2m + g.TransverseWallWidth)
                    + " m)より小さくしてください(左右のハンチが重なります)。");
            }
            else if (g.HaunchHeight > g.ColumnHeight)
            {
                errors.Add(name + "のハンチ高 H2 は柱高さ Y2 以下にしてください。");
            }
            if (g.DeckAxialOverhang < 0m) { errors.Add(name + "の操作台縦方向張出長 X3 は 0 以上にしてください。"); }
            if (g.DeckSideOverhang < 0m) { errors.Add(name + "の操作台横方向張出長 X6 は 0 以上にしてください。"); }

            decimal length = g.ColumnLength + 2m * g.DeckAxialOverhang;
            if (System.Math.Abs(g.DeckLength - length) > Tolerance)
            {
                errors.Add(name + "の操作台縦方向幅 X1 = " + g.DeckLength + " m が X4 + X5 + 2 × X3 = " + length
                    + " m と一致しません。");
            }
            decimal width = p.InnerWidth + 2m * g.ColumnBreadth + 2m * g.DeckSideOverhang;
            if (System.Math.Abs(g.DeckWidth - width) > Tolerance)
            {
                errors.Add(name + "の操作台横方向幅 X2 = " + g.DeckWidth + " m が 内空幅 B + 2 × (X7 + X8) + 2 × X6 = " + width
                    + " m と一致しません。");
            }

            if (g.Bridge)
            {
                if (g.SeatLength <= 0m || g.SeatLength >= g.DeckLength)
                {
                    errors.Add(name + "の操作台受け台縦方向幅 W1 は 0 より大きく操作台縦方向幅 X1 より小さくしてください。");
                }
                if (g.SeatWidth <= 0m || g.SeatWidth >= g.DeckWidth)
                {
                    errors.Add(name + "の操作台受け台横方向幅 W2 は 0 より大きく操作台横方向幅 X2 より小さくしてください。");
                }
                if (g.SeatDepth <= 0m || g.SeatDepth >= g.DeckThickness)
                {
                    errors.Add(name + "の操作台受け台高さ H1 は 0 より大きく操作台高さ Y1 より小さくしてください。");
                }
            }

            // 門柱の位置は川表側胸壁のたて壁で決まる(門柱の背面 = たて壁の背面)ので、川表側胸壁が要る。
            if (!p.DownstreamBreast.Exists)
            {
                errors.Add(name + "の位置は川表側胸壁のたて壁の背面で決まります。川表側胸壁を設置してください。");
                return;
            }
            (decimal Min, decimal Max) column = HikanGeometry.GateColumnRange(p);
            (decimal Min, decimal Max) deck = HikanGeometry.GateDeckRange(p);
            if (column.Min < -Tolerance || column.Max > p.BarrelLength + Tolerance)
            {
                errors.Add(name + "の門柱(川表側胸壁のたて壁の背面 " + column.Min + " m から X4 + X5 = " + g.ColumnLength
                    + " m)が函体延長 0〜" + p.BarrelLength + " m からはみ出します。川表側胸壁の軸位置を見直してください。");
            }
            for (int i = 1; i <= p.CutoffCount; i++)
            {
                decimal c = HikanGeometry.CutoffPosition(p, i);
                decimal cMin = c - p.CutoffThickness / 2m;
                decimal cMax = c + p.CutoffThickness / 2m;
                if (deck.Min < cMax && cMin < deck.Max)
                {
                    errors.Add(name + "の門柱・操作台(" + deck.Min + "〜" + deck.Max
                        + " m)が " + i + " 枚目のしゃ水壁(" + cMin + "〜" + cMax + " m)と重なります。");
                }
            }
            // 川表側胸壁のたて壁とは重なってよい(背面をそろえるので必ず接し、門柱が厚ければ重なる)。重なりは 1 回だけ数える。
            CheckGateAgainstBreast(errors, p, p.UpstreamBreast, "川裏側胸壁");
            if (deck.Min < 0m) { CheckGateAboveWing(errors, p, p.UpstreamWing, "川裏側"); }
            if (deck.Max > p.BarrelLength) { CheckGateAboveWing(errors, p, p.DownstreamWing, "川表側"); }
        }

        /// <summary>川裏側胸壁の底版・たて壁と門柱・操作台が函体方向に重なると 3 部材の重なりになり得るので弾く。</summary>
        private static void CheckGateAgainstBreast(
            System.Collections.Generic.List<string> errors, HikanParameters p, HikanBreastWall w, string name)
        {
            if (!w.Exists) { return; }
            (decimal Min, decimal Max) deck = HikanGeometry.GateDeckRange(p);
            (decimal Min, decimal Max) foot = HikanGeometry.BreastFootingRange(w, true);
            if (deck.Min < foot.Max && foot.Min < deck.Max)
            {
                errors.Add("門柱・操作台(" + deck.Min + "〜" + deck.Max + " m)が" + name + "(" + foot.Min + "〜" + foot.Max
                    + " m)と重なります。");
            }
        }

        /// <summary>函体端より外へ張り出した操作台は、その側の翼壁・堤防保護部より高い所になければならない。</summary>
        private static void CheckGateAboveWing(
            System.Collections.Generic.List<string> errors, HikanParameters p, HikanWingWall w, string end)
        {
            if (!w.Exists) { return; }
            decimal top = System.Math.Max(w.CrownStart, w.CrownEnd);
            if (w.Protection.Exists)
            {
                top = System.Math.Max(top, HikanGeometry.WingFloorTop(p) + w.Protection.StemHeight);
            }
            decimal deckBottom = p.OuterHeight + p.Gate.ColumnHeight;
            if (deckBottom < top)
            {
                errors.Add("門柱・操作台の柱高さ Y2 では操作台の下面(" + deckBottom + " m)が" + end + "翼壁(堤防保護部を含む)の天端 "
                    + top + " m より低く、函体端より外へ張り出した操作台が重なります。");
            }
        }

        private static void CheckBreastAgainstCutoffs(
            System.Collections.Generic.List<string> errors,
            HikanParameters p,
            HikanBreastWall w,
            bool upstream,
            string name)
        {
            if (!w.Exists) { return; }
            (decimal Min, decimal Max) b = HikanGeometry.BreastFootingRange(w, upstream);
            for (int i = 1; i <= p.CutoffCount; i++)
            {
                decimal c = HikanGeometry.CutoffPosition(p, i);
                decimal cMin = c - p.CutoffThickness / 2m;
                decimal cMax = c + p.CutoffThickness / 2m;
                if (b.Min <= cMax && cMin <= b.Max)
                {
                    errors.Add(name + "(" + b.Min + "〜" + b.Max + " m)が " + i + " 枚目のしゃ水壁("
                        + cMin + "〜" + cMax + " m)と干渉します。");
                }
            }
        }

        private static void CheckThickness(System.Collections.Generic.List<string> errors, decimal t, string name)
        {
            if (t < MinMemberThickness)
            {
                errors.Add(name + " は最小部材厚 " + MinMemberThickness + " m 以上にしてください(基準)。");
            }
            else if (t % ThicknessPitch != 0m)
            {
                errors.Add(name + " は " + ThicknessPitch + " m ピッチで規格化してください(基準)。");
            }
        }

        /// <summary>
        /// 新規入力(入力画面・コマンドライン・Dynamo)用。Validate に加えて、胸壁・翼壁を必ず設置すること
        /// (たて壁厚・側壁厚 0.40 m 以上)を求める。Validate 単体は旧図面(厚 0 = 設置なし)を読めるよう 0 を許す。
        /// </summary>
        public static System.Collections.Generic.List<string> ValidateInput(HikanParameters p)
        {
            System.Collections.Generic.List<string> errors = Validate(p);
            RequireWall(errors, p.UpstreamBreast.StemThickness, "川裏側胸壁のたて壁厚");
            RequireWall(errors, p.DownstreamBreast.StemThickness, "川表側胸壁のたて壁厚");
            RequireWall(errors, p.UpstreamWing.StemThickness, "川裏側翼壁の側壁厚");
            RequireWall(errors, p.DownstreamWing.StemThickness, "川表側翼壁の側壁厚");
            return errors;
        }

        /// <summary>厚 0(旧図面の「設置なし」)を新規入力では認めない。0 以外は Validate が検査済み。</summary>
        private static void RequireWall(System.Collections.Generic.List<string> errors, decimal t, string name)
        {
            if (t == 0m) { CheckThickness(errors, t, name); }
        }

        public static void EnsureInputValid(HikanParameters p)
        {
            System.Collections.Generic.List<string> errors = ValidateInput(p);
            if (errors.Count > 0)
            {
                throw new HikanValidationException(errors);
            }
        }

        public static void EnsureValid(HikanParameters p)
        {
            System.Collections.Generic.List<string> errors = Validate(p);
            if (errors.Count > 0)
            {
                throw new HikanValidationException(errors);
            }
        }
    }
}
