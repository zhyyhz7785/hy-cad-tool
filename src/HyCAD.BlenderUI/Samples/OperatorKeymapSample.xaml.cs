using System.Windows;
using System.Windows.Input;
using HyCAD.BlenderUI.Controls;
using HyCAD.BlenderUI.Theming;
using HyCAD.BlenderUI.WM.Dispatch;
using HyCAD.BlenderUI.WM.KeyMap;
using HyCAD.BlenderUI.WM.Operators;

namespace HyCAD.BlenderUI.Samples
{
    public partial class OperatorKeymapSample : BlenderWindow
    {
        private readonly BlenderInputRouter _router = new BlenderInputRouter();

        public OperatorKeymapSample()
        {
            InitializeComponent();
            BlenderThemeManager.Apply(BlenderThemeManager.BlenderThemeName.BlenderDark);

            OperatorRegistry.Register("sample.log", () => new LogOperator(this));

            _router.KeyMap.Items.Add(new KeyMapItem
            {
                Key = Key.D,
                Modifiers = ModifierKeys.Control | ModifierKeys.Shift,
                OperatorId = "sample.log",
            });

            Loaded += (_, __) => _router.Attach(this);
            Closed += (_, __) => _router.Detach(this);
        }

        private sealed class LogOperator : Operator
        {
            private readonly OperatorKeymapSample _win;

            public LogOperator(OperatorKeymapSample win) => _win = win;

            public override string Id => "sample.log";

            public override OperatorResult Execute(OperatorContext ctx)
            {
                _win.Log.Text = "sample.log 已执行（Ctrl+Shift+D）";
                return OperatorResult.Finished;
            }
        }
    }
}
