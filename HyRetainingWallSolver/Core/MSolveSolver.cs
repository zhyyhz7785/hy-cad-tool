using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using MSolve.Edu.Analyzers;
using MSolve.Edu.FEM;
using MSolve.Edu.FEM.Elements;
using MSolve.Edu.FEM.Entities;
using MSolve.Edu.FEM.Material;
using MSolve.Edu.FEM.Mesh;
using MSolve.Edu.LinearAlgebra;
using MSolve.Edu.Solvers;
using System;
using System.Collections.Generic;
using System.Linq;
namespace RetainingWallCalculator
{
    public class HyCommands
    {
        [CommandMethod("CalculateRetainingWall")]
        public void CalculateRetainingWall()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            // 步骤 1：提取四边形 Polyline
            var (vertices, height, thickness) = ExtractPolyline(ed, db);
            if (vertices == null) return;
            // 步骤 2：创建 FEA 模型
            var model = CreateRetainingWallModel(height, thickness, vertices, ed);
            // 步骤 3：运行分析并输出结果
            var displacements = RunAnalysis(model, ed);
            // 步骤 4：绘制内力图和计算配筋/裂缝
            PostProcessResults(model, displacements, db, ed);
        }
        private (List<double[]>, double, double) ExtractPolyline(Editor ed, Database db)
        {
            var peo = new PromptEntityOptions("\n请选择一个四边形 Polyline：");
            peo.SetRejectMessage("\n必须选择 Polyline。");
            peo.AddAllowedClass(typeof(Polyline), true);
            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return (null, 0, 0);
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var polyline = (Polyline)tr.GetObject(per.ObjectId, OpenMode.ForRead);
                if (polyline.NumberOfVertices != 4 || !polyline.Closed)
                {
                    ed.WriteMessage("\n错误：必须是闭合的四边形 Polyline。");
                    return (null, 0, 0);
                }
                var vertices = new List<double[]>();
                double minX = double.MaxValue, maxX = double.MinValue;
                double minY = double.MaxValue, maxY = double.MinValue;
                for (int i = 0; i < polyline.NumberOfVertices; i++)
                {
                    var point = polyline.GetPoint3dAt(i);
                    vertices.Add(new[] { point.X, point.Y, point.Z });
                    minX = Math.Min(minX, point.X);
                    maxX = Math.Max(maxX, point.X);
                    minY = Math.Min(minY, point.Y);
                    maxY = Math.Max(maxY, point.Y);
                    ed.WriteMessage($"\n顶点 {i}：({point.X}, {point.Y}, {point.Z})");
                }
                double height = maxX - minX;
                double thickness = maxY - minY;
                if (height <= 0 || thickness <= 0)
                {
                    ed.WriteMessage("\n错误：高度或厚度无效。");
                    return (null, 0, 0);
                }
                tr.Commit();
                return (vertices, height, thickness);
            }
        }
        private Model CreateRetainingWallModel(double height, double thickness, List<double[]> vertices, Editor ed)
        {
            // 参数
            double youngModulus = 3E7; // Pa (混凝土)
            double poissonRatio = 0.2;
            double gamma = 18000.0; // N/m³ (土重度)
            double phiRad = 30.0 * Math.PI / 180.0; // 内摩擦角
            double K_a = (1 - Math.Sin(phiRad)) / (1 + Math.Sin(phiRad)); // 朗肯主动土压力系数
            int numElementsX = 20; // 沿高度
            int numElementsY = 1; // 沿厚度
            // 归一化坐标（映射到 X: 0 到 height，Y: 0 到 thickness）
            double minX = vertices.Min(v => v[0]);
            double minY = vertices.Min(v => v[1]);
            var normalizedVertices = vertices.Select(v => new[] { v[0] - minX, v[1] - minY, v[2] }).ToList();
            // 创建网格
            var meshGenerator = new UniformMeshGenerator2D(0, 0, height, thickness, numElementsX, numElementsY);
            (Node[] nodes, Element[] elements) = meshGenerator.CreateMesh();
            // 创建模型并添加节点
            var model = new Model();
            foreach (Node node in nodes) model.NodesDictionary.Add(node.ID, node);
            // 底部固定（X = height）
            var baseNodes = model.NodesDictionary.Values.Where(n => Math.Abs(n.X - height) < 1e-6).ToList();
            foreach (var node in baseNodes)
            {
                node.Constraints.AddRange(new[] { DOFType.X, DOFType.Y });
            }
            // 施加土压力（Y = 0，背面）
            var backFaceNodes = model.NodesDictionary.Values.Where(n => Math.Abs(n.Y) < 1e-6).ToList();
            double dX = height / numElementsX;
            foreach (var node in backFaceNodes)
            {
                double X_i = node.X;
                double sigma_h = K_a * gamma * X_i; // Pa
                double w_i = sigma_h * thickness; // N/m
                double F_Y = -w_i * dX; // N
                model.Loads.Add(new Load { Amount = F_Y, Node = node, DOF = DOFType.Y });
            }
            // 创建 Quad4 元素和材料
            var material = new ElasticMaterial2D(StressState2D.PlaneStress)
            {
                YoungModulus = youngModulus,
                PoissonRatio = poissonRatio
            };
            foreach (Element element in elements)
            {
                element.ElementType = new Quad4(material) { Thickness = thickness };
                model.ElementsDictionary.Add(element.ID, element);
            }
            model.ConnectDataStructures();
            return model;
        }
        private Vector RunAnalysis(Model model, Editor ed)
        {
            var linearSystem = new SkylineLinearSystem(model.Forces);
            var solver = new SolverSkyline(linearSystem);
            var provider = new ProblemStructural(model);
            var childAnalyzer = new LinearAnalyzer(solver);
            var parentAnalyzer = new StaticAnalyzer(provider, childAnalyzer, linearSystem);
            parentAnalyzer.BuildMatrices();
            parentAnalyzer.Initialize();
            parentAnalyzer.Solve();
            var displacements = linearSystem.Solution;
            foreach (var node in model.NodesDictionary.Values)
            {
                int idx = node.ID * 2; // 假设每个节点 2 个自由度
                if (idx < displacements.Length)
                {
                    ed.WriteMessage($"\n节点 {node.ID} 位移：X={displacements[idx]:F4}, Y={displacements[idx + 1]:F4}");
                }
            }
            return displacements;
        }
        private void PostProcessResults(Model model, Vector displacements, Database db, Editor ed)
        {
            // 绘制内力图（占位：标注最大位移）
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                double maxDisp = displacements.Data.Max(d => Math.Abs(d));
                var topNode = model.NodesDictionary.Values.FirstOrDefault(n => Math.Abs(n.X) < 1e-6 && Math.Abs(n.Y) < 1e-6);
                if (topNode != null)
                {
                    var center = new Point3d(topNode.X, topNode.Y, topNode.Z);
                    var text = new DBText
                    {
                        Position = center,
                        Height = 0.1 * maxDisp,
                        TextString = $"最大位移={maxDisp:F4} m"
                    };
                    btr.AppendEntity(text);
                    tr.AddNewlyCreatedDBObject(text, true);
                }
                tr.Commit();
            }
            // 计算配筋和裂缝（占位：假设力矩）
            double moment = 10.0; // kN·m（需从分析结果提取）
            double effectiveHeight = 0.27; // 有效高度 (m)，原变量名 d 改为 effectiveHeight
            double fck = 20.0; // 混凝土抗压强度 (MPa)
            double fy = 400.0; // 钢筋抗拉强度 (MPa)
            double b = 1.0; // 宽度 (m)
            double As = moment * 1e3 / (0.9 * fy * effectiveHeight); // 配筋面积 (mm²)
            ed.WriteMessage($"\n配筋面积：{As:F2} mm²");
            double sigma_sk = fy;
            double Es = 2.0e5; // 钢筋弹性模量 (MPa)
            double w_max = 0.3 * 1.0 * sigma_sk / Es; // 裂缝宽度 (mm)
            ed.WriteMessage($"\n最大裂缝宽度：{w_max:F3} mm");
        }
    }
}