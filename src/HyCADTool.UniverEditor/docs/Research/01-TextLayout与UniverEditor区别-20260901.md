# TextLayout 与 UniverEditor 的区别

结论：二者不是同一功能的两套实现，也互不引用。HyCADTool.TextLayout（文字排版程序集）管 **Markdown 设计说明怎么切块、估高、流栏、编成 MText**；HyCADTool.UniverEditor（独立表格编辑器侧车）管 **工程表怎么在独立窗口里改、再按纸面毫米落到图纸**。选哪边改，先看用户手里是「一篇说明」还是「一张表」。**已验证**。

## 1. 一句话对照

| | TextLayout | UniverEditor |
|---|---|---|
| 为难谁 | 同一段说明在预览和图纸上栏宽/换行对不齐 | CAD 原生表难改，通用表格软件又落不进图纸 |
| 用户手里的东西 | Markdown（轻量标记原文：标题、列表、GFM 表） | 网格表（行列、合并、单元格文字） |
| 交出的东西 | 分页分栏结果 + MText（AutoCAD 多行文字格式码）字符串 | 图上的表几何（纸面毫米 × Scale，Scale 是落图比例） |
| 有没有窗口 | 无。纯库，给插件代码调用 | 有。N38 打开独立窗口，里面嵌 Univer（开源表格引擎） |
| 像什么 | 排版计算器 | 旁路 Excel，但真相在图纸领域模型 |

## 2. 解决什么困难（不要混）

| 困难 | 走 TextLayout | 走 UniverEditor |
|---|---|---|
| 设计说明要像图纸一样分栏分页 | 是 | 否 |
| 说明里的 GFM 表只是正文里的一块，要估高或抽出去画 | 是（抽表/估高）；画 CAD Table（表格实体）在设计说明宿主，不在本库 | 否 |
| 人员表、图框表要像 Excel 那样改行高列宽再插入 DWG | 否 | 是 |
| 屏幕上的毫米要和图上量到的毫米对齐 | 栏宽跟纸走，估高用 CJK=2 约定尺子 | 口径 B：编辑一律纸面毫米，只有落图乘 Scale |

**两库都不解决**：不当 Word；不把 HyMd（进程外说明编辑器）或 Univer 工作簿文件当成图纸长期真相。

## 3. 什么样的人碰到哪边

| 适合 TextLayout | 适合 UniverEditor |
|---|---|
| 改 `hymd` / 设计说明链路里「说明怎么排」的开发者 | 要做人员表并插入图纸的制图员 |
| 要对齐「字宽、栏宽、块高」口径 | 要在独立窗口里插删行列、合并、按纸面毫米调尺寸 |

最终用户几乎不会直接打开 TextLayout；会打开 UniverEditor 窗口。**已验证**（TextLayout 无 UI；UniverEditor 有 N38 窗口）。

## 4. 机制怎么不同（不展开实现）

| 概念 | TextLayout | UniverEditor |
|---|---|---|
| 最小单位 | 顶层块（一段标题/一段列表/一张 GFM 表） | 单元格 + 行/列/合并 |
| 尺子 | 显示宽度：ASCII=1，CJK（中日韩汉字）/全角=2 | 纸面毫米；像素只用来适配屏幕 |
| 排版动作 | 当前栏放不下 → 下一栏 → 下一页 | 纸面框住网格，zoom 适配窗口，不把说明流进下一栏 |
| 和 CAD 的关系 | 不算 AutoCAD API；宿主把字符串写成 MText | 窗口是 net8 侧车，主插件 net48 用反射加载；落图/拾取在主工程表格功能 |
| 现行贴图 | `hymd` 主路径用 HyMd 导出的毫米坐标，本库 `LayoutEngine`（分页分栏调度）不在热路径 | 落图走 TableGrid（表格网格领域模型）× Scale |

二者工程引用：主插件 **直接引用** TextLayout；**不引用程序集、只触发编译** UniverEditor。UniverEditor 源码中无 TextLayout 符号。**已验证**。

历史文档里表格单元格 CJK 换行「V2 接 TextLayout」是规划句，现码未接。**已验证**（UniverEditor 无引用；规划在表格主题 Plan，非现码）。

## 5. 怎么选（边界）

改说明分栏、块高、MText 控制码口径 → TextLayout（并核对接的是本库还是设计说明 Domain 里那份平行渲染器）。

改独立表窗口、布局 Tab、Univer 快照、N38、拾取/落图 → UniverEditor + 主工程 `Features/Tables`。

两边都出现「表」：TextLayout 的表是 Markdown 里的一块；UniverEditor 的表是整张工程表。不要用 TextLayout 去驱动 Univer 网格，也不要用 UniverEditor 去排 Markdown 说明。**建议**。

## 6. 记忆链

一篇说明、要对齐栏 → TextLayout。一张工程表、要像 Excel 再落图 → UniverEditor。

- 库 ≠ 窗口
- GFM 表 ≠ Univer 表
- 估高尺子 ≠ 纸面毫米（约定字宽 vs 真实 mm）
- 规划「单元格换行接 TextLayout」≠ 已经接上

---

**证据级别**：产品分工、无互相引用、加载方式、`hymd` 不经 `LayoutEngine` **已验证**；单元格换行是否应接 TextLayout **建议**（规划有、代码无）。
**版本**：v1.0 | **更新**：2026-09-01
