#!/usr/bin/env python3
"""按命名空间扫描 Features/Shared 现状，写出架构白名单（仅生成基线，测试以 C# 扫描为准）。"""
from __future__ import annotations

import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[3]
SRC = REPO / "src" / "HyCADTool"
OUT = Path(__file__).resolve().parent

NS_RE = re.compile(r"^namespace\s+([A-Za-z0-9_.]+)", re.M)
SHELL_RE = re.compile(r"HyCADTool\.Shell")
GETPOINT_RE = re.compile(r"\.\s*GetPoint\s*\(")
PROCESS_WIDE_RE = re.compile(r"\[\s*ProcessWide\b")
STATIC_FIELD_RE = re.compile(
    r"^\s*(?:(?:public|private|internal|protected|new|unsafe|volatile|partial|static|readonly|const)\s+)+static\s+"
    r"(?!readonly\b)(?!const\b)(?!event\b)(?!class\b)(?!struct\b)(?!enum\b)(?!delegate\b)(?!implicit\b)(?!explicit\b)"
)

FEATURE_PREFIXES = ("HyCADTool.Features", "HyCADTool.Domain")
SHARED_PREFIXES = ("HyCADTool.Shared",)


def strip_comments(text: str) -> str:
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.S)
    lines = []
    for line in text.splitlines():
        if "//" in line:
            line = line[: line.index("//")]
        lines.append(line)
    return "\n".join(lines)


def is_field(line: str) -> bool:
    if re.search(r"\b\w+\s*=>", line) or re.search(r"\)\s*=>", line):
        return False
    if "(" in line:
        before_paren = line.split("(", 1)[0]
        if "{" not in before_paren and not re.search(r"\bnew\b", before_paren):
            return False
    if re.search(r"\{\s*(get|set|add|remove)\b", line):
        return False
    if line.rstrip().endswith("{") or "{ get" in line or "{get" in line:
        return False
    return ";" in line or "=" in line


def has_process_wide(prev_attrs: list[str]) -> bool:
    return any(PROCESS_WIDE_RE.search(x) for x in prev_attrs)


def declared_namespace(text: str) -> str | None:
    m = NS_RE.search(text)
    return m.group(1) if m else None


def matches_prefix(ns: str | None, prefixes: tuple[str, ...]) -> bool:
    if not ns:
        return False
    return any(ns == p or ns.startswith(p + ".") for p in prefixes)


def iter_cs() -> list[Path]:
    rows = []
    for path in SRC.rglob("*.cs"):
        parts = {p.lower() for p in path.parts}
        if "bin" in parts or "obj" in parts:
            continue
        rows.append(path)
    return sorted(rows)


def scan() -> tuple[list[str], list[str], list[str]]:
    shell: list[str] = []
    getpoint: list[str] = []
    statics: list[str] = []
    for path in iter_cs():
        raw = path.read_text(encoding="utf-8")
        ns = declared_namespace(raw)
        if not matches_prefix(ns, FEATURE_PREFIXES):
            continue
        rel = path.relative_to(REPO).as_posix()
        text = strip_comments(raw)
        if SHELL_RE.search(text):
            shell.append(rel)
        if GETPOINT_RE.search(text):
            getpoint.append(rel)

        prev: list[str] = []
        for line in text.splitlines():
            stripped = line.strip()
            if not stripped:
                prev.clear()
                continue
            if stripped.startswith("["):
                prev.append(stripped)
                continue
            if STATIC_FIELD_RE.match(line) and is_field(line) and not has_process_wide(prev):
                statics.append(f"{rel}::{stripped}")
            prev.clear()
    return shell, getpoint, statics


def write_list(name: str, rows: list[str]) -> None:
    (OUT / name).write_text("\n".join(rows) + ("\n" if rows else ""), encoding="utf-8")


def main() -> int:
    shell, getpoint, statics = scan()
    write_list("whitelist-features-shell.txt", shell)
    write_list("whitelist-editor-getpoint.txt", getpoint)
    write_list("whitelist-mutable-static.txt", statics)
    print(f"shell={len(shell)} getpoint={len(getpoint)} static={len(statics)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
