# NuGet 包源错误修复说明

## 🐛 遇到的问题

**错误信息**:
```
无法加载源 https://pkgs.dev.azure.com/isml/FE%20Software/_packaging/msolve/nuget/v3/index.json 的服务索引。
响应状态代码不指示成功: 404 (Not Found)。
```

**原因**: 
Visual Studio 的全局 NuGet 配置中存在一个无效的 Azure DevOps 私有包源，该源已不可访问或需要身份验证。

---

## ✅ 解决方案

已在解决方案根目录创建 `NuGet.Config` 文件，配置为只使用官方 NuGet 源。

### 创建的文件内容

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <!-- 清除所有包源，只使用官方源 -->
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
```

---

## 🔧 下一步操作

### 在 Visual Studio 中：

1. **关闭解决方案**（如果已打开）
2. **重新打开解决方案** `HyCADtoolGpt.sln`
3. **还原 NuGet 包**：
   - 右键点击解决方案 → **还原 NuGet 包**
   - 或者在菜单：**工具 → NuGet 包管理器 → 程序包管理器控制台**
   - 运行命令：`Update-Package -Reinstall`

4. **重新生成解决方案**：
   - 按 `Ctrl + Shift + B`
   - 或菜单：**生成 → 重新生成解决方案**

---

## 📊 预期结果

还原 NuGet 包后，应该看到：

```
正在还原 HyCADTool.Refactored 的 NuGet 包...
  已还原 AutoCAD.NET 24.3.0
  已还原 Autofac 7.1.0
  已还原 Clipper2 1.4.0
  已还原 NetTopologySuite 2.5.0
  已还原 Newtonsoft.Json 13.0.3
  
正在还原 ReCall 的 NuGet 包...
  已还原 AutoCAD.NET 24.3.0
  
正在还原 HyCADTool 的 NuGet 包...
  [已有包]
  
NuGet 包还原已完成
```

---

## 🔍 如果问题仍然存在

### 方法 1: 清除 NuGet 缓存

在 **程序包管理器控制台** 中运行：
```powershell
dotnet nuget locals all --clear
```

或使用 NuGet CLI：
```powershell
nuget locals all -clear
```

### 方法 2: 手动编辑全局 NuGet 配置

如果本地 `NuGet.Config` 不起作用，需要修改全局配置：

**位置**: `%AppData%\NuGet\NuGet.Config`

打开该文件，找到并删除或注释掉无效的包源：

```xml
<!-- 删除或注释这一行 -->
<!-- <add key="msolve" value="https://pkgs.dev.azure.com/..." /> -->
```

### 方法 3: 使用 Visual Studio 包管理器 UI

1. **工具 → NuGet 包管理器 → 程序包管理器设置**
2. 选择 **包源**
3. 找到 `msolve` 或 Azure DevOps 相关的源
4. **取消勾选** 或 **删除** 该源
5. 点击 **确定**

---

## ✅ 验证修复

运行以下命令确认包源配置：

**在程序包管理器控制台**:
```powershell
dotnet nuget list source
```

**预期输出**:
```
已注册的源:
  1. nuget.org [已启用]
     https://api.nuget.org/v3/index.json
```

---

## 📝 相关资源

- [NuGet 配置文件参考](https://docs.microsoft.com/zh-cn/nuget/reference/nuget-config-file)
- [NuGet 包源管理](https://docs.microsoft.com/zh-cn/nuget/consume-packages/configuring-nuget-behavior)

---

**修复时间**: 2025-01-08  
**状态**: ✅ 已创建本地配置文件  
**下一步**: 重新打开解决方案并还原 NuGet 包


