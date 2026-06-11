using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Features.Reinforcement.Domain.Enums;
using HyCADTool.Features.DCEL.Domain.Enums;
using HyCADTool.Shell.Contracts;
using HyCADTool.Features.BaseRein.Domain;
using HyCADTool.Shared.AutoCAD.Configuration;
using HyCADTool.Shared.AutoCAD.Extensions;
using HyCADTool.Shared.AutoCAD.Services;
using HyCADTool.Shell.Configuration.User;

using System;
using System.Collections.Generic;
using System.Linq;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.BaseRein.Services
{
    /// <summary>
    /// 基础配筋服务
    /// 移植自旧项目 BaseRein 静态类
    /// </summary>
    public class BaseReinforcementService : IBaseReinforcementService
    {
        private readonly IStyleService _styleService;
        private BaseReinforcementConfig _config;

        private const string LayerTargetText = "筏板板元配筋标注";

        private static string LayerReinforcementOutline =>
            UserLayerNameResolver.Get(LayerSemanticIds.RaftOutline, LayerBuiltinDefaults.RaftOutline);
        private static string LayerAdjustedOutline =>
            UserLayerNameResolver.Get(LayerSemanticIds.RaftOutlineAdjust, LayerBuiltinDefaults.RaftOutlineAdjust);

        // 步骤间共享数据（Step4 产出 → Step5 消费）
        private Dictionary<ObjectId, List<double>> _reinforcementAreaData;
        private List<ObjectId> _pillarPierIds;
        private string _cachedDatabaseKey;

        public BaseReinforcementService(IStyleService styleService)
        {
            _styleService = styleService;
            _config = BaseReinforcementConfig.CreateDefault();
        }

        #region 样式设置

        public void ApplyStyles(double scale)
        {
            // 不再做任何操作 — 图层和样式已在 PluginInitializer 启动时一次性创建
            // 面板参数变更时通过 SettingsPanelViewModel.EnsureStylesApplied() 按需更新
        }

        #endregion

        #region 步骤 1：整理底图

        /// <summary>
        /// 用户框选对象，保留指定图层的实体，删除其余实体
        /// </summary>
        public void OptimizeBasemap()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var selOpts = new PromptSelectionOptions { MessageForAdding = "\n请选择图形对象: " };
            var selRes = ed.GetSelection(selOpts);
            if (selRes.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n用户取消了选择。");
                return;
            }

            string[] targetLayers = { "砼墙", "柱", "板元", "筏板板元配筋标注", "筏板" };
            int deletedCount = 0;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (SelectedObject selObj in selRes.Value)
                {
                    if (selObj == null) continue;
                    var ent = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Entity;
                    if (ent == null) continue;

                    if (!targetLayers.Contains(ent.Layer))
                    {
                        ent.UpgradeOpen();
                        ent.Erase();
                        deletedCount++;
                    }
                }
                tr.Commit();
            }

            ed.WriteMessage($"\n[BaseRein] 整理底图完成，删除 {deletedCount} 个非目标图层对象。");
        }

        #endregion

        #region 步骤 2：选择并删除不需要的文字

        /// <summary>
        /// 根据过滤条件选择匹配的文字对象，高亮显示供用户手动删除
        /// </summary>
        public void SelectAndDeleteUnusedText(List<string> fixedValues)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var db = doc.Database;

            if (fixedValues == null || fixedValues.Count == 0)
            {
                ed.WriteMessage("\n过滤值为空，操作取消。");
                return;
            }

            var selOpts = new PromptSelectionOptions { MessageForAdding = "\n请选择图形对象: " };
            var selRes = ed.GetSelection(selOpts);
            if (selRes.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n用户取消了选择。");
                return;
            }

            var matchingIds = new List<ObjectId>();

            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (SelectedObject selObj in selRes.Value)
                {
                    if (selObj == null) continue;
                    var dbObj = tr.GetObject(selObj.ObjectId, OpenMode.ForRead);

                    if (dbObj is DBText dbText && dbText.Layer == LayerTargetText
                        && MatchesCondition(dbText.TextString, fixedValues))
                    {
                        matchingIds.Add(selObj.ObjectId);
                    }
                    else if (dbObj is MText mText && mText.Layer == LayerTargetText
                        && MatchesCondition(mText.Text, fixedValues))
                    {
                        matchingIds.Add(selObj.ObjectId);
                    }
                }
                tr.Commit();
            }

            if (matchingIds.Count > 0)
            {
                ed.SetImpliedSelection(matchingIds.ToArray());
                ed.WriteMessage($"\n[BaseRein] 共找到 {matchingIds.Count} 个匹配的文字对象（已高亮），请手动删除。");
            }
            else
            {
                ed.WriteMessage("\n[BaseRein] 未找到匹配的文字对象。");
            }
        }

        /// <summary>
        /// 判断文字内容是否匹配过滤条件
        /// 支持格式：精确值(12)、范围(5x12)、大于(>5)、小于(&lt;5)、大于等于(>=5)、小于等于(&lt;=5)
        /// </summary>
        private static bool MatchesCondition(string text, List<string> conditions)
        {
            if (!double.TryParse(text, out double textValue))
                return false;

            foreach (var condition in conditions)
            {
                // 范围格式：5x12（表示 5 < value < 12）
                int xIndex = condition.IndexOf('x');
                if (xIndex != -1)
                {
                    string lowerStr = condition.Substring(0, xIndex).Trim();
                    string upperStr = condition.Substring(xIndex + 1).Trim();
                    if (double.TryParse(lowerStr, out double lower) && double.TryParse(upperStr, out double upper))
                    {
                        if (textValue > lower && textValue < upper) return true;
                    }
                    continue;
                }

                // >= 和 <=
                if (condition.StartsWith(">=") && double.TryParse(condition.Substring(2), out double geVal))
                {
                    if (textValue >= geVal) return true;
                }
                else if (condition.StartsWith("<=") && double.TryParse(condition.Substring(2), out double leVal))
                {
                    if (textValue <= leVal) return true;
                }
                // > 和 <
                else if (condition.StartsWith(">") && double.TryParse(condition.Substring(1), out double gtVal))
                {
                    if (textValue > gtVal) return true;
                }
                else if (condition.StartsWith("<") && double.TryParse(condition.Substring(1), out double ltVal))
                {
                    if (textValue < ltVal) return true;
                }
                // 精确值
                else if (double.TryParse(condition, out double exactVal))
                {
                    if (textValue == exactVal) return true;
                }
            }
            return false;
        }

        #endregion

        #region 步骤 4：生成配筋面积

        /// <summary>
        /// 步骤 4 总流程：选择有限元网格 → 生成包络线 → 空间分组 → 合并为配筋区域
        /// 对应旧代码 BR03 + BR04a + BR04b + BR04c
        /// </summary>
        public void GenerateReinforcementArea(BaseReinforcementConfig config)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            EnsureDataForCurrentDocument(db);
            if (!config.IsValid(out string error))
            {
                ed.WriteMessage($"\n[BaseRein] 参数无效：{error}");
                return;
            }

            _reinforcementAreaData = null;
            _cachedDatabaseKey = GetDatabaseKey(db);

            // 4.0 选择有限元网格（BR03）
            var selOpts = new PromptSelectionOptions { MessageForAdding = "\n请选择有限元网格中的对象: " };
            var selRes = ed.GetSelection(selOpts);
            if (selRes.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n用户取消了选择。");
                return;
            }

            using (var tr = db.TransactionManager.StartTransaction())
            {
                // 收集所有选中的对象
                var allObjects = new Dictionary<ObjectId, DBObject>();
                foreach (SelectedObject selObj in selRes.Value)
                {
                    if (selObj != null)
                        allObjects[selObj.ObjectId] = tr.GetObject(selObj.ObjectId, OpenMode.ForRead);
                }

                // 提取柱墩多段线（图层"柱"，线型 ByLayer）
                _pillarPierIds = allObjects.Values
                    .OfType<Polyline>()
                    .Where(p => p.Layer == "柱" && p.Linetype == "ByLayer")
                    .Select(p => p.ObjectId)
                    .ToList();

                var numericTexts = CollectNumericTexts(allObjects.Values);

                var textToPolyMap = new Dictionary<ObjectId, ObjectId>();
                foreach (var textHit in numericTexts)
                {
                    foreach (var kvp in allObjects)
                    {
                        if (kvp.Value is Polyline poly && IsPointInsidePolyline(poly, textHit.AlignmentPoint))
                        {
                            textToPolyMap[textHit.TextId] = kvp.Key;
                            break;
                        }
                    }
                }

                int deletedCount = 0;
                if (textToPolyMap.Count > 0)
                {
                    var usedPolyIds = new HashSet<ObjectId>(textToPolyMap.Values);
                    foreach (var kvp in allObjects)
                    {
                        if (!usedPolyIds.Contains(kvp.Key) && kvp.Value is Polyline poly && poly.Layer == "板元")
                        {
                            poly.UpgradeOpen();
                            poly.Erase();
                            deletedCount++;
                        }
                    }
                }
                else
                {
                    ed.WriteMessage("\n[BaseRein] 未匹配到数值文字，跳过板元删除。");
                }

                ed.WriteMessage($"\n[BaseRein] 选择网格完成：{textToPolyMap.Count} 组配筋数据，删除 {deletedCount} 个多余网格。");

                // 4.1 为每个多段线生成包络矩形（BR04a）
                // 图层已在 PluginInitializer 统一创建
                var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                // textId → 新包络 polyId
                var textToEnvelopeMap = new Dictionary<ObjectId, ObjectId>();
                foreach (var kvp in textToPolyMap)
                {
                    var oldPoly = tr.GetObject(kvp.Value, OpenMode.ForWrite) as Polyline;
                    if (oldPoly == null) continue;

                    var ext = oldPoly.GeometricExtents;
                    var envelope = CreateBoundingBoxPolyline(ext.MinPoint, ext.MaxPoint);
                    envelope.Layer = LayerReinforcementOutline;
                    btr.AppendEntity(envelope);
                    tr.AddNewlyCreatedDBObject(envelope, true);

                    textToEnvelopeMap[kvp.Key] = envelope.ObjectId;

                    oldPoly.Erase(); // 删除原始网格多段线
                }

                ed.WriteMessage($"\n[BaseRein] 包络线生成完成：{textToEnvelopeMap.Count} 个。");

                // 4.2 按空间邻近性分组（BR04b）
                // 收集文字位置用于分组
                var textPositions = new Dictionary<ObjectId, Point3d>();
                foreach (var textId in textToEnvelopeMap.Keys)
                {
                    var textObj = tr.GetObject(textId, OpenMode.ForRead);
                    if (textObj is DBText dbText)
                        textPositions[textId] = dbText.AlignmentPoint;
                    else if (textObj is MText mText)
                        textPositions[textId] = mText.Location;
                }

                var groups = GroupBySpatialProximity(textPositions, textToEnvelopeMap, config.ProximityThreshold);
                ed.WriteMessage($"\n[BaseRein] 空间分组完成：{groups.Count} 个配筋区域。");

                // 4.3 为每个分组创建合并包络多边形（BR04c）
                // 图层已在 PluginInitializer 统一创建
                _reinforcementAreaData = new Dictionary<ObjectId, List<double>>();

                foreach (var group in groups)
                {
                    double minX = double.MaxValue, minY = double.MaxValue;
                    double maxX = double.MinValue, maxY = double.MinValue;
                    var textValues = new List<double>();

                    foreach (var textId in group)
                    {
                        // 获取包络多段线范围
                        if (textToEnvelopeMap.TryGetValue(textId, out var envId))
                        {
                            var envPoly = tr.GetObject(envId, OpenMode.ForRead) as Polyline;
                            if (envPoly != null)
                            {
                                var p0 = envPoly.GetPoint2dAt(0);
                                var p2 = envPoly.GetPoint2dAt(2);
                                if (p0.X < minX) minX = p0.X;
                                if (p0.Y < minY) minY = p0.Y;
                                if (p2.X > maxX) maxX = p2.X;
                                if (p2.Y > maxY) maxY = p2.Y;
                            }
                        }

                        // 收集配筋数值
                        if (TryReadNumericText(tr.GetObject(textId, OpenMode.ForRead), out double val))
                            textValues.Add(val);
                    }

                    if (textValues.Count == 0) continue;

                    // 创建合并包络多边形
                    var mergedPoly = CreateBoundingBoxPolyline(
                        new Point3d(minX, minY, 0), new Point3d(maxX, maxY, 0));
                    mergedPoly.Layer = LayerAdjustedOutline;
                    btr.AppendEntity(mergedPoly);
                    tr.AddNewlyCreatedDBObject(mergedPoly, true);

                    textValues.Sort();
                    _reinforcementAreaData[mergedPoly.ObjectId] = textValues;
                }

                tr.Commit();
            }

            ed.WriteMessage($"\n[BaseRein] 步骤4完成：生成 {_reinforcementAreaData?.Count ?? 0} 个配筋区域。");
        }

        /// <summary>
        /// 检查点是否在闭合多段线内部（射线法）
        /// </summary>
        private static bool IsPointInsidePolyline(Polyline polyline, Point3d testPoint)
        {
            int n = polyline.NumberOfVertices;
            bool inside = false;
            var tp = new Point2d(testPoint.X, testPoint.Y);

            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                var pi = polyline.GetPoint2dAt(i);
                var pj = polyline.GetPoint2dAt(j);
                if (((pi.Y > tp.Y) != (pj.Y > tp.Y)) &&
                    (tp.X < (pj.X - pi.X) * (tp.Y - pi.Y) / (pj.Y - pi.Y) + pi.X))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        /// <summary>
        /// 创建矩形包络多段线
        /// </summary>
        private static Polyline CreateBoundingBoxPolyline(Point3d min, Point3d max)
        {
            var poly = new Polyline();
            poly.AddVertexAt(0, new Point2d(min.X, min.Y), 0, 0, 0);
            poly.AddVertexAt(1, new Point2d(max.X, min.Y), 0, 0, 0);
            poly.AddVertexAt(2, new Point2d(max.X, max.Y), 0, 0, 0);
            poly.AddVertexAt(3, new Point2d(min.X, max.Y), 0, 0, 0);
            poly.Closed = true;
            return poly;
        }

        /// <summary>
        /// 按空间邻近性分组：距离小于阈值的文字归为一组（BFS 连通分量）
        /// </summary>
        private static List<List<ObjectId>> GroupBySpatialProximity(
            Dictionary<ObjectId, Point3d> textPositions,
            Dictionary<ObjectId, ObjectId> textToEnvelopeMap,
            double threshold)
        {
            var groups = new List<List<ObjectId>>();
            var visited = new HashSet<ObjectId>();
            var allTextIds = textToEnvelopeMap.Keys.ToList();

            foreach (var textId in allTextIds)
            {
                if (visited.Contains(textId)) continue;

                var group = new List<ObjectId>();
                var queue = new Queue<ObjectId>();
                queue.Enqueue(textId);
                visited.Add(textId);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    group.Add(current);

                    if (!textPositions.TryGetValue(current, out var currentPos)) continue;

                    foreach (var other in allTextIds)
                    {
                        if (visited.Contains(other)) continue;
                        if (!textPositions.TryGetValue(other, out var otherPos)) continue;

                        if (currentPos.DistanceTo(otherPos) <= threshold)
                        {
                            visited.Add(other);
                            queue.Enqueue(other);
                        }
                    }
                }

                groups.Add(group);
            }

            return groups;
        }

        #endregion

        #region 步骤 5：绘制钢筋

        // 允许的钢筋直径
        private static readonly double[] AllowedDiameters = { 6, 8, 10, 12, 14, 16, 18, 20, 22, 25 };

        /// <summary>
        /// 步骤 5：根据配筋区域数据绘制钢筋
        /// 可选单方向（config.Direction）或全方向（dimAll=true）
        /// 对应旧代码 BR051 ReinforcementStepA + BR052 ReinforcementStepB / BR053 ReinforcementAll
        /// </summary>
        public void DrawReinforcement(BaseReinforcementConfig config, bool dimAll)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            EnsureDataForCurrentDocument(db);
            if (!config.IsValid(out string error))
            {
                ed.WriteMessage($"\n[BaseRein] 参数无效：{error}");
                return;
            }

            // 如果没有 Step4 的数据，让用户重新选择配筋区域
            if (_reinforcementAreaData == null || _reinforcementAreaData.Count == 0)
            {
                ed.WriteMessage("\n[BaseRein] 无配筋区域数据，请先执行步骤4，或选择已有配筋区域。");
                // 尝试从图中选择已有配筋区域
                _reinforcementAreaData = SelectExistingReinforcementAreas(ed, db);
                if (_reinforcementAreaData == null || _reinforcementAreaData.Count == 0)
                    return;
            }

            // 图层经 LayerCatalogFactory 注册，由 PluginInitializer 统一创建

            int rebarCount = 0;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                foreach (var kvp in _reinforcementAreaData)
                {
                    var polyline = tr.GetObject(kvp.Key, OpenMode.ForRead) as Polyline;
                    if (polyline == null || polyline.NumberOfVertices < 4) continue;

                    var textValues = kvp.Value;
                    if (textValues.Count == 0) continue;

                    double maxArea = textValues.Max() * 100 * config.ReinforceSafety;

                    // 计算已有通筋面积
                    double singleRebarArea = Math.PI * Math.Pow(config.RebarDiameter / 2, 2);
                    double existingArea = config.ExistingRebar ? (1000 / config.RebarSpacing * singleRebarArea) : 0;

                    // 找到合适的附加钢筋直径
                    double additionalDiameter = config.MinAdditionalDiameter;
                    foreach (var dia in AllowedDiameters)
                    {
                        additionalDiameter = Math.Max(dia, config.MinAdditionalDiameter);
                        double additionalSingleArea = Math.PI * Math.Pow(additionalDiameter / 2, 2);
                        double additionalArea = 1000 / config.AdditionalSpacing * additionalSingleArea;
                        if (additionalArea + existingArea > maxArea) break;
                    }

                    if (dimAll)
                    {
                        // 全方向：4个方向各画一根
                        DrawRebarForPolyline(tr, btr, polyline, additionalDiameter, config, RebarDirection.TopX);
                        DrawRebarForPolyline(tr, btr, polyline, additionalDiameter, config, RebarDirection.TopY);
                        DrawRebarForPolyline(tr, btr, polyline, additionalDiameter, config, RebarDirection.BottomX);
                        DrawRebarForPolyline(tr, btr, polyline, additionalDiameter, config, RebarDirection.BottomY);
                        rebarCount += 4;
                    }
                    else
                    {
                        DrawRebarForPolyline(tr, btr, polyline, additionalDiameter, config, config.Direction);
                        rebarCount++;
                    }
                }

                tr.Commit();
            }

            ed.WriteMessage($"\n[BaseRein] 步骤5完成：绘制 {rebarCount} 根钢筋。");
        }

        /// <summary>
        /// 为单个配筋区域绘制一个方向的钢筋
        /// </summary>
        private void DrawRebarForPolyline(Transaction tr, BlockTableRecord btr,
            Polyline polyline, double diameter, BaseReinforcementConfig config, RebarDirection direction)
        {
            var scale = config.Scale;
            var anchorLen = config.AddAnchorLength ? config.AnchorFactor * diameter : 0;
            var hookLen = config.HookLength * scale;
            var polyWidth = config.PolylineWidth * scale;
            var reinDist = config.ReinforceDistance * scale;

            var p0 = polyline.GetPoint3dAt(0); // 左下
            var p1 = polyline.GetPoint3dAt(1); // 右下
            var p2 = polyline.GetPoint3dAt(2); // 右上

            Polyline rebar;
            string rebarLayer, textLayer;
            Point3d textPos;
            double textRotation = 0;

            switch (direction)
            {
                case RebarDirection.TopX:
                {
                    double yCenter = (p0.Y + p2.Y) / 2 + reinDist;
                    rebar = CreateXRebar(p0.X - anchorLen, p1.X + anchorLen, yCenter, hookLen, polyWidth, isTop: true);
                    rebarLayer = ResolveRaftLayer(LayerSemanticIds.RaftSlabXTop);
                    textLayer = ResolveRaftLayer(LayerSemanticIds.RaftTextX);
                    var mid = new Point3d((p0.X + p1.X) / 2 + config.ReinforceTextDistanceX * scale,
                        yCenter + config.TextToLineDistance * scale, 0);
                    textPos = mid;
                    break;
                }
                case RebarDirection.BottomX:
                {
                    double yCenter = (p0.Y + p2.Y) / 2 - reinDist;
                    double w = hookLen / Math.Sqrt(2);
                    rebar = CreateXRebar(p0.X - anchorLen, p1.X + anchorLen, yCenter, w, polyWidth, isTop: false);
                    rebarLayer = ResolveRaftLayer(LayerSemanticIds.RaftSlabXBottom);
                    textLayer = ResolveRaftLayer(LayerSemanticIds.RaftTextX);
                    var mid = new Point3d((p0.X + p1.X) / 2 + config.ReinforceTextDistanceX * scale,
                        yCenter - hookLen + config.TextToLineDistance * scale, 0);
                    textPos = mid;
                    break;
                }
                case RebarDirection.TopY:
                {
                    double xCenter = (p0.X + p1.X) / 2 + reinDist;
                    rebar = CreateYRebar(p1.Y - anchorLen, p2.Y + anchorLen, xCenter, hookLen, polyWidth, isTop: true);
                    rebarLayer = ResolveRaftLayer(LayerSemanticIds.RaftSlabYTop);
                    textLayer = ResolveRaftLayer(LayerSemanticIds.RaftTextY);
                    var mid = new Point3d(xCenter - config.TextToLineDistance * scale,
                        (p1.Y + p2.Y) / 2 + config.ReinforceTextDistanceY * scale, 0);
                    textPos = mid;
                    textRotation = Math.PI / 2;
                    break;
                }
                case RebarDirection.BottomY:
                {
                    double xCenter = (p0.X + p1.X) / 2 - reinDist;
                    double w = hookLen / Math.Sqrt(2);
                    rebar = CreateYRebar(p1.Y - anchorLen, p2.Y + anchorLen, xCenter, w, polyWidth, isTop: false);
                    rebarLayer = ResolveRaftLayer(LayerSemanticIds.RaftSlabYBottom);
                    textLayer = ResolveRaftLayer(LayerSemanticIds.RaftTextY);
                    var mid = new Point3d(xCenter + hookLen - config.TextToLineDistance * scale,
                        (p1.Y + p2.Y) / 2 + config.ReinforceTextDistanceY * scale, 0);
                    textPos = mid;
                    textRotation = Math.PI / 2;
                    break;
                }
                default:
                    return;
            }

            rebar.Layer = rebarLayer;
            btr.AppendEntity(rebar);
            tr.AddNewlyCreatedDBObject(rebar, true);

            // 标注文字
            var label = new DBText
            {
                Position = textPos,
                Height = 3 * scale,
                WidthFactor = 0.7,
                TextString = $"\\u+e532{diameter}@{config.AdditionalSpacing}",
                Layer = textLayer,
                Rotation = textRotation,
                HorizontalMode = TextHorizontalMode.TextCenter,
                VerticalMode = TextVerticalMode.TextBase
            };
            label.AlignmentPoint = label.Position;
            btr.AppendEntity(label);
            tr.AddNewlyCreatedDBObject(label, true);
        }

        /// <summary>创建 X 向钢筋多段线（水平，带弯钩）</summary>
        private static Polyline CreateXRebar(double xLeft, double xRight, double y, double hookOrW, double polyWidth, bool isTop)
        {
            var rebar = new Polyline();
            if (isTop)
            {
                // 上皮：弯钩向下
                rebar.AddVertexAt(0, new Point2d(xLeft, y - hookOrW), 0, polyWidth, polyWidth);
                rebar.AddVertexAt(1, new Point2d(xLeft, y), 0, polyWidth, polyWidth);
                rebar.AddVertexAt(2, new Point2d(xRight, y), 0, polyWidth, polyWidth);
                rebar.AddVertexAt(3, new Point2d(xRight, y - hookOrW), 0, polyWidth, polyWidth);
            }
            else
            {
                // 下皮：45° 弯钩向上
                rebar.AddVertexAt(0, new Point2d(xLeft + hookOrW, y + hookOrW), 0, polyWidth, polyWidth);
                rebar.AddVertexAt(1, new Point2d(xLeft, y), 0, polyWidth, polyWidth);
                rebar.AddVertexAt(2, new Point2d(xRight, y), 0, polyWidth, polyWidth);
                rebar.AddVertexAt(3, new Point2d(xRight - hookOrW, y + hookOrW), 0, polyWidth, polyWidth);
            }
            return rebar;
        }

        /// <summary>创建 Y 向钢筋多段线（竖直，带弯钩）</summary>
        private static Polyline CreateYRebar(double yBottom, double yTop, double x, double hookOrW, double polyWidth, bool isTop)
        {
            var rebar = new Polyline();
            if (isTop)
            {
                // 上皮：弯钩向右
                rebar.AddVertexAt(0, new Point2d(x + hookOrW, yBottom), 0, polyWidth, polyWidth);
                rebar.AddVertexAt(1, new Point2d(x, yBottom), 0, polyWidth, polyWidth);
                rebar.AddVertexAt(2, new Point2d(x, yTop), 0, polyWidth, polyWidth);
                rebar.AddVertexAt(3, new Point2d(x + hookOrW, yTop), 0, polyWidth, polyWidth);
            }
            else
            {
                // 下皮：45° 弯钩向左
                rebar.AddVertexAt(0, new Point2d(x - hookOrW, yBottom + hookOrW), 0, polyWidth, polyWidth);
                rebar.AddVertexAt(1, new Point2d(x, yBottom), 0, polyWidth, polyWidth);
                rebar.AddVertexAt(2, new Point2d(x, yTop), 0, polyWidth, polyWidth);
                rebar.AddVertexAt(3, new Point2d(x - hookOrW, yTop - hookOrW), 0, polyWidth, polyWidth);
            }
            return rebar;
        }

        /// <summary>
        /// 从图中选择已有配筋区域（当无 Step4 数据时的后备方案）
        /// </summary>
        private Dictionary<ObjectId, List<double>> SelectExistingReinforcementAreas(Editor ed, Database db)
        {
            // 让用户选择一个实体确定图层
            var peo = new PromptEntityOptions("\n请选择一个配筋区域实体来确定图层: ");
            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return null;

            string layerName;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var ent = (Entity)tr.GetObject(per.ObjectId, OpenMode.ForRead);
                layerName = ent.Layer;
                tr.Commit();
            }

            // 选择该图层的 Polyline 和所有文字
            var pso = new PromptSelectionOptions
            {
                MessageForAdding = $"\n请选择'{layerName}'图层的Polyline和配筋文字:"
            };
            var selRes = ed.GetSelection(pso);
            if (selRes.Status != PromptStatus.OK) return null;

            var result = new Dictionary<ObjectId, List<double>>();

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var polylines = new List<Polyline>();
                var texts = new List<DBText>();

                foreach (var id in selRes.Value.GetObjectIds())
                {
                    var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (entity is Polyline poly && poly.Layer == layerName && poly.Closed && poly.NumberOfVertices == 4)
                        polylines.Add(poly);
                    else if (entity is DBText text && double.TryParse(text.TextString, out _))
                        texts.Add(text);
                }

                foreach (var poly in polylines)
                {
                    var values = new List<double>();
                    foreach (var text in texts)
                    {
                        if (IsPointInsidePolyline(poly, text.Position) && double.TryParse(text.TextString, out double val))
                            values.Add(val);
                    }
                    if (values.Count > 0)
                    {
                        values.Sort();
                        result[poly.ObjectId] = values;
                    }
                }

                tr.Commit();
            }

            ed.WriteMessage($"\n[BaseRein] 从图中选取 {result.Count} 个配筋区域。");
            return result;
        }

        #endregion

        #region 步骤 6：标注配筋区域

        /// <summary>
        /// 步骤 6：根据轴线交点对配筋区域进行尺寸标注
        /// 对应旧代码 BR06 Poly4Dim + BR061/BR062/BR063
        /// </summary>
        public void DimensionReinforcementArea(IntersectionsDirection direction)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            // 选择轴线
            var axisPick = SelectLine(ed, db, "\n请选择一条轴线:");
            if (axisPick == null)
            {
                ed.WriteMessage("\n未选择轴线，操作取消。");
                return;
            }

            // 获取轴线图层的所有直线
            string axisLayer = axisPick.Value.layer;
            var pso = new PromptSelectionOptions { MessageForAdding = $"\n请选择'{axisLayer}'图层的轴线:" };
            var axisFilter = new SelectionFilter(new[] {
                new TypedValue((int)DxfCode.LayerName, axisLayer),
                new TypedValue((int)DxfCode.Start, "LINE")
            });
            var axisSel = ed.GetSelection(pso, axisFilter);
            if (axisSel.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[BaseRein] 未选择轴线，操作取消。");
                return;
            }

            // 选择配筋区域 Polyline
            var reinPick = SelectPolyline(ed, db, "\n请选择代表配筋区域的Polyline:");
            if (reinPick == null)
            {
                ed.WriteMessage("\n未选择配筋区域，操作取消。");
                return;
            }

            string reinLayer = reinPick.Value.layer;
            var reinPso = new PromptSelectionOptions { MessageForAdding = $"\n请选择'{reinLayer}'图层的配筋区域:" };
            var reinFilter = new SelectionFilter(new[] {
                new TypedValue((int)DxfCode.LayerName, reinLayer),
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE")
            });
            var reinSel = ed.GetSelection(reinPso, reinFilter);
            if (reinSel.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[BaseRein] 未选择配筋区域，操作取消。");
                return;
            }

            string dimLayer = direction == IntersectionsDirection.LeftRight
                ? ResolveRaftLayer(LayerSemanticIds.RaftDimX)
                : ResolveRaftLayer(LayerSemanticIds.RaftDimY);

            int dimCount = 0;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                // 收集轴线
                var axisLines = new List<Line>();
                foreach (SelectedObject selObj in axisSel.Value)
                {
                    if (selObj == null) continue;
                    var line = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Line;
                    if (line != null) axisLines.Add(line);
                }

                // 收集配筋多段线
                var reinPolylines = new List<Polyline>();
                foreach (SelectedObject selObj in reinSel.Value)
                {
                    if (selObj == null) continue;
                    var poly = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Polyline;
                    if (poly != null && poly.Closed && poly.NumberOfVertices == 4)
                        reinPolylines.Add(poly);
                }

                var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                double dimDist = _config.DimensionDistanceWithDim * _config.Scale;

                // 为每个配筋区域创建标注
                foreach (var poly in reinPolylines)
                {
                    if (direction == IntersectionsDirection.LeftRight)
                    {
                        // 沿右侧边（edge 1: p1→p2）标注 Y 方向尺寸
                        var p1 = poly.GetPoint3dAt(1);
                        var p2 = poly.GetPoint3dAt(2);
                        double dimX = p1.X + dimDist;

                        var dim = new RotatedDimension
                        {
                            XLine1Point = p1,
                            XLine2Point = p2,
                            DimLinePoint = new Point3d(dimX, (p1.Y + p2.Y) / 2, 0),
                            Rotation = Math.PI / 2,
                            Layer = dimLayer
                        };
                        btr.AppendEntity(dim);
                        tr.AddNewlyCreatedDBObject(dim, true);
                        dimCount++;
                    }
                    else
                    {
                        // 沿下侧边（edge 0: p0→p1）标注 X 方向尺寸
                        var p0 = poly.GetPoint3dAt(0);
                        var p1 = poly.GetPoint3dAt(1);
                        double dimY = p0.Y - dimDist;

                        var dim = new RotatedDimension
                        {
                            XLine1Point = p0,
                            XLine2Point = p1,
                            DimLinePoint = new Point3d((p0.X + p1.X) / 2, dimY, 0),
                            Rotation = 0,
                            Layer = dimLayer
                        };
                        btr.AppendEntity(dim);
                        tr.AddNewlyCreatedDBObject(dim, true);
                        dimCount++;
                    }
                }

                tr.Commit();
            }

            ed.WriteMessage($"\n[BaseRein] 步骤6完成：创建 {dimCount} 个标注。");
        }

        /// <summary>选择一条直线，在事务内读取图层名后返回 ObjectId。</summary>
        private static (ObjectId id, string layer)? SelectLine(Editor ed, Database db, string prompt)
        {
            var peo = new PromptEntityOptions(prompt);
            peo.SetRejectMessage("\n选择的对象必须是直线。");
            peo.AddAllowedClass(typeof(Line), true);
            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return null;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var line = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Line;
                if (line == null) return null;
                string layer = line.Layer;
                tr.Commit();
                return (per.ObjectId, layer);
            }
        }

        /// <summary>选择一条多段线，在事务内读取图层名后返回 ObjectId。</summary>
        private static (ObjectId id, string layer)? SelectPolyline(Editor ed, Database db, string prompt)
        {
            var peo = new PromptEntityOptions(prompt);
            peo.SetRejectMessage("\n选择的对象必须是Polyline。");
            peo.AddAllowedClass(typeof(Polyline), true);
            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return null;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var poly = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Polyline;
                if (poly == null) return null;
                string layer = poly.Layer;
                tr.Commit();
                return (per.ObjectId, layer);
            }
        }

        #endregion

        #region 配置管理

        public BaseReinforcementConfig GetCurrentConfig()
        {
            return _config.Clone();
        }

        public void SaveConfig(BaseReinforcementConfig config)
        {
            _config = config.Clone();
        }

        public BaseReinforcementConfig ResetToDefault()
        {
            _config = BaseReinforcementConfig.CreateDefault();
            return _config.Clone();
        }

        #endregion

        private void EnsureDataForCurrentDocument(Database db)
        {
            string key = GetDatabaseKey(db);
            if (!string.Equals(_cachedDatabaseKey, key, StringComparison.OrdinalIgnoreCase))
            {
                _reinforcementAreaData = null;
                _pillarPierIds = null;
                _cachedDatabaseKey = key;
            }
        }

        private static string GetDatabaseKey(Database db) =>
            db?.Filename ?? string.Empty;

        private readonly struct NumericTextHit
        {
            public ObjectId TextId { get; }
            public Point3d AlignmentPoint { get; }

            public NumericTextHit(ObjectId textId, Point3d alignmentPoint)
            {
                TextId = textId;
                AlignmentPoint = alignmentPoint;
            }
        }

        private static List<NumericTextHit> CollectNumericTexts(IEnumerable<DBObject> objects)
        {
            var result = new List<NumericTextHit>();
            foreach (var obj in objects)
            {
                if (obj is DBText dbText && double.TryParse(dbText.TextString, out _))
                    result.Add(new NumericTextHit(dbText.ObjectId, dbText.AlignmentPoint));
                else if (obj is MText mText)
                {
                    string text = mText.Text.Replace("\\P", " ");
                    if (double.TryParse(text, out _))
                        result.Add(new NumericTextHit(mText.ObjectId, mText.Location));
                }
            }
            return result;
        }

        private static bool TryReadNumericText(DBObject textObj, out double value)
        {
            value = 0;
            if (textObj is DBText dbText)
                return double.TryParse(dbText.TextString, out value);
            if (textObj is MText mText)
                return double.TryParse(mText.Text.Replace("\\P", " "), out value);
            return false;
        }

        private static string ResolveRaftLayer(string semanticId)
        {
            string fallback = semanticId switch
            {
                LayerSemanticIds.RaftSlabXTop => LayerBuiltinDefaults.RaftSlabXTop,
                LayerSemanticIds.RaftSlabXBottom => LayerBuiltinDefaults.RaftSlabXBottom,
                LayerSemanticIds.RaftSlabYTop => LayerBuiltinDefaults.RaftSlabYTop,
                LayerSemanticIds.RaftSlabYBottom => LayerBuiltinDefaults.RaftSlabYBottom,
                LayerSemanticIds.RaftTextX => LayerBuiltinDefaults.RaftTextX,
                LayerSemanticIds.RaftTextY => LayerBuiltinDefaults.RaftTextY,
                LayerSemanticIds.RaftDimX => LayerBuiltinDefaults.RaftDimX,
                LayerSemanticIds.RaftDimY => LayerBuiltinDefaults.RaftDimY,
                _ => null
            };
            return UserLayerNameResolver.Get(semanticId, fallback);
        }
    }
}
