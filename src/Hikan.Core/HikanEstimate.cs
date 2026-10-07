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
        public decimal ConcreteVolume { get; set; }

        public decimal HorizontalProjection { get; set; }
        public decimal DropHeight { get; set; }

        public decimal FormworkInner { get; set; }
        public decimal FormworkOuterSide { get; set; }
        public decimal FormworkTop { get; set; }
        public decimal FormworkEnd { get; set; }

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
