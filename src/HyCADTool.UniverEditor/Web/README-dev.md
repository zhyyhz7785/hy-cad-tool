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
| 入口 / 插件栈 | `src/main.ts` | Univer 注册、dev 宿主、选区/布局宿主消息 |
| 文件伪 Tab | `src/file-tab-inject.ts` | 最左「文件▾」→ `hyCadFileAction` |
| 布局 Tab | `src/layout-tab-inject.ts` | 「数据」后「布局」+ 第二行 Ribbon（纸张/行列/合并/落图） |
| 主题伪 Tab | `src/theme-menu-inject.ts` | 「布局」后「主题▾」（Univer + HyCAD 面板主题） |
| 窗口控制 | `src/window-controls-inject.ts` | 顶栏最右 — □ ✕ → `hyCadWindowControl` |
| 落图范围 | `src/ribbon-range-inject.ts` | 开始 Tab 工具栏「落图范围▾」 |
| CAD 桥接 | `src/univer-bridge.ts` | `loadSnapshot` / `exportSnapshot` / 内容驱动 autoFit |
| 浏览器 mock | `src/dev-host.ts` | 无 WebView2 时的宿主模拟 |
| 浏览器测试面板 | `src/dev-panel.ts` | 左下角快捷按钮 + 选区 + 验收清单 |
| 样式 | `src/global.css` | 伪 Tab、布局 Ribbon、dev 面板 |
| 演示数据 | `src/demo-workbook.ts` | dev 默认 workbook |
| 人员样表 | `public/fixtures/personnel-snapshot.json` | dev「加载人员样表」fixture |

## 当前 Tab 行布局（2026-06）

```
[文件▾]     开始 | 公式 | 数据 | 布局 | 主题▾          [—][□][✕]
  ↑ 绝对贴左   ↑ 居中组                              ↑ 绝对贴右
```

- **布局 Tab**：点击后隐藏 Univer 默认第二行工具栏，显示 HyCAD 布局 Ribbon（结构模式、纸张 L0、行列、合并、Scale、落图/拾取）。
- **主题▾**：Univer 默认/深色/绿色 + HyCAD 面板浅/深（Blender 蓝 `#4772B3` 映射到 Univer primary）。
- **窗口按钮**：浏览器 dev 仅提示「仅 CAD 宿主」；CAD 内控制 WPF 窗口。

## 浏览器 dev 面板（左下角）

无 WebView2 时自动出现 **HyCAD Univer Dev** 面板：

| 按钮 | 行为 |
|---|---|
| 人员样表 | `fetch` fixture → `loadSnapshot` |
| 新建空表 | 重置为 `demo-workbook.ts` |
| 导出快照 | `exportSnapshot` → Console 打印 JSON |
| 回填布局初值 | 模拟宿主 `setViewport` + `setScale`（布局 Tab 纸张/比例框应回填） |

面板还显示：**当前选区**（如 `B3:D5`）、**最近操作**、**Tab 行验收清单**。

## 浏览器 dev mock 行为

- **文件菜单**：新建/人员样表可用；导入导出/拾取/落图 → 提示「仅 CAD 宿主可用」。
- **布局 Tab**：
  - `新建空表` / `从模板→空白` → demo workbook
  - `从模板→人员` → personnel fixture
  - `自动调整行高/列宽` → Univer API 本地执行，发 `hyCadTrackSizes`（CAD 才写回 Domain）
  - 插删行列/合并/改 mm 尺寸/列宽适配纸宽 → 提示「仅 CAD 宿主可用」
- **主题▾**：本地切换 Univer `ThemeService` + `toggleDarkMode`，无需 CAD。
- **落图范围▾**（开始 Tab）：提示「仅 CAD 宿主可用」。

## 浏览器 dev 预检（5174）

1. `npm run dev` → http://127.0.0.1:5174
2. 目视 Tab 行：文件贴左、中间五 Tab、主题▾、右侧窗口键
3. **文件▾ → 加载人员样表** 或面板 **人员样表** → merge：标题 7 列、照片 3 行
4. 点 **布局** → 第二行出现 HyCAD Ribbon；纸张/Scale 框应有初值（mock `setViewport`）
5. 框选单元格 → dev 面板 **选区** 更新；行高/列宽 mm 框跟随选区
6. **主题▾ → HyCAD 面板（深色）** → 界面变暗、强调色变 Blender 蓝
7. 改 editable 格（如「张三」）→ dev 面板状态无 error
8. **导出快照** → Console 有 JSON，`cells` 数量合理

## 验收（回 C# 宿主）

### 自动化（开发机，必须先绿）

```powershell
dotnet test src/HyCADTool.Tests/HyCADTool.Tests.csproj -c Debug --filter "FullyQualifiedName~TableGridUniverAdapterTests"

cd src/HyCADTool.UniverEditor/Web
npm run build

cd ../../..
dotnet build src/HyCADTool/HyCADTool.csproj -c Debug
```

#### AutoCAD（必须）

1. **C2 → C1**（`TestCommand` 指向 N38）或命令行 **N38**
2. **文件▾ → 加载人员样表** 或布局 Tab **从模板→人员** → merge 正确
3. **布局 Tab**：改行高/列宽 mm、插删行列、合并 → 摘要/状态更新
4. **落图** / **拾取** / **落图范围** → CAD 图面验证
5. **主题▾** 切换 → Univer 界面变色
6. 顶栏 **— □ ✕** → 窗口最小化/最大化/隐藏
7. 关闭再开 N38 → 无 dist / WebView2 路径错误

#### UI 壳核对

- Tab 行：`文件▾ | 开始 | 公式 | 数据 | 布局 | 主题▾ | — □ ✕`
- 文件菜单 9 项 → C# `HandleHyCadFileAction`
- 布局 Tab 各组按钮 → C# `HandleHyCadLayout` / `OnLayoutOp`
- 无 WPF Application 全局资源改动（PaletteSet 安全）

## 日常循环

```
浏览器改 UI（5174 HMR）
  → dev 面板 / 文件菜单 / 布局 Tab 自测
  → npm run build
  → dotnet build
  → C2 → N38 在 CAD 里验收桥接
```

## 重新生成 personnel fixture

与 CAD 侧 `TableSamples.BuildPersonnelTable()` 保持一致。CI 默认 **不** 覆写 fixture；`PersonnelFixture_IsInSyncWithDomain` 单测保障同步。

```powershell
dotnet build src/HyCADTool.Tests/HyCADTool.Tests.csproj -c Debug
dotnet test src/HyCADTool.Tests/HyCADTool.Tests.csproj -c Debug --filter ExportPersonnelUniverFixture --no-build
```

（需临时去掉 `ExportPersonnelUniverFixture` 上的 Skip）

输出：`Web/public/fixtures/personnel-snapshot.json`，再 `npm run build`。
