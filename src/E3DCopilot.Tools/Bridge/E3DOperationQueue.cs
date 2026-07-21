using System;
using System.Threading;
using System.Threading.Tasks;

namespace E3DCopilot.Tools.Bridge
{
    /// <summary>
    /// E3D 操作队列 — 确保写操作跨会话串行化
    /// 
    /// 问题场景：
    ///   Tab A: 读取 PIPE-001.WTHK = SCH30 → 准备修改为 SCH40
    ///   Tab B: 读取 PIPE-001.WTHK = SCH30 → 准备修改为 SCH80
    ///   Tab A: 写入 SCH40 ✓
    ///   Tab B: 写入 SCH80 ✓ (覆盖了 A 的修改，但 B 不知道 A 已经改过)
    /// 
    /// 解决方案：
    ///   - 读操作：可以并发（通过 UI 线程串行化）
    ///   - 写操作：全局互斥锁，确保原子性
    ///   
    /// 使用方式：
    ///   await E3DOperationQueue.ExecuteWriteAsync(() => {
    ///       // 读-改-写 操作序列
    ///       var old = env.GetAttribute("PIPE-001", "WTHK");
    ///       env.SetAttribute("PIPE-001", "WTHK", "SCH40");
    ///       return $"Changed from {old} to SCH40";
    ///   });
    /// </summary>
    public static class E3DOperationQueue
    {
        // 全局写操作锁 — 确保同一时刻只有一个写操作在执行
        private static readonly SemaphoreSlim WriteLock = new SemaphoreSlim(1, 1);

        // 操作超时（防止死锁）
        private const int DefaultTimeoutMs = 30000; // 30 秒

        /// <summary>
        /// 执行写操作（互斥）
        /// 所有修改 E3D 数据的操作都应通过此方法执行
        /// </summary>
        /// <typeparam name="T">返回类型</typeparam>
        /// <param name="operation">写操作委托</param>
        /// <param name="operationName">操作名称（用于日志/调试）</param>
        /// <param name="timeoutMs">超时毫秒数</param>
        /// <returns>操作结果</returns>
        public static async Task<T> ExecuteWriteAsync<T>(
            Func<Task<T>> operation,
            string operationName = null,
            int timeoutMs = DefaultTimeoutMs)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            bool acquired = false;
            try
            {
                // 尝试获取写锁（带超时）
                acquired = await WriteLock.WaitAsync(timeoutMs).ConfigureAwait(false);
                if (!acquired)
                {
                    throw new TimeoutException(
                        $"E3D 写操作超时 ({timeoutMs / 1000}s): {operationName ?? "unknown"}. " +
                        "可能有其他会话正在执行写操作，请稍后重试。");
                }

                // 执行写操作
                return await operation().ConfigureAwait(false);
            }
            finally
            {
                if (acquired)
                {
                    WriteLock.Release();
                }
            }
        }

        /// <summary>
        /// 执行写操作（互斥，无返回值）
        /// </summary>
        public static async Task ExecuteWriteAsync(
            Func<Task> operation,
            string operationName = null,
            int timeoutMs = DefaultTimeoutMs)
        {
            await ExecuteWriteAsync(async () =>
            {
                await operation().ConfigureAwait(false);
                return true;
            }, operationName, timeoutMs).ConfigureAwait(false);
        }

        /// <summary>
        /// 执行写操作（同步版本，互斥）
        /// </summary>
        public static T ExecuteWrite<T>(
            Func<T> operation,
            string operationName = null,
            int timeoutMs = DefaultTimeoutMs)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            bool acquired = false;
            try
            {
                acquired = WriteLock.Wait(timeoutMs);
                if (!acquired)
                {
                    throw new TimeoutException(
                        $"E3D 写操作超时 ({timeoutMs / 1000}s): {operationName ?? "unknown"}. " +
                        "可能有其他会话正在执行写操作，请稍后重试。");
                }

                return operation();
            }
            finally
            {
                if (acquired)
                {
                    WriteLock.Release();
                }
            }
        }

        /// <summary>
        /// 执行写操作（同步版本，无返回值）
        /// </summary>
        public static void ExecuteWrite(
            Action operation,
            string operationName = null,
            int timeoutMs = DefaultTimeoutMs)
        {
            ExecuteWrite<object>(() =>
            {
                operation();
                return null;
            }, operationName, timeoutMs);
        }

        /// <summary>
        /// 尝试执行写操作（不抛超时异常，返回是否成功）
        /// </summary>
        public static bool TryExecuteWrite(
            Action operation,
            out Exception error,
            string operationName = null,
            int timeoutMs = 5000)
        {
            error = null;
            bool acquired = false;
            try
            {
                acquired = WriteLock.Wait(timeoutMs);
                if (!acquired)
                {
                    error = new TimeoutException($"无法获取写锁: {operationName}");
                    return false;
                }

                operation();
                return true;
            }
            catch (Exception ex)
            {
                error = ex;
                return false;
            }
            finally
            {
                if (acquired)
                {
                    WriteLock.Release();
                }
            }
        }

        /// <summary>
        /// 检查当前是否有写操作正在执行
        /// </summary>
        public static bool IsWriteInProgress => WriteLock.CurrentCount == 0;

        /// <summary>
        /// 等待所有写操作完成（用于关闭/清理场景）
        /// </summary>
        public static async Task WaitForPendingWritesAsync(int timeoutMs = 60000)
        {
            bool acquired = await WriteLock.WaitAsync(timeoutMs).ConfigureAwait(false);
            if (acquired)
            {
                WriteLock.Release();
            }
        }
    }
}
