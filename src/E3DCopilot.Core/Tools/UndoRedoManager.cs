using System;
using System.Collections.Generic;

namespace E3DCopilot.Core.Tools
{
    /// <summary>
    /// 撤销/重做管理器 — 追踪修改操作的历史记录
    /// 
    /// 设计：
    /// - 每次 modify/design/piping 操作前记录快照（元素名+属性名+旧值）
    /// - undo：恢复旧值
    /// - redo：重新应用新值
    /// - 单例模式，跨工具共享
    /// </summary>
    public class UndoRedoManager
    {
        private static readonly Lazy<UndoRedoManager> _instance = new Lazy<UndoRedoManager>(() => new UndoRedoManager());
        public static UndoRedoManager Instance => _instance.Value;

        private readonly Stack<UndoEntry> _undoStack = new Stack<UndoEntry>();
        private readonly Stack<UndoEntry> _redoStack = new Stack<UndoEntry>();
        private readonly object _lock = new object();

        private const int MaxHistory = 50;

        /// <summary>
        /// 记录一次修改操作（在修改前调用）
        /// </summary>
        public void Record(string element, string attribute, string oldValue, string newValue, string toolName = "modify")
        {
            lock (_lock)
            {
                _undoStack.Push(new UndoEntry
                {
                    Element = element,
                    Attribute = attribute,
                    OldValue = oldValue,
                    NewValue = newValue,
                    ToolName = toolName,
                    Timestamp = DateTime.Now
                });

                // 清空 redo 栈（新操作后无法 redo）
                _redoStack.Clear();

                // 限制历史大小
                if (_undoStack.Count > MaxHistory)
                {
                    var temp = new Stack<UndoEntry>();
                    var arr = _undoStack.ToArray();
                    for (int i = arr.Length - 1; i >= MaxHistory; i--)
                        temp.Push(arr[i]);
                    _undoStack.Clear();
                    while (temp.Count > 0) _undoStack.Push(temp.Pop());
                }
            }
        }

        /// <summary>
        /// 弹出最近一条 undo 记录（不移除，供 peek）
        /// </summary>
        public UndoEntry PeekUndo()
        {
            lock (_lock) { return _undoStack.Count > 0 ? _undoStack.Peek() : null; }
        }

        /// <summary>
        /// 弹出最近一条 undo 记录（移除）
        /// </summary>
        public UndoEntry PopUndo()
        {
            lock (_lock) { return _undoStack.Count > 0 ? _undoStack.Pop() : null; }
        }

        /// <summary>
        /// 推入 redo 记录
        /// </summary>
        public void PushRedo(UndoEntry entry)
        {
            lock (_lock) { _redoStack.Push(entry); }
        }

        /// <summary>
        /// 推入 undo 记录（redo 时恢复到 undo 栈）
        /// </summary>
        public void PushUndo(UndoEntry entry)
        {
            lock (_lock) { _undoStack.Push(entry); }
        }

        /// <summary>
        /// 弹出最近一条 redo 记录
        /// </summary>
        public UndoEntry PopRedo()
        {
            lock (_lock) { return _redoStack.Count > 0 ? _redoStack.Pop() : null; }
        }

        public int UndoCount { get { lock (_lock) { return _undoStack.Count; } } }
        public int RedoCount { get { lock (_lock) { return _redoStack.Count; } } }

        public void Clear()
        {
            lock (_lock)
            {
                _undoStack.Clear();
                _redoStack.Clear();
            }
        }
    }

    public class UndoEntry
    {
        public string Element { get; set; }
        public string Attribute { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public string ToolName { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
