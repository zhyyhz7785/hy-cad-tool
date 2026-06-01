using HYFEA.Core.Boundary;
using HyCAD.Geometry;
using HYFEA.Core.Loads;
using HYFEA.Core.Materials;
using HYFEA.Core.Model;
using HYFEA.Core.Sections;

namespace HYFEA.Core.Model;

public sealed class FemProblemBuilder
{
    private readonly List<FemNode> _nodes = [];
    private readonly List<FemElementDefinition> _elements = [];
    private readonly Dictionary<MaterialId, MaterialDefinition> _materials = [];
    private readonly Dictionary<SectionId, SectionDefinition> _sections = [];
    private readonly List<FixedSupport> _supports = [];
    private readonly List<NodalLoad> _loads = [];
    private readonly List<IElementLoad> _elementLoads = [];
    private UnitSystem _units = UnitSystem.MmN;
    private UnitDescriptor? _customUnits;

    public FemProblemBuilder WithUnits(UnitSystem units, UnitDescriptor? custom = null)
    {
        _units = units;
        _customUnits = custom;
        return this;
    }

    public FemProblemBuilder AddNode(NodeId id, double x, double y)
    {
        _nodes.Add(new FemNode(id, new Point2D(x, y)));
        return this;
    }

    public FemProblemBuilder AddMaterial(MaterialDefinition material)
    {
        _materials[material.Id] = material;
        return this;
    }

    public FemProblemBuilder AddSection(SectionDefinition section)
    {
        _sections[section.Id] = section;
        return this;
    }

    public FemProblemBuilder AddSpring1D(ElementId id, NodeId a, NodeId b, MaterialId mat, SectionId sec)
    {
        _elements.Add(new Spring1DElementDef(id, a, b, mat, sec));
        return this;
    }

    public FemProblemBuilder AddTruss2D(ElementId id, NodeId a, NodeId b, MaterialId mat, SectionId sec)
    {
        _elements.Add(new Truss2DElementDef(id, a, b, mat, sec));
        return this;
    }

    public FemProblemBuilder AddEulerBeam2D(ElementId id, NodeId a, NodeId b, MaterialId mat, SectionId sec)
    {
        _elements.Add(new EulerBeam2DElementDef(id, a, b, mat, sec));
        return this;
    }

    public FemProblemBuilder AddSupport(FixedSupport support)
    {
        _supports.Add(support);
        return this;
    }

    public FemProblemBuilder AddLoad(NodalLoad load)
    {
        _loads.Add(load);
        return this;
    }

    public FemProblemBuilder AddElementLoad(IElementLoad load)
    {
        _elementLoads.Add(load);
        return this;
    }

    public FemProblem Build()
    {
        return new FemProblem(
            _nodes,
            _elements,
            _materials,
            _sections,
            _supports,
            new LoadCase(_loads, _elementLoads),
            _units,
            _customUnits);
    }
}
