using HyCADTool.Domain.Ports.Road;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.App.Bootstrap;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

using HyCADTool.Features.Road.PlanAlignment.Services;
using HyCADTool.Features.Road.CrossSection.Domain;
using HyCADTool.Features.Road.PlanAlignment.Commands;
using HyCADTool.Features.Road.CrossSection.Commands;
namespace HyCADTool.Features.Road
{
    /// <summary>
    /// P0 预留命令：glTF 三维导出入口。
    ///
    /// v1 阶段：
    /// - DI 容器注入的是 <see cref="NotImplementedThreeDExportPort"/>；
    /// - 调用后返回失败 + 提示"v2（P7）启用"，避免用户误以为功能缺失。
    ///
    /// v2 阶段：
    /// - 在 Autofac 中切换到真实的 GltfExportPort（Infrastructure 实现）；
    /// - 命令逻辑保持不变，实现透明升级。
    /// </summary>
    public sealed class Road3dExportGltfCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();
            var port = ServiceLocator.Resolve<IThreeDExportPort>();
            var design = registry.GetOrCreate(doc.Name);

            var options = new ThreeDExportOptions
            {
                Format = ThreeDExportFormat.Gltf,
                OutputPath = System.IO.Path.ChangeExtension(doc.Name ?? "road", ".glb"),
                TranslateToOrigin = true,
                IncludeMaterials = true
            };

            var result = port.Export(design, options);
            if (result.Success)
                doc.Editor.WriteMessage($"\n[道路] 已导出 glTF：{result.OutputPath}");
            else
                doc.Editor.WriteMessage($"\n[道路] 3D 导出未启用：{result.ErrorMessage}");
        }
    }
}
