---
name: iso-drawing-pipeline
description: ISO出图完整流程 — 从材料验证到图纸生成的4步编排。涉及 ISO 出图时先调用此 skill 获取流程和必填参数。
runAs: inline
tags: [ISO, 出图, 管道, AutoCAD]
---

# ISO 出图完整流程

> 基于 CNPE.IC.ISO 项目集成的 ISO 等轴测图生成流程。
> 核心引擎：`CNPE.ISO.E3D.Draw` → ISODRAFT 模块 → AutoCAD 格式化输出 DWG。
> 涉及 ISO 出图时，按此 4 步流程执行，避免漏环节或参数错。

---

## 一、4 步流程

```
Step 1: query_material     — 验证材料编码（可选，材料缺失时必做）
Step 2: get_pipe_info      — 提取管道详细信息
Step 3: generate_iso_drawing — 生成 ISO 图纸
Step 4: query_status       — 查询生成进度（批量出图必查）
```

**前置条件**：
- ✅ E3D 2.1 已运行并连接数据库
- ✅ AutoCAD 已安装（`generate_iso_drawing` 的 `cad_exe_path` 参数会自动检测，或从配置读取）
- ✅ 材料数据 CSV 已放在 `lib/iso/{project_id}/` 目录

**注意**：没有专门的"检测 AutoCAD 路径"工具。如果 `generate_iso_drawing` 报 AutoCAD 相关错误，让用户手动提供 `cad_exe_path` 参数。

---

## 二、Step 1：验证材料编码（可选）

```
query_material(action=get_by_type, material_type=PIPE, project_id=1907)
query_material(action=get_by_code, material_code=SPC00025, project_id=1907)
query_material(action=search, keyword=DN100, project_id=1907)
```

**action 枚举**：

| action | 用途 | 必填参数 |
|--------|------|---------|
| `search` | 按关键词搜索 | `keyword` |
| `get_by_code` | 按材料编码查 | `material_code` |
| `get_by_type` | 按类型列出 | `material_type` |
| `list_types` | 列出所有材料类型 | — |
| `list_projects` | 列出支持的项目 | — |

**material_type 枚举**：`PIPE` / `BOLT` / `SCTN` / `SUPP`

**project_id 枚举**：`1907` / `1916` / `2016` / `2026`

**何时跳过此步**：管道材料编码已知且确认存在时。

---

## 三、Step 2：提取管道信息

```
get_pipe_info(action=get_pipe_detail, pipe_name='/PIPE-001')
```

**action 枚举**：

| action | 用途 | 必填参数 |
|--------|------|---------|
| `get_pipe_detail` | 获取管道详情 | `pipe_name` |
| `get_branch_info` | 获取分支信息 | `pipe_name` 或 `branch_name` |
| `get_pipe_components` | 获取管件列表 | `pipe_name` |
| `get_supports` | 获取支吊架信息 | `pipe_name` |
| `list_pipes` | 列出管道 | `zone_name` |
| `get_pipe_hierarchy` | 获取层级结构 | `pipe_name` |

**可选参数**：`include_attributes`（默认 true）/ `include_hierarchy`（默认 false）/ `limit`（默认 50）

**用途**：确认管道数据完整后再出图，避免图纸生成失败。完整出图前建议依次调用 `get_pipe_detail` + `get_pipe_components` + `get_supports` 确认数据齐全。

---

## 四、Step 3：生成 ISO 图纸

### 单个管道

```
generate_iso_drawing(
  action=generate,
  pipe_name='/PIPE-001',
  project_id=1907,
  output_dir='D:/ISO输出',
  open_in_cad=false
)
```

### 批量生成

```
generate_iso_drawing(
  action=batch_generate,
  pipe_names=['/PIPE-001','/PIPE-002','/PIPE-003'],
  project_id=1907,
  output_dir='D:/ISO输出'
)
```

**参数说明**：

| 参数 | 单个 | 批量 | 说明 |
|------|:----:|:----:|------|
| `action` | ✅ | ✅ | `generate` / `batch_generate` / `query_status` |
| `pipe_name` | ✅ | — | 管道 DBURI |
| `pipe_names` | — | ✅ | 管道 DBURI 数组 |
| `project_id` | ✅ | ✅ | `1907`/`1916`/`2016`/`2026` |
| `output_dir` | 可选 | 可选 | 默认从配置读取 |
| `cad_exe_path` | 可选 | 可选 | 默认自动检测或从配置读取 |
| `open_in_cad` | 可选 | 可选 | 默认 false |

**约定**：
- 批量上限：建议不超过 10 个/批（避免 AutoCAD 进程资源耗尽，工具本身无硬上限）
- `output_dir` 不存在时工具会尝试创建
- 批量场景 `open_in_cad` 建议 false

---

## 五、Step 4：查询进度

```
generate_iso_drawing(action=query_status)
```

**批量出图必查**：返回每个管道的生成状态。

**失败重试**：对 failed 的管道单独重新 `generate`，**不要整批重跑**。

---

## 六、完整对话示例

```
用户：帮我把 /PIPE-001/ 出 ISO 图

AI：
[调用 get_pipe_info(pipe_name=/PIPE-001, project_id=1907)]
管道 /PIPE-001 信息：
- 分支：3 个（BRAN-001/002/003）
- 管件：12 个
- 支吊架：4 个

[调用 generate_iso_drawing(action=generate, pipe_name=/PIPE-001, project_id=1907,
                            output_dir=D:/ISO输出)]
✅ ISO 图纸已生成：D:/ISO输出/PIPE-001.dwg
```

**材料缺失场景**：

```
用户：/PIPE-002/ 出 ISO 图，但材料编码不全

AI：
[调用 get_pipe_info(action=get_pipe_components, pipe_name=/PIPE-002)]
管道 /PIPE-002 有 2 个管件的材料编码缺失

[调用 query_material(action=search, keyword=DN100, project_id=1907)]
找到匹配材料：SPC00025 (DN100/SCH40)

[调用 generate_iso_drawing(...)]
✅ ISO 图纸已生成（已自动补全材料编码）
```

---

## 七、相关资源

- 完整使用指南：`read_file("docs/guides/ISO出图功能使用指南.md")`
- 管道设计规范：`run_skill(name='piping-design-standards')`
- 元素定位规范：`run_skill(name='element-resolution-guide')`
