using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Interfaces;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services
{
    /// <summary>
    /// 图层管理器实现
    /// 负责 AutoCAD 图层的创建和配置
    /// </summary>
    public class LayerManager : ILayerManager
    {
        private readonly Database _database;
        
        public LayerManager()
        {
            _database = Application.DocumentManager.MdiActiveDocument.Database;
        }
        
        public LayerManager(Database database)
        {
            _database = database;
        }
        
        /// <summary>
        /// 确保图层存在，如果不存在则创建
        /// </summary>
        public void EnsureLayer(Transaction tr, string layerName, short colorIndex)
        {
            var layerTable = (LayerTable)tr.GetObject(_database.LayerTableId, OpenMode.ForRead);
            
            if (!layerTable.Has(layerName))
            {
                layerTable.UpgradeOpen();
                var layer = new LayerTableRecord
                {
                    Name = layerName,
                    Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex)
                };
                layerTable.Add(layer);
                tr.AddNewlyCreatedDBObject(layer, true);
                layerTable.DowngradeOpen();
            }
            else
            {
                var layer = (LayerTableRecord)tr.GetObject(layerTable[layerName], OpenMode.ForWrite);
                layer.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex);
            }
        }
        
        /// <summary>
        /// 批量创建图层
        /// </summary>
        public void EnsureLayers(Transaction tr, params (string name, short colorIndex)[] layers)
        {
            foreach (var (name, colorIndex) in layers)
            {
                EnsureLayer(tr, name, colorIndex);
            }
        }
    }
}

