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
            "川裏側胸壁_m3",
            "川表側胸壁_m3",
            "しゃ水壁_m3",
            "川裏側翼壁_m3",
            "川表側翼壁_m3",
            "川裏側堤防保護部_m3",
            "川表側堤防保護部_m3",
            "門柱_m3",
            "操作台_m3",
            "土留壁_m3",
            "川裏側翼壁開き角_度",
            "川表側翼壁開き角_度",
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
            "型枠_翼壁_m2",
            "型枠_門柱操作台_m2",
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
            double upstreamBreastStemThickness = 0.4,
            double upstreamBreastPosition = 1.0,
            double upstreamBreastLength = 2.0,
            double upstreamBreastCrownHeight = 4.0,
            double upstreamBreastFootingThickness = 0.5,
            double upstreamBreastToeLength = 0.8,
            double upstreamBreastHeelLength = 1.2,
            double downstreamBreastStemThickness = 0.4,
            double downstreamBreastPosition = 18.0,
            double downstreamBreastLength = 2.0,
            double downstreamBreastCrownHeight = 4.0,
            double downstreamBreastFootingThickness = 0.5,
            double downstreamBreastToeLength = 0.8,
            double downstreamBreastHeelLength = 1.2,
            double upstreamWingStemThickness = 0.4,
            double upstreamWingLength = 3.0,
            double upstreamWingEndWidth = 3.0,
            double upstreamWingCrownStart = 3.0,
            double upstreamWingCrownEnd = 2.0,
            double upstreamWingFloorThickness = 0.4,
            double downstreamWingStemThickness = 0.4,
            double downstreamWingLength = 3.0,
            double downstreamWingEndWidth = 3.0,
            double downstreamWingCrownStart = 3.0,
            double downstreamWingCrownEnd = 2.0,
            double downstreamWingFloorThickness = 0.4,
            bool upstreamWingProtection = false,
            double upstreamProtectionWidth = 1.0,
            double upstreamProtectionStemThickness = 0.4,
            double upstreamProtectionStemHeight = 2.0,
            double upstreamProtectionToeLength = 0.5,
            double upstreamProtectionHeelLength = 1.0,
            bool downstreamWingProtection = false,
            double downstreamProtectionWidth = 1.0,
            double downstreamProtectionStemThickness = 0.4,
            double downstreamProtectionStemHeight = 2.0,
            double downstreamProtectionToeLength = 0.5,
            double downstreamProtectionHeelLength = 1.0,
            bool gate = true,
            double gateDeckLength = 2.7,
            double gateDeckWidth = 5.8,
            double gateDeckAxialOverhang = 0.7,
            double gateDeckSideOverhang = 1.1,
            double gateDeckThickness = 0.6,
            bool gateBridge = true,
            double gateSeatLength = 0.4,
            double gateSeatWidth = 1.4,
            double gateSeatDepth = 0.3,
            double gateLongitudinalWallWidth = 0.9,
            double gateTransverseWallThickness = 0.4,
            double gateLongitudinalWallThickness = 0.4,
            double gateTransverseWallWidth = 0.4,
            double gateColumnHeight = 6.4,
            double gateHaunchHeight = 0.2,
            double gateRetainingWallHeight = 0.5,
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
            p.UpstreamBreast.Position = (decimal)upstreamBreastPosition;
            p.UpstreamBreast.Length = (decimal)upstreamBreastLength;
            p.UpstreamBreast.CrownHeight = (decimal)upstreamBreastCrownHeight;
            p.UpstreamBreast.FootingThickness = (decimal)upstreamBreastFootingThickness;
            p.UpstreamBreast.ToeLength = (decimal)upstreamBreastToeLength;
            p.UpstreamBreast.HeelLength = (decimal)upstreamBreastHeelLength;
            p.DownstreamBreast.StemThickness = (decimal)downstreamBreastStemThickness;
            p.DownstreamBreast.Position = (decimal)downstreamBreastPosition;
            p.DownstreamBreast.Length = (decimal)downstreamBreastLength;
            p.DownstreamBreast.CrownHeight = (decimal)downstreamBreastCrownHeight;
            p.DownstreamBreast.FootingThickness = (decimal)downstreamBreastFootingThickness;
            p.DownstreamBreast.ToeLength = (decimal)downstreamBreastToeLength;
            p.DownstreamBreast.HeelLength = (decimal)downstreamBreastHeelLength;
            p.UpstreamWing.StemThickness = (decimal)upstreamWingStemThickness;
            p.UpstreamWing.Length = (decimal)upstreamWingLength;
            p.UpstreamWing.EndWidth = (decimal)upstreamWingEndWidth;
            p.UpstreamWing.CrownStart = (decimal)upstreamWingCrownStart;
            p.UpstreamWing.CrownEnd = (decimal)upstreamWingCrownEnd;
            p.UpstreamWing.FloorThickness = (decimal)upstreamWingFloorThickness;
            p.DownstreamWing.StemThickness = (decimal)downstreamWingStemThickness;
            p.DownstreamWing.Length = (decimal)downstreamWingLength;
            p.DownstreamWing.EndWidth = (decimal)downstreamWingEndWidth;
            p.DownstreamWing.CrownStart = (decimal)downstreamWingCrownStart;
            p.DownstreamWing.CrownEnd = (decimal)downstreamWingCrownEnd;
            p.DownstreamWing.FloorThickness = (decimal)downstreamWingFloorThickness;
            p.UpstreamWing.Protection.Exists = upstreamWingProtection;
            p.UpstreamWing.Protection.Width = (decimal)upstreamProtectionWidth;
            p.UpstreamWing.Protection.StemThickness = (decimal)upstreamProtectionStemThickness;
            p.UpstreamWing.Protection.StemHeight = (decimal)upstreamProtectionStemHeight;
            p.UpstreamWing.Protection.ToeLength = (decimal)upstreamProtectionToeLength;
            p.UpstreamWing.Protection.HeelLength = (decimal)upstreamProtectionHeelLength;
            p.DownstreamWing.Protection.Exists = downstreamWingProtection;
            p.DownstreamWing.Protection.Width = (decimal)downstreamProtectionWidth;
            p.DownstreamWing.Protection.StemThickness = (decimal)downstreamProtectionStemThickness;
            p.DownstreamWing.Protection.StemHeight = (decimal)downstreamProtectionStemHeight;
            p.DownstreamWing.Protection.ToeLength = (decimal)downstreamProtectionToeLength;
            p.DownstreamWing.Protection.HeelLength = (decimal)downstreamProtectionHeelLength;
            p.Gate.Exists = gate;
            p.Gate.DeckLength = (decimal)gateDeckLength;
            p.Gate.DeckWidth = (decimal)gateDeckWidth;
            p.Gate.DeckAxialOverhang = (decimal)gateDeckAxialOverhang;
            p.Gate.DeckSideOverhang = (decimal)gateDeckSideOverhang;
            p.Gate.DeckThickness = (decimal)gateDeckThickness;
            p.Gate.Bridge = gateBridge;
            p.Gate.SeatLength = (decimal)gateSeatLength;
            p.Gate.SeatWidth = (decimal)gateSeatWidth;
            p.Gate.SeatDepth = (decimal)gateSeatDepth;
            p.Gate.LongitudinalWallWidth = (decimal)gateLongitudinalWallWidth;
            p.Gate.TransverseWallThickness = (decimal)gateTransverseWallThickness;
            p.Gate.LongitudinalWallThickness = (decimal)gateLongitudinalWallThickness;
            p.Gate.TransverseWallWidth = (decimal)gateTransverseWallWidth;
            p.Gate.ColumnHeight = (decimal)gateColumnHeight;
            p.Gate.HaunchHeight = (decimal)gateHaunchHeight;
            p.Gate.RetainingWallHeight = (decimal)gateRetainingWallHeight;
            p.CutoffCount = cutoffCount;
            p.CutoffThickness = (decimal)cutoffThickness;
            p.CutoffProjection = (decimal)cutoffProjection;

            // 新規入力として検査する(胸壁・翼壁は必ず設置。たて壁厚・側壁厚 0.40 m 以上)。
            Hikan.Core.HikanValidator.EnsureInputValid(p);
            Hikan.Core.HikanEstimate e = Hikan.Core.HikanEstimator.Calculate(p);

            System.Collections.Generic.Dictionary<string, object> r =
                new System.Collections.Generic.Dictionary<string, object>();
            r["外形幅_m"] = (double)e.OuterWidth;
            r["外形高_m"] = (double)e.OuterHeight;
            r["敷高_m"] = (double)e.InvertLevel;
            r["コンクリート総計_m3"] = (double)e.ConcreteVolume;
            r["函体_m3"] = (double)e.BarrelConcreteVolume;
            r["川裏側胸壁_m3"] = (double)e.UpstreamBreastVolume;
            r["川表側胸壁_m3"] = (double)e.DownstreamBreastVolume;
            r["しゃ水壁_m3"] = (double)e.CutoffTotalVolume;
            r["川裏側翼壁_m3"] = (double)e.UpstreamWingVolume;
            r["川表側翼壁_m3"] = (double)e.DownstreamWingVolume;
            r["川裏側堤防保護部_m3"] = (double)e.UpstreamProtectionVolume;
            r["川表側堤防保護部_m3"] = (double)e.DownstreamProtectionVolume;
            r["門柱_m3"] = (double)e.GateColumnVolume;
            r["操作台_m3"] = (double)e.GateDeckVolume;
            r["土留壁_m3"] = (double)e.GateRetainingWallVolume;
            r["川裏側翼壁開き角_度"] = (double)e.UpstreamWingFlareAngle;
            r["川表側翼壁開き角_度"] = (double)e.DownstreamWingFlareAngle;
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
            r["型枠_翼壁_m2"] = (double)e.FormworkWing;
            r["型枠_門柱操作台_m2"] = (double)e.FormworkGate;
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
