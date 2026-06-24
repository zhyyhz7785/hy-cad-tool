namespace HyCAD.Tables.Operations;

/// <summary>
/// 表格操作日志（Table OpLog）：快照指针式 Undo/Redo 内存栈。
/// </summary>
public sealed class TableOpLog
{
    private readonly TableGrid _initial;
    private readonly List<(TableOperation Op, TableGrid Snapshot)> _entries = new();
    private int _cursor = -1;

    /// <summary>以初始表格创建空日志。</summary>
    public TableOpLog(TableGrid initial)
    {
        _initial = initial ?? throw new ArgumentNullException(nameof(initial));
    }

    /// <summary>当前表格状态。</summary>
    public TableGrid Current =>
        _cursor < 0 ? _initial : _entries[_cursor].Snapshot;

    /// <summary>是否可撤销。</summary>
    public bool CanUndo => _cursor >= 0;

    /// <summary>是否可重做。</summary>
    public bool CanRedo => _cursor < _entries.Count - 1;

    /// <summary>已应用的命令序列（审计）。</summary>
    public IReadOnlyList<TableOperation> History
    {
        get
        {
            if (_cursor < 0)
                return Array.Empty<TableOperation>();

            var history = new TableOperation[_cursor + 1];
            for (var i = 0; i <= _cursor; i++)
                history[i] = _entries[i].Op;
            return history;
        }
    }

    /// <summary>应用操作并压入历史；截断 redo 分支。</summary>
    public TableGrid Apply(TableOperation op)
    {
        if (op == null)
            throw new ArgumentNullException(nameof(op));

        var next = op.Apply(Current);

        if (_cursor < _entries.Count - 1)
            _entries.RemoveRange(_cursor + 1, _entries.Count - _cursor - 1);

        _entries.Add((op, next));
        _cursor++;

        return next;
    }

    /// <summary>撤销一步，返回当前状态。</summary>
    public TableGrid Undo()
    {
        if (!CanUndo)
            throw new InvalidOperationException("没有可撤销的操作。");

        _cursor--;
        return Current;
    }

    /// <summary>重做一步，返回当前状态。</summary>
    public TableGrid Redo()
    {
        if (!CanRedo)
            throw new InvalidOperationException("没有可重做的操作。");

        _cursor++;
        return Current;
    }
}
