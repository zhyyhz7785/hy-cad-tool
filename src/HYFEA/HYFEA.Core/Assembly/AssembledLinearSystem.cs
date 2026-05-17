using HYFEA.Core.Dofs;
using HYFEA.Core.LinearAlgebra;

namespace HYFEA.Core.Assembly;

public sealed class AssembledLinearSystem
{
    public AssembledLinearSystem(DenseMatrix stiffness, DenseVector force, DofLayout layout)
    {
        Stiffness = stiffness;
        Force = force;
        Layout = layout;
    }

    public DenseMatrix Stiffness { get; }
    public DenseVector Force { get; }
    public DofLayout Layout { get; }
}

public sealed class ReducedLinearSystem
{
    public ReducedLinearSystem(DenseMatrix stiffness, DenseVector force, AssembledLinearSystem full)
    {
        Stiffness = stiffness;
        Force = force;
        Full = full;
    }

    public DenseMatrix Stiffness { get; }
    public DenseVector Force { get; }
    public AssembledLinearSystem Full { get; }
}
