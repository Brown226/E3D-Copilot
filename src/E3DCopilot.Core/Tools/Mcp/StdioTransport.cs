using System;
using System.Collections.Concurrent;
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
    /// 后台读线程将响应按 id 匹配到对应的 TaskCompletionSource；
    /// 写请求带超时保护，避免 server 无响应时永久挂起。
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

        public StdioTransport(string command, string[] args, int timeoutMs = 30000)
        {
            if (string.IsNullOrWhiteSpace(command)) throw new ArgumentNullException(nameof(command));
            _timeoutMs = timeoutMs;

            _proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = string.Join(" ", args ?? new string[0]),
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            _proc.Start();
            _stdin = _proc.StandardInput;
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
