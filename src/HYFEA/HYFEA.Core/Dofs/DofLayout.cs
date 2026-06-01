using HYFEA.Core.Model;

namespace HYFEA.Core.Dofs;

/// <summary>Maps structural (NodeId, DofType) pairs to packed global equation indices and free/reduced set.</summary>
public sealed class DofLayout
{
    private readonly Dictionary<(NodeId Node, DofType Dof), int> _globalIndex;
    private readonly (NodeId node, DofType dof)[] _pairByGlobal;

    private DofLayout(
        Dictionary<(NodeId Node, DofType Dof), int> globalIndex,
        (NodeId node, DofType dof)[] pairByGlobal,
        int totalDofCount,
        int freeDofCount,
        IReadOnlyList<int> freeToGlobal,
        IReadOnlyList<int> globalToFree,
        HashSet<(NodeId, DofType)> constrained)
    {
        _globalIndex = globalIndex;
        _pairByGlobal = pairByGlobal;
        TotalDofCount = totalDofCount;
        FreeDofCount = freeDofCount;
        FreeToGlobal = freeToGlobal;
        GlobalToFree = globalToFree;
        ConstrainedPairs = constrained;
    }

    public int TotalDofCount { get; }
    public int FreeDofCount { get; }
    public IReadOnlyList<int> FreeToGlobal { get; }
    /// <summary>Length <see cref="TotalDofCount"/>; entry is free-subspace index or -1 if constrained.</summary>
    public IReadOnlyList<int> GlobalToFree { get; }
    public HashSet<(NodeId Node, DofType Dof)> ConstrainedPairs { get; }

    public int GetGlobalIndex(NodeId nodeId, DofType dof)
    {
        if (!_globalIndex.TryGetValue((nodeId, dof), out int g))
            throw new ArgumentException($"DOF ({nodeId.Value},{dof}) is not active in the model.");
        return g;
    }

    public static DofLayout Build(FemProblem problem)
    {
        var required = new Dictionary<NodeId, HashSet<DofType>>();
        foreach (var el in problem.Elements)
        {
            switch (el)
            {
                case Spring1DElementDef s:
                    AddDof(required, s.NodeA, DofType.UX);
                    AddDof(required, s.NodeB, DofType.UX);
                    break;
                case Truss2DElementDef t:
                    AddDof(required, t.NodeA, DofType.UX);
                    AddDof(required, t.NodeA, DofType.UY);
                    AddDof(required, t.NodeB, DofType.UX);
                    AddDof(required, t.NodeB, DofType.UY);
                    break;
                case EulerBeam2DElementDef b:
                    AddDof(required, b.NodeA, DofType.UX);
                    AddDof(required, b.NodeA, DofType.UY);
                    AddDof(required, b.NodeA, DofType.RZ);
                    AddDof(required, b.NodeB, DofType.UX);
                    AddDof(required, b.NodeB, DofType.UY);
                    AddDof(required, b.NodeB, DofType.RZ);
                    break;
                default:
                    throw new NotSupportedException($"Element type {el.GetType().Name} is not supported.");
            }
        }

        foreach (var su in problem.Supports)
        {
            if (!required.TryGetValue(su.NodeId, out var set))
            {
                set = [];
                required[su.NodeId] = set;
            }
            set.Add(su.Dof);
        }

        foreach (var load in problem.LoadCase.Loads)
        {
            if (!required.TryGetValue(load.NodeId, out var set))
            {
                set = [];
                required[load.NodeId] = set;
            }
            set.Add(load.Dof);
        }

        var constrained = new HashSet<(NodeId, DofType)>();
        foreach (var su in problem.Supports)
            constrained.Add((su.NodeId, su.Dof));

        var orderedNodes = required.Keys.OrderBy(n => n.Value).ToList();
        var globalIndexDict = new Dictionary<(NodeId, DofType), int>();
        int next = 0;
        foreach (var nodeId in orderedNodes)
        {
            var dofs = required[nodeId].OrderBy(d => (int)d).ToList();
            foreach (var d in dofs)
                globalIndexDict[(nodeId, d)] = next++;
        }

        int total = next;
        var globalToFree = Enumerable.Repeat(-1, total).ToArray();
        var freeToGlobal = new List<int>();
        var reverse = new (NodeId node, DofType dof)[total];
        foreach (var kv in globalIndexDict)
            reverse[kv.Value] = kv.Key;

        for (int g = 0; g < total; g++)
        {
            var pair = reverse[g];
            if (constrained.Contains(pair))
                continue;
            globalToFree[g] = freeToGlobal.Count;
            freeToGlobal.Add(g);
        }

        return new DofLayout(globalIndexDict, reverse, total, freeToGlobal.Count, freeToGlobal, globalToFree, constrained);
    }

    public (NodeId node, DofType dof) GetPair(int globalIndex) => _pairByGlobal[globalIndex];

    private static void AddDof(Dictionary<NodeId, HashSet<DofType>> map, NodeId node, DofType dof)
    {
        if (!map.TryGetValue(node, out var set))
        {
            set = [];
            map[node] = set;
        }
        set.Add(dof);
    }
}
