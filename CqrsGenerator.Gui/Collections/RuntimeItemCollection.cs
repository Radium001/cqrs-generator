using System.Collections.ObjectModel;

namespace CqrsGenerator.Gui.Collections;

public sealed class RuntimeItemCollection<T> : ObservableCollection<T>
{
    private readonly Func<T, string> _keySelector;
    private readonly List<T> _discovered = [];
    private readonly List<T> _runtime = [];

    public RuntimeItemCollection(Func<T, string> keySelector)
    {
        _keySelector = keySelector;
    }

    public bool HasRuntimeItems => _runtime.Count > 0;

    public void SetDiscovered(IEnumerable<T> items)
    {
        _discovered.Clear();
        _discovered.AddRange(items);
        Rebuild();
    }

    public void AddRuntime(T item)
    {
        if (_discovered.Any(d => _keySelector(d) == _keySelector(item)))
            return;

        var existing = _runtime.FirstOrDefault(r => _keySelector(r) == _keySelector(item));
        if (existing is not null)
            _runtime.Remove(existing);

        _runtime.Add(item);
        Rebuild();
    }

    public bool RemoveRuntime(T item)
    {
        var removed = _runtime.Remove(item);
        if (removed)
            Rebuild();
        return removed;
    }

    public void ClearRuntime()
    {
        if (_runtime.Count == 0)
            return;
        _runtime.Clear();
        Rebuild();
    }

    private void Rebuild()
    {
        Clear();
        foreach (var item in _discovered.Concat(_runtime))
            Add(item);
    }
}
