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

            // 整合性チェック: 部位別体積の和 == 断面積 × 延長。断面の分解と全体が食い違う入力を弾く。
            if (p.InnerWidth > 0m && p.InnerHeight > 0m && p.BarrelLength > 0m)
            {
                decimal parts = p.OuterWidth * p.TopSlabThickness * p.BarrelLength
                    + p.OuterWidth * p.BottomSlabThickness * p.BarrelLength
                    + 2m * p.WallThickness * p.InnerHeight * p.BarrelLength;
                decimal whole = HikanGeometry.ModelVolume(p);
                if (System.Math.Abs(parts - whole) > Tolerance)
                {
                    errors.Add("部位別体積の和 " + parts + " m3 が断面積×延長 " + whole + " m3 と一致しません。");
                }
            }

            return errors;
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
