# Settlement 沉降计算

- **命令**：HYJC（主入口）/ hySC（同义）
- **面板**：独立 `SettlementWindow`（WPF），内嵌 `SettlementPanel`
- **算法**：`SettlementCalculationService`（天然地基 / 复合地基 / 桩基沉降）
- **输出**：`SettlementTableService`（AutoCAD 表格）+ `SettlementReportGenerator`（Markdown 报告）
- **历史**：见 `Doc/`
