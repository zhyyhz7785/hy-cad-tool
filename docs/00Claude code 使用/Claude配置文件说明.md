# Claude Code 配置文件说明

## 配置文件概览

Claude Code 使用多层配置体系，按优先级从高到低分为：

1. **项目级配置** - `.claude/settings.local.json` (项目根目录)
2. **用户级配置** - `~/.claude/settings.json` (用户主目录)
3. **会话数据** - `~/.claude/projects/<项目名>/` (会话历史和内存)

---

## 1. 项目级配置

**位置**: `e:\BaiduSyncdisk\Code\CSharp\CursorProjects\hy-cad-tool\.claude\settings.local.json`

**作用**: 仅对当前项目生效的配置，优先级最高，不应提交到 Git 仓库

**当前配置**:
```json
{
  "permissions": {
    "allow": [
      "Bash(git show *)",
      "Read(//tmp/**)",
      "Bash(npm run *)"
    ]
  }
}
```

### 配置项说明

#### `permissions.allow`
预先授权的命令模式，匹配这些模式的操作不会再弹出权限确认提示：

- **`Bash(git show *)`** - 允许执行所有 `git show` 命令，用于查看 Git 提交历史和文件内容
- **`Read(//tmp/**)`** - 允许读取所有 `/tmp` 目录下的文件
- **`Bash(npm run *)`** - 允许执行所有 `npm run` 命令，用于运行项目脚本

**使用场景**: 
- 减少重复的权限确认提示
- 加快常用操作的执行速度
- 适合频繁使用的安全命令

---

## 2. 用户级配置

**位置**: `C:\Users\ZHY\.claude\settings.json`

**作用**: 对所有项目生效的全局配置

**当前配置**:
```json
{
  "effortLevel": "xhigh",
  "env": {
    "ANTHROPIC_AUTH_TOKEN": "sk-936bc770fafefd461a2692ae5fdb4afc5aaef0e6b355cfd21637c5dac3d28e01",
    "ANTHROPIC_BASE_URL": "https://ai.dongli.work"
  }
}
```

### 配置项说明

#### `effortLevel`
思考深度级别，控制 Claude 在需要推理时的详尽程度：

- **当前值**: `xhigh` (超高)
- **可选值**: `low` | `medium` | `high` | `xhigh` | `max`
- **作用**: 
  - 越高的级别会在复杂问题上花费更多的推理步骤
  - 提高解决方案的准确性和全面性
  - 可能增加响应时间和 token 消耗

#### `env`
环境变量配置，用于自定义 API 访问：

- **`ANTHROPIC_AUTH_TOKEN`** - Anthropic API 的认证令牌
  - 用于访问 Claude API 服务
  - 此配置使用了自定义的认证密钥

- **`ANTHROPIC_BASE_URL`** - API 基础 URL
  - **当前值**: `https://ai.dongli.work`
  - **作用**: 将 API 请求路由到自定义服务器而不是官方服务器
  - **用途**: 可能用于企业内部代理、镜像加速或自建服务

---

## 3. 会话数据目录

**位置**: `C:\Users\ZHY\.claude\`

### 主要目录结构

```
~/.claude/
├── settings.json                    # 全局配置文件
├── history.jsonl                    # 命令历史记录
├── sessions/                        # 会话快照
├── projects/                        # 项目相关数据
│   └── e--BaiduSyncdisk-Code-CSharp-CursorProjects-hy-cad-tool/
│       ├── *.jsonl                  # 会话转录文件
│       └── memory/                  # 项目记忆存储 (当前为空)
├── cache/                           # 缓存数据
├── backups/                         # 备份文件
├── file-history/                    # 文件历史记录
├── debug/                           # 调试日志
├── plugins/                         # 插件
├── session-env/                     # 会话环境
├── shell-snapshots/                 # Shell 快照
├── telemetry/                       # 遥测数据
├── todos/                           # 待办事项
└── ide/                             # IDE 集成数据
```

### 关键目录说明

#### `projects/<项目名>/`
存储特定项目的会话数据：

- **`*.jsonl`** - 会话转录文件，记录完整的对话历史
  - 每个文件对应一个会话 ID
  - JSONL 格式（每行一个 JSON 对象）
  - 包含用户输入、模型响应、工具调用等

- **`memory/`** - 项目记忆系统
  - 存储长期记忆文件
  - 每个记忆是一个独立的 Markdown 文件
  - 当前项目的记忆目录为空

#### `sessions/`
会话状态快照，用于恢复中断的会话

#### `cache/`
缓存数据，加速重复操作

#### `file-history/`
跟踪文件修改历史，支持回滚操作

---

## 配置优先级

当同一配置项在多个文件中出现时，优先级规则：

```
项目级 (.claude/settings.local.json)
  ↓ 覆盖
用户级 (~/.claude/settings.json)
  ↓ 覆盖
默认值
```

**示例**: 如果项目级配置设置了 `effortLevel: "low"`，会覆盖用户级的 `"xhigh"`

---

## 最佳实践

### 1. 权限管理
- 仅授权可信且频繁使用的命令
- 避免使用过于宽泛的通配符（如 `Bash(*)`）
- 定期审查授权列表

### 2. 环境变量
- 敏感信息（如 API token）应放在用户级配置
- 不要将包含敏感信息的配置提交到 Git
- 考虑使用 `.gitignore` 排除 `.claude/settings.local.json`

### 3. 思考深度
- 简单任务使用 `low` 或 `medium` 节省资源
- 复杂架构决策使用 `high` 或 `xhigh`
- `max` 级别仅在最关键的场景使用

### 4. 记忆系统
- 记录项目特定的约定和模式
- 保存常用命令和配置说明
- 定期清理过时的记忆文件

---

## 常用配置场景

### 场景 1: 前端项目常用权限
```json
{
  "permissions": {
    "allow": [
      "Bash(npm *)",
      "Bash(yarn *)",
      "Bash(pnpm *)",
      "Bash(git status)",
      "Bash(git log *)",
      "Read(node_modules/**)"
    ]
  }
}
```

### 场景 2: Python 项目常用权限
```json
{
  "permissions": {
    "allow": [
      "Bash(python *)",
      "Bash(pip *)",
      "Bash(pytest *)",
      "Read(.venv/**)"
    ]
  }
}
```

### 场景 3: 使用代理服务器
```json
{
  "env": {
    "ANTHROPIC_BASE_URL": "https://your-proxy.com",
    "HTTP_PROXY": "http://proxy:8080"
  }
}
```

---

## 相关命令

- **/config** - 打开配置界面
- **/fewer-permission-prompts** - 自动分析并添加常用权限
- **/update-config** - 通过对话修改配置

---

## 注意事项

⚠️ **安全提醒**:
- API token 应妥善保管，不要分享或提交到公开仓库
- 自定义 API 地址时确保服务器可信
- 定期轮换认证凭据

⚠️ **性能提醒**:
- 过高的 `effortLevel` 会显著增加响应时间
- 大量权限授权可能降低安全性
- 会话文件会随时间增长，考虑定期清理

---

**最后更新**: 2026-07-01
**适用版本**: Claude Code (Opus 4.8)
