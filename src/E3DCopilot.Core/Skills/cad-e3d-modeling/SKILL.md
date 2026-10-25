---
name: cad-e3d-modeling
description: CAD 图纸转 E3D 三维模型 — 通道选型、图层过滤、构件分类、建模质量约束。从 DWG/DXF/AutoCAD 导入建墙柱梁时必读。合并原 cad-e3d-import-workflow + cad-direct-control + e3d-to-cad-export 三个 skill。
runAs: inline
tags: [CAD, DWG, E3D, 建模, 导入, 图层, 导出]
---

# CAD 图纸 → E3D 三维模型

> 这是 E小智的核心场景：二维图纸识别解析 → E3D 三维建模。
> 本文解决三件事：**选哪条通道、参数怎么填、建出来的模型像不像**。
> 底层建模走 **PML**（`NEW STWALL` / `NEW STCOLUMN` / `NEW STBEAM`），不是 C# API——这不是缺陷，是 E3D 的标准做法。

---

## 一、通道选型（先看这张）

```
要导入 CAD 到 E3D？
│
├─ 已有 DWG/DXF 文件（离线，不依赖 AutoCAD）
│   └─ ★ 首选：design_workflow（六步编排，含 AB 差异比对）
│       或 cad_import(action=import, file_path=...)
│
├─ AutoCAD 正在运行、想在当前图上操作
│   ├─ 全量按图层导入  → autocad(action=import_all, layer_filter=[...])   ★推荐
│   └─ 用户框选部分对象 → autocad(action=import_selection)
│
└─ 只有坐标数据
    └─ cad_import(action=import, paths_string="[(x1,y1,z1),(x2,y2,z2)],...")
```

| 场景 | 用什么 | 理由 |
|------|--------|------|
| 演示 / 正式导入 | `design_workflow` | 六步固定编排 + 平台台账回写 + AB 差异 |
| 只建一次模型 | `cad_import` | 直给，不经六步 |
| AutoCAD 现场交互 | `autocad` | 读当前打开的图 |
| 反向出图 | `e3d_to_cad_export` / `structure_drawing` | 见第六节 |

**不要自己拼 `glob` 找 DWG**。路径由用户提供，缺失时直接问用户。

---

## 二、参数：`cad_import` 与 `autocad`

### 2.1 `cad_import`

| 参数 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `action` | string | 必填 | `parse`（只预览）/ `import`（解析+生成 PML+可选执行） |
| `file_path` | string | — | DWG/DXF 路径，与 `paths_string` 二选一 |
| `paths_string` | string | — | `[(x1,y1,z1),(x2,y2,z2)],...` |
| **`layers`** | **array** | 全部 | **图层过滤，强烈建议给** |
| `owner` | string | `/IMPORT_ZONE` | 父元素路径，如 `/Copy-of-CIVIL` |
| `wall_height` | number | 3000 | 墙高 mm |
| `wall_thickness` | number | 200 | 墙厚 mm |
| `auto_execute` | boolean | true | 是否自动 `RunInPdms` |
| `specifications` | object | `/Concrete_*-SPEC` | 规格覆盖，如 `{"Wall": "/MyWall-SPEC"}` |
| `database` | string | 当前库 | 目标 DESIGN 库名 |

### 2.2 `autocad`

| action | 用途 | 阻塞 | 推荐度 |
|--------|------|:----:|:------:|
| `status` | 检查是否运行/已连接 | 否 | ★★★ |
| `connect` | 连接运行中的 AutoCAD | 否 | ★★★ |
| `list_objects` | 按图层分组统计对象 | 否 | ★★★ |
| `import_all` | 全量按图层导入 | 否 | ★★★★★ |
| `get_selection` | 预览框选对象 | **是** | ★★ |
| `import_selection` | 导入框选对象 | **是** | ★★ |

参数：`layer_filter`（array）、`wall_height`、`wall_thickness`、`owner`、`auto_execute`、`specifications`、`database`。

> **框选类（`get_selection` / `import_selection`）会阻塞 E3D UI 线程等用户在 AutoCAD 里框选。演示时优先用 `import_all`。**

---

## 三、图层过滤：这是效果好坏的第一决定因素

**不给图层 = 轴线、标注框、图框线全部被当成构件**。实测同一张图：

| 图层范围 | 有效线段 | 建模效果 |
|---------|:-------:|---------|
| 不给（全量） | **794 条** | 模型树里一团乱线，PML 执行慢 |
| 只给「土建」 | 268 条 | 干净 |
| 「土建」+「C1 柱墙剖线」 | **312 条** | 干净且含柱墙剖线 |

**标准做法**：

```
Step 1: cad_import(action=parse, file_path=...)          — 先看图层分布
Step 2: 挑出构件图层（土建 / WALL / COLUMN / 柱 / 梁 等）
Step 3: cad_import(action=import, ..., layers=["土建", "C1柱墙剖线"])
```

`autocad` 通道同理：先 `list_objects` 看图层，再 `import_all(layer_filter=[...])`。

---

## 四、建模质量：当前实现的真实边界

**这一段必须如实告知用户，不要承诺做不到的事。**

### 4.1 构件类型判定

`CadImportHandler.ClassifyByLayer()` 按图层名关键词判定：

```
含 "COLUMN"/"COL"/"柱"  → 柱
含 "BEAM"/"梁"          → 梁
含 "WALL"/"墙"          → 墙
其余（含「土建」）        → 墙   ← 兜底
```

**推论**：如果整张图只有「土建」一个图层，**所有线段都会建成墙**。想要柱梁，必须在 CAD 里用带「柱」「梁」关键词的图层名。

### 4.2 短线段过滤

长度 < **100mm** 的线段直接丢弃（多为图框线、标注引线、填充碎片）。

### 4.3 共线合并

导入前自动 `MergeCollinearSegments()` 合并共线线段，减少元素数量。

### 4.4 已知限制（如实说明，不要掩盖）

| 限制 | 影响 |
|------|------|
| 多边形墙未闭合 | 闭合多段线会被拆成独立直墙，**拐角处可能有缺口** |
| 柱无独立截面 | 柱按点创建，截面尺寸取默认值 |
| 只支持平面线段 | 立面/剖面图不适合此流程 |

### 4.5 坐标系

- CAD mm → E3D mm，**1:1 直接映射**
- 平面图 Z 通常为 0，墙高沿 Z 向上延伸
- `owner` 不给时会自建 `NEW SITE` + `NEW ZONE`；**推荐给 `owner`**（如 `/Copy-of-CIVIL`）避免多余层级

---

## 五、导入后验证（不要跳过）

```
Step 1: query(type=ALL, scope=<owner>)              — 查元素清单，核数量
Step 2: get_attributes(element=<第一个>, attributes=[...]) — 抽查属性
Step 3: check(type=exists, element=<某个>)           — 确认存在
```

| 异常 | 原因 | 处理 |
|------|------|------|
| 返回成功但 query 查不到 | `owner` 路径错 | 核对 owner，或改用默认 |
| 元素在但属性空 | PML 部分失败 | 用返回的 `pmlScript` 手动 `execute_pml` 重跑 |
| 数量远少于预期 | 图层过滤掉了构件层 | `parse` 先看各图层线段数 |
| 数量远多于预期 | 没给 `layers` | 加图层过滤重新导入 |

---

## 六、反向出图（E3D → CAD）

这些工具**都已真实实现**，不是占位：

| 工具 | 用途 | 关键 action |
|------|------|------------|
| `e3d_to_cad_export` | 导出 E3D 元素为 DXF/DWG | `preview` / `export_dxf` / `export_dwg` / `list_exportable` |
| `structure_drawing` | 土建结构出图 | `export_plan` / `export_elevation` / `export_section` / `batch_export` / `preview` |
| `autocad_control` | 操作运行中的 AutoCAD | `send_command` / `create_entity` / `set_layer` / `add_text` / `add_dimension` / `save_drawing` / `get_layers` / `get_entity_properties` |

**`e3d_to_cad_export` 常用参数**：`elements`（DBURI 数组）或 `scope`+`element_type`、`output_path`、`options.projection`（`plan`/`elevation`/`iso`）、`options.layer_prefix`。

**注意**：`export_dwg` 依赖 Teigha 或 AutoCAD COM；`export_dxf` 走 netDxf，更稳。演示优先 DXF。

---

## 七、完整示例

```
用户：把结构图.dwg 导入 E3D，只要土建和柱墙剖线图层，墙高 3000，墙厚 200

AI：
[cad_import(action=parse, file_path=E:\...\结构图.dwg)]
解析结果：图层 30 个，实体 699，有效线段 312 条（已过滤 <100mm）

[f] 确认用图层 ["土建","C1柱墙剖线"]，落位 /IMPORT_ZONE，墙高 3000，墙厚 200

[cad_import(action=import, file_path=..., layers=["土建","C1柱墙剖线"],
            wall_height=3000, wall_thickness=200, owner=/IMPORT_ZONE,
            auto_execute=true)]
✅ 已创建 312 个元素，落位 /IMPORT_ZONE

[query(type=ALL, scope=/IMPORT_ZONE)]
验证：找到 312 个元素 ✅（其中墙 312 / 柱 0 / 梁 0 —— 该图层名未含「柱」「梁」关键词）
```

**最后那句括号必须如实报**：如果全是墙，要主动说明「本次未识别出柱梁，因为图层名不含对应关键词」，而不是含糊说「建模完成」。
