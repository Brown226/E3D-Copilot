#!/usr/bin/env node
/**
 * 数据管理平台 MCP Server（演示用本地桩）。
 *
 * 背景：E小智 是 MCP Client，同伴的数据管理平台需要实现 MCP Server 才能对接。
 * 演示当天若依赖同伴的 Server 上线，第一步就可能断链。本桩把这条链变成完全自控：
 *
 *   E小智 → (stdio JSON-RPC) → 本桩 → data/*.json 落盘
 *
 * 平台就绪后，只需把 E小智 的 McpServerConfig 从本桩换成平台地址，上层一行不改。
 *
 * 协议：JSON-RPC 2.0 over stdio，逐行（NDJSON）。
 * 版本：对齐 E小智 McpClient 的 2024-11-05。
 * 依赖：零依赖（只用 node 内置模块），断网内网可直接跑。
 */
import { readFileSync, writeFileSync, existsSync, mkdirSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { createInterface } from "node:readline";

const HERE = dirname(fileURLToPath(import.meta.url));
const DATA_DIR = process.env.PLATFORM_DATA_DIR
  ? resolve(process.env.PLATFORM_DATA_DIR)
  : join(HERE, "platform-data");

const SERVER_INFO = { name: "e3d-data-platform", version: "0.1.0" };
const PROTOCOL_VERSION = "2024-11-05";

// ─────────────────────────────────────────────────────────────
//  存储：每个实体一个 JSON 文件，简单直接，演示够用
// ─────────────────────────────────────────────────────────────

function ensureDir() {
  if (!existsSync(DATA_DIR)) mkdirSync(DATA_DIR, { recursive: true });
}

function storePath(name) {
  return join(DATA_DIR, `${name}.json`);
}

function loadStore(name) {
  ensureDir();
  const p = storePath(name);
  if (!existsSync(p)) return [];
  try {
    const parsed = JSON.parse(readFileSync(p, "utf8"));
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    // 文件损坏时返回空数组而不是崩掉整个 server——演示不能因为一行脏数据全停
    return [];
  }
}

function saveStore(name, rows) {
  ensureDir();
  writeFileSync(storePath(name), JSON.stringify(rows, null, 2), "utf8");
}

function nextId(rows, prefix) {
  const n = rows.length + 1;
  return `${prefix}-${String(n).padStart(4, "0")}`;
}

// ─────────────────────────────────────────────────────────────
//  业务工具：图纸台账 / 操作留痕 / 模型清单 / 版本差异 / 历史查询
// ─────────────────────────────────────────────────────────────

const TOOLS = [
  {
    name: "record_drawing",
    description:
      "登记一份图纸的基础信息（版本号、图层数、实体数、文件路径、提交人）。在解析图纸后调用，作为本次设计的台账起点。",
    inputSchema: {
      type: "object",
      properties: {
        drawingNo: { type: "string", description: "图号，如 20161SD-JGS06" },
        version: { type: "string", description: "版本号，如 A / B" },
        filePath: { type: "string", description: "图纸文件绝对路径" },
        layerCount: { type: "number" },
        entityCount: { type: "number" },
        textCount: { type: "number" },
        cadVersion: { type: "string", description: "DWG 内部版本，如 AC1024" },
        submittedBy: { type: "string", description: "提交人" },
        note: { type: "string" },
      },
      required: ["drawingNo", "version", "filePath"],
    },
    annotations: { readOnlyHint: false, destructiveHint: false },
    handler(args) {
      const rows = loadStore("drawings");
      const row = {
        id: nextId(rows, "DWG"),
        drawingNo: args.drawingNo,
        version: args.version,
        filePath: args.filePath,
        layerCount: args.layerCount ?? null,
        entityCount: args.entityCount ?? null,
        textCount: args.textCount ?? null,
        cadVersion: args.cadVersion ?? null,
        submittedBy: args.submittedBy ?? null,
        note: args.note ?? null,
        recordedAt: new Date().toISOString(),
      };
      rows.push(row);
      saveStore("drawings", rows);
      return { ok: true, record: row, total: rows.length };
    },
  },
  {
    name: "log_operation",
    description:
      "记录一次用户/智能体操作（解析图纸、发起建模、执行 PML、比对版本等），用于全流程留痕与事后追溯。",
    inputSchema: {
      type: "object",
      properties: {
        drawingNo: { type: "string" },
        version: { type: "string" },
        operation: { type: "string", description: "操作类型，如 parse_drawing / build_model / diff_version / execute_pml" },
        detail: { type: "string", description: "操作细节（自由文本或 JSON 字符串）" },
        operator: { type: "string", description: "操作人" },
        result: { type: "string", description: "success / failed / partial" },
      },
      required: ["operation"],
    },
    annotations: { readOnlyHint: false, destructiveHint: false },
    handler(args) {
      const rows = loadStore("operations");
      const row = {
        id: nextId(rows, "OP"),
        drawingNo: args.drawingNo ?? null,
        version: args.version ?? null,
        operation: args.operation,
        detail: args.detail ?? null,
        operator: args.operator ?? null,
        result: args.result ?? "success",
        at: new Date().toISOString(),
      };
      rows.push(row);
      saveStore("operations", rows);
      return { ok: true, record: row, total: rows.length };
    },
  },
  {
    name: "record_model",
    description:
      "写入 E3D 建模完成后的模型清单（按构件类型汇总：墙/柱/梁/门窗，含数量与总体积或长度）。建模结束后调用。",
    inputSchema: {
      type: "object",
      properties: {
        drawingNo: { type: "string" },
        version: { type: "string" },
        modelName: { type: "string", description: "E3D 模型名/区域" },
        components: {
          type: "array",
          description: "构件清单",
          items: {
            type: "object",
            properties: {
              type: { type: "string", description: "墙 / 柱 / 梁 / 门窗 / 设备" },
              count: { type: "number" },
              totalLength: { type: "number" },
              note: { type: "string" },
            },
            required: ["type", "count"],
          },
        },
        builtAt: { type: "string" },
      },
      required: ["drawingNo", "version", "components"],
    },
    annotations: { readOnlyHint: false, destructiveHint: false },
    handler(args) {
      const rows = loadStore("models");
      const row = {
        id: nextId(rows, "MDL"),
        drawingNo: args.drawingNo,
        version: args.version,
        modelName: args.modelName ?? null,
        components: args.components ?? [],
        componentTotal: (args.components ?? []).reduce((s, c) => s + (c.count ?? 0), 0),
        builtAt: args.builtAt ?? new Date().toISOString(),
        recordedAt: new Date().toISOString(),
      };
      rows.push(row);
      saveStore("models", rows);
      return { ok: true, record: row, total: rows.length };
    },
  },
  {
    name: "record_diff",
    description:
      "写入两版图纸的差异清单（新增/删除/修改）。由 dwg-diff 的结果批量提交，也可单条提交。",
    inputSchema: {
      type: "object",
      properties: {
        drawingNo: { type: "string" },
        versionA: { type: "string" },
        versionB: { type: "string" },
        tol: { type: "number", description: "比对容差（图纸单位）" },
        records: {
          type: "array",
          description: "差异条目清单（来自 dwg-diff 输出）",
          items: { type: "object" },
        },
      },
      required: ["drawingNo", "versionA", "versionB", "records"],
    },
    annotations: { readOnlyHint: false, destructiveHint: false },
    handler(args) {
      const rows = loadStore("diffs");
      const batchId = nextId(rows, "DIFF");
      const records = args.records ?? [];
      const counts = { added: 0, removed: 0, modified: 0 };
      for (const r of records) {
        if (r.changeType in counts) counts[r.changeType]++;
      }
      const row = {
        id: batchId,
        drawingNo: args.drawingNo,
        versionA: args.versionA,
        versionB: args.versionB,
        tol: args.tol ?? null,
        counts,
        recordCount: records.length,
        records,
        recordedAt: new Date().toISOString(),
      };
      rows.push(row);
      saveStore("diffs", rows);
      return { ok: true, batchId, counts, recordCount: records.length };
    },
  },
  {
    name: "query_history",
    description:
      "查询平台上的历史数据。kind 可选 drawings / operations / models / diffs / all；可按图号与版本过滤。",
    inputSchema: {
      type: "object",
      properties: {
        kind: { type: "string", enum: ["drawings", "operations", "models", "diffs", "all"] },
        drawingNo: { type: "string" },
        version: { type: "string" },
        limit: { type: "number", description: "最多返回条数，默认 50" },
      },
      required: ["kind"],
    },
    annotations: { readOnlyHint: true, destructiveHint: false },
    handler(args) {
      const kinds = args.kind === "all" ? ["drawings", "operations", "models", "diffs"] : [args.kind];
      const limit = args.limit ?? 50;
      const out = {};
      for (const k of kinds) {
        let rows = loadStore(k);
        if (args.drawingNo) rows = rows.filter((r) => r.drawingNo === args.drawingNo);
        if (args.version) {
          rows = rows.filter((r) => r.version === args.version || r.versionA === args.version || r.versionB === args.version);
        }
        out[k] = { total: rows.length, rows: rows.slice(-limit) };
      }
      return { ok: true, result: out };
    },
  },
];

const TOOL_MAP = new Map(TOOLS.map((t) => [t.name, t]));

// ─────────────────────────────────────────────────────────────
//  JSON-RPC 2.0 over stdio（NDJSON）
// ─────────────────────────────────────────────────────────────

function send(msg) {
  process.stdout.write(JSON.stringify(msg) + "\n");
}

function ok(id, result) {
  send({ jsonrpc: "2.0", id, result });
}

function fail(id, code, message) {
  send({ jsonrpc: "2.0", id, error: { code, message } });
}

/** tools/call 的 content 必须是 [{type:"text", text}]，E小智 按这个结构解析。 */
function textResult(payload, isError = false) {
  return {
    content: [{ type: "text", text: JSON.stringify(payload, null, 2) }],
    ...(isError ? { isError: true } : {}),
  };
}

function handle(msg) {
  const { id, method, params } = msg;

  // 通知类消息（无 id）：不回复
  if (method === "notifications/initialized" || method === "initialized") return;
  if (id === undefined || id === null) return;

  switch (method) {
    case "initialize":
      return ok(id, {
        protocolVersion: PROTOCOL_VERSION,
        capabilities: { tools: {} },
        serverInfo: SERVER_INFO,
        instructions:
          "E3D 数据管理平台（演示桩）。提供图纸台账、操作留痕、模型清单、版本差异记录与历史查询。",
      });

    case "ping":
      return ok(id, {});

    case "tools/list":
      return ok(id, {
        tools: TOOLS.map((t) => ({
          name: t.name,
          description: t.description,
          inputSchema: t.inputSchema,
          annotations: t.annotations,
        })),
      });

    case "tools/call": {
      const name = params?.name;
      const args = params?.arguments ?? {};
      const tool = TOOL_MAP.get(name);
      if (!tool) return ok(id, textResult({ ok: false, error: `未知工具: ${name}` }, true));

      // 必填校验（简单版：只查 required 是否存在）
      const required = tool.inputSchema.required ?? [];
      const missing = required.filter((k) => args[k] === undefined || args[k] === null);
      if (missing.length) {
        return ok(id, textResult({ ok: false, error: `缺少必填参数: ${missing.join(", ")}` }, true));
      }

      try {
        const result = tool.handler(args);
        return ok(id, textResult(result));
      } catch (e) {
        return ok(id, textResult({ ok: false, error: `${e.name}: ${e.message}` }, true));
      }
    }

    default:
      return fail(id, -32601, `未实现的方法: ${method}`);
  }
}

function main() {
  ensureDir();
  process.stderr.write(`[platform-mcp] 启动，数据目录: ${DATA_DIR}\n`);

  const rl = createInterface({ input: process.stdin, crlfDelay: Infinity });
  rl.on("line", (line) => {
    const trimmed = line.trim();
    if (!trimmed) return;
    let msg;
    try {
      msg = JSON.parse(trimmed);
    } catch {
      // 解析失败没有 id 可回，只能写 stderr 提示
      process.stderr.write(`[platform-mcp] 无法解析的行: ${trimmed.slice(0, 120)}\n`);
      return;
    }
    try {
      handle(msg);
    } catch (e) {
      if (msg?.id !== undefined) fail(msg.id, -32603, `${e.name}: ${e.message}`);
      else process.stderr.write(`[platform-mcp] 处理异常: ${e.message}\n`);
    }
  });
  rl.on("close", () => process.exit(0));
}

main();
