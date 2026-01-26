# HYLOCATETIF 命令文档

## 概述

**HYLOCATETIF** 是 HyCADTool.Refactored 插件中的一个核心命令，用于读取 TFW（Tiff World File）文件并自动定位对应的 TIF 地理图像在 AutoCAD 中的空间位置。

## 命令调用

```
命令名称: HYLOCATETIF
快捷键: 无
别名: 无
```

在 AutoCAD 命令行输入 `HYLOCATETIF` 并按 Enter 执行。

## 功能流程

```
┌─────────────────────────────────────────────────────────────┐
│ 1. 用户选择 TFW 文件                                        │
│    (支持 AutoCAD 文件对话框和系统对话框)                   │
└──────────────────┬──────────────────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────────────────┐
│ 2. 解析 TFW 文件                                            │
│    (提取 6 行世界文件参数)                                  │
└──────────────────┬──────────────────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────────────────┐
│ 3. 查找对应的 TIF 文件                                      │
│    (支持 .tif, .tiff, .TIF, .TIFF 等扩展名)               │
└──────────────────┬──────────────────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────────────────┐
│ 4. 读取 TIF 图像尺寸                                        │
│    (使用 System.Drawing.Image)                             │
└──────────────────┬──────────────────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────────────────┐
│ 5. 计算图像地理边界                                        │
│    (四个角点的地理坐标)                                    │
└──────────────────┬──────────────────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────────────────┐
│ 6. 在 CAD 中绘制结果                                        │
│    - 红色多段线: 图像边界                                  │
│    - 绿色文本: 图像名称和尺寸                              │
│    - 图层: 00_Hy_图像定位                                  │
└─────────────────────────────────────────────────────────────┘
```

## TFW 文件格式

TFW 文件是一个纯文本文件，包含 6 行参数，用于描述栅格图像与地理坐标系的对应关系。

### 文件结构

```
第 1 行: PixelSizeX      (像素在 X 方向的大小，单位：米/像素)
第 2 行: RotationX       (旋转参数 X，通常为 0)
第 3 行: RotationY       (旋转参数 Y，通常为 0)
第 4 行: PixelSizeY      (像素在 Y 方向的大小，单位：米/像素，通常为负值)
第 5 行: OriginX         (左上角像素的地理 X 坐标)
第 6 行: OriginY         (左上角像素的地理 Y 坐标)
```

### 示例

```
1.5
0.0
0.0
-1.5
500000.0
2000000.0
```

这表示：
- 每个像素代表 1.5 米 × 1.5 米
- 图像未旋转
- 左上角像素对应的地理坐标为 (500000, 2000000)

## 坐标转换原理

### 像素坐标 → 地理坐标

使用仿射变换公式：

$$X_{geo} = OriginX + PixelX \times PixelSizeX + PixelY \times RotationX$$

$$Y_{geo} = OriginY + PixelX \times RotationY + PixelY \times PixelSizeY$$

其中：
- $(PixelX, PixelY)$ 是图像中的像素坐标（从 0 开始）
- $(X_{geo}, Y_{geo})$ 是对应的地理坐标

### 地理坐标 → 像素坐标

使用逆仿射变换：

$$PixelX = \frac{(X_{geo} - OriginX) \times PixelSizeY - (Y_{geo} - OriginY) \times RotationX}{det}$$

$$PixelY = \frac{(Y_{geo} - OriginY) \times PixelSizeX - (X_{geo} - OriginX) \times RotationY}{det}$$

其中：

$$det = PixelSizeX \times PixelSizeY - RotationX \times RotationY$$

## 实现细节

### 核心类

#### LocateTifCommand
- **文件**: `Presentation/Commands/LocateTifCommand.cs`
- **命令方法**: `Execute()`
- **依赖**: `IGeospatialService`

#### GeospatialService
- **文件**: `Domain/Services/GeospatialService.cs`
- **接口**: `Domain/Interfaces/IGeospatialService.cs`
- **职责**: 地理空间计算和坐标转换

### 关键方法

#### ParseWorldFile(string tfwFilePath)
解析 TFW 文件并返回世界文件参数结构。

```csharp
public IGeospatialService.WorldFileParameters ParseWorldFile(string tfwFilePath)
{
    // 读取文件的 6 行参数
    // 返回 WorldFileParameters 结构
}
```

#### CalculateImageBoundary(WorldFileParameters parameters, int imageWidth, int imageHeight)
根据世界文件参数和图像尺寸计算图像的四个角点地理坐标。

```csharp
public Polygon2D CalculateImageBoundary(
    IGeospatialService.WorldFileParameters parameters, 
    int imageWidth, 
    int imageHeight)
{
    // 计算四个角点：
    // - 左上角: (0, 0)
    // - 右上角: (imageWidth, 0)
    // - 右下角: (imageWidth, imageHeight)
    // - 左下角: (0, imageHeight)
    // 返回 Polygon2D 对象
}
```

#### PixelToGeo(WorldFileParameters parameters, double pixelX, double pixelY)
将像素坐标转换为地理坐标。

```csharp
public Point2D PixelToGeo(
    IGeospatialService.WorldFileParameters parameters, 
    double pixelX, 
    double pixelY)
{
    // 使用仿射变换公式
    // 返回地理坐标点
}
```

#### GeoToPixel(WorldFileParameters parameters, double geoX, double geoY)
将地理坐标转换为像素坐标。

```csharp
public Point2D GeoToPixel(
    IGeospatialService.WorldFileParameters parameters, 
    double geoX, 
    double geoY)
{
    // 使用逆仿射变换公式
    // 返回像素坐标点
}
```

## 错误处理

命令包含全面的错误处理机制：

| 错误情况 | 处理方式 | 用户提示 |
|---------|--------|--------|
| 用户取消文件选择 | 正常退出 | "操作已取消。" |
| TFW 文件不存在 | 异常捕获 | "TFW文件不存在: [路径]" |
| TFW 文件格式错误 | 异常捕获 | "TFW文件格式错误: 需要6行参数，实际[N]行" |
| TFW 参数无效 | 参数验证 | "TFW文件参数无效。" |
| TIF 文件不存在 | 文件检查 | "未找到对应的TIF文件: [路径]" |
| TIF 读取失败 | 异常捕获 | "读取TIF文件失败: [错误信息]" |
| 边界计算失败 | 异常捕获 | "计算图像边界失败: [错误信息]" |

## 输出结果

命令执行成功后，会在 CAD 中创建以下对象：

### 1. 图层
- **名称**: `00_Hy_图像定位`
- **颜色**: 红色（ColorIndex = 1）

### 2. 多段线（Polyline）
- **用途**: 表示图像边界
- **颜色**: 红色
- **闭合**: 是
- **顶点**: 4 个（图像的四个角点）

### 3. 多行文本（MText）
- **位置**: 图像中心
- **内容**: 图像名称 + 尺寸（宽 × 高，单位：米）
- **颜色**: 绿色（ColorIndex = 3）
- **文字高度**: 根据图像分辨率自动调整

### 4. 命令行输出
```
图像定位完成: [图像文件名]
  图像尺寸: [宽] x [高] 像素
  地理范围: X:[最小X] - [最大X], Y:[最小Y] - [最大Y]
INFO: 图像定位 耗时 [毫秒] 毫秒
```

## 使用示例

### 场景：定位航拍正射影像

1. **准备文件**
   - 航拍图像: `aerial_photo.tif` (4000 × 3000 像素)
   - 世界文件: `aerial_photo.tfw`

2. **TFW 文件内容**
   ```
   0.5
   0.0
   0.0
   -0.5
   500000.0
   2000000.0
   ```

3. **执行命令**
   - 在 AutoCAD 命令行输入: `HYLOCATETIF`
   - 选择 `aerial_photo.tfw` 文件

4. **结果**
   - 在 CAD 中绘制图像边界（红色多段线）
   - 显示图像信息标注（绿色文本）
   - 图像地理范围: X: 500000.0 - 502000.0, Y: 1998500.0 - 2000000.0

## 性能考虑

- **执行时间**: 通常 < 500ms（取决于 TIF 文件大小）
- **内存占用**: 最小化（仅加载图像元数据，不加载完整像素数据）
- **支持的图像尺寸**: 无限制（理论上）

## 扩展功能建议

1. **批量处理**: 支持一次性加载多个 TFW/TIF 文件
2. **图像显示**: 在 CAD 中显示 TIF 图像缩略图
3. **坐标系统**: 支持不同的地理坐标系统（WGS84、UTM 等）
4. **旋转图像**: 完整支持带旋转的图像（RotationX/Y ≠ 0）
5. **配置界面**: 提供 UI 配置图层颜色、文字大小等参数

## 常见问题

### Q: 如何找到对应的 TIF 文件？
A: 命令首先查找同名的 TIF 文件（支持多种扩展名），如果找不到，会尝试查找目录中唯一的 TIF 文件。

### Q: 支持哪些 TIF 扩展名？
A: 支持 `.tif`, `.tiff`, `.TIF`, `.TIFF`（不区分大小写）。

### Q: 如果 TFW 文件中有旋转参数怎么办？
A: 命令完全支持旋转和倾斜的图像，通过仿射变换公式正确处理。

### Q: 如何修改图层名称或颜色？
A: 编辑 `LocateTifCommand.cs` 中的 `DrawImageBoundary` 方法，修改 `layerName` 和 `ColorIndex` 参数。

### Q: 命令执行失败，如何调试？
A: 查看 AutoCAD 命令行的错误信息，通常会显示具体的失败原因和文件路径。

## 技术栈

- **AutoCAD.NET** 24.3.0 - AutoCAD 二次开发 API
- **Autofac** 7.1.0 - 依赖注入容器
- **System.Drawing** - 图像处理
- **.NET Framework** 4.8 - 运行时环境

## 相关文件

| 文件 | 描述 |
|-----|------|
| `Presentation/Commands/LocateTifCommand.cs` | 命令实现 |
| `Domain/Interfaces/IGeospatialService.cs` | 地理空间服务接口 |
| `Domain/Services/GeospatialService.cs` | 地理空间服务实现 |
| `Domain/ValueObjects/Geometry/Polygon2D.cs` | 二维多边形值对象 |
| `Domain/ValueObjects/Geometry/Point2D.cs` | 二维点值对象 |

## 许可证

本命令是 HyCADTool.Refactored 项目的一部分，遵循项目的许可证。

---

**最后更新**: 2024-12-01
**版本**: 1.0
**作者**: HyCADTool 开发团队
