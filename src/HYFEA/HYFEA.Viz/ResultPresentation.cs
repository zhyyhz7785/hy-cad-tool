using System;
using System.IO;
using HYFEA.Core.Model;
using HYFEA.Core.Results;

namespace HYFEA.Viz;

/// <summary>
/// 求解成功后的网格 / JSON / VTU，供后台线程构建、UI 只绑定。
/// </summary>
public sealed class ResultPresentation
{
    public ResultPresentation(ResultMesh mesh, string meshJson, string vtuPath)
    {
        Mesh = mesh ?? throw new ArgumentNullException(nameof(mesh));
        MeshJson = meshJson ?? throw new ArgumentNullException(nameof(meshJson));
        VtuPath = vtuPath ?? throw new ArgumentNullException(nameof(vtuPath));
    }

    public ResultMesh Mesh { get; }

    public string MeshJson { get; }

    public string VtuPath { get; }

    public static string DefaultVtuPath => Path.Combine(Path.GetTempPath(), "hyfea-last.vtu");

    public static ResultPresentation Build(FemProblem problem, FemResult result, string? vtuPath = null)
    {
        var mesh = BeamResultMeshBuilder.From(problem, result);
        var json = ResultMeshJson.ToJson(mesh);
        var path = string.IsNullOrWhiteSpace(vtuPath) ? DefaultVtuPath : vtuPath!;
        VtuWriter.Write(mesh, path);
        return new ResultPresentation(mesh, json, path);
    }
}
