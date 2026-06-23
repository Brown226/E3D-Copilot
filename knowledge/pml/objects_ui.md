# PML UI 控件对象完整参考

> 来源: Software Customisation Reference Manual Chapter 2
> 覆盖: FORM, BUTTON, TEXT, LIST, TOGGLE, FRAME, PARAGRAPH, TEXTPANE, OPTION, COMBOBOX, RTOGGLE, SELECTOR, SLIDER, CONTAINER, BAR, MENU, VIEW(ALPHA/AREA/PLOT/VOLUME)

---

## FORM 对象

### 成员

| 属性 | 类型 | 说明 |
|------|:----:|------|
| `.FormTitle` | STRING | 表单标题 |
| `.IconTitle` | STRING | 图标标题 |
| `.FormRevision` | STRING | 版本号 |
| `.Initcall` | STRING | 初始化回调 |
| `.Okcall` | STRING | OK 按钮回调 |
| `.Cancelcall` | STRING | CANCEL 回调 |
| `.Quitcall` | STRING | 关闭按钮回调 |
| `.Killingcall` | STRING | 销毁回调 |
| `.FirstShowncall` | STRING | 首次显示回调 |
| `.Autocall` | STRING | 属性变化回调 |
| `.Active` | BOOLEAN | 活动状态 |
| `.Maximised` | BOOLEAN | 最大化状态 |
| `.Popup` | MENU | 弹出菜单 |
| `.KeyboardFocus` | GADGET | 初始焦点控件 |
| `.AutoScroll` | BOOLEAN | 自动滚动条 |
| `.HelpContextID` | STRING | 帮助上下文 ID |

### 方法

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `!form.Name()` | STRING | 窗体名 |
| `!form.FullName()` | STRING | 完整名(含!!) |
| `!form.Shown()` | BOOLEAN | 是否显示 |
| `!form.Show('FREE')` | — | 显示为自由窗口 |
| `!form.Show('AT', x, y)` | — | 在指定位置显示 |
| `!form.Show('CEN', x, y)` | — | 居中显示 |
| `!form.Hide()` | — | 隐藏 |
| `!form.SetActive(bool)` | — | 激活/变灰 |
| `!form.SetGadgetsActive(bool)` | — | 变灰所有控件 |
| `!form.Owner()` | FORM | 父窗体 |
| `!form.NewMenu('name')` | MENU | 新建菜单 |
| `!form.SetPopup(!menu)` | — | 设置弹出菜单 |
| `!form.RemovePopup(!menu)` | — | 移除弹出菜单 |
| `!form.GetPickedPopup()` | MENU | 最后选择的弹出菜单 |
| `!form.Subtype()` | STRING | 子类型 |
| `!form.SetOpacity(!pct)` | — | 设置透明度(10-100) |

### 控件通用成员

所有控件（Gadget）共享以下成员：

| 成员 | 类型 | 说明 |
|------|:----:|------|
| `.Name` | STRING | 控件名 |
| `.Active` | BOOLEAN | 活跃/变灰 |
| `.Val` | ANY | 控件值 |
| `.Background` | REAL/STRING | 背景色 |
| `.Highlight` | REAL/STRING | 高亮色 |
| `.Callback` | STRING | 回调字符串 |
| `.Visible` | BOOLEAN | 可见性 |
| `.Tag` | STRING | 标签文字 |

### 控件通用方法

| 方法 | 说明 |
|------|------|
| `!g.SetFocus()` | 设置键盘焦点 |
| `!g.SetEditable(bool)` | 设置是否可编辑 |

---

## BUTTON 控件

### 附加成员

| 成员 | 类型 | 说明 |
|------|:----:|------|
| `.Subtype` | STRING | 按钮类型('PUSH'/'CHECK'/'MENU') |
| `.Tooltip` | STRING | 提示文字 |

### 附加方法

| 方法 | 说明 |
|------|------|
| `!btn.AddPixmap('file')` | 添加图标 |
| `!btn.SetPopup(!menu)` | 设置弹出菜单 |

---

## TEXT 控件

### 附加成员

| 成员 | 类型 | 说明 |
|------|:----:|------|
| `.IsString` | BOOLEAN | 输入字符串 |
| `.IsReal` | BOOLEAN | 输入实数 |
| `.Width` | REAL | 宽度 |
| `.Tagwid` | REAL | 标签宽度 |

### Form 定义语法

```pml
TEXT .name '标签' AT x y WIDTH n [IS STRING | IS REAL | IS BOOLEAN]
```

---

## LIST 控件

### 附加成员

| 成员 | 类型 | 说明 |
|------|:----:|------|
| `.Dtext` | ARRAY | 显示文本数组 |
| `.Count` | REAL | 项数 |

### 附加方法

| 方法 | 说明 |
|------|------|
| `!list.Clear()` | 清空 |
| `!list.SetHeadings(!arr)` | 设置列头 |
| `!list.SetRows(!arr)` | 设置多行数据 |
| `!list.Selection()` | 获取选中值 |
| `!list.Selection('Dtext')` | 获取选中显示文本 |

### Form 定义语法

```pml
LIST .name AT x y WIDTH n LENGTH n
```

---

## TOGGLE 控件

复选框。

### 附加成员

| 成员 | 类型 | 说明 |
|------|:----:|------|
| `.Val` | BOOLEAN | 选中状态 |

### Form 定义语法

```pml
TOGGLE .name '标签' AT x y
```

---

## FRAME 控件

分组框/页签容器。

### 附加方法

| 方法 | 说明 |
|------|------|
| `!frame.SetText('title')` | 设置标题 |

### TABSET（多页签）

```pml
FRAME .tabset TABSET 'tabset' AT x y WIDTH n ANCHOR ALL
    FRAME .page1 '页1' AT x0 ymax
        -- 页1控件
    EXIT
    FRAME .page2 '页2' AT x0 ymax
        -- 页2控件
    EXIT
EXIT
```

---

## PARAGRAPH 控件

静态文本标签。

### Form 定义语法

```pml
PARAGRAPH .name '文本' AT x y [WIDTH n]
```

---

## TEXTPANE 控件

多行文本编辑框。

### 附加成员

| 成员 | 类型 | 说明 |
|------|:----:|------|
| `.Val` | STRING | 文本内容 |

### 方法

| 方法 | 说明 |
|------|------|
| `!tp.Clear()` | 清空 |
| `!tp.Append('text')` | 追加文本 |
| `!tp.SetEditable(bool)` | 设置是否可编辑 |

---

## OPTION 控件

单选按钮。

### 附加成员

| 成员 | 类型 | 说明 |
|------|:----:|------|
| `.Val` | REAL | 选中索引 |

---

## COMBOBOX 控件

下拉选择框。

### 附加方法

| 方法 | 说明 |
|------|------|
| `!cb.Add('text')` | 添加选项 |
| `!cb.Clear()` | 清空 |
| `!cb.Select(n)` | 选中第 n 项 |
| `!cb.Delete(n)` | 删除第 n 项 |
| `!cb.Count()` | 选项数量 |

---

## RTOGGLE 控件

单选按钮组。

---

## SELECTOR 控件

选择器（数字滚动）。

| 方法 | 说明 |
|------|------|
| `!sel.SetRange(min, max)` | 设置范围 |
| `!sel.SetStep(n)` | 设置步长 |

---

## SLIDER 控件

滑动条。

| 方法 | 说明 |
|------|------|
| `!sl.SetRange(min, max)` | 设置范围 |

---

## CONTAINER 控件

.NET 控件容器。

| 成员 | 类型 | 说明 |
|------|:----:|------|
| `.Control` | HANDLE | .NET 控件句柄 |

---

## BAR 对象

菜单栏。

| 方法 | 说明 |
|------|------|
| `!bar.Add('label', !menu)` | 添加菜单 |
| `!bar.Insert(n, 'label', !menu)` | 插入菜单 |
| `!bar.SetActive('label', bool)` | 设置活跃状态 |
| `!bar.Remove('label')` | 移除菜单 |
| `!bar.SetFieldProperty(...)` | 设置属性 |
| `!bar.FieldProperty(...)` | 获取属性 |

---

## MENU 对象

### 成员

| 成员 | 类型 | 说明 |
|------|:----:|------|
| `.Name` | STRING | 菜单名 |
| `.Type` | STRING | 类型(POPUP/MAIN) |
| `.Active` | BOOLEAN | 活跃状态 |

### 方法

| 方法 | 说明 |
|------|------|
| `!menu.Add('label', 'cmd')` | 添加菜单项 |
| `!menu.Insert(n, 'label', 'cmd')` | 插入菜单项 |
| `!menu.AddSeparator()` | 添加分隔线 |
| `!menu.Remove('label')` | 移除菜单项 |
| `!menu.SetActive('label', bool)` | 设置活跃状态 |
| `!menu.AddMenu('label', !submenu)` | 添加子菜单 |
| `!menu.Clear()` | 清空 |

---

## VIEW 控件 (ALPHA/AREA/PLOT/VOLUME)

### ALPHA 视图（命令行窗口）

| 方法 | 说明 |
|------|------|
| `!view.Clear()` | 清空 |
| `!view.Refresh()` | 刷新 |
| `!view.SetFocus()` | 设置焦点 |
| `!view.RemoveRequests()` | 移除请求通道 |

### AREA 视图（2D 图形）

| 成员 | 说明 |
|------|------|
| `.Limits[4]` | 显示范围 [x1,y1,x2,y2] |
| `.Background` | 背景色 |
| `.Highlight` | 高亮色 |
| `.Borders` | 边框 |

| 方法 | 说明 |
|------|------|
| `!view.Clear()` | 清空 |
| `!view.Refresh()` | 刷新 |
| `!view.SaveView(n)` | 保存视图(1-4) |
| `!view.RestoreView(n)` | 恢复视图 |
| `!view.SetSize(w, h)` | 设置大小 |

### PLOT 视图（绘图）

| 方法 | 说明 |
|------|------|
| `!view.Add('file.plt')` | 添加绘图文件 |
| `!view.Clear()` | 清空 |
| `!view.Refresh()` | 刷新 |
| `!view.SetSize(w, h)` | 设置大小 |

### VOLUME 视图（3D Design）

| 成员 | 说明 |
|------|------|
| `.Limits[6]` | 范围 [E1,E2,N1,N2,U1,U2] |
| `.Direction[3]` | 视角方向 [dE,dN,dU] |
| `.Through[3]` | 视点 [E,N,U] |
| `.Radius` | 视角半径 |
| `.Range` | 视距 |
| `.Step` | 步长 |
| `.Projection` | 投影('PERSPECTIVE'/'PARALLEL') |
| `.Mousemode` | 鼠标模式('ZOOM'/'PAN'/'ROTATE'/'WALK') |
| `.Shaded` | 着色显示 |
| `.EyeMode` | 眼睛模式 |
| `.Borders` | 边框 |
| `.Background` | 背景色 |
| `.Highlight` | 高亮色 |
| `.LabelStyle` | 标签风格('ENU'/'XYZ') |

| 方法 | 说明 |
|------|------|
| `!view.Refresh()` | 刷新 |
| `!view.SaveView(n)` | 保存视图 |
| `!view.RestoreView(n)` | 恢复视图 |
| `!view.SetSize(w, h)` | 设置大小 |

### VIEW Form 定义语法

```pml
VIEW .name ALPHA HEI n WIDTH n [CHANNEL COMMANDS] [CHANNEL REQUESTS]
VIEW .name AREA WIDTH n HEI n
VIEW .name PLOT WIDTH n HEI n
VIEW .name VOLUME WIDTH n HEI n
```
