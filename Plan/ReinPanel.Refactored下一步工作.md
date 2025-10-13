# ReinPanel.Refactored 下一步工作计划

## 📊 当前进度

✅ **架构设计**: 100%  
✅ **项目结构**: 100%  
✅ **框架代码**: 100%  
⏳ **业务逻辑**: 10%（框架完成，算法待迁移）

---

## 🎯 核心任务：迁移业务逻辑

### 任务清单

#### 阶段 A: 核心几何算法（优先级：🔴 高）

| 方法 | 原始位置 | 行数 | 复杂度 | 状态 |
|------|---------|------|--------|------|
| `GetSubReinforcements` | ReinforcementFun.cs:60 | ~30 | 高 | ⏳ |
| `ConnectReinByCondition` | 需查找 | ~20 | 中 | ⏳ |
| `GetSubReinforcementWithAnchors` | ReinforcementFun.cs:101 | ~15 | 中 | ⏳ |
| `AddHooks` (Addhook) | ReinforcementFun.cs:116 | ~150 | 高 | ⏳ |

#### 阶段 B: 点钢生成（优先级：🟡 中）

| 方法 | 原始位置 | 行数 | 复杂度 | 状态 |
|------|---------|------|--------|------|
| `GetDotReinCenterPoly` | 需查找 | ~20 | 中 | ⏳ |
| `AddDotRein` | ReinforcementFun.cs:154 | ~15 | 低 | ⏳ |
| `AddReduceDotRein` | ReinforcementFun.cs:166 | ~15 | 低 | ⏳ |
| `PointsToDotRein` | ReinforcementFun.cs:752 | ~30 | 中 | ⏳ |

#### 阶段 C: 标注功能（优先级：🟡 中）

| 方法 | 原始位置 | 行数 | 复杂度 | 状态 |
|------|---------|------|--------|------|
| `AddMleaders` | 需查找 | ~50 | 中 | ⏳ |
| `GenerateDimension` | DimensionForReinforcement.cs | ~800 | 极高 | ⏳ |

#### 阶段 D: 辅助功能（优先级：🟢 低）

| 功能 | 说明 | 状态 |
|------|------|------|
| `PreprocessBoundary` | 边界预处理 | ⏳ |
| `CreateLayers` | 图层创建 | ⏳ |
| 样式应用 | BaseConfig.InitializeStyle | ⏳ |

---

## 📝 实施步骤

### 步骤 1: 创建扩展方法类

**文件**: `ReinPanel.Refactored/Infrastructure/AutoCAD/PolylineExtensions.cs`

```csharp
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;

namespace ReinPanel.Refactored.Infrastructure.AutoCAD
{
    public static class PolylineExtensions
    {
        /// <summary>
        /// 获取多段线每段的角度
        /// </summary>
        public static double[] GetPolySegmentAngle(this Polyline polyline)
        {
            // 从原代码迁移
            return new double[0];
        }
        
        /// <summary>
        /// 获取指定索引的线段
        /// </summary>
        public static LineSegment3d GetLineSegmentAt(this Polyline polyline, int index)
        {
            Point3d startPoint = polyline.GetPoint3dAt(index);
            Point3d endPoint = polyline.GetPoint3dAt((index + 1) % polyline.NumberOfVertices);
            return new LineSegment3d(startPoint, endPoint);
        }
        
        /// <summary>
        /// 3D 点转 2D 点
        /// </summary>
        public static Point2d Convert2d(this Point3d point, Plane plane)
        {
            return new Point2d(point.X, point.Y);
        }
        
        /// <summary>
        /// 连接两个多段线
        /// </summary>
        public static void JoinEntity(this Polyline poly1, Polyline poly2)
        {
            // 从原代码迁移
        }
        
        // ... 更多扩展方法
    }
}
```

### 步骤 2: 创建图层管理器

**文件**: `ReinPanel.Refactored/Infrastructure/AutoCAD/LayerManager.cs`

```csharp
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;

namespace ReinPanel.Refactored.Infrastructure.AutoCAD
{
    public static class LayerManager
    {
        public static void CreateMultipleLayers(params (string name, short colorIndex)[] layers)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            
            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                LayerTable lt = trans.GetObject(db.LayerTableId, OpenMode.ForWrite) as LayerTable;
                
                foreach (var (name, colorIndex) in layers)
                {
                    if (!lt.Has(name))
                    {
                        LayerTableRecord ltr = new LayerTableRecord();
                        ltr.Name = name;
                        ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex);
                        lt.Add(ltr);
                        trans.AddNewlyCreatedDBObject(ltr, true);
                    }
                }
                
                trans.Commit();
            }
        }
        
        public static void SetCurrentLayer(string layerName)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            
            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                LayerTable lt = trans.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
                
                if (lt.Has(layerName))
                {
                    db.Clayer = lt[layerName];
                }
                
                trans.Commit();
            }
        }
    }
}
```

### 步骤 3: 逐个实现核心方法

按照以下顺序实现：

1. **GetSubReinforcements** (最重要)
   - 从 `HyCADTool/Tools/CreatEntity/Rein/ReinforcementFun.cs` 第60行复制
   - 修改静态属性引用为参数传递
   - 测试：应该能生成钢筋分段

2. **GetSubReinforcementWithAnchors**
   - 从 `ReinforcementFun.cs` 第101行复制
   - 添加锚固长度计算

3. **AddHooks**
   - 从 `ReinforcementFun.cs` 第116行复制
   - 添加弯钩逻辑

4. **PointsToDotRein**
   - 从 `ReinforcementFun.cs` 第752行复制
   - 生成点钢筋圆

5. **其他辅助方法**
   - 按需实现

---

## 🧪 测试策略

### 单元测试（可选）

创建 `ReinPanel.Refactored.Tests` 项目：

```csharp
[Test]
public void DrawReinforcement_WithValidParameters_ShouldSucceed()
{
    var service = new ReinforcementService();
    var parameters = ReinParameters.CreateDefault();
    
    // 需要 Mock AutoCAD API
    service.DrawReinforcement(parameters);
    
    // 验证结果
}
```

### 集成测试（主要）

在 AutoCAD 中手动测试：

1. 绘制一个矩形多段线
2. 执行 `gj` 命令
3. 选择多段线
4. 检查生成的钢筋
5. 验证参数是否正确应用

---

## 📚 参考资料

### 原始代码位置

```
HyCADTool/Tools/CreatEntity/Rein/
├── ReinforcementMain - 复制.cs      # 主方法
├── ReinforcementFun.cs               # 核心算法
├── ReinforcementSingleFunsA.cs       # 辅助方法 A
├── ReinforcementSingleFunsB.cs       # 辅助方法 B
├── ReinforcementSingleFunsC.cs       # 辅助方法 C
├── ReinforcementSingleFunsD.cs       # 辅助方法 D
├── ReinforcementSingleFunsE.cs       # 辅助方法 E
├── ReinforcementSingleFunsF.cs       # 辅助方法 F
└── ReinforcementOutside.cs           # 外部钢筋
```

### 相关工具类

```
HyCADTool/Tools/
├── ZTools.cs                         # 通用工具
├── SelectTool/SelectEntityTool.cs    # 选择工具
└── GptModifyTool/                    # 修改工具
```

---

## 💡 重构建议

### 保持简洁

- 只迁移必要的方法
- 复杂的扩展方法可以先简化实现
- 优先实现主流程，细节功能后续完善

### 避免过度工程化

- 不需要创建过多的抽象层
- 直接在 `ReinforcementService` 中实现算法
- 扩展方法放在 `Infrastructure/AutoCAD/` 下

### 保持可测试性

- 每个方法职责单一
- 参数通过方法传递，不使用全局状态
- 便于后续添加单元测试

---

## ✅ 验收标准

完成后应该能够：

1. ✅ 显示钢筋配置面板
2. ✅ 修改参数并实时生效
3. ✅ 执行 `gj` 命令选择多段线
4. ✅ 在 AutoCAD 中看到生成的钢筋
5. ✅ 钢筋包含锚固和弯钩
6. ✅ 生成点钢筋
7. ✅ 生成引线标注
8. ✅ 执行 `gb/gb1/gb2` 命令进行标注

---

**文档版本**: 1.0  
**创建时间**: 2025-10-11  
**预计完成时间**: 8-12 小时开发



