using HyCADTool.Domain.Entities.Pile;

namespace HyCADTool.Domain.ValueObjects.Configuration.Modules
{
    public sealed class PileConfiguration
    {
        public PileConfiguration(
            PileSectionType section,
            double diameter,
            PileArrangementType arrangementType,
            double arrangeRate,
            (double up, double down, double left, double right) margin,
            double minPileCenterDistance,
            double inputDisplacementRate,
            double inputDistanceFromContour)
        {
            Section = section;
            DiameterOrEdge = diameter;
            ArrangementType = arrangementType;
            PileArrangeRate = arrangeRate;
            Margin = margin;
            MinPileCenterDistance = minPileCenterDistance;
            InputDisplacementRate = inputDisplacementRate;
            InputDistanceFromContour = inputDistanceFromContour;
        }

        public PileSectionType Section { get; }
        public double DiameterOrEdge { get; }
        public PileArrangementType ArrangementType { get; }
        public double PileArrangeRate { get; }
        public (double Up, double Down, double Left, double Right) Margin { get; }
        public double MinPileCenterDistance { get; }
        public double InputDisplacementRate { get; }
        public double InputDistanceFromContour { get; }

        public static PileConfiguration CreateDefault()
        {
            return new PileConfiguration(
                PileSectionType.Circle,
                400.0,
                PileArrangementType.Rectangle,
                0.5,
                (400.0, 400.0, 400.0, 400.0),
                1200.0,
                0.02,
                400.0);
        }
    }
}
