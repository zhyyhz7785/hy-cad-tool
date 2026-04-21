# 07 — `hy-settings.json` 示例骨架（全字段）

以下为根据 `SettingsPanelViewModel.SettingsData` **默认值**生成的 **JSON 骨架**，便于：

- 手工新建或合并配置文件；  
- 对比缺省与当前用户文件差异。

**注意**：实际磁盘上的键顺序、缩进、以及是否包含 Obsolete 字段（`FontFileName` / `BigFontFileName`）可能因保存版本而异；反序列化以 Newtonsoft.Json 为准。

```json
{
  "Scale": 50.0,
  "UseSubScale": false,
  "SubScale": 50.0,
  "Unit": "Millimeter",
  "Precision": 0,
  "StyleTName": "0-hy-说明-T",
  "StyleTFont": "微软雅黑",
  "StyleSName": "0-hy-说明-S",
  "StyleSFont": "tssdeng.shx",
  "StyleSBigFont": "tssdchn.shx",
  "FontFileName": "tssdeng.shx",
  "BigFontFileName": "hztxt.shx",
  "TextSize": 2.5,
  "StyleTXScale": 1.0,
  "StyleSXScale": 0.7,
  "TextXScale": 0.7,
  "Dimtxt": 2.5,
  "Dimexo": 1.0,
  "Dimexe": 1.0,
  "Dimdle": 0.5,
  "Dimgap": 1.0,
  "Dimasz": 1.0,
  "DimArrowName": "_ARCHTICK",
  "MLeaderArrowSize": 2.0,
  "MLeaderArrowName": "_DotSmall",
  "MLeaderLandingGap": 0.5,
  "MLeaderTextColorIndex": 7,
  "RebarDiameter": 14.0,
  "RebarSpacing": 200.0,
  "AnchorageLength": 500.0,
  "DotSeparation": 200.0,
  "BendingLineMinLength": 150.0,
  "AnchorageJoinLength": 1500.0,
  "HookLength": 1.0,
  "ProtectionThickness": 1.0,
  "ReinforcementDiameter": 0.35,
  "DotReinOffset": 1.35,
  "PolylineWidth": 0.4,
  "DimensionDistanceInside": 6.0,
  "DimensionDistanceOutside": 14.0,
  "DimensionDistanceWithDim": 6.0,
  "MleaderDistance": 6.0,
  "DimDistanceTolerance": 30.0,
  "RoadGapWidth": 5.0,
  "RoadCrosswalkWidth": 5.0,
  "RoadStopLineDistance": 2.0,
  "RoadStripeSpacing": 1.0,
  "AlignmentDefaultRadius": 30.0,
  "AlignmentDefaultSpiralIn": 0.0,
  "AlignmentDefaultSpiralOut": 0.0,
  "AlignmentDefaultStartStation": 0.0,
  "StationMainInterval": 20.0,
  "StationSubInterval": 5.0,
  "StationTickLengthMain": 4.0,
  "StationTickLengthSub": 1.5,
  "StationTextHeight": 3.0,
  "StationTextMargin": 0.5,
  "StationRotateTextAlongTangent": true,
  "StationTextSide": "Left",
  "Theme": "BlenderDark",
  "UiFontScale": 1.0,
  "UiDensityScale": 1.0,
  "UiInputWidthScale": 1.0,
  "EquipmentDataFilePath": ""
}
```

**与源码同步**：若增删 `SettingsData` 字段，请同时更新本文件与 [02-hy-settings-json-字段手册](./02-hy-settings-json-字段手册.md)。
