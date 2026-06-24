# 知识库维护指南

> ~~原 CLEANUP_PLAN.md（已废弃）曾建议删除 api/ 目录，经评估后取消。~~
> **结论：api/ 和 patterns/ 中的 C# 内容均应保留。**

---

## 为什么保留 api/ 目录？

1. **写操作工具的底层是 C# 编排 + PML 执行**：LLM 生成 execute_pml 前需理解 E3D 数据模型
2. **DbElement.md 是核心参考**：GetElement/GetAsString/SetAttribute/FirstMember/NextMember 签名
3. **API-corrections.md 防止幻觉**：如误用 `D3Point` 代替 `Position`，误用 `CommandLine` 代替 `Command`
4. **Geometry 类签名**：Position/Direction/Orientation 是 geometry 工具和 calculate 工具的底层参考

## 为什么保留 patterns/ 中的 C# 段落？

patterns/ 的 C# 等价实现展示了 **C#+PML 协作模式**（Command.CreateCommand → RunInPdms），
这正是 modify/design/piping 等写操作工具的实际执行方式。

---

## 当前知识库结构（应保留）

```
knowledge/
├── api/              ✅ 保留 — C# API 签名参考（26 个文件）
├── domain/           ✅ 保留 — 领域知识 + 工具选择指南（5 个文件）
├── patterns/         ✅ 保留 — 黄金范式模板（18 个文件，含 C# 段落）
├── pml/              ✅ 保留 — PML 语法完整参考（11 个文件）
├── search_index.json ✅ 保留 — 关键词倒排索引
└── 维护指南.md        本文件
```

## 各目录用途

| 目录 | 内容 | 对应工具 |
|------|------|---------|
| `api/` | Aveva C# DLL 类签名、方法签名、命名空间 | get_attributes, geometry, design |
| `domain/` | 元素类型表、属性映射表、坐标系、错误码、**工具选择指南** | 所有工具的选型依据 |
| `patterns/` | 已验证的 PML 代码模板 + C# 等价实现 | execute_pml, modify, query |
| `pml/` | PML 语法/对象/函数完整参考 | execute_pml（生成 PML 时参考） |

## 维护注意事项

- 新增黄金范式时，在文件头部添加 `preferred_tool` 字段标注对应工具
- 新增 API 文档时，同步更新 `search_index.json`
- 不要删除 C# 代码段 —— 它们是理解 API 和 PML 关系的关键
