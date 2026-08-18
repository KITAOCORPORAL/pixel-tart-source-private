using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using PixelTart.Kernel;

namespace RAWSelectionAssistant.Views;

public sealed class ModuleWorkspaceHost : ContentControl
{
    private readonly Dictionary<string, object> _viewCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Border _inlineState;
    private readonly TextBlock _stateTitle;
    private readonly TextBlock _stateMessage;
    private readonly Button _retryButton;
    private DispatcherOperation? _pendingFocus;

    public static readonly DependencyProperty ModuleRegistryProperty = DependencyProperty.Register(
        nameof(ModuleRegistry), typeof(IModuleRegistry), typeof(ModuleWorkspaceHost), new PropertyMetadata(null, OnModuleRegistryChanged));

    public static readonly DependencyProperty RouteProperty = DependencyProperty.Register(
        nameof(Route), typeof(string), typeof(ModuleWorkspaceHost), new PropertyMetadata(string.Empty, OnRouteChanged));

    public ModuleWorkspaceHost()
    {
        _stateTitle = new TextBlock
        {
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        _stateTitle.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");

        _stateMessage = new TextBlock
        {
            Margin = new Thickness(0, 7, 0, 12),
            MaxWidth = 520,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        _stateMessage.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");

        _retryButton = new Button
        {
            Content = "重试",
            HorizontalAlignment = HorizontalAlignment.Center,
            MinWidth = 88
        };
        _retryButton.SetResourceReference(StyleProperty, "SecondaryButton");
        AutomationProperties.SetAutomationId(_retryButton, "RetryModuleWorkspace");
        AutomationProperties.SetName(_retryButton, "重试打开模块页面");
        _retryButton.Click += (_, _) => RetryCurrentRoute();

        _inlineState = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Padding = new Thickness(24),
            Child = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { _stateTitle, _stateMessage, _retryButton }
            }
        };
        AutomationProperties.SetLiveSetting(_inlineState, AutomationLiveSetting.Polite);

        Loaded += (_, _) => RequestInitialFocus();
        IsVisibleChanged += (_, args) =>
        {
            if (args.NewValue is true)
            {
                RequestInitialFocus();
            }
        };
        Unloaded += (_, _) => CancelPendingFocus();
    }

    public IModuleRegistry? ModuleRegistry
    {
        get => (IModuleRegistry?)GetValue(ModuleRegistryProperty);
        set => SetValue(ModuleRegistryProperty, value);
    }

    public string Route
    {
        get => (string?)GetValue(RouteProperty) ?? string.Empty;
        set => SetValue(RouteProperty, value);
    }

    public void RetryCurrentRoute()
    {
        RefreshContent();
    }

    public void RequestInitialFocus()
    {
        CancelPendingFocus();
        if (!IsLoaded || !IsVisible || Content is not UIElement content)
        {
            return;
        }

        _pendingFocus = Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            _pendingFocus = null;
            if (!IsLoaded || !IsVisible || !ReferenceEquals(Content, content))
            {
                return;
            }

            if (!content.MoveFocus(new TraversalRequest(FocusNavigationDirection.First)))
            {
                content.Focus();
            }
        });
    }

    private static void OnModuleRegistryChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var host = (ModuleWorkspaceHost)dependencyObject;
        host._viewCache.Clear();
        host.RefreshContent();
    }

    private static void OnRouteChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        ((ModuleWorkspaceHost)dependencyObject).RefreshContent();
    }

    private void RefreshContent()
    {
        var route = Route.Trim();
        if (ModuleRegistry is null)
        {
            ShowMissingState("模块工作区尚未就绪。", "主程序仍在准备模块。请稍后重试。", canRetry: true);
            return;
        }

        if (route.Length == 0)
        {
            ShowMissingState("未指定模块页面。", "请选择一个可用的模块页面。", canRetry: false);
            return;
        }

        if (!ModuleRegistry.Routes.TryGet(route, out var descriptor))
        {
            ShowMissingState("无法打开模块页面。", $"路由“{route}”尚未注册。你可以在模块就绪后重试。", canRetry: true);
            return;
        }

        if (_viewCache.TryGetValue(route, out var cached))
        {
            try
            {
                Content = cached;
                RequestInitialFocus();
            }
            catch (Exception)
            {
                _viewCache.Remove(route);
                ShowErrorState(descriptor.DisplayName);
            }
            return;
        }

        try
        {
            var view = descriptor.ViewFactory()
                ?? throw new InvalidOperationException($"Route '{route}' returned no view.");
            Content = view;
            _viewCache[route] = view;
            RequestInitialFocus();
        }
        catch (Exception)
        {
            ShowErrorState(descriptor.DisplayName);
        }
    }

    private void ShowMissingState(string title, string message, bool canRetry)
    {
        ShowInlineState("ModuleWorkspaceMissingState", title, message, canRetry, AutomationLiveSetting.Polite);
    }

    private void ShowErrorState(string displayName)
    {
        ShowInlineState(
            "ModuleWorkspaceErrorState",
            "模块暂时无法打开。",
            $"加载“{displayName}”时发生错误。请重试；如果问题持续，请重新打开应用。",
            canRetry: true,
            AutomationLiveSetting.Assertive);
    }

    private void ShowInlineState(
        string automationId,
        string title,
        string message,
        bool canRetry,
        AutomationLiveSetting liveSetting)
    {
        _stateTitle.Text = title;
        _stateMessage.Text = message;
        _retryButton.Visibility = canRetry ? Visibility.Visible : Visibility.Collapsed;
        _retryButton.IsEnabled = canRetry;
        AutomationProperties.SetAutomationId(_inlineState, automationId);
        AutomationProperties.SetName(_inlineState, title);
        AutomationProperties.SetLiveSetting(_inlineState, liveSetting);
        Content = _inlineState;
        RequestInitialFocus();
    }

    private void CancelPendingFocus()
    {
        _pendingFocus?.Abort();
        _pendingFocus = null;
    }
}
