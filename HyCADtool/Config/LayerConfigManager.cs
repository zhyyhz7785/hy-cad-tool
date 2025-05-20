using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
namespace HyCADTool.Config
{
    public static class LayerConfigManager
    {
        // 指定默认目录路径
        // 图层枚举，用于在编码中选择 Markdown 中的图层
        public enum LayerType
        {
            Public_Text,                 // 00_hy_1公共_文字
            Public_Text_LargeFont,       // 00_hy_1公共_文字_大字体
            Public_Frame,                // 00_hy_2公共_图框
            Public_Frame_Text,           // 00_hy_2公共_图框_文字
            Public_Viewport,             // 00_hy_2公共_视口
            Public_Annotation1_Outer,    // 00_hy_3公共_标注1_外
            Public_Annotation2_Inner,    // 00_hy_3公共_标注2_内
            Public_Annotation3_Leader,   // 00_hy_3公共_标注3_引线
            Public_Annotation4_Elevation,// 00_hy_3公共_标注4_标高
            Public_Axis_Total,           // 00_hy_3公共_轴线_总
            Public_Axis_Internal,        // 00_hy_3公共_轴线_内部
            Public_Modification,         // 00_hy_4公共_修改
            Public_Table,                // 00_hy_4公共_表格
            Node_OuterContour,           // 01_hy_1节点_外轮廓
            Node_InnerContour,           // 01_hy_1节点_内轮廓
            Node_Opening,                // 01_hy_1节点_洞口
            Reinforcement_Line,          // 01_hy_1钢筋_线钢筋
            Reinforcement_Point,         // 01_hy_1钢筋_点钢筋
            Pile_Main,                   // 02_hy_1桩_主
            Pile_FoundationInnerContour, // 02_hy_3桩_地基内轮廓
            Pile_FoundationOuterContour, // 02_hy_2桩_地基外轮廓
            Pile_ServiceArea_Corner,     // 02_hy_11桩_服务区域_角部
            Pile_ServiceArea_Edge,       // 02_hy_12桩_服务区域_边部
            Pile_ServiceArea_Center      // 02_hy_13桩_服务区域_中部
        }
        public static string GetLayerName(this LayerType layerType)
        {
            switch (layerType)
            {
                case LayerType.Public_Text:
                    return "00_hy_1公共_文字";
                case LayerType.Public_Text_LargeFont:
                    return "00_hy_1公共_文字_大字体";
                case LayerType.Public_Frame:
                    return "00_hy_2公共_图框";
                case LayerType.Public_Frame_Text:
                    return "00_hy_2公共_图框_文字";
                case LayerType.Public_Viewport:
                    return "00_hy_2公共_视口";
                case LayerType.Public_Annotation1_Outer:
                    return "00_hy_3公共_标注1_外";
                case LayerType.Public_Annotation2_Inner:
                    return "00_hy_3公共_标注2_内";
                case LayerType.Public_Annotation3_Leader:
                    return "00_hy_3公共_标注3_引线";
                case LayerType.Public_Annotation4_Elevation:
                    return "00_hy_3公共_标注4_标高";
                case LayerType.Public_Axis_Total:
                    return "00_hy_3公共_轴线_总";
                case LayerType.Public_Axis_Internal:
                    return "00_hy_3公共_轴线_内部";
                case LayerType.Public_Modification:
                    return "00_hy_4公共_修改";
                case LayerType.Public_Table:
                    return "00_hy_4公共_表格";
                case LayerType.Node_OuterContour:
                    return "01_hy_1节点_外轮廓";
                case LayerType.Node_InnerContour:
                    return "01_hy_1节点_内轮廓";
                case LayerType.Node_Opening:
                    return "01_hy_1节点_洞口";
                case LayerType.Reinforcement_Line:
                    return "01_hy_1钢筋_线钢筋";
                case LayerType.Reinforcement_Point:
                    return "01_hy_1钢筋_点钢筋";
                case LayerType.Pile_Main:
                    return "02_hy_1桩_主";
                case LayerType.Pile_FoundationInnerContour:
                    return "02_hy_3桩_地基内轮廓";
                case LayerType.Pile_FoundationOuterContour:
                    return "02_hy_2桩_地基外轮廓";
                case LayerType.Pile_ServiceArea_Corner:
                    return "02_hy_11桩_服务区域_角部";
                case LayerType.Pile_ServiceArea_Edge:
                    return "02_hy_12桩_服务区域_边部";
                case LayerType.Pile_ServiceArea_Center:
                    return "02_hy_13桩_服务区域_中部";
                default:
                    throw new ArgumentException($"未知的图层类型: {layerType}");
            }
        }
        private static readonly string filePath = @"E:\BaiduSyncdisk\Code\testResult";
        private static readonly string configFile = Path.Combine(filePath, "0LayerConfig.md");
        #region 导出方法
        // 将当前图层导出到 Markdown 文件
        public static void ExportLayersToMarkdown()
        {
            Directory.CreateDirectory(filePath); // 确保目录存在
            var layers = GetCurrentLayers();
            WriteLayersToMarkdown(layers, configFile);
        }
        // 获取当前 AutoCAD 图层
        private static List<LayerTableRecord> GetCurrentLayers()
        {
            var layers = new List<LayerTableRecord>();
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                LayerTable layerTable = (LayerTable)trans.GetObject(db.LayerTableId, OpenMode.ForRead);
                foreach (ObjectId layerId in layerTable)
                {
                    var layer = (LayerTableRecord)trans.GetObject(layerId, OpenMode.ForRead);
                    layers.Add(layer);
                }
                trans.Commit();
            }
            return layers;
        }
        // 写入 Markdown 文件（只保留图层名称、颜色、线型、线宽）
        private static void WriteLayersToMarkdown(List<LayerTableRecord> layers, string filePath)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Layer Configuration");
            sb.AppendLine("| 图层名称       | 颜色 | 线型       | 线宽         |");
            sb.AppendLine("|----------------|------|------------|--------------|");
            foreach (var layer in layers)
            {
                string lineTypeName = GetLinetypeName(layer);
                sb.AppendLine($"| {layer.Name} | {layer.Color.ColorIndex} | {lineTypeName} | {layer.LineWeight} |");
            }
            File.WriteAllText(filePath, sb.ToString());
        }
        #endregion
        #region 读取和创建方法
        // 从 Markdown 读取图层数据（跳过标题行和分隔行，验证数据格式）
        private static Dictionary<string, LayerDefinition> ReadLayersFromMarkdown()
        {
            if (!File.Exists(configFile))
            {
                CreateDefaultMarkdown(); // 如果文件不存在，创建默认文件
            }
            var lines = File.ReadAllLines(configFile).Skip(2).Where(line => !string.IsNullOrWhiteSpace(line)); // 跳过标题和分隔行
            var layers = new Dictionary<string, LayerDefinition>();
            foreach (var line in lines)
            {
                var cells = line.Split('|').Select(cell => cell.Trim()).Where(cell => !string.IsNullOrEmpty(cell)).ToArray();
                if (cells.Length >= 4 && IsValidDataRow(cells)) // 检查是否为有效数据行
                {
                    var layerDef = new LayerDefinition
                    {
                        Name = cells[0],
                        ColorIndex = short.Parse(cells[1]), // 假设颜色是有效的 short 值
                        LineType = cells[2],
                        LineWeight = (LineWeight)Enum.Parse(typeof(LineWeight), cells[3])
                    };
                    layers[layerDef.Name] = layerDef; // 使用图层名称作为键
                }
            }
            return layers;
        }
        // 使用图层名称创建指定图层
        public static void CreateLayer(string layerName)
        {
            HyTool.SetCurrentLayer(layerName);
            layerName.GetLayerId();
            var layers = ReadLayersFromMarkdown();
            if (layers.TryGetValue(layerName, out var layerDef))
            {
                CreateLayerInAutoCAD(layerDef);
            }
            else
            {
                throw new ArgumentException($"Markdown 文件中未找到图层: {layerName}");
            }
        }
        // 从 Markdown 导入所有图层（原名 CreateAllLayers）
        public static void ImportLayersFromMarkdown()
        {
            var layers = ReadLayersFromMarkdown();
            foreach (var layer in layers.Values)
            {
                CreateLayerInAutoCAD(layer);
            }
        }
        // 在 AutoCAD 中创建图层
        private static void CreateLayerInAutoCAD(LayerDefinition layerDef)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            using (doc.LockDocument())
            {
                using (Transaction trans = db.TransactionManager.StartTransaction())
                {
                    LayerTable layerTable = (LayerTable)trans.GetObject(db.LayerTableId, OpenMode.ForRead);
                    if (!layerTable.Has(layerDef.Name))
                    {
                        layerTable.UpgradeOpen();
                        LayerTableRecord layer = new LayerTableRecord
                        {
                            Name = layerDef.Name,
                            Color = Color.FromColorIndex(ColorMethod.ByAci, layerDef.ColorIndex),
                            LinetypeObjectId = GetOrCreateLinetypeId(db, layerDef.LineType, trans),
                            LineWeight = layerDef.LineWeight
                        };
                        layerTable.Add(layer);
                        trans.AddNewlyCreatedDBObject(layer, true);
                    }
                    trans.Commit();
                }
            }
        }
        #endregion
        #region 辅助方法
        // 获取线型名称
        private static string GetLinetypeName(LayerTableRecord layer)
        {
            if (!layer.LinetypeObjectId.IsValid) return "Continuous";
            using (var trans = layer.Database.TransactionManager.StartTransaction())
            {
                var lt = (LinetypeTableRecord)trans.GetObject(layer.LinetypeObjectId, OpenMode.ForRead);
                trans.Commit();
                return lt.Name;
            }
        }
        // 获取或创建线型
        private static ObjectId GetOrCreateLinetypeId(Database db, string lineTypeName, Transaction trans)
        {
            LinetypeTable lt = (LinetypeTable)trans.GetObject(db.LinetypeTableId, OpenMode.ForRead);
            if (lt.Has(lineTypeName))
            {
                return lt[lineTypeName];
            }
            else
            {
                lt.UpgradeOpen();
                LinetypeTableRecord ltr = new LinetypeTableRecord { Name = lineTypeName };
                lt.Add(ltr);
                trans.AddNewlyCreatedDBObject(ltr, true);
                return ltr.ObjectId;
            }
        }
        // 创建默认 Markdown 文件（只保留图层名称、颜色、线型、线宽）
        private static void CreateDefaultMarkdown()
        {
            var defaultLayers = new List<LayerDefinition>
            {
                new LayerDefinition { Name = "DefaultLayer", ColorIndex = 0, LineType = "Continuous", LineWeight = LineWeight.LineWeight025 },
                new LayerDefinition { Name = "StructuralLayer", ColorIndex = 1, LineType = "Dashed", LineWeight = LineWeight.LineWeight050 },
                new LayerDefinition { Name = "AnnotationLayer", ColorIndex = 3, LineType = "Dotted", LineWeight = LineWeight.LineWeight030 }
            };
            WriteLayersToMarkdown(defaultLayers.Cast<LayerTableRecord>().ToList(), configFile); // 简单转换以复用方法
        }
        // 验证是否为有效数据行
        private static bool IsValidDataRow(string[] cells)
        {
            // 检查是否包含分隔符（如 "------"）或空值
            if (cells.Any(cell => cell.Contains("-") || string.IsNullOrWhiteSpace(cell)))
            {
                return false;
            }
            // 验证颜色是否为有效的 short 值
            if (!short.TryParse(cells[1], out _))
            {
                return false;
            }
            // 验证线宽是否为有效的 LineWeight 枚举值（显式指定类型参数）
            if (!Enum.TryParse<LineWeight>(cells[3], out _))
            {
                return false;
            }
            return true;
        }
        #endregion
        #region 图层定义类
        private class LayerDefinition
        {
            public string Name { get; set; }
            public short ColorIndex { get; set; }
            public string LineType { get; set; }
            public LineWeight LineWeight { get; set; }
        }
        #endregion
    }
}