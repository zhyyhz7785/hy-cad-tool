---
name: hycad-new-command-registration
description: |
  HyCADTool 在 ReCall + C2 热重载下新增 AutoCAD 命令的最小流程：主工程 HyCADTool 写 Execute、
  commands.json 映射 N1~N50、同步到 bin\Debug、AI dotnet build 后 C2、常见坑与占位符清理、何时关 CAD 转正。
  触发：新命令、注册命令、N1~N50、占位符、commands.json、CommandFacade、C2 注册、hyRecall。
author: HyCADTool
version: 2.0.0
date: 2026-06-12
---
# HyCAD 新命令注册（ReCall + N 占位符）

## 0. 必须先编译（最常见「不起作用」原因）

改主工程 `src/HyCADTool` 里任意 `.cs` 后：**AI 自己编译**，再让用户在 AutoCAD 里输 `C2`：

```powershell
dotnet build src\HyCADTool\HyCADTool.csproj -c Debug
```

主工程不引用 ReCall.dll，AutoCAD 开着也能编译（不会锁文件）。若报 `CS2012 obj\...dll 被 MSBuild.exe 锁定` → `dotnet build-server shutdown` 后加 `-nodeReuse:false` 重试。

只保存源码、不编译 → C2 重载的仍是**上一次**的 `HyCADTool.dll` → `N?` 行为不变或仍是旧实现。

---

## 一、四步流程（日常）

```
写命令类 → 映射到 N? → 复制 json 到 bin → AI dotnet build → 用户 C2 → 输 N?
```

| # | 步骤 | 文件 | 要点 |
|---|------|------|------|
| 1 | 写命令类 | `src/HyCADTool/Features/<域>/XxxCommand.cs`（部分域有 `Commands/` 子目录） | `public sealed class` + `public void Execute()`；**禁用 `[CommandMethod]` 与 `[assembly: CommandClass]`**（C2 热重载会 eDuplicateKey） |
| 2 | 映射 N? | `src/ReCall/commands.json` | 找第一个 `null` 的 `"N?"`，改成 `{ "type": "...完全限定名...", "method": "Execute", ... }` |
| 3 | 同步到 bin | `src/ReCall/bin/Debug/commands.json` | `Copy-Item src\ReCall\commands.json src\ReCall\bin\Debug\commands.json -Force` —— 运行时只读 bin 那份 |
| 4 | **AI 编译** → 用户 `C2` → `N?` | — | C2 加载**最新编译产物**；`N?` 经 ReCall 反射调 `Execute()` |

**命令类模板**（命名空间 = `HyCADTool.Features.<域>`，与目录一致）：

```csharp
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.XXX
{
    public sealed class FooCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var db = doc.Database;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                // ... 业务逻辑 ...
                tr.Commit();
            }
        }
    }
}
```

**`commands.json` 条目模板**：

```jsonc
"N4": {
  "type": "HyCADTool.Features.XXX.FooCommand",
  "method": "Execute",
  "category": "杂项",
  "displayName": "Foo 命令(N4)",
  "tooltip": "一句话说明",
  "order": 97
}
```

---

## 二、N1~N50 占位符满了怎么办

**原则**：占位符是**临时调试通道**，用完即清。

1. **已正名**：`CommandFacade` 里已有 `[CommandMethod("hyXxx")]` → 对应 `Nx` 改回 `null`。
2. **已废弃**：类删了 / C2 报 `TypeNotFound` → `Nx` 改回 `null`。
3. **太久不用**：从 N1 起清。
4. 清完 **`Copy-Item` 到 `bin\Debug`**，新命令再从空槽占用。

---

## 三、何时关 CAD「转正」（冷启动，提醒用户）

功能稳定后要**正式命令名**时（改 `ReCall/*.cs` 属冷启动，**必须提醒用户关 AutoCAD**）：

1. **提醒用户**关 AutoCAD  
2. `CommandFacade.cs`：`[CommandMethod("hyXxx")] public void Cmd_hyXxx() => ReCallClass.Invoke("hyXxx");`  
3. `commands.json` 增加 `"hyXxx": { ... }`，原 `Nx` 置 `null`  
4. AutoCAD 关闭后编译 **ReCall**（`dotnet build src\ReCall\ReCall.csproj -c Debug`）  
5. 用户开 CAD → `NETLOAD ReCall.dll`

---

## 四、常见坑

| 症状 | 原因 | 修法 |
|------|------|------|
| C2 后 N? 仍旧逻辑 / 「不起作用」 | 未编译主工程 | AI `dotnet build src\HyCADTool\HyCADTool.csproj -c Debug` → 再 C2 |
| C2 报 `eDuplicateKey` | 主工程里写了 `[CommandMethod]` | 删掉，只走 ReCall 转发 |
| 「占位符尚未分配」 | 只改了源码 `commands.json` | `Copy-Item` 到 `src\ReCall\bin\Debug\commands.json` |
| `TypeNotFound` | `type` 拼错或类已删 | 核对完全限定名；`Nx` → `null` |
| 正式名「未知命令」 | `CommandFacade` 无对应 `[CommandMethod]` | 占位符 N? 或走转正 |

---

## 五、命令类放哪

按业务域放 `src/HyCADTool/Features/<域>/`，命名空间与目录一致：

| 业务域举例 | 目录 / 命名空间 |
|--------|------|
| 道路 | `Features/Road/<子域>/Commands/` → `HyCADTool.Features.Road.*` |
| 钢筋 | `Features/Reinforcement/` → `HyCADTool.Features.Reinforcement` |
| 尺寸标注 | `Features/AcadDimension/Commands/` → `HyCADTool.Features.AcadDimension.Commands` |
| 面板/基础设施 | `Shell/Commands/` → `HyCADTool.Shell.Commands` |

---

**参考**：`src/ReCall/Doc/Recall-说明.md`（三种改动 → 三种流程）。
