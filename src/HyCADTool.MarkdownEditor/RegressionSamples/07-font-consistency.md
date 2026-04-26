# 字体一致性样本

## 目标

验证预览与插入在正文、粗体、代码字体上的一致性表现。

## 正文段落

本段为中文正文，建议观察字形风格、字面宽度、行内换行位置是否稳定。  
This paragraph mixes English words to verify Latin glyph width behavior.

## 粗体段落

这是普通文字，下面是粗体文字：**同家族粗体展示效果**。  
观察粗体前后是否出现明显跨家族跳变（尤其在中文和英文混排时）。

## 行内代码与代码块

行内代码示例：`FontFallback("Consolas","Courier New")`。  

```csharp
public static string ResolveCodeFont()
{
    return "Consolas -> Courier New";
}
```

请确认代码块与行内代码可读，不因字体替换导致字距明显异常。

