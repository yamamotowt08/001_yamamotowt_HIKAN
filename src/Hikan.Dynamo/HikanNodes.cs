// === 参照DLLバージョン検証: 未検証 ===
// DynamoServices.dll : 3.3.x 期待 (C:\Program Files\Autodesk\AutoCAD 2025\C3D\Dynamo\Core\)  未検証
// 検証日: 未実施(この開発環境に Autodesk 製品と PowerShell が無いため実測できていない)
// 検証コマンド: scripts/verify-dll-versions.ps1
// 未検証リスク: Dynamo のバージョン不一致は MultiReturn 属性の解決に失敗してノードが現れない、
//               または TypeLoadException を起こす。いずれもビルド成功では検出できない。
namespace Hikan.Dynamo
{
    /// <summary>
    /// 樋管函体の数量を返す Zero Touch Node。モデルは作らず数値のみ返すため
    /// ProtoGeometry.dll / DSCoreNodes.dll は参照しない。
    /// 列挙型は Dynamo から扱いにくいため公開せず、引数は double / int の素の型に限る。
    /// HikanValidationException はそのまま伝播させ、ノードを警告状態にする。
    /// </summary>
    public static class HikanNodes
    {
        [Autodesk.DesignScript.Runtime.MultiReturn(new string[]
        {
            "外形幅_m",
            "外形高_m",
            "敷高_m",
            "コンクリート計_m3",
            "頂版_m3",
            "底版_m3",
            "側壁_m3",
            "型枠_内空_m2",
            "型枠_外側面_m2",
            "型枠_頂版上面_m2",
            "型枠_端面_m2",
            "水平投影長_m",
            "落差_m",
            "ブロック長_m",
            "ブロック当りコンクリート_m3",
            "床掘り_m3",
            "埋戻_m3",
            "残土_m3"
        })]
        public static System.Collections.Generic.Dictionary<string, object> Estimate(
            double innerWidth = 2.0,
            double innerHeight = 2.0,
            double wallThickness = 0.4,
            double topSlabThickness = 0.4,
            double bottomSlabThickness = 0.4,
            double barrelLength = 20.0,
            double bottomSlope = 0.0,
            double soilCover = 2.0,
            int blockCount = 4,
            double excavationMargin = 0.5,
            double excavationSlope = 0.5,
            double foundationThickness = 0.1)
        {
            Hikan.Core.HikanParameters p = new Hikan.Core.HikanParameters();
            p.InnerWidth = (decimal)innerWidth;
            p.InnerHeight = (decimal)innerHeight;
            p.WallThickness = (decimal)wallThickness;
            p.TopSlabThickness = (decimal)topSlabThickness;
            p.BottomSlabThickness = (decimal)bottomSlabThickness;
            p.BarrelLength = (decimal)barrelLength;
            p.BottomSlope = (decimal)bottomSlope;
            p.SoilCover = (decimal)soilCover;
            p.BlockCount = blockCount;
            p.ExcavationMargin = (decimal)excavationMargin;
            p.ExcavationSlope = (decimal)excavationSlope;
            p.FoundationThickness = (decimal)foundationThickness;

            Hikan.Core.HikanEstimate e = Hikan.Core.HikanEstimator.Calculate(p);

            System.Collections.Generic.Dictionary<string, object> r =
                new System.Collections.Generic.Dictionary<string, object>();
            r["外形幅_m"] = (double)e.OuterWidth;
            r["外形高_m"] = (double)e.OuterHeight;
            r["敷高_m"] = (double)e.InvertLevel;
            r["コンクリート計_m3"] = (double)e.ConcreteVolume;
            r["頂版_m3"] = (double)e.TopSlabVolume;
            r["底版_m3"] = (double)e.BottomSlabVolume;
            r["側壁_m3"] = (double)e.WallVolume;
            r["型枠_内空_m2"] = (double)e.FormworkInner;
            r["型枠_外側面_m2"] = (double)e.FormworkOuterSide;
            r["型枠_頂版上面_m2"] = (double)e.FormworkTop;
            r["型枠_端面_m2"] = (double)e.FormworkEnd;
            r["水平投影長_m"] = (double)e.HorizontalProjection;
            r["落差_m"] = (double)e.DropHeight;
            r["ブロック長_m"] = (double)e.BlockLength;
            r["ブロック当りコンクリート_m3"] = (double)e.ConcretePerBlock;
            r["床掘り_m3"] = (double)e.ExcavationVolume;
            r["埋戻_m3"] = (double)e.BackfillVolume;
            r["残土_m3"] = (double)e.SurplusVolume;
            return r;
        }
    }
}
