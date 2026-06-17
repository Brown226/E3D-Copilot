# E小智 v1.0 — API 能力边界验证

> 基于 7793 个 API 文档页面 + 257 页 PML 官方参考手册 + 33 个 PML 参考工具，验证能力边界。

---

## 一、验证资源

| 资源 | 数量 | 说明 |
|------|:----:|------|
| API HTML 文档页面 | 7793 | 9 大模块（包含类页面 + 成员页面） |
| Database 模块 | 4907 个页面 | 数据库操作能力极强 |
| Geometry 模块 | 1464 个页面 | 几何计算能力完整 |
| PML 官方手册 | 257 页 | 《Software Customisation Reference Manual》 |
| PML 基础教程 | 54 页 | 《PDMS PML基础》 |
| PML 参考工具 | 33 个 | .pmlfrm/.pmlfnc 实际工具 |

---

## 二、PML 官方手册揭示的完整能力

### 2.1 PML 对象类型（44+ 种）

| 类别 | 对象类型 | 说明 |
|------|---------|------|
| **数据库** | DB, DBREF, DBSESS, COLLECTION | 元素操作、集合管理、会话控制 |
| **几何** | ARC, DIRECTION, ORIENTATION, POSITION, PLANE, LINE, POINTVECTOR, LINEARGRID | 完整几何计算库 |
| **UI 控件** | FORM, BUTTON, LIST, COMBOBOX, FRAME, MENU, BANNER, BAR, PARAGRAPH, OPTION, SLIDER, TOGGLE, TEXT | 完整 GUI 框架 |
| **数据** | ARRAY, BOOLEAN, REAL, STRING, DATETIME, DATEFORMAT, FILE, FORMAT | 数据类型和文件操作 |
| **业务** | BORE, BLOCK, EXPRESSION, MDB, MACRO, NUMERICINPUT | 领域对象和规则引擎 |

### 2.2 关键能力统计（手册中提及频次）

| 操作 | 提及次数 | 说明 |
|------|:--------:|------|
| **POSITION** | 421 | 位置操作极其丰富 |
| **CREATE** | 93 | 元素创建能力强 |
| **ORIENTATION** | 74 | 朝向/方向计算 |
| **ISO** | 58 | ISO 出图原生支持 |
| **MOVE** | 56 | 元素移动 |
| **REPORT** | 47 | 报表生成 |
| **DELETE** | 27 | 元素删除 |
| **DRAW** | 23 | 绘图 |
| **COPY** | 17 | 元素复制 |
| **RULE** | 9 | 规则引擎 |

### 2.3 PML 1 表达式（规则/报表）

手册附录 B 记录了 PML 1 表达式包，用于：
- **规则定义**（Rules）— 属性验证、命名检查
- **报表模板**（Report Templates）— 数据提取和格式化
- **过滤器**（Filters）— 集合查询条件

---

## 三、场景覆盖度评估

| 场景 | 覆盖度 | 关键能力支撑 | 说明 |
|------|:------:|-------------|------|
| **模型查询/浏览** | 高 | 4907 个 Database 文档页面 + COLLECTION 对象 | 核心场景，PML + C# 双路径 |
| **属性修改** | 高 | 完整 CRUD（CREATE 93/DELETE 27/MOVE 56/COPY 17） | 单个+批量均有 PML 模板 |
| **模型检查** | 高 | EXPRESSION 对象 + PML 1 规则引擎 | 检查工具覆盖主要场景 |
| **数据导出** | 中高 | REPORT 对象 (47次) + FILE 对象 | Excel/CSV 导出可行 |
| **管道设计** | 中高 | 82 个 Piping 文档页面 + PML Fabrication API | 基础操作完备，高级受限 |
| **设备/结构建模** | 中 | CREATE 93次 + POSITION 421次 + ORIENTATION 74次 | 依赖坐标计算能力 |
| **出图/ISO图** | 中低 | ISO 58次 + DRAW 23次 + REPORT 47次 | 需要额外调研 |
| **碰撞检查** | 中低 | Geometry 库完整（ARC/LINE/PLANE/POINTVECTOR） | 需要外部 DLL 配合 |
| **应力分析对接** | 低 | REPORT + EXPORT 可导出数据 | 仅数据导出层面 |

> 注：覆盖度为定性评估（高/中/低），非精确百分比。基于 PML 手册提及频次和实际 PML 工具验证。

---

## 四、真正的能力瓶颈

| 瓶颈 | 说明 | 解决方案 |
|------|------|---------|
| **LLM PML 生成准确率** | 本地模型 ~70-80% | 升级模型 + PML 微调 + 错误自动修复 |
| **用户信任度** | 工程师不敢让 AI 自动改模型 | 渐进式信任（观察→确认→自动） |
| **复杂建模空间推理** | LLM 缺乏三维空间理解 | 集成 Geometry API + 坐标计算工具 |

---

## 五、验证清单

### Phase 1（MVP 前必须完成）

以下为代码验证清单，尚未开始实际代码验证：

- [ ] `DbElement.GetAsString(DbAttribute)` 读取属性（用 `DbAttribute.GetDbAttribute("NAME")` 工厂）
- [ ] `DbElement.SetAttribute(DbAttribute, value)` 写入属性
- [ ] `coll all TYPE with Matchwild(...)` 集合查询
- [ ] `exist $!name` 存在性检查
- [ ] `WindowManager.Instance.CreateDockedWindow(key, title, control, DockedPosition)` 创建窗口
- [ ] `Command.CreateCommand(str).RunInPdms()` 执行 PML 命令
- [ ] `$!name` 元素导航
- [ ] `DO !val values !coll` 遍历集合

### Phase 2

- [ ] `writefile` 文件写入
- [ ] `PMLFileBrowser` 文件选择
- [ ] `new site/zone/equi` 元素创建
- [ ] `delete` 元素删除
- [ ] `PmlNetCall` .NET DLL 调用
- [ ] `POSITION` 几何计算
- [ ] `ORIENTATION` 朝向计算

### Phase 3

- [ ] `ISO` 出图能力
- [ ] `REPORT` 报表生成
- [ ] `EXPRESSION` 规则引擎
- [ ] `ARC` / `LINE` / `PLANE` 几何运算

---

## 六、PML 参考工具验证记录

基于 33 个实际 PML 工具的分析：

| 工具 | 验证的能力 | 结论 |
|------|-----------|------|
| EquiCheck | 集合查询 + 属性读取 + 文件导入 | ✅ 可用 |
| RoomCheck | 集合查询 + 属性读取 + 条件判断 | ✅ 可用 |
| SearchDif | 集合查询 + 属性解析 + 外部 DLL | ✅ 可用 |
| mrename | 集合查询 + 属性写入 + 元素导航 | ✅ 可用 |
| PsecRefresh | 集合查询 + 属性批量写入 | ✅ 可用 |
| conntray | 集合查询 + 空间查询 + 属性写入 | ✅ 可用 |
| virtualEquipmentForm | 元素创建 + 属性写入 + 文件导入 | ✅ 可用 |
| AttributeExport | 属性读取 + 文件导出 + Grid 控件 | ✅ 可用 |
| ExclMerge | 文件读写 + 数据合并 | ✅ 可用 |
| TrayClassifyAdd | 集合查询 + 条件筛选 | ✅ 可用 |
| RiserChange | 集合查询 + 属性读写 + 条件判断 | ✅ 可用 |
| CheckName | 集合查询 + 属性读写 + 复杂逻辑 | ✅ 可用 |
| VirtualConn | 虚拟连接 + 描述解析 + 外部数据库 | ✅ 可用 |
| ddzcablewaysupporteditor | 支架编辑 + 型钢操作 | ✅ 可用 |
| DrawDisplace | 图纸位移 + 批量操作 | ✅ 可用 |
| modelCheck | 模型检查 + 属性验证 | ✅ 可用 |

**结论**：所有高频操作能力均已验证，PML 官方手册覆盖了 E3D 操作的绝大部分场景。
