# -*- coding: utf-8 -*-
"""生成 HyCAD 图层-v2.xlsx。

v2 目标：
1. 以 HyCAD图层-v1.1.xlsx 为主数据源（保留你手改的内容），同时对错位/空行做清洗
2. 吸收你手改中的再合并信号：
   - 新增 `00-hy-4说明` 子组（一般-文 / 图表-文）
   - 结构-配筋 的"-文 / -标"类全部合并到 `00-hy-4说明-一般-文`
   - 结构-配筋 筏板附加上下层合并到 `钢筋线`，用线型区分
3. 把 老旧设置.xlsx 原样作为参考 sheet，并给出"老旧→v2候选映射"
4. 列出 "v2 候选 Delta"：相对 v1.1 的新增/建议，让你继续勾选"采纳/否决"

运行：
    python generate_layer_excel_v2.py
"""
from __future__ import annotations

from pathlib import Path

from openpyxl import Workbook, load_workbook
from openpyxl.styles import Alignment, Font, PatternFill
from openpyxl.utils import get_column_letter

ROOT = Path(__file__).resolve().parent
V11_XLSX = ROOT / "HyCAD图层-v1.1.xlsx"
LEGACY_XLSX = ROOT / "老旧设置.xlsx"
OUT_XLSX = ROOT / "HyCAD图层-v2.xlsx"


# ---------------------------------------------------------------------------
# 1. v2 主清单（按大类 → 子组 → 具体层 分组，基于用户 v1.1 手改收敛）
#    列：大类号 | 大类名 | 子组 | 新层名 | v2状态 | 语义ID | ACI色 | 线型 |
#        线宽mm | 锁定 | 打印 | 归属 | 合并目标/备注 | XData说明 | 是否模板
#
#    v2状态枚举：
#      保留       —— v1.1 原样
#      用户新增   —— 你在 v1.1 xlsx 里加的
#      用户合并   —— 你在 v1.1 xlsx 里用"合并到 XXX"标的（不再是独立层，仅列出去向）
#      建议新增   —— 来自老旧设置的候选层（默认留空，等你勾选）
#      建议合并   —— 我基于老旧 + v1.1 交集建议的合并（默认留空）
# ---------------------------------------------------------------------------

MAIN_HEADERS = [
    "大类号",
    "大类名",
    "子组",
    "新层名",
    "v2状态",
    "语义ID",
    "ACI色",
    "线型",
    "线宽mm",
    "锁定",
    "打印",
    "归属",
    "合并目标/备注",
    "XData说明",
    "是否模板",
]


# 主清单数据。手工维护，来源于 v1.1 md + 你的 xlsx 手改。
# 单元格含义：(大类号, 子组, 新层名, v2状态, 语义ID, ACI色, 线型, 线宽, 锁, 打, 归属, 合并/备注, XData, 模板)
MAIN_ROWS: list[tuple] = [
    # ============ 00 公共 ============
    # 1 图框
    ("00", "1图框", "00-hy-1图框-主", "保留", "Public.TitleBlock.Main", 7, "Continuous", 0.50, "—", "Y", "DesignSpecService.TitleBlockLayer", "", "", "否"),
    ("00", "1图框", "00-hy-1图框-说明-文", "保留", "Public.TitleBlock.SheetText", 7, "Continuous", -1, "—", "Y", "DesignSpecService.TitleTextLayer", "", "", "否"),
    ("00", "1图框", "00-hy-1图框-图签-文", "用户新增", "Public.TitleBlock.SealText", 131, "Continuous", -1, "—", "Y", "DesignSpecService（图签文字，原用色 131 蓝）", "你建议蓝色", "", "否"),
    # 2 视口
    ("00", "2视口", "00-hy-2视口-主", "保留", "Public.Viewport.Main", 8, "Continuous", -1, "—", "N", "MBRCommand / PackViewportsCommand / PublicViewport", "你建议蓝色", "", "否"),
    # 3 标注（尺寸线本身，不含文字）
    ("00", "3标注", "00-hy-3标注-外", "保留", "Public.Dimension.Outside", 3, "Continuous", -1, "—", "Y", "RoadCsDrawStyleFactory / 通用标注", "", "", "否"),
    ("00", "3标注", "00-hy-3标注-内横", "保留", "Public.Dimension.InsideHorizontal", 1, "Continuous", -1, "—", "Y", "基础配筋横向尺寸", "", "", "否"),
    ("00", "3标注", "00-hy-3标注-内纵", "保留", "Public.Dimension.InsideVertical", 2, "Continuous", -1, "—", "Y", "基础配筋纵向尺寸", "", "", "否"),
    ("00", "3标注", "00-hy-3标注-引线", "保留", "Public.Dimension.Leader", 3, "Continuous", -1, "—", "Y", "RoadCsDrawStyleFactory.LeaderLayer", "", "", "否"),
    # 4 说明（新增，汇聚所有文字/标注文字类）
    ("00", "4说明", "00-hy-4说明-一般-文", "用户新增", "Public.Note.General", 7, "Continuous", -1, "—", "Y", "通用文字 / 注释 / 标注文字（v2 新汇聚层）", "你建议蓝色", "HY_NOTE_GENERAL.Kind：见合并重构", "否"),
    ("00", "4说明", "00-hy-4说明-图表-文", "用户新增", "Public.Note.Table", 131, "Continuous", -1, "—", "Y", "图签/表格文字（v2 新）", "你建议蓝色", "", "否"),
    # 5 表格
    ("00", "5表格", "00-hy-5表格-主", "保留", "Public.Table.Main", 7, "Continuous", -1, "—", "Y", "EquipmentFoundationService / 通用表格", "", "", "否"),
    # 6 轴线
    ("00", "6轴线", "00-hy-6轴线-主", "保留", "Public.Axis.Main", 1, "CENTER", -1, "—", "Y", "EquipmentFoundationService.AxisLayer", "", "", "否"),
    ("00", "6轴线", "00-hy-6轴线-主-文", "保留", "Public.Axis.Text", 1, "Continuous", -1, "—", "Y", "EquipmentFoundationService.AxisTextLayer", "", "", "否"),
    # 7 标高（你在 v1.1 中删了 警告 / {分组}，此处按删除记录）
    ("00", "7标高", "00-hy-7标高-符号", "保留", "Public.Elevation.Symbol", 3, "Continuous", -1, "—", "Y", "ElevationSymbolJig.SymbolLayer", "", "", "否"),
    ("00", "7标高", "00-hy-7标高-无文", "保留", "Public.Elevation.NoText", 200, "Continuous", -1, "—", "N", "GroupCirclesByElevationCommand.NoTextLayer", "", "", "否"),
    ("00", "7标高", "00-hy-7标高-警告", "建议保留", "Public.Elevation.Warning", 1, "Continuous", -1, "—", "N", "GroupCirclesByElevationCommand.WarningLayer", "你已在 v1.1 xlsx 中删除—此行仅作备忘，若确认删除请整行删除", "", "否"),
    ("00", "7标高", "00-hy-7标高-{分组}", "建议保留", "Public.Elevation.GroupTemplate", "动态", "Continuous", -1, "—", "Y", "GroupCirclesByElevationCommand.GroupLayerFormat", "你已在 v1.1 xlsx 中删除—同上", "", "是"),
    # 8 图像
    ("00", "8图像", "00-hy-8图像-定位", "保留", "Public.Image.Locator", 8, "Continuous", -1, "L", "N", "ImageLocatorService", "", "", "否"),
    # 9 标记（你在 v1.1 xlsx 里大类号错填成 03，v2 规整回 00）
    ("00", "9标记", "00-hy-9标记-打断点", "保留", "Public.Marker.Break", 1, "Continuous", -1, "—", "N", "MarkerLayerService.BreakLayer", "", "", "否"),
    ("00", "9标记", "00-hy-9标记-重复线", "保留", "Public.Marker.Duplicate", 2, "Continuous", -1, "—", "N", "MarkerLayerService.DuplicateLayer", "", "", "否"),
    ("00", "9标记", "00-hy-9标记-外轮廓", "保留", "Public.Marker.Outer", 3, "Continuous", -1, "—", "N", "MarkerLayerService.OuterLayer", "", "", "否"),
    ("00", "9标记", "00-hy-9标记-内孔洞", "保留", "Public.Marker.Inner", 5, "Continuous", -1, "—", "N", "MarkerLayerService.InnerLayer", "", "", "否"),
    ("00", "9标记", "00-hy-9标记-标高检查", "保留", "Public.Marker.ElevationCheck", 6, "Continuous", -1, "—", "N", "MarkerLayerService.ElevationCheckLayer", "", "", "否"),
    ("00", "9标记", "00-hy-9标记-独立端点", "保留", "Public.Marker.IsolatedEnd", 4, "Continuous", -1, "—", "N", "MarkerLayerService.IsolatedEndLayer", "", "", "否"),
    ("00", "9标记", "00-hy-9标记-未连接线", "保留", "Public.Marker.Unconnected", 200, "Continuous", -1, "—", "N", "MarkerLayerService.DcelUnconnectedLayer", "", "", "否"),

    # ============ 01 结构 ============
    # 1 配筋（v2 瘦身）
    ("01", "1配筋", "01-hy-1配筋-钢筋线", "保留（扩容）", "Structure.Rebar.Line", 1, "Continuous", -1, "—", "Y", "BaseReinforcementService · 线钢筋主层", "v2 合并目标：接收筏板附加配筋（用线型+颜色区分方向/上下）", "XData HY_REBAR_LINE.Kind = Main/AddHT/AddHB/AddVT/AddVB", "否"),
    ("01", "1配筋", "01-hy-1配筋-钢筋点", "保留", "Structure.Rebar.Dot", 2, "Continuous", -1, "—", "Y", "BaseReinforcementService · 点式钢筋", "", "", "否"),
    ("01", "1配筋", "01-hy-1配筋-钢筋外", "保留", "Structure.Rebar.External", 3, "Continuous", -1, "—", "Y", "BaseReinforcementService · 外部线钢筋", "", "", "否"),
    ("01", "1配筋", "01-hy-1配筋-手动", "保留", "Structure.Rebar.Manual", 6, "Continuous", -1, "—", "Y", "ManualRebarCommand · 手动配筋", "", "", "否"),
    ("01", "1配筋", "01-hy-1配筋-钢筋-文", "用户合并", "（合并去向）", "—", "—", "—", "—", "—", "—", "—", "你已合并到 00-hy-4说明-一般-文；代码筛选改按 XData HY_REBAR_TEXT.Kind", "—", "—"),
    ("01", "1配筋", "01-hy-1配筋-筏板横上", "用户合并", "（合并去向）", "—", "—", "—", "—", "—", "—", "—", "你已合并到 01-hy-1配筋-钢筋线，颜色=1/线型=Continuous", "—", "—"),
    ("01", "1配筋", "01-hy-1配筋-筏板横下", "用户合并", "（合并去向）", "—", "—", "—", "—", "—", "—", "—", "你已合并到 01-hy-1配筋-钢筋线，颜色=1/线型=DASHED", "—", "—"),
    ("01", "1配筋", "01-hy-1配筋-筏板纵上", "用户合并", "（合并去向）", "—", "—", "—", "—", "—", "—", "—", "你已合并到 01-hy-1配筋-钢筋线，颜色=3/线型=Continuous", "—", "—"),
    ("01", "1配筋", "01-hy-1配筋-筏板纵下", "用户合并", "（合并去向）", "—", "—", "—", "—", "—", "—", "—", "你已合并到 01-hy-1配筋-钢筋线，颜色=3/线型=DASHED", "—", "—"),
    ("01", "1配筋", "01-hy-1配筋-筏板横-标", "用户合并", "（合并去向）", "—", "—", "—", "—", "—", "—", "—", "你已合并到 00-hy-4说明-一般-文", "—", "—"),
    ("01", "1配筋", "01-hy-1配筋-筏板纵-标", "用户合并", "（合并去向）", "—", "—", "—", "—", "—", "—", "—", "你已合并到 00-hy-4说明-一般-文", "—", "—"),
    ("01", "1配筋", "01-hy-1配筋-筏板厚度-文", "用户合并", "（合并去向）", "—", "—", "—", "—", "—", "—", "—", "你已合并到 00-hy-4说明-一般-文", "—", "—"),
    ("01", "1配筋", "01-hy-1配筋-筏板轮廓", "保留", "Structure.Raft.Outline", 3, "Continuous", -1, "—", "Y", "BaseReinforcementService.OutlineLayer", "", "", "否"),
    ("01", "1配筋", "01-hy-1配筋-筏板轮廓-辅", "保留", "Structure.Raft.OutlineAdjust", 1, "Continuous", -1, "—", "N", "BaseReinforcementService.AdjustOutlineLayer", "", "", "否"),
    ("01", "1配筋", "01-hy-1配筋-筏板-体", "保留", "Structure.Raft.Solid", 8, "Continuous", -1, "—", "N", "Elevation3DCommand.RaftSolidLayer · 筏板三维", "", "", "否"),
    # 2 桩
    ("01", "2桩", "01-hy-2桩-主", "保留", "Structure.Pile.Main", 2, "Continuous", -1, "—", "Y", "PileDrawingService.PileLayer", "", "", "否"),
    ("01", "2桩", "01-hy-2桩-地基轮廓", "保留", "Structure.Pile.GroundOutline", 7, "Continuous", -1, "—", "Y", "PileDrawingService.GroundOutlineLayer", "", "", "否"),
    ("01", "2桩", "01-hy-2桩-阵列", "保留", "Structure.Pile.Array", 2, "Continuous", -1, "—", "Y", "PileVoronoiOptimizationCommand.PileLayer", "", "", "否"),
    ("01", "2桩", "01-hy-2桩-维诺-辅", "保留", "Structure.Pile.Voronoi", 5, "Continuous", -1, "—", "N", "PileVoronoiOptimizationCommand.VoronoiLayer", "", "", "否"),
    # 3 锚栓
    ("01", "3锚栓", "01-hy-3锚栓-主", "保留", "Structure.AnchorBolt.Main", 1, "Continuous", -1, "—", "Y", "AnchorBoltCommand · 主层", "", "", "否"),
    ("01", "3锚栓", "01-hy-3锚栓-轮廓", "保留", "Structure.AnchorBolt.Outline", 3, "Continuous", -1, "—", "Y", "AnchorBoltCommand · 轮廓", "", "", "否"),
    ("01", "3锚栓", "01-hy-3锚栓-编号", "保留", "Structure.AnchorBolt.Number", 2, "Continuous", -1, "—", "Y", "AnchorBoltCommand · 编号", "", "", "否"),
    ("01", "3锚栓", "01-hy-3锚栓-预埋", "保留", "Structure.AnchorBolt.Embedded", 6, "Continuous", -1, "—", "Y", "AnchorBoltCommand · 预埋板", "", "", "否"),
    ("01", "3锚栓", "01-hy-3锚栓-{型号}", "保留", "Structure.AnchorBolt.ModelTemplate", "动态", "Continuous", -1, "—", "Y", "AnchorBoltCommand.ModelLayerFormat", "{型号} 为锚栓型号中文标识", "", "是"),
    # 4 设备基础
    ("01", "4设备基础", "01-hy-4设备基础-侧-体", "保留", "Structure.EquipFoundation.SideSolid", 1, "Continuous", -1, "—", "N", "SlabGenerationService · 侧面体", "", "", "否"),
    ("01", "4设备基础", "01-hy-4设备基础-顶-体", "保留", "Structure.EquipFoundation.TopSolid", 3, "Continuous", -1, "—", "N", "SlabGenerationService · 顶面体", "", "", "否"),
    ("01", "4设备基础", "01-hy-4设备基础-底-体", "保留", "Structure.EquipFoundation.BottomSolid", 5, "Continuous", -1, "—", "N", "SlabGenerationService · 底面体", "", "", "否"),
    # 5 垫层
    ("01", "5垫层", "01-hy-5垫层-主", "保留", "Structure.Cushion.Main", 8, "Continuous", -1, "—", "Y", "CushionService", "", "", "否"),
    # 6 墙
    ("01", "6墙", "01-hy-6墙-砼墙", "保留", "Structure.Wall.Concrete", 2, "Continuous", -1, "—", "Y", "BaseReinforcementService · 裸中文'砼墙'", "", "", "否"),
    ("01", "6墙", "01-hy-6墙-砼墙-虚", "建议新增", "Structure.Wall.ConcreteDashed", 2, "DASHED", -1, "—", "Y", "（老旧 027-S-WALL-DASH 对应；v2 候选）", "老旧有 -DASH 对配，是否采纳？", "", "否"),
    ("01", "6墙", "01-hy-6墙-主-体", "保留", "Structure.Wall.Solid", 8, "Continuous", -1, "—", "N", "Elevation3DCommand.WallSolidLayer", "", "", "否"),
    ("01", "6墙", "01-hy-6墙-挡土-体", "保留", "Structure.Wall.RetainSolid", 5, "Continuous", -1, "—", "N", "Elevation3DCommand.RetainWallSolidLayer / WallGenerationService 挡土墙", "", "", "否"),
    ("01", "6墙", "01-hy-6墙-连接-体", "保留", "Structure.Wall.ConnectSolid", 4, "Continuous", -1, "—", "N", "WallGenerationService · 连接墙", "", "", "否"),
    # 7 柱（v2 从 v1.1 的"柱板"拆出来）
    ("01", "7柱", "01-hy-7柱-主", "建议保留（改名）", "Structure.Column.Main", 3, "Continuous", -1, "—", "Y", "BaseReinforcementService · 裸中文'柱'（v1.1 名: 01-hy-7柱板-柱）", "老旧 026-S-COLUMN；v2 建议把 柱板 子组拆为 7柱 + 8板", "", "否"),
    ("01", "7柱", "01-hy-7柱-虚", "建议新增", "Structure.Column.Dashed", 3, "DASHED", -1, "—", "Y", "（老旧 026-S-COLUMN-DASH 对应）", "是否采纳？", "", "否"),
    # 8 板（v2 从 v1.1 的"柱板"拆出来）
    ("01", "8板", "01-hy-8板-主", "建议保留（改名）", "Structure.Slab.Main", 4, "Continuous", -1, "—", "Y", "BaseReinforcementService · 裸中文'板元'（v1.1 名: 01-hy-7柱板-板）", "老旧 029-S-SLAB", "", "否"),
    ("01", "8板", "01-hy-8板-虚", "建议新增", "Structure.Slab.Dashed", 4, "DASHED", -1, "—", "Y", "（老旧 029-S-SLAB-DASH 对应）", "是否采纳？", "", "否"),
    ("01", "8板", "01-hy-8板-配筋-标", "建议合并", "—", "—", "—", "—", "—", "—", "—", "v1.1 的'01-hy-7柱板-板配筋-标'在 v2 方案下建议合并到 00-hy-4说明-一般-文", "—", "—"),
    ("01", "8板", "01-hy-8板-外环-辅", "保留（改名）", "Structure.Slab.OuterRing", 6, "Continuous", -1, "—", "N", "Elevation3DCommand.DcelOuterLayer", "v1.1 名：01-hy-7柱板-外环-辅", "", "否"),
    ("01", "8板", "01-hy-8板-内环-辅", "保留（改名）", "Structure.Slab.InnerRing", 5, "Continuous", -1, "—", "N", "Elevation3DCommand.DcelInnerLayer", "v1.1 名：01-hy-7柱板-内环-辅", "", "否"),
    # 9 聚类（编号挪位：v1.1 的 8聚类 在 v2 变 9聚类，让出 7/8 给柱/板）
    ("01", "9聚类", "01-hy-9聚类-主", "保留（改号）", "Structure.Cluster.Main", 3, "Continuous", -1, "—", "Y", "ClusterDrawService（v1.1 编号 8，v2 建议改 9）", "合并 BP/AAP/BAP/ABolt/SteelPlate", "HY_CLUSTER_MAIN.Kind", "否"),
    ("01", "9聚类", "01-hy-9聚类-轴", "保留（改号）", "Structure.Cluster.Axis", 7, "CENTER", -1, "—", "Y", "ClusterDrawService", "合并 AxisCircle + AxisText", "HY_CLUSTER_AXIS.Kind", "否"),
    ("01", "9聚类", "01-hy-9聚类-区域", "保留（改号）", "Structure.Cluster.Region", 9, "Continuous", -1, "—", "Y", "ClusterDrawService", "合并 Region + RegionText", "HY_CLUSTER_REGION.Kind", "否"),
    ("01", "9聚类", "01-hy-9聚类-标", "保留（改号）", "Structure.Cluster.Dim", 7, "Continuous", -1, "—", "Y", "ClusterDrawService", "合并 Dim_X + Dim_Y", "HY_CLUSTER_DIM.Kind", "否"),
    ("01", "9聚类", "01-hy-9聚类-辅", "保留（改号）", "Structure.Cluster.Aux", 8, "Continuous", -1, "—", "N", "ClusterDrawService", "合并 EP/EEP/Hull/Pts", "HY_CLUSTER_AUX.Kind", "否"),

    # ============ 02 道路 ============
    # 1 平面
    ("02", "1平面", "02-hy-1平面-线位", "保留", "Road.PlaneAlignment", 3, "Continuous", -1, "—", "Y", "HyRoadLayers._defAlignment", "", "", "否"),
    ("02", "1平面", "02-hy-1平面-走廊", "保留", "Road.PlaneCorridor", 2, "Continuous", -1, "—", "Y", "HyRoadLayers._defCorridor", "", "", "否"),
    ("02", "1平面", "02-hy-1平面-红线", "保留", "Road.PlaneRedLine", 1, "Continuous", -1, "—", "Y", "HyRoadLayers._defRedLine", "", "", "否"),
    ("02", "1平面", "02-hy-1平面-板块", "保留", "Road.PlaneTileBoundary", 5, "Continuous", -1, "—", "Y", "HyRoadLayers._defTileBoundary", "", "", "否"),
    ("02", "1平面", "02-hy-1平面-标线", "保留", "Road.PlaneMarking", 4, "Continuous", -1, "—", "Y", "HyRoadLayers._defMarking", "", "", "否"),
    # 2 纵断
    ("02", "2纵断", "02-hy-2纵断-主", "保留", "Road.ProfileMain", 1, "Continuous", -1, "—", "Y", "HyRoadLayers._defProfile", "", "", "否"),
    # 3 横断
    ("02", "3横断", "02-hy-3横断-轮廓", "保留", "Road.CrossSection.Outline", 7, "Continuous", -1, "—", "Y", "CrossSectionLayoutBuilder · 外轮廓", "", "", "否"),
    ("02", "3横断", "02-hy-3横断-中心", "保留", "Road.CrossSection.Centerline", 1, "CENTER", -1, "—", "Y", "CrossSectionLayoutBuilder · 中心线", "", "", "否"),
    ("02", "3横断", "02-hy-3横断-车道", "保留", "Road.CrossSection.Pavement", 2, "Continuous", -1, "—", "Y", "CrossSectionLayoutBuilder · 车行道", "", "", "否"),
    ("02", "3横断", "02-hy-3横断-人道", "保留", "Road.CrossSection.Sidewalk", 5, "Continuous", -1, "—", "Y", "CrossSectionLayoutBuilder · 人行道", "", "", "否"),
    ("02", "3横断", "02-hy-3横断-路牙", "保留", "Road.CrossSection.Kerb", 6, "Continuous", -1, "—", "Y", "CrossSectionLayoutBuilder · 路牙", "", "", "否"),
    ("02", "3横断", "02-hy-3横断-绿化", "保留", "Road.CrossSection.Green", 3, "Continuous", -1, "—", "Y", "CrossSectionLayoutBuilder · 绿化带", "", "", "否"),
    ("02", "3横断", "02-hy-3横断-尺寸-标", "保留", "Road.CrossSection.DimChain", 7, "Continuous", -1, "—", "Y", "CrossSectionLayoutBuilder · 尺寸链", "", "", "否"),
    ("02", "3横断", "02-hy-3横断-注释-文", "建议合并", "—", "—", "—", "—", "—", "—", "—", "v2 建议并入 00-hy-4说明-一般-文（与你对结构配筋的处理一致）", "HY_ROAD_CS_ANNO.Kind", "—"),
    ("02", "3横断", "02-hy-3横断-图题", "保留", "Road.CrossSection.Title", 7, "Continuous", -1, "—", "Y", "DrawingSheetTitleSpec · 图题+装饰（合并）", "", "HY_ROAD_CS_TITLE.Kind", "否"),
    # 5 标线
    ("02", "5标线", "02-hy-5标线-主", "保留", "Road.Marking.Main", 4, "Continuous", -1, "—", "Y", "DrawCrosswalkCommand 通用", "", "", "否"),
    ("02", "5标线", "02-hy-5标线-人道", "保留", "Road.Marking.Crosswalk", 4, "Continuous", -1, "—", "Y", "合并 HyRoadLayers._defCrosswalk + DrawCrosswalkCommand.CrosswalkLayer", "", "", "否"),
    ("02", "5标线", "02-hy-5标线-停止", "保留", "Road.Marking.StopLine", 1, "Continuous", -1, "—", "Y", "DrawCrosswalkCommand.StopLineLayer", "", "", "否"),
    ("02", "5标线", "02-hy-5标线-辅助-辅", "保留", "Road.Marking.Auxiliary", 8, "Continuous", -1, "—", "N", "DrawCrosswalkCommand.AuxiliaryLayer", "", "", "否"),
    # 6 桩号
    ("02", "6桩号", "02-hy-6桩号-主", "保留", "Road.Station.Main", 7, "Continuous", -1, "—", "Y", "HyRoadLayers._defStation", "", "", "否"),
    # 7 几何
    ("02", "7几何", "02-hy-7几何-点", "保留", "Road.Geometry.Point", 3, "Continuous", -1, "—", "Y", "HyRoadLayers._defPoint", "", "", "否"),
    ("02", "7几何", "02-hy-7几何-偏移", "保留", "Road.Geometry.Offset", 5, "Continuous", -1, "—", "Y", "HyRoadLayers._defOffset", "", "", "否"),
    ("02", "7几何", "02-hy-7几何-缘石", "保留", "Road.Geometry.CurbRamp", 4, "Continuous", -1, "—", "Y", "HyRoadLayers._defCurbRamp · 缘石坡道", "", "", "否"),
    ("02", "7几何", "02-hy-7几何-盲道", "保留", "Road.Geometry.Blind", 2, "Continuous", -1, "—", "Y", "HyRoadLayers._defBlind · 盲道", "", "", "否"),
    # 8 交叉口
    ("02", "8交叉口", "02-hy-8交叉口-主", "保留", "Road.Intersection.Main", 6, "Continuous", -1, "—", "Y", "HyRoadLayers._defIntersection", "", "", "否"),
    # 9 预览
    ("02", "9预览", "02-hy-9预览-原线-预", "保留", "Road.Preview.RawPolyline", 8, "Continuous", -1, "L", "N", "HyRoadLayers.RawPolylineLayer", "", "", "否"),
    ("02", "9预览", "02-hy-9预览-实时-预", "保留", "Road.Preview.Realtime", 252, "Continuous", -1, "—", "N", "AlignmentWorkbenchService.PreviewLayer", "", "", "否"),
    ("02", "9预览", "02-hy-9预览-拾取-预", "保留", "Road.Preview.Pick", 253, "Continuous", -1, "—", "N", "通用用户拾取预览层", "", "", "否"),
]


# 大类名（用于主清单"大类名"列）
CATEGORY_NAME = {"00": "公共", "01": "结构", "02": "道路"}


# ---------------------------------------------------------------------------
# 2. v2 Delta（老旧→v2候选映射）
# ---------------------------------------------------------------------------

LEGACY_DELTA_HEADERS = ["老旧层名", "老旧含义", "v2 候选层名", "建议方式", "备注"]

LEGACY_DELTA_ROWS = [
    ("000-C-TITLE", "公共-图框/图框底层", "00-hy-1图框-主", "1:1 映射", "颜色 135 建议改为 v2 默认 7"),
    ("000-C-WINS", "公共-视口", "00-hy-2视口-主", "1:1 映射", ""),
    ("000-C-TITLE-TEXT", "公共-图框文字/图签文字", "00-hy-1图框-图签-文", "1:1 映射", "你已在 v1.1 xlsx 中新增此层"),
    ("000-C-MODIFY-WIDE", "公共-修改粗线", "（建议新增）00-hy-9标记-修改", "建议新增", "属于'修订云/修改'类，v2 建议归 9标记"),
    ("000-C-MODIFY-THIN", "公共-修改细线", "（建议合并入 00-hy-9标记-修改）", "建议合并", "粗细差异用 -虚 或颜色表达"),
    ("000-C-GROUND", "公共-地面底图", "00-hy-8图像-定位", "1:1 映射", "色彩/线型按各自标准保留"),
    ("021-S-AXIS", "结构-轴线主", "00-hy-6轴线-主", "1:1 映射", "老旧按结构类编号，v2 归公共大类（所有专业共用）"),
    ("021-S-AXIS-SE", "结构-轴线剖面", "（建议新增）00-hy-6轴线-剖", "建议新增", "当前 v2 没有'剖面版轴线'区分，是否采纳？"),
    ("022-S-AXIS-DIMS", "结构-轴线尺寸", "00-hy-3标注-外", "建议合并", "尺寸线归入公共标注"),
    ("022-S-DIMS-PLAN1", "结构-平面尺寸 1", "00-hy-3标注-外", "建议合并", "比例档次不用层分，用标注样式"),
    ("022-S-DIMS-PLAN2", "结构-平面尺寸 2", "00-hy-3标注-外", "建议合并", "同上"),
    ("022-S-DIMS-SE-SCALE1", "结构-剖面尺寸比例1", "00-hy-3标注-外", "建议合并", "同上"),
    ("022-S-DIMS-PLAN2-SCALE2", "结构-剖面尺寸比例2", "00-hy-3标注-外", "建议合并", "同上"),
    ("022-S-DIMS-PLAN2-SCALE3", "结构-剖面尺寸比例3", "00-hy-3标注-外", "建议合并", "同上"),
    ("023-S-TEXT-3", "结构-文字3（中号）", "00-hy-4说明-一般-文", "建议合并", "文字字高档次用标注样式"),
    ("023-S-TEXT-5", "结构-注释说明", "00-hy-4说明-一般-文", "建议合并", ""),
    ("023-S-TEXT-5-T", "结构-图纸内点注", "00-hy-4说明-一般-文", "建议合并", ""),
    ("023-S-TEXT-7-T", "结构-图纸总注释", "00-hy-4说明-一般-文", "建议合并", ""),
    ("024-S-HATCH", "结构-填充默认", "（建议新增）00-hy-?-填", "建议新增", "v2 暂未落填充层，后续如需再补"),
    ("024-S-HATCH-1", "结构-填充粗", "—", "建议用 Hatch 图案/比例区分", "按规范：后缀 -填 一个层 + 样式差"),
    ("024-S-HATCH-2", "结构-填充中", "—", "同上", ""),
    ("024-S-HATCH-3", "结构-填充细", "—", "同上", ""),
    ("025-S-FUDN-RAFT", "结构-基础筏板", "01-hy-1配筋-筏板轮廓", "1:1 映射", "保持 v2 层名"),
    ("025-S-FUDN-RAFT-DASH", "结构-基础筏板虚线", "01-hy-1配筋-筏板轮廓 + 线型 DASHED", "建议合并（用线型区分）", "与你对筏板附加的处理一致"),
    ("025-S-FUDN-INDEPENDENT", "结构-独立基础", "（建议新增）01-hy-4设备基础-独立", "建议新增", "v2 4设备基础目前只有侧/顶/底三层，是否扩容？"),
    ("025-S-FUDN-INDEPENDENT-DASH", "结构-独立基础虚线", "同上 + 线型 DASHED", "建议合并", ""),
    ("025-S-FUDN-DADOS", "结构-条形基础", "（建议新增）01-hy-4设备基础-条形", "建议新增", ""),
    ("025-S-FUDN-DADOS-DASH", "结构-条形基础虚线", "同上 + DASHED", "建议合并", ""),
    ("025-S-FUDN-ELEVATOR", "结构-电梯基础", "（建议新增）01-hy-4设备基础-电梯", "建议新增", ""),
    ("025-S-FUDN-ELEVATOR-DASH", "结构-电梯基础虚线", "同上 + DASHED", "建议合并", ""),
    ("025-S-FUDN-SUMP", "结构-集水坑", "（建议新增）01-hy-4设备基础-集水", "建议新增", ""),
    ("025-S-FUDN-SUMP-DASH", "结构-集水坑虚线", "同上 + DASHED", "建议合并", ""),
    ("025-S-FUDN-UNDERCOUISE", "结构-基础梁", "（建议新增）01-hy-4设备基础-基梁", "建议新增", "Undercourse = 基础梁"),
    ("025-S-FUDN-UNDERCOUISE-DASH", "结构-基础梁虚线", "同上 + DASHED", "建议合并", ""),
    ("026-S-COLUMN", "结构-柱", "01-hy-7柱-主", "1:1 映射", "v2 建议把 '柱板' 子组拆为 7柱 + 8板"),
    ("026-S-COLUMN-DASH", "结构-柱虚线", "01-hy-7柱-虚", "建议新增", "用独立层（v2 建议采纳）"),
    ("027-S-WALL", "结构-墙", "01-hy-6墙-砼墙", "1:1 映射", ""),
    ("027-S-WALL-DASH", "结构-墙虚线", "01-hy-6墙-砼墙-虚", "建议新增", ""),
    ("028-S-BEAM", "结构-梁", "（建议新增）01-hy-7梁-主 或归入 7柱 / 8板", "建议新增", "v2 目前没有梁子组，如何归？是否新增 7梁？"),
    ("028-S-BEAM-DASH", "结构-梁虚线", "同上 -虚", "建议新增", ""),
    ("029-S-SLAB", "结构-板", "01-hy-8板-主", "1:1 映射", ""),
    ("029-S-SLAB-DASH", "结构-板虚线", "01-hy-8板-虚", "建议新增", ""),
    ("030-S-SECTION", "结构-剖面", "（建议新增）01-hy-?-剖", "建议新增", "剖面类是否独立？"),
    ("030-S-SECTION-DASH", "结构-剖面虚线", "同上 -虚", "建议新增", ""),
]


# ---------------------------------------------------------------------------
# 3. 从 v1.1 xlsx 抽取你的原始修改（保留 sheet 供对照）
# ---------------------------------------------------------------------------


def copy_sheet(src_ws, dst_wb, title: str):
    dst = dst_wb.create_sheet(title)
    for row in src_ws.iter_rows(values_only=True):
        dst.append(row)
    # 尝试复制列宽
    for col_letter, dim in src_ws.column_dimensions.items():
        if dim.width:
            dst.column_dimensions[col_letter].width = dim.width
    return dst


# ---------------------------------------------------------------------------
# 4. XData 合并一览 / ACI 颜色
# ---------------------------------------------------------------------------

XDATA_HEADERS = ["#", "合并组", "旧层数", "新层", "XData注册簇", "KIND枚举"]
XDATA_ROWS = [
    (1, "钢筋线（含筏板附加）", 5, "01-hy-1配筋-钢筋线", "HY_REBAR_LINE", "Main / AddHT / AddHB / AddVT / AddVB"),
    (2, "钢筋文字 + 筏板标注类 → 说明", 4, "00-hy-4说明-一般-文", "HY_NOTE_GENERAL", "RebarText / RaftDimH / RaftDimV / RaftThick"),
    (3, "图框", 2, "00-hy-1图框-主", "—", "—"),
    (4, "挡土墙体", 2, "01-hy-6墙-挡土-体", "HY_RETAIN_WALL", "Legacy / Generated"),
    (5, "聚类主", 5, "01-hy-9聚类-主", "HY_CLUSTER_MAIN", "BP / AAP / BAP / ABolt / SteelPlate"),
    (6, "聚类轴", 2, "01-hy-9聚类-轴", "HY_CLUSTER_AXIS", "Circle / Text"),
    (7, "聚类区域", 2, "01-hy-9聚类-区域", "HY_CLUSTER_REGION", "Frame / Text"),
    (8, "聚类标", 2, "01-hy-9聚类-标", "HY_CLUSTER_DIM", "Horizontal / Vertical"),
    (9, "聚类辅", 4, "01-hy-9聚类-辅", "HY_CLUSTER_AUX", "EndPoint / ExtEndPoint / Hull / Points"),
    (10, "横断注释 → 说明", 2, "00-hy-4说明-一般-文（建议）", "HY_NOTE_GENERAL", "RoadCsAnno / RoadCsOrientation"),
    (11, "横断图题", 2, "02-hy-3横断-图题", "HY_ROAD_CS_TITLE", "Main / Decoration"),
    (12, "人行横道", 2, "02-hy-5标线-人道", "—", "—"),
]

ACI_HEADERS = ["ACI", "颜色", "典型用途"]
ACI_ROWS = [
    (1, "红", "主轮廓 · 重点标注 · 红线 · 配筋 · 停止线"),
    (2, "黄", "次轮廓 · 车道 · 钢筋点 · 编号"),
    (3, "绿", "标注 · 符号 · 线位 · 标高 · 绿化 · 点 · 柱"),
    (4, "青", "内孔 · 标线 · 人道 · 板 · 连接墙"),
    (5, "蓝", "板块 · 偏移 · 底基础 · 维诺 · 挡土墙"),
    (6, "洋红", "设备基础顶 · 路牙 · 外环 · 预埋 · 手动"),
    (7, "白/黑", "文字 · 表格 · 尺寸标 · 轮廓 · 桩号"),
    (8, "暗灰", "底图 · 三维 · 走廊 · 原线预览"),
    (9, "深灰", "区域 · 填充"),
    (131, "蓝（深）", "图签文字（v2 新增）"),
    (135, "蓝（浅）", "老旧 000-C-TITLE（参考）"),
    (200, "紫", "标记（不可打印） · 无文标高"),
    (212, "灰紫", "老旧 000-C-MODIFY-THIN（参考）"),
    (252, "极浅灰", "实时预览"),
    (253, "浅灰", "拾取预览 · 辅助"),
]


# ---------------------------------------------------------------------------
# 5. 输出 xlsx
# ---------------------------------------------------------------------------

STATUS_FILL = {
    "保留": "FFFFFFFF",            # white
    "保留（扩容）": "FFE2F0D9",    # light green
    "保留（改名）": "FFE2F0D9",
    "保留（改号）": "FFE2F0D9",
    "用户新增": "FFDDEBF7",        # light blue
    "用户合并": "FFFCE4D6",        # light orange
    "建议新增": "FFFFF2CC",        # light yellow
    "建议合并": "FFFFD966",        # yellow
    "建议保留": "FFF2F2F2",        # light grey
}


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
        for row in range(1, min(ws.max_row, 300) + 1):
            v = ws.cell(row=row, column=col).value
            if v is not None:
                max_len = max(max_len, min(len(str(v)), cap))
        ws.column_dimensions[letter].width = max_len + 2


def main():
    wb = Workbook()

    # --- 说明 ---
    ws0 = wb.active
    ws0.title = "说明"
    intro = [
        "HyCAD 图层-v2（由 Python 脚本生成；基于 v1.1 + 你的手改 + 老旧设置.xlsx 参考整合）",
        "",
        "【核心变化】",
        "1. 新增 00-hy-4说明 子组（一般-文 / 图表-文），作为所有'文字/标注文字'的汇聚点。",
        "2. 结构-1配筋 大幅瘦身：",
        "   · 筏板附加配筋上/下/横/纵 4 条合并到 01-hy-1配筋-钢筋线（线型+颜色区分）",
        "   · 钢筋-文 / 筏板横-标 / 筏板纵-标 / 筏板厚度-文 合并到 00-hy-4说明-一般-文",
        "3. 结构-7柱板 拆为 7柱 + 8板（老旧 026/029 对应），原 8聚类 改号为 9聚类。",
        "4. 新增类型后缀 -虚（与老旧 -DASH 对应），仅作为候选选项存在。",
        "5. 横断-注释-文 建议并入 00-hy-4说明-一般-文（与你对结构的处理一致）。",
        "6. 9标记 大类号从你 v1.1 xlsx 中误填的 '03' 规整回 '00'。",
        "",
        "【工作表】",
        "· 图层完整清单v2   —— 主清单（列含 v2状态 / 合并目标/备注 / XData说明）",
        "· v1.1 用户修改原表  —— 你在 HyCAD图层-v1.1.xlsx 里的原始修改（对照）",
        "· 老旧设置原表       —— 老旧设置.xlsx 原样",
        "· 老旧到v2候选映射   —— 我草拟的对照表（建议新增/合并）",
        "· XData 合并        —— 注册簇一览（v2 版）",
        "· ACI 颜色           —— 色卡速查",
        "",
        "【v2状态颜色】白=保留，淡绿=保留/调整，淡蓝=用户新增，淡橙=用户合并（不再落层），淡黄/黄=我建议项（待你勾选）。",
        "",
        "【再生成】在 doc/Layers 下执行：python generate_layer_excel_v2.py",
    ]
    for i, line in enumerate(intro, start=1):
        c = ws0.cell(row=i, column=1, value=line)
        c.alignment = Alignment(wrap_text=True, vertical="top")
        if i == 1:
            c.font = Font(bold=True, size=14)
    ws0.column_dimensions["A"].width = 110

    # --- 主清单 ---
    ws_main = wb.create_sheet("图层完整清单v2")
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
        ])
    style_header(ws_main, len(MAIN_HEADERS))
    # 按 v2状态 着色
    for row in range(2, ws_main.max_row + 1):
        status = ws_main.cell(row=row, column=5).value
        color = STATUS_FILL.get(status)
        if color:
            for col in range(1, len(MAIN_HEADERS) + 1):
                ws_main.cell(row=row, column=col).fill = PatternFill("solid", fgColor=color)
    ws_main.freeze_panes = "E2"
    autosize(ws_main)

    # --- v1.1 用户修改原表 ---
    if V11_XLSX.exists():
        src = load_workbook(V11_XLSX, data_only=True)
        for name in src.sheetnames:
            copy_sheet(src[name], wb, f"v1.1原表-{name}")

    # --- 老旧设置原表 ---
    if LEGACY_XLSX.exists():
        src = load_workbook(LEGACY_XLSX, data_only=True)
        for name in src.sheetnames:
            copy_sheet(src[name], wb, f"老旧原表-{name}")

    # --- 老旧到 v2 候选映射 ---
    ws_delta = wb.create_sheet("老旧到v2候选映射")
    ws_delta.append(LEGACY_DELTA_HEADERS)
    for r in LEGACY_DELTA_ROWS:
        ws_delta.append(list(r))
    style_header(ws_delta, len(LEGACY_DELTA_HEADERS))
    ws_delta.freeze_panes = "A2"
    autosize(ws_delta)

    # --- XData ---
    ws_x = wb.create_sheet("XData合并")
    ws_x.append(XDATA_HEADERS)
    for r in XDATA_ROWS:
        ws_x.append(list(r))
    style_header(ws_x, len(XDATA_HEADERS))
    ws_x.freeze_panes = "A2"
    autosize(ws_x)

    # --- ACI ---
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
    print(f"  delta rows = {len(LEGACY_DELTA_ROWS)}")


if __name__ == "__main__":
    main()
