using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Tools.Mcp
{
    /// <summary>
    /// Streamable HTTP 传输层：POST JSON-RPC。
    /// 对齐 Reasonix internal/plugin http/streamable-http transport。
    /// 支持自定义 Headers、可配置超时。
    /// </summary>
    public class HttpTransport : IMcpTransport
    {
        private readonly HttpClient _http;
        private readonly string _endpoint;
        private readonly Dictionary<string, string> _headers;

        public HttpTransport(string endpoint, int timeoutMs = 30000,
            Dictionary<string, string> headers = null)
        {
            if (string.IsNullOrWhiteSpace(endpoint)) throw new ArgumentNullException(nameof(endpoint));
            _endpoint = endpoint;
            _headers = headers;
            _http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeoutMs) };
        }

        public async Task<JObject> SendAsync(JObject request, CancellationToken ct)
        {
            var json = request.ToString(Newtonsoft.Json.Formatting.None);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // 注入自定义 Headers（对齐 Reasonix Spec.Headers）
            if (_headers != null)
            {
                foreach (var kv in _headers)
                    content.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            }

            using (var resp = await _http.PostAsync(_endpoint, content, ct).ConfigureAwait(false))
            {
                var body = await resp.Content.ReadAsStringAsync();
                return JObject.Parse(body);
            }
        }

        public void Dispose() => _http.Dispose();
    }
}
