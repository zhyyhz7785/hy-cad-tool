using HyCAD.Tables.Structure;

namespace HyCAD.Tables.Formulas;

/// <summary>
/// 公式求值上下文：读取单元格/区域数值。
/// </summary>
public interface IFormulaContext
{
    /// <summary>算术引用：空→0，非数→抛 <see cref="FormulaException"/>。</summary>
    double GetNumber(CellAddr addr);

    /// <summary>聚合读取：空/文本→false，数值→true。</summary>
    bool TryGetNumber(CellAddr addr, out double value);
}
