namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class NavigationSafety204Tests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [TestMethod]
    public void ReleaseStartupDoesNotRunUiReviewController()
    {
        var mainWindow = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "RAWSelectionAssistant", "MainWindow.xaml.cs"));
        StringAssert.Contains(mainWindow, "#if UI_REVIEW_BUILD");
        StringAssert.Contains(mainWindow, "StartUiReviewController();");
        var project = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "RAWSelectionAssistant", "RAWSelectionAssistant.csproj"));
        StringAssert.Contains(project, "DefineConstants Condition=\"'$(UiReviewBuild)' == 'true'\"");
    }

    [TestMethod]
    public void ReleaseProjectDoesNotDefineUiReviewByDefault()
    {
        var project = ProductProject();
        StringAssert.Contains(project, "DefineConstants Condition=\"'$(UiReviewBuild)' == 'true'\"");
        Assert.IsFalse(project.Contains("<UiReviewBuild>true</UiReviewBuild>", StringComparison.Ordinal));
    }

    [TestMethod]
    public void StartupDefaultsToWorkbenchAndDoesNotRestoreCollage()
    {
        var viewModel = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "RAWSelectionAssistant", "ViewModels", "MainViewModel.cs"));
        StringAssert.Contains(viewModel, "private string _currentPage = \"Workbench\"");
        StringAssert.Contains(viewModel, "NavigateToSurface(\"Workbench\", recordHistory: false)");
        Assert.IsFalse(viewModel.Contains("CurrentPage = \"Collage\";", StringComparison.Ordinal));
    }

    [TestMethod]
    public void StartupDoesNotImportCollageImages()
    {
        var viewModel = MainViewModel();
        var constructor = Slice(viewModel, "public MainViewModel(", "public ObservableCollection<SourceDirectoryEntry>");
        Assert.IsFalse(constructor.Contains("CollagePage.AddPaths", StringComparison.Ordinal));
        Assert.IsFalse(constructor.Contains("AddPhotosCommand.Execute", StringComparison.Ordinal));
    }

    [TestMethod]
    public void StartupDoesNotSelectTwoByTwoTemplate()
    {
        var viewModel = MainViewModel();
        var constructor = Slice(viewModel, "public MainViewModel(", "public ObservableCollection<SourceDirectoryEntry>");
        Assert.IsFalse(constructor.Contains("4-grid", StringComparison.Ordinal));
        Assert.IsFalse(constructor.Contains("2×2", StringComparison.Ordinal));
    }

    [TestMethod]
    public void DuplicateNavigationIsIgnored()
    {
        var viewModel = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "RAWSelectionAssistant", "ViewModels", "MainViewModel.cs"));
        StringAssert.Contains(viewModel, "if (string.Equals(CurrentPage, normalizedTarget, StringComparison.Ordinal)) return;");
    }

    [TestMethod]
    public void CollageNavigationUsesSingleSharedPageViewModel()
    {
        var viewModel = MainViewModel();
        Assert.AreEqual(1, Count(viewModel, "CollagePage = new CollageViewModel"));
        StringAssert.Contains(viewModel, "public CollageViewModel CollagePage");
    }

    [TestMethod]
    public void CollageImportCommandCannotReenter()
    {
        var source = Text("src/RAWSelectionAssistant/ViewModels/ToolPageViewModels.cs");
        StringAssert.Contains(source, "AddPhotosCommand = new RelayCommand(_ => AddPhotos(), _ => !IsBusy && !_isImporting)");
        StringAssert.Contains(source, "if (_isImporting) return;");
        StringAssert.Contains(source, "finally { _isImporting = false; RefreshCommandStates(); }");
        StringAssert.Contains(source, "ExportCommand = new AsyncRelayCommand(_ => ExportAsync(), _ => !IsBusy && Images.Count > 0)");
    }

    [TestMethod]
    public void AsyncCommandsHaveBuiltInReentryProtection()
    {
        var source = Text("src/RAWSelectionAssistant/Utilities/RelayCommand.cs");
        StringAssert.Contains(source, "private bool _isExecuting;");
        StringAssert.Contains(source, "public bool CanExecute(object? parameter) => !_isExecuting");
    }

    [TestMethod]
    public void CollageLoadedOnlyRendersPreview()
    {
        var source = Text("src/RAWSelectionAssistant/Views/CollageView.xaml.cs");
        var loadedHandler = Slice(source, "Loaded += (_, _) => RenderPreview();", "private void OnDataContextChanged");
        StringAssert.Contains(loadedHandler, "RenderPreview();");
        Assert.IsFalse(loadedHandler.Contains("AddPaths", StringComparison.Ordinal));
        Assert.IsFalse(loadedHandler.Contains("SelectedTemplate", StringComparison.Ordinal));
    }

    [TestMethod]
    public void InstalledAutomationRequiresExplicitIsolationGate()
    {
        var script = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "AutomatedDpiAcceptance", "Invoke-InstalledInteraction.ps1"));
        StringAssert.Contains(script, "IsolatedAcceptanceRun");
        StringAssert.Contains(script, "PIXEL_TART_ALLOW_INSTALLED_AUTOMATION");
    }

    [TestMethod]
    public void InstalledAutomationAlwaysCleansUpControlledProcess()
    {
        var script = Text("tools/AutomatedDpiAcceptance/Invoke-InstalledInteraction.ps1");
        StringAssert.Contains(script, "finally {");
        StringAssert.Contains(script, "Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue");
        StringAssert.Contains(script, "unins000.exe");
    }

    [TestMethod]
    public void UiReviewScenarioIsCompileTimeIsolated()
    {
        var source = Text("src/RAWSelectionAssistant/MainWindow.xaml.cs");
        var reviewCode = Slice(source, "#if UI_REVIEW_BUILD", "#endif");
        StringAssert.Contains(reviewCode, "System.Text.Json");
        StringAssert.Contains(source, "#if UI_REVIEW_BUILD");
        StringAssert.Contains(ProductProject(), "AssemblyName Condition=\"'$(UiReviewBuild)' == 'true'\"");
    }

    [TestMethod]
    public void LocalSplitWizardAndWorkflowHaveDifferentVisualTrees()
    {
        var xaml = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "RAWSelectionAssistant", "MainWindow.xaml"));
        StringAssert.Contains(xaml, "本地分片快速向导");
        StringAssert.Contains(xaml, "x:Name=\"WorkflowWorkspace\"");
        StringAssert.Contains(xaml, "Visibility=\"{Binding IsLocalSplitPage");
        StringAssert.Contains(xaml, "Visibility=\"{Binding IsWorkflowPage");
    }

    [TestMethod]
    public void WorkbenchLocalSplitCardOpensWizard()
    {
        var xaml = MainXaml();
        StringAssert.Contains(xaml, "x:Name=\"StartLocalSplitCard\"");
        var localSplitButton = Slice(xaml, "x:Name=\"StartLocalSplitCard\"", "</Button>");
        StringAssert.Contains(localSplitButton, "CommandParameter=\"LocalSplit\"");
    }

    [TestMethod]
    public void SidebarWorkflowEntryOpensWorkspace()
    {
        var sidebar = Sidebar();
        StringAssert.Contains(sidebar, "Content=\"归片工作区\"");
        StringAssert.Contains(sidebar, "CommandParameter=\"Workflow\"");
    }

    [TestMethod]
    public void SidebarDoesNotDuplicateLocalSplitWizard()
    {
        Assert.IsFalse(Sidebar().Contains("Content=\"本地分片\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void WizardCanEnterWorkflowExplicitly()
    {
        var xaml = MainXaml();
        var wizard = Slice(xaml, "Text=\"本地分片快速向导\"", "Visibility=\"{Binding IsBatchCompressPage");
        StringAssert.Contains(wizard, "Content=\"进入归片工作区\"");
        StringAssert.Contains(wizard, "CommandParameter=\"Workflow\"");
    }

    [TestMethod]
    public void ReturningToWorkbenchDoesNotRestoreWizard()
    {
        var viewModel = MainViewModel();
        Assert.IsFalse(viewModel.Contains("LastPage", StringComparison.Ordinal));
        Assert.IsFalse(viewModel.Contains("RestoreLastPage", StringComparison.Ordinal));
    }

    [TestMethod]
    public void OneNavigationRequestWritesOneSanitizedLog()
    {
        var navigate = Slice(MainViewModel(), "private void Navigate(object? parameter)", "private void TogglePinnedTool");
        Assert.AreEqual(1, Count(navigate, "_logService.Info"));
        StringAssert.Contains(navigate, "navigationCorrelationId");
        StringAssert.Contains(navigate, "if (string.Equals(CurrentPage, normalizedTarget, StringComparison.Ordinal)) return;");
    }

    private static string ProductProject() => Text("src/RAWSelectionAssistant/RAWSelectionAssistant.csproj");
    private static string MainViewModel() => Text("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs");
    private static string MainXaml() => Text("src/RAWSelectionAssistant/MainWindow.xaml");
    private static string Sidebar()
    {
        var xaml = MainXaml();
        return Slice(xaml, "x:Name=\"SidebarContainer\"", "<Grid Grid.Column=\"1\">");
    }
    private static string Text(string relativePath) => File.ReadAllText(Path.Combine(RepositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    private static string Slice(string text, string startValue, string endValue)
    {
        var start = text.IndexOf(startValue, StringComparison.Ordinal);
        var end = text.IndexOf(endValue, start + startValue.Length, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, start, $"未找到起始标记：{startValue}");
        Assert.IsGreaterThan(start, end, $"未找到结束标记：{endValue}");
        return text[start..end];
    }
    private static int Count(string text, string value)
    {
        var count = 0;
        for (var index = 0; (index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0; index += value.Length) count++;
        return count;
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
