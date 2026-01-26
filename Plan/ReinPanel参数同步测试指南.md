# ReinPanel 参数同步功能 - 测试指南

## 📋 测试目标

验证重构后的 `ReinPanel` 能否正确将 ViewModel 中的参数同步到原有系统，确保原有命令（如 `GB1`、`GB2`）能正常使用面板参数。

---

## 🔧 测试环境设置

### 前提条件
- AutoCAD 已打开
- **ReCall.dll 已加载**（一次性操作，后续无需重启 AutoCAD）

### 热加载工作流程
```
1️⃣ 首次加载 ReCall.dll
   - 命令: NETLOAD → 选择 ReCall.dll
   
2️⃣ 加载 HyCADTool.Refactored.dll
   - 命令: C2
   - 输出: "插件加载成功"
   
3️⃣ 运行测试
   - 命令: C1
   - 查看测试结果
   
4️⃣ 修改代码后重新测试（无需重启 AutoCAD）
   - 编译: MSBuild HyCADtoolGpt.sln /t:HyCADTool_Refactored
   - 重新加载: C2
   - 再次测试: C1
```

---

## 📝 测试步骤

### 测试 1: 自动化参数同步测试

#### 操作步骤
1. 在 AutoCAD 命令行输入：
   ```
   C2
   ```
   （加载插件）

2. 输入：
   ```
   C1
   ```
   （运行自动化测试）

#### 预期输出
```
========================================
  HyCADTool.Refactored 测试命令
========================================

=== 测试 1: ReinPanel 参数同步 ===
✅ ServiceLocator.Container 已初始化
✅ ReinPanel 已成功解析
✅ ReinPanel.ActivePanel 已设置

--- 默认参数值 ---
  钢筋直径 (RebarDiameter): 14
  钢筋间距 (RebarSpacing): 200
  比例 (Scale): 40
  锚固长度 (AnchorageLength): 500
  文字大小 (TextSize): 3
  钩长 (HookLength): 1
  保护层厚度 (ProtectionThickness): 1

✅ ViewModel 已正确绑定

--- 测试参数修改 ---
  正在修改 RebarDiameter: 14 → 18
  读取 ActivePanel.RebarDiameter: 18
  ✅ 参数同步成功！

  正在修改 RebarSpacing: 200 → 150
  读取 ActivePanel.RebarSpacing: 150
  ✅ 参数同步成功！

--- 测试说明 ---
  请在 AutoCAD 中执行以下操作验证：
  1. 运行 HYREFACTOR 显示主面板
  2. 在 [钢筋] 面板中修改参数（如钢筋直径改为 20）
  3. 再次运行 C1，查看参数是否更新
  4. 运行原有命令（如 GB1、GB2）验证功能

✅ 测试 1 完成

========================================
  测试完成
========================================
```

#### ✅ 判断标准
- 所有输出显示 `✅`
- 参数修改后能正确读取新值

---

### 测试 2: 手动 UI 参数同步测试

#### 操作步骤

1. **显示主面板**
   ```
   HYREFACTOR
   ```

2. **修改参数**
   - 在 "钢筋" Tab 中：
     - 钢筋直径：`14` → `20`
     - 钢筋间距：`200` → `150`
     - 比例：`40` → `50`

3. **验证参数同步**
   ```
   C1
   ```
   查看输出中的 "默认参数值" 部分，应显示：
   ```
   钢筋直径 (RebarDiameter): 20
   钢筋间距 (RebarSpacing): 150
   比例 (Scale): 50
   ```

#### ✅ 判断标准
- UI 修改的值能在 `C1` 输出中正确显示

---

### 测试 3: 原有命令功能验证

#### 操作步骤

1. **绘制测试图形**
   - 在 AutoCAD 中绘制一些线条或多段线

2. **修改钢筋参数**
   - 运行 `HYREFACTOR`
   - 在 "钢筋" 面板中设置：
     - 钢筋直径：`18`
     - 钢筋间距：`100`

3. **执行原有命令**
   ```
   GB1
   ```
   或
   ```
   GB2
   ```

4. **检查绘制结果**
   - 钢筋直径是否为 18mm
   - 钢筋间距是否为 100mm

#### ✅ 判断标准
- 原有命令能正常运行
- 使用的参数与面板设置一致

---

## 🐛 常见问题排查

### 问题 1: "未找到类型 'RefactoredTestCommand'"

**原因**: 插件未正确加载

**解决方案**:
```
C2
```
（重新加载插件）

---

### 问题 2: 参数没有同步

**检查项**:
1. 运行 `C1`，查看 "ReinPanel.ActivePanel 已设置" 是否显示 ✅
2. 如果显示 ❌，说明 `ActivePanel` 未正确初始化

**解决方案**:
```
C2     （重新加载）
HYREFACTOR  （显示面板以初始化 ActivePanel）
C1     （再次测试）
```

---

### 问题 3: 原有命令报错

**检查项**:
1. 确认 `HyCADTool.dll` 是否已加载
2. 确认原有命令是否依赖其他未加载的模块

**解决方案**:
- 如果 `GB1`/`GB2` 不存在，请在原项目中找到对应的命令名称
- 使用 `HYREFACTOR` 面板中的功能按钮替代命令行命令

---

## 📊 测试报告模板

### 测试环境
- AutoCAD 版本: ___________
- 测试日期: ___________

### 测试结果

| 测试项 | 预期结果 | 实际结果 | 状态 |
|--------|----------|----------|------|
| 测试 1: 自动化参数同步 | 所有 ✅ | _______ | ☐ 通过 ☐ 失败 |
| 测试 2: UI 参数同步 | 参数正确显示 | _______ | ☐ 通过 ☐ 失败 |
| 测试 3: 原有命令功能 | 命令正常运行 | _______ | ☐ 通过 ☐ 失败 |

### 问题记录
```
（记录遇到的问题和解决方案）
```

---

## 🎯 下一步测试计划

测试通过后，在 `RefactoredTestCommand.cs` 中：

1. **注释掉当前测试**：
   ```csharp
   // TestReinPanelParameterSync(ed);
   ```

2. **编写新测试**：
   - 测试 FilterPanel 功能
   - 测试 BaseReinPanel 功能
   - 测试 PilePanel 功能
   - 测试 ClusterPanel 功能

3. **重新编译并测试**：
   ```
   MSBuild → C2 → C1
   ```

---

## 📖 技术细节

### 参数同步机制

#### 原理
```csharp
// ReinPanel.xaml.cs
public static ReinPanel ActivePanel { get; set; }

public double RebarDiameter => ViewModel?.RebarDiameter ?? 14.0;
```

#### 调用链
```
原有系统 (ReinforcementMain.cs)
  ↓
ReinPanel.ActivePanel.RebarDiameter
  ↓
ReinPanel.ViewModel.RebarDiameter
  ↓
ReinPanelConfig.RebarDiameter
```

#### 关键代码位置
- **面板代码**: `HyCADTool.Refactored/Presentation/Views/ReinPanel.xaml.cs`
- **ViewModel**: `HyCADTool.Refactored/Presentation/ViewModels/ReinPanelViewModel.cs`
- **Config**: `HyCADTool.Refactored/Domain/Models/Configuration/ReinPanelConfig.cs`
- **原有系统**: `HyCADTool/Tools/CreatEntity/Rein/ReinforcementMain - 复制.cs`

---

## ✅ 测试完成标志

- [ ] 测试 1 通过（自动化参数同步）
- [ ] 测试 2 通过（UI 参数同步）
- [ ] 测试 3 通过（原有命令功能）
- [ ] 已注释当前测试代码
- [ ] 已编写新测试（如有需要）

完成以上所有项后，可以继续下一个功能的测试！🎉






