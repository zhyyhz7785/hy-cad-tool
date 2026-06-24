#!/usr/bin/env python3
"""Quick G101 atlas stats from hygeom slice (DBText-focused)."""
import json
import re
import sys
from collections import Counter
from pathlib import Path


def main() -> int:
    base = Path(r"e:\00HYCADTOOL\16G101-1图集CAD版.dwg.hyob\exports")
    dbtext_path = base / "682833f4fe.DBText.hygeom.json"
    dim_path = base / "682833f4fe.Dimension.hygeom.json"
    block_path = base / "682833f4fe.BlockReference.hygeom.json"

    with dbtext_path.open(encoding="utf-8") as f:
        dbtext = json.load(f)
    with dim_path.open(encoding="utf-8") as f:
        dims = json.load(f)
    with block_path.open(encoding="utf-8") as f:
        blocks = json.load(f)

    texts = []
    for e in dbtext["entities"]:
        t = (e.get("fields") or {}).get("text") or ""
        if t.strip():
            texts.append(t)

    gbz = [t for t in texts if re.search(r"GBZ\d", t, re.I)]
    spacing = [t for t in texts if "%%130" in t and "@" in t]
    grade = [t for t in texts if "%%132" in t]

    layer_counter = Counter(
        (e.get("fields") or {}).get("layer", "?") for e in dbtext["entities"]
    )
    block_counter = Counter(
        (e.get("fields") or {}).get("block_name", "?") for e in blocks["entities"]
    )

    print("=== 16G101 图集 hygeom 可读性抽样 ===")
    print(f"DBText 非空: {len(texts)} / {len(dbtext['entities'])}")
    print(f"Dimension: {len(dims['entities'])}")
    print(f"BlockReference: {len(blocks['entities'])}")
    print(f"图层(配筋相关): {[n for n in layer_counter if '配筋' in n or '钢筋' in n][:12]}")
    print(f"\nGBZ 边缘构件标注: {len(gbz)} 条")
    for s in sorted(set(gbz))[:15]:
        print(f"  - {s}")
    print(f"\n间距标注(含%%130@): {len(spacing)} 条")
    for s in sorted(set(spacing))[:12]:
        print(f"  - {s}")
    print(f"\n含钢筋等级(%%132): {len(grade)} 条")
    print(f"\nTop BlockReference 块名:")
    for name, cnt in block_counter.most_common(12):
        print(f"  {name}: {cnt}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
