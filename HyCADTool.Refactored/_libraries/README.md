# \_libraries —— HyCAD 道路设计字典资源

> 这是 P0 落地的一部分：道路领域的**共享字典**，供 Domain 模型、Xdata、JSON 持久化、代码检测及 v2 Blender 材质映射使用。
> 版本随 `SchemaVersion.Current` 对齐；不与 AutoCAD 强绑定，**Blender 端也将直接读取同一目录**。

## 目录

| 文件                                 | 作用                                                    | 消费方（v1）            | 消费方（v2）             |
| ------------------------------------ | ------------------------------------------------------- | ----------------------- | ------------------------ |
| `feature-catalog.json`               | 线型、点类型、部件、标线、标志、规范枚举                  | Domain 校验、命令 UI    | Blender Addon 同步       |
| `blender-material-mapping.json`      | `MaterialKey → Principled BSDF / 挤出 hint` 模板        | 占位（不读取）          | `hyRoad3dExportGltf` 写 extras、Blender Addon 建材质 |

## v1 行为

- **仅作为 schema 占位** —— C# 端不读取、不热加载（避免引入 JSON 解析热点）。
- 进入 v1 发版时，随 DLL 同级目录部署，便于后续升级。
- `_libraries/` 前缀的下划线用于与 HyCAD 其他 `config.json` 配置隔离。

## v2 行为（P7）

- Blender Python Addon 在启动时加载这两个 JSON，建立 `MaterialKey → bpy.data.materials` 映射；
- `hyRoad3dExportGltf` 在 glTF `extras` 字段中写入 `materialKey`，Blender 端 import 时按映射赋值。
- 新增材质时：先在 `blender-material-mapping.json` 增条目 → 在 Domain `TemplatePoint.MaterialKey` 使用即可，无需改 C# 代码。
