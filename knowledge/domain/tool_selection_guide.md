# 工具选择决策指南

> AI 在回答用户问题时，应按此决策树选择正确的工具。
> **核心原则**：优先用专用工具，最后才用 execute_pml 兜底。

---

## 决策树

```
用户需求是什么？
│
├─ 读取元素属性（壁厚/直径/规格/坐标...）
│   ├─ 已知元素名 → ✅ get_attributes
│   └─ 不知道元素名，需要先查找
│       ├─ 查找1个元素 → ✅ query (limit=1) → 再 get_attributes
│       └─ 批量查找 → ✅ query → 再 get_attributes
│
├─ 查询/搜索元素（按类型/名称/范围）
│   └─ ✅ query
│       type: PIPE/EQUI/STRU/BRAN/ZONE/VALV/NOZZ/HOLE
│       name: 通配符 *DN100*
│       scope: 限定范围
│
├─ 修改元素属性
│   ├─ 单个元素单属性 → ✅ modify
│   ├─ 单个元素多属性 → ✅ modify (attributes={...})
│   └─ 批量修改多个元素 → ✅ modify + 先 query 确认目标
│
├─ 检查/验证
│   ├─ 元素是否存在 → ✅ check(type=exists)
│   ├─ 属性值是否正确 → ✅ check(type=attribute)
│   ├─ 命名规则检查 → ✅ check(type=naming)
│   └─ 间距/碰撞检查 → ✅ check(type=clearance)
│
├─ 创建设计元素
│   ├─ 设备(EQUI) → ✅ design(action=create, type=EQUI)
│   ├─ 结构(STRU) → ✅ design(action=create, type=STRU)
│   ├─ 管道(PIPE/BRAN) → ✅ piping(action=create)
│   └─ 管件(FTUB/BEND/TEE) → ✅ piping(action=create)
│
├─ 几何/空间查询
│   ├─ 元素位置坐标 → ✅ geometry(action=position)
│   ├─ 元素朝向 → ✅ geometry(action=orientation)
│   ├─ 包围盒 → ✅ geometry(action=bbox)
│   └─ 两点距离/角度/向量计算 → ✅ calculate
│
├─ 导出/报表
│   ├─ 导出元素列表 → ✅ export(action=export, format=csv/excel)
│   └─ 生成 PML 脚本 → ✅ export(action=generate_pml)
│
├─ 执行复杂 PML 逻辑
│   └─ ✅ execute_pml（兜底工具）
│       适用：上面工具无法覆盖的复杂操作
│       注意：纯读属性操作会被拦截，请用 get_attributes
│
├─ 向用户确认/提问
│   └─ ✅ ask_user
│
├─ 创建/追踪子任务
│   └─ ✅ task
│
├─ 读取用户上传的文件
│   └─ ✅ read_file
│
└─ 查找 API/PML/范式参考
    └─ ✅ search_knowledge
        时机：生成 PML 或 C# 代码前调用，避免 API 幻觉
```

---

## 工具优先级规则

### 规则 1：读属性永远用 get_attributes，不用 execute_pml

```
❌ execute_pml("!!CE.WTHK")           → 会被拦截
❌ execute_pml("output ce.attributes()") → 会被拦截
✅ get_attributes(element="VT18")      → 快速、稳定
✅ get_attributes(element="VT18", attributes=["WTHK","SPEC"])  → 精确
```

### 规则 2：查询元素用 query，不用 execute_pml 写 coll

```
❌ execute_pml("VAR !LIST COLL ALL PIPE ...")
✅ query(type="PIPE", name="*DN100*", scope="CE")
```

### 规则 3：修改属性用 modify，区分 design/piping 的边界

| 操作 | 工具 | 理由 |
|------|------|------|
| 改 EQUI 的描述 | modify | 只改属性，不创建元素 |
| 改 PIPE 的壁厚 | modify | 只改属性 |
| 创建新 EQUI | design(action=create) | 创建设计元素 |
| 创建新 PIPE/BRAN | piping(action=create) | 创建管道元素 |
| 删除 STRU | design(action=delete) | 删除结构元素 |
| 批量改名 | modify + query | 先查再改 |

### 规则 4：geometry 读坐标，calculate 做数学

```
geometry(action=position, element="VT18")  → 返回坐标 [E, N, U]
calculate(operation=distance, point1=[...], point2=[...])  → 返回距离值
```

先 geometry 取坐标，再 calculate 算距离 —— 两步组合使用。

### 规则 5：execute_pml 是最后手段

以下场景才需要 execute_pml：
- 复杂的条件分支逻辑（IF/ELSE 嵌套）
- 集合遍历 + 条件判断 + 批量操作组合
- 调用 PML 内置函数（Matchwild/REPORT/UNDOABLE）
- 创建自定义属性
- 上面所有工具都无法覆盖的操作

---

## 工具组合使用模式

### 模式 A：查询 + 读取（最常见）

```
1. query(type="PIPE", name="*DN100*")     → 得到元素列表
2. get_attributes(element="PIPE-001")     → 读取每个元素的属性
3. 汇总结果回复用户
```

### 模式 B：查询 + 修改

```
1. query(type="EQUI", scope="ZONE-01")    → 找到目标元素
2. ask_user(question="确认修改以下设备？")  → 用户确认
3. modify(dburi="/MDS/...", attributes={...}) → 执行修改
```

### 模式 C：坐标 + 计算

```
1. geometry(action=position, element="NOZZ-1") → 坐标 A
2. geometry(action=position, element="NOZZ-2") → 坐标 B
3. calculate(operation=distance, point1=A, point2=B) → 距离
```

### 模式 D：兜底 PML

```
1. search_knowledge(query="REPORT 导出")  → 查 PML REPORT 语法
2. execute_pml(script="...REPORT 代码...") → 执行复杂导出
```

---

## 常见错误选型

| 用户需求 | ❌ 错误工具 | ✅ 正确工具 | 原因 |
|---------|------------|------------|------|
| "VT18 的壁厚是多少" | execute_pml | get_attributes | 纯读属性 |
| "找出所有 DN100 管道" | execute_pml | query | 标准查询 |
| "两个管嘴的距离" | execute_pml | geometry + calculate | 两步组合 |
| "修改管道壁厚" | piping | modify | piping 是创建，不是改属性 |
| "创建设备" | modify | design | modify 只改已有元素 |
| "检查管道是否存在" | query | check(type=exists) | check 专门做验证 |
| "导出设备清单到 CSV" | execute_pml | export | 已有专用工具 |

---

## 代码生成前的知识库检索

**在生成 execute_pml 脚本前，必须调用 search_knowledge**：

| 需要生成的操作 | search_knowledge 查询 |
|--------------|---------------------|
| 集合查询 | `query="coll all"`, `source="pattern"` |
| 属性修改 | `query="modify attribute"`, `source="pattern"` |
| 距离计算 | `query="distance"`, `source="pattern"` |
| 错误处理 | `query="HANDLE ANY"`, `source="pattern"` |
| 元素导航 | `query="FirstMember NextMember"`, `source="pattern"` |
| PML 语法 | `query="IF FOR DO"`, `source="pml"` |
| 文件操作 | `query="file CSV"`, `source="pattern"` |
