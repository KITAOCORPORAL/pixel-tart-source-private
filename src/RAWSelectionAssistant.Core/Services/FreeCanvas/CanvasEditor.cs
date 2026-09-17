namespace RAWSelectionAssistant.Core.Services.FreeCanvas;

public readonly record struct CanvasBounds(double X, double Y, double Width, double Height);

/// <summary>Pure, undoable document edits. All coordinates are unbounded logical units.</summary>
public sealed class CanvasEditor
{
    private readonly Stack<CanvasDocument> _undo = new(), _redo = new();
    private CanvasObject[] _clipboard = [];
    private CanvasDocument? _gesture;
    public CanvasEditor(CanvasDocument document) { CanvasDocumentStore.Validate(document); Document = document; }
    public CanvasDocument Document { get; private set; }
    public HashSet<Guid> Selection { get; } = [];
    public IReadOnlyList<CanvasObject> Selected => Document.Objects.Where(item => Selection.Contains(item.ObjectId)).ToArray();
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public event EventHandler? Changed;
    public void Select(Guid? id, bool additive = false)
    {
        if (!additive) Selection.Clear();
        if (id is Guid value && Document.Objects.FirstOrDefault(item => item.ObjectId == value) is { } hit)
        {
            var ids = hit.GroupId is Guid group ? Document.Objects.Where(item => item.GroupId == group).Select(item => item.ObjectId).ToArray() : [value];
            var remove = additive && ids.All(Selection.Contains);
            foreach (var member in ids) { if (remove) Selection.Remove(member); else Selection.Add(member); }
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }
    public void SelectAll() { Selection.UnionWith(Document.Objects.Select(item => item.ObjectId)); Changed?.Invoke(this, EventArgs.Empty); }
    public void Marquee(CanvasBounds rect, bool additive = false)
    {
        if (!additive) Selection.Clear();
        foreach (var item in Document.Objects.Where(item => item.X < rect.X + rect.Width && item.X + item.Width > rect.X && item.Y < rect.Y + rect.Height && item.Y + item.Height > rect.Y))
            if (item.GroupId is Guid group) Selection.UnionWith(Document.Objects.Where(other => other.GroupId == group).Select(other => other.ObjectId)); else Selection.Add(item.ObjectId);
        Changed?.Invoke(this, EventArgs.Empty);
    }
    public void Add(IEnumerable<CanvasObject> objects)
    {
        var start = Document.Objects.Select(item => item.ZIndex).DefaultIfEmpty(-1).Max() + 1;
        var added = objects.Select((item, index) => item with { CanvasId = Document.CanvasId, ObjectId = Guid.NewGuid(), ZIndex = start + index }).ToArray();
        if (added.Length == 0) return;
        Commit(Document with { Objects = Document.Objects.Concat(added).ToArray() });
        Selection.Clear(); Selection.UnionWith(added.Select(item => item.ObjectId)); Changed?.Invoke(this, EventArgs.Empty);
    }
    public void AddText(double x, double y, string text = "输入文字") => Add([new() { X = x, Y = y, Width = 300, Height = 80, Text = text, Name = "文字" }]);
    public void BeginGesture() => _gesture ??= Document;
    public void EndGesture(bool cancel = false)
    {
        if (_gesture is not { } before) return;
        _gesture = null;
        if (cancel) Document = before;
        else if (!ReferenceEquals(before, Document)) { _undo.Push(before); _redo.Clear(); }
        Changed?.Invoke(this, EventArgs.Empty);
    }
    private void Commit(CanvasDocument next)
    {
        CanvasDocumentStore.Validate(next);
        if (_gesture is null) { _undo.Push(Document); _redo.Clear(); }
        Document = next with { UpdatedAt = DateTimeOffset.UtcNow };
        Selection.IntersectWith(Document.Objects.Select(item => item.ObjectId));
        Changed?.Invoke(this, EventArgs.Empty);
    }
    private bool Editable(CanvasObject item) => Selection.Contains(item.ObjectId) && !item.Locked && (item.GroupId is null || !Document.Objects.Any(other => other.GroupId == item.GroupId && other.Locked));
    public void Transform(Func<CanvasObject, CanvasObject> transform)
    {
        if (!Document.Objects.Any(Editable)) return;
        Commit(Document with { Objects = Document.Objects.Select(item => Editable(item) ? transform(item) : item).ToArray() });
    }
    public CanvasBounds Bounds(bool selectedOnly = true)
    {
        var items = (selectedOnly ? Selected : Document.Objects).ToArray();
        if (items.Length == 0) return new(0, 0, 1, 1);
        var left = items.Min(item => item.X); var top = items.Min(item => item.Y);
        return new(left, top, items.Max(item => item.X + item.Width) - left, items.Max(item => item.Y + item.Height) - top);
    }
    public void Move(double dx, double dy) => Transform(item => item with { X = item.X + dx, Y = item.Y + dy });
    public void Scale(double factor)
    {
        if (!double.IsFinite(factor) || factor <= 0 || Selected.Any(item => !Editable(item))) return;
        var bounds = Bounds();
        factor = Math.Clamp(factor, .02, 50);
        Transform(item => item with { X = bounds.X + (item.X - bounds.X) * factor, Y = bounds.Y + (item.Y - bounds.Y) * factor, Width = Math.Max(1, item.Width * factor), Height = Math.Max(1, item.Height * factor), FontSize = item.IsText ? Math.Max(1, item.FontSize * factor) : item.FontSize });
    }
    public void Remove() { var ids = Document.Objects.Where(Editable).Select(item => item.ObjectId).ToHashSet(); if (ids.Count > 0) Commit(Document with { Objects = Document.Objects.Where(item => !ids.Contains(item.ObjectId)).ToArray() }); }
    public void Copy() => _clipboard = Selected.ToArray();
    public void Paste()
    {
        var groups = _clipboard.Where(item => item.GroupId is not null).Select(item => item.GroupId!.Value).Distinct().ToDictionary(id => id, _ => Guid.NewGuid());
        Add(_clipboard.Select(item => item with { X = item.X + 24, Y = item.Y + 24, GroupId = item.GroupId is Guid group ? groups[group] : null, Locked = false }));
    }
    public void Duplicate() { Copy(); Paste(); }
    public void EditText(string text, double fontSize, string color) => Transform(item => item.IsText ? item with { Text = text, FontSize = Math.Clamp(fontSize, 8, 300), TextColor = color } : item);
    public void SetProject(Guid? id) => Commit(Document with { ProjectId = id });
    public void Rename(string name) { if (!string.IsNullOrWhiteSpace(name)) Commit(Document with { Name = name.Trim() }); }
    public void Undo() { if (_undo.TryPop(out var before)) { _redo.Push(Document); Document = before; Selection.IntersectWith(Document.Objects.Select(item => item.ObjectId)); Changed?.Invoke(this, EventArgs.Empty); } }
    public void Redo() { if (_redo.TryPop(out var after)) { _undo.Push(Document); Document = after; Selection.IntersectWith(Document.Objects.Select(item => item.ObjectId)); Changed?.Invoke(this, EventArgs.Empty); } }
}
