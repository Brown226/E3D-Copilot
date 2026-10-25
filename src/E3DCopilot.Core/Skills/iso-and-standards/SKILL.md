---
name: iso-and-standards
description: ISO 出图与管道设计规范 — ISO 四步流程、材料数据真实结构、壁厚/材料/支吊架规则。出 ISO 图或改管道参数前必读。合并原 iso-drawing-guide + iso-drawing-pipeline + piping-design-standards 三个 skill。
runAs: inline
tags: [ISO, 出图, 管道, 规范, 材料, 壁厚]
---

# ISO 出图与管道设计规范

> 两件事：**ISO 轴测图怎么出**、**管道参数合规怎么判断**。
> 原则：**语法对 ≠ 工程对。改工程参数前必须查规范。**

---

## 一、ISO 出图：四步流程

### 1.1 引擎与前置条件

核心引擎 `CNPE.ISO.E3D.Draw` → ISODRAFT 模块 → AutoCAD 输出 DWG。

| 前置条件 | 检查方式 |
|---------|---------|
| E3D 已运行并连接数据库 | `autocad(action=status)` 或直接尝试 |
| AutoCAD 已安装 | `generate_iso_drawing` 自动检测，失败则手动传 `cad_exe_path` |
| 材料数据 CSV | `lib/iso/{project_id}/` 下 `PIPE.csv` / `BOLT.csv` / `SCTN.csv` / `SUPP.csv` |

**没有「检测 AutoCAD 路径」的独立工具**。若 `generate_iso_drawing` 报 AutoCAD 相关错误，让用户提供 `cad_exe_path`。

### 1.2 四步

```
Step 1  query_material          — 验证材料编码（可选，材料缺失时必做）
Step 2  get_pipe_info           — 提取管道详情
Step 3  generate_iso_drawing    — 生成图纸
Step 4  generate_iso_drawing(action=query_status) — 查进度（批量必查）
```

### 1.3 工具真实参数

**`query_material`**

| action | 必填参数 |
|--------|---------|
| `search` | `keyword` |
| `get_by_code` | `material_code` |
| `get_by_type` | `material_type` |
| `list_types` | — |
| `list_projects` | — |

`material_type`：`PIPE` / `BOLT` / `SCTN` / `SUPP`
`project_id`：`1907` / `1916` / `2016` / `2026`

**`get_pipe_info`**

| action | 必填 | 用途 |
|--------|------|------|
| `get_pipe_detail` | `pipe_name` | 管道详情 |
| `get_branch_info` | `pipe_name` 或 `branch_name` | 分支信息 |
| `get_pipe_components` | `pipe_name` | 管件列表 |
| `get_supports` | `pipe_name` | 支吊架 |
| `list_pipes` | `zone_name` | 列出区域管道 |
| `get_pipe_hierarchy` | `pipe_name` | 层级结构 |

可选：`include_attributes`（默认 true）、`include_hierarchy`（默认 false）、`limit`（默认 50）

**`generate_iso_drawing`**

```
generate_iso_drawing(action=generate, pipe_name=/PIPE-001,
                     project_id=1907, output_dir=D:/ISO输出, open_in_cad=false)

generate_iso_drawing(action=batch_generate,
                     pipe_names=[/PIPE-001,/PIPE-002], project_id=1907)
```

| 参数 | 单个 | 批量 | 说明 |
|------|:----:|:----:|------|
| `action` | ✅ | ✅ | `generate` / `batch_generate` / `query_status` |
| `pipe_name` | ✅ | — | 管道 DBURI |
| `pipe_names` | — | ✅ | DBURI 数组 |
| `project_id` | ✅ | ✅ | 见上 |
| `output_dir` | 可选 | 可选 | 默认从配置读 |
| `cad_exe_path` | 可选 | 可选 | 默认自动检测 |
| `open_in_cad` | 可选 | 可选 | 默认 false |
| `include_material_list` | 可选 | 可选 | 默认 true（受 ISO 模板控制） |
| `template_type` | 可选 | 可选 | `standard`/`detailed`/`simplified`（**预留参数，当前由 ISODRAFT 模板文件控制**） |

**约定**：
- 批量建议 ≤10 个/批（避免 AutoCAD 进程资源耗尽，工具本身无硬上限）
- 批量时 `open_in_cad` 用 false
- `output_dir` 不存在时会尝试创建

### 1.4 失败处理

| 问题 | 原因 | 处理 |
|------|------|------|
| 管道数据为空 | 路径错 | `query` 确认 DBURI |
| 材料等级缺失 | 管道未分配 Spec | 提示用户在 E3D 设置，或 `query_material` 查可用编码 |
| DXF/DWG 生成失败 | Teigha 库未加载 / AutoCAD 未装 | 检查 `lib/iso/`、提供 `cad_exe_path` |
| 图面重叠 | 管段过多 | 按管道号分批出图 |
| 批量子项失败 | — | **只重跑 failed 的，不要整批重跑** |

### 1.5 ISO 图应包含

图面主体（30°/60° 投影走向）、管段标注（长度/管径/壁厚）、焊口标记、材料表（管子/弯头/法兰/阀门/垫片/螺栓）、图框信息（图号/版本/设计/校核/批准）、方向标（北向 + 标高基准）。

---

## 二、项目材料数据的**真实**结构

**这一节纠正了旧文档的错误**，务必以此为准：

```
lib/iso/PIPE.csv    管道及管件
lib/iso/BOLT.csv    螺栓
lib/iso/SCTN.csv    型钢
lib/iso/SUPP.csv    支吊架
```

- **编码：GBK**（不是 UTF-8）。用脚本读时要显式 `encoding='gbk'`
- **层级：直接放在 `lib/iso/` 下，没有 `{project_id}` 子目录**
- **`PIPE.csv` 是管件主数据表，不是壁厚对照表**

`PIPE.csv` 真实字段（18 列）：

```
编码 | 类型 | 英文品名 | 品名 | 压力(壁厚) | 制造形式 | 连接形式 |
材料牌号 | 材料标准 | 规格标准 | RCC-M | 质保等级 | 公称直径 | 外径/Φ |
单位 | 单重 | 单重单位 | 备注
```

> ⚠️ 旧文档提到的 `spref.flnn` 字段**不存在**。查材料请用 `query_material`，不要直接读 CSV 猜字段。

**查材料的正确方式**：

```
query_material(action=get_by_type, material_type=PIPE, project_id=1907)
query_material(action=get_by_code, material_code=SPC00025, project_id=1907)
query_material(action=search, keyword=DN100, project_id=1907)
```

---

## 三、管径-壁厚（ASME B36.10M / GB/T 28897）

| DN | 外径(mm) | SCH20 | SCH40 | SCH80 |
|----|:--------:|:-----:|:-----:|:-----:|
| DN15 | 21.3 | 2.0 | 2.77 | 3.73 |
| DN20 | 26.7 | 2.0 | 2.87 | 3.91 |
| DN25 | 33.4 | 2.5 | 3.38 | 4.55 |
| DN40 | 48.3 | 2.5 | 3.68 | 5.08 |
| DN50 | 60.3 | 2.5 | 3.91 | 5.54 |
| DN80 | 88.9 | 3.0 | 5.49 | 7.62 |
| DN100 | 114.3 | 3.6 | 6.02 | 8.56 |
| DN150 | 168.3 | — | 7.11 | 11.13 |
| DN200 | 219.1 | — | 8.18 | 12.70 |
| DN300 | 323.9 | — | 10.31 | — |

> 表中 `—` 表示该组合非标准规格。SCH160 及更大口径请以 `PIPE.csv` 实际数据为准。

**生成管道参数前必查**：DN 是否在上表范围 → 壁厚等级是否有对应值 → 都没有则向用户确认，**不要编造**。

---

## 四、材料选择

```
介质类型？
├─ 水/常规流体 → 碳钢（20# / A335-P11）
├─ 蒸汽（高温） → 合金钢（A335-P22 / A335-P91）
├─ 腐蚀性介质 → 不锈钢（A312-TP304 / A312-TP316）
└─ 高压（>10MPa）→ 查 PIPE.csv 实际可用等级
```

| 温度 | 推荐材料 | 备注 |
|------|---------|------|
| < 200℃ | 碳钢 20# | 常规水管 |
| 200–425℃ | A335-P11 | 1.25Cr-0.5Mo |
| 425–580℃ | A335-P22 | 2.25Cr-1Mo |
| > 580℃ | A335-P91 | 9Cr-1Mo-V |

**AI 流程**：用户描述介质/温度/压力 → 按上表预选 → `query_material` 验证项目是否真有该材料 → 找不到则 `search` 全表搜 → 仍找不到则问用户。

---

## 五、支吊架间距

| DN | SCH40 最大间距(m) | SCH80 最大间距(m) |
|----|:----------------:|:----------------:|
| DN15 | 1.5 | 1.8 |
| DN25 | 2.0 | 2.5 |
| DN50 | 3.0 | 3.5 |
| DN100 | 4.0 | 5.0 |
| DN200 | 6.0 | 7.0 |
| DN300 | 8.0 | 9.0 |

**检查方式**：`check(type=distance)` **未实现**（返回「净距检查尚未实现」）。改用：
- `geometry(action=distance_between, element=...)` 算实际距离
- 或 `query` 取支吊架清单 + 逐个比对间距

---

## 六、改管道参数前的必检清单

调用 `piping(action=create_pipe/set_spec)` 或 `modify(attributes={DIA/WTHK/SPRE})` 前逐项过：

- [ ] DN 在管径表范围内？
- [ ] 壁厚等级有对应厚度值？
- [ ] 材料等级在 `PIPE.csv` 里存在？（用 `query_material` 验证）
- [ ] 温度/压力/介质与材料匹配？
- [ ] 支吊架间距在范围内（如已有支吊架）？

**任一项不通过 → 暂停，向用户说明，不要自行决定。**

---

## 七、改参数的安全流程

```
1. query(type=PIPE, ...)                                查目标
2. get_attributes(element=..., attributes=[DIA,WTHK,SPRE])  读当前值
3. 对照本 skill 规范表检查合规性
4. 展示计划（标注「已通过规范检查」）
5. 用户审批 → batch(preview=false) → get_attributes 复核
```

批量流程详见 `run_skill(name='e3d-element-ops')`。

---

## 八、相关资源

- 元素操作与批量安全：`run_skill(name='e3d-element-ops')`
- PML 编写：`run_skill(name='pml-authoring')`
- 属性映射表：`read_file("knowledge/domain/attribute_map.md")`
- 元素类型表：`read_file("knowledge/domain/element_types.md")`
