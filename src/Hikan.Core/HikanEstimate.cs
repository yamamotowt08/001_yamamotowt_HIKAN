namespace Hikan.Core
{
    /// <summary>派生量・数量の保持(ロジックは HikanEstimator)。単位はメートル系。</summary>
    public sealed class HikanEstimate
    {
        public decimal OuterWidth { get; set; }
        public decimal OuterHeight { get; set; }
        public decimal InvertLevel { get; set; }

        public decimal OuterSectionArea { get; set; }
        public decimal InnerSectionArea { get; set; }
        public decimal ConcreteSectionArea { get; set; }

        public decimal TopSlabVolume { get; set; }
        public decimal BottomSlabVolume { get; set; }
        public decimal WallVolume { get; set; }
        /// <summary>函体のみのコンクリート [m3]</summary>
        public decimal BarrelConcreteVolume { get; set; }
        /// <summary>全部材のコンクリート [m3](函体 + 胸壁 + しゃ水壁 + 翼壁 + 堤防保護部)</summary>
        public decimal ConcreteVolume { get; set; }

        public decimal UpstreamBreastVolume { get; set; }
        public decimal DownstreamBreastVolume { get; set; }
        public decimal UpstreamWingVolume { get; set; }
        public decimal DownstreamWingVolume { get; set; }
        /// <summary>川裏側翼壁の開き角 [度]</summary>
        public decimal UpstreamWingFlareAngle { get; set; }
        /// <summary>川表側翼壁の開き角 [度]</summary>
        public decimal DownstreamWingFlareAngle { get; set; }
        /// <summary>門柱 2 本のコンクリート [m3]</summary>
        public decimal GateColumnVolume { get; set; }
        /// <summary>操作台のコンクリート [m3]</summary>
        public decimal GateDeckVolume { get; set; }
        /// <summary>川裏側堤防保護部のコンクリート [m3](翼壁との重なりを含む)</summary>
        public decimal UpstreamProtectionVolume { get; set; }
        /// <summary>川表側堤防保護部のコンクリート [m3](翼壁との重なりを含む)</summary>
        public decimal DownstreamProtectionVolume { get; set; }
        public decimal CutoffTotalVolume { get; set; }
        public decimal CutoffSpacing { get; set; }
        public decimal SeepagePathLength { get; set; }

        public decimal HorizontalProjection { get; set; }
        public decimal DropHeight { get; set; }

        public decimal FormworkInner { get; set; }
        public decimal FormworkOuterSide { get; set; }
        public decimal FormworkTop { get; set; }
        public decimal FormworkEnd { get; set; }
        public decimal FormworkBreast { get; set; }
        public decimal FormworkCutoff { get; set; }
        public decimal FormworkWing { get; set; }
        public decimal FormworkGate { get; set; }

        public decimal BlockLength { get; set; }
        public decimal ConcretePerBlock { get; set; }

        public decimal ExcavationBottomWidth { get; set; }
        public decimal ExcavationBottomLength { get; set; }
        public decimal ExcavationDepth { get; set; }
        public decimal ExcavationVolume { get; set; }
        public decimal FoundationVolume { get; set; }
        public decimal OccupiedVolume { get; set; }
        public decimal BackfillVolume { get; set; }
        public decimal SurplusVolume { get; set; }
    }
}
