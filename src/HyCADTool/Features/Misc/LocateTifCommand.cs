using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Features.Elevation.Domain.Interfaces;
using HyCAD.Geometry;
using HyCADTool.App.Bootstrap;
using System;
using System.Diagnostics;
using System.IO;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Misc
{
    /// <summary>
    /// 读取TFW文件自动定位TIF图像的命令
    /// </summary>
    public class LocateTifCommand
    {
        private readonly IGeospatialService _geospatialService;

        public LocateTifCommand()
        {
            _geospatialService = ServiceLocator.Resolve<IGeospatialService>();
        }

        /// <summary>
        /// 执行命令：选择TFW文件并定位对应的TIF图像
        /// </summary>
        public void Execute()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // 让用户选择TIF文件
                string tifFilePath = SelectTifFile(ed);
                if (string.IsNullOrEmpty(tifFilePath))
                {
                    ed.WriteMessage("\n操作已取消。");
                    return;
                }

                // 获取对应的TFW文件路径
                string tfwFilePath = GetCorrespondingTfwFile(tifFilePath);
                if (!File.Exists(tfwFilePath))
                {
                    ed.WriteMessage($"\n未找到对应的TFW文件: {tfwFilePath}");
                    ed.WriteMessage($"\n提示: TFW文件应与TIF文件同名，位于同一目录");
                    return;
                }

                // 解析TFW文件
                IGeospatialService.WorldFileParameters parameters;
                try
                {
                    parameters = _geospatialService.ParseWorldFile(tfwFilePath);
                    if (!parameters.IsValid)
                    {
                        ed.WriteMessage("\nTFW文件参数无效。");
                        return;
                    }
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n解析TFW文件失败: {ex.Message}");
                    return;
                }

                // 获取图像尺寸
                System.Drawing.Size imageSize;
                try
                {
                    using (var image = System.Drawing.Image.FromFile(tifFilePath))
                    {
                        imageSize = image.Size;
                    }
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n读取TIF文件失败: {ex.Message}");
                    return;
                }

                // 计算图像边界
                Polygon2D boundary;
                try
                {
                    boundary = _geospatialService.CalculateImageBoundary(parameters, imageSize.Width, imageSize.Height);
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n计算图像边界失败: {ex.Message}");
                    return;
                }

                // 在CAD中插入图像和绘制边界
                DrawImageBoundary(ed, boundary, Path.GetFileNameWithoutExtension(tifFilePath), parameters, tifFilePath);

                // 输出统计信息
                var bounds = boundary.GetBoundingBox();
                ed.WriteMessage($"\n图像插入完成: {Path.GetFileNameWithoutExtension(tifFilePath)}");
                ed.WriteMessage($"\n  图像尺寸: {imageSize.Width} x {imageSize.Height} 像素");
                ed.WriteMessage($"\n  地理范围: X:{boundary.Vertices[0].X:F2} - {boundary.Vertices[1].X:F2}, Y:{boundary.Vertices[3].Y:F2} - {boundary.Vertices[0].Y:F2}");

                stopwatch.Stop();
                ed.WriteMessage($"\nINFO: 图像插入 耗时 {stopwatch.ElapsedMilliseconds} 毫秒");

            }
            catch (System.Exception ex)
            {
                stopwatch.Stop();
                ed.WriteMessage($"\n错误：{ex.Message}");
                if (ex.InnerException != null)
                {
                    ed.WriteMessage($"\n详细信息：{ex.InnerException.Message}");
                }
            }
        }

        /// <summary>
        /// 让用户选择TIF文件
        /// </summary>
        private string SelectTifFile(Editor ed)
        {
            try
            {
                // 使用AutoCAD的文件选择对话框
                var fileDialog = new Autodesk.AutoCAD.Windows.OpenFileDialog(
                    "选择TIF图像文件",
                    null,
                    "tif",
                    "TIF Files (*.tif;*.tiff)|*.tif;*.tiff|All Files (*.*)|*.*",
                    Autodesk.AutoCAD.Windows.OpenFileDialog.OpenFileDialogFlags.NoUrls
                );

                if (fileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    return fileDialog.Filename;
                }
            }
            catch
            {
                // 如果AutoCAD文件对话框失败，使用系统对话框
                var openFileDialog = new System.Windows.Forms.OpenFileDialog
                {
                    Title = "选择TIF图像文件",
                    Filter = "TIF Files (*.tif;*.tiff)|*.tif;*.tiff|All Files (*.*)|*.*",
                    CheckFileExists = true,
                    CheckPathExists = true
                };

                if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    return openFileDialog.FileName;
                }
            }

            return null;
        }

        /// <summary>
        /// 根据TIF文件路径获取对应的TFW文件路径
        /// </summary>
        private string GetCorrespondingTfwFile(string tifFilePath)
        {
            string directory = Path.GetDirectoryName(tifFilePath);
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(tifFilePath);

            // 常见TFW文件扩展名
            string[] tfwExtensions = { ".tfw", ".TFW", ".tiff.tfw", ".tif.tfw" };

            foreach (var ext in tfwExtensions)
            {
                string tfwPath = Path.Combine(directory, fileNameWithoutExt + ext);
                if (File.Exists(tfwPath))
                {
                    return tfwPath;
                }
            }

            // 如果没找到同名文件，尝试查找目录中的TFW文件（简单策略）
            var tfwFiles = Directory.GetFiles(directory, "*.tfw", SearchOption.TopDirectoryOnly);
            if (tfwFiles.Length == 1)
            {
                return tfwFiles[0];
            }

            // 返回默认的TFW文件路径（同名.tfw扩展名）
            return Path.Combine(directory, fileNameWithoutExt + ".tfw");
        }

        /// <summary>
        /// 在CAD中插入图像和绘制图像边界
        /// </summary>
        private void DrawImageBoundary(Editor ed, Polygon2D boundary, string imageName, IGeospatialService.WorldFileParameters parameters, string tifFilePath)
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                // 创建图层
                string layerName = "00_Hy_图像定位";
                CreateLayerIfNotExists(db, tr, layerName);

                // 只插入TIF图像，不绘制边界
                try
                {
                    InsertRasterImage(db, tr, btr, tifFilePath, boundary, layerName);
                    ed.WriteMessage($"\n✓ TIF 图像已成功插入并按 TFW 信息自动定位");
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n错误: 图像插入失败 - {ex.Message}");
                    return;
                }

                tr.Commit();
            }
        }

        /// <summary>
        /// 在CAD中插入栅格图像（TIF）
        /// </summary>
        private void InsertRasterImage(Database db, Transaction tr, BlockTableRecord btr, string tifFilePath, Polygon2D boundary, string layerName)
        {
            try
            {
                // 获取或创建图像字典
                var imageDictId = RasterImageDef.GetImageDictionary(db);
                if (imageDictId == ObjectId.Null)
                {
                    imageDictId = RasterImageDef.CreateImageDictionary(db);
                }

                var imageDict = (DBDictionary)tr.GetObject(imageDictId, OpenMode.ForWrite);

                // 创建图像定义
                var imageDef = new RasterImageDef();
                imageDef.SourceFileName = Path.GetFullPath(tifFilePath);

                // 将图像定义添加到字典
                string imageDefName = $"Image_{Path.GetFileNameWithoutExtension(tifFilePath)}_{DateTime.Now.Ticks}";
                imageDict.SetAt(imageDefName, imageDef);
                tr.AddNewlyCreatedDBObject(imageDef, true);

                // 加载图像
                imageDef.Load();

                // 获取图像的四个角点
                var vertices = boundary.Vertices;
                if (vertices.Count < 4)
                {
                    throw new InvalidOperationException("边界必须至少有4个顶点");
                }

                // 创建图像实例
                var rasterImage = new RasterImage();
                rasterImage.ImageDefId = imageDef.ObjectId;
                rasterImage.Layer = layerName;

                // 设置图像的基本变换
                // 计算图像的变换矩阵 - 使用左下角作为插入点
                var imageOrigin = new Point3d(vertices[3].X, vertices[3].Y, 0); // 左下角作为插入点
                var imageWidth = Math.Abs(vertices[1].X - vertices[0].X);
                var imageHeight = Math.Abs(vertices[0].Y - vertices[3].Y);
                
                // 获取图像的原始尺寸
                var imageSize = imageDef.Size;
                var originalWidth = imageSize.X;
                var originalHeight = imageSize.Y;
                
                // 计算缩放因子（使用平均缩放以保持比例）
                var scaleX = imageWidth / originalWidth;
                var scaleY = imageHeight / originalHeight;
                var averageScale = (scaleX + scaleY) / 2.0; // 使用平均缩放保持比例
                
                // 创建变换矩阵：先缩放，再平移到正确位置
                var transformMatrix = Matrix3d.Identity;
                
                // 统一缩放（以原点为中心）
                var scaleMatrix = Matrix3d.Scaling(averageScale, Point3d.Origin);
                transformMatrix = transformMatrix.PreMultiplyBy(scaleMatrix);
                
                // 平移到目标位置
                var displacementMatrix = Matrix3d.Displacement(imageOrigin.GetAsVector());
                transformMatrix = transformMatrix.PreMultiplyBy(displacementMatrix);
                
                // 应用变换
                rasterImage.TransformBy(transformMatrix);

                // 添加图像到模型空间
                btr.AppendEntity(rasterImage);
                tr.AddNewlyCreatedDBObject(rasterImage, true);
            }
            catch (System.Exception ex)
            {
                // 如果插入失败，记录错误但不中断流程
                throw new InvalidOperationException($"插入图像失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 创建图层（如果不存在）
        /// </summary>
        private void CreateLayerIfNotExists(Database db, Transaction tr, string layerName)
        {
            var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (!layerTable.Has(layerName))
            {
                layerTable.UpgradeOpen();
                var newLayer = new LayerTableRecord
                {
                    Name = layerName,
                    Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 1)
                };
                layerTable.Add(newLayer);
                tr.AddNewlyCreatedDBObject(newLayer, true);
            }
        }
    }
}
