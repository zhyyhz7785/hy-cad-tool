# Features（按功能切片）

## 约定

- **一个功能 = 本目录下的一个子文件夹**，该文件夹内放此功能相关的模型、服务、命令、视图、ViewModel（自包含）。
- **namespace**：`HyCADTool.Features.{文件夹名}`（如 `HyCADTool.Features.Settlement`）。Road 等超大功能可用子命名空间 `HyCADTool.Features.Road.Alignment`。
- **README.md**：每个 Feature 根目录一份稳定档案卡（命令、面板、算法、依赖）。
- **Doc/**：每个 Feature 下的 `Doc/序号-标题-YYYY-MM-DD-HHMMSS.md` 记录修改历史；跨多个 Feature 的全局文档仍放在仓库顶层 `doc/`。

## Shared 何时抽离

仅当某代码 **≥3 个 Feature 使用**、接口稳定、且确为平台基础能力时，才迁入 `HyCADTool/Shared/`。宁可重复，避免过早抽象。
