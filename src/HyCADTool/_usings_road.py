# Add missing Shared.AutoCAD.Services.Road to Alignment + PlanProfile service files
from pathlib import Path
ROOT = Path(__file__).resolve().parent
need = "using HyCADTool.Shared.AutoCAD.Services.Road;"
for sub in [
    "Features/Road/Alignment/Services",
    "Features/Road/PlanProfile/Services",  # folder still Profile
]:
    d = ROOT / sub
    if not d.is_dir():
        d = ROOT / sub.replace("PlanProfile", "Profile")
    if not d.is_dir():
        print("skip", d)
        continue
    for f in d.glob("*.cs"):
        t = f.read_text(encoding="utf-8")
        if need in t:
            continue
        if "RoadDesignRegistry" in t or "RoadJsonExportService" in t or "Alignment" in f.name:
            # insert after last using
            i = t.rfind("using ", 0, t.find("namespace "))
            if i < 0:
                continue
            j = t.find(";", i) + 1
            t = t[:j] + "\n" + need + t[j:]
            f.write_text(t, encoding="utf-8")
            print("svc", f.name)

# CrossSection ViewModels: Domain for presets + PlanAlignment.Domain for code check types
cvm = ROOT / "Features/Road/CrossSection/ViewModels"
dom = "using HyCADTool.Features.Road.CrossSection.Domain;\n"
pad = "using HyCADTool.Features.Road.PlanAlignment.Domain;\n"
if cvm.is_dir():
    for f in cvm.glob("*.cs"):
        t = f.read_text(encoding="utf-8")
        o = t
        if "CrossSectionPresets" in t or "PresetDescriptor" in t or "CrossSectionPresetService" in t:
            if dom not in t:
                t = t.replace("namespace ", dom + "namespace ", 1)
        if "CodeCheck" in t and pad not in t:
            t = t.replace("namespace ", pad + "namespace ", 1)
        if t != o:
            f.write_text(t, encoding="utf-8")
            print("vm", f.name)

# RoadPlanCommand
rpc = ROOT / "Features/Road/RoadPlanCommand.cs"
if rpc.is_file():
    t = rpc.read_text(encoding="utf-8")
    u = "using HyCADTool.Features.Road.CrossSection.Services;\n"
    if u not in t and "CrossSectionDrawMode" in t:
        t = t.replace("namespace ", u + "namespace ", 1)
        rpc.write_text(t, encoding="utf-8")
        print("road plan")

# CrossSection xaml cs preset service
for fn in ["CrossSectionDrawPanel.xaml.cs", "CrossSectionDrawWindow.xaml.cs"]:
    f = ROOT / "Features/Road/CrossSection/Views" / fn
    if f.is_file():
        t = f.read_text(encoding="utf-8")
        u = "using HyCADTool.Features.Road.CrossSection.Domain;\n"
        if u not in t and "CrossSectionPresetService" in t:
            t = t.replace("namespace ", u + "namespace ", 1)
            f.write_text(t, encoding="utf-8")
            print("view", fn)

# StationLabel - PlanAlignment.Services
sl = ROOT / "Features/Road/Alignment/ViewModels/StationLabelConfigViewModel.cs"
if sl.is_file():
    t = sl.read_text(encoding="utf-8")
    u = "using HyCADTool.Features.Road.PlanAlignment.Services;\n"
    if u not in t:
        t = t.replace("namespace ", u + "namespace ", 1)
        sl.write_text(t, encoding="utf-8")
        print("station")

# Workbench VM - preview service
wb = ROOT / "Features/Road/Alignment/ViewModels/RoadAlignmentWorkbenchViewModel.cs"
if wb.is_file():
    t = wb.read_text(encoding="utf-8")
    u = "using HyCADTool.Features.Road.PlanAlignment.Services;\n"
    if u not in t and "RoadAlignmentPreviewService" in t:
        t = t.replace("namespace ", u + "namespace ", 1)
        wb.write_text(t, encoding="utf-8")
        print("wb")

# Profile domain + vm - use PlanProfile types from ProfileCodeChecker
for rel in [
    "Features/Road/Profile/Domain/ProfileCodeChecker.cs",
    "Features/Road/Profile/Services/ProfileLabelDrawService.cs",
    "Features/Road/Profile/Services/RoadProfileService.cs",
    "Features/Road/Profile/ViewModels/ProfileEditorViewModel.cs",
]:
    f = ROOT / rel
    if not f.is_file():
        continue
    t = f.read_text(encoding="utf-8")
    o = t
    t2 = "using HyCADTool.Features.Road.PlanAlignment.Domain;\n"
    if "CodeCheck" in t and t2 not in t:
        t = t.replace("namespace ", t2 + "namespace ", 1)
    # Fg/Check types from own domain
    t3 = "using HyCADTool.Features.Road.PlanProfile.Domain;\n"
    if ("ProfileFg" in t or "ProfileCheck" in t) and t3 not in t:
        t = t.replace("namespace ", t3 + "namespace ", 1)
    if t != o:
        f.write_text(t, encoding="utf-8")
        print("prof", f.name)
print("done")
