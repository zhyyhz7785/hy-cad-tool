# HyCADTool.Refactored 架构设计规范

> **版本**: v1.0  
> **制定日期**: 2025-10-29  
> **适用范围**: 所有新命令开发和现有命令重构  
> **核心原则**: Clean Architecture + 务实主义

---

## 📋 核心要点（快速参考）

### ✅ 关键决策

1. **Workflow 必须放在 Infrastructure 层**
   - ✅ Workflow 必然依赖 AutoCAD API（Transaction, ObjectId, Curve）
   - ❌ 不能放在 Application 层（违反 Clean Architecture）
   - ✅ 放在 `Infrastructure/AutoCAD/Workflows/`

2. **Command 只负责用户交互**
   - ✅ 选择对象、输入参数
   - ✅ 调用 Workflow 或 Service
   - ✅ 格式化输出（≤ 3行）
   - ❌ 不包含复杂业务逻辑

3. **简单命令不使用 Workflow**
   - < 300行 → Command 直接调用 Service ✅
   - 300-500行 → 视情况使用 Workflow
   - > 500行 → 强烈推荐 Workflow ✅

4. **测试策略清晰**
   - Domain 层：100% 单元测试（脱离 AutoCAD）✅
   - Infrastructure 层：集成测试（在 AutoCAD 环境）⚠️
   - Presentation 层：E2E 测试（C1/C2）⚠️

---

## 📋 详细目录

1. [核心架构原则](#1-核心架构原则)
2. [分层职责定义](#2-分层职责定义)
3. [Workflow 层使用规范](#3-workflow-层使用规范)
4. [命令开发决策树](#4-命令开发决策树)
5. [测试策略](#5-测试策略)
6. [代码检查清单](#6-代码检查清单)
7. [重构指南](#7-重构指南)

---

## 1. 核心架构原则

### 1.1 Clean Architecture 的本质

**依赖规则**：依赖只能向内（向稳定方向）

```
┌─────────────────────────────────────────────────────────┐
│  Presentation (UI/Commands)                             │
│  - 最不稳定（经常变化）                                  │
│  - 可以依赖所有层                                        │
└────────────────────┬────────────────────────────────────┘
                     │ 依赖
                     ▼
┌─────────────────────────────────────────────────────────┐
│  Infrastructure (Platform-Specific Implementation)       │
│  - 平台特定实现（AutoCAD API）                           │
│  - 可以依赖 Domain，不能依赖 Presentation                │
└────────────────────┬────────────────────────────────────┘
                     │ 依赖
                     ▼
┌─────────────────────────────────────────────────────────┐
│  Domain (Business Logic - Platform Independent)         │
│  - 最稳定（很少变化）                                    │
│  - 零依赖（100% 可迁移到其他平台）                       │
└─────────────────────────────────────────────────────────┘
```

### 1.2 关键洞察：Workflow 必然依赖 AutoCAD API

**核心认知**：
```csharp
// Workflow 的职责是编排 AutoCAD 特定的业务流程
public class SomeWorkflow
{
    public Result Execute(
        ObjectId[] objects,      // ❌ AutoCAD 类型
        Transaction trans)        // ❌ AutoCAD 类型
    {
        // ❌ 必须使用 AutoCAD API
        var ent = trans.GetObject(objId, OpenMode.ForRead);
        
        // ✅ 转换到 Domain 对象
        var domainObj = ConvertToDomain(ent);
        
        // ✅ 调用平台无关算法
        var result = _domainService.Process(domainObj);
        
        // ❌ 转换回 AutoCAD 对象
        var cadObj = ConvertToCAD(result);
        
        return result;
    }
}
```

**结论**：
- ✅ **Workflow 应该放在 `Infrastructure/AutoCAD/Workflows/`**
- ✅ **可以依赖 AutoCAD API（这是合理的）**
- ❌ **不应该放在 Application 层（违反 Clean Architecture）**

---

## 2. 分层职责定义

### 2.1 Presentation Layer (表示层)

**位置**：`Presentation/Commands/`

**职责**：
- ✅ 用户交互（选择对象、输入参数）
- ✅ 输出格式化（控制台输出）
- ✅ 命令注册（`[CommandMethod]`）
- ❌ 不包含复杂业务逻辑

**代码规模**：100-300 行

---

### 2.2 Infrastructure Layer (基础设施层)

#### 2.2.1 Workflows（业务流程编排）

**位置**：`Infrastructure/AutoCAD/Workflows/`

**职责**：
- ✅ 复杂 AutoCAD 业务流程编排
- ✅ 多阶段处理协调
- ✅ 时间分析和性能监控
- ✅ **可以依赖 AutoCAD API**
- ✅ **可以在多个 Command 间复用**

**代码规模**：300-1000 行

---

#### 2.2.2 Services（AutoCAD 数据库操作）

**位置**：`Infrastructure/AutoCAD/Services/`

**职责**：
- ✅ AutoCAD 数据库读写
- ✅ AutoCAD 对象转换
- ✅ AutoCAD 特定工具
- ❌ 不包含业务逻辑

**代码规模**：每个 Service 50-300 行

---

### 2.3 Domain Layer (领域层)

**位置**：`Domain/`

**职责**：
- ✅ **100% 平台无关**的业务逻辑和算法
- ✅ 值对象（`Line2D`, `Point2D`, `Polygon2D`）
- ✅ 领域服务（空间索引、几何计算）
- ❌ **零依赖**
- ✅ **100% 可迁移**到 Blender Python

**代码规模**：每个 Service 100-500 行

---

## 3. Workflow 层使用规范

### 3.1 何时使用 Workflow？

| 场景 | 是否使用 Workflow | 理由 |
|------|------------------|------|
| **命令 < 300 行** | ❌ 不使用 | 业务逻辑简单，直接在 Command 中处理 |
| **命令 300-500 行** | 🤔 视情况而定 | 如果有复杂流程编排，建议使用 |
| **命令 > 500 行** | ✅ 强烈推荐 | 必须分离业务逻辑 |
| **多入口调用** | ✅ 必须使用 | 需要被多个 Command 或 API 复用 |
| **需要详细性能分析** | ✅ 推荐使用 | Workflow 可系统化记录各阶段耗时 |
| **多阶段处理（>5个阶段）** | ✅ 推荐使用 | 流程编排更清晰 |

---

### 3.2 Workflow vs Command 的职责划分

```
Command (Presentation) - 100-200 行
  ├─ 用户交互（选择、输入参数）
  ├─ 调用 Workflow 或 Service
  ├─ 格式化输出（简洁）
  └─ 更新图纸（最终事务提交）
       ↓
Workflow (Infrastructure) - 300-1000 行
  ├─ 复杂业务流程编排（多阶段处理）
  ├─ AutoCAD 数据库操作（只读事务）
  ├─ 时间分析和性能监控
  └─ 返回结构化结果
       ↓
Service (Infrastructure) + Domain (Platform-Free)
  ├─ 原子操作（AutoCAD 数据库读写、对象转换）
  └─ 平台无关算法（100% 可测试）
```

---

## 4. 命令开发决策树

```
开始开发新命令
    ↓
评估命令复杂度
    ├─ 预估代码行数
    ├─ 计算业务流程阶段数
    └─ 判断是否有复用需求
    ↓
┌────────┴────────┐
│                 │
[简单命令]    [复杂命令]
< 300 行      > 300 行
│                 │
↓                 ↓
不使用 Workflow   使用 Workflow
Command           Command
  ↓                 ↓
Services          Workflow
  ↓                 ↓
Domain            Services
                    ↓
                  Domain
```

---

## 5. 测试策略

### 5.1 测试金字塔

```
           ┌────────┐
           │ E2E测试│  ← AutoCAD 命令测试（C1/C2）
           │  10%   │
           └────────┘
       ┌──────────────┐
       │  集成测试     │  ← Workflow 测试（在 CAD 环境）
       │    30%       │
       └──────────────┘
   ┌──────────────────────┐
   │    单元测试           │  ← Domain 层测试（100% 覆盖）
   │      60%             │
   └──────────────────────┘
```

### 5.2 关键认知：AutoCAD 命令本身无法脱离 AutoCAD 测试

**这是正确的！**

```
❌ 错误期望：完全脱离 AutoCAD 环境测试所有代码
✅ 正确策略：
   - Domain 层：100% 单元测试（脱离 AutoCAD）
   - Infrastructure 层：集成测试（在 AutoCAD 环境）
   - Presentation 层：E2E 测试（手动 + 自动化）
```

**测试成本降低策略**：
1. **最大化 Domain 层代码**（可脱离 CAD 测试）
2. **Workflow 尽量薄**（只做编排，不包含算法）
3. **使用热重启**（C2）提高测试效率
4. **自动化测试**（C1 + TestRunner）

---

## 6. 代码检查清单

### 6.1 开发前检查

- [ ] **架构决策**：是否使用 Workflow？
- [ ] **依赖分析**：需要哪些 Service 和 Domain 服务？
- [ ] **数据流设计**：AutoCAD → Domain → AutoCAD 的转换路径
- [ ] **性能预估**：是否需要详细的阶段时间分析？

### 6.2 开发中检查

- [ ] **Command 职责单一**：只负责用户交互和输出格式化
- [ ] **Workflow 位置正确**：在 `Infrastructure/AutoCAD/Workflows/`
- [ ] **Service 位置正确**：在 `Infrastructure/AutoCAD/Services/`
- [ ] **Domain 层零依赖**：不依赖任何 AutoCAD 类型
- [ ] **输出精简**：RELEASE 模式 ≤ 3 行
- [ ] **异常处理**：关键路径用空 `catch`（性能优先）

### 6.3 开发后检查

- [ ] **编译通过**：0 个错误
- [ ] **Linter 检查**：无警告
- [ ] **C2/C1 测试**：功能正常，性能符合要求
- [ ] **输出验证**：简洁且有效
- [ ] **代码审查**：符合架构规范
- [ ] **文档更新**：命令说明、架构文档

---

## 7. 重构指南

### 7.1 现有问题识别

**问题：HyApplication/Services/ 中的服务依赖 AutoCAD API**

```
❌ 当前结构（违反 Clean Architecture）
HyApplication/Services/
  ├─ ElevationDataExtractor.cs      ❌ using Autodesk.AutoCAD
  ├─ WallGenerationService.cs       ❌ using Autodesk.AutoCAD
  ├─ SlabGenerationService.cs       ❌ using Autodesk.AutoCAD
  └─ WallGeometryCalculator.cs      ❌ using Autodesk.AutoCAD
```

**解决方案**：移动到 `Infrastructure/AutoCAD/Workflows/`

```
✅ 正确结构
Infrastructure/AutoCAD/Workflows/
  ├─ Elevation3DWorkflow.cs         ✅ 编排整体流程
  │   ├─ ElevationDataExtractor     ✅ 提取数据
  │   ├─ WallGenerationService      ✅ 生成墙体
  │   ├─ SlabGenerationService      ✅ 生成筏板
  │   └─ WallGeometryCalculator     ✅ 几何计算
```

---

### 7.2 重构优先级

| 命令/模块 | 当前行数 | 是否需要 Workflow | 优先级 | 预计工作量 |
|----------|---------|------------------|--------|----------|
| **C15 (Elevation3D)** | 1000+ | ✅ 是 | ⭐⭐⭐⭐⭐ | 2-3 天 |
| **HyApplication/Services/** | - | ✅ 需要移动 | ⭐⭐⭐⭐⭐ | 1 天 |
| **HYBC** | 319 | ❌ 否（已优化） | ✅ 完成 | - |
| **HYOV** | 299 | ❌ 否 | ✅ 完成 | - |

---

## 8. 常见问题 (FAQ)

### Q1: 为什么 Workflow 不能放在 Application 层？

**A**: Application 层应该是平台无关的，但 Workflow 必然依赖 AutoCAD API（Transaction, ObjectId, Curve 等），因此只能放在 Infrastructure 层。

---

### Q2: 简单命令是否必须使用 Workflow？

**A**: 不必须。对于 < 300 行的简单命令，直接在 Command 中调用 Service 更简洁高效。

---

### Q3: 如何测试依赖 AutoCAD API 的代码？

**A**: 
- **Domain 层**: 100% 单元测试（脱离 AutoCAD）
- **Infrastructure 层**: 集成测试（在 AutoCAD 环境，使用 C1/C2）
- **Presentation 层**: E2E 测试（手动 + 自动化）

**关键**：最大化 Domain 层代码，最小化 Infrastructure 层代码。

---

### Q4: 是否会过度工程化？

**A**: 遵循决策树，根据命令复杂度选择：
- 简单命令（< 300行）→ 不使用 Workflow ✅
- 复杂命令（> 500行）→ 使用 Workflow ✅

**务实主义**：不为简单问题创建复杂方案。

---

## 9. 总结

### 9.1 核心原则

1. ✅ **Command 只负责用户交互**
2. ✅ **Workflow 负责复杂业务编排**（在 Infrastructure 层）
3. ✅ **Service 负责原子操作**
4. ✅ **Domain 层 100% 平台无关**
5. ✅ **依赖方向：Presentation → Infrastructure → Domain**
6. ✅ **务实主义：简单命令不使用 Workflow**

### 9.2 决策树

```
命令 < 300 行        → Command → Service → Domain
命令 300-500 行      → 视情况使用 Workflow
命令 > 500 行        → Command → Workflow → Service → Domain
多入口调用           → 必须使用 Workflow
需要详细性能分析     → 推荐使用 Workflow
```

### 9.3 立即行动

1. **重构 HyApplication/Services/**
   - 移动到 `Infrastructure/AutoCAD/Workflows/`
   - 创建 `Elevation3DWorkflow`

2. **审查现有命令**
   - HYBC/HYOV: ✅ 保持当前架构（无 Workflow）
   - C15: ⚠️ 需要重构（添加 Workflow）

3. **新命令开发**
   - 使用本规范的决策树
   - 开发前确定架构
   - 严格遵守分层规则

---

**架构规范制定完成！请严格遵守！** 📐

**版本**: v1.0  
**制定日期**: 2025-10-29  
**维护者**: 开发团队


