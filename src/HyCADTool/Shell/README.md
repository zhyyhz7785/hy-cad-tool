# Shell

**客户端壳**：主 Blender 侧栏/筛选/首选项、许可激活窗、键位与调度、全局资源字典、Ribbon/CUI 构建、与 Markdown 编辑器的桥接。命令基础设施（`CommandDispatcher`、`EditorLoader`）也在此，用于路由到 `Features` 内具体命令类。

- **不**放按业务域切片的命令/算法（在 `Features/`）或真通用 AutoCAD/几何（在 `Shared/`）。
