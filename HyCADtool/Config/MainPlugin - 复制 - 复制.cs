// HyCADTool/MainPlugin.cs
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Interfaces;
using HyCADTool.Services;
using HyCADTool.Tools;
using Exception = Autodesk.AutoCAD.Runtime.Exception;
[assembly: CommandClass(typeof(HyCADTool.MainPlugin))]
namespace HyCADTool
{
    public class MainPlugin : IExtensionApplication
    {
        private readonly Editor _editor = Application.DocumentManager.MdiActiveDocument.Editor;
        private readonly ICadService _cadService;
        private readonly IAreaFactory _areaFactory;//桩标准区域
        private readonly IConfigService _configService;
        //private readonly PilePanel _pilePanel;
        public MainPlugin()
        {
           // EtGpt.PrewarmAnnotation();
            _cadService = new AutoCadService();
            _configService = new ConfigService();
            _areaFactory = new AreaFactory(_cadService, _configService);
            // _pilePanel = new PilePanel(_cadService, _areaFactory, _configService);
        }
        public void Initialize()
        {
            try
            {
                _configService.InitializeStyle();
                RegisterAppEvents();
                foreach (Document doc in Application.DocumentManager)
                {
                    RegisterDocEvents(doc);
                }
                _editor.WriteMessage("\nHyCADTool Plugin Initialized.");
                // 这里可以显示 PilePanel，例如通过 PaletteSet
                // Application.ShowModelessDialog(_pilePanel);
            }
            catch (Exception ex)
            {
                _editor.WriteMessage($"\n初始化失败: {ex.Message}");
            }
        }
        public void Terminate()
        {
            try
            {
                Application.DocumentManager.DocumentActivated -= DocumentManager_DocumentActivated;
                Application.DocumentManager.DocumentCreated -= DocumentManager_DocumentCreated;
                _editor.WriteMessage("\nHyCADTool Plugin Terminated.");
            }
            catch (Exception ex)
            {
                _editor.WriteMessage($"\n终止失败: {ex.Message}");
            }
        }
        private void RegisterAppEvents()
        {
            Application.DocumentManager.DocumentActivated += DocumentManager_DocumentActivated;
            Application.DocumentManager.DocumentCreated += DocumentManager_DocumentCreated;
        }
        private void RegisterDocEvents(Document doc)
        {
            // 可扩展
        }
        private void DocumentManager_DocumentActivated(object sender, DocumentCollectionEventArgs e) { }
        private void DocumentManager_DocumentCreated(object sender, DocumentCollectionEventArgs e)
        {
            RegisterDocEvents(e.Document);
        }
    }
}