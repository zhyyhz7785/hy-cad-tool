//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.Colors;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Geometry;
//using Autodesk.AutoCAD.GraphicsInterface;
//using Autodesk.AutoCAD.Runtime;
//using HyCADTool.Tools;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using static System.Net.Mime.MediaTypeNames;
//using Application = Autodesk.AutoCAD.ApplicationServices.Application;
//using Polyline = Autodesk.AutoCAD.DatabaseServices.Polyline;
//[assembly: CommandClass(typeof(HyCADTool.BaseRein))]
//namespace HyCADTool
//{
//    public static partial class BaseRein
//    {
//        public static void ReinforcementStepB(
//            Dictionary<Polyline, (int Count, double Min, double Max, double Average, double StdDev, double additionalDiameter)> keyValuePairs)
//        {
//            var (doc, db, ed) = InitializeCadEnvironment();
//            if (keyValuePairs == null || keyValuePairs.Count == 0)
//            {
//                ed.WriteMessage("\n请重新选择");
//                return;
//            }
//            Dictionary<ObjectId, (int Count, double Min, double Max, double Average, double StdDev, double additionalDiameter)> updatedKeyValuePairs = new
//            Dictionary<ObjectId, (int Count, double Min, double Max, double Average, double StdDev, double additionalDiameter)>();
//            List<ObjectId> toBeErased = new List<ObjectId>();
//            //生成配筋，更新配筋区域
//            using (DocumentLock docLock = doc.LockDocument()) // 添加文档锁
//            {
//                using (Transaction tr = db.TransactionManager.StartTransaction())
//                {
//                    foreach (var kvp in keyValuePairs)
//                    {
//                        ProcessPolyline(tr, kvp.Key, kvp.Value, updatedKeyValuePairs, toBeErased);
//                    }
//                    AddTableToDrawing(tr, updatedKeyValuePairs, Scale);
//                    tr.Commit();
//                }
//            }
//            //删除旧配筋区域
//            toBeErased.DeleteByObjectIDs(doc, db);
//            using (Transaction tr = db.TransactionManager.StartTransaction())
//            {
//                UpdatePolylineDictionary(tr, updatedKeyValuePairs, keyValuePairs);
//            }
//        }
//        //1. 处理单个多段线的方法
//        private static void ProcessPolyline(
//            Transaction tr,
//            Polyline polyline, 
//            (int Count, double Min, double Max, double Average, double StdDev, double additionalDiameter) stats,
//            Dictionary<ObjectId, (int Count, double Min, double Max, double Average, double StdDev, double additionalDiameter)> updatedKeyValuePairs,
//            List<ObjectId> toBeErased)
//        {
//            //钢筋面积（厘米转换为毫米）
//            double maxArea = stats.Max * 100 * ReinforceSafety;
//            //得到单根钢筋面积
//            double singleRebarArea = Math.PI * Math.Pow(RebarDiameter / 2, 2);
//            double customerArea;
//            if (ExistingRebar == false)
//            {
//                customerArea = 0;
//            }
//            else
//            {
//                customerArea = 1000 / RebarSpacing * singleRebarArea;
//            }
//            double additionalArea = 0;
//            double additionalDiameter = MinAdditionalDiameter;
//            double additionalSpacing = AdditionalSpacing;
//            //循环找到合理配筋
//            foreach (var dia in AllowedDiameters)
//            {
//                // 确保 additionalDiameter 不小于 MinAdditionalDiameter
//                additionalDiameter = Math.Max(dia, MinAdditionalDiameter);
//                double additionalSingleRebarArea = Math.PI * Math.Pow(additionalDiameter / 2, 2);
//                additionalArea = 1000 / additionalSpacing * additionalSingleRebarArea;
//                if (additionalArea + customerArea > maxArea)
//                {
//                    break;
//                }
//            }           
//            Polyline newPolyline = (Polyline)polyline.Clone();
//            //画出钢筋，添加           
//            if (Direction == RebarDirection.TopX)
//            {
//                //1画出x向钢筋,纲吉延伸了锚固长度
//                DrawRebarsInXDirection(tr, newPolyline, additionalDiameter, additionalSpacing, Scale, AnchorFactor);
//                //2调整配筋区域，x向增加锚固长度，并添加到图形。
//                AdjustXDirectionPolyline(newPolyline, additionalDiameter, AnchorFactor);
//                newPolyline.SetLayer("00_hy_调整配筋轮廓_上x");
//                newPolyline.ToSpace();
//                //3更新配筋区域，和钢筋统计数据的字典（统计数据不变）
//                updatedKeyValuePairs.Add(newPolyline.ObjectId, (stats.Count, stats.Min, stats.Max, stats.Average, stats.StdDev, additionalDiameter));
//                //4删除旧的配筋区域
//                toBeErased.Add(polyline.ObjectId);
//            }
//            else if (Direction == RebarDirection.TopY)
//            {
//                //1画出x向钢筋,纲吉延伸了锚固长度
//                DrawRebarsInYDirection(tr, newPolyline, additionalDiameter, additionalSpacing, Scale, AnchorFactor);
//                //2调整配筋区域，x向增加锚固长度，并添加到图形。
//                AdjustYDirectionPolyline(newPolyline, additionalDiameter, AnchorFactor);
//                newPolyline.SetLayer("00_hy_调整配筋轮廓_上y");
//                newPolyline.ToSpace();
//                //3更新配筋区域，和钢筋统计数据的字典（统计数据不变）
//                updatedKeyValuePairs.Add(newPolyline.ObjectId, (stats.Count, stats.Min, stats.Max, stats.Average, stats.StdDev, additionalDiameter));
//                //4删除旧的配筋区域
//                toBeErased.Add(polyline.ObjectId);
//            }
//            else if (Direction == RebarDirection.BottomX)
//            {
//                //1画出x向钢筋,纲吉延伸了锚固长度
//                DrawRebarsInXDirectionButton(tr, newPolyline, additionalDiameter, additionalSpacing, Scale, AnchorFactor);
//                //2调整配筋区域，x向增加锚固长度，并添加到图形。
//                AdjustXDirectionPolyline(newPolyline, additionalDiameter, AnchorFactor);
//                newPolyline.SetLayer("00_hy_调整配筋轮廓_下");
//                newPolyline.ToSpace();
//                //3更新配筋区域，和钢筋统计数据的字典（统计数据不变）
//                updatedKeyValuePairs.Add(newPolyline.ObjectId, (stats.Count, stats.Min, stats.Max, stats.Average, stats.StdDev, additionalDiameter));
//                //4删除旧的配筋区域
//                toBeErased.Add(polyline.ObjectId);
//            }
//            else if (Direction == RebarDirection.BottomY)
//            {
//                //1画出x向钢筋,纲吉延伸了锚固长度
//                DrawRebarsInYDirectionButton(tr, newPolyline, additionalDiameter, additionalSpacing, Scale, AnchorFactor);
//                //2调整配筋区域，x向增加锚固长度，并添加到图形。
//                AdjustYDirectionPolyline(newPolyline, additionalDiameter, AnchorFactor);
//                newPolyline.SetLayer("00_hy_调整配筋轮廓_下");
//                newPolyline.ToSpace();
//                //3更新配筋区域，和钢筋统计数据的字典（统计数据不变）
//                updatedKeyValuePairs.Add(newPolyline.ObjectId, (stats.Count, stats.Min, stats.Max, stats.Average, stats.StdDev, additionalDiameter));
//                //4删除旧的配筋区域
//                toBeErased.Add(polyline.ObjectId);
//            }
//        }
//        //2 统计数据添加到表格中放入CAD
//        private static void AddTableToDrawing(Transaction tr, Dictionary<ObjectId,
//    (int Count, double Min, double Max, double Average, double StdDev, double additionalDiameter)> keyValuePairs,
//    double scale)
//        {
//            Document doc = Application.DocumentManager.MdiActiveDocument;
//            Database db = doc.Database;
//            // 获取并排序多段线对象，根据X增、Y减的顺序
//            var sortedKeyValuePairs = keyValuePairs.OrderBy(kvp =>
//            {
//                var polyline = (Polyline)tr.GetObject(kvp.Key, OpenMode.ForRead);
//                return (polyline.GetPoint3dAt(0).X, -polyline.GetPoint3dAt(0).Y);
//            }).ToList();
//            var p = (Polyline)tr.GetObject(sortedKeyValuePairs[0].Key, OpenMode.ForRead);
//            // 表格的位置
//            var tablePosition = p.NumberOfVertices >= 4 ? p.GetPoint3dAt(3) : new Point3d(0, 0, 0);
//            tablePosition = tablePosition + new Vector3d(-200 * scale, 200 * scale, 0);
//            using (Table table = new Table())
//            {
//                // 设置表格大小：行数 = 数据数 + 表头 + 统计行，列数 = 18
//                table.SetSize(sortedKeyValuePairs.Count + 2, 18); // +1 for header, +1 for stats row
//                var tablelayerId = "00_hy_表格".GetLayerId();
//                table.LayerId = tablelayerId;
//                // 设置表头对齐方式为水平和垂直居中
//                for (int col = 0; col < 18; col++)
//                {
//                    table.Cells[0, col].Alignment = CellAlignment.MiddleCenter;
//                }
//                // 设置数据单元格的对齐方式为水平和垂直居中
//                for (int row = 1; row <= sortedKeyValuePairs.Count+1; row++)
//                {
//                    for (int col = 0; col < 18; col++)
//                    {
//                        table.Cells[row, col].Alignment = CellAlignment.MiddleCenter;
//                        table.Cells[row, col].TextStyleId = TextID;
//                    }
//                }
//                // 设置表头
//                table.Cells[0, 0].TextString = "编号";
//                table.Cells[0, 1].TextString = "数量";
//                table.Cells[0, 2].TextString = "最小(cm²)";
//                table.Cells[0, 3].TextString = "最大(cm²)";
//                table.Cells[0, 4].TextString = "平均(cm²)";
//                table.Cells[0, 5].TextString = "标准差";
//                table.Cells[0, 6].TextString = "面积（m²）";
//                table.Cells[0, 7].TextString = "通长钢筋直径(mm)";
//                table.Cells[0, 8].TextString = "通长钢筋间距(mm)";
//                table.Cells[0, 9].TextString = "通长钢筋每平米用钢量(Kg/m²)";
//                table.Cells[0, 10].TextString = "附加钢筋直径(mm)";
//                table.Cells[0, 11].TextString = "附加钢筋间距(mm)";
//                table.Cells[0, 12].TextString = "附加钢筋每平米用钢量(Kg/m²)";
//                table.Cells[0, 13].TextString = "附加钢筋总重量(Kg)";
//                table.Cells[0, 14].TextString = "配筋量(cm²)";
//                table.Cells[0, 15].TextString = "配筋差值最大(cm²)";
//                table.Cells[0, 16].TextString = "配筋差值最小(cm²)";
//                table.Cells[0, 17].TextString = "配筋差值平均(cm²)";
//                // 统计数据初始化                
//                double totalArea = 0;               
//                double totalReinGA = 0;
//                // 填充数据
//                int rowIndex = 1;
//                int polylineIndex = 1;
//                foreach (var kvp in sortedKeyValuePairs)
//                {
//                    var additionalDiameter = kvp.Value.additionalDiameter;
//                    var polyline = (Polyline)tr.GetObject(kvp.Key, OpenMode.ForRead);
//                    // 附加钢筋面积
//                    double additionalSingleRebarArea = Math.PI * Math.Pow(additionalDiameter / 2, 2);
//                    double SingleRebarArea = Math.PI * Math.Pow(RebarDiameter / 2, 2);
//                    // 计算多段线面积
//                    double area = polyline.Closed ? polyline.Area : 0;
//                    area = area / 1000000;
//                    // 通长钢筋总面积、总重量、用钢量
//                    double reinA, reinG, reinM, reinAA, reinGA, reinMA;
//                    var totalRein = (1000 / AdditionalSpacing * additionalSingleRebarArea + 1000 / RebarSpacing * SingleRebarArea) / 100;
//                    var L1 = polyline.GetLineSegment2dAt(0).Length;
//                    var L2 = polyline.GetLineSegment2dAt(1).Length;
//                    if (Direction == RebarDirection.BottomX || Direction == RebarDirection.TopX)
//                    {
//                        reinA = (Math.Ceiling(L2 / AdditionalSpacing) + 1) * SingleRebarArea * L1 / 1000;
//                        reinG = 7.85 * reinA / 1000;
//                        reinM = reinG / area;
//                        reinAA = (Math.Ceiling(L2 / AdditionalSpacing) + 1) * additionalSingleRebarArea * L1 / 1000;
//                        reinGA = 7.85 * reinAA / 1000;
//                        reinMA = reinGA / area;
//                    }
//                    else
//                    {
//                        reinA = (Math.Ceiling(L1 / AdditionalSpacing) + 1) * SingleRebarArea * L2 / 1000;
//                        reinG = 7.85 * reinA / 1000;
//                        reinM = reinG / area;
//                        reinAA = (Math.Ceiling(L1 / AdditionalSpacing) + 1) * additionalSingleRebarArea * L2 / 1000;
//                        reinGA = 7.85 * reinAA / 1000;
//                        reinMA = reinGA / area;
//                    }
//                    // 填充编号及其他数据
//                    table.Cells[rowIndex, 0].TextString = polylineIndex.ToString();
//                    table.Cells[rowIndex, 1].TextString = kvp.Value.Count.ToString();
//                    table.Cells[rowIndex, 2].TextString = kvp.Value.Min.ToString("F2");
//                    table.Cells[rowIndex, 3].TextString = kvp.Value.Max.ToString("F2");
//                    table.Cells[rowIndex, 4].TextString = kvp.Value.Average.ToString("F2");
//                    table.Cells[rowIndex, 5].TextString = kvp.Value.StdDev.ToString("F2");
//                    table.Cells[rowIndex, 6].TextString = area.ToString("F2");
//                    table.Cells[rowIndex, 7].TextString = RebarDiameter.ToString("F0");
//                    table.Cells[rowIndex, 8].TextString = RebarSpacing.ToString("F0");
//                    table.Cells[rowIndex, 9].TextString = reinM.ToString("F3");
//                    table.Cells[rowIndex, 10].TextString = additionalDiameter.ToString("F0");
//                    table.Cells[rowIndex, 11].TextString = AdditionalSpacing.ToString("F0");
//                    table.Cells[rowIndex, 12].TextString = reinMA.ToString("F3");
//                    table.Cells[rowIndex, 13].TextString = reinGA.ToString("F3");
//                    table.Cells[rowIndex, 14].TextString = totalRein.ToString("F3");
//                    table.Cells[rowIndex, 15].TextString = (totalRein - kvp.Value.Min).ToString("F3");
//                    table.Cells[rowIndex, 16].TextString = (totalRein - kvp.Value.Max).ToString("F3");
//                    table.Cells[rowIndex, 17].TextString = (totalRein - kvp.Value.Average).ToString("F3");
//                    // 统计数据累加                   
//                    totalArea += area;                   
//                    totalReinGA += reinGA;
//                    rowIndex++;
//                    polylineIndex++;
//                }
//                // 添加统计行
//                table.Cells[rowIndex, 0].TextString = "合计";                
//                table.Cells[rowIndex, 6].TextString = totalArea.ToString("F2");               
//                table.Cells[rowIndex, 13].TextString = totalReinGA.ToString("F3");
//                // 设置表格位置和大小
//                table.Position = tablePosition;
//                table.SetRowHeight(5 * scale);
//                table.SetColumnWidth(18 * scale);
//                // 将表格添加到图形中
//                BlockTableRecord btrTable = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
//                btrTable.AppendEntity(table);
//                tr.AddNewlyCreatedDBObject(table, true);
//            }
//        }
//        //3绘制钢筋
//        #region 绘制钢筋的方法
//        /// <summary>
//        /// 
//        /// </summary>
//        /// <param name="tr"></param>
//        /// <param name="polyline"></param> 配筋区域
//        /// <param name="diameter"></param>
//        /// <param name="spacing"></param>
//        /// <param name="scale"></param>
//        /// <param name="anchorFactor"></param>
//        private static void DrawRebarsInXDirection(Transaction tr, Polyline polyline, double diameter, double spacing, double scale, double anchorFactor)
//        {
//            var doc = Application.DocumentManager.MdiActiveDocument;
//            var db = doc.Database;
//            // 获取polyline起点和终点
//            var startPoint = polyline.GetPoint3dAt(0);
//            var endPoint = polyline.GetPoint3dAt(1);
//            var point3 = polyline.GetPoint3dAt(2);
//            // 计算y向中心位置
//            double yCenter = (startPoint.Y + point3.Y) / 2 + ReinforceDistance * scale;
//            // 创建钢筋Polyline
//            Polyline rebarPolyline = new Polyline();
//            rebarPolyline.AddVertexAt(0, new Point2d(startPoint.X - AnchorFactor * diameter, yCenter - HookLength * Scale), 0, PolylineWidth * Scale, PolylineWidth * Scale); // 设置起始宽度和结束宽度
//            rebarPolyline.AddVertexAt(1, new Point2d(startPoint.X - AnchorFactor * diameter, yCenter), 0, PolylineWidth * Scale, PolylineWidth * Scale);
//            rebarPolyline.AddVertexAt(2, new Point2d(endPoint.X + AnchorFactor * diameter, yCenter), 0, PolylineWidth * Scale, PolylineWidth * Scale);
//            rebarPolyline.AddVertexAt(3, new Point2d(endPoint.X + AnchorFactor * diameter, yCenter - HookLength * Scale), 0, PolylineWidth * Scale, PolylineWidth * Scale);
//            rebarPolyline.Layer = "00_hy_筏板附加配筋x_上";
//            // 设置颜色
//            //rebarPolyline.Color = Color.FromColorIndex(ColorMethod.ByAci, 1); // 红色            
//            // 添加钢筋Polyline到图形
//            var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
//            btr.AppendEntity(rebarPolyline);
//            tr.AddNewlyCreatedDBObject(rebarPolyline, true);
//            // 创建标注
//            CreateRebarLabelx(tr, rebarPolyline, diameter, spacing, scale);
//        }
//        private static void DrawRebarsInYDirection(Transaction tr, Polyline polyline, double diameter, double spacing, double scale, double anchorFactor)
//        {
//            var doc = Application.DocumentManager.MdiActiveDocument;
//            var db = doc.Database;
//            // 获取polyline起点和终点
//            var startPoint = polyline.GetPoint3dAt(1);
//            var endPoint = polyline.GetPoint3dAt(2);
//            var point3 = polyline.GetPoint3dAt(0);
//            double xDistance = (startPoint.X - point3.X) / 2 + ReinforceDistance * scale;
//            // 创建钢筋Polyline
//            Polyline rebarPolyline = new Polyline();
//            // Y向上下延长 AnchorFactor * diameter
//            rebarPolyline.AddVertexAt(0, new Point2d(startPoint.X - xDistance + HookLength * Scale, startPoint.Y - AnchorFactor * diameter), 0, PolylineWidth * Scale, PolylineWidth * Scale); // 设置起始宽度和结束宽度
//            rebarPolyline.AddVertexAt(1, new Point2d(startPoint.X - xDistance, startPoint.Y - AnchorFactor * diameter), 0, PolylineWidth * Scale, PolylineWidth * Scale);
//            rebarPolyline.AddVertexAt(2, new Point2d(startPoint.X - xDistance, endPoint.Y + AnchorFactor * diameter), 0, PolylineWidth * Scale, PolylineWidth * Scale);
//            rebarPolyline.AddVertexAt(3, new Point2d(startPoint.X - xDistance + HookLength * Scale, endPoint.Y + AnchorFactor * diameter), 0, PolylineWidth * Scale, PolylineWidth * Scale);
//            rebarPolyline.Layer = "00_hy_筏板附加配筋y_上";
//            // 添加钢筋Polyline到图形
//            var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
//            btr.AppendEntity(rebarPolyline);
//            tr.AddNewlyCreatedDBObject(rebarPolyline, true);
//            // 创建标注
//            CreateRebarLabely(tr, rebarPolyline, diameter, spacing, scale);
//        }
//        private static void DrawRebarsInXDirectionButton(Transaction tr, Polyline polyline, double diameter, double spacing, double scale, double anchorFactor)
//        {
//            var doc = Application.DocumentManager.MdiActiveDocument;
//            var db = doc.Database;
//            // 获取polyline起点和终点
//            var startPoint = polyline.GetPoint3dAt(0);
//            var endPoint = polyline.GetPoint3dAt(1);
//            var point3 = polyline.GetPoint3dAt(2);
//            var m = AnchorFactor * diameter;
//            var w = HookLength * Scale / Math.Pow(2, 0.5);
//            // 计算y向中心位置
//            double yCenter = (startPoint.Y + point3.Y) / 2 - ReinforceDistance * scale;
//            // 创建钢筋Polyline
//            Polyline rebarPolyline = new Polyline();
//            rebarPolyline.AddVertexAt(0, new Point2d(startPoint.X - m + w, yCenter + w), 0, PolylineWidth * Scale, PolylineWidth * Scale); // 设置起始宽度和结束宽度
//            rebarPolyline.AddVertexAt(1, new Point2d(startPoint.X - m, yCenter), 0, PolylineWidth * Scale, PolylineWidth * Scale);
//            rebarPolyline.AddVertexAt(2, new Point2d(endPoint.X + m, yCenter), 0, PolylineWidth * Scale, PolylineWidth * Scale);
//            rebarPolyline.AddVertexAt(3, new Point2d(endPoint.X + m - w, yCenter + w), 0, PolylineWidth * Scale, PolylineWidth * Scale);
//            rebarPolyline.Layer = "00_hy_筏板附加配筋x_上";
//            // 设置颜色
//            //rebarPolyline.Color = Color.FromColorIndex(ColorMethod.ByAci, 1); // 红色            
//            // 添加钢筋Polyline到图形
//            var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
//            btr.AppendEntity(rebarPolyline);
//            tr.AddNewlyCreatedDBObject(rebarPolyline, true);
//            // 创建标注
//            CreateRebarLabelx(tr, rebarPolyline, diameter, spacing, scale);
//        }
//        private static void DrawRebarsInYDirectionButton(Transaction tr, Polyline polyline, double diameter, double spacing, double scale, double anchorFactor)
//        {
//            var doc = Application.DocumentManager.MdiActiveDocument;
//            var db = doc.Database;
//            // 获取polyline起点和终点
//            var startPoint = polyline.GetPoint3dAt(1);
//            var endPoint = polyline.GetPoint3dAt(2);
//            var point3 = polyline.GetPoint3dAt(0);
//            double x = (startPoint.X - point3.X) / 2 - ReinforceDistance * scale;
//            var m = AnchorFactor * diameter;
//            var w = HookLength * Scale / Math.Pow(2, 0.5);
//            // 创建钢筋Polyline
//            Polyline rebarPolyline = new Polyline();
//            // Y向上下延长 AnchorFactor * diameter
//            rebarPolyline.AddVertexAt(0, new Point2d(startPoint.X - x - w, startPoint.Y - m + w), 0, PolylineWidth * Scale, PolylineWidth * Scale); // 设置起始宽度和结束宽度
//            rebarPolyline.AddVertexAt(1, new Point2d(startPoint.X - x, startPoint.Y - m), 0, PolylineWidth * Scale, PolylineWidth * Scale);
//            rebarPolyline.AddVertexAt(2, new Point2d(startPoint.X - x, endPoint.Y + m), 0, PolylineWidth * Scale, PolylineWidth * Scale);
//            rebarPolyline.AddVertexAt(3, new Point2d(startPoint.X - x - w, endPoint.Y + m - w), 0, PolylineWidth * Scale, PolylineWidth * Scale);
//            rebarPolyline.Layer = "00_hy_筏板附加配筋y_上";
//            // 添加钢筋Polyline到图形
//            var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
//            btr.AppendEntity(rebarPolyline);
//            tr.AddNewlyCreatedDBObject(rebarPolyline, true);
//            // 创建标注
//            CreateRebarLabely(tr, rebarPolyline, diameter, spacing, scale);
//        }
//        private static void CreateRebarLabelx(Transaction tr, Polyline rebarPolyline, double diameter, double spacing, double scale)
//        {
//            var doc = Application.DocumentManager.MdiActiveDocument;
//            var db = doc.Database;
//            // 获取钢筋中心点
//            var midpoint = rebarPolyline.GetPoint3dAt(0).GetMidPoint(rebarPolyline.GetPoint3dAt(2));
//            // 创建DBText对象
//            if (Direction == RebarDirection.BottomX)
//            {
//                midpoint = new Point3d(midpoint.X, midpoint.Y - HookLength * scale, midpoint.Z);
//            }
//            midpoint = new Point3d(midpoint.X + ReinforceTextDistanceX * scale, midpoint.Y, midpoint.Z);
//            DBText rebarLabel = new DBText
//            {
//                Position = new Point3d(midpoint.X, midpoint.Y + TextToLineDistance * scale, 0),
//                Height = 3 * scale,
//                WidthFactor = 0.7,
//                TextString = $"\\u+e532{diameter}@{spacing}",
//                Layer = "00_hy_筏板附加配筋文字_x",
//                Color = Color.FromColorIndex(ColorMethod.ByAci, 7) // 白色
//            };
//            // 设置文本对齐方式为下中
//            rebarLabel.HorizontalMode = TextHorizontalMode.TextCenter; // 水平居中
//            rebarLabel.VerticalMode = TextVerticalMode.TextBase; // 垂直下对齐
//                                                                 // 调整文本位置，使其以 Position 属性为基准点
//            rebarLabel.AlignmentPoint = rebarLabel.Position;
//            // 添加DBText到图形
//            var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
//            btr.AppendEntity(rebarLabel);
//            tr.AddNewlyCreatedDBObject(rebarLabel, true);
//        }
//        private static void CreateRebarLabely(Transaction tr, Polyline rebarPolyline, double diameter, double spacing, double scale)
//        {
//            var doc = Application.DocumentManager.MdiActiveDocument;
//            var db = doc.Database;
//            // 获取钢筋中心点
//            var midpoint = rebarPolyline.GetPoint3dAt(0).GetMidPoint(rebarPolyline.GetPoint3dAt(2));
//            if (Direction == RebarDirection.BottomY)
//            {
//                midpoint = new Point3d(midpoint.X + HookLength * scale, midpoint.Y, midpoint.Z);
//            }
//            midpoint = new Point3d(midpoint.X, midpoint.Y + ReinforceTextDistanceY * scale, midpoint.Z);
//            // 创建DBText对象
//            DBText rebarLabel = new DBText
//            {
//                Position = new Point3d(midpoint.X - TextToLineDistance * scale, midpoint.Y, 0),
//                Height = 3 * scale,
//                WidthFactor = 0.7,
//                Rotation = Math.PI / 2, // 旋转90度
//                TextString = $"\\u+e532{diameter}@{spacing}",
//                Layer = "00_hy_筏板附加配筋文字_y",
//                Color = Color.FromColorIndex(ColorMethod.ByAci, 7) // 白色
//            };
//            // 设置文本对齐方式为下中
//            rebarLabel.HorizontalMode = TextHorizontalMode.TextCenter; // 水平居中
//            rebarLabel.VerticalMode = TextVerticalMode.TextBase; // 垂直下对齐
//                                                                 // 调整文本位置，使其以 Position 属性为基准点
//            rebarLabel.AlignmentPoint = rebarLabel.Position;
//            // 添加DBText到图形
//            var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
//            btr.AppendEntity(rebarLabel);
//            tr.AddNewlyCreatedDBObject(rebarLabel, true);
//        }
//        //4. 调整多段线的方法  
//        private static void AdjustXDirectionPolyline(Polyline newPolyline, double additionalDiameter, double anchorFactor)
//        {
//            Point3d point0 = newPolyline.GetPoint3dAt(0);
//            Point3d point3 = newPolyline.GetPoint3dAt(3);
//            Point3d point1 = newPolyline.GetPoint3dAt(1);
//            Point3d point2 = newPolyline.GetPoint3dAt(2);
//            newPolyline.SetPointAt(0, new Point2d(point0.X - anchorFactor * additionalDiameter, point0.Y));
//            newPolyline.SetPointAt(3, new Point2d(point3.X - anchorFactor * additionalDiameter, point3.Y));
//            newPolyline.SetPointAt(1, new Point2d(point1.X + anchorFactor * additionalDiameter, point1.Y));
//            newPolyline.SetPointAt(2, new Point2d(point2.X + anchorFactor * additionalDiameter, point2.Y));
//        }
//        private static void AdjustYDirectionPolyline(Polyline newPolyline, double additionalDiameter, double anchorFactor)
//        {
//            Point3d point0 = newPolyline.GetPoint3dAt(0);
//            Point3d point3 = newPolyline.GetPoint3dAt(3);
//            Point3d point1 = newPolyline.GetPoint3dAt(1);
//            Point3d point2 = newPolyline.GetPoint3dAt(2);
//            var d = anchorFactor * additionalDiameter;
//            newPolyline.SetPointAt(0, new Point2d(point0.X, point0.Y - d));
//            newPolyline.SetPointAt(1, new Point2d(point1.X, point1.Y - d));
//            newPolyline.SetPointAt(2, new Point2d(point2.X, point2.Y + d));
//            newPolyline.SetPointAt(3, new Point2d(point3.X, point3.Y + d));
//        }
//        #endregion
//        //辅助方法4.1
//        //4.1 提取文档和数据库初始化
//        private static (Document, Database, Editor) InitializeCadEnvironment()
//        {
//            Document doc = Application.DocumentManager.MdiActiveDocument;
//            Database db = doc.Database;
//            Editor ed = doc.Editor;
//            return (doc, db, ed);
//        }
//        //4.2 提取更新字典的方法
//        private static void UpdatePolylineDictionary(Transaction tr,
//            Dictionary<ObjectId, (int Count, double Min, double Max, double Average, double StdDev, double additionalDiameter)> updatedKeyValuePairs,
//            Dictionary<Polyline, (int Count, double Min, double Max, double Average, double StdDev, double additionalDiameter)> keyValuePairs)
//        {
//            keyValuePairs.Clear();
//            foreach (var kvp in updatedKeyValuePairs)
//            {
//                var polyline = tr.GetObject(kvp.Key, OpenMode.ForRead) as Polyline;
//                keyValuePairs.Add(polyline, kvp.Value);
//            }
//        }
//        //4.2 求中点
//        public static Point3d GetMidPoint(this Point3d pt1, Point3d pt2)
//        {
//            return new Point3d((pt1.X + pt2.X) / 2, (pt1.Y + pt2.Y) / 2, (pt1.Z + pt2.Z) / 2);
//        }
//    }
//}
