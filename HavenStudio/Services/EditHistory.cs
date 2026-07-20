using System;
using System.Collections.Generic;

namespace HavenStudio.Services;

public sealed class EditHistory
{
    private sealed record Entry(string Description, Action Undo, Action Redo);

    private sealed class PendingEntry
    {
        public PendingEntry(string description, Action undo)
        {
            Description = description;
            Undo = undo;
        }

        public string Description { get; }
        public Action Undo { get; }
        public Action? Redo { get; set; }
    }

    private readonly List<Entry> _undo = [];
    private readonly List<Entry> _redo = [];
    private readonly int _capacity;
    private PendingEntry? _pending;

    public EditHistory(int capacity = 100)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _capacity = capacity;
    }

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public int Count => _undo.Count;
    public string? UndoDescription => _undo.Count == 0 ? null : _undo[^1].Description;
    public string? RedoDescription => _redo.Count == 0 ? null : _redo[^1].Description;
    public bool IsCoalescing => _pending != null;

    public event Action? Changed;

    public void Execute(string description, Action execute, Action undo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(execute);
        ArgumentNullException.ThrowIfNull(undo);

        execute();
        RecordApplied(description, undo, execute);
    }

    public void RecordApplied(string description, Action undo, Action redo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(undo);
        ArgumentNullException.ThrowIfNull(redo);

        CancelCoalesced();
        AddUndo(new Entry(description, undo, redo));
        _redo.Clear();
        Changed?.Invoke();
    }

    public void BeginCoalesced(string description, Action undo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(undo);

        CancelCoalesced();
        _pending = new PendingEntry(description, undo);
    }

    public void UpdateCoalesced(Action redo)
    {
        ArgumentNullException.ThrowIfNull(redo);
        if (_pending == null)
        {
            throw new InvalidOperationException("No coalesced edit is active.");
        }

        _pending.Redo = redo;
    }

    public bool CommitCoalesced()
    {
        var pending = _pending;
        _pending = null;
        if (pending?.Redo == null)
        {
            return false;
        }

        AddUndo(new Entry(pending.Description, pending.Undo, pending.Redo));
        _redo.Clear();
        Changed?.Invoke();
        return true;
    }

    public void CancelCoalesced()
    {
        _pending = null;
    }

    public bool Undo()
    {
        CancelCoalesced();
        if (_undo.Count == 0)
        {
            return false;
        }

        var entry = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        try
        {
            entry.Undo();
        }
        catch
        {
            _undo.Add(entry);
            throw;
        }

        _redo.Add(entry);
        Changed?.Invoke();
        return true;
    }

    public bool Redo()
    {
        CancelCoalesced();
        if (_redo.Count == 0)
        {
            return false;
        }

        var entry = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        try
        {
            entry.Redo();
        }
        catch
        {
            _redo.Add(entry);
            throw;
        }

        AddUndo(entry);
        Changed?.Invoke();
        return true;
    }

    public void Clear()
    {
        _pending = null;
        if (_undo.Count == 0 && _redo.Count == 0)
        {
            return;
        }

        _undo.Clear();
        _redo.Clear();
        Changed?.Invoke();
    }

    private void AddUndo(Entry entry)
    {
        _undo.Add(entry);
        if (_undo.Count > _capacity)
        {
            _undo.RemoveAt(0);
        }
    }
}
