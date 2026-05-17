# Excel 计算表格 — 示例与参考

## 一、算例脚本参考

**用途**：单组参数、控制台输出完整计算过程，无 Excel 依赖。

**结构要点**：

| 区块 | 内容 |
|------|------|
| 输入 | 脚本顶部集中赋值，单位在注释中注明 |
| 材料 | 按牌号字典存放强度、弹性模量等 |
| 纯函数 | 几何与公式用函数实现，不依赖全局可变状态 |
| main | 顺序计算、打印标题/输入回显/每步结果/结论 |

---

## 二、验算型 openpyxl 脚本参考

**用途**：生成 .xlsx，用户在 Excel 中改参数、看公式结果。

**结构要点**：

| 区块 | 内容 |
|------|------|
| 输入区 | 固定行号：参数名在 A 列，默认值在 B 列 |
| 公式区 | 按规范逐步写出 Excel 公式，与算例脚本一一对应 |
| 多 sheet | 计算 sheet + 可选「计算说明」sheet |
| 样式 | 标题合并、加粗、居中；边框按需 |

---

## 三、联动型概算表参考（核心模式）

**用途**：工程概算、工程量清单、造价计算表。修改参数或单价后全表自动刷新。

### 3.1 完整结构示意

```
Sheet "基本参数"
├── 左侧：工程参数（蓝色=可调，灰色=自动）
│   ├── 路线全长 L (m)          [蓝色] 1436.246
│   ├── 红线宽度 W (m)          [灰色] 50
│   ├── 机动车道宽度 (m)         [灰色] 22
│   ├── ...
│   ├── 行道树间距 (m)           [蓝色] 7
│   └── 路灯间距 (m)            [蓝色] 32
├── 右侧：费率参数
│   ├── 措施费率                 [蓝色] 10%
│   ├── 企业管理费率             [灰色] 5.5%
│   └── ...
└── 下方：面积自动计算
    ├── 道路总面积               [灰色] =C7*C6
    ├── 机动车道面积             [灰色] =C8*C6
    └── ...

Sheet "分项概算"
├── 一、旧路拆除工程
│   ├── 0.1 旧路面拆除  m²  =基本参数!C34  [蓝色]45  =ROUND(D*E/10000,2)
│   └── 小计                                          =SUM(...)
├── 二、土方工程
│   └── ...
├── ...（十二个分类）
└── 直接工程费合计 [黄色] =F小计1+F小计2+...

Sheet "费用汇总"
├── 上半段：直接费→措施费→管理费→利润→规费→税前→增值税→建安费 [黄色]
├── 下半段：建安费→其他费→预备费→征地拆迁→总投资 [黄色]
└── 关键指标：单位长度建安费、与可研对比等
```

### 3.2 关键代码模式

#### 基本参数 sheet — 参数区

```python
params = [
    # (name, value, note, is_input)
    ("路线全长 L (m)", 1436.246, "德胜西路～崇文路", True),
    ("红线宽度 W (m)", 50, "", False),
    ...
]

for i, (name, val, note, is_inp) in enumerate(params):
    rr = 6 + i
    fl = FILL_INPUT if is_inp else FILL_CALC
    ft = FT_INPUT if is_inp else FT_NORMAL
    cell(ws, rr, 2, name)
    cell(ws, rr, 3, val, font=ft, align=AL_R, fill=fl)
    cell(ws, rr, 4, note, font=FT_SMALL)
```

#### 基本参数 sheet — 面积自动计算

```python
area_rows = [
    ("道路总面积", "=C7*C6", "红线宽×全长"),
    ("机动车道面积", "=C8*C6", "机动车道宽×全长"),
    ("行道树株数（双侧）", "=ROUNDUP(C6/C13,0)*2", "全长÷间距×2侧"),
    ...
]
for i, (name, formula, note) in enumerate(area_rows):
    cell(ws, rr, 3, formula, fill=FILL_CALC, fmt=FMT_INT)  # 灰色自动计算
```

#### 分项概算 sheet — 数据驱动循环

```python
P = "基本参数"  # 跨 sheet 引用前缀

sections = [
    ("一、土方工程", [
        # (seq, name, unit, qty_formula, price, note, price_adjustable)
        ("1.1", "清除表土（30cm）", "m³",
         f"=ROUND({P}!C26*0.3,0)", 25, "总面积×0.3m", True),
        ...
    ]),
    ("二、机动车道路面", [
        ("2.1", "沥青混凝土路面（79cm）", "m²",
         f"={P}!C27", 420, "参照冀衡路上浮7%", True),
    ]),
    ...
]

all_subtotal_cells = []

for sec_name, items in sections:
    section_row(ws, r, sec_name, 7)  # 浅绿分类标题
    r += 1
    sec_first_row = r

    for item in items:
        seq, name, unit, qty, price, note, is_input = item
        # 数量：公式引用基本参数 或 硬编码
        cell(ws, r, 4, qty, fill=FILL_CALC, fmt=FMT_INT)
        # 单价：蓝色可调
        cell(ws, r, 5, price, fill=FILL_INPUT if is_input else FILL_CALC)
        # 合价：公式自动计算
        cell(ws, r, 6, f"=ROUND(D{r}*E{r}/10000,2)", fill=FILL_CALC, fmt=FMT_MONEY)
        r += 1

    # 小计行
    cell(ws, r, 6, f"=SUM(F{sec_first_row}:F{r-1})", fill=FILL_SUBTOTAL)
    all_subtotal_cells.append(r)
    r += 1

# 合计行
grand_formula = "=" + "+".join([f"F{sr}" for sr in all_subtotal_cells])
cell(ws, r, 6, grand_formula, fill=FILL_RESULT)  # 黄色关键结果
```

#### 费用汇总 sheet — 费率链

```python
# 直接费（引用分项概算合计行）
cell(ws, 4, 5, f"=分项概算!F{grand_row}", fill=FILL_CALC)

# 措施费（引用基本参数费率）
cell(ws, 5, 5, f"=ROUND(E4*基本参数!G6,2)", fill=FILL_CALC)

# ... 逐层计算 ...

# 建安费 = 税前 + 增值税（黄色关键结果）
cell(ws, 11, 5, f"=E{pretax_row}+E{tax_row}", fill=FILL_RESULT)

# 总投资 = 建安费 + 其他费 + 预备费 + 征地（黄色关键结果）
cell(ws, 19, 5, f"=SUM(E15:E18)", fill=FILL_RESULT)

# 经济指标（引用建安费与基本参数）
cell(ws, 22, 5, f"=ROUND(E11/(基本参数!C6/1000),0)")  # 万元/km
cell(ws, 23, 5, f"=E11/基本参数!C20")                   # 与可研对比
```

### 3.3 设计要点速查

| 要点 | 做法 | 不要 |
|------|------|------|
| 数量 | 用 Excel 公式引用基本参数 | 硬编码数值 |
| 单价 | 蓝色可调输入 | 写死在公式里 |
| 合价 | `=ROUND(D行*E行/10000,2)` | 用 Python 算好填入 |
| 小计 | `=SUM(范围)` | Python 求和后填入 |
| 合计 | `=F小计1+F小计2+...` | Python 求和 |
| 费率 | 引用基本参数 sheet 的费率单元格 | 公式中写死费率数字 |
| Sheet 数 | 3 个（参数+分项+汇总） | 拆成 10+ 个 sheet |
| 行号追踪 | 代码中用变量 `r` 动态递增 | 硬编码行号 |

---

## 四、参考文件路径（供 Agent 读取）

| 类型 | 路径 |
|------|------|
| 联动型概算表（道路工程） | `Work/Drawingdescription/Rode/excel/generate_tables.py` |
| 联动型概算表（海绵城市） | `Work/Drawingdescription/Rode/excel/gen_sponge_v2.py` |
| 算例脚本风格 | `Structured/03《钢结构设计标准》/01学习/calc_pipe_325x8.py` |
| 验算型 openpyxl | `Structured/03《钢结构设计标准》/01学习/gen_excel.py` |

---

## 五、常见问题

### Q1: print() 报 UnicodeEncodeError
Windows PowerShell 默认 GBK 编码。`print()` 中不要用 `²`、`φ`、`×` 等字符。改用 ASCII 替代：`m2`、`phi`、`x`。Excel 单元格内不受此限制。

### Q2: 概算表要不要做成静态数值表？
**不要**。所有概算/造价表必须全公式联动，用户改一个参数或单价就能看到全表变化。唯一例外是用户明确要求「帮我算好填进去」。

### Q3: Sheet 太多怎么办？
概算表一般 3 个 sheet 足够。如果用户的参考文件有 7 个 sheet，先判断是否可以合并。分项概算用分类标题行（浅绿 section_row）区分即可，不需要每个分类单独 sheet。

### Q4: 合价单位是元还是万元？
概算表合价统一用**万元**（`=ROUND(D*E/10000,2)`）。单价用**元**。两者不要混用。
