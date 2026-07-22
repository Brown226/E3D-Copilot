using System;
using System.Linq;
using System.Text.Json;
using E3DCopilot.Core.Messaging;

namespace E3DCopilot.WebHost
{
    /// <summary>
    /// Bridge — Trace 诊断日志管理
    /// 提供对话执行轨迹的查询和导出功能，供 AI 诊断分析使用
    /// </summary>
    public partial class Bridge
    {
        /// <summary>
        /// 获取最近一次 trace 文件内容
        /// </summary>
        private void HandleTraceLatest(string requestId)
        {
            try
            {
                var tracer = _controller.Tracer;
                if (tracer == null || !tracer.IsEnabled)
                {
                    SendToFrontend(MessageTypes.TraceLatest, new
                    {
                        enabled = false,
                        message = "Trace 功能未启用，请在配置中设置 logging.traceEnabled = true"
                    }, requestId);
                    return;
                }

                var path = tracer.GetLatestTracePath();
                if (string.IsNullOrEmpty(path))
                {
                    SendToFrontend(MessageTypes.TraceLatest, new
                    {
                        enabled = true,
                        trace = (object)null,
                        message = "暂无 trace 记录，请先进行一次对话"
                    }, requestId);
                    return;
                }

                var content = tracer.ReadTrace(path);
                SendToFrontend(MessageTypes.TraceLatest, new
                {
                    enabled = true,
                    path,
                    fileName = System.IO.Path.GetFileName(path),
                    content
                }, requestId);
            }
            catch (Exception ex)
            {
                SendToFrontend(MessageTypes.Error, new { message = $"获取 Trace 失败: {ex.Message}" }, requestId);
            }
        }

        /// <summary>
        /// 列出最近 N 条 trace 文件
        /// </summary>
        private void HandleTraceList(string requestId)
        {
            try
            {
                var tracer = _controller.Tracer;
                if (tracer == null || !tracer.IsEnabled)
                {
                    SendToFrontend(MessageTypes.TraceList, new
                    {
                        enabled = false,
                        traces = new object[0]
                    }, requestId);
                    return;
                }

                var traces = tracer.ListTraces(20).Select(t => new
                {
                    path = t.Path,
                    fileName = t.FileName,
                    sizeBytes = t.SizeBytes,
                    lastModified = t.LastModified.ToString("yyyy-MM-dd HH:mm:ss")
                }).ToList();

                SendToFrontend(MessageTypes.TraceList, new
                {
                    enabled = true,
                    traceDir = tracer.TraceDir,
                    traces
                }, requestId);
            }
            catch (Exception ex)
            {
                SendToFrontend(MessageTypes.Error, new { message = $"列出 Trace 失败: {ex.Message}" }, requestId);
            }
        }

        /// <summary>
        /// 读取指定 trace 文件内容
        /// </summary>
        private void HandleTraceRead(JsonElement? payload, string requestId)
        {
            try
            {
                string path = null;
                if (payload.HasValue && payload.Value.TryGetProperty("path", out var pathProp))
                    path = pathProp.GetString();

                if (string.IsNullOrEmpty(path))
                {
                    SendToFrontend(MessageTypes.Error, new { message = "缺少 path 参数" }, requestId);
                    return;
                }

                var tracer = _controller.Tracer;
                var content = tracer?.ReadTrace(path);

                if (content == null)
                {
                    SendToFrontend(MessageTypes.Error, new { message = $"无法读取 Trace 文件: {path}" }, requestId);
                    return;
                }

                SendToFrontend(MessageTypes.TraceRead, new
                {
                    path,
                    fileName = System.IO.Path.GetFileName(path),
                    content
                }, requestId);
            }
            catch (Exception ex)
            {
                SendToFrontend(MessageTypes.Error, new { message = $"读取 Trace 失败: {ex.Message}" }, requestId);
            }
        }
    }
}
