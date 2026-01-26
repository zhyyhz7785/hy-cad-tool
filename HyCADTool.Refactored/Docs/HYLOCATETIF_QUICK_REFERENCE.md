# HYLOCATETIF 快速参考卡片

## 命令基本信息

| 项目 | 值 |
|-----|-----|
| **命令名称** | `HYLOCATETIF` |
| **命令类** | `LocateTifCommand` |
| **文件位置** | `Presentation/Commands/LocateTifCommand.cs` |
| **功能** | 读取 TFW 文件，自动定位 TIF 图像 |
| **快捷键** | 无 |
| **别名** | 无 |

## 快速使用

### 步骤 1: 启动命令
```
在 AutoCAD 命令行输入: HYLOCATETIF
按 Enter 键
```

### 步骤 2: 选择文件
```
选择 TFW 文件（Tiff World File）
支持的扩展名: .tfw
```

### 步骤 3: 查看结果
```
✓ 红色多段线: 图像边界
✓ 绿色文本: 图像信息
✓ 命令行: 执行统计信息
```

## TFW 文件格式速查

```
Line 1: PixelSizeX      (米/像素，X 方向)
Line 2: RotationX       (旋转参数，通常为 0)
Line 3: RotationY       (旋转参数，通常为 0)
Line 4: PixelSizeY      (米/像素，Y 方向，通常为负)
Line 5: OriginX         (左上角 X 坐标)
Line 6: OriginY         (左上角 Y 坐标)
```

### 示例 TFW 文件

```
1.0
0.0
0.0
-1.0
500000.0
2000000.0
```

## 坐标转换公式速查

### 像素 → 地理坐标

$$X_{geo} = OriginX + PixelX \times PixelSizeX + PixelY \times RotationX$$

$$Y_{geo} = OriginY + PixelX \times RotationY + PixelY \times PixelSizeY$$

### 地理 → 像素坐标

$$det = PixelSizeX \times PixelSizeY - RotationX \times RotationY$$

$$PixelX = \frac{(X_{geo} - OriginX) \times PixelSizeY - (Y_{geo} - OriginY) \times RotationX}{det}$$

$$PixelY = \frac{(Y_{geo} - OriginY) \times PixelSizeX - (X_{geo} - OriginX) \times RotationY}{det}$$

## 支持的文件格式

| 文件类型 | 扩展名 | 说明 |
|---------|------|------|
| 世界文件 | `.tfw` | 必需，包含 6 行参数 |
| 栅格图像 | `.tif` | 与 TFW 同名 |
| 栅格图像 | `.tiff` | 与 TFW 同名 |
| 栅格图像 | `.TIF` | 与 TFW 同名（大写） |
| 栅格图像 | `.TIFF` | 与 TFW 同名（大写） |

## 输出对象

### 多段线 (Polyline)
- **图层**: `00_Hy_图像定位`
- **颜色**: 红色 (ColorIndex = 1)
- **顶点**: 4 个（图像四个角点）
- **闭合**: 是

### 文本标注 (MText)
- **图层**: `00_Hy_图像定位`
- **颜色**: 绿色 (ColorIndex = 3)
- **位置**: 图像中心
- **内容**: 图像名称 + 尺寸

## 错误信息速查

| 错误信息 | 原因 | 解决方案 |
|---------|------|--------|
| 操作已取消。 | 用户取消文件选择 | 重新执行命令 |
| TFW文件不存在: [路径] | 文件路径错误或文件已删除 | 检查文件路径 |
| TFW文件格式错误: 需要6行参数，实际[N]行 | TFW 文件行数不足 | 检查 TFW 文件内容 |
| TFW文件参数无效。 | 参数值为 0 或无效 | 检查 TFW 文件参数 |
| 未找到对应的TIF文件: [路径] | TIF 文件不存在 | 创建对应的 TIF 文件 |
| 读取TIF文件失败: [错误信息] | TIF 文件损坏或格式不支持 | 检查 TIF 文件完整性 |
| 计算图像边界失败: [错误信息] | 参数计算异常 | 检查 TFW 参数有效性 |

## 常用快捷操作

### 快速创建测试 TFW 文件

**Python 脚本**:
```python
# 创建标准 TFW 文件
with open('test.tfw', 'w') as f:
    f.write('1.0\n')      # PixelSizeX
    f.write('0.0\n')      # RotationX
    f.write('0.0\n')      # RotationY
    f.write('-1.0\n')     # PixelSizeY
    f.write('500000.0\n') # OriginX
    f.write('2000000.0\n')# OriginY
```

### 快速创建测试 TIF 文件

**Python 脚本**:
```python
from PIL import Image

# 创建 1000x1000 的红色 TIF 图像
img = Image.new('RGB', (1000, 1000), color='red')
img.save('test.tif')
```

## 性能指标

| 操作 | 目标时间 | 备注 |
|-----|--------|------|
| 标准图像加载 | < 200ms | 1000×1000 像素 |
| 高分辨率加载 | < 500ms | 2000×2000 像素 |
| 大尺寸加载 | < 2s | 10000×10000 像素 |
| 内存占用 | < 50MB | 峰值 |

## 核心类和接口

### LocateTifCommand
```csharp
[CommandMethod("HYLOCATETIF")]
public void Execute() { }
```

### IGeospatialService
```csharp
public interface IGeospatialService
{
    WorldFileParameters ParseWorldFile(string tfwFilePath);
    Polygon2D CalculateImageBoundary(WorldFileParameters parameters, 
                                     int imageWidth, int imageHeight);
    Point2D PixelToGeo(WorldFileParameters parameters, 
                       double pixelX, double pixelY);
    Point2D GeoToPixel(WorldFileParameters parameters, 
                       double geoX, double geoY);
}
```

### WorldFileParameters
```csharp
public struct WorldFileParameters
{
    public double PixelSizeX { get; set; }
    public double RotationX { get; set; }
    public double RotationY { get; set; }
    public double PixelSizeY { get; set; }
    public double OriginX { get; set; }
    public double OriginY { get; set; }
    public bool IsValid => PixelSizeX != 0 && PixelSizeY != 0;
}
```

## 依赖关系

```
LocateTifCommand
    ↓
IGeospatialService
    ↓
GeospatialService
    ↓
Polygon2D, Point2D, WorldFileParameters
```

## 文件位置速查

| 文件 | 路径 |
|-----|------|
| 命令实现 | `Presentation/Commands/LocateTifCommand.cs` |
| 服务接口 | `Domain/Interfaces/IGeospatialService.cs` |
| 服务实现 | `Domain/Services/GeospatialService.cs` |
| 多边形类 | `Domain/ValueObjects/Geometry/Polygon2D.cs` |
| 点类 | `Domain/ValueObjects/Geometry/Point2D.cs` |
| 命令文档 | `Docs/HYLOCATETIF_COMMAND.md` |
| 测试指南 | `Docs/HYLOCATETIF_TEST_GUIDE.md` |
| 架构文档 | `Docs/HYLOCATETIF_ARCHITECTURE.md` |

## 调试技巧

### 启用详细日志
在 `LocateTifCommand.Execute()` 中添加:
```csharp
ed.WriteMessage($"\nDEBUG: TFW 参数: {parameters}");
ed.WriteMessage($"\nDEBUG: 图像尺寸: {imageSize}");
ed.WriteMessage($"\nDEBUG: 边界: {boundary}");
```

### 验证坐标转换
```csharp
// 测试像素到地理坐标转换
var geoPoint = _geospatialService.PixelToGeo(parameters, 0, 0);
ed.WriteMessage($"\nDEBUG: 左上角地理坐标: ({geoPoint.X}, {geoPoint.Y})");

// 应该等于 (OriginX, OriginY)
```

### 检查图层创建
在 AutoCAD 中打开图层管理器，验证 `00_Hy_图像定位` 图层是否存在。

## 常见问题快速答案

| 问题 | 答案 |
|-----|------|
| 如何修改图层名称？ | 编辑 `DrawImageBoundary()` 方法中的 `layerName` 变量 |
| 如何修改颜色？ | 编辑 `ColorIndex` 参数（1=红, 3=绿, 5=蓝 等） |
| 如何支持其他图像格式？ | 修改 `GetCorrespondingTifFile()` 方法 |
| 如何批量处理多个文件？ | 创建 `BatchLocateTifCommand` 类 |
| 如何导出坐标数据？ | 在 `DrawImageBoundary()` 后添加导出逻辑 |

## 扩展开发

### 添加新的坐标系统
1. 创建新的 `IGeospatialService` 实现
2. 在 Autofac 中注册
3. 在命令中选择合适的服务

### 添加批量处理
1. 创建 `BatchLocateTifCommand`
2. 循环调用 `IGeospatialService`
3. 支持进度报告

### 添加 UI 配置
1. 创建 WPF 对话框
2. 配置图层、颜色、文字大小
3. 保存配置到 config.json

## 相关命令

| 命令 | 功能 |
|-----|------|
| `HYLOCATETIF` | 定位单个 TIF 图像 |
| `LAYER` | 管理图层 |
| `ERASE` | 删除绘制的边界 |
| `ZOOM` | 缩放到图像范围 |

## 快速参考链接

- [完整文档](HYLOCATETIF_COMMAND.md)
- [测试指南](HYLOCATETIF_TEST_GUIDE.md)
- [架构设计](HYLOCATETIF_ARCHITECTURE.md)
- [AutoCAD API 文档](https://help.autodesk.com/view/ACDNNET/2024/ENU/)

---

**最后更新**: 2024-12-01
**版本**: 1.0
**快速参考卡片**
