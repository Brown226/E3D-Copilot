using System.Drawing;

namespace E3DCopilot.UI.Themes
{
    /// <summary>
    /// E小智 暗色主题配色方案
    /// </summary>
    public static class CopilotTheme
    {
        // ---- 基础色 ----
        public static Color BgDark => Color.FromArgb(25, 25, 28);
        public static Color BgMid => Color.FromArgb(35, 35, 40);
        public static Color BgLight => Color.FromArgb(45, 45, 52);
        public static Color BgHighlight => Color.FromArgb(55, 55, 62);

        // ---- 文字 ----
        public static Color TextPrimary => Color.FromArgb(230, 230, 235);
        public static Color TextSecondary => Color.FromArgb(160, 160, 170);
        public static Color TextMuted => Color.FromArgb(110, 110, 120);

        // ---- 品牌色 ----
        public static Color AccentBlue => Color.FromArgb(0, 120, 215);
        public static Color AccentBlueHover => Color.FromArgb(0, 100, 190);
        public static Color AccentGreen => Color.FromArgb(80, 180, 80);
        public static Color AccentOrange => Color.FromArgb(220, 140, 40);
        public static Color AccentRed => Color.FromArgb(200, 60, 60);
        public static Color AccentPurple => Color.FromArgb(140, 90, 200);

        // ---- 消息气泡 ----
        public static Color BubbleUser => Color.FromArgb(0, 90, 170);
        public static Color BubbleAssistant => Color.FromArgb(42, 42, 48);
        public static Color BubbleSystem => Color.FromArgb(30, 35, 40);
        public static Color BubbleError => Color.FromArgb(55, 25, 25);
        public static Color BubbleReasoning => Color.FromArgb(35, 35, 42);

        // ---- 边框 ----
        public static Color Border => Color.FromArgb(60, 60, 68);
        public static Color BorderLight => Color.FromArgb(50, 50, 58);

        // ---- 字体 ----
        public static Font FontNormal => new Font("Microsoft YaHei UI", 9.5f);
        public static Font FontSmall => new Font("Microsoft YaHei UI", 8.5f);
        public static Font FontTitle => new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
        public static Font FontCode => new Font("Consolas", 9.5f);
        public static Font FontInput => new Font("Microsoft YaHei UI", 10f);
    }
}
