namespace E3DCopilot.Core.Events
{
    /// <summary>
    /// 事件接收器接口，解耦 AgentLoop 和 UI
    /// </summary>
    public interface IEventSink
    {
        void Emit(CopilotEvent evt);
    }
}
