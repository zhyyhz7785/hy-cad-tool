# HY 设置面板（SettingsPanel）

## 目标

创建独立的 WPF 设置面板，使用 `TabControl` 结构，第一阶段只实现"样式设置"标签页。解决文字/标注/引线样式在重构后不一致的问题。后续其它修复（点钢筋、MLeader 创建逻辑等）在面板完成后再做。

## 核心设计

### 动态样式名称

与旧代码保持一致，样式名根据 Scale 动态生成：

- 文字样式: `0_Hy_{Scale}` (如 `0_Hy_40`)
- 标注样式: `0_Hy_{Scale}_Dim`
- 引线样式: `0_Hy_{Scale}_Mleader`
- 表格样式: `0_Hy_{Scale}_Table`

### 面板结构（TabControl）

```
SettingsPanel (TabControl)
├── Tab A: 样式设置  <-- 本次实现
│   ├── 比例 Scale
│   ├── 文字样式 (字体、高度、宽度比)
│   ├── 标注样式 (参数一览)
│   ├── 引线样式 (参数一览)
│   └── [应用样式] [恢复默认]
├── Tab B: 预留
├── Tab C: 预留
└── Tab D: 预留
```

------

## 需要创建/修改的文件

### 新建文件（4个）

1. **ViewModel**: `Presentation/ViewModels/SettingsPanelViewModel.cs`
   - 样式相关全部属性（Scale, 文字参数, 标注参数, 引线参数）
   - 计算属性：动态样式名 `TextStyleName => $"0_Hy_{Scale}"`
   - ApplyStyleCommand / ResetCommand
   - 调用 `IStyleService` 的完整方法
2. **View**: `Presentation/Views/SettingsPanel.xaml`
   - TabControl 骨架，暗色主题
   - Tab A: 样式设置，使用 Expander 分组（文字、标注、引线）
   - Tab B/C/D: 预留空白标签页
3. **Code-behind**: `Presentation/Views/SettingsPanel.xaml.cs`
   - 极简：构造函数接收 ViewModel，设置 DataContext
4. **Command**: 在 `ShowPanelCommand.cs` 中添加 `HYSET` 命令

### 修改文件（3个）

1. **`StyleService.cs`** -- 修复 `CreateMLeaderStyle`
   - 添加 `scale` 参数
   - 补全旧代码所有属性：TextHeight, TextColor, TextAttachmentType, ArrowSize(`_DotSmall`), LandingGap, LeaderLineWeight
   - 添加 `GetArrowObjectId` 私有辅助方法
2. **`StyleService.cs`** -- 修复 `CreateDimensionStyle`
   - 改用 `Dimscale` 方式（旧代码：`s.Dimscale = BaseConfig.Scale`，不逐个乘）
   - 补全缺失属性：Dimtofl, Dimtad, Dimtix, Dimtih, Dimtoh, Dimclrt, Dimdle
   - 补全箭头：`_ARCHTICK`
   - 样式已存在时更新（旧代码支持更新，新代码只创建不更新）
3. **`IStyleService.cs`** -- 接口变更
   - `CreateMLeaderStyle` 增加 `scale` 参数
4. **`AutofacModule.cs`** -- 注册新面板
   - 注册 SettingsPanelViewModel + SettingsPanel

------

## SettingsPanelViewModel 属性设计

### 基础属性

- `Scale` (double, 默认 40) -- 核心，其他样式名都依赖它

### 文字样式属性（对应旧 TextStyleConfig）

- `TextStyleName` (只读计算) = `$"0_Hy_{Scale}"`
- `FontFileName` (string) = `"tssdeng.shx"`
- `BigFontFileName` (string) = `"hztxt.shx"`
- `TextSize` (double) = `2.5` (基础值，实际 = TextSize * Scale)
- `TextXScale` (double) = `0.7`

### 标注样式属性（对应旧 DimStyleConfig）

- `DimStyleName` (只读计算) = `$"0_Hy_{Scale}_Dim"`
- `Dimtxt` = 2.5, `Dimexo` = 1.0, `Dimexe` = 1.0
- `Dimdle` = 0.5, `Dimgap` = 1.0, `Dimasz` = 1.0
- `DimArrowName` = `"_ARCHTICK"`

### 引线样式属性（对应旧 MLeaderStyleConfig/SetMleaderStyle）

- `MLeaderStyleName` (只读计算) = `$"0_Hy_{Scale}_Mleader"`
- `MLeaderArrowSize` = 2.0 (基础值)
- `MLeaderArrowName` = `"_DotSmall"`
- `MLeaderLandingGap` = 0.5 (基础值)
- `MLeaderTextColorIndex` = 7

### 命令

- `ApplyStyleCommand` -- 调用 StyleService 创建/更新全部样式，设为当前
- `ResetCommand` -- 恢复默认值

------

## 样式应用逻辑（ApplyStyle）

```
1. 计算动态名称
   textStyleName = $"0_Hy_{Scale}"
   dimStyleName  = $"0_Hy_{Scale}_Dim"
   mleaderStyleName = $"0_Hy_{Scale}_Mleader"

2. 创建/更新文字样式
   StyleService.CreateTextStyle(textStyleName, font, bigFont, TextSize*Scale, XScale)
   StyleService.SetCurrentTextStyle(textStyleName)

3. 创建/更新标注样式
   StyleService.CreateDimensionStyle(dimStyleName, textStyleName, Scale)
   StyleService.SetCurrentDimensionStyle(dimStyleName)

4. 创建/更新引线样式
   StyleService.CreateMLeaderStyle(mleaderStyleName, textStyleName, Scale)
   StyleService.SetCurrentMLeaderStyle(mleaderStyleName)
```

------

## XAML 布局草案（Tab A: 样式设置）

```
ScrollViewer
└── StackPanel
    ├── [比例] Scale TextBox
    ├── [样式名预览] 0_Hy_40 / 0_Hy_40_Dim / 0_Hy_40_Mleader (只读)
    ├── Expander "文字样式"
    │   ├── 字体文件: tssdeng.shx
    │   ├── 大字体: hztxt.shx
    │   ├── 基础文字高度: 2.5
    │   └── 宽度比: 0.7
    ├── Expander "标注样式"
    │   ├── 文字高度: 2.5
    │   ├── 箭头大小: 1.0
    │   ├── 箭头类型: _ARCHTICK
    │   ├── 尺寸线超出: 0.5
    │   └── (其他参数...)
    ├── Expander "引线样式"
    │   ├── 箭头大小: 2.0
    │   ├── 箭头类型: _DotSmall
    │   ├── 着陆间距: 0.5
    │   └── 文字颜色: 7(白)
    ├── [应用样式] Button
    └── [恢复默认] Button
```