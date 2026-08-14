namespace PixelTart.Kernel;

public sealed class PixelTartModuleRegistry : IModuleRegistry
{
    private readonly Dictionary<string, IPixelTartModule> _modules = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ModuleLifecycleState> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string?> _failures = new(StringComparer.OrdinalIgnoreCase);
    public PixelTartModuleRegistry()
    {
        Capabilities = new PixelTartCapabilityRegistry();
        Providers = new PixelTartProviderRegistry();
        Routes = new PixelTartRouteRegistry();
        Navigation = new PixelTartNavigationRegistry();
        Settings = new PixelTartSettingsRegistry();
        Tasks = new PixelTartTaskRegistry();
        Context = new ModuleRegistrationContext(Capabilities, Providers, Routes, Navigation, Settings, Tasks);
    }

    public PixelTartCapabilityRegistry Capabilities { get; }
    public PixelTartProviderRegistry Providers { get; }
    public PixelTartRouteRegistry Routes { get; }
    public PixelTartNavigationRegistry Navigation { get; }
    public PixelTartSettingsRegistry Settings { get; }
    public PixelTartTaskRegistry Tasks { get; }
    public ModuleRegistrationContext Context { get; }
    IRouteRegistry IModuleRegistry.Routes => Routes;
    INavigationRegistry IModuleRegistry.Navigation => Navigation;
    public IReadOnlyCollection<IPixelTartModule> Modules => _modules.Values.ToArray();
    public IReadOnlyCollection<ModuleDiagnosticsSnapshot> Diagnostics => _modules.Values.Select(module =>
        new ModuleDiagnosticsSnapshot(module.Manifest.ModuleId, _states.GetValueOrDefault(module.Manifest.ModuleId, ModuleLifecycleState.Registered), _failures.GetValueOrDefault(module.Manifest.ModuleId),
            Routes.Items.Where(route => route.ModuleId.Equals(module.Manifest.ModuleId, StringComparison.OrdinalIgnoreCase)).Select(route => route.Route).ToArray(),
            module.Manifest.Provides, module.Manifest.Dependencies)).ToArray();

    public void Register(IPixelTartModule module)
    {
        if (!_modules.TryAdd(module.Manifest.ModuleId, module)) throw new InvalidOperationException($"Duplicate module id: {module.Manifest.ModuleId}");
        _states[module.Manifest.ModuleId] = ModuleLifecycleState.Registered;
        module.RegisterServices(Context);
        module.RegisterCapabilities(Context);
        module.RegisterProviders(Context);
        module.RegisterRoutes(Context);
        module.RegisterNavigation(Context);
        module.RegisterTasks(Context);
        module.RegisterSettings(Context);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        foreach (var module in TopologicalOrder())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var missing = module.Manifest.Requires.Where(required => !Capabilities.Contains(required) && !_modules.Values.Any(candidate => candidate.Manifest.Provides.Contains(required, StringComparer.OrdinalIgnoreCase))).ToArray();
            if (missing.Length > 0)
            {
                SetFailure(module, $"Missing required capabilities: {string.Join(", ", missing)}");
                continue;
            }
            try
            {
                await module.InitializeAsync(cancellationToken).ConfigureAwait(false);
                _states[module.Manifest.ModuleId] = ModuleLifecycleState.Initialized;
            }
            catch (Exception ex)
            {
                SetFailure(module, ex.Message);
            }
        }
    }

    public async Task ActivateAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var module in TopologicalOrder())
        {
            if (_states[module.Manifest.ModuleId] != ModuleLifecycleState.Initialized) continue;
            try { await module.ActivateAsync(cancellationToken).ConfigureAwait(false); _states[module.Manifest.ModuleId] = ModuleLifecycleState.Active; }
            catch (Exception ex) { SetFailure(module, ex.Message); }
        }
    }

    public async Task DeactivateAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var module in TopologicalOrder().Reverse())
        {
            if (_states[module.Manifest.ModuleId] != ModuleLifecycleState.Active) continue;
            await module.DeactivateAsync(cancellationToken).ConfigureAwait(false);
            _states[module.Manifest.ModuleId] = ModuleLifecycleState.Deactivated;
        }
    }

    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        foreach (var module in TopologicalOrder().Reverse())
        {
            if (_states[module.Manifest.ModuleId] is ModuleLifecycleState.Shutdown or ModuleLifecycleState.Registered) continue;
            try { await module.ShutdownAsync(cancellationToken).ConfigureAwait(false); _states[module.Manifest.ModuleId] = ModuleLifecycleState.Shutdown; }
            catch (Exception ex) { SetFailure(module, ex.Message); }
        }
    }

    private void SetFailure(IPixelTartModule module, string message)
    {
        _states[module.Manifest.ModuleId] = ModuleLifecycleState.Failed;
        _failures[module.Manifest.ModuleId] = message;
    }

    private IReadOnlyList<IPixelTartModule> TopologicalOrder()
    {
        var ordered = new List<IPixelTartModule>();
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Visit(IPixelTartModule module)
        {
            if (visited.Contains(module.Manifest.ModuleId)) return;
            if (!visiting.Add(module.Manifest.ModuleId)) throw new InvalidOperationException($"Module dependency cycle at {module.Manifest.ModuleId}");
            foreach (var dependency in module.Manifest.Dependencies)
            {
                if (!_modules.TryGetValue(dependency, out var dependencyModule)) throw new InvalidOperationException($"Missing module dependency: {module.Manifest.ModuleId} -> {dependency}");
                Visit(dependencyModule);
            }
            visiting.Remove(module.Manifest.ModuleId);
            visited.Add(module.Manifest.ModuleId);
            ordered.Add(module);
        }
        foreach (var module in _modules.Values) Visit(module);
        return ordered;
    }
}
