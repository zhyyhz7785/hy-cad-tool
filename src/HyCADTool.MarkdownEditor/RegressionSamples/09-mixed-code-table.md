# 代码块与表格混排样本

## 说明

用于验证代码块、表格、正文混排时的分栏索引稳定性与落图一致性。

## 代码块

```python
def calc_display_units(text: str) -> int:
    units = 0
    for ch in text:
        if ch.isspace():
            continue
        units += 1 if ord(ch) <= 0x007F else 2
    return units
```

## 表格

| 项目 | 值 | 备注 |
|---|---:|---|
| 文档总字数 | 1280 | 估算值 |
| 目标栏数 | 3 | A2 横向 |
| 预览缩放 | 1.0 | 默认 |

## 结论段

若本段与表格在栏底迁移，应保持“整块迁移优先”，避免统计分栏索引与最终落图顺序失配。

