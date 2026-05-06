# 05 - 优化范式的统一：从 ClusterPanel 与 PileVoronoi 抽出 HyCAD 通用优化引擎

> 本文与 01~04 互补不重叠。
> 01 立项（What 是注释）；02 立柱（How / Why 注释可被系统化）；03 立尺（Optimal——注释能否求极值）；04 立路（Roadmap——HyCAD 应在已有代码上如何开始）。
> **05 立梁**：把已被工程实现验证的"优化命题"从注释扩展到模型构建本身，抽出统一接口，让 `Cluster` / `Pile` / 未来的注释优化器共享同一根梁。

---

## 1. 缘起：04 §1 的"AI 替代难度"表有一个直接反例

04 §1 给出了如下判断：

| 层级 | AI 替代难度 |
|---|---:|
| 模型构建 | 高（设计意图不完整，输入模糊） |
| 模型属性识别 | 中 |
| 注释 / 符号生成 | 低到中 |
| 图纸表现优化 | 中 |

这个判断**整体方向正确**，但有一个被低估的子集：**模型构建中"约束极强"的部分**（桩布置 / 承台配筋 / 标准节点 / 重复构件），其难度其实是**低**——它和"注释生成"在数学上是同一类问题。

证据来自 HyCAD 现有代码 `Features/Pile`：

| 求解阶段 | 实现 | 文件 |
|---|---|---|
| **S1 规则系统** | 矩形 / 梅花形网格反算 \(n_X \times n_Y\) | `PileLayoutService.Calculate` |
| **S2 启发式 + 局部搜索** | Lloyd 迭代 4500 次（CVT 收敛） | `VoronoiOptimizationService.ApplyLloydOptimization` |

它们都不依赖 AI，纯规则与启发式即可工程化。这说明 04 §1 表应被精化为：

| 层级 | 子集 | AI 替代难度 |
|---|---|---:|
| 模型构建 | 自由设计（建筑造型 / 创意） | 高 |
| 模型构建 | **约束极强（桩布置 / 标准节点）** | **低**（已被 PileVoronoi 证伪） |
| 模型构建 | 工程对象拼装 | 中 |
| 注释 / 符号生成 | 标准注释 | 低 |
| 注释 / 符号生成 | 复杂避让 | 中 |

**核心修正**：03 §1 命题 \(A^{*}=\arg\max Q(A;M,C,U)\) 不只描述"注释最优化"，它描述的是 **HyCAD 全部"约束极强子领域"的通用工程命题**。

---

## 2. 现状：HyCAD 已无意识实现的两类优化范式

### 2.1 范式 A — 注释组合优化（ClusterPanel）

**任务类型**：模型已建好 → 把已存在的点分组并标注。

```text
M（输入）：DWG 几何 + 5 类点 + 轴线
A（候选）：每条尺寸标注的方向 + 分组 + 偏置
约束：不重叠 / 跨区域去重 / 行内相邻
Q（隐式）：去重粒度 + 偏置层叠数 + 漏标点
求解：DBSCAN 长方形邻域 → FilterDuplicateDimensions
```

### 2.2 范式 B — 模型布局优化（PileVoronoi / PileLayout）

**任务类型**：约束已知 → 生成新的几何对象。

```text
M（输入）：承台多边形 + 桩径 + 置换率
A（候选）：N 个桩心位置（连续 \(\mathbb{R}^2\) ）
约束：必须在多边形内 / 总数=N / 桩间最小距离
Q（隐式）：Voronoi 单元面积方差（最小化）
求解：Lloyd 迭代 4500 次（梯度下降式 CVT 收敛）
```

### 2.3 同构性

两者的命题骨架完全一致：

\[
A^{*} = \arg\max_{A \in \mathcal{A}(M)} \; Q(A;\; M, C, U)
\]

| 实例化 | 范式 A（注释） | 范式 B（布局） |
|---|---|---|
| \(M\) | 几何 + 已建点 | 承台 + 参数 |
| \(\mathcal{A}\) | 注释方案空间 | 点位连续空间 |
| \(C\) | 图纸密度、比例、字高 | 桩径、间距、轮廓 |
| \(U\) | 客户行业惯例 | 设计规范、地基类型 |
| 求解 | 组合优化 + 启发式去重 | 连续优化 + 启发式迭代 |
| 阶梯 | S1 | S2（已实现 CVT） |

**关键洞察**：HyCAD 不是在做"两个独立功能"，而是在用同一种数学骨架处理两类不同的工程任务。这根骨架就是接下来要抽出的统一引擎。

---

## 3. 共同骨架：4 个职责 + 1 个外壳

把 ClusterPanel 与 PileVoronoi 的代码并排展开，可以看出它们其实在做**同样四件事**，只是名字不同：

| 职责 | ClusterPanel 代码 | PileVoronoi 代码 | 抽象名 |
|---|---|---|---|
| 1. 约束容器 | `AxisAnalysisService` 划分的 `Region` | 用户选择的 `Polygon` | `IConstraintContainer` |
| 2. 候选生成 | DBSCAN 长方形邻域枚举聚类 | 随机点 / 已选圆心作为初始候选 | `ICandidateGenerator` |
| 3. 质量评估 | （隐式）`FilterDuplicateDimensions` 去重保留 | （隐式）单元面积阈值 + 是否在多边形内 | `IQualityFunction` |
| 4. 求解迭代 | 一次性枚举求解 | Lloyd 迭代 4500 次 | `IOptimizer` |

外壳是 5：

| 职责 | ClusterPanel 代码 | PileVoronoi 代码 | 抽象名 |
|---|---|---|---|
| 5. 渲染输出 | `ClusterDrawService.Draw` | `DrawVoronoiRegion` + `DrawPiles` | `IRenderer` |

→ **得到 \(4 + 1\) 接口的统一引擎。**

---

## 4. 优化阶梯现状定位：HyCAD 已经走到 S2，剩 Q + S3 + S4

引用 03 §5 的阶梯：

| 阶段 | 方法 | HyCAD 已有 | 缺什么 |
|---|---|---|---|
| **S1 规则系统** | 规则 + 优先级 + 候选枚举 | ClusterPanel（DBSCAN）+ PileLayoutService（网格反算） | Quality Score 度量好坏 |
| **S2 启发式 + 局部搜索** | 力导向 / 退火 / Lloyd | PileVoronoi（CVT 4500 迭代） | Quality Score 当 Reward |
| **S3 全局优化** | ILP / CP-SAT / OR-Tools | 无 | 求解器接入 + 时间预算管理 |
| **S4 学习增强** | 模仿学习 / RL | 无 | 反馈数据集 + 训练框架 |

→ **HyCAD 已经走到 S2，缺的不是算法**，而是：

1. **Quality Score（横切）**：S1/S2 都没有显式打分，导致同一问题"哪个方案更好"无法对比。
2. **持久化绑定（横切）**：注释 / 桩位写入后与源模型脱钩，重生成不可能。
3. **Profile 化（横切）**：同一问题对不同客户出不同解，没机制。
4. **S3 / S4**：未来扩展，当前还不缺。

→ **真正的工程突破口是补 1 / 2 / 3，把 S1 / S2 接到产品级**，而非急着上 S3 / S4。

---

## 5. 抽象：`IOptimizationProblem<TSolution>` 接口族

### 5.1 5 个核心接口（建议放 `Domain/Optimization`）

```text
IOptimizationProblem<TSolution>
  ├─ IConstraintContainer            约束容器（图纸区域 / 承台多边形 / 视图边界）
  ├─ ICandidateGenerator<TSolution>  候选生成（点 / 注释 / 桩位）
  ├─ IQualityFunction<TSolution>     质量评估（六组指标 / Voronoi 方差 / 重叠数）
  ├─ IOptimizer<TSolution>           求解器（S1 规则 / S2 启发式 / S3 全局 / S4 学习）
  └─ IRenderer<TSolution>            渲染输出（DWG Entity / WPF Visual / SVG）
```

### 5.2 关键设计决策

1. **`TSolution` 泛型化**——不强制 `TSolution` 是注释或桩位；只要它能被评分、能被渲染就行。
2. **`IConstraintContainer` 抽象**——`ClusterPanel.Region` 和 `PileVoronoi.Polygon` 都实现它，未来标高 / 钢筋 / 桩基的"约束区域"也都走这个接口。
3. **`IQualityFunction` 必须可分解**——子项可独立计算 + 独立调权重，否则 Profile 化做不了。
4. **`IOptimizer` 接受时间预算**——S1 < 100 ms / S2 < 5 s / S3 < 60 s。
5. **求解可中断**——Lloyd 4500 次迭代不能一直跑到死，要支持 cancel + 渐进结果。

### 5.3 与 02 §10 / 03 §10 / 04 §6 的接口关系

| 文档接口 | 本文接口 | 关系 |
|---|---|---|
| 02 §10 `IAnnotationAnchor` 等 6 个 | 不冲突 | 02 是注释域专属；本文是跨域骨架 |
| 03 §10 `IAnnotationQualityFunction` | `IQualityFunction<Annotation>` | 03 接口是本文接口在注释域的实例化 |
| 04 §6 `LayoutOptimizer` 模块 | `IOptimizer` | 04 是产品视角；本文是接口视角 |

→ **02 / 03 / 04 都是"注释域的 IOptimizationProblem"**；05 把它扩展到所有"约束极强子领域"。

---

## 6. 跨范式洞察：三个统一概念

### 6.1 洞察 1：约束容器（Constraint Container）

`ClusterPanel.Region`（轴线划分的矩形区域）和 `PileVoronoi.Polygon`（承台多边形）本质相同——都是**"在这里面找解"**。

抽象后：

| 容器类型 | 来源 | 实例 |
|---|---|---|
| 矩形区域 | 轴线交叉划分 | ClusterPanel `Extents3d` |
| 任意多边形 | 用户拾取轮廓 | PileVoronoi `Polygon` |
| 视图边界 | 布局视口 | 未来 3D 注释 |
| 图幅 | 打印边界 | 未来全图布局 |
| 子构件包围盒 | 标注锚定区 | 未来注释避让 |

→ **约束容器统一成 `IConstraintContainer { bool Contains(Point); double DistanceToBoundary(Point); }`**。整个项目可以共用同一组容器实现。

### 6.2 洞察 2：空间划分（Spatial Partitioning）

`ClusterPanel` 的 DBSCAN 长方形邻域和 `PileVoronoi` 的 Voronoi 单元，**都是"把约束容器划分成若干小块"**：

| 划分方法 | 块的语义 | 工程用途 |
|---|---|---|
| DBSCAN 长方形邻域 | 邻近点的同一簇 | 同向连续标注 |
| Voronoi 单元 | 离当前点最近的区域 | 桩位均布 + 服务半径 |
| 网格 | 行列对齐的小矩形 | 规则桩布置 + 标注列 |
| 凸包 | 包络外形 | 簇外形提取 |
| 力导向布局 | 软边界划分 | 标签自动避让 |

→ HyCAD 应有一个 `IPartitioner<TContainer, TPartition>` 接口，让聚类 / Voronoi / 网格作为它的实现，**未来标注避让也复用同一套**。

### 6.3 洞察 3：参数化迭代（Parameterized Iteration）

`PileVoronoi` 的 Lloyd 4500 次迭代和 `ClusterPanel` 的 DBSCAN 单次扫描，**都是参数化的求解过程**：

| 迭代类型 | 参数 | 终止条件 |
|---|---|---|
| Lloyd 迭代 | iterations / pileDiameter | 固定次数 |
| DBSCAN 扩展 | EpsilonX / EpsilonY / MinPoints | 全部访问完成 |
| 力导向松弛 | iterations / step | 收敛阈值 |
| 模拟退火 | T0 / cooling rate | 温度极低 |

→ 抽象成 `IIterativeOptimizer { Run(budget) → IObservable<TSolution> }`，**支持中断 / 进度展示 / 渐进可视化**。这是 PileVoronoi 跑 4500 次时**不能取消**的当下痛点。

---

## 7. 三个 Feature 共同的缺口 Top10

把 ClusterPanel + PileVoronoi + PileLayout 三处问题归并：

| # | 缺口 | 影响范围 | 严重度 |
|---|---|---|---:|
| 1 | 没有显式 Quality Score 接口 | 所有优化雏形 | 高 |
| 2 | 写入后无 XData 回绑源对象 | ClusterPanel + PileVoronoi | 高 |
| 3 | 不可重生成（修改后只能全删重画） | 所有 Feature | 高 |
| 4 | 客户偏好仅"方案 1/2/3"无语义命名 | ClusterPanel + PileLayout | 中 |
| 5 | 长时间求解不可取消（Lloyd 4500 次） | PileVoronoi | 中 |
| 6 | 算法注释错位（"DBSCAN" vs 长方形邻域 / "BFS" 死代码） | ClusterPanel | 低 |
| 7 | 校核（漏标 / 重叠 / 孤儿）全部缺失 | 所有 Feature | 中 |
| 8 | UI 参数命名歧义（EpsilonX/Y 在 X/Y 两组里撞名） | ClusterPanel | 低 |
| 9 | 默认参数关闭关键功能（MinPoints=1 等于关 DBSCAN 噪声过滤） | ClusterPanel | 低 |
| 10 | 求解只能一次性写 DWG，不支持预览 / 撤销 | 所有 Feature | 中 |

→ 1 / 2 / 3 / 7 / 10 是**横切问题**，单独修一个 Feature 不解决根因；必须由统一引擎层解决。这正是抽 `IOptimizationProblem<T>` 的工程动机。

---

## 8. 增量演进路线（先抽接口，三个 Feature 各自向接口对齐）

### Phase 0 — 概念正名 + 接口骨架（1 周，**不写实现**）

- [ ] 在 `Domain/Optimization` 建空目录
- [ ] 写 5 个空接口：`IOptimizationProblem<TSolution>` / `IConstraintContainer` / `ICandidateGenerator<T>` / `IQualityFunction<T>` / `IOptimizer<T>` / `IRenderer<T>`
- [ ] 在 ClusterPanel 与 PileVoronoi 对应的 Service 顶部 XML 注释里加："实现 `IConstraintContainer` / `ICandidateGenerator` / ..."（先文档对齐，不动代码）
- [ ] 把 `Domain/Services/ClusteringService.cs`（圆形 BFS 死代码）标 `[Obsolete]` 并文档化

### Phase 1 — Quality Score 横切补强（2~3 周）

- [ ] 写最小 5 项指标的 `IQualityFunction<TAnnotation>`（漏标点 / 重叠 / 偏置层叠 / 行间距方差 / 跨区域错配）
- [ ] 写最小 3 项指标的 `IQualityFunction<TPileLayout>`（Voronoi 面积方差 / 在轮廓内比例 / 桩间最小距离命中率）
- [ ] 给 ClusterPanel 与 PilePanel 加一个**只读的"评分卡"区域**（不影响生成逻辑），先看到分数，不强制接管求解

### Phase 2 — XData 持久化与重生成（3~4 周）

- [ ] 定义 `IAnnotationStore` / `ILayoutStore`，写入 DWG 时同时写 XData 绑回源对象
- [ ] 加一个跨 Feature 的 `RefreshCommand`：扫描 XData → 重新跑求解器 → 增量更新（不全删全画）
- [ ] 这一步打通后，ClusterPanel "改方案后重出图" / PileVoronoi "改置换率重布桩"才真的可用

### Phase 3 — Profile 化 + 客户语义命名（2 周）

- [ ] ClusterPanel 方案 1/2/3 → 命名为"底板 / 螺栓 / 通用"
- [ ] PilePanel 加"规则布置 / Voronoi 均布 / 混合"三档（S1 / S2 / 加权混合）
- [ ] Profile = `IQualityFunction` 的权重向量 + 部分软约束修正项
- [ ] 内置 5~7 个 Profile 校准

### Phase 4 — 长求解可中断 + 渐进展示（2 周，针对 PileVoronoi）

- [ ] Lloyd 4500 次迭代加 `IObservable<TSolution>` 进度
- [ ] 用户可在中途取消并接受当前解
- [ ] 评分曲线实时显示（每 100 次迭代刷一次 Q）

### Phase 5 — 横切校核能力（3~4 周）

- [ ] `IValidator<TSolution>` 接口
- [ ] 三个 Feature 各自写校核器：漏标 / 重叠 / 孤儿 / 超界
- [ ] 校核结果统一出报告 UI（清单 + 跳转）

### Phase 6 — 反馈数据收集（持续）

- [ ] 用户接受方案 → 隐式正反馈
- [ ] 用户手调 → 隐式负反馈
- [ ] 攒数据集为 Phase 7 学习增强蓄力

### Phase 7 — S3 全局优化 / S4 学习增强（视效果决定，年后）

→ **每一阶段都不破坏老命令**；现有 ClusterPanel 与 PileVoronoiOptimizationCommand 在过渡期保持原样可用。

---

## 9. 关键护城河更新（覆盖 04 §11 第 5 / 6 项）

04 §11 列了六条护城河。结合 Cluster + Pile 两脉真实代码，本文增补：

| # | 护城河（更新后） | 真实证据 |
|---|---|---|
| 1 | 工程对象库 | `AnchorBolt` / `Pile` / `Reinforcement` / `Elevation` 已积累 |
| 2 | 符号模板库 | 标高 / 轴号 / 钢筋 / 螺栓 / Voronoi 桩 |
| 3 | 客户样式库 | ClusterPanel 方案 1/2/3 + PileLayout 矩形/梅花 |
| 4 | 评价函数库 | **当前缺**——这是 Phase 1 重点 |
| 5 | 语义绑定能力 | **当前缺**——这是 Phase 2 重点 |
| 6 | 重生成能力 | **当前缺**——这是 Phase 2 重点 |
| **7** | **统一优化引擎** | **本文 §5：5 个跨 Feature 接口，行业内罕见** |
| **8** | **S1 + S2 双路径已商用** | **PileLayout（S1）+ PileVoronoi（S2）已落地，行业内罕见** |
| **9** | **多样化"约束极强子领域"积累** | 桩布置 + 注释 + 钢筋 + 标高，足以训练统一引擎 |

→ **第 7 / 8 / 9 条**是 04 §11 没有列出的、Cluster + Pile 实证给出的新护城河。竞品缺第 7（接口未抽）和第 8（要么纯 S1 要么纯 S2）。

---

## 10. 跨域映射：让命题清晰可验证

把 03 §1 的优化命题对每个 HyCAD Feature 实例化一遍，证明命题的普适性：

| Feature | \(M\) | \(\mathcal{A}\) | 约束 \(R\) | \(Q\) 候选 | 已实现求解 |
|---|---|---|---|---|---|
| ClusterPanel | 5 类点 + 轴线 | 区域内尺寸标注集合 | 不重叠 / 跨区域去重 | 漏标 / 偏置层叠 / 区域错配 | S1（DBSCAN 长方形邻域） |
| PileVoronoi | 承台 + 桩参数 | N 个连续点位 | 在多边形内 / 总数=N | Voronoi 面积方差 / 桩间距 | S2（Lloyd 4500） |
| PileLayout | 矩形承台 + 桩参数 | nX×nY 网格点位 | 行列对齐 / 边距 | 实际置换率 - 目标置换率 | S1（网格反算） |
| AnchorBolt | 螺栓 ObjectId 集合 | 编号 + 引线 + 表格 | 行业符号标准 | 同图唯一编号 / 表格无遗漏 | 当前 L1 模板（待 S1） |
| Reinforcement | 钢筋路径 | 编号气泡 + 间距文字 | 行业符号 / 重复合并 | 编号一致 / 重复率最低 | 当前 L1 / L2（待 S1） |
| Elevation | 高度参考点 | 标高块 + 引线方向 | 不遮挡 / 朝向偏好 | 漏标 / 重叠 | 当前 L1（待 S1） |
| 未来 3D 注释 | 三维构件 | Billboard 标签位置 | 视图相关 / 不遮挡 | 可读性 / 深度排序 | 待 S1 / S2 |

→ **同一个引擎能跑下整张表**——这就是 §5 抽象 `IOptimizationProblem<TSolution>` 的全部理由。

---

## 11. 与 03 / 04 / 现有代码的交叉索引

| 概念 | 03 文档位置 | 04 文档位置 | 现有代码 | 本文位置 |
|---|---|---|---|---|
| 优化命题 \(A^* = \arg\max Q\) | §1 | §4 | （隐式存在） | §2.3 |
| Quality Score 6 组指标 | §3 | — | — | §8 Phase 1 |
| 候选生成 + 8 方位 | §8 Phase 1 | §9 | 不存在 | §5 接口 |
| 约束硬/软 | §4 | §4 | 部分（图层名 / 点过滤） | §6.1 约束容器 |
| S1~S4 求解阶梯 | §5 | — | S1+S2 已实现 | §4 |
| 客户 Profile | §8 Phase 3 | §10 阶段 3 | ClusterPanel 方案 1/2/3 | §8 Phase 3 |
| Lloyd / CVT | §16 参考 | — | `VoronoiOptimizationService` | §2.2 / §3 |
| 长方形邻域 DBSCAN | — | — | `ClusterFactoryService` | §2.1 / §6.2 |
| XData 重生成 | §13 | §11 第 6 条 | 不存在 | §8 Phase 2 |
| 校核 / Validator | §3.1 完备性 | §6 模块 | 不存在 | §8 Phase 5 |

---

## 12. 立即可做（半天，不写实现）

1. 在 `src/HyCADTool/Domain/Optimization` 建空目录
2. 写 5 个接口骨架：
   - `IConstraintContainer`：`bool Contains(Point2D); double DistanceToBoundary(Point2D); BoundingBox Envelope { get; }`
   - `ICandidateGenerator<TSolution>`：`IEnumerable<TSolution> Generate(IConstraintContainer ctx);`
   - `IQualityFunction<TSolution>`：`QualityScoreCard Evaluate(TSolution s, IConstraintContainer ctx);`
   - `IOptimizer<TSolution>`：`IObservable<TSolution> Solve(IOptimizationProblem<TSolution> p, TimeSpan budget);`
   - `IRenderer<TSolution>`：`void Render(TSolution s, IRenderTarget target);`
3. 在 `ClusterFactoryService` / `VoronoiOptimizationService` 顶部 XML 注释加"未来对齐到 `IOptimizer<T>`"
4. 标记 `Domain/Services/ClusteringService.cs` 为 `[Obsolete("用 ClusterFactoryService.CreateClustersFromDomain 替代；本类是死代码")]`
5. 把 03 / 04 / Cluster / PileVoronoi 之间的索引（§11）放进每个 Feature 的 README 或顶部注释

→ **这一步不动任何业务逻辑**，只是把"已无意识做了三件同型工作"这件事**显式写到代码里**——之后所有 PR 都有依据可循。

---

## 13. 最重要的原则（与 04 §15 互补，跨范式视角）

1. **接口先于实现**——5 个接口定下来，新 Feature 按接口走；老 Feature 不强迫迁移。
2. **跨 Feature 共用骨架**——注释、桩布置、钢筋、标高都走 `IOptimizationProblem<T>`，**避免每条业务链各自重发明**。
3. **Quality 是横切公共品**——每个 Feature 都需要 \(Q\)，但 \(Q\) 的实现在各自域里；接口必须公共。
4. **S1 与 S2 是产品级双引擎**——HyCAD 当前的真护城河不是某个算法，而是"两个都能跑"，对竞品形成代差。
5. **重生成是命门**——没有 XData 绑回 + 重生成，所有"自动化"都退回一次性命令；这是 Phase 2 的最高优先级。
6. **长求解必须可观察可中断**——Lloyd 4500 次跑死等的体验让用户失去信心。
7. **AI 不是必须的**——03 §1 命题不要求 AI；S1/S2 + Quality 已经能解决 90% 工程问题。

---

## 14. 一句话定位

> **HyCAD 已经在用同一种数学骨架解两类问题：**
> **「已建模型 → 注释最优化」（ClusterPanel）与「约束已知 → 模型布局最优化」（PileVoronoi）。**
> **05 把这根骨架显式抽出来，让未来所有"约束极强子领域"共享同一根梁。**

注释只是这根梁能扛起的第一块板；桩布置是已经压上去的第二块。后面还有钢筋、标高、设备基础、轴线、表格——它们都将放到同一根梁上。

---

## 15. 参考研究方向（与 03 §16 / 04 文档参考互补）

### Voronoi 与 CVT

- Lloyd, *Least squares quantization in PCM*（1982，CVT 奠基）
- Du / Faber / Gunzburger, *Centroidal Voronoi Tessellations: Applications and Algorithms*（1999）
- NetTopologySuite `VoronoiDiagramBuilder` 文档（HyCAD 已使用）

### 约束布局算法

- de Berg et al., *Computational Geometry: Algorithms and Applications*（容器 / 划分基础）
- Held, *VRONI: An Engineering Approach to the Reliable and Efficient Computation of Voronoi Diagrams*

### 跨域优化框架

- Google OR-Tools / CP-SAT
- COIN-OR Project（开源 LP/IP/CP 求解器集合）
- TimefoldSolver / OptaPlanner（Java 系，但思想可借鉴）

### 工程标准

- 《建筑桩基技术规范》JGJ 94 / 《岩土工程勘察规范》GB 50021（桩布置约束依据）
- ISO 128 / 129 / 1101 / 16792（注释约束依据）

### 算法 + CAD 的少数交叉点

- ArcGIS Maplex Label Engine（注释自动布局，S2 工业范本）
- Revit Generative Design（建筑布局生成，S2~S3）
- Tekla Auto Connection（钢节点自动选型，S1）

---

**文档版本** v1.0 | **创建** 2026-04-27 02:15 | **作者** Cursor Agent
