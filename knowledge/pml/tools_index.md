# PML 工具索引表

> 33 个真实 PML 工具的功能速查 + 元素类型 + 对应黄金范式。
> AI 可通过工具名快速找到相关编码模式。

---

## 一、电缆/桥架工具

| # | 工具名 | 类型 | 功能 | 元素类型 | 关键属性 | 对应范式 |
|:-:|--------|:----:|------|----------|---------|---------|
| 1 | **CableLayTools** | .pmlfrm | 主菜单，启动其他所有工具 | — | — | — |
| 2 | **conntray** | .pmlfrm | 桥架托盘连接：查询支架、查看 FTUB 连接、手动/自动分配 conntray | ZONE, SUPPO, GENSEC, FTUB, ELBO, BEND, TEE, REDU, CROS | `:conntray`, `pspec.name`, `flnn` | [query_elements](patterns/query_elements.md), [custom_attributes](patterns/custom_attributes.md) |
| 3 | **ddzcablewaysupporteditor** | .pmlfrm | 电缆桥架支吊架编辑器：3D 视图、截面修剪、托臂调整/复制 | STRU, SCTN, SUPC, ANCI, PJOI, FTUB, SUPPO | `pos`, `poss`, `pose`, `drns`, `drne`, `cutl`, `worpos` | [geometry_operations](patterns/geometry_operations.md), [net_integration](patterns/net_integration.md) |
| 4 | **RotateElecSuppo** | .pmlfrm | 绕 ANCI 点旋转支架 | STRU, ANCI | `worpos`, `ori.zdir()`, `type`, `flnn` | [geometry_operations](patterns/geometry_operations.md) |
| 5 | **ReWriteElecWidth** | .pmlfrm | 批量写入桥架宽度/高度属性 | BRANCH, FTUB | `:BranWidth`, `:BranHigh`, `PARA[1]`, `PARA[2]`, `ABORE`, `SPREF` | [modify_attributes](patterns/modify_attributes.md), [list_grid_operations](patterns/list_grid_operations.md) |
| 6 | **TrayClassifyAdd** | .pmlfrm | 按类型/色标筛选桥架并加载到选择集 | ZONE, BRAN | `name`, `pspec` | [query_elements](patterns/query_elements.md) |
| 7 | **RiserChange** | .pmlfrm | 分隔板修改：检查 desc/func、验证分隔板分配 | BRAN, FTUB, ZONE, ATTA | `desc`, `func`, `pspec.flnn`, `flnm` | [check_attribute_complete](patterns/check_attribute_complete.md), [element_type_dispatch](patterns/element_type_dispatch.md) |
| 8 | **PsecRefresh** | .pmlfrm | 桥架等级属性刷新：批量刷新 pspec/lstube/hstube | SITE, ZONE, BRAN, PIPE, FTUB, ATTA, TUBI | `spref.flnn`, `lstube.flnn`, `hstube`, `pspec`, `para[1]` | [modify_attributes](patterns/modify_attributes.md), [collection_query](patterns/collection_query.md) |

---

## 二、管道/设备工具

| # | 工具名 | 类型 | 功能 | 元素类型 | 关键属性 | 对应范式 |
|:-:|--------|:----:|------|----------|---------|---------|
| 9 | **EquiCheck** | .pmlfrm | 从 CSV 导入设备清单，检查模型是否存在，导出结果 | SITE, EQUI, VALV, PCOM, INST, DAMP, BATT | `flnn` | [file_io](patterns/file_io.md), [check_exists](patterns/check_exists.md) |
| 10 | **EquiInfo** | .pmlfnc | .NET 启动器：设备信息对比工具 | — | — | [net_integration](patterns/net_integration.md) |
| 11 | **cnpedrawlist** | .pmlfnc | 带版本控制的设备清单工具启动器 | — | — | [net_integration](patterns/net_integration.md) |
| 12 | **MdsMgbChange** | .pmlfrm | 更改锚固板(MGB)类型和旋转角度 | SPCO, FIXING | `spref.name`, `spref` | [modify_attributes](patterns/modify_attributes.md) |
| 13 | **mrename** | .pmlfrm | 批量重命名成员（前缀/后缀/插入/替换/删除/编号） | MEM, DDNM | `name` | [rename_elements](patterns/rename_elements.md) |
| 14 | **DrawDisplace** | .pmlfrm | 仪表管偏移量：从 CSV 导入阀门清单，标记 MAX/MIN 偏移 | SITE, EQUI, VALV, PCOM, INST, DAMP, BATT | `flnn`, `spref.flnn` | [file_io](patterns/file_io.md) |
| 15 | **RoomCheck** | .pmlfrm | 房间号检查：按专业检查 `:ROOM_NO` 长度 ≠5 的设备 | SITE, EQUI, VALV, PCOM, INST, DAMP, BATT, ZONE | `:ROOM_NO`, `:3D_SJRY`, `flnn` | [custom_attributes](patterns/custom_attributes.md), [list_grid_operations](patterns/list_grid_operations.md) |
| 16 | **CheckName** | .pmlfrm | 命名规则检查 + 自动分配房间号 | ZONE, STRU, FRMW, SBFR, BRAN, ATTA, EQUI, INST, VALV, DAMP, BATT, SCTN, SUPPO, TEXT, ELCONN | `:ROOM_NO`, `:DESCR`, `TYPE`, `FLNN`, `fullname` | [custom_attributes](patterns/custom_attributes.md), [collection_query](patterns/collection_query.md) |
| 17 | **modelCheck** | .pmlfnc | 电缆模型检查 .NET 启动器 | — | — | [net_integration](patterns/net_integration.md) |
| 18 | **ElecInfo** | .pmlfnc | 电气信息 .NET 启动器（UNC 路径加载） | — | — | [net_integration](patterns/net_integration.md) |

---

## 三、虚拟连接工具

| # | 工具名 | 类型 | 功能 | 元素类型 | 关键属性 | 对应范式 |
|:-:|--------|:----:|------|----------|---------|---------|
| 19 | **VirtualConn** | .pmlfrm | 虚拟管嘴连接主界面：创建/显示/删除 | HOLE | `conntray` | [query_elements](patterns/query_elements.md), [virtual_conn](patterns/virtual_conn.md) |
| 20 | **CheckConn** | .pmlfnc | 检查 TEXT 元素中编码的连接关系是否重复 | TEXT, PIPE | `:descr` | [custom_attributes](patterns/custom_attributes.md) |
| 21 | **drawConn** | .pmlfnc | 在设备之间找最近端口并绘制连接线 | EQUI, NBOX, NCYL, JLDATU, FIXING, FTUB, PFIT, SBFI, CMPF, FITT, BEND, TEE, CROS | `pos of`, `p1-p4 pos of`, `Distance()` | [geometry_operations](patterns/geometry_operations.md), [element_type_dispatch](patterns/element_type_dispatch.md) |
| 22 | **deletebyhole** | .pmlfrm | 按孔洞删除关联连接 | FITT, NBOX, NCYL, PFIT, SBFI, JLDATU, TEXT | `:DESCR`, `refno` | [net_integration](patterns/net_integration.md) |
| 23 | **SearchDif** | .pmlfrm | 虚拟连接核查：解析 descr、类型一致性、导出/导入 | TEXT, PFIT, SBFI, FITT, FTUB, BEND, TEE, CROS, ZONE | `:descr`, `type`, `owner.namn`, `flnm` | [custom_attributes](patterns/custom_attributes.md), [element_navigation](patterns/element_navigation.md) |
| 24 | **SearchDif-** | .pmlfrm | 简版 SearchDif：错误连接核查 | TEXT | `type`, `owner`, `namn` | [custom_attributes](patterns/custom_attributes.md) |
| 25 | **HoleExport** | .pmlfnc | 孔洞数据导出 .NET 启动器 | — | — | [net_integration](patterns/net_integration.md) |

---

## 四、数据处理/导出工具

| # | 工具名 | 类型 | 功能 | 元素类型 | 关键属性 | 对应范式 |
|:-:|--------|:----:|------|----------|---------|---------|
| 26 | **AttributeExport** | .pmlfrm | 从 CSV 导入设备清单 → 对比模型属性 → 标记差异 → 导出 | EQUI | `:ROOM_NO`, `:DESCR`, `TYPE`, `worpos` | [file_io](patterns/file_io.md), [custom_attributes](patterns/custom_attributes.md) |
| 27 | **ExclMerge** | .pmlfrm | 碰撞反馈 Excel 合并：多专业 Excel → 按碰撞号+物项名匹配 → 输出合并 | —（纯数据处理） | — | [file_io](patterns/file_io.md) |
| 28 | **EquipmentDrawList** | .pmlfnc + .txt | 设备清单 .NET 启动器（带版本热更新） | — | — | [net_integration](patterns/net_integration.md) |
| 29 | **ExploreTreeOrder** | .pmlfrm | 物项结构树顺序调整：`reorder before/after` | SITE, ZONE | `mem`, `name`, `pre`, `next`, `owner` | [element_navigation](patterns/element_navigation.md) |
| 30 | **ddzdistancecheck** | .pmlfnc | 距离检查 .NET 插件启动器（本地 DLL + 版本控制） | — | — | [net_integration](patterns/net_integration.md) |

---

## 五、综合/其他工具

| # | 工具名 | 类型 | 功能 | 元素类型 | 关键属性 | 对应范式 |
|:-:|--------|:----:|------|----------|---------|---------|
| 31 | **virtualEquipmentForm** | .pmlfrm | 虚拟设备 Form：多页签复杂工具 | EQUI | — | [list_grid_operations](patterns/list_grid_operations.md) |
| 32 | **ExclMerge** | .pmlfrm | （已列在上面） | — | — | — |
| 33 | **EquiInfo** | .pmlfnc | （已列在上面） | — | — | — |

---

## 六、工具 → 范式反向索引

> AI 可以这样用：搜范式名→找到哪些工具用了它→看真实代码

| 范式 | 使用该范式的工具（工具 #） |
|------|--------------------------|
| [query_elements](patterns/query_elements.md) | #2 conntray, #6 TrayClassifyAdd, #19 VirtualConn, #7 RiserChange |
| [modify_attributes](patterns/modify_attributes.md) | #5 ReWriteElecWidth, #8 PsecRefresh, #12 MdsMgbChange |
| [check_exists](patterns/check_exists.md) | #9 EquiCheck, #20 CheckConn |
| [check_distance](patterns/check_distance.md) | #21 drawConn, #30 ddzdistancecheck |
| [rename_elements](patterns/rename_elements.md) | #13 mrename |
| [file_io](patterns/file_io.md) | #9 EquiCheck, #14 DrawDisplace, #15 RoomCheck, #26 AttributeExport, #27 ExclMerge |
| [custom_attributes](patterns/custom_attributes.md) | #2 conntray, #15 RoomCheck, #16 CheckName, #20 CheckConn, #23 SearchDif, #26 AttributeExport |
| [element_navigation](patterns/element_navigation.md) | #23 SearchDif, #29 ExploreTreeOrder |
| [element_type_dispatch](patterns/element_type_dispatch.md) | #21 drawConn, #5 ReWriteElecWidth |
| [geometry_operations](patterns/geometry_operations.md) | #3 ddzcablewaysupporteditor, #4 RotateElecSuppo, #21 drawConn |
| [list_grid_operations](patterns/list_grid_operations.md) | #5 ReWriteElecWidth, #15 RoomCheck, #31 virtualEquipmentForm |
| [net_integration](patterns/net_integration.md) | #10 EquiInfo, #11 cnpedrawlist, #17 modelCheck, #18 ElecInfo, #25 HoleExport, #28 EquipmentDrawList, #30 ddzdistancecheck, #22 deletebyhole |
| [error_handling](patterns/error_handling.md) | #2 conntray, #10 EquiInfo, #13 mrename, #20 CheckConn 等几乎所有工具 |
| [collection_query](patterns/collection_query.md) | #8 PsecRefresh, #16 CheckName, #7 RiserChange |
| [virtual_conn](patterns/virtual_conn.md) | #19 VirtualConn |
| [check_bore_consistency](patterns/check_bore_consistency.md) | #2 conntray |
| [check_attribute_complete](patterns/check_attribute_complete.md) | #7 RiserChange, #16 CheckName |

---

## 七、工具 → 元素类型反向索引

> AI 可以这样用：搜元素类型→找到操作它的工具→看真实代码

| 元素类型 | 操作它的工具 |
|----------|------------|
| **ZONE** | #2 conntray, #6 TrayClassifyAdd, #8 PsecRefresh, #15 RoomCheck, #16 CheckName, #23 SearchDif, #29 ExploreTreeOrder |
| **PIPE** | #8 PsecRefresh, #20 CheckConn |
| **BRAN** | #5 ReWriteElecWidth, #6 TrayClassifyAdd, #7 RiserChange, #8 PsecRefresh, #16 CheckName |
| **FTUB** | #2 conntray, #3 ddzcablewaysupporteditor, #5 ReWriteElecWidth, #7 RiserChange, #8 PsecRefresh, #21 drawConn, #23 SearchDif |
| **EQUI** | #9 EquiCheck, #14 DrawDisplace, #15 RoomCheck, #16 CheckName, #21 drawConn, #26 AttributeExport, #31 virtualEquipmentForm |
| **VALV** | #9 EquiCheck, #14 DrawDisplace, #15 RoomCheck |
| **STRUCTURE (STRU/SCTN/SUPPO 等)** | #2 conntray, #3 ddzcablewaysupporteditor, #4 RotateElecSuppo, #16 CheckName |
| **TEXT** | #16 CheckName, #20 CheckConn, #22 deletebyhole, #23 SearchDif |
| **HOLE** | #19 VirtualConn, #22 deletebyhole |
| **NOZZ** | #21 drawConn |
| **TEE/BEND/ELBO** | #2 conntray, #21 drawConn, #23 SearchDif |
| **CROS/REDU** | #2 conntray, #21 drawConn, #23 SearchDif |
| **FITT/PFIT/SBFI** | #21 drawConn, #22 deletebyhole, #23 SearchDif |
| **SITE** | #8 PsecRefresh, #9 EquiCheck, #14 DrawDisplace, #15 RoomCheck, #29 ExploreTreeOrder |

---

## 八、工具文件类型说明

| 后缀 | 说明 | 数量 |
|:----:|------|:----:|
| `.pmlfrm` | **Form** — 带 GUI 的完整工具（含 Frame/Button/List/TEXT/TOGGLE 等控件） | ~24 |
| `.pmlfnc` | **Function** — 纯逻辑函数，无 GUI，常用作 .NET 启动器 | ~9 |
| `.txt` | 配置/批处理脚本 | ~1 |
