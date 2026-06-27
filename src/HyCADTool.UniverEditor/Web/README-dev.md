# HyCAD Univer Editor — 浏览器 Dev 指南

在 AutoCAD / WebView2 外先用 Vite 热更新调 Ribbon、Tab 与 CSS；定稿后 `npm run build`，C# 宿主加载 `dist` 即可，**无需把 UI 转写回 WPF**。

## 启动

```powershell
cd src/HyCADTool.UniverEditor/Web
npm install
npm run dev
```

**必须先执行 `npm run dev`**，否则 5174 端口无服务，浏览器会打不开。

浏览器打开 **http://127.0.0.1:5174**（推荐；Windows 上 `localhost` 有时只走 IPv6 会连不上）。  
`vite.config.ts` 已绑定 `127.0.0.1:5174`，`npm run dev` 会自动打开该地址。

## 改哪些文件

| 模块 | 路径 | 说明 |
|---|---|---|
| 入口 / 插件栈 | `src/main.ts` | Univer 注册、dev 宿主接线 |
| 文件伪 Tab | `src/file-tab-inject.ts` | 「文件▾」菜单与 `hyCadFileAction` |
| 布局 Tab | `src/layout-tab-inject.ts` | 行高列宽 mm / 比例 / 行列合并 / 落图拾取 / 角色占位 |
| CAD 桥接 | `src/univer-bridge.ts` | `loadSnapshot` / `exportSnapshot` |
| 浏览器 mock | `src/dev-host.ts` | 无 WebView2 时本地处理文件菜单 |
| 样式 | `src/global.css` | 伪 Tab、主题色、dev 状态条 |
| 演示数据 | `src/demo-workbook.ts` | dev 默认 workbook |
| 人员样表 | `public/fixtures/personnel-snapshot.json` | dev「加载人员样表」fixture |

## 浏览器 dev 行为

- 检测到无真实 `window.chrome.webview` 时，`dev-host.ts` 注入 mock 宿主。
- **新建空表**：重置为 `demo-workbook.ts` 内容。
- **加载人员样表**：`fetch('/fixtures/personnel-snapshot.json')` → `loadSnapshot`。
- **导入 xlsx / 导出 / 拾取 / 落图**：右下角状态条提示「仅 CAD 宿主可用」。
- 右下角 **dev 状态条** 显示最近操作。

## 验收（回 C# 宿主）

### P0 v1 round-trip smoke

对应总纲：[new/001 §7 P0](../../../../docs/product-discovery/table/new/001-Univer路线-原需求最优落地总纲-2026-06-26-163000.md)

#### 自动化（开发机，必须先绿）

```powershell
dotnet test src/HyCADTool.Tests/HyCADTool.Tests.csproj -c Debug --filter "FullyQualifiedName~TableGridUniverAdapterTests"

cd src/HyCADTool.UniverEditor/Web
npm run build

cd ../../..
dotnet build src/HyCADTool/HyCADTool.csproj -c Debug
```

单测覆盖：merge 区域、只读格跳过、竖排去换行、多格编辑回写 OpLog、fixture 与 Domain 同步。

#### 浏览器 dev（5174，可选预检）

1. `npm run dev` → 打开 http://127.0.0.1:5174
2. **文件▾ → 加载人员样表** → 目视 merge：标题跨 7 列、照片格跨 3 行
3. 改「张三」等 editable 格 → 右下角 dev 状态条无 error

#### AutoCAD（必须）

1. **C2 → C1**（`TestCommand` 已指向 N38）或命令行 **N38**
2. 点工具栏 **「人员样表」** 或 **文件▾ → 加载人员样表** → 标题 merge、侧栏竖排可见
3. 改 editable 格（如「张三」→「李四」）→ 摘要/状态条更新
4. **落图** → 窗口会自动让出前台（Topmost=false），**请看 AutoCAD 命令行指定插入点**（Esc 取消）→ 图面单元格文字为「李四」
5. 在 Univer **第 8 行或右侧列**输入文字 → **落图** → 图面应包含新增内容（内容外包矩形扩展）
6. 框选子区域 → **落图范围▾ → 整个选区**（或文件菜单同名项）→ 指定插入点 → 仅选区落图
7. 框选含空格区域 → **选区内仅有内容** → 仅非空单元格所在范围落图
8. **拾取** 刚落图的 HyTable → 编辑器回显「李四」
9. 关闭再开 N38 → 无 `DirectoryNotFoundException` / WebView2 加载失败

#### UI 壳核对

- Tab 行：**文件▾ | 开始 | 公式 | 数据**
- 文件菜单 7 项 → C# `HandleHyCadFileAction`
- 开始 Tab HyCAD 菜单 → pick / publish / 落图范围▾
- 无 WPF Application 全局资源改动（PaletteSet 安全）

#### 通过标准

- merge 正确（标题 7 列、照片 3 行、籍贯 3 列）
- 编辑后落图文字与编辑器一致
- 拾取回读与落图前一致
- 无 WebView2 / dist 路径错误
- PaletteSet 无 Badge / XamlParse 回归（目视）

## 日常循环

```
浏览器改 UI（5174 HMR）
  → 文件菜单 / 样表用 dev mock 自测
  → npm run build
  → dotnet build
  → C2 → N38 在 CAD 里验收桥接
```

## 重新生成 personnel fixture

与 CAD 侧 `TableSamples.BuildPersonnelTable()` 保持一致。CI 默认 **不** 覆写 fixture；`PersonnelFixture_IsInSyncWithDomain` 单测保障同步。

手动刷新步骤：

1. 打开 `src/HyCADTool.Tests/Features/Tables/TableGridUniverAdapterTests.cs`
2. 临时去掉 `ExportPersonnelUniverFixture` 上的 `[Fact(Skip = "...")]`
3. 运行：

```powershell
dotnet build src/HyCADTool.Tests/HyCADTool.Tests.csproj -c Debug
dotnet test src/HyCADTool.Tests/HyCADTool.Tests.csproj -c Debug --filter ExportPersonnelUniverFixture --no-build
```

4. 恢复 Skip，再执行 `npm run build`（`public/fixtures` → `dist/fixtures`）

输出：`Web/public/fixtures/personnel-snapshot.json`。
