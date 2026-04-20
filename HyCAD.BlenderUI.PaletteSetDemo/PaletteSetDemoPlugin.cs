using System;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;

[assembly: ExtensionApplication(typeof(HyCAD.BlenderUI.PaletteSetDemo.PaletteSetDemoApp))]
[assembly: CommandClass(typeof(HyCAD.BlenderUI.PaletteSetDemo.PaletteSetDemoCommands))]

namespace HyCAD.BlenderUI.PaletteSetDemo
{
    /// <summary>NETLOAD 后注册命令；PaletteSet 内嵌 HyCAD.BlenderUI（阶段 7 回归样本）。</summary>
    public sealed class PaletteSetDemoApp : IExtensionApplication
    {
        public void Initialize()
        {
        }

        public void Terminate()
        {
        }
    }

    public sealed class PaletteSetDemoCommands
    {
        private static readonly Guid PaletteGuid = new Guid("C7E8F9A1-2345-6789-ABCD-EF0123456789");
        private static PaletteSet _palette;

        /// <summary>打开 Blender UI 1:1 PaletteSet 演示（单 Area + Properties 壳）。</summary>
        [CommandMethod("BLENDERUI_DEMO")]
        public void ShowBlenderUiPaletteDemo()
        {
            if (_palette == null)
            {
                _palette = new PaletteSet("Blender UI 1:1 Demo", PaletteGuid)
                {
                    Style = PaletteSetStyles.ShowCloseButton |
                            PaletteSetStyles.ShowAutoHideButton |
                            PaletteSetStyles.Snappable,
                    DockEnabled = (DockSides)((int)DockSides.Left | (int)DockSides.Right |
                                              (int)DockSides.Top | (int)DockSides.Bottom),
                    MinimumSize = new System.Drawing.Size(360, 280),
                    Size = new System.Drawing.Size(420, 520)
                };
                var panel = new PaletteSetDemoPanel();
                _palette.AddVisual("Blender", panel);
            }

            _palette.Visible = true;
        }
    }
}
