using System;
using Autodesk.AutoCAD.DatabaseServices;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata
{
    /// <summary>
    /// 道路模块的 AutoCAD XData 读写工具（对应决策 4 - DWG Xdata 作为稳定 GUID 载体）。
    ///
    /// 设计：
    /// - 在 <see cref="RegAppName"/>（"HY_ROAD"）命名空间下读写几个固定 Key（"ID" / "KIND" / "SCHEMA"）。
    /// - 使用 AutoCAD 原生 <c>XData</c> 机制（TypedValue 数组），而非 <c>ExtensionDictionary</c>，因为：
    ///     · GUID 与类型标签合计 ~60 字节，远在 XData 16KB 限制以内。
    ///     · XData 贴近对象、查询简单、导出 DXF 时保留，符合"对象附加数据"心智模型。
    /// - v1 只写 ID + KIND；v2 Blender 同步阶段可扩展 LOD / 材质指针等。
    ///
    /// 使用须知：
    /// - 首次写入任何对象前需要保证 RegApp 已注册（<see cref="EnsureRegApp"/>）。
    /// - 所有方法都在调用方提供的 <c>Transaction</c> 作用域内执行，本类不自主开启事务。
    /// </summary>
    public static class HyRoadXdata
    {
        /// <summary>RegApp 名称（HY_ROAD）。</summary>
        public const string RegAppName = "HY_ROAD";

        /// <summary>领域对象 GUID 的 Xdata Key。</summary>
        public const string KeyId = "ID";

        /// <summary>对象类型标签的 Xdata Key（如 "Alignment" / "Profile" / "Template" / "Corridor" / "RoadArm"）。</summary>
        public const string KeyKind = "KIND";

        /// <summary>Schema 版本 Key（对应 <c>SchemaVersion.Current</c>）。</summary>
        public const string KeySchema = "SCHEMA";

        // =============================================================================
        // KIND 常量（M6 扩展到 14 种）。命令层必须通过本组常量写入 / 读取，避免字符串散落。
        // =============================================================================

        /// <summary>平面线位。</summary>
        public const string KindAlignment = "Alignment";
        /// <summary>路线工作台「原线」开关绘制的 Polyline（非正式中心线实体）。</summary>
        public const string KindAlignmentRawPick = "AlignmentRawPick";
        /// <summary>
        /// 路线工作台主预览实体（当前选中 Alignment 的中心线黄色预览，绘制在
        /// <c>HyRoadLayers.LivePreviewLayer</c> 上）。与 <see cref="KindAlignmentRawPick"/> 区分：
        /// RawPick 是创建时刻快照（横跨所有线位），LivePreview 只是工作台当前会话视觉反馈，
        /// 面板关闭时由 <c>RoadAlignmentLivePreviewService.EraseAll</c> 自动清理。
        /// </summary>
        public const string KindAlignmentLivePreview = "AlignmentLivePreview";
        /// <summary>交叉口。</summary>
        public const string KindIntersection = "Intersection";
        /// <summary>交叉口转角圆弧。</summary>
        public const string KindCornerArc = "CornerArc";
        /// <summary>缘石坡道（GB 50763）。</summary>
        public const string KindCurbRamp = "CurbRamp";
        /// <summary>盲道（GB 50763）。</summary>
        public const string KindTactilePaving = "TactilePaving";
        /// <summary>人行横道（GB 5768）。</summary>
        public const string KindCrosswalk = "Crosswalk";
        /// <summary>停止线。</summary>
        public const string KindStopLine = "StopLine";
        /// <summary>标准横断面图（单张出图实例）。</summary>
        public const string KindCrossSection = "CrossSection";
        /// <summary>几何点标注（BP/EP/PI/BC/EC/TS/SC/CS/ST）。</summary>
        public const string KindGeometryPointLabel = "GeometryPointLabel";

        // --------- M6 / M7 / M8 / M10 新增 KIND ---------

        /// <summary>结构层（M7）。</summary>
        public const string KindStructureLayer = "StructureLayer";
        /// <summary>绿化带实例（M8 提取命令使用）。</summary>
        public const string KindGreenStripInstance = "GreenStripInstance";
        /// <summary>人行道实例（M8 提取命令使用）。</summary>
        public const string KindSidewalkInstance = "SidewalkInstance";
        /// <summary>车道分界线标线（M10）。</summary>
        public const string KindLaneStripe = "LaneStripe";
        /// <summary>平面分段模型绘图要素（M10）。</summary>
        public const string KindCorridorPlan = "CorridorPlan";

        // --------- 控制体 KIND（M6） ---------

        /// <summary>控制体 · 参考点。</summary>
        public const string KindControlReferencePoint = "Control.ReferencePoint";
        /// <summary>控制体 · 参考线。</summary>
        public const string KindControlReferenceLine = "Control.ReferenceLine";
        /// <summary>控制体 · 参考面。</summary>
        public const string KindControlReferencePlane = "Control.ReferencePlane";
        /// <summary>控制体 · 选中集合。</summary>
        public const string KindControlSelectionSet = "Control.SelectionSet";

        /// <summary>
        /// 判断 KIND 是否属于「控制体」（以 <c>"Control."</c> 开头）。
        /// 用于 DWG 扫描时把控制体与实体分流，选中集 / 导出过滤等场景。
        /// </summary>
        public static bool IsControlKind(string kind)
            => !string.IsNullOrEmpty(kind) && kind.StartsWith("Control.", System.StringComparison.Ordinal);

        /// <summary>
        /// 确保 <see cref="RegAppName"/> 已注册到数据库的 RegAppTable。
        /// 首次写入 Xdata 之前必须调用一次（幂等）。
        /// </summary>
        public static void EnsureRegApp(Transaction tr, Database db)
        {
            if (tr == null) throw new ArgumentNullException(nameof(tr));
            if (db == null) throw new ArgumentNullException(nameof(db));

            var regAppTable = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
            if (regAppTable.Has(RegAppName)) return;

            regAppTable.UpgradeOpen();
            using (var record = new RegAppTableRecord { Name = RegAppName })
            {
                regAppTable.Add(record);
                tr.AddNewlyCreatedDBObject(record, true);
            }
        }

        /// <summary>
        /// 读取道路对象的 GUID。若对象无 HY_ROAD Xdata 或格式异常，返回 <see cref="Guid.Empty"/>。
        /// </summary>
        public static Guid ReadId(Transaction tr, DBObject obj)
        {
            string raw = ReadStringValue(obj, KeyId);
            if (string.IsNullOrWhiteSpace(raw)) return Guid.Empty;
            return Guid.TryParse(raw, out var g) ? g : Guid.Empty;
        }

        /// <summary>
        /// 读取道路对象的类型标签（"Alignment" / "Profile" / ...）。不存在返回 <c>null</c>。
        /// </summary>
        public static string ReadKind(Transaction tr, DBObject obj)
        {
            return ReadStringValue(obj, KeyKind);
        }

        /// <summary>
        /// 写入 / 覆盖道路对象的 ID + KIND（+ SCHEMA）。
        /// 本方法会重写全部 HY_ROAD Xdata 段，不保留未识别的 Key。
        /// </summary>
        public static void Write(Transaction tr, Database db, DBObject obj, Guid id, string kind, string schemaVersion)
        {
            if (tr == null) throw new ArgumentNullException(nameof(tr));
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            if (id == Guid.Empty) throw new ArgumentException("id cannot be empty", nameof(id));

            EnsureRegApp(tr, db);

            if (!obj.IsWriteEnabled) obj.UpgradeOpen();

            var rb = new ResultBuffer(
                new TypedValue((int)DxfCode.ExtendedDataRegAppName, RegAppName),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, KeyId),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, id.ToString("D")),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, KeyKind),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, kind ?? string.Empty),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, KeySchema),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, schemaVersion ?? string.Empty));

            obj.XData = rb;
            rb.Dispose();
        }

        /// <summary>
        /// 按 Key 读取 HY_ROAD Xdata 的字符串值。
        /// 未找到 RegApp 或 Key 不存在时返回 <c>null</c>。
        /// </summary>
        private static string ReadStringValue(DBObject obj, string key)
        {
            if (obj == null) return null;
            var rb = obj.GetXDataForApplication(RegAppName);
            if (rb == null) return null;

            try
            {
                var values = rb.AsArray();
                // values[0] 为 RegAppName，随后为 key/value 对
                for (int i = 1; i + 1 < values.Length; i++)
                {
                    if (values[i].TypeCode != (int)DxfCode.ExtendedDataAsciiString) continue;
                    if (string.Equals(values[i].Value?.ToString(), key, StringComparison.Ordinal))
                    {
                        return values[i + 1].Value?.ToString();
                    }
                }
                return null;
            }
            finally
            {
                rb.Dispose();
            }
        }
    }
}
