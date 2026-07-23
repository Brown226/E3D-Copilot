---
name: e3d-batch-modify
description: E3D 批量修改安全流程 — 批量操作的标准五步法（查询→预览→审批→执行→验证）。涉及批量修改时调用此 skill。
runAs: inline
tags: [批量, 安全, 修改, 审批, 回滚]
---

# E3D 批量修改安全流程

> 批量修改（>5 个元素）属于 L3 安全级别，必须严格执行五步法。
> 违反流程直接操作 = 数据灾难（E3D 无全局 Undo）。

---

## 五步法（不可跳过）

### Step 1: 查询目标集合
```pml
-- 明确范围，展示给用户
var !targets coll all PIPE with Matchwild(name, '*DN100*') for /MDS/PIPING
var !count 0
DO !item values !targets
    var !count (!count + 1)
ENDDO
-- 输出: 找到 $!count 个匹配元素
```

**输出给用户**：`找到 N 个匹配元素，是否继续？`

### Step 2: 生成修改计划（预览表格）

必须以表格形式展示：

| # | 元素名 | 属性 | 当前值 | 目标值 |
|---|--------|------|--------|--------|
| 1 | /MDS/PIPES/PIPE-001 | WTHK | SCH20 | SCH40 |
| 2 | /MDS/PIPES/PIPE-002 | WTHK | SCH20 | SCH40 |
| ... | ... | ... | ... | ... |

**关键**：先读取所有旧值，再展示计划。

### Step 3: 用户审批

等待用户明确确认（"确认执行" / "全部允许"）。
- 用户拒绝 → 终止，不执行任何修改
- 用户修改范围 → 回到 Step 1 重新查询

### Step 4: 执行修改（带异常处理）
```pml
var !success 0
var !failed 0
var !errors ''

DO !item values !targets
    handle (code)
        var !failed (!failed + 1)
        var !errors (!errors + ' ' + !item.Name + ':ERR' + code.String)
    endhandle
    
    -- 执行修改
    !item.Wthk = 'SCH40'
    var !success (!success + 1)
ENDDO

-- 输出结果
-- 成功: $!success, 失败: $!failed
```

### Step 5: 验证结果
```pml
-- 抽样验证（取前 3 个确认修改生效）
var !verify coll all PIPE with Matchwild(name, '*DN100*') for /MDS/PIPING
var !i 0
DO !item values !verify
    var !i (!i + 1)
    IF (!i > 3) THEN
        BREAK
    ENDIF
    -- 输出: !item.Name WTHK = !item.Wthk
ENDDO
```

---

## 安全红线

| 规则 | 说明 |
|------|------|
| 单次上限 | 单次批量 ≤ 50 个元素，超过分批执行 |
| 必须预览 | 任何批量修改前必须展示计划表格 |
| 必须审批 | 用户未明确确认前不执行 |
| 异常不中断 | handle 捕获错误，继续处理剩余元素 |
| 记录旧值 | 修改前的旧值记入 checkpoint（供回滚） |
| 禁止盲改 | 不确定的属性值必须先查询确认 |

---

## 回滚机制

如果修改出错，通过 CheckpointManager 回滚：
- 每次批量修改前自动 `AutoCheckpointBeforeTool`
- 回滚命令：`/rewind 1`（回退到上一个 checkpoint）
- 或手动：用记录的旧值逐个恢复

参考：`read_file("knowledge/patterns/error_handling.md")`
