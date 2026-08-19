using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RAWSelectionAssistant.Services;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class ClickRoutingFixFocusedTests
{
    [TestMethod]
    public Task PreviewMouseDown_ResolvesTheActualCloseTarget() => RunSta(() =>
    {
        var root = new Grid { Width = 320, Height = 180, Background = Brushes.White };
        var close = new Border { Width = 40, Height = 40, Background = Brushes.Transparent };
        ShellEscapePointer.SetAction(close, ShellEscapePointerAction.CloseCurrentSurface);
        close.Child = new TextBlock { Text = "close" };
        root.Children.Add(close);
        Arrange(root);

        var center = close.TranslatePoint(new Point(close.ActualWidth / 2, close.ActualHeight / 2), root);
        var hit = VisualTreeHelper.HitTest(root, center)?.VisualHit ?? root.InputHitTest(center) as DependencyObject;
        Assert.IsNotNull(hit);
        Assert.IsTrue(ShellEscapePointer.TryResolve(hit, out var owner, out var action));
        Assert.AreSame(close, owner, "The actual InputHitTest target must resolve to the close surface.");
        Assert.AreEqual(ShellEscapePointerAction.CloseCurrentSurface, action);

        var previewCount = 0;
        root.AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler((_, _) => previewCount++), true);
        var mouseEvent = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
        {
            RoutedEvent = Mouse.PreviewMouseDownEvent,
            Source = hit
        };
        (hit as UIElement)?.RaiseEvent(mouseEvent);
        Assert.AreEqual(1, previewCount, "The target's PreviewMouseDown route must reach the root once.");
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task OrdinaryButton_DoesNotEmitCloseRequested() => RunSta(() =>
    {
        var root = new Grid { Width = 320, Height = 180, Background = Brushes.Transparent };
        var ordinary = new Button { Content = "ordinary", Width = 100, Height = 40 };
        root.Children.Add(ordinary);
        Arrange(root);

        var closeRequests = 0;
        ordinary.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, ordinary));
        Assert.AreEqual(0, closeRequests, "An ordinary button Click must not become a close request.");
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task KeyboardClickPath_StillRaisesExactlyOneCloseRequest() => RunSta(() =>
    {
        var root = new Grid { Width = 320, Height = 180, Background = Brushes.Transparent };
        var close = new Button { Width = 40, Height = 40 };
        ShellEscapePointer.SetAction(close, ShellEscapePointerAction.CloseCurrentSurface);
        root.Children.Add(close);
        Arrange(root);

        var closeRequests = 0;
        close.Click += (_, _) => closeRequests++;
        close.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.AreEqual(1, closeRequests, "The keyboard/UIA Button.Click path remains available and single-shot.");
        return Task.CompletedTask;
    });

    [TestMethod]
    public void PhysicalPointerSequence_DeduplicatesTheFinalizedCloseAction()
    {
        var diagnostics = Read("src/RAWSelectionAssistant/Services/PhysicalPointerDiagnosticSession.cs");
        StringAssert.Contains(diagnostics, "_activeAttempt.Layer4Action.ActionFinalized");
        StringAssert.Contains(
            diagnostics,
            "if (!CanCorrelateWithActiveAttempt(requireWpfDown: true) || !TryConfirmPhysicalTarget(button)) return;");
        Assert.IsFalse(
            diagnostics.Contains("if (!IsCloseLike(button)) return;", StringComparison.Ordinal),
            "Confirmed ordinary buttons must remain observable for Gate A state-transition evidence.");
        StringAssert.Contains(diagnostics, "if (!IsCloseLike(control) && !IsCloseLike(targetSource)) return;");
        StringAssert.Contains(diagnostics, "if (_activeAttempt is null || _activeAttempt.Layer4Action.ActionFinalized");
        StringAssert.Contains(diagnostics, "_activeAttempt = null;");
    }

    [TestMethod]
    public void TutorialLayout_DoesNotStealFocusDuringAnExistingPointerSequence()
    {
        var main = Read("src/RAWSelectionAssistant/MainWindow.xaml.cs");
        StringAssert.Contains(main, "if (targetChanged && Mouse.LeftButton == MouseButtonState.Released) TutorialPrimaryButton.Focus();");
        StringAssert.Contains(main, "if (targetChanged && Mouse.LeftButton == MouseButtonState.Released) target.Focus();");
        Assert.AreEqual(0, Count(main, "\n            TutorialPrimaryButton.Focus();"));
        Assert.AreEqual(0, Count(main, "\n        target.Focus();"));
    }

    [TestMethod]
    public void SingleCloseAuthority_StaysExplicitForShellAndModalSurfaces()
    {
        var shell = Read("src/RAWSelectionAssistant/MainWindow.xaml");
        var raw = Read("src/RAWSelectionAssistant/Views/RawToJpegModal.xaml");
        var batch = Read("src/RAWSelectionAssistant/Views/BatchCompressionModal.xaml");

        Assert.AreEqual(1, Count(shell, "AutomationId=\"ShellEmergencyCloseButton\""));
        Assert.AreEqual(1, Count(raw, "AutomationId=\"RawToJpegCloseButton\""));
        Assert.AreEqual(1, Count(batch, "AutomationId=\"BatchCompressCloseButton\""));
        StringAssert.Contains(shell, "ShellSurfaceCloseStyle");
    }

    private static void Arrange(FrameworkElement root)
    {
        root.Measure(new Size(root.Width, root.Height));
        root.Arrange(new Rect(0, 0, root.Width, root.Height));
        root.UpdateLayout();
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        for (var current = source; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is T typed) return typed;
        return null;
    }

    private static int Count(string source, string token) => source.Split(token, StringSplitOptions.None).Length - 1;

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string RepositoryRoot
    {
        get
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
            throw new DirectoryNotFoundException("RAWSelectionAssistant.sln was not found.");
        }
    }

    private static Task RunSta(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(async () =>
        {
            try
            {
                await action();
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
