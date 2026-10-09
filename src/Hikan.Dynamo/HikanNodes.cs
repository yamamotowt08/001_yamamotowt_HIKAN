// === 参照DLLバージョン検証済み ===
// DynamoServices.dll : 3.3.0.6316  (C:\Program Files\Autodesk\AutoCAD 2025\C3D\Dynamo\Core\)  期待 3.3.x  OK
// ProtoGeometry.dll  : 3.0.0.5365  (同上)  参照しない(参考)。Dynamo 本体と独立したバージョン体系で
//                      Dynamo 3.3 環境でも 3.0.x 系になる。3.3.x を期待してはいけない。
// 検証日: 2026-10-07
// 検証コマンド: scripts/verify-dll-versions.ps1
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
            "コンクリート総計_m3",
            "函体_m3",
            "上流胸壁_m3",
            "下流胸壁_m3",
            "しゃ水壁_m3",
            "浸透路長_m",
            "頂版_m3",
            "底版_m3",
            "側壁_m3",
            "型枠_内空_m2",
            "型枠_外側面_m2",
            "型枠_頂版上面_m2",
            "型枠_端面_m2",
            "型枠_胸壁_m2",
            "型枠_しゃ水壁_m2",
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
            double foundationThickness = 0.1,
            double upstreamBreastStemThickness = 0.0,
            double upstreamBreastLength = 2.0,
            double upstreamBreastCrownHeight = 4.0,
            double upstreamBreastEmbedment = 1.0,
            double upstreamBreastFootingThickness = 0.5,
            double upstreamBreastToeLength = 0.8,
            double upstreamBreastHeelLength = 1.2,
            double downstreamBreastStemThickness = 0.0,
            double downstreamBreastLength = 2.0,
            double downstreamBreastCrownHeight = 4.0,
            double downstreamBreastEmbedment = 1.0,
            double downstreamBreastFootingThickness = 0.5,
            double downstreamBreastToeLength = 0.8,
            double downstreamBreastHeelLength = 1.2,
            int cutoffCount = 0,
            double cutoffThickness = 0.5,
            double cutoffProjection = 0.5)
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
            p.UpstreamBreast.StemThickness = (decimal)upstreamBreastStemThickness;
            p.UpstreamBreast.Length = (decimal)upstreamBreastLength;
            p.UpstreamBreast.CrownHeight = (decimal)upstreamBreastCrownHeight;
            p.UpstreamBreast.Embedment = (decimal)upstreamBreastEmbedment;
            p.UpstreamBreast.FootingThickness = (decimal)upstreamBreastFootingThickness;
            p.UpstreamBreast.ToeLength = (decimal)upstreamBreastToeLength;
            p.UpstreamBreast.HeelLength = (decimal)upstreamBreastHeelLength;
            p.DownstreamBreast.StemThickness = (decimal)downstreamBreastStemThickness;
            p.DownstreamBreast.Length = (decimal)downstreamBreastLength;
            p.DownstreamBreast.CrownHeight = (decimal)downstreamBreastCrownHeight;
            p.DownstreamBreast.Embedment = (decimal)downstreamBreastEmbedment;
            p.DownstreamBreast.FootingThickness = (decimal)downstreamBreastFootingThickness;
            p.DownstreamBreast.ToeLength = (decimal)downstreamBreastToeLength;
            p.DownstreamBreast.HeelLength = (decimal)downstreamBreastHeelLength;
            p.CutoffCount = cutoffCount;
            p.CutoffThickness = (decimal)cutoffThickness;
            p.CutoffProjection = (decimal)cutoffProjection;

            Hikan.Core.HikanEstimate e = Hikan.Core.HikanEstimator.Calculate(p);

            System.Collections.Generic.Dictionary<string, object> r =
                new System.Collections.Generic.Dictionary<string, object>();
            r["外形幅_m"] = (double)e.OuterWidth;
            r["外形高_m"] = (double)e.OuterHeight;
            r["敷高_m"] = (double)e.InvertLevel;
            r["コンクリート総計_m3"] = (double)e.ConcreteVolume;
            r["函体_m3"] = (double)e.BarrelConcreteVolume;
            r["上流胸壁_m3"] = (double)e.UpstreamBreastVolume;
            r["下流胸壁_m3"] = (double)e.DownstreamBreastVolume;
            r["しゃ水壁_m3"] = (double)e.CutoffTotalVolume;
            r["浸透路長_m"] = (double)e.SeepagePathLength;
            r["頂版_m3"] = (double)e.TopSlabVolume;
            r["底版_m3"] = (double)e.BottomSlabVolume;
            r["側壁_m3"] = (double)e.WallVolume;
            r["型枠_内空_m2"] = (double)e.FormworkInner;
            r["型枠_外側面_m2"] = (double)e.FormworkOuterSide;
            r["型枠_頂版上面_m2"] = (double)e.FormworkTop;
            r["型枠_端面_m2"] = (double)e.FormworkEnd;
            r["型枠_胸壁_m2"] = (double)e.FormworkBreast;
            r["型枠_しゃ水壁_m2"] = (double)e.FormworkCutoff;
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
