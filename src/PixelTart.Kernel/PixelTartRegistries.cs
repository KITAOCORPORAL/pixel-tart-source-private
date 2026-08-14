namespace PixelTart.Kernel;

public sealed class PixelTartCapabilityRegistry : ICapabilityRegistry
{
    private readonly Dictionary<string, CapabilityDescriptor> _items = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<CapabilityDescriptor> Items => _items.Values.ToArray();
    public void Register(CapabilityDescriptor descriptor)
    {
        if (!_items.TryAdd(descriptor.Name, descriptor)) throw new InvalidOperationException($"Duplicate capability: {descriptor.Name}");
    }
    public bool Contains(string capability) => _items.ContainsKey(capability);
}

public sealed class PixelTartProviderRegistry : IProviderRegistry
{
    private readonly Dictionary<string, ProviderDescriptor> _items = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<ProviderDescriptor> Items => _items.Values.ToArray();
    public void Register(ProviderDescriptor descriptor)
    {
        if (!_items.TryAdd(descriptor.Name, descriptor)) throw new InvalidOperationException($"Duplicate provider: {descriptor.Name}");
    }
    public bool TryGet(string name, out ProviderDescriptor descriptor) => _items.TryGetValue(name, out descriptor!);
}

public sealed class PixelTartRouteRegistry : IRouteRegistry, INavigationRegistry
{
    private readonly Dictionary<string, ModuleRoute> _items = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<ModuleRoute> Items => _items.Values.OrderBy(x => x.NavigationOrder).ToArray();
    public void Register(ModuleRoute route)
    {
        if (!_items.TryAdd(route.Route, route)) throw new InvalidOperationException($"Duplicate route: {route.Route}");
    }
    public bool TryGet(string route, out ModuleRoute descriptor) => _items.TryGetValue(route, out descriptor!);
}

public sealed class PixelTartNavigationRegistry : INavigationRegistry
{
    private readonly Dictionary<string, ModuleRoute> _items = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<ModuleRoute> Items => _items.Values.OrderBy(x => x.NavigationOrder).ToArray();
    public void Register(ModuleRoute route)
    {
        if (!_items.TryAdd(route.Route, route)) throw new InvalidOperationException($"Duplicate navigation route: {route.Route}");
    }
}

public sealed class PixelTartSettingsRegistry : ISettingsRegistry
{
    private readonly Dictionary<string, SettingsDescriptor> _items = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<SettingsDescriptor> Items => _items.Values.ToArray();
    public void Register(SettingsDescriptor descriptor)
    {
        if (!_items.TryAdd(descriptor.Key, descriptor)) throw new InvalidOperationException($"Duplicate setting: {descriptor.Key}");
    }
}

public sealed class PixelTartTaskRegistry : ITaskRegistry
{
    private readonly Dictionary<string, TaskDescriptor> _items = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<TaskDescriptor> Items => _items.Values.ToArray();
    public void Register(TaskDescriptor descriptor)
    {
        if (!_items.TryAdd(descriptor.TaskType, descriptor)) throw new InvalidOperationException($"Duplicate task type: {descriptor.TaskType}");
    }
}
