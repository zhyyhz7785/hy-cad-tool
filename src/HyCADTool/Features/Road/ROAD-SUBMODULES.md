# Road 子模块（规划）

按方案将 `hyRoad*` 相关代码从单一 `Features/Road/` 平铺与 `Domain/**/Road`、`Shared/AutoCAD/**/Road` 逐步收拢到下列子目录（每目录自含 `Commands/` / `Domain/` / `Services/` / `Views/` / `ViewModels/` 中实际存在的层）：

| 子目录 | 内容侧重 |
|--------|----------|
| `Alignment/` | 路线、PI、断链、加宽表等 |
| `CrossSection/` | 横断、标准横断、参数窗 |
| `Profile/` | 纵断、纵断标注 |
| `Intersection/` | 交叉口、路缘、合并规则 |
| `Marking/` | 标线、人行横道、缘石坡道、盲道、停止线 |
| `Plan/` | 总平面、走廊、项目树、历史、结构层 |
| `Export/` | LandXML/JSON/GLTF 等 |
| `Events/` | 道路领域事件总线（由 `Domain/Events/Road` 迁入） |
| `Shared/` | 跨子模块复用（图层、XData、桥接服务） |

迁移时可分批提交，每批保持 `dotnet build` 与 C2 通过。
