---
name: e3d-to-cad-export
description: E3D→CAD反向导出 — 把E3D元素(管道/设备/结构)导出为DXF/DWG。依赖未实现的 e3d_to_cad_export 工具。
runAs: inline
tags: [E3D, CAD, 导出, DXF, DWG, 管道, 设备]
---

# E3D → CAD 反向导出

> ⚠️ **前置说明**：此 skill 依赖未实现的 `e3d_to_cad_export` 工具。
> 当前只有 CAD→E3D 单向导入（autocad/cad_import），没有反向导出能力。
> 需要新建 `CadExportService` + 新增 `E3dToCadExportHandler`。
> 本 skill 定义行为规范，C# 实现后即可按此 skill 编排。

---

## 一、能力边界

| 方向 | 能力 | 现状 |
|------|------|:----:|
| CAD → E3D | 从 DWG/DXF 导入建筑模型 | ✅ 已有 |
| CAD → E3D | 从运行中 AutoCAD 导入 | ✅ 已有 |
| E3D → CAD | 导出元素到 DXF 文件 | ⚠️ 待实现 |
| E3D → CAD | 导出元素到 DWG 文件 | ⚠️ 待实现 |
| E3D → CAD | 导出到运行中 AutoCAD | ⚠️ 待实现 |

---

## 二、待实现工具签名：e3d_to_cad_export

> C# 开发时按此签名实现 `E3dToCadExportHandler` 并注册到 `ToolExecutor.CreateDefault`。

### 2.1 ParameterSchema

```json
{
  "type": "object",
  "properties": {
    "action": {
      "type": "string",
      "enum": ["export_dxf", "export_dwg", "preview", "list_exportable"]
    },
    "elements": {
      "type": "array",
      "items": { "type": "string" },
      "description": "E3D 元素 DBURI 数组"
    },
    "scope": {
      "type": "string",
      "description": "查询范围（与 elements 二选一），如 /ZONE-PIPE-01"
    },
    "element_type": {
      "type": "string",
      "description": "元素类型过滤（scope 模式用），如 PIPE/EQUI/STRU/ALL"
    },
    "output_path": {
      "type": "string",
      "description": "输出文件路径（.dxf 或 .dwg）"
    },
    "options": {
      "type": "object",
      "properties": {
        "layer_prefix": { "type": "string", "description": "图层名前缀，默认空" },
        "include_text": { "type": "boolean", "description": "是否包含标注文字，默认 true" },
        "include_dimensions": { "type": "boolean", "description": "是否包含尺寸标注，默认 true" },
        "scale": { "type": "number", "description": "比例，默认 1.0" },
        "projection": {
          "type": "string",
          "enum": ["plan", "elevation", "iso"],
          "description": "投影方式，默认 plan（平面图）"
        }
      }
    }
  },
  "required": ["action"]
}
```

### 2.2 底层 C# 方法签名

```csharp
// 新建 CadExportService.cs
public class CadExportService
{
    public ExportResult ExportToDxf(List<E3DElementInfo> elements, string outputPath,
                                    ExportOptions options = null);
    public ExportResult ExportToDwg(List<E3DElementInfo> elements, string outputPath,
                                    ExportOptions options = null);
}

public class ExportOptions
{
    public string LayerPrefix { get; set; } = "";
    public bool IncludeText { get; set; } = true;
    public bool IncludeDimensions { get; set; } = true;
    public double Scale { get; set; } = 1.0;
    public string Projection { get; set; } = "plan";  // plan/elevation/iso
}

public class E3DElementInfo
{
    public string Dburi { get; set; }
    public string Type { get; set; }       // PIPE/EQUI/STRU/BRAN
    public string Name { get; set; }
    public Point3D Position { get; set; }
    public Point3D EndPosition { get; set; }  // 管道用
    public Dictionary<string, string> Attributes { get; set; }
}
```

---

## 三、导出场景路由

### 3.1 按元素类型映射 CAD 实体

| E3D 元素 | CAD 实体 | 图层 | 标注内容 |
|---------|---------|------|---------|
| PIPE | Polyline（起点→终点） | `PIPE-DN{DIA}` | `DN{DIA} / {NAME}` |
| EQUI | BlockReference 或矩形 | `EQUI-{TYPE}` | `{PNO} / {DESC}` |
| STRU | Line（轮廓线） | `STRU-{TYPE}` | `{NAME}` |
| BRAN | Polyline（分段） | `BRAN-DN{DIA}` | `DN{DIA} / {SPRE}` |
| SUPP | Circle 或 BlockReference | `SUPP` | `{TYPE}` |

### 3.2 投影方式

| projection | 说明 | 用途 |
|-----------|------|------|
| `plan` | 平面投影，取 X/Y，忽略 Z | 平面布置图 |
| `elevation` | 立面投影，取 X/Z，忽略 Y | 立面图 |
| `iso` | 等轴测投影，30° 旋转 | ISO 图 |

---

## 四、标准导出流程

### 4.1 按元素列表导出

```
Step 1: e3d_to_cad_export(action=preview, elements=[/PIPE-001, /EQUI-001])
        — 预览将要导出的元素和 CAD 实体映射
Step 2: 向用户展示预览结果
Step 3: e3d_to_cad_export(action=export_dxf,
        elements=[/PIPE-001, /EQUI-001],
        output_path=D:/output/drawing.dxf,
        options={projection=plan, include_text=true})
Step 4: 验证文件生成成功
```

### 4.2 按范围批量导出

```
Step 1: e3d_to_cad_export(action=list_exportable, scope=/ZONE-PIPE-01, element_type=PIPE)
        — 列出可导出的元素
Step 2: 向用户展示元素数量和类型分布
Step 3: e3d_to_cad_export(action=export_dwg,
        scope=/ZONE-PIPE-01,
        element_type=PIPE,
        output_path=D:/output/zone-pipe-01.dwg,
        options={layer_prefix=ZONE01-, projection=plan})
Step 4: 验证文件
```

---

## 五、图层命名规范

导出时自动按元素类型和属性创建图层：

```
{layer_prefix}{类型}-{属性}
```

**示例**（layer_prefix="ZONE01-"）：

| 图层名 | 包含内容 |
|--------|---------|
| `ZONE01-PIPE-DN100` | DN100 管道 |
| `ZONE01-PIPE-DN150` | DN150 管道 |
| `ZONE01-EQUI-PUMP` | 泵类设备 |
| `ZONE01-STRU-BEAM` | 结构梁 |
| `ZONE01-BRAN-DN100` | DN100 分支 |
| `ZONE01-SUPP` | 支吊架 |
| `ZONE01-DIM` | 标注 |
| `ZONE01-TITLE` | 图框 |

---

## 六、坐标系转换

### 6.1 坐标映射

| 方向 | E3D | CAD | 转换 |
|------|-----|-----|------|
| 平面(plan) | X, Y, Z(mm) | X, Y(mm) | X→X, Y→Y, 忽略 Z |
| 立面(elevation) | X, Y, Z(mm) | X, Z(mm) | X→X, Z→Y, 忽略 Y |
| 等轴测(iso) | X, Y, Z(mm) | X', Y'(mm) | 30° 等轴测变换 |

### 6.2 比例

- `scale=1.0`：1:1（默认，mm 对 mm）
- `scale=0.001`：1:1000（mm 对 m）
- 导出后 CAD 图纸单位为 mm

### 6.3 原点

- 默认原点对齐：E3D (0,0,0) → CAD (0,0)
- 可选：以第一个元素为原点（减少坐标值大小）

---

## 七、完整对话示例

```
用户：把 /ZONE-PIPE-01/ 下的管道导出到 CAD

AI：
[e3d_to_cad_export(action=list_exportable, scope=/ZONE-PIPE-01, element_type=PIPE)]
可导出元素：
- PIPE：12 条
- DN100：5 条
- DN150：4 条
- DN200：3 条

确认导出吗？输出格式 DWG，平面投影

用户：导出到 D:/output/zone-pipe-01.dwg

AI：
[e3d_to_cad_export(action=export_dwg,
  scope=/ZONE-PIPE-01,
  element_type=PIPE,
  output_path=D:/output/zone-pipe-01.dwg,
  options={projection=plan, include_text=true, layer_prefix=ZONE01-})]
✅ 导出完成：
- 文件：D:/output/zone-pipe-01.dwg
- 元素数：12 个
- 图层：ZONE01-PIPE-DN100(5), ZONE01-PIPE-DN150(4), ZONE01-PIPE-DN200(3)
- 标注：已添加 DN 和 NAME 标注
```

---

## 八、错误处理

| 错误 | 原因 | 解决 |
|------|------|------|
| "元素不存在" | DBURI 无效 | 先 `query` 确认元素存在 |
| "输出路径不可写" | 目录不存在/无权限 | 检查 output_path 目录权限 |
| "元素无可导出的几何信息" | 元素无坐标属性 | 检查元素是否有 POSITION/坐标属性 |
| "导出超时" | 元素过多 | 分批导出，每次不超过 100 个 |
| "DWG 格式不支持" | Teigha 版本不兼容 | 改用 DXF 格式 |

---

## 九、性能约定

- 单次导出上限：建议不超过 500 个元素
- 超过 500 时分批：按 ZONE 或类型分批导出
- preview action 不生成文件，只返回映射表，用于预检查

---

## 十、相关资源

- CAD→E3D 导入流程：`run_skill(name='cad-e3d-import-workflow')`
- CAD 直接控制：`run_skill(name='cad-direct-control')`
- 元素定位规范：`run_skill(name='element-resolution-guide')`
- 管道设计规范：`run_skill(name='piping-design-standards')`
