using HYFEA.Core.Geometry;

namespace HYFEA.Core.Model;

public readonly record struct FemNode(NodeId Id, Point2D Position);
