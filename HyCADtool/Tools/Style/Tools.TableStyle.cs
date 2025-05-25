using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Config;
namespace HyCADTool.Tools
{
    public static partial class Tools
    {
        public static class TableStyleConfig
        {
            public static string Name => $"0_Hy_{BaseConfig.Scale}_Table";
            public static string TextStyleName => Tools.TextStyleConfig.Name;
            // 表格行设置
            public static double TitleRowHeight { get; } = 8;   // 标题行高度
            public static double DataRowHeight { get; } = 6;     // 数据行高度
            // 颜色索引 (ACI颜色)
            public static int TitleRowColorIndex { get; } = 1;   // 红色标题行
            public static int DataRowColorIndex { get; } = 7;   // 白色数据行
            // 对齐方式
            public static CellAlignment TitleHorizontalAlignment { get; } = CellAlignment.MiddleCenter;
            public static CellAlignment DataHorizontalAlignment { get; } = CellAlignment.MiddleLeft;
            // 边距设置
            public static double CellHorizontalMargin { get; } = 0.5;
            public static double CellVerticalMargin { get; } = 0.3;
            // 网格线设置
            public static double GridLineWeight { get; } = 0.15; // 线宽(mm)
            public static int GridColorIndex { get; } = 7;       // 白色网格线
        }
        public static ObjectId CreateTableStyle(string styleName)
        {
            ObjectId styleId = ObjectId.Null;
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            using (DocumentLock docLock = doc.LockDocument())
            {
                using (Transaction trans = db.TransactionManager.StartTransaction())
                {
                    // 获取表格样式字典
                    DBDictionary tableStyleDict = trans.GetObject(db.TableStyleDictionaryId, OpenMode.ForWrite) as DBDictionary;
                    // 如果样式已存在，直接返回其 ObjectId
                    if (tableStyleDict.Contains(styleName))
                    {
                        styleId = tableStyleDict.GetAt(styleName);
                    }
                    else
                    {
                        // 创建样式对象（注意：此时不能设置 Name）
                        TableStyle newStyle = new TableStyle();
                        // 添加到字典后，才能设置 Name
                        styleId = tableStyleDict.SetAt(styleName, newStyle);
                        trans.AddNewlyCreatedDBObject(newStyle, true);
                        // 设置样式属性
                        UpdateTableStyle(newStyle);
                        // 设置名称（必须在添加到数据库之后执行）
                        newStyle.Name = styleName;
                    }
                    trans.Commit();
                }
            }
            return styleId;
            // 内部方法：设置表格样式参数
            void UpdateTableStyle(TableStyle style)
            {
                // 设置文字样式（使用配置值或默认）
                ObjectId textStyleId = BaseConfig.TextStyleId.IsValid ? BaseConfig.TextStyleId : db.Textstyle;
                style.SetTextStyle(textStyleId, (int)(RowType.TitleRow | RowType.HeaderRow | RowType.DataRow));
                // 设置边距（乘以缩放系数）
                style.HorizontalCellMargin = Tools.TableStyleConfig.CellHorizontalMargin * BaseConfig.Scale;
                style.VerticalCellMargin = Tools.TableStyleConfig.CellVerticalMargin * BaseConfig.Scale;
                // 设置文本高度
                style.SetTextHeight(1 * BaseConfig.Scale, (int)RowType.TitleRow);    // 例如 250
                style.SetTextHeight(1 * BaseConfig.Scale, (int)RowType.HeaderRow);   // 例如 175
                style.SetTextHeight(1 * BaseConfig.Scale, (int)RowType.DataRow);     // 例如 125
            }
        }
    }
}
