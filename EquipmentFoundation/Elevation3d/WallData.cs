using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using EquipmentFoundation.Models;




namespace EquipmentFoundation
{
    public partial class ElevationModelGenerator
    {
        // 在 ElevationModelGenerator 类中添加以下方法
        public void GenerateBoltHoles(
            GeometryData geomData,
            BlockTableRecord btr,
            Transaction tr,
            AnchorBolt AnchorBolt
        )
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage("\n开始生成螺栓开孔3D模型...\n");

            if (AnchorBolt == null)
            {
                ed.WriteMessage("\n警告: 未指定 AnchorBolt，跳过螺栓开孔生成\n");
                return;
            }

            if (geomData.Polygon == null || !geomData.Polygon.Closed)
            {
                ed.WriteMessage("\n警告: 底板 Polygon 未定义或未闭合，跳过螺栓开孔生成\n");
                return;
            }

            double diameter = AnchorBolt.D;
            double holeDepth = AnchorBolt.H1 + 150.0; // h1 + 150
            double baseElevation = (geomData.Walls != null && geomData.Walls.Count > 0)
                ? geomData.Walls[0].InnerElevation  // 使用底板顶部标高
                : 0.0;

            // 在底板中心生成一个示例螺栓孔（可以根据需要调整位置）
            Point3d centerPoint = geomData.Polygon.GetPoint3dAt(geomData.Polygon.NumberOfVertices / 2);
            CreateBoltHoleCylinder(centerPoint, diameter, holeDepth, baseElevation, btr, tr);
            ed.WriteMessage($"\n螺栓开孔生成: 位置={centerPoint}, 直径={diameter}, 深度={holeDepth}, 顶部标高={baseElevation}\n");
        }

        // 创建单个螺栓孔圆柱体
        private void CreateBoltHoleCylinder(
            Point3d centerPoint,
            double diameter,
            double holeDepth,
            double topElevation,
            BlockTableRecord btr,
            Transaction tr
        )
        {
            using (Solid3d cylinder = new Solid3d())
            {
                // 创建圆形基底
                using (Circle baseCircle = new Circle())
                {
                    baseCircle.Center = centerPoint;
                    baseCircle.Radius = diameter / 2.0;
                    baseCircle.Normal = Vector3d.ZAxis;

                    // 创建拉伸的圆柱体，向下延伸
                    cylinder.CreateExtrudedSolid(baseCircle, new Vector3d(0, 0, -holeDepth), new SweepOptions());
                    cylinder.TransformBy(Matrix3d.Displacement(new Vector3d(0, 0, topElevation))); // 移动到顶部标高
                    cylinder.LayerId = Bufferid_Base; // 使用底板图层，也可以定义新图层如 Bufferid_BoltHole
                    btr.AppendEntity(cylinder);
                    tr.AddNewlyCreatedDBObject(cylinder, true);
                }
            }
        }
        // 生成墙体
        public void GenerateWalls(List<GeometryData> geometryDatas,
            AnchorBolt AnchorBolt = null // 预留后期AnchorBolt类型
        )
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            ed.WriteMessage("\n开始生成墙体3D模型...\n");

            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    GeometryExtensions.JoinType = JoinType.Miter;

                    foreach (var geomData in geometryDatas)
                    {
                        geomData.GetWallSegments();
                        ed.WriteMessage($"墙体段类型: {geomData.SegmentType}\n");

                        ProcessWalls(geomData, btr, tr, AnchorBolt);
                    }

                    tr.Commit();
                    ed.WriteMessage("\n墙体生成完成\n");
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\n处理失败: {ex.Message}\n");
                    tr.Abort();
                }
            }
        }

        // 生成筏板和底板
        public void GenerateRaftAndBase(List<GeometryData> geometryDatas, double defaultRaftThickness = 400.0)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            ed.WriteMessage("\n开始生成筏板和底板3D模型...\n");

            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    foreach (var geomData in geometryDatas)
                    {
                        // 为每个 GeometryData 计算独立厚度
                        double raftThickness = GetThicknessForGeometryData(geomData, defaultRaftThickness);
                        ProcessRaftAndBase(geomData, btr, tr, raftThickness);
                    }

                    tr.Commit();
                    ed.WriteMessage("\n筏板和底板生成完成\n");
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\n处理失败: {ex.Message}\n");
                    tr.Abort();
                }
            }
        }

        // 处理墙体
        private void ProcessWalls(
            GeometryData geomData,
            BlockTableRecord btr,
            Transaction tr,
            AnchorBolt AnchorBolt
        )
        {
            List<(Polyline Buffer, WallData Wall)> bufferWallPairs = null;
            switch (geomData.SegmentType)
            {
                case WallSegmentType.ClosedCurve:
                    bufferWallPairs = GenerateClosedCurveBuffers(geomData);
                    break;
                case WallSegmentType.SingleOpenCurve:
                case WallSegmentType.MultipleOpenCurves:
                    bufferWallPairs = GenerateOpenCurveBuffers(geomData);
                    break;
                default:
                    var ed = Application.DocumentManager.MdiActiveDocument.Editor;
                    ed.WriteMessage("未定义的墙体类型，跳过处理\n");
                    return;
            }

            if (bufferWallPairs != null && bufferWallPairs.Count > 0)
            {
                ConvertWallsTo3D(bufferWallPairs, btr, tr, AnchorBolt);
            }
        }

        // 处理筏板和底板
        private void ProcessRaftAndBase(
            GeometryData geomData,
            BlockTableRecord btr,
            Transaction tr,
            double raftThickness
        )
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;

            // 生成墙体下的筏板
            if (geomData.Walls != null && geomData.Walls.Count > 0)
            {
                List<(Polyline Buffer, WallData Wall)> bufferWallPairs = null;
                switch (geomData.SegmentType)
                {
                    case WallSegmentType.ClosedCurve:
                        bufferWallPairs = GenerateClosedCurveBuffers(geomData);
                        break;
                    case WallSegmentType.SingleOpenCurve:
                    case WallSegmentType.MultipleOpenCurves:
                        bufferWallPairs = GenerateOpenCurveBuffers(geomData);
                        break;
                }

                if (bufferWallPairs != null && bufferWallPairs.Count > 0)
                {
                    GenerateWallRaft(bufferWallPairs, btr, tr, raftThickness);
                }
            }

            // 生成独立的底板（可能没有墙体）
            GenerateBasePlate(geomData, btr, tr, raftThickness);
        }

        // 生成封闭曲线的缓冲区
        private List<(Polyline Buffer, WallData Wall)> GenerateClosedCurveBuffers(GeometryData geomData)
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage("\n开始处理封闭曲线\n");

            if (!geomData.Polygon.Closed || !geomData.Polygon.StartPoint.IsEqualTo(geomData.Polygon.EndPoint))
            {
                ed.WriteMessage("\n警告: 多边形未闭合，已跳过处理\n");
                return new List<(Polyline, WallData)>();
            }

            List<(Polyline Buffer, WallData Wall)> bufferWallPairs = new List<(Polyline, WallData)>();
            List<WallData> validWalls = new List<WallData>();

            for (int i = 0; i < geomData.ClosedCurve.Count; i++)
            {
                var wall = geomData.ClosedCurve[i];
                if (wall.Thickness <= 0 || !wall.Boundary.IsWall)
                {
                    ed.WriteMessage($"\n警告: 墙体 {i} 无效 (厚度: {wall.Thickness}, IsWall: {wall.Boundary.IsWall})\n");
                    continue;
                }
                Polyline qb = GeometryExtensions.CreateWallBuffer(wall.Edge, wall.Thickness);
                qb.LayerId = Bufferid1;
                bufferWallPairs.Add((qb, wall));
                validWalls.Add(wall);
            }

            int validCount = validWalls.Count;
            ed.WriteMessage($"有效墙体数量: {validCount}\n");
            if (validCount < 3)
            {
                ed.WriteMessage("有效墙体数量不足以形成封闭空间，跳过处理\n");
                return bufferWallPairs;
            }

            for (int i = 0; i < validCount; i++)
            {
                int nextIndex = (i + 1) % validCount;
                var wall1 = validWalls[i];
                var wall2 = validWalls[nextIndex];
                Polyline qb1 = bufferWallPairs[i].Buffer;
                Polyline qb2 = bufferWallPairs[nextIndex].Buffer;
                GeometryExtensions.HandleWallConnection(wall1.Edge, wall2.Edge, wall1.Thickness, wall2.Thickness, ref qb1, ref qb2);
                bufferWallPairs[i] = (qb1, wall1);
                bufferWallPairs[nextIndex] = (qb2, wall2);
                qb1.LayerId = Bufferid2;
                qb2.LayerId = Bufferid2;
            }

            return bufferWallPairs;
        }

        // 生成开放曲线的缓冲区
        private List<(Polyline Buffer, WallData Wall)> GenerateOpenCurveBuffers(GeometryData geomData)
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage("\n开始处理非封闭曲线\n");

            List<(Polyline Buffer, WallData Wall)> bufferWallPairs = new List<(Polyline, WallData)>();
            List<WallData> validWalls = new List<WallData>();
            var layerId = Bufferid4;

            List<WallData> wallsToProcess = geomData.SegmentType == WallSegmentType.SingleOpenCurve
                ? geomData.SingleOpenCurve
                : geomData.MultipleOpenCurves.SelectMany(x => x).ToList();

            for (int i = 0; i < wallsToProcess.Count; i++)
            {
                var wall = wallsToProcess[i];
                if (wall.Thickness <= 0 || !wall.Boundary.IsWall)
                {
                    ed.WriteMessage($"\n警告: 墙体 {i} 无效 (厚度: {wall.Thickness}, IsWall: {wall.Boundary.IsWall})\n");
                    continue;
                }
                Polyline qb = (i == 0 || i == wallsToProcess.Count - 1)
                    ? GeometryExtensions.HandleEndpoint(wall, i == 0, wall.EndType)
                    : GeometryExtensions.CreateWallBuffer(wall.Edge, wall.Thickness);
                qb.LayerId = layerId;
                bufferWallPairs.Add((qb, wall));
                validWalls.Add(wall);
            }

            int validCount = validWalls.Count;
            ed.WriteMessage($"有效墙体数量: {validCount}\n");
            if (validCount < 1) return bufferWallPairs;

            for (int i = 0; i < validCount - 1; i++)
            {
                var wall1 = validWalls[i];
                var wall2 = validWalls[i + 1];
                Polyline qb1 = bufferWallPairs[i].Buffer;
                Polyline qb2 = bufferWallPairs[i + 1].Buffer;
                GeometryExtensions.HandleWallConnection(wall1.Edge, wall2.Edge, wall1.Thickness, wall2.Thickness, ref qb1, ref qb2);
                bufferWallPairs[i] = (qb1, wall1);
                bufferWallPairs[i + 1] = (qb2, wall2);
                qb1.LayerId = Bufferid3;
                qb2.LayerId = Bufferid3;
            }
            if (validCount > 0) bufferWallPairs[validCount - 1].Buffer.LayerId = Bufferid3;

            return bufferWallPairs;
        }

        // 将墙体缓冲区转换为3D墙体
        private void ConvertWallsTo3D(
            List<(Polyline Buffer, WallData Wall)> bufferWallPairs,
            BlockTableRecord btr,
            Transaction tr,
            AnchorBolt AnchorBolt
        )
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;

            foreach (var (buffer, wall) in bufferWallPairs)
            {
                buffer.LayerId = Bufferid_Wall;
                btr.AppendEntity(buffer);
                tr.AddNewlyCreatedDBObject(buffer, true);
                ed.WriteMessage($"\n墙体缓冲区输入完成，顶点数: {buffer.NumberOfVertices}, 图层: Bufferid_Wall\n");

                double innerElevation = wall.InnerElevation;
                double outerElevation = wall.OuterElevation;
                ExtrudeWallRegion(buffer, innerElevation, outerElevation, btr, tr, Bufferid_Wall);
                ed.WriteMessage($"\n3D墙体生成，墙高: {outerElevation - innerElevation}, 图层: Bufferid_Wall\n");

                if (AnchorBolt != null)
                {
                    ed.WriteMessage($"\n检测到 AnchorBolt 类型: {AnchorBolt}, 当前未实现具体逻辑\n");
                }
            }
        }

        // 生成墙体下的筏板
        private void GenerateWallRaft(
            List<(Polyline Buffer, WallData Wall)> bufferWallPairs,
            BlockTableRecord btr,
            Transaction tr,
            double raftThickness
        )
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;

            foreach (var (buffer, wall) in bufferWallPairs)
            {
                Polyline raftRegion = buffer.Clone() as Polyline;
                double innerElevation = wall.InnerElevation;
                double raftBottomElevation = innerElevation - raftThickness;
                ExtrudeWallRegion(raftRegion, raftBottomElevation, innerElevation, btr, tr, Bufferid_Wall);
                ed.WriteMessage($"\n筏板生成，厚度: {raftThickness} (从 {raftBottomElevation} 到 {innerElevation}), 图层: Bufferid_Wall\n");
            }
        }

        // 生成底板
        private void GenerateBasePlate(GeometryData geomData, BlockTableRecord btr, Transaction tr, double raftThickness)
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;

            if (geomData.Polygon == null || !geomData.Polygon.Closed)
            {
                ed.WriteMessage("\n警告: 底板 Polygon 未定义或未闭合，跳过底板生成\n");
                return;
            }

            Polyline baseRegion = geomData.Polygon.Clone() as Polyline;
            baseRegion.LayerId = Bufferid_Base;
            btr.AppendEntity(baseRegion);
            tr.AddNewlyCreatedDBObject(baseRegion, true);
            ed.WriteMessage($"\n底板缓冲区输入完成，顶点数: {baseRegion.NumberOfVertices}, 图层: Bufferid_Base\n");

            double baseElevation = (geomData.Walls != null && geomData.Walls.Count > 0)
                ? geomData.Walls[0].InnerElevation
                : 0.0;
            double baseBottomElevation;

            if (baseElevation <= 0)
            {
                baseBottomElevation = baseElevation - raftThickness;
                ExtrudeWallRegion(baseRegion, baseBottomElevation, baseElevation, btr, tr, Bufferid_Base);
                ed.WriteMessage($"\n3D底板生成，厚度: {raftThickness} (从 {baseBottomElevation} 到 {baseElevation}), 图层: Bufferid_Base\n");
            }
            else
            {
                baseBottomElevation = -raftThickness;
                ExtrudeWallRegion(baseRegion, baseBottomElevation, baseElevation, btr, tr, Bufferid_Base);
                ed.WriteMessage($"\n3D底板生成，总厚度: {baseElevation + raftThickness} (从 {baseBottomElevation} 到 {baseElevation}), 图层: Bufferid_Base\n");
            }

            // 生成螺栓孔（仅在底板上）
           // GenerateBoltHoles(geomData, btr, tr, new AnchorBolt("Default", 20.0, 300.0)); // 示例默认值，可根据需要调整
        }

        // 拉伸区域生成3D模型
        private void ExtrudeWallRegion(Polyline region, double innerElevation, double outerElevation, BlockTableRecord btr, Transaction tr, ObjectId layerId)
        {
            if (!region.Closed)
            {
                throw new InvalidOperationException("区域未闭合，无法拉伸。");
            }

            double height = outerElevation - innerElevation;
            if (Math.Abs(height) < Tolerance.Global.EqualPoint)
            {
                throw new InvalidOperationException($"拉伸高度无效: InnerElevation={innerElevation}, OuterElevation={outerElevation}");
            }

            region.Elevation = innerElevation;
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage($"\n拉伸区域: Vertices={region.NumberOfVertices}, Closed={region.Closed}, Height={height}, InnerElevation={innerElevation}, OuterElevation={outerElevation}\n");

            using (Solid3d solid = new Solid3d())
            {
                solid.CreateExtrudedSolid(region, new Vector3d(0, 0, height), new SweepOptions());
                solid.LayerId = layerId;
                btr.AppendEntity(solid);
                tr.AddNewlyCreatedDBObject(solid, true);
            }
        }
    }

  


}