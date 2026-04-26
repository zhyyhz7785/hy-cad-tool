from pathlib import Path
root = Path(__file__).resolve().parent / "Features/Road"
fixes = [
    ("Alignment/Views", "HyCADTool.Features.Road.PlanAlignment.Views",
     "HyCADTool.Features.Road.PlanAlignment.ViewModels"),
    ("CrossSection/Views", "HyCADTool.Features.Road.CrossSection.Views",
     "HyCADTool.Features.Road.CrossSection.ViewModels"),
    ("Profile/Views", "HyCADTool.Features.Road.Profile.Views",
     "HyCADTool.Features.Road.Profile.ViewModels"),
]
for sub, view_ns, vm_ns in fixes:
    d = root / sub
    if not d.is_dir():
        continue
    for f in d.glob("*.xaml"):
        t = f.read_text(encoding="utf-8")
        o = t
        t = t.replace('x:Class="HyCADTool.Presentation.Views.Road.', f'x:Class="{view_ns}.')
        t = t.replace("clr-namespace:HyCADTool.Presentation.ViewModels.Road", f"clr-namespace:{vm_ns}")
        if t != o:
            f.write_text(t, encoding="utf-8")
            print("updated", f)
print("done")
