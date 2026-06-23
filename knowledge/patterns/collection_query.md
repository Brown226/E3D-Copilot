# 黄金范式：Collection 对象式查询

**用途**: 使用 Collection 对象（而非 coll all 语法）查询元素
**验证**: ✅ 来自 CheckName / ReWriteElecWidth 真实工具

## 对象式查询（替代 coll all）

```pml
-- 创建 Collection 对象
!coll = OBJECT COLLECTION()
!coll.type('PIPE')          -- 元素类型
!coll.scope(!ce.Dbref())    -- 搜索范围
!results = !coll.results()  -- 返回数组
```

## 通过 first 导航

```pml
-- 获取第一个子元素
!ftub = FIRST FTUB OF $!element
HANDLE (2, 111) (2, 113)    -- 元素不存在
ELSEHANDLE NONE
    $P '找到 FTUB: ', !ftub.Name
ENDHANDLE

-- 遍历兄弟
NEXT                              -- 移到下一个
HANDLE (2, 113)                   -- 没有更多了
ENDHANDLE
```

## C# 等价实现

```csharp
using Aveva.Core.Database;

// 遍历子元素
DbElement child = element.FirstMember();
while (child.IsValid)
{
    string type = child.GetAsString(DbAttributeInstance.Type);
    Console.WriteLine(child.Name);
    child = element.NextMember(child);
}
```

## 按类型 + 条件查询

```pml
-- 带匹配条件
VAR !LIST COLL ALL PIPE WITH MATCH(NAME,|*DN100*|) neq 0 FOR $!ce

-- 或使用 Matchwild
VAR !LIST COLL ALL PIPE WITH Matchwild(NAME, '*DN100*', true) FOR $!ce
```
