# 🏷️ 知识库清理标记

> 标记说明：
> - `[C#-ONLY]` = 整个文件都是 C# 内容，可整体删除
> - `[MIXED]` = 文件以 PML 为主但含 C# 代码段，需清理 C# 部分
> - `[CLEAN]` = 纯 PML，无需动

---

## 一、api/ 目录 — 全部 [C#-ONLY] 🗑️

共 26 个文件，确认后可整体删除 `knowledge/api/` 整个目录。

| 文件 | 标记 |
|------|:----:|
| `api/API-corrections.md` | [C#-ONLY] |
| `api/ApplicationFramework/Addin.md` | [C#-ONLY] |
| `api/ApplicationFramework/Command.md` | [C#-ONLY] |
| `api/ApplicationFramework/CommandBarManager.md` | [C#-ONLY] |
| `api/ApplicationFramework/DockedWindow.md` | [C#-ONLY] |
| `api/ApplicationFramework/ServiceManager.md` | [C#-ONLY] |
| `api/ApplicationFramework/SettingsManager.md` | [C#-ONLY] |
| `api/ApplicationFramework/WindowManager.md` | [C#-ONLY] |
| `api/Database/Db.md` | [C#-ONLY] |
| `api/Database/DbAttribute.md` | [C#-ONLY] |
| `api/Database/DbElement.md` | [C#-ONLY] |
| `api/Database/DbFilter.md` | [C#-ONLY] |
| `api/Database/DbTransaction.md` | [C#-ONLY] |
| `api/Design/Aid.md` | [C#-ONLY] |
| `api/Geometry/D2Angle.md` | [C#-ONLY] |
| `api/Geometry/D2Arc.md` | [C#-ONLY] |
| `api/Geometry/Direction.md` | [C#-ONLY] |
| `api/Geometry/Orientation.md` | [C#-ONLY] |
| `api/Geometry/Position.md` | [C#-ONLY] |
| `api/Graphics/Colour.md` | [C#-ONLY] |
| `api/Graphics/DrawList.md` | [C#-ONLY] |
| `api/Piping/Piping.md` | [C#-ONLY] |
| `api/Shared/Clipboard.md` | [C#-ONLY] |
| `api/Shared/CurrentElement.md` | [C#-ONLY] |
| `api/Standalone/PdmsStandalone.md` | [C#-ONLY] |
| `api/Utilities/Command.md` | [C#-ONLY] |

**删除命令**（确认后执行）：
```bash
Remove-Item -Recurse -Force "knowledge/api/"
```

---

## 二、patterns/ 目录 — 部分 [MIXED]

共 11 个文件含 C# 代码段，需删除 ````csharp` 部分。

| 文件 | 标记 | 需清理内容 |
|------|:----:|-----------|
| `patterns/check_attribute_complete.md` | [MIXED] | 末尾 "C# 等价实现" 段落 |
| `patterns/check_bore_consistency.md` | [MIXED] | 末尾 "C# 等价实现" 段落 |
| `patterns/check_distance.md` | [MIXED] | 末尾 "C# 等价实现" 段落 |
| `patterns/check_exists.md` | [MIXED] | 末尾 "C# 等价实现" 段落 |
| `patterns/collection_query.md` | [MIXED] | 末尾 "C# 等价实现" 段落 |
| `patterns/custom_attributes.md` | [MIXED] | 末尾 "C# 等价实现" 段落 |
| `patterns/element_navigation.md` | [MIXED] | 末尾 "C# 等价实现" 段落 |
| `patterns/element_type_dispatch.md` | [MIXED] | 末尾 "C# 等价实现" 段落 |
| `patterns/geometry_operations.md` | [MIXED] | 末尾 "C# 等价实现" 段落 |
| `patterns/modify_attributes.md` | [MIXED] | 末尾 "C# 等价实现" 段落 |
| `patterns/query_elements.md` | [MIXED] | 末尾 "C# 等价实现" 段落 |

---

## 三、domain/ 目录 — 部分 [MIXED]

| 文件 | 标记 | 需清理内容 |
|------|:----:|-----------|
| `domain/attribute_map.md` | [MIXED] | 含 ````csharp` 代码段 |
| `domain/coordinate_systems.md` | [MIXED] | 含 ````csharp` 代码段 |
| `domain/element_types.md` | [MIXED] | 含 ````csharp` 代码段 |
| `domain/error_codes.md` | [MIXED] | 含 ````csharp` 代码段 |

---

## 四、pml/ 目录 — 基本 [CLEAN]

| 文件 | 标记 |
|------|:----:|
| `pml/commands.md` | [CLEAN] |
| `pml/expressions.md` | [CLEAN] |
| `pml/forms.md` | [CLEAN] |
| `pml/functions.md` | [CLEAN] |
| `pml/objects_core.md` | [CLEAN] |
| `pml/objects_geometry.md` | [CLEAN] |
| `pml/objects_grid.md` | [CLEAN] |
| `pml/objects_system.md` | [CLEAN] |
| `pml/objects_ui.md` | [CLEAN] |
| `pml/tools_index.md` | [CLEAN] |
| `pml/type_conversion.md` | [MIXED] — 含少量 C# 对照 |

---

## 五、SearchKnowledgeHandler.cs — 含 HTML 回退逻辑

**文件**: `src/E3DCopilot.Core/Tools/Handlers/SearchKnowledgeHandler.cs`

| 待清理内容 | 说明 |
|-----------|------|
| `DocsRoot` 字段 | 指向 `E3D官方API文档/docs/` 的路径 |
| `SearchHtmlDocsAsync()` 方法 | HTML 全文搜索回退逻辑 |
| `SearchHtmlFileAsync()` 方法 | 单个 HTML 文件搜索 |

确认删除 C# 端后，可移除上述 3 个部分，保留知识库搜索逻辑。

---

## 六、清理顺序

```
第 1 步：删除 api/ 整个目录（26 个文件）
第 2 步：清理 patterns/ 中 11 个文件的 C# 代码段
第 3 步：清理 domain/ 中 4 个文件的 C# 代码段
第 4 步：清理 pml/type_conversion.md 的 C# 对照
第 5 步：移除 SearchKnowledgeHandler.cs 的 HTML 回退逻辑
第 6 步：更新 search_index.json 移除 api/ 的条目
```
