using System;
using System.Collections.Generic;
using System.Text;
using E3DCopilot.Core.Services.Cad;
using E3DCopilot.Core.Models.Building;
using E3DCopilot.Core.Models.Geometry;

class Program
{
    static void Main()
    {
        var gen = new PmlScriptGenerator();

        var elems = new List<BuildingElement>
        {
            new BuildingElement
            {
                Type = BuildingElementType.Wall,
                Points = new List<Point3D>
                {
                    new Point3D(0, 0, 0),
                    new Point3D(5000, 0, 0)
                },
                Properties = new Dictionary<string, object>
                {
                    ["Height"] = 3000.0,
                    ["Thickness"] = 200.0
                }
            },
            new BuildingElement
            {
                Type = BuildingElementType.Column,
                Points = new List<Point3D> { new Point3D(5000, 0, 0) },
                Properties = new Dictionary<string, object>
                {
                    ["Width"] = 400.0,
                    ["Depth"] = 400.0,
                    ["Height"] = 3000.0
                }
            }
        };

        string script = gen.GenerateBuildingScript(elems, owner: "/Copy-of-CIVIL");

        Console.WriteLine("=== 生成的 PML 脚本（带行号）===");
        var lines = script.Split('\n');
        for (int i = 0; i < lines.Length; i++)
            Console.WriteLine("{0,3}| {1}", i + 1, lines[i].TrimEnd('\r'));

        Console.WriteLine();
        Console.WriteLine("=== ASCII 检查 ===");
        int nonAscii = 0;
        var badLines = new List<int>();
        for (int i = 0; i < lines.Length; i++)
        {
            foreach (char c in lines[i])
            {
                if (c > 127)
                {
                    nonAscii++;
                    if (!badLines.Contains(i + 1)) badLines.Add(i + 1);
                    break;
                }
            }
        }
        if (nonAscii == 0)
            Console.WriteLine("PASS: 全脚本纯 ASCII，不存在 UTF-8 -> ANSI 乱码风险");
        else
        {
            Console.WriteLine("FAIL: 发现非 ASCII 字符，行号 = " + string.Join(",", badLines));
        }

        Console.WriteLine();
        Console.WriteLine("=== 语法检查 ===");
        Console.WriteLine("$!ECHO 残留: " + (script.Contains("$!ECHO") ? "FAIL 仍存在" : "PASS 已清除"));
        int pCount = 0;
        foreach (var l in lines) if (l.TrimStart().StartsWith("$P ")) pCount++;
        Console.WriteLine("$P 输出语句数量: " + pCount);

        Console.WriteLine();
        Console.WriteLine("=== 首行 ===");
        Console.WriteLine(lines[0]);
    }
}
