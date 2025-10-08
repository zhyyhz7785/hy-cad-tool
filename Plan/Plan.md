# AutoCAD 插件重构执行计划

## 📐 架构设计原则

**核心目标**: 平台无关的领域层 + 可替换的基础设施层，为 Blender 迁移铺路

**分层规则**:
- `Domain/`: 零 AutoCAD 依赖，纯 C# 领域模型和算法
- `Application/`: 业务用例编排,依赖接口而非实现
- `Infrastructure/AutoCAD/`: AutoCAD API 封装，实现领域接口
- `Presentation/`: 命令和 UI，协调应用层和基础设施层

---

## 🏗️ 阶段 1: 项目基础设施搭建 ✅

**状态**: 已完成  
**文档**: [阶段1.md](./阶段1.md)

### 主要成果
- ✅ 创建新项目结构 `HyCADTool.Refactored/`
- ✅ 配置依赖注入容器（Autofac）
- ✅ 创建领域几何值对象（平台无关）
- ✅ 实现基础服务（几何、图层、样式）
- ✅ 创建类型转换器

### 测试要求
- 插件加载测试
- 几何计算测试（面积、质心、点在多边形内）
- 图层和样式创建测试

---

## 🔧 阶段 2: 底层几何组件迁移

**状态**: 待开始  
**预估时间**: 5-7 天

### 2.1 几何算法服务（领域层）
**目标文件**: `Domain/Services/GeometryAlgorithms/`

**迁移内容**:
- `PolygonAlgorithms.cs`: 多边形简化、凸性判断、三角剖分
- `IntersectionAlgorithms.cs`: 完善的交点计算
- `ConvexHullAlgorithm.cs`: Graham 扫描法、Jarvis 步进法
- `OffsetAlgorithm.cs`: 多边形偏移（基于 Clipper2）

### 2.2 Clipper2 适配器完善
- 完整的偏移操作封装
- 简化操作
- 路径转换优化

### 2.3 NetTopologySuite 适配器
- `Infrastructure/AutoCAD/Converters/NetTopologySuiteAdapter.cs`
- 拓扑关系判断
- 缓冲区分析

### 测试要求
- 多边形简化算法正确性
- 凸包计算准确性
- 偏移操作精度验证

---

## 🎨 阶段 3: 样式与配置管理

**状态**: 待开始  
**预估时间**: 2-3 天

### 3.1 配置文件加载
- JSON 配置读取
- 配置验证逻辑
- 默认值处理

### 3.2 配置管理服务
- `Infrastructure/Configuration/ConfigurationService.cs`
- 从原 `Config/ConfigManager.cs` 迁移

### 测试要求
- JSON 配置加载测试
- 配置验证测试
- 默认配置测试

---

## 🔨 阶段 4: 第一个简单功能迁移（验证架构）

**状态**: 待开始  
**预估时间**: 3-4 天

### 4.1 选择示范功能：OverKill
**原始命令**: `Commands/OverKill/OverKill.cs`

### 4.2 实现步骤
1. 创建领域实体 `Domain/Entities/DuplicateEntity.cs`
2. 创建应用用例 `Application/UseCases/OverKill/OverKillUseCase.cs`
3. 创建 AutoCAD 命令 `Presentation/Commands/OverKillCommand.cs`
4. 注册依赖到 Autofac

### 测试要求
- 端到端功能测试
- 架构依赖规则验证
- 性能对比测试

---

## 🏗️ 阶段 5: 核心业务功能迁移

### 5.1 地脚螺栓功能
**状态**: 待开始  
**预估时间**: 4-5 天

**迁移内容**:
- 领域实体：`Domain/Entities/AnchorBolt/`
- 数据持久化：`Infrastructure/AutoCAD/Persistence/AnchorBoltRepository.cs`
- 应用用例：`Application/UseCases/AnchorBolt/`

**测试要求**:
- 螺栓创建测试
- 数据持久化测试（ExtensionDictionary）
- 力学计算验证

---

### 5.2 桩基布置优化
**状态**: 待开始  
**预估时间**: 5-6 天

**迁移内容**:
- Voronoi 算法：`Domain/Services/PileLayout/VoronoiOptimizer.cs`
- Lloyd 优化器：`Domain/Services/PileLayout/LloydOptimizer.cs`
- 应用用例：`Application/UseCases/PileLayout/`

**测试要求**:
- Voronoi 图正确性
- Lloyd 迭代收敛性
- 布桩结果合理性验证

---

### 5.3 基础底板配筋
**状态**: 待开始  
**预估时间**: 6-8 天

**迁移内容**:
- 领域聚合根：`Domain/Entities/Reinforcement/BaseReinforcementLayout.cs`
- 多步骤流程：`Application/UseCases/BaseReinforcement/`
- 配筋算法：`Domain/Services/ReinforcementAlgorithms/`

**测试要求**:
- 配筋生成正确性
- 标注准确性
- 完整流程端到端测试

---

## 🧪 阶段 6: 测试与验证

**状态**: 待开始  
**预估时间**: 3-5 天

### 6.1 单元测试项目
- 创建 `HyCADTool.Refactored.Tests/` 项目
- 添加 `NetArchTest.Rules` NuGet 包
- 编写架构一致性测试

### 6.2 测试覆盖
- Domain 层单元测试（目标覆盖率 > 60%）
- Application 层集成测试
- Infrastructure 层 AutoCAD 集成测试

### 测试要求
- 所有架构测试通过
- 领域层无 AutoCAD 依赖
- 核心算法单元测试通过

---

## 📚 阶段 7: 文档与知识提取

**状态**: 待开始  
**预估时间**: 2-3 天

### 7.1 领域知识文档
- `Docs/DomainKnowledge/ReinforcementCalculation.md`
- `Docs/DomainKnowledge/VoronoiOptimization.md`
- `Docs/DomainKnowledge/StructuralMechanics.md`

### 7.2 架构决策记录（ADR）
- `Docs/ADR/001-clean-architecture.md`
- `Docs/ADR/002-platform-agnostic-geometry.md`
- `Docs/ADR/003-dependency-injection.md`

### 7.3 Blender 迁移指南
- `Docs/BlenderMigration.md`
- C# → Python 类型映射表
- 领域模型迁移清单

---

## 📊 进度跟踪

| 阶段 | 状态 | 完成度 | 预估时间 | 实际时间 |
|------|------|--------|----------|----------|
| 阶段 1 | ✅ 已完成 | 100% | 2-3 天 | - |
| 阶段 2 | ⏸️ 待开始 | 0% | 5-7 天 | - |
| 阶段 3 | ⏸️ 待开始 | 0% | 2-3 天 | - |
| 阶段 4 | ⏸️ 待开始 | 0% | 3-4 天 | - |
| 阶段 5.1 | ⏸️ 待开始 | 0% | 4-5 天 | - |
| 阶段 5.2 | ⏸️ 待开始 | 0% | 5-6 天 | - |
| 阶段 5.3 | ⏸️ 待开始 | 0% | 6-8 天 | - |
| 阶段 6 | ⏸️ 待开始 | 0% | 3-5 天 | - |
| 阶段 7 | ⏸️ 待开始 | 0% | 2-3 天 | - |

**总体完成度**: ~11% (阶段 1/9)

---

## ✅ 验收标准

### 整体标准
- [ ] 所有架构测试通过（Domain 层零 AutoCAD 依赖）
- [ ] 至少 3 个核心功能完成迁移（OverKill + AnchorBolt + PileLayout）
- [ ] 单元测试覆盖率 > 60%（Domain 和 Application 层）
- [ ] 所有原有命令在新架构下正常工作

### 代码质量标准
- [ ] 所有公共 API 都有 XML 文档注释
- [ ] 每个领域服务都有对应的单元测试
- [ ] 复杂算法有详细注释和数学公式说明

### 未来可迁移性验证
- [ ] Domain 层可以编译为独立 DLL（无 AutoCAD 引用）
- [ ] 几何算法可以脱离 AutoCAD 运行
- [ ] 接口设计考虑了 Python 实现的可行性

---

## 📝 文档索引

- **总体计划**: [Plan.md](./Plan.md)（本文档）
- **阶段文档**:
  - [阶段1.md](./阶段1.md) - 项目基础设施搭建（已完成）
  - [阶段2.md](./阶段2.md) - 底层几何组件迁移（待开始）
  - [阶段3.md](./阶段3.md) - 样式与配置管理（待开始）
  - [阶段4.md](./阶段4.md) - 简单功能迁移验证（待开始）
  - [阶段5.md](./阶段5.md) - 核心业务功能迁移（待开始）
  - [阶段6.md](./阶段6.md) - 测试与验证（待开始）
  - [阶段7.md](./阶段7.md) - 文档与知识提取（待开始）

---

**最后更新**: 2025-01-08  
**当前阶段**: 阶段 2（准备中）  
**下一步行动**: 用户测试阶段 1 成果后，开始阶段 2 实施

