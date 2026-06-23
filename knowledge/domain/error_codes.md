# E3D 常见错误码和原因

> 用于诊断 PML/C# 执行错误。

## 常见 PdmsException 错误

| 错误信息 | 可能原因 | 解决方法 |
|----------|---------|---------|
| `Element not found` | 元素名称不存在 | 核对元素名大小写和路径 |
| `Attribute not found` | 属性名不存在或拼写错误 | 核对属性名（如 :WTHK 非 :WALLTHK） |
| `No current element` | 未设置 CE | 先执行 `!!ce` 或 `CurrentElement.Element = element` |
| `Database not open` | 数据库未打开 | 检查是否打开了项目 |
| `Permission denied` | 权限不足 | 检查用户角色和写权限 |
| `Element is locked` | 元素被其他用户锁定 | 等待或让管理员解锁 |
| `Cannot delete element with children` | 删除非空元素 | 先删除子元素 |
| `Invalid attribute value` | 属性值格式错误 | 检查值类型（String/Real/Integer） |
| `Transaction in progress` | 事务未提交 | 调用 `DbTransaction.Commit()` 或 `Rollback()` |
| `Type mismatch` | 类型不匹配 | 检查 C# 类型是否为期望类型 |
| `Command failed` | PML 命令执行失败 | 检查 PML 语法和上下文 |

## PML 运行时错误

| 错误 | 原因 | 解决 |
|------|------|------|
| `!var is not an object` | 变量未初始化 | 先赋初值再使用 |
| `Coll is empty` | 集合查询无结果 | 放宽查询条件 |
| `Division by zero` | 除零 | 加非零判断 |
| `Array index out of bounds` | 数组越界 | 检查数组大小 |
| `File not found` | 文件找不到 | 检查文件路径 |
| `Type mismatch in expression` | 类型不匹配 | 检查变量类型 |

## 调试技巧

```pml
-- 输出调试信息
$P '!var 的值 = ', !var

-- 检查元素是否存在
IF exist !ele THEN
    $P '元素存在: ', !ele.Name
ELSE
    $P '元素不存在'
ENDIF

-- 错误捕获
ON ERROR
    $P '错误发生: ', $ERROR.Message
ENDON
```

```csharp
// C# 中捕获 PdmsException
try
{
    element.SetAttribute(DbAttributeInstance.WTHK, "SCH40");
}
catch (PdmsException ex)
{
    // 友好提示
    PdmsMessage.Show(ex.Message, "E3D 错误", PdmsMessageButtons.OK);
}
```
