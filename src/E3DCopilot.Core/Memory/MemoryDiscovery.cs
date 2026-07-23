using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace E3DCopilot.Core.Memory
{
    /// <summary>
    /// 层级记忆发现 — 对齐 Reasonix internal/memory.Load(opts)
    ///
    /// 三级发现（优先级从高到低）：
    ///   1. 项目级: workspace/.e3dcopilot/AGENTS.md
    ///   2. 用户级: %LocalAppData%/E3DCopilot/AGENTS.md
    ///   3. 全局默认: 内置 fallback
    ///
    /// 启动时一次性加载所有层级，折叠到 SystemPrompt。
    /// 高优先级覆盖低优先级（同名 section 以项目级为准）。
    /// </summary>
    public class MemoryDiscovery
    {
        /// <summary>项目工作目录</summary>
        public string CWD { get; private set; }

        /// <summary>用户配置根目录</summary>
        public string UserDir { get; private set; }

        /// <summary>发现的所有记忆文档（按优先级升序：全局 → 用户 → 项目）</summary>
        public List<MemorySource> Docs { get; private set; }

        /// <summary>自动记忆索引（MEMORY.md 内容）</summary>
        public string Index { get; private set; }

        /// <summary>
        /// 加载所有层级的记忆文档。
        /// Best-effort：文件不存在只是少一层记忆，不报错。
        /// </summary>
        public static MemoryDiscovery Load(string cwd = null, string userDir = null)
        {
            var discovery = new MemoryDiscovery
            {
                CWD = cwd ?? ".",
                UserDir = userDir ?? DefaultUserDir(),
                Docs = new List<MemorySource>()
            };

            discovery.DiscoverDocs();
            discovery.LoadIndex();
            return discovery;
        }

        /// <summary>
        /// 获取指定 scope 的记忆文件写入路径。
        /// 优先使用已存在的文件（AGENTS.md），不存在时创建默认。
        /// </summary>
        public string DocPath(MemoryScope scope)
        {
            switch (scope)
            {
                case MemoryScope.Project:
                    return Path.Combine(CWD, ".e3dcopilot", "AGENTS.md");
                case MemoryScope.User:
                    if (string.IsNullOrEmpty(UserDir)) return null;
                    return Path.Combine(UserDir, "AGENTS.md");
                case MemoryScope.Local:
                    return Path.Combine(CWD, ".e3dcopilot", "AGENTS.local.md");
                default:
                    return Path.Combine(CWD, ".e3dcopilot", "AGENTS.md");
            }
        }

        /// <summary>
        /// 将所有层级的记忆文档合并为注入 SystemPrompt 的文本。
        /// 按优先级升序拼接（项目级最后，权重最高）。
        /// </summary>
        public string Compose()
        {
            if (Docs.Count == 0 && string.IsNullOrEmpty(Index))
                return null;

            var sb = new StringBuilder();

            foreach (var doc in Docs)
            {
                if (string.IsNullOrWhiteSpace(doc.Content)) continue;
                sb.AppendLine($"<memory scope=\"{doc.Scope}\" source=\"{doc.SourcePath}\">");
                sb.AppendLine(doc.Content.TrimEnd());
                sb.AppendLine($"</memory>");
                sb.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(Index))
            {
                sb.AppendLine("<auto-memory-index>");
                sb.AppendLine(Index.TrimEnd());
                sb.AppendLine("</auto-memory-index>");
            }

            string result = sb.ToString().TrimEnd();
            return string.IsNullOrEmpty(result) ? null : result;
        }

        // ═══════════════════════════════════════════════════════════
        //  内部实现
        // ═══════════════════════════════════════════════════════════

        private void DiscoverDocs()
        {
            // 按优先级升序发现（全局 → 用户 → 项目），后发现的覆盖先发现的

            // 1. 全局默认（用户级 AGENTS.md 作为全局 fallback）
            if (!string.IsNullOrEmpty(UserDir))
            {
                TryAddDoc(Path.Combine(UserDir, "AGENTS.md"), MemoryScope.User);
                // 兼容 CLAUDE.md / REASONIX.md 约定
                TryAddDoc(Path.Combine(UserDir, "CLAUDE.md"), MemoryScope.User);
            }

            // 2. 项目级
            if (!string.IsNullOrEmpty(CWD) && CWD != ".")
            {
                var projectDir = Path.Combine(CWD, ".e3dcopilot");
                TryAddDoc(Path.Combine(projectDir, "AGENTS.md"), MemoryScope.Project);
                TryAddDoc(Path.Combine(projectDir, "AGENTS.local.md"), MemoryScope.Local);
                // 兼容：项目根目录的 AGENTS.md
                TryAddDoc(Path.Combine(CWD, "AGENTS.md"), MemoryScope.Project);
                TryAddDoc(Path.Combine(CWD, "CLAUDE.md"), MemoryScope.Project);
            }
        }

        private void TryAddDoc(string path, MemoryScope scope)
        {
            try
            {
                if (!File.Exists(path)) return;
                string content = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(content)) return;

                // 去重：同一文件不重复添加
                foreach (var existing in Docs)
                {
                    if (string.Equals(existing.SourcePath, path, StringComparison.OrdinalIgnoreCase))
                        return;
                }

                Docs.Add(new MemorySource
                {
                    Scope = scope.ToString().ToLowerInvariant(),
                    SourcePath = path,
                    Content = content
                });
            }
            catch
            {
                // Best-effort：读取失败跳过
            }
        }

        private void LoadIndex()
        {
            // 自动记忆索引：MEMORY.md（对齐 Reasonix auto-memory index）
            try
            {
                string indexPath = null;

                // 优先项目级
                if (!string.IsNullOrEmpty(CWD) && CWD != ".")
                {
                    var projectIndex = Path.Combine(CWD, ".e3dcopilot", "MEMORY.md");
                    if (File.Exists(projectIndex))
                        indexPath = projectIndex;
                }

                // 回退用户级
                if (indexPath == null && !string.IsNullOrEmpty(UserDir))
                {
                    var userIndex = Path.Combine(UserDir, "MEMORY.md");
                    if (File.Exists(userIndex))
                        indexPath = userIndex;
                }

                if (indexPath != null)
                    Index = File.ReadAllText(indexPath, Encoding.UTF8);
            }
            catch
            {
                Index = null;
            }
        }

        private static string DefaultUserDir()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "E3DCopilot");
        }
    }

    /// <summary>
    /// 记忆文档来源
    /// </summary>
    public class MemorySource
    {
        /// <summary>层级：project / user / local</summary>
        public string Scope { get; set; }

        /// <summary>文件路径</summary>
        public string SourcePath { get; set; }

        /// <summary>文件内容</summary>
        public string Content { get; set; }
    }

    /// <summary>
    /// 记忆写入范围
    /// </summary>
    public enum MemoryScope
    {
        /// <summary>项目级（.e3dcopilot/AGENTS.md）</summary>
        Project,
        /// <summary>用户级（%LocalAppData%/E3DCopilot/AGENTS.md）</summary>
        User,
        /// <summary>本地级（.e3dcopilot/AGENTS.local.md，不提交 git）</summary>
        Local
    }
}
