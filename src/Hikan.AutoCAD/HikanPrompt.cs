// === 参照DLLバージョン検証済み(2026-10-07) === (詳細は HikanSolidBuilder.cs 先頭を参照。scripts/verify-dll-versions.ps1 で検証)
namespace Hikan.AutoCAD
{
    /// <summary>コマンドラインからのパラメータ入力。Enter で既定値(現在値)を採用、ESC でキャンセル(false)。</summary>
    internal static class HikanPrompt
    {
        private static bool GetDecimal(Autodesk.AutoCAD.EditorInput.Editor ed, string message, decimal current, out decimal value)
        {
            Autodesk.AutoCAD.EditorInput.PromptDoubleOptions o = new Autodesk.AutoCAD.EditorInput.PromptDoubleOptions("\n" + message);
            o.DefaultValue = (double)current;
            o.UseDefaultValue = true;
            o.AllowNone = true;
            Autodesk.AutoCAD.EditorInput.PromptDoubleResult r = ed.GetDouble(o);
            if (r.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
            {
                value = current;
                return false;
            }
            value = (decimal)r.Value;
            return true;
        }

        private static bool GetInt(Autodesk.AutoCAD.EditorInput.Editor ed, string message, int current, out int value)
        {
            Autodesk.AutoCAD.EditorInput.PromptIntegerOptions o = new Autodesk.AutoCAD.EditorInput.PromptIntegerOptions("\n" + message);
            o.DefaultValue = current;
            o.UseDefaultValue = true;
            o.AllowNone = true;
            Autodesk.AutoCAD.EditorInput.PromptIntegerResult r = ed.GetInteger(o);
            if (r.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
            {
                value = current;
                return false;
            }
            value = r.Value;
            return true;
        }

        /// <summary>全パラメータを順に入力する。p を直接更新する。基準点は Create 時のみ askBasePoint=true で入力。</summary>
        public static bool PromptAll(Autodesk.AutoCAD.EditorInput.Editor ed, Hikan.Core.HikanParameters p, bool askBasePoint)
        {
            decimal d;
            int n;

            if (askBasePoint)
            {
                Autodesk.AutoCAD.EditorInput.PromptPointOptions po =
                    new Autodesk.AutoCAD.EditorInput.PromptPointOptions("\n基準点(上流端・底版下面・函体中心線上)を指定 <0,0,0>: ");
                po.AllowNone = true;
                Autodesk.AutoCAD.EditorInput.PromptPointResult pr = ed.GetPoint(po);
                if (pr.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                {
                    p.BaseX = (decimal)pr.Value.X;
                    p.BaseY = (decimal)pr.Value.Y;
                    p.BaseZ = (decimal)pr.Value.Z;
                }
                else if (pr.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.None)
                {
                    return false;
                }
            }

            if (!GetDecimal(ed, "内空幅 B [m]", p.InnerWidth, out d)) { return false; }
            p.InnerWidth = d;
            if (!GetDecimal(ed, "内空高 H [m]", p.InnerHeight, out d)) { return false; }
            p.InnerHeight = d;
            if (!GetDecimal(ed, "側壁厚 [m] (0.40 以上・0.10 ピッチ)", p.WallThickness, out d)) { return false; }
            p.WallThickness = d;
            if (!GetDecimal(ed, "頂版厚 [m] (0.40 以上・0.10 ピッチ)", p.TopSlabThickness, out d)) { return false; }
            p.TopSlabThickness = d;
            if (!GetDecimal(ed, "底版厚 [m] (0.40 以上・0.10 ピッチ)", p.BottomSlabThickness, out d)) { return false; }
            p.BottomSlabThickness = d;
            if (!GetDecimal(ed, "函体延長 L [m] (底版下面に沿う斜距離)", p.BarrelLength, out d)) { return false; }
            p.BarrelLength = d;
            if (!GetDecimal(ed, "底版勾配 i (下流下がり、0 で水平)", p.BottomSlope, out d)) { return false; }
            p.BottomSlope = d;
            if (!GetDecimal(ed, "土かぶり (頂版上面〜現地盤) [m]", p.SoilCover, out d)) { return false; }
            p.SoilCover = d;
            if (!GetInt(ed, "ブロック数 (数量の按分表示のみ)", p.BlockCount, out n)) { return false; }
            p.BlockCount = n;
            if (!GetDecimal(ed, "床掘り余裕幅 (片側) [m]", p.ExcavationMargin, out d)) { return false; }
            p.ExcavationMargin = d;
            if (!GetDecimal(ed, "床掘り法勾配 1:n の n", p.ExcavationSlope, out d)) { return false; }
            p.ExcavationSlope = d;
            if (!GetDecimal(ed, "基礎材厚 (均しコン等) [m]", p.FoundationThickness, out d)) { return false; }
            p.FoundationThickness = d;
            if (!GetInt(ed, "ソリッド色 (ACI 0〜256)", p.ColorIndex, out n)) { return false; }
            p.ColorIndex = n;

            return true;
        }
    }
}
