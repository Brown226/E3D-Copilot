using System;
using System.Threading;
using System.Threading.Tasks;

namespace E3DCopilot.Core.Threading
{
    /// <summary>
    /// 线程调度器：所有 E3D API 调用必须通过此类切换到 UI 线程
    /// 在 E3D 主线程初始化时捕获 SynchronizationContext
    /// </summary>
    public class ThreadMarshaller
    {
        private readonly SynchronizationContext _uiContext;

        public ThreadMarshaller()
        {
            // 在 E3D 主线程（UI 线程）上初始化时捕获
            _uiContext = SynchronizationContext.Current
                ?? new SynchronizationContext();
        }

        /// <summary>
        /// 在 UI 线程上执行有返回值的操作
        /// </summary>
        public Task<T> InvokeOnUIThread<T>(Func<T> action)
        {
            var tcs = new TaskCompletionSource<T>();
            _uiContext.Post(_ =>
            {
                try
                {
                    tcs.SetResult(action());
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }, null);
            return tcs.Task;
        }

        /// <summary>
        /// 在 UI 线程上执行无返回值的操作
        /// </summary>
        public Task InvokeOnUIThread(Action action)
        {
            var tcs = new TaskCompletionSource<object>();
            _uiContext.Post(_ =>
            {
                try
                {
                    action();
                    tcs.SetResult(null);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }, null);
            return tcs.Task;
        }

        /// <summary>
        /// 检查当前是否在 UI 线程上
        /// </summary>
        public bool IsOnUIThread =>
            SynchronizationContext.Current == _uiContext;

        // ---- 静态便捷方法（供 RealE3DEnvironment 等使用） ----

        private static readonly Lazy<ThreadMarshaller> _default = new Lazy<ThreadMarshaller>(() => new ThreadMarshaller());

        /// <summary>
        /// 默认实例（首次访问时创建，捕获当前 SynchronizationContext）
        /// </summary>
        public static ThreadMarshaller Default => _default.Value;

        /// <summary>
        /// 在 UI 线程上同步执行有返回值的操作（阻塞直至完成）
        /// </summary>
        public static T Invoke<T>(Func<T> action)
        {
            return Default.InvokeOnUIThread(action).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 在 UI 线程上同步执行无返回值的操作（阻塞直至完成）
        /// </summary>
        public static void Invoke(Action action)
        {
            Default.InvokeOnUIThread(action).GetAwaiter().GetResult();
        }
    }
}
