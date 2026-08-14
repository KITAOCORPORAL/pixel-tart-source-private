namespace PixelTart.Kernel;

public enum ModuleType
{
    WorkspaceModule,
    ToolModule,
    ServiceModule
}

public enum ModuleLifecycleState
{
    Registered,
    Initialized,
    Active,
    Deactivated,
    Failed,
    Shutdown
}

public sealed record PixelTartModuleManifest(
    string ModuleId,
    string DisplayName,
    string Version,
    ModuleType ModuleType,
    string? Route,
    string? NavigationGroup,
    int NavigationOrder,
    IReadOnlyList<string> Provides,
    IReadOnlyList<string> Requires,
    IReadOnlyList<string> Optional,
    IReadOnlyList<string> Dependencies);

public sealed record ModuleRoute(
    string Route,
    string DisplayName,
    string NavigationGroup,
    int NavigationOrder,
    string ModuleId,
    Func<object> ViewFactory,
    bool IsNavigationVisible = true);

public sealed record CapabilityDescriptor(string Name, string ModuleId, string ContractVersion);
public sealed record ProviderDescriptor(string Name, string ModuleId, string ContractVersion, object Provider);
public sealed record SettingsDescriptor(string Key, string ModuleId, string DisplayName);
public sealed record TaskDescriptor(string TaskType, string ModuleId, string DisplayName);

public interface ICapabilityRegistry
{
    IReadOnlyCollection<CapabilityDescriptor> Items { get; }
    void Register(CapabilityDescriptor descriptor);
    bool Contains(string capability);
}

public interface IProviderRegistry
{
    IReadOnlyCollection<ProviderDescriptor> Items { get; }
    void Register(ProviderDescriptor descriptor);
    bool TryGet(string name, out ProviderDescriptor descriptor);
}

public interface IRouteRegistry
{
    IReadOnlyCollection<ModuleRoute> Items { get; }
    void Register(ModuleRoute route);
    bool TryGet(string route, out ModuleRoute descriptor);
}

public interface INavigationRegistry
{
    IReadOnlyCollection<ModuleRoute> Items { get; }
    void Register(ModuleRoute route);
}

public interface ISettingsRegistry
{
    IReadOnlyCollection<SettingsDescriptor> Items { get; }
    void Register(SettingsDescriptor descriptor);
}

public interface ITaskRegistry
{
    IReadOnlyCollection<TaskDescriptor> Items { get; }
    void Register(TaskDescriptor descriptor);
}

public sealed class ModuleRegistrationContext
{
    public ModuleRegistrationContext(
        ICapabilityRegistry capabilities,
        IProviderRegistry providers,
        IRouteRegistry routes,
        INavigationRegistry navigation,
        ISettingsRegistry settings,
        ITaskRegistry tasks)
    {
        Capabilities = capabilities;
        Providers = providers;
        Routes = routes;
        Navigation = navigation;
        Settings = settings;
        Tasks = tasks;
    }

    public ICapabilityRegistry Capabilities { get; }
    public IProviderRegistry Providers { get; }
    public IRouteRegistry Routes { get; }
    public INavigationRegistry Navigation { get; }
    public ISettingsRegistry Settings { get; }
    public ITaskRegistry Tasks { get; }
}

public interface IPixelTartModule
{
    PixelTartModuleManifest Manifest { get; }
    void RegisterServices(ModuleRegistrationContext context);
    void RegisterNavigation(ModuleRegistrationContext context);
    void RegisterRoutes(ModuleRegistrationContext context);
    void RegisterCapabilities(ModuleRegistrationContext context);
    void RegisterProviders(ModuleRegistrationContext context);
    void RegisterTasks(ModuleRegistrationContext context);
    void RegisterSettings(ModuleRegistrationContext context);
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task ActivateAsync(CancellationToken cancellationToken = default);
    Task DeactivateAsync(CancellationToken cancellationToken = default);
    Task ShutdownAsync(CancellationToken cancellationToken = default);
}

public abstract class PixelTartModuleBase : IPixelTartModule
{
    protected PixelTartModuleBase(PixelTartModuleManifest manifest) => Manifest = manifest;
    public PixelTartModuleManifest Manifest { get; }
    public virtual void RegisterServices(ModuleRegistrationContext context) { }
    public virtual void RegisterNavigation(ModuleRegistrationContext context) { }
    public virtual void RegisterRoutes(ModuleRegistrationContext context) { }
    public virtual void RegisterCapabilities(ModuleRegistrationContext context) { }
    public virtual void RegisterProviders(ModuleRegistrationContext context) { }
    public virtual void RegisterTasks(ModuleRegistrationContext context) { }
    public virtual void RegisterSettings(ModuleRegistrationContext context) { }
    public virtual Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public virtual Task ActivateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public virtual Task DeactivateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public virtual Task ShutdownAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed record ModuleDiagnosticsSnapshot(
    string ModuleId,
    ModuleLifecycleState State,
    string? Failure,
    IReadOnlyList<string> Routes,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> Dependencies);

public interface IModuleRegistry
{
    IReadOnlyCollection<IPixelTartModule> Modules { get; }
    IReadOnlyCollection<ModuleDiagnosticsSnapshot> Diagnostics { get; }
    IRouteRegistry Routes { get; }
    INavigationRegistry Navigation { get; }
    void Register(IPixelTartModule module);
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task ActivateAllAsync(CancellationToken cancellationToken = default);
    Task DeactivateAllAsync(CancellationToken cancellationToken = default);
    Task ShutdownAsync(CancellationToken cancellationToken = default);
}
