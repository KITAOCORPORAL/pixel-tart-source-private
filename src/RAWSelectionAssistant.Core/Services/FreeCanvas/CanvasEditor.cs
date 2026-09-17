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
    public void Scale(double factor, double? anchorX = null, double? anchorY = null)
    {
        if (!double.IsFinite(factor) || factor <= 0 || Selected.Any(item => !Editable(item))) return;
        var bounds = Bounds();
        var x=anchorX??bounds.X;var y=anchorY??bounds.Y;
        factor = Math.Clamp(factor, .02, 50);
        Transform(item => item with { X = x + (item.X - x) * factor, Y = y + (item.Y - y) * factor, Width = Math.Max(1, item.Width * factor), Height = Math.Max(1, item.Height * factor), FontSize = item.IsText ? Math.Max(1, item.FontSize * factor) : item.FontSize });
    }
    public void Remove() { var ids = Document.Objects.Where(Editable).Select(item => item.ObjectId).ToHashSet(); if (ids.Count > 0) Commit(Document with { Objects = Document.Objects.Where(item => !ids.Contains(item.ObjectId)).ToArray() }); }
    public void Copy() => _clipboard = Selected.ToArray();
    public void Paste()
    {
        var groups = _clipboard.Where(item => item.GroupId is not null).Select(item => item.GroupId!.Value).Distinct().ToDictionary(id => id, _ => Guid.NewGuid());
        Add(_clipboard.Select(item => item with { X = item.X + 24, Y = item.Y + 24, GroupId = item.GroupId is Guid group ? groups[group] : null, Locked = false }));
    }
    public void Duplicate() { Copy(); Paste(); }
    public void Rotate(double degrees, bool snap = false)
    {
        if (!double.IsFinite(degrees)) return;
        if(Selected.Count>1&&Selected.Any(item=>!Editable(item)))return;
        if(snap)degrees=Math.Round(((Selected.FirstOrDefault()?.Rotation??0)+degrees)/15)*15-(Selected.FirstOrDefault()?.Rotation??0);
        var bounds=Bounds();var cx=bounds.X+bounds.Width/2;var cy=bounds.Y+bounds.Height/2;var radians=degrees*Math.PI/180;
        Transform(item =>
        {
            var x=item.X+item.Width/2-cx;var y=item.Y+item.Height/2-cy;
            return item with { X=Selected.Count>1?cx+x*Math.Cos(radians)-y*Math.Sin(radians)-item.Width/2:item.X,Y=Selected.Count>1?cy+x*Math.Sin(radians)+y*Math.Cos(radians)-item.Height/2:item.Y,Rotation=NormalizeAngle(item.Rotation+degrees) };
        });
    }
    private static double NormalizeAngle(double angle) => ((angle % 360) + 360) % 360;
    public void Flip(bool horizontal) => Transform(item => horizontal ? item with { FlipX = !item.FlipX } : item with { FlipY = !item.FlipY });
    public void Crop(CanvasCrop crop)
    {
        if (!new[] { crop.X, crop.Y, crop.Width, crop.Height }.All(double.IsFinite)) return;
        crop = crop.Normalize();
        Transform(item => item.IsText ? item : item with
        {
            CropRect = crop,
            Height = item.Width * item.SourceHeight * crop.Height / (Math.Max(1, item.SourceWidth) * crop.Width)
        });
    }
    public static CanvasCrop CropForAspect(CanvasObject item, double? ratio)
    {
        if (ratio is null || ratio <= 0) return new();
        var original = Math.Max(1, item.SourceWidth) / Math.Max(1, item.SourceHeight);
        var width = Math.Min(1, ratio.Value / original);
        var height = Math.Min(1, original / ratio.Value);
        return new((1 - width) / 2, (1 - height) / 2, width, height);
    }
    public void EditText(string text, double fontSize, string color) => Transform(item => item.IsText ? item with { Text = text, FontSize = Math.Clamp(fontSize, 8, 300), TextColor = color } : item);
    public void Group()
    {
        if (Selected.Count < 2 || Selected.Any(item => !Editable(item))) return;
        var group = Guid.NewGuid(); Transform(item => item with { GroupId = group });
    }
    public void Ungroup() => Transform(item => item with { GroupId = null });
    public void SetLocked(bool locked)
    {
        if (Selected.Count > 0) Commit(Document with { Objects = Document.Objects.Select(item => Selection.Contains(item.ObjectId) ? item with { Locked = locked } : item).ToArray() });
    }
    public void Layer(bool top)
    {
        var ordered = Document.Objects.OrderBy(item => item.ZIndex).ToArray();
        var movable = ordered.Where(Editable).ToArray(); var others = ordered.Where(item => !Editable(item)).ToArray();
        if (movable.Length == 0) return;
        Commit(Document with { Objects = (top ? others.Concat(movable) : movable.Concat(others)).Select((item, index) => item with { ZIndex = index }).ToArray() });
    }
    public void Align(string mode)
    {
        var bounds = Bounds();
        Transform(item => mode switch
        {
            "left" => item with { X = bounds.X },
            "top" => item with { Y = bounds.Y },
            "center" => item with { X = bounds.X + (bounds.Width - item.Width) / 2 },
            _ => item
        });
    }
    public void Arrange(string mode)
    {
        var units = Document.Objects.Where(Editable).GroupBy(item => item.GroupId ?? item.ObjectId).ToArray();
        if (units.Length == 0) return;
        var columns = mode == "horizontal" ? units.Length : Math.Max(1, (int)Math.Ceiling(Math.Sqrt(units.Length)));
        var bounds = Bounds(); var x = bounds.X; var y = bounds.Y; var rowHeight = 0d; var column = 0;
        var updates = new Dictionary<Guid, CanvasObject>();
        var cellWidth = units.Max(unit => unit.Max(item => item.X + item.Width) - unit.Min(item => item.X));
        foreach (var unit in units)
        {
            var left = unit.Min(item => item.X); var top = unit.Min(item => item.Y);
            var width = unit.Max(item => item.X + item.Width) - left; var height = unit.Max(item => item.Y + item.Height) - top;
            foreach (var item in unit) updates[item.ObjectId] = item with { X = x + item.X - left, Y = y + item.Y - top };
            x += (mode == "grid" ? cellWidth : width) + 24; rowHeight = Math.Max(rowHeight, height);
            if (++column == columns) { x = bounds.X; y += rowHeight + 24; rowHeight = 0; column = 0; }
        }
        Commit(Document with { Objects = Document.Objects.Select(item => updates.GetValueOrDefault(item.ObjectId, item)).ToArray() });
    }
    public void SetProject(Guid? id) => Commit(Document with { ProjectId = id });
    public void Rename(string name) { if (!string.IsNullOrWhiteSpace(name)) Commit(Document with { Name = name.Trim() }); }
    public void Undo() { if (_undo.TryPop(out var before)) { _redo.Push(Document); Document = before; Selection.IntersectWith(Document.Objects.Select(item => item.ObjectId)); Changed?.Invoke(this, EventArgs.Empty); } }
    public void Redo() { if (_redo.TryPop(out var after)) { _undo.Push(Document); Document = after; Selection.IntersectWith(Document.Objects.Select(item => item.ObjectId)); Changed?.Invoke(this, EventArgs.Empty); } }
}
