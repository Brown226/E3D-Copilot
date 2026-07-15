using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Tools.Mcp
{
    /// <summary>Streamable HTTP 传输层：POST JSON-RPC，读取 result（只读场景足矣）</summary>
    public class HttpTransport : IMcpTransport
    {
        private readonly HttpClient _http;
        private readonly string _endpoint;

        public HttpTransport(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint)) throw new ArgumentNullException(nameof(endpoint));
            _endpoint = endpoint;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        public async Task<JObject> SendAsync(JObject request, CancellationToken ct)
        {
            var json = request.ToString(Newtonsoft.Json.Formatting.None);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            using (var resp = await _http.PostAsync(_endpoint, content, ct).ConfigureAwait(false))
            {
                var body = await resp.Content.ReadAsStringAsync();
                return JObject.Parse(body);
            }
        }

        public void Dispose() => _http.Dispose();
    }
}
