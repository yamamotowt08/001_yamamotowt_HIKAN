// === 参照DLLバージョン検証済み(2026-10-07) === (詳細は HikanSolidBuilder.cs 先頭を参照。scripts/verify-dll-versions.ps1 で検証)
namespace Hikan.AutoCAD
{
    /// <summary>
    /// 樋管函体の AutoCAD / Civil 3D コマンド。NETLOAD で Hikan.AutoCAD.dll を読み込んで使う。
    /// HIKAN_Create / HIKAN_Action(入力画面)、-HIKAN_Create / -HIKAN_Action(コマンドライン入力)、
    /// HIKAN_Query / HIKAN_SelfTest の 6 本。
    /// </summary>
    public class HikanCommands
    {
        private const double Tolerance = 0.001;

        [Autodesk.AutoCAD.Runtime.CommandMethod("HIKAN_Create")]
        public void Create()
        {
            CreateCore(true);
        }

        /// <summary>コマンドライン入力版(スクリプト実行用)。</summary>
        [Autodesk.AutoCAD.Runtime.CommandMethod("-HIKAN_Create")]
        public void CreateCommandLine()
        {
            CreateCore(false);
        }

        [Autodesk.AutoCAD.Runtime.CommandMethod("HIKAN_Action")]
        public void Action()
        {
            ActionCore(true);
        }

        /// <summary>コマンドライン入力版(スクリプト実行用)。</summary>
        [Autodesk.AutoCAD.Runtime.CommandMethod("-HIKAN_Action")]
        public void ActionCommandLine()
        {
            ActionCore(false);
        }

        /// <summary>入力画面版は寸法を先に決めてから基準点を指定する。コマンドライン版は従来どおり基準点が先。</summary>
        private static void CreateCore(bool useDialog)
        {
            Autodesk.AutoCAD.ApplicationServices.Document doc =
                Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) { return; }
            Autodesk.AutoCAD.EditorInput.Editor ed = doc.Editor;

            try
            {
                Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
                if (useDialog)
                {
                    p = HikanDialog.Edit(p, "樋管 作成");
                    if (p == null || !HikanPrompt.PromptBasePoint(ed, p))
                    {
                        ed.WriteMessage("\nキャンセルしました。");
                        return;
                    }
                }
                else if (!HikanPrompt.PromptAll(ed, p, true))
                {
                    ed.WriteMessage("\nキャンセルしました。");
                    return;
                }
                Hikan.Core.HikanValidator.EnsureInputValid(p);
                Autodesk.AutoCAD.DatabaseServices.ObjectId id = HikanSolidBuilder.Create(doc.Database, p);
                Hikan.Core.HikanEstimate e = Hikan.Core.HikanEstimator.Calculate(p);
                ed.WriteMessage("\nレイヤー: " + HikanSolidBuilder.LayerName
                    + "  ハンドル: " + id.Handle
                    + "\n外形 " + e.OuterWidth + " × " + e.OuterHeight + " m、延長 " + p.BarrelLength
                    + " m、コンクリート " + e.ConcreteVolume + " m3");
            }
            catch (Hikan.Core.HikanValidationException ex)
            {
                WriteStop(ed, ex);
            }
        }

        private static void ActionCore(bool useDialog)
        {
            Autodesk.AutoCAD.ApplicationServices.Document doc =
                Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) { return; }
            Autodesk.AutoCAD.EditorInput.Editor ed = doc.Editor;

            try
            {
                Autodesk.AutoCAD.DatabaseServices.ObjectId id;
                Hikan.Core.HikanParameters p;
                if (!SelectAndRead(doc, out id, out p)) { return; }

                if (useDialog)
                {
                    p = HikanDialog.Edit(p, "樋管 修正");
                    if (p == null)
                    {
                        ed.WriteMessage("\nキャンセルしました。");
                        return;
                    }
                }
                else if (!HikanPrompt.PromptAll(ed, p, false))
                {
                    ed.WriteMessage("\nキャンセルしました。");
                    return;
                }
                Hikan.Core.HikanValidator.EnsureInputValid(p);
                Autodesk.AutoCAD.DatabaseServices.ObjectId newId = HikanSolidBuilder.Replace(doc.Database, id, p);
                ed.WriteMessage("\n同じ基準点 (" + p.BaseX + ", " + p.BaseY + ", " + p.BaseZ
                    + ") で再生成しました。新ハンドル: " + newId.Handle);
            }
            catch (Hikan.Core.HikanValidationException ex)
            {
                WriteStop(ed, ex);
            }
        }

        [Autodesk.AutoCAD.Runtime.CommandMethod("HIKAN_Query")]
        public void Query()
        {
            Autodesk.AutoCAD.ApplicationServices.Document doc =
                Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) { return; }
            Autodesk.AutoCAD.EditorInput.Editor ed = doc.Editor;

            try
            {
                Autodesk.AutoCAD.DatabaseServices.ObjectId id;
                Hikan.Core.HikanParameters p;
                if (!SelectAndRead(doc, out id, out p)) { return; }

                Hikan.Core.HikanEstimate e = Hikan.Core.HikanEstimator.Calculate(p);
                ed.WriteMessage("\n--- 樋管函体 諸元・数量 (信頼度: 入力 / 確定 / 概算 / 推定) ---");
                foreach (Hikan.Core.HikanItem item in Hikan.Core.HikanReport.Build(p, e))
                {
                    ed.WriteMessage("\n" + item.JapaneseName + " (" + item.EnglishName + ") = "
                        + item.Value + " " + item.Unit + "  [" + item.Confidence + "]");
                }
                foreach (string note in Hikan.Core.HikanReport.Notes)
                {
                    ed.WriteMessage("\n注) " + note);
                }
            }
            catch (Hikan.Core.HikanValidationException ex)
            {
                WriteStop(ed, ex);
            }
        }

        /// <summary>
        /// 実機検証。頂版厚 ≠ 底版厚 かつ基準点を 3 軸すべて非ゼロにして走らせる。
        /// この条件でないと断面の上下反転と Z オフセットの取り違えを検出できない。
        /// </summary>
        [Autodesk.AutoCAD.Runtime.CommandMethod("HIKAN_SelfTest")]
        public void SelfTest()
        {
            Autodesk.AutoCAD.ApplicationServices.Document doc =
                Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) { return; }
            Autodesk.AutoCAD.EditorInput.Editor ed = doc.Editor;

            try
            {
                // 段階ごとに分けて、どの段階の部材で落ちたかを切り分けられるようにする。
                // 第1段階(函体のみ)・第2段階(胸壁+しゃ水壁)は退行確認、第3段階(翼壁)が新規分。
                RunCase(ed, doc, Case(0.00m, false, false, false), "函体のみ・勾配なし");
                RunCase(ed, doc, Case(0.02m, false, false, false), "函体のみ・勾配 0.02");
                RunCase(ed, doc, Case(0.00m, true, false, false), "胸壁+しゃ水壁・勾配なし");
                RunCase(ed, doc, Case(0.02m, true, false, false), "胸壁+しゃ水壁・勾配 0.02");
                RunCase(ed, doc, Case(0.00m, true, true, false), "胸壁+しゃ水壁+翼壁・勾配なし");
                RunCase(ed, doc, Case(0.02m, true, true, false), "胸壁+しゃ水壁+翼壁・勾配 0.02");
                // 翼壁の端部から左右へ張り出す堤防保護部(翼壁の側壁・底版と重なり、BoolUnite で一体化する)。
                RunCase(ed, doc, WithProtection(Case(0.00m, true, true, false)), "+堤防保護部・勾配なし");
                RunCase(ed, doc, WithProtection(Case(0.02m, true, true, false)), "+堤防保護部・勾配 0.02");
                // 門柱・操作台(頂版の上に立つ。操作台は川表側の函体端より外へ張り出し、翼壁の上を通る)。
                RunCase(ed, doc, WithGate(Case(0.00m, true, true, false)), "+門柱・操作台・勾配なし");
                RunCase(ed, doc, WithGate(Case(0.02m, true, true, false)), "+門柱・操作台・勾配 0.02");
                // 胸壁を函体端に寄せ、つま先版が翼壁の底版と重なる配置(BoolUnite で一体化した実体を確かめる)。
                RunCase(ed, doc, Case(0.00m, true, true, true), "胸壁を函体端に寄せて翼壁と重なる・勾配なし");
                RunCase(ed, doc, Case(0.02m, true, true, true), "胸壁を函体端に寄せて翼壁と重なる・勾配 0.02");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\nSelfTest で例外: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// 検証用のパラメータ。頂版厚 ≠ 底版厚 かつ基準点を 3 軸すべて非ゼロにするのが要点で、
        /// これが無いと断面の上下反転と Z オフセットの取り違えを検出できない。
        /// 川裏側胸壁(軸位置 2.0・厚 0.5・張出し 1.5・天端 4.0)と川表側胸壁(17.5・0.6・2.0・4.5)を
        /// 別寸法にして、川裏・川表の取り違えを検出できるようにする。左右は同一形状。
        /// 軸位置はどちらも函体端から離して、位置指定が効いていることを確かめる。
        /// 翼壁(U 字の 3 面張り水路)は川裏側を八の字(内空幅 2.0 → 端部 3.0、翼壁長 3.0)、川表側を平行にして、
        /// 平面が斜めの部材と天端が傾いた部材(BoolIntersect で作る)の両方を確かめる。
        /// 川表側は底版を 0.8 にして、底版下面が函体底版下面(Z = 0)より下がる場合も確かめる。
        /// </summary>
        /// <param name="breastAtEnds">true なら胸壁のたて壁を函体端に面一で置き、
        /// 胸壁のつま先版と翼壁の底版を重ねる(BoolUnite で一体化した実体を確かめる)。</param>
        private static Hikan.Core.HikanParameters Case(decimal slope, bool withExtraParts, bool withWings, bool breastAtEnds)
        {
            // 胸壁・翼壁は厚 0(旧図面の「設置なし」)から始め、ケースごとに付け足す。
            // 「函体のみ」のケースは、旧版で作った図面を読み直した場合の互換確認を兼ねる。
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.UpstreamBreast.StemThickness = 0m;
            p.DownstreamBreast.StemThickness = 0m;
            p.UpstreamWing.StemThickness = 0m;
            p.DownstreamWing.StemThickness = 0m;
            p.Gate.Exists = false;
            p.TopSlabThickness = 0.4m;
            p.BottomSlabThickness = 0.6m;
            p.BaseX = 1.5m;
            p.BaseY = 2.5m;
            p.BaseZ = 0.75m;
            p.BottomSlope = slope;

            if (withExtraParts)
            {
                p.UpstreamBreast.StemThickness = 0.5m;
                p.UpstreamBreast.Position = 2.0m;
                p.UpstreamBreast.Length = 1.5m;
                p.UpstreamBreast.CrownHeight = 4.0m;
                p.UpstreamBreast.FootingThickness = 0.5m;
                p.UpstreamBreast.ToeLength = 0.8m;
                p.UpstreamBreast.HeelLength = 1.2m;
                p.DownstreamBreast.StemThickness = 0.6m;
                p.DownstreamBreast.Position = 17.5m;
                p.DownstreamBreast.Length = 2.0m;
                p.DownstreamBreast.CrownHeight = 4.5m;
                p.DownstreamBreast.FootingThickness = 0.6m;
                p.DownstreamBreast.ToeLength = 1.0m;
                p.DownstreamBreast.HeelLength = 1.5m;
                p.CutoffCount = 2;
                p.CutoffThickness = 0.5m;
                p.CutoffProjection = 0.7m;
            }

            if (withWings)
            {
                p.UpstreamWing.StemThickness = 0.5m;
                p.UpstreamWing.Length = 3.0m;
                p.UpstreamWing.EndWidth = 3.0m;
                p.UpstreamWing.CrownStart = 3.5m;
                p.UpstreamWing.CrownEnd = 2.0m;
                p.UpstreamWing.FloorThickness = 0.6m;
                p.DownstreamWing.StemThickness = 0.6m;
                p.DownstreamWing.Length = 4.0m;
                p.DownstreamWing.EndWidth = 2.0m;
                p.DownstreamWing.CrownStart = 4.0m;
                p.DownstreamWing.CrownEnd = 2.5m;
                p.DownstreamWing.FloorThickness = 0.8m;
            }

            if (breastAtEnds)
            {
                // 胸壁の底版と翼壁の底版はどちらも上面が敷高なので、函体端に寄せるだけで重なる。
                p.UpstreamBreast.Position = p.UpstreamBreast.StemThickness / 2m;
                p.DownstreamBreast.Position = p.BarrelLength - p.DownstreamBreast.StemThickness / 2m;
            }
            return p;
        }

        /// <summary>
        /// 堤防保護部を両端に付ける。川裏側(八の字の翼壁)と川表側(平行の翼壁)で寸法を変えて取り違えを検出する。
        /// 川裏側 X4 1.2 / L3 0.4 / Y3 2.0 / L2 0.5 / L4 1.0、川表側 X4 1.5 / L3 0.5 / Y3 2.5 / L2 0.6 / L4 1.2。
        /// </summary>
        private static Hikan.Core.HikanParameters WithProtection(Hikan.Core.HikanParameters p)
        {
            Hikan.Core.HikanLeveeProtection up = p.UpstreamWing.Protection;
            up.Exists = true;
            up.Width = 1.2m;
            up.StemThickness = 0.4m;
            up.StemHeight = 2.0m;
            up.ToeLength = 0.5m;
            up.HeelLength = 1.0m;
            Hikan.Core.HikanLeveeProtection down = p.DownstreamWing.Protection;
            down.Exists = true;
            down.Width = 1.5m;
            down.StemThickness = 0.5m;
            down.StemHeight = 2.5m;
            down.ToeLength = 0.6m;
            down.HeelLength = 1.2m;
            return p;
        }

        /// <summary>
        /// 門柱・操作台を付ける。門柱(L 字、X4 0.9 / X5 0.5 / X7 0.5 / X8 0.4、柱高さ 6.0)の背面は川表側胸壁のたて壁の
        /// 背面(S 17.2)にそろうので S 17.2〜18.6、操作台は前後に X3 0.7 張り出して 16.5〜19.3。
        /// 門柱の全幅 0.9 は函体の側壁厚 0.4 より厚く、胸壁のたて壁(天端 4.5 > 頂版上面 3.0)と重なる(一体化して 1 回だけ数える)。
        /// X7 ≠ X8・X4 ≠ X5 にして、横方向壁の向き(川裏側)や内外の取り違えを重心で検出できるようにする。
        /// 管理橋ありで、操作台の川裏側の端に受け台(桁受けの切欠き)を作る。ハンチ H2 0.3(三角柱を BoolUnite する経路の確認)、
        /// 土留壁 WH 1.0(門柱の間をふさぐ)。
        /// 内空幅 2.0 なので X1 = 1.4 + 1.4 = 2.8、X2 = 2.0 + 2 × 0.9 + 2 × 1.1 = 6.0。
        /// </summary>
        private static Hikan.Core.HikanParameters WithGate(Hikan.Core.HikanParameters p)
        {
            Hikan.Core.HikanGateTower g = p.Gate;
            g.Exists = true;
            g.LongitudinalWallWidth = 0.9m;
            g.TransverseWallThickness = 0.5m;
            g.LongitudinalWallThickness = 0.5m;
            g.TransverseWallWidth = 0.4m;
            g.ColumnHeight = 6.0m;
            g.DeckAxialOverhang = 0.7m;
            g.DeckSideOverhang = 1.1m;
            g.DeckLength = 2.8m;
            g.DeckWidth = 6.0m;
            g.DeckThickness = 0.6m;
            g.Bridge = true;
            g.SeatLength = 0.4m;
            g.SeatWidth = 1.4m;
            g.SeatDepth = 0.3m;
            g.HaunchHeight = 0.3m;
            g.RetainingWallHeight = 1.0m;
            return p;
        }

        private static void RunCase(
            Autodesk.AutoCAD.EditorInput.Editor ed,
            Autodesk.AutoCAD.ApplicationServices.Document doc,
            Hikan.Core.HikanParameters p,
            string label)
        {
            Autodesk.AutoCAD.DatabaseServices.ObjectId id = HikanSolidBuilder.Create(doc.Database, p);

            bool xdataOk = false;
            bool layerOk = false;
            bool colorOk = false;
            bool volumeOk = false;
            bool hollowOk = false;
            bool extentsOk = false;
            bool centroidOk = false;

            using (Autodesk.AutoCAD.DatabaseServices.Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                Autodesk.AutoCAD.DatabaseServices.Solid3d s = (Autodesk.AutoCAD.DatabaseServices.Solid3d)
                    tr.GetObject(id, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForWrite);

                Hikan.Core.HikanParameters q;
                if (HikanSolidBuilder.TryReadParameters(s, out q))
                {
                    xdataOk = SameDictionary(p.ToDictionary(), q.ToDictionary());
                }
                layerOk = s.Layer == HikanSolidBuilder.LayerName;
                colorOk = s.ColorIndex == p.ColorIndex;

                Autodesk.AutoCAD.DatabaseServices.Solid3dMassProperties mp = s.MassProperties;
                volumeOk = System.Math.Abs(mp.Volume - (double)Hikan.Core.HikanGeometry.ModelVolume(p)) <= Tolerance;
                hollowOk = mp.Volume < (double)Hikan.Core.HikanGeometry.EnvelopeVolume(p) - Tolerance;

                (decimal MinX, decimal MinY, decimal MinZ, decimal MaxX, decimal MaxY, decimal MaxZ) x =
                    Hikan.Core.HikanGeometry.ExpectedExtents(p);
                Autodesk.AutoCAD.DatabaseServices.Extents3d ext = s.GeometricExtents;
                extentsOk = Near(ext.MinPoint.X, x.MinX) && Near(ext.MinPoint.Y, x.MinY) && Near(ext.MinPoint.Z, x.MinZ)
                    && Near(ext.MaxPoint.X, x.MaxX) && Near(ext.MaxPoint.Y, x.MaxY) && Near(ext.MaxPoint.Z, x.MaxZ);

                (decimal X, decimal Y, decimal Z) g = Hikan.Core.HikanGeometry.ExpectedCentroid(p);
                centroidOk = Near(mp.Centroid.X, g.X) && Near(mp.Centroid.Y, g.Y) && Near(mp.Centroid.Z, g.Z);

                s.Erase();
                tr.Commit();
            }

            ed.WriteMessage("\n[" + label + "] 体積=" + Ok(volumeOk) + " 内空控除=" + Ok(hollowOk)
                + " エクステント=" + Ok(extentsOk) + " 重心=" + Ok(centroidOk)
                + " XData=" + Ok(xdataOk) + " レイヤー=" + Ok(layerOk) + " 色=" + Ok(colorOk));
        }

        private static bool Near(double actual, decimal expected)
        {
            return System.Math.Abs(actual - (double)expected) <= Tolerance;
        }

        private static bool SameDictionary(
            System.Collections.Generic.Dictionary<string, string> a,
            System.Collections.Generic.Dictionary<string, string> b)
        {
            if (a.Count != b.Count) { return false; }
            foreach (System.Collections.Generic.KeyValuePair<string, string> kv in a)
            {
                string v;
                if (!b.TryGetValue(kv.Key, out v) || v != kv.Value) { return false; }
            }
            return true;
        }

        private static string Ok(bool b)
        {
            return b ? "OK" : "NG";
        }

        private static void WriteStop(Autodesk.AutoCAD.EditorInput.Editor ed, Hikan.Core.HikanValidationException ex)
        {
            ed.WriteMessage("\nエラー停止(再生成しません):");
            foreach (string e in ex.Errors)
            {
                ed.WriteMessage("\n  - " + e);
            }
        }

        private static bool SelectAndRead(
            Autodesk.AutoCAD.ApplicationServices.Document doc,
            out Autodesk.AutoCAD.DatabaseServices.ObjectId id,
            out Hikan.Core.HikanParameters p)
        {
            id = Autodesk.AutoCAD.DatabaseServices.ObjectId.Null;
            p = null;
            Autodesk.AutoCAD.EditorInput.Editor ed = doc.Editor;

            Autodesk.AutoCAD.EditorInput.PromptEntityOptions o =
                new Autodesk.AutoCAD.EditorInput.PromptEntityOptions("\n樋管函体のソリッドを選択: ");
            o.SetRejectMessage("\nSolid3d を選択してください。");
            o.AddAllowedClass(typeof(Autodesk.AutoCAD.DatabaseServices.Solid3d), true);
            Autodesk.AutoCAD.EditorInput.PromptEntityResult r = ed.GetEntity(o);
            if (r.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
            {
                return false;
            }

            using (Autodesk.AutoCAD.DatabaseServices.Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                Autodesk.AutoCAD.DatabaseServices.Entity ent = (Autodesk.AutoCAD.DatabaseServices.Entity)
                    tr.GetObject(r.ObjectId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);
                if (!HikanSolidBuilder.TryReadParameters(ent, out p))
                {
                    ed.WriteMessage("\n選択したソリッドに XData(" + HikanSolidBuilder.AppName + ")がありません。");
                    return false;
                }
                tr.Commit();
            }
            id = r.ObjectId;
            return true;
        }
    }
}
