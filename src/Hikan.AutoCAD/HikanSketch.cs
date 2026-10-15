// === 参照DLLバージョン検証済み(2026-10-07) === (詳細は HikanSolidBuilder.cs 先頭を参照。scripts/verify-dll-versions.ps1 で検証)
// このファイルは AutoCAD API を使わない(System.Drawing のみ)。
namespace Hikan.AutoCAD
{
    /// <summary>
    /// 入力画面の模式図。寸法記号の意味を示すだけで、入力値には連動しない(形・比率は固定)。
    /// 図は 100 × 60 の仮想座標(y 上向き)で描き、表示領域に縦横比を保って収める。
    /// 記号は入力画面の「記号」列と一致させること。
    /// </summary>
    internal sealed class HikanSketch
    {
        private const float CanvasWidth = 100f;
        private const float CanvasHeight = 60f;
        private const float Margin = 8f;

        private static readonly System.Drawing.Color ConcreteColor = System.Drawing.Color.FromArgb(205, 205, 205);
        private static readonly System.Drawing.Color FloorColor = System.Drawing.Color.FromArgb(228, 228, 228);
        private static readonly System.Drawing.Color SoilColor = System.Drawing.Color.FromArgb(244, 236, 222);
        private static readonly System.Drawing.Color OutlineColor = System.Drawing.Color.FromArgb(80, 80, 80);
        private static readonly System.Drawing.Color DimensionColor = System.Drawing.Color.FromArgb(30, 60, 200);
        private static readonly System.Drawing.Color NoteColor = System.Drawing.Color.FromArgb(90, 90, 90);
        private static readonly System.Drawing.Color HaunchColor = System.Drawing.Color.FromArgb(200, 40, 40);

        private readonly System.Drawing.Graphics g;
        private readonly System.Drawing.Font font;
        private readonly float scale;
        private readonly float left;
        private readonly float bottom;
        /// <summary>設定中はこの x を軸に左右反転して描く(川表側を川裏側の鏡像で描くため)。</summary>
        private float? mirrorAxis;

        private HikanSketch(System.Drawing.Graphics g, System.Drawing.Rectangle area, System.Drawing.Font font)
        {
            this.g = g;
            this.font = font;
            scale = System.Math.Max(0.1f, System.Math.Min(
                (area.Width - 2f * Margin) / CanvasWidth, (area.Height - 2f * Margin) / CanvasHeight));
            left = area.X + (area.Width - CanvasWidth * scale) / 2f;
            bottom = area.Y + (area.Height + CanvasHeight * scale) / 2f;
        }

        public static void Draw(
            System.Drawing.Graphics g,
            System.Drawing.Rectangle area,
            System.Drawing.Font font,
            System.Action<HikanSketch> figure)
        {
            g.Clear(System.Drawing.Color.White);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            figure(new HikanSketch(g, area, font));
        }

        // ---------------------------------------------------------------- 図

        /// <summary>
        /// 函体の断面(下流を見る)。記号は手本の樋門設計ソフトの「断面寸法」に合わせる(WL・WR は側壁厚で左右共通)。
        /// 内空の 4 隅のハンチ LT・RT・LB・RB は H(鉛直)× W(水平)の直角三角形。
        /// </summary>
        public void BarrelSection()
        {
            // 外形 x 22〜78 / y 6〜50、内空 x 29〜71 / y 13〜43。上のハンチ 6、下のハンチ 4 の例。
            Solid(22, 6, 78, 6, 78, 50, 22, 50);
            Hollow(33, 13, 67, 13, 71, 17, 71, 37, 65, 43, 35, 43, 29, 37, 29, 17);
            Dashed(50, 3, 50, 53);
            Dim(22, 50, 29, 50, 4, "WL");
            Dim(29, 50, 71, 50, 4, "SW");
            Dim(71, 50, 78, 50, 4, "WR");
            Dim(22, 50, 22, 43, -4, "WT");
            Dim(22, 43, 22, 13, -4, "SH");
            Dim(22, 13, 22, 6, -4, "WB");
            Text(42, 46.5f, "LT", HaunchColor);
            Text(58, 46.5f, "RT", HaunchColor);
            Text(42, 9.5f, "LB", HaunchColor);
            Text(58, 9.5f, "RB", HaunchColor);
            Dim(29, 43, 29, 37, -2.5f, "H");
            Dim(29, 43, 35, 43, 2.5f, "W");
            Note(88, 28, "WL = WR");
        }

        /// <summary>函体の縦断面(中心線で切る)。端部は部材厚を変えられ、外形が段違いになる。スパンの合計が函体延長。</summary>
        public void BarrelProfile()
        {
            // 標準部の外形 y 22〜38、端部 y 20〜40(厚くした例)。内空 y 26〜34 は全長で共通。
            Solid(8, 20, 24, 20, 24, 22, 76, 22, 76, 20, 92, 20, 92, 40, 76, 40, 76, 38, 24, 38, 24, 40, 8, 40);
            Fill(System.Drawing.Color.White, 8, 26, 92, 26, 92, 34, 8, 34);
            Line(8, 26, 92, 26);
            Line(8, 34, 92, 34);
            Dashed(24, 18, 24, 42);
            Dashed(76, 18, 76, 42);
            Dim(8, 40, 24, 40, 4, "川裏側端部");
            Dim(24, 40, 76, 40, 4, "標準部");
            Dim(76, 40, 92, 40, 4, "川表側端部");
            Dashed(36, 12, 36, 20);
            Dashed(62, 12, 62, 20);
            Dim(8, 20, 36, 20, -5, "スパン1");
            Dim(36, 20, 62, 20, -5, "スパン2");
            Dim(62, 20, 92, 20, -5, "スパン3");
            Note(16, 30, "川裏");
            Note(84, 30, "川表");
            Note(50, 30, "内空");
            Note(50, 6, "函体延長 L = スパン長の合計(継手 = スパンの境目)");
            Title(50, 55, "縦断面");
        }

        /// <summary>函体の平断面(内空の高さで切る)。内空幅は全長で共通、端部は側壁厚を変えられる。</summary>
        public void BarrelPlan()
        {
            // 左の側壁(下流を見て左 = 図の上)と右の側壁。端部は側壁を厚くした例。
            Solid(8, 44, 24, 44, 24, 42, 76, 42, 76, 44, 92, 44, 92, 38, 8, 38);
            Solid(8, 16, 24, 16, 24, 18, 76, 18, 76, 16, 92, 16, 92, 22, 8, 22);
            Dashed(24, 12, 24, 48);
            Dashed(76, 12, 76, 48);
            Dim(50, 22, 50, 38, 0, "SW");
            Dim(92, 38, 92, 44, -4, "WL");
            Dim(92, 16, 92, 22, -4, "WR");
            Note(16, 30, "川裏");
            Note(84, 30, "川表");
            Note(16, 8, "川裏側端部");
            Note(50, 8, "標準部");
            Note(84, 8, "川表側端部");
            Title(50, 55, "平断面");
        }

        /// <summary>胸壁: 断面(下流を見る)と側面。川裏側はつま先が川裏端側、川表側は川表端側。</summary>
        public void Breast(bool upstream)
        {
            // 断面。Z = 0 は y = 20、函体の敷高(内空底面)は y = 22.5。
            // 底版(上面 = 敷高)は左右の張出し端から端まで 1 枚で函体の下を通る。ここでは底版厚 > 函体の底版厚の例。
            Solid(7, 18.5f, 43, 18.5f, 43, 22.5f, 7, 22.5f);
            Solid(17, 20, 33, 20, 33, 34, 17, 34);
            Hollow(19.5f, 22.5f, 30.5f, 22.5f, 30.5f, 32, 19.5f, 32);
            foreach (float x0 in new float[] { 7, 33 })
            {
                Solid(x0, 22.5f, x0 + 10, 22.5f, x0 + 10, 46, x0, 46);
            }
            if (upstream)
            {
                // 川裏側の土留壁(左右のたて壁の間、頂版上面から WH。背面 = たて壁の背面、厚さ WT は川裏側へ)
                Solid(17, 34, 33, 34, 33, 39, 17, 39);
                Dim(29, 34, 29, 39, 0, "WH");
                Note(21.5f, 36.5f, "土留壁");
                Note(76, 7, "土留壁 WT は たて壁の背面(函体中央側)から川裏側へ");
            }
            Dashed(3, 20, 47, 20);
            Note(25, 16.5f, "Z = 0");
            Note(25, 24.3f, "敷高");
            Dim(33, 46, 43, 46, 3, "Lw");
            Dim(43, 20, 43, 46, -3, "Hc");
            Dim(7, 22.5f, 7, 18.5f, -3, "tf");
            Note(25, 12.5f, "底版は函体の下で左右つながる");
            Title(25, 57, "断面(下流を見る)");

            // 側面。函体側面 x 54〜96、川裏端 S = 0 は x = 54。
            Solid(54, 20, 96, 20, 96, 34, 54, 34);
            Dashed(54, 22.5f, 96, 22.5f);
            Note(54, 37, "川裏端 S=0");
            Note(96, 37, "川表端");
            float c = upstream ? 64 : 86;
            float toe = upstream ? c - 7 : c + 7;
            float heel = upstream ? c + 13 : c - 13;
            Solid(System.Math.Min(toe, heel), 18.5f, System.Math.Max(toe, heel), 18.5f,
                System.Math.Max(toe, heel), 22.5f, System.Math.Min(toe, heel), 22.5f);
            Solid(c - 2, 22.5f, c + 2, 22.5f, c + 2, 46, c - 2, 46);
            Dashed(c, 14, c, 51);
            Dashed(54, 34, 54, 51);
            Dim(54, 49, c, 49, 0, "d");
            Dim(c - 2, 40, c + 2, 40, 0, "t");
            if (upstream)
            {
                Dim(toe, 18.5f, c - 2, 18.5f, -3, "Lt");
                Dim(c + 2, 18.5f, heel, 18.5f, -3, "Lh");
                Dim(heel, 18.5f, heel, 22.5f, -3, "tf");
            }
            else
            {
                Dim(c + 2, 18.5f, toe, 18.5f, -3, "Lt");
                Dim(heel, 18.5f, c - 2, 18.5f, -3, "Lh");
                Dim(heel, 18.5f, heel, 22.5f, 3, "tf");
            }
            Title(76, 57, "側面(函体の側面を見る)");
        }

        /// <summary>翼壁(U 字の 3 面張り水路): 平面と側面。川表側は川裏側の左右反転。</summary>
        public void Wing(bool upstream)
        {
            // 平面。函体端 x = 40、翼壁端部 x = 12、中心線 y = 31。
            mirrorAxis = upstream ? (float?)null : 30f;
            Solid(40, 20, 54, 20, 54, 42, 40, 42);
            Dashed(40, 24, 54, 24);
            Dashed(40, 38, 54, 38);
            Text(47, 31, "函体", OutlineColor);
            Fill(FloorColor, 40, 20, 12, 15, 12, 47, 40, 42);
            Solid(40, 38, 12, 43, 12, 47, 40, 42);
            Solid(40, 24, 12, 19, 12, 15, 40, 20);
            Note(23, 31, "底版(水路床)");
            Dim(12, 15, 40, 15, -3, "La");
            Dim(12, 19, 12, 43, 3, "W");
            Dim(12, 43, 12, 47, 3, "ts");
            Dim(37, 24, 37, 38, 0, "B");
            mirrorAxis = null;
            Note(30, 52, "開き角は B と W の差で決まる(W = B で平行)");
            Title(30, 57, upstream ? "平面(川裏側)" : "平面(川表側)");

            // 側面。Z = 0 は y = 16、底版上面(敷高)は y = 20。
            mirrorAxis = upstream ? (float?)null : 79f;
            Solid(90, 16, 97, 16, 97, 38, 90, 38);
            Hollow(90, 20, 97, 20, 97, 34, 90, 34);
            Solid(64, 16, 90, 16, 90, 20, 64, 20);
            Solid(64, 20, 90, 20, 90, 34, 64, 27);
            Note(93.5f, 41, "函体");
            Note(76, 24, "側壁");
            Dim(64, 16, 90, 16, -3, "La");
            Dim(64, 16, 64, 27, 3, "H1");
            Dim(87, 16, 87, 34, 0, "H0");
            Dim(70, 16, 70, 20, 0, "tf");
            mirrorAxis = null;
            Note(79, 46, "底版上面 = 函体の敷高");
            Note(79, 50, "H0・H1 は Z = 0(函体底版下面)から");
            Title(79, 57, "側面(側壁に沿って)");
        }

        /// <summary>堤防保護部: 平面(上が函体側)と側面。川裏側・川表側で同じ図(函体との向きで読む)。</summary>
        public void Protection()
        {
            // 平面。函体端 y = 46、翼壁端部 y = 22、中心線 x = 30。翼壁は八の字(内面 x 24/36 → 21/39)。
            Solid(19, 46, 41, 46, 41, 52, 19, 52);
            Dashed(24, 46, 24, 52);
            Dashed(36, 46, 36, 52);
            Note(30, 49, "函体");
            Fill(FloorColor, 21.5f, 46, 18.5f, 22, 41.5f, 22, 38.5f, 46);
            // 底版(たて壁の下 + かかと版)とつま先版は薄く、たて壁と翼壁の側壁は濃く塗る。
            Fill(FloorColor, 8, 22, 21, 22, 21, 31, 8, 31);
            Fill(FloorColor, 39, 22, 52, 22, 52, 31, 39, 31);
            Fill(FloorColor, 8, 18, 52, 18, 52, 22, 8, 22);
            Line(8, 31, 21, 31);
            Line(39, 31, 52, 31);
            Line(8, 18, 52, 18);
            Line(8, 18, 8, 31);
            Line(52, 18, 52, 31);
            Line(8, 22, 21, 22);
            Line(39, 22, 52, 22);
            Solid(24, 46, 21, 22, 18.5f, 22, 21.5f, 46);
            Solid(36, 46, 39, 22, 41.5f, 22, 38.5f, 46);
            Solid(8, 22, 21, 22, 21, 25, 8, 25);
            Solid(39, 22, 52, 22, 52, 25, 39, 25);
            Note(30, 34, "水路");
            Note(14.5f, 28, "かかと版");
            Note(30, 20, "つま先版");
            Dim(8, 18, 21, 18, -3, "X4");
            Dim(39, 18, 52, 18, -3, "X4");
            Dim(52, 18, 52, 22, -3, "L2");
            Dim(52, 22, 52, 25, -3, "L3");
            Dim(52, 25, 52, 31, -3, "L4");
            Note(30, 9, "X4 は端部の側壁内面から外向き");
            Title(30, 57, "平面(上が函体側)");

            // 側面。底版上面(敷高)は y = 20。翼壁端部は x = 70(たて壁の外側の面)、函体側は +x。
            Dashed(62, 20, 98, 20);
            Solid(64, 16, 92, 16, 92, 20, 64, 20);
            Solid(70, 20, 74, 20, 74, 38, 70, 38);
            Dashed(70, 38, 70, 44);
            Note(70, 46, "翼壁端部");
            Note(89, 41, "函体側 →");
            Dim(64, 16, 70, 16, -3, "L2");
            Dim(70, 16, 74, 16, -3, "L3");
            Dim(74, 16, 92, 16, -3, "L4");
            Dim(74, 20, 74, 38, -3, "Y3");
            Dim(92, 16, 92, 20, -3, "tf");
            Note(83, 24, "底版上面 = 敷高");
            Note(80, 51, "tf は翼壁の底版厚と同じ");
            Note(88, 33, "Y3 = H1 − 敷高");
            Title(80, 57, "側面(保護部を横から)");
        }

        /// <summary>操作台タブ: 正面(下流を見る)と側面(外側から見る)。記号は手本の樋門設計ソフトに合わせる。</summary>
        public void Gate()
        {
            GateFront();

            // 側面。左が川表、右が川裏。頂版上面は y = 8。門柱は縦方向壁(X4、川表側)と横方向壁(X5、川裏側)。
            Dashed(56, 8, 98, 8);
            Solid(70, 8, 79, 8, 79, 40, 70, 40);
            Solid(79, 8, 83, 8, 83, 40, 79, 40);
            Solid(63, 40, 90, 40, 90, 45, 63, 45);
            Dashed(86, 42.5f, 90, 42.5f);
            Dashed(86, 42.5f, 86, 45);
            Dim(63, 45, 90, 45, 6, "X1");
            Dim(86, 45, 90, 45, 2, "W1");
            Dim(90, 42.5f, 90, 45, -3, "H1");
            Dim(63, 40, 63, 45, 3, "Y1");
            Dim(63, 40, 70, 40, -2, "X3");
            Dim(83, 40, 90, 40, -2, "X3");
            Dim(70, 8, 70, 40, 3, "Y2");
            Dim(70, 8, 79, 8, -3, "X4");
            Dim(79, 8, 83, 8, -3, "X5");
            Note(60, 24, "川表");
            Note(93, 24, "川裏");
            Note(91, 10.5f, "頂版上面");
            Title(77, 57, "側面(外側から見る)");
        }

        /// <summary>門柱タブ: 正面(下流を見る)と門柱の平面(右の柱、上が川表)。</summary>
        public void GateColumn()
        {
            GateFront();

            // 平面。内空側面は x = 62、横方向壁幅 X8 は 62〜74、縦方向壁厚 X7 は 74〜86。
            // 横方向壁(X5)は川裏側(下)の y 14〜24、切欠きのある区間(X4)は川表側(上)の y 24〜46。
            Solid(62, 14, 86, 14, 86, 24, 62, 24);
            Solid(74, 24, 86, 24, 86, 46, 74, 46);
            Dashed(62, 10, 62, 50);
            // 土留壁は横方向壁(X5)と同じ厚さ・背面で、門柱の間(函体中心側)をふさぐ。
            Solid(54, 14, 62, 14, 62, 24, 54, 24);
            Note(57.5f, 19, "土留壁");
            // ハンチは操作台の下、縦方向壁の内面(切欠き側)に沿って X4 の区間に付く(平面では隠れ線)。
            Dashed(71, 24, 71, 46);
            Note(68, 28, "ハンチ");
            Note(68, 37, "切欠き");
            Note(68, 33, "(ゲート)");
            Note(80, 35, "縦方向壁");
            Note(74, 19, "横方向壁");
            Dim(62, 46, 74, 46, 2, "X8");
            Dim(74, 46, 86, 46, 2, "X7");
            Dim(86, 24, 86, 46, -3, "X4");
            Dim(86, 14, 86, 24, -3, "X5");
            Note(62, 52.5f, "内空側面");
            Note(56, 30, "← 中心");
            Note(94, 52.5f, "川表 ↑");
            Note(74, 9, "川裏");
            Note(74, 5, "背面 = 川表側胸壁のたて壁の背面");
            Title(77, 57, "門柱の平面(右の柱)");
        }

        /// <summary>
        /// 門柱・操作台の正面(下流を見る)。胸壁の底版の上面(敷高)は y = 6、頂版上面は y = 13、内空側面は x 15 / 35。
        /// 門柱は敷高から立つ(柱高さ Y2 も敷高から)。函体の側壁と重なる部分は一体。
        /// </summary>
        private void GateFront()
        {
            Dashed(3, 6, 47, 6);
            Dashed(3, 13, 47, 13);
            Note(4.5f, 8, "敷高");
            Note(4.5f, 15, "頂版");
            // 門柱は横方向壁の全幅(内側 X8 + 外側 X7)で見える。帯の境目が切欠きの縁。
            Solid(8, 6, 11.5f, 6, 11.5f, 40, 8, 40);
            Solid(11.5f, 6, 15, 6, 15, 40, 11.5f, 40);
            Solid(35, 6, 38.5f, 6, 38.5f, 40, 35, 40);
            Solid(38.5f, 6, 42, 6, 42, 40, 38.5f, 40);
            Solid(2, 40, 48, 40, 48, 45, 2, 45);
            // 土留壁(門柱の間、頂版上面から WH)。正面(川裏側から見る)では手前にある。
            Solid(15, 13, 35, 13, 35, 19, 15, 19);
            Dim(31, 13, 31, 19, 0, "WH");
            // ハンチ(縦方向壁の内面と操作台下面の隅の三角 1:1)は、手前の横方向壁に隠れるので隠れ線。
            Dashed(14, 40, 11.5f, 37.5f);
            Dashed(36, 40, 38.5f, 37.5f);
            Dim(15, 37.5f, 15, 40, -2.5f, "H2");
            Dashed(19, 42.5f, 31, 42.5f);
            Dashed(19, 42.5f, 19, 45);
            Dashed(31, 42.5f, 31, 45);
            Dim(2, 45, 48, 45, 6, "X2");
            Dim(19, 45, 31, 45, 2, "W2");
            Dim(48, 40, 48, 45, -3, "Y1");
            Dim(42, 6, 42, 40, -4, "Y2");
            Dim(42, 40, 48, 40, -2, "X6");
            Dim(15, 6, 35, 6, -2.5f, "B");
            Dim(35, 6, 38.5f, 6, -2.5f, "X8");
            Dim(38.5f, 6, 42, 6, -2.5f, "X7");
            Note(22, 16, "土留壁");
            Note(25, 30, "内空幅 B");
            Title(25, 57, "正面(下流を見る)");
        }

        /// <summary>しゃ水壁: 断面(下流を見る)と側面(Nc = 3 の例)。</summary>
        public void Cutoff()
        {
            Solid(7, 10, 43, 10, 43, 46, 7, 46);
            Solid(11, 14, 39, 14, 39, 42, 11, 42);
            Hollow(15, 18, 35, 18, 35, 38, 15, 38);
            Dim(25, 14, 25, 10, 0, "a");
            Dim(39, 30, 43, 30, 0, "a");
            Note(25, 50, "全周に一律で張り出す(下方にも)");
            Title(25, 57, "断面(下流を見る)");

            Solid(54, 20, 96, 20, 96, 34, 54, 34);
            Dashed(54, 23, 96, 23);
            Dashed(54, 31, 96, 31);
            foreach (float c in new float[] { 61, 75, 89 })
            {
                Solid(c - 1.5f, 16, c + 1.5f, 16, c + 1.5f, 38, c - 1.5f, 38);
                Dashed(c, 7, c, 16);
            }
            Dashed(54, 7, 54, 20);
            Dim(54, 9, 61, 9, 0, "L/2Nc");
            Dim(61, 9, 75, 9, 0, "L/Nc");
            Dim(87.5f, 38, 90.5f, 38, 3, "tc");
            Note(54, 37, "川裏端");
            Note(96, 37, "川表端");
            Note(75, 48, "延長 L を Nc 等分した各区間の中央に置く");
            Title(75, 57, "側面(Nc = 3 の例)");
        }

        /// <summary>床掘り断面(数量の概算用)。</summary>
        public void Excavation()
        {
            Fill(SoilColor, 15, 48, 30, 10, 70, 10, 85, 48);
            Line(15, 48, 30, 10);
            Line(30, 10, 70, 10);
            Line(70, 10, 85, 48);
            Line(3, 48, 15, 48);
            Line(85, 48, 97, 48);
            Fill(FloorColor, 30, 10, 70, 10, 70, 13, 30, 13);
            Line(30, 13, 70, 13);
            Solid(36, 13, 64, 13, 64, 33, 36, 33);
            Hollow(39, 16, 61, 16, 61, 30, 39, 30);
            Dim(64, 33, 64, 48, -4, "Hs");
            Dim(30, 16, 36, 16, 0, "m");
            Dim(70, 10, 70, 13, -3, "f");
            Text(19, 30, "1 : n", DimensionColor);
            Note(91, 50.5f, "現地盤");
            Note(50, 23, "函体");
            Title(50, 57, "床掘り断面(数量の概算用。底版勾配は考慮しない)");
        }

        // ---------------------------------------------------------------- 描画部品

        private System.Drawing.PointF P(float x, float y)
        {
            if (mirrorAxis.HasValue) { x = 2f * mirrorAxis.Value - x; }
            return new System.Drawing.PointF(left + x * scale, bottom - y * scale);
        }

        private System.Drawing.PointF[] Points(float[] xy)
        {
            System.Drawing.PointF[] pts = new System.Drawing.PointF[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++)
            {
                pts[i] = P(xy[2 * i], xy[2 * i + 1]);
            }
            return pts;
        }

        private void Fill(System.Drawing.Color color, params float[] xy)
        {
            using (System.Drawing.SolidBrush brush = new System.Drawing.SolidBrush(color))
            {
                g.FillPolygon(brush, Points(xy));
            }
        }

        /// <summary>コンクリート部材(塗り + 外形線)。</summary>
        private void Solid(params float[] xy)
        {
            Fill(ConcreteColor, xy);
            using (System.Drawing.Pen pen = new System.Drawing.Pen(OutlineColor, 1.5f))
            {
                g.DrawPolygon(pen, Points(xy));
            }
        }

        /// <summary>内空など抜けている部分(白塗り + 外形線)。</summary>
        private void Hollow(params float[] xy)
        {
            Fill(System.Drawing.Color.White, xy);
            using (System.Drawing.Pen pen = new System.Drawing.Pen(OutlineColor, 1.2f))
            {
                g.DrawPolygon(pen, Points(xy));
            }
        }

        private void Line(float x1, float y1, float x2, float y2)
        {
            using (System.Drawing.Pen pen = new System.Drawing.Pen(OutlineColor, 1.5f))
            {
                g.DrawLine(pen, P(x1, y1), P(x2, y2));
            }
        }

        /// <summary>中心線・隠れ線・基準線。</summary>
        private void Dashed(float x1, float y1, float x2, float y2)
        {
            using (System.Drawing.Pen pen = new System.Drawing.Pen(NoteColor, 1f))
            {
                pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                g.DrawLine(pen, P(x1, y1), P(x2, y2));
            }
        }

        private void Point(float x, float y)
        {
            System.Drawing.PointF c = P(x, y);
            using (System.Drawing.SolidBrush brush = new System.Drawing.SolidBrush(System.Drawing.Color.Red))
            {
                g.FillEllipse(brush, c.X - 3.5f, c.Y - 3.5f, 7f, 7f);
            }
        }

        /// <summary>
        /// 寸法線。(x1,y1)→(x2,y2) を測り、進行方向の左側へ offset だけ離して両矢印の線を引く(負なら右側)。
        /// offset 0 は部材の内側に直接引く(引出し線なし)。記号は寸法線のさらに外側に置く。
        /// </summary>
        private void Dim(float x1, float y1, float x2, float y2, float offset, string label)
        {
            float dx = x2 - x1;
            float dy = y2 - y1;
            float len = (float)System.Math.Sqrt(dx * dx + dy * dy);
            float nx = -dy / len;
            float ny = dx / len;
            float ax = x1 + nx * offset;
            float ay = y1 + ny * offset;
            float bx = x2 + nx * offset;
            float by = y2 + ny * offset;

            using (System.Drawing.Pen thin = new System.Drawing.Pen(DimensionColor, 1f))
            {
                if (offset != 0f)
                {
                    float over = System.Math.Sign(offset) * 0.8f;
                    g.DrawLine(thin, P(x1, y1), P(ax + nx * over, ay + ny * over));
                    g.DrawLine(thin, P(x2, y2), P(bx + nx * over, by + ny * over));
                }
            }
            using (System.Drawing.Pen pen = new System.Drawing.Pen(DimensionColor, 1.2f))
            using (System.Drawing.Drawing2D.AdjustableArrowCap cap = new System.Drawing.Drawing2D.AdjustableArrowCap(3.5f, 4.5f))
            {
                pen.CustomStartCap = cap;
                pen.CustomEndCap = cap;
                g.DrawLine(pen, P(ax, ay), P(bx, by));
            }

            float side = offset < 0f ? -1f : 1f;
            Text((ax + bx) / 2f + nx * 2.4f * side, (ay + by) / 2f + ny * 2.4f * side, label, DimensionColor);
        }

        private void Title(float x, float y, string text)
        {
            using (System.Drawing.Font bold = new System.Drawing.Font(font, System.Drawing.FontStyle.Bold))
            {
                Text(x, y, text, System.Drawing.Color.Black, bold);
            }
        }

        private void Note(float x, float y, string text)
        {
            Text(x, y, text, NoteColor);
        }

        private void Text(float x, float y, string text, System.Drawing.Color color)
        {
            Text(x, y, text, color, font);
        }

        /// <summary>(x, y) を中心に文字を置く。線と重なっても読めるよう白の下地を敷く。</summary>
        private void Text(float x, float y, string text, System.Drawing.Color color, System.Drawing.Font f)
        {
            System.Drawing.PointF c = P(x, y);
            System.Drawing.SizeF size = g.MeasureString(text, f);
            System.Drawing.RectangleF box = new System.Drawing.RectangleF(
                c.X - size.Width / 2f, c.Y - size.Height / 2f, size.Width, size.Height);
            using (System.Drawing.SolidBrush back = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(220, 255, 255, 255)))
            using (System.Drawing.SolidBrush fore = new System.Drawing.SolidBrush(color))
            {
                g.FillRectangle(back, box.X + 2f, box.Y + 1f, box.Width - 4f, box.Height - 2f);
                g.DrawString(text, f, fore, box.Location);
            }
        }
    }
}
