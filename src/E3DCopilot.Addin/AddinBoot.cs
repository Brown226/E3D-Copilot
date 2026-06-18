using System;
using Aveva.ApplicationFramework;
using Aveva.ApplicationFramework.Presentation;
using Aveva.Core.Utilities.CommandLine;

namespace E3DCopilot.Addin
{
    /// <summary>
    /// E小智 Addin — 直接实现 IAddin 接口（而不是继承 Addin 抽象类）
    /// E3D 2.1 的 IAddin 只有 Name/Description + Start()/Stop()
    /// </summary>
    public class CopilotAddin : IAddin
    {
        private DockedWindow _dockedWindow;

        public string Name => "E3DCopilot";
        public string Description => "E小智 AI Copilot for E3D";

        public void Start(ServiceManager serviceManager)
        {
            try
            {
                var panel = new System.Windows.Forms.Panel();
                var label = new System.Windows.Forms.Label();
                label.Text = "E小智 OK!";
                label.Dock = System.Windows.Forms.DockStyle.Fill;
                label.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
                label.Font = new System.Drawing.Font("Microsoft YaHei", 20, System.Drawing.FontStyle.Bold);
                label.ForeColor = System.Drawing.Color.DodgerBlue;
                panel.Controls.Add(label);

                _dockedWindow = WindowManager.Instance.CreateDockedWindow(
                    "E3DCopilot",
                    "小智",
                    panel,
                    DockedPosition.Right
                );
                _dockedWindow.Width = 400;
                _dockedWindow.Show();

                var cmd = Aveva.Core.Utilities.CommandLine.Command.CreateCommand("$p E小智 Copilot v1.0 已启动");
                cmd.RunInPdms();
            }
            catch (Exception ex)
            {
                try
                {
                    var cmd = Aveva.Core.Utilities.CommandLine.Command.CreateCommand("$p E小智 失败: " + ex.Message);
                    cmd.RunInPdms();
                }
                catch { }
            }
        }

        public void Stop()
        {
            if (_dockedWindow != null)
            {
                _dockedWindow.Close();
                _dockedWindow = null;
            }
        }
    }
}
