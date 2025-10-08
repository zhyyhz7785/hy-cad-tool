# HyCADTool - 专业 CAD 辅助设计工具集

## 📖 项目简介

**HyCADTool** 是一个基于 AutoCAD .NET API 开发的专业 CAD 辅助设计插件，专为结构工程设计领域打造。提供钢筋配筋、地脚螺栓、桩基布置、三维建模、智能标注等80余个专业命令，大幅提升设计效率。

### ⭐ 核心特性

- ✅ **高度自动化** - 从底图处理到配筋生成全流程自动化
- ✅ **智能算法** - 集成 Voronoi、Lloyd、DCEL 等先进算法
- ✅ **专业适配** - 深度适配 YJK、PKPM 等国内主流结构软件
- ✅ **批量处理** - 支持大批量图元的高效处理
- ✅ **易于扩展** - 清晰的分层架构，支持快速功能扩展
- ✅ **配置灵活** - 丰富的参数配置，适应不同设计标准

---

## 📚 文档导航

### 📖 核心文档

| 文档 | 描述 | 适合人群 |
|------|------|---------|
| **[项目功能详细介绍](./项目功能详细介绍.md)** | 完整的功能说明、使用场景和技术特点 | 所有用户 |
| **[命令速查表](./命令速查表.md)** | 所有命令快速查询、常用组合和使用技巧 | 日常使用 |
| **[功能架构与分类](./功能架构与分类.md)** | 系统架构、技术实现和开发指南 | 开发者 |

### 📝 快速链接

- [快速开始](#快速开始) - 5分钟上手
- [安装说明](#安装说明) - 插件安装步骤
- [典型应用场景](#典型应用场景) - 实战案例
- [常见问题](#常见问题) - FAQ
- [联系与支持](#联系与支持)

---

## 🚀 快速开始

### 第一步：打开主面板

在 AutoCAD 命令行输入：
```
hy
```

将打开 HyCADTool 主控制面板，包含5个子面板：
- **钢筋** - 钢筋绘制与标注
- **过滤器** - 图元筛选工具
- **基础钢筋** - 基础配筋自动生成
- **桩** - 桩基布置与优化
- **螺栓聚类与基础标注** - 地脚螺栓管理

### 第二步：尝试核心功能

#### 🔹 场景1：基础底板配筋（最常用）

```
命令: hyb5all
```
一键完成以下流程：
1. 优化底图
2. 筛选无用文本
3. 识别有限元网格
4. 空间分组
5. 生成配筋
6. 自动标注

#### 🔹 场景2：绘制标高符号

```
命令: bg
```
交互式绘制标高符号，实时预览，支持连续绘制。

#### 🔹 场景3：批量标注

```
命令: ddss
```
选择多个多段线，自动添加智能尺寸标注。

#### 🔹 场景4：创建地脚螺栓

```
命令: hyab
提示: 请输入地脚螺栓型号 (1-9) [默认1]: 3
提示: 请选择多个圆: (选择圆形)
```
自动将圆转换为带数据的螺栓图元。

### 第三步：查看效果

所有生成的图元都会自动放置在对应的图层中，命名规范为：
- `00_Hy_螺栓_1` ~ `00_Hy_螺栓_9`
- `00_Hy_配筋`
- `00_Hy_标注`
- `00_Hy_轴线`

---

## 💻 安装说明

### 环境要求

- **操作系统**: Windows 10/11
- **AutoCAD 版本**: AutoCAD 2024 或更高版本
- **.NET Framework**: 4.8

### 安装步骤

1. **编译项目**
   ```bash
   # 使用 Visual Studio 2019/2022
   打开 HyCADtoolGpt.sln
   选择 Release 配置
   生成解决方案
   ```

2. **定位 DLL 文件**
   ```
   输出目录: HyCADtool\bin\Release\HyCADTool.dll
   ```

3. **加载插件**

   **方法一：NETLOAD 命令**
   ```
   AutoCAD 命令行输入: NETLOAD
   浏览并选择: HyCADTool.dll
   ```

   **方法二：自动加载 (推荐)**
   
   创建 `HyCADTool.bundle` 文件夹结构：
   ```
   HyCADTool.bundle/
   ├── Contents/
   │   ├── Windows/
   │   │   └── HyCADTool.dll
   │   └── PackageContents.xml
   ```

   `PackageContents.xml` 内容：
   ```xml
   <?xml version="1.0" encoding="utf-8"?>
   <ApplicationPackage>
     <Components>
       <RuntimeRequirements OS="Win64" Platform="AutoCAD"/>
       <ComponentEntry AppName="HyCADTool" ModuleName="./HyCADTool.dll"/>
     </Components>
   </ApplicationPackage>
   ```

   将 `HyCADTool.bundle` 复制到：
   ```
   C:\ProgramData\Autodesk\ApplicationPlugins\
   ```

4. **验证安装**

   重启 AutoCAD，在命令行输入 `hy`，如果主面板打开，则安装成功。

---

## 🎯 典型应用场景

### 场景1：基础底板配筋设计（完整流程）

```
步骤1: 导入 YJK/PKPM 有限元计算结果 (DWG格式)

步骤2: 一键生成配筋
命令: hyb5all
等待: 约10-30秒（取决于图元数量）

步骤3: 批量标注
命令: ddss
选择: 生成的配筋多段线

步骤4: 生成布局
命令: HYMBR
结果: 自动分组并创建布局视口
```

**时间节省**: 传统手工绘制需要 2-4 小时，使用插件仅需 **5-10 分钟**

### 场景2：设备基础设计（带地脚螺栓）

```
步骤1: 绘制基础轮廓（矩形或多边形）

步骤2: 在轮廓内绘制圆（表示螺栓位置）

步骤3: 创建地脚螺栓
命令: hyab
输入: 3  (选择3号螺栓)
选择: 所有圆

步骤4: 对齐螺栓
命令: hyabA_Align
选择: 垂直参考线
结果: 螺栓自动对齐到轴线交点

步骤5: 计算受力
命令: hyabC_Calculat
选择: 基础轮廓线
输入: 荷载参数

步骤6: 生成汇总表
命令: hyabCT_CreateAnchorBoltTable
结果: 自动生成螺栓统计表格

步骤7: 生成三维模型
命令: hy3_Generate
结果: 从平面生成三维基础模型
```

**时间节省**: 传统流程需要 1-2 小时，使用插件仅需 **15-20 分钟**

### 场景3：桩基布置优化

```
步骤1: 打开桩基面板
命令: hy
选择: "桩" 标签页

步骤2: 配置参数
- 桩型: 圆形
- 直径: 600mm
- 最小桩距: 1800mm
- 边距: 上下左右各400mm

步骤3: 选择基础边界
提示: 选择基础轮廓线（闭合多段线）

步骤4: 运行优化算法
命令: hyz_PlacePileAndVoronoiWithLloydOptimization
算法: Voronoi + Lloyd 迭代优化
迭代次数: 10次（可配置）

步骤5: 查看结果
- 桩位圆形自动生成
- Voronoi 单元显示
- 检查桩距是否满足要求

步骤6: 手动微调（可选）
使用 AutoCAD 移动命令微调个别桩位
```

**时间节省**: 传统手工布桩需要 30-60 分钟，使用插件仅需 **3-5 分钟**

### 场景4：批量出图

```
步骤1: 模型空间准备
在模型空间中按区域排列多个图形

步骤2: 创建最小边界矩形
命令: HYMBR
功能: 自动识别图形分组，生成聚类MBR

步骤3: 批量创建视口
命令: HYMBRC_CreateLayoutViewports
结果: 在布局中自动创建多个视口
比例: 1:50 (可配置)

步骤4: 添加图框
命令: HYMBRD_DrawTitleBlock
选择: 标准图框模板

步骤5: 批量打印
使用 AutoCAD 批量打印功能
```

**时间节省**: 传统手工创建10张图需要 30-40 分钟，使用插件仅需 **5 分钟**

---

## 📊 功能统计

| 功能模块 | 命令数量 | 核心特性 |
|---------|---------|---------|
| 钢筋配筋 | 12 | 自动锚固、智能延伸、批量标注 |
| 基础底板配筋 | 7 | YJK/PKPM 适配、一键生成 |
| 地脚螺栓 | 6 | 数据存储、受力计算、汇总表 |
| 标高与剖面 | 6 | Jig 绘制、三维建模 |
| 尺寸标注 | 4 | 智能识别、轴线分组 |
| 轴线功能 | 2 | 自动编号、区域划分 |
| 桩基布置 | 2 | Voronoi 优化、Lloyd 算法 |
| 设备基础 | 3 | 扩展字典、数据关联 |
| 图形处理 | 8 | Overkill、DCEL、Clipper2 |
| 布局出图 | 3 | 自动排版、批量视口 |
| 数据导出 | 3 | CSV、Markdown |
| 其他工具 | 4 | 图块处理、文本管理 |
| **合计** | **60+** | **80+ 专业命令** |

---

## 🔥 最常用命令 Top 10

| 排名 | 命令 | 功能 | 使用频率 |
|-----|------|------|---------|
| 1 | `hy` | 打开主面板 | ⭐⭐⭐⭐⭐ |
| 2 | `hyb5all` | 一键基础配筋 | ⭐⭐⭐⭐⭐ |
| 3 | `ddss` | 批量标注 | ⭐⭐⭐⭐⭐ |
| 4 | `hyab` | 创建螺栓 | ⭐⭐⭐⭐ |
| 5 | `bg` | 绘制标高 | ⭐⭐⭐⭐ |
| 6 | `hyov` | 清理重复线 | ⭐⭐⭐⭐ |
| 7 | `hy3_Generate` | 生成三维模型 | ⭐⭐⭐ |
| 8 | `HYMBR` | 批量布局 | ⭐⭐⭐ |
| 9 | `gj` | 钢筋绘制 | ⭐⭐⭐ |
| 10 | `hyz_PlacePileAndVoronoiWithLloydOptimization` | 优化布桩 | ⭐⭐⭐ |

---

## ⚙️ 配置说明

### 配置文件位置

```
HyCADtool/config.json
```

### 主要配置项

#### 1. 桩基配置 (PileConfigData)

```json
{
  "PileConfigData": {
    "Section": "Circle",           // 桩型: Circle/Square
    "DiameterOrEdge": 400.0,       // 直径或边长 (mm)
    "ArrangementType": "Rectangle", // 排列: Rectangle/Circular
    "MinPileCenterDistance": 1200.0, // 最小桩距 (mm)
    "Margin": {                     // 边距 (mm)
      "Up": 400.0,
      "Down": 400.0,
      "Left": 400.0,
      "Right": 400.0
    }
  }
}
```

#### 2. 样式配置 (BaseConfigData)

```json
{
  "BaseConfigData": {
    "TextStyle": {
      "name": "0_Hy_40",
      "fontFileName": "tssdeng.shx",
      "bigFontFileName": "hztxt.shx",
      "textSize": 2.5,
      "textXScale": 0.7
    },
    "DimStyle": {
      "name": "0_Hy_40_Dim",
      "dimtxt": 2.5,
      "dimexo": 1.0,
      "dimexe": 1.0
    }
  }
}
```

### 修改比例

在代码中修改：
```csharp
BaseConfig.Scale = 30;  // 默认比例 1:30
```

---

## 🛠️ 开发与扩展

### 技术栈

- **.NET Framework**: 4.8
- **AutoCAD .NET API**: 24.3.0
- **几何库**: Clipper2, NetTopologySuite
- **UI 框架**: WPF (MVVM)

### 添加新命令

```csharp
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;

[CommandMethod("MyCommand")]
public static void MyNewCommand()
{
    Document doc = Application.DocumentManager.MdiActiveDocument;
    Editor ed = doc.Editor;
    
    using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
    {
        // 实现功能
        tr.Commit();
    }
    
    ed.WriteMessage("\n命令执行完成！");
}
```

### 项目结构

详见 [功能架构与分类](./功能架构与分类.md)

---

## ❓ 常见问题

### Q1: 命令输入后无响应？

**A**: 
1. 检查插件是否已加载（`NETLOAD` 或自动加载）
2. 查看命令行是否有错误提示
3. 重启 AutoCAD 并重新加载插件

### Q2: 配筋生成结果不理想？

**A**:
1. 使用分步命令 `hyb1` - `hyb6` 逐步检查
2. 调整 `config.json` 中的参数
3. 检查输入数据是否符合要求（YJK/PKPM 格式）

### Q3: 螺栓数据丢失？

**A**:
1. 螺栓数据存储在扩展字典中，避免使用"分解"命令
2. 使用 `hyabCD_ChangeDisPlay` 切换显示模式
3. 使用 `hyef_Base_HighlightBoltData` 查看数据

### Q4: 标注样式不符合要求？

**A**:
1. 修改 `config.json` 中的 `DimStyle` 配置
2. 调整 `BaseConfig.Scale` 比例
3. 手动修改 AutoCAD 标注样式后保存为模板

### Q5: Voronoi 优化布桩失败？

**A**:
1. 检查基础边界是否为闭合多段线
2. 调整最小桩距参数
3. 检查边距设置是否合理

### Q6: 三维模型生成不完整？

**A**:
1. 确保平面配筋数据完整
2. 检查是否有重叠或断开的线段
3. 使用 `hyov` 清理重复线段后重试

---

## 📖 延伸阅读

### 相关文档

- **[项目功能详细介绍](./项目功能详细介绍.md)** - 完整功能说明
- **[命令速查表](./命令速查表.md)** - 命令快速查询
- **[功能架构与分类](./功能架构与分类.md)** - 架构与开发指南
- **[轴线系统说明](./Models/Axis/AxisReadme.md)** - 轴线功能详解

### 外部资源

- [AutoCAD .NET API 文档](https://help.autodesk.com/view/OARX/2024/CHS/)
- [Clipper2 库文档](https://github.com/AngusJohnson/Clipper2)
- [NetTopologySuite 文档](https://nettopologysuite.github.io/NetTopologySuite/)

---

## 📄 许可证

本项目为专业 CAD 辅助工具，版权归开发团队所有。

---

## 👥 联系与支持

### 版本信息

- **当前分支**: RefactorDev
- **框架版本**: .NET Framework 4.8
- **目标 CAD**: AutoCAD 2024
- **文档生成**: 2025-10-07

### 技术支持

如有问题或建议，请联系开发团队。

---

## 🌟 致谢

感谢以下开源项目：
- **Clipper2** - 强大的多边形布尔运算库
- **NetTopologySuite** - 全面的拓扑分析工具
- **RectanglePacker** - 高效的矩形装箱算法

---

<div align="center">

**HyCADTool** - 让结构设计更高效

Made with ❤️ by HyCADTool Team

</div>

