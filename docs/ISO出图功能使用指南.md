# E小智 ISO出图功能使用指南

> **版本**: v2.2.0  
> **更新日期**: 2026-06-26  
> **功能来源**: 集成 CNPE.IC.ISO 项目  
> **核心引擎**: `CNPE.ISO.E3D.Draw` → ISODRAFT 模块 → AutoCAD WCF 格式化

---

## 功能概述

ISO出图功能是E小智的新增核心功能，基于CNPE.IC.ISO项目实现，支持从E3D管道数据自动生成ISO等轴测图。

### 核心能力

- ✅ **ISO图纸生成**：从E3D管道数据生成标准ISO图纸
- ✅ **材料查询**：查询管道材料编码和规格信息
- ✅ **管道信息提取**：从E3D提取管道详细信息
- ✅ **AutoCAD进程管理**：管理AutoCAD进程的启动、关闭、通信
- ✅ **自动路径检测**：自动检测用户电脑上的AutoCAD安装路径

---

## 前置准备

### 1. 环境要求

- ✅ E3D 2.1 软件正在运行
- ✅ E小智插件已正确安装和加载
- ✅ AutoCAD软件已安装（用于ISO出图）
- ✅ 材料数据文件已复制到 `lib/iso/` 目录

### 2. 首次使用配置

首次使用ISO出图功能时，需要配置AutoCAD路径：

```
用户：检测AutoCAD安装路径

AI：我来为您检测AutoCAD安装路径。
[调用 manage_autocad detect_path]

AI：检测到AutoCAD安装路径: C:\Program Files\Autodesk\AutoCAD 2024\acad.exe
是否保存此路径到配置？

用户：是的

[调用 manage_autocad save_path]

AI：配置完成！现在可以使用ISO出图功能了。
```

---

## 工具列表

### 1. generate_iso_drawing - ISO图纸生成

**功能**：从E3D管道数据生成ISO等轴测图

**参数**：

| 参数 | 类型 | 必填 | 说明 |
|------|------|:----:|------|
| `action` | string | ✅ | 操作类型：`generate` / `batch_generate` / `query_status` |
| `pipe_name` | string | | 管道名称（单个生成时必需） |
| `pipe_names` | string[] | | 管道名称列表（批量生成时必需） |
| `project_id` | string | | 项目编号：`1907` / `1916` / `2016` / `2026` |
| `output_dir` | string | | 输出目录路径 |
| `cad_exe_path` | string | | AutoCAD可执行文件路径 |
| `include_material_list` | bool | | 是否包含材料清单（由ISO模板控制，预留参数） |
| `template_type` | string | | 模板类型（预留参数，当前由ISODRAFT模板文件控制） |
| `open_in_cad` | bool | | 生成完成后是否自动用AutoCAD打开DWG文件，默认 `false` |

**JSON 参数示例**：

```json
// 单个管道生成
{
  "action": "generate",
  "pipe_name": "/PIPE-1001",
  "project_id": "1907",
  "output_dir": "D:\\ISO出图结果",
  "cad_exe_path": "C:\\Program Files\\Autodesk\\AutoCAD 2024\\acad.exe",
  "open_in_cad": true
}

// 批量生成
{
  "action": "batch_generate",
  "pipe_names": ["/PIPE-1001", "/PIPE-1002", "/PIPE-1003"],
  "project_id": "1907",
  "output_dir": "D:\\ISO出图结果"
}

// 查询生成状态
{
  "action": "query_status",
  "output_dir": "D:\\ISO出图结果"
}
```

**自然语言示例**：

```
# 单个管道生成
用户：为管道PIPE-1001生成ISO图纸，项目编号1907

# 批量生成
用户：为以下管道批量生成ISO图：PIPE-1001, PIPE-1002, PIPE-1003

# 查询生成状态
用户：查看ISO出图目录的状态
```

---

### 2. query_material - 材料查询

**功能**：查询管道材料编码和规格信息

**参数**：

| 参数 | 类型 | 必填 | 说明 |
|------|------|:----:|------|
| `action` | string | ✅ | 操作类型：`search` / `get_by_code` / `get_by_type` / `list_types` / `list_projects` |
| `keyword` | string | | 搜索关键词 |
| `material_code` | string | | 材料编码 |
| `material_type` | string | | 材料类型：`PIPE` / `BOLT` / `SCTN` / `SUPP` |
| `project_id` | string | | 项目编号 |
| `limit` | int | | 返回结果数量限制 |

**使用示例**：

```
# 搜索材料
用户：查询包含"异径承插"的材料信息

# 按编码查询
用户：查询材料编码SPC00025的详细信息

# 按类型查询
用户：查询1907项目中所有类型为BOLT的材料

# 列出材料类型
用户：列出所有支持的材料类型
```

---

### 3. get_pipe_info - 管道信息提取

**功能**：从E3D中提取管道详细信息

**参数**：

| 参数 | 类型 | 必填 | 说明 |
|------|------|:----:|------|
| `action` | string | ✅ | 操作类型：`get_pipe_detail` / `get_branch_info` / `get_pipe_components` / `get_supports` / `list_pipes` / `get_pipe_hierarchy` |
| `pipe_name` | string | | 管道名称 |
| `branch_name` | string | | 分支名称 |
| `zone_name` | string | | 区域名称 |
| `include_attributes` | bool | | 是否包含详细属性 |
| `include_hierarchy` | bool | | 是否包含层级结构 |
| `limit` | int | | 返回结果数量限制 |

**使用示例**：

```
# 获取管道详情
用户：获取管道PIPE-1001的详细信息

# 获取分支信息
用户：获取分支BRAN-1001-1的信息

# 获取管件列表
用户：获取管道PIPE-1001的管件列表

# 列出管道
用户：列出当前区域的所有管道

# 获取层级结构
用户：获取管道PIPE-1001的层级结构
```

---

### 4. manage_autocad - AutoCAD进程管理

**功能**：管理AutoCAD进程的启动、关闭、通信

**参数**：

| 参数 | 类型 | 必填 | 说明 |
|------|------|:----:|------|
| `action` | string | ✅ | 操作类型：`start` / `stop` / `status` / `execute_script` / `list_processes` / `detect_path` / `save_path` |
| `cad_exe_path` | string | | AutoCAD可执行文件路径 |
| `script_content` | string | | 要执行的脚本内容 |
| `script_file_path` | string | | 要执行的脚本文件路径 |
| `timeout_seconds` | int | | 操作超时时间（秒） |

**使用示例**：

```
# 启动AutoCAD
用户：启动AutoCAD用于ISO出图

# 检查状态
用户：检查AutoCAD是否正在运行

# 自动检测路径
用户：检测AutoCAD安装路径

# 保存路径
用户：保存AutoCAD路径 C:\Program Files\Autodesk\AutoCAD 2024\acad.exe

# 执行脚本
用户：在AutoCAD中执行脚本
```

---

## 使用流程

### 流程1：首次使用 - 配置AutoCAD

```
1. 用户：我要使用ISO出图功能
2. AI自动检测AutoCAD安装路径
3. AI提示用户确认路径
4. 用户确认后，AI保存路径到配置
5. 配置完成，可以开始使用
```

### 流程2：生成单个管道ISO图

```
1. 用户：为管道PIPE-1001生成ISO图纸
2. AI从配置读取AutoCAD路径和项目编号
3. AI启动AutoCAD（如果未运行）
4. AI调用ISO生成工具
5. AI返回生成结果和文件路径
```

### 流程3：批量生成ISO图

```
1. 用户：为以下管道批量生成ISO图：PIPE-1001, PIPE-1002, PIPE-1003
2. AI启动AutoCAD（如果未运行）
3. AI逐个生成ISO图纸
4. AI返回批量生成结果
```

### 流程4：查询材料信息

```
1. 用户：查询SPC00025材料的详细信息
2. AI从材料数据库查询
3. AI返回材料详细信息
```

---

## 配置说明

### ISO出图配置

配置文件位置：`%LOCALAPPDATA%\E3DCopilot\config.json`

```json
{
  "Iso": {
    "AutoCadPath": "C:\\Program Files\\Autodesk\\AutoCAD 2024\\acad.exe",
    "DefaultProjectId": "1907",
    "DefaultOutputDir": "D:\\ISO出图结果",
    "IncludeMaterialList": true,
    "DefaultTemplateType": "standard",
    "AutoCadTimeoutSeconds": 60,
    "AutoStartAutoCad": true
  }
}
```

### 配置项说明

| 配置项 | 说明 | 默认值 |
|--------|------|--------|
| `AutoCadPath` | AutoCAD可执行文件路径 | "" (自动检测) |
| `DefaultProjectId` | 默认项目编号 | "1907" |
| `DefaultOutputDir` | 默认输出目录 | "D:\ISO出图结果" |
| `IncludeMaterialList` | 是否包含材料清单 | true |
| `DefaultTemplateType` | 默认模板类型 | "standard" |
| `AutoCadTimeoutSeconds` | AutoCAD启动超时时间 | 60秒 |
| `AutoStartAutoCad` | 是否自动启动AutoCAD | true |

---

## 支持的项目

| 项目编号 | 项目名称 | 说明 |
|----------|----------|------|
| 1907 | 1907项目 | 核电项目1907 |
| 1916 | 1916项目 | 核电项目1916 |
| 2016 | 2016项目 | 核电项目2016 |
| 2026 | 2026项目 | 核电项目2026 |

---

## 材料类型

| 类型代码 | 类型名称 | 说明 |
|----------|----------|------|
| PIPE | 管道材料 | 管道、管件、法兰等 |
| BOLT | 螺栓 | 螺栓、螺母、垫片等紧固件 |
| SCTN | 型钢 | 角钢、槽钢、工字钢等型钢材料 |
| SUPP | 支吊架 | 管道支吊架、弹簧支吊架等 |

---

## 常见问题

### Q1: AutoCAD启动失败

**原因**：AutoCAD路径配置错误或AutoCAD未正确安装

**解决**：
1. 使用 `manage_autocad detect_path` 检测路径
2. 确认AutoCAD已正确安装
3. 检查是否有足够的系统权限

### Q2: 材料查询无结果

**原因**：项目编号错误或材料数据文件缺失

**解决**：
1. 检查项目编号是否正确（1907/1916/2016/2026）
2. 确认材料数据文件已复制到 `lib/iso/` 目录
3. 尝试使用不同的搜索关键词

### Q3: ISO图纸生成失败

**原因**：多种可能 — AutoCAD未安装、管道不存在、ISODRAFT模块异常、模板文件缺失

**解决**：
1. 确认 AutoCAD 已安装且路径配置正确
2. 检查管道名称是否正确（在E3D中可查询到）
3. 确认E3D中存在该管道且包含分支（BRAN）
4. 检查输出目录是否有写入权限
5. 确认部署文件完整（IsoTemplate、seed.dwg、SKEYS、LoadPlugins.scr）
6. 查看 Draw 返回的错误消息（ToolResult 中包含）

> **注意**：`Draw.Detail()` 内部会启动一个独立的 AutoCAD 进程（通过 WCF 命名管道通信），
> 不需要预先手动启动 AutoCAD。如果生成失败，AutoCAD 可能会弹出 MessageBox 显示错误。

### Q4: 如何修改默认配置

**方式1**：直接编辑配置文件
```
%LOCALAPPDATA%\E3DCopilot\config.json
```

**方式2**：通过AI工具
```
用户：设置默认项目编号为1916
用户：设置默认输出目录为 E:\ISO输出
```

---

## 技术架构

### 出图流程（generate / batch_generate）

```
用户自然语言
    ↓
E小智 AI Agent → generate_iso_drawing 工具
    ↓
┌──────────────────────────────────────────────────┐
│  IsoDrawingHandler.ExecuteAsync()                │
│  1. 读取配置（CopilotConfig.Iso）                │
│  2. 验证管道存在（PipeReader 构造函数）           │
│  3. 调用 Draw.Instance.Detail()  ← 后台线程      │
└──────────────────────────────────────────────────┘
    ↓
┌──────────────────────────────────────────────────┐
│  CNPE.ISO.E3D.Draw.Detail()                      │
│  ├─ 创建 CadProxy（启动 AutoCAD，WCF 连接）      │
│  └─ 对每个管道：                                  │
│     ├─ IsoItem.SetItemModel()                    │
│     │  ├─ PcomReader   — 阀门格式化               │
│     │  ├─ ConnReader    — 接图信息                 │
│     │  ├─ MaterialReader — 材料信息               │
│     │  ├─ PipeReader    — 管道属性                 │
│     │  └─ BranReader    — 焊缝/支架信息           │
│     ├─ IsoItem.OutputTran()                      │
│     │  └─ ISODRAFT PML: $M template / TRANSFER   │
│     ├─ IsoItem.ResetTran()                       │
│     │  └─ 修改 TRAN（SKEY编号、ATTA点处理）       │
│     ├─ IsoItem.ConvertTranToDxfs()               │
│     │  └─ ISODRAFT PML: PROCESS TRANSFERFILE     │
│     └─ CadProxy.FormatDxf()                      │
│        └─ WCF → AutoCAD 插件 → 保存 DWG           │
└──────────────────────────────────────────────────┘
    ↓
输出 DWG 文件到 output_dir
```

### DLL 依赖关系

```
E3DCopilot.Core.dll
  ├─ CNPE.ISO.E3D.dll        ← Draw, IsoItem, CadProxy
  ├─ CNPE.ISO.E3D.Core.dll   ← PipeReader, BranReader, ConnReader
  ├─ CNPE.ISO.CAD.Core.dll   ← CAD 格式化核心
  └─ CNPE.ISO.Model.dll      ← ItemModel, PipeLineInfoModel

运行时传递依赖（E3D 环境提供）：
  ├─ CNPE.ISO.Common.dll     ← ApplicationEvar, Log, WCFHelper
  ├─ CNPE.ISO.Contract.dll   ← IDetailIso (WCF 接口)
  ├─ CNPE.ISO.E3D.Utility.dll ← PmlUtility
  ├─ CNPE.ISO.E3D.DetailDrawing.dll ← BranElement
  └─ Aveva.Core.*.dll        ← E3D API
```

### 部署文件要求

以下文件必须与 `CNPE.ISO.E3D.dll` 放在同一目录（E3D 插件目录）：

| 文件/目录 | 说明 |
|-----------|------|
| `IsoTemplate/` | ISODRAFT 模板文件目录 |
| `seed.dwg` | AutoCAD 种子文件 |
| `SKEYS` | 管件符号库 |
| `LoadPlugins.scr` | AutoCAD 插件加载脚本 |
| `Evars.config` | 环境变量配置（CAD路径、插件目录） |
| `PIPE.csv` / `BOLT.csv` / `SCTN.csv` / `SUPP.csv` | 材料编码数据 |

---

## 更新日志

### v2.2.0 (2026-06-26)

- ✅ **替换 Mock 实现**：`IsoDrawingHandler` 现在调用真实的 `CNPE.ISO.E3D.Draw.Instance.Detail()`
- ✅ **完整 ISODRAFT 流程**：TRAN 生成 → TRAN 修改 → DXF 转换 → AutoCAD 格式化
- ✅ **新增 `open_in_cad` 参数**：生成后可选自动打开 DWG 文件
- ✅ **管道预验证**：通过 `PipeReader` 构造函数验证管道存在性
- ✅ **批量优化**：一次性传入所有管道，单次 AutoCAD 会话处理
- ✅ **新增 DLL 引用**：`CNPE.ISO.E3D.dll`（Draw / IsoItem / CadProxy）
- ✅ **补充部署文档**：IsoTemplate、seed.dwg、SKEYS 等文件要求

### v2.1.0 (2026-06-25)

- ✅ 新增ISO出图功能（Mock 实现）
- ✅ 新增材料查询功能
- ✅ 新增管道信息提取功能
- ✅ 新增AutoCAD进程管理功能
- ✅ 支持AutoCAD路径自动检测
- ✅ 支持4个核电项目（1907/1916/2016/2026）
- ✅ 集成CNPE.IC.ISO核心库
