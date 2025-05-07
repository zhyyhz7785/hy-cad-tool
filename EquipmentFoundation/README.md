

# AutoCADPluginProject

## 项目概述

`AutoCADPluginProject` 是一个基于 AutoCAD 的插件项目，旨在实现设备基础的几何建模和结果生成功能。它支持从 AutoCAD 环境中提取多边形和标高数据，计算墙体和底板厚度，生成剖面图、墙体和底板/筏板的三维模型。该项目由 xAI 提供技术支持，使用 C# 和 AutoCAD .NET API 开发。

当前日期：2025年4月6日

## 功能特点

- **数据转换**: 从 AutoCAD 模型空间提取多边形和标高信息，转换为几何数据模型。
- **几何运算**: 计算墙体和底板厚度，基于标高差和跨度。
- **结果生成**: 生成剖面图、墙体和底板/筏板的三维模型，并输出到 AutoCAD。
- **用户交互**: 提供命令接口，支持用户选择剖面线和输入参数。

## 目录结构

```
AutoCADPluginProject/
├── Core/                    # 核心逻辑模块
│   ├── EntityType.cs        # 定义边界条件、墙体数据和几何数据类
│   ├── DataConverter.cs     # 数据转换逻辑
│   ├── GeometryOperations.cs # 几何计算和集合运算
│   └── ResultProcessor.cs   # 结果生成和处理
├── Commands/                # AutoCAD命令模块
│   ├── CreateTypeCommand.cs # 创建类型命令
│   ├── ConvertCommand.cs    # 数据转换命令
│   ├── OperationCommand.cs  # 几何运算命令
│   └── OutputCommand.cs     # 输出结果命令
├── Utils/                   # 工具模块
│   ├── AutoCADHelper.cs     # AutoCAD交互工具
│   ├── SelectionUtil.cs     # 用户选择辅助工具
│   └── Logging.cs           # 日志记录工具
├── Models/                  # 数据模型模块
│   ├── CustomEntity.cs      # 自定义枚举类型
│   └── ResultEntity.cs      # 结果实体类
├── Properties/              # 项目属性
│   └── AssemblyInfo.cs      # 程序集信息
├── App.config               # 配置文件（可选）
└── README.md                # 项目说明文档
```

## 安装说明

1. **环境要求**:
   - AutoCAD 2018 或更高版本
   - .NET Framework 4.7.2 或更高版本
   - Visual Studio 2019 或更高版本（推荐）

2. **编译项目**:
   - 克隆或下载项目代码到本地。
   - 在 Visual Studio 中打开 `AutoCADPluginProject.sln`。
   - 确保引用 AutoCAD .NET API（`AcCoreMgd.dll`, `AcDbMgd.dll`, `AcMgd.dll`）。
   - 设置目标框架并编译项目，生成 `AutoCADPluginProject.dll`。

3. **加载插件**:
   - 打开 AutoCAD。
   - 输入命令 `NETLOAD`，选择生成的 `AutoCADPluginProject.dll` 文件并加载。

## 使用方法

1. **准备数据**:
   - 在 AutoCAD 模型空间中绘制闭合多边形（`LWPOLYLINE`）。
   - 使用 `TEXT` 对象标注多边形的标高（例如 "1000" 表示 1000mm）。

2. **执行命令**:
   - **`ConvertData`**: 转换 AutoCAD 数据为几何模型。
     ```
     Command: ConvertData
     ```
   - **`PerformOperations`**: 执行几何运算，计算墙体和底板厚度。
     ```
     Command: PerformOperations
     ```
   - **`CreateSectionCommand`**: 生成并绘制剖面图。
     ```
     Command: CreateSectionCommand
     ```
   - **`CreateType`**: 创建自定义类型（例如墙体类型）。
     ```
     Command: CreateType
     ```

3. **查看结果**:
   - 转换和运算结果会输出到 AutoCAD 命令行。
   - 剖面图、墙体和底板模型会绘制到指定图层（`Section`, `Walls`, `Base`）。

## 示例

### 输入
- 绘制两个闭合多边形，一个标高为 "0"（外部轮廓），另一个标高为 "-1000"。
- 执行 `ConvertData`，查看几何数据。
- 执行 `PerformOperations`，计算厚度。
- 执行 `CreateSectionCommand`，选择剖面线生成剖面图。

### 输出
- 命令行输出：
  ```
  成功转换 2 个几何数据对象：
  多边形标高：0，有效墙体数：0，底板厚度：400
  多边形标高：-1000，有效墙体数：4，底板厚度：400
  剖面图创建成功。
  ```
- 模型空间中生成剖面图和三维模型。

## 注意事项

- **依赖未实现类**: 项目中部分类（如 `AnchorBolt`, `AnchorBolt`）为占位符，需根据实际需求补充实现。
- **错误处理**: 若输入数据无效（例如未闭合多边形或无标高文本），命令会输出提示信息。
- **日志输出**: 日志通过 AutoCAD 编辑器显示，建议在调试时关注命令行信息。

## 贡献

欢迎提交问题或拉取请求至项目仓库。联系方式：support@xai.com

## 许可证

本项目采用 MIT 许可证，详情见 `LICENSE` 文件（若存在）。

---

### 说明
- **内容调整**: 根据你的项目特点，我生成了一个通用的 `README.md`，涵盖了核心功能和使用说明。
- **扩展性**: 如果需要添加更多细节（如具体依赖版本、测试用例），请告诉我，我可以进一步完善。
- **文件生成**: 你可以将此内容保存为 `README.md` 并放入项目根目录。

如果有其他需求（如调整格式或添加特定章节），请随时告诉我！