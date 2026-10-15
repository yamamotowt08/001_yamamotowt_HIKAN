// === 参照DLLバージョン検証済み(2026-10-07) === (詳細は HikanSolidBuilder.cs 先頭を参照。scripts/verify-dll-versions.ps1 で検証)
namespace Hikan.AutoCAD
{
    /// <summary>
    /// パラメータ入力画面。構造物ごとのタブ(函体 / 胸壁 / 翼壁 / しゃ水壁 / 門柱・操作台 / 床掘り・色)で入力する。
    /// 翼壁のタブは内側に「基本条件 / 形状寸法 / 堤防保護部」のタブを持ち、堤防保護部のタブは
    /// 基本条件で「設置する」を選んだときだけ表示する。
    /// 入力項目と意味はコマンドライン入力(HikanPrompt)と同じ。基準点はここでは扱わない。
    /// 確定時に Core の整合性チェック(HikanValidator.ValidateInput)を通し、エラーがあれば画面を閉じずに
    /// 該当欄へ印を付けてそのタブへ切り替える。
    /// </summary>
    internal sealed class HikanDialog : System.Windows.Forms.Form
    {
        /// <summary>入力欄 1 つ。Key は HikanValidator のエラー文の書き出し(該当欄の特定に使う)。</summary>
        private sealed class Field
        {
            public string Name;
            public string Key;
            public bool Integer;
            public System.Func<Hikan.Core.HikanParameters, decimal> Get;
            public System.Action<Hikan.Core.HikanParameters, decimal> Set;
            public System.Windows.Forms.TextBox Box;
            public Page Page;
            /// <summary>翼壁タブの内側のタブ(基本条件 / 形状寸法 / 堤防保護部)に置いた欄なら、そのタブ。</summary>
            public System.Windows.Forms.TabPage InnerTab;
        }

        /// <summary>タブ 1 枚。Presence は「0 で設置しない」欄(しゃ水壁の枚数)で、0 のとき他の欄を無効にする。</summary>
        private sealed class Page
        {
            public string Title;
            public string Prefix;
            public System.Windows.Forms.TabPage Tab;
            /// <summary>AddField が欄を追加する入力表(と、それを置いた内側のタブ)。</summary>
            public System.Windows.Forms.TableLayoutPanel Grid;
            public System.Windows.Forms.TabPage InnerTab;
            public Field Presence;
            /// <summary>欄以外の入力(選択ボタンなど)をパラメータに反映する。</summary>
            public System.Action<Hikan.Core.HikanParameters> Apply;
            public System.Collections.Generic.List<Field> Fields = new System.Collections.Generic.List<Field>();
        }

        private readonly Hikan.Core.HikanParameters source;
        private readonly System.Collections.Generic.List<Page> pages = new System.Collections.Generic.List<Page>();
        private readonly System.Collections.Generic.List<Field> errorTargets = new System.Collections.Generic.List<Field>();
        private readonly System.Collections.Generic.List<Page> errorPages = new System.Collections.Generic.List<Page>();
        private readonly System.Windows.Forms.TabControl tabs = new System.Windows.Forms.TabControl();
        private readonly System.Windows.Forms.ListBox errorList = new System.Windows.Forms.ListBox();
        private readonly System.Windows.Forms.ErrorProvider marker = new System.Windows.Forms.ErrorProvider();

        /// <summary>確定したパラメータ(基準点は元の値のまま)。取消時は null。</summary>
        public Hikan.Core.HikanParameters Result { get; private set; }

        /// <summary>入力画面を開く。確定したらパラメータ(新しいインスタンス)、取消なら null を返す。</summary>
        public static Hikan.Core.HikanParameters Edit(Hikan.Core.HikanParameters p, string title)
        {
            using (HikanDialog dialog = new HikanDialog(p, title))
            {
                System.Windows.Forms.DialogResult r = Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(dialog);
                return r == System.Windows.Forms.DialogResult.OK ? dialog.Result : null;
            }
        }

        private HikanDialog(Hikan.Core.HikanParameters p, string title)
        {
            source = p;
            AutoScaleDimensions = new System.Drawing.SizeF(96f, 96f);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            Font = new System.Drawing.Font("Meiryo UI", 9f);
            Text = title;
            ClientSize = new System.Drawing.Size(1000, 640);
            MinimumSize = new System.Drawing.Size(820, 560);
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;

            BuildPages();

            tabs.Dock = System.Windows.Forms.DockStyle.Fill;
            foreach (Page page in pages) { tabs.TabPages.Add(page.Tab); }

            errorList.Dock = System.Windows.Forms.DockStyle.Fill;
            errorList.ForeColor = System.Drawing.Color.Firebrick;
            errorList.IntegralHeight = false;
            errorList.SelectedIndexChanged += delegate { ShowError(errorList.SelectedIndex); };
            marker.BlinkStyle = System.Windows.Forms.ErrorBlinkStyle.NeverBlink;

            System.Windows.Forms.Button ok = new System.Windows.Forms.Button();
            ok.Text = "確定";
            ok.Size = new System.Drawing.Size(100, 30);
            ok.Click += OnOk;
            System.Windows.Forms.Button cancel = new System.Windows.Forms.Button();
            cancel.Text = "取消";
            cancel.Size = new System.Drawing.Size(100, 30);
            cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            AcceptButton = ok;
            CancelButton = cancel;

            System.Windows.Forms.FlowLayoutPanel buttons = new System.Windows.Forms.FlowLayoutPanel();
            buttons.Dock = System.Windows.Forms.DockStyle.Fill;
            buttons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);

            System.Windows.Forms.Label errorTitle = new System.Windows.Forms.Label();
            errorTitle.Text = "入力チェック(確定時に実行。行を選ぶと該当欄へ移動)";
            errorTitle.AutoSize = true;
            errorTitle.Margin = new System.Windows.Forms.Padding(3, 6, 3, 0);

            System.Windows.Forms.TableLayoutPanel root = new System.Windows.Forms.TableLayoutPanel();
            root.Dock = System.Windows.Forms.DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
            root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 80f));
            root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 42f));
            root.Controls.Add(tabs, 0, 0);
            root.Controls.Add(errorTitle, 0, 1);
            root.Controls.Add(errorList, 0, 2);
            root.Controls.Add(buttons, 0, 3);
            Controls.Add(root);

            foreach (Page page in pages) { UpdateEnabled(page); }
        }

        // ---------------------------------------------------------------- タブの定義

        private void BuildPages()
        {
            Page b = AddPage("函体", null, s => s.Barrel(),
                "部材厚は 0.40 m 以上・0.10 m ピッチ(基準)。内空幅・内空高は 3.0 m 程度以下(適用範囲)。"
                + "勾配は押出し後の回転で表すので、両端面はわずかに傾く。ブロック数は数量の按分表示のみ。");
            AddField(b, "B", "内空幅", "m", "内空幅 B", q => q.InnerWidth, (q, v) => q.InnerWidth = v);
            AddField(b, "H", "内空高", "m", "内空高 H", q => q.InnerHeight, (q, v) => q.InnerHeight = v);
            AddField(b, "tw", "側壁厚", "m", "側壁厚", q => q.WallThickness, (q, v) => q.WallThickness = v);
            AddField(b, "tt", "頂版厚", "m", "頂版厚", q => q.TopSlabThickness, (q, v) => q.TopSlabThickness = v);
            AddField(b, "tb", "底版厚", "m", "底版厚", q => q.BottomSlabThickness, (q, v) => q.BottomSlabThickness = v);
            AddField(b, "L", "函体延長(斜距離)", "m", "函体延長 L", q => q.BarrelLength, (q, v) => q.BarrelLength = v);
            AddField(b, "i", "底版勾配(下流下がり)", "―", "底版勾配 i", q => q.BottomSlope, (q, v) => q.BottomSlope = v);
            AddIntField(b, "―", "ブロック数", "", "ブロック数", q => q.BlockCount, (q, v) => q.BlockCount = v);

            AddBreastPage("川裏側胸壁", true, q => q.UpstreamBreast);
            AddBreastPage("川表側胸壁", false, q => q.DownstreamBreast);
            AddWingPage("川裏側翼壁", true, q => q.UpstreamWing);
            AddWingPage("川表側翼壁", false, q => q.DownstreamWing);

            Page c = AddPage("しゃ水壁", "しゃ水壁", s => s.Cutoff(),
                "枚数 0 で設置しない。函体延長を枚数で等分した各区間の中央に置く。");
            AddIntField(c, "Nc", "枚数(0 で設置しない)", "枚", "しゃ水壁の枚数", q => q.CutoffCount, (q, v) => q.CutoffCount = v);
            c.Presence = c.Fields[0];
            AddField(c, "tc", "厚", "m", "しゃ水壁厚", q => q.CutoffThickness, (q, v) => q.CutoffThickness = v);
            AddField(c, "a", "張出し量(全周一律)", "m", "しゃ水壁の張出し量", q => q.CutoffProjection, (q, v) => q.CutoffProjection = v);

            AddGatePage();

            Page e = AddPage("床掘り・色", null, s => s.Excavation(),
                "床掘り・埋戻しは水平床掘りとした概算。色は AutoCAD インデックスカラー(ACI 0〜256)。");
            AddField(e, "Hs", "土かぶり(頂版上面〜現地盤)", "m", "土かぶり", q => q.SoilCover, (q, v) => q.SoilCover = v);
            AddField(e, "m", "床掘り余裕幅(片側)", "m", "床掘り余裕幅", q => q.ExcavationMargin, (q, v) => q.ExcavationMargin = v);
            AddField(e, "n", "床掘り法勾配 1:n の n", "―", "床掘り法勾配", q => q.ExcavationSlope, (q, v) => q.ExcavationSlope = v);
            AddField(e, "f", "基礎材厚(均しコン等)", "m", "基礎材厚", q => q.FoundationThickness, (q, v) => q.FoundationThickness = v);
            AddIntField(e, "―", "ソリッド色(ACI)", "", "色番号", q => q.ColorIndex, (q, v) => q.ColorIndex = v);
        }

        private void AddBreastPage(
            string title,
            bool upstream,
            System.Func<Hikan.Core.HikanParameters, Hikan.Core.HikanBreastWall> wall)
        {
            Page w = AddPage(title, title, s => s.Breast(upstream),
                "函体の左右側面に 1 基ずつ取り付く逆T字擁壁(左右同一)。たて壁厚・底版厚は 0.40 m 以上・0.10 m ピッチ。"
                + "底版上面は函体の敷高にそろえ、たて壁は敷高から立ち上がる。左右の底版は函体の下を通って 1 枚につながる。"
                + "d は川裏函体端からたて壁の軸(厚さの中心)まで。つま先版は近い方の函体端の側、かかと版は函体中央の側。"
                + "つま先版長・かかと版長のどちらかを 0 にすると L 字。"
                + (upstream ? "土留壁(WH・WT)は左右のたて壁の間をふさぐ壁で、下端は頂版上面、背面はたて壁の背面(函体中央側の面)。"
                    + "WT は 0.40 m 以上・0.10 m ピッチ。" : ""));
            AddField(w, "t", "たて壁厚", "m", title + "のたて壁厚",
                q => wall(q).StemThickness, (q, v) => wall(q).StemThickness = v);
            AddField(w, "d", "たて壁の軸位置", "m", title + "のたて壁(",
                q => wall(q).Position, (q, v) => wall(q).Position = v);
            AddField(w, "Lw", "張出し長(函体側面から)", "m", title + "の張出し長",
                q => wall(q).Length, (q, v) => wall(q).Length = v);
            AddField(w, "Hc", "天端高(Z = 0 から)", "m", title + "の天端高",
                q => wall(q).CrownHeight, (q, v) => wall(q).CrownHeight = v);
            AddField(w, "tf", "底版厚(上面は敷高)", "m", title + "の底版厚",
                q => wall(q).FootingThickness, (q, v) => wall(q).FootingThickness = v);
            AddField(w, "Lt", "つま先版長", "m", title + "のつま先版長",
                q => wall(q).ToeLength, (q, v) => wall(q).ToeLength = v);
            AddField(w, "Lh", "かかと版長", "m", title + "のかかと版長",
                q => wall(q).HeelLength, (q, v) => wall(q).HeelLength = v);
            if (upstream)
            {
                // 川裏側の土留壁(左右のたて壁の間、頂版の上。背面 = たて壁の背面、厚さは川裏側へ)
                AddField(w, "WH", "土留壁高さ(頂版上面から、0 でなし)", "m", "川裏側土留壁の高さ",
                    q => q.UpstreamRetainingWall.Height, (q, v) => q.UpstreamRetainingWall.Height = v);
                AddField(w, "WT", "土留壁厚(背面から川裏側へ)", "m", "川裏側土留壁の厚さ",
                    q => q.UpstreamRetainingWall.Thickness, (q, v) => q.UpstreamRetainingWall.Thickness = v);
            }
        }

        /// <summary>
        /// 翼壁のタブ。内側に「基本条件 / 形状寸法 / 堤防保護部」のタブを持つ。
        /// 形式は U 型だけ実装済みなので、他の形式は表示だけして選べないようにする。
        /// 堤防保護部のタブは「設置する」のときだけ出し、「設置しない」なら欄を無効にして値は保持する。
        /// </summary>
        private void AddWingPage(
            string title,
            bool upstream,
            System.Func<Hikan.Core.HikanParameters, Hikan.Core.HikanWingWall> wing)
        {
            Page w = new Page();
            w.Title = title;
            w.Prefix = title;
            w.Tab = new System.Windows.Forms.TabPage(title);
            pages.Add(w);

            System.Windows.Forms.TabControl inner = new System.Windows.Forms.TabControl();
            inner.Dock = System.Windows.Forms.DockStyle.Fill;
            w.Tab.Controls.Add(inner);

            System.Windows.Forms.TabPage basic = new System.Windows.Forms.TabPage("基本条件");
            System.Windows.Forms.TabPage shape = new System.Windows.Forms.TabPage("形状寸法");
            System.Windows.Forms.TabPage protection = new System.Windows.Forms.TabPage("堤防保護部");
            inner.TabPages.Add(basic);
            inner.TabPages.Add(shape);

            shape.Controls.Add(BuildSheet(s => s.Wing(upstream),
                "左右の側壁と底版が一体の U 字の 3 面張り水路(左右同一)。側壁厚・底版厚は 0.40 m 以上・0.10 m ピッチ。"
                + "底版上面は函体の敷高に合わせる。開き角は内空幅 B と端部の水路幅 W の差で決まる。", out w.Grid));
            w.InnerTab = shape;
            AddField(w, "ts", "側壁厚", "m", title + "の側壁厚",
                q => wing(q).StemThickness, (q, v) => wing(q).StemThickness = v);
            AddField(w, "La", "翼壁長(函体軸方向)", "m", title + "の翼壁長",
                q => wing(q).Length, (q, v) => wing(q).Length = v);
            AddField(w, "W", "端部の水路幅", "m", title + "の端部水路幅",
                q => wing(q).EndWidth, (q, v) => wing(q).EndWidth = v);
            AddField(w, "H0", "側壁の天端高(函体端)", "m", title + "の函体端での天端高",
                q => wing(q).CrownStart, (q, v) => wing(q).CrownStart = v);
            AddField(w, "H1", "側壁の天端高(端部)", "m", title + "の端部での天端高",
                q => wing(q).CrownEnd, (q, v) => wing(q).CrownEnd = v);
            AddField(w, "tf", "底版厚", "m", title + "の底版厚",
                q => wing(q).FloorThickness, (q, v) => wing(q).FloorThickness = v);

            string name = (upstream ? "川裏側" : "川表側") + "堤防保護部";
            protection.Controls.Add(BuildSheet(s => s.Protection(),
                "翼壁の端部から左右へ張り出す逆T字擁壁(左右同一)。たて壁は外側の面を翼壁端部にそろえる。"
                + "X4 は翼壁端部の側壁内面から外向き(側壁厚を含む)。底版は上面 = 敷高、厚さ = 翼壁の底版厚。"
                + "つま先版は水路床の下も含めて左右つながる。たて壁高 Y3 は入力せず、たて壁の上面を翼壁端部の天端 H1 にそろえる。", out w.Grid));
            w.InnerTab = protection;
            int first = w.Fields.Count;
            AddField(w, "X4", "張出し幅(端部の側壁内面から)", "m", name + "の張出し幅",
                q => wing(q).Protection.Width, (q, v) => wing(q).Protection.Width = v);
            AddField(w, "L3", "たて壁厚", "m", name + "のたて壁厚",
                q => wing(q).Protection.StemThickness, (q, v) => wing(q).Protection.StemThickness = v);
            AddField(w, "L2", "つま先版長(端部より外側)", "m", name + "のつま先版長",
                q => wing(q).Protection.ToeLength, (q, v) => wing(q).Protection.ToeLength = v);
            AddField(w, "L4", "かかと版長(函体側)", "m", name + "のかかと版",
                q => wing(q).Protection.HeelLength, (q, v) => wing(q).Protection.HeelLength = v);
            System.Collections.Generic.List<Field> protectionFields = w.Fields.GetRange(first, w.Fields.Count - first);

            // Y3 = H1 − 敷高(函体の底版厚)は計算値なので表示だけする。H1・底版厚を書き換えたら表示し直す。
            Field crownEnd = w.Fields.Find(f => f.Name == "側壁の天端高(端部)");
            Field slab = pages[0].Fields.Find(f => f.Name == "底版厚");
            System.Windows.Forms.Label y3 = GridLabel("", System.Drawing.Color.DimGray);
            int y3Row = w.Grid.RowStyles.Count;
            w.Grid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            w.Grid.Controls.Add(y3, 0, y3Row);
            w.Grid.SetColumnSpan(y3, 4);
            System.Action showY3 = delegate
            {
                decimal h1, tb;
                y3.Text = TryRead(crownEnd, out h1) && TryRead(slab, out tb)
                    ? "Y3 たて壁高(自動)= H1 − 敷高 = " + (h1 - tb).ToString("0.000###", System.Globalization.CultureInfo.InvariantCulture) + " m"
                    : "Y3 たて壁高(自動)= H1 − 敷高";
            };
            crownEnd.Box.TextChanged += delegate { showY3(); };
            slab.Box.TextChanged += delegate { showY3(); };
            showY3();

            System.Windows.Forms.RadioButton install = Choice("設置する", true);
            System.Windows.Forms.RadioButton none = Choice("設置しない", true);
            install.Checked = wing(source).Protection.Exists;
            none.Checked = !install.Checked;
            System.Action show = delegate
            {
                bool on = install.Checked;
                if (on && !inner.TabPages.Contains(protection)) { inner.TabPages.Add(protection); }
                if (!on && inner.TabPages.Contains(protection)) { inner.TabPages.Remove(protection); }
                foreach (Field f in protectionFields) { f.Box.Enabled = on; }
            };
            install.CheckedChanged += delegate { show(); };
            show();
            w.Apply = q => wing(q).Protection.Exists = install.Checked;

            System.Windows.Forms.RadioButton u = Choice("U型", true);
            u.Checked = true;
            System.Windows.Forms.Label later = new System.Windows.Forms.Label();
            later.Text = "U型以外は今後対応予定";
            later.ForeColor = System.Drawing.Color.DimGray;
            later.AutoSize = true;
            later.Margin = new System.Windows.Forms.Padding(12, 9, 3, 3);

            System.Windows.Forms.FlowLayoutPanel column = new System.Windows.Forms.FlowLayoutPanel();
            column.Dock = System.Windows.Forms.DockStyle.Fill;
            column.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            column.WrapContents = false;
            column.Padding = new System.Windows.Forms.Padding(12);
            column.Controls.Add(Group("形式", Choice("逆T型", false), u, Choice("U型張り出し", false), Choice("U型底版傾斜", false), later));
            column.Controls.Add(Group("堤防保護部", install, none));
            basic.Controls.Add(column);
        }

        /// <summary>
        /// 門柱・操作台のタブ。内側に「基本条件 / 操作台 / 門柱」のタブを持つ(記号は手本の樋門設計ソフトに合わせる)。
        /// 基本条件は 設置する/しない・門柱形式・操作台形状形式・管理橋の設置。門柱形式は 2 本柱、操作台形状形式は
        /// 前後・左右に張り出す単純な版だけ実装済みなので、他は表示だけして選べないようにする。
        /// 「設置しない」なら寸法の欄を、管理橋「しない」なら受け台の欄を無効にする(値は保持)。
        /// </summary>
        private void AddGatePage()
        {
            const string title = "門柱・操作台";
            Page g = new Page();
            g.Title = title;
            g.Prefix = title;
            g.Tab = new System.Windows.Forms.TabPage(title);
            pages.Add(g);

            System.Windows.Forms.TabControl inner = new System.Windows.Forms.TabControl();
            inner.Dock = System.Windows.Forms.DockStyle.Fill;
            g.Tab.Controls.Add(inner);
            System.Windows.Forms.TabPage basic = new System.Windows.Forms.TabPage("基本条件");
            System.Windows.Forms.TabPage deck = new System.Windows.Forms.TabPage("操作台");
            System.Windows.Forms.TabPage column = new System.Windows.Forms.TabPage("門柱");
            inner.TabPages.Add(basic);
            inner.TabPages.Add(deck);
            inner.TabPages.Add(column);

            deck.Controls.Add(BuildSheet(s => s.Gate(),
                "操作台は両方の門柱にまたがる版。X1 = X4 + X5 + 2 × X3、X2 = 内空幅 B + 2 × (X7 + X8) + 2 × X6 になるよう入力する"
                + "(1 mm を超えて食い違うとエラー)。受け台(W1・W2・H1)は管理橋の桁を受ける切欠きで、操作台の川裏側の端の上面を欠く。"
                + "Y1 は 0.40 m 以上・0.10 m ピッチ。", out g.Grid));
            g.InnerTab = deck;
            AddField(g, "X1", "操作台縦方向幅", "m", title + "の操作台縦方向幅",
                q => q.Gate.DeckLength, (q, v) => q.Gate.DeckLength = v);
            AddField(g, "X2", "操作台横方向幅", "m", title + "の操作台横方向幅",
                q => q.Gate.DeckWidth, (q, v) => q.Gate.DeckWidth = v);
            AddField(g, "X3", "操作台縦方向張出長", "m", title + "の操作台縦方向張出長",
                q => q.Gate.DeckAxialOverhang, (q, v) => q.Gate.DeckAxialOverhang = v);
            AddField(g, "X6", "操作台横方向張出長", "m", title + "の操作台横方向張出長",
                q => q.Gate.DeckSideOverhang, (q, v) => q.Gate.DeckSideOverhang = v);
            AddField(g, "Y1", "操作台高さ", "m", title + "の操作台高さ",
                q => q.Gate.DeckThickness, (q, v) => q.Gate.DeckThickness = v);
            int seatFirst = g.Fields.Count;
            AddField(g, "W1", "操作台受け台縦方向幅", "m", title + "の操作台受け台縦方向幅",
                q => q.Gate.SeatLength, (q, v) => q.Gate.SeatLength = v);
            AddField(g, "W2", "操作台受け台横方向幅", "m", title + "の操作台受け台横方向幅",
                q => q.Gate.SeatWidth, (q, v) => q.Gate.SeatWidth = v);
            AddField(g, "H1", "操作台受け台高さ", "m", title + "の操作台受け台高さ",
                q => q.Gate.SeatDepth, (q, v) => q.Gate.SeatDepth = v);
            System.Collections.Generic.List<Field> seatFields = g.Fields.GetRange(seatFirst, 3);

            column.Controls.Add(BuildSheet(s => s.GateColumn(),
                "門柱は函体の左右の側壁の上に立つ L 字の柱(左右同一)。内側の切欠き(X8 × X4)がゲートの溝で、横方向壁(X5)は川裏側。"
                + "内面は函体の内空側面にそろえる。門柱の位置は入力しない: 門柱の背面(川裏側の面)を川表側胸壁のたて壁の背面にそろえる。"
                + "門柱は胸壁の底版の上面(敷高)から立ち、柱高さ Y2 もそこから測る。"
                + "X5・X7 は 0.40 m 以上・0.10 m ピッチ。ハンチ(H2)は縦方向壁の内面(切欠き側)と操作台下面の隅の三角(1:1)で、"
                + "縦方向壁の区間(川表側の柱)に付く。0 でハンチなし。"
                + "土留壁(WH)は左右の門柱の間をふさぐ壁で、下端は頂版上面、背面は門柱の背面、厚さは X5。0 で土留壁なし。", out g.Grid));
            g.InnerTab = column;
            AddField(g, "X4", "縦方向壁幅", "m", title + "の縦方向壁幅",
                q => q.Gate.LongitudinalWallWidth, (q, v) => q.Gate.LongitudinalWallWidth = v);
            AddField(g, "X5", "横方向壁厚", "m", title + "の横方向壁厚",
                q => q.Gate.TransverseWallThickness, (q, v) => q.Gate.TransverseWallThickness = v);
            AddField(g, "X7", "縦方向壁厚", "m", title + "の縦方向壁厚",
                q => q.Gate.LongitudinalWallThickness, (q, v) => q.Gate.LongitudinalWallThickness = v);
            AddField(g, "X8", "横方向壁幅", "m", title + "の横方向壁幅",
                q => q.Gate.TransverseWallWidth, (q, v) => q.Gate.TransverseWallWidth = v);
            AddField(g, "Y2", "柱高さ(胸壁底版の上面から)", "m", title + "の柱高さ",
                q => q.Gate.ColumnHeight, (q, v) => q.Gate.ColumnHeight = v);
            AddField(g, "H2", "ハンチ高(川表側の柱の内面)", "m", title + "のハンチ高",
                q => q.Gate.HaunchHeight, (q, v) => q.Gate.HaunchHeight = v);
            AddField(g, "WH", "土留壁高さ(頂版上面から)", "m", title + "の土留壁高さ",
                q => q.Gate.RetainingWallHeight, (q, v) => q.Gate.RetainingWallHeight = v);

            System.Windows.Forms.RadioButton install = Choice("設置する", true);
            System.Windows.Forms.RadioButton none = Choice("設置しない", true);
            install.Checked = source.Gate.Exists;
            none.Checked = !install.Checked;
            System.Windows.Forms.RadioButton bridge = Choice("する", true);
            System.Windows.Forms.RadioButton noBridge = Choice("しない", true);
            bridge.Checked = source.Gate.Bridge;
            noBridge.Checked = !bridge.Checked;
            System.Action show = delegate
            {
                foreach (Field f in g.Fields)
                {
                    f.Box.Enabled = install.Checked && (bridge.Checked || !seatFields.Contains(f));
                }
            };
            install.CheckedChanged += delegate { show(); };
            bridge.CheckedChanged += delegate { show(); };
            show();
            g.Apply = q =>
            {
                q.Gate.Exists = install.Checked;
                q.Gate.Bridge = bridge.Checked;
            };

            System.Windows.Forms.RadioButton twoColumns = IconChoice("2 本柱", true, HikanIcons.TwoColumns);
            twoColumns.Checked = true;
            System.Windows.Forms.RadioButton standardDeck = IconChoice("形式 4", true, HikanIcons.DeckType4);
            standardDeck.Checked = true;

            System.Windows.Forms.FlowLayoutPanel list = new System.Windows.Forms.FlowLayoutPanel();
            list.Dock = System.Windows.Forms.DockStyle.Fill;
            list.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            list.WrapContents = false;
            list.AutoScroll = true;
            list.Padding = new System.Windows.Forms.Padding(12);
            list.Controls.Add(Group("門柱・操作台", install, none));
            list.Controls.Add(Group("門柱形式", twoColumns, IconChoice("3 本柱", false, HikanIcons.ThreeColumns), Later()));
            list.Controls.Add(Group("操作台形状形式",
                IconChoice("形式 1", false, HikanIcons.DeckType1), IconChoice("形式 2", false, HikanIcons.DeckType2),
                IconChoice("形式 3", false, HikanIcons.DeckType3), standardDeck,
                IconChoice("形式 5", false, HikanIcons.DeckType5), IconChoice("形式 6", false, HikanIcons.DeckType6), Later()));
            list.Controls.Add(Group("管理橋の設置", noBridge, bridge));
            basic.Controls.Add(list);
        }

        /// <summary>絵付きの選択ボタン(手本の画面のように形を絵で示す)。</summary>
        private static System.Windows.Forms.RadioButton IconChoice(string text, bool enabled, System.Action<System.Drawing.Graphics> draw)
        {
            System.Windows.Forms.RadioButton r = Choice(text, enabled);
            r.Image = HikanIcons.Render(draw, enabled);
            r.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            r.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            r.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            return r;
        }

        private static System.Windows.Forms.Label Later()
        {
            System.Windows.Forms.Label later = new System.Windows.Forms.Label();
            later.Text = "灰色の形式は今後対応予定";
            later.ForeColor = System.Drawing.Color.DimGray;
            later.AutoSize = true;
            later.Margin = new System.Windows.Forms.Padding(12, 9, 3, 3);
            return later;
        }

        private static System.Windows.Forms.RadioButton Choice(string text, bool enabled)
        {
            System.Windows.Forms.RadioButton r = new System.Windows.Forms.RadioButton();
            r.Text = text;
            r.Enabled = enabled;
            r.AutoSize = true;
            r.Margin = new System.Windows.Forms.Padding(6, 6, 18, 3);
            return r;
        }

        /// <summary>枠付きの選択欄。中の選択ボタンは同じ枠の中で排他になる。</summary>
        private static System.Windows.Forms.GroupBox Group(string text, params System.Windows.Forms.Control[] items)
        {
            System.Windows.Forms.FlowLayoutPanel row = new System.Windows.Forms.FlowLayoutPanel();
            row.Dock = System.Windows.Forms.DockStyle.Fill;
            row.AutoSize = true;
            row.WrapContents = true;
            row.Controls.AddRange(items);

            System.Windows.Forms.GroupBox box = new System.Windows.Forms.GroupBox();
            box.Text = text;
            box.AutoSize = true;
            box.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            box.MinimumSize = new System.Drawing.Size(520, 0);
            box.Padding = new System.Windows.Forms.Padding(8, 6, 8, 8);
            box.Margin = new System.Windows.Forms.Padding(3, 3, 3, 14);
            box.Controls.Add(row);
            return box;
        }

        /// <summary>タブを追加する。左に模式図、右に入力欄(記号 / 名称 / 値 / 単位)と注記。</summary>
        private Page AddPage(string title, string prefix, System.Action<HikanSketch> figure, string note)
        {
            Page page = new Page();
            page.Title = title;
            page.Prefix = prefix;
            page.Tab = new System.Windows.Forms.TabPage(title);
            page.Tab.Controls.Add(BuildSheet(figure, note, out page.Grid));
            pages.Add(page);
            return page;
        }

        /// <summary>模式図(左)と入力表・注記(右)の 1 画面。入力表は grid に返す。</summary>
        private System.Windows.Forms.Control BuildSheet(
            System.Action<HikanSketch> figure, string note, out System.Windows.Forms.TableLayoutPanel grid)
        {
            System.Windows.Forms.PictureBox picture = new System.Windows.Forms.PictureBox();
            picture.Dock = System.Windows.Forms.DockStyle.Fill;
            picture.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            picture.Paint += (s, e) => HikanSketch.Draw(e.Graphics, picture.ClientRectangle, Font, figure);
            picture.Resize += delegate { picture.Invalidate(); };

            grid = new System.Windows.Forms.TableLayoutPanel();
            grid.Dock = System.Windows.Forms.DockStyle.Fill;
            grid.AutoScroll = true;
            grid.ColumnCount = 4;
            grid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            grid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100f));
            grid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 110f));
            grid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            grid.Padding = new System.Windows.Forms.Padding(4, 8, 20, 4);

            System.Windows.Forms.Label noteLabel = new System.Windows.Forms.Label();
            noteLabel.Text = note;
            noteLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            noteLabel.ForeColor = System.Drawing.Color.DimGray;
            noteLabel.Padding = new System.Windows.Forms.Padding(4, 8, 8, 4);

            System.Windows.Forms.TableLayoutPanel right = new System.Windows.Forms.TableLayoutPanel();
            right.Dock = System.Windows.Forms.DockStyle.Fill;
            right.ColumnCount = 1;
            right.RowCount = 2;
            right.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 70f));
            right.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 30f));
            right.Controls.Add(grid, 0, 0);
            right.Controls.Add(noteLabel, 0, 1);

            System.Windows.Forms.TableLayoutPanel layout = new System.Windows.Forms.TableLayoutPanel();
            layout.Dock = System.Windows.Forms.DockStyle.Fill;
            layout.ColumnCount = 2;
            layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 60f));
            layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 40f));
            layout.Controls.Add(picture, 0, 0);
            layout.Controls.Add(right, 1, 0);
            return layout;
        }

        private void AddIntField(
            Page page, string symbol, string name, string unit, string key,
            System.Func<Hikan.Core.HikanParameters, int> get,
            System.Action<Hikan.Core.HikanParameters, int> set)
        {
            AddField(page, symbol, name, unit, key, q => get(q), (q, v) => set(q, (int)v));
            page.Fields[page.Fields.Count - 1].Integer = true;
            page.Fields[page.Fields.Count - 1].Box.Text = get(source).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private void AddField(
            Page page, string symbol, string name, string unit, string key,
            System.Func<Hikan.Core.HikanParameters, decimal> get,
            System.Action<Hikan.Core.HikanParameters, decimal> set)
        {
            Field f = new Field();
            f.Name = name;
            f.Key = key;
            f.Get = get;
            f.Set = set;
            f.Page = page;
            f.InnerTab = page.InnerTab;
            f.Box = new System.Windows.Forms.TextBox();
            f.Box.Width = 100;
            f.Box.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            f.Box.Text = get(source).ToString("0.000###", System.Globalization.CultureInfo.InvariantCulture);
            f.Box.TextChanged += delegate { UpdateEnabled(page); };

            int row = page.Grid.RowStyles.Count;
            page.Grid.RowCount = row + 2;
            page.Grid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            page.Grid.Controls.Add(GridLabel(symbol, System.Drawing.Color.FromArgb(30, 60, 200)), 0, row);
            page.Grid.Controls.Add(GridLabel(name, System.Drawing.Color.Black), 1, row);
            page.Grid.Controls.Add(f.Box, 2, row);
            page.Grid.Controls.Add(GridLabel(unit, System.Drawing.Color.Black), 3, row);
            page.Fields.Add(f);
        }

        private static System.Windows.Forms.Label GridLabel(string text, System.Drawing.Color color)
        {
            System.Windows.Forms.Label label = new System.Windows.Forms.Label();
            label.Text = text;
            label.ForeColor = color;
            label.AutoSize = true;
            label.Anchor = System.Windows.Forms.AnchorStyles.Left;
            label.Margin = new System.Windows.Forms.Padding(3, 7, 6, 7);
            return label;
        }

        /// <summary>「0 で設置しない」欄が 0 なら、そのタブの他の欄を無効にする(読めない値の間は有効のまま)。</summary>
        private void UpdateEnabled(Page page)
        {
            if (page.Presence == null) { return; }
            decimal v;
            bool on = !TryRead(page.Presence, out v) || v > 0m;
            foreach (Field f in page.Fields)
            {
                if (f != page.Presence) { f.Box.Enabled = on; }
            }
        }

        // ---------------------------------------------------------------- 確定

        private void OnOk(object sender, System.EventArgs e)
        {
            marker.Clear();
            errorList.Items.Clear();
            errorTargets.Clear();
            errorPages.Clear();
            foreach (Page page in pages) { page.Tab.Text = page.Title; }

            // 元のパラメータの複製に入力値を上書きする(基準点と、無効な欄の値は元のまま)。
            Hikan.Core.HikanParameters q = Hikan.Core.HikanParameters.FromDictionary(source.ToDictionary());
            foreach (Page page in pages)
            {
                if (page.Apply != null) { page.Apply(q); }
                foreach (Field f in page.Fields)
                {
                    if (!f.Box.Enabled) { continue; }
                    decimal v;
                    if (TryRead(f, out v))
                    {
                        f.Set(q, v);
                    }
                    else
                    {
                        AddError(f.Name + " に" + (f.Integer ? "整数" : "数値") + "を入力してください。", f, page);
                    }
                }
            }

            // 数値として読めない欄が無いときだけ整合性チェックへ進む(読めない値で判定しても意味がない)。
            if (errorTargets.Count == 0)
            {
                foreach (string message in Hikan.Core.HikanValidator.ValidateInput(q))
                {
                    Field f = FindField(message);
                    AddError(message, f, f != null ? f.Page : FindPage(message));
                }
            }

            if (errorTargets.Count == 0)
            {
                Result = q;
                DialogResult = System.Windows.Forms.DialogResult.OK;
                return;
            }
            ShowError(0);
        }

        private void AddError(string message, Field f, Page page)
        {
            errorList.Items.Add("[" + page.Title + "] " + message);
            errorTargets.Add(f);
            errorPages.Add(page);
            if (!page.Tab.Text.EndsWith(" ●", System.StringComparison.Ordinal)) { page.Tab.Text = page.Title + " ●"; }
            if (f != null)
            {
                string current = marker.GetError(f.Box);
                marker.SetError(f.Box, current.Length == 0 ? message : current + "\n" + message);
            }
        }

        /// <summary>index 番目のエラーのタブを開き、該当欄があればそこへフォーカスする。</summary>
        private void ShowError(int index)
        {
            if (index < 0 || index >= errorPages.Count) { return; }
            tabs.SelectedTab = errorPages[index].Tab;
            Field f = errorTargets[index];
            if (f != null && f.InnerTab != null && f.InnerTab.Parent is System.Windows.Forms.TabControl inner)
            {
                inner.SelectedTab = f.InnerTab;
            }
            if (f != null && f.Box.Enabled)
            {
                f.Box.Focus();
                f.Box.SelectAll();
            }
        }

        /// <summary>エラー文の書き出しで該当欄を探す。見つからなければ null(タブだけ示す)。</summary>
        private Field FindField(string message)
        {
            foreach (Page page in pages)
            {
                foreach (Field f in page.Fields)
                {
                    if (message.StartsWith(f.Key, System.StringComparison.Ordinal)) { return f; }
                }
            }
            return null;
        }

        /// <summary>欄を特定できないエラー(部材どうしの干渉など)は、書き出しの部材名でタブを決める。既定は函体。</summary>
        private Page FindPage(string message)
        {
            foreach (Page page in pages)
            {
                if (page.Prefix != null && message.StartsWith(page.Prefix, System.StringComparison.Ordinal)) { return page; }
            }
            return pages[0];
        }

        /// <summary>全角数字・全角ピリオドも受け付ける。桁区切りのカンマは受け付けない(1,5 を 15 と読まないため)。</summary>
        private static bool TryRead(Field f, out decimal value)
        {
            string text = f.Box.Text.Normalize(System.Text.NormalizationForm.FormKC).Trim();
            System.Globalization.NumberStyles style = System.Globalization.NumberStyles.AllowLeadingSign
                | System.Globalization.NumberStyles.AllowLeadingWhite
                | System.Globalization.NumberStyles.AllowTrailingWhite;
            if (f.Integer)
            {
                int n;
                bool ok = int.TryParse(text, style, System.Globalization.CultureInfo.InvariantCulture, out n);
                value = n;
                return ok;
            }
            return decimal.TryParse(text, style | System.Globalization.NumberStyles.AllowDecimalPoint,
                System.Globalization.CultureInfo.InvariantCulture, out value);
        }
    }
}
