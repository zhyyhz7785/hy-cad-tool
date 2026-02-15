# L4 诊断检查清单（详细版）

当 SKILL.md 中的 L4 基础检查维度不够时，参考此文件补充深入检查。

---

## WPF / MVVM 专项

| 检查项 | 问题表现 | 修复方向 |
|--------|----------|----------|
| View 代码隐藏过重 | .xaml.cs 超过 200 行业务逻辑 | 提取到 ViewModel 或 Service |
| ViewModel 引用 UI 类型 | 引用 System.Windows.* | 通过接口/事件解耦 |
| 缺少 ICommand | 事件处理写在 code-behind | 用 RelayCommand 绑定 |
| 属性变更未通知 | 赋值后 UI 不更新 | 确保 OnPropertyChanged |
| 资源字典冗余 | 相同样式重复定义 | 提取到 EditorTheme.xaml |

## WebView2 专项

| 检查项 | 问题表现 | 修复方向 |
|--------|----------|----------|
| 初始化未等待 | EnsureCoreWebView2Async 未 await | 添加 await + 错误处理 |
| JS 调用无超时 | ExecuteScriptAsync 卡死 | 添加 CancellationToken |
| 内存泄漏 | WebView2 未 Dispose | 窗口关闭时显式释放 |
| 消息传递无验证 | PostWebMessageAsJson 未校验 | 添加 schema 验证 |

## 数据流专项

| 检查项 | 问题表现 | 修复方向 |
|--------|----------|----------|
| JSON 契约不稳定 | 新增字段导致旧版崩溃 | 添加 SchemaVersion + 兼容处理 |
| 序列化异常静默 | Deserialize 失败返回 null | 显式 try-catch + 日志 |
| 配置无默认值 | Config 字段为 0/null | 构造函数赋默认值 |

## 文件/缓存专项

| 检查项 | 问题表现 | 修复方向 |
|--------|----------|----------|
| 缓存无版本管理 | Vditor 升级后仍用旧缓存 | 版本号作为目录名 |
| 缓存无清理机制 | 旧版本目录永远保留 | 启动时清理非当前版本 |
| 文件锁未处理 | 多实例同时写文件 | 使用 FileShare 或互斥锁 |
| 路径拼接不安全 | 手动拼接 `\` | 使用 Path.Combine |
