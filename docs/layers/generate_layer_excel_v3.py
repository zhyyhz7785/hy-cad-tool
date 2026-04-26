# -*- coding: utf-8 -*-
"""生成 HyCAD 图层-v3.xlsx。

在 v2 基础上：
- 结构子组重排：1配筋…9板；筏板/设备基础并入 4基础；聚类独立为 03 大类
- 直线层增加 -虚 孪生（结构 11 + 道路横断 4），线型名「虚线」（HyCAD-Linetypes.lin）
- 轴线/中心线/聚类轴：线型「点划线」
- 主清单增加列「线型定义来源」；增加 sheet「线型定义」

运行：python generate_layer_excel_v3.py
"""
from __future__ import annotations

from pathlib import Path

from openpyxl import Workbook, load_workbook
from openpyxl.styles import Alignment, Font, PatternFill
from openpyxl.utils import get_column_letter

ROOT = Path(__file__).resolve().parent
V11_XLSX = ROOT / "HyCAD图层-v1.1.xlsx"
LEGACY_XLSX = ROOT / "老旧设置.xlsx"
OUT_XLSX = ROOT / "HyCAD图层-v3.xlsx"

AUTO = "AutoCAD 内置"
HY = "HyCAD 自定义"

MAIN_HEADERS = [
    "大类号",
    "大类名",
    "子组",
    "新层名",
    "v3状态",
    "语义ID",
    "ACI色",
    "线型",
    "线型定义来源",
    "线宽mm",
    "锁定",
    "打印",
    "归属",
    "合并目标/备注",
    "XData说明",
    "是否模板",
]

# (大类号, 子组, 新层名, v3状态, 语义ID, ACI色, 线型, 线型来源, 线宽, 锁, 打, 归属, 备注, XData, 模板)
MAIN_ROWS: list[tuple] = []

def R(
    cat: str,
    sub: str,
    name: str,
    st: str,
    sem,
    aci,
    lt: str,
    src: str,
    lw,
    lock: str,
    plot: str,
    own: str,
    note: str,
    xdata: str,
    tpl: str,
):
    MAIN_ROWS.append((cat, sub, name, st, sem, aci, lt, src, lw, lock, plot, own, note, xdata, tpl))


CATEGORY_NAME = {"00": "公共", "01": "结构", "02": "道路", "03": "聚类"}

# ============ 00 公共 ============
R("00", "1图框", "00-hy-1图框-主", "保留", "Public.TitleBlock.Main", 7, "Continuous", AUTO, 0.50, "—", "Y", "DesignSpecService.TitleBlockLayer", "", "", "否")
R("00", "1图框", "00-hy-1图框-说明-文", "保留", "Public.TitleBlock.SheetText", 7, "Continuous", AUTO, -1, "—", "Y", "DesignSpecService.TitleTextLayer", "", "", "否")
R("00", "1图框", "00-hy-1图框-图签-文", "保留", "Public.TitleBlock.SealText", 131, "Continuous", AUTO, -1, "—", "Y", "DesignSpecService（图签文字）", "", "", "否")
R("00", "2视口", "00-hy-2视口-主", "保留", "Public.Viewport.Main", 8, "Continuous", AUTO, -1, "—", "N", "MBRCommand / PackViewportsCommand", "", "", "否")
R("00", "3标注", "00-hy-3标注-外", "保留", "Public.Dimension.Outside", 3, "Continuous", AUTO, -1, "—", "Y", "RoadCsDrawStyleFactory / 通用标注", "", "", "否")
R("00", "3标注", "00-hy-3标注-内横", "保留", "Public.Dimension.InsideHorizontal", 1, "Continuous", AUTO, -1, "—", "Y", "基础配筋横向尺寸", "", "", "否")
R("00", "3标注", "00-hy-3标注-内纵", "保留", "Public.Dimension.InsideVertical", 2, "Continuous", AUTO, -1, "—", "Y", "基础配筋纵向尺寸", "", "", "否")
R("00", "3标注", "00-hy-3标注-引线", "保留", "Public.Dimension.Leader", 3, "Continuous", AUTO, -1, "—", "Y", "RoadCsDrawStyleFactory.LeaderLayer", "", "", "否")
R("00", "4说明", "00-hy-4说明-一般-文", "保留", "Public.Note.General", 7, "Continuous", AUTO, -1, "—", "Y", "通用文字 / 注释 / 标注文字", "", "HY_NOTE_GENERAL.Kind", "否")
R("00", "4说明", "00-hy-4说明-图表-文", "保留", "Public.Note.Table", 131, "Continuous", AUTO, -1, "—", "Y", "图签/表格文字", "", "", "否")
R("00", "5表格", "00-hy-5表格-主", "保留", "Public.Table.Main", 7, "Continuous", AUTO, -1, "—", "Y", "EquipmentFoundationService / 通用表格", "", "", "否")
R("00", "6轴线", "00-hy-6轴线-主", "v3改动", "Public.Axis.Main", 1, "点划线", HY, -1, "—", "Y", "EquipmentFoundationService.AxisLayer", "v3：CENTER→点划线", "", "否")
R("00", "6轴线", "00-hy-6轴线-主-文", "保留", "Public.Axis.Text", 1, "Continuous", AUTO, -1, "—", "Y", "EquipmentFoundationService.AxisTextLayer", "", "", "否")
R("00", "7标高", "00-hy-7标高-符号", "保留", "Public.Elevation.Symbol", 3, "Continuous", AUTO, -1, "—", "Y", "ElevationSymbolJig.SymbolLayer", "", "", "否")
R("00", "7标高", "00-hy-7标高-无文", "保留", "Public.Elevation.NoText", 200, "Continuous", AUTO, -1, "—", "N", "GroupCirclesByElevationCommand.NoTextLayer", "", "", "否")
R("00", "7标高", "00-hy-7标高-警告", "建议保留", "Public.Elevation.Warning", 1, "Continuous", AUTO, -1, "—", "N", "GroupCirclesByElevationCommand.WarningLayer", "v1.1 xlsx 曾删除—备忘行", "", "否")
R("00", "7标高", "00-hy-7标高-{分组}", "建议保留", "Public.Elevation.GroupTemplate", "动态", "Continuous", AUTO, -1, "—", "Y", "GroupCirclesByElevationCommand.GroupLayerFormat", "备忘", "", "是")
R("00", "8图像", "00-hy-8图像-定位", "保留", "Public.Image.Locator", 8, "Continuous", AUTO, -1, "L", "N", "ImageLocatorService", "", "", "否")
R("00", "9标记", "00-hy-9标记-打断点", "保留", "Public.Marker.Break", 1, "Continuous", AUTO, -1, "—", "N", "MarkerLayerService.BreakLayer", "", "", "否")
R("00", "9标记", "00-hy-9标记-重复线", "保留", "Public.Marker.Duplicate", 2, "Continuous", AUTO, -1, "—", "N", "MarkerLayerService.DuplicateLayer", "", "", "否")
R("00", "9标记", "00-hy-9标记-外轮廓", "保留", "Public.Marker.Outer", 3, "Continuous", AUTO, -1, "—", "N", "MarkerLayerService.OuterLayer", "", "", "否")
R("00", "9标记", "00-hy-9标记-内孔洞", "保留", "Public.Marker.Inner", 5, "Continuous", AUTO, -1, "—", "N", "MarkerLayerService.InnerLayer", "", "", "否")
R("00", "9标记", "00-hy-9标记-标高检查", "保留", "Public.Marker.ElevationCheck", 6, "Continuous", AUTO, -1, "—", "N", "MarkerLayerService.ElevationCheckLayer", "", "", "否")
R("00", "9标记", "00-hy-9标记-独立端点", "保留", "Public.Marker.IsolatedEnd", 4, "Continuous", AUTO, -1, "—", "N", "MarkerLayerService.IsolatedEndLayer", "", "", "否")
R("00", "9标记", "00-hy-9标记-未连接线", "保留", "Public.Marker.Unconnected", 200, "Continuous", AUTO, -1, "—", "N", "MarkerLayerService.DcelUnconnectedLayer", "", "", "否")

# ============ 01 结构 ============
# 1 配筋（筏板轮廓已迁至 4基础）
R("01", "1配筋", "01-hy-1配筋-钢筋线", "保留", "Structure.Rebar.Line", 1, "Continuous", AUTO, -1, "—", "Y", "BaseReinforcementService · 线钢筋（含筏板附加，线型+颜色区分）", "", "HY_REBAR_LINE.Kind", "否")
R("01", "1配筋", "01-hy-1配筋-钢筋点", "保留", "Structure.Rebar.Dot", 2, "Continuous", AUTO, -1, "—", "Y", "BaseReinforcementService · 点式钢筋", "", "", "否")
R("01", "1配筋", "01-hy-1配筋-钢筋外", "保留", "Structure.Rebar.External", 3, "Continuous", AUTO, -1, "—", "Y", "BaseReinforcementService · 外部线钢筋", "", "", "否")
R("01", "1配筋", "01-hy-1配筋-手动", "保留", "Structure.Rebar.Manual", 6, "Continuous", AUTO, -1, "—", "Y", "ManualRebarCommand", "", "", "否")
R("01", "1配筋", "01-hy-1配筋-钢筋-文", "用户合并", "（合并去向）", "—", "—", "—", "—", "—", "—", "→ 00-hy-4说明-一般-文", "", "", "—")
R("01", "1配筋", "01-hy-1配筋-筏板横上", "用户合并", "（合并去向）", "—", "—", "—", "—", "—", "—", "→ 01-hy-1配筋-钢筋线", "", "", "—")
R("01", "1配筋", "01-hy-1配筋-筏板横下", "用户合并", "（合并去向）", "—", "—", "—", "—", "—", "—", "→ 01-hy-1配筋-钢筋线", "", "", "—")
R("01", "1配筋", "01-hy-1配筋-筏板纵上", "用户合并", "（合并去向）", "—", "—", "—", "—", "—", "—", "→ 01-hy-1配筋-钢筋线", "", "", "—")
R("01", "1配筋", "01-hy-1配筋-筏板纵下", "用户合并", "（合并去向）", "—", "—", "—", "—", "—", "—", "→ 01-hy-1配筋-钢筋线", "", "", "—")
R("01", "1配筋", "01-hy-1配筋-筏板横-标", "用户合并", "（合并去向）", "—", "—", "—", "—", "—", "—", "→ 00-hy-4说明-一般-文", "", "", "—")
R("01", "1配筋", "01-hy-1配筋-筏板纵-标", "用户合并", "（合并去向）", "—", "—", "—", "—", "—", "—", "→ 00-hy-4说明-一般-文", "", "", "—")
R("01", "1配筋", "01-hy-1配筋-筏板厚度-文", "用户合并", "（合并去向）", "—", "—", "—", "—", "—", "—", "→ 00-hy-4说明-一般-文", "", "", "—")

R("01", "2桩", "01-hy-2桩-主", "保留", "Structure.Pile.Main", 2, "Continuous", AUTO, -1, "—", "Y", "PileDrawingService.PileLayer", "", "", "否")
R("01", "2桩", "01-hy-2桩-地基轮廓", "保留", "Structure.Pile.GroundOutline", 7, "Continuous", AUTO, -1, "—", "Y", "PileDrawingService.GroundOutlineLayer", "", "", "否")
R("01", "2桩", "01-hy-2桩-阵列", "保留", "Structure.Pile.Array", 2, "Continuous", AUTO, -1, "—", "Y", "PileVoronoiOptimizationCommand.PileLayer", "", "", "否")
R("01", "2桩", "01-hy-2桩-维诺-辅", "保留", "Structure.Pile.Voronoi", 5, "Continuous", AUTO, -1, "—", "N", "PileVoronoiOptimizationCommand.VoronoiLayer", "", "", "否")

R("01", "3锚栓", "01-hy-3锚栓-主", "保留", "Structure.AnchorBolt.Main", 1, "Continuous", AUTO, -1, "—", "Y", "AnchorBoltCommand · 主层", "", "", "否")
R("01", "3锚栓", "01-hy-3锚栓-轮廓", "保留", "Structure.AnchorBolt.Outline", 3, "Continuous", AUTO, -1, "—", "Y", "AnchorBoltCommand · 轮廓", "", "", "否")
R("01", "3锚栓", "01-hy-3锚栓-编号", "保留", "Structure.AnchorBolt.Number", 2, "Continuous", AUTO, -1, "—", "Y", "AnchorBoltCommand · 编号", "", "", "否")
R("01", "3锚栓", "01-hy-3锚栓-预埋", "保留", "Structure.AnchorBolt.Embedded", 6, "Continuous", AUTO, -1, "—", "Y", "AnchorBoltCommand · 预埋板", "", "", "否")
R("01", "3锚栓", "01-hy-3锚栓-{型号}", "保留", "Structure.AnchorBolt.ModelTemplate", "动态", "Continuous", AUTO, -1, "—", "Y", "AnchorBoltCommand.ModelLayerFormat", "", "", "是")

# 4 基础（筏板 + 设备实体 + 扩展基础类型 + -虚 对配）
R("01", "4基础", "01-hy-4基础-筏板-轮廓", "v3改动", "Structure.Raft.Outline", 3, "Continuous", AUTO, -1, "—", "Y", "BaseReinforcementService.OutlineLayer", "自 1配筋 迁入", "", "否")
R("01", "4基础", "01-hy-4基础-筏板-轮廓-虚", "v3新增", "Structure.Raft.OutlineDashed", 3, "虚线", HY, -1, "—", "Y", "对应老旧 025-S-FUDN-RAFT-DASH", "", "", "否")
R("01", "4基础", "01-hy-4基础-筏板-辅", "保留", "Structure.Raft.OutlineAdjust", 1, "Continuous", AUTO, -1, "—", "N", "BaseReinforcementService.AdjustOutlineLayer", "", "", "否")
R("01", "4基础", "01-hy-4基础-筏板-体", "保留", "Structure.Raft.Solid", 8, "Continuous", AUTO, -1, "—", "N", "Elevation3DCommand.RaftSolidLayer", "", "", "否")
R("01", "4基础", "01-hy-4基础-设备-侧-体", "保留", "Structure.EquipFoundation.SideSolid", 1, "Continuous", AUTO, -1, "—", "N", "SlabGenerationService · 侧面体", "原 4设备基础", "", "否")
R("01", "4基础", "01-hy-4基础-设备-顶-体", "保留", "Structure.EquipFoundation.TopSolid", 3, "Continuous", AUTO, -1, "—", "N", "SlabGenerationService · 顶面体", "", "", "否")
R("01", "4基础", "01-hy-4基础-设备-底-体", "保留", "Structure.EquipFoundation.BottomSolid", 5, "Continuous", AUTO, -1, "—", "N", "SlabGenerationService · 底面体", "", "", "否")
R("01", "4基础", "01-hy-4基础-独立-主", "v3新增", "Structure.Foundation.Independent", 7, "Continuous", AUTO, -1, "—", "Y", "025-S-FUDN-INDEPENDENT", "", "", "否")
R("01", "4基础", "01-hy-4基础-独立-虚", "v3新增", "Structure.Foundation.IndependentDashed", 7, "虚线", HY, -1, "—", "Y", "025-S-FUDN-INDEPENDENT-DASH", "", "", "否")
R("01", "4基础", "01-hy-4基础-条形-主", "v3新增", "Structure.Foundation.Strip", 5, "Continuous", AUTO, -1, "—", "Y", "025-S-FUDN-DADOS", "", "", "否")
R("01", "4基础", "01-hy-4基础-条形-虚", "v3新增", "Structure.Foundation.StripDashed", 5, "虚线", HY, -1, "—", "Y", "025-S-FUDN-DADOS-DASH", "", "", "否")
R("01", "4基础", "01-hy-4基础-电梯-主", "v3新增", "Structure.Foundation.ElevatorPit", 8, "Continuous", AUTO, -1, "—", "Y", "025-S-FUDN-ELEVATOR", "", "", "否")
R("01", "4基础", "01-hy-4基础-电梯-虚", "v3新增", "Structure.Foundation.ElevatorPitDashed", 8, "虚线", HY, -1, "—", "Y", "025-S-FUDN-ELEVATOR-DASH", "", "", "否")
R("01", "4基础", "01-hy-4基础-集水-主", "v3新增", "Structure.Foundation.Sump", 6, "Continuous", AUTO, -1, "—", "Y", "025-S-FUDN-SUMP", "", "", "否")
R("01", "4基础", "01-hy-4基础-集水-虚", "v3新增", "Structure.Foundation.SumpDashed", 6, "虚线", HY, -1, "—", "Y", "025-S-FUDN-SUMP-DASH", "", "", "否")
R("01", "4基础", "01-hy-4基础-基梁-主", "v3新增", "Structure.Foundation.GradeBeam", 4, "Continuous", AUTO, -1, "—", "Y", "025-S-FUDN-UNDERCOUISE", "", "", "否")
R("01", "4基础", "01-hy-4基础-基梁-虚", "v3新增", "Structure.Foundation.GradeBeamDashed", 4, "虚线", HY, -1, "—", "Y", "025-S-FUDN-UNDERCOUISE-DASH", "", "", "否")

R("01", "5垫层", "01-hy-5垫层-主", "保留", "Structure.Cushion.Main", 8, "Continuous", AUTO, -1, "—", "Y", "CushionService", "", "", "否")

R("01", "6墙", "01-hy-6墙-砼墙", "保留", "Structure.Wall.Concrete", 2, "Continuous", AUTO, -1, "—", "Y", "BaseReinforcementService · 砼墙", "", "", "否")
R("01", "6墙", "01-hy-6墙-砼墙-虚", "v3改动", "Structure.Wall.ConcreteDashed", 2, "虚线", HY, -1, "—", "Y", "027-S-WALL-DASH", "v2 为 DASHED，v3 统一为「虚线」", "", "否")
R("01", "6墙", "01-hy-6墙-主-体", "保留", "Structure.Wall.Solid", 8, "Continuous", AUTO, -1, "—", "N", "Elevation3DCommand.WallSolidLayer", "", "", "否")
R("01", "6墙", "01-hy-6墙-挡土-体", "保留", "Structure.Wall.RetainSolid", 5, "Continuous", AUTO, -1, "—", "N", "挡土墙体", "", "HY_RETAIN_WALL.Kind", "否")
R("01", "6墙", "01-hy-6墙-连接-体", "保留", "Structure.Wall.ConnectSolid", 4, "Continuous", AUTO, -1, "—", "N", "WallGenerationService · 连接墙", "", "", "否")

R("01", "7柱", "01-hy-7柱-主", "保留", "Structure.Column.Main", 3, "Continuous", AUTO, -1, "—", "Y", "BaseReinforcementService · 柱", "", "", "否")
R("01", "7柱", "01-hy-7柱-虚", "v3改动", "Structure.Column.Dashed", 3, "虚线", HY, -1, "—", "Y", "026-S-COLUMN-DASH", "", "", "否")

R("01", "8梁", "01-hy-8梁-主", "v3新增", "Structure.Beam.Main", 3, "Continuous", AUTO, -1, "—", "Y", "028-S-BEAM", "", "", "否")
R("01", "8梁", "01-hy-8梁-虚", "v3新增", "Structure.Beam.Dashed", 3, "虚线", HY, -1, "—", "Y", "028-S-BEAM-DASH", "", "", "否")

R("01", "9板", "01-hy-9板-主", "保留", "Structure.Slab.Main", 4, "Continuous", AUTO, -1, "—", "Y", "BaseReinforcementService · 板元", "", "", "否")
R("01", "9板", "01-hy-9板-虚", "v3改动", "Structure.Slab.Dashed", 4, "虚线", HY, -1, "—", "Y", "029-S-SLAB-DASH", "", "", "否")
R("01", "9板", "01-hy-9板-配筋-标", "建议合并", "—", "—", "—", "—", "—", "—", "—", "建议 → 00-hy-4说明-一般-文", "", "", "—")
R("01", "9板", "01-hy-9板-外环-辅", "保留", "Structure.Slab.OuterRing", 6, "Continuous", AUTO, -1, "—", "N", "Elevation3DCommand.DcelOuterLayer", "", "", "否")
R("01", "9板", "01-hy-9板-内环-辅", "保留", "Structure.Slab.InnerRing", 5, "Continuous", AUTO, -1, "—", "N", "Elevation3DCommand.DcelInnerLayer", "", "", "否")

# ============ 03 聚类（自 01 迁出）===========
R("03", "1主", "03-hy-1主", "v3改动", "Structure.Cluster.Main", 3, "Continuous", AUTO, -1, "—", "Y", "ClusterDrawService", "", "HY_CLUSTER_MAIN.Kind", "否")
R("03", "2轴", "03-hy-2轴", "v3改动", "Structure.Cluster.Axis", 7, "点划线", HY, -1, "—", "Y", "ClusterDrawService", "", "HY_CLUSTER_AXIS.Kind", "否")
R("03", "3区域", "03-hy-3区域", "v3改动", "Structure.Cluster.Region", 9, "Continuous", AUTO, -1, "—", "Y", "ClusterDrawService", "", "HY_CLUSTER_REGION.Kind", "否")
R("03", "4标", "03-hy-4标", "v3改动", "Structure.Cluster.Dim", 7, "Continuous", AUTO, -1, "—", "Y", "ClusterDrawService", "", "HY_CLUSTER_DIM.Kind", "否")
R("03", "5辅", "03-hy-5辅", "v3改动", "Structure.Cluster.Aux", 8, "Continuous", AUTO, -1, "—", "N", "ClusterDrawService", "", "HY_CLUSTER_AUX.Kind", "否")

# ============ 02 道路 ============
R("02", "1平面", "02-hy-1平面-线位", "保留", "Road.PlaneAlignment", 3, "Continuous", AUTO, -1, "—", "Y", "HyRoadLayers._defAlignment", "", "", "否")
R("02", "1平面", "02-hy-1平面-走廊", "保留", "Road.PlaneCorridor", 2, "Continuous", AUTO, -1, "—", "Y", "HyRoadLayers._defCorridor", "", "", "否")
R("02", "1平面", "02-hy-1平面-红线", "保留", "Road.PlaneRedLine", 1, "Continuous", AUTO, -1, "—", "Y", "HyRoadLayers._defRedLine", "", "", "否")
R("02", "1平面", "02-hy-1平面-板块", "保留", "Road.PlaneTileBoundary", 5, "Continuous", AUTO, -1, "—", "Y", "HyRoadLayers._defTileBoundary", "", "", "否")
R("02", "1平面", "02-hy-1平面-标线", "保留", "Road.PlaneMarking", 4, "Continuous", AUTO, -1, "—", "Y", "HyRoadLayers._defMarking", "", "", "否")
R("02", "2纵断", "02-hy-2纵断-主", "保留", "Road.ProfileMain", 1, "Continuous", AUTO, -1, "—", "Y", "HyRoadLayers._defProfile", "", "", "否")
R("02", "3横断", "02-hy-3横断-轮廓", "保留", "Road.CrossSection.Outline", 7, "Continuous", AUTO, -1, "—", "Y", "CrossSectionLayoutBuilder · 外轮廓", "", "", "否")
R("02", "3横断", "02-hy-3横断-轮廓-虚", "v3新增", "Road.CrossSection.OutlineDashed", 7, "虚线", HY, -1, "—", "Y", "横断外轮廓-虚孪生", "", "", "否")
R("02", "3横断", "02-hy-3横断-中心", "v3改动", "Road.CrossSection.Centerline", 1, "点划线", HY, -1, "—", "Y", "CrossSectionLayoutBuilder · 中心线", "CENTER→点划线", "", "否")
R("02", "3横断", "02-hy-3横断-车道", "保留", "Road.CrossSection.Pavement", 2, "Continuous", AUTO, -1, "—", "Y", "CrossSectionLayoutBuilder · 车行道", "", "", "否")
R("02", "3横断", "02-hy-3横断-车道-虚", "v3新增", "Road.CrossSection.PavementDashed", 2, "虚线", HY, -1, "—", "Y", "车道-虚孪生", "", "", "否")
R("02", "3横断", "02-hy-3横断-人道", "保留", "Road.CrossSection.Sidewalk", 5, "Continuous", AUTO, -1, "—", "Y", "CrossSectionLayoutBuilder · 人行道", "", "", "否")
R("02", "3横断", "02-hy-3横断-人道-虚", "v3新增", "Road.CrossSection.SidewalkDashed", 5, "虚线", HY, -1, "—", "Y", "人道-虚孪生", "", "", "否")
R("02", "3横断", "02-hy-3横断-路牙", "保留", "Road.CrossSection.Kerb", 6, "Continuous", AUTO, -1, "—", "Y", "CrossSectionLayoutBuilder · 路牙", "", "", "否")
R("02", "3横断", "02-hy-3横断-路牙-虚", "v3新增", "Road.CrossSection.KerbDashed", 6, "虚线", HY, -1, "—", "Y", "路牙-虚孪生", "", "", "否")
R("02", "3横断", "02-hy-3横断-绿化", "保留", "Road.CrossSection.Green", 3, "Continuous", AUTO, -1, "—", "Y", "CrossSectionLayoutBuilder · 绿化带", "", "", "否")
R("02", "3横断", "02-hy-3横断-尺寸-标", "保留", "Road.CrossSection.DimChain", 7, "Continuous", AUTO, -1, "—", "Y", "CrossSectionLayoutBuilder · 尺寸链", "", "", "否")
R("02", "3横断", "02-hy-3横断-注释-文", "建议合并", "—", "—", "—", "—", "—", "—", "—", "建议 → 00-hy-4说明-一般-文", "", "HY_ROAD_CS_ANNO.Kind", "—")
R("02", "3横断", "02-hy-3横断-图题", "保留", "Road.CrossSection.Title", 7, "Continuous", AUTO, -1, "—", "Y", "DrawingSheetTitleSpec", "", "HY_ROAD_CS_TITLE.Kind", "否")
R("02", "5标线", "02-hy-5标线-主", "保留", "Road.Marking.Main", 4, "Continuous", AUTO, -1, "—", "Y", "DrawCrosswalkCommand", "", "", "否")
R("02", "5标线", "02-hy-5标线-人道", "保留", "Road.Marking.Crosswalk", 4, "Continuous", AUTO, -1, "—", "Y", "HyRoadLayers._defCrosswalk", "", "", "否")
R("02", "5标线", "02-hy-5标线-停止", "保留", "Road.Marking.StopLine", 1, "Continuous", AUTO, -1, "—", "Y", "DrawCrosswalkCommand.StopLineLayer", "", "", "否")
R("02", "5标线", "02-hy-5标线-辅助-辅", "保留", "Road.Marking.Auxiliary", 8, "Continuous", AUTO, -1, "—", "N", "DrawCrosswalkCommand.AuxiliaryLayer", "", "", "否")
R("02", "6桩号", "02-hy-6桩号-主", "保留", "Road.Station.Main", 7, "Continuous", AUTO, -1, "—", "Y", "HyRoadLayers._defStation", "", "", "否")
R("02", "7几何", "02-hy-7几何-点", "保留", "Road.Geometry.Point", 3, "Continuous", AUTO, -1, "—", "Y", "HyRoadLayers._defPoint", "", "", "否")
R("02", "7几何", "02-hy-7几何-偏移", "保留", "Road.Geometry.Offset", 5, "Continuous", AUTO, -1, "—", "Y", "HyRoadLayers._defOffset", "", "", "否")
R("02", "7几何", "02-hy-7几何-缘石", "保留", "Road.Geometry.CurbRamp", 4, "Continuous", AUTO, -1, "—", "Y", "HyRoadLayers._defCurbRamp", "", "", "否")
R("02", "7几何", "02-hy-7几何-盲道", "保留", "Road.Geometry.Blind", 2, "Continuous", AUTO, -1, "—", "Y", "HyRoadLayers._defBlind", "", "", "否")
R("02", "8交叉口", "02-hy-8交叉口-主", "保留", "Road.Intersection.Main", 6, "Continuous", AUTO, -1, "—", "Y", "HyRoadLayers._defIntersection", "", "", "否")
R("02", "9预览", "02-hy-9预览-原线-预", "保留", "Road.Preview.RawPolyline", 8, "Continuous", AUTO, -1, "L", "N", "HyRoadLayers.RawPolylineLayer", "", "", "否")
R("02", "9预览", "02-hy-9预览-实时-预", "保留", "Road.Preview.Realtime", 252, "Continuous", AUTO, -1, "—", "N", "AlignmentWorkbenchService.PreviewLayer", "", "", "否")
R("02", "9预览", "02-hy-9预览-拾取-预", "保留", "Road.Preview.Pick", 253, "Continuous", AUTO, -1, "—", "N", "通用拾取预览", "", "", "否")

# ---------------------------------------------------------------------------
LEGACY_DELTA_HEADERS = ["老旧层名", "老旧含义", "v3 候选层名", "建议方式", "备注"]
LEGACY_DELTA_ROWS = [
    ("000-C-TITLE", "公共-图框/图框底层", "00-hy-1图框-主", "1:1 映射", ""),
    ("000-C-WINS", "公共-视口", "00-hy-2视口-主", "1:1 映射", ""),
    ("000-C-TITLE-TEXT", "公共-图框文字/图签文字", "00-hy-1图框-图签-文", "1:1 映射", ""),
    ("000-C-MODIFY-WIDE", "公共-修改粗线", "（建议）00-hy-9标记-修改", "建议新增", ""),
    ("000-C-MODIFY-THIN", "公共-修改细线", "（建议合并）", "建议合并", ""),
    ("000-C-GROUND", "公共-地面底图", "00-hy-8图像-定位", "1:1 映射", ""),
    ("021-S-AXIS", "结构-轴线主", "00-hy-6轴线-主（点划线）", "1:1 映射", ""),
    ("025-S-FUDN-RAFT", "结构-基础筏板", "01-hy-4基础-筏板-轮廓", "1:1 映射", "v3 迁入 4基础"),
    ("025-S-FUDN-RAFT-DASH", "结构-基础筏板虚线", "01-hy-4基础-筏板-轮廓-虚", "1:1 映射", "线型「虚线」"),
    ("025-S-FUDN-INDEPENDENT", "结构-独立基础", "01-hy-4基础-独立-主", "1:1 映射", ""),
    ("025-S-FUDN-INDEPENDENT-DASH", "结构-独立基础虚线", "01-hy-4基础-独立-虚", "1:1 映射", ""),
    ("025-S-FUDN-DADOS", "结构-条形基础", "01-hy-4基础-条形-主", "1:1 映射", ""),
    ("025-S-FUDN-DADOS-DASH", "结构-条形基础虚线", "01-hy-4基础-条形-虚", "1:1 映射", ""),
    ("025-S-FUDN-ELEVATOR", "结构-电梯基础", "01-hy-4基础-电梯-主", "1:1 映射", ""),
    ("025-S-FUDN-ELEVATOR-DASH", "结构-电梯基础虚线", "01-hy-4基础-电梯-虚", "1:1 映射", ""),
    ("025-S-FUDN-SUMP", "结构-集水坑", "01-hy-4基础-集水-主", "1:1 映射", ""),
    ("025-S-FUDN-SUMP-DASH", "结构-集水坑虚线", "01-hy-4基础-集水-虚", "1:1 映射", ""),
    ("025-S-FUDN-UNDERCOUISE", "结构-基础梁", "01-hy-4基础-基梁-主", "1:1 映射", ""),
    ("025-S-FUDN-UNDERCOUISE-DASH", "结构-基础梁虚线", "01-hy-4基础-基梁-虚", "1:1 映射", ""),
    ("026-S-COLUMN", "结构-柱", "01-hy-7柱-主", "1:1 映射", ""),
    ("026-S-COLUMN-DASH", "结构-柱虚线", "01-hy-7柱-虚", "1:1 映射", ""),
    ("027-S-WALL", "结构-墙", "01-hy-6墙-砼墙", "1:1 映射", ""),
    ("027-S-WALL-DASH", "结构-墙虚线", "01-hy-6墙-砼墙-虚", "1:1 映射", ""),
    ("028-S-BEAM", "结构-梁", "01-hy-8梁-主", "1:1 映射", ""),
    ("028-S-BEAM-DASH", "结构-梁虚线", "01-hy-8梁-虚", "1:1 映射", ""),
    ("029-S-SLAB", "结构-板", "01-hy-9板-主", "1:1 映射", ""),
    ("029-S-SLAB-DASH", "结构-板虚线", "01-hy-9板-虚", "1:1 映射", ""),
]

XDATA_HEADERS = ["#", "合并组", "旧层数", "新层", "XData注册簇", "KIND枚举"]
XDATA_ROWS = [
    (1, "钢筋线（含筏板附加）", 5, "01-hy-1配筋-钢筋线", "HY_REBAR_LINE", "Main / AddHT / AddHB / AddVT / AddVB"),
    (2, "钢筋文字 + 筏板标注类 → 说明", 4, "00-hy-4说明-一般-文", "HY_NOTE_GENERAL", "RebarText / RaftDimH / RaftDimV / RaftThick"),
    (3, "图框", 2, "00-hy-1图框-主", "—", "—"),
    (4, "挡土墙体", 2, "01-hy-6墙-挡土-体", "HY_RETAIN_WALL", "Legacy / Generated"),
    (5, "聚类主", 5, "03-hy-1主", "HY_CLUSTER_MAIN", "BP / AAP / BAP / ABolt / SteelPlate"),
    (6, "聚类轴", 2, "03-hy-2轴", "HY_CLUSTER_AXIS", "Circle / Text"),
    (7, "聚类区域", 2, "03-hy-3区域", "HY_CLUSTER_REGION", "Frame / Text"),
    (8, "聚类标", 2, "03-hy-4标", "HY_CLUSTER_DIM", "Horizontal / Vertical"),
    (9, "聚类辅", 4, "03-hy-5辅", "HY_CLUSTER_AUX", "EndPoint / ExtEndPoint / Hull / Points"),
    (10, "横断注释 → 说明", 2, "00-hy-4说明-一般-文（建议）", "HY_NOTE_GENERAL", "RoadCsAnno / RoadCsOrientation"),
    (11, "横断图题", 2, "02-hy-3横断-图题", "HY_ROAD_CS_TITLE", "Main / Decoration"),
    (12, "人行横道", 2, "02-hy-5标线-人道", "—", "—"),
]

ACI_HEADERS = ["ACI", "颜色", "典型用途"]
ACI_ROWS = [
    (1, "红", "主轮廓 · 重点标注 · 红线 · 配筋 · 停止线 · 横断中心(点划线)"),
    (2, "黄", "次轮廓 · 车道 · 钢筋点 · 编号"),
    (3, "绿", "标注 · 符号 · 线位 · 标高 · 绿化 · 点 · 柱/梁"),
    (4, "青", "内孔 · 标线 · 人道 · 板 · 连接墙 · 基梁"),
    (5, "蓝", "板块 · 偏移 · 维诺 · 挡土墙 · 条形基础"),
    (6, "洋红", "设备基础顶 · 路牙 · 外环 · 预埋 · 手动"),
    (7, "白/黑", "文字 · 表格 · 尺寸标 · 轮廓 · 桩号"),
    (8, "暗灰", "底图 · 三维 · 走廊 · 原线预览 · 电梯基坑"),
    (9, "深灰", "区域 · 填充"),
    (131, "蓝（深）", "图签文字"),
    (135, "蓝（浅）", "老旧 000-C-TITLE（参考）"),
    (200, "紫", "标记（不可打印） · 无文标高"),
    (212, "灰紫", "老旧 000-C-MODIFY-THIN（参考）"),
    (252, "极浅灰", "实时预览"),
    (253, "浅灰", "拾取预览 · 辅助"),
]

STATUS_FILL = {
    "保留": "FFFFFFFF",
    "保留（扩容）": "FFE2F0D9",
    "保留（改名）": "FFE2F0D9",
    "保留（改号）": "FFE2F0D9",
    "用户新增": "FFDDEBF7",
    "用户合并": "FFFCE4D6",
    "建议新增": "FFFFF2CC",
    "建议合并": "FFFFD966",
    "建议保留": "FFF2F2F2",
    "v3新增": "FFE0F2F1",
    "v3改动": "FFFFE0B2",
}


def copy_sheet(src_ws, dst_wb, title: str):
    dst = dst_wb.create_sheet(title)
    for row in src_ws.iter_rows(values_only=True):
        dst.append(row)
    for col_letter, dim in src_ws.column_dimensions.items():
        if dim.width:
            dst.column_dimensions[col_letter].width = dim.width
    return dst


def style_header(ws, ncols: int):
    fill = PatternFill("solid", fgColor="FF4472C4")
    font = Font(bold=True, color="FFFFFFFF")
    for c in range(1, ncols + 1):
        cell = ws.cell(row=1, column=c)
        cell.fill = fill
        cell.font = font
        cell.alignment = Alignment(horizontal="center", vertical="center", wrap_text=True)


def autosize(ws, cap: int = 50):
    for col in range(1, ws.max_column + 1):
        letter = get_column_letter(col)
        max_len = 10
        for row in range(1, min(ws.max_row, 400) + 1):
            v = ws.cell(row=row, column=col).value
            if v is not None:
                max_len = max(max_len, min(len(str(v)), cap))
        ws.column_dimensions[letter].width = max_len + 2


LIN_DEF_ROWS = [
    ("点划线", HY, "A,12,-3,5,-3", "轴线/中心线；Resources/HyCAD-Linetypes.lin"),
    ("虚线", HY, "A,3,-2", "-虚 孪生层；同上"),
]


def main():
    wb = Workbook()

    ws0 = wb.active
    ws0.title = "说明"
    intro = [
        "HyCAD 图层-v3（点划线 + 虚线孪生 + 4基础 + 03聚类）",
        "",
        "【相对 v2】",
        "1. 线型：轴线/横断中心/聚类轴 使用 HyCAD「点划线」；结构-虚与道路横断-虚 使用 HyCAD「虚线」。",
        "2. 资源：HyCADTool.Refactored/Resources/HyCAD-Linetypes.lin，启动时 PluginInitializer 注入。",
        "3. 结构：筏板与设备基础并入 01-hy-4基础；扩展独立/条形/电梯/集水/基梁 主+虚；新增 8梁 子组。",
        "4. 聚类：独立 03 大类（03-hy-1主 … 5辅）。",
        "5. 道路横断：轮廓/车道/人道/路牙 各增 -虚 孪生层。",
        "",
        "【工作表】图层完整清单v3 · 线型定义 · v1.1原表-* · 老旧原表-* · 老旧到v3映射 · XData合并 · ACI颜色",
        "",
        "【再生成】doc/Layers 下：python generate_layer_excel_v3.py",
    ]
    for i, line in enumerate(intro, start=1):
        c = ws0.cell(row=i, column=1, value=line)
        c.alignment = Alignment(wrap_text=True, vertical="top")
        if i == 1:
            c.font = Font(bold=True, size=14)
    ws0.column_dimensions["A"].width = 110

    ws_main = wb.create_sheet("图层完整清单v3")
    ws_main.append(MAIN_HEADERS)
    for r in MAIN_ROWS:
        ws_main.append([
            r[0],
            CATEGORY_NAME.get(r[0], ""),
            r[1],
            r[2],
            r[3],
            r[4],
            r[5],
            r[6],
            r[7],
            r[8],
            r[9],
            r[10],
            r[11],
            r[12],
            r[13],
            r[14],
        ])
    style_header(ws_main, len(MAIN_HEADERS))
    for row in range(2, ws_main.max_row + 1):
        status = ws_main.cell(row=row, column=5).value
        color = STATUS_FILL.get(status)
        if color:
            for col in range(1, len(MAIN_HEADERS) + 1):
                ws_main.cell(row=row, column=col).fill = PatternFill("solid", fgColor=color)
    ws_main.freeze_panes = "F2"
    autosize(ws_main)

    ws_lin = wb.create_sheet("线型定义")
    ws_lin.append(["线型名", "定义来源", "图案行（.lin）", "说明"])
    for row in LIN_DEF_ROWS:
        ws_lin.append(list(row))
    style_header(ws_lin, 4)
    raw = (ROOT.parent.parent / "HyCADTool.Refactored" / "Resources" / "HyCAD-Linetypes.lin")
    if raw.exists():
        txt = raw.read_text(encoding="utf-8")
    else:
        txt = "（未找到源码树中的 HyCAD-Linetypes.lin）"
    ws_lin.cell(row=6, column=1, value="HyCAD-Linetypes.lin 全文")
    ws_lin.cell(row=7, column=1, value=txt)
    ws_lin.merge_cells(start_row=7, start_column=1, end_row=7, end_column=4)
    ws_lin.cell(row=7, column=1).alignment = Alignment(wrap_text=True, vertical="top")
    autosize(ws_lin)

    if V11_XLSX.exists():
        src = load_workbook(V11_XLSX, data_only=True)
        for name in src.sheetnames:
            copy_sheet(src[name], wb, f"v1.1原表-{name}")

    if LEGACY_XLSX.exists():
        src = load_workbook(LEGACY_XLSX, data_only=True)
        for name in src.sheetnames:
            copy_sheet(src[name], wb, f"老旧原表-{name}")

    ws_delta = wb.create_sheet("老旧到v3映射")
    ws_delta.append(LEGACY_DELTA_HEADERS)
    for r in LEGACY_DELTA_ROWS:
        ws_delta.append(list(r))
    style_header(ws_delta, len(LEGACY_DELTA_HEADERS))
    ws_delta.freeze_panes = "A2"
    autosize(ws_delta)

    ws_x = wb.create_sheet("XData合并")
    ws_x.append(XDATA_HEADERS)
    for r in XDATA_ROWS:
        ws_x.append(list(r))
    style_header(ws_x, len(XDATA_HEADERS))
    ws_x.freeze_panes = "A2"
    autosize(ws_x)

    ws_aci = wb.create_sheet("ACI颜色")
    ws_aci.append(ACI_HEADERS)
    for r in ACI_ROWS:
        ws_aci.append(list(r))
    style_header(ws_aci, len(ACI_HEADERS))
    ws_aci.freeze_panes = "A2"
    autosize(ws_aci)

    wb.save(OUT_XLSX)
    print(f"Wrote {OUT_XLSX}")
    print(f"  main rows = {len(MAIN_ROWS)}")


if __name__ == "__main__":
    main()
