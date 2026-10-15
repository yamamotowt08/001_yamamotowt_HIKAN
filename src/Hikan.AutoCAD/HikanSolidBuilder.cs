// === 参照DLLバージョン検証済み ===
// AcCoreMgd.dll     : 25.0.154.0.0  (C:\Program Files\Autodesk\AutoCAD 2025\)  期待 25.x  OK
// AcDbMgd.dll       : 25.0.154.0.0  (同上)                                     期待 25.x  OK
// AcMgd.dll         : 25.0.154.0.0  (同上)                                     期待 25.x  OK
// AeccDbMgd.dll     : 13.7.1175.0   (...\AutoCAD 2025\C3D\)  Civil 3D 2025。参照しない(参考)
// 検証日: 2026-10-07
// 検証コマンド: scripts/verify-dll-versions.ps1
// 注: AutoCAD 系はメジャー 25.x が一致していれば ABI 互換。マイナーが異なる環境で
//     TypeLoadException / MissingMethodException が出た場合は上記スクリプトを再実行すること。
namespace Hikan.AutoCAD
{
    /// <summary>
    /// 樋管(函体 + 胸壁 + しゃ水壁 + 翼壁)の Solid3d 生成・差替え・XData 入出力。
    ///
    /// 生成手順: 部材ごとに 断面ポリライン → Region → Extrude で中実プリズムを作り、
    /// BoolUnite で合成 → 翼壁(平面プリズムと側面プリズムの共通部分)を BoolUnite
    /// → 最後に内空プリズムを BoolSubtract → X 軸まわり +90 度回転
    /// → 基準点へ平行移動 → 底版勾配の回転。
    /// 薄いシェル同士を同一平面でブーリアンしないため、部材を増やしても退化しにくい。
    /// 合成は ModelSpace に追加したあと DB 常駐のまま行う(非常駐 Solid3d のブーリアンは
    /// 環境によって拒否されることがあるため)。途中で失敗しても tr.Commit() に到達しないので
    /// 図面には何も残らない。
    ///
    /// Verify は体積・3軸エクステント・3成分重心を解析解と照合する。体積とエクステントだけでは
    /// 断面の上下反転(頂版厚 ≠ 底版厚 のとき)を検出できないため重心の照合が必須。
    /// Verify は tr.Commit() の前に呼ぶので、失敗時は図面に何も残らない。
    /// </summary>
    public static class HikanSolidBuilder
    {
        public const string LayerName = "樋管函体";
        public const string AppName = "HIKAN_PARAM";

        /// <summary>長さの許容誤差 1 mm [m]</summary>
        private const double LengthTolerance = 0.001;
        /// <summary>面積の許容誤差 [m2]</summary>
        private const double AreaTolerance = 0.001;
        /// <summary>体積の許容誤差 [m3]</summary>
        private const double VolumeTolerance = 0.001;

        public static Autodesk.AutoCAD.DatabaseServices.ObjectId Create(
            Autodesk.AutoCAD.DatabaseServices.Database db,
            Hikan.Core.HikanParameters p)
        {
            Hikan.Core.HikanValidator.EnsureValid(p);

            using (Autodesk.AutoCAD.DatabaseServices.Transaction tr = db.TransactionManager.StartTransaction())
            {
                Autodesk.AutoCAD.DatabaseServices.ObjectId id = Append(tr, db, p);
                tr.Commit();
                return id;
            }
        }

        /// <summary>既存ソリッドを同じ基準点で作り直す。旧ソリッドは消去するのでハンドルは変わる。</summary>
        public static Autodesk.AutoCAD.DatabaseServices.ObjectId Replace(
            Autodesk.AutoCAD.DatabaseServices.Database db,
            Autodesk.AutoCAD.DatabaseServices.ObjectId oldId,
            Hikan.Core.HikanParameters p)
        {
            Hikan.Core.HikanValidator.EnsureValid(p);

            using (Autodesk.AutoCAD.DatabaseServices.Transaction tr = db.TransactionManager.StartTransaction())
            {
                Autodesk.AutoCAD.DatabaseServices.ObjectId id = Append(tr, db, p);
                Autodesk.AutoCAD.DatabaseServices.Entity old = (Autodesk.AutoCAD.DatabaseServices.Entity)
                    tr.GetObject(oldId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForWrite);
                old.Erase();
                tr.Commit();
                return id;
            }
        }

        /// <summary>XData(HIKAN_PARAM)からパラメータを復元する。</summary>
        public static bool TryReadParameters(
            Autodesk.AutoCAD.DatabaseServices.Entity ent,
            out Hikan.Core.HikanParameters p)
        {
            p = null;
            Autodesk.AutoCAD.DatabaseServices.ResultBuffer rb = ent.GetXDataForApplication(AppName);
            if (rb == null)
            {
                return false;
            }
            System.Collections.Generic.Dictionary<string, string> d =
                new System.Collections.Generic.Dictionary<string, string>();
            try
            {
                foreach (Autodesk.AutoCAD.DatabaseServices.TypedValue tv in rb)
                {
                    if (tv.TypeCode == (short)Autodesk.AutoCAD.DatabaseServices.DxfCode.ExtendedDataAsciiString)
                    {
                        string s = (string)tv.Value;
                        int i = s.IndexOf('=');
                        if (i > 0)
                        {
                            d[s.Substring(0, i)] = s.Substring(i + 1);
                        }
                    }
                }
            }
            finally
            {
                rb.Dispose();
            }
            p = Hikan.Core.HikanParameters.FromDictionary(d);
            return true;
        }

        private static Autodesk.AutoCAD.DatabaseServices.ObjectId Append(
            Autodesk.AutoCAD.DatabaseServices.Transaction tr,
            Autodesk.AutoCAD.DatabaseServices.Database db,
            Hikan.Core.HikanParameters p)
        {
            Hikan.Core.HikanPart[] parts = Hikan.Core.HikanGeometry.GetParts(p);
            decimal k = Hikan.Core.HikanGeometry.GlobalSMax(p);

            // 先頭は必ず函体。これを DB に載せてから、以降の部材を DB 常駐のまま合成する。
            Autodesk.AutoCAD.DatabaseServices.Solid3d solid = BuildPrism(parts[0], k);
            try
            {
                Autodesk.AutoCAD.DatabaseServices.BlockTable bt = (Autodesk.AutoCAD.DatabaseServices.BlockTable)
                    tr.GetObject(db.BlockTableId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);
                Autodesk.AutoCAD.DatabaseServices.BlockTableRecord ms = (Autodesk.AutoCAD.DatabaseServices.BlockTableRecord)
                    tr.GetObject(bt[Autodesk.AutoCAD.DatabaseServices.BlockTableRecord.ModelSpace],
                        Autodesk.AutoCAD.DatabaseServices.OpenMode.ForWrite);
                ms.AppendEntity(solid);
                tr.AddNewlyCreatedDBObject(solid, true);
            }
            catch
            {
                solid.Dispose();
                throw;
            }

            Combine(solid, parts, k, false, Autodesk.AutoCAD.DatabaseServices.BooleanOperationType.BoolUnite);
            CombineHaunches(solid, p, k);
            CombineWings(solid, p, k);
            Combine(solid, parts, k, true, Autodesk.AutoCAD.DatabaseServices.BooleanOperationType.BoolSubtract);

            solid.TransformBy(BuildPlacement(p));

            EnsureLayer(tr, db);
            solid.Layer = LayerName;
            solid.ColorIndex = (short)p.ColorIndex;
            AttachXData(tr, db, solid, p);
            Verify(solid, p);
            return solid.ObjectId;
        }

        /// <summary>parts のうち isVoid が一致するものを(先頭の函体を除いて)順に合成する。</summary>
        private static void Combine(
            Autodesk.AutoCAD.DatabaseServices.Solid3d solid,
            Hikan.Core.HikanPart[] parts,
            decimal k,
            bool isVoid,
            Autodesk.AutoCAD.DatabaseServices.BooleanOperationType operation)
        {
            for (int i = 1; i < parts.Length; i++)
            {
                if (parts[i].IsVoid != isVoid) { continue; }
                Autodesk.AutoCAD.DatabaseServices.Solid3d other = BuildPrism(parts[i], k);
                try
                {
                    solid.BooleanOperation(operation, other);
                }
                finally
                {
                    other.Dispose();
                }
            }
        }

        /// <summary>
        /// 部材 1 個ぶんの中実プリズム。断面を作図平面に置き、押出し座標 W = GlobalSMax − S になるよう
        /// Elevation をずらしてから押出す。配置変換は全部材を合成したあとに 1 回だけ適用する。
        /// </summary>
        private static Autodesk.AutoCAD.DatabaseServices.Solid3d BuildPrism(Hikan.Core.HikanPart part, decimal globalSMax)
        {
            return ExtrudePolygon(
                Hikan.Core.HikanGeometry.GetSectionPoints(part),
                part.Name,
                (double)(globalSMax - part.SMax),
                (double)(part.SMax - part.SMin),
                (double)((part.UMax - part.UMin) * (part.VMax - part.VMin)));
        }

        /// <summary>
        /// 門柱のハンチ(断面が三角の柱)を合成する。断面を作図平面に置いて押出すのは直方体の部材と同じ。
        /// 門柱の内面・操作台の下面と面で接するだけで他と重ならない(HikanValidator で検査済み)。
        /// </summary>
        private static void CombineHaunches(
            Autodesk.AutoCAD.DatabaseServices.Solid3d solid,
            Hikan.Core.HikanParameters p,
            decimal k)
        {
            foreach (Hikan.Core.HikanPrismPart part in Hikan.Core.HikanGeometry.GetHaunchParts(p))
            {
                Autodesk.AutoCAD.DatabaseServices.Solid3d other = ExtrudePolygon(
                    part.Section, part.Name, (double)(k - part.SMax), (double)(part.SMax - part.SMin), (double)part.Area);
                try
                {
                    solid.BooleanOperation(Autodesk.AutoCAD.DatabaseServices.BooleanOperationType.BoolUnite, other);
                }
                finally
                {
                    other.Dispose();
                }
            }
        }

        /// <summary>
        /// 翼壁を 1 部材ずつ合成する。翼壁は函体端より外にあって他の部材と重ならない
        /// (HikanValidator で検査済み)ので、函体などの合成のあと・内空の控除の前に BoolUnite する。
        /// </summary>
        private static void CombineWings(
            Autodesk.AutoCAD.DatabaseServices.Solid3d solid,
            Hikan.Core.HikanParameters p,
            decimal k)
        {
            foreach (Hikan.Core.HikanTaperedPart part in Hikan.Core.HikanGeometry.GetWingParts(p))
            {
                Autodesk.AutoCAD.DatabaseServices.Solid3d other = BuildTaperedPart(part, k);
                try
                {
                    solid.BooleanOperation(Autodesk.AutoCAD.DatabaseServices.BooleanOperationType.BoolUnite, other);
                }
                finally
                {
                    other.Dispose();
                }
            }
        }

        /// <summary>
        /// 翼壁の部材(平面が台形・天端が傾斜)。
        /// 1) 平面形 (U, S) を鉛直(V)に押出した「平面プリズム」を作る。
        /// 2) 天端が傾いていれば、側面形 (S, V) の台形を横断方向(U)に押出した「側面プリズム」と共通部分を取る。
        /// どちらも 断面ポリライン → Region → Extrude で作り、構築座標から作図座標へ剛体変換で移す。
        /// 作図座標は他の部材と同じ (x, y, z) = (U, V, GlobalSMax − S)。
        /// </summary>
        private static Autodesk.AutoCAD.DatabaseServices.Solid3d BuildTaperedPart(Hikan.Core.HikanTaperedPart part, decimal k)
        {
            decimal top = part.VTop0 > part.VTop1 ? part.VTop0 : part.VTop1;
            Autodesk.AutoCAD.DatabaseServices.Solid3d plan = ExtrudePolygon(
                part.PlanPoints(),
                part.Name + "(平面)",
                (double)part.VBottom,
                (double)(top - part.VBottom),
                (double)part.PlanArea);
            try
            {
                plan.TransformBy(PlanToDrawing(k));
                if (part.FlatTop)
                {
                    return plan;
                }

                // 側面プリズムは平面プリズムより S・U・下方に余裕をとって、境界の面が重ならないようにする。
                // 天端の直線は S 方向に延長しても同じ直線なので、共通部分の天端は正しい傾きになる。
                decimal margin = 1m;
                decimal sA = part.S0 - margin;
                decimal sB = part.S1 + margin;
                decimal topA = part.TopAt(sA);
                decimal topB = part.TopAt(sB);
                decimal bottom = part.VBottom;
                if (topA < bottom) { bottom = topA; }
                if (topB < bottom) { bottom = topB; }
                bottom -= margin;
                decimal uMin = (part.UL0 < part.UL1 ? part.UL0 : part.UL1) - margin;
                decimal uMax = (part.UR0 > part.UR1 ? part.UR0 : part.UR1) + margin;

                (decimal U, decimal V)[] profilePoints = new (decimal U, decimal V)[]
                {
                    (sA, bottom), (sB, bottom), (sB, topB), (sA, topA)
                };
                decimal profileArea = ((topA - bottom) + (topB - bottom)) / 2m * (sB - sA);
                Autodesk.AutoCAD.DatabaseServices.Solid3d profile = ExtrudePolygon(
                    profilePoints,
                    part.Name + "(側面)",
                    (double)uMin,
                    (double)(uMax - uMin),
                    (double)profileArea);
                try
                {
                    profile.TransformBy(ProfileToDrawing(k));
                    plan.BooleanOperation(Autodesk.AutoCAD.DatabaseServices.BooleanOperationType.BoolIntersect, profile);
                }
                finally
                {
                    profile.Dispose();
                }
                return plan;
            }
            catch
            {
                plan.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 構築座標 (x', y', z') = (U, S, V) を作図座標 (U, V, k − S) へ写す剛体変換(平面プリズム用)。
        /// x'×y' = z' が 作図の (1,0,0)×(0,0,−1) = (0,1,0) に一致するので鏡映にならない。
        /// </summary>
        private static Autodesk.AutoCAD.Geometry.Matrix3d PlanToDrawing(decimal k)
        {
            return Autodesk.AutoCAD.Geometry.Matrix3d.AlignCoordinateSystem(
                Autodesk.AutoCAD.Geometry.Point3d.Origin,
                Autodesk.AutoCAD.Geometry.Vector3d.XAxis,
                Autodesk.AutoCAD.Geometry.Vector3d.YAxis,
                Autodesk.AutoCAD.Geometry.Vector3d.ZAxis,
                new Autodesk.AutoCAD.Geometry.Point3d(0.0, 0.0, (double)k),
                new Autodesk.AutoCAD.Geometry.Vector3d(1.0, 0.0, 0.0),
                new Autodesk.AutoCAD.Geometry.Vector3d(0.0, 0.0, -1.0),
                new Autodesk.AutoCAD.Geometry.Vector3d(0.0, 1.0, 0.0));
        }

        /// <summary>
        /// 構築座標 (x', y', z') = (S, V, U) を作図座標 (U, V, k − S) へ写す剛体変換(側面プリズム用)。
        /// x'×y' = z' が 作図の (0,0,−1)×(0,1,0) = (1,0,0) に一致するので鏡映にならない。
        /// </summary>
        private static Autodesk.AutoCAD.Geometry.Matrix3d ProfileToDrawing(decimal k)
        {
            return Autodesk.AutoCAD.Geometry.Matrix3d.AlignCoordinateSystem(
                Autodesk.AutoCAD.Geometry.Point3d.Origin,
                Autodesk.AutoCAD.Geometry.Vector3d.XAxis,
                Autodesk.AutoCAD.Geometry.Vector3d.YAxis,
                Autodesk.AutoCAD.Geometry.Vector3d.ZAxis,
                new Autodesk.AutoCAD.Geometry.Point3d(0.0, 0.0, (double)k),
                new Autodesk.AutoCAD.Geometry.Vector3d(0.0, 0.0, -1.0),
                new Autodesk.AutoCAD.Geometry.Vector3d(0.0, 1.0, 0.0),
                new Autodesk.AutoCAD.Geometry.Vector3d(1.0, 0.0, 0.0));
        }

        /// <summary>
        /// 多角形を作図平面(XY)の指定標高に置いて Region にし、+Z に押出す。
        /// 押出し前に Region の面積を期待値と照合し、2D 段階の失敗を 3D と切り分ける。
        /// </summary>
        private static Autodesk.AutoCAD.DatabaseServices.Solid3d ExtrudePolygon(
            (decimal U, decimal V)[] pts,
            string label,
            double elevation,
            double height,
            double expectedArea)
        {
            Autodesk.AutoCAD.DatabaseServices.Region region = null;
            Autodesk.AutoCAD.DatabaseServices.Solid3d solid = null;
            try
            {
                region = CreateRegion(pts, label, elevation);
                if (System.Math.Abs(region.Area - expectedArea) > AreaTolerance)
                {
                    throw new Hikan.Core.HikanValidationException(
                        label + " の断面積 " + region.Area + " m2 が " + expectedArea + " m2 と一致しません。");
                }

                solid = new Autodesk.AutoCAD.DatabaseServices.Solid3d();
                solid.Extrude(region, height, 0.0);
                return solid;
            }
            catch
            {
                if (solid != null)
                {
                    solid.Dispose();
                }
                throw;
            }
            finally
            {
                if (region != null)
                {
                    region.Dispose();
                }
            }
        }

        /// <summary>局所断面 (U, V) を作図平面の指定標高に置いて閉 Region 1 個にする。</summary>
        private static Autodesk.AutoCAD.DatabaseServices.Region CreateRegion(
            (decimal U, decimal V)[] pts,
            string label,
            double elevation)
        {
            using (Autodesk.AutoCAD.DatabaseServices.Polyline pl = new Autodesk.AutoCAD.DatabaseServices.Polyline())
            {
                pl.Elevation = elevation;
                for (int i = 0; i < pts.Length; i++)
                {
                    pl.AddVertexAt(i,
                        new Autodesk.AutoCAD.Geometry.Point2d((double)pts[i].U, (double)pts[i].V), 0.0, 0.0, 0.0);
                }
                pl.Closed = true;

                Autodesk.AutoCAD.DatabaseServices.DBObjectCollection curves =
                    new Autodesk.AutoCAD.DatabaseServices.DBObjectCollection();
                curves.Add(pl);
                Autodesk.AutoCAD.DatabaseServices.DBObjectCollection regions =
                    Autodesk.AutoCAD.DatabaseServices.Region.CreateFromCurves(curves);
                if (regions.Count != 1)
                {
                    foreach (Autodesk.AutoCAD.DatabaseServices.DBObject o in regions)
                    {
                        o.Dispose();
                    }
                    throw new Hikan.Core.HikanValidationException(label + " の Region を1個生成できませんでした。");
                }
                return (Autodesk.AutoCAD.DatabaseServices.Region)regions[0];
            }
        }

        /// <summary>
        /// 配置変換。押出し後の (U, V, W) を
        /// (BaseX + U, BaseY + GlobalSMax − W, BaseZ + V) に写し、続いて底版勾配の回転をかける。
        /// 胸壁が無ければ GlobalSMax は函体延長に一致するので、第1段階と同じ配置になる。
        /// X 軸まわり +90 度は (x, y, z) → (x, −z, y) なので、断面の V が正しく Z に乗る
        /// (−90 度だと断面が上下反転し、頂版厚 ≠ 底版厚 のとき静かに誤る)。
        /// </summary>
        private static Autodesk.AutoCAD.Geometry.Matrix3d BuildPlacement(Hikan.Core.HikanParameters p)
        {
            Autodesk.AutoCAD.Geometry.Matrix3d rotate = Autodesk.AutoCAD.Geometry.Matrix3d.Rotation(
                System.Math.PI / 2.0,
                Autodesk.AutoCAD.Geometry.Vector3d.XAxis,
                Autodesk.AutoCAD.Geometry.Point3d.Origin);
            Autodesk.AutoCAD.Geometry.Matrix3d move = Autodesk.AutoCAD.Geometry.Matrix3d.Displacement(
                new Autodesk.AutoCAD.Geometry.Vector3d(
                    (double)p.BaseX,
                    (double)(p.BaseY + Hikan.Core.HikanGeometry.GlobalSMax(p)),
                    (double)p.BaseZ));
            Autodesk.AutoCAD.Geometry.Matrix3d place = move * rotate;

            if (p.BottomSlope == 0m)
            {
                return place;
            }

            // 下流下がりなので基準点を通る X 軸まわりに負の回転。上流端の高さが不動点になる。
            Autodesk.AutoCAD.Geometry.Matrix3d slope = Autodesk.AutoCAD.Geometry.Matrix3d.Rotation(
                -System.Math.Atan((double)p.BottomSlope),
                Autodesk.AutoCAD.Geometry.Vector3d.XAxis,
                new Autodesk.AutoCAD.Geometry.Point3d((double)p.BaseX, (double)p.BaseY, (double)p.BaseZ));
            return slope * place;
        }

        /// <summary>生成後検証: 体積・3軸エクステント・3成分重心・内空が抜けていること。</summary>
        private static void Verify(
            Autodesk.AutoCAD.DatabaseServices.Solid3d solid,
            Hikan.Core.HikanParameters p)
        {
            Autodesk.AutoCAD.DatabaseServices.Solid3dMassProperties mp = solid.MassProperties;

            double expectedVolume = (double)Hikan.Core.HikanGeometry.ModelVolume(p);
            if (System.Math.Abs(mp.Volume - expectedVolume) > VolumeTolerance)
            {
                throw new Hikan.Core.HikanValidationException(
                    "生成ソリッドの体積 " + mp.Volume + " m3 が解析解 " + expectedVolume + " m3 と一致しません。");
            }

            double envelope = (double)Hikan.Core.HikanGeometry.EnvelopeVolume(p);
            if (mp.Volume >= envelope - VolumeTolerance)
            {
                throw new Hikan.Core.HikanValidationException(
                    "内空が控除されていません(体積 " + mp.Volume + " m3 が外形エンベロープ " + envelope + " m3 と同等)。");
            }

            (decimal MinX, decimal MinY, decimal MinZ, decimal MaxX, decimal MaxY, decimal MaxZ) x =
                Hikan.Core.HikanGeometry.ExpectedExtents(p);
            Autodesk.AutoCAD.DatabaseServices.Extents3d ext = solid.GeometricExtents;
            CheckLength(ext.MinPoint.X, (double)x.MinX, "エクステント MinX");
            CheckLength(ext.MinPoint.Y, (double)x.MinY, "エクステント MinY");
            CheckLength(ext.MinPoint.Z, (double)x.MinZ, "エクステント MinZ");
            CheckLength(ext.MaxPoint.X, (double)x.MaxX, "エクステント MaxX");
            CheckLength(ext.MaxPoint.Y, (double)x.MaxY, "エクステント MaxY");
            CheckLength(ext.MaxPoint.Z, (double)x.MaxZ, "エクステント MaxZ");

            (decimal X, decimal Y, decimal Z) g = Hikan.Core.HikanGeometry.ExpectedCentroid(p);
            CheckLength(mp.Centroid.X, (double)g.X, "重心 X");
            CheckLength(mp.Centroid.Y, (double)g.Y, "重心 Y");
            CheckLength(mp.Centroid.Z, (double)g.Z, "重心 Z");
        }

        private static void CheckLength(double actual, double expected, string label)
        {
            if (System.Math.Abs(actual - expected) > LengthTolerance)
            {
                throw new Hikan.Core.HikanValidationException(
                    label + " が " + actual + " m で、期待値 " + expected + " m と一致しません(押出し方向・回転符号・基準点を確認)。");
            }
        }

        private static void EnsureLayer(
            Autodesk.AutoCAD.DatabaseServices.Transaction tr,
            Autodesk.AutoCAD.DatabaseServices.Database db)
        {
            Autodesk.AutoCAD.DatabaseServices.LayerTable lt = (Autodesk.AutoCAD.DatabaseServices.LayerTable)
                tr.GetObject(db.LayerTableId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);
            if (!lt.Has(LayerName))
            {
                lt.UpgradeOpen();
                Autodesk.AutoCAD.DatabaseServices.LayerTableRecord rec =
                    new Autodesk.AutoCAD.DatabaseServices.LayerTableRecord();
                rec.Name = LayerName;
                lt.Add(rec);
                tr.AddNewlyCreatedDBObject(rec, true);
            }
        }

        private static void AttachXData(
            Autodesk.AutoCAD.DatabaseServices.Transaction tr,
            Autodesk.AutoCAD.DatabaseServices.Database db,
            Autodesk.AutoCAD.DatabaseServices.Entity ent,
            Hikan.Core.HikanParameters p)
        {
            Autodesk.AutoCAD.DatabaseServices.RegAppTable rat = (Autodesk.AutoCAD.DatabaseServices.RegAppTable)
                tr.GetObject(db.RegAppTableId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);
            if (!rat.Has(AppName))
            {
                rat.UpgradeOpen();
                Autodesk.AutoCAD.DatabaseServices.RegAppTableRecord rec =
                    new Autodesk.AutoCAD.DatabaseServices.RegAppTableRecord();
                rec.Name = AppName;
                rat.Add(rec);
                tr.AddNewlyCreatedDBObject(rec, true);
            }

            // 「キー=値」の ASCII 文字列(1000)で保存する。実数は不変カルチャの文字列なので往復で劣化しない。
            using (Autodesk.AutoCAD.DatabaseServices.ResultBuffer rb = new Autodesk.AutoCAD.DatabaseServices.ResultBuffer(
                new Autodesk.AutoCAD.DatabaseServices.TypedValue(
                    (int)Autodesk.AutoCAD.DatabaseServices.DxfCode.ExtendedDataRegAppName, AppName)))
            {
                foreach (System.Collections.Generic.KeyValuePair<string, string> kv in p.ToDictionary())
                {
                    rb.Add(new Autodesk.AutoCAD.DatabaseServices.TypedValue(
                        (int)Autodesk.AutoCAD.DatabaseServices.DxfCode.ExtendedDataAsciiString, kv.Key + "=" + kv.Value));
                }
                ent.XData = rb;
            }
        }
    }
}
