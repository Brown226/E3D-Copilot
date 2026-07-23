---
name: piping-design-standards
description: 管道设计规范约束 — 管径/壁厚/材料等级的行业规则。设计管道或改管道参数前先查此 skill。
runAs: inline
tags: [管道, 规范, 壁厚, 材料]
---

# 管道设计规范约束

> AI 生成管道设计 PML 或修改管道参数前，必须先查此 skill 确保工程参数合规。
> 语法对 ≠ 工程对。下面是行业通用规则，项目级规范以 `lib/iso/{project_id}/` 下 CSV 为准。
>
> ⚠️ **占位说明**：以下规范数值为通用骨架，具体数值需按项目标准填充。
> 当前用 `[待填]` 标记的位置，需要设计院提供实际数据后替换。

---

## 一、管径-壁厚对应表

> 标准：ASME B36.10M / GB/T 28897
> 常用壁厚等级：SCH20 / SCH40 / SCH80 / SCH160

| DN | 外径(mm) | SCH20 | SCH40 | SCH80 | SCH160 |
|----|---------|-------|-------|-------|--------|
| DN15 | 21.3 | 2.0 | 2.77 | 3.73 | [待填] |
| DN20 | 26.7 | 2.0 | 2.87 | 3.91 | [待填] |
| DN25 | 33.4 | 2.5 | 3.38 | 4.55 | [待填] |
| DN40 | 48.3 | 2.5 | 3.68 | 5.08 | [待填] |
| DN50 | 60.3 | 2.5 | 3.91 | 5.54 | [待填] |
| DN80 | 88.9 | 3.0 | 5.49 | 7.62 | [待填] |
| DN100 | 114.3 | 3.6 | 6.02 | 8.56 | [待填] |
| DN150 | 168.3 | [待填] | 7.11 | 11.13 | [待填] |
| DN200 | 219.1 | [待填] | 8.18 | 12.7 | [待填] |
| DN300 | 323.9 | [待填] | 10.31 | [待填] | [待填] |
| DN500 | 508.0 | [待填] | [待填] | [待填] | [待填] |

**AI 生成 piping 操作前的检查**：
- 用户指定的 DN 是否在上表范围
- 壁厚等级是否存在对应厚度值
- 不存在则向用户确认

---

## 二、材料等级规则

> 材料等级 = 温度 + 压力 + 介质 → 选材
> 项目级材料数据：`lib/iso/{project_id}/PIPE.csv`

### 2.1 选材决策（简化版）

```
介质类型？
├─ 水/常规流体 → 碳钢（A335-P11 / 20#）
├─ 蒸汽（高温） → 合金钢（A335-P22 / A335-P91）
├─ 腐蚀性介质 → 不锈钢（A312-TP304 / A312-TP316）
└─ 高压（>10MPa）→ [待填]
```

### 2.2 温度-材料对应

| 温度范围 | 推荐材料 | 备注 |
|---------|---------|------|
| < 200℃ | 碳钢 20# | 常规水管 |
| 200-425℃ | A335-P11 | 1.25Cr-0.5Mo |
| 425-580℃ | A335-P22 | 2.25Cr-1Mo |
| > 580℃ | A335-P91 | 9Cr-1Mo-V |

> 项目实际材料等级清单在 `PIPE.csv` 的 `spref.flnn` 字段。

### 2.3 查询项目材料数据

用 `query_material` 工具（action 枚举：`search` / `get_by_code` / `get_by_type` / `list_types` / `list_projects`）：

```
query_material(action=get_by_type, material_type=PIPE, project_id=1907)
query_material(action=get_by_code, material_code=SPC00025, project_id=1907)
query_material(action=search, keyword=DN100, project_id=1907)
```

**material_type 枚举**：`PIPE` / `BOLT` / `SCTN` / `SUPP`
**project_id 枚举**：`1907` / `1916` / `2016` / `2026`

**AI 流程**：
1. 用户描述介质/温度/压力
2. 按上表预选材料
3. `query_material(action=get_by_type)` 验证项目是否有此材料
4. 找不到则 `query_material(action=search, keyword=...)` 全表搜索

---

## 三、支吊架间距规则

> 最大间距按管径和壁厚等级决定，避免管道下垂/振动。

| DN | SCH40 最大间距(m) | SCH80 最大间距(m) |
|----|:----------------:|:----------------:|
| DN15 | 1.5 | 1.8 |
| DN25 | 2.0 | 2.5 |
| DN50 | 3.0 | 3.5 |
| DN100 | 4.0 | 5.0 |
| DN200 | 6.0 | 7.0 |
| DN300 | 8.0 | 9.0 |

> 其他壁厚等级和 DN 的间距：[待填]

**AI 检查流程**：
- 用户布置支吊架后，可用 `check(type=distance, element=...)` 检查间距
- 间距超过上表 → 报警"支吊架间距过大"
- 推荐增加支吊架位置

---

## 四、设计前必检清单

AI 生成 `piping(action=create_pipe / create_branch / add_fitment / set_spec)` 或 `modify(dburi=..., attributes={WTHK/DIA/SPRE:...})` 调用前，逐项检查：

- [ ] DN 在管径表范围内？
- [ ] 壁厚等级有对应厚度值？
- [ ] 材料等级在 `PIPE.csv` 里存在？（用 `query_material` 验证）
- [ ] 温度/压力/介质与材料匹配？
- [ ] 支吊架间距在范围内（如已有支吊架）？

任一项不通过 → 暂停执行，向用户说明并询问如何处理。

---

## 五、修改管道参数的安全流程

涉及管道参数修改时，结合 `run_skill(name='batch-modify-workflow')` 执行：

```
1. query(type=PIPE, ...)                         — 查目标管道
2. get_attributes(element=..., attributes=[DIA,WTHK,SPRE])  — 读当前值
3. 对照本 skill 的规范表检查合规性
4. 展示计划（含"已通过规范检查"标注）
5. 用户审批 → batch(preview=false) → get_attributes 复核
```

---

## 六、相关资源

- 属性映射表：`read_file("knowledge/domain/attribute_map.md")`
- 元素类型表：`read_file("knowledge/domain/element_types.md")`
- 项目材料数据：`lib/iso/{project_id}/PIPE.csv`
- 批量修改流程：`run_skill(name='batch-modify-workflow')`
- 元素定位规范：`run_skill(name='element-resolution-guide')`
