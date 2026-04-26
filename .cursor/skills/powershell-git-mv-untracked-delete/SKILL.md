---
name: powershell-git-mv-untracked-delete
description: |
  避免在 PowerShell 中于 git mv 失败后仍执行 Remove-Item 删除源目录，导致未纳入 git 的源码永久丢失。
  涉及：Windows、PowerShell、git mv、HyCADTool 等大仓中大量未跟踪文件。
author: Cursor Agent
version: 1.0.0
date: 2026-04-26
---
# PowerShell：git mv 失败勿删源目录

## Problem
在 `git mv "path/*.cs" dest` 因引号/路径解析失败时，若同脚本继续 `Remove-Item` 清空源目录，且这些文件**从未被 git 跟踪**，则无法 `git checkout` 恢复，造成整树类型丢失与编译崩溃。

## Context / Trigger Conditions
- `fatal: '/*.cs' is outside repository` 或 pathspec 错误
- 仓库中 `git ls-files` 对 `src/HyCADTool` 仅部分跟踪，大量 `.cs` 处于未跟踪状态
- 清理/迁移脚本混用 `git mv` 与 `Remove-Item`

## Solution
1. **先 `git add` 要迁移的目录**再重命名，或仅使用不依赖通配符展开的正确 `git mv`（逐文件或 `Get-ChildItem | ForEach-Object { git mv ... }`）。
2. **仅在确认 git mv 成功**后再删除空目录；或改用 `move-item`+事后 `git add`/`git rm` 并复查 `git status`。
3. 对关键目录启用 **先提交或备份**再批量移动。
4. 丢失后：从网盘/本地历史/另一工作区反查；用引用代码（`JsonConfigurationLoader` 等）**反推**类型属性和默认值。

## Verification
- `git status` 无意外大量 `D` 未恢复
- 迁移后 `dotnet build` 全绿

## Notes
- 本仓库 `HyCADTool` 曾出现此事故；`Domain/ValueObjects/Configuration` 经手工按加载器/调用方**重建**。
