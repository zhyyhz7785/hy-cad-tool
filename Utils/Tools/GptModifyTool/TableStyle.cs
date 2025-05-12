using Autodesk.AutoCAD.DatabaseServices;
namespace CadUtils
{
    public static partial class EtGpt
    {
        public static ObjectId CreateTableStyle(string styleName)
        {
            ObjectId styleId = ObjectId.Null;
            using (var tr = HostApplicationServices.WorkingDatabase.TransactionManager.StartTransaction())
            {
                var db = HostApplicationServices.WorkingDatabase;
                var tableStyleDict = (DBDictionary)tr.GetObject(db.TableStyleDictionaryId, OpenMode.ForRead);
                if (tableStyleDict.Contains(styleName))
                {
                    styleId = tableStyleDict.GetAt(styleName);
                    var existingStyle = (TableStyle)tr.GetObject(styleId, OpenMode.ForWrite);
                    UpdateTableStyle(existingStyle);
                }
                else
                {
                    var newStyle = new TableStyle();
                    UpdateTableStyle(newStyle);
                    tableStyleDict.UpgradeOpen();
                    styleId = tableStyleDict.SetAt(styleName, newStyle);
                    tr.AddNewlyCreatedDBObject(newStyle, true);
                }
                tr.Commit();
            }
            return styleId;
            void UpdateTableStyle(TableStyle style)
            {
                var db = HostApplicationServices.WorkingDatabase;
                // 设置文字样式（避免重复调用）
                ObjectId textStyleId = BaseConfig.TextStyleId.IsValid ? BaseConfig.TextStyleId : db.Textstyle; // 使用默认样式作为回退
                style.SetTextStyle(textStyleId, (int)(RowType.TitleRow | RowType.HeaderRow | RowType.DataRow));
                // 设置合理的边距
                style.HorizontalCellMargin = BaseConfig.TableStyleConfig.CellHorizontalMargin * BaseConfig.Scale; // 例如 0.1 * 50 = 5
                style.VerticalCellMargin = BaseConfig.TableStyleConfig.CellVerticalMargin * BaseConfig.Scale;   // 例如 0.1 * 50 = 5
                // 设置文本高度
                style.SetTextHeight(1 * BaseConfig.Scale, (int)RowType.TitleRow);    // 250
                style.SetTextHeight(1 * BaseConfig.Scale, (int)RowType.HeaderRow); // 175
                style.SetTextHeight(1 * BaseConfig.Scale, (int)RowType.DataRow);   // 125
            }
        }
    }
}