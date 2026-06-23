# PML Forms 完整参考

> PML 窗体（.pmlfrm）用于构建 E3D 内嵌对话框

## 基本结构

```pml
SETUP FORM !!FormName [DIALOG] [RESIZABLE]

    -- 标题
    TITLE '对话框标题'

    -- 构造器调用（可选）
    !this.formTitle = '标题'
    !this.quitCall = '|!this.close()|'
    !this.firstShownCall = '|!this.init()|'

    -- 控件定义
    FRAME .f1 '分组' AT x1 y1 WIDTH 30 HEIGHT 20
        BUTTON .ok '确定' WIDTH 6 CALL |!this.doAction()|
        TEXT .input '输入' AT x1 ymax+0.5 WIDTH 20 IS STRING
        LIST .list AT x1 ymax+0.5 WIDTH 30 LENGTH 10
        TOGGLE .tog1 '选项1' AT x1 ymax+0.5
        PARAGRAPH .msg '提示信息' AT x1 ymax+0.5 WIDTH 20
    EXIT

    -- 成员变量
    MEMBER .data is ARRAY

EXIT

-- 构造器
DEFINE METHOD .FormName()
    -- 初始化逻辑
ENDMETHOD

-- 初始化
DEFINE METHOD .init()
    -- 加载数据
ENDMETHOD

-- 关闭
DEFINE METHOD .close()
    KILL !!FormName
ENDMETHOD
```

## 常用控件

| 控件 | 用法 | 说明 |
|------|------|------|
| BUTTON | `BUTTON .name '标签' WIDTH N CALL \|!this.action()\|` | 按钮 |
| TEXT | `TEXT .name AT x y WIDTH N IS STRING` | 文本输入框 |
| PARAGRAPH | `PARAGRAPH .name '文本' AT x y` | 静态文字 |
| TOGGLE | `TOGGLE .name '标签' AT x y` | 复选框 |
| LIST | `LIST .name AT x y WIDTH N LENGTH N` | 列表框 |
| FRAME | `FRAME .name '标题' AT x y WIDTH N HEIGHT N` | 分组框 |
| TABSET | `FRAME .tabset TABSET 'tabset' AT x y WIDTH N ANCHOR ALL` | 多页签 |
| OPTION | `OPTION .name '标签' AT x y` | 单选按钮 |
| CONTAINER | `CONTAINER .name PmlNetControl 'CtrlName' Dock Fill` | .NET 控件容器 |

## 控件定位语法

| 定位 | 说明 |
|------|------|
| `x1 y1` | 绝对坐标 |
| `xmin.f1` | 取控件 f1 的左侧 |
| `ymax.f1` | 取控件 f1 的底部 |
| `xmin.f1 + 2` | 控件 f1 左侧 + 2 单位 |
| `ANCHOR L+B+T+R` | 锚定（左+下+上+右） |

## 显示与关闭

```pml
-- 显示对话框
PmlRehash ALL
SHOW !!FormName

-- 关闭
HIDE !!FormName

-- 彻底销毁
KILL !!FormName
```

## 多页签（TABSET）结构

```pml
FRAME .tabset TABSET 'tabset' AT x0.3 y0.15 WIDTH 35 ANCHOR ALL
    FRAME .page1 '页面1' AT x0 ymax
        -- 页面1控件
    EXIT

    FRAME .page2 '页面2' AT x0 ymax
        -- 页面2控件
    EXIT
EXIT
```

## .NET 控件嵌入

```pml
FRAME .netFrame AT x1 y1 WIDTH 40 HEIGHT 20
    CONTAINER .pane PmlNetControl 'MyControl' Dock Fill
EXIT

DEFINE METHOD .init()
    !ctrl = OBJECT MyDataGrid()
    !this.pane.control = !ctrl.handle()
    !ctrl.addEventHandler('CellClick', !this, 'onCellClick')
ENDMETHOD
```
