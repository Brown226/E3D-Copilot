using System;
using System.IO;
using E3DCopilot.Core.Logging;

namespace E3DCopilot.Core.Recovery
{
    /// <summary>
    /// 崩溃检测器 — 对齐 Reasonix desktop/crash_app.go
    ///
    /// 机制：
    ///   - 启动时写 lock 文件（含 PID + 时间戳）
    ///   - 正常退出时删除 lock 文件
    ///   - 下次启动发现残留 lock = 上次异常退出
    ///   - 连续崩溃计数：记录在 crash_count 文件中
    /// </summary>
    public class CrashDetector
    {
        private readonly string _lockPath;
        private readonly string _crashCountPath;
        private bool _isActive;

        public CrashDetector(string baseDir = null)
        {
            baseDir = baseDir ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "E3DCopilot");

            if (!Directory.Exists(baseDir))
                Directory.CreateDirectory(baseDir);

            _lockPath = Path.Combine(baseDir, "session.lock");
            _crashCountPath = Path.Combine(baseDir, "crash_count");
        }

        /// <summary>上次是否异常退出（lock 文件残留）</summary>
        public bool LastSessionCrashed { get; private set; }

        /// <summary>连续崩溃次数</summary>
        public int ConsecutiveCrashes { get; private set; }

        /// <summary>上次崩溃时间（从 lock 文件读取）</summary>
        public DateTime? LastCrashTime { get; private set; }

        /// <summary>
        /// 启动时调用：检测上次是否崩溃，然后写入新 lock。
        /// 返回 true 表示检测到上次异常退出。
        /// </summary>
        public bool DetectAndArm()
        {
            LastSessionCrashed = false;
            ConsecutiveCrashes = ReadCrashCount();

            if (File.Exists(_lockPath))
            {
                // 残留 lock = 上次异常退出
                LastSessionCrashed = true;
                try
                {
                    var info = new FileInfo(_lockPath);
                    LastCrashTime = info.LastWriteTimeUtc;
                }
                catch { }

                ConsecutiveCrashes++;
                WriteCrashCount(ConsecutiveCrashes);

                CopilotLogger.Info("CrashDetector: 检测到上次异常退出 (连续第 {0} 次)", ConsecutiveCrashes);
            }

            // 写入新 lock
            try
            {
                string content = $"pid={System.Diagnostics.Process.GetCurrentProcess().Id}\nstarted={DateTime.UtcNow:o}";
                File.WriteAllText(_lockPath, content);
                _isActive = true;
            }
            catch (Exception ex)
            {
                CopilotLogger.Error(ex, "CrashDetector: 写入 lock 文件失败");
            }

            return LastSessionCrashed;
        }

        /// <summary>
        /// 正常退出时调用：删除 lock 文件，重置崩溃计数。
        /// </summary>
        public void Disarm()
        {
            if (!_isActive) return;

            try
            {
                if (File.Exists(_lockPath))
                    File.Delete(_lockPath);

                // 正常退出，重置连续崩溃计数
                WriteCrashCount(0);
                ConsecutiveCrashes = 0;
                _isActive = false;
            }
            catch (Exception ex)
            {
                CopilotLogger.Error(ex, "CrashDetector: 删除 lock 文件失败");
            }
        }

        /// <summary>
        /// 是否应进入安全模式（连续崩溃 >= 2 次）
        /// </summary>
        public bool ShouldEnterSafeMode()
        {
            return ConsecutiveCrashes >= 2;
        }

        private int ReadCrashCount()
        {
            try
            {
                if (File.Exists(_crashCountPath))
                {
                    string text = File.ReadAllText(_crashCountPath).Trim();
                    int count;
                    if (int.TryParse(text, out count))
                        return count;
                }
            }
            catch { }
            return 0;
        }

        private void WriteCrashCount(int count)
        {
            try
            {
                File.WriteAllText(_crashCountPath, count.ToString());
            }
            catch { }
        }
    }
}
