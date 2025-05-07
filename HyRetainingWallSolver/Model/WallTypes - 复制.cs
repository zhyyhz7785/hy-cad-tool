using System.Collections.Generic;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.DatabaseServices;
using Newtonsoft.Json;
using System;

namespace HyRetainingWallSolver.Models
{
    /// <summary>
    /// 枚举类型，表示边界的约束方式。
    /// </summary>
    public enum BoundaryFixity
    {
        Fixed,   // 固定端约束：约束平动和转动
        Hinged,  // 简支端约束：约束平动，自由转动
        Free     // 自由端：无约束
    }

    /// <summary>
    /// 表示 AutoCAD 中通过 ExtensionDictionary 存储的墙边界信息。
    /// 包含边界起止坐标、边界类型。
    /// 用于与 CAD 图形同步交互。
    /// </summary>
    public class WallBoundary
    {
        public double StartX { get; set; }
        public double StartY { get; set; }
        public double EndX { get; set; }
        public double EndY { get; set; }

        public BoundaryFixity Fixity { get; set; }

        [JsonIgnore]
        public Point3d Start => new Point3d(StartX, StartY, 0);

        [JsonIgnore]
        public Point3d End => new Point3d(EndX, EndY, 0);

        public WallBoundary() { }

        public WallBoundary(Point3d start, Point3d end, BoundaryFixity fixity)
        {
            StartX = start.X;
            StartY = start.Y;
            EndX = end.X;
            EndY = end.Y;
            Fixity = fixity;
        }
    }

    /// <summary>
    /// 表示从 AutoCAD 多段线提取的墙体几何信息与边界条件定义。
    /// 用于构建 FEM 输入模型的原始图形数据来源。
    /// </summary>
    public class WallGeometryInput
    {
        public ObjectId PolylineId { get; set; }
        public List<WallBoundary> Boundaries { get; set; } = new List<WallBoundary>();
    }

    /// <summary>
    /// 表示墙体边界上的 FEM 约束条件输入。
    /// 与 WallBoundary 对应，但用于结构计算模块。
    /// </summary>
    public class BoundaryConditionInput
    {
        public Point3d Start { get; set; }
        public Point3d End { get; set; }
        public BoundaryFixity Fixity { get; set; }

        public BoundaryConditionInput(Point3d start, Point3d end, BoundaryFixity fixity)
        {
            Start = start;
            End = end;
            Fixity = fixity;
        }
    }

    /// <summary>
    /// 表示 FEM 板单元建模所需输入参数，包括板厚、材料参数与边界约束。
    /// 用于构建刚度矩阵与有限元网格建模。
    /// </summary>
    public class WallPanelInput
    {
        public ObjectId PolylineId { get; set; }
        public List<BoundaryConditionInput> Edges { get; set; } = new List<BoundaryConditionInput>();

        /// <summary>
        /// 板厚，单位 mm
        /// </summary>
        public double Thickness { get; set; }

        /// <summary>
        /// 弹性模量，单位 kN/m²（通常为 3e7）
        /// </summary>
        public double ElasticModulus { get; set; }

        /// <summary>
        /// 泊松比（通常为 0.2）
        /// </summary>
        public double PoissonRatio { get; set; }
    }

    /// <summary>
    /// 从 WallGeometryInput 派生生成 FEM 输入模型 WallPanelInput。
    /// 用于后续 FEM 网格生成与计算模块。
    /// </summary>
    public static class WallInputFactory
    {
        public static WallPanelInput CreateFromGeometryInput(WallGeometryInput geometry, Polyline polyline)
        {
            var panel = new WallPanelInput
            {
                PolylineId = geometry.PolylineId,
                Thickness = 300,           // 默认厚度（单位 mm）
                ElasticModulus = 3e7,      // 默认混凝土弹性模量（kN/m²）
                PoissonRatio = 0.2         // 默认泊松比
            };

            int n = Math.Min(polyline.NumberOfVertices, geometry.Boundaries.Count);
            for (int i = 0; i < n; i++)
            {
                var start = polyline.GetPoint3dAt(i);
                var end = polyline.GetPoint3dAt((i + 1) % polyline.NumberOfVertices);
                var fixity = geometry.Boundaries[i].Fixity;
                panel.Edges.Add(new BoundaryConditionInput(start, end, fixity));
            }

            return panel;
        }
    }

    public class QuadElement
    {
        public int Id { get; set; }
        public Point3d[] Nodes { get; set; } = new Point3d[4];
    }
    public static class WallMeshGenerator
    {
        public static List<QuadElement> GenerateMeshFromPolyline(WallPanelInput input, Polyline polyline, int nx, int ny)
        {
            if (polyline.NumberOfVertices != 4)
                throw new ArgumentException("仅支持四边形多段线用于矩形网格划分。");

            // 明确按点顺序取出角点：默认 polyline 为逆时针
            Point3d p0 = polyline.GetPoint3dAt(0); // 左下
            Point3d p1 = polyline.GetPoint3dAt(1); // 右下
            Point3d p2 = polyline.GetPoint3dAt(2); // 右上
            Point3d p3 = polyline.GetPoint3dAt(3); // 左上

            var quads = new List<QuadElement>();
            int id = 0;

            for (int i = 0; i < nx; i++)
            {
                double sx0 = (double)i / nx;
                double sx1 = (double)(i + 1) / nx;

                for (int j = 0; j < ny; j++)
                {
                    double sy0 = (double)j / ny;
                    double sy1 = (double)(j + 1) / ny;

                    Point3d q0 = Interpolate(p0, p1, p2, p3, sx0, sy0);
                    Point3d q1 = Interpolate(p0, p1, p2, p3, sx1, sy0);
                    Point3d q2 = Interpolate(p0, p1, p2, p3, sx1, sy1);
                    Point3d q3 = Interpolate(p0, p1, p2, p3, sx0, sy1);

                    quads.Add(new QuadElement
                    {
                        Id = id++,
                        Nodes = new[] { q0, q1, q2, q3 }
                    });
                }
            }

            return quads;
        }

        private static Point3d Interpolate(Point3d p00, Point3d p10, Point3d p11, Point3d p01, double sx, double sy)
        {
            double x = (1 - sx) * (1 - sy) * p00.X + sx * (1 - sy) * p10.X + sx * sy * p11.X + (1 - sx) * sy * p01.X;
            double y = (1 - sx) * (1 - sy) * p00.Y + sx * (1 - sy) * p10.Y + sx * sy * p11.Y + (1 - sx) * sy * p01.Y;
            return new Point3d(x, y, 0);
        }


      
    }

}
