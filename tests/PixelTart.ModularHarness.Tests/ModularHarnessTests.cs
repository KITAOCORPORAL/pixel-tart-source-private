using System.Windows;
using PixelTart.Kernel;
using PixelTart.Modules.AssetLibrary;
using PixelTart.Modules.OnlineSelection;
using PixelTart.Modules.RawTool;

namespace PixelTart.ModularHarness.Tests;

[TestClass]
public sealed class ModularHarnessTests
{
    [TestMethod]
    public async Task BuiltInModules_InitializeAndActivate()
    {
        var registry = CreateRegistry();
        await registry.InitializeAsync();
        await registry.ActivateAllAsync();
        Assert.HasCount(3, registry.Modules);
        Assert.IsTrue(registry.Diagnostics.All(item => item.State == ModuleLifecycleState.Active));
    }

    [TestMethod]
    public void Registries_RejectDuplicateCapabilityAndProvider()
    {
        var registry = new PixelTartModuleRegistry();
        registry.Capabilities.Register(new("test.capability", "test", "v1"));
        Assert.Throws<InvalidOperationException>(() => registry.Capabilities.Register(new("test.capability", "test", "v1")));
        registry.Providers.Register(new("test.provider", "test", "v1", new object()));
        Assert.Throws<InvalidOperationException>(() => registry.Providers.Register(new("test.provider", "test", "v1", new object())));
    }

    [TestMethod]
    public void AssetRoute_IsEmbeddedAndOnlineIsDescriptorOnly()
    {
        var registry = CreateRegistry();
        Assert.IsTrue(registry.Routes.TryGet("asset-library", out var assetRoute));
        RunSta(() => Assert.IsInstanceOfType<FrameworkElement>(assetRoute.ViewFactory()));
        Assert.IsTrue(registry.Navigation.Items.Any(item => item.Route == "asset-library"));
        Assert.IsFalse(registry.Navigation.Items.Any(item => item.Route == "online-selection"));
        Assert.IsTrue(registry.Routes.TryGet("online-selection", out var onlineRoute));
        Assert.IsFalse(onlineRoute.IsNavigationVisible);
    }

    [TestMethod]
    public void BuiltInManifests_ExposeExpectedModuleTypesAndCapabilities()
    {
        var registry = CreateRegistry();
        Assert.AreEqual(ModuleType.WorkspaceModule, registry.Modules.Single(item => item.Manifest.ModuleId == AssetLibraryModule.ModuleId).Manifest.ModuleType);
        Assert.AreEqual(ModuleType.ToolModule, registry.Modules.Single(item => item.Manifest.ModuleId == RawToolModule.ModuleId).Manifest.ModuleType);
        Assert.IsTrue(registry.Capabilities.Contains("asset.visual-analysis"));
        Assert.IsTrue(registry.Capabilities.Contains("raw.decode"));
        Assert.IsTrue(registry.Providers.TryGet("visual-analysis.local-pixel", out _));
    }

    [TestMethod]
    public void DuplicateModuleId_IsRejected()
    {
        var registry = new PixelTartModuleRegistry();
        var first = new TestModule("test.module");
        registry.Register(first);
        Assert.Throws<InvalidOperationException>(() => registry.Register(new TestModule("test.module")));
    }

    [TestMethod]
    public async Task InitializationFailure_IsolatedFromHealthyModule()
    {
        var registry = new PixelTartModuleRegistry();
        registry.Register(new TestModule("healthy.module"));
        registry.Register(new TestModule("failing.module", throwOnInitialize: true));
        await registry.InitializeAsync();
        Assert.AreEqual(ModuleLifecycleState.Initialized, registry.Diagnostics.Single(item => item.ModuleId == "healthy.module").State);
        Assert.AreEqual(ModuleLifecycleState.Failed, registry.Diagnostics.Single(item => item.ModuleId == "failing.module").State);
    }

    [TestMethod]
    public async Task CircularDependencies_AreRejected()
    {
        var registry = new PixelTartModuleRegistry();
        registry.Register(new TestModule("module.a", dependencies: ["module.b"]));
        registry.Register(new TestModule("module.b", dependencies: ["module.a"]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => registry.InitializeAsync());
    }

    [TestMethod]
    public void RegistrationFailure_RollsBackEveryRegistry()
    {
        var registry = new PixelTartModuleRegistry();
        Assert.Throws<InvalidOperationException>(() => registry.Register(new TestModule("broken.registration", throwOnRegistration: true)));
        Assert.IsFalse(registry.Modules.Any(module => module.Manifest.ModuleId == "broken.registration"));
        Assert.IsFalse(registry.Capabilities.Contains("broken.registration.capability"));
        Assert.IsFalse(registry.Routes.Items.Any(route => route.ModuleId == "broken.registration"));
    }

    [TestMethod]
    public async Task DeactivationFailure_DoesNotPreventOtherModulesFromDeactivating()
    {
        var registry = new PixelTartModuleRegistry();
        registry.Register(new TestModule("healthy.deactivation"));
        registry.Register(new TestModule("broken.deactivation", throwOnDeactivate: true));
        await registry.InitializeAsync();
        await registry.ActivateAllAsync();
        await registry.DeactivateAllAsync();
        Assert.AreEqual(ModuleLifecycleState.Deactivated, registry.Diagnostics.Single(item => item.ModuleId == "healthy.deactivation").State);
        Assert.AreEqual(ModuleLifecycleState.Failed, registry.Diagnostics.Single(item => item.ModuleId == "broken.deactivation").State);
    }

    private static PixelTartModuleRegistry CreateRegistry()
    {
        var registry = new PixelTartModuleRegistry();
        foreach (var capability in new[] { "core.navigation", "core.task-center", "core.settings", "core.file-safety" })
            registry.Capabilities.Register(new(capability, "pixel-tart.kernel", "kernel/v1"));
        registry.Register(new AssetLibraryModule());
        registry.Register(new RawToolModule());
        registry.Register(new OnlineSelectionModule());
        return registry;
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); } catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;
    }

    private sealed class TestModule : PixelTartModuleBase
    {
        private readonly bool _throwOnInitialize;
        private readonly bool _throwOnRegistration;
        private readonly bool _throwOnDeactivate;
        public TestModule(string id, bool throwOnInitialize = false, IReadOnlyList<string>? dependencies = null, bool throwOnRegistration = false, bool throwOnDeactivate = false)
            : base(new PixelTartModuleManifest(id, id, "1.0", ModuleType.ServiceModule, null, null, 0, [], [], [], dependencies ?? []))
        {
            _throwOnInitialize = throwOnInitialize;
            _throwOnRegistration = throwOnRegistration;
            _throwOnDeactivate = throwOnDeactivate;
        }

        public override void RegisterCapabilities(ModuleRegistrationContext context)
        {
            context.Capabilities.Register(new($"{Manifest.ModuleId}.capability", Manifest.ModuleId, "test/v1"));
            if (_throwOnRegistration) throw new InvalidOperationException("registration failure");
        }

        public override Task InitializeAsync(CancellationToken cancellationToken = default) =>
            _throwOnInitialize ? throw new InvalidOperationException("test failure") : Task.CompletedTask;

        public override Task DeactivateAsync(CancellationToken cancellationToken = default) =>
            _throwOnDeactivate ? throw new InvalidOperationException("deactivation failure") : Task.CompletedTask;
    }
}
