# 代码块样本

下面代码块用于验证“视觉占用”与“字符占用”不完全一致时，统计是否仍可解释。

```csharp
public static class Demo
{
    public static int Sum(int a, int b)
    {
        return a + b;
    }
}
```

继续输入普通段落，观察分栏后段落索引分配是否正确。  
如果代码块在栏底发生迁移，应只整体迁移，不应被拆断。
