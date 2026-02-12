using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Domain.Models.Text;
using HyCADTool.Refactored.Presentation.ViewModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services
{
    /// <summary>
    /// 设计说明排版服务 v4：每栏独立 MText，各自独立宽度
    /// </summary>
    public class DesignSpecService
    {
        private const string LAYER_TEXT = "00_hy_1公共_文字";
        private const string XREC_KEY_MD = "HyDesignSpec_MD";
        private const string XREC_KEY_CFG = "HyDesignSpec_CFG";
        private const string XREC_KEY_GROUP = "HyDesignSpec_Group";

        /// <summary>
        /// 插入多个独立 MText（每栏一个），横向排列
        /// </summary>
        /// <param name="columnContents">每栏的 MText 内容数组</param>
        /// <param name="markdownSource">完整 Markdown 源码</param>
        /// <param name="config">配置</param>
        /// <param name="insertionPoint">左上角插入点</param>
        public void Insert(string[] columnContents, string markdownSource, DesignSpecConfig config, Point3d insertionPoint)
        {
            if (columnContents == null || columnContents.Length == 0)
                throw new ArgumentException("MText 内容不能为空");

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) throw new InvalidOperationException("无活动文档");

            var db = doc.Database;
            var area = TextAreaCalculator.Calculate(config);
            var ed = doc.Editor;

            // 诊断
            string widthsStr = string.Join(", ", Array.ConvertAll(area.ColumnWidths, w => w.ToString("F1")));
            ed.WriteMessage($"\n[计算] {area.ColumnCount}栏 | 栏宽=[{widthsStr}] | 总宽={area.TotalWidth:F1} | 总高={area.TotalHeight:F1}");

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    var textStyleId = EnsureTextStyle(db, tr, config);

                    // 生成唯一组ID，标识这组 MText 属于同一个设计说明
                    string groupId = Guid.NewGuid().ToString("N");

                    double xOffset = 0;
                    var mtextIds = new List<ObjectId>();

                    for (int i = 0; i < area.ColumnCount; i++)
                    {
                        string content = (i < columnContents.Length && !string.IsNullOrWhiteSpace(columnContents[i]))
                            ? columnContents[i]
                            : "";

                        if (string.IsNullOrWhiteSpace(content))
                        {
                            xOffset += area.ColumnWidths[i] + area.ColumnGutter;
                            continue;
                        }

                        double colWidth = area.ColumnWidths[i];

                        var mtext = new MText();
                        mtext.SetDatabaseDefaults();
                        mtext.Location = new Point3d(
                            insertionPoint.X + xOffset,
                            insertionPoint.Y,
                            insertionPoint.Z);
                        mtext.Attachment = AttachmentPoint.TopLeft;
                        mtext.TextStyleId = textStyleId;
                        mtext.TextHeight = config.ActualTextHeight;
                        mtext.LineSpacingStyle = LineSpacingStyle.Exactly;
                        mtext.LineSpacingFactor = config.LineSpacingFactor;
                        mtext.Width = colWidth;
                        mtext.Contents = content;

                        SetLayer(db, tr, mtext, LAYER_TEXT);
                        btr.AppendEntity(mtext);
                        tr.AddNewlyCreatedDBObject(mtext, true);

                        // 第一个 MText 存完整的 Markdown 和 Config
                        if (i == 0)
                        {
                            ExtensionDictionaryService.WriteLongString(tr, mtext, markdownSource, XREC_KEY_MD);
                            string cfgJson = JsonConvert.SerializeObject(config);
                            ExtensionDictionaryService.WriteLongString(tr, mtext, cfgJson, XREC_KEY_CFG);
                        }

                        // 所有 MText 存组ID，便于后续更新时找到同组
                        ExtensionDictionaryService.WriteLongString(tr, mtext, groupId, XREC_KEY_GROUP);
                        mtextIds.Add(mtext.ObjectId);

                        ed.WriteMessage($"\n[栏{i + 1}] 宽={colWidth:F1}, 位置X={insertionPoint.X + xOffset:F1}, 内容高={mtext.ActualHeight:F1}");

                        xOffset += colWidth + area.ColumnGutter;
                    }

                    tr.Commit();
                    ed.WriteMessage($"\n已插入 {mtextIds.Count} 个 MText（{area.ColumnCount}栏 | 字高={config.ActualTextHeight:F1}）");
                }
                catch (System.Exception ex)
                {
                    tr.Abort();
                    ed.WriteMessage($"\n插入失败: {ex.Message}\n{ex.StackTrace}");
                    throw;
                }
            }
        }

        /// <summary>
        /// 更新已有 MText 组
        /// </summary>
        public void Update(ObjectId mtextId, string[] columnContents, string markdownSource, DesignSpecConfig config)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) throw new InvalidOperationException("无活动文档");

            var db = doc.Database;
            var area = TextAreaCalculator.Calculate(config);
            var ed = doc.Editor;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    // 读取组ID，找到同组所有 MText
                    var mtext0 = tr.GetObject(mtextId, OpenMode.ForRead) as MText;
                    if (mtext0 == null) throw new InvalidOperationException("选中实体不是 MText");

                    string groupId = ExtensionDictionaryService.ReadLongString(tr, mtext0, XREC_KEY_GROUP);

                    // 删除旧的同组 MText
                    if (!string.IsNullOrEmpty(groupId))
                    {
                        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                        var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                        var toDelete = new List<ObjectId>();

                        foreach (ObjectId id in btr)
                        {
                            var ent = tr.GetObject(id, OpenMode.ForRead) as MText;
                            if (ent == null) continue;
                            string gid = ExtensionDictionaryService.ReadLongString(tr, ent, XREC_KEY_GROUP);
                            if (gid == groupId)
                                toDelete.Add(id);
                        }

                        foreach (var id in toDelete)
                        {
                            var ent = tr.GetObject(id, OpenMode.ForWrite);
                            ent.Erase();
                        }
                    }

                    // 重新插入（复用 Insert 逻辑位置取原始位置）
                    var insertPt = mtext0.Location;

                    var bt2 = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr2 = (BlockTableRecord)tr.GetObject(bt2[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    var textStyleId = EnsureTextStyle(db, tr, config);
                    string newGroupId = Guid.NewGuid().ToString("N");

                    double xOffset = 0;
                    for (int i = 0; i < area.ColumnCount; i++)
                    {
                        string content = (i < columnContents.Length && !string.IsNullOrWhiteSpace(columnContents[i]))
                            ? columnContents[i] : "";

                        if (string.IsNullOrWhiteSpace(content))
                        {
                            xOffset += area.ColumnWidths[i] + area.ColumnGutter;
                            continue;
                        }

                        double colWidth = area.ColumnWidths[i];
                        var newMtext = new MText();
                        newMtext.SetDatabaseDefaults();
                        newMtext.Location = new Point3d(insertPt.X + xOffset, insertPt.Y, insertPt.Z);
                        newMtext.Attachment = AttachmentPoint.TopLeft;
                        newMtext.TextStyleId = textStyleId;
                        newMtext.TextHeight = config.ActualTextHeight;
                        newMtext.LineSpacingStyle = LineSpacingStyle.Exactly;
                        newMtext.LineSpacingFactor = config.LineSpacingFactor;
                        newMtext.Width = colWidth;
                        newMtext.Contents = content;

                        SetLayer(db, tr, newMtext, LAYER_TEXT);
                        btr2.AppendEntity(newMtext);
                        tr.AddNewlyCreatedDBObject(newMtext, true);

                        if (i == 0)
                        {
                            ExtensionDictionaryService.WriteLongString(tr, newMtext, markdownSource, XREC_KEY_MD);
                            string cfgJson = JsonConvert.SerializeObject(config);
                            ExtensionDictionaryService.WriteLongString(tr, newMtext, cfgJson, XREC_KEY_CFG);
                        }
                        ExtensionDictionaryService.WriteLongString(tr, newMtext, newGroupId, XREC_KEY_GROUP);

                        xOffset += colWidth + area.ColumnGutter;
                    }

                    tr.Commit();
                    ed.WriteMessage($"\n已更新设计说明（{area.ColumnCount}栏）");
                }
                catch (System.Exception ex)
                {
                    tr.Abort();
                    ed.WriteMessage($"\n更新失败: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// 从 MText 的 XRecord 读取 Markdown 源码
        /// </summary>
        public static string ReadMarkdownSource(Transaction tr, Entity entity)
        {
            return ExtensionDictionaryService.ReadLongString(tr, entity, XREC_KEY_MD);
        }

        /// <summary>
        /// 从 MText 的 XRecord 读取配置
        /// </summary>
        public static DesignSpecConfig ReadConfig(Transaction tr, Entity entity)
        {
            string json = ExtensionDictionaryService.ReadLongString(tr, entity, XREC_KEY_CFG);
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                return JsonConvert.DeserializeObject<DesignSpecConfig>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 判断 MText 是否含 Markdown 源码
        /// </summary>
        public static bool HasMarkdownSource(Transaction tr, Entity entity)
        {
            return ExtensionDictionaryService.HasKey(tr, entity, XREC_KEY_MD);
        }

        private ObjectId EnsureTextStyle(Database db, Transaction tr, DesignSpecConfig config)
        {
            string styleName = $"0_Hy_{config.Scale}";

            var vm = SettingsPanelViewModel.Current;
            if (vm != null) styleName = vm.TextStyleName;

            var tst = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            if (tst.Has(styleName))
            {
                var rec = (TextStyleTableRecord)tr.GetObject(tst[styleName], OpenMode.ForWrite);
                rec.TextSize = 0;
                rec.XScale = config.TextXScale;
                return tst[styleName];
            }

            tst.UpgradeOpen();
            var newRec = new TextStyleTableRecord
            {
                Name = styleName,
                FileName = config.FontFileName,
                BigFontFileName = config.BigFontFileName,
                TextSize = 0,
                XScale = config.TextXScale
            };
            tst.Add(newRec);
            tr.AddNewlyCreatedDBObject(newRec, true);
            return newRec.ObjectId;
        }

        private void SetLayer(Database db, Transaction tr, Entity entity, string layerName)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(layerName))
                entity.LayerId = lt[layerName];
        }
    }
}
