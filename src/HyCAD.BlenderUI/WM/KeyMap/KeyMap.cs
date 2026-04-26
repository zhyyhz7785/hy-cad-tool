using System.Collections.Generic;

namespace HyCAD.BlenderUI.WM.KeyMap
{
    public sealed class KeyMapConfig
    {
        public string Name { get; set; }
        public List<KeyMapItem> Items { get; } = new List<KeyMapItem>();
    }
}
