using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using PixelTart.Kernel;
using PixelTart.Modules.AssetLibrary;
using PixelTart.Modules.OnlineSelection;
using PixelTart.Modules.RawTool;

namespace PixelTart.ModularHarness.Tests;

[TestClass]
public sealed class ModularHarnessAcceptanceContractTests
{
    private static readonly string[] CoreCapabilities =
    [
        "core.navigation",
        "core.task-center",
        "core.settings",
        "core.file-safety"
    ];

    private static readonly string[] AssetCapabilities =
    [
        "asset.query",
        "asset.pick",
        "asset.import",
        "asset.folder",
        "asset.tag",
        "asset.smart-folder",
        "asset.visual-analysis",
        "asset.visual-search"
    ];

    [TestMethod]
    public void BuiltInDescriptorMatrix_IsCompleteAndExact()
    {
        var registry = CreateRegistry();

        Assert.HasCount(3, registry.Modules);

        var asset = registry.Modules.Single(module => module.Manifest.ModuleId == AssetLibraryModule.ModuleId).Manifest;
        Assert.AreEqual("素材库", asset.DisplayName);
        Assert.AreEqual("1.6.0-dev", asset.Version);
        Assert.AreEqual(ModuleType.WorkspaceModule, asset.ModuleType);
        Assert.AreEqual("asset-library", asset.Route);
        Assert.AreEqual("primary", asset.NavigationGroup);
        Assert.AreEqual(20, asset.NavigationOrder);
        CollectionAssert.AreEquivalent(AssetCapabilities, asset.Provides.ToArray());
        CollectionAssert.AreEquivalent(CoreCapabilities, asset.Requires.ToArray());
        CollectionAssert.AreEquivalent(new[] { "selection.create-from-assets" }, asset.Optional.ToArray());

        var raw = registry.Modules.Single(module => module.Manifest.ModuleId == RawToolModule.ModuleId).Manifest;
        Assert.AreEqual(ModuleType.ToolModule, raw.ModuleType);
        Assert.IsNull(raw.Route);
        CollectionAssert.AreEquivalent(new[] { "raw.decode" }, raw.Provides.ToArray());
        CollectionAssert.AreEquivalent(new[] { "core.navigation", "core.task-center", "core.file-safety" }, raw.Requires.ToArray());

        var online = registry.Modules.Single(module => module.Manifest.ModuleId == OnlineSelectionModule.ModuleId).Manifest;
        Assert.AreEqual(ModuleType.WorkspaceModule, online.ModuleType);
        Assert.AreEqual("online-selection", online.Route);
        CollectionAssert.AreEquivalent(new[] { "selection.create-from-assets" }, online.Provides.ToArray());
        CollectionAssert.AreEquivalent(new[] { "core.navigation", "core.task-center" }, online.Requires.ToArray());
        CollectionAssert.AreEquivalent(new[] { "asset.pick" }, online.Optional.ToArray());

        Assert.HasCount(14, registry.Capabilities.Items);
        Assert.IsTrue(registry.Capabilities.Contains("selection.create-from-assets"));
        Assert.HasCount(1, registry.Capabilities.Items.Where(capability =>
            capability.Name == "selection.create-from-assets" &&
            capability.ModuleId == OnlineSelectionModule.ModuleId));
    }

    [TestMethod]
    public async Task ActivatedDiagnostics_ReportExactRoutesCapabilitiesAndNoFailure()
    {
        var registry = CreateRegistry();
        await registry.InitializeAsync();
        await registry.ActivateAllAsync();

        var diagnostics = registry.Diagnostics.ToDictionary(item => item.ModuleId, StringComparer.OrdinalIgnoreCase);
        Assert.HasCount(3, diagnostics);

        AssertDiagnostic(diagnostics[AssetLibraryModule.ModuleId], ["asset-library"], AssetCapabilities);
        AssertDiagnostic(diagnostics[RawToolModule.ModuleId], [], ["raw.decode"]);
        AssertDiagnostic(diagnostics[OnlineSelectionModule.ModuleId], ["online-selection"], ["selection.create-from-assets"]);
    }

    [TestMethod]
    public void SharedRegistries_ExposeOneEmbeddedAssetRouteAndOneLocalVisualProvider()
    {
        var registry = CreateRegistry();

        Assert.HasCount(1, registry.Providers.Items);
        Assert.HasCount(2, registry.Routes.Items);
        Assert.HasCount(1, registry.Navigation.Items);
        Assert.HasCount(5, registry.Tasks.Items);
        Assert.HasCount(4, registry.Settings.Items);
        Assert.HasCount(1, registry.Routes.Items.Where(route => route.Route == AssetLibraryModule.Route));
        Assert.HasCount(1, registry.Navigation.Items.Where(route => route.Route == AssetLibraryModule.Route));
        Assert.HasCount(1, registry.Providers.Items.Where(provider => provider.Name == "visual-analysis.local-pixel"));
        Assert.HasCount(1, registry.Tasks.Items.Where(task => task.TaskType == "asset.visual-analysis"));

        Assert.IsTrue(registry.Routes.TryGet(AssetLibraryModule.Route, out var route));
        var view = RunSta(route.ViewFactory);
        Assert.IsInstanceOfType<UserControl>(view);
        Assert.IsNotInstanceOfType<Window>(view);
        Assert.AreEqual(AssetLibraryModule.ModuleId, route.ModuleId);
        Assert.AreEqual("primary", route.NavigationGroup);
        Assert.AreEqual(20, route.NavigationOrder);
        Assert.IsTrue(route.IsNavigationVisible);
    }

    [TestMethod]
    public async Task FullLifecycle_ShutsDownEveryBuiltInModuleWithoutFailure()
    {
        var registry = CreateRegistry();

        await registry.InitializeAsync();
        await registry.ActivateAllAsync();
        await registry.DeactivateAllAsync();
        await registry.ShutdownAsync();

        Assert.IsTrue(registry.Diagnostics.All(item => item.State == ModuleLifecycleState.Shutdown));
        Assert.IsTrue(registry.Diagnostics.All(item => item.Failure is null));
    }

    [TestMethod]
    public void AssetRouteFactory_DoesNotCreateAChildProcess()
    {
        if (!OperatingSystem.IsWindows()) Assert.Inconclusive("The exact process-tree snapshot uses the Windows ToolHelp API.");

        var registry = CreateRegistry();
        Assert.IsTrue(registry.Routes.TryGet(AssetLibraryModule.Route, out var route));
        var before = ProcessTreeSnapshot.DescendantProcessIds(Environment.ProcessId);
        _ = RunSta(route.ViewFactory);
        var after = ProcessTreeSnapshot.DescendantProcessIds(Environment.ProcessId);

        CollectionAssert.AreEquivalent(before, after, "Creating the embedded route must not start a child process.");
    }

    private static void AssertDiagnostic(
        ModuleDiagnosticsSnapshot snapshot,
        IReadOnlyList<string> expectedRoutes,
        IReadOnlyList<string> expectedCapabilities)
    {
        Assert.AreEqual(ModuleLifecycleState.Active, snapshot.State);
        Assert.IsNull(snapshot.Failure);
        CollectionAssert.AreEquivalent(expectedRoutes.ToArray(), snapshot.Routes.ToArray());
        CollectionAssert.AreEquivalent(expectedCapabilities.ToArray(), snapshot.Capabilities.ToArray());
        Assert.IsEmpty(snapshot.Dependencies);
    }

    private static PixelTartModuleRegistry CreateRegistry()
    {
        var registry = new PixelTartModuleRegistry();
        foreach (var capability in CoreCapabilities)
            registry.Capabilities.Register(new(capability, "pixel-tart.kernel", "kernel/v1"));
        registry.Register(new AssetLibraryModule());
        registry.Register(new RawToolModule());
        registry.Register(new OnlineSelectionModule());
        return registry;
    }

    private static object RunSta(Func<object> factory)
    {
        object? result = null;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { result = factory(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;
        return result ?? throw new InvalidOperationException("The asset route returned no view.");
    }

    private static class ProcessTreeSnapshot
    {
        private const uint Th32csSnapProcess = 0x00000002;
        private static readonly nint InvalidHandleValue = new(-1);

        public static int[] DescendantProcessIds(int rootProcessId)
        {
            var parentByProcess = EnumerateProcesses();
            var descendants = new List<int>();
            var pending = new Queue<int>();
            pending.Enqueue(rootProcessId);
            while (pending.Count > 0)
            {
                var parent = pending.Dequeue();
                foreach (var child in parentByProcess.Where(item => item.Value == parent).Select(item => item.Key))
                {
                    if (descendants.Contains(child)) continue;
                    descendants.Add(child);
                    pending.Enqueue(child);
                }
            }
            return descendants.OrderBy(processId => processId).ToArray();
        }

        private static IReadOnlyDictionary<int, int> EnumerateProcesses()
        {
            var snapshot = CreateToolhelp32Snapshot(Th32csSnapProcess, 0);
            if (snapshot == InvalidHandleValue) throw new InvalidOperationException("Unable to create a process snapshot.");
            try
            {
                var result = new Dictionary<int, int>();
                var entry = new ProcessEntry32 { Size = (uint)Marshal.SizeOf<ProcessEntry32>() };
                if (!Process32First(snapshot, ref entry)) return result;
                do
                {
                    result[(int)entry.ProcessId] = (int)entry.ParentProcessId;
                    entry.Size = (uint)Marshal.SizeOf<ProcessEntry32>();
                }
                while (Process32Next(snapshot, ref entry));
                return result;
            }
            finally
            {
                _ = CloseHandle(snapshot);
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ProcessEntry32
        {
            public uint Size;
            public uint Usage;
            public uint ProcessId;
            public nint DefaultHeapId;
            public uint ModuleId;
            public uint Threads;
            public uint ParentProcessId;
            public int PriorityClassBase;
            public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string ExeFile;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern nint CreateToolhelp32Snapshot(uint flags, uint processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32First(nint snapshot, ref ProcessEntry32 entry);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32Next(nint snapshot, ref ProcessEntry32 entry);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(nint handle);
    }
}
