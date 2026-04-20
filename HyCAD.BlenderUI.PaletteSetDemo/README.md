# HyCAD.BlenderUI.PaletteSetDemo

AutoCAD 插件样本：在 `PaletteSet` 中托管 `HyCAD.BlenderUI`（`BlenderArea` + `Properties` Space 壳）。

## 构建

```bash
dotnet build HyCAD.BlenderUI.PaletteSetDemo.csproj -c Debug
```

## 加载

将输出目录中的 `HyCAD.BlenderUI.PaletteSetDemo.dll`、`HyCAD.BlenderUI.dll` 及依赖一并放到 AutoCAD 可加载路径，执行 `NETLOAD` 选择该 DLL，然后运行命令 **`BLENDERUI_DEMO`**。

## 命令

| 命令 | 说明 |
|------|------|
| `BLENDERUI_DEMO` | 显示 PaletteSet 演示面板 |
