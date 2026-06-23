# Piping 管道操作完整参考

**命名空间**: `Aveva.Core.Piping`（⚠️ 非 `Aveva.Pdms.Piping`）
**用途**: 管道元素的创建、管件操作、Fabrication（加工制造）

## 管道层次结构

```
ZONE
└── PIPE (管道)
    └── BRAN (分支)
        ├── FTUB (直管段/管件)
        ├── ELBO (弯头) / BEND
        ├── TEE (三通)
        ├── REDU (异径管)
        ├── VALV (阀门)
        ├── FLAN (法兰)
        ├── CAP (管帽)
        └── NOZZ (管嘴)
```

## C# 创建管道元素

```csharp
using Aveva.Core.Database;

// 创建管道
DbElement zone = DbElement.GetElement("ZONE-01");
DbElement pipe = zone.AddNew("PIPE");
pipe.SetAttribute(DbAttributeInstance.Name, "100-P-001");
pipe.SetAttribute(DbAttributeInstance.Spec, "CS150");

// 创建分支
DbElement branch = pipe.AddNew("BRAN");
branch.SetAttribute(DbAttributeInstance.Name, "A1");
branch.SetAttribute(DbAttributeInstance.PSpec, "CS150");

// 添加弯头
DbElement elbow = branch.AddNew("ELBO");
elbow.SetAttribute(DbAttributeInstance.Name, "ELBOW-001");

// 添加阀门
DbElement valve = branch.AddNew("VALV");
valve.SetAttribute(DbAttributeInstance.Name, "VALVE-001");
```

## C# 遍历管道元素

```csharp
// 获取管道下的所有分支
DbElement pipe = DbElement.GetElement("100-P-001");
DbElement branch = pipe.FirstMember();
while (branch.IsValid)
{
    string type = branch.GetAsString(DbAttributeInstance.Type);
    string spec = branch.GetAsString(DbAttributeInstance.PSpec);
    Console.WriteLine($"{branch.Name} ({type}) spec={spec}");

    // 遍历分支下的管件
    DbElement fitment = branch.FirstMember();
    while (fitment.IsValid)
    {
        Console.WriteLine($"  {fitment.Name} ({fitment.GetAsString(DbAttributeInstance.Type)})");
        fitment = branch.NextMember(fitment);
    }

    branch = pipe.NextMember(branch);
}
```

## Fabrication 子命名空间 API

### PipePieceManager — 管段管理器

```csharp
using Aveva.Core.Piping.Fabrication;

var ppm = new PipePieceManager();
DbElement pipe = DbElement.GetElement("100-P-001");

// 生成管段
ppm.GeneratePipePieces(pipe);

// 获取管段列表
List<DbElement> pieces = ppm.GetPipePieces(pipe);
foreach (var piece in pieces)
{
    Console.WriteLine($"管段: {piece.Name}");
}

// 移除管段
ppm.RemovePipePieces(pipe);
```

### PipeSpoolManager — 管段组件管理器

```csharp
var psm = new PipeSpoolManager();

// 为管道生成管段组件
DbElement pipe = DbElement.GetElement("100-P-001");
psm.GeneratePipeSpoolsForPipe(pipe);

// 获取元素所属的管段组件
DbElement spool = psm.GetPipeSpoolForElement(element);

// 获取管段组件包含的管段
List<DbElement> pieces = psm.GetPipePiecesForSpool(spool);
```

### FabricationMachineManager — 加工设备管理器

```csharp
var fmm = new FabricationMachineManager();

// 设置弯管机
fmm.SetBendingMachine(bendingMachineElement);

// 检查管段是否可弯曲
bool canBend = fmm.BendingMachineAcceptsPipePiece(pipePiece);

// 获取弯管分析结果
BendingMachineResult result = fmm.GetBendingMachineResult(pipePiece);
if (result.Pass)
{
    Console.WriteLine($"切割长度: {result.CutLength}");
    Console.WriteLine($"成品长度: {result.FinishedLength}");
}
```

### BendingMachineResult — 弯管分析结果

| 属性 | 类型 | 说明 |
|------|:----:|------|
| Pass | bool | 是否通过检查 |
| CutLength | double | 切割长度 |
| FinishedLength | double | 成品长度 |
| ValidBendRadius | bool | 弯曲半径有效 |
| ValidOD | bool | 管径有效 |
| ValidWallThickness | bool | 壁厚有效 |
| ValidMaterial | bool | 材质有效 |
| ModificationsRequired | bool | 需要修改 |

### WeldingMachineResult — 焊接分析结果

| 属性 | 类型 | 说明 |
|------|:----:|------|
| Pass | bool | 是否通过检查 |
| ValidOD | bool | 管径有效 |
| ValidMaterial | bool | 材质有效 |

## PML 中管道操作

```pml
-- 收集管道
VAR !PIPES COLL ALL PIPE FOR $!this.cename

-- 遍历分支
DO !PIPE values !PIPES
    !pipeName = !PIPE.Name
    !pspec = !PIPE.Dbref().:PSPEC

    -- 获取第一个管件
    !ftub = FIRST FTUB OF $!PIPE
    HANDLE (2, 111) (2, 113)
    ELSEHANDLE NONE
        !bore = !ftub.Dbref().:DIA
    ENDHANDLE
ENDDO
```

## ⚠️ 注意事项

- Fabrication API 的真实命名空间可能是 `Aveva.Core.Piping.Fabrication`（需进一步验证）
- `PipePieceManager` 等类需要 E3D 已安装 Fabrication 模块支持
