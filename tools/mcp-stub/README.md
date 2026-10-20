# MCP 平台桩（e3d-data-platform）

演示用的**本地替身**，不是真实的数据管理平台。

## 为什么存在

E小智 是 **MCP Client**，要连的"数据管理平台"必须自己实现 MCP Server。
演示当天若依赖同伴的平台上线，第一步就可能断链。本桩把这条链变成完全自控：

```
E小智 → (stdio JSON-RPC) → server.mjs → platform-data/*.json 落盘
```

平台就绪后，把 `config.json` 里 `McpServers[0].Command` / `Args` 改成平台的启动命令即可，
**E小智 上层代码一行不用改**。

## 暴露的 5 个工具

| 工具 | 用途 | 只读 |
|---|---|---|
| `record_drawing` | 登记图纸基础信息（图号、版本、图层数、实体数） | 否 |
| `log_operation` | 记录操作留痕（parse_drawing / build_model / diff_version …） | 否 |
| `record_model` | 回写模型清单（构件 ID / 类型 / 位置 / 尺寸） | 否 |
| `record_diff` | 回写 A/B 版本差异（新增 / 删除 / 修改清单） | 否 |
| `query_history` | 查询历史图纸、操作、差异 | **是** |

## 运行方式

零依赖（只用 Node 内置模块），断网内网可直接跑：

```bash
node server.mjs
```

数据目录由环境变量 `PLATFORM_DATA_DIR` 指定，不设则落在同目录的 `platform-data/`。

## 协议

- JSON-RPC 2.0 over stdio，逐行（NDJSON）
- 协议版本 `2024-11-05`，对齐 E小智 `McpClient`

## 部署

`deploy.cmd` 会自动把本目录复制到 `<E3D目录>/mcp/e3d-data-platform/`。
如需指定其他位置，设置环境变量 `E3D_MCP_STUB_DIR`。
