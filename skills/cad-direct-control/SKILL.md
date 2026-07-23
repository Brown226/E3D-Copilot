---
name: cad-direct-control
description: CAD直接控制编排 — 图层管理/图框生成/自动标注/几何绘制的命令编排。依赖未实现的 autocad_control 工具。
runAs: inline
tags: [CAD, AutoCAD, 直接控制, 图层, 图框, 标注, 绘制]
---

# CAD 直接控制编排

> ⚠️ **前置说明**：此 skill 依赖未实现的 `autocad_control` 工具。
> 当前 `autocad` 工具只能读 CAD 数据（status/connect/list_objects/import），
> 不能发命令、创建实体、改图层、标注。
> 需要先扩展 `AutoCadComService` + 新增 `AutoCadControlHandler`。
> 本 skill 定义行为规范，C# 实现后即可按此 skill 编排。

---

## 一、能力边界

| 能力 | 现状 | 实现方式 |
|------|:----:|---------|
| 连接 AutoCAD | ✅ 已有 | `autocad(action=connect)` |
| 读图纸对象 | ✅ 已有 | `autocad(action=list_objects)` |
| 导入到 E3D | ✅ 已有 | `autocad(action=import_all)` |
| 发送命令行 | ⚠️ 待实现 | `autocad_control(action=send_command)` |
| 创建实体 | ⚠️ 待实现 | `autocad_control(action=create_entity)` |
| 图层管理 | ⚠️ 待实现 | `autocad_control(action=set_layer)` |
| 添加文字 | ⚠️ 待实现 | `autocad_control(action=add_text)` |
| 尺寸标注 | ⚠️ 待实现 | `autocad_control(action=add_dimension)` |
| 保存图纸 | ⚠️ 待实现 | `autocad_control(action=save_drawing)` |

---

## 二、待实现工具签名：autocad_control

> C# 开发时按此签名实现 `AutoCadControlHandler` 并注册到 `ToolExecutor.CreateDefault`。

### 2.1 ParameterSchema

```json
{
  "type": "object",
  "properties": {
    "action": {
      "type": "string",
      "enum": ["send_command", "create_entity", "set_layer", "add_text",
               "add_dimension", "save_drawing", "get_layers", "get_entity_properties"]
    },
    "command": { "type": "string", "description": "AutoCAD 命令行字符串（send_command 用）" },
    "entity_type": {
      "type": "string",
      "enum": ["Line", "Circle", "Polyline", "Arc", "Text", "BlockReference"],
      "description": "实体类型（create_entity 用）"
    },
    "points": {
      "type": "array",
      "items": { "type": "array", "items": { "type": "number" } },
      "description": "坐标点数组 [[x,y,z],...]"
    },
    "layer": { "type": "string", "description": "图层名" },
    "color": { "type": "integer", "description": "ACI 颜色号 0-256" },
    "linetype": { "type": "string", "description": "线型名" },
    "text": { "type": "string", "description": "文字内容（add_text 用）" },
    "height": { "type": "number", "description": "文字高度/尺寸（mm）" },
    "rotation": { "type": "number", "description": "旋转角度（度）" },
    "dim_type": {
      "type": "string",
      "enum": ["Aligned", "Linear", "Angular"],
      "description": "标注类型（add_dimension 用）"
    },
    "file_path": { "type": "string", "description": "保存路径（save_drawing 用，空则保存当前）" },
    "element_handle": { "type": "string", "description": "实体 Handle（get_entity_properties 用）" }
  },
  "required": ["action"]
}
```

### 2.2 底层 C# 方法签名

```csharp
// AutoCadComService.cs 扩展
public ToolResult SendCommand(string command);
public ToolResult CreateEntity(string type, List<Point3D> points, string layer,
                               Dictionary<string, object> properties = null);
public ToolResult SetLayer(string name, int? color = null, string linetype = null,
                           bool? frozen = null, bool? locked = null);
public ToolResult AddText(Point3D position, string content, double height,
                          string layer = null, double rotation = 0);
public ToolResult AddDimension(string type, Point3D start, Point3D end,
                               Point3D dimLinePos, string layer = null);
public ToolResult SaveDrawing(string path = null);
public List<string> GetLayers();
public Dictionary<string, object> GetEntityProperties(string handle);
```

---

## 三、场景 1：图层批量管理

### 3.1 典型需求

- 按 E3D 元素类型同步 CAD 图层（PIPE→PIPE 图层，EQUI→EQUI 图层）
- 批量改图层颜色/线型
- 冻结/锁定特定图层

### 3.2 编排流程

```
Step 1: autocad(action=status) + autocad(action=connect)
Step 2: autocad_control(action=get_layers)                        — 获取现有图层
Step 3: autocad_control(action=set_layer, name="PIPE-DN100",
        color=1, linetype="Continuous")                           — 创建/修改图层
Step 4: 循环 set_layer 直到所有目标图层创建完毕
```

### 3.3 图层命名规范

| 图层名 | 颜色 | 用途 |
|--------|:----:|------|
| `PIPE-DN{N}` | 1（红） | 管道，按管径分图层 |
| `EQUI-{TYPE}` | 2（黄） | 设备，按类型分图层 |
| `STRU-{TYPE}` | 3（绿） | 结构 |
| `BRAN` | 5（蓝） | 分支 |
| `SUPP` | 6（紫） | 支吊架 |
| `DIM` | 7（白） | 标注 |
| `TITLE` | 7（白） | 图框/标题栏 |

---

## 四、场景 2：标准图框生成

### 4.1 典型需求

按 A0/A1/A2/A3 图幅生成图框 + 标题栏 + 会签栏。

### 4.2 编排流程

```
Step 1: autocad(action=connect)
Step 2: autocad_control(action=set_layer, name="TITLE", color=7)
Step 3: autocad_control(action=create_entity, entity_type=Polyline,
        points=[[0,0,0],[W,0,0],[W,H,0],[0,H,0],[0,0,0]], layer="TITLE")  — 外框
Step 4: autocad_control(action=create_entity, entity_type=Polyline,
        points=[[m,m,0],[W-m,H-m,0],...], layer="TITLE")                  — 内框
Step 5: autocad_control(action=add_text, position=[W-100, H-50, 0],
        text="项目名称", height=5, layer="TITLE")                         — 标题
Step 6: 循环 add_text 填充标题栏其他字段
Step 7: autocad_control(action=save_drawing)
```

### 4.3 图幅尺寸表

| 图幅 | W(mm) | H(mm) | 内框边距 |
|------|:-----:|:-----:|:--------:|
| A0 | 1189 | 841 | 10 |
| A1 | 841 | 594 | 10 |
| A2 | 594 | 420 | 10 |
| A3 | 420 | 297 | 5 |
| A4 | 297 | 210 | 5 |

---

## 五、场景 3：自动标注

### 5.1 典型需求

从 E3D 元素属性生成 CAD 标注（管道直径、设备位号、标高等）。

### 5.2 编排流程

```
Step 1: query(type=PIPE, scope=...)                               — 查 E3D 元素
Step 2: get_attributes(element=..., attributes=[DIA, NAME])       — 读属性
Step 3: autocad(action=connect)
Step 4: autocad_control(action=set_layer, name="DIM", color=7)
Step 5: 对每个元素:
        autocad_control(action=add_text,
          position=[元素X, 元素Y+200, 0],
          text="DN100 / PIPE-001",
          height=3, layer="DIM")
Step 6: autocad_control(action=save_drawing)
```

### 5.3 标注内容规范

| 元素类型 | 标注格式 | 示例 |
|---------|---------|------|
| PIPE | `DN{DIA} / {NAME}` | `DN100 / PIPE-001` |
| EQUI | `{PNO} / {DESC}` | `P-101 / 离心泵` |
| BRAN | `DN{DIA} / {SPRE}` | `DN100 / A335-P11` |
| SUPP | `{TYPE}` | `SPRING` |

---

## 六、场景 4：几何绘制

### 6.1 典型需求

在 CAD 里画线、圆、多段线等几何图形（如管道走向、设备轮廓）。

### 6.2 编排流程

```
Step 1: autocad(action=connect)
Step 2: autocad_control(action=set_layer, name="PIPE-DN100", color=1)
Step 3: autocad_control(action=create_entity, entity_type=Line,
        points=[[0,0,0],[5000,0,0]], layer="PIPE-DN100")
Step 4: autocad_control(action=create_entity, entity_type=Circle,
        points=[[5000,0,0]], height=50, layer="EQUI-PUMP")
Step 5: autocad_control(action=save_drawing)
```

### 6.3 坐标系转换

- E3D 世界坐标(mm) → CAD 图纸坐标(mm)，1:1 映射
- 3D → 2D 平面投影：取 X/Y，忽略 Z（或按标高分图层）
- 角度：E3D 用弧度，CAD 用度，转换时 ×180/π

---

## 七、通用错误处理

| 错误 | 原因 | 解决 |
|------|------|------|
| "AutoCAD 未运行" | AutoCAD 未启动 | 先 `autocad(action=status)` 检查 |
| "COM 连接失败" | AutoCAD 忙/权限不足 | 重试 `autocad(action=connect)` |
| "图层不存在" | set_layer 未先创建 | 先 `set_layer(name=...)` 创建图层 |
| "实体创建失败" | 坐标/参数错误 | 检查 points 格式 `[[x,y,z],...]` |
| "命令执行超时" | 命令阻塞等待用户输入 | 避免 `send_command` 发交互式命令 |

---

## 八、安全约定

1. **修改前先备份**：`autocad_control(action=save_drawing, file_path=备份路径)`
2. **不删除实体**：当前设计不提供 delete_entity（避免误删）
3. **不修改系统图层**：0 层和 DEFPOINTS 层不可修改
4. **坐标范围检查**：坐标值超过 ±1000000mm 时警告（可能是单位错误）

---

## 九、相关资源

- CAD→E3D 导入流程：`run_skill(name='cad-e3d-import-workflow')`
- E3D→CAD 反向导出：`run_skill(name='e3d-to-cad-export')`
- 元素定位规范：`run_skill(name='element-resolution-guide')`
- 管道设计规范：`run_skill(name='piping-design-standards')`
