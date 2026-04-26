using System;
using System.Windows.Controls;
using HyCAD.BlenderUI.Screen.Areas;

namespace HyCAD.BlenderUI.Controls.Spaces
{
    /// <summary>按 <see cref="SpaceTypeId"/> 创建对应 Space 壳（阶段 5）。</summary>
    public static class SpaceShellFactory
    {
        public static UserControl Create(SpaceTypeId id)
        {
            switch (id)
            {
                case SpaceTypeId.View3D: return new View3DSpace();
                case SpaceTypeId.Outliner: return new OutlinerSpace();
                case SpaceTypeId.Properties: return new PropertiesSpace();
                case SpaceTypeId.GraphEditor: return new GraphEditorSpace();
                case SpaceTypeId.DopeSheet: return new DopeSheetSpace();
                case SpaceTypeId.NlaEditor: return new NlaEditorSpace();
                case SpaceTypeId.ImageEditor: return new ImageEditorSpace();
                case SpaceTypeId.NodeEditor: return new NodeEditorSpace();
                case SpaceTypeId.Sequencer: return new SequencerSpace();
                case SpaceTypeId.MovieClip: return new MovieClipSpace();
                case SpaceTypeId.TextEditor: return new TextEditorSpace();
                case SpaceTypeId.Info: return new InfoSpace();
                case SpaceTypeId.Console: return new ConsoleSpace();
                case SpaceTypeId.FileBrowser: return new FileBrowserSpace();
                case SpaceTypeId.Preferences: return new PreferencesSpace();
                case SpaceTypeId.Spreadsheet: return new SpreadsheetSpace();
                case SpaceTypeId.TopBar: return new TopBarSpace();
                case SpaceTypeId.StatusBar: return new StatusBarSpace();
                case SpaceTypeId.Timeline: return new TimelineSpace();
                default:
                    throw new ArgumentOutOfRangeException(nameof(id), id, null);
            }
        }
    }
}
