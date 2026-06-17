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
    }
}
