# ReinPanel.Refactored 使用指南

## 快速开始

### 1. 编译项目

```bash
cd ReinPanel.Refactored
dotnet build
```

**输出位置**: `ReinPanel.Refactored/bin/Debug/net48/ReinPanel.Refactored.dll`

---

### 2. 在 AutoCAD 中加载

#### 方法 1: 使用热加载（推荐）

1. **加载 ReCall.dll**（首次）
   ```
   NETLOAD → 选择 ReCall/bin/Debug/ReCall.dll
   ```

2. **重新加载 ReinPanel**
   ```
   C2
   ```
   输出：
   ```
   ========================================
     ReinPanel.Refactored 热加载工具
   ========================================
   ✓ DLL 已复制到临时文件
   ✓ 依赖注入容器已初始化
   ✓ ReinPanel.Refactored.dll 已重新加载
   ========================================
     使用 C1 命令显示面板
     使用 gj 命令绘制钢筋
     使用 gb/gb1/gb2 命令标注钢筋
   ========================================
   ```

3. **显示面板**
   ```
   C1
   ```

#### 方法 2: 直接加载

```
NETLOAD → 选择 ReinPanel.Refactored/bin/Debug/net48/ReinPanel.Refactored.dll
```

---

## 命令列表

### 热加载命令

| 命令 | 功能 | 说明 |
|------|------|------|
| `C2` | 重新加载 DLL | 修改代码后编译，使用此命令重新加载 |
| `C1` | 显示钢筋面板 | 显示或激活钢筋配置面板 |

### 面板命令

| 命令 | 功能 | 标志 |
|------|------|------|
| `TESTREINPANEL` | 显示钢筋面板 | Session |
| `INITREINPANEL` | 初始化容器 | Session（自动调用）|

### 功能命令

| 命令 | 功能 | 模式 |
|------|------|------|
| `gj` | 绘制钢筋 | Modal |
| `gb` | 三点标注钢筋 | Modal |
| `gb1` | 单点标注钢筋 | Modal |
| `gb2` | 六点标注钢筋 | Modal |

---

## 开发工作流

### 标准流程

```
1. 修改代码
   ↓
2. 编译项目 (dotnet build)
   ↓
3. 在 AutoCAD 中执行 C2
   ↓
4. 执行 C1 测试面板
   ↓
5. 测试功能命令 (gj, gb, gb1, gb2)
   ↓
6. 重复步骤 1-5
```

### 示例

```bash
# 1. 修改代码
# 编辑 Domain/Services/ReinforcementService.cs

# 2. 编译
cd ReinPanel.Refactored
dotnet build

# 3. 在 AutoCAD 中
C2      # 重新加载
C1      # 显示面板
gj      # 测试绘制
```

---

## 面板使用

### 参数说明

#### 基础参数
- **主图形比例**: 默认 40.0
- **钢筋直径**: 默认 12.0
- **钢筋间距**: 默认 200.0

#### 钢筋参数（折叠面板）
- 钢筋锚固长度: 500.0
- 点钢筋间距: 200.0
- 钢筋弯折最小长度: 150.0
- 锚固钢筋连接长度: 1500.0
- 弯钩长度: 1.0
- 保护层厚度: 1.0
- 点钢直径: 0.35
- 点钢偏移: 1.35

#### 尺寸参数（折叠面板）
- 内侧标注距离: 6.0
- 外标注距离: 14.0
- 标注间距: 6.0
- 引线距离: 6.0
- 允许距离: 30.0

### 按钮功能

| 按钮 | 功能 |
|------|------|
| **设置样式** | 应用当前参数到样式 |
| **恢复默认值** | 重置所有参数为默认值 |
| **绘制** | 绘制钢筋（同 gj 命令）|
| **标注钢筋** (左) | 三点标注（同 gb 命令）|
| **标注钢筋** (中) | 单点标注（同 gb1 命令）|
| **标注钢筋** (右) | 六点标注（同 gb2 命令）|

---

## 故障排除

### 问题 1: C2 命令提示"未找到插件文件"

**原因**: 项目未编译或路径错误

**解决**:
```bash
cd ReinPanel.Refactored
dotnet build
```

### 问题 2: C1 命令提示"请先使用 C2 命令加载插件"

**原因**: 未执行 C2 加载 DLL

**解决**:
```
C2
```

### 问题 3: 面板不显示

**原因**: 容器未初始化

**解决**:
```
INITREINPANEL
TESTREINPANEL
```

### 问题 4: 命令执行报错

**原因**: ReinforcementService 业务逻辑未实现

**状态**: 当前版本只有框架，业务逻辑待迁移

---

## 架构说明

### 项目结构

```
ReinPanel.Refactored/
├── Domain/              # 领域层（业务逻辑）
├── Infrastructure/      # 基础设施层（DI、配置）
├── Presentation/        # 表现层（UI、ViewModel）
└── Test/               # 测试命令
```

### 依赖关系

```
Presentation → Domain ← Infrastructure
```

### 关键类型

- **ReinParameters**: 参数值对象
- **IReinService**: 服务接口
- **ReinforcementService**: 服务实现
- **ReinPanelViewModel**: MVVM ViewModel
- **ServiceLocator**: DI 容器访问

---

## 下一步开发

### 待实现功能

1. **ReinforcementService 业务逻辑**
   - 从原 `Reinforcement` 类迁移代码
   - 重构静态方法为实例方法
   - 实现参数传递

2. **功能测试**
   - 绘制钢筋功能
   - 标注钢筋功能
   - 参数应用功能

3. **文档完善**
   - API 文档
   - 代码注释
   - 使用示例

---

## 参考资料

### 相关文档
- `Plan/ReinPanel.Refactored完成报告.md` - 完整实施报告
- `Plan/Plan.md` - 详细实施计划

### 原始代码位置
- `HyCADTool/Tools/CreatEntity/Rein/` - 原 Reinforcement 类
- `HyCADTool/Commands/gj.cs` - 原绘制命令
- `HyCADTool/Commands/ReinCommands/ReinCommand03.cs` - 原标注命令

---

**文档版本**: 1.0  
**更新日期**: 2025-10-11  
**项目状态**: ✅ 编译成功，待功能实现




