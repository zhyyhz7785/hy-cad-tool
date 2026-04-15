using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services;
using System.Linq;
using System.Threading.Tasks;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 标注文字防重叠命令 (ddaa)
    /// 选择一组 RotatedDimension，检测并解决文字重叠
    /// </summary>
    public class DimensionAlignCommand : Base.ICommand
    {
        public string Name => "DimensionAlign";

        public Task<bool> ExecuteAsync()
        {
            Execute();
            return Task.FromResult(true);
        }

        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                var filter = new SelectionFilter(new[]
                {
                    new TypedValue((int)DxfCode.Start, "DIMENSION")
                });

                var opts = new PromptSelectionOptions
                {
                    MessageForAdding = "\n选择需要对齐的标注（框选或逐个）:"
                };

                var selResult = ed.GetSelection(opts, filter);
                if (selResult.Status != PromptStatus.OK || selResult.Value.Count == 0)
                {
                    ed.WriteMessage("\n未选择标注。");
                    return;
                }

                var dimIds = selResult.Value.GetObjectIds()
                    .Where(id => !id.IsNull)
                    .ToArray();

                if (dimIds.Length == 0)
                {
                    ed.WriteMessage("\n选择集中无有效标注。");
                    return;
                }

                var service = new DimensionTextAlignService();
                int count = service.AlignDimensionTexts(dimIds);

                ed.WriteMessage(count > 0
                    ? $"\n已调整 {count} 个标注文字位置。"
                    : "\n未检测到文字重叠，无需调整。");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误：{ex.Message}");
            }
        }
    }
}
