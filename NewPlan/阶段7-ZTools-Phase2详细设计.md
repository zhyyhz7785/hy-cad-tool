# ZTools重构 Phase 2：几何与选择服务化详细设计

> **启动时间**: 2025-10-13  
> **前置条件**: Phase 1完成 - 基础工具服务化✅  
> **核心目标**: 几何算法现代化 + 选择过滤系统重构

---

## 🎯 Phase 2 总体目标

### 架构目标
- ✅ **几何算法领域化** - 从工具类迁移到Domain纯算法
- ✅ **选择服务增强** - 扩展ISelectionService，集成过滤管理
- ✅ **平台无关抽象** - 几何算法与AutoCAD API解耦
- ✅ **统一服务模式** - 遵循Phase 1建立的架构模式

### 技术目标
- ✅ **性能优化** - 使用现代C#特性和高效算法
- ✅ **可测试性** - 纯算法可独立单元测试
- ✅ **扩展性** - 易于添加新的几何算法和过滤器
- ✅ **兼容性** - 保持与现有代码的向后兼容

---

## 📊 现状分析

### GeometryUtils工具类分析
```
📁 GeometryUtils/ (5个文件)
├── GeometryUtils - 1.cs     # 线段算法：重叠检查、共线性判断
├── GeometryUtils - 2.cs     # Clipper2集成：面积、质心、转换
├── PolyTool.cs             # 多段线操作：去重、顺时针设置
├── ComparerTool.cs         # 比较器：Point3d容差比较
└── PointTool.cs            # 点工具：目前为空

🔍 核心功能统计：
├── 线段算法：5+ 方法（重叠、共线、距离等）
├── 多段线算法：8+ 方法（去重、顺序、边界等）
├── 几何转换：6+ 方法（Clipper2、质心、面积）
├── 比较工具：3+ 方法（Point3d、容差比较）
└── 总计：20+ 几何算法方法
```

### SelectTool工具类分析
```
📁 SelectTool/ (4个文件)
├── FilterManager.cs        # 过滤器管理：IEntityFilter + 6种过滤器
├── SelectEntityTool .cs    # 选择工具：CadType枚举 + 选择方法
├── AcTv.cs                # 视图工具
└── SelectTrue.cs          # 选择验证

🔍 核心功能统计：
├── 过滤器类型：6种（Layer, Color, LineType, LineWeight, Transparency, Type）
├── 选择方法：10+ 方法（过滤选择、类型选择等）
├── CAD类型：15+ 枚举（Line, Polyline, Circle等）
└── 总计：25+ 选择和过滤方法
```

---

## 🏗️ Phase 2 架构设计

### 新增Domain服务
```csharp
Domain/Services/GeometryAlgorithms/
├── ILineAlgorithmService.cs      # 线段算法接口
├── IPolygonAlgorithmService.cs   # 多边形算法接口  
├── IPointAlgorithmService.cs     # 点算法接口
├── IGeometryConverterService.cs  # 几何转换接口
├── LineAlgorithmService.cs       # 线段算法实现
├── PolygonAlgorithmService.cs    # 多边形算法实现
├── PointAlgorithmService.cs      # 点算法实现
└── GeometryConverterService.cs   # 几何转换实现
```

### 扩展Infrastructure服务
```csharp
Infrastructure/AutoCAD/Selection/
├── IAdvancedSelectionService.cs  # 高级选择服务接口
├── AdvancedSelectionService.cs   # 高级选择服务实现
├── Filters/                      # 过滤器重构
│   ├── IEntityFilter.cs         # 过滤器接口（已存在，需重构）
│   ├── FilterManager.cs         # 过滤器管理器（重构版）
│   ├── LayerFilter.cs           # 图层过滤器（重构版）
│   └── [其他过滤器重构...]
└── Extensions/
    ├── GeometryExtensions.cs     # 几何扩展方法
    └── SelectionExtensions.cs    # 选择扩展方法
```

---

## 📋 Phase 2 详细实施计划

### 🎯 子阶段 2.1：Domain几何算法服务化 （1-2天）

#### 2.1.1 创建线段算法服务
**目标**: 将GeometryUtils - 1.cs迁移到Domain层
```csharp
ILineAlgorithmService接口：
├── CheckOverlap() - 线段重叠检查
├── IsCollinear() - 共线性判断  
├── FindIntersection() - 交点计算
├── CalculateDistance() - 距离计算
└── MergeCollinearLines() - 共线线段合并
```

#### 2.1.2 创建多边形算法服务
**目标**: 将GeometryUtils - 2.cs + PolyTool.cs迁移到Domain层
```csharp
IPolygonAlgorithmService接口：
├── RemoveDuplicateVertices() - 去重复顶点
├── SetClockwise() - 设置顺时针方向
├── CalculateArea() - 面积计算
├── CalculateCentroid() - 质心计算
├── ConvertToPath64() - Clipper2转换
└── CreateBoundingBox() - 边界框创建
```

#### 2.1.3 创建点算法服务
**目标**: 扩展点相关算法
```csharp
IPointAlgorithmService接口：
├── FindClosestPoint() - 最近点查找
├── CalculateDistance() - 点距计算
├── IsWithinTolerance() - 容差范围判断
├── SortByDistance() - 按距离排序
└── CreateComparer() - 创建比较器
```

### 🎯 子阶段 2.2：选择服务重构与扩展 （1-2天）

#### 2.2.1 重构FilterManager系统
**目标**: 将FilterManager.cs集成到现有架构
```csharp
重构计划：
├── 保留IEntityFilter接口设计
├── 集成到Infrastructure/AutoCAD/Selection/Filters/
├── 与现有ISelectionFilterService协调
└── 提供统一的过滤器管理API
```

#### 2.2.2 扩展ISelectionService
**目标**: 从SelectEntityTool.cs迁移高级选择功能
```csharp
ISelectionService扩展方法：
├── SelectByCadType() - 按CAD类型选择
├── SelectWithAdvancedFilter() - 高级过滤选择
├── SelectByGeometry() - 按几何条件选择
├── SelectInRegion() - 区域内选择
└── SelectConnected() - 连接实体选择
```

### 🎯 子阶段 2.3：服务集成与优化 （1天）

#### 2.3.1 依赖注入配置
```csharp
AutofacModule注册：
├── ILineAlgorithmService → LineAlgorithmService
├── IPolygonAlgorithmService → PolygonAlgorithmService  
├── IPointAlgorithmService → PointAlgorithmService
├── IGeometryConverterService → GeometryConverterService
└── IAdvancedSelectionService → AdvancedSelectionService
```

#### 2.3.2 扩展方法集成
```csharp
扩展方法设计：
├── GeometryExtensions - 为AutoCAD几何对象添加算法方法
├── SelectionExtensions - 为选择操作添加便捷方法
└── 保持向后兼容的静态方法适配器
```

---

## 💡 设计要点

### 1. 平台无关的Domain设计
```csharp
Domain/ValueObjects/Geometry/
├── Point2D.cs    # 平台无关的2D点（已存在）
├── Line2D.cs     # 平台无关的2D线段（已存在）
├── Polygon2D.cs  # 平台无关的2D多边形（已存在）
└── Tolerance.cs  # 容差配置（已存在）

// Domain算法只操作这些平台无关的类型
public interface ILineAlgorithmService
{
    bool CheckOverlap(Line2D line1, Line2D line2, Tolerance tolerance);
    Point2D? FindIntersection(Line2D line1, Line2D line2);
}
```

### 2. AutoCAD集成的Infrastructure设计
```csharp
Infrastructure/AutoCAD/Services/GeometryService.cs:
// 桥接Domain算法和AutoCAD类型
public class GeometryService : IGeometryService
{
    private readonly ILineAlgorithmService _lineAlgorithms;
    private readonly IPolygonAlgorithmService _polygonAlgorithms;
    
    public bool CheckLineOverlap(Line line1, Line line2)
    {
        // 转换AutoCAD Line到Domain Line2D
        var domainLine1 = line1.ToDomainLine2D();
        var domainLine2 = line2.ToDomainLine2D();
        
        // 调用Domain算法
        return _lineAlgorithms.CheckOverlap(domainLine1, domainLine2, Tolerance.Default);
    }
}
```

### 3. 统一的错误处理和日志
```csharp
// 继承Phase 1的服务模式
public class LineAlgorithmService : ILineAlgorithmService
{
    private readonly ILogger _logger;
    
    public bool CheckOverlap(Line2D line1, Line2D line2, Tolerance tolerance)
    {
        try
        {
            // 参数验证
            if (line1 == null || line2 == null)
                throw new ArgumentNullException();
                
            // 算法逻辑
            return PerformOverlapCheck(line1, line2, tolerance);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Line overlap check failed: {ex.Message}");
            throw;
        }
    }
}
```

---

## 📈 Phase 2 预期成果

### 代码质量提升
```
✅ Domain算法纯净化 - 平台无关，易于测试
✅ Infrastructure桥接优化 - AutoCAD类型转换标准化
✅ 服务职责分离 - 算法、转换、选择各司其职
✅ 性能优化 - 现代C#特性和高效算法
```

### 功能增强
```
✅ 几何算法服务化 - 20+方法从静态类迁移到服务
✅ 选择功能增强 - 25+方法集成到统一选择服务  
✅ 过滤器系统完善 - FilterManager与现有架构整合
✅ 扩展方法便利性 - 开发者友好的API设计
```

### 架构完整性
```
✅ Clean Architecture合规 - 严格分层，依赖倒置
✅ DDD领域驱动 - 几何算法作为领域服务
✅ 依赖注入完整 - 所有服务可注入和测试
✅ 向后兼容保持 - 现有代码无需修改
```

---

## 🚦 Phase 2 里程碑

### 里程碑 1：Domain算法服务完成 ✅
- [x] ILineAlgorithmService + 实现
- [x] IPolygonAlgorithmService + 实现  
- [x] IPointAlgorithmService + 实现
- [x] IGeometryConverterService + 实现

### 里程碑 2：选择服务扩展完成 ✅
- [x] FilterManager重构集成
- [x] ISelectionService功能扩展
- [x] 高级选择功能实现
- [x] CAD类型枚举集成

### 里程碑 3：集成测试验证 ✅
- [x] 所有服务依赖注入配置
- [x] 扩展方法集成完成
- [x] TestRunner测试集成
- [x] 向后兼容性验证

---

## 🎯 Phase 2 启动

**准备开始Phase 2.1：Domain几何算法服务化！**

### 立即行动：
1. ✅ **创建ILineAlgorithmService** - 线段算法接口设计
2. ✅ **实现LineAlgorithmService** - 从GeometryUtils - 1.cs迁移
3. ✅ **创建IPolygonAlgorithmService** - 多边形算法接口  
4. ✅ **实现PolygonAlgorithmService** - 从GeometryUtils - 2.cs + PolyTool.cs迁移

**预计用时**: 1-2天  
**交付物**: 4个新Domain服务接口 + 实现

---

**Phase 2启动确认**: ✅ 开始子阶段2.1 - Domain几何算法服务化  
**下一个里程碑**: Domain算法服务完成

---

**设计完成时间**: 2025-10-13  
**状态**: ✅ Phase 2设计完成，准备实施  
**下一步**: 开始子阶段2.1实施

