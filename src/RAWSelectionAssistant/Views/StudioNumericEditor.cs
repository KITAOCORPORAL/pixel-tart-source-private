using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RAWSelectionAssistant.Services;

namespace RAWSelectionAssistant.Views;

/// <summary>A text draft, never a second parameter value. The adjustment stack is authoritative.</summary>
internal sealed class StudioNumericEditor : TextBox
{
    private readonly Func<double> _read;
    private readonly Action<double> _commit;
    private readonly double _minimum;
    private readonly double _maximum;
    private readonly Func<Guid?>? _identity;
    private Guid? _draftIdentity;
    internal StudioNumericEditor(Func<double> read, Action<double> commit, double minimum, double maximum, Func<Guid?>? identity = null)
    {
        _read = read; _commit = commit; _minimum = minimum; _maximum = maximum;
        _identity = identity;
        GotKeyboardFocus += (_, _) => _draftIdentity = _identity?.Invoke();
        LostKeyboardFocus += (_, _) => { if (!CommitDraft()) RestoreCommitted(); };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && CancelDraft()) e.Handled = true;
            else if (e.Key == Key.Enter)
            {
                if (CommitDraft()) Keyboard.ClearFocus();
                e.Handled = true;
            }
        };
        RestoreCommitted();
    }
    private string CommittedText => _read().ToString("0.###", CultureInfo.CurrentCulture);
    internal bool HasDraft => Text != CommittedText;
    internal bool CancelDraft()
    {
        if (!HasDraft) return false;
        RestoreCommitted(); SelectAll(); return true;
    }
    internal bool CommitDraft()
    {
        if (_identity is not null && _identity() != _draftIdentity) { RestoreCommitted(); return false; }
        if (!HasDraft) return true;
        if (!double.TryParse(Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) ||
            !double.IsFinite(value) || value < _minimum || value > _maximum)
            return Reject();
        try { _commit(value); RestoreCommitted(); return true; }
        catch (ArgumentException) { return Reject(); }
    }
    private bool Reject()
    {
        BorderBrush = Brushes.OrangeRed;
        StudioToolPanel.Text(this, ToolTipProperty, "NumericInvalid", $"{{0}} ({_minimum}–{_maximum})");
        return false;
    }
    internal void RefreshCommitted() { if (!IsKeyboardFocusWithin) RestoreCommitted(); }
    internal void RestoreCommitted()
    {
        _draftIdentity = _identity?.Invoke();
        Text = CommittedText;
        ClearValue(BorderBrushProperty);
        StudioToolPanel.Text(this, ToolTipProperty, "Enter 保存，Esc 取消", $"{_minimum}–{_maximum} · {{0}}");
    }
}
