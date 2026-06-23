# 黄金范式：导出报表

**用途**: 将查询结果导出为报表或文件
**验证**: ✅ 使用 PML REPORT 对象

## PML REPORT 原生报表

```pml
-- 创建报表对象
!report = OBJECT REPORT

-- 设置报表属性
!report.title = '管道清单'
!report.author = 'E小智'

-- 查询数据
VAR !LIST COLL ALL PIPE FOR CE

-- 遍历写入报表
DO !ELE values !LIST
    !report.line()
    !report.column(1, !ELE.Name)
    !report.column(2, !ELE.Dbref().:SPEC)
    !report.column(3, !ELE.Dbref().:WTHK)
ENDDO

-- 输出
!report.print()
```

## PML 文件导出（CSV 格式）

```pml
-- 打开文件
!file = FILE 'output.csv' OPEN FOR WRITE

-- 写表头
!file.writeline('NAME,SPEC,WTHK')

-- 写数据
VAR !LIST COLL ALL PIPE FOR CE
DO !ELE values !LIST
    !line = !ELE.Name + ',' + !ELE.Dbref().:SPEC + ',' + !ELE.Dbref().:WTHK
    !file.writeline(!line)
ENDDO

-- 关闭文件
!file.close()
```
