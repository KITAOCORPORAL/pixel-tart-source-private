using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class RealLayoutRegressionTests
{
    private static readonly string? EvidenceRoot = Environment.GetEnvironmentVariable("PIXEL_TART_RC7_EVIDENCE");

    [TestMethod]
    public Task ProductionControls_ReportNonIntersectingActualBounds() => RunSta(() =>
    {
        EnsureApplication();
        // Load every production XAML resource before the native DatePicker popup exercises
        // the test host's separate HWND lifetime.
        var windows = new List<Window>();
        var view = new FinanceView();
        ((FrameworkElement)view.FindName("FinanceEditorSurface")).Visibility = Visibility.Collapsed;
        var header = new SurfaceHeader { Title = "这是一个用于高 DPI 验证的很长中文对话框标题", Subtitle = "标题与关闭按钮必须位于不同列。" };
        var host = new Grid { Width = 1280, Height = 820 };
        host.Children.Add(view);
        host.Children.Add(header);
        header.Width = 560;
        header.Height = 180;
        header.HorizontalAlignment = HorizontalAlignment.Left;
        header.VerticalAlignment = VerticalAlignment.Top;
        header.Margin = new Thickness(640, 400, 0, 0);
        windows.Add(Show(host, 1280, 820));
        var buttons = FindVisualChildren<Button>(view).Where(HasActualBounds).ToArray();
        var income = buttons.Single(button => AutomationProperties.GetName(button) == "新建收入");
        var expense = buttons.Single(button => AutomationProperties.GetName(button) == "新建支出");
        var export = buttons.Single(button => AutomationProperties.GetName(button) == "主动导出筛选后的收支CSV");
        AssertPairwiseDisjoint([Bounds(income, view), Bounds(expense, view), Bounds(export, view)], "finance toolbar");
        Assert.AreEqual(0, FindVisualChildren<SurfaceCloseButton>(view).Count(close => close.IsVisible && HasActualBounds(close)), "Closed editor must not expose a workspace close X.");

        var title = FindVisualChildren<TextBlock>(header).First(text => text.Text.StartsWith("这是一个"));
        var close = FindVisualChildren<SurfaceCloseButton>(header).Single();
        Assert.IsTrue(Rect.Intersect(Bounds(title, header), Bounds(close, header)).IsEmpty);
        Assert.IsGreaterThanOrEqualTo(40d, close.ActualWidth);
        Assert.IsGreaterThanOrEqualTo(40d, close.ActualHeight);
        Capture(host, "06_finance_toolbar.png", 1);
        Capture(header, "07_dialog_close.png", 1, Brushes.White);

        var root = Path.Combine(Path.GetTempPath(), "PixelTart-RC7-Create-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var dialog = new NewAssetLibraryDialog(root);
            dialog.Show();
            windows.Add(dialog);
            DoEvents();
            Assert.IsTrue(dialog.NameInput.IsEnabled && dialog.NameInput.Focusable);
            Assert.IsTrue(dialog.LocationInput.IsEnabled && dialog.LocationInput.IsReadOnly);
            Assert.IsTrue(dialog.CreateButton.IsEnabled);
            StringAssert.EndsWith(dialog.PreviewPath, "我的素材库.ptlibrary");
            Assert.IsTrue(dialog.TryBuildRequest(out var request, out var error), error);
            Assert.AreEqual(Path.Combine(root, "我的素材库.ptlibrary"), request!.ContainerPath);
            Capture(dialog, "04_asset_library_new.png", 1);
            var descriptor = new RAWSelectionAssistant.Core.Services.AssetLibrary.AssetLibraryContainerService()
                .CreateAsync(request.ContainerPath, request.DisplayName).GetAwaiter().GetResult();
            Assert.IsTrue(Directory.Exists(descriptor.ContainerPath));
            Assert.IsTrue(File.Exists(Path.Combine(descriptor.ContainerPath, RAWSelectionAssistant.Core.Services.AssetLibrary.AssetLibraryContainerService.ManifestFileName)));
            var created = new Border { Width = 720, Height = 360, Background = Brushes.White, Padding = new Thickness(28) };
            created.Child = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = "素材库", FontSize = 26, FontWeight = FontWeights.SemiBold },
                    new TextBlock { Text = $"当前库 · {descriptor.DisplayName}", FontSize = 18, Margin = new Thickness(0, 22, 0, 0) },
                    new TextBlock { Text = descriptor.ContainerPath, Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap },
                    new TextBlock { Text = "素材库为空 · 可以导入照片开始整理", Margin = new Thickness(0, 50, 0, 0), HorizontalAlignment = HorizontalAlignment.Center }
                }
            };
            created.Measure(new Size(720, 360)); created.Arrange(new Rect(0, 0, 720, 360)); created.UpdateLayout();
            Capture(created, "05_asset_library_created.png", 1);
            dialog.Hide();
        }
        finally { Directory.Delete(root, recursive: true); }

        foreach (var month in new[] { new DateTime(2026, 2, 1), new DateTime(2026, 9, 1), new DateTime(2026, 12, 1) })
        {
            var picker = new DatePicker { Width = 180, SelectedDate = month };
            windows.Add(Show(picker, 520, 520));
            picker.Focus();
            picker.ApplyTemplate();
            picker.IsDropDownOpen = true;
            DoEvents();
            var popup = picker.Template.FindName("PART_Popup", picker) as Popup
                ?? throw new InvalidOperationException("DatePicker did not expose its native PART_Popup.");
            var calendar = popup.Child is DependencyObject popupRoot
                ? FindVisualChildren<Calendar>(popupRoot).SingleOrDefault()
                : null;
            calendar ??= FindVisualChildren<Calendar>(picker).SingleOrDefault();
            if (calendar is null) throw new InvalidOperationException("Native DatePicker popup did not expose a Calendar visual.");
            calendar.Measure(new Size(420, 420));
            calendar.Arrange(new Rect(calendar.DesiredSize));
            DoEvents();
            calendar.UpdateLayout();
            var item = FindVisualChildren<CalendarItem>(calendar).SingleOrDefault();
            Assert.IsNotNull(item, "CalendarItem was not created. Visual tree: " + DescribeVisualTree(calendar));
            var allDays = FindVisualChildren<CalendarDayButton>(item).ToArray();
            var days = allDays.Where(HasActualBounds).ToArray();
            Assert.HasCount(42, days, $"{month:yyyy-MM} must render 42 laid-out day cells; total={allDays.Length}; sizes={string.Join(",", allDays.Take(8).Select(day => $"{day.ActualWidth:0.#}x{day.ActualHeight:0.#}"))}.");
            var rects = days.Select(day => Bounds(day, item)).ToArray();
            Assert.HasCount(7, Cluster(rects.Select(rect => rect.Left)));
            Assert.HasCount(6, Cluster(rects.Select(rect => rect.Top)));
            AssertPairwiseDisjoint(rects, $"{month:yyyy-MM} day cells");
            Assert.IsLessThan(420d, item.ActualHeight, $"{month:yyyy-MM} calendar height");
            Assert.IsLessThan(420d, item.ActualWidth, $"{month:yyyy-MM} calendar width");
            Assert.IsGreaterThan(12d, days.Min(day => day.ActualHeight));
            var monthName = month.Month switch { 2 => "feb", 9 => "sep", 12 => "dec", _ => month.ToString("MMM").ToLowerInvariant() };
            foreach (var scale in month.Month == 9 ? new[] { 1d, 1.25d, 1.5d, 2d } : new[] { 1d })
                Capture(calendar, $"datepicker_{monthName}_{scale * 100:0}.png", scale);
            if (month.Month == 2)
            {
                Capture(picker, "01_calendar_closed.png", 1);
                Capture(calendar, "02_calendar_open.png", 1);
            }
            if (month.Month == 9) Capture(calendar, "03_calendar_150.png", 1.5);
            picker.IsDropDownOpen = false;
        }
        foreach (var window in windows) window.Close();
        return Task.CompletedTask;
    });

    private static Window Show(FrameworkElement content, double width, double height)
    {
        var window = new Window { Content = content, Width = width, Height = height, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = 24, Top = 24 };
        window.Show();
        DoEvents();
        return window;
    }

    private static Rect Bounds(FrameworkElement element, Visual ancestor) =>
        element.TransformToAncestor(ancestor).TransformBounds(new Rect(new Point(), element.RenderSize));

    private static bool HasActualBounds(FrameworkElement element) => element.ActualWidth > 0 && element.ActualHeight > 0;

    private static List<double> Cluster(IEnumerable<double> values)
    {
        var result = new List<double>();
        foreach (var value in values.Order()) if (result.Count == 0 || Math.Abs(result[^1] - value) > 1d) result.Add(value);
        return result;
    }

    private static void AssertPairwiseDisjoint(IReadOnlyList<Rect> rects, string label)
    {
        for (var left = 0; left < rects.Count; left++)
        for (var right = left + 1; right < rects.Count; right++)
        {
            var intersection = Rect.Intersect(rects[left], rects[right]);
            Assert.IsTrue(intersection.IsEmpty || intersection.Width <= .1 || intersection.Height <= .1, $"{label}: {left} intersects {right}: {intersection}");
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match) yield return match;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            foreach (var child in FindVisualChildren<T>(VisualTreeHelper.GetChild(root, index))) yield return child;
    }

    private static string DescribeVisualTree(DependencyObject root) => string.Join(" > ", Walk(root).Select(item => item.GetType().Name));
    private static IEnumerable<DependencyObject> Walk(DependencyObject root)
    {
        yield return root;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            foreach (var child in Walk(VisualTreeHelper.GetChild(root, index))) yield return child;
    }

    private static void DoEvents() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    private static void Capture(FrameworkElement element, string fileName, double scale, Brush? background = null)
    {
        if (string.IsNullOrWhiteSpace(EvidenceRoot) || element.ActualWidth <= 0 || element.ActualHeight <= 0) return;
        Directory.CreateDirectory(EvidenceRoot);
        var width = Math.Max(1, (int)Math.Ceiling(element.ActualWidth * scale));
        var height = Math.Max(1, (int)Math.Ceiling(element.ActualHeight * scale));
        var bitmap = new RenderTargetBitmap(width, height, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        if (background is null) bitmap.Render(element);
        else
        {
            var visual = new DrawingVisual();
            using (var drawing = visual.RenderOpen())
            {
                drawing.DrawRectangle(background, null, new Rect(0, 0, element.ActualWidth, element.ActualHeight));
                var brush = new VisualBrush(element) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top };
                drawing.DrawRectangle(brush, null, new Rect(0, 0, element.ActualWidth, element.ActualHeight));
            }
            bitmap.Render(visual);
        }
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(EvidenceRoot, fileName));
        encoder.Save(stream);
    }

    private static void EnsureApplication()
    {
        if (Application.Current is not null)
        {
            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            return;
        }
        var application = new Application();
        application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        LoadProductionResources(application);
    }

    private static void LoadProductionResources(Application application)
    {
        var sourceRoot = Path.Combine(Root(), "src", "RAWSelectionAssistant");
        var loaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Load(string relative)
        {
            relative = relative.Replace('/', Path.DirectorySeparatorChar);
            if (!loaded.Add(relative)) return;
            var document = XDocument.Load(Path.Combine(sourceRoot, relative));
            if (relative.EndsWith("App.xaml", StringComparison.OrdinalIgnoreCase) ||
                relative.EndsWith("PixelTart.Theme.xaml", StringComparison.OrdinalIgnoreCase) ||
                relative.EndsWith("PixelTart.Components.xaml", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var source in document.Descendants().Attributes("Source"))
                    Load(source.Value.TrimStart('/', '\\'));
                return;
            }
            application.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(
                new Uri("/KitaoPhotoSelector;component/" + relative.Replace(Path.DirectorySeparatorChar, '/'), UriKind.Relative)));
        }
        Load("App.xaml");
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar)));
    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }

    private static Task RunSta(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(async () =>
        {
            try { await action(); completion.SetResult(); }
            catch (Exception exception) { completion.SetException(exception); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}
