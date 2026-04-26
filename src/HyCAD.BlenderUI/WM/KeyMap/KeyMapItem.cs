using System.Windows.Input;

namespace HyCAD.BlenderUI.WM.KeyMap
{
    public sealed class KeyMapItem
    {
        public Key Key { get; set; }
        public ModifierKeys Modifiers { get; set; }
        public string OperatorId { get; set; }
    }
}
