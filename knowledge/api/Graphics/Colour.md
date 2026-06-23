# Colour — 颜色类

**命名空间**: `Aveva.Core.Graphics`（⚠️ 非 `Aveva.Pdms.Graphics`）
**程序集**: Aveva.Core.Graphics.dll
**用途**: 表示 RGB 颜色值，用于图形渲染和元素显示

## 关键属性

| 属性 | 签名 | 说明 |
|------|------|------|
| Red | `int Red { get; set; }` | 红色分量 (0-255) |
| Green | `int Green { get; set; }` | 绿色分量 (0-255) |
| Blue | `int Blue { get; set; }` | 蓝色分量 (0-255) |
| Index | `int Index { get; }` | 颜色索引（预定义颜色表） |
| Name | `string Name { get; }` | 颜色名称 |

## 预定义颜色

| 静态属性 | 颜色 |
|----------|------|
| `Colour.Red` | 红色 |
| `Colour.Green` | 绿色 |
| `Colour.Blue` | 蓝色 |
| `Colour.Yellow` | 黄色 |
| `Colour.White` | 白色 |
| `Colour.Black` | 黑色 |

## Colours 颜色表类

提供预定义颜色集合。

| 成员 | 说明 |
|------|------|
| `Colours.ColourTable` | 获取预定义颜色表 |
| `.Red` / `.Green` / `.Blue` | 标准颜色 |
| `.AllColours` | 遍历所有预定义颜色 |

## 典型用法

```csharp
using Aveva.Core.Graphics;
using Aveva.Core.Database;

// 创建自定义颜色（橙色）
Colour myColor = new Colour();
myColor.Red = 255;
myColor.Green = 128;
myColor.Blue = 0;

// 使用预定义颜色
Colour red = Colour.Red;

// 通过颜色表访问
Colours colourTable = Colours.ColourTable;
Colour standardRed = colourTable.Red;

// 获取颜色索引并设置到元素
int colorIdx = myColor.Index;
element.SetAttribute(DbAttributeInstance.ColourIndex, colorIdx);
```

## ⚠️ 注意事项

- 命名空间是 `Aveva.Core.Graphics`（文档写 `Aveva.Pdms.Graphics`）
- 设置元素颜色需通过 `DbAttributeInstance.ColourIndex` 属性
