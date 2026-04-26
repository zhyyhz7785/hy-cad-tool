# 表格样本

| 项目 | 参数 | 说明 |
| --- | --- | --- |
| TextSize | 2.5 | 图纸文字高度（mm） |
| TextXScale | 0.7 | 宽度比例 |
| DrawScale | 40 | 出图比例分母 |
| PreviewScale | 1.8 | 预览缩放（仅预览） |

| Column | CharsPerLine | Paragraphs |
| --- | ---: | ---: |
| 1 | 32 | 5 |
| 2 | 28 | 6 |
| 3 | 24 | 7 |

表格后接普通段落，检查 blockTypes 里是否包含 `table`，并验证栏下统计变化是否平滑。
