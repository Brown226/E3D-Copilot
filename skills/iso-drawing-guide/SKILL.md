---
name: iso-drawing-guide
description: ISO 管道轴测图出图流程指南 — 从 E3D 模型提取管道数据并生成 ISO 图的标准流程。涉及出图/ISO/轴测图时调用此 skill。
runAs: inline
tags: [ISO, 出图, 轴测图, 管道, DXF]
---

# ISO 管道轴测图出图流程指南

> ISO 图（Isometric Drawing）是管道施工的核心交付物。
> 本流程使用 E小智内置的 ISO 出图工具链完成。

---

## 一、出图工具链概览

| 工具 | 用途 | 安全级别 |
|------|------|----------|
| `generate_iso_drawing` | 从 E3D 管道生成 ISO 轴测图 | L0（只读） |
| `query_material` | 查询管道材料等级/壁厚 | L0（只读） |
| `get_pipe_info` | 获取管道几何信息（起点/终点/管径） | L0（只读） |
| `structure_drawing` | 土建结构出图 | L0（只读） |

---

## 二、标准出图流程

### Step 1: 确认出图范围
```
用户指定：
- 管道编号/区域（如 /MDS/PIPES/PIPE-001 或 "所有 DN100 管道"）
- 出图比例（默认 1:1）
- 图框标准（默认 A3）
```

### Step 2: 查询管道数据
使用 `get_pipe_info` 获取：
```json
{"tool": "get_pipe_info", "args": {"element_name": "/MDS/PIPES/PIPE-001"}}
```
返回：管道路由、管径/壁厚、保温层、阀门/法兰位置

### Step 3: 查询材料信息
使用 `query_material` 获取：
```json
{"tool": "query_material", "args": {"element_name": "/MDS/PIPES/PIPE-001", "query_type": "spec"}}
```
返回：材料等级、壁厚表、法兰等级

### Step 4: 生成 ISO 图
使用 `generate_iso_drawing`：
```json
{"tool": "generate_iso_drawing", "args": {"pipe_names": ["/MDS/PIPES/PIPE-001", "/MDS/PIPES/PIPE-002"], "scale": "1:1", "paper_size": "A3"}}
```
输出：DXF 文件路径 + 材料汇总表 + 焊口统计表

### Step 5: 输出结果
- DXF 文件路径
- 材料汇总表（BOM）
- 焊口统计表

---

## 三、ISO 图内容标准

一张完整的 ISO 图应包含：

| 区域 | 内容 |
|------|------|
| 图面主体 | 管道轴测走向（30°/60° 投影） |
| 管段标注 | 每段管子的长度、管径、壁厚 |
| 焊口标记 | 每个焊接点的编号和类型 |
| 材料表 | 管子/弯头/法兰/阀门/垫片/螺栓 |
| 图框信息 | 图号、版本、设计/校核/批准 |
| 方向标 | 北方向 + 标高基准 |

---

## 四、常见问题处理

| 问题 | 原因 | 解决 |
|------|------|------|
| 管道数据为空 | 元素路径错误 | 用 `query` 工具确认路径 |
| 材料等级缺失 | 管道未分配 Spec | 提示用户在 E3D 中设置 |
| DXF 生成失败 | Teigha 库未加载 | 检查 lib/Teigha/ 目录 |
| 图面重叠 | 管段过多 | 建议分管道号出图 |

---

## 五、PML 辅助查询（可选）

当工具链不满足时，可用 PML 直接查询：
```pml
-- 获取管道所有管段
var !pipe ref /MDS/PIPES/PIPE-001
var !segs coll all TUBE for $!pipe
DO !seg values !segs
    var !p1 !seg.Position
    var !p2 !seg.Position2
    -- 输出: 起点 (!p1.East, !p1.North, !p1.Up)
    -- 输出: 终点 (!p2.East, !p2.North, !p2.Up)
ENDDO
```

参考：`read_file("knowledge/patterns/geometry_operations.md")`
