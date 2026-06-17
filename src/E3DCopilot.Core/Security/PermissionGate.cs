using System.Threading.Tasks;

namespace E3DCopilot.Core.Security
{
    /// <summary>
    /// 审批结果
    /// </summary>
    public class ApprovalResult
    {
        public bool Allow { get; set; }
        public bool SessionPersist { get; set; }
    }

    /// <summary>
    /// 待审批请求（连接后台线程和 UI 线程）
    /// </summary>
    public class PendingApproval
    {
        public string Id { get; } = System.Guid.NewGuid().ToString("N");
        public string ToolName { get; set; }
        public string Args { get; set; }
        public string Description { get; set; }

        private readonly TaskCompletionSource<ApprovalResult> _tcs
            = new TaskCompletionSource<ApprovalResult>();

        /// <summary>
        /// UI 线程调用：用户点击审批按钮
        /// </summary>
        public void Complete(bool allow, bool sessionPersist = false)
        {
            _tcs.TrySetResult(new ApprovalResult
            {
                Allow = allow,
                SessionPersist = sessionPersist
            });
        }

        /// <summary>
        /// 后台线程调用：阻塞等待审批结果
        /// </summary>
        public Task<ApprovalResult> WaitAsync()
        {
            return _tcs.Task;
        }
    }

    /// <summary>
    /// 权限门控 — 运行时检查操作是否需要审批
    /// </summary>
    public class PermissionGate
    {
        private readonly ToolPolicy _policy;

        public PermissionGate(ToolPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// 检查工具是否需要审批
        /// </summary>
        public bool NeedsApproval(string toolName)
        {
            return _policy.GetMode(toolName) == ApprovalMode.Ask;
        }
    }
}
