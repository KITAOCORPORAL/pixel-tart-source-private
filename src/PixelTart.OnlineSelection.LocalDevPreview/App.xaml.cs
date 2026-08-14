using System.Windows;
using System.IO;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.OnlineSelection;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Services.OnlineSelection;
using RAWSelectionAssistant.ViewModels;

namespace PixelTart.OnlineSelection.LocalDevPreview;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var endpointText = Environment.GetEnvironmentVariable("PIXELTART_SELECTION_LOCALDEV_ENDPOINT") ?? "http://127.0.0.1:5127";
            var root = Path.GetFullPath(Environment.GetEnvironmentVariable("PIXELTART_SELECTION_PREVIEW_ROOT")
                ?? Path.Combine(Path.GetTempPath(), "PixelTart_OnlineSelection_LocalDev_Preview", "Desktop"));
            Directory.CreateDirectory(root);
            var provider = new LocalDevOnlineSelectionProvider(new Uri(endpointText), Path.Combine(root, "localdev-access.dpapi"));
            var store = new JsonSelectionWorkspaceStore(Path.Combine(root, "workspace.json"));
            var proxyService = new SelectionProxyJpegService(new WpfSelectionProxyRenderer(new LibRawDecoder()));
            var viewModel = new OnlineSelectionViewModel(
                provider,
                store,
                new SelectionResultSyncService(new FileNameNormalizer()),
                proxyService,
                Path.Combine(root, "Proxies"),
                new LocalDevPreviewDialogService());
            await viewModel.RefreshAsync();
            var window = new PreviewWindow { DataContext = viewModel };
            MainWindow = window;
            window.Show();
        }
        catch (Exception exception)
        {
            MessageBox.Show($"LocalDev Preview could not start.\n{exception.Message}", "Pixel Tart Online Selection LocalDev", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }
}
