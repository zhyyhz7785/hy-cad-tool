using Autodesk.AutoCAD.Geometry;
using System.Collections.Generic;
namespace HyCADTool.HelpClass.DCEL
{
    public class Vertex
    {
        public Point3d Position { get; set; } // 顶点的位置
        public List<HalfEdge> OutgoingHalfedges { get; set; } // 从该顶点出发的半边列表
        public Vertex(Point3d position)
        {
            Position = position;
            OutgoingHalfedges = new List<HalfEdge>();
        }
    }
    public class HalfEdge
    {
        public Vertex StartVertex { get; set; } // 半边的起始顶点
        public HalfEdge Twin { get; set; } // 半边的孪生边（与之相反的边）
        public HalfEdge Next { get; set; } // 下一条半边
        public HalfEdge Prev { get; set; } // 前一条半边
        public Face IncidentFace { get; set; } // 半边所属的面
                                               // 表示半边是否已初始化并属于某个面的新属性
        public bool IsInitialized { get; set; }
        public HalfEdge()
        {
            StartVertex = null;
            Twin = null;
            Next = null;
            Prev = null;
            IncidentFace = null;
            IsInitialized = false; // 初始状态为未初始化
        }
        public Vector3d Vector()
        {
            return Twin.StartVertex.Position - StartVertex.Position;
        }
    }
    //public class Face
    //{
    //    public HalfEdge OuterHalfEdge { get; set; } // 面的外边界的半边
    //    public List<HalfEdge> InnerComponents { get; set; } // 面的内部组成部分（洞）
    //    public List<HalfEdge> OuterContour { get; set; } // 面的外轮射的半边列表
    //    public Face()
    //    {
    //        OuterHalfEdge = null;
    //        InnerComponents = new List<HalfEdge>();
    //        OuterContour = new List<HalfEdge>();
    //    }
    //}
    public class Face
    {
        public List<HalfEdge> Components { get; set; } // 面的内部组成部分（洞）
        public Face()
        {
            Components = new List<HalfEdge>();
        }
    }
    public class DCEL
    {
        public List<Vertex> Vertices { get; private set; } // 顶点列表
        public List<HalfEdge> HalfEdges { get; private set; } // 半边列表
        public List<Face> OuterFaces { get; private set; } // 半边列表
        public List<Face> InterFaces { get; private set; } // 半边列表
        public List<Face> Faces { get; private set; } // 面列表
        public DCEL()
        {
            Vertices = new List<Vertex>();
            HalfEdges = new List<HalfEdge>();
            OuterFaces = new List<Face>();
            InterFaces = new List<Face>();
            Faces = new List<Face>();
        }
        // 添加一个顶点
        public Vertex AddVertex(Point3d position)
        {
            var vertex = new Vertex(position);
            Vertices.Add(vertex);
            return vertex;
        }
        public (HalfEdge, HalfEdge) AddEdgePair(Vertex origin, Vertex destination)
        {
            // 创建半边 1 和半边 2
            var he1 = new HalfEdge { StartVertex = origin };
            var he2 = new HalfEdge { StartVertex = destination };
            // 设置双半边关系
            he1.Twin = he2;
            he2.Twin = he1;
            // 批量添加半边和更新相关的出射半边列表
            origin.OutgoingHalfedges.Add(he1);
            destination.OutgoingHalfedges.Add(he2);
            // 一次性将半边添加到集合中
            HalfEdges.Add(he1);
            HalfEdges.Add(he2);
            return (he1, he2);
        }
        // 创建一个面
        public Face CreateFace(List<HalfEdge> halfEdges)
        {
            var face = new Face { };
            int edgeCount = halfEdges.Count;
            for (int i = 0; i < edgeCount; i++)
            {
                halfEdges[i].IncidentFace = face;
                halfEdges[i].IsInitialized = true; // 设置为已初始化
                halfEdges[i].Next = halfEdges[(i + 1) % edgeCount];
                halfEdges[i].Prev = halfEdges[(i - 1 + edgeCount) % edgeCount];
                face.Components.Add(halfEdges[i]);
            }
            Faces.Add(face);
            return face;
        }
    }
}