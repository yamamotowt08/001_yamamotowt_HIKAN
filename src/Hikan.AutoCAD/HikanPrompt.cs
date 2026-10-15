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

        /// <summary>胸壁 1 端ぶん。胸壁は必ず設置する(たて壁厚 0.40 m 以上)。</summary>
        /// <remarks>左右の胸壁は同一形状なので、入力は片側分だけ。底版上面は函体の敷高に合わせるので、高さは聞かない。</remarks>
        private static bool PromptBreast(
            Autodesk.AutoCAD.EditorInput.Editor ed,
            Hikan.Core.HikanBreastWall w,
            string name)
        {
            decimal d;
            if (!GetDecimal(ed, name + " たて壁厚 [m] (0.40 以上・0.10 ピッチ)", w.StemThickness, out d)) { return false; }
            w.StemThickness = d;

            if (!GetDecimal(ed, name + " たて壁軸位置 [m] (川裏函体端からたて壁厚の中心まで)", w.Position, out d)) { return false; }
            w.Position = d;
            if (!GetDecimal(ed, name + " 張出し長 [m] (函体側面から外向き。左右同一)", w.Length, out d)) { return false; }
            w.Length = d;
            if (!GetDecimal(ed, name + " 天端高 [m] (函体底版下面から)", w.CrownHeight, out d)) { return false; }
            w.CrownHeight = d;
            if (!GetDecimal(ed, name + " 底版厚 [m] (上面は函体の敷高)", w.FootingThickness, out d)) { return false; }
            w.FootingThickness = d;
            if (!GetDecimal(ed, name + " つま先版長 [m] (近い方の函体端の側。0 で L 字)", w.ToeLength, out d)) { return false; }
            w.ToeLength = d;
            if (!GetDecimal(ed, name + " かかと版長 [m] (函体中央の側。0 で L 字)", w.HeelLength, out d)) { return false; }
            w.HeelLength = d;
            return true;
        }

        /// <summary>翼壁 1 端ぶん(U 字の 3 面張り水路)。翼壁は必ず設置する(側壁厚 0.40 m 以上)。</summary>
        /// <remarks>左右の側壁は同一形状なので、入力は片側分だけ。開き角は端部水路幅と内空幅の差から決まる。
        /// 底版上面は函体の敷高に合わせるので、高さは聞かない。</remarks>
        private static bool PromptWing(
            Autodesk.AutoCAD.EditorInput.Editor ed,
            Hikan.Core.HikanWingWall w,
            string name)
        {
            decimal d;
            if (!GetDecimal(ed, name + " 側壁厚 [m] (壁面に直交。0.40 以上・0.10 ピッチ)", w.StemThickness, out d)) { return false; }
            w.StemThickness = d;

            if (!GetDecimal(ed, name + " 翼壁長 [m] (函体端から翼壁端部まで、函体軸方向)", w.Length, out d)) { return false; }
            w.Length = d;
            if (!GetDecimal(ed, name + " 端部の水路幅 [m] (左右の側壁内面間。内空幅と同じで平行)", w.EndWidth, out d)) { return false; }
            w.EndWidth = d;
            if (!GetDecimal(ed, name + " 側壁の天端高 [m] (函体端、函体底版下面から)", w.CrownStart, out d)) { return false; }
            w.CrownStart = d;
            if (!GetDecimal(ed, name + " 側壁の天端高 [m] (翼壁端部、函体底版下面から)", w.CrownEnd, out d)) { return false; }
            w.CrownEnd = d;
            if (!GetDecimal(ed, name + " 底版厚 [m] (上面は函体の敷高)", w.FloorThickness, out d)) { return false; }
            w.FloorThickness = d;

            // 堤防保護部(翼壁の端部から左右へ張り出す逆 T 字擁壁)。設置しないなら残りは聞かない。たて壁高 Y3 は H1 − 敷高で決まる。
            Hikan.Core.HikanLeveeProtection g = w.Protection;
            int n;
            if (!GetInt(ed, name + " 堤防保護部 (1 = 設置する / 0 = 設置しない)", g.Exists ? 1 : 0, out n)) { return false; }
            g.Exists = n != 0;
            if (!g.Exists) { return true; }
            if (!GetDecimal(ed, name + " 堤防保護部 X4 張出し幅 [m] (翼壁端部の側壁内面から外向き)", g.Width, out d)) { return false; }
            g.Width = d;
            if (!GetDecimal(ed, name + " 堤防保護部 L3 たて壁厚 [m] (外側の面を翼壁端部にそろえる)", g.StemThickness, out d)) { return false; }
            g.StemThickness = d;
            if (!GetDecimal(ed, name + " 堤防保護部 L2 つま先版長 [m] (翼壁端部より外側)", g.ToeLength, out d)) { return false; }
            g.ToeLength = d;
            if (!GetDecimal(ed, name + " 堤防保護部 L4 かかと版長 [m] (函体側)", g.HeelLength, out d)) { return false; }
            g.HeelLength = d;
            return true;
        }

        /// <summary>
        /// 門柱・操作台(記号は手本の樋門設計ソフトに合わせる)。設置しないなら残りは聞かない。
        /// 門柱の位置は聞かない(門柱の背面を川表側胸壁のたて壁の背面にそろえる)。
        /// </summary>
        private static bool PromptGate(Autodesk.AutoCAD.EditorInput.Editor ed, Hikan.Core.HikanGateTower g)
        {
            decimal d;
            int n;
            if (!GetInt(ed, "門柱・操作台 (1 = 設置する / 0 = 設置しない)", g.Exists ? 1 : 0, out n)) { return false; }
            g.Exists = n != 0;
            if (!g.Exists) { return true; }
            if (!GetDecimal(ed, "門柱 X4 縦方向壁幅 [m] (切欠きのある区間、川表側)", g.LongitudinalWallWidth, out d)) { return false; }
            g.LongitudinalWallWidth = d;
            if (!GetDecimal(ed, "門柱 X5 横方向壁厚 [m] (川裏側)", g.TransverseWallThickness, out d)) { return false; }
            g.TransverseWallThickness = d;
            if (!GetDecimal(ed, "門柱 X7 縦方向壁厚 [m] (外側)", g.LongitudinalWallThickness, out d)) { return false; }
            g.LongitudinalWallThickness = d;
            if (!GetDecimal(ed, "門柱 X8 横方向壁幅 [m] (内側、切欠きの幅)", g.TransverseWallWidth, out d)) { return false; }
            g.TransverseWallWidth = d;
            if (!GetDecimal(ed, "門柱 Y2 柱高さ [m] (胸壁の底版の上面 = 敷高から操作台下面まで)", g.ColumnHeight, out d)) { return false; }
            g.ColumnHeight = d;
            if (!GetDecimal(ed, "門柱 H2 ハンチ高 [m] (川表側の柱の内面と操作台下面の隅の三角 1:1、0 でなし)", g.HaunchHeight, out d)) { return false; }
            g.HaunchHeight = d;
            if (!GetDecimal(ed, "土留壁 WH 高さ [m] (門柱の間、頂版上面から、厚さ X5。0 でなし)", g.RetainingWallHeight, out d)) { return false; }
            g.RetainingWallHeight = d;
            if (!GetDecimal(ed, "操作台 X1 縦方向幅 [m] (= X4 + X5 + 2 × X3)", g.DeckLength, out d)) { return false; }
            g.DeckLength = d;
            if (!GetDecimal(ed, "操作台 X2 横方向幅 [m] (= 内空幅 + 2 × (X7 + X8) + 2 × X6)", g.DeckWidth, out d)) { return false; }
            g.DeckWidth = d;
            if (!GetDecimal(ed, "操作台 X3 縦方向張出長 [m]", g.DeckAxialOverhang, out d)) { return false; }
            g.DeckAxialOverhang = d;
            if (!GetDecimal(ed, "操作台 X6 横方向張出長 [m]", g.DeckSideOverhang, out d)) { return false; }
            g.DeckSideOverhang = d;
            if (!GetDecimal(ed, "操作台 Y1 高さ [m]", g.DeckThickness, out d)) { return false; }
            g.DeckThickness = d;
            if (!GetInt(ed, "管理橋の設置 (1 = する / 0 = しない)", g.Bridge ? 1 : 0, out n)) { return false; }
            g.Bridge = n != 0;
            if (!g.Bridge) { return true; }
            if (!GetDecimal(ed, "操作台 W1 受け台縦方向幅 [m] (川裏側の端の桁受け切欠き)", g.SeatLength, out d)) { return false; }
            g.SeatLength = d;
            if (!GetDecimal(ed, "操作台 W2 受け台横方向幅 [m] (中央)", g.SeatWidth, out d)) { return false; }
            g.SeatWidth = d;
            if (!GetDecimal(ed, "操作台 H1 受け台高さ [m] (切欠きの深さ)", g.SeatDepth, out d)) { return false; }
            g.SeatDepth = d;
            return true;
        }

        /// <summary>基準点を入力する。Enter で (0,0,0)、ESC でキャンセル(false)。</summary>
        public static bool PromptBasePoint(Autodesk.AutoCAD.EditorInput.Editor ed, Hikan.Core.HikanParameters p)
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
                return true;
            }
            return pr.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.None;
        }

        /// <summary>全パラメータを順に入力する。p を直接更新する。基準点は Create 時のみ askBasePoint=true で入力。</summary>
        public static bool PromptAll(Autodesk.AutoCAD.EditorInput.Editor ed, Hikan.Core.HikanParameters p, bool askBasePoint)
        {
            decimal d;
            int n;

            if (askBasePoint && !PromptBasePoint(ed, p)) { return false; }

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

            // 胸壁(必ず設置する)。
            if (!PromptBreast(ed, p.UpstreamBreast, "川裏側胸壁")) { return false; }
            if (!GetDecimal(ed, "川裏側土留壁 高さ [m] (左右のたて壁の間、頂版上面から。0 でなし)", p.UpstreamRetainingWall.Height, out d)) { return false; }
            p.UpstreamRetainingWall.Height = d;
            if (p.UpstreamRetainingWall.Exists)
            {
                if (!GetDecimal(ed, "川裏側土留壁 厚さ [m] (背面 = たて壁の背面から川裏側へ)", p.UpstreamRetainingWall.Thickness, out d)) { return false; }
                p.UpstreamRetainingWall.Thickness = d;
            }
            if (!PromptBreast(ed, p.DownstreamBreast, "川表側胸壁")) { return false; }

            // 翼壁(必ず設置する)。
            if (!PromptWing(ed, p.UpstreamWing, "川裏側翼壁")) { return false; }
            if (!PromptWing(ed, p.DownstreamWing, "川表側翼壁")) { return false; }

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
            if (!PromptGate(ed, p.Gate)) { return false; }
            if (!GetInt(ed, "ソリッド色 (ACI 0〜256)", p.ColorIndex, out n)) { return false; }
            p.ColorIndex = n;

            return true;
        }
    }
}
