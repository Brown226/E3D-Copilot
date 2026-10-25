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

        // ── 创建批次栈：cad_import / design 建模类工具的"一键批量删除"撤销 ──
        private readonly Stack<CreationBatch> _creationStack = new Stack<CreationBatch>();
        private const int MaxCreationBatches = 20;

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
                _creationStack.Clear();
            }
        }

        // ════════════════════════════════════════
        //  创建批次（cad_import 等批量建模的撤销）
        // ════════════════════════════════════════

        /// <summary>记录一次批量创建（元素创建成功后调用，undo_create 可整批删除）</summary>
        public void RecordCreation(string toolName, string description, System.Collections.Generic.IEnumerable<string> elementNames)
        {
            if (elementNames == null) return;
            lock (_lock)
            {
                _creationStack.Push(new CreationBatch
                {
                    ToolName = toolName,
                    Description = description,
                    Elements = new System.Collections.Generic.List<string>(elementNames),
                    Timestamp = DateTime.Now
                });
                while (_creationStack.Count > MaxCreationBatches)
                {
                    var temp = new Stack<CreationBatch>();
                    var arr = _creationStack.ToArray();
                    for (int i = arr.Length - 2; i >= 0; i--) temp.Push(arr[i]);
                    _creationStack.Clear();
                    while (temp.Count > 0) _creationStack.Push(temp.Pop());
                }
            }
        }

        public CreationBatch PeekCreation()
        {
            lock (_lock) { return _creationStack.Count > 0 ? _creationStack.Peek() : null; }
        }

        public CreationBatch PopCreation()
        {
            lock (_lock) { return _creationStack.Count > 0 ? _creationStack.Pop() : null; }
        }

        public int CreationCount { get { lock (_lock) { return _creationStack.Count; } } }
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
    /// <summary>一次批量创建的记录（cad_import / design 建模），undo_create 时整批删除</summary>
    public class CreationBatch
    {
        public string ToolName { get; set; }
        public string Description { get; set; }
        public System.Collections.Generic.List<string> Elements { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
