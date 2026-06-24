# 黄金范式：List/Grid 控件与 UI 交互

**用途**: PML Form 中列表控件的高级操作（多列、选中、刷新）
**验证**: ✅ 来自 DrawDisplace / RoomCheck / TrayClassifyAdd 等真实工具
**preferred_tool**: `execute_pml` — UI 控件操作仅在 PML Form 中使用

## 列表初始化

```pml
-- 设置列头（字符串，逗号分隔）
!heads = '名称,规格,壁厚,流体'
!this.list1.setHeadings(!heads.split(','))

-- 设置回调
!this.list1.callback = '!this.onSelect()'
```

## 填充列表数据

```pml
-- 单列（dtext）
!this.list1.clear()
!this.list1.dtext = !nameArray

-- 多列（setRows — 数组的数组）
!rows = ARRAY()
DO !i FROM 1 TO !data.Size() BY 1
    !row = ARRAY()
    !row.Append(!data[!i].Name)
    !row.Append(!data[!i].Dbref().:SPEC)
    !row.Append(!data[!i].Dbref().:WTHK)
    !rows.Append(!row)
ENDDO
!this.list1.setRows(!rows)
```

## 读取列表选中值

```pml
-- 选中项的值
!selected = !this.list1.selection()
$P '选中: ', !selected

-- 选中项的显示文本
!displayText = !this.list1.selection('Dtext')
$P '显示文本: ', !displayText

-- 选中索引
!index = !this.list1.val
```

## TEXTPANE 多行文本

```pml
-- 多行文本控件
TEXTPANE .text1 at x0 y0.5 WIDTH 23 HEIGHT 12

-- 读取内容
!content = !this.text1.val

-- 写入内容
!this.text1.val = '多行文本内容'
```

## 控件计数

```pml
!count = !this.list1.count
$P '共 ', !count, ' 条记录'
```

## 常见 UI 回调

```pml
-- 选中回调
DEFINE METHOD .onSelect()
    !selected = !this.list1.selection()
    $!selected    -- 导航到选中元素
ENDMETHOD

-- 双击回调
DEFINE METHOD .onDoubleClick()
    !selected = !this.list1.selection()
    SHOW $!selected   -- 在 3D 视图中显示
ENDMETHOD
```
