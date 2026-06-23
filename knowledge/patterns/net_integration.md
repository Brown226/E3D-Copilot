# 黄金范式：.NET 集成（PmlNetCall / PmlNetAddin）

**用途**: 从 PML 中调用 .NET DLL 启动 WinForms 窗口或 E3D 嵌入式插件
**验证**: ✅ 来自 ElecInfo / cnpedrawlist / modelCheck / ddzdistancecheck 等真实工具

## 层级1：独立窗口启动（PmlNetCall）

```pml
-- 从 UNC 路径或本地路径加载 DLL
import |\\server\share\MyTool.dll|

-- 忽略加载失败
handle any
endhandle

-- 指定命名空间
using namespace |MyToolNamespace|

-- 启动独立窗口
!obj = Object PmlNetCall()
!obj.Start()
```

## 层级2：E3D 嵌入式插件（PmlNetAddin）

```pml
-- 加载 DLL
import |$!dllPath\MyAddin.dll|
using namespace |MyAddinNamespace|

-- 作为插件嵌入 E3D 主界面
!obj = Object PmlNetAddin()
!obj.Start()
```

## 层级3：Form 内嵌 .NET 控件

```pml
-- 在 PML Form 中嵌入 .NET 控件
container .pane PmlNetControl 'ControlName' Dock Fill

!ctrl = object MyGridControl()
!pane.control = !ctrl.handle()
!ctrl.addeventhandler('CellClick', !this, 'onCellClick')
```

## 带版本控制的 DLL 加载

```pml
!version = '20251106'
!versionFlag = !!myToolVersion

handle any
    !!myToolVersion = !version
endhandle

IF !versionFlag neq !version THEN
    !!alert.Message('请重启 E3D 以更新工具版本')
    RETURN
ENDIF

-- 获取自身路径
!path = !!pml.getpathName('mytool.pmlfnc')
!file = object File(!path)
!dllPath = !file.directory().fullName() + '\' + !version

import |$!dllPath\MyTool.dll|
using namespace |MyTool|
!obj = Object PmlNetCall()
!obj.Start()
```
