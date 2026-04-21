# 02 — `hy-settings.json` 字段手册

**序列化类型**：`SettingsPanelViewModel` 内部私有类 `SettingsData`（`Newtonsoft.Json` 序列化，缩进格式化）。

**源码**：`HyCADTool.Refactored/Presentation/ViewModels/SettingsPanelViewModel.cs`（`SaveSettings` / `LoadSettings` / `SettingsData`）。

---

## 1. 读写行为

| 行为 | 说明 |
|------|------|
| 默认自动保存 | `SetProperty` 在 `_isLoading == false` 且 `AutoSaveEnabled == true` 时，**每次属性变更**调用 `SaveSettings()` 写盘。 |
| 关闭自动保存 | `AutoSaveEnabled = false` 时仅更新内存与 UI，需通过「保存用户设置」等显式 `SavePublic()` / `SaveSettingsToFile`。 |
| 加载 | `LoadSettings()`：文件不存在则静默保留默认值；反序列化失败则 Debug 输出并尽量保持默认。 |
| 样式脏标记 | `LoadSettings` 前后比较 `BuildStyleSignature()`，若样式相关字段变化则 `_stylesDirty = true`，命令执行前 `EnsureStylesApplied()` 再同步到 AutoCAD。 |

**兼容性**：`SettingsData` 注释强调「新增字段要给默认值」，旧 JSON 缺字段时 `LoadSettings` 有多处兜底（如 `Scale`、`StyleTXScale`、`StationMainInterval` 等）。

---

## 2. 字段分组：Tab A — 样式与标注

| JSON 属性 | 类型 | 默认值（代码） | 含义 |
|-----------|------|----------------|------|
| `Scale` | number | 50.0 | 主绘图比例分母（与业务 Scale 一致）。 |
| `UseSubScale` | bool | false | 是否启用副比例。 |
| `SubScale` | number | 50.0 | 副比例；`UseSubScale=false` 时常与 `Scale` 同步逻辑在 VM 内。 |
| `Unit` | string | `"Millimeter"` | 绘图单位枚举字符串：`Millimeter` / `Centimeter` / `Meter`。 |
| `Precision` | int | 0 | 标注小数位（与单位组合，setter 内可能 Clamp）。 |
| `StyleTName` | string | `"0-hy-说明-T"` | TrueType 说明样式名。 |
| `StyleTFont` | string | `"微软雅黑"` | TrueType 字体名。 |
| `StyleSName` | string | `"0-hy-说明-S"` | SHX 样式名。 |
| `StyleSFont` | string | `"tssdeng.shx"` | SHX 主字体。 |
| `StyleSBigFont` | string | `"tssdchn.shx"` | SHX 大字体。 |
| `FontFileName` | string | （Obsolete） | 旧字段，兼容读，等同于 `StyleSFont`。 |
| `BigFontFileName` | string | （Obsolete） | 旧字段，兼容读，等同于 `StyleSBigFont`。 |
| `TextSize` | number | 2.5 | 文字高度（与样式创建一致）。 |
| `StyleTXScale` | number | 1.0 | TrueType 字宽比。 |
| `StyleSXScale` | number | 0.7 | SHX 字宽比。 |
| `TextXScale` | number | 0.7 | **兼容旧文件**：写入时与 `StyleSXScale` 同步；读取时若新字段缺失则兜底。 |
| `Dimtxt` | number | 2.5 | 标注文字高度相关。 |
| `Dimexo` | number | 1.0 | 尺寸界线起点偏移。 |
| `Dimexe` | number | 1.0 | 尺寸界线超出量。 |
| `Dimdle` | number | 0.5 | 尺寸线偏移等。 |
| `Dimgap` | number | 1.0 | 文字与尺寸线间距。 |
| `Dimasz` | number | 1.0 | 箭头大小。 |
| `DimArrowName` | string | `"_ARCHTICK"` | 箭头块名。 |
| `MLeaderArrowSize` | number | 2.0 | 多重引线箭头大小。 |
| `MLeaderArrowName` | string | `"_DotSmall"` | 多重引线箭头。 |
| `MLeaderLandingGap` | number | 0.5 | 基线间隙。 |
| `MLeaderTextColorIndex` | int | 7 | 引线文字颜色 ACI。 |

---

## 3. 字段分组：Tab B — 钢筋

| JSON 属性 | 类型 | 默认值 | 含义 |
|-----------|------|--------|------|
| `RebarDiameter` | number | 14.0 | 钢筋直径等主参数。 |
| `RebarSpacing` | number | 200.0 | 间距。 |
| `AnchorageLength` | number | 500.0 | 锚固长度。 |
| `DotSeparation` | number | 200.0 | 点筋分离距离。 |
| `BendingLineMinLength` | number | 150.0 | 弯折线最小长度。 |
| `AnchorageJoinLength` | number | 1500.0 | 搭接/连接长度类参数。 |
| `HookLength` | number | 1.0 | 钩长。 |
| `ProtectionThickness` | number | 1.0 | 保护层。 |
| `ReinforcementDiameter` | number | 0.35 | 加强筋等直径相关。 |
| `DotReinOffset` | number | 1.35 | 点筋偏移。 |
| `PolylineWidth` | number | 0.4 | 多段线宽。 |
| `DimensionDistanceInside` | number | 6.0 | 内侧标注距离。 |
| `DimensionDistanceOutside` | number | 14.0 | 外侧标注距离。 |
| `DimensionDistanceWithDim` | number | 6.0 | 带标注时的距离。 |
| `MleaderDistance` | number | 6.0 | 引线距离。 |
| `DimDistanceTolerance` | number | 30.0 | 标注距离容差。 |

`CreateReinParameters()` 将上述字段打包为 `ReinParameters` 供配筋命令使用。

---

## 4. 字段分组：Tab C — 道路与线位默认

| JSON 属性 | 类型 | 默认值 | 含义 |
|-----------|------|--------|------|
| `RoadGapWidth` | number | 5.0 | 道路相关间隙（如中央分隔带开口等模块使用，具体见命令）。 |
| `RoadCrosswalkWidth` | number | 5.0 | 人行横道宽度相关默认。 |
| `RoadStopLineDistance` | number | 2.0 | 停止线距离相关默认。 |
| `RoadStripeSpacing` | number | 1.0 | 条纹间距类默认。 |
| `AlignmentDefaultRadius` | number | 30.0 | 新建 PI 默认半径（`hyRoadAlnByPi` / `hyRoadAlnDefaults` 等）。 |
| `AlignmentDefaultSpiralIn` | number | 0.0 | 默认缓和曲线入。 |
| `AlignmentDefaultSpiralOut` | number | 0.0 | 默认缓和曲线出。 |
| `AlignmentDefaultStartStation` | number | 0.0 | 默认起桩号。 |

**道路命令注释**中多处标明从 `hy-settings` 读取以上默认值（例如 `RoadAlignmentByPiCommand`、`RoadAlignmentDefaultsCommand`）。

---

## 5. 字段分组：桩号标注（Station）

| JSON 属性 | 类型 | 默认值 | 含义 |
|-----------|------|--------|------|
| `StationMainInterval` | number | 20.0 | 主桩间距。 |
| `StationSubInterval` | number | 5.0 | 次桩间距。 |
| `StationTickLengthMain` | number | 4.0 | 主刻度线长度。 |
| `StationTickLengthSub` | number | 1.5 | 次刻度线长度。 |
| `StationTextHeight` | number | 3.0 | 桩号文字字高。 |
| `StationTextMargin` | number | 0.5 | 文字边距。 |
| `StationRotateTextAlongTangent` | bool | true | 文字是否沿切向旋转。 |
| `StationTextSide` | string | `"Left"` | 文字在基准线哪一侧（Left/Right 等，以代码约定为准）。 |

> 注：`RoadStationLabelOptions` 等类注释中曾规划「完全改读 hy-settings」，若后续重构，仍以源码为准。

---

## 6. 字段分组：界面外观（Blender / PaletteSet）

| JSON 属性 | 类型 | 默认值 | 含义 |
|-----------|------|--------|------|
| `Theme` | string | `"BlenderDark"` | `BlenderThemeManager` 主题名：`BlenderDark` / `BlenderLight` / `AcadLight` / `AcadDark`（见 `SettingsData` 注释）。 |
| `UiFontScale` | number | 1.0 | 界面字号（Metric_Font*）缩放。 |
| `UiDensityScale` | number | 1.0 | 行高、间距等密度缩放。 |
| `UiInputWidthScale` | number | 1.0 | 输入框宽、标签列宽缩放。 |

`LoadSettings` 结束时尝试 `BlenderMetricsScaleManager.Apply(...)`，保证读盘后与 UI 度量一致。

---

## 7. 其他

| JSON 属性 | 类型 | 默认值 | 含义 |
|-----------|------|--------|------|
| `EquipmentDataFilePath` | string | `""` | 设备等相关外部数据路径（具体消费处见引用 `EquipmentDataFilePath` 的命令或服务）。 |

---

## 8. 明确不在此文件中的内容

- **AutoCAD 图层名称与颜色表**：由 `PluginInitializer.GetRequiredLayers()` 与 `HyRoadLayers` 定义，**不**写入 `hy-settings.json`。  
- **桩基面板独立文件**：`hy-pile-settings.json`（见 [05](./05-模块独立设置文件.md)）。  
- **沉降面板独立文件**：`hy-settlement-settings.json`。

---

## 9. 相关源码位置

- 序列化 DTO：`SettingsPanelViewModel` 内嵌类 `SettingsData`（约 L1410–1496）。  
- 保存：`SaveSettings`（约 L1160–1238）。  
- 加载：`LoadSettings`（约 L1262–1385）。  

下一篇：[03-config-json-与全局服务](./03-config-json-与全局服务.md)。
