using System.Collections.Generic;

namespace HyCADTool.Shared.AutoCAD.Selection.Rules
{
    public abstract class SelectionPredicate
    {
    }

    public sealed class PropertyPredicate : SelectionPredicate
    {
        public string PropertyName { get; set; }

        public ComparisonOperator Operator { get; set; }

        public object Value { get; set; }

        public object SecondValue { get; set; }
    }

    public sealed class CompositePredicate : SelectionPredicate
    {
        public LogicalOperator Logic { get; set; }

        public IReadOnlyList<SelectionPredicate> Children { get; set; } = new List<SelectionPredicate>();
    }

    public sealed class FunctionPredicate : SelectionPredicate
    {
        public string FunctionName { get; set; }

        public IReadOnlyList<object> Arguments { get; set; } = new List<object>();
    }
}
