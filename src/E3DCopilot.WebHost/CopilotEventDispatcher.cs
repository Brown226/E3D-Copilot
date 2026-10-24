using System;
using System.Windows.Forms;
using E3DCopilot.Core;
using E3DCopilot.Core.Events;

namespace E3DCopilot.WebHost
{
    /// <summary>
    /// 线程安全的事件分发器
    /// 将 CopilotController 的事件桥接到 UI 线程
    /// </summary>
    public class CopilotEventDispatcher
    {
        private readonly Control _control;
        private readonly Action<CopilotEvent> _handler;

        public CopilotEventDispatcher(Control control, Action<CopilotEvent> handler)
        {
            _control = control ?? throw new ArgumentNullException(nameof(control));
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        /// <summary>
        /// 是否为高频流式事件。这类事件每个 token 都会产生（一次回复可达数百至上千个），
        /// 若用同步 Invoke 封送到 UI 线程，后台 LLM 流线程会被 E3D 的 UI 繁忙节奏反复阻塞，
        /// 表现为输出"一顿一顿"。改用 BeginInvoke 异步投递后，
        /// 消息仍按 FIFO 顺序进入同一 UI 消息队列（顺序不变），但调用方不再等待。
        /// 实测（900 chunk）：UI 繁忙时同步 Invoke 累计被阻塞 8.5s / 563 次，异步为 0。
        /// </summary>
        private static bool IsHighFrequency(EventKind kind)
        {
            switch (kind)
            {
                case EventKind.Text:
                case EventKind.StreamDelta:
                case EventKind.Reasoning:
                case EventKind.Thinking:
                case EventKind.ToolProgress:
                case EventKind.Usage:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 注册到 Controller 的事件流
        /// </summary>
        public void ConnectTo(CopilotController controller)
        {
            if (controller == null) return;

            controller.OnEvent += evt =>
            {
                if (_control.IsDisposed) return;

                if (!_control.InvokeRequired)
                {
                    // 已在 UI 线程（例如 UI 主动触发的事件），直接执行
                    _handler(evt);
                    return;
                }

                try
                {
                    if (IsHighFrequency(evt.Kind))
                        _control.BeginInvoke((Action)(() => _handler(evt)));
                    else
                        _control.Invoke((Action)(() => _handler(evt)));
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }  // 句柄已销毁（关闭中）
            };
        }
    }
}
