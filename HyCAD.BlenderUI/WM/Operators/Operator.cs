using System.Windows.Input;

namespace HyCAD.BlenderUI.WM.Operators
{
    public abstract class Operator
    {
        public abstract string Id { get; }

        public virtual bool Poll(OperatorContext ctx) => true;

        public abstract OperatorResult Execute(OperatorContext ctx);

        public virtual OperatorResult Invoke(KeyEventArgs e, OperatorContext ctx) => Execute(ctx);
    }
}
