using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace E3DCopilot.Core.Memory
{
    /// <summary>
    /// BM25 轻量全文检索引擎 — 纯 C# 实现，无外部依赖
    /// 对齐 Reasonix internal/retrieval/ 的 BM25 + relative score floor
    ///
    /// 特性：
    ///   - 中文分词：字符 bigram + 空格/标点分词混合策略
    ///   - 英文分词：空格 + 标点分割 + 小写化
    ///   - BM25 评分：k1=1.2, b=0.75（经典参数）
    ///   - Relative score floor：低于最高分 30% 的结果被过滤
    ///   - 增量索引：支持 Add/Remove 文档
    /// </summary>
    public class BM25Index
    {
        // BM25 经典参数
        private const double K1 = 1.2;
        private const double B = 0.75;
        // Relative score floor：低于最高分此比例的结果被过滤
        private const double RelativeScoreFloor = 0.3;

        private readonly List<IndexedDoc> _docs = new List<IndexedDoc>();
        private readonly Dictionary<string, int> _docFreq = new Dictionary<string, int>(); // term → 包含该 term 的文档数
        private double _avgDocLen = 0;

        /// <summary>索引中的文档数量</summary>
        public int Count => _docs.Count;

        // ═══════════════════════════════════════════════════════════
        //  索引管理
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 添加文档到索引
        /// </summary>
        public void Add(string docId, string text, string source = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            var terms = Tokenize(text);
            var termFreq = new Dictionary<string, int>();
            foreach (var t in terms)
            {
                if (!termFreq.ContainsKey(t)) termFreq[t] = 0;
                termFreq[t]++;
            }

            var doc = new IndexedDoc
            {
                DocId = docId,
                Source = source,
                TermFreq = termFreq,
                Length = terms.Count
            };
            _docs.Add(doc);

            // 更新文档频率
            foreach (var term in termFreq.Keys)
            {
                if (!_docFreq.ContainsKey(term)) _docFreq[term] = 0;
                _docFreq[term]++;
            }

            // 更新平均文档长度
            RecalcAvgLen();
        }

        /// <summary>
        /// 批量添加文档
        /// </summary>
        public void AddRange(IEnumerable<KeyValuePair<string, string>> docs)
        {
            foreach (var kv in docs)
                Add(kv.Key, kv.Value);
        }

        /// <summary>
        /// 从索引中移除文档
        /// </summary>
        public bool Remove(string docId)
        {
            var doc = _docs.FirstOrDefault(d => d.DocId == docId);
            if (doc == null) return false;

            _docs.Remove(doc);
            foreach (var term in doc.TermFreq.Keys)
            {
                if (_docFreq.ContainsKey(term))
                {
                    _docFreq[term]--;
                    if (_docFreq[term] <= 0)
                        _docFreq.Remove(term);
                }
            }
            RecalcAvgLen();
            return true;
        }

        /// <summary>
        /// 清空索引
        /// </summary>
        public void Clear()
        {
            _docs.Clear();
            _docFreq.Clear();
            _avgDocLen = 0;
        }

        // ═══════════════════════════════════════════════════════════
        //  搜索
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// BM25 搜索，返回按分数降序排列的结果。
        /// 应用 relative score floor 过滤低分结果。
        /// </summary>
        public List<SearchHit> Search(string query, int topK = 10)
        {
            if (string.IsNullOrWhiteSpace(query) || _docs.Count == 0)
                return new List<SearchHit>();

            var queryTerms = Tokenize(query);
            if (queryTerms.Count == 0)
                return new List<SearchHit>();

            int N = _docs.Count;
            var scores = new double[_docs.Count];

            for (int i = 0; i < _docs.Count; i++)
            {
                double score = 0;
                var doc = _docs[i];

                foreach (var term in queryTerms)
                {
                    int tf;
                    if (!doc.TermFreq.TryGetValue(term, out tf) || tf == 0)
                        continue;

                    int df;
                    _docFreq.TryGetValue(term, out df);
                    if (df == 0) continue;

                    // IDF: log((N - df + 0.5) / (df + 0.5) + 1)
                    double idf = Math.Log((N - df + 0.5) / (df + 0.5) + 1.0);

                    // TF normalization
                    double tfNorm = (tf * (K1 + 1)) /
                        (tf + K1 * (1 - B + B * doc.Length / Math.Max(_avgDocLen, 1)));

                    score += idf * tfNorm;
                }

                scores[i] = score;
            }

            // 找最高分
            double maxScore = 0;
            for (int i = 0; i < scores.Length; i++)
            {
                if (scores[i] > maxScore) maxScore = scores[i];
            }

            if (maxScore <= 0)
                return new List<SearchHit>();

            // Relative score floor：过滤低于最高分 30% 的结果
            double floor = maxScore * RelativeScoreFloor;

            var results = new List<SearchHit>();
            for (int i = 0; i < _docs.Count; i++)
            {
                if (scores[i] >= floor)
                {
                    results.Add(new SearchHit
                    {
                        DocId = _docs[i].DocId,
                        Source = _docs[i].Source,
                        Score = scores[i],
                        NormalizedScore = scores[i] / maxScore
                    });
                }
            }

            return results
                .OrderByDescending(h => h.Score)
                .Take(topK)
                .ToList();
        }

        // ═══════════════════════════════════════════════════════════
        //  分词器 — 中英文混合策略
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 混合分词：
        ///   - 英文/数字：按空格和标点分割，小写化，长度 > 1
        ///   - 中文：字符 bigram（滑动窗口 2）+ 单字（高频字）
        ///   - 混合：先按非字母数字非中文字符分割，再对每段分别处理
        /// </summary>
        public static List<string> Tokenize(string text)
        {
            if (string.IsNullOrEmpty(text)) return new List<string>();

            var tokens = new List<string>();
            var currentWord = new StringBuilder();

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (IsCjk(c))
                {
                    // 先 flush 当前英文词
                    FlushWord(currentWord, tokens);

                    // 中文 bigram
                    if (i + 1 < text.Length && IsCjk(text[i + 1]))
                    {
                        tokens.Add(new string(new[] { c, text[i + 1] }));
                    }
                    // 单字也加入（对短查询有效）
                    tokens.Add(c.ToString());
                }
                else if (char.IsLetterOrDigit(c))
                {
                    currentWord.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    // 分隔符
                    FlushWord(currentWord, tokens);
                }
            }
            FlushWord(currentWord, tokens);

            return tokens;
        }

        private static void FlushWord(StringBuilder sb, List<string> tokens)
        {
            if (sb.Length > 1) // 长度 > 1 才有意义
                tokens.Add(sb.ToString());
            sb.Clear();
        }

        private static bool IsCjk(char c)
        {
            // CJK Unified Ideographs + Extension A + 常用标点
            return (c >= 0x4E00 && c <= 0x9FFF) ||   // CJK 基本区
                   (c >= 0x3400 && c <= 0x4DBF) ||   // CJK 扩展 A
                   (c >= 0xF900 && c <= 0xFAFF) ||   // CJK 兼容
                   (c >= 0x3000 && c <= 0x303F);     // CJK 标点
        }

        private void RecalcAvgLen()
        {
            if (_docs.Count == 0)
            {
                _avgDocLen = 0;
                return;
            }
            long total = 0;
            foreach (var d in _docs)
                total += d.Length;
            _avgDocLen = (double)total / _docs.Count;
        }

        // ═══════════════════════════════════════════════════════════
        //  数据结构
        // ═══════════════════════════════════════════════════════════

        private class IndexedDoc
        {
            public string DocId;
            public string Source;
            public Dictionary<string, int> TermFreq;
            public int Length;
        }
    }

    /// <summary>
    /// 搜索命中结果
    /// </summary>
    public class SearchHit
    {
        /// <summary>文档 ID</summary>
        public string DocId { get; set; }

        /// <summary>来源标识（文件路径或会话 ID）</summary>
        public string Source { get; set; }

        /// <summary>BM25 原始分数</summary>
        public double Score { get; set; }

        /// <summary>归一化分数（0~1，最高分 = 1）</summary>
        public double NormalizedScore { get; set; }
    }
}
