# 阶段 7：ZTools 大重构方案

> **目标**: 彻底重构 ZTools 部分类，建立清晰的服务架构  
> **影响**: 60+ 命令，2000+ 行代码  
> **方案**: 大重构 + 极简命令框架

---

## ✅ 极简命令框架（已完成）

### 3个核心文件

```csharp
// 1. ICommand.cs (20行) - 极简接口
public interface ICommand
{
    string Name { get; }
    Task<bool> ExecuteAsync();
}

// 2. CommandResult.cs (36行) - 简单结果
public class CommandResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public static CommandResult Ok(string message = "执行成功");
    public static CommandResult Fail(string message);
}

// 3. CommandExecutor.cs (40行) - 静态执行器
public static class CommandExecutor
{
    public static async Task<CommandResult> RunAsync(ICommand command);
}
```

**特点**: 总共96行代码，极简但功能完整！

---

## 🔥 ZTools 大重构方案

### 现状分析

**ZTools 部分类分布**：
```
HyCADtool/Tools/
├── Layer/Tools.Layer.cs           # 图层操作
├── SelectTool/SelectEntityTool.cs  # 选择工具
├── Style/Tools.*.cs               # 5个样式文件
├── GptModifyTool/*.cs             # 15个通用工具
├── input/Get*.cs                  # 用户输入工具
├── PileLayout/*.cs                # 桩基布置
└── ... 40+文件
```

**问题**：
- ❌ 超级部分类：2000+ 行分散在40+文件
- ❌ 职责混乱：选择、绘图、变换、样式全混合
- ❌ 静态依赖：60+命令直接调用 ZTools.XXX()
- ❌ 难以测试：全是静态方法，无法Mock

### 重构策略

#### 阶段 7A：服务接口设计（2天）

**创建核心服务接口**：

```csharp
// 1. 选择服务
public interface ISelectionService
{
    ObjectId[] SelectEntities<T>() where T : Entity;
    ObjectId[] SelectEntitiesByFilter(SelectionFilter filter);
    T GetEntity<T>(ObjectId id) where T : Entity;
}

// 2. 绘图服务  
public interface IDrawingService
{
    ObjectId CreateLine(Point3d start, Point3d end, string layer = null);
    ObjectId CreatePolyline(Point2d[] points, string layer = null);
    ObjectId CreateCircle(Point3d center, double radius, string layer = null);
    ObjectId CreateText(Point3d position, string text, double height, string layer = null);
}

// 3. 图层服务
public interface ILayerService
{
    ObjectId CreateLayer(string name, short colorIndex = 7);
    void SetCurrentLayer(string name);
    bool LayerExists(string name);
    void SetLayerColor(string name, short colorIndex);
}

// 4. 变换服务
public interface ITransformService
{
    void MoveEntities(ObjectId[] ids, Vector3d vector);
    void RotateEntities(ObjectId[] ids, Point3d basePoint, double angle);
    void ScaleEntities(ObjectId[] ids, Point3d basePoint, double scale);
    void CopyEntities(ObjectId[] ids, Vector3d vector);
}

// 5. 样式服务
public interface IStyleService
{
    void SetTextStyle(string name);
    void SetDimStyle(string name); 
    void SetMLeaderStyle(string name);
    void CreateTextStyle(string name, string fontFile, double height);
}
```

#### 阶段 7B：服务实现（3天）

**实现具体服务类**：

```csharp
// Infrastructure/AutoCAD/Services/SelectionService.cs
public class SelectionService : ISelectionService
{
    public ObjectId[] SelectEntities<T>() where T : Entity
    {
        var ed = Application.DocumentManager.MdiActiveDocument.Editor;
        var filter = new SelectionFilter(new TypedValue[] 
        { 
            new TypedValue((int)DxfCode.Start, GetDxfName<T>()) 
        });
        
        var result = ed.GetSelection(filter);
        return result.Status == PromptStatus.OK 
            ? result.Value.GetObjectIds() 
            : new ObjectId[0];
    }
    
    // ... 其他方法实现
}

// Infrastructure/AutoCAD/Services/DrawingService.cs  
public class DrawingService : IDrawingService
{
    public ObjectId CreateLine(Point3d start, Point3d end, string layer = null)
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        var db = doc.Database;
        
        using (var tr = db.TransactionManager.StartTransaction())
        {
            var bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            var btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
            
            var line = new Line(start, end);
            if (!string.IsNullOrEmpty(layer))
                line.Layer = layer;
                
            btr.AppendEntity(line);
            tr.AddNewlyCreatedDBObject(line, true);
            tr.Commit();
            
            return line.ObjectId;
        }
    }
    
    // ... 其他方法实现
}
```

#### 阶段 7C：命令迁移（5天）

**逐步迁移现有命令**：

**示例：锚栓命令重构**
```csharp
// 旧版（直接调用ZTools）
[CommandMethod("hyab")]
public static void AttachAnchorBoltToCircles()
{
    // 130行代码，直接操作Database、Transaction等
    var circles = ZTools.SelectEntities<Circle>();  // 静态调用
    var layer = ZTools.CreateLayer("螺栓层", 159);   // 静态调用
    // ... 复杂的事务处理
}

// 新版（使用服务+命令模式）
public class CreateAnchorBoltCommand : ICommand
{
    private readonly ISelectionService _selection;
    private readonly ILayerService _layer;
    private readonly IAnchorBoltService _boltService;
    
    public CreateAnchorBoltCommand(
        ISelectionService selection,
        ILayerService layer, 
        IAnchorBoltService boltService)
    {
        _selection = selection;
        _layer = layer;
        _boltService = boltService;
    }
    
    public string Name => "CreateAnchorBolt";
    
    public async Task<bool> ExecuteAsync()
    {
        var ed = Application.DocumentManager.MdiActiveDocument.Editor;
        
        try
        {
            // 1. 获取用户输入
            var result = ed.GetString("\n请输入螺栓型号(1-9)[1]: ");
            if (result.Status != PromptStatus.OK) return false;
            var model = string.IsNullOrEmpty(result.StringResult) ? "1" : result.StringResult;
            
            // 2. 选择圆（使用服务）
            var circles = _selection.SelectEntities<Circle>();
            if (circles.Length == 0)
            {
                ed.WriteMessage("\n未选择任何圆");
                return false;
            }
            
            // 3. 创建图层（使用服务）
            var layerName = $"00_Hy_螺栓_{model}";
            _layer.CreateLayer(layerName, 159);
            
            // 4. 创建螺栓（使用业务服务）
            var count = await _boltService.CreateBoltsAsync(model, circles, layerName);
            
            ed.WriteMessage($"\n✓ 成功处理 {count} 个螺栓");
            return true;
        }
        catch (Exception ex)
        {
            ed.WriteMessage($"\n✗ 执行失败: {ex.Message}");
            return false;
        }
    }
}

// AutoCAD命令入口
[CommandMethod("hyab")]
public static async void CreateAnchorBolt()
{
    var selection = ServiceLocator.Get<ISelectionService>();
    var layer = ServiceLocator.Get<ILayerService>();
    var boltService = ServiceLocator.Get<IAnchorBoltService>();
    
    var command = new CreateAnchorBoltCommand(selection, layer, boltService);
    var result = await CommandExecutor.RunAsync(command);
    
    var ed = Application.DocumentManager.MdiActiveDocument.Editor;
    ed.WriteMessage($"\n{result}");
}
```

#### 阶段 7D：依赖注入配置（1天）

**配置IoC容器**：
```csharp
// Infrastructure/Configuration/AutofacModule.cs (扩展)
protected override void Load(ContainerBuilder builder)
{
    // ===== 阶段 7: 服务层 =====
    
    // CAD操作服务
    builder.RegisterType<SelectionService>()
        .As<ISelectionService>()
        .SingleInstance();
        
    builder.RegisterType<DrawingService>()
        .As<IDrawingService>()
        .SingleInstance();
        
    builder.RegisterType<LayerService>()
        .As<ILayerService>()
        .SingleInstance();
        
    builder.RegisterType<TransformService>()
        .As<ITransformService>()
        .SingleInstance();
        
    builder.RegisterType<StyleService>()
        .As<IStyleService>()
        .SingleInstance();
    
    // 业务服务
    builder.RegisterType<AnchorBoltService>()  
        .As<IAnchorBoltService>()
        .InstancePerDependency();
}
```

---

## 📊 迁移计划

### 命令迁移优先级

**第1批：高频命令（影响60+命令）**
- ✅ hyab (锚栓命令) - 使用频率⭐⭐⭐⭐
- ✅ gj (钢筋绘制) - 使用频率⭐⭐⭐⭐⭐  
- ✅ bg (标高绘制) - 使用频率⭐⭐⭐⭐
- ✅ hyb1-6 (基础配筋) - 使用频率⭐⭐⭐⭐⭐

**第2批：中频命令**
- ddss/dds (尺寸标注)
- hyov (线段清理)
- hy3_Generate (三维生成)

**第3批：低频命令**
- 测试命令
- 实验性命令
- 辅助工具

### 时间安排

```
阶段7A: 服务接口设计     2天   [第1-2天]
阶段7B: 服务实现        3天   [第3-5天] 
阶段7C: 命令迁移        5天   [第6-10天]
阶段7D: 依赖注入配置     1天   [第11天]
测试验收:              1天   [第12天]
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
总计:                 12天
```

### 风险控制

**1. 向后兼容**：
- 保留旧版ZTools类（标记为Obsolete）
- 逐步迁移，不一次性删除
- 新旧版本并存一段时间

**2. 测试策略**：
- 每迁移一个命令，立即测试
- 对比新旧版本功能一致性
- 自动化测试覆盖核心服务

**3. 回滚方案**：
- Git分支管理
- 关键节点创建备份
- 支持快速回退到稳定版本

---

## 🎯 预期收益

### 架构收益
- ✅ **可测试性**：服务接口可Mock，支持单元测试
- ✅ **可维护性**：职责清晰，单一服务专注单一功能  
- ✅ **可扩展性**：新增功能只需实现接口
- ✅ **解耦合**：命令不直接依赖AutoCAD API

### 代码质量
- ✅ **行数减少**：2000+行 → 5个服务各200行 = 1000行
- ✅ **复杂度降低**：巨型类 → 小而专注的服务类
- ✅ **依赖清晰**：接口依赖 → 依赖注入

### 开发效率  
- ✅ **新命令开发**：按模板快速创建
- ✅ **问题定位**：服务边界清晰，容易调试
- ✅ **团队协作**：接口契约明确，并行开发

---

## 🚀 下一步行动

**你准备好开始了吗？**

**选项1：立即开始阶段7A**（推荐）
- ✅ 创建5个核心服务接口
- ✅ 预计2天完成

**选项2：先试点1个命令**
- ✅ 选择hyab命令做完整重构
- ✅ 验证方案可行性

**选项3：调整方案**
- ❓ 如有疑虑，可进一步讨论

你希望从哪里开始？

