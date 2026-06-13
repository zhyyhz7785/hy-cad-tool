# 071 HyCAD.bundle 安装与使用说明

> 2026-06-13 | 发版流程见 `doc/070-客户安装与发版快速路径-2026-06-13-030000.md`

---

## 一、两个「位置」，别搞混

很多人找不到，是因为 **Setup.exe 安装位置** 和 **仓库里打出来的包** 不是同一个地方。

| 是什么 | 在哪里 | 谁用 |
|---|---|---|
| **安装后的插件**（AutoCAD 实际加载） | `%APPDATA%\Autodesk\ApplicationPlugins\HyCAD.bundle` | 客户 / 你安装 Setup 之后 |
| **发版前的构建产物**（还没装） | 仓库 `build\artifacts\HyCAD.bundle` | 仅开发者打包时用 |
| **发给客户的安装程序** | 仓库 `build\artifacts\HyCAD-Setup-x.y.z.exe` | 双击安装，装到上一行 AppData 路径 |

**本机当前用户名下的真实路径示例**（把 `ZHY` 换成你的 Windows 用户名）：

```
C:\Users\ZHY\AppData\Roaming\Autodesk\ApplicationPlugins\HyCAD.bundle
```

**为什么资源管理器里不好找？**

- `AppData\Roaming` 默认隐藏
- 文件夹名叫 `HyCAD.bundle`，不在「程序文件」或桌面
- 安装向导不让你改路径（固定装到 ApplicationPlugins）

**最快打开方式**（任选其一）：

1. 按 `Win + R`，粘贴后回车：
   ```
   %APPDATA%\Autodesk\ApplicationPlugins
   ```
2. 资源管理器地址栏粘贴：
   ```
   %APPDATA%\Autodesk\ApplicationPlugins\HyCAD.bundle
   ```
3. PowerShell：
   ```powershell
   explorer "$env:APPDATA\Autodesk\ApplicationPlugins\HyCAD.bundle"
   ```

若该文件夹**不存在**，说明还没运行过 `HyCAD-Setup-*.exe`，或安装失败。

---

## 二、HyCAD.bundle 目录结构

```
HyCAD.bundle/
├── PackageContents.xml          ← AutoCAD 读这个，决定启动时自动加载哪个 DLL
├── unins000.exe                 ← 卸载程序（Setup 安装后才有）
├── unins000.dat
└── Contents/
    └── Win64/
        ├── HyCADTool.dll        ← 主程序（入口）
        ├── commands.json        ← 命令表 + 许可档位 tier
        ├── HyCADTool.Licensing.dll
        ├── HyCAD.BlenderUI.dll
        ├── …（其它依赖 dll）
        ├── config.json
        ├── Resources/
        └── _libraries/
```

**AutoCAD 如何加载**：启动时扫描 `%APPDATA%\Autodesk\ApplicationPlugins\` 下所有 `*.bundle`，读 `PackageContents.xml` 里 `LoadOnAutoCADStartup=True` 的项，自动加载 `Contents/Win64/HyCADTool.dll`。**不需要每次 NETLOAD**。

支持版本：AutoCAD 2024–2027（R24.0–R27.0，见 `PackageContents.xml`）。

---

## 三、客户安装（推荐：Setup.exe）

1. 双击 **`HyCAD-Setup-x.y.z.exe`**
2. 按 Inno 向导完成（欢迎页说明绿色插件；无需管理员）
3. **完全退出并重启 AutoCAD**
4. 命令行输入 **`hyLicense`** → 复制机器码 → 收到授权码后粘贴激活

License 文件**不在 bundle 里**，单独存放在：

```
C:\ProgramData\HyCAD\license.lic
```

（ `%ProgramData%` = 通常 `C:\ProgramData` ）

---

## 四、手动安装（ZIP / 拷贝文件夹）

若没有 Setup.exe，只有 `HyCAD-vX-bundle.zip`：

1. 解压得到 **`HyCAD.bundle` 文件夹**（注意：必须是这个名字，带 `.bundle` 后缀）
2. 整文件夹复制到：
   ```
   %APPDATA%\Autodesk\ApplicationPlugins\
   ```
   最终路径应为：
   ```
   %APPDATA%\Autodesk\ApplicationPlugins\HyCAD.bundle\PackageContents.xml
   ```
3. 重启 AutoCAD

**不要**只复制 `Contents` 或 `Win64` 里的 dll 到任意目录——AutoCAD ApplicationPlugins 机制要求 **bundle 根目录 + PackageContents.xml** 成套存在。

---

## 五、卸载

**方式 A**（推荐）：再次运行 **`HyCAD-Setup-x.y.z.exe`** → 检测到已安装时选 **「是」卸载**（不删除授权）。

**方式 B**：开始菜单 → **「卸载 HyCAD 插件」**（安装时创建）。

**方式 C**：进入 `%APPDATA%\Autodesk\ApplicationPlugins\HyCAD.bundle`，运行 **`unins000.exe`**。

**方式 D**（手动拷贝安装的）：直接删除整个 **`HyCAD.bundle`** 文件夹。

卸载插件**不会**删除 `%ProgramData%\HyCAD\license.lic`；重装后同一台机器仍可用原授权（机器码未变时）。

---

## 六、开发者：bundle 从哪来

仓库根目录一条命令：

```powershell
powershell -ExecutionPolicy Bypass -File build\scripts\Publish-Setup.ps1 -Version 1.0.0
```

产出：

| 文件 | 用途 |
|---|---|
| `build\artifacts\HyCAD.bundle\` | 目录形态，可手动拷贝测试 |
| `build\artifacts\HyCAD-v1.0.0-bundle.zip` | 压缩包备用 |
| `build\artifacts\HyCAD-Setup-1.0.0.exe` | **正式发给客户** |

仅打 bundle、不编 Setup：

```powershell
powershell -ExecutionPolicy Bypass -File build\scripts\PackBundle.ps1 -Version 1.0.0
```

---

## 七、常见问题

**Q：装了 Setup，CAD 里还是没有 HyCAD 命令？**  
- 确认路径存在：`%APPDATA%\Autodesk\ApplicationPlugins\HyCAD.bundle\Contents\Win64\HyCADTool.dll`  
- **完全退出 AutoCAD 再启动**（不要只关图纸）  
- 确认 AutoCAD 版本在 2024–2027  
- 若同时 NETLOAD 了 ReCall 开发包，可能与 Production 冲突——体验客户流程时**不要**再加载 ReCall

**Q：我在仓库 `build\artifacts` 里看到了 bundle，为什么 CAD 不加载？**  
A：那是**构建输出**，AutoCAD 只认 **ApplicationPlugins** 下的副本。要么运行 Setup.exe，要么手动复制过去。

**Q：升级新版本？**  
A：再运行新版 Setup.exe 覆盖安装，或删除旧 `HyCAD.bundle` 后重新安装。一般**不必**重新激活。

**Q：bundle 里要放 license 吗？**  
A：**不要**。授权在 `%ProgramData%\HyCAD\license.lic`，由 `hyLicense` 窗口写入。

---

## 八、快速自检清单

- [ ] `%APPDATA%\Autodesk\ApplicationPlugins\HyCAD.bundle\PackageContents.xml` 存在  
- [ ] `Contents\Win64\HyCADTool.dll` 存在  
- [ ] `Contents\Win64\commands.json` 存在  
- [ ] AutoCAD 重启后命令行能输入 `hyLicense`  
- [ ] bundle 内**无** `ReCall.dll` / `PrivateKey.xml` / `LicenseSigner.exe`
