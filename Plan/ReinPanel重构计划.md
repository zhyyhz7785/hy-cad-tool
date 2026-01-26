# ReinPanel 重构计划

**日期**: 2025-10-11  
**目标**: 将原项目的 ReinPanel (166行 Code-Behind) 重构为 MVVM 架构

---

## 📊 现状分析

### 原项目结构
- **XAML**: 332 行
- **Code-Behind**: 166 行（包含业务逻辑）
- **配置属性**: 22 个
- **按钮事件**: 7 个（Apply, Reset, DrawReinforcement, DIM1/2/3, TextChanged）
- **依赖**: `Reinforcement` 静态类, `HyCommand` 静态类

### 重构目标
- **Code-Behind**: 166 行 → ~30 行 (**-82%**)
- **XAML**: 数据绑定率 100%
- **架构**: Bridge Pattern 适配原有逻辑
- **可测试性**: ViewModel 单元测试友好

---

## 🎯 重构步骤

### Step 1: 创建配置模型

**文件**: `Domain/Models/Configuration/ReinPanelConfig.cs`

```csharp
public class ReinPanelConfig
{
    // 基础参数
    public double Scale { get; set; } = 40.0;
    
    // 钢筋参数（7个）
    public double AnchorageLength { get; set; } = 500.0;
    public double DotSeparation { get; set; } = 200.0;
    public double BendingLineMinLength { get; set; } = 150.0;
    public double AnchorageJoinLength { get; set; } = 1500.0;
    public double HookLength { get; set; } = 1.0;
    public double ProtectionThickness { get; set; } = 1.0;
    public double ReinforcementDiameter { get; set} = 0.35;
    public double DotReinOffset { get; set; } = 1.35;
    
    // 标注参数（2个）
    public double RebarDiameter { get; set; } = 14.0;
    public double RebarSpacing { get; set; } = 200.0;
    
    // 尺寸参数（5个）
    public double DimensionDistanceInside { get; set; } = 6.0;
    public double DimensionDistanceOutside { get; set; } = 14.0;
    public double DimensionDistanceWithDim { get; set; } = 6.0;
    public double MleaderDistance { get; set; } = 6.0;
    public double DimDistanceTolerance { get; set; } = 30.0;
    
    // 其他参数
    public double TextXScale { get; set; } = 0.7;
    public double TextSize { get; set; } = 3.0;
}
```

---

### Step 2: 创建服务接口

**文件**: `Domain/Interfaces/IReinService.cs`

```csharp
public interface IReinService
{
    /// <summary>
    /// 应用样式（初始化 AutoCAD 样式）
    /// </summary>
    void ApplyStyle();
    
    /// <summary>
    /// 绘制钢筋
    /// </summary>
    void DrawReinforcement();
    
    /// <summary>
    /// 标注钢筋（多引线）
    /// </summary>
    void DimensionReinforcement();
    
    /// <summary>
    /// 标注钢筋（方式1）
    /// </summary>
    void DimensionReinforcementOne();
    
    /// <summary>
    /// 标注钢筋（方式2）
    /// </summary>
    void DimensionReinforcementTwo();
}
```

---

### Step 3: 实现服务适配器

**文件**: `Application/Services/ReinService.cs` (Bridge Pattern)

```csharp
public class ReinService : IReinService
{
    public void ApplyStyle()
    {
        BaseConfig.InitializeStyle();
    }
    
    public void DrawReinforcement()
    {
        HyCADTool.Commands.Reinforcement.Rein();
    }
    
    public void DimensionReinforcement()
    {
        HyCADTool.Commands.HyCommand.MleaderRein();
    }
    
    public void DimensionReinforcementOne()
    {
        HyCADTool.Commands.HyCommand.MleaderReinOne();
    }
    
    public void DimensionReinforcementTwo()
    {
        HyCADTool.Commands.HyCommand.MleaderReinTwo();
    }
}
```

---

### Step 4: 创建 ViewModel

**文件**: `Presentation/ViewModels/ReinPanelViewModel.cs`

**职责**:
- 管理 22 个配置属性
- 提供 5 个 ICommand（Apply, Reset, DrawReinforcement, DIM1/2/3）
- 双向绑定支持
- 自动同步到 `BaseConfig` 和原有逻辑

**代码量**: ~250 行

---

### Step 5: 创建 XAML

**文件**: `Presentation/Views/ReinPanel.xaml`

**特点**:
- 移除所有 `helper:TextBoxHelper`
- 使用 `{Binding}` 替换 `x:Name` + `TextChanged`
- 使用 `Command="{Binding ApplyCommand}"` 替换 `Click="Apply_Click"`
- 使用 `LibraryResources.xaml` 统一样式

---

### Step 6: 创建 Code-Behind

**文件**: `Presentation/Views/ReinPanel.xaml.cs`

**代码量**: ~30 行（极简化）

```csharp
public partial class ReinPanel : UserControl
{
    public ReinPanel()
    {
        InitializeComponent();
        
        if (DataContext == null && ServiceLocator.Container != null)
        {
            DataContext = ServiceLocator.Container.Resolve<ReinPanelViewModel>();
        }
    }
}
```

---

### Step 7: 注册 DI

**文件**: `Infrastructure/Configuration/AutofacModule.cs`

```csharp
// ReinPanel 及其服务
builder.RegisterType<ReinService>()
    .As<IReinService>()
    .SingleInstance();

builder.RegisterType<ReinPanelViewModel>()
    .AsSelf()
    .InstancePerDependency();

builder.RegisterType<ReinPanel>()
    .AsSelf()
    .InstancePerDependency();
```

---

### Step 8: 集成测试

**测试方式**:
1. 运行 `HYREFACTOR` 显示主面板
2. 切换到"钢筋"Tab
3. 验证功能：
   - 修改参数（Scale, AnchorageLength 等）
   - 点击"设置样式"
   - 点击"绘制"
   - 点击"标注钢筋"按钮
   - 点击"恢复默认值"

---

## 📈 预期效果

| 指标 | 原项目 | 重构后 | 优化 |
|------|--------|--------|------|
| Code-Behind 行数 | 166 | 30 | **-82%** |
| XAML 行数 | 332 | 280 | -15% |
| 数据绑定率 | 0% | 100% | +100% |
| 依赖注入 | ✖ | ✅ | 可测试 |

---

## 🚀 开始实施

请确认是否开始实施！我将逐步创建以上文件。

**预计用时**: 8 个步骤，约 10-15 分钟







