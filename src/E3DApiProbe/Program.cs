using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace E3DApiProbe
{
    class Program
    {
        static int Main(string[] args)
        {
            var log = new StringBuilder();
            log.AppendLine("==================================================");
            log.AppendLine("  E3D API Probe Tool");
            log.AppendLine("  Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            log.AppendLine("==================================================");
            log.AppendLine();

            string probeDir = null;
            if (args.Length > 0 && !string.IsNullOrEmpty(args[0]))
            {
                probeDir = args[0];
                log.AppendLine("Using E3D directory from argument: " + probeDir);
            }
            else
            {
                // Default: D:\AVEVA\Everything3D2.10
                probeDir = @"D:\AVEVA\Everything3D2.10";
                log.AppendLine("Using default E3D directory: " + probeDir);
            }

            // Also try local lib/e3d as fallback
            string localLib = Path.GetFullPath(
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "..", "..", "..", "..", "..", "lib", "e3d"));
            if (Directory.Exists(localLib))
            {
                log.AppendLine("Local lib/e3d directory: " + localLib);
            }
            else
            {
                log.AppendLine("Local lib/e3d directory NOT found at: " + localLib);
            }

            log.AppendLine();

            // Set up AssemblyResolve to load dependencies from E3D directory
            string e3dDir = probeDir;
            AppDomain.CurrentDomain.AssemblyResolve += (sender, resolveArgs) =>
            {
                // Extract the simple assembly name (no version/SN)
                string simpleName = resolveArgs.Name.Split(',')[0];

                // First check if already loaded
                var loaded = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var a in loaded)
                {
                    if (a.GetName().Name == simpleName)
                        return a;
                }

                // Try to find the DLL in the E3D directory
                if (!string.IsNullOrEmpty(e3dDir) && Directory.Exists(e3dDir))
                {
                    string dllPath = Path.Combine(e3dDir, simpleName + ".dll");
                    if (File.Exists(dllPath))
                    {
                        try { return Assembly.LoadFrom(dllPath); }
                        catch { }
                    }
                }

                // Also try local lib/e3d
                if (!string.IsNullOrEmpty(localLib) && Directory.Exists(localLib))
                {
                    string dllPath = Path.Combine(localLib, simpleName + ".dll");
                    if (File.Exists(dllPath))
                    {
                        try { return Assembly.LoadFrom(dllPath); }
                        catch { }
                    }
                }

                return null;
            };

            // Define assemblies to probe
            var assemblyNames = new (string Name, string FileName)[]
            {
                ("Aveva.Core", "Aveva.Core.dll"),
                ("Aveva.Core.Database", "Aveva.Core.Database.dll"),
                ("Aveva.Core.Utilities", "Aveva.Core.Utilities.dll"),
                ("Aveva.ApplicationFramework", "Aveva.ApplicationFramework.dll"),
                ("Aveva.ApplicationFramework.Presentation", "Aveva.ApplicationFramework.Presentation.dll"),
                ("Aveva.Core.Geometry", "Aveva.Core.Geometry.dll"),
                ("Aveva.Core.Graphics", "Aveva.Core.Graphics.dll"),
            };

            var loadedAssemblies = new List<Assembly>();

            // Try loading from probeDir first, then local lib, then GAC
            foreach (var (asmName, asmFile) in assemblyNames)
            {
                Assembly asm = TryLoadAssembly(asmName, asmFile, probeDir, localLib, log);
                if (asm != null)
                {
                    loadedAssemblies.Add(asm);
                }
                log.AppendLine();
            }

            // Also try PMLNet.dll separately (gives access to Aveva.Core.PmlNet)
            Assembly pmlNetAsm = TryLoadAssembly("PMLNet", "PMLNet.dll", probeDir, localLib, log);
            log.AppendLine();

            log.AppendLine("==================================================");
            log.AppendLine("  TYPE PROBE RESULTS");
            log.AppendLine("==================================================");
            log.AppendLine();

            // Define key types to probe with expected members
            var typeProbes = new List<TypeProbe>
            {
                new TypeProbe("Aveva.Core.Database.DbElement", "Aveva.Core.Database")
                {
                    ExpectedStaticMethods = { "GetElement" },
                    ExpectedInstanceMethods = { "GetAsString", "GetAttribute", "SetAttribute", "GetElements", "GetParent", "GetChild", "Move", "Copy", "Delete", "CreateElement", "GetDatabase", "GetTypeName", "GetName" },
                    ExpectedProperties = { "Name", "DbUri", "Parent", "Children", "Type", "Database" }
                },
                new TypeProbe("Aveva.Core.Database.Database", "Aveva.Core.Database")
                {
                    ExpectedStaticMethods = { "OpenDatabase", "GetDatabase", "CurrentDatabase" },
                    ExpectedInstanceMethods = { "GetElement", "CreateElement", "GetNamedElement", "FindObject", "GetRootElement", "Refresh", "Close" },
                    ExpectedProperties = { "Name", "Current", "RootElement", "IsOpen" }
                },
                new TypeProbe("Aveva.Core.Database.DbAttribute", "Aveva.Core.Database")
                {
                    ExpectedStaticMethods = { "GetDbAttribute", "GetDbAttributeFromDbName" },
                    ExpectedInstanceMethods = { "GetValue", "SetValue", "GetValueAsString", "GetName", "GetDescription" },
                    ExpectedProperties = { "Name", "Description", "DataType", "Unit" }
                },
                new TypeProbe("Aveva.Core.Database.Filter", "Aveva.Core.Database")
                {
                    ExpectedStaticMethods = { "CreateFilter", "GetFilter", "Default" },
                    ExpectedInstanceMethods = { "Evaluate", "AddMember", "RemoveMember", "Clear" },
                    ExpectedProperties = { "Name", "Members", "IsEmpty" }
                },
                new TypeProbe("Aveva.Core.Utilities.CommandLine.Command", "Aveva.Core.Utilities")
                {
                    ExpectedStaticMethods = { "CreateCommand" },
                    ExpectedInstanceMethods = { "Run", "RunInPdms", "Cancel", "ToString" },
                    ExpectedProperties = { "Result", "ErrorString", "Status", "IsRunning" }
                },
                new TypeProbe("Aveva.Core.PmlNet.PmlNetAssembly", "PMLNet")
                {
                    ExpectedStaticMethods = { "LoadAssembly", "GetAssembly" },
                    ExpectedInstanceMethods = { "ExecuteMethod", "GetMethod", "CreateInstance", "Invoke" },
                    ExpectedProperties = { "Name", "Methods" }
                },
                new TypeProbe("Aveva.ApplicationFramework.Addin", "Aveva.ApplicationFramework")
                {
                    ExpectedStaticMethods = { },
                    ExpectedInstanceMethods = { "Start", "Stop", "Initialize", "Dispose" },
                    ExpectedProperties = { "Name", "Enabled", "Description" }
                },
                new TypeProbe("Aveva.ApplicationFramework.Presentation.WindowManager", "Aveva.ApplicationFramework.Presentation")
                {
                    ExpectedStaticMethods = { "Instance", "GetInstance" },
                    ExpectedInstanceMethods = { "CreateDockedWindow", "GetDockedWindow", "ShowWindow", "HideWindow", "CloseWindow" },
                    ExpectedProperties = { "ActiveWindow", "Windows", "MainWindow" }
                },
                new TypeProbe("Aveva.ApplicationFramework.Presentation.DockedWindow", "Aveva.ApplicationFramework.Presentation")
                {
                    ExpectedStaticMethods = { },
                    ExpectedInstanceMethods = { "Show", "Hide", "Close", "Activate", "Focus" },
                    ExpectedProperties = { "Title", "Visible", "Size", "Position", "Content" }
                },
                new TypeProbe("Aveva.Core.Geometry.D3Point", "Aveva.Core.Geometry")
                {
                    ExpectedStaticMethods = { "Create", "FromArray" },
                    ExpectedInstanceMethods = { "DistanceTo", "Add", "Subtract", "Multiply", "Normalize", "ToString" },
                    ExpectedProperties = { "X", "Y", "Z", "Length" }
                },
                new TypeProbe("Aveva.Core.Geometry.D3Vector", "Aveva.Core.Geometry")
                {
                    ExpectedStaticMethods = { "Create", "FromArray" },
                    ExpectedInstanceMethods = { "CrossProduct", "DotProduct", "Normalize", "Add", "Subtract", "Multiply", "ToString" },
                    ExpectedProperties = { "X", "Y", "Z", "Length", "IsUnit" }
                },
            };

            // Add types from PMLNet if loaded
            if (pmlNetAsm != null)
            {
                // Already included PmlNetAssembly above
            }

            foreach (var probe in typeProbes)
            {
                ProbeType(probe, loadedAssemblies, pmlNetAsm, log);
                log.AppendLine();
            }

            // Also print all types found in each assembly for discovery
            log.AppendLine("==================================================");
            log.AppendLine("  ALL EXPORTED TYPES (DISCOVERY)");
            log.AppendLine("==================================================");
            log.AppendLine();

            foreach (var asm in loadedAssemblies)
            {
                log.AppendLine($"--- {asm.GetName().Name} ---");
                try
                {
                    var types = asm.GetExportedTypes()
                        .OrderBy(t => t.FullName)
                        .ToArray();
                    foreach (var t in types)
                    {
                        // Skip nested types and compiler-generated
                        if (t.IsNested || t.IsSpecialName) continue;
                        string kind = t.IsInterface ? "interface" :
                                      t.IsEnum ? "enum" :
                                      t.IsValueType ? "struct" : "class";
                        log.AppendLine($"  [{kind}] {t.FullName}");
                    }
                    log.AppendLine($"  Total exported types: {types.Length}");
                }
                catch (Exception ex)
                {
                    log.AppendLine($"  (Error getting exported types: {ex.Message})");
                }
                log.AppendLine();
            }

            // Write to file
            string logPath = @"E:\工作\E3D-E小智\E小智-v1.0-开发中\.git\sdd\e3d-api-probe.log";

            Directory.CreateDirectory(Path.GetDirectoryName(logPath));
            File.WriteAllText(logPath, log.ToString(), Encoding.UTF8);

            // Also write to console
            Console.Write(log.ToString());

            Console.WriteLine();
            Console.WriteLine("Log written to: " + logPath);
            return 0;
        }

        static Assembly TryLoadAssembly(string name, string fileName, string probeDir, string localLib, StringBuilder log)
        {
            log.AppendLine($"--- {name} ({fileName}) ---");

            // Try GAC / already loaded first
            try
            {
                var asm = Assembly.Load(name);
                log.AppendLine($"  Loaded from GAC/refs: {asm.Location}");
                log.AppendLine($"  FullName: {asm.FullName}");
                return asm;
            }
            catch { }

            // Try probeDir
            if (!string.IsNullOrEmpty(probeDir) && Directory.Exists(probeDir))
            {
                string path = Path.Combine(probeDir, fileName);
                if (File.Exists(path))
                {
                    try
                    {
                        var asm = Assembly.LoadFrom(path);
                        log.AppendLine($"  Loaded from: {asm.Location}");
                        log.AppendLine($"  FullName: {asm.FullName}");
                        return asm;
                    }
                    catch (Exception ex)
                    {
                        log.AppendLine($"  Failed to load from {path}: {ex.Message}");
                    }
                }
                else
                {
                    log.AppendLine($"  Not found in probeDir: {path}");
                }
            }
            else
            {
                log.AppendLine($"  ProbeDir not specified or not found.");
            }

            // Try local lib
            if (!string.IsNullOrEmpty(localLib) && Directory.Exists(localLib))
            {
                string path = Path.Combine(localLib, fileName);
                if (File.Exists(path))
                {
                    try
                    {
                        var asm = Assembly.LoadFrom(path);
                        log.AppendLine($"  Loaded from local lib: {asm.Location}");
                        log.AppendLine($"  FullName: {asm.FullName}");
                        return asm;
                    }
                    catch (Exception ex)
                    {
                        log.AppendLine($"  Failed to load from local lib {path}: {ex.Message}");
                    }
                }
                else
                {
                    log.AppendLine($"  Not found in local lib: {path}");
                }
            }
            else
            {
                log.AppendLine($"  Local lib not found: {localLib}");
            }

            log.AppendLine($"  ** {name} NOT LOADED **");
            return null;
        }

        static void ProbeType(TypeProbe probe, List<Assembly> assemblies, Assembly pmlNetAsm, StringBuilder log)
        {
            log.AppendLine($"--- {probe.TypeName} ---");

            // Determine which assembly to search
            Assembly targetAsm = assemblies.FirstOrDefault(a => a.GetName().Name == probe.AssemblyName);

            // Special case: PmlNetAssembly might be in PMLNet.dll
            if (targetAsm == null && probe.AssemblyName == "PMLNet" && pmlNetAsm != null)
                targetAsm = pmlNetAsm;

            // Also search across all loaded assemblies
            Type type = null;
            if (targetAsm != null)
            {
                type = targetAsm.GetType(probe.TypeName, throwOnError: false);
            }

            // Fallback: search all loaded assemblies
            if (type == null)
            {
                foreach (var asm in assemblies)
                {
                    type = asm.GetType(probe.TypeName, throwOnError: false);
                    if (type != null) break;
                }
            }

            // Also try GAC by full name
            if (type == null)
            {
                try { type = Type.GetType(probe.TypeName + ", " + probe.AssemblyName, throwOnError: false); }
                catch { }
            }

            if (type == null)
            {
                log.AppendLine("  ** TYPE NOT FOUND **");
                return;
            }

            log.AppendLine($"  Found: {type.FullName}");
            log.AppendLine($"  Assembly: {type.Assembly.GetName().Name}");
            log.AppendLine($"  IsPublic: {type.IsPublic}");
            log.AppendLine($"  IsAbstract: {type.IsAbstract}");
            log.AppendLine($"  IsSealed: {type.IsSealed}");
            log.AppendLine($"  BaseType: {type.BaseType?.FullName ?? "(none)"}");

            var interfaces = type.GetInterfaces();
            if (interfaces.Length > 0)
            {
                log.AppendLine($"  Interfaces: {string.Join(", ", interfaces.Select(i => i.FullName))}");
            }

            log.AppendLine();

            // --- Static methods ---
            log.AppendLine("  [Static Methods]");
            var staticMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Concat(type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                .Where(m => !m.IsSpecialName)
                .DistinctBy(m => m.Name + ":" + string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name)))
                .OrderBy(m => m.Name)
                .ToList();

            if (probe.ExpectedStaticMethods.Count > 0)
            {
                log.AppendLine("    --- Expected ---");
                foreach (var expected in probe.ExpectedStaticMethods)
                {
                    var matches = staticMethods.Where(m => m.Name == expected).ToList();
                    if (matches.Count > 0)
                    {
                        foreach (var m in matches)
                            log.AppendLine($"    ✓ {FormatMethod(m)}");
                    }
                    else
                    {
                        log.AppendLine($"    ✗ {expected} (NOT FOUND)");
                    }
                }
            }

            if (staticMethods.Count > probe.ExpectedStaticMethods.Count)
            {
                log.AppendLine("    --- Additional ---");
                foreach (var m in staticMethods)
                {
                    if (!probe.ExpectedStaticMethods.Contains(m.Name))
                        log.AppendLine($"    + {FormatMethod(m)}");
                }
            }
            log.AppendLine();

            // --- Instance methods ---
            log.AppendLine("  [Instance Methods]");
            var instanceMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Concat(type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                .Where(m => !m.IsSpecialName)
                .DistinctBy(m => m.Name + ":" + string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name)))
                .OrderBy(m => m.Name)
                .ToList();

            if (probe.ExpectedInstanceMethods.Count > 0)
            {
                log.AppendLine("    --- Expected ---");
                foreach (var expected in probe.ExpectedInstanceMethods)
                {
                    var matches = instanceMethods.Where(m => m.Name == expected).ToList();
                    if (matches.Count > 0)
                    {
                        foreach (var m in matches)
                            log.AppendLine($"    ✓ {FormatMethod(m)}");
                    }
                    else
                    {
                        log.AppendLine($"    ✗ {expected} (NOT FOUND)");
                    }
                }
            }

            if (instanceMethods.Count > probe.ExpectedInstanceMethods.Count)
            {
                log.AppendLine("    --- Additional ---");
                foreach (var m in instanceMethods)
                {
                    if (!probe.ExpectedInstanceMethods.Contains(m.Name))
                        log.AppendLine($"    + {FormatMethod(m)}");
                }
            }
            log.AppendLine();

            // --- Properties ---
            log.AppendLine("  [Properties]");
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Concat(type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                .DistinctBy(p => p.Name)
                .OrderBy(p => p.Name)
                .ToList();

            if (probe.ExpectedProperties.Count > 0)
            {
                log.AppendLine("    --- Expected ---");
                foreach (var expected in probe.ExpectedProperties)
                {
                    var match = props.FirstOrDefault(p => p.Name == expected);
                    if (match != null)
                    {
                        string getter = match.CanRead ? "get" : "";
                        string setter = match.CanWrite ? "set" : "";
                        log.AppendLine($"    ✓ {match.PropertyType.Name} {match.Name} {{{getter}; {setter}}}");
                    }
                    else
                    {
                        log.AppendLine($"    ✗ {expected} (NOT FOUND)");
                    }
                }
            }

            if (props.Count > probe.ExpectedProperties.Count)
            {
                log.AppendLine("    --- Additional ---");
                foreach (var p in props)
                {
                    if (!probe.ExpectedProperties.Contains(p.Name))
                    {
                        string getter = p.CanRead ? "get" : "";
                        string setter = p.CanWrite ? "set" : "";
                        log.AppendLine($"    + {p.PropertyType.Name} {p.Name} {{{getter}; {setter}}}");
                    }
                }
            }
            log.AppendLine();

            // --- All methods summary ---
            log.AppendLine($"  Found {staticMethods.Count} static methods, {instanceMethods.Count} instance methods, {props.Count} properties.");
        }

        static string FormatMethod(MethodInfo m)
        {
            var parms = m.GetParameters();
            var parmStrs = parms.Select(p =>
            {
                string typeName = p.ParameterType.Name;
                if (p.ParameterType.IsGenericType)
                {
                    var args = p.ParameterType.GetGenericArguments();
                    typeName = p.ParameterType.Name.Split('`')[0] + "<" + string.Join(",", args.Select(a => a.Name)) + ">";
                }
                string defaultVal = p.HasDefaultValue ? " = " + (p.DefaultValue ?? "null") : "";
                return typeName + " " + p.Name + defaultVal;
            });

            string retType = m.ReturnType.Name;
            if (m.ReturnType.IsGenericType)
            {
                var args = m.ReturnType.GetGenericArguments();
                retType = m.ReturnType.Name.Split('`')[0] + "<" + string.Join(",", args.Select(a => a.Name)) + ">";
            }

            return $"{retType} {m.Name}({string.Join(", ", parmStrs)})";
        }
    }

    class TypeProbe
    {
        public string TypeName { get; }
        public string AssemblyName { get; }
        public List<string> ExpectedStaticMethods { get; } = new List<string>();
        public List<string> ExpectedInstanceMethods { get; } = new List<string>();
        public List<string> ExpectedProperties { get; } = new List<string>();

        public TypeProbe(string typeName, string assemblyName)
        {
            TypeName = typeName;
            AssemblyName = assemblyName;
        }
    }

    // Simple helper to approximate DistinctBy for .NET 4.8
    static class EnumerableExtensions
    {
        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(
            this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            var seenKeys = new HashSet<TKey>();
            foreach (var element in source)
            {
                if (seenKeys.Add(keySelector(element)))
                {
                    yield return element;
                }
            }
        }
    }
}
