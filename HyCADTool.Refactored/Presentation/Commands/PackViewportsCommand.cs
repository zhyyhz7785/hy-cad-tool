using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Domain.ValueObjects.Configuration.User;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Configuration;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services;
using System.Collections.Generic;
using System.Linq;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 将布局中的视口打包排布到指定图框内（HY_PackViewports）
    /// </summary>
    public class PackViewportsCommand
    {
        private static string ViewportLayerName => UserLayerNameResolver.Get(LayerSemanticIds.PublicViewport, LayerBuiltinDefaults.PublicViewport);

        #region 内部类型

        private class VpInfo
        {
            public ObjectId Id;
            public double Width, Height;
            public Point2d NewPos;
            public bool Placed;
        }

        private class Slot
        {
            public double X, Y, W, H;
            public double Area => W * H;
        }

        private class Packer
        {
            private readonly List<Slot> _slots;

            public Packer(double w, double h)
            {
                _slots = new List<Slot> { new Slot { X = 0, Y = 0, W = w, H = h } };
            }

            public bool TryPack(double w, double h, out Point2d pos)
            {
                pos = default;
                var ordered = _slots.OrderBy(s => s.Y).ThenBy(s => s.X).ToList();
                foreach (var s in ordered)
                {
                    if (s.W >= w && s.H >= h)
                    {
                        pos = new Point2d(s.X, s.Y);
                        _slots.Remove(s);
                        if (s.W > w)
                            _slots.Add(new Slot { X = s.X + w, Y = s.Y, W = s.W - w, H = s.H });
                        if (s.H > h)
                            _slots.Add(new Slot { X = s.X, Y = s.Y + h, W = w, H = s.H - h });
                        _slots.RemoveAll(sl => sl.W < 0.1 || sl.H < 0.1);
                        return true;
                    }
                }
                return false;
            }

            public List<Slot> GetSlots() => _slots.Where(s => s.Area > 100).ToList();
        }

        #endregion

        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            try
            {
                // 选择图框
                var frameOpts = new PromptKeywordOptions("\n请选择图框类型:") { AllowNone = false };
                foreach (var k in new[] { "A0", "A1", "A2", "A3", "A4" })
                    frameOpts.Keywords.Add(k);
                var frameRes = ed.GetKeywords(frameOpts);
                if (frameRes.Status != PromptStatus.OK) return;

                var tb = TitleBlockFactory.Create(frameRes.StringResult);
                double fw = tb.Width - tb.LeftMargin - tb.RightMargin;
                double fh = tb.Height - tb.TopMargin - tb.BottomMargin;

                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    // 收集视口
                    var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);
                    var vpList = new List<VpInfo>();

                    foreach (ObjectId id in space)
                    {
                        if (id.ObjectClass.Name != "AcDbViewport") continue;
                        var vp = tr.GetObject(id, OpenMode.ForRead) as Viewport;
                        if (vp == null || vp.IsErased || vp.Number == 1) continue;
                        if (vp.Layer != ViewportLayerName) continue;

                        vpList.Add(new VpInfo
                        {
                            Id = id,
                            Width = vp.Width,
                            Height = vp.Height
                        });
                    }

                    if (vpList.Count == 0)
                    {
                        ed.WriteMessage("\n未找到视口");
                        return;
                    }

                    // 按面积降序排列
                    vpList = vpList.OrderByDescending(v => v.Width * v.Height).ToList();

                    var packer = new Packer(fw, fh);
                    int placed = 0;

                    foreach (var vi in vpList)
                    {
                        if (packer.TryPack(vi.Width, vi.Height, out var pos))
                        {
                            vi.NewPos = pos;
                            vi.Placed = true;
                            placed++;
                        }
                    }

                    // 应用位置
                    foreach (var vi in vpList.Where(v => v.Placed))
                    {
                        var vp = tr.GetObject(vi.Id, OpenMode.ForWrite) as Viewport;
                        if (vp == null) continue;
                        vp.CenterPoint = new Point3d(
                            tb.LeftMargin + vi.NewPos.X + vi.Width / 2,
                            tb.BottomMargin + vi.NewPos.Y + vi.Height / 2, 0);
                    }

                    tr.Commit();
                    ed.WriteMessage($"\n放置 {placed}/{vpList.Count} 个视口");
                    if (placed < vpList.Count)
                        ed.WriteMessage($"\n{vpList.Count - placed} 个视口无法放置（空间不足）");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n排布失败: {ex.Message}");
            }
        }
    }
}
