using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Views;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class InputRoutingHitTestSmokeTests
{
    [TestMethod]
    public Task RootEmergencyClose_IsHitAboveTutorialBlockerAtItsActualCenter() => RunSta(() =>
    {
        var root = new Grid
        {
            Width = 800,
            Height = 600,
            Background = Brushes.Transparent,
            IsHitTestVisible = true
        };

        var tutorialBlocker = new Border
        {
            Background = Brushes.Transparent,
            IsHitTestVisible = true
        };
        Panel.SetZIndex(tutorialBlocker, 1000);
        root.Children.Add(tutorialBlocker);

        var callout = new Border
        {
            Width = 280,
            Height = 180,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Background = Brushes.DimGray,
            IsHitTestVisible = true
        };
        Panel.SetZIndex(callout, 2200);
        root.Children.Add(callout);

        var emergency = CreateButton("ShellEmergencyCloseButton", 40, 40);
        emergency.HorizontalAlignment = HorizontalAlignment.Right;
        emergency.VerticalAlignment = VerticalAlignment.Top;
        emergency.Margin = new Thickness(0, 12, 12, 0);
        Panel.SetZIndex(emergency, 30000);
        root.Children.Add(emergency);

        using var host = ShowOffscreen(root);
        var point = CenterOf(emergency, root);
        AssertHit(root, point, "ShellEmergencyCloseButton");
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task TutorialCalloutControls_AreHitAboveItsFullScreenBackdrop() => RunSta(() =>
    {
        var root = new Grid
        {
            Width = 800,
            Height = 600,
            Background = Brushes.Transparent,
            IsHitTestVisible = true
        };

        var blocker = new Border { Background = Brushes.Transparent, IsHitTestVisible = true };
        Panel.SetZIndex(blocker, 1000);
        root.Children.Add(blocker);

        var callout = new Border
        {
            Width = 320,
            Height = 220,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brushes.DimGray,
            IsHitTestVisible = true
        };
        var calloutGrid = new Grid();
        callout.Child = calloutGrid;
        var localClose = CreateButton("TutorialCalloutCloseButton", 40, 40);
        localClose.HorizontalAlignment = HorizontalAlignment.Right;
        localClose.VerticalAlignment = VerticalAlignment.Top;
        var exit = CreateButton("TutorialExitButton", 120, 40);
        exit.HorizontalAlignment = HorizontalAlignment.Right;
        exit.VerticalAlignment = VerticalAlignment.Bottom;
        exit.Margin = new Thickness(0, 0, 12, 12);
        calloutGrid.Children.Add(localClose);
        calloutGrid.Children.Add(exit);
        Panel.SetZIndex(callout, 2200);
        root.Children.Add(callout);

        using var host = ShowOffscreen(root);
        AssertHit(root, CenterOf(localClose, root), "TutorialCalloutCloseButton");
        AssertHit(root, CenterOf(exit, root), "TutorialExitButton");
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task SurfaceCloseButton_ClickContractBubblesThroughVisualRootWithoutCommandExecution() => RunSta(() =>
    {
        var root = new Grid { Width = 200, Height = 100, Background = Brushes.Transparent };
        var closeChrome = new ContentControl { Width = 36, Height = 36 };
        var innerButton = new Button { Width = 36, Height = 36, Content = "close" };
        closeChrome.Content = innerButton;
        root.Children.Add(closeChrome);
        Arrange(root);

        var raised = false;
        root.AddHandler(SurfaceCloseButton.CloseRequestedEvent, new RoutedEventHandler((_, args) =>
        {
            raised = true;
            Assert.AreSame(closeChrome, args.OriginalSource);
        }));
        innerButton.Click += (_, args) =>
        {
            args.Handled = true;
            closeChrome.RaiseEvent(new RoutedEventArgs(SurfaceCloseButton.CloseRequestedEvent, closeChrome));
        };

        innerButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, innerButton));
        Assert.IsTrue(raised, "The shell close routed event must reach the visual root.");

        var xaml = Read("src/RAWSelectionAssistant/Views/SurfaceCloseButton.xaml");
        var code = Read("src/RAWSelectionAssistant/Views/SurfaceCloseButton.xaml.cs");
        ContainsAll(xaml, "Click=\"CloseButton_Click\"");
        ContainsAll(code,
            "RoutingStrategy.Bubble",
            "private void CloseButton_Click",
            "e.Handled = true",
            "RaiseEvent(new RoutedEventArgs(CloseRequestedEvent, this))");
        Assert.IsFalse(xaml.Contains("Command=", StringComparison.Ordinal),
            "The shared shell close button must not depend on command execution.");

        return Task.CompletedTask;
    });

    [TestMethod]
    public Task ShellEscapePointer_ResolvesOnlyExplicitEscapeTargetsAndDeduplicatesOneInput() => RunSta(() =>
    {
        var root = new Grid();
        var close = new Border();
        var closeContent = new TextBlock { Text = "close" };
        close.Child = closeContent;
        ShellEscapePointer.SetAction(close, ShellEscapePointerAction.CloseCurrentSurface);
        root.Children.Add(close);

        var ordinary = new Button { Content = "ordinary" };
        root.Children.Add(ordinary);

        Assert.IsTrue(ShellEscapePointer.TryResolve(closeContent, out var owner, out var action));
        Assert.AreSame(close, owner);
        Assert.AreEqual(ShellEscapePointerAction.CloseCurrentSurface, action);
        Assert.IsFalse(ShellEscapePointer.TryResolve(ordinary, out _, out _),
            "Ordinary controls must not enter the shell escape route.");

        var input = new MouseButtonEventArgs(Mouse.PrimaryDevice, 42, MouseButton.Left);
        Assert.IsTrue(ShellEscapePointer.TryBeginDispatch(input));
        Assert.IsFalse(ShellEscapePointer.TryBeginDispatch(input),
            "The same pointer input must dispatch only once.");
        var laterInput = new MouseButtonEventArgs(Mouse.PrimaryDevice, 42, MouseButton.Left);
        Assert.IsTrue(ShellEscapePointer.TryBeginDispatch(laterInput),
            "A later input must remain available to keyboard, UIA and pointer paths.");
        return Task.CompletedTask;
    });

    [TestMethod]
    public void EscapeAndAutomationContracts_AreShellOwnedAndStable()
    {
        var mainXaml = Read("src/RAWSelectionAssistant/MainWindow.xaml");
        var mainCode = ReadAll("src/RAWSelectionAssistant", "MainWindow*.cs");
        var viewModel = Read("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs");
        var coreServices = ReadAll("src/RAWSelectionAssistant.Core/Services", "*.cs");
        var raw = Read("src/RAWSelectionAssistant/Views/RawToJpegModal.xaml");
        var batch = Read("src/RAWSelectionAssistant/Views/BatchCompressionModal.xaml");
        var surfaceClose = Read("src/RAWSelectionAssistant/Views/SurfaceCloseButton.xaml") +
                           Read("src/RAWSelectionAssistant/Views/SurfaceCloseButton.xaml.cs");

        foreach (var automationId in new[]
                 {
                     "WorkbenchRoot", "SidebarRoot", "TutorialCalloutCloseButton", "TutorialExitButton",
                     "ShellEmergencyCloseButton", "RawToJpegCloseButton", "RawToJpegCancelTaskButton",
                     "BatchCompressCloseButton", "BatchCompressCancelTaskButton"
                 })
            Assert.AreEqual(1, CountAutomationIdDeclarations(automationId, mainXaml, raw, batch),
                $"{automationId} must be declared exactly once in product markup.");

        ContainsAll(mainXaml, "ShellSurfaceCloseButton", "Panel.ZIndex=\"30000\"");
        ContainsAll(mainXaml,
            "StringFormat=WorkbenchQuickTool_{0}",
            "AutomationId=\"WorkbenchToolboxButton\"",
            "StringFormat=ToolboxCatalogTool_{0}");
        ContainsAll(surfaceClose, "AutomationProperties.AutomationId", "AutomationIdProperty");
        ContainsAll(mainCode,
            "PreviewMouseLeftButtonDown",
            "PreviewMouseLeftButtonUp",
            "PreviewKeyDown",
            "InputRoutingDiagnostics.RecordWindowMouse",
            "ForceCloseCurrentSurface",
            "ForceExitTutorial",
            "ForceReturnToWorkbench");
        ContainsAll(mainCode,
            "AddHandler(UIElement.PreviewMouseLeftButtonDownEvent",
            "ShellEscapePointer.TryResolve",
            "ShellEscapePointer.TryBeginDispatch",
            "ShellEscapePointerAction.ExitTutorial",
            "await RequestEscapeCloseAsync()");
        ContainsAll(mainXaml,
            "services:ShellEscapePointer.Action=\"ExitTutorial\"",
            "AutomationProperties.AutomationId=\"TutorialExitButton\"");
        var surfaceCloseCode = Read("src/RAWSelectionAssistant/Views/SurfaceCloseButton.xaml.cs");
        ContainsAll(surfaceCloseCode,
            "ShellEscapePointer.TryBeginDispatch(e)",
            "RaiseEvent(new RoutedEventArgs(CloseRequestedEvent, this))");
        var diagnostics = Read("src/RAWSelectionAssistant/Services/InputRoutingDiagnostics.cs");
        ContainsAll(diagnostics, "InputHitTest", "VisualTreeHelper.HitTest", "visual_parent_chain");
        ContainsAll(coreServices, "IShellEscapeService", "ForceCloseCurrentSurface", "ForceExitTutorial", "ForceReturnToWorkbench");
        StringAssert.Contains(mainCode, "Key.Escape");
        Assert.IsFalse(mainXaml.Contains("Key=\"Escape\"", StringComparison.Ordinal),
            "Escape must be captured by the Window shell boundary, not a business command binding.");

        var landmarkSource = Read("src/RAWSelectionAssistant/Views/AutomationLandmark.cs");
        ContainsAll(landmarkSource,
            "OnCreateAutomationPeer",
            "AutomationControlType.Pane",
            "IsControlElementCore() => true",
            "IsContentElementCore() => true",
            "IsHitTestVisible = false");

        var document = XDocument.Parse(mainXaml);
        var tutorial = NamedElement(document, "TutorialOverlay");
        var emergency = NamedElement(document, "ShellSurfaceCloseButton");
        Assert.AreSame(tutorial.Parent, emergency.Parent,
            "ShellSurfaceCloseButton must be a RootGrid sibling of TutorialOverlay.");
        Assert.IsTrue(string.Equals(Attribute(emergency, "HorizontalAlignment"), "Right", StringComparison.Ordinal));
        Assert.IsTrue(string.Equals(Attribute(emergency, "VerticalAlignment"), "Top", StringComparison.Ordinal));
        Assert.IsTrue(double.TryParse(Attribute(emergency, "Width"), out var width) && width >= 36 && width <= 64,
            "ShellSurfaceCloseButton must only wrap its close button, not capture the full window.");
        Assert.IsTrue(double.TryParse(Attribute(emergency, "Height"), out var height) && height >= 36 && height <= 64,
            "ShellSurfaceCloseButton must only wrap its close button, not capture the full window.");
        var tutorialClose = ElementWithAutomationId(document, "TutorialCalloutCloseButton");
        var tutorialExit = ElementWithAutomationId(document, "TutorialExitButton");
        Assert.IsTrue(tutorialClose.Ancestors().Contains(tutorial), "Tutorial local close must remain inside TutorialOverlay.");
        Assert.IsTrue(tutorialExit.Ancestors().Contains(tutorial), "Tutorial text exit must remain inside TutorialOverlay.");
        Assert.IsNull(tutorialClose.Attributes().SingleOrDefault(attribute => attribute.Name.LocalName == "Command"),
            "Tutorial close cannot depend on a business command.");
        Assert.IsNull(tutorialExit.Attributes().SingleOrDefault(attribute => attribute.Name.LocalName == "Command"),
            "Tutorial exit cannot depend on a business command.");

        var escapeHandler = Slice(mainCode, "private async void Window_PreviewKeyDown", "private bool TryCloseActiveInputPopup");
        var escapeBranch = Slice(escapeHandler, "if (e.Key == Key.Escape)", "if (_viewModel?.IsOnboardingActive");
        Assert.IsFalse(escapeBranch.Contains("Command.Execute", StringComparison.Ordinal),
            "The Escape close route must not execute a business command.");

        ContainsAll(viewModel,
            "if (operationSession is null)",
            "tutorialSession is null ? CurrentPersistedTaskProjectId() : null");
    }

    [TestMethod]
    public Task AutomationLandmark_ExposesPanePeerWithoutCapturingInput() => RunSta(() =>
    {
        var landmark = new AutomationLandmark { Width = 320, Height = 180 };
        AutomationProperties.SetAutomationId(landmark, "WorkbenchRoot");
        Arrange(landmark);

        var peer = UIElementAutomationPeer.CreatePeerForElement(landmark);
        Assert.IsNotNull(peer, "The root landmark must create a real WPF automation peer.");
        Assert.AreEqual(AutomationControlType.Pane, peer.GetAutomationControlType());
        Assert.AreEqual("WorkbenchRoot", peer.GetAutomationId());
        Assert.IsFalse(landmark.IsHitTestVisible, "The UIA root marker must never intercept mouse input.");
        return Task.CompletedTask;
    });

    [TestMethod]
    public void ForceExitTutorialContract_DetachesBeforeAwaitingBackgroundCleanup()
    {
        var viewModel = Read("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs");
        var shell = Read("src/RAWSelectionAssistant/MainWindow.xaml.cs");
        var source = viewModel + Environment.NewLine + shell;
        var forceDeclaration = Regex.Match(source,
            @"(?m)^\s*(?:public|private|internal)\s+(?:async\s+)?[\w<>,?.]+\s+ForceExitTutorial(?:Async)?\s*\(",
            RegexOptions.CultureInvariant);
        Assert.IsTrue(forceDeclaration.Success, "A shell-owned ForceExitTutorial method is required.");
        var forceIndex = forceDeclaration.Index;
        var method = MethodBodyFrom(source, forceIndex);
        var detachMarkers = new[]
        {
            "RestoreNormalWorkspace",
            "TutorialOverlay.Visibility",
            "IsOnboardingActive = false",
            "TutorialActive = false",
            "DetachTutorial"
        };
        var detachIndex = detachMarkers
            .Select(marker => method.IndexOf(marker, StringComparison.Ordinal))
            .Where(index => index >= 0)
            .DefaultIfEmpty(-1)
            .Min();
        Assert.IsGreaterThanOrEqualTo(0, detachIndex,
            "ForceExitTutorial must expose an immediate UI detach operation.");

        var awaitIndex = method.IndexOf("await ", StringComparison.Ordinal);
        Assert.IsTrue(awaitIndex < 0 || detachIndex < awaitIndex,
            "Tutorial UI must detach before awaiting potentially stuck background work.");
        Assert.IsTrue(source.Contains("500", StringComparison.Ordinal) ||
                      source.Contains("1000", StringComparison.Ordinal) ||
                      source.Contains("FromSeconds(1)", StringComparison.Ordinal),
            "Tutorial cleanup must use the bounded 500-1000ms safety window.");
    }

    private static Button CreateButton(string automationId, double width, double height)
    {
        var button = new Button
        {
            Width = width,
            Height = height,
            MinWidth = width,
            MinHeight = height,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Focusable = true,
            Content = automationId
        };
        AutomationProperties.SetAutomationId(button, automationId);
        return button;
    }

    private static void Arrange(FrameworkElement root)
    {
        root.Measure(new Size(root.Width, root.Height));
        root.Arrange(new Rect(0, 0, root.Width, root.Height));
        root.UpdateLayout();
    }

    private static WindowScope ShowOffscreen(FrameworkElement root)
    {
        var applicationBefore = Application.Current;
        var window = new Window
        {
            Left = -32000,
            Top = -32000,
            ShowInTaskbar = false,
            ShowActivated = false,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            SizeToContent = SizeToContent.WidthAndHeight,
            Content = root
        };
        window.Show();
        Arrange(root);
        return new WindowScope(window, applicationBefore);
    }

    private static Point CenterOf(FrameworkElement element, Visual root)
    {
        var bounds = element.TransformToAncestor(root).TransformBounds(new Rect(new Point(), element.RenderSize));
        return new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
    }

    private static void AssertHit(FrameworkElement root, Point point, string expectedAutomationId)
    {
        var inputHit = root.InputHitTest(point);
        Assert.IsNotNull(inputHit, $"InputHitTest returned no element at {point}.");
        var visualHit = VisualTreeHelper.HitTest(root, point)?.VisualHit;
        Assert.IsNotNull(visualHit, $"VisualTreeHelper.HitTest returned no element at {point}.");
        Assert.AreEqual(expectedAutomationId, FindAutomationId(inputHit as DependencyObject),
            $"InputHitTest did not resolve to {expectedAutomationId}.");
        Assert.AreEqual(expectedAutomationId, FindAutomationId(visualHit),
            $"VisualTreeHelper.HitTest did not resolve to {expectedAutomationId}.");
    }

    private static string? FindAutomationId(DependencyObject? element)
    {
        for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is UIElement uiElement)
            {
                var id = AutomationProperties.GetAutomationId(uiElement);
                if (!string.IsNullOrWhiteSpace(id)) return id;
            }
        }

        return null;
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

    private static void ContainsAll(string source, params string[] expected)
    {
        foreach (var token in expected) StringAssert.Contains(source, token);
    }

    private static XElement NamedElement(XDocument document, string name) =>
        document.Descendants().Single(element => element.Attributes().Any(attribute =>
            attribute.Name.LocalName == "Name" && string.Equals(attribute.Value, name, StringComparison.Ordinal)));

    private static XElement ElementWithAutomationId(XDocument document, string automationId) =>
        document.Descendants().Single(element => element.Attributes().Any(attribute =>
            IsAutomationIdAttribute(attribute) && string.Equals(attribute.Value, automationId, StringComparison.Ordinal)));

    private static int CountAutomationIdDeclarations(string automationId, params string[] markupSources) =>
        markupSources
            .Select(XDocument.Parse)
            .SelectMany(document => document.Descendants())
            .Where(element => element.Attributes().Any(attribute =>
                IsAutomationIdAttribute(attribute) && string.Equals(attribute.Value, automationId, StringComparison.Ordinal)))
            .Count();

    private static bool IsAutomationIdAttribute(XAttribute attribute) =>
        attribute.Name.LocalName.EndsWith("AutomationId", StringComparison.Ordinal);

    private static string? Attribute(XElement element, string localName) =>
        element.Attributes().SingleOrDefault(attribute => attribute.Name.LocalName == localName)?.Value;

    private static string Slice(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, start, $"Missing start marker: {startMarker}");
        var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.IsGreaterThan(start, end, $"Missing end marker: {endMarker}");
        return source[start..end];
    }

    private static string MethodBodyFrom(string source, int methodNameIndex)
    {
        var open = source.IndexOf('{', methodNameIndex);
        Assert.IsGreaterThan(methodNameIndex, open, "ForceExitTutorial has no method body.");
        var depth = 0;
        for (var index = open; index < source.Length; index++)
        {
            if (source[index] == '{') depth++;
            if (source[index] != '}') continue;
            depth--;
            if (depth == 0) return source[open..(index + 1)];
        }
        Assert.Fail("ForceExitTutorial method body is not balanced.");
        return string.Empty;
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(Root(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string ReadAll(string relativeDirectory, string pattern) => string.Join(Environment.NewLine,
        Directory.GetFiles(Path.Combine(Root(), relativeDirectory.Replace('/', Path.DirectorySeparatorChar)), pattern, SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(File.ReadAllText));

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("RAWSelectionAssistant.sln was not found.");
    }

    private sealed class WindowScope(Window window, Application? applicationBefore) : IDisposable
    {
        public void Dispose()
        {
            window.Content = null;
            window.Close();
            Assert.AreSame(applicationBefore, Application.Current,
                "The isolated hit-test host must not create, replace or shut down the process-wide WPF Application.");
        }
    }
}
