# structure_drawing 工具使用指南

## 工具名称
`structure_drawing`

## 工具用途
土建结构出图工具：将 E3D 结构元素(SCTN/STWL/FRMW等)导出为 CAD 二维工程图(DXF格式)。

**使用场景：**
- 用户说"结构出图"、"导出结构图"、"生成结构图纸"
- 用户需要将 SCTN(梁/柱)、STWL(墙)、FRMW(框架)等结构元素导出为 DXF
- 用户需要平面图、立面图、剖面图

## 支持的 Action

| Action | 说明 | 使用场景 |
|--------|------|----------|
| `preview` | 预览出图效果 | 用户想先看效果，不生成文件 |
| `export_plan` | 导出平面图 | 俯视图 (top) |
| `export_elevation` | 导出立面图 | 正视图/侧视图 |
| `export_section` | 导出剖面图 | 剖切视图 |
| `batch_export` | 批量导出 | 一次导出多张图纸 |

## 参数说明

### 必需参数
- `action`: 操作类型 (preview/export_plan/export_elevation/export_section/batch_export)
- `elements`: 元素名称列表，如 `["/C1101-BASE", "/SCTN-001"]`

### 可选参数
- `direction`: 投影方向 (`top`, `front`, `side`, `north`, `south`, `east`, `west`)
- `output_path`: 输出文件路径，如 `"C:\\Temp\\drawing.dxf"`
- `options`: 选项对象
  - `title`: 图纸标题
  - `scale`: 比例，如 `"1:100"`
  - `show_dimensions`: 是否显示尺寸标注 (true/false)
  - `show_hidden_lines`: 是否显示隐藏线 (true/false)
  - `title_block`: 是否显示标题栏 (true/false)

## 使用示例

### 示例 1：预览结构出图
```json
{
  "action": "preview",
  "elements": ["/C1101-BASE", "/SCTN-001"],
  "direction": "top"
}
```

### 示例 2：导出平面图
```json
{
  "action": "export_plan",
  "elements": ["/C1101-BASE", "/SCTN-001", "/STWL-001"],
  "output_path": "C:\\Temp\\structure_plan.dxf",
  "direction": "top",
  "options": {
    "title": "结构平面图",
    "scale": "1:100",
    "show_dimensions": true,
    "show_hidden_lines": true,
    "title_block": true
  }
}
```

### 示例 3：导出立面图
```json
{
  "action": "export_elevation",
  "elements": ["/C1101-BASE", "/SCTN-001"],
  "output_path": "C:\\Temp\\elevation_south.dxf",
  "direction": "south",
  "options": {
    "title": "南立面图",
    "scale": "1:100"
  }
}
```

### 示例 4：批量导出
```json
{
  "action": "batch_export",
  "output_dir": "C:\\Temp\\Drawings",
  "exports": [
    {
      "action": "export_plan",
      "file_name": "plan.dxf",
      "elements": ["/C1101-BASE"],
      "direction": "top"
    },
    {
      "action": "export_elevation",
      "file_name": "elevation.dxf",
      "elements": ["/C1101-BASE"],
      "direction": "south"
    }
  ]
}
```

## 支持的元素类型

- `SCTN`: 梁/柱 (Section)
- `STWL`: 墙 (Structural Wall)
- `FRMW`: 框架 (Framework)
- `GENSEC`: 通用截面
- `STRU`: 结构容器

## 输出文件

生成的 DXF 文件包含以下图层：
- `STRUCTURE`: 结构实线 (白色)
- `STRUCTURE-HIDDEN`: 结构隐藏线 (灰色)
- `CENTERLINE`: 中心线 (红色)
- `DIMENSION`: 尺寸标注 (绿色)
- `TEXT`: 文字 (白色)
- `TITLE-BLOCK`: 标题栏
- `FRAME`: 图框

## 注意事项

1. **元素数据提取**：工具会尝试从 E3D 提取真实数据，如果失败则使用模拟数据
2. **消隐处理**：自动根据深度排序处理隐藏线
3. **尺寸标注**：自动计算并添加总尺寸和元素间距标注
4. **图框**：自动根据内容大小选择 A3 或 A2 图框

## 常见错误处理

- 如果元素提取失败，会回退到模拟数据
- 如果输出目录不存在，会自动创建
- 如果文件已存在，会覆盖
