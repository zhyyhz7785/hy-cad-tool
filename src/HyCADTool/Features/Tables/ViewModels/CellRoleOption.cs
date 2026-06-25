using System.Windows.Controls;

namespace HyCADTool.Features.Tables.ViewModels
{
    /// <summary>
    /// Inspector / ComboBox 用 Role 选项（含「无」）。
    /// </summary>
    public sealed class CellRoleOption
    {
        public CellRoleOption(string displayName, HyCAD.Tables.Structure.CellRole? role)
        {
            DisplayName = displayName;
            Role = role;
        }

        public string DisplayName { get; }

        public HyCAD.Tables.Structure.CellRole? Role { get; }
    }
}
