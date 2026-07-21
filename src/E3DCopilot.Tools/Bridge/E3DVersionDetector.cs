using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace E3DCopilot.Tools.Bridge
{
    /// <summary>
    /// E3D 版本检测工具 — 运行时检测 E3D 版本，确保插件兼容性
    /// 
    /// 问题：
    /// - E小智 v1.0 针对 E3D 1.1.x 开发
    /// - 如果在 E3D 2.x 上加载，API 签名可能变化导致崩溃
    /// - 需要运行时检测并给出友好提示
    /// </summary>
    public static class E3DVersionDetector
    {
        /// <summary>
        /// E3D 版本信息
        /// </summary>
        public class E3DVersionInfo
        {
            /// <summary>主版本号</summary>
            public int Major { get; set; }

            /// <summary>次版本号</summary>
            public int Minor { get; set; }

            /// <summary>修订号</summary>
            public int Patch { get; set; }

            /// <summary>完整版本字符串</summary>
            public string FullVersion { get; set; }

            /// <summary>产品名称</summary>
            public string ProductName { get; set; }

            /// <summary>安装路径</summary>
            public string InstallPath { get; set; }

            /// <summary>是否与当前插件兼容</summary>
            public bool IsCompatible { get; set; }

            /// <summary>兼容性说明</summary>
            public string CompatibilityNote { get; set; }

            public override string ToString()
            {
                return $"{ProductName} {FullVersion} ({(IsCompatible ? "兼容" : "不兼容")})";
            }
        }

        // 当前插件支持的 E3D 版本范围
        private const int SupportedMajorMin = 1;
        private const int SupportedMajorMax = 1;
        private const int SupportedMinorMin = 1;

        /// <summary>
        /// 检测当前运行的 E3D 版本
        /// </summary>
        /// <returns>版本信息，检测失败返回 null</returns>
        public static E3DVersionInfo DetectVersion()
        {
            var info = new E3DVersionInfo();

            try
            {
                // 方法 1：从 Aveva.ApplicationFramework 程序集获取版本
                var frameworkAssembly = GetAvevaAssembly("Aveva.ApplicationFramework");
                if (frameworkAssembly != null)
                {
                    var version = frameworkAssembly.GetName().Version;
                    info.Major = version.Major;
                    info.Minor = version.Minor;
                    info.Patch = version.Build;
                    info.FullVersion = version.ToString();
                    info.ProductName = "AVEVA E3D";
                }

                // 方法 2：从进程获取安装路径
                var process = Process.GetCurrentProcess();
                if (process != null && process.MainModule != null)
                {
                    info.InstallPath = Path.GetDirectoryName(process.MainModule.FileName);
                }

                // 方法 3：尝试从环境变量获取
                if (string.IsNullOrEmpty(info.InstallPath))
                {
                    info.InstallPath = Environment.GetEnvironmentVariable("E3D_INSTALL_DIR")
                        ?? Environment.GetEnvironmentVariable("PDMS_INSTALL_DIR");
                }

                // 判断兼容性
                CheckCompatibility(info);

                return info;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[E3DVersionDetector] 版本检测失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 检查版本兼容性
        /// </summary>
        private static void CheckCompatibility(E3DVersionInfo info)
        {
            if (info.Major < SupportedMajorMin)
            {
                info.IsCompatible = false;
                info.CompatibilityNote = $"E3D 版本过低 ({info.FullVersion})。" +
                    $"E小智 v1.0 需要 E3D {SupportedMajorMin}.{SupportedMinorMin} 或更高版本。";
            }
            else if (info.Major > SupportedMajorMax)
            {
                info.IsCompatible = false;
                info.CompatibilityNote = $"E3D 版本过高 ({info.FullVersion})。" +
                    $"E小智 v1.0 针对 E3D {SupportedMajorMax}.x 开发，" +
                    $"E3D {info.Major}.x 可能存在 API 兼容性问题。请升级 E小智 插件。";
            }
            else if (info.Major == SupportedMajorMin && info.Minor < SupportedMinorMin)
            {
                info.IsCompatible = false;
                info.CompatibilityNote = $"E3D 版本过低 ({info.FullVersion})。" +
                    $"需要 E3D {SupportedMajorMin}.{SupportedMinorMin} 或更高版本。";
            }
            else
            {
                info.IsCompatible = true;
                info.CompatibilityNote = "版本兼容";
            }
        }

        /// <summary>
        /// 获取 Aveva 程序集
        /// </summary>
        private static Assembly GetAvevaAssembly(string name)
        {
            // 先从已加载的程序集中查找
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                {
                    return asm;
                }
            }

            // 尝试按名称加载
            try
            {
                return Assembly.Load(name);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 验证版本兼容性，不兼容时抛出异常
        /// 应在 Addin 启动时调用
        /// </summary>
        /// <exception cref="InvalidOperationException">版本不兼容时抛出</exception>
        public static void ValidateCompatibility()
        {
            var info = DetectVersion();

            if (info == null)
            {
                // 无法检测版本时记录警告但不阻止启动
                Debug.WriteLine("[E3DVersionDetector] 警告: 无法检测 E3D 版本，继续启动");
                return;
            }

            if (!info.IsCompatible)
            {
                throw new InvalidOperationException(
                    $"E小智 无法在当前 E3D 版本上运行。\n\n" +
                    $"检测到: {info}\n" +
                    $"{info.CompatibilityNote}\n\n" +
                    $"请联系管理员获取兼容版本的 E小智 插件。");
            }

            Debug.WriteLine($"[E3DVersionDetector] E3D 版本检测通过: {info}");
        }

        /// <summary>
        /// 获取版本摘要字符串（用于日志/关于对话框）
        /// </summary>
        public static string GetVersionSummary()
        {
            var info = DetectVersion();
            if (info == null)
            {
                return "E3D 版本: 未知";
            }

            return $"E3D 版本: {info.FullVersion} ({(info.IsCompatible ? "兼容" : "不兼容")})\n" +
                   $"安装路径: {info.InstallPath ?? "未知"}";
        }
    }
}
