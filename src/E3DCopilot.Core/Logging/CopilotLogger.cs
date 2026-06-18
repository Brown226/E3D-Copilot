using System;
using System.IO;

namespace E3DCopilot.Core.Logging
{
    /// <summary>
    /// 统一日志系统 — 文件输出 + Debug 输出
    /// （不依赖 Messaging 或 PML，避免 E3D 幻觉 API）
    /// </summary>
    public static class CopilotLogger
    {
        private static readonly string LogDir;
        private static readonly object LockObj = new object();

        static CopilotLogger()
        {
            LogDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "E3DCopilot", "logs");
            Directory.CreateDirectory(LogDir);
        }

        /// <summary>
        /// 日志级别
        /// </summary>
        public enum LogLevel
        {
            DEBUG,
            INFO,
            WARN,
            ERROR
        }

        public static LogLevel MinimumLevel { get; set; } = LogLevel.INFO;

        public static void Debug(string format, params object[] args) =>
            Write(LogLevel.DEBUG, format, args);

        public static void Info(string format, params object[] args) =>
            Write(LogLevel.INFO, format, args);

        public static void Warn(string format, params object[] args) =>
            Write(LogLevel.WARN, format, args);

        public static void Error(Exception ex, string format, params object[] args)
        {
            try
            {
                string msg = string.Format(format, args);
                try
                {
                    msg += " | " + ex.ToString();
                }
                catch
                {
                    msg += " | [无法序列化异常]";
                }
                Write(LogLevel.ERROR, "{0}", msg);
            }
            catch
            {
                // 日志写入失败时静默处理，避免级联异常
            }
        }

        private static void Write(LogLevel level, string format, params object[] args)
        {
            if (level < MinimumLevel) return;

            string line = string.Format("{0:yyyy-MM-dd HH:mm:ss.fff} [{1}] {2}",
                DateTime.Now, level, string.Format(format, args));

            lock (LockObj)
            {
                string file = Path.Combine(LogDir,
                    "e3dcopilot." + DateTime.Now.ToString("yyyy-MM-dd") + ".log");
                File.AppendAllText(file, line + Environment.NewLine);
            }

            System.Diagnostics.Debug.WriteLine(line);
        }
    }
}
