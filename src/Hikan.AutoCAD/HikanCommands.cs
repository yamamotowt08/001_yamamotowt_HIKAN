// === 参照DLLバージョン検証済み(2026-10-07) === (詳細は HikanSolidBuilder.cs 先頭を参照。scripts/verify-dll-versions.ps1 で検証)
namespace Hikan.AutoCAD
{
    /// <summary>
    /// 樋管函体の AutoCAD / Civil 3D コマンド。NETLOAD で Hikan.AutoCAD.dll を読み込んで使う。
    /// HIKAN_Create / HIKAN_Action / HIKAN_Query / HIKAN_SelfTest の 4 本。
    /// </summary>
    public class HikanCommands
    {
        private const double Tolerance = 0.001;

        [Autodesk.AutoCAD.Runtime.CommandMethod("HIKAN_Create")]
        public void Create()
        {
            Autodesk.AutoCAD.ApplicationServices.Document doc =
                Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) { return; }
            Autodesk.AutoCAD.EditorInput.Editor ed = doc.Editor;

            try
            {
                Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
                if (!HikanPrompt.PromptAll(ed, p, true))
                {
                    ed.WriteMessage("\nキャンセルしました。");
                    return;
                }
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

        [Autodesk.AutoCAD.Runtime.CommandMethod("HIKAN_Action")]
        public void Action()
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

                if (!HikanPrompt.PromptAll(ed, p, false))
                {
                    ed.WriteMessage("\nキャンセルしました。");
                    return;
                }
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
                RunCase(ed, doc, 0.00m, "勾配なし");
                RunCase(ed, doc, 0.02m, "勾配 0.02");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\nSelfTest で例外: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static void RunCase(
            Autodesk.AutoCAD.EditorInput.Editor ed,
            Autodesk.AutoCAD.ApplicationServices.Document doc,
            decimal slope,
            string label)
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.TopSlabThickness = 0.4m;
            p.BottomSlabThickness = 0.6m;
            p.BaseX = 1.5m;
            p.BaseY = 2.5m;
            p.BaseZ = 0.75m;
            p.BottomSlope = slope;

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
                hollowOk = mp.Volume < (double)(p.OuterWidth * p.OuterHeight * p.BarrelLength) - Tolerance;

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
