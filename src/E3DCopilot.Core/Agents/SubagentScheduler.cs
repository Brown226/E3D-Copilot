using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Logging;

namespace E3DCopilot.Core.Agents
{
    /// <summary>
    /// 子代理并发调度器 — 对齐 Reasonix internal/agent/scheduler.go + write_claims.go
    ///
    /// 功能：
    ///   - SemaphoreSlim 控制并发（默认 max=3）
    ///   - 写声明（Write Claims）：子代理声明要写的元素路径，冲突时串行化
    ///   - 任务队列 + 结果收集
    /// </summary>
    public class SubagentScheduler
    {
        private readonly int _maxConcurrency;
        private readonly SemaphoreSlim _semaphore;
        private readonly ConcurrentDictionary<string, string> _writeClaims
            = new ConcurrentDictionary<string, string>(); // elementPath → ownerId

        /// <summary>默认最大并发数（对齐 Reasonix agent.max_subagent_concurrency=6，E3D 保守取 3）</summary>
        public const int DefaultMaxConcurrency = 3;

        public SubagentScheduler(int maxConcurrency = DefaultMaxConcurrency)
        {
            _maxConcurrency = maxConcurrency > 0 ? maxConcurrency : DefaultMaxConcurrency;
            _semaphore = new SemaphoreSlim(_maxConcurrency, _maxConcurrency);
        }

        /// <summary>当前活跃的子代理数</summary>
        public int ActiveCount => _maxConcurrency - _semaphore.CurrentCount;

        /// <summary>最大并发数</summary>
        public int MaxConcurrency => _maxConcurrency;

        // ═══════════════════════════════════════════════════════════
        //  调度执行
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 调度一个子代理任务（受并发限制）
        /// </summary>
        public async Task<T> ScheduleAsync<T>(string ownerId, Func<CancellationToken, Task<T>> work,
            CancellationToken ct = default)
        {
            await _semaphore.WaitAsync(ct);
            try
            {
                return await work(ct);
            }
            finally
            {
                _semaphore.Release();
                // 释放该 owner 的所有写声明
                ReleaseClaims(ownerId);
            }
        }

        /// <summary>
        /// 批量调度多个子代理任务（对齐 Reasonix fleet dispatch）
        /// 返回所有结果（按输入顺序）
        /// </summary>
        public async Task<List<ScheduledResult<T>>> ScheduleBatchAsync<T>(
            List<ScheduledTask<T>> tasks, CancellationToken ct = default)
        {
            var results = new List<ScheduledResult<T>>(new ScheduledResult<T>[tasks.Count]);
            var running = new List<Task>();

            for (int i = 0; i < tasks.Count; i++)
            {
                int idx = i; // 闭包捕获
                var task = tasks[idx];

                var t = ScheduleAsync(task.OwnerId, async token =>
                {
                    try
                    {
                        var result = await task.Work(token);
                        results[idx] = new ScheduledResult<T>
                        {
                            Index = idx,
                            OwnerId = task.OwnerId,
                            Value = result,
                            Success = true
                        };
                        return result;
                    }
                    catch (Exception ex)
                    {
                        results[idx] = new ScheduledResult<T>
                        {
                            Index = idx,
                            OwnerId = task.OwnerId,
                            Error = ex.Message,
                            Success = false
                        };
                        return default(T);
                    }
                }, ct);

                running.Add(t);
            }

            await Task.WhenAll(running);
            return results;
        }

        // ═══════════════════════════════════════════════════════════
        //  写声明（Write Claims）— 对齐 Reasonix write_claims.go
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 尝试声明对指定元素路径的写权限。
        /// 如果已被其他 owner 声明，返回 false（调用方应等待或串行化）。
        /// </summary>
        public bool TryClaimWrite(string ownerId, string elementPath)
        {
            if (string.IsNullOrEmpty(elementPath)) return true;

            string normalized = NormalizePath(elementPath);
            return _writeClaims.TryAdd(normalized, ownerId);
        }

        /// <summary>
        /// 检查指定路径是否已被其他 owner 声明
        /// </summary>
        public bool IsClaimedByOther(string ownerId, string elementPath)
        {
            if (string.IsNullOrEmpty(elementPath)) return false;

            string normalized = NormalizePath(elementPath);
            string existingOwner;
            if (_writeClaims.TryGetValue(normalized, out existingOwner))
            {
                return existingOwner != ownerId;
            }
            return false;
        }

        /// <summary>
        /// 释放指定 owner 的所有写声明
        /// </summary>
        public void ReleaseClaims(string ownerId)
        {
            var toRemove = _writeClaims
                .Where(kv => kv.Value == ownerId)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in toRemove)
            {
                string removed;
                _writeClaims.TryRemove(key, out removed);
            }
        }

        /// <summary>
        /// 获取当前所有活跃的写声明
        /// </summary>
        public Dictionary<string, string> GetActiveClaims()
        {
            return new Dictionary<string, string>(_writeClaims);
        }

        private static string NormalizePath(string path)
        {
            return (path ?? "").Trim().ToUpperInvariant();
        }
    }

    /// <summary>调度任务定义</summary>
    public class ScheduledTask<T>
    {
        public string OwnerId { get; set; }
        public Func<CancellationToken, Task<T>> Work { get; set; }
    }

    /// <summary>调度结果</summary>
    public class ScheduledResult<T>
    {
        public int Index { get; set; }
        public string OwnerId { get; set; }
        public T Value { get; set; }
        public string Error { get; set; }
        public bool Success { get; set; }
    }
}
