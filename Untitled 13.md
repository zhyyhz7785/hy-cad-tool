# WPF DIP 与物理像素、文字高度、标尺换算 完整说明

## 一、WPF 的 DIP（设备无关像素）

### 1.1 什么是 DIP

WPF 中所有尺寸单位都是 DIP（Device Independent Pixel），也叫"逻辑像素"。

1 DIP = 1/96 英寸（固定定义）

当你在 XAML 里写 Width="960" Height="720" 时：

- 这是 960 DIP × 720 DIP

- 不是 960 个屏幕物理像素

### 1.2 DIP → 物理像素换算

物理像素 = DIP × DPI缩放系数

| Windows 缩放 | DPI  | 缩放系数 | 960 DIP → 物理像素 | 720 DIP → 物理像素 |
| :----------- | :--- | :------- | :----------------- | :----------------- |
| 100%         | 96   | 1.0      | 960 px             | 720 px             |
| 125%         | 120  | 1.25     | 1200 px            | 900 px             |
| 150%         | 144  | 1.5      | 1440 px            | 1080 px            |
| 175%         | 168  | 1.75     | 1680 px            | 1260 px            |
| 200%         | 192  | 2.0      | 1920 px            | 1440 px            |

### 1.3 为什么这么设计

- 同一份 XAML 在不同 DPI 屏幕上"看起来一样大"

- 程序员只关心逻辑尺寸，WPF 自动处理物理渲染

- 字体、控件、布局全部基于 DIP

### 1.4 代码中获取缩放系数

// 方法 1：从窗口获取

var source = PresentationSource.FromVisual(this);

double dpiX = source.CompositionTarget.TransformToDevice.M11; // 水平缩放系数

double dpiY = source.CompositionTarget.TransformToDevice.M22; // 垂直缩放系数

// 方法 2：获取 DPI 值

double dpi = dpiX * 96; // 例如 1.5 × 96 = 144 DPI

------

## 二、文字高度 2.5mm 是如何显示的

### 2.1 换算链路

项目中用户设定的"字高"单位是 mm（毫米），但屏幕显示用的是 CSS px（像素）。

换算公式（定义在 PreviewHtmlRenderer.cs）：

fontPx = TextSize(mm) × BASE_FONT_PX × PreviewScale(x)

其中：

- BASE_FONT_PX = 8.0（常量）

- PreviewScale 即 x，默认 1.5

所以默认情况下：

fontPx = 2.5 × 8.0 × 1.5 = 30.0 px

### 2.2 字高变化时的联动

| 字高 (mm) | x=1.0 时字体 | x=1.5 时字体 | x=2.0 时字体 |
| :-------- | :----------- | :----------- | :----------- |
| 2.0       | 16.0 px      | 24.0 px      | 32.0 px      |
| 2.5       | 20.0 px      | 30.0 px      | 40.0 px      |
| 3.0       | 24.0 px      | 36.0 px      | 48.0 px      |
| 5.0       | 40.0 px      | 60.0 px      | 80.0 px      |

### 2.3 每行字数计算

定义在 PreviewHtmlRenderer.cs 的 JS distribute() 函数中：

var contentMm = contentWidthPx / ppm;    *// 栏内容宽度(mm)*

var charMm  = textMm * textXScale;     *// 单字宽度(mm)*

var charsPerLine = Math.floor(contentMm / charMm);

- ppm = x（即 PreviewScale）

- textMm = TextSize（字高，默认 2.5）

- textXScale = TextXScale（字宽比，默认 0.7）

例：栏宽 200mm，字高 2.5mm，字宽比 0.7：

单字宽 = 2.5 × 0.7 = 1.75 mm

每行字数 = 200 / 1.75 ≈ 114 字/行

------

## 三、标尺数字是如何计算和显示的

### 3.1 核心公式

标尺的换算系数定义在 EditorWindow.xaml.cs 的 UpdateRulerScale() 中：

double pixelsPerMm = PreviewScale; // 即 x

这意味着：

1mm 在屏幕上 = x 个 CSS 像素（DIP）

默认 x=1.5 时：

- 1mm = 1.5 DIP

- 20mm 刻度间距 = 30 DIP

- 297mm（A4 高度）= 445.5 DIP

- 420mm（A2 高度）= 630 DIP

### 3.2 标尺控件渲染逻辑

标尺控件是 RulerControl.cs，核心参数：

| 参数         | 值                | 说明                       |
| :----------- | :---------------- | :------------------------- |
| PixelsPerMm  | 由 C# 传入（= x） | mm → DIP 换算              |
| MAJOR_MM     | 20                | 大刻度间距（mm）           |
| MINOR_MM     | 4                 | 小刻度间距（mm）           |
| THICKNESS    | 24 DIP            | 标尺宽度/高度              |
| SegmentCount | 1                 | 水平标尺连续（不按栏重置） |

渲染过程（OnRender）：

1. 计算总长度：totalMm = 控件实际长度(DIP) / PixelsPerMm
2. 从 0 开始，每 MINOR_MM(4mm) 画一个小刻度
3. 每 MAJOR_MM(20mm) 画一个大刻度 + 数字标注
4. 刻度位置(DIP) = mm值 × PixelsPerMm

### 3.3 标尺与图纸对齐

图纸像素尺寸也使用相同公式（定义在 PreviewHtmlRenderer.cs）：

paperWidthPx = PageWidthMm × x

paperHeightPx = PageHeightMm × x

所以标尺上 297 处的像素位置 = 297 × x，图纸右边界像素位置也 = 297 × x。两者使用同一个 x，天然对齐。

### 3.4 Ctrl+滚轮缩放时

- 改变的是 x（PreviewScale）

- 标尺：PixelsPerMm 更新 → 刻度间距变大/变小

- 图纸：paperPx 重新计算 → 图纸跟着放大/缩小

- 标签上的 mm 数字不变（20, 40, 60...），但物理间距变了（放大镜效果）

### 3.5 完整换算示例（A2 横向，x=1.5）

图纸：594mm × 420mm

标尺：

 水平：0, 20, 40, ... 594 (每格 20mm = 30 DIP)

 垂直：0, 20, 40, ... 420 (每格 20mm = 30 DIP)

图纸像素：594×1.5 = 891 DIP × 420×1.5 = 630 DIP

标尺 420mm 处：420×1.5 = 630 DIP ← 和图纸底边精确对齐

------

## 四、三者关系总结

用户设定          中间量           屏幕显示

─────────        ──────────        ──────────

TextSize (mm) ──┐

​         ├──→ fontPx = TextSize × 8 × x ──→ CSS 字体大小

PreviewScale (x) ┤

​         ├──→ ppm = x           ──→ 标尺 PixelsPerMm

​         │

PageWidthMm ───┼──→ paperPx = mm × x      ──→ 图纸像素宽高

PageHeightMm ───┘

字高变 → 字体变、字/行变 → 图纸/标尺不变

x 变  → 字体变、图纸变、标尺变（放大镜效果）

------

如果你需要我把这份文档保存到项目的 doc/ 目录下，切到 Agent 模式告诉我，我直接帮你存。