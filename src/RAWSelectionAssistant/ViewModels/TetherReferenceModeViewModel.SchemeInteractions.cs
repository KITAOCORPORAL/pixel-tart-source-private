using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Utilities;
using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.ViewModels;

public sealed partial class TetherReferenceModeViewModel
{
    private ColorStudioSchemeV2? _appliedColorScheme;
    private ColorStudioSchemeV2? _pendingScheme;
    private ColorStudioSchemeV2? _pendingDeleteScheme;
    private bool _schemeSwitchOpen;
    private bool _schemeDeleteOpen;
    public string CurrentColorSchemeName => _appliedColorScheme?.Name ?? "尚未保存";
    public bool HasUnsavedSchemeChanges => AdjustmentStack.Nodes.Count > 0 && (_persistedStack is null ||
        ColorStudioSchemeSerializer.ComputeHash(new ColorStudioSchemeV2(Guid.Empty, "session", AdjustmentStack, DateTimeOffset.UnixEpoch)) !=
        ColorStudioSchemeSerializer.ComputeHash(new ColorStudioSchemeV2(Guid.Empty, "session", _persistedStack, DateTimeOffset.UnixEpoch)));
    public bool SchemeSwitchOpen { get => _schemeSwitchOpen; set => SetProperty(ref _schemeSwitchOpen, value); }
    public bool SchemeDeleteOpen { get => _schemeDeleteOpen; set => SetProperty(ref _schemeDeleteOpen, value); }
    public RelayCommand RequestApplySchemeCommand { get; private set; } = null!;
    public RelayCommand DiscardAndApplySchemeCommand { get; private set; } = null!;
    public RelayCommand CancelSchemeSwitchCommand { get; private set; } = null!;
    public AsyncRelayCommand SaveAndApplySchemeCommand { get; private set; } = null!;
    public RelayCommand RequestDeleteSchemeCommand { get; private set; } = null!;
    public RelayCommand CancelDeleteSchemeCommand { get; private set; } = null!;
    public AsyncRelayCommand ConfirmDeleteSchemeCommand { get; private set; } = null!;
    public AsyncRelayCommand UpdateColorSchemeCommand { get; private set; } = null!;

    private void InitializeSchemeInteractions()
    {
        RequestApplySchemeCommand = new RelayCommand(_ =>
        {
            if (SelectedColorScheme is null) return;
            _pendingScheme = SelectedColorScheme;
            if (HasUnsavedSchemeChanges) SchemeSwitchOpen = true;
            else ApplyPendingScheme();
        });
        DiscardAndApplySchemeCommand = new RelayCommand(_ => ApplyPendingScheme());
        CancelSchemeSwitchCommand = new RelayCommand(_ => { SchemeSwitchOpen = false; _pendingScheme = null; });
        SaveAndApplySchemeCommand = new AsyncRelayCommand(async _ =>
        {
            var pending = _pendingScheme;
            // Selecting another card must not rename the currently applied scheme.
            if (_appliedColorScheme is not null) ColorSchemeName = _appliedColorScheme.Name;
            await SaveColorSchemeAsync(false);
            if (HasError || pending is null) return;
            _pendingScheme = pending; ApplyPendingScheme();
        });
        UpdateColorSchemeCommand = new AsyncRelayCommand(_ => SaveColorSchemeAsync(false), _ => _appliedColorScheme is not null);
        RequestDeleteSchemeCommand = new RelayCommand(_ =>
        {
            if (SelectedColorScheme is null) return;
            _pendingDeleteScheme = SelectedColorScheme; SchemeDeleteOpen = true;
        });
        CancelDeleteSchemeCommand = new RelayCommand(_ => { SchemeDeleteOpen = false; _pendingDeleteScheme = null; });
        ConfirmDeleteSchemeCommand = new AsyncRelayCommand(async _ =>
        {
            if (_pendingDeleteScheme is not { } scheme) return;
            SelectedColorScheme = scheme;
            await DeleteColorSchemeAsync();
            if (ColorSchemes.All(item => item.Id != scheme.Id))
            {
                if (_appliedColorScheme?.Id == scheme.Id) _appliedColorScheme = null;
                SchemeDeleteOpen = false; _pendingDeleteScheme = null; NotifySchemeState();
            }
        });
    }
    private void ApplyPendingScheme()
    {
        if (_pendingScheme is not { } scheme) return;
        SelectedColorScheme = scheme; ApplyColorScheme(); SchemeSwitchOpen = false; _pendingScheme = null;
    }
    private void NotifySchemeState()
    {
        OnPropertyChanged(nameof(CurrentColorSchemeName)); OnPropertyChanged(nameof(ColorSchemeName));
        OnPropertyChanged(nameof(HasSessionAdjustment)); OnPropertyChanged(nameof(SessionAdjustmentText));
        UpdateColorSchemeCommand?.RaiseCanExecuteChanged(); RestoreSchemeCommand?.RaiseCanExecuteChanged();
    }
}
