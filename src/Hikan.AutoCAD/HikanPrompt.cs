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

        /// <summary>胸壁 1 端ぶん。たて壁厚 0 なら「設置しない」として残りを聞かない。</summary>
        /// <remarks>左右の胸壁は同一形状なので、入力は片側分だけ。</remarks>
        private static bool PromptBreast(
            Autodesk.AutoCAD.EditorInput.Editor ed,
            Hikan.Core.HikanBreastWall w,
            string name)
        {
            decimal d;
            if (!GetDecimal(ed, name + " たて壁厚 [m] (0 で設置しない)", w.StemThickness, out d)) { return false; }
            w.StemThickness = d;
            if (!w.Exists) { return true; }

            if (!GetDecimal(ed, name + " 張出し長 [m] (函体側面から外向き。左右同一)", w.Length, out d)) { return false; }
            w.Length = d;
            if (!GetDecimal(ed, name + " 天端高 [m] (函体底版下面から)", w.CrownHeight, out d)) { return false; }
            w.CrownHeight = d;
            if (!GetDecimal(ed, name + " 根入れ深さ [m] (底版厚以上)", w.Embedment, out d)) { return false; }
            w.Embedment = d;
            if (!GetDecimal(ed, name + " 底版厚 [m]", w.FootingThickness, out d)) { return false; }
            w.FootingThickness = d;
            if (!GetDecimal(ed, name + " つま先版長 [m] (函体から遠い側。0 で L 字)", w.ToeLength, out d)) { return false; }
            w.ToeLength = d;
            if (!GetDecimal(ed, name + " かかと版長 [m] (函体に近い側。0 で L 字)", w.HeelLength, out d)) { return false; }
            w.HeelLength = d;
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

            // 胸壁。たて壁厚 0 なら設置しないので残りは聞かない。
            if (!PromptBreast(ed, p.UpstreamBreast, "上流胸壁")) { return false; }
            if (!PromptBreast(ed, p.DownstreamBreast, "下流胸壁")) { return false; }

            // しゃ水壁。枚数 0 なら設置しないので厚・張出しは聞かない。
            if (!GetInt(ed, "しゃ水壁 枚数 (0 で設置しない。等間隔配置)", p.CutoffCount, out n)) { return false; }
            p.CutoffCount = n;
            if (p.CutoffCount > 0)
            {
                if (!GetDecimal(ed, "しゃ水壁 厚 [m]", p.CutoffThickness, out d)) { return false; }
                p.CutoffThickness = d;
                if (!GetDecimal(ed, "しゃ水壁 張出し量 [m] (全周一律、下方にも)", p.CutoffProjection, out d)) { return false; }
                p.CutoffProjection = d;
            }
            if (!GetInt(ed, "ソリッド色 (ACI 0〜256)", p.ColorIndex, out n)) { return false; }
            p.ColorIndex = n;

            return true;
        }
    }
}
