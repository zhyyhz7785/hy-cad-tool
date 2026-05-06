# hyob 子系统目录占位

> 总计划：`docs/DataExchange/04-hyob实施总计划-2026-05-07-011100.md`
>
> 设计基线：`02` `03` 两篇 DataExchange 文档

## 当前状态

**M3 完成**（文字与块 TypedObject 白名单上线）。


| 里程碑 | 状态  | 内容                                                              |
| --- | --- | --------------------------------------------------------------- |
| M0  | ✅   | 命令骨架 + commands.json 注册 + CommandFacade `[CommandMethod]`        |
| M1  | ✅   | Hash / CRC32 / Header / OpaqueObject / ObjectStore / Tree / Commit / RefStore / AutoCadDatabaseMirror / RoundTripValidator |
| M2  | ✅   | HyobLine / HyobPolyline / HyobArc / HyobCircle + Typed 优先     |
| M3  | ✅   | HyobDBText / HyobMText / HyobBlockReference (内联 Attr) + ObjectReader 委托 |
| M4-A | ✅  | HyobDimension 9 种 subtype 统一 schema（变长 points + extras） |
| M4-B | ✅  | HyobMLeader（语义级）+ HyobHatch（模式参数级）+ HyobWipeout（类型预留） |
| M5   | ✅  | HyobXDataAttachment + HyobExtensionDictionary（扁平摘要级），并入 by-handle 树 |
| M6   | ⏳  | TextStyleDef / DimStyleDef / LayerDef / BlockDef               |


## 命令一览（M0 仅 stub）


| 命令       | 类                           | 落地里程碑  |
| -------- | --------------------------- | ------ |
| `hyobI`  | HyobInitCommand             | M1     |
| `hyobS`  | HyobStatusCommand           | M1     |
| `hyobC`  | HyobCommitCommand           | M1     |
| `hyobL`  | HyobLogCommand              | M1     |
| `hyobCo` | HyobCheckoutCommand         | M1 后期  |
| `hyobD`  | HyobDiffCommand             | M2+    |
| `hyobR`  | HyobRoundTripCommand        | M11    |
| `hyobG`  | HyobGcCommand               | M7     |
| `hyobB`  | HyobBranchCommand           | M8     |
| `hyobM`  | HyobMergeCommand            | M8/M10 |
| `hyobP`  | HyobShowHistoryPanelCommand | M10    |
| `hyobES` | HyobExportHygeomCommand     | M12    |


## 测试方法（M2）

```text
1. VS 编译 HyCADTool
2. C2（ReCall 重载）
3. 命令行依次：
   hyobI    自检 16 项（M1 12 + M2 4 Typed）应全 PASS；输出 Typed=N + Opaque=M + 分类
   hyobC    再次提交一个 commit；分类应能反映新增/删除几何
   hyobR    遍历整个 DAG，验证全部 TypedObject 能 Decode（不再有 Warning）
```

预期：在含 Line/Polyline/Arc/Circle 的 DWG 上，hyobR 输出例如：
```
typed   : Polyline=412, Line=180, Arc=22, Circle=8
status  : ✓ 无错误，仓库链路完整
```

## 后续目录骨架（M1 起按需创建）

```text
Features/DataExchange/Hyob/
  Domain/
    Objects/
    Schemas/
    Codec/
    Repository/
    Models/
    Diff/
    Merge/
  Infrastructure/
    AutoCadMirror/
    Persistence/
    RoundTrip/
  Presentation/
    Commands/         ← 已就位
    Panels/           ← M10
    ViewModels/       ← M10
```

