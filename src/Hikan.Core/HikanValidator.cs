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

            CheckBreastWall(errors, p, p.UpstreamBreast, "上流胸壁");
            CheckBreastWall(errors, p, p.DownstreamBreast, "下流胸壁");
            CheckBreastClearance(errors, p);
            CheckCutoff(errors, p);

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
        /// 胸壁。たて壁厚 0 は「設置しない」なので寸法を検査しない。
        /// 幅・天端高を函体外形以上に、根入れ深さを底版厚以上に強制するのは、
        /// たて壁と函体の重なりが厳密に「外形断面 × たて壁厚」になり、底版が函体と重ならないことを保証するため。
        /// これが崩れると体積の解析解(ModelVolume)が実形状と一致せず Verify が通らない。
        /// </summary>
        private static void CheckBreastWall(
            System.Collections.Generic.List<string> errors,
            HikanParameters p,
            HikanBreastWall w,
            string name)
        {
            if (w.StemThickness < 0m)
            {
                errors.Add(name + "のたて壁厚は 0 以上にしてください(0 で設置しない)。");
                return;
            }
            if (!w.Exists) { return; }

            CheckThickness(errors, w.StemThickness, name + "のたて壁厚");
            CheckThickness(errors, w.FootingThickness, name + "の底版厚");

            if (w.Width < p.OuterWidth - Tolerance)
            {
                errors.Add(name + "の幅は函体外形幅 " + p.OuterWidth + " m 以上にしてください。");
            }
            if (w.CrownHeight < p.OuterHeight - Tolerance)
            {
                errors.Add(name + "の天端高は函体外形高 " + p.OuterHeight + " m 以上にしてください。");
            }
            if (w.Embedment < w.FootingThickness - Tolerance)
            {
                errors.Add(name + "の根入れ深さは底版厚 " + w.FootingThickness
                    + " m 以上にしてください(底版が函体と干渉します)。");
            }
            if (w.ToeLength < 0m) { errors.Add(name + "のつま先版長は 0 以上にしてください。"); }
            if (w.HeelLength < 0m) { errors.Add(name + "のかかと版長は 0 以上にしてください。"); }
            if (p.BarrelLength > 0m && w.StemThickness > p.BarrelLength)
            {
                errors.Add(name + "のたて壁厚が函体延長を超えています。");
            }
        }

        /// <summary>上下流の胸壁が函体の中で干渉しないこと。かかと版どうしが重なると体積の解析解が崩れる。</summary>
        private static void CheckBreastClearance(System.Collections.Generic.List<string> errors, HikanParameters p)
        {
            if (!p.UpstreamBreast.Exists || !p.DownstreamBreast.Exists) { return; }
            decimal upstreamEnd = p.UpstreamBreast.StemThickness + p.UpstreamBreast.HeelLength;
            decimal downstreamStart = p.BarrelLength - p.DownstreamBreast.StemThickness - p.DownstreamBreast.HeelLength;
            if (upstreamEnd >= downstreamStart)
            {
                errors.Add("上流胸壁の下流端 " + upstreamEnd + " m と下流胸壁の上流端 " + downstreamStart
                    + " m が干渉します。たて壁厚とかかと版長を見直してください。");
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

            // 胸壁とカラーは X・Z で重なるので、Y で離れていないと体積の解析解が崩れる。
            decimal firstMin = HikanGeometry.CutoffPosition(p, 1) - p.CutoffThickness / 2m;
            decimal lastMax = HikanGeometry.CutoffPosition(p, p.CutoffCount) + p.CutoffThickness / 2m;
            if (p.UpstreamBreast.Exists)
            {
                decimal end = p.UpstreamBreast.StemThickness + p.UpstreamBreast.HeelLength;
                if (end >= firstMin)
                {
                    errors.Add("上流胸壁の下流端 " + end + " m が 1 枚目のしゃ水壁(" + firstMin + " m)と干渉します。");
                }
            }
            if (p.DownstreamBreast.Exists)
            {
                decimal start = p.BarrelLength - p.DownstreamBreast.StemThickness - p.DownstreamBreast.HeelLength;
                if (start <= lastMax)
                {
                    errors.Add("下流胸壁の上流端 " + start + " m が最後のしゃ水壁(" + lastMax + " m)と干渉します。");
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
