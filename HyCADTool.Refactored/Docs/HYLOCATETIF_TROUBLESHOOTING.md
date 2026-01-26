# HYLOCATETIF 命令 - 故障排查指南

## 问题 1: 命令不可用 (输入 HYLOCATETIF 无反应)

### 症状
- 在 AutoCAD 命令行输入 `HYLOCATETIF` 后无任何反应
- 命令行显示 "未知命令" 或类似错误
- 命令不在自动完成列表中

### 根本原因
**AutoCAD 无法发现命令类**。这通常是因为：
1. 命令类没有被 `[CommandClass]` 属性注册
2. DLL 没有正确加载
3. 命令类不在正确的命名空间中

### 解决方案

#### 方案 A: 添加 CommandClass 属性 (推荐)

在 `LocateTifCommand.cs` 文件顶部添加：

```csharp
[assembly: CommandClass(typeof(HyCADTool.Refactored.Presentation.Commands.LocateTifCommand))]
```

**完整示例**:
```csharp
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Infrastructure.Configuration;
using System;
using System.Diagnostics;
using System.IO;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(HyCADTool.Refactored.Presentation.Commands.LocateTifCommand))]

namespace HyCADTool.Refactored.Presentation.Commands
{
    public class LocateTifCommand
    {
        // ... 类实现 ...
    }
}
```

#### 方案 B: 验证 DLL 加载

1. **检查 DLL 是否加载**
   ```
   在 AutoCAD 命令行输入: NETLOAD
   选择 HyCADTool.Refactored.dll
   ```

2. **查看初始化消息**
   - 如果 DLL 正确加载，应该看到初始化消息
   - 查看是否有错误信息

3. **检查依赖 DLL**
   - 确保所有依赖 DLL 都在同一目录
   - 依赖包括: Autofac.dll, NetTopologySuite.dll, Newtonsoft.Json.dll

#### 方案 C: 验证命令类配置

检查 `LocateTifCommand` 类是否满足以下条件：

```csharp
// ✅ 正确的配置
[CommandMethod("HYLOCATETIF")]
public void Execute()
{
    // 命令实现
}

// ❌ 错误的配置
// 1. 方法不是 public
private void Execute() { }

// 2. 方法没有 [CommandMethod] 属性
public void Execute() { }

// 3. 方法有参数
public void Execute(string param) { }

// 4. 方法返回值不是 void
public int Execute() { }
```

### 验证步骤

1. **重新编译项目**
   ```bash
   dotnet clean
   dotnet build -c Release
   ```

2. **重新加载 DLL**
   ```
   关闭 AutoCAD
   删除旧的 DLL
   复制新的 DLL
   重启 AutoCAD
   使用 NETLOAD 加载 DLL
   ```

3. **测试命令**
   ```
   在命令行输入: HYLOCATETIF
   应该看到文件选择对话框
   ```

## 问题 2: DLL 加载失败

### 症状
- NETLOAD 时出现错误
- 错误信息: "无法加载文件或程序集"
- 错误信息: "找不到指定的模块"

### 根本原因
- 依赖 DLL 缺失
- .NET Framework 版本不匹配
- DLL 文件损坏

### 解决方案

#### 检查依赖 DLL

```bash
# 确保以下文件都存在于加载项目录
Autofac.dll
NetTopologySuite.dll
Newtonsoft.Json.dll
Clipper2.dll
```

#### 检查 .NET Framework 版本

```bash
# 项目要求 .NET Framework 4.8
# 检查系统是否安装了 .NET Framework 4.8

# Windows 10/11 通常已预装
# 如果没有，下载安装: https://dotnet.microsoft.com/download/dotnet-framework/net48
```

#### 重新编译 DLL

```bash
# 清理旧的编译输出
dotnet clean

# 重新编译
dotnet build -c Release

# 检查输出
# bin\Release\HyCADTool.Refactored.dll 应该存在
```

## 问题 3: 命令执行失败

### 症状
- 命令可以识别，但执行时出错
- 错误信息显示在命令行
- 文件选择对话框不出现

### 常见错误信息

#### 错误 1: "对象引用未设置为对象的实例"

**原因**: ServiceLocator 未初始化或 IGeospatialService 未注册

**解决方案**:
```csharp
// 检查 PluginInitializer.Initialize() 是否正确执行
// 确保 Autofac 容器正确初始化
// 确保 IGeospatialService 已注册

// 在 AutofacModule.cs 中检查:
builder.RegisterType<GeospatialService>()
    .As<IGeospatialService>()
    .SingleInstance();
```

#### 错误 2: "TFW 文件不存在"

**原因**: 文件路径错误或文件已删除

**解决方案**:
```
1. 确保 TFW 文件存在
2. 检查文件路径中没有特殊字符
3. 检查文件权限（可读）
```

#### 错误 3: "TFW 文件格式错误"

**原因**: TFW 文件内容不符合格式要求

**解决方案**:
```
TFW 文件必须包含 6 行参数：
Line 1: PixelSizeX (数字)
Line 2: RotationX (数字)
Line 3: RotationY (数字)
Line 4: PixelSizeY (数字)
Line 5: OriginX (数字)
Line 6: OriginY (数字)

示例:
1.0
0.0
0.0
-1.0
500000.0
2000000.0
```

#### 错误 4: "未找到对应的 TIF 文件"

**原因**: TIF 文件不存在或扩展名不匹配

**解决方案**:
```
1. 确保 TIF 文件与 TFW 文件同名
2. 检查支持的扩展名: .tif, .tiff, .TIF, .TIFF
3. 确保 TIF 文件在同一目录
```

## 问题 4: 性能问题

### 症状
- 命令执行很慢
- AutoCAD 无响应
- 内存占用很高

### 根本原因
- TIF 文件过大
- 系统资源不足
- 磁盘 I/O 缓慢

### 解决方案

#### 检查 TIF 文件大小

```bash
# TIF 文件大小应该 < 100MB
# 如果过大，考虑压缩或分割

# 检查文件大小
dir /s *.tif
```

#### 检查系统资源

```bash
# 打开任务管理器
# 检查:
# - CPU 使用率
# - 内存使用率
# - 磁盘使用率
```

#### 优化建议

1. **使用 SSD**: 磁盘 I/O 更快
2. **增加内存**: 至少 8GB RAM
3. **关闭其他应用**: 释放系统资源
4. **使用较小的 TIF 文件**: 用于测试

## 问题 5: 图像边界不正确

### 症状
- 绘制的多段线位置错误
- 坐标计算不对
- 图像边界超出预期范围

### 根本原因
- TFW 参数错误
- 坐标系统不匹配
- 旋转参数设置不当

### 解决方案

#### 验证 TFW 参数

```bash
# 检查 TFW 文件参数是否正确
# 特别是:
# - PixelSizeX 和 PixelSizeY 的符号
# - OriginX 和 OriginY 的值
# - RotationX 和 RotationY 是否为 0
```

#### 手动计算验证

```
已知:
- TFW 参数: PixelSizeX=1.0, PixelSizeY=-1.0, OriginX=500000, OriginY=2000000
- TIF 尺寸: 1000x1000 像素

计算四个角点:
- 左上角 (0, 0): (500000, 2000000)
- 右上角 (1000, 0): (501000, 2000000)
- 右下角 (1000, 1000): (501000, 1999000)
- 左下角 (0, 1000): (500000, 1999000)

对比 AutoCAD 中的结果是否一致
```

#### 调试建议

在 `LocateTifCommand.cs` 中添加调试输出：

```csharp
// 添加到 Execute() 方法
ed.WriteMessage($"\nDEBUG: TFW 参数");
ed.WriteMessage($"\n  PixelSizeX: {parameters.PixelSizeX}");
ed.WriteMessage($"\n  PixelSizeY: {parameters.PixelSizeY}");
ed.WriteMessage($"\n  OriginX: {parameters.OriginX}");
ed.WriteMessage($"\n  OriginY: {parameters.OriginY}");
ed.WriteMessage($"\n  RotationX: {parameters.RotationX}");
ed.WriteMessage($"\n  RotationY: {parameters.RotationY}");

ed.WriteMessage($"\nDEBUG: 图像尺寸: {imageSize.Width} x {imageSize.Height}");

ed.WriteMessage($"\nDEBUG: 边界顶点");
foreach (var vertex in boundary.Vertices)
{
    ed.WriteMessage($"\n  ({vertex.X}, {vertex.Y})");
}
```

## 问题 6: 文件对话框不出现

### 症状
- 输入命令后没有文件选择对话框
- 命令行显示 "操作已取消"
- 没有任何错误信息

### 根本原因
- 文件对话框创建失败
- AutoCAD 文件对话框不可用
- 系统对话框被禁用

### 解决方案

#### 检查对话框代码

```csharp
// 检查 SelectTfwFile() 方法
private string SelectTfwFile(Editor ed)
{
    try
    {
        // 尝试 AutoCAD 文件对话框
        var fileDialog = new Autodesk.AutoCAD.Windows.OpenFileDialog(
            "选择TFW文件",
            null,
            "tfw",
            "TFW Files (*.tfw)|*.tfw",
            Autodesk.AutoCAD.Windows.OpenFileDialog.OpenFileDialogFlags.NoUrls
        );

        if (fileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            return fileDialog.Filename;
        }
    }
    catch
    {
        // 如果失败，尝试系统对话框
        var openFileDialog = new System.Windows.Forms.OpenFileDialog
        {
            Title = "选择TFW文件",
            Filter = "TFW Files (*.tfw)|*.tfw|All Files (*.*)|*.*",
            CheckFileExists = true,
            CheckPathExists = true
        };

        if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            return openFileDialog.FileName;
        }
    }

    return null;
}
```

#### 启用 WPF/WinForms

确保项目文件包含：

```xml
<UseWPF>true</UseWPF>
<UseWindowsForms>true</UseWindowsForms>
```

## 快速诊断清单

当遇到问题时，按以下顺序检查：

- [ ] **DLL 是否加载?**
  ```
  NETLOAD HyCADTool.Refactored.dll
  查看初始化消息
  ```

- [ ] **命令是否注册?**
  ```
  输入: HYLOCATETIF
  检查是否有反应
  ```

- [ ] **依赖 DLL 是否存在?**
  ```
  检查加载项目录中的所有 DLL
  ```

- [ ] **TFW 文件格式是否正确?**
  ```
  检查文件是否有 6 行参数
  ```

- [ ] **TIF 文件是否存在?**
  ```
  检查同名的 TIF 文件
  ```

- [ ] **错误信息是什么?**
  ```
  查看 AutoCAD 命令行的完整错误信息
  ```

## 获取帮助

### 收集诊断信息

当报告问题时，请提供：

1. **AutoCAD 版本**
   ```
   ABOUT 命令查看版本
   ```

2. **.NET Framework 版本**
   ```
   控制面板 > 程序 > 程序和功能
   ```

3. **完整的错误信息**
   ```
   复制 AutoCAD 命令行的所有输出
   ```

4. **DLL 加载日志**
   ```
   NETLOAD 时的完整输出
   ```

5. **TFW 文件内容**
   ```
   提供示例 TFW 文件
   ```

### 调试模式

启用详细日志：

```csharp
// 在 LocateTifCommand.cs 中
ed.WriteMessage("\n[DEBUG] 开始执行 HYLOCATETIF 命令");
ed.WriteMessage($"\n[DEBUG] 选择文件: {tfwFilePath}");
ed.WriteMessage($"\n[DEBUG] 参数有效性: {parameters.IsValid}");
ed.WriteMessage($"\n[DEBUG] 执行完成");
```

## 常见问题速查表

| 问题 | 症状 | 解决方案 |
|-----|------|--------|
| CommandClass 未注册 | 命令不可用 | 添加 `[assembly: CommandClass(...)]` |
| DLL 未加载 | 命令不可用 | 使用 NETLOAD 加载 DLL |
| 依赖 DLL 缺失 | DLL 加载失败 | 复制所有依赖 DLL |
| ServiceLocator 未初始化 | 对象引用异常 | 检查 PluginInitializer 初始化 |
| TFW 文件格式错误 | 格式错误提示 | 检查 TFW 文件有 6 行参数 |
| TIF 文件不存在 | 文件不存在提示 | 创建同名的 TIF 文件 |
| 坐标计算错误 | 边界位置不对 | 验证 TFW 参数 |
| 性能缓慢 | 执行时间长 | 检查 TIF 文件大小和系统资源 |

---

**最后更新**: 2024-12-01  
**版本**: 1.0  
**状态**: 完成
