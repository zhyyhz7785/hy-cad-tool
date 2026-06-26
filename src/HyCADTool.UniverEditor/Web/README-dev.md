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
| HyCAD Ribbon | `src/ribbon-hycad.ts` | 开始 Tab 内落图/拾取等 |
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

```powershell
cd src/HyCADTool.UniverEditor/Web
npm run build

cd ../../..
dotnet build src/HyCADTool/HyCADTool.csproj -c Debug
```

AutoCAD：**C2 → N38**（或 `TestCommand.cs` 指向的 Univer 命令）。

核对：

- Tab 行：**文件▾ | 开始 | 公式 | 数据**
- 文件菜单 7 项 → C# `HandleHyCadFileAction`
- 开始 Tab HyCAD 菜单 → pick / publish
- 无 WPF Application 全局资源改动（PaletteSet 安全）

## 日常循环

```
浏览器改 UI（5174 HMR）
  → 文件菜单 / 样表用 dev mock 自测
  → npm run build
  → dotnet build
  → C2 → N38 在 CAD 里验收桥接
```

## 重新生成 personnel fixture

与 CAD 侧 `TableSamples.BuildPersonnelTable()` 保持一致：

```powershell
dotnet build src/HyCADTool.Tests/HyCADTool.Tests.csproj -c Debug
dotnet test src/HyCADTool.Tests/HyCADTool.Tests.csproj -c Debug --filter ExportPersonnelUniverFixture --no-build
```

输出：`Web/public/fixtures/personnel-snapshot.json`。
