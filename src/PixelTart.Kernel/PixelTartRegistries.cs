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
    internal void RemoveByModule(string moduleId) => RemoveWhere(item => item.ModuleId.Equals(moduleId, StringComparison.OrdinalIgnoreCase));
    private void RemoveWhere(Func<CapabilityDescriptor, bool> predicate)
    {
        foreach (var key in _items.Where(pair => predicate(pair.Value)).Select(pair => pair.Key).ToArray()) _items.Remove(key);
    }
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
    internal void RemoveByModule(string moduleId)
    {
        foreach (var key in _items.Where(pair => pair.Value.ModuleId.Equals(moduleId, StringComparison.OrdinalIgnoreCase)).Select(pair => pair.Key).ToArray()) _items.Remove(key);
    }
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
    internal void RemoveByModule(string moduleId)
    {
        foreach (var key in _items.Where(pair => pair.Value.ModuleId.Equals(moduleId, StringComparison.OrdinalIgnoreCase)).Select(pair => pair.Key).ToArray()) _items.Remove(key);
    }
}

public sealed class PixelTartNavigationRegistry : INavigationRegistry
{
    private readonly Dictionary<string, ModuleRoute> _items = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<ModuleRoute> Items => _items.Values.OrderBy(x => x.NavigationOrder).ToArray();
    public void Register(ModuleRoute route)
    {
        if (!_items.TryAdd(route.Route, route)) throw new InvalidOperationException($"Duplicate navigation route: {route.Route}");
    }
    internal void RemoveByModule(string moduleId)
    {
        foreach (var key in _items.Where(pair => pair.Value.ModuleId.Equals(moduleId, StringComparison.OrdinalIgnoreCase)).Select(pair => pair.Key).ToArray()) _items.Remove(key);
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
    internal void RemoveByModule(string moduleId)
    {
        foreach (var key in _items.Where(pair => pair.Value.ModuleId.Equals(moduleId, StringComparison.OrdinalIgnoreCase)).Select(pair => pair.Key).ToArray()) _items.Remove(key);
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
    internal void RemoveByModule(string moduleId)
    {
        foreach (var key in _items.Where(pair => pair.Value.ModuleId.Equals(moduleId, StringComparison.OrdinalIgnoreCase)).Select(pair => pair.Key).ToArray()) _items.Remove(key);
    }
}
