/**
 * mathClassify — 判断 $…$ 内容是否可能是数学公式
 *
 * 过滤常见误报：$5 (货币), $PATH (环境变量), 版本号等
 * 保留：\alpha, x^2, 2.5x, sin(x) 等数学表达式
 */
export function isLikelyInlineMath(math: string): boolean {
  if (!math || math !== math.trim() || math.includes('\n')) return false
  if (math.includes('://') || math.includes('](')) return false

  // 数字+变量隐式乘法：2.5x, 3y^2
  if (/^\d+(?:\.\d+)?[A-Za-z](?:[A-Za-z0-9^_{}]*)?$/.test(math)) return true
  // 数字+LaTeX转义：10\%, 5\cdot3
  if (/^\d+(?:\.\d+)?\\(?:%|[A-Za-z]+)(?:\{[^{}]*\})?(?:[A-Za-z0-9\\{}^_+\-*/=<>.()]*)$/.test(math)) return true
  // 纯数字/百分比 → 太常是货币或文本百分比
  if (/^\d+(?:\.\d+)?%?$/.test(math)) return false

  // 一元正负号：+2, -x, +\alpha
  if (/^[+\-]\s*(?:\d+(?:\.\d+)?|[A-Za-z\\])/.test(math)) return true

  // LaTeX 命令
  if (/\\[A-Za-z]+\b/.test(math)) return true
  // 运算符/下标/上标
  if (/[\^_{}|]/.test(math)) return true
  // 数学关键词
  if (/\b(?:alpha|beta|gamma|sum|int|prod|lim|infty|sqrt|frac|sin|cos|tan|log|ln|max|min|partial|nabla|left|right)\b/.test(math)) return true
  // 函数调用格式：f(x)
  if (/^[A-Za-z]\s*\([^)]{1,80}\)$/.test(math)) return true
  // 二元运算：x+1, a=b, x<y
  if (/[A-Za-z0-9)\]}]\s*[+\-*/=<>]\s*[A-Za-z0-9([{\\]/.test(math)) return true
  // 单侧比较：> 0, < B
  if (/^(?:<=?|>=?|≠|≤|≥)\s*[A-Za-z0-9]|[A-Za-z0-9]\s*(?:<=?|>=?|≠|≤|≥)$/.test(math)) return true
  // 逗号分隔的 token：有序对、元组、集合 (A, B)
  if (/^\(?(?:[A-Za-z0-9]|\\[A-Za-z]+)(?:\s*,\s*(?:[A-Za-z0-9]|\\[A-Za-z]+)){1,10}\)?$/.test(math)) return true

  // 两个英文单词（不太可能是数学）
  if (/[A-Za-z]\s+[A-Za-z]/.test(math)) return false
  // 全大写标识符（环境变量）
  if (/^[A-Z][A-Z0-9_]{1,}$/.test(math)) return false
  // 版本号
  if (/^v\d+(?:\.\d+)*$/i.test(math)) return false
  // 纯字母 ≥2 字符
  if (/^[A-Za-z]{2,}$/.test(math)) return false

  // 单字母 (S, A, G, x, y) → 数学中常见
  return /^[A-Za-z]$/.test(math)
}
