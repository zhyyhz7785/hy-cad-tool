# HyCADTool.Refactored

## 📐 项目概述

这是 HyCADTool AutoCAD 插件的重构版本，采用 **Clean Architecture + DDD（领域驱动设计）** 架构，目标是创建一个平台无关的核心业务逻辑层，为未来迁移到 Blender 做准备。

## 🏗️ 架构设计

### 分层结构

```
HyCADTool.Refactored/
├── Domain/                          # 领域层（零 AutoCAD 依赖）
│   ├── Entities/                    # 领域实体
│   ├── ValueObjects/                # 值对象
│   │   ├── Geometry/                # 几何值对象（Point2D, Polygon2D 等）
│   │   └── Configuration/           # 配置值对象
│   ├── Services/                    # 领域服务
│   └── Interfaces/                  # 领域接口（抽象契约）
│
├── Application/                     # 应用层（业务用例编排）
│   ├── UseCases/                    # 用例（业务流程）
│   ├── DTOs/                        # 数据传输对象
│   └── Interfaces/                  # 应用层接口
│
├── Infrastructure/                  # 基础设施层（AutoCAD 特定）
│   ├── AutoCAD/
│   │   ├── Services/                # AutoCAD 服务实现
│   │   ├── Converters/              # 类型转换器
│   │   └── Persistence/             # 数据持久化
│   └── Configuration/               # 配置管理
│       ├── AutofacModule.cs         # 依赖注入配置
│       └── ServiceLocator.cs        # 服务定位器
│
├── Presentation/                    # 表示层（命令与 UI）
│   ├── Commands/                    # AutoCAD 命令
│   ├── Panels/                      # WPF 面板
│   └── PluginInitializer.cs         # 插件入口点
│
└── Shared/                          # 共享工具
    ├── Extensions/                  # 扩展方法
    └── Helpers/                     # 辅助类
```

### 依赖规则

```
✅ 允许的依赖方向：
Presentation → Application → Domain
Presentation → Infrastructure
Infrastructure → Domain

🚫 禁止的依赖：
Domain → Infrastructure (❌)
Domain → Presentation (❌)
Application → Infrastructure (❌)
Application → Presentation (❌)
```

## 🎯 核心设计原则

### 1. **平台无关的领域层**

领域层（Domain/）完全不依赖 AutoCAD API，所有几何对象、算法和业务逻辑都是纯 C# 实现：

```csharp
// ✅ 领域层：平台无关
namespace HyCADTool.Refactored.Domain.ValueObjects.Geometry
{
    public readonly struct Point3D
    {
        public double X { get; }
        public double Y { get; }
        public double Z { get; }
        
        public double DistanceTo(Point3D other) => ...
    }
}

// ✅ 基础设施层：AutoCAD 特定
namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Converters
{
    public class GeometryConverter
    {
        public Autodesk.AutoCAD.Geometry.Point3d ToAutoCADPoint3d(Point3D domainPoint)
        {
            return new Autodesk.AutoCAD.Geometry.Point3d(
                domainPoint.X, domainPoint.Y, domainPoint.Z);
        }
    }
}
```

### 2. **依赖注入（Autofac）**

所有服务通过接口定义，使用 Autofac 容器管理生命周期：

```csharp
// Domain/Interfaces/IGeometryService.cs
public interface IGeometryService
{
    bool IsPointInsidePolygon(Point2D point, Polygon2D polygon);
    IEnumerable<Polygon2D> Union(IEnumerable<Polygon2D> polygons);
}

// Infrastructure/AutoCAD/Services/AutoCadGeometryService.cs
public class AutoCadGeometryService : IGeometryService
{
    // AutoCAD 实现（使用 Clipper2）
}

// 注册：Infrastructure/Configuration/AutofacModule.cs
builder.RegisterType<AutoCadGeometryService>()
    .As<IGeometryService>()
    .SingleInstance();
```

### 3. **可测试性**

领域层可以在不依赖 AutoCAD 环境的情况下进行单元测试：

```csharp
[Test]
public void CalculateArea_Square_ReturnsCorrectArea()
{
    var square = new Polygon2D(new[]
    {
        new Point2D(0, 0),
        new Point2D(10, 0),
        new Point2D(10, 10),
        new Point2D(0, 10)
    });
    
    var area = square.GetArea();
    
    Assert.AreEqual(100.0, area, 1e-6);
}
```

## 🚀 快速开始

### 环境要求

- **.NET Framework 4.8**
- **AutoCAD 2024** 或更高版本
- **Visual Studio 2019/2022**

### 编译项目

```bash
# 打开解决方案
打开 HyCADtoolGpt.sln

# 选择配置
选择 "Debug" 或 "Release"

# 生成项目
生成 → 生成解决方案
```

### 加载插件

```
AutoCAD 命令行:
NETLOAD

浏览并选择:
HyCADTool.Refactored\bin\Debug\HyCADTool.Refactored.dll
```

### 验证加载

加载成功后会在命令行看到：

```
========================================
HyCADTool.Refactored 插件初始化中...
========================================
✓ 依赖注入容器已初始化
✓ 样式和图层已初始化
  - 已创建文本样式: HyCAD_Standard
  - 已创建标注样式: HyCAD_Dim
  - 已创建默认图层
========================================
✓ HyCADTool.Refactored 插件初始化完成！
========================================
```

## 📦 主要依赖

| 包名 | 版本 | 用途 |
|------|------|------|
| AutoCAD.NET | 24.3.0 | AutoCAD API |
| Autofac | 7.1.0 | 依赖注入容器 |
| Clipper2 | 1.4.0 | 多边形布尔运算 |
| NetTopologySuite | 2.5.0 | 拓扑分析 |
| Newtonsoft.Json | 13.0.3 | JSON 序列化 |

## 🔄 与原项目的关系

| 方面 | 原项目（HyCADTool） | 重构项目（HyCADTool.Refactored） |
|------|---------------------|----------------------------------|
| 架构 | 过程式 + 静态工具类 | Clean Architecture + DDD |
| 依赖 | 紧耦合 AutoCAD API | 领域层零 AutoCAD 依赖 |
| 测试 | 难以单元测试 | 领域层可独立测试 |
| 迁移性 | 无法迁移到其他平台 | 核心逻辑可迁移到 Blender/Python |
| 状态 | 生产环境使用 | 重构进行中 |

## 🛠️ 开发指南

### 添加新功能的步骤

1. **定义领域模型**（Domain/Entities 或 ValueObjects）
2. **创建领域服务**（Domain/Services，如果需要）
3. **定义应用用例**（Application/UseCases）
4. **实现基础设施**（Infrastructure/AutoCAD/Services）
5. **创建命令**（Presentation/Commands）
6. **注册依赖**（Infrastructure/Configuration/AutofacModule.cs）
7. **编写测试**（Tests/）

### 示例：添加一个简单命令

```csharp
// 1. 定义领域接口（如果需要新服务）
// Domain/Interfaces/IMyService.cs

// 2. 创建 AutoCAD 实现
// Infrastructure/AutoCAD/Services/MyService.cs

// 3. 注册服务
// Infrastructure/Configuration/AutofacModule.cs
builder.RegisterType<MyService>().As<IMyService>().SingleInstance();

// 4. 创建命令
// Presentation/Commands/MyCommand.cs
[CommandMethod("MYCMD")]
public void Execute()
{
    var service = ServiceLocator.Resolve<IMyService>();
    // ... 实现逻辑
}
```

## 📚 未来计划

### 阶段 1: 基础设施（✅ 已完成）
- [x] 项目结构
- [x] 依赖注入容器
- [x] 领域几何值对象
- [x] 样式和图层服务

### 阶段 2: 几何组件迁移
- [ ] 几何算法服务
- [ ] Clipper2 和 NTS 适配器
- [ ] 几何转换器完善

### 阶段 3: 核心功能迁移
- [ ] 地脚螺栓功能
- [ ] 桩基布置优化（Voronoi + Lloyd）
- [ ] 基础底板配筋

### 阶段 4: Blender 迁移准备
- [ ] 提取领域知识文档
- [ ] Python 接口设计
- [ ] Blender API 映射

## 📖 相关文档

- [架构决策记录（ADR）](./Docs/ADR/)
- [领域知识文档](./Docs/DomainKnowledge/)
- [Blender 迁移指南](./Docs/BlenderMigration.md)

## 🤝 贡献指南

1. 遵循 Clean Architecture 原则
2. 确保领域层零 AutoCAD 依赖
3. 所有公共 API 添加 XML 文档注释
4. 复杂算法添加详细注释和数学公式
5. 编写单元测试验证核心逻辑

## 📄 许可证

版权所有 © 2025 HyCADTool Team

---

**🌟 让结构设计更高效，为 Blender 迁移铺路！**

