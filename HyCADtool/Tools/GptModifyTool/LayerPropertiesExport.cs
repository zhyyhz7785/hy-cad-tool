using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
namespace HyCADTool.Tools
{
    public static partial class ZTools
    {
        #region 主要方法
        // 导出图层信息到Markdown
        public static void ExportLayersToMarkdown()
        {
            // 获取图层表记录列表
            List<LayerTableRecord> layerTableRecords = GetLayerTableRecords();
            var filePath = ZTools.GetSaveFilePath();
            ExportLayerRecordsToMarkdown(layerTableRecords, filePath);
        }
        // 从Markdown导入图层
        public static void ImportLayerRecordsFromMarkdown()
        {
            var filePath = ZTools.GetOpenFilePath();
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("文件路径不能为空", nameof(filePath));
            }
            var rows = GetMarkdownRows(filePath);
            ProcessRowsInTransaction(rows);
        }
        #endregion
        #region 帮助方法
        #region 导出
        public static void ExportLayerRecordsToMarkdown(List<LayerTableRecord> layerTableRecords, string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("文件路径不能为空", nameof(filePath));
            }
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Layer Table Records");
            sb.AppendLine("| 图层名称 | 颜色 | 线型 | 线宽 | 透明度 | 说明 |");
            sb.AppendLine("|----------|------|------|------|--------|------|");
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                foreach (var layer in layerTableRecords)
                {
                    // 获取线型名称
                    string lineTypeName = GetLinetypeName(layer);
                    sb.AppendLine($"| {layer.Name} | {layer.Color.ColorIndex} | {lineTypeName} | {layer.LineWeight} | {layer.Transparency.GetTransparencyString()} | {layer.Description} |");
                }
                trans.Commit();
            }
            // 保存Markdown文件到指定路径
            File.WriteAllText(filePath, sb.ToString());
        }
        // 获取图层表记录
        public static List<LayerTableRecord> GetLayerTableRecords()
        {
            List<LayerTableRecord> layerTableRecords = new List<LayerTableRecord>();
            // 获取当前文档和数据库
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            // 添加文档锁
            using (doc.LockDocument())
            {
                // 启动一个事务
                using (Transaction trans = db.TransactionManager.StartTransaction())
                {
                    // 获取图层表
                    LayerTable layerTable = (LayerTable)trans.GetObject(db.LayerTableId, OpenMode.ForRead);
                    // 遍历图层表中的每个图层表记录
                    foreach (ObjectId layerId in layerTable)
                    {
                        // 获取图层表记录
                        LayerTableRecord layerTableRecord = (LayerTableRecord)trans.GetObject(layerId, OpenMode.ForRead);
                        // 将图层表记录添加到列表中
                        layerTableRecords.Add(layerTableRecord);
                    }
                    // 提交事务
                    trans.Commit();
                }
            }
            return layerTableRecords;
        }
        #endregion
        #region 导入
        public static IEnumerable<string[]> GetMarkdownRows(string filePath)
        {
            var lines = File.ReadAllLines(filePath);
            var rows = new List<string[]>();
            foreach (var line in lines.Skip(2)) // 跳过标题行
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var cells = line.Split('|').Select(cell => cell.Trim()).ToArray();
                rows.Add(cells);
            }
            return rows;
        }
        public static void ProcessRowsInTransaction(IEnumerable<string[]> rows)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            using (doc.LockDocument())
            {
                using (Transaction trans = db.TransactionManager.StartTransaction())
                {
                    LayerTable layerTable = (LayerTable)trans.GetObject(db.LayerTableId, OpenMode.ForRead);
                    foreach (var row in rows)
                    {
                        ProcessRowData(row, layerTable, db, trans);
                    }
                    trans.Commit();
                }
            }
        }
        public static void ProcessRowData(string[] row, LayerTable layerTable, Database db, Transaction trans)
        {
            string name = row[0];
            string colorName = row[1];
            string lineType = row[2];
            string lineWeight = row[3];
            string transparency = row[4];
            string description = row[5];
            if (!layerTable.Has(name))
            {
                layerTable.UpgradeOpen();
                LayerTableRecord layerTableRecord = new LayerTableRecord
                {
                    Name = name,
                    Color = SetColorIndex(colorName),
                    LinetypeObjectId = GetOrCreateLinetypeId(db, lineType, trans),
                    LineWeight = (LineWeight)Enum.Parse(typeof(LineWeight), lineWeight)
                };
                layerTable.Add(layerTableRecord);
                trans.AddNewlyCreatedDBObject(layerTableRecord, true);
                // 设置剩余的图层属性
                layerTableRecord.Transparency = transparency.SetTransparency();
                layerTableRecord.Description = description;
            }
        }
        #endregion
        // 获取线型名称
        public static string GetLinetypeName(LayerTableRecord layer)
        {
            string lineTypeName = "ByLayer";
            if (layer.LinetypeObjectId.IsValid)
            {
                using (var lineTypeTr = layer.LinetypeObjectId.GetObject(OpenMode.ForRead) as LinetypeTableRecord)
                {
                    if (lineTypeTr != null)
                    {
                        lineTypeName = lineTypeTr.Name;
                    }
                }
            }
            return lineTypeName;
        }
        // 获取或创建线型ID
        public static ObjectId GetOrCreateLinetypeId(Database db, string linetypeName, Transaction trans)
        {
            LinetypeTable linetypeTable = (LinetypeTable)trans.GetObject(db.LinetypeTableId, OpenMode.ForRead);
            if (linetypeTable.Has(linetypeName))
            {
                return linetypeTable[linetypeName];
            }
            else
            {
                // 获取AutoCAD支持文件路径
                string supportPath = HostApplicationServices.Current.FindFile("acad.lin", db, FindFileHint.Default);
                string directoryPath = System.IO.Path.GetDirectoryName(supportPath);
                List<string> allSupportPaths = new List<string> { directoryPath };
                // 获取所有支持文件路径
                try
                {
                    string acadSupportPath = (string)Application.GetSystemVariable("ACAD");
                    var additionalPaths = acadSupportPath.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    allSupportPaths.AddRange(additionalPaths);
                }
                catch (Autodesk.AutoCAD.Runtime.Exception ex)
                {
                    // 记录异常信息并继续
                    Console.WriteLine($"Error retrieving ACAD system variable: {ex.Message}");
                }
                // 查找所有支持目录中的线型文件
                foreach (var path in allSupportPaths)
                {
                    var linetypeFiles = System.IO.Directory.GetFiles(path, "*.lin");
                    foreach (var linetypeFile in linetypeFiles)
                    {
                        // 检查文件是否包含目标线型
                        if (FileContainsLinetype(linetypeFile, linetypeName))
                        {
                            // 尝试加载线型文件并加载线型
                            try
                            {
                                db.LoadLineTypeFile(linetypeName, linetypeFile);
                                if (linetypeTable.Has(linetypeName))
                                {
                                    return linetypeTable[linetypeName];
                                }
                            }
                            catch (Autodesk.AutoCAD.Runtime.Exception ex)
                            {
                                // 记录异常
                                Console.WriteLine($"Error loading linetype {linetypeName} from file {linetypeFile}: {ex.Message}");
                            }
                        }
                    }
                }
                // 如果线型文件中没有找到目标线型，设置默认线型
                return linetypeTable["Continuous"];
            }
        }
        private static bool FileContainsLinetype(string filePath, string linetypeName)
        {
            // 读取文件内容，检查是否包含目标线型定义
            var lines = System.IO.File.ReadAllLines(filePath);
            foreach (var line in lines)
            {
                if (line.Trim().StartsWith("*" + linetypeName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
        // 从Transparency对象获取0-90范围的透明度值
        #endregion
        #region GET SET Transparency
        public static string GetTransparencyString(this Transparency transparency)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            switch (transparency)
            {
                case var _ when transparency.IsByAlpha:
                    int alphaValue = transparency.Alpha; // 获取 Alpha 值
                    var transparencyValue = (255 - alphaValue) / 2.55;
                    return transparencyValue.ToString();
                case var _ when transparency.IsByBlock:
                    ed.WriteMessage($"\n透明度由块定义: ByBlock");
                    return "IsByBlock";
                case var _ when transparency.IsByLayer:
                    ed.WriteMessage($"\n透明度由图层定义: ByLayer");
                    return "IsByLayer";
                default:
                    ed.WriteMessage($"\n未知透明度类型。");
                    return "0";
            }
        }
        public static Transparency SetTransparency(this string transparencyValue)
        {
            try
            {
                switch (transparencyValue)
                {
                    case "IsByLayer":
                        return new Transparency(TransparencyMethod.ByLayer);
                    case "IsByBlock":
                        return new Transparency(TransparencyMethod.ByBlock);
                    default:
                        if (double.TryParse(transparencyValue, out double parsedValue))
                        {
                            byte alpha = (byte)(255 - 2.55 * parsedValue);
                            return new Transparency(alpha);
                        }
                        else
                        {
                            return new Transparency(TransparencyMethod.ByLayer); // 返回一个默认值
                        }
                }
            }
            catch
            {
                return new Transparency(TransparencyMethod.ByLayer); // 返回一个默认值
            }
        }
        #endregion
        public static Color SetColorIndex(string colorIndexString)
        {
            if (short.TryParse(colorIndexString, out short colorIndex))
            {
                var color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex);
                return color;
            }
            else
            {
                throw new ArgumentException("Invalid color index string", nameof(colorIndexString));
            }
        }
    }
}
