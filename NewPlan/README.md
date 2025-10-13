# NewPlan - HyCADTool 重构规划文档

> **创建日期**: 2025-10-13  
> **项目**: HyCADTool AutoCAD 插件重构  
> **目标**: 从底层架构开始，建立可维护、可扩展、平台无关的代码库

---

## 📁 文档目录

### 核心文档

| 文档 | 说明 | 状态 |
|------|------|------|
| **[命令详细清单.md](./命令详细清单.md)** | 90+ 个命令的完整分析与依赖关系 | ✅ 已完成 |
| **[详细计划.md](./详细计划.md)** | 完整的重构执行计划与技术方案 | ✅ 已完成 |
| **[依赖分析.md](./依赖分析.md)** | 原项目依赖关系深度分析 | 📝 待创建 |
| **[阶段执行记录.md](./阶段执行记录.md)** | 各阶段执行进度与问题记录 | 📝 待创建 |

---

## 🎯 重构核心目标

### 架构原则

1. **平台无关**: Domain 层零 CAD 依赖，可迁移到 Blender/Python
2. **Clean Architecture**: 严格分层，依赖倒置
3. **SOLID 原则**: 可测试、可扩展、可维护
4. **单一项目**: 所有重构代码在 HyCADTool.Refactored 中

### 技术栈

- **.NET Framework 4.8** - 基础框架
- **AutoCAD.NET 24.3.0** - CAD API
- **Clipper2** - 多边形布尔运算
- **NetTopologySuite** - 拓扑分析
- **Autofac** - 依赖注入

---

## 📊 项目现状

### 已完成工作

| 项目 | 状态 | 说明 |
|------|------|------|
| HyCADTool | ✅ 原项目 | 90+ 命令，功能完整 |
| HyCADTool.Refactored | ⚠️ 部分完成 | 依赖原项目，需重构 |
| ReinPanel.Refactored | ❌ 已删除 | 测试失败，功能不可用 |

### 当前问题

1. **依赖耦合**: HyCADTool.Refactored 依赖原项目（50+ 文件）
2. **架构混乱**: Adapter 模式过多，缺乏真实实现
3. **测试不足**: 缺少系统性测试
4. **文档缺失**: 技术文档不完整

---

## 🗺️ 重构路线图

**策略**: 自下而上，从实际依赖开始（而非机械套用架构模式）

### 阶段 0: 环境准备 (1 天)
- 删除失败项目（ReinPanel.Refactored）
- 清理 Adapter 层和测试命令
- 分析现有代码可保留部分

### 阶段 1: 配置层 (2-3 天)
- 重构 BaseConfig 静态类 → 配置服务
- 实现 JSON 配置加载
- 配置验证与默认值

### 阶段 2: 服务层 (3-4 天)
- 完善基础 CAD 服务（Layer, Style, Drawing）
- 创建 TransactionHelper
- 服务依赖注入

### 阶段 3: 通用工具层 ZTools (4-5 天)
- 分类迁移 ZTools（2000+ 行）
- 创建扩展方法（Polyline, Entity, Point3d）
- 替换原 ZTools 引用

### 阶段 4: 辅助类层 (4-6 天)
- Jig 交互式绘图
- ElevationSymbol 标高符号
- Pile 桩基算法
- DCEL 拓扑数据结构

### 阶段 5: 业务核心工具层 (10-15 天)
- Reinforcement 钢筋绘制（1500+ 行）
- DimensionForReinforcement 标注（800+ 行）
- BaseRein 基础配筋（2000+ 行）
- GeometryUtils 几何工具

### 阶段 6: 命令层 (6-8 天)
- 钢筋命令（12 个）
- 基础配筋命令（7 个）
- 地脚螺栓命令（6 个）
- 桩基布置命令（2 个）

**总工期估算**: 30-42 天

---

## 📖 使用指南

### 快速开始

1. **阅读命令清单**: 了解原项目功能
   ```
   查看: 命令详细清单.md
   ```

2. **理解重构计划**: 掌握技术方案
   ```
   查看: 详细计划.md
   ```

3. **开始执行**: 按阶段逐步推进
   ```
   参考: 阶段执行记录.md (后续创建)
   ```

### 文档更新规范

- 每完成一个阶段，更新 `阶段执行记录.md`
- 遇到重大技术决策，记录到 `详细计划.md`
- 发现依赖问题，补充到 `依赖分析.md`

---

## 🔗 相关资源

### 项目文档

- [HyCADTool README](../HyCADTool/README.md)
- [Plan 目录](../Plan/) - 历史计划文档
- [Docs 目录](../HyCADTool.Refactored/Docs/) - 重构进度文档

### 技术参考

- [Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [SOLID 原则](https://en.wikipedia.org/wiki/SOLID)
- [AutoCAD .NET API](https://help.autodesk.com/view/OARX/2024/ENU/)

---

## 📝 版本历史

| 版本 | 日期 | 说明 |
|------|------|------|
| v1.0 | 2025-10-13 | 初始版本，完成基础文档 |

---

<div align="center">

**HyCADTool NewPlan - 为更好的架构而重构**

Last Updated: 2025-10-13

</div>

