using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Markup;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class ThemeStartupSmokeTests
{
    [TestMethod]
    public Task AppAndDesignSystemResources_LoadInStartupOrder() => RunSta(() =>
    {
        var app = new RAWSelectionAssistant.App();
        app.InitializeComponent();
        Assert.IsNotNull(Find(app, "PrimaryButton"));
        Assert.IsNotNull(Find(app, "SecondaryButton"));
        Assert.IsNotNull(Find(app, "GhostButton"));
        Assert.IsNotNull(Find(app, "DangerButton"));
        Assert.IsNotNull(Find(app, "PixelTartGhostButton"));
        Assert.IsNotNull(Find(app, typeof(ContextMenu)));
        var tether = (FrameworkElement)XamlReader.Parse("""
            <views:TetherCaptureView xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                     xmlns:views="clr-namespace:RAWSelectionAssistant.Views;assembly=KitaoPhotoSelector" />
            """);

        var buttons = new StackPanel();
        foreach (var key in new[] { "PrimaryButton", "SecondaryButton", "GhostButton", "DangerButton" })
            buttons.Children.Add(new Button { Content = key, Style = (Style)Find(app, key)! });
        buttons.Children.Add(new Border { Style = (Style)Find(app, "PanelBorder")!, Child = new TextBlock { Text = "Thumbnail" } });
        buttons.Children.Add(tether);
        var window = new Window { Width = 640, Height = 720, ShowInTaskbar = false, WindowStyle = WindowStyle.None, Content = buttons };
        window.Show();
        window.UpdateLayout();
        foreach (var button in buttons.Children.OfType<Button>())
        {
            button.ApplyTemplate();
            Assert.IsNotNull(VisualTreeHelper.GetChild(button, 0), button.Content?.ToString());
        }
        window.Close();
        var shell = new RAWSelectionAssistant.MainWindow();
        shell.Show();
        shell.UpdateLayout();
        shell.Close();
        app.Shutdown();
    });

    private static Task RunSta(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => { try { action(); completion.SetResult(); } catch (Exception ex) { completion.SetException(ex); } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static object? Find(Application app, object key) => app.Resources[key] ?? app.Resources.MergedDictionaries
        .Select(dictionary => dictionary[key])
        .FirstOrDefault(value => value is not null);
}
