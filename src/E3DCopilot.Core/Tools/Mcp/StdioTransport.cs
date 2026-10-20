using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Tools.Mcp
{
    /// <summary>
    /// stdio 传输层：启动 MCP server 子进程，通过 stdin/stdout 交换 JSON-RPC。
    /// 对齐 Reasonix internal/plugin stdio transport。
    /// 支持工作目录、环境变量、超时配置。
    /// </summary>
    public class StdioTransport : IMcpTransport
    {
        private readonly Process _proc;
        private readonly StreamWriter _stdin;
        private readonly StreamReader _stdout;
        private readonly ConcurrentDictionary<int, TaskCompletionSource<JObject>> _pending =
            new ConcurrentDictionary<int, TaskCompletionSource<JObject>>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly int _timeoutMs;
        private readonly Task _readLoop;

        /// <summary>进程是否已退出</summary>
        public bool HasExited { get { try { return _proc.HasExited; } catch { return true; } } }

        /// <summary>进程退出码</summary>
        public int ExitCode { get { try { return _proc.ExitCode; } catch { return -1; } } }

        public StdioTransport(string command, string[] args, int timeoutMs = 30000,
            string workingDirectory = null, Dictionary<string, string> env = null)
        {
            if (string.IsNullOrWhiteSpace(command)) throw new ArgumentNullException(nameof(command));
            _timeoutMs = timeoutMs;

            var startInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = string.Join(" ", args ?? new string[0]),
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                // MCP 协议规定 JSON-RPC 走 UTF-8。不显式指定时 .NET 用系统 ANSI 代码页
                // （中文 Windows 为 GBK）解码，多字节字符会被截断成非法 JSON，
                // 进而被 ReadLoop 的 JsonReaderException 静默丢弃，表现为"server 无响应"超时。
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
                // 注意：net48 的 ProcessStartInfo 没有 StandardInputEncoding（仅 .NET Core 有），
                // stdin 编码在下面用 StreamWriter 显式指定。
            };

            // 工作目录（对齐 Reasonix Spec.Dir）
            if (!string.IsNullOrEmpty(workingDirectory) && Directory.Exists(workingDirectory))
                startInfo.WorkingDirectory = workingDirectory;

            // 环境变量注入（对齐 Reasonix Spec.Env）
            if (env != null)
            {
                foreach (var kv in env)
                    startInfo.EnvironmentVariables[kv.Key] = kv.Value;
            }

            _proc = new Process { StartInfo = startInfo };
            _proc.Start();

            // stdin 用 UTF-8 StreamWriter 包装：net48 的 StandardInput 是系统 ANSI（GBK）编码，
            // 直接写入会让含中文的 JSON-RPC 参数在 server 侧变成乱码。
            _stdin = new StreamWriter(_proc.StandardInput.BaseStream, new System.Text.UTF8Encoding(false));
            _stdout = _proc.StandardOutput;
            _readLoop = Task.Run(() => ReadLoop());
        }

        private async Task ReadLoop()
        {
            try
            {
                while (!_cts.IsCancellationRequested && !_stdout.EndOfStream)
                {
                    string line = await _stdout.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    JObject msg;
                    try { msg = JObject.Parse(line); }
                    catch (JsonReaderException) { continue; }

                    var idToken = msg["id"];
                    if (idToken == null || idToken.Type != JTokenType.Integer) continue;
                    int id = idToken.Value<int>();
                    if (_pending.TryRemove(id, out var tcs)) tcs.TrySetResult(msg);
                }
            }
            catch (ObjectDisposedException) { }
            catch (IOException) { }
        }

        public async Task<JObject> SendAsync(JObject request, CancellationToken ct)
        {
            int id = request["id"]?.Value<int>() ?? 0;
            var tcs = new TaskCompletionSource<JObject>();
            _pending[id] = tcs;

            string json = request.ToString(Formatting.None);
            await _stdin.WriteLineAsync(json);
            await _stdin.FlushAsync();

            var timeoutTask = Task.Delay(_timeoutMs, ct);
            var completed = await Task.WhenAny(tcs.Task, timeoutTask);
            if (completed != tcs.Task)
                throw new TimeoutException("MCP stdio 响应超时（server 可能未启动或未输出 JSON-RPC）");
            return await tcs.Task;
        }

        public void Dispose()
        {
            _cts.Cancel();
            try { _stdin?.Dispose(); } catch { }
            try { if (!_proc.HasExited) _proc.Kill(); } catch { }
            try { _proc?.Dispose(); } catch { }
        }
    }
}
