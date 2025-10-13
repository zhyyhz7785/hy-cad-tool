# ReinPanel.Refactored 测试说明

## 当前状态

✅ **项目编译成功**  
✅ **框架实现完成**  
⏳ **业务逻辑部分实现**（框架级别）

---

## 测试方法

### 方法 1: 直接加载 DLL（推荐）

由于 ReCall 项目有编译问题，建议使用此方法：

#### 步骤 1: 在 AutoCAD 中加载

```
命令: NETLOAD
```

选择文件：
```
E:\BaiduSyncdisk\Code\CSharp\CursorProjects\hy-cad-tool\ReinPanel.Refactored\bin\Debug\net48\ReinPanel.Refactored.dll
```

#### 步骤 2: 初始化容器（自动执行）

```
命令: INITREINPANEL
```

**输出**：
```
✓ ReinPanel 容器初始化成功
```

#### 步骤 3: 显示面板

```
命令: TESTREINPANEL
```

**预期结果**：
- 右侧显示钢筋配置面板
- 包含所有参数输入框
- 包含 6 个按钮

#### 步骤 4: 测试面板功能

1. **修改参数**
   - 修改"主图形比例"为 50
   - 修改"钢筋直径"为 16
   - 修改"钢筋间距"为 150

2. **点击按钮**
   - 点击"恢复默认值" → 所有参数重置
   - 点击"设置样式" → 输出提示信息
   - 点击"绘制" → 提示选择多段线

#### 步骤 5: 测试命令

```
命令: gj
```

**预期流程**：
1. 提示"请选择多段线..."
2. 选择一个多段线
3. 输出"开始绘制钢筋，比例: 40"
4. 输出"钢筋直径: 12, 间距: 200"
5. 输出"✓ 钢筋绘制完成"

**当前状态**：框架实现，不会实际绘制钢筋（辅助方法返回空数组）

---

### 方法 2: 使用热加载（需要修复 ReCall）

如果您修复了 ReCall 项目的编译问题：

```
1. 命令: C2  (重新加载 DLL)
2. 命令: C1  (显示面板)
3. 修改代码 → 编译 → C2 → C1 测试
```

---

## 当前实现状态

### ✅ 已实现

1. **完整的架构框架**
   - Domain 层（接口、值对象、服务框架）
   - Presentation 层（ViewModel、View、XAML）
   - Infrastructure 层（DI 配置）

2. **完整的 UI 功能**
   - 参数输入和绑定
   - 命令绑定
   - 恢复默认值

3. **基本的业务流程**
   - 选择 Polyline
   - 参数验证
   - 错误处理
   - 图形添加框架

### ⏳ 待实现（标记为 TODO）

以下方法需要从原代码迁移：

1. **GetSubReinforcements** - 钢筋分段算法
2. **ConnectReinByCondition** - 钢筋连接逻辑
3. **GetSubReinforcementWithAnchors** - 锚固计算
4. **AddHooks** - 弯钩添加
5. **GetDotReinCenterPoly** - 点钢中心线
6. **AddDotRein** - 点钢生成
7. **AddReduceDotRein** - 减少点钢
8. **PointsToDotRein** - 点转钢筋
9. **AddMleaders** - 引线标注
10. **PreprocessBoundary** - 边界预处理
11. **CreateLayers** - 图层创建
12. **GenerateDimension** - 标注生成

---

## 测试预期

### 当前版本测试

执行 `gj` 命令：

**✅ 会正常工作的部分**：
- 显示提示信息
- 选择 Polyline
- 参数验证
- 输出日志

**⏳ 不会实际执行的部分**：
- 钢筋分段计算（返回空数组）
- 锚固和弯钩（返回空数组）
- 点钢生成（返回空数组）
- 实际绘制到 AutoCAD

**预期结果**：
```
请选择多段线...
[选择多段线]
开始绘制钢筋，比例: 40
钢筋直径: 12, 间距: 200
✓ 钢筋绘制完成
```

但不会在 AutoCAD 中看到实际的钢筋图形。

---

## 下一步开发

### 优先级 1: 实现核心几何算法

**文件**: `ReinPanel.Refactored/Domain/Services/ReinforcementService.cs`

**需要迁移的方法**（按依赖顺序）：

1. **GetSubReinforcements** (60行)
   - 从 `ReinforcementFun.cs` 第60行迁移
   - 依赖：GetPolySegmentAngle, GetLineSegmentAt 等扩展方法

2. **ConnectReinByCondition** (需要查找)
   - 连接钢筋的条件判断

3. **GetSubReinforcementWithAnchors** (100行)
   - 从 `ReinforcementFun.cs` 第101行迁移
   - 添加锚固长度

4. **AddHooks** (Addhook 方法，116行)
   - 从 `ReinforcementFun.cs` 第116行迁移
   - 添加弯钩

### 优先级 2: 实现辅助方法

需要创建扩展方法类或工具类：

**文件**: `ReinPanel.Refactored/Infrastructure/AutoCAD/PolylineExtensions.cs`

```csharp
public static class PolylineExtensions
{
    public static double[] GetPolySegmentAngle(this Polyline polyline);
    public static LineSegment3d GetLineSegmentAt(this Polyline polyline, int index);
    public static Point2d Convert2d(this Point3d point, Plane plane);
    public static void JoinEntity(this Polyline poly1, Polyline poly2);
    // ... 更多扩展方法
}
```

### 优先级 3: 实现图层和样式

**文件**: `ReinPanel.Refactored/Infrastructure/AutoCAD/LayerManager.cs`

```csharp
public static class LayerManager
{
    public static void CreateMultipleLayers(params (string name, short color)[] layers);
    public static void SetCurrentLayer(string layerName);
}
```

---

## 开发建议

### 渐进式实现

1. **第一步**：实现 `GetSubReinforcements` 方法
   - 这是最核心的算法
   - 测试：执行 gj 命令，检查是否能生成钢筋分段

2. **第二步**：实现锚固和弯钩
   - `GetSubReinforcementWithAnchors`
   - `AddHooks`
   - 测试：检查钢筋是否有锚固和弯钩

3. **第三步**：实现点钢
   - `AddDotRein`
   - `PointsToDotRein`
   - 测试：检查是否生成点钢筋

4. **第四步**：实现标注
   - `AddMleaders`
   - `GenerateDimension`
   - 测试：检查标注是否正确

### 测试驱动开发

每实现一个方法后：

```
1. 编译
2. NETLOAD 重新加载（或重启 AutoCAD）
3. 执行 gj 命令测试
4. 检查 AutoCAD 图形
5. 验证功能正确性
```

---

## 故障排除

### 问题 1: 命令不可用

**原因**: DLL 未加载

**解决**: 
```
NETLOAD → 选择 ReinPanel.Refactored.dll
```

### 问题 2: 选择 Polyline 后没有反应

**原因**: 辅助方法返回空数组

**状态**: 正常，待实现业务逻辑

### 问题 3: 面板参数修改后命令没有使用新参数

**原因**: ViewModel 是单例，需要重新获取

**解决**: 当前 ViewModel 是 `InstancePerDependency`，每次调用会创建新实例

---

## 总结

**当前版本**：
- ✅ 架构完整
- ✅ 编译成功
- ✅ 可以加载和测试
- ⏳ 业务逻辑待迁移

**下一步**：
1. 逐个实现 TODO 标记的方法
2. 从原代码迁移几何算法
3. 测试每个功能模块

**预计工作量**：
- 核心算法迁移：4-6 小时
- 辅助方法实现：2-3 小时
- 测试和调试：2-3 小时
- **总计**：8-12 小时

---

**文档版本**: 1.0  
**更新时间**: 2025-10-11  
**项目状态**: ✅ 框架完成，可开始业务逻辑迁移



