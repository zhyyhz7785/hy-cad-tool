using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Config;
namespace HyCADTool.Tools
{
    public static partial class EtGpt
    {
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
                style.HorizontalCellMargin = BaseConfig.TableStyleConfig.CellHorizontalMargin * BaseConfig.Scale;
                style.VerticalCellMargin = BaseConfig.TableStyleConfig.CellVerticalMargin * BaseConfig.Scale;
                // 设置文本高度
                style.SetTextHeight(1 * BaseConfig.Scale, (int)RowType.TitleRow);    // 例如 250
                style.SetTextHeight(1 * BaseConfig.Scale, (int)RowType.HeaderRow);   // 例如 175
                style.SetTextHeight(1 * BaseConfig.Scale, (int)RowType.DataRow);     // 例如 125
            }
        }
    }
}
