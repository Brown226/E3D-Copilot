# 黄金范式：元素导航与选择操作

**用途**: 切换到指定元素、获取选中状态、元素树遍历
**验证**: ✅ 来自 DrawDisplace / ExploreTreeOrder / SearchDif 等真实工具

## 导航到元素

```pml
-- 切换到元素（$! 语法）
$!elementName

-- 或通过完整路径
$/SITE-001/ZONE-01/PIPE-001

-- 切换到元素后自动居中
$!elementName
AUTO CE

-- 切换后获取当前元素信息
$!itemName
!type = :TYPE
!name = :name
```

## 元素存在性检查

```pml
-- 方式1：EXISTS 关键字
VAR !FLAG EXISTS $!elementName
IF !FLAG EQ 'TRUEA' THEN
    $P '元素存在'
ENDIF

-- 方式2：导航 + Handle 错误
$!elementName
HANDLE (2, 107)   -- 未定义
    $P '元素不存在'
ELSEHANDLE (2, 109)  -- 未找到
    $P '元素未找到'
ELSEHANDLE NONE
    $P '元素存在: ', :name
ENDHANDLE
```

## 元素树遍历

```pml
-- 获取上一个/下一个兄弟
!pre = PRE
!next = NEXT
HANDLE (2, 113)    -- 已到达末尾
ENDHANDLE

-- 获取父元素和子元素
!owner = OWNER
!firstChild = !!ce.mem[1]
```

## 选择集操作

```pml
-- 添加到当前选择
ADD $!elementName

-- 清除选择
CLEAR

-- 显示选择
SHOW $!elementName
```

## 重新排序

```pml
-- 在结构树中移动元素
$!elementName
!pre = PRE            -- 获取前一个兄弟
!owner = OWNER        -- 获取父元素
$!ownerName
REORDER $!element BEFORE $!pre    -- 移到前面
-- 或
REORDER $!element AFTER $!next    -- 移到后面
```

## C# 等价实现

```csharp
using Aveva.Core.Database;

// 设置当前元素
CurrentElement.Element = element;

// 获取兄弟
DbElement parent = element.Owner();
DbElement prev = parent.PreviousMember(element);
DbElement next = parent.NextMember(element);

// 遍历子元素
DbElement child = element.FirstMember();
while (child.IsValid)
{
    Console.WriteLine(child.Name);
    child = element.NextMember(child);
}
```
