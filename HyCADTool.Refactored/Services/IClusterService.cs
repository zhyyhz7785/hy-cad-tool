using HyCADTool.Models.Cluster;
using System.Collections.Generic;
using Autodesk.AutoCAD.Geometry;

namespace HyCADTool.Services
{
    public interface IClusterService
    {
        List<ClusterResult> PerformClustering(List<Point3d> points, ClusterConfig config);
        void AnnotateClusters(List<ClusterResult> results);
    }
}
