# 黄金范式：文件浏览器与 CSV 导入导出

**用途**: 让用户选择文件、读取 CSV/Excel、导入到 E3D 或导出结果
**验证**: ✅ 来自 DrawDisplace / EquiCheck / RoomCheck / ExclMerge 等真实工具

## 文件浏览器对话框

```pml
-- 需要先导入 PMLFileBrowser
import 'PMLFileBrowser'
HANDLE ANY
ENDHANDLE

-- 打开文件（OPEN 模式）
!browser = OBJECT PMLFileBrowser('OPEN')
!browser.show('D:\', '', '选择文件', false, 'Excel Documents|*.csv', 1)
!fileName = !browser.file()

-- 保存文件（SAVE 模式）
!browser = OBJECT PMLFileBrowser('SAVE')
!browser.show('D:\', 'result.csv', '保存结果', false, 'Excel Documents|*.csv', 1)
!savePath = !browser.file()
```

## CSV 文件读取

```pml
-- 读取全部内容（支持大文件）
!file = OBJECT FILE('$!fileName')
!file.OPEN('READ')
!lines = !file.ReadFile(30000)   -- 参数 = 最大行数
!file.CLOSE()

-- 逐行解析
!headers = !lines[1].split(',')
DO !i FROM 2 TO !lines.Size() BY 1
    !row = !lines[!i].split(',')
    !col1 = !row[1]
    !col2 = !row[2]
    -- 处理每一行
ENDDO
```

## CSV 文件写入

```pml
-- 构建输出数组
!output = ARRAY()

-- 添加表头
!headerLine = '名称,规格,壁厚'
!output.Append(!headerLine)

-- 写数据行
DO !ELE values !LIST
    !line = !ELE.Name + ',' + !ELE.Dbref().:SPEC + ',' + !ELE.Dbref().:WTHK
    !output.Append(!line)
ENDDO

-- 写入文件
!outFile = OBJECT FILE('输出.csv')
!outFile.writefile('write', !output)
!outFile.CLOSE()
```

## Excel I/O（通过 .NET NetGridControl）

```pml
-- 读取 Excel
!grid = OBJECT NetGridControl()
!nds = OBJECT NetDataSource('Sheet1', !filePath)
!grid.BindToDataSource(!nds)
!rows = !grid.getRows()

-- 写入 Excel
!nds = OBJECT NetDataSource('Sheet1', !headers, !dataRows)
!grid.BindToDataSource(!nds)
!grid.SaveGridToExcel(!filePath)
```
