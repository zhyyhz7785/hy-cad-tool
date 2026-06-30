# 在 Cursor 中使用 Claude Code —— 与原生 Agent 的对比详解

> **版本**：1.0 | **日期**：2026-06-30
> **配合阅读**：[001-Cursor与Claude-Code-使用总览](./001-Cursor与Claude-Code-使用总览-2026-06-30-120000.md)（Cursor 视角）、[002-Rules与Skills-速查对照](./002-Rules与Skills-速查对照-2026-06-30-120000.md)
> **本篇视角**：Claude Code 视角 + 两者正面对比。回答「同一个仓库里 Cursor 原生 AI 和 Claude Code 怎么共存、会不会打架、目录怎么分、设置怎么共享与隔离」。

---

## 0. 一分钟看懂

本仓库现状（实测）：

| 工具 | 配置目录 | 现状 |
|------|----------|------|
| **Cursor 原生 Agent** | `.cursor/` | ✅ 很完整：6 条 `rules/*.mdc` + 12 个 `skills/*/SKILL.md` + `settings.json` |
| **Claude Code** | `.claude/` + `CLAUDE.md` | ⚠️ 几乎空白：只有一个 `settings.local.json`，**没有 `CLAUDE.md`**、没有 `.claude/skills/` |

**核心结论先给**：

1. 两个工具**各读各的目录**，默认互不干扰 → **不会自动打架**。
2. 但也**不会自动共享** → 你现在那套丰富的 `.cursor` 配置，Claude Code **基本读不到**（除了 Skills 因为是同一开放标准，可被兼容读取）。
3. 想让 Claude Code 拥有同等"项目智商"，需要补 **`CLAUDE.md`** 并让它指向已有的 `.cursor/skills/`。

---

## 1. 两个"在 Cursor 里的 AI"分别是什么

| | Cursor 原生 Agent | Claude Code |
|---|---|---|
| 出品方 | Cursor（Anysphere） | Anthropic |
| 形态 | IDE 内置功能 | 独立 CLI / IDE 扩展，**可嵌在 Cursor 里跑** |
| 入口 | 侧边栏 Agent、`Ctrl+L`、Composer、Inline | Cursor 集成终端里 `claude`，或 VS Code 系扩展面板 |
| 底层模型 | 可选多家（含 Claude） | Claude 系列（Opus / Sonnet / Haiku） |
| 配置根 | `.cursor/` | `.claude/` + 根目录 `CLAUDE.md` |
| 权限模型 | IDE 内置，无独立权限文件 | **有**：`settings.json` 的 `permissions` 白/黑名单 |
| 记忆 | Cursor Memories（云/本地） | `~/.claude/projects/<项目>/memory/` 文件 |

> 重点：Claude Code **不是** Cursor 的一个模式开关，而是**另一个独立程序**，只是恰好可以运行在 Cursor 的集成终端 / 扩展里，和原生 Agent 同时存在于一个仓库。

---

## 2. 如何在 Cursor 中使用 Claude Code

### 2.1 安装

```bash
# 方式一：npm 全局安装
npm install -g @anthropic-ai/claude-code

# 方式二：原生安装脚本（macOS/Linux）
curl -fsSL https://claude.ai/install.sh | bash
```

Windows 推荐在 **Cursor 的集成终端（Git Bash / PowerShell）** 里安装与运行。

### 2.2 在 Cursor 里启动

1. Cursor 打开本仓库 → 顶部菜单 **Terminal → New Terminal**
2. 终端输入：

```bash
claude          # 进入交互式会话
claude "帮我看看 hyXXX 命令为什么没注册上"   # 带初始任务启动
```

3. 首次运行会引导登录（OAuth / API Key）。

### 2.3 与 Cursor 协同的常用动作

| 意图 | 操作 |
|------|------|
| 让 Claude Code 知道你在编辑器选中的代码 | IDE 扩展会注入 `ide_selection`；CLI 模式则 `@文件:行号` 引用 |
| 在编辑器里看 Claude 改了什么 | Claude Code 直接改盘上文件，Cursor 的 Git 面板即时显示 diff |
| 跑斜杠命令/技能 | 会话内输入 `/skill-name` |
| 退出 | `Ctrl+C` 两次或输入 `/exit` |

> 两边可**同时开**：左边用 Cursor Composer 大范围重构，右边集成终端用 Claude Code 跑长任务/审查。它们改的是**同一份磁盘文件**，靠 Git 区分谁改了什么。

---

## 3. Rules（规则）对比：这是两者差异最大的地方

### 3.1 机制根本不同

| | Cursor 原生 Agent | Claude Code |
|---|---|---|
| 规则载体 | `.cursor/rules/*.mdc`（多文件） | 根目录 `CLAUDE.md`（单一主文件，可 `@import` 拆分） |
| 文件格式 | `.mdc` + YAML frontmatter | 纯 Markdown |
| 激活方式 | 4 种：`alwaysApply` / `globs` / `description` 智能 / `@` 手动 | **基本只有"始终加载"**：`CLAUDE.md` 全文每次进上下文 |
| 按文件类型条件加载 | ✅ `globs: **/*.xaml` | ❌ 无原生 glob 激活；靠 `@import` 子文件近似 |
| 层级 | Team > Project > User | 企业 > 全局(`~/.claude/CLAUDE.md`) > 项目(`./CLAUDE.md`) > 子目录 |
| 跨工具兼容 | 也会读 `AGENTS.md` / `CLAUDE.md` | 读 `CLAUDE.md`，**不读 `.cursor/rules/*.mdc`** |

### 3.2 关键差异点

- **Cursor 的 `.mdc` 有"条件激活"**：`globs` 命中文件才附加、`description` 让 Agent 智能选用。这是 Claude Code 的 `CLAUDE.md` 没有的——`CLAUDE.md` 是"全文常驻"，更像 Cursor 里 `alwaysApply: true` 的那几条规则的合集。
- **Claude Code 用 `@import` 拆分**：在 `CLAUDE.md` 里写 `@docs/rules/01-代码规范.md` 可把外部文件拉进来，近似 Cursor 的多 `.mdc` 文件管理。
- **Claude Code 多了"记忆系统"**：它会在 `~/.claude/projects/.../memory/` 持续沉淀事实，相当于 Cursor Memories 的文件化版本。

### 3.3 本仓库落地建议（Rules）

你那 6 条 `.cursor/rules/*.mdc` 里，`alwaysApply: true` 的（`00`、`01`、`03`、`05`）是"全项目铁律"。要让 Claude Code 也遵守，最省事的做法是**新建 `CLAUDE.md`** 并用 `@import` 指向它们：

```markdown
# CLAUDE.md（放仓库根目录）

# HyCADTool 项目规则（Claude Code 入口）

本项目同时使用 Cursor 原生 Agent，规则正文见 .cursor/rules/。
以下为 Claude Code 必须遵守的铁律（与 Cursor alwaysApply 规则一致）：

@.cursor/rules/00-AI 工作规则.mdc
@.cursor/rules/01-AI热启动模式.mdc
@.cursor/rules/05-AdWindows-WPF-PaletteSet宿主.mdc

## 编译验证
- 改完代码 AI 自己 `dotnet build`，0 error 才提示用户测试
- 改了 src/ReCall/*.cs 必须提醒用户关闭 AutoCAD 再编译
```

> 这样**一份规则源**（`.cursor/rules/`）被两个工具共用，避免维护两套。

---

## 4. Skills（技能）对比：这是两者最能"无缝共享"的地方

### 4.1 好消息：同一开放标准

`SKILL.md` 是 Anthropic 主推的 **Agent Skills 开放标准**，Cursor 和 Claude Code **都认**。所以你那 12 个 `.cursor/skills/*/SKILL.md` 在格式上对两边通用。

| | Cursor 原生 Agent | Claude Code |
|---|---|---|
| 项目级扫描路径 | `.cursor/skills/`、`.agents/skills/`，**并兼容 `.claude/skills/`** | `.claude/skills/` |
| 全局路径 | `~/.cursor/skills/`，兼容 `~/.claude/skills/` | `~/.claude/skills/` |
| 文件结构 | `<name>/SKILL.md`(+附件) | 同左 |
| 调用 | `/name`、`@name`、自然语言触发 | `/name`（Skill 工具）、自然语言触发 |
| frontmatter | `name` / `description` / `paths` / `disable-model-invocation` | `name` / `description`（额外字段一般可共存） |

### 4.2 关键差异点

- **方向不对称**：Cursor **会兼容读 `.claude/skills/`**，但 Claude Code **默认只读 `.claude/skills/` 与 `~/.claude/skills/`，不读 `.cursor/skills/`**。
- 所以你现有 12 个技能放在 `.cursor/skills/`，Cursor 用得很爽，但 **Claude Code 看不到**。

### 4.3 让两者共享 Skills 的三种做法

**做法 A —— Symlink（推荐，单一真源）**

让 `.claude/skills` 指向 `.cursor/skills`，两边读同一份：

```bash
# Git Bash（仓库根目录）
ln -s ../.cursor/skills .claude/skills

# Windows PowerShell（管理员）
New-Item -ItemType SymbolicLink -Path .claude\skills -Target .cursor\skills
```

**做法 B —— 把技能主目录定在 `.claude/skills/`**
新技能都建在 `.claude/skills/`，反向让 Cursor 去兼容读（Cursor 支持）。这样 Claude Code 是"一等公民"。

**做法 C —— 各放各的**
仅当某技能只想给其中一个工具用时才这么做（少见）。

### 4.4 斜杠命令 vs 技能的小差异

- Cursor 还有 `.cursor/commands/*.md`（斜杠命令）；Claude Code 对应 `.claude/commands/*.md`。
- 两者命令的 frontmatter 略有差异，跨用时去掉对方专有字段即可。
- 本仓库目前没用 commands 目录，统一走 Skills，跨工具更省心。

---

## 5. 配置 / Settings 对比：用途完全不同，不会冲突

| | `.cursor/settings.json` | `.claude/settings.json` / `settings.local.json` |
|---|---|---|
| 管什么 | Cursor **IDE/插件**行为（本仓库仅 `figma` 插件开关） | Claude Code 的**权限、环境变量、Hooks** |
| 典型字段 | `plugins`、editor 相关 | `permissions.allow/deny`、`env`、`hooks` |
| 是否影响对方 | ❌ Claude Code 不读 | ❌ Cursor 不读 |
| Git | 视内容，团队插件配置可提交 | `settings.json` 提交；`settings.local.json` 不提交 |

本仓库现有 [.claude/settings.local.json](../../.claude/settings.local.json)：

```json
{
  "permissions": {
    "allow": [
      "Bash(git show *)",
      "Read(//tmp/**)"
    ]
  }
}
```

> 这是**只有 Claude Code 才有**的能力——细粒度工具权限白名单。Cursor 原生 Agent 没有等价的磁盘配置文件。所以这块**没有"冲突"问题，只有"各管各"**。

---

## 6. 会不会混淆？目录如何区分？（核心问答）

### 6.1 谁读谁、一图说清

```
仓库根
├── .cursor/                      ← 只有 Cursor 原生 Agent 读
│   ├── rules/*.mdc               ← Cursor 规则（Claude Code 读不到）
│   ├── skills/*/SKILL.md         ← 两者都可读（开放标准；CC 需指向）
│   └── settings.json             ← 只 Cursor（IDE/插件）
│
├── .claude/                      ← 只有 Claude Code 读
│   ├── settings.json             ← Claude Code 权限/env/hooks
│   ├── settings.local.json       ← 同上，个人本地，不提交
│   └── skills/*/SKILL.md         ← Claude Code 技能（Cursor 兼容读）
│
├── CLAUDE.md                     ← Claude Code 主读；Cursor 也会加载
├── AGENTS.md                     ← 跨工具开放标准；两者都读（本仓库暂无）
│
└── docs/rules/*.md               ← 纯人类文档，两个 Agent 都【不自动读】
```

### 6.2 三条判别口诀

1. **`.cursor/` = Cursor 的，`.claude/` = Claude Code 的**，互不越界（Skills 例外，是共享标准）。
2. **`CLAUDE.md` / `AGENTS.md` 是"中立区"**，两个工具都读 → 放跨工具共识的规则。
3. **`docs/rules/` 不是配置**，是给人看的文档归档，Agent 运行时不加载（容易踩的坑）。

### 6.3 什么时候真的会"混淆"

| 风险 | 场景 | 规避 |
|------|------|------|
| 规则写两套且不一致 | `.cursor/rules` 和 `CLAUDE.md` 各写一份，内容漂移 | `CLAUDE.md` 用 `@import` 指向 `.cursor/rules`，单一真源 |
| Claude Code 没规则可读 | 没建 `CLAUDE.md` → CC 不知道项目铁律 | 补 `CLAUDE.md`（见 §3.3） |
| 技能只有一边能用 | 技能全在 `.cursor/skills`，CC 读不到 | symlink 或主目录定在 `.claude/skills`（见 §4.3） |
| 把 `docs/rules` 当配置 | 改了 `docs/rules` 以为 Agent 会生效 | 改 `.cursor/rules` 或 `CLAUDE.md` |
| 权限配置找错地方 | 在 `.cursor` 找 Claude 权限 | 权限只在 `.claude/settings*.json` |

---

## 7. 共同工作时：如何共享、如何区分设置

### 7.1 应该"共享"的（写在中立区或做软链）

| 内容 | 共享方式 | 落点 |
|------|----------|------|
| 项目铁律 / 编码规范 | `CLAUDE.md` `@import` 现有 `.cursor/rules` | 根目录 `CLAUDE.md` |
| 多步骤工作流（技能） | symlink 或统一主目录 | `.cursor/skills` ↔ `.claude/skills` |
| 项目说明 / 架构总览 | 新建 `AGENTS.md`（两工具都读） | 根目录 `AGENTS.md` |

### 7.2 应该"区分/隔离"的（各工具独有）

| 内容 | 归属 | 原因 |
|------|------|------|
| Cursor 的 `globs` / `description` 条件规则 | 仅 `.cursor/rules/*.mdc` | Claude Code 无此机制 |
| Claude Code 工具权限 / Hooks | 仅 `.claude/settings.json` | Cursor 无等价文件 |
| Claude Code 记忆 | `~/.claude/.../memory/` | 个人 + 工具私有，不入库 |
| 个人 API Key / 本地路径 | `.claude/settings.local.json`（CC）；Cursor User Settings（Cursor） | 敏感，不提交 |

### 7.3 推荐的 `.gitignore`

```gitignore
# Claude Code 个人本地配置（不提交）
.claude/settings.local.json

# 但要提交团队共享配置
!.claude/settings.json
!.claude/skills/

# Cursor 调试日志
.cursor/debug-*.log
```

### 7.4 团队协作清单

- [ ] 仓库根有 `CLAUDE.md`，`@import` 指向 `.cursor/rules` 的 alwaysApply 铁律
- [ ] `.claude/skills` 已 symlink 到 `.cursor/skills`（或反之），技能单一真源
- [ ] `.claude/settings.json`（团队权限）已提交；`settings.local.json`（个人）已 gitignore
- [ ] 敏感信息（Key、私有路径）只在 `settings.local.json` / Cursor User Settings
- [ ] 规则只维护一份，避免 `.cursor/rules` 与 `CLAUDE.md` 内容漂移

---

## 8. 落地：把本仓库改造成"双工具友好"

当前 `.claude` 几乎空白。建议三步打通：

```bash
# 1. 建 Claude Code 入口规则（手动写，见 §3.3 模板）
#    新建 ./CLAUDE.md，@import 现有 .cursor/rules

# 2. 让 Claude Code 复用现有 12 个技能
ln -s ../.cursor/skills .claude/skills   # Git Bash
# 或 PowerShell：New-Item -ItemType SymbolicLink -Path .claude\skills -Target .cursor\skills

# 3. 把团队通用权限从 local 提升到共享
#    把常用、安全的 Bash/Read/Write 权限写进 .claude/settings.json（提交）
```

完成后：
- Cursor 原生 Agent：照旧，零改动
- Claude Code：拥有同等的规则铁律 + 全部 12 个技能 + 团队权限

---

## 9. 速查总表

| 维度 | Cursor 原生 Agent | Claude Code |
|------|-------------------|-------------|
| 规则文件 | `.cursor/rules/*.mdc` | `CLAUDE.md`(+`@import`) |
| 规则激活 | always/globs/智能/@ 四种 | 全文常驻 |
| 技能 | `.cursor/skills/*/SKILL.md` | `.claude/skills/*/SKILL.md`（同标准） |
| 技能调用 | `/` `@` 自然语言 | `/` 自然语言 |
| 设置 | `.cursor/settings.json`(IDE) | `.claude/settings*.json`(权限/env/hooks) |
| 权限白名单 | 无 | ✅ 有 |
| 记忆 | Cursor Memories | `~/.claude/.../memory/` |
| 中立共享区 | `CLAUDE.md` / `AGENTS.md` | `CLAUDE.md` / `AGENTS.md` |
| 互读关系 | 读 `CLAUDE.md`、兼容读 `.claude/skills` | 读 `CLAUDE.md`，**不读** `.cursor/rules` |

---

## 附录：常见疑问

**Q：两个工具同时改文件会冲突吗？**
A：它们改同一份磁盘文件，不存在锁；但**不要让两边同时编辑同一文件**，否则后保存覆盖先保存。实践中分工：一个跑长任务/审查，一个做交互编辑。

**Q：`.cursor/rules` 我已经写得很全，非要再写 `CLAUDE.md` 吗？**
A：只要你也想用 Claude Code，就要。否则 Claude Code 对项目铁律一无所知。用 `@import` 指向 `.cursor/rules` 即可，不必重写。

**Q：Skills 到底放 `.cursor` 还是 `.claude`？**
A：选一个当主目录，另一个用 symlink 指过去。Cursor 两个都兼容读，所以**主目录定在 `.claude/skills` 对 Claude Code 更稳**。

**Q：`docs/rules/` 和 `.cursor/rules/` 区别？**
A：`docs/rules/` 是给人看的文档归档，Agent **不**自动读；`.cursor/rules/` 才是 Cursor 运行时加载的真规则。

---

*维护：本仓库 `.cursor` / `.claude` 配置变更时，请同步更新 §0 现状表与 §8 落地步骤。*
