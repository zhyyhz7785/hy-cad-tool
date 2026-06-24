#!/usr/bin/env python3
"""Filter hygeom.json entities by type (mirrors hyobES ExportFilter type:XXX)."""
import json
import sys
from pathlib import Path


def filter_hygeom(src: Path, type_name: str, dst: Path) -> int:
    with src.open("r", encoding="utf-8") as f:
        doc = json.load(f)

    entities = doc.get("entities") or []
    filtered = [e for e in entities if e.get("type") == type_name]

    out = dict(doc)
    out["filter"] = {"type": type_name}
    out["entities"] = filtered

    dst.parent.mkdir(parents=True, exist_ok=True)
    with dst.open("w", encoding="utf-8", newline="\n") as f:
        json.dump(out, f, ensure_ascii=False, indent=2)
        f.write("\n")

    return len(filtered)


def main(argv: list[str]) -> int:
    if len(argv) < 2:
        print("Usage: hyob-filter-hygeom.py <src.hygeom.json> [Type1 Type2 ...]")
        return 1

    src = Path(argv[1])
    if not src.is_file():
        print(f"Not found: {src}")
        return 1

    with src.open("r", encoding="utf-8") as f:
        short = json.load(f)["commit"]["short"][:10]

    types = argv[2:] or ["DBText", "Dimension", "BlockReference"]
    for t in types:
        dst = src.parent / f"{short}.{t}.hygeom.json"
        n = filter_hygeom(src, t, dst)
        print(f"{dst.name}: {n} entities")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
