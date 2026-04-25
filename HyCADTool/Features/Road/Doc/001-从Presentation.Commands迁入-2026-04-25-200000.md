# 001-从Presentation.Commands迁入-2026-04-25-200000

## 起因
道路相关 AutoCAD 命令集中到 `Features/Road`，与 `commands.json` 中 `HyCADTool.Features.Road.*` 类型全名一致。

## 改动
- 目录：`Presentation/Commands/Road` → `Features/Road`（约 60 个 `.cs`）
- 命名空间：`HyCADTool.Features.Road`；并批量替换历史 `HyCADTool.Refactored.*` 为当前程序集布局
- `RoadDesignViewModel`、`RoadAlignmentWorkbenchViewModel` 等引用已改为 Features.Road
- `ReCall/commands.json` 中道路命令 `type` 前缀已改为 `HyCADTool.Features.Road.`

## 验证
- `dotnet build HyCADtoolGpt.sln`
- 抽样：hyRoadA、hyRoadCs、hyRoadTree

## 遗留
- 自 git 归档恢复的部分文件中文注释/字符串存在乱码；`RoadAlignmentCommand` 中一段用户可见输出已改为可读中文，其余乱码串建议从本地备份或 IDE 历史再修一遍。
