# One-off: align restored Road files with post-Stage5 namespaces
from pathlib import Path
ROOT = Path(__file__).resolve().parent

pairs = [
  ("using HyCADTool.Domain.ValueObjects.Configuration.Modules;", "using HyCADTool.Shell.Configuration.Modules;"),
  ("using HyCADTool.Domain.ValueObjects.Configuration.User;", "using HyCADTool.Shell.Configuration.User;"),
  ("using HyCADTool.Domain.ValueObjects.Configuration.Global;", "using HyCADTool.Shell.Configuration.Global;"),
  ("HyCADTool.Domain.ValueObjects.Configuration.User.", "HyCADTool.Shell.Configuration.User."),
  ("HyCADTool.Domain.ValueObjects.Configuration.Global.", "HyCADTool.Shell.Configuration.Global."),
  ("using HyCADTool.Domain.Models.Drawing;", "using HyCADTool.Shared.Drawing.Models;"),
  ("using HyCADTool.Domain.ValueObjects.Drawing;", "using HyCADTool.Shared.Drawing.ValueObjects;"),
  ("HyCADTool.Domain.Models.Drawing.", "HyCADTool.Shared.Drawing.Models."),
  ("HyCADTool.Domain.ValueObjects.Drawing.", "HyCADTool.Shared.Drawing.ValueObjects."),
  ("using HyCADTool.Shared.AutoCAD.Services.Road.FillRendering;", "using HyCADTool.Features.Road.CrossSection.Services.FillRendering;"),
  ("HyCADTool.Shared.AutoCAD.Services.Road.FillRendering.", "HyCADTool.Features.Road.CrossSection.Services.FillRendering."),
  ("using HyCADTool.Presentation.Factories;", "using HyCADTool.Features.Road.CrossSection.Services;"),
  ("using HyCADTool.Shared.AutoCAD.Workflows.Road;", "using HyCADTool.Features.Road.CrossSection.Services.Workflows;"),
  ("HyCADTool.Shared.AutoCAD.Workflows.Road.", "HyCADTool.Features.Road.CrossSection.Services.Workflows."),
]

svc_pairs = [
  ("HyCADTool.Shared.AutoCAD.Services.Road.RoadAlignmentService", "HyCADTool.Features.Road.Alignment.Services.RoadAlignmentService"),
  ("HyCADTool.Shared.AutoCAD.Services.Road.RoadAlignmentApplyService", "HyCADTool.Features.Road.Alignment.Services.RoadAlignmentApplyService"),
  ("HyCADTool.Shared.AutoCAD.Services.Road.RoadAlignmentCommitService", "HyCADTool.Features.Road.Alignment.Services.RoadAlignmentCommitService"),
  ("HyCADTool.Shared.AutoCAD.Services.Road.RoadAlignmentDeleteService", "HyCADTool.Features.Road.Alignment.Services.RoadAlignmentDeleteService"),
  ("HyCADTool.Shared.AutoCAD.Services.Road.RoadAlignmentUserPickRegisterService", "HyCADTool.Features.Road.Alignment.Services.RoadAlignmentUserPickRegisterService"),
  ("HyCADTool.Shared.AutoCAD.Services.Road.RoadAlignmentUserPickPreviewService", "HyCADTool.Features.Road.Alignment.Services.RoadAlignmentUserPickPreviewService"),
  ("HyCADTool.Shared.AutoCAD.Services.Road.RoadProfileService", "HyCADTool.Features.Road.Profile.Services.RoadProfileService"),
  ("HyCADTool.Shared.AutoCAD.Services.Road.ProfileLabelDrawService", "HyCADTool.Features.Road.Profile.Services.ProfileLabelDrawService"),
  ("HyCADTool.Shared.AutoCAD.Services.Road.CrossSectionCommitDrawService", "HyCADTool.Features.Road.CrossSection.Services.CrossSectionCommitDrawService"),
  ("HyCADTool.Shared.AutoCAD.Services.Road.RoadStandardSectionDrawService", "HyCADTool.Features.Road.CrossSection.Services.RoadStandardSectionDrawService"),
]

vm_pairs = [
  ("HyCADTool.Presentation.ViewModels.Road.RoadAlignmentWorkbenchViewModel", "HyCADTool.Features.Road.Alignment.ViewModels.RoadAlignmentWorkbenchViewModel"),
  ("HyCADTool.Presentation.ViewModels.Road.CrossSectionDrawViewModel", "HyCADTool.Features.Road.CrossSection.ViewModels.CrossSectionDrawViewModel"),
]

n = 0
for fp in ROOT.rglob("*.cs"):
    if "_fix_ns.py" in str(fp):
        continue
    try:
        t = fp.read_text(encoding="utf-8")
    except UnicodeDecodeError:
        t = fp.read_text(encoding="utf-8", errors="replace")
    o = t
    for a, b in pairs + svc_pairs + vm_pairs:
        t = t.replace(a, b)
    if t != o:
        fp.write_text(t, encoding="utf-8")
        n += 1
print("Updated", n, "files")
