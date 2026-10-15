// === 参照DLLバージョン検証済み(2026-10-07) === (詳細は HikanSolidBuilder.cs 先頭を参照。scripts/verify-dll-versions.ps1 で検証)
// このファイルは AutoCAD API を使わない(System.Drawing のみ)。
namespace Hikan.AutoCAD
{
    /// <summary>
    /// 入力画面の選択ボタンに付ける小さな絵(手本の樋門設計ソフトの「門柱形式」「操作台形状形式」を模したもの)。
    /// 絵は 72 × 48 の画素座標で描く。操作台形状形式は側面から見た形で、並び順は手本の画面と同じ。
    /// </summary>
    internal static class HikanIcons
    {
        private const int Width = 72;
        private const int Height = 48;

        public static System.Drawing.Bitmap Render(System.Action<System.Drawing.Graphics> draw, bool enabled)
        {
            System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(Width, Height);
            using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.White);
                draw(g);
            }
            if (!enabled)
            {
                // 選べない形式は薄く表示する
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        System.Drawing.Color c = bmp.GetPixel(x, y);
                        bmp.SetPixel(x, y, System.Drawing.Color.FromArgb((c.R + 2 * 255) / 3, (c.G + 2 * 255) / 3, (c.B + 2 * 255) / 3));
                    }
                }
            }
            return bmp;
        }

        private static void Rect(System.Drawing.Graphics g, int x, int y, int w, int h)
        {
            using (System.Drawing.SolidBrush fill = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(190, 190, 190)))
            using (System.Drawing.Pen pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(60, 60, 60)))
            {
                g.FillRectangle(fill, x, y, w, h);
                g.DrawRectangle(pen, x, y, w, h);
            }
        }

        public static void TwoColumns(System.Drawing.Graphics g)
        {
            Rect(g, 6, 6, 60, 8);
            Rect(g, 14, 14, 9, 30);
            Rect(g, 49, 14, 9, 30);
        }

        public static void ThreeColumns(System.Drawing.Graphics g)
        {
            Rect(g, 6, 6, 60, 8);
            Rect(g, 10, 14, 9, 30);
            Rect(g, 31, 14, 9, 30);
            Rect(g, 53, 14, 9, 30);
        }

        public static void DeckType1(System.Drawing.Graphics g)
        {
            Rect(g, 6, 8, 36, 8);
            Rect(g, 42, 12, 16, 4);
            Rect(g, 16, 16, 12, 28);
            Rect(g, 50, 26, 10, 18);
        }

        public static void DeckType2(System.Drawing.Graphics g)
        {
            Rect(g, 6, 8, 36, 8);
            Rect(g, 42, 12, 14, 4);
            Rect(g, 16, 16, 12, 28);
            Rect(g, 48, 26, 8, 12);
        }

        public static void DeckType3(System.Drawing.Graphics g)
        {
            Rect(g, 10, 8, 54, 8);
            Rect(g, 18, 16, 12, 28);
        }

        public static void DeckType4(System.Drawing.Graphics g)
        {
            Rect(g, 8, 8, 56, 8);
            Rect(g, 30, 16, 12, 28);
        }

        public static void DeckType5(System.Drawing.Graphics g)
        {
            Rect(g, 6, 6, 40, 8);
            Rect(g, 40, 14, 6, 10);
            Rect(g, 40, 24, 26, 6);
            Rect(g, 16, 14, 12, 30);
        }

        public static void DeckType6(System.Drawing.Graphics g)
        {
            Rect(g, 6, 8, 40, 8);
            Rect(g, 46, 8, 20, 14);
            Rect(g, 18, 16, 12, 28);
        }
    }
}
