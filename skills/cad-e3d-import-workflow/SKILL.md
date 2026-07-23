---
name: cad-e3d-import-workflow
description: CAD→E3D导入工作流 — 选型决策树+参数规范+验证流程。从CAD导入建筑模型到E3D时必读。
runAs: inline
tags: [CAD, AutoCAD, 导入, 建筑模型, DWG, DXF]
---

# CAD → E3D 导入工作流

> 把 AutoCAD 图纸里的建筑模型（墙/柱/梁）导入到 E3D 数据库。
> 底层有两个工具：`autocad`（运行时交互）和 `cad_import`（文件/坐标解析）。
> 此 skill 解决"何时用哪个工具、参数怎么填、导入后怎么验证"。

---

## 一、工具选型决策树

```
用户要导入 CAD 数据到 E3D？
│
├─ AutoCAD 正在运行且打开了图纸？
│   ├─ 是 → 需要实时交互？
│   │       ├─ 是（用户要框选）→ autocad(action=import_selection)
│   │       └─ 否（全量按图层导入）→ autocad(action=import_all) ★推荐
│   └─ 否 → 有 DWG/DXF 文件？
│           ├─ 是 → cad_import(action=import, file_path=...)
│           └─ 否 → 只有坐标数据？
│                   └─ 是 → cad_import(action=import, paths_string=...)
```

**推荐顺序**：`autocad(import_all)` > `cad_import(file_path)` > `autocad(import_selection)` > `cad_import(paths_string)`

---

## 二、工具 A：autocad（运行时交互）

### 2.1 完整 action 枚举

| action | 用途 | 是否阻塞 | 推荐度 |
|--------|------|:--------:|:------:|
| `status` | 检查 AutoCAD 是否运行/已连接 | 否 | ★★★ |
| `connect` | 连接到运行中的 AutoCAD | 否 | ★★★ |
| `list_objects` | 列出图纸对象（按图层分组统计） | 否 | ★★★ |
| `import_all` | 全量导入（按图层过滤，非阻塞） | 否 | ★★★★★ |
| `get_selection` | 预览用户框选的对象 | 是 | ★★ |
| `import_selection` | 导入用户框选的对象 | 是 | ★★ |

### 2.2 标准导入流程（4 步）

```
Step 1: autocad(action=status)                           — 确认 AutoCAD 运行
Step 2: autocad(action=connect)                          — 建立连接
Step 3: autocad(action=list_objects)                     — 查看图层分布（可选但推荐）
Step 4: autocad(action=import_all, layer_filter=[...])   — 按图层导入
```

### 2.3 参数说明

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `action` | string | （必填） | 见上表 |
| `layer_filter` | array | 无（全部图层） | 图层名数组，如 `["WALL", "COLUMN"]` |
| `wall_height` | number | 3000 | 墙高(mm) |
| `wall_thickness` | number | 200 | 墙厚(mm) |
| `owner` | string | 新建 SITE/ZONE | 父元素路径，如 `/Copy-of-CIVIL` |
| `auto_execute` | boolean | true | 是否自动执行生成的 PML |
| `specifications` | object | /Concrete_*-SPEC | 规格名覆盖，如 `{"Wall": "/MyWall-SPEC"}` |
| `database` | string | 当前库 | 目标 DESIGN 数据库名 |

### 2.4 典型调用

```
autocad(action=status)
autocad(action=connect)
autocad(action=list_objects)
autocad(
  action=import_all,
  layer_filter=["WALL", "COLUMN"],
  wall_height=3500,
  wall_thickness=240,
  owner=/Copy-of-CIVIL,
  auto_execute=true
)
```

---

## 三、工具 B：cad_import（文件/坐标解析）

### 3.1 完整 action 枚举

| action | 用途 | 数据源 |
|--------|------|--------|
| `parse` | 解析预览（不创建 E3D 元素） | DWG/DXF 文件或坐标字符串 |
| `import` | 解析 + 生成 PML + 可选执行 | 同上 |

### 3.2 参数说明

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `action` | string | （必填） | `parse` / `import` |
| `file_path` | string | — | DWG/DXF 文件路径（与 paths_string 二选一） |
| `paths_string` | string | — | 坐标字符串：`[(x1,y1,z1),(x2,y2,z2)],...` |
| `owner` | string | /IMPORT_ZONE | 父元素路径 |
| `wall_height` | number | 3000 | 墙高(mm) |
| `wall_thickness` | number | 200 | 墙厚(mm) |
| `auto_execute` | boolean | true | import 时是否自动执行 PML |
| `specifications` | object | /Concrete_*-SPEC | 规格名覆盖 |
| `database` | string | 当前库 | 目标 DESIGN 数据库名 |

### 3.3 典型调用

**文件导入**：
```
cad_import(action=parse, file_path=D:/drawings/floorplan.dwg)
cad_import(action=import, file_path=D:/drawings/floorplan.dwg, owner=/ZONE-CIVIL, auto_execute=true)
```

**坐标字符串导入**：
```
cad_import(action=import, paths_string="[(0,0,0),(5000,0,0)],[(5000,0,0),(5000,3000,0)]", owner=/ZONE-CIVIL)
```

---

## 四、重要约束（必读）

### 4.1 只生成 Wall 类型

**当前实现限制**：两个工具的 `ConvertSegmentsToElements` 方法把所有线段都转成 `BuildingElementType.Wall`。

- 没有柱（Column）/梁（Beam）的自动识别逻辑
- `specifications` 参数支持 Wall/Column/Beam 三种 key，但实际只会生成 Wall
- 如果需要区分柱/梁，需要在 CAD 里用不同图层命名，导入后手动改类型

### 4.2 短线段过滤

长度 < 100mm 的线段会被自动忽略（`if (segment.Length < 100) continue`）。

### 4.3 共线合并

导入前会自动合并共线线段（`MergeCollinearSegments`），减少元素数量。

### 4.4 坐标系

- CAD 图纸坐标（mm）→ E3D 世界坐标（mm），1:1 直接映射
- Z 轴：CAD 的 Z 坐标通常为 0（平面图），E3D 从 Z=0 开始创建
- 墙高沿 Z 轴向上延伸

### 4.5 owner 参数

- 不提供：脚本自动 `NEW SITE` + `NEW ZONE`
- 提供 `/Copy-of-CIVIL`：在该元素下直接创建子元素，跳过 NEW SITE/ZONE
- **推荐**：提供 owner 避免创建多余的 SITE/ZONE

---

## 五、导入后验证流程

```
Step 1: query(type=ALL, scope=<owner>)        — 查询导入的元素清单
Step 2: get_attributes(element=<第一个元素>)   — 抽查属性
Step 3: check(type=exists, element=<元素>)     — 验证存在性
```

**验证示例**：
```
query(type=ALL, scope=/Copy-of-CIVIL)
→ 返回 N 个 Wall 元素

get_attributes(element=/Copy-of-CIVIL/WALL-001, attributes=[HEIGHT, THICKNESS])
→ 确认 HEIGHT=3500, THICKNESS=240

check(type=exists, element=/Copy-of-CIVIL/WALL-001)
→ 确认元素存在
```

**异常处理**：
- 导入返回成功但 query 查不到 → 检查 owner 路径是否正确
- 元素存在但属性为空 → PML 执行可能部分失败，用 `execute_pml` 重新执行返回的 pmlScript

---

## 六、错误处理

| 错误 | 原因 | 解决 |
|------|------|------|
| "AutoCAD 未运行" | AutoCAD 未启动 | 让用户启动 AutoCAD 并打开图纸 |
| "未连接到 AutoCAD" | 跳过了 connect | 先执行 `autocad(action=connect)` |
| "未选择任何对象" | import_selection 时用户没框选 | 让用户在 AutoCAD 中选择对象后重试 |
| "图纸中未找到有效线段" | layer_filter 过滤掉了所有图层 | 检查图层名是否正确，用 `list_objects` 查看实际图层名 |
| "PML 执行失败" | E3D 数据库问题 | 检查 database 参数，或用 `execute_pml` 手动执行返回的 pmlScript |
| "无法解析坐标字符串" | paths_string 格式错误 | 格式必须为 `[(x1,y1,z1),(x2,y2,z2)],...` |

---

## 七、完整对话示例

```
用户：把 D:/drawings/floorplan.dwg 导入到 E3D

AI：
[cad_import(action=parse, file_path=D:/drawings/floorplan.dwg)]
解析结果：
- 原始线段：45 条
- 将创建元素：38 个（共线合并后）
- 墙高：3000mm，墙厚：200mm

确认导入吗？目标位置 /IMPORT_ZONE，墙高 3000mm，墙厚 200mm

用户：导入到 /Copy-of-CIVIL 下，墙高改成 3500

AI：
[cad_import(action=import, file_path=D:/drawings/floorplan.dwg,
            owner=/Copy-of-CIVIL, wall_height=3500, auto_execute=true)]
✅ 导入完成：
- 已创建 38 个 Wall 元素
- 落位：/Copy-of-CIVIL
- 墙高：3500mm，墙厚：200mm

[query(type=ALL, scope=/Copy-of-CIVIL)]
验证：找到 38 个元素 ✅
```

---

## 八、相关资源

- 元素定位规范：`run_skill(name='element-resolution-guide')`
- PML 语法基础：`run_skill(name='aveva-pml-language')`
- E3D→CAD 反向导出：`run_skill(name='e3d-to-cad-export')`
- CAD 直接控制：`run_skill(name='cad-direct-control')`
