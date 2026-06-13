# 071 HyCAD.bundle 安装与使用说明

> 2026-06-13 | 发版流程见 [070](070-客户安装与发版快速路径-2026-06-13-030000.md)；许可细节见 [069](069-许可证体系定稿说明-2026-06-13-024000.md)

---

## 一、两个「位置」，别搞混

| 是什么 | 在哪里 | 谁用 |
|---|---|---|
| **安装后的插件**（AutoCAD 实际加载） | `%APPDATA%\Autodesk\ApplicationPlugins\HyCAD.bundle` | 客户 / Setup 安装后 |
| **发版前的构建产物**（还没装） | 仓库 `build\artifacts\HyCAD.bundle` | 开发者打包 |
| **发给客户的安装程序** | 仓库 `build\artifacts\HyCAD-Setup-x.y.z.exe` | 客户双击安装 |

**本机路径示例**（把 `ZHY` 换成你的 Windows 用户名）：

```
C:\Users\ZHY\AppData\Roaming\Autodesk\ApplicationPlugins\HyCAD.bundle
```

**为什么资源管理器里不好找？**

- `AppData\Roaming` 默认隐藏
- 文件夹名是 `HyCAD.bundle`，不在「程序文件」
- Setup 固定装到 ApplicationPlugins，不可改路径

**最快打开方式**：

1. `Win + R` → `%APPDATA%\Autodesk\ApplicationPlugins`
2. 地址栏：`%APPDATA%\Autodesk\ApplicationPlugins\HyCAD.bundle`
3. PowerShell：`explorer "$env:APPDATA\Autodesk\ApplicationPlugins\HyCAD.bundle"`

若文件夹不存在，说明尚未运行 Setup 或安装失败。

---

## 二、HyCAD.bundle 目录结构

```
HyCAD.bundle/
├── PackageContents.xml          ← AutoCAD 启动自动加载
├── unins000.exe                 ← 卸载（Setup 安装后才有）
├── unins000.dat
└── Contents/
    └── Win64/
        ├── HyCADTool.dll
        ├── HyCADTool.Licensing.dll
        ├── commands.json        ← 命令表 + tier
        ├── HyCAD.BlenderUI.dll
        ├── HyCAD.Geometry.dll
        ├── HYFEA.Core.dll
        ├── …（其它依赖 dll）
        ├── config.json
        ├── Resources/
        └── _libraries/
```

**加载机制**：AutoCAD 扫描 `%APPDATA%\Autodesk\ApplicationPlugins\` 下所有 `*.bundle`，读 `PackageContents.xml` 中 `LoadOnAutoCADStartup=True`，加载 `Contents/Win64/HyCADTool.dll`。**无需 NETLOAD**。

支持版本：AutoCAD 2024–2027（R24.0–R27.0，见 `build/installer/PackageContents.xml`）。

---

## 三、客户安装（推荐：Setup.exe）

1. 双击 **`HyCAD-Setup-x.y.z.exe`**
2. 按向导完成（无需管理员）
3. **完全退出并重启 AutoCAD**
4. 输入 **`hyLicense`**：
   - 复制 **8 段机器码** 发给销售/开发者
   - 收到 **`HYC1.` 授权码** → 粘贴 → 确定激活
   - 或收到 **`license.lic`** → 从文件导入

**授权与插件分离**（不在 bundle 内）：

| 文件 | 路径 | 说明 |
|---|---|---|
| `license.lic` | `%ProgramData%\HyCAD\license.lic` | 带签名的 JSON；激活后自动写入 |
| `state.bin` | `%ProgramData%\HyCAD\state.bin` | 防时钟回拨；损坏时可删除后重启 |

---

## 四、手动安装（ZIP / 拷贝文件夹）

若没有 Setup.exe，只有 `HyCAD-vX-bundle.zip`：

1. 解压得到 **`HyCAD.bundle`** 文件夹（必须带 `.bundle` 后缀）
2. 复制到 `%APPDATA%\Autodesk\ApplicationPlugins\`
3. 确认存在：`...\HyCAD.bundle\PackageContents.xml`
4. 重启 AutoCAD

**不要**只复制 `Contents\Win64` 里的 dll——必须 **bundle 根 + PackageContents.xml** 成套。

---

## 五、卸载

| 方式 | 操作 |
|---|---|
| A（推荐） | 再运行 Setup.exe → 已安装时选 **「是」卸载** |
| B | 开始菜单 → **「卸载 HyCAD 插件」** |
| C | 运行 bundle 内 `unins000.exe` |
| D（手动拷贝） | 删除整个 `HyCAD.bundle` 文件夹 |

卸载**不删除** `%ProgramData%\HyCAD\license.lic`；同机重装后一般无需重新激活。

---

## 六、开发者：bundle 从哪来

仓库根目录：

```powershell
powershell -ExecutionPolicy Bypass -File build\scripts\Publish-Setup.ps1 -Version 1.0.0
```

| 产出 | 用途 |
|---|---|
| `build\artifacts\HyCAD.bundle\` | 目录，可手动拷贝测试 |
| `build\artifacts\HyCAD-v1.0.0-bundle.zip` | 压缩包备用 |
| `build\artifacts\HyCAD-Setup-1.0.0.exe` | **正式发给客户** |

仅打 bundle：

```powershell
powershell -ExecutionPolicy Bypass -File build\scripts\PackBundle.ps1 -Version 1.0.0
```

> 发版统一走 `build/` 脚本；仓库内**无**独立 `HyCADTool.Installer` 工程。

---

## 七、常见问题

**Q：装了 Setup，CAD 里没有 HyCAD 命令？**

- 确认 `%APPDATA%\...\HyCAD.bundle\Contents\Win64\HyCADTool.dll` 存在
- **完全退出 AutoCAD 再启动**
- 版本需在 2024–2027
- 测客户流程时不要同时 NETLOAD ReCall 开发包

**Q：`build\artifacts` 里的 bundle 为什么 CAD 不加载？**  
A：那是构建输出；AutoCAD 只认 ApplicationPlugins 下的副本。运行 Setup 或手动复制。

**Q：升级新版本？**  
A：新版 Setup 覆盖安装即可，通常不必重新激活。

**Q：bundle 里要放 license 吗？**  
A：**不要**。授权在 `%ProgramData%\HyCAD\`，由 `hyLicense` 写入。

**Q：激活后仍提示未授权？**  
A：确认 `license.lic` 存在；机器码与签发一致；若提示时间回拨，删 `state.bin` 后重启（见 069）。

**Q：HYC1 粘贴报错？**  
A：确认完整复制（含 `HYC1.` 前缀）；或让销售发 `license.lic` 用「从文件导入」。

---

## 八、快速自检清单

- [ ] `%APPDATA%\...\HyCAD.bundle\PackageContents.xml` 存在
- [ ] `Contents\Win64\HyCADTool.dll` + `HyCADTool.Licensing.dll` + `commands.json` 存在
- [ ] AutoCAD 重启后 `hyLicense` 可用
- [ ] bundle 内**无** `ReCall.dll` / `PrivateKey.xml` / `LicenseSigner.exe`
- [ ] 激活后 `%ProgramData%\HyCAD\license.lic` 存在
