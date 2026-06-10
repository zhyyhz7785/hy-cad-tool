using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Shared.AutoCAD.Interactive;
using HyCADTool.Shared.AutoCAD.Services;
using System;
using System.Collections.Generic;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Elevation.Services
{
    /// <summary>
    /// 标高符号服务
    /// 封装图层/文字样式初始化、符号字典构建、批量更新/旋转
    /// 迁移自旧代码 ElevationSymbol 的静态方法
    /// </summary>
    public class ElevationService
    {
        private static readonly Dictionary<IntPtr, (ObjectId textStyleId, ObjectId layerId)> _styleCacheByDatabase
            = new Dictionary<IntPtr, (ObjectId, ObjectId)>();

        /// <summary>
        /// 确保标高图层和文字样式已创建，返回 (textStyleId, layerId)
        /// </summary>
        public static (ObjectId textStyleId, ObjectId layerId) EnsureStylesCreated()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var dbKey = db.UnmanagedObject;

            if (!_styleCacheByDatabase.TryGetValue(dbKey, out var cached)
                || cached.textStyleId.IsNull || cached.textStyleId.IsErased
                || cached.layerId.IsNull || cached.layerId.IsErased)
            {
                var textStyleId = CreateTextStyleForElevation(db, doc);
                var layerId = CreateLayerForElevation(db, doc);
                cached = (textStyleId, layerId);
                _styleCacheByDatabase[dbKey] = cached;
            }

            return cached;
        }

        /// <summary>
        /// 重置全部缓存（测试或全局清理）。
        /// </summary>
        public static void ResetCache()
        {
            _styleCacheByDatabase.Clear();
        }

        /// <summary>
        /// 文档关闭时移除对应 Database 的样式缓存。
        /// </summary>
        public static void RemoveDocumentCache(Database db)
        {
            if (db == null) return;
            _styleCacheByDatabase.Remove(db.UnmanagedObject);
        }

        /// <summary>
        /// 获取当前单位因子（paper-mm → model-unit）。
        /// 优先取活动文档的设置；拿不到时回退到全局活动比例上下文。
        /// </summary>
        public static double GetCurrentUnitFactor()
        {
            try
            {
                var vm = Presentation.ViewModels.SettingsPanelViewModel.Current;
                if (vm != null)
                {
                    double unitFactor = vm.BuildScaleContext().UnitFactor;
                    if (unitFactor > 0)
                        return unitFactor;
                }
            }
            catch
            {
            }

            var fallback = ActiveScaleContextProvider.Current;
            return fallback != null && fallback.UnitFactor > 0 ? fallback.UnitFactor : 1.0;
        }

        /// <summary>
        /// 把纸面 mm 基值换算为当前图纸模型空间长度。
        /// 统一规则：model = paper_mm × unitFactor × scale。
        /// </summary>
        public static double ToModelLength(double paperMillimeters, double scale)
        {
            return paperMillimeters * scale * GetCurrentUnitFactor();
        }

        /// <summary>
        /// 把当前图纸模型空间高差换算为标高文字值（单位：m）。
        /// </summary>
        public static double ToElevationMeters(double modelDelta)
        {
            return modelDelta / GetCurrentUnitFactor() / 1000.0;
        }

        /// <summary>
        /// 从用户选择构建符号字典
        /// 键: Tuple(Shape ObjectId, Shape1 ObjectId)  值: Text ObjectId
        /// </summary>
        public static Dictionary<Tuple<ObjectId, ObjectId>, ObjectId> BuildSymbolDictionaryFromSelection()
        {
            var dict = new Dictionary<Tuple<ObjectId, ObjectId>, ObjectId>();
            var ed = AcApp.DocumentManager.MdiActiveDocument.Editor;
            var db = AcApp.DocumentManager.MdiActiveDocument.Database;

            // 过滤标高图层
            var filter = new TypedValue[]
            {
                new TypedValue((int)DxfCode.LayerName, ElevationSymbolJig.ElevationLayerName)
            };
            var selFilter = new SelectionFilter(filter);
            PromptSelectionResult psr = ed.GetSelection(selFilter);

            if (psr.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n未选择任何对象。");
                return dict;
            }

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var ss = psr.Value;
                var shapes = new List<ObjectId>();    // 4 顶点 Polyline
                var shape1s = new List<ObjectId>();   // 2 顶点 Polyline
                var texts = new List<ObjectId>();     // DBText

                foreach (ObjectId id in ss.GetObjectIds())
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent is Polyline polyline)
                    {
                        if (polyline.NumberOfVertices == 4) shapes.Add(id);
                        else if (polyline.NumberOfVertices == 2) shape1s.Add(id);
                    }
                    else if (ent is DBText)
                    {
                        texts.Add(id);
                    }
                }

                // 匹配 Shape + Shape1 → Text
                foreach (ObjectId shapeId in shapes)
                {
                    var shape = tr.GetObject(shapeId, OpenMode.ForRead) as Polyline;
                    if (shape == null || shape.NumberOfVertices != 4) continue;

                    Point3d shapeThirdPoint = shape.GetPoint3dAt(2);

                    foreach (ObjectId shape1Id in shape1s)
                    {
                        var shape1 = tr.GetObject(shape1Id, OpenMode.ForRead) as Polyline;
                        if (shape1 == null || shape1.NumberOfVertices != 2) continue;

                        Point2d sp = shape1.GetPoint2dAt(0);
                        Point2d ep = shape1.GetPoint2dAt(1);
                        var midPoint = new Point3d((sp.X + ep.X) / 2, (sp.Y + ep.Y) / 2, shapeThirdPoint.Z);

                        if (midPoint.DistanceTo(shapeThirdPoint) < 0.001)
                        {
                            // 找到匹配，寻找最近的 DBText
                            DBText nearestText = null;
                            double minDist = double.MaxValue;
                            foreach (ObjectId textId in texts)
                            {
                                var text = tr.GetObject(textId, OpenMode.ForRead) as DBText;
                                if (text != null)
                                {
                                    double dist = shapeThirdPoint.DistanceTo(text.Position);
                                    if (dist < minDist)
                                    {
                                        minDist = dist;
                                        nearestText = text;
                                    }
                                }
                            }

                            if (nearestText != null)
                            {
                                dict[new Tuple<ObjectId, ObjectId>(shapeId, shape1Id)] = nearestText.ObjectId;
                                texts.Remove(nearestText.ObjectId);
                            }
                            break;
                        }
                    }
                }

                tr.Commit();
            }

            return dict;
        }

        /// <summary>
        /// 更新所有标高文字（基于新基准点）
        /// </summary>
        public static void UpdateElevationsByPoint(
            Point3d newBasePoint,
            Dictionary<Tuple<ObjectId, ObjectId>, ObjectId> dictionary)
        {
            var db = AcApp.DocumentManager.MdiActiveDocument.Database;
            var ed = AcApp.DocumentManager.MdiActiveDocument.Editor;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (var entry in dictionary)
                {
                    ObjectId shapeId = entry.Key.Item1;
                    ObjectId textId = entry.Value;

                    var shape = tr.GetObject(shapeId, OpenMode.ForRead) as Polyline;
                    var text = tr.GetObject(textId, OpenMode.ForWrite) as DBText;

                    if (shape != null && text != null && shape.NumberOfVertices >= 3)
                    {
                        Point3d refPoint = shape.GetPoint3dAt(2);
                        double deltaY = refPoint.Y - newBasePoint.Y;
                        double elevation = ToElevationMeters(deltaY);
                        text.TextString = Math.Abs(elevation) <= 0.001
                            ? $"±{elevation:F3}"
                            : $"{elevation:F3}";
                    }
                }
                tr.Commit();
            }
        }

        /// <summary>
        /// 旋转所有标高符号
        /// </summary>
        public static void RotateAllByAngle(
            double angleDegrees,
            Dictionary<Tuple<ObjectId, ObjectId>, ObjectId> dictionary)
        {
            var db = AcApp.DocumentManager.MdiActiveDocument.Database;
            double angleRadians = angleDegrees * Math.PI / 180.0;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (var entry in dictionary)
                {
                    var shape = tr.GetObject(entry.Key.Item1, OpenMode.ForWrite) as Polyline;
                    var shape1 = tr.GetObject(entry.Key.Item2, OpenMode.ForWrite) as Polyline;
                    var text = tr.GetObject(entry.Value, OpenMode.ForWrite) as DBText;

                    if (shape != null && shape.NumberOfVertices >= 3)
                    {
                        Point3d center = shape.GetPoint3dAt(2);

                        // 旋转 Shape
                        shape.TransformBy(Matrix3d.Rotation(angleRadians, Vector3d.ZAxis, center));

                        // 旋转 Shape1
                        if (shape1 != null)
                            shape1.TransformBy(Matrix3d.Rotation(angleRadians, Vector3d.ZAxis, center));

                        // 旋转 Text
                        if (text != null)
                            text.TransformBy(Matrix3d.Rotation(angleRadians, Vector3d.ZAxis, center));
                    }
                }
                tr.Commit();
            }
        }

        /// <summary>
        /// 计算 _currentPoint 使得生成的 Label.Position 与指定 textPosition 重合
        /// 用于从文字创建标高符号
        /// </summary>
        public static Point3d CalculateCurrentPointFromText(
            Point3d textPosition, double scale, double d, double angleRadians)
        {
            double paperToModelScale = ToModelLength(1.0, scale);
            double sqrt2 = Math.Sqrt(2) / 2 * d;
            double offsetX = 0.909585 * sqrt2 * paperToModelScale;
            double offsetY = -1.57695 * sqrt2 * paperToModelScale;
            return new Point3d(textPosition.X + offsetX, textPosition.Y + offsetY, 0);
        }

        #region Private

        private static ObjectId CreateTextStyleForElevation(Database db, Document doc)
        {
            var vm = Presentation.ViewModels.SettingsPanelViewModel.Current;
            string styleName = vm != null ? vm.TextStyleName : "0_Hy_40";
            string fontName = vm != null ? vm.FontFileName : "tssdeng.shx";
            string bigFontName = vm != null ? vm.BigFontFileName : "hztxt.shx";
            double scale = vm != null ? vm.Scale : ActiveScaleContextProvider.Current.MainScale;
            double textHeight = (vm != null ? vm.TextSize : 2.5) * GetCurrentUnitFactor() * scale;
            double widthFactor = vm != null ? vm.TextXScale : 0.7;

            ObjectId styleId = ObjectId.Null;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var tst = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                if (tst.Has(styleName))
                {
                    styleId = tst[styleName];
                }
                else
                {
                    tst.UpgradeOpen();
                    var rec = new TextStyleTableRecord
                    {
                        Name = styleName,
                        FileName = fontName,
                        BigFontFileName = bigFontName,
                        TextSize = textHeight,
                        XScale = widthFactor
                    };
                    styleId = tst.Add(rec);
                    tr.AddNewlyCreatedDBObject(rec, true);
                }
                tr.Commit();
            }
            return styleId;
        }

        private static ObjectId CreateLayerForElevation(Database db, Document doc)
        {
            ObjectId layerId = ObjectId.Null;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (lt.Has(ElevationSymbolJig.ElevationLayerName))
                {
                    layerId = lt[ElevationSymbolJig.ElevationLayerName];
                }
                else
                {
                    lt.UpgradeOpen();
                    var rec = new LayerTableRecord
                    {
                        Name = ElevationSymbolJig.ElevationLayerName,
                        Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                            Autodesk.AutoCAD.Colors.ColorMethod.ByAci, ElevationSymbolJig.ElevationLayerColor)
                    };
                    layerId = lt.Add(rec);
                    tr.AddNewlyCreatedDBObject(rec, true);
                }
                tr.Commit();
            }
            return layerId;
        }

        #endregion
    }
}
