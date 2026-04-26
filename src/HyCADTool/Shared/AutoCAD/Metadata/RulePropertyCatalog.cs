using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Shared.AutoCAD.Entities;

namespace HyCADTool.Shared.AutoCAD.Metadata
{
    /// <summary>
    /// Curated property catalog for rule queries. It intentionally exposes a small,
    /// user-oriented whitelist instead of every AutoCAD .NET public property.
    /// </summary>
    public static class RulePropertyCatalog
    {
        private static readonly IReadOnlyList<RulePropertyDescriptor> CommonDescriptors = CreateCommonDescriptors();
        private static readonly IReadOnlyDictionary<string, IReadOnlyList<RulePropertyDescriptor>> TypeDescriptors = CreateTypeDescriptors();

        public static IReadOnlyList<RulePropertyDescriptor> GetDescriptors(Entity entity, bool includeAdvanced = false)
        {
            if (entity == null)
                return new List<RulePropertyDescriptor>();

            return GetDescriptors(entity.GetType().Name, includeAdvanced);
        }

        public static IReadOnlyList<RulePropertyDescriptor> GetDescriptors(string entityType, bool includeAdvanced = false)
        {
            var descriptors = new List<RulePropertyDescriptor>();
            descriptors.AddRange(CommonDescriptors);

            if (!string.IsNullOrWhiteSpace(entityType) && TypeDescriptors.TryGetValue(entityType, out var specific))
                descriptors.AddRange(specific);

            if (IsDimensionType(entityType) && TypeDescriptors.TryGetValue("Dimension", out var dimension))
                descriptors.AddRange(dimension);

            return descriptors
                .Where(d => includeAdvanced || d.IsDefault)
                .GroupBy(d => d.PropertyName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderBy(d => d.Priority).First())
                .ToList();
        }

        public static Dictionary<string, (object Value, string Type)> GetFilterableProperties(Entity entity, bool includeAdvanced = false)
        {
            var props = new Dictionary<string, (object, string)>(StringComparer.OrdinalIgnoreCase);
            foreach (var descriptor in GetDescriptors(entity, includeAdvanced))
            {
                try
                {
                    var value = descriptor.GetValue(entity);
                    if (value != null)
                        props[descriptor.PropertyName] = (value, descriptor.PropertyType);
                }
                catch
                {
                    // Some properties are only available for specific DB states; skip them.
                }
            }

            return props;
        }

        public static List<PropertyMetadata> GetMetadataList(bool includeAdvanced = false)
        {
            return CommonDescriptors
                .Concat(TypeDescriptors.Values.SelectMany(v => v))
                .Where(d => includeAdvanced || d.IsDefault)
                .Select(ToMetadata)
                .ToList();
        }

        private static PropertyMetadata ToMetadata(RulePropertyDescriptor descriptor)
        {
            return new PropertyMetadata
            {
                EntityType = descriptor.EntityType,
                PropertyName = descriptor.PropertyName,
                PropertyType = descriptor.PropertyType,
                DisplayName = descriptor.DisplayName,
                Category = descriptor.Category
            };
        }

        private static IReadOnlyList<RulePropertyDescriptor> CreateCommonDescriptors()
        {
            return new List<RulePropertyDescriptor>
            {
                Common("DxfType", "图形类型", "String", e => e.GetRXClass()?.DxfName),
                Common("EntityType", "C# 类型", "String", e => e.GetType().Name, RulePropertyPriority.AdvancedGeometry),
                Common("Layer", "图层", "String", e => e.Layer),
                Common("TrueColor", "真颜色", "String", e => EntityAppearanceResolver.GetTrueColor(e)?.ToString()),
                Common("ColorSource", "颜色来源", "String", e => e.Color.ColorMethod.ToString(), RulePropertyPriority.AdvancedGeometry),
                Common("TrueLineWeight", "真线宽", "Int32", e => EntityAppearanceResolver.GetTrueLineWeight(e)),
                Common("TrueLinetype", "真线型", "String", e => GetLinetypeName(e)),
                Common("TrueTransparency", "透明度", "Int32", e => EntityAppearanceResolver.GetTrueTransparency(e), RulePropertyPriority.AdvancedGeometry),
                Common("LinetypeScale", "线型比例", "Double", e => e.LinetypeScale),
                Common("Visible", "可见性", "Boolean", e => e.Visible, RulePropertyPriority.AdvancedGeometry)
            };
        }

        private static IReadOnlyDictionary<string, IReadOnlyList<RulePropertyDescriptor>> CreateTypeDescriptors()
        {
            var map = new Dictionary<string, IReadOnlyList<RulePropertyDescriptor>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Line"] = new List<RulePropertyDescriptor>
                {
                    For<Line>("Line", "Length", "长度", "Double", l => l.Length),
                    For<Line>("Line", "Angle", "角度", "Double", l => l.Angle),
                    For<Line>("Line", "StartX", "起点X", "Double", l => l.StartPoint.X),
                    For<Line>("Line", "StartY", "起点Y", "Double", l => l.StartPoint.Y),
                    For<Line>("Line", "EndX", "终点X", "Double", l => l.EndPoint.X),
                    For<Line>("Line", "EndY", "终点Y", "Double", l => l.EndPoint.Y),
                    For<Line>("Line", "MidX", "中点X", "Double", l => (l.StartPoint.X + l.EndPoint.X) / 2.0, RulePropertyPriority.AdvancedGeometry),
                    For<Line>("Line", "MidY", "中点Y", "Double", l => (l.StartPoint.Y + l.EndPoint.Y) / 2.0, RulePropertyPriority.AdvancedGeometry),
                    For<Line>("Line", "Thickness", "厚度", "Double", l => l.Thickness, RulePropertyPriority.AdvancedGeometry)
                },
                ["Polyline"] = new List<RulePropertyDescriptor>
                {
                    For<Polyline>("Polyline", "Length", "长度", "Double", p => p.Length),
                    For<Polyline>("Polyline", "Area", "面积", "Double", p => SafeArea(p)),
                    For<Polyline>("Polyline", "IsClosed", "是否闭合", "Boolean", p => p.Closed),
                    For<Polyline>("Polyline", "VertexCount", "顶点数", "Int32", p => p.NumberOfVertices),
                    For<Polyline>("Polyline", "ConstantWidth", "固定宽度", "Double", p => p.ConstantWidth),
                    For<Polyline>("Polyline", "Elevation", "高程", "Double", p => p.Elevation, RulePropertyPriority.AdvancedGeometry),
                    For<Polyline>("Polyline", "StartX", "起点X", "Double", p => p.StartPoint.X, RulePropertyPriority.AdvancedGeometry),
                    For<Polyline>("Polyline", "StartY", "起点Y", "Double", p => p.StartPoint.Y, RulePropertyPriority.AdvancedGeometry),
                    For<Polyline>("Polyline", "EndX", "终点X", "Double", p => p.EndPoint.X, RulePropertyPriority.AdvancedGeometry),
                    For<Polyline>("Polyline", "EndY", "终点Y", "Double", p => p.EndPoint.Y, RulePropertyPriority.AdvancedGeometry)
                },
                ["Circle"] = new List<RulePropertyDescriptor>
                {
                    For<Circle>("Circle", "Radius", "半径", "Double", c => c.Radius),
                    For<Circle>("Circle", "Diameter", "直径", "Double", c => c.Radius * 2.0),
                    For<Circle>("Circle", "Area", "面积", "Double", c => Math.PI * c.Radius * c.Radius),
                    For<Circle>("Circle", "CenterX", "圆心X", "Double", c => c.Center.X),
                    For<Circle>("Circle", "CenterY", "圆心Y", "Double", c => c.Center.Y),
                    For<Circle>("Circle", "Circumference", "周长", "Double", c => 2.0 * Math.PI * c.Radius, RulePropertyPriority.AdvancedGeometry),
                    For<Circle>("Circle", "Thickness", "厚度", "Double", c => c.Thickness, RulePropertyPriority.AdvancedGeometry)
                },
                ["Arc"] = new List<RulePropertyDescriptor>
                {
                    For<Arc>("Arc", "Radius", "半径", "Double", a => a.Radius),
                    For<Arc>("Arc", "Length", "弧长", "Double", a => a.Length),
                    For<Arc>("Arc", "TotalAngle", "总角度", "Double", a => a.TotalAngle),
                    For<Arc>("Arc", "StartAngle", "起始角度", "Double", a => a.StartAngle),
                    For<Arc>("Arc", "EndAngle", "终止角度", "Double", a => a.EndAngle),
                    For<Arc>("Arc", "CenterX", "圆心X", "Double", a => a.Center.X),
                    For<Arc>("Arc", "CenterY", "圆心Y", "Double", a => a.Center.Y),
                    For<Arc>("Arc", "StartX", "起点X", "Double", a => a.StartPoint.X, RulePropertyPriority.AdvancedGeometry),
                    For<Arc>("Arc", "StartY", "起点Y", "Double", a => a.StartPoint.Y, RulePropertyPriority.AdvancedGeometry),
                    For<Arc>("Arc", "EndX", "终点X", "Double", a => a.EndPoint.X, RulePropertyPriority.AdvancedGeometry),
                    For<Arc>("Arc", "EndY", "终点Y", "Double", a => a.EndPoint.Y, RulePropertyPriority.AdvancedGeometry),
                    For<Arc>("Arc", "ChordLength", "弦长", "Double", a => a.StartPoint.DistanceTo(a.EndPoint), RulePropertyPriority.AdvancedGeometry)
                },
                ["DBText"] = new List<RulePropertyDescriptor>
                {
                    For<DBText>("DBText", "TextString", "文字内容", "String", t => t.TextString),
                    For<DBText>("DBText", "Height", "字高", "Double", t => t.Height),
                    For<DBText>("DBText", "Rotation", "旋转", "Double", t => t.Rotation),
                    For<DBText>("DBText", "PositionX", "位置X", "Double", t => t.Position.X),
                    For<DBText>("DBText", "PositionY", "位置Y", "Double", t => t.Position.Y),
                    For<DBText>("DBText", "WidthFactor", "宽度因子", "Double", t => t.WidthFactor, RulePropertyPriority.Style),
                    For<DBText>("DBText", "Oblique", "倾斜角度", "Double", t => t.Oblique, RulePropertyPriority.AdvancedGeometry)
                },
                ["MText"] = new List<RulePropertyDescriptor>
                {
                    For<MText>("MText", "Text", "纯文本", "String", t => StripMTextFormatting(t.Contents)),
                    For<MText>("MText", "TextHeight", "字高", "Double", t => t.TextHeight),
                    For<MText>("MText", "Width", "宽度", "Double", t => t.Width),
                    For<MText>("MText", "LocationX", "位置X", "Double", t => t.Location.X),
                    For<MText>("MText", "LocationY", "位置Y", "Double", t => t.Location.Y),
                    For<MText>("MText", "ActualWidth", "实际宽度", "Double", t => t.ActualWidth, RulePropertyPriority.AdvancedGeometry),
                    For<MText>("MText", "ActualHeight", "实际高度", "Double", t => t.ActualHeight, RulePropertyPriority.AdvancedGeometry),
                    For<MText>("MText", "Rotation", "旋转", "Double", t => t.Rotation, RulePropertyPriority.AdvancedGeometry)
                },
                ["BlockReference"] = new List<RulePropertyDescriptor>
                {
                    For<BlockReference>("BlockReference", "BlockName", "块名", "String", b => b.Name),
                    For<BlockReference>("BlockReference", "EffectiveName", "有效块名", "String", b => b.IsDynamicBlock ? GetBlockName(b, b.DynamicBlockTableRecord) : b.Name),
                    For<BlockReference>("BlockReference", "PositionX", "位置X", "Double", b => b.Position.X),
                    For<BlockReference>("BlockReference", "PositionY", "位置Y", "Double", b => b.Position.Y),
                    For<BlockReference>("BlockReference", "Rotation", "旋转", "Double", b => b.Rotation),
                    For<BlockReference>("BlockReference", "ScaleX", "X比例", "Double", b => b.ScaleFactors.X),
                    For<BlockReference>("BlockReference", "ScaleY", "Y比例", "Double", b => b.ScaleFactors.Y),
                    For<BlockReference>("BlockReference", "HasAttributes", "有属性", "Boolean", b => b.AttributeCollection != null && b.AttributeCollection.Count > 0, RulePropertyPriority.AdvancedFunction),
                    For<BlockReference>("BlockReference", "AttributeCount", "属性数量", "Int32", b => b.AttributeCollection?.Count ?? 0, RulePropertyPriority.AdvancedFunction)
                },
                ["Hatch"] = new List<RulePropertyDescriptor>
                {
                    For<Hatch>("Hatch", "PatternName", "图案名", "String", h => h.PatternName),
                    For<Hatch>("Hatch", "PatternScale", "图案比例", "Double", h => h.PatternScale),
                    For<Hatch>("Hatch", "PatternAngle", "图案角度", "Double", h => h.PatternAngle),
                    For<Hatch>("Hatch", "Area", "面积", "Double", h => SafeArea(h)),
                    For<Hatch>("Hatch", "LoopCount", "边界数", "Int32", h => h.NumberOfLoops),
                    For<Hatch>("Hatch", "Associative", "关联填充", "Boolean", h => h.Associative, RulePropertyPriority.AdvancedGeometry)
                },
                ["Dimension"] = new List<RulePropertyDescriptor>
                {
                    For<Dimension>("Dimension", "DimensionStyleName", "标注样式", "String", d => GetDimensionStyleName(d)),
                    For<Dimension>("Dimension", "Measurement", "测量值", "Double", d => d.Measurement),
                    For<Dimension>("Dimension", "TextOverride", "文字替代", "String", d => d.DimensionText),
                    For<Dimension>("Dimension", "DimScale", "标注比例", "Double", d => GetDoubleProperty(d, "Dimscale"), RulePropertyPriority.Style)
                },
                ["MLeader"] = new List<RulePropertyDescriptor>
                {
                    For<MLeader>("MLeader", "Text", "文本", "String", m => GetMLeaderText(m)),
                    For<MLeader>("MLeader", "TextHeight", "字高", "Double", m => m.TextHeight),
                    For<MLeader>("MLeader", "Scale", "比例", "Double", m => m.Scale),
                    For<MLeader>("MLeader", "LeaderCount", "引线数量", "Int32", m => GetIntProperty(m, "LeaderCount")),
                    For<MLeader>("MLeader", "ArrowSize", "箭头尺寸", "Double", m => m.ArrowSize, RulePropertyPriority.AdvancedGeometry),
                    For<MLeader>("MLeader", "LandingGap", "着陆间隙", "Double", m => m.LandingGap, RulePropertyPriority.AdvancedGeometry),
                    For<MLeader>("MLeader", "DoglegLength", "折线长度", "Double", m => m.DoglegLength, RulePropertyPriority.AdvancedGeometry)
                },
                ["Wipeout"] = new List<RulePropertyDescriptor>
                {
                    For<Wipeout>("Wipeout", "Width", "宽度", "Double", w => GetDoubleProperty(w, "Width")),
                    For<Wipeout>("Wipeout", "Height", "高度", "Double", w => GetDoubleProperty(w, "Height")),
                    For<Wipeout>("Wipeout", "PositionX", "位置X", "Double", w => GetPointProperty(w, "Position").X),
                    For<Wipeout>("Wipeout", "PositionY", "位置Y", "Double", w => GetPointProperty(w, "Position").Y),
                    For<Wipeout>("Wipeout", "Rotation", "旋转", "Double", w => GetDoubleProperty(w, "Rotation"))
                },
                ["Region"] = new List<RulePropertyDescriptor>
                {
                    For<Region>("Region", "Area", "面积", "Double", r => r.Area),
                    For<Region>("Region", "BoundsMinX", "边界最小X", "Double", r => r.GeometricExtents.MinPoint.X),
                    For<Region>("Region", "BoundsMinY", "边界最小Y", "Double", r => r.GeometricExtents.MinPoint.Y),
                    For<Region>("Region", "BoundsMaxX", "边界最大X", "Double", r => r.GeometricExtents.MaxPoint.X),
                    For<Region>("Region", "BoundsMaxY", "边界最大Y", "Double", r => r.GeometricExtents.MaxPoint.Y)
                },
                ["Solid3d"] = new List<RulePropertyDescriptor>
                {
                    For<Solid3d>("Solid3d", "BoundsMinX", "边界最小X", "Double", s => s.GeometricExtents.MinPoint.X),
                    For<Solid3d>("Solid3d", "BoundsMinY", "边界最小Y", "Double", s => s.GeometricExtents.MinPoint.Y),
                    For<Solid3d>("Solid3d", "BoundsMinZ", "边界最小Z", "Double", s => s.GeometricExtents.MinPoint.Z),
                    For<Solid3d>("Solid3d", "BoundsMaxX", "边界最大X", "Double", s => s.GeometricExtents.MaxPoint.X),
                    For<Solid3d>("Solid3d", "BoundsMaxY", "边界最大Y", "Double", s => s.GeometricExtents.MaxPoint.Y),
                    For<Solid3d>("Solid3d", "BoundsMaxZ", "边界最大Z", "Double", s => s.GeometricExtents.MaxPoint.Z)
                }
            };

            return map;
        }

        private static RulePropertyDescriptor Common(
            string propertyName,
            string displayName,
            string propertyType,
            Func<Entity, object> accessor,
            RulePropertyPriority priority = RulePropertyPriority.Common)
        {
            return new RulePropertyDescriptor
            {
                EntityType = "*",
                PropertyName = propertyName,
                DisplayName = displayName,
                PropertyType = propertyType,
                Category = "通用属性",
                Priority = priority,
                ValueAccessor = accessor
            };
        }

        private static RulePropertyDescriptor For<T>(
            string entityType,
            string propertyName,
            string displayName,
            string propertyType,
            Func<T, object> accessor,
            RulePropertyPriority priority = RulePropertyPriority.Core,
            string category = "规则查询属性")
            where T : Entity
        {
            return new RulePropertyDescriptor
            {
                EntityType = entityType,
                PropertyName = propertyName,
                DisplayName = displayName,
                PropertyType = propertyType,
                Category = category,
                Priority = priority,
                ValueAccessor = e => e is T typed ? accessor(typed) : null
            };
        }

        private static bool IsDimensionType(string entityType)
        {
            return !string.IsNullOrWhiteSpace(entityType)
                && (entityType.IndexOf("Dimension", StringComparison.OrdinalIgnoreCase) >= 0
                    || entityType.Equals("Dimension", StringComparison.OrdinalIgnoreCase));
        }

        private static double SafeArea(Curve curve)
        {
            try { return curve.Area; }
            catch { return 0.0; }
        }

        private static double SafeArea(Hatch hatch)
        {
            try { return hatch.Area; }
            catch { return 0.0; }
        }

        private static string GetLinetypeName(Entity entity)
        {
            var linetypeId = EntityAppearanceResolver.GetTrueLinetype(entity);
            if (linetypeId.IsNull)
                return entity.Linetype;

            using (var tr = entity.Database.TransactionManager.StartTransaction())
            {
                var record = tr.GetObject(linetypeId, OpenMode.ForRead, false) as LinetypeTableRecord;
                tr.Commit();
                return record?.Name ?? entity.Linetype;
            }
        }

        private static string GetBlockName(BlockReference block, ObjectId blockTableRecordId)
        {
            if (blockTableRecordId.IsNull)
                return block.Name;

            using (var tr = block.Database.TransactionManager.StartTransaction())
            {
                var record = tr.GetObject(blockTableRecordId, OpenMode.ForRead, false) as BlockTableRecord;
                tr.Commit();
                return record?.Name ?? block.Name;
            }
        }

        private static string GetDimensionStyleName(Dimension dimension)
        {
            if (dimension.DimensionStyle.IsNull)
                return string.Empty;

            using (var tr = dimension.Database.TransactionManager.StartTransaction())
            {
                var record = tr.GetObject(dimension.DimensionStyle, OpenMode.ForRead, false) as DimStyleTableRecord;
                tr.Commit();
                return record?.Name ?? string.Empty;
            }
        }

        private static string GetMLeaderText(MLeader leader)
        {
            try
            {
                return leader.MText?.Contents ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string StripMTextFormatting(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\P", "\n");
        }

        private static double GetDoubleProperty(object source, string propertyName)
        {
            var value = GetPropertyValue(source, propertyName);
            return value == null ? 0.0 : Convert.ToDouble(value);
        }

        private static int GetIntProperty(object source, string propertyName)
        {
            var value = GetPropertyValue(source, propertyName);
            return value == null ? 0 : Convert.ToInt32(value);
        }

        private static Point3d GetPointProperty(object source, string propertyName)
        {
            var value = GetPropertyValue(source, propertyName);
            return value is Point3d point ? point : Point3d.Origin;
        }

        private static object GetPropertyValue(object source, string propertyName)
        {
            return source?.GetType()
                .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(source);
        }
    }
}
