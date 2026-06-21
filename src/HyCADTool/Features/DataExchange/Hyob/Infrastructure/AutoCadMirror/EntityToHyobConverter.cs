using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Features.DataExchange.Hyob.Domain.Objects.Opaque;
using HyCADTool.Features.DataExchange.Hyob.Domain.Objects.Typed;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;
using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;

namespace HyCADTool.Features.DataExchange.Hyob.Infrastructure.AutoCadMirror
{
    /// <summary>把 AutoCAD <c>Entity</c> 转成 hyob 对象的转换结果。</summary>
    internal readonly struct EntityConvertResult
    {
        public IHyobObject Object { get; }
        /// <summary>关联的 XData 对象（M5）；entity 无 XData 时为 null。</summary>
        public IHyobObject XData { get; }
        /// <summary>关联的 ExtensionDictionary 对象（M5）；entity 无 ExtDict 时为 null。</summary>
        public IHyobObject ExtDict { get; }
        public string HandleHex { get; }
        public string RxClassName { get; }
        public HyobObjectKind TypeId { get; }
        public bool IsTyped { get; }

        public EntityConvertResult(IHyobObject obj, IHyobObject xdata, IHyobObject extDict,
                                   string handleHex, string rxClass,
                                   HyobObjectKind typeId, bool isTyped)
        {
            Object = obj;
            XData = xdata;
            ExtDict = extDict;
            HandleHex = handleHex;
            RxClassName = rxClass;
            TypeId = typeId;
            IsTyped = isTyped;
        }
    }

    /// <summary>
    /// AutoCAD Entity → hyob Object 分发器。
    ///
    /// 白名单：
    ///   M2   — Line / Polyline (lightweight) / Arc / Circle
    ///   M3   — DBText / MText / BlockReference (内联 Attribute)
    ///   M4-A — Dimension 9 种 subtype 统一 schema
    ///   M4-B — MLeader (语义级) / Hatch (模式参数级)
    ///   M5   — XData / ExtensionDictionary 作为独立 hyob object 与 entity 并列
    ///
    /// 设计原则（03 §3.5 自动降级）：
    ///   1. 优先尝试白名单 TypedObject；
    ///   2. 任意异常或不在白名单 → 回退 <see cref="HyobOpaqueObject"/>；
    ///   3. 退化路径必有，不允许镜像中断。
    ///
    /// 部分 Typed builder 需要事务上下文（textStyleName / attribute 解析）；
    /// 通过 <paramref name="reader"/> 委托读 ObjectId 解决
    /// <see cref="Transaction"/> 与 <see cref="OpenCloseTransaction"/> 类型分裂问题，
    /// 也方便测试时注入 mock。<paramref name="reader"/> 为 null 时按"无 tx 简化路径"取值。
    /// </summary>
    internal static class EntityToHyobConverter
    {
        /// <summary>读 DBObject 的统一委托（兼容 Transaction / OpenCloseTransaction）。</summary>
        public delegate DBObject ObjectReader(ObjectId id, OpenMode mode);

        public static EntityConvertResult Convert(Entity ent, ObjectReader reader)
        {
            string handle = ent.Handle.Value.ToString("X");
            string rxName = ent.GetRXClass()?.Name ?? "";

            IHyobObject obj = null;
            HyobObjectKind kind = HyobObjectKind.Opaque;
            bool isTyped = false;

            try
            {
                switch (ent)
                {
                    case Line line:
                        obj = BuildLine(line, handle); kind = HyobObjectKind.Line; isTyped = true; break;
                    case Circle circle:
                        obj = BuildCircle(circle, handle); kind = HyobObjectKind.Circle; isTyped = true; break;
                    case Arc arc:
                        obj = BuildArc(arc, handle); kind = HyobObjectKind.Arc; isTyped = true; break;
                    case Polyline pl:
                        obj = BuildPolyline(pl, handle); kind = HyobObjectKind.Polyline; isTyped = true; break;
                    case BlockReference br:
                        obj = BuildBlockRef(br, handle, reader); kind = HyobObjectKind.BlockReference; isTyped = true; break;
                    case MText mt:
                        obj = BuildMText(mt, handle, reader); kind = HyobObjectKind.MText; isTyped = true; break;
                    case DBText txt:
                        obj = BuildDBText(txt, handle, reader); kind = HyobObjectKind.DBText; isTyped = true; break;
                    case Dimension dim:
                        obj = BuildDimension(dim, handle); kind = HyobObjectKind.Dimension; isTyped = true; break;
                    case MLeader ml:
                        obj = BuildMLeader(ml, handle, reader); kind = HyobObjectKind.MLeader; isTyped = true; break;
                    case Hatch hat:
                        obj = BuildHatch(hat, handle); kind = HyobObjectKind.HatchBoundary; isTyped = true; break;
                }
            }
            catch
            {
                // 任意异常静默降级到 Opaque 兜底，保证镜像不中断。
                obj = null;
            }

            if (obj == null)
            {
                obj = BuildOpaqueObject(ent);
                kind = HyobObjectKind.Opaque;
                isTyped = false;
            }

            // M5: 任何 entity 都可能附带 XData / ExtensionDictionary，独立存储不影响 entity hash
            IHyobObject xdata = null;
            IHyobObject extDict = null;
            try { xdata = TryBuildXData(ent); } catch { }
            try { extDict = TryBuildExtDict(ent, reader); } catch { }

            return new EntityConvertResult(obj, xdata, extDict, handle, rxName, kind, isTyped);
        }

        // ------------------------------------------------------------ Typed builders (M2)

        private static HyobLine BuildLine(Line l, string handle)
        {
            return new HyobLine(
                layer: l.Layer ?? string.Empty,
                handleHex: handle,
                sx: l.StartPoint.X, sy: l.StartPoint.Y, sz: l.StartPoint.Z,
                ex: l.EndPoint.X,   ey: l.EndPoint.Y,   ez: l.EndPoint.Z);
        }

        private static HyobCircle BuildCircle(Circle c, string handle)
        {
            return new HyobCircle(
                layer: c.Layer ?? string.Empty,
                handleHex: handle,
                cx: c.Center.X, cy: c.Center.Y, cz: c.Center.Z,
                radius: c.Radius,
                nx: c.Normal.X, ny: c.Normal.Y, nz: c.Normal.Z);
        }

        private static HyobArc BuildArc(Arc a, string handle)
        {
            return new HyobArc(
                layer: a.Layer ?? string.Empty,
                handleHex: handle,
                cx: a.Center.X, cy: a.Center.Y, cz: a.Center.Z,
                radius: a.Radius,
                startAngleRad: a.StartAngle,
                endAngleRad: a.EndAngle,
                nx: a.Normal.X, ny: a.Normal.Y, nz: a.Normal.Z);
        }

        private static HyobPolyline BuildPolyline(Polyline p, string handle)
        {
            int n = p.NumberOfVertices;
            var verts = new List<HyobPolylineVertex>(n);
            for (int i = 0; i < n; i++)
            {
                var pt = p.GetPoint2dAt(i);
                double bulge = p.GetBulgeAt(i);
                verts.Add(new HyobPolylineVertex(pt.X, pt.Y, bulge));
            }
            return new HyobPolyline(
                layer: p.Layer ?? string.Empty,
                handleHex: handle,
                closed: p.Closed,
                elevation: p.Elevation,
                nx: p.Normal.X, ny: p.Normal.Y, nz: p.Normal.Z,
                constantWidth: ResolvePolylineStoredWidth(p),
                vertices: verts);
        }

        /// <summary>
        /// 读取多段线有效全局线宽：优先 ConstantWidth；getter 不可用时回退首段 Start/End 宽。
        /// </summary>
        private static double ResolvePolylineStoredWidth(Polyline p)
        {
            try
            {
                if (p.ConstantWidth > 0) return p.ConstantWidth;
            }
            catch { }

            if (p.NumberOfVertices > 0)
            {
                double w = p.GetStartWidthAt(0);
                if (w > 0) return w;
                w = p.GetEndWidthAt(0);
                if (w > 0) return w;
            }
            return 0;
        }

        // ------------------------------------------------------------ Typed builders (M3)

        private static HyobDBText BuildDBText(DBText t, string handle, ObjectReader reader)
        {
            byte flags = 0;
            if (t.IsMirroredInX) flags |= HyobDBText.FlagMirroredX;
            if (t.IsMirroredInY) flags |= HyobDBText.FlagMirroredY;

            return new HyobDBText(
                layer: t.Layer ?? string.Empty,
                handleHex: handle,
                textString: t.TextString ?? string.Empty,
                textStyleName: ReadTextStyleName(reader, t.TextStyleId),
                px: t.Position.X, py: t.Position.Y, pz: t.Position.Z,
                height: t.Height,
                rotationRad: t.Rotation,
                widthFactor: t.WidthFactor,
                obliqueRad: t.Oblique,
                thickness: t.Thickness,
                nx: t.Normal.X, ny: t.Normal.Y, nz: t.Normal.Z,
                horizontalMode: (byte)t.HorizontalMode,
                verticalMode: (byte)t.VerticalMode,
                ax: t.AlignmentPoint.X, ay: t.AlignmentPoint.Y, az: t.AlignmentPoint.Z,
                flags: flags);
        }

        private static HyobMText BuildMText(MText m, string handle, ObjectReader reader)
        {
            uint argb = 0;
            try
            {
                if (m.BackgroundFillColor != null)
                    argb = (uint)m.BackgroundFillColor.ColorValue.ToArgb();
            }
            catch { }

            return new HyobMText(
                layer: m.Layer ?? string.Empty,
                handleHex: handle,
                contents: m.Contents ?? string.Empty,
                textStyleName: ReadTextStyleName(reader, m.TextStyleId),
                lx: m.Location.X, ly: m.Location.Y, lz: m.Location.Z,
                textHeight: m.TextHeight,
                width: m.Width,
                rotationRad: m.Rotation,
                nx: m.Normal.X, ny: m.Normal.Y, nz: m.Normal.Z,
                dx: m.Direction.X, dy: m.Direction.Y, dz: m.Direction.Z,
                attachment: (byte)m.Attachment,
                drawDirection: 0,  // MText 无此属性；schema 保留占位（M5+ 由 BackgroundFlags 填充）
                lineSpacingStyle: (byte)m.LineSpacingStyle,
                lineSpacingFactor: m.LineSpacingFactor,
                backgroundFill: (byte)(m.BackgroundFill ? 1 : 0),
                backgroundColorArgb: argb,
                backgroundScaleFactor: m.BackgroundScaleFactor);
        }

        private static HyobBlockReference BuildBlockRef(BlockReference br, string handle, ObjectReader reader)
        {
            var attrs = new List<HyobBlockAttribute>();
            if (reader != null && br.AttributeCollection != null)
            {
                foreach (ObjectId aid in br.AttributeCollection)
                {
                    if (aid.IsNull || aid.IsErased) continue;
                    try
                    {
                        var a = (AttributeReference)reader(aid, OpenMode.ForRead);
                        attrs.Add(new HyobBlockAttribute(a.Tag ?? string.Empty, a.TextString ?? string.Empty));
                    }
                    catch { }
                }
            }

            return new HyobBlockReference(
                layer: br.Layer ?? string.Empty,
                handleHex: handle,
                blockName: br.Name ?? string.Empty,
                px: br.Position.X, py: br.Position.Y, pz: br.Position.Z,
                sx: br.ScaleFactors.X, sy: br.ScaleFactors.Y, sz: br.ScaleFactors.Z,
                rotationRad: br.Rotation,
                nx: br.Normal.X, ny: br.Normal.Y, nz: br.Normal.Z,
                attributes: attrs);
        }

        // ------------------------------------------------------------ Typed builders (M4-A: Dimension)

        private static HyobDimension BuildDimension(Dimension d, string handle)
        {
            string layer = d.Layer ?? string.Empty;
            string blockName = d.BlockName ?? string.Empty;
            string dimText = d.DimensionText ?? string.Empty;
            string dimStyle = d.DimensionStyleName ?? string.Empty;
            var tp = d.TextPosition;
            double meas = 0;
            try { meas = d.Measurement; } catch { }
            var n = d.Normal;

            HyobDimension.Subtype subType;
            var pts = new List<HyobPoint3d>();
            var extras = new List<double>();

            switch (d)
            {
                case RotatedDimension rd:
                    subType = HyobDimension.Subtype.Rotated;
                    pts.Add(P(rd.XLine1Point));
                    pts.Add(P(rd.XLine2Point));
                    pts.Add(P(rd.DimLinePoint));
                    extras.Add(rd.Rotation);
                    extras.Add(rd.Oblique);
                    break;

                case AlignedDimension ad:
                    subType = HyobDimension.Subtype.Aligned;
                    pts.Add(P(ad.XLine1Point));
                    pts.Add(P(ad.XLine2Point));
                    pts.Add(P(ad.DimLinePoint));
                    extras.Add(ad.Oblique);
                    break;

                case DiametricDimension dd:
                    subType = HyobDimension.Subtype.Diametric;
                    pts.Add(P(dd.ChordPoint));
                    pts.Add(P(dd.FarChordPoint));
                    extras.Add(dd.LeaderLength);
                    break;

                case RadialDimensionLarge rdl:
                    subType = HyobDimension.Subtype.RadialLarge;
                    pts.Add(P(rdl.Center));
                    pts.Add(P(rdl.ChordPoint));
                    pts.Add(P(rdl.OverrideCenter));
                    pts.Add(P(rdl.JogPoint));
                    extras.Add(rdl.JogAngle);
                    break;

                case RadialDimension rad:
                    subType = HyobDimension.Subtype.Radial;
                    pts.Add(P(rad.Center));
                    pts.Add(P(rad.ChordPoint));
                    extras.Add(rad.LeaderLength);
                    break;

                case ArcDimension arcd:
                    subType = HyobDimension.Subtype.Arc;
                    pts.Add(P(arcd.XLine1Point));
                    pts.Add(P(arcd.XLine2Point));
                    pts.Add(P(arcd.ArcPoint));
                    extras.Add((double)(byte)arcd.ArcSymbolType);
                    extras.Add(arcd.IsPartial ? 1.0 : 0.0);
                    break;

                case OrdinateDimension od:
                    subType = HyobDimension.Subtype.Ordinate;
                    pts.Add(P(od.DefiningPoint));
                    pts.Add(P(od.LeaderEndPoint));
                    extras.Add(od.UsingXAxis ? 1.0 : 0.0);
                    break;

                case Point3AngularDimension p3a:
                    subType = HyobDimension.Subtype.Point3Angular;
                    pts.Add(P(p3a.XLine1Point));
                    pts.Add(P(p3a.XLine2Point));
                    pts.Add(P(p3a.CenterPoint));
                    pts.Add(P(p3a.ArcPoint));
                    break;

                case LineAngularDimension2 la2:
                    subType = HyobDimension.Subtype.LineAngular;
                    pts.Add(P(la2.XLine1Start));
                    pts.Add(P(la2.XLine1End));
                    pts.Add(P(la2.XLine2Start));
                    pts.Add(P(la2.XLine2End));
                    pts.Add(P(la2.ArcPoint));
                    break;

                default:
                    subType = HyobDimension.Subtype.Other;
                    break;
            }

            return new HyobDimension(
                subType, layer, handle, blockName, dimText, dimStyle,
                tp.X, tp.Y, tp.Z, meas, n.X, n.Y, n.Z, pts, extras);
        }

        private static HyobPoint3d P(Autodesk.AutoCAD.Geometry.Point3d p)
            => new HyobPoint3d(p.X, p.Y, p.Z);

        // ------------------------------------------------------------ Typed builders (M4-B: MLeader / Hatch)

        private static HyobMLeader BuildMLeader(MLeader m, string handle, ObjectReader reader)
        {
            // ContentType enum (NoneContent / BlockContent / MTextContent / ToleranceContent)
            // 直接 byte cast；具体值不依赖 AutoCAD 版本，因为 hyob 内部常量自洽，
            // 实际 contents 在 mtext/block 字段已分支存储。
            byte ct = HyobMLeader.ContentOther;
            try { ct = (byte)m.ContentType; } catch { }

            string mtextContents = string.Empty;
            string blockName = string.Empty;
            try
            {
                if (m.MText != null) mtextContents = m.MText.Contents ?? string.Empty;
            }
            catch { }
            try
            {
                if (reader != null && !m.BlockContentId.IsNull)
                {
                    var btr = reader(m.BlockContentId, OpenMode.ForRead) as BlockTableRecord;
                    if (btr != null) blockName = btr.Name ?? string.Empty;
                }
            }
            catch { }

            string styleName = string.Empty;
            try
            {
                if (reader != null && !m.MLeaderStyle.IsNull)
                {
                    var ms = reader(m.MLeaderStyle, OpenMode.ForRead) as MLeaderStyle;
                    if (ms != null) styleName = ms.Name ?? string.Empty;
                }
            }
            catch { }

            var tl = new Autodesk.AutoCAD.Geometry.Point3d();
            try { tl = m.TextLocation; } catch { }

            uint leaderCount = 0;
            uint leaderLineCount = 0;
            try { leaderCount = (uint)m.LeaderCount; } catch { }
            try { leaderLineCount = (uint)m.LeaderLineCount; } catch { }

            return new HyobMLeader(
                layer: m.Layer ?? string.Empty,
                handleHex: handle,
                contentType: ct,
                mtextContents: mtextContents,
                blockName: blockName,
                mleaderStyleName: styleName,
                tx: tl.X, ty: tl.Y, tz: tl.Z,
                textHeight: SafeGet(() => m.TextHeight),
                arrowSize: SafeGet(() => m.ArrowSize),
                doglegLength: SafeGet(() => m.DoglegLength),
                landingGap: SafeGet(() => m.LandingGap),
                scale: SafeGet(() => m.Scale),
                blockRotation: SafeGet(() => m.BlockRotation),
                leaderCount: leaderCount,
                leaderLineCount: leaderLineCount);
        }

        private static HyobHatch BuildHatch(Hatch h, string handle)
        {
            var n = h.Normal;
            return new HyobHatch(
                layer: h.Layer ?? string.Empty,
                handleHex: handle,
                patternType: (byte)h.PatternType,
                patternName: h.PatternName ?? string.Empty,
                patternScale: SafeGet(() => h.PatternScale),
                patternAngleRad: SafeGet(() => h.PatternAngle),
                patternSpace: SafeGet(() => h.PatternSpace),
                hatchStyle: (byte)h.HatchStyle,
                elevation: SafeGet(() => h.Elevation),
                nx: n.X, ny: n.Y, nz: n.Z,
                numberOfLoops: (uint)Math.Max(0, h.NumberOfLoops),
                numberOfPatternDefinitions: (uint)Math.Max(0, h.NumberOfPatternDefinitions),
                area: SafeGet(() => h.Area),
                associative: (byte)(h.Associative ? 1 : 0));
        }

        private static double SafeGet(System.Func<double> f)
        {
            try { return f(); } catch { return 0.0; }
        }

        // ------------------------------------------------------------ Helpers

        private static string ReadTextStyleName(ObjectReader reader, ObjectId styleId)
        {
            if (reader == null || styleId.IsNull) return string.Empty;
            try
            {
                var ts = (TextStyleTableRecord)reader(styleId, OpenMode.ForRead);
                return ts.Name ?? string.Empty;
            }
            catch { return string.Empty; }
        }

        // ------------------------------------------------------------ Opaque fallback

        private static IHyobObject BuildOpaqueObject(Entity ent)
        {
            var snap = EntitySnapshot.FromEntity(ent);
            return new HyobOpaqueObject(
                dwgClassName: snap.RxClassName,
                dwgAppName: "ACAD",
                handleHex: snap.Handle,
                rawDxf: snap.EncodeRawDxf());
        }

        // ------------------------------------------------------------ XData / ExtDict helpers (M5)

        /// <summary>
        /// 提取 entity 的 XData。AutoCAD 的 ResultBuffer 用 DxfCode=1001 分组（每出现一次
        /// 1001 表示进入新 RegApp 命名空间）。后续 (code, value) 对都属于该 app 直到下一个 1001。
        /// 返回 null 表示无 XData 或解析后为空。
        /// </summary>
        private static IHyobObject TryBuildXData(Entity ent)
        {
            ResultBuffer rb = null;
            try { rb = ent.XData; }
            catch { return null; }
            if (rb == null) return null;

            var groups = new List<HyobXDataAppGroup>();
            string currentApp = null;
            var currentEntries = new List<HyobXDataEntry>();

            try
            {
                foreach (TypedValue tv in rb)
                {
                    int code = (int)tv.TypeCode;
                    if (code == 1001)
                    {
                        if (currentApp != null)
                            groups.Add(new HyobXDataAppGroup(currentApp, currentEntries));
                        currentApp = tv.Value as string ?? string.Empty;
                        currentEntries = new List<HyobXDataEntry>();
                    }
                    else
                    {
                        string val = tv.Value?.ToString() ?? string.Empty;
                        currentEntries.Add(new HyobXDataEntry(code, val));
                    }
                }
                if (currentApp != null)
                    groups.Add(new HyobXDataAppGroup(currentApp, currentEntries));
            }
            finally
            {
                rb.Dispose();
            }

            if (groups.Count == 0) return null;
            return new HyobXDataAttachment(groups);
        }

        /// <summary>
        /// 提取 entity 的 ExtensionDictionary（扁平 entry 摘要）。嵌套 DBDictionary 不递归展开。
        /// 返回 null 表示无扩展字典或字典为空。
        /// </summary>
        private static IHyobObject TryBuildExtDict(Entity ent, ObjectReader reader)
        {
            if (reader == null) return null;
            ObjectId extId = ent.ExtensionDictionary;
            if (extId.IsNull) return null;

            DBDictionary dict = null;
            try { dict = reader(extId, OpenMode.ForRead) as DBDictionary; }
            catch { return null; }
            if (dict == null) return null;

            var entries = new List<HyobExtDictEntry>();
            foreach (DBDictionaryEntry kv in dict)
            {
                string key = kv.Key ?? string.Empty;
                ObjectId vId = kv.Value;
                if (vId.IsNull) continue;
                try
                {
                    var inner = reader(vId, OpenMode.ForRead);
                    if (inner is Xrecord xr)
                    {
                        string summary;
                        using (var rb = xr.Data)
                        {
                            summary = rb?.ToString() ?? string.Empty;
                        }
                        entries.Add(new HyobExtDictEntry(key, HyobExtDictEntry.KindXrecord, summary));
                    }
                    else if (inner is DBDictionary nd)
                    {
                        entries.Add(new HyobExtDictEntry(key, HyobExtDictEntry.KindDictionary, $"<{nd.Count}>"));
                    }
                    else if (inner != null)
                    {
                        string rxClass = string.Empty;
                        try { rxClass = inner.GetRXClass()?.Name ?? string.Empty; } catch { }
                        entries.Add(new HyobExtDictEntry(key, HyobExtDictEntry.KindOther, rxClass));
                    }
                }
                catch { /* 单 entry 失败不影响其他 entry */ }
            }

            if (entries.Count == 0) return null;
            return new HyobExtensionDictionary(entries);
        }
    }
}
