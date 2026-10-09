using HyCAD.Tables.Formulas;
using Xunit;

namespace HyCADTool.LinuxTests
{
    public class FormulaAuditTests
    {
        [Theory]
        [InlineData("=SIN()")]
        [InlineData("COS()")]
        [InlineData("ATAN2(1)")]
        [InlineData("POWER(2)")]
        [InlineData("MOD(1)")]
        [InlineData("LOG()")]
        [InlineData("ROUND()")]
        [InlineData("SQRT()")]
        [InlineData("FACT()")]
        public void Too_few_scalar_arguments_return_value_error(string formula)
        {
            var result = FormulaEvaluator.Evaluate(formula);

            Assert.True(result.IsError);
            Assert.Equal("#VALUE!", result.ToDisplay());
        }

        [Fact]
        public void Unknown_function_stays_a_name_error()
        {
            Assert.Equal("#NAME?", FormulaEvaluator.Evaluate("UNKNOWN()").ToDisplay());
        }

        [Fact]
        public void Power_is_left_associative_like_excel()
        {
            Assert.Equal(64, FormulaEvaluator.Evaluate("2^3^2").Value, 9);
            Assert.Equal(512, FormulaEvaluator.Evaluate("2^(3^2)").Value, 9);
            Assert.Equal(0.125, FormulaEvaluator.Evaluate("2^-3").Value, 9);
            Assert.Equal(-4, FormulaEvaluator.Evaluate("-2^2").Value, 9);
            Assert.Equal(1, FormulaEvaluator.Evaluate("SIN(PI/2)").Value, 9);
        }
    }
}
