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
        /// 注册到 Controller 的事件流
        /// </summary>
        public void ConnectTo(CopilotController controller)
        {
            if (controller == null) return;

            controller.OnEvent += evt =>
            {
                if (_control.IsDisposed) return;

                if (_control.InvokeRequired)
                {
                    try { _control.Invoke((Action)(() => _handler(evt))); }
                    catch (ObjectDisposedException) { }
                }
                else
                {
                    _handler(evt);
                }
            };
        }
    }
}
