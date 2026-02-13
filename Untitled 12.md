## 关键对象名称

### WPF（EditorWindow.xaml）

- 标尺容器：PreviewRulerGrid

- 水平标尺控件：PreviewHRuler（RulerControl）

- 垂直标尺控件：PreviewVRuler（RulerControl）

- 标尺左上角块：RulerCorner

- 图纸预览浏览器：PreviewBrowser（WebBrowser）

### HTML（PreviewHtmlRenderer.cs 生成）

- 整张图纸：#paper

- 图纸内容区：#paper-inner

- 图纸下方标题带：#paper-foot

- 第 i 栏内容：#col-i

- 每栏字数：#chars-i

------

## 当前换算逻辑（你重点看这个）

- 缩放系数：x = PreviewScale

- 标尺换算：1 mm = x px

- 图纸换算：paperPx = mm * x

- 所以示例：

- A2 宽：594 * x

- A2 高：420 * x

- 若 x=1.5，则 891 x 630 px

------

## 调试数据来源与含义

### C# 采集入口（EditorWindow.xaml.cs）

- 方法：ProbePreviewAlignmentAsync()

- 调用时机：RefreshPreview() 后自动采样

- 读取 JS：PreviewBrowser.InvokeScript("getDebugMetrics")

### JS 导出（PreviewHtmlRenderer.cs 内）

- 方法：getDebugMetrics()

- 核心字段：

- ppm：HTML 侧 mm→px 比例

- paperW/paperH：图纸实测像素

- pageWmm/pageHmm：图纸毫米尺寸

- paperTop/paperBottom：图纸边界

- footTop/footBottom：下方标题带边界

### 状态栏 DBG 字段解释

- x：当前缩放系数

- rulerPpm：标尺 ppm

- htmlPpm：HTML ppm

- paper=WxH：实测像素

- exp=WxH：按 mm*x 计算的期望像素

- Δw/Δh：实测 - 期望（应尽量为 0）

- rectY=a->b：图纸上/下边界

- footY=a->b：底部标题带边界（这是纸外区域）

------

## 你可直接用的自测标准

- rulerPpm == htmlPpm == x

- Δw == 0 且 Δh == 0（允许 ±1px 以内浮动）

- A2 + x=1.5 时：paper=891x630

- 图纸底边应看 rectY 的下界，不看 footY

------

如果你要，我可以再给你一份“最小测试用例参数表”（A0~A4 在 x=1.0/1.5/2.0 的期望像素清单），你只需对照 DBG 一项项勾选即可。


Width="960" Height="720"