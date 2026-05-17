using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Features.AcadDimension.Domain.Results;
using HyCADTool.Shared.AutoCAD.Xdata;

namespace HyCADTool.Features.AcadDimension.Services
{
    /// <summary>
    /// 默认 Renderer：把 Domain 标注（DerivedDimension）转换为 AutoCAD RotatedDimension，
    /// 写入外层 Service 提供的 Document + Transaction（不嵌套事务，修 06 §7 #9）。
    ///
    /// Phase 5：可选将 <see cref="NewDdsDimensionBinding"/> 写入每条标注扩展字典（键
    /// <see cref="NewDdsBindingKeys.ExtensionDictionaryKey"/>），便于 nddsR 追溯源多段线。
    /// </summary>
    public sealed class AcadDimensionRenderer : INewDdsRenderer
    {
        public int Render(
            NewDdsResult result,
            Document document,
            Transaction transaction,
            NewDdsRenderBindingContext bindingContext = null)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (result.Dimensions == null || result.Dimensions.Count == 0) return 0;

            var db = document.Database;
            var bt = (BlockTable)transaction.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)transaction.GetObject(
                bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            NewDdsDimensionBinding bindingTemplate = null;
            if (bindingContext != null && bindingContext.IsBindingEnabled)
            {
                var src = transaction.GetObject(bindingContext.SourcePolylineId, OpenMode.ForRead);
                bindingTemplate = new NewDdsDimensionBinding
                {
                    SourcePolylineHandle = src.Handle.ToString(),
                    FeatureSignature = bindingContext.FeatureSignature,
                    ConfigSnapshot = bindingContext.ConfigSnapshot
                };
            }

            int written = 0;
            foreach (var d in result.Dimensions)
            {
                if (d == null) continue;

                var p1 = new Point3d(d.ExtensionLine1Point.X, d.ExtensionLine1Point.Y, 0.0);
                var p2 = new Point3d(d.ExtensionLine2Point.X, d.ExtensionLine2Point.Y, 0.0);
                var dl = new Point3d(d.DimensionLinePoint.X, d.DimensionLinePoint.Y, 0.0);

                var dim = new RotatedDimension(d.Rotation, p1, p2, dl, string.Empty, ObjectId.Null);
                ms.AppendEntity(dim);
                transaction.AddNewlyCreatedDBObject(dim, true);

                if (bindingTemplate != null)
                {
                    ExtensionDictionaryService.Write(
                        transaction,
                        dim,
                        bindingTemplate,
                        NewDdsBindingKeys.ExtensionDictionaryKey);
                }

                written++;
            }

            return written;
        }
    }
}
