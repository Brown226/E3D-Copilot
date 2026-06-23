# 黄金范式：错误处理策略

**用途**: PML 错误处理的各种模式
**验证**: ✅ 来自多个真实工具

## 策略A：静默吞错

```pml
-- 用于 DLL 加载等可能失败的操作
import |\\server\share\Dll.dll|
HANDLE ANY
ENDHANDLE
```

## 策略B：特定错误码处理

```pml
-- 用于元素导航等操作
NEXT
HANDLE (2, 113)           -- 没有更多元素
    $P '已到达末尾'
ELSEHANDLE (2, 111)        -- 元素不存在
    $P '元素无效'
ELSEHANDLE NONE
    $P '操作成功'
ENDHANDLE
```

## 策略C：错误码 + 跳过

```pml
-- 用于遍历中忽略特定错误
DO !ELE values !LIST
    !name = !ELE.Dbref().:NAME
    HANDLE (47, 15)          -- 元素不存在
        SKIP
    ENDHANDLE
    $P !name
ENDDO
```

## 策略D：错误 + 日志 + 返回

```pml
HANDLE (2, 25)               -- 只读数据库
    !this.WriteLog('只读DB，不可修改')
    RETURN
ENDHANDLE
```

## 策略E：全局 ON ERROR

```pml
ON ERROR
    $P '发生错误: ', $ERROR.Message
    $P '错误码: ', $ERROR.Code
ENDON
```

## 常见错误码参考

| 错误码 | 含义 |
|:------:|------|
| (2, 25) | 只读数据库 |
| (2, 46) | 无权限 |
| (2, 111) | 元素不存在 |
| (2, 113) | 已到达末尾 |
| (47, 15) | 元素未找到 |
| (99, 532) | 属性不存在 |
| (2, 503) | 属性只读 |
