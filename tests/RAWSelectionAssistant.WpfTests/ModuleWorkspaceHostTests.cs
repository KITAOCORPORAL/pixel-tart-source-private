using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using PixelTart.Kernel;
using RAWSelectionAssistant.Views;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
[DoNotParallelize]
public sealed class ModuleWorkspaceHostTests
{
    [TestMethod]
    public Task RouteViews_AreCreatedOnceAndReusedPerRegistryAndRoute() => RunSta(() =>
    {
        var firstFactoryCalls = 0;
        var secondFactoryCalls = 0;
        var firstView = new Border();
        var secondView = new Border();
        var registry = Registry(
            Route("first", () => { firstFactoryCalls++; return firstView; }),
            Route("second", () => { secondFactoryCalls++; return secondView; }));
        var host = new ModuleWorkspaceHost { ModuleRegistry = registry, Route = "first" };

        Assert.AreSame(firstView, host.Content);
        host.Route = "second";
        Assert.AreSame(secondView, host.Content);
        host.Route = "FIRST";
        Assert.AreSame(firstView, host.Content);
        Assert.AreEqual(1, firstFactoryCalls);
        Assert.AreEqual(1, secondFactoryCalls);

        var replacementCalls = 0;
        var replacementView = new Border();
        host.ModuleRegistry = Registry(Route("first", () => { replacementCalls++; return replacementView; }));

        Assert.AreSame(replacementView, host.Content);
        Assert.AreEqual(1, replacementCalls);
    });

    [TestMethod]
    public Task MissingRoute_ShowsSafeInlineStateAndCanRetryAfterRegistration() => RunSta(() =>
    {
        var registry = Registry();
        var host = new ModuleWorkspaceHost { ModuleRegistry = registry, Route = "late-route" };

        AssertInlineState(host, "ModuleWorkspaceMissingState");
        var view = new Border();
        registry.Routes.Register(Route("late-route", () => view));

        RetryButton(host).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.AreSame(view, host.Content);
    });

    [TestMethod]
    public Task FactoryFailure_ShowsSharedErrorStateAndRetryCanRecover() => RunSta(() =>
    {
        var attempts = 0;
        var recoveredView = new Border();
        var registry = Registry(Route("unstable", () =>
        {
            attempts++;
            if (attempts == 1) throw new InvalidOperationException("Synthetic route failure.");
            return recoveredView;
        }));

        var host = new ModuleWorkspaceHost { ModuleRegistry = registry };
        var sharedInlineState = host.Content;
        host.Route = "unstable";

        Assert.AreEqual(1, attempts);
        AssertInlineState(host, "ModuleWorkspaceErrorState");
        Assert.AreSame(sharedInlineState, host.Content, "Missing and factory-error states should share one inline surface.");
        RetryButton(host).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.AreSame(recoveredView, host.Content);
        Assert.AreEqual(2, attempts);

        host.RetryCurrentRoute();
        Assert.AreSame(recoveredView, host.Content);
        Assert.AreEqual(2, attempts, "A successful route must stay cached after recovery.");
    });

    [TestMethod]
    public Task VisibleHost_RequestsInitialKeyboardFocusInsideTheRouteView() => RunSta(() =>
    {
        var firstControl = new Button { Content = "First control" };
        var routeView = new StackPanel { Children = { firstControl, new Button { Content = "Second control" } } };
        var host = new ModuleWorkspaceHost
        {
            ModuleRegistry = Registry(Route("focus", () => routeView)),
            Route = "focus"
        };
        var window = new Window
        {
            Width = 320,
            Height = 180,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.ToolWindow,
            Content = host
        };

        try
        {
            window.Show();
            window.Activate();
            DrainDispatcher();
            host.RequestInitialFocus();
            DrainDispatcher();

            Assert.IsTrue(firstControl.IsKeyboardFocused, "The first focusable control in a shown route should receive initial focus.");
        }
        finally
        {
            window.Close();
        }
    });

    private static PixelTartModuleRegistry Registry(params ModuleRoute[] routes)
    {
        var registry = new PixelTartModuleRegistry();
        foreach (var route in routes) registry.Routes.Register(route);
        return registry;
    }

    private static ModuleRoute Route(string route, Func<object> factory) =>
        new(route, route, "workspace", 0, "test.module", factory);

    private static void AssertInlineState(ModuleWorkspaceHost host, string expectedAutomationId)
    {
        Assert.IsInstanceOfType<Border>(host.Content);
        Assert.AreEqual(expectedAutomationId, AutomationProperties.GetAutomationId((Border)host.Content));
        Assert.AreEqual("RetryModuleWorkspace", AutomationProperties.GetAutomationId(RetryButton(host)));
    }

    private static Button RetryButton(ModuleWorkspaceHost host) =>
        FindDescendant<Button>((DependencyObject)host.Content) ?? throw new AssertFailedException("Retry button was not found.");

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) return match;
            if (FindDescendant<T>(child) is { } descendant) return descendant;
        }

        return null;
    }

    private static void DrainDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static Task RunSta(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                action();
                completion.SetResult();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}
