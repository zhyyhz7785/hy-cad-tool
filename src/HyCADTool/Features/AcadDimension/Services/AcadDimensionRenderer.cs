using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Features.AcadDimension.Domain.Results;

namespace HyCADTool.Features.AcadDimension.Services
{
    /// <summary>
    /// 默认 Renderer：把 Domain 标注（DerivedDimension）转换为 AutoCAD RotatedDimension，
    /// 写入外层 Service 提供的 Document + Transaction（不嵌套事务，修 06 §7 #9）。
    ///
    /// 设计要点：
    ///  - 单 Transaction 写入；任何异常 throw 给 Service，由 Service Abort 整体回滚；
    ///  - 当前阶段使用文档当前 DimStyle / 当前 Layer——Phase 5 再扩展图层与样式策略；
    ///  - 仅 ModelSpace 写入（旧 dds 一致）。
    /// </summary>
    public sealed class AcadDimensionRenderer : INewDdsRenderer
    {
        public int Render(NewDdsResult result, Document document, Transaction transaction)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (result.Dimensions == null || result.Dimensions.Count == 0) return 0;

            var db = document.Database;
            var bt = (BlockTable)transaction.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)transaction.GetObject(
                bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

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
                written++;
            }
            return written;
        }
    }
}
