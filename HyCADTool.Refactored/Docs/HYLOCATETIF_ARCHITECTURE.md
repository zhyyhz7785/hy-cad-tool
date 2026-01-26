# HYLOCATETIF 命令架构设计文档

## 架构概览

HYLOCATETIF 命令采用 **Clean Architecture + DDD (Domain-Driven Design)** 设计模式，遵循 **SOLID 原则**。

```
┌─────────────────────────────────────────────────────────┐
│                  Presentation Layer                     │
│  ┌───────────────────────────────────────────────────┐  │
│  │  LocateTifCommand                                 │  │
│  │  - 命令入口点                                     │  │
│  │  - 用户交互                                       │  │
│  │  - 结果展示                                       │  │
│  └───────────────────────────────────────────────────┘  │
└────────────────────┬────────────────────────────────────┘
                     │ 依赖注入
┌────────────────────▼────────────────────────────────────┐
│                  Application Layer                      │
│  (业务流程编排 - 在 LocateTifCommand 中实现)          │
│  - 文件选择                                            │
│  - 流程控制                                            │
│  - 错误处理                                            │
└────────────────────┬────────────────────────────────────┘
                     │ 使用
┌────────────────────▼────────────────────────────────────┐
│                   Domain Layer                          │
│  ┌───────────────────────────────────────────────────┐  │
│  │  IGeospatialService (Interface)                   │  │
│  │  - ParseWorldFile()                               │  │
│  │  - CalculateImageBoundary()                       │  │
│  │  - PixelToGeo()                                   │  │
│  │  - GeoToPixel()                                   │  │
│  └───────────────────────────────────────────────────┘  │
│  ┌───────────────────────────────────────────────────┐  │
│  │  Value Objects                                    │  │
│  │  - WorldFileParameters (struct)                   │  │
│  │  - Polygon2D                                      │  │
│  │  - Point2D                                        │  │
│  └───────────────────────────────────────────────────┘  │
└────────────────────┬────────────────────────────────────┘
                     │ 实现
┌────────────────────▼────────────────────────────────────┐
│               Infrastructure Layer                      │
│  ┌───────────────────────────────────────────────────┐  │
│  │  GeospatialService (Implementation)               │  │
│  │  - 坐标转换算法                                   │  │
│  │  - 几何计算                                       │  │
│  └───────────────────────────────────────────────────┘  │
│  ┌───────────────────────────────────────────────────┐  │
│  │  AutoCAD Integration                              │  │
│  │  - 数据库操作                                     │  │
│  │  - 图形绘制                                       │  │
│  └───────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
```

## 分层设计

### 1. Presentation Layer (表现层)

**文件**: `Presentation/Commands/LocateTifCommand.cs`

**职责**:
- 接收用户输入（文件选择）
- 协调各层完成业务流程
- 展示结果给用户
- 处理用户交互异常

**关键特性**:
- 依赖注入 `IGeospatialService`
- 使用 AutoCAD 文件对话框
- 性能监视（Stopwatch）
- 详细的错误提示

**设计模式**:
- **Command Pattern**: 实现 `[CommandMethod]` 属性
- **Dependency Injection**: 通过 ServiceLocator 获取服务
- **Template Method**: 定义固定的执行流程

### 2. Domain Layer (领域层)

#### 2.1 服务接口

**文件**: `Domain/Interfaces/IGeospatialService.cs`

```csharp
public interface IGeospatialService
{
    // 值对象
    public struct WorldFileParameters { ... }
    
    // 核心方法
    WorldFileParameters ParseWorldFile(string tfwFilePath);
    Polygon2D CalculateImageBoundary(WorldFileParameters parameters, 
                                     int imageWidth, int imageHeight);
    Point2D PixelToGeo(WorldFileParameters parameters, 
                       double pixelX, double pixelY);
    Point2D GeoToPixel(WorldFileParameters parameters, 
                       double geoX, double geoY);
}
```

**设计原则**:
- **Interface Segregation Principle**: 接口只包含必要的方法
- **Dependency Inversion Principle**: 依赖于抽象而非具体实现

#### 2.2 值对象

**WorldFileParameters (struct)**

```csharp
public struct WorldFileParameters
{
    public double PixelSizeX { get; set; }      // 像素 X 大小
    public double RotationX { get; set; }       // 旋转参数 X
    public double RotationY { get; set; }       // 旋转参数 Y
    public double PixelSizeY { get; set; }      // 像素 Y 大小
    public double OriginX { get; set; }         // 原点 X
    public double OriginY { get; set; }         // 原点 Y
    public bool IsValid => PixelSizeX != 0 && PixelSizeY != 0;
}
```

**特点**:
- 值类型（struct）：不可变性
- 自验证：`IsValid` 属性
- 封装了 TFW 文件的所有参数

**Polygon2D (class)**

```csharp
public class Polygon2D
{
    public IReadOnlyList<Point2D> Vertices { get; }
    public bool IsClosed { get; }
    
    public Polygon2D(IEnumerable<Point2D> vertices, bool isClosed = true);
    public BoundingBox GetBoundingBox();
    public Point2D GetCentroid();
}
```

**特点**:
- 不可变集合（IReadOnlyList）
- 支持闭合/开放多边形
- 提供几何计算方法

**Point2D (class)**

```csharp
public class Point2D
{
    public double X { get; }
    public double Y { get; }
    
    public Point2D(double x, double y);
}
```

**特点**:
- 简单的二维点表示
- 平台无关（不依赖 AutoCAD）

### 3. Infrastructure Layer (基础设施层)

**文件**: `Domain/Services/GeospatialService.cs`

**职责**:
- 实现 `IGeospatialService` 接口
- 执行坐标转换算法
- 执行几何计算

**核心算法**:

#### 3.1 TFW 文件解析

```csharp
public WorldFileParameters ParseWorldFile(string tfwFilePath)
{
    // 1. 验证文件存在
    // 2. 读取 6 行参数
    // 3. 解析为 double 类型
    // 4. 返回 WorldFileParameters 结构
}
```

**错误处理**:
- `FileNotFoundException`: 文件不存在
- `FormatException`: 参数行数不足或格式错误

#### 3.2 图像边界计算

```csharp
public Polygon2D CalculateImageBoundary(WorldFileParameters parameters, 
                                        int imageWidth, int imageHeight)
{
    // 计算四个角点的地理坐标
    var topLeft = new Point2D(parameters.OriginX, parameters.OriginY);
    var topRight = PixelToGeo(parameters, imageWidth, 0);
    var bottomRight = PixelToGeo(parameters, imageWidth, imageHeight);
    var bottomLeft = PixelToGeo(parameters, 0, imageHeight);
    
    return new Polygon2D(new[] { topLeft, topRight, bottomRight, bottomLeft });
}
```

#### 3.3 坐标转换 - 像素到地理

**仿射变换公式**:

$$X_{geo} = OriginX + PixelX \times PixelSizeX + PixelY \times RotationX$$

$$Y_{geo} = OriginY + PixelX \times RotationY + PixelY \times PixelSizeY$$

```csharp
public Point2D PixelToGeo(WorldFileParameters parameters, 
                          double pixelX, double pixelY)
{
    double geoX = parameters.OriginX + pixelX * parameters.PixelSizeX 
                  + pixelY * parameters.RotationX;
    double geoY = parameters.OriginY + pixelX * parameters.RotationY 
                  + pixelY * parameters.PixelSizeY;
    
    return new Point2D(geoX, geoY);
}
```

#### 3.4 坐标转换 - 地理到像素

**逆仿射变换公式**:

$$det = PixelSizeX \times PixelSizeY - RotationX \times RotationY$$

$$PixelX = \frac{(X_{geo} - OriginX) \times PixelSizeY - (Y_{geo} - OriginY) \times RotationX}{det}$$

$$PixelY = \frac{(Y_{geo} - OriginY) \times PixelSizeX - (X_{geo} - OriginX) \times RotationY}{det}$$

```csharp
public Point2D GeoToPixel(WorldFileParameters parameters, 
                          double geoX, double geoY)
{
    double det = parameters.PixelSizeX * parameters.PixelSizeY 
                 - parameters.RotationX * parameters.RotationY;
    
    if (Math.Abs(det) < 1e-10)
        throw new InvalidOperationException("变换矩阵不可逆");
    
    double pixelX = ((geoX - parameters.OriginX) * parameters.PixelSizeY 
                     - (geoY - parameters.OriginY) * parameters.RotationX) / det;
    double pixelY = ((geoY - parameters.OriginY) * parameters.PixelSizeX 
                     - (geoX - parameters.OriginX) * parameters.RotationY) / det;
    
    return new Point2D(pixelX, pixelY);
}
```

## SOLID 原则应用

### 1. Single Responsibility Principle (SRP)

**应用**:
- `LocateTifCommand`: 仅负责命令执行和用户交互
- `GeospatialService`: 仅负责地理空间计算
- `Polygon2D`: 仅表示多边形几何

**好处**: 代码易于理解、测试和维护

### 2. Open/Closed Principle (OCP)

**应用**:
- 通过 `IGeospatialService` 接口定义扩展点
- 新的坐标系统可以通过实现新的服务而不修改现有代码

**好处**: 支持扩展而不修改现有代码

### 3. Liskov Substitution Principle (LSP)

**应用**:
- `GeospatialService` 可以替代 `IGeospatialService`
- 任何实现 `IGeospatialService` 的类都可以互换使用

**好处**: 支持多态和依赖注入

### 4. Interface Segregation Principle (ISP)

**应用**:
- `IGeospatialService` 只包含必要的方法
- 不强制客户端依赖不需要的方法

**好处**: 接口简洁，易于实现

### 5. Dependency Inversion Principle (DIP)

**应用**:
- `LocateTifCommand` 依赖于 `IGeospatialService` 抽象
- 通过 ServiceLocator 进行依赖注入

**好处**: 解耦，便于测试和替换实现

## 设计模式

### 1. Command Pattern

**实现**:
```csharp
[CommandMethod("HYLOCATETIF")]
public void Execute()
{
    // 命令逻辑
}
```

**优点**: 将命令作为对象，支持队列、日志、撤销等操作

### 2. Service Locator Pattern

**实现**:
```csharp
_geospatialService = ServiceLocator.Resolve<IGeospatialService>();
```

**优点**: 集中管理依赖，便于配置和替换

### 3. Value Object Pattern

**实现**:
- `WorldFileParameters` (struct)
- `Point2D` (class)
- `Polygon2D` (class)

**优点**: 表示不可变的值，支持值比较

### 4. Template Method Pattern

**实现**: `Execute()` 方法定义固定的执行流程

**优点**: 确保流程一致，易于扩展

## 依赖关系图

```
LocateTifCommand
    │
    ├─→ IGeospatialService (interface)
    │       │
    │       └─→ GeospatialService (implementation)
    │           │
    │           ├─→ Polygon2D
    │           ├─→ Point2D
    │           └─→ WorldFileParameters
    │
    ├─→ AutoCAD.DatabaseServices
    │   ├─→ Database
    │   ├─→ Transaction
    │   ├─→ Polyline
    │   └─→ MText
    │
    └─→ System.Drawing
        └─→ Image
```

## 扩展点

### 1. 添加新的坐标系统

**步骤**:
1. 创建新的服务实现 `IGeospatialService`
2. 在 Autofac 容器中注册
3. 在 `LocateTifCommand` 中选择合适的服务

### 2. 添加新的输出格式

**步骤**:
1. 创建新的输出接口 `IImageBoundaryRenderer`
2. 实现不同的渲染器（CAD、GIS、Web 等）
3. 在命令中调用合适的渲染器

### 3. 添加批量处理

**步骤**:
1. 创建 `BatchLocateTifCommand`
2. 循环调用 `IGeospatialService` 处理多个文件
3. 支持进度报告和错误恢复

## 性能优化

### 1. 避免重复计算

- 缓存 `WorldFileParameters` 的验证结果
- 缓存坐标转换矩阵的行列式

### 2. 最小化内存占用

- 不加载完整的 TIF 像素数据，仅读取元数据
- 使用 `IReadOnlyList` 避免不必要的复制

### 3. 并行处理

- 支持批量加载多个图像（使用 Parallel.ForEach）

## 测试策略

### 单元测试

**测试对象**: `GeospatialService`

```csharp
[TestClass]
public class GeospatialServiceTests
{
    [TestMethod]
    public void ParseWorldFile_ValidFile_ReturnsCorrectParameters() { }
    
    [TestMethod]
    public void PixelToGeo_StandardParameters_ReturnsCorrectCoordinates() { }
    
    [TestMethod]
    public void GeoToPixel_ReverseTransform_ReturnsOriginalPixelCoordinates() { }
    
    [TestMethod]
    public void CalculateImageBoundary_ValidParameters_ReturnsFourVertices() { }
}
```

### 集成测试

**测试对象**: `LocateTifCommand`

```csharp
[TestClass]
public class LocateTifCommandTests
{
    [TestMethod]
    public void Execute_ValidTfwAndTifFiles_CreatesPolylineAndMText() { }
    
    [TestMethod]
    public void Execute_MissingTifFile_DisplaysErrorMessage() { }
    
    [TestMethod]
    public void Execute_InvalidTfwFormat_DisplaysFormatError() { }
}
```

## 文件结构

```
HyCADTool.Refactored/
├── Presentation/
│   └── Commands/
│       └── LocateTifCommand.cs
├── Domain/
│   ├── Interfaces/
│   │   └── IGeospatialService.cs
│   ├── Services/
│   │   └── GeospatialService.cs
│   └── ValueObjects/
│       └── Geometry/
│           ├── Point2D.cs
│           └── Polygon2D.cs
└── Docs/
    ├── HYLOCATETIF_COMMAND.md
    ├── HYLOCATETIF_TEST_GUIDE.md
    └── HYLOCATETIF_ARCHITECTURE.md
```

## 配置和依赖注入

**Autofac 注册** (在 `AutofacModule.cs` 中):

```csharp
builder.RegisterType<GeospatialService>()
    .As<IGeospatialService>()
    .SingleInstance();
```

## 版本历史

| 版本 | 日期 | 描述 |
|-----|------|------|
| 1.0 | 2024-12-01 | 初始版本，支持基本的 TFW 解析和图像定位 |

## 参考资源

- [AutoCAD .NET API Documentation](https://help.autodesk.com/view/ACDNNET/2024/ENU/)
- [Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [Domain-Driven Design](https://www.domainlanguage.com/ddd/)
- [SOLID Principles](https://en.wikipedia.org/wiki/SOLID)
- [Affine Transformation](https://en.wikipedia.org/wiki/Affine_transformation)

---

**最后更新**: 2024-12-01
**版本**: 1.0
**作者**: HyCADTool 开发团队
