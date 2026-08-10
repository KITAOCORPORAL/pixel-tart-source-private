#if UI_REVIEW_BUILD
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Services.Business;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;

namespace RAWSelectionAssistant;

public partial class MainWindow
{
    private bool _automatedDpiAcceptanceEnabled;
    private double _automatedDpiScale = 1d;
    private int _automatedPhysicalWidth;
    private int _automatedPhysicalHeight;
    private string? _automatedMetadataPath;
    private string _automatedScenarioName = string.Empty;
    private string _automatedThemeName = "Dark";
    private Window? _automatedAuxiliaryWindow;
    private ContextMenu? _automatedContextMenu;
    private ToolTip? _automatedToolTip;
    private Popup? _automatedPopup;

    private void ConfigureAutomatedDpiAcceptance(JsonElement root)
    {
        var scale = 0d;
        _automatedDpiAcceptanceEnabled = root.TryGetProperty("DpiScale", out var scaleElement) && scaleElement.TryGetDouble(out scale) && scale > 0;
        if (!_automatedDpiAcceptanceEnabled) return;

        _automatedDpiScale = scale;
        _automatedPhysicalWidth = root.TryGetProperty("PhysicalWidth", out var widthElement) ? widthElement.GetInt32() : 2560;
        _automatedPhysicalHeight = root.TryGetProperty("PhysicalHeight", out var heightElement) ? heightElement.GetInt32() : 1440;
        _automatedMetadataPath = root.TryGetProperty("MetadataPath", out var metadataElement) ? metadataElement.GetString() : null;
        _automatedScenarioName = root.TryGetProperty("State", out var stateElement) ? stateElement.GetString() ?? string.Empty : string.Empty;
        _automatedThemeName = root.TryGetProperty("Theme", out var themeElement) ? themeElement.GetString() ?? "Dark" : "Dark";
    }

    private async Task<bool> PrepareAutomatedDpiAcceptanceStateAsync(string? state)
    {
        if (!_automatedDpiAcceptanceEnabled || _viewModel is null || string.IsNullOrWhiteSpace(state)) return false;

        var demoDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KitaoPhotoSelector.UiReview",
            "DemoImages");
        var demoImages = Directory.Exists(demoDirectory)
            ? Directory.GetFiles(demoDirectory, "DPI_TEST_*.png").OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray()
            : [];

        if (state.StartsWith("WorkbenchCalendar", StringComparison.OrdinalIgnoreCase) || state.StartsWith("WorkbenchTaskCenter", StringComparison.OrdinalIgnoreCase))
        {
            _viewModel.NavigateCommand.Execute("Workbench");
            ApplyCalendarReviewState(state);
            if (state.StartsWith("WorkbenchCalendarHotfix", StringComparison.OrdinalIgnoreCase))
            {
                _taskCenterDrawerOpen = true;
                UpdateWorkbenchResponsiveLayout();
            }
            return true;
        }

        if (state.StartsWith("Calendar", StringComparison.OrdinalIgnoreCase) || state.StartsWith("CreateShoot", StringComparison.OrdinalIgnoreCase) || state.StartsWith("Documents", StringComparison.OrdinalIgnoreCase))
        {
            _viewModel.NavigateCommand.Execute("WorkCalendar");
            ApplyCalendarReviewState(state);
            if (state.StartsWith("CreateShoot", StringComparison.OrdinalIgnoreCase) || state.StartsWith("Documents", StringComparison.OrdinalIgnoreCase))
            {
                var editor = await CreateBookingEditorReviewStateAsync(state, demoDirectory).ConfigureAwait(true);
                _automatedAuxiliaryWindow = new Window
                {
                    Title = editor.DialogTitle,
                    Owner = this,
                    Content = new ShootBookingEditorView { DataContext = editor },
                    Width = 900,
                    Height = 780,
                    ShowInTaskbar = false,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Background = Application.Current.TryFindResource("WindowBackgroundBrush") as Brush
                };
                _automatedAuxiliaryWindow.Show();
            }
            return true;
        }

        if (state.StartsWith("Finance", StringComparison.OrdinalIgnoreCase))
        {
            _viewModel.NavigateCommand.Execute("Finance");
            _viewModel.FinancePage?.ApplyReviewState(state);
            return true;
        }

        if (state.StartsWith("DatePicker", StringComparison.OrdinalIgnoreCase) || state == "CalendarPopupDark" || state == "ComboBoxDark")
        {
            var panel = new StackPanel { Margin = new Thickness(22), Width = 420 };
            panel.Children.Add(new TextBlock { Text = state == "ComboBoxDark" ? "深色下拉控件" : "运行时日期选择器", FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) });
            if (state == "ComboBoxDark")
            {
                var combo = new ComboBox { ItemsSource = new[] { "全部排期", "未拍摄", "已拍摄", "待返图" }, SelectedIndex = 0, Width = 260, HorizontalAlignment = HorizontalAlignment.Left };
                panel.Children.Add(combo);
                _automatedAuxiliaryWindow = ReviewWindow(panel, 520, 300);
                _automatedAuxiliaryWindow.Show();
                combo.ApplyTemplate(); combo.IsDropDownOpen = true; combo.UpdateLayout();
                _automatedPopup = combo.Template.FindName("PART_Popup", combo) as Popup;
            }
            else
            {
                var picker = new DatePicker { SelectedDate = DateTime.Today, Width = 260, HorizontalAlignment = HorizontalAlignment.Left };
                panel.Children.Add(picker);
                _automatedAuxiliaryWindow = ReviewWindow(panel, 520, 520);
                _automatedAuxiliaryWindow.Show();
                picker.ApplyTemplate(); picker.IsDropDownOpen = true; picker.UpdateLayout();
                _automatedPopup = picker.Template.FindName("PART_Popup", picker) as Popup;
            }
            return true;
        }

        if (state.StartsWith("Tether", StringComparison.OrdinalIgnoreCase) || state.StartsWith("Lut", StringComparison.OrdinalIgnoreCase) || state.StartsWith("ColorProfile", StringComparison.OrdinalIgnoreCase) || state.StartsWith("ClientMonitor", StringComparison.OrdinalIgnoreCase) || state == "MixedDpi")
        {
            _viewModel.NavigateCommand.Execute("Tether");
            if (_viewModel.TetherPage is null) return false;
            var (assets, annotations) = CreateTetherReviewAssets(demoDirectory, state);
            _viewModel.TetherPage.ApplyReviewState(state, assets, annotations);
            return true;
        }

        switch (state)
        {
            case "WorkbenchDarkExpanded":
            case "WorkbenchDarkCollapsed":
            case "WorkbenchLight":
            case "WorkbenchHighContrast":
            case "WorkbenchCalendarRightTop":
            case "WorkbenchDpi150":
            case "WorkbenchDpi200":
            case "WorkbenchLegend":
            case "Workbench1280":
                ApplyCalendarReviewState(state);
                return true;
            case "WorkbenchPinStates":
                ApplyToolboxPinReviewState(true);
                _viewModel.NavigateCommand.Execute("Workbench");
                return true;
            case "LocalSplitHelp":
            case "LocalSplitHover":
                _viewModel.NavigateCommand.Execute("Workbench");
                return true;
            case "SettingsDialog":
                _viewModel.IsSettingsModalOpen = true;
                return true;
            case "ToolboxPopup":
            case "QuickToolsManager":
            case "FeedbackDialog":
            case "ConfirmationDialog":
            case "ContextMenu":
            case "ContextMenuDark":
            case "Tooltip":
                return true;
            case "ToastDark":
                _viewModel.NavigateCommand.Execute("Workbench");
                _viewModel.GetType().GetMethod("ShowToast", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.Invoke(_viewModel, ["主题通知：日期与资料已安全保存到本机。"]);
                return true;
            case "ToolboxFullPage":
                _viewModel.NavigateCommand.Execute("Toolbox");
                return true;
            case "RuntimeToolboxPinned":
                ApplyToolboxPinReviewState(true);
                _viewModel.NavigateCommand.Execute("Toolbox");
                return true;
            case "RuntimeToolboxUnpinned":
                ApplyToolboxPinReviewState(false);
                _viewModel.NavigateCommand.Execute("Toolbox");
                return true;
            case "ToolboxClosedAfterSelection":
                _viewModel.NavigateCommand.Execute("PhotoGrouping");
                WorkbenchToolboxPopup.IsOpen = false;
                return true;
            case "CollectionNoOverlap":
                _viewModel.NavigateCommand.Execute("Workflow");
                return true;
            case "RuntimeCollectionEmpty":
                _viewModel.NavigateCommand.Execute("Workflow");
                _viewModel.GoToWorkflowStepCommand.Execute("1");
                return true;
            case "RuntimeCollectionImported":
                _viewModel.NavigateCommand.Execute("Workflow");
                _viewModel.GoToWorkflowStepCommand.Execute("2");
                _viewModel.TextInput = "DPI_TEST_001\nDPI_TEST_002";
                _viewModel.ParseTextCommand.Execute(null);
                return true;
            case "RuntimeLocalSplit":
                _viewModel.NavigateCommand.Execute("LocalSplit");
                return true;
            case "CompressNoOverlap":
                _viewModel.NavigateCommand.Execute("BatchCompress");
                return true;
            case "WatermarkNoOverlap":
                _viewModel.NavigateCommand.Execute("Watermark");
                return true;
            case "RuntimeWatermarkPreview":
                _viewModel.NavigateCommand.Execute("Watermark");
                return true;
            case "LicenseNoOverlap":
                _viewModel.NavigateCommand.Execute("Activation");
                return true;
            case "OrganizeEmpty":
                _viewModel.NavigateCommand.Execute("PhotoGrouping");
                return true;
            case "OrganizeNoOverlap":
                _viewModel.NavigateCommand.Execute("PhotoGrouping");
                await _viewModel.OrganizePhotosPage.AddPathsAsync(demoImages);
                return true;
            case "OrganizeGrouped":
            case "OrganizeManifest":
                _viewModel.NavigateCommand.Execute("PhotoGrouping");
                await _viewModel.OrganizePhotosPage.AddPathsAsync(demoImages);
                if (state == "OrganizeManifest")
                {
                    _viewModel.OrganizePhotosPage.OutputPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "KitaoPhotoSelector.UiReview",
                        "OrganizeOutput");
                    if (_viewModel.OrganizePhotosPage.PreviewPlanCommand.CanExecute(null))
                        _viewModel.OrganizePhotosPage.PreviewPlanCommand.Execute(null);
                }
                return true;
            case "CollageEmpty":
                _viewModel.NavigateCommand.Execute("Collage");
                return true;
            case "Collage2x2":
            case "CollageVertical":
            case "CollageExport":
                _viewModel.NavigateCommand.Execute("Collage");
                _viewModel.CollagePage.AddPaths(demoImages);
                if (state == "Collage2x2")
                {
                    _viewModel.CollagePage.Mode = CollageMode.Template;
                    _viewModel.CollagePage.SelectedTemplate = CollageTemplateCatalog.Get("4-grid");
                    _viewModel.CollagePage.AspectRatio = "1:1";
                }
                else if (state == "CollageVertical")
                {
                    _viewModel.CollagePage.Mode = CollageMode.VerticalStrip;
                    _viewModel.CollagePage.AspectRatio = "9:16";
                    _viewModel.CollagePage.Spacing = 18;
                }
                else
                {
                    _viewModel.CollagePage.Mode = CollageMode.Template;
                    _viewModel.CollagePage.SelectedTemplate = CollageTemplateCatalog.Get("4-grid");
                    _viewModel.CollagePage.AspectRatio = "4:5";
                    _viewModel.CollagePage.BorderWidth = 5;
                    _viewModel.CollagePage.Shadow = true;
                    _viewModel.CollagePage.BackgroundColor = "#24364B";
                    _viewModel.CollagePage.Project.Export.Format = "PNG";
                }
                return true;
            default:
                return false;
        }
    }

    private void ApplyCalendarReviewState(string state)
    {
        if (_viewModel is null) return;
        var calendar = _viewModel.WorkCalendarPage;
        var items = new List<ShootBookingSummary>();
        var weather = new Dictionary<Guid, BookingWeatherSummary?>();
        if (!string.Equals(state, "CalendarEmptyMonth", StringComparison.OrdinalIgnoreCase) && !string.Equals(state, "CalendarEmptyDates", StringComparison.OrdinalIgnoreCase))
        {
            var zone = TimeZoneInfo.Local;
            var specifications = new[]
            {
                (Offset: 0, Hour: 9, Duration: 2, Status: ShootBookingStatus.Confirmed, Title: "品牌人像拍摄", Location: "一号影棚", WeatherCode: "1"),
                (Offset: 0, Hour: 10, Duration: 2, Status: ShootBookingStatus.Preparing, Title: "新品静物拍摄", Location: "二号影棚", WeatherCode: "61"),
                (Offset: 1, Hour: 14, Duration: 3, Status: ShootBookingStatus.Completed, Title: "服装目录拍摄", Location: "合成外景地", WeatherCode: "2"),
                (Offset: 2, Hour: 9, Duration: 1, Status: ShootBookingStatus.AwaitingDelivery, Title: "演员定妆照", Location: "三号影棚", WeatherCode: "3"),
                (Offset: 3, Hour: 11, Duration: 2, Status: ShootBookingStatus.Delivered, Title: "活动主视觉", Location: "会议中心", WeatherCode: "0"),
                (Offset: 5, Hour: 15, Duration: 2, Status: ShootBookingStatus.Postponed, Title: "场地勘景", Location: "合成场地", WeatherCode: "45"),
                (Offset: 7, Hour: 10, Duration: 4, Status: ShootBookingStatus.Cancelled, Title: "样片补拍", Location: "四号影棚", WeatherCode: "80")
            };
            foreach (var specification in specifications)
            {
                var id = Guid.Parse($"23020000-0000-0000-0000-{items.Count + 1:000000000000}");
                var localStart = DateTime.SpecifyKind(DateTime.Today.AddDays(specification.Offset).AddHours(specification.Hour), DateTimeKind.Unspecified);
                var localEnd = localStart.AddHours(specification.Duration);
                var start = new DateTimeOffset(localStart, zone.GetUtcOffset(localStart)).ToUniversalTime();
                var end = new DateTimeOffset(localEnd, zone.GetUtcOffset(localEnd)).ToUniversalTime();
                items.Add(new ShootBookingSummary(id, null, specification.Title, "合成演示客户", start, end, zone.Id, false,
                    specification.Status, specification.Location, "Commercial", false, false));
                var representative = new HourlyWeatherForecast(start, specification.WeatherCode, 24, 25, specification.WeatherCode == "61" ? 70 : 10, 0, 12, 20, 55, 30, 10000);
                var risks = specification.WeatherCode == "61" ? new[] { new WeatherRiskNotice("Rain", "降雨风险", NotificationSeverity.Warning) } : [];
                weather[id] = new BookingWeatherSummary(id, WeatherAvailability.Cached,
                    new WeatherLocation("合成演示地点", null, "CN", 0, 0, zone.Id, "UI review"), representative, null,
                    [representative], [], risks, DateTimeOffset.UtcNow, "UI review", true, false, false, "合成天气，仅用于界面验收");
            }
        }

        var selectedOffset = state switch
        {
            "WorkbenchCalendarShot" => 1,
            "WorkbenchCalendarPendingReturn" => 2,
            "WorkbenchCalendarReturned" => 3,
            "WorkbenchCalendarSelected" => 2,
            "WorkbenchCalendarNumberVisible" => 5,
            "WorkbenchCalendarDarkTheme" => 1,
            "WorkbenchCalendarFree" => 10,
            _ => 0
        };
        var selectedDate = DateTime.Today.AddDays(selectedOffset);
        calendar.Month.Configure(DateTime.Today, items, selectedDate, weather);
        calendar.Week.Configure(WorkCalendarViewModel.StartOfWeek(DateTime.Today), items, DateTime.Today, weather);
        calendar.Day.Configure(DateTime.Today, items, weather);
        calendar.DaySchedule.Configure(selectedDate, items.Where(item => CalendarBookingItemViewModel.SpansDate(item, selectedDate)).ToArray(), weather);
        if (state is "CalendarDayClosed" or "CalendarClosedBookingDay")
        {
            var index = calendar.Month.Days.ToList().FindIndex(day => day.Date == DateTime.Today);
            if (index >= 0)
            {
                var current = calendar.Month.Days[index];
                var closed = new MonthDayViewModel
                {
                    Date = current.Date,
                    IsCurrentMonth = current.IsCurrentMonth,
                    VisualState = current.VisualState is { } visual ? visual with { IsClosed = true } : null,
                    OverflowCount = current.OverflowCount,
                    HasConflict = current.HasConflict
                };
                foreach (var booking in current.VisibleBookings) closed.VisibleBookings.Add(booking);
                calendar.Month.Days[index] = closed;
            }
        }
        var statusText = state switch
        {
            "CalendarEmptyMonth" => "RC2 运行时验收：当前月份没有拍摄排期。",
            "CalendarStatusColors" => "RC2 运行时验收：状态颜色、数量、冲突和天气标记。",
            "CalendarSelectedDay" => "RC2 运行时验收：已选择今天并同步右侧日期详情。",
            "CalendarHeaderLayout" => "RC5 UI补丁：完整日历工具栏按功能分组并保持响应式间距。",
            "CalendarYearMonthSpacing" => "RC5 UI补丁：年份与月份使用独立标签和稳定间距。",
            "WorkbenchCalendarDarkTheme" => "RC5 UI补丁：迷你日历状态格在深色主题下保持清晰对比。",
            "WorkbenchCalendarFree" => "RC5 UI补丁：空闲日期使用灰色日期数字格。",
            "WorkbenchCalendarScheduled" => "RC5 UI补丁：待拍摄日期使用红色日期数字格。",
            "WorkbenchCalendarShot" => "RC5 UI补丁：已拍摄日期使用绿色日期数字格。",
            "WorkbenchCalendarPendingReturn" => "RC5 UI补丁：待返片日期使用黄色日期数字格。",
            "WorkbenchCalendarReturned" => "RC5 UI补丁：已返片日期使用蓝色日期数字格。",
            "WorkbenchCalendarNumberVisible" => "RC5 UI补丁：日期数字在状态格内完整可见。",
            "WorkbenchCalendarToday" => "RC5 UI补丁：今天描边与状态背景保持独立。",
            "WorkbenchCalendarSelected" => "RC5 UI补丁：选择描边与今天描边保持独立。",
            "WorkbenchTaskCenterEmpty" => "RC5 UI补丁：任务中心空状态固定在内容区。",
            "WorkbenchTaskCenter5Tasks" => "RC5 UI补丁：任务中心展示来源、进度、状态和更新时间。",
            "WorkbenchTaskCenter20Tasks" => "RC5 UI补丁：任务中心中间列表支持滚动。",
            "WorkbenchTaskCenterScrolled" => "RC5 UI补丁：任务中心滚动后页头和页脚保持固定。",
            "CalendarCreateButton" => "RC2 运行时验收：加号默认使用当前选中日期。",
            "CalendarDayDetails" => "RC2 运行时验收：日期详情与月历使用同一数据源。",
            "CalendarCompleted" => "RC2 运行时验收：完成状态保留在日历中且不自动归档。",
            "CalendarArchived" => "RC2 运行时验收：归档入口独立，恢复不删除排期数据。",
            _ => "RC2 运行时验收：工作台与完整工作日历共享正式日历模型。"
        };
        typeof(WorkCalendarViewModel).GetProperty(nameof(WorkCalendarViewModel.StatusText))?.SetValue(calendar, statusText);
    }

    private async Task<ShootBookingEditorViewModel> CreateBookingEditorReviewStateAsync(string state, string demoDirectory)
    {
        var calendar = _viewModel!.WorkCalendarPage;
        var bookingService = PrivateField<IShootBookingService>(calendar, "_bookingService");
        var projectRepository = PrivateField<IProjectRepository>(calendar, "_projectRepository");
        var people = PrivateField<IBookingPeopleService?>(calendar, "_bookingPeopleService");
        var documents = PrivateField<IBookingDocumentWorkflowService?>(calendar, "_documentWorkflow");
        var dialogs = PrivateField<IDialogService?>(calendar, "_dialogs");
        var editor = new ShootBookingEditorViewModel(bookingService, projectRepository, suggestedStart: DateTime.Today.AddHours(9), peopleService: people, documentWorkflow: documents, dialogs: dialogs);
        await editor.InitializeAsync().ConfigureAwait(true);
        editor.Title = state switch
        {
            "CreateShootBasic" => string.Empty,
            "CreateShootTimeLocation" => "RC2 时间与地点验收",
            "CreateShootWeather" => "RC2 天气降级验收",
            "CreateShootDocuments" => "RC2 本地资料关联验收",
            "CreateShootStaff" => "RC2 工作人员边界验收",
            "CreateShootConflict" => "RC2 时间冲突验收",
            "CreateShootSaved" => "RC2 保存同步验收",
            _ => "RC3 合成拍摄任务"
        };
        editor.ClientDisplayName = "合成演示客户";
        editor.StartDate = DateTime.Today;
        editor.EndDate = DateTime.Today;
        editor.StartTimeText = "09:30";
        editor.EndTimeText = "12:00";
        editor.Location = state == "CreateShootWeather" ? "合成外景地（天气仅作界面验收）" : state == "CreateShootTimeLocation" ? "RC2 合成影棚" : "一号影棚";
        editor.ShootingRequirements = state == "CreateShootDocuments" ? "资料关联：策划案、拍摄协议和灯光图均使用隔离合成文件。" : state == "CreateShootWeather" ? "天气不可用时仍允许保存；远期天气不会编造。" : "主光、背景与机位按合成验收方案准备。";
        editor.PreparationNotes = state == "CreateShootStaff" ? "工作人员：摄影、灯光、造型；当前 Schema 3 不新增工作人员表。" : state == "CreateShootSaved" ? "保存后刷新月历、选中日期详情、今日拍摄和未来 7 天。" : "仅使用合成资料，不含真实客户信息。";
        editor.Notes = "RC3 UI review / synthetic data";
        editor.TotalAmountText = "6800.00";
        editor.DepositAmountText = "2000.00";
        editor.PaidAmountText = "2000.00";
        if (state == "CreateShootConflict")
        {
            var start = new DateTimeOffset(DateTime.Today.AddHours(10));
            editor.Conflicts.Add(new BookingConflictViewModel(new BookingConflict(
                Guid.Parse("23020000-0000-0000-0000-000000000099"), "已存在的合成拍摄", "合成演示客户",
                start, start.AddHours(2), "二号影棚", ShootBookingStatus.Confirmed, TimeSpan.FromHours(1), false, true)));
            SetPrivateField(editor, "_isConflictVisible", true);
            SetPrivateField(editor, "_validationText", "检测到时间冲突。请选择返回修改、仍然保存，或明确允许重叠。");
        }
        editor.CurrentStep = state switch
        {
            "CreateShootStep2" => 2,
            "CreateShootStep3" or "DocumentsImages" or "DocumentsPdf" or "DocumentsText" or "DocumentsUnsupported" => 3,
            "CreateShootStep4" or "CreateShootContacts" or "CreateShootStaff" => 4,
            _ => 1
        };
        if (state == "CreateShootContacts")
        {
            editor.Contacts.Add(new() { DisplayName = "合成客户代号 A", Phone = "138****0000", WeChat = "synthetic_wechat", Email = "demo@example.invalid", IsPrimary = true, Note = "仅用于 RC3 隔离界面验收" });
            editor.Contacts.Add(new() { DisplayName = "合成模特代号 B", OtherContact = "经纪人转达", Note = "不含真实联系方式" });
        }
        if (state == "CreateShootStaff")
        {
            editor.Staff.Add(new() { DisplayName = "合成摄影师", SelectedRole = editor.StaffRoleOptions.First(item => item.Value == BookingStaffRole.Photographer), ArrivalTimeText = $"{DateTime.Today:yyyy-MM-dd} 08:30", Note = "主机位" });
            editor.Staff.Add(new() { DisplayName = "合成灯光师", SelectedRole = editor.StaffRoleOptions.First(item => item.Value == BookingStaffRole.LightingTechnician), ArrivalTimeText = $"{DateTime.Today:yyyy-MM-dd} 08:00", Note = "灯光图已关联" });
        }
        if (state.StartsWith("Documents", StringComparison.OrdinalIgnoreCase)) editor.Documents?.ApplyReviewState(state, demoDirectory);
        return editor;
    }

    private Window ReviewWindow(FrameworkElement content, double width, double height) => new()
    {
        Title = "像素蛋挞 RC3 隔离验收", Owner = this, Content = content, Width = width, Height = height,
        ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Background = Application.Current.TryFindResource("WindowBackgroundBrush") as Brush
    };

    private static T PrivateField<T>(object instance, string name) =>
        (T)(instance.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(instance)
            ?? throw new InvalidOperationException($"Review field not found: {name}"));

    private static void SetPrivateField(object instance, string name, object value)
    {
        var field = instance.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Review field not found: {name}");
        field.SetValue(instance, value);
    }

    private static (IReadOnlyList<TetherAssetRecord> Assets, IReadOnlyDictionary<Guid, TetherAnnotationRecord> Annotations) CreateTetherReviewAssets(
        string demoDirectory,
        string state)
    {
        if (state is "TetherEmpty" or "TetherWaiting" or "TetherNoPhotoFullscreen")
            return ([], new Dictionary<Guid, TetherAnnotationRecord>());

        var sessionId = Guid.Parse("23000000-0000-0000-0000-000000000003");
        var now = DateTimeOffset.UtcNow;
        var imagePaths = Directory.Exists(demoDirectory)
            ? Directory.GetFiles(demoDirectory, "STAGEC_*.png").OrderBy(path => path, StringComparer.OrdinalIgnoreCase).Take(12).ToArray()
            : [];
        var records = new List<TetherAssetRecord>();
        var rawId = Guid.Parse("23000000-0000-0000-0000-000000000099");
        var requestedCount = state switch
        {
            "TetherAssets1000" => 999,
            "TetherBurst" => 99,
            _ => imagePaths.Length
        };
        for (var index = 0; index < requestedCount && imagePaths.Length > 0; index++)
        {
            var id = Guid.Parse($"23000000-0000-0000-0000-{index + 1:000000000000}");
            var path = imagePaths[index % imagePaths.Length];
            var file = new FileInfo(path);
            records.Add(new(
                id, sessionId, null, path, path.ToUpperInvariant(), file.Name, file.Extension,
                TetherMediaKind.PreviewImage, file.Exists ? file.Length : null, file.Exists ? file.LastWriteTimeUtc : null,
                now.AddSeconds(-index * 12), TetherStabilityState.Stable,
                index == 7 ? TetherProcessingState.NeedsAttention : TetherProcessingState.Ready,
                TetherPreviewState.Ready, now.AddSeconds(-index * 12), now.AddSeconds(-index * 12),
                PairingKey: index == 0 ? "STAGEC_PAIR" : null, PairedAssetId: index == 0 ? rawId : null,
                LastErrorCode: index == 7 ? "TETHER_SOURCE_TEMPORARILY_UNAVAILABLE" : null));
        }

        var rawPath = Path.Combine(demoDirectory, "STAGEC_RAW.nef");
        var rawFile = new FileInfo(rawPath);
        records.Add(new(
            rawId, sessionId, null, rawPath, rawPath.ToUpperInvariant(), rawFile.Name, rawFile.Extension,
            TetherMediaKind.Raw, rawFile.Exists ? rawFile.Length : null, rawFile.Exists ? rawFile.LastWriteTimeUtc : null,
            now.AddSeconds(-160), TetherStabilityState.Stable, TetherProcessingState.Ready, TetherPreviewState.Placeholder,
            now.AddSeconds(-160), now.AddSeconds(-160), PairingKey: "STAGEC_RAW_UNPAIRED"));

        var annotations = new Dictionary<Guid, TetherAnnotationRecord>();
        for (var index = 0; index < Math.Min(5, records.Count); index++)
        {
            var asset = records[index];
            annotations[asset.Id] = new(
                Guid.Parse($"23000000-0000-0000-0001-{index + 1:000000000000}"), asset.Id,
                index == 0 ? 5 : 4 - index % 3, index % 2 == 0 ? "绿" : "蓝",
                index == 0 ? "主光位置确认，保留这一张。" : null, now, now,
                ClientFavorite: index is 0 or 2, ClientNote: index == 0 ? "客户现场收藏" : null, IsRejected: index == 4);
        }
        return (records, annotations);
    }

    private void FinalizeAutomatedDpiAcceptanceState(string? state)
    {
        if (!_automatedDpiAcceptanceEnabled || _viewModel is null || string.IsNullOrWhiteSpace(state)) return;

        if (state is "ToolboxPopup" or "WorkbenchPinStates")
        {
            WorkbenchToolboxPopup.IsOpen = true;
        }
        else if (state == "QuickToolsManager")
        {
            _automatedAuxiliaryWindow = new QuickToolsManagerWindow(_viewModel.Settings.PinnedQuickTools)
            {
                Owner = this,
                ShowInTaskbar = false
            };
            _automatedAuxiliaryWindow.Show();
        }
        else if (state == "FeedbackDialog")
        {
            var feedbackService = new FeedbackService(
                new FeedbackRequestBuilder().Build(),
                new WpfFeedbackClipboard(),
                new ShellFeedbackMailLauncher(),
                new FileLogService());
            _automatedAuxiliaryWindow = new FeedbackDialog(feedbackService)
            {
                Owner = this,
                ShowInTaskbar = false
            };
            _automatedAuxiliaryWindow.Show();
        }
        else if (state == "ConfirmationDialog")
        {
            _automatedAuxiliaryWindow = new UpgradeTutorialWindow
            {
                Owner = this,
                ShowInTaskbar = false
            };
            _automatedAuxiliaryWindow.Show();
        }
        else if (state is "ContextMenu" or "ContextMenuDark")
        {
            _viewModel.SetQuickToolsCompact(false);
            QuickToolsOverflowButton.Visibility = Visibility.Collapsed;
            Grid.SetColumnSpan(PinnedQuickToolsList, 3);
            PinnedQuickToolsList.UpdateLayout();
            var target = FindVisualChildren<Button>(PinnedQuickToolsList)
                .FirstOrDefault(button => button.ContextMenu is not null);
            if (target?.ContextMenu is not null)
            {
                _automatedContextMenu = target.ContextMenu;
                _automatedContextMenu.PlacementTarget = target;
                _automatedContextMenu.Placement = PlacementMode.Bottom;
                _automatedContextMenu.IsOpen = true;
            }
        }
        else if (state == "Tooltip")
        {
            _automatedToolTip = new ToolTip
            {
                Content = ToolboxQuickButton.ToolTip?.ToString() ?? "打开全部照片工具",
                PlacementTarget = ToolboxQuickButton,
                Placement = PlacementMode.Bottom,
                IsOpen = true
            };
        }
        else if (state == "LocalSplitHelp")
        {
            _automatedToolTip = LocalSplitHelpToolTip;
            _automatedToolTip.PlacementTarget = LocalSplitHelpButton;
            _automatedToolTip.Placement = PlacementMode.Bottom;
            _automatedToolTip.IsOpen = true;
        }
        else if (state == "LocalSplitHover")
        {
            LocalSplitHelpButton.Focus();
        }
        else if (state == "CalendarViewMenu")
        {
            var combo = FindVisualChildren<ComboBox>(RootGrid).FirstOrDefault(item => string.Equals(AutomationProperties.GetName(item), "工作日历视图", StringComparison.Ordinal));
            if (combo is not null)
            {
                combo.ApplyTemplate(); combo.IsDropDownOpen = true; combo.UpdateLayout();
                _automatedPopup = combo.Template.FindName("PART_Popup", combo) as Popup;
            }
        }
        else if (state is "CalendarContextMenu" or "CalendarBookingContextMenu" or "WorkbenchCalendarContextMenu")
        {
            FrameworkElement? target = state switch
            {
                "CalendarBookingContextMenu" => FindVisualChildren<Button>(RootGrid)
                    .FirstOrDefault(item => item.DataContext is CalendarBookingItemViewModel && item.ContextMenu is not null),
                "WorkbenchCalendarContextMenu" => FindVisualChildren<Border>(RootGrid)
                    .FirstOrDefault(item => string.Equals(item.Name, "CalendarDayCell", StringComparison.Ordinal) && item.ContextMenu is not null),
                _ => FindVisualChildren<Border>(RootGrid)
                    .FirstOrDefault(item => item.DataContext is MonthDayViewModel && item.ContextMenu is not null)
            };
            if (target?.ContextMenu is not null)
            {
                _automatedContextMenu = target.ContextMenu;
                _automatedContextMenu.PlacementTarget = target;
                _automatedContextMenu.IsOpen = true;
            }
        }
        else if (state.StartsWith("ClientMonitor", StringComparison.OrdinalIgnoreCase) || state == "MixedDpi")
        {
            var color = _viewModel.TetherPage?.ColorSettings;
            var client = new ClientMonitorViewModel
            {
                DisplayImage = color?.VisibleImage,
                FollowMode = state switch
                {
                    "ClientMonitorFollowLatest" => ClientMonitorFollowMode.FollowLatest,
                    "ClientMonitorLocked" => ClientMonitorFollowMode.Locked,
                    _ => ClientMonitorFollowMode.FollowMainSelection
                },
                ShowIdentifier = false,
                ShowTechnicalMetadata = false,
                ShowRating = state == "ClientMonitorFavoriteNote",
                ShowClientControls = state is not "ClientMonitorDisconnected" and not "ClientMonitorReconnected",
                IsFavorite = state == "ClientMonitorFavoriteNote",
                ClientNote = state == "ClientMonitorFavoriteNote" ? "喜欢服装和光线" : null,
                NewAssetCount = state == "ClientMonitorLocked" ? 3 : 0,
                StatusText = state switch
                {
                    "ClientMonitorDisconnected" => "客户显示器未连接 · 联机会话继续",
                    "ClientMonitorReconnected" => "客户显示器已重新连接 · 可恢复监看",
                    "ClientMonitorPrivacy" => "隐私默认：文件名与路径隐藏",
                    "MixedDpi" => "客户屏150% · 独立ICC",
                    _ => "客户监看开发版验证"
                }
            };
            _automatedAuxiliaryWindow = new ClientMonitorWindow { DataContext = client, Width = state == "MixedDpi" ? 900 : 1120, Height = state == "MixedDpi" ? 760 : 700, ShowInTaskbar = false };
            _automatedAuxiliaryWindow.Show();
        }

        _automatedAuxiliaryWindow?.UpdateLayout();
        _automatedContextMenu?.UpdateLayout();
        _automatedToolTip?.UpdateLayout();
        _automatedPopup?.Child?.UpdateLayout();
    }

    private void ApplyToolboxPinReviewState(bool pinned)
    {
        if (_viewModel is null) return;
        var ids = pinned ? new[] { "Workflow", "PhotoOrganize", "BatchCompress" } : Array.Empty<string>();
        _viewModel.Settings.PinnedQuickTools = ids.ToList();
        _viewModel.Settings.QuickToolLayout.OrderedToolIds = ids.ToList();
        foreach (var item in _viewModel.ToolboxItems)
            item.SetPinned(pinned && ids.Contains(item.Id, StringComparer.OrdinalIgnoreCase));
    }

    private void CaptureAutomatedDpiFrame(string outputPath)
    {
        RootGrid.UpdateLayout();
        if (RootGrid.ActualWidth <= 0 || RootGrid.ActualHeight <= 0) return;

        var physicalWidth = Math.Max(1, _automatedPhysicalWidth);
        var physicalHeight = Math.Max(1, _automatedPhysicalHeight);
        var scale = Math.Max(.25, _automatedDpiScale);
        var logicalWidth = physicalWidth / scale;
        var logicalHeight = physicalHeight / scale;
        var composition = new DrawingVisual();
        using (var drawing = composition.RenderOpen())
        {
            drawing.DrawRectangle(ResolveBackgroundBrush(), null, new Rect(0, 0, physicalWidth, physicalHeight));
            drawing.PushTransform(new ScaleTransform(scale, scale));
            drawing.DrawRectangle(new VisualBrush(RootGrid), null, new Rect(0, 0, RootGrid.ActualWidth, RootGrid.ActualHeight));
            var toolboxPopupChild = string.Equals(_automatedScenarioName, "ToolboxPopup", StringComparison.OrdinalIgnoreCase)
                ? WorkbenchToolboxPopup.Child as FrameworkElement
                : WorkbenchToolboxPopup.IsOpen ? WorkbenchToolboxPopup.Child as FrameworkElement : null;
            DrawPopup(drawing, toolboxPopupChild, logicalWidth, logicalHeight, .63, .12);
            DrawPopup(drawing, QuickToolsOverflowPopup.IsOpen ? QuickToolsOverflowPopup.Child as FrameworkElement : null, logicalWidth, logicalHeight, .58, .12);
            DrawPopup(drawing, _automatedContextMenu, logicalWidth, logicalHeight, .46, .17);
            DrawPopup(drawing, _automatedToolTip, logicalWidth, logicalHeight, .60, .15);
            DrawPopup(drawing, _automatedPopup?.Child as FrameworkElement, logicalWidth, logicalHeight, .44, .18);
            DrawAuxiliaryWindow(drawing, logicalWidth, logicalHeight);
            drawing.Pop();
        }

        var bitmap = new RenderTargetBitmap(physicalWidth, physicalHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(composition);
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var temporaryPath = outputPath + ".tmp";
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(temporaryPath)) encoder.Save(stream);
        File.Move(temporaryPath, outputPath, true);

        WriteAutomatedDpiMetadata(outputPath, logicalWidth, logicalHeight);
        CloseAutomatedDpiOverlays();
    }

    private Brush ResolveBackgroundBrush() =>
        RootGrid.Background is SolidColorBrush brush ? new SolidColorBrush(brush.Color) : Brushes.Black;

    private static void DrawPopup(DrawingContext drawing, FrameworkElement? popup, double logicalWidth, double logicalHeight, double xRatio, double yRatio)
    {
        if (popup is null) return;
        popup.UpdateLayout();
        if (popup.ActualWidth <= 0 || popup.ActualHeight <= 0)
        {
            popup.Measure(new Size(logicalWidth, logicalHeight));
            popup.Arrange(new Rect(popup.DesiredSize));
        }
        var width = Math.Min(popup.ActualWidth > 0 ? popup.ActualWidth : popup.DesiredSize.Width, logicalWidth - 24);
        var height = Math.Min(popup.ActualHeight > 0 ? popup.ActualHeight : popup.DesiredSize.Height, logicalHeight - 24);
        var x = Math.Clamp(logicalWidth * xRatio, 12, Math.Max(12, logicalWidth - width - 12));
        var y = Math.Clamp(logicalHeight * yRatio, 12, Math.Max(12, logicalHeight - height - 12));
        drawing.DrawRectangle(new VisualBrush(popup), null, new Rect(x, y, width, height));
    }

    private void DrawAuxiliaryWindow(DrawingContext drawing, double logicalWidth, double logicalHeight)
    {
        if (_automatedAuxiliaryWindow?.Content is not FrameworkElement content) return;
        _automatedAuxiliaryWindow.UpdateLayout();
        content.UpdateLayout();
        var width = Math.Min(
            content.ActualWidth > 0 ? content.ActualWidth : Math.Max(320, _automatedAuxiliaryWindow.Width),
            logicalWidth - 32);
        var height = Math.Min(
            content.ActualHeight > 0 ? content.ActualHeight : Math.Max(220, _automatedAuxiliaryWindow.Height),
            logicalHeight - 32);
        var x = Math.Max(16, (logicalWidth - width) / 2);
        var y = Math.Max(16, (logicalHeight - height) / 2);
        drawing.DrawRectangle(new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), null, new Rect(0, 0, logicalWidth, logicalHeight));
        drawing.DrawRectangle(new VisualBrush(content), null, new Rect(x, y, width, height));
    }

    private void WriteAutomatedDpiMetadata(string outputPath, double logicalWidth, double logicalHeight)
    {
        FrameworkElement layoutRoot = string.Equals(_automatedScenarioName, "SettingsDialog", StringComparison.OrdinalIgnoreCase)
            ? SettingsModal
            : string.Equals(_automatedScenarioName, "Settings", StringComparison.OrdinalIgnoreCase)
                ? SettingsPageContent
                : IsTetherColorReviewState(_automatedScenarioName)
                    ? TetherMonitorView
                    : RootGrid;
        var layout = InspectLayout(layoutRoot, layoutRoot.ActualWidth, layoutRoot.ActualHeight);
        var auxiliary = _automatedAuxiliaryWindow?.Content is FrameworkElement content
            ? InspectLayout(content, content.ActualWidth, content.ActualHeight)
            : null;
        var themeInspection = InspectThemeResources();
        var miniCalendarInspection = _automatedScenarioName.StartsWith("WorkbenchCalendar", StringComparison.OrdinalIgnoreCase)
            ? InspectMiniCalendarLayout()
            : null;
        var miniCalendarScope = _automatedScenarioName.StartsWith("WorkbenchCalendarHotfix", StringComparison.OrdinalIgnoreCase);
        var metadata = new
        {
            scenario = _automatedScenarioName,
            theme = _automatedThemeName,
            validationMode = "automated-logical-simulation",
            physicalDpiManuallyTested = false,
            targetDpiX = 96d * _automatedDpiScale,
            targetDpiY = 96d * _automatedDpiScale,
            scale = _automatedDpiScale,
            physicalViewport = new { width = _automatedPhysicalWidth, height = _automatedPhysicalHeight },
            logicalViewport = new { width = logicalWidth, height = logicalHeight },
            rootActual = new { width = RootGrid.ActualWidth, height = RootGrid.ActualHeight },
            screenshot = outputPath,
            sourceCommit = ResolveSourceCommit(),
            layout,
            miniCalendarInspection,
            auxiliaryLayout = auxiliary,
            themeInspection,
            passed = miniCalendarScope
                ? (miniCalendarInspection?.Passed ?? false) && themeInspection.Passed
                : layout.BlockingIssueCount == 0 && (miniCalendarInspection?.Passed ?? true) && (auxiliary?.BlockingIssueCount ?? 0) == 0 && themeInspection.Passed,
            generatedAt = DateTimeOffset.Now
        };
        var metadataPath = string.IsNullOrWhiteSpace(_automatedMetadataPath) ? outputPath + ".json" : _automatedMetadataPath;
        var directory = Path.GetDirectoryName(metadataPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(metadataPath, JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
    }

    private MiniCalendarLayoutInspection InspectMiniCalendarLayout()
    {
        var panel = FindVisualChildren<WorkbenchCalendarPanel>(RootGrid).FirstOrDefault();
        if (panel is null) return new(false, 0, 0, 0, 0, 0, 0, 0, 0, ["Mini calendar panel was not rendered."]);
        panel.UpdateLayout();
        var cells = FindVisualChildren<Border>(panel).Where(item => item.Name == "CalendarDayCell").ToArray();
        var badges = FindVisualChildren<Border>(panel).Where(item => item.Name == "DayNumberBadge").ToArray();
        var texts = FindVisualChildren<TextBlock>(panel).Where(item => item.Name == "DayNumberText").ToArray();
        var issues = new List<string>();
        if (cells.Length != 42 || badges.Length != 42 || texts.Length != 42) issues.Add($"Expected 42 cells/badges/texts; found {cells.Length}/{badges.Length}/{texts.Length}.");
        var count = Math.Min(cells.Length, Math.Min(badges.Length, texts.Length));
        for (var index = 0; index < count; index++)
        {
            var cell = cells[index];
            var badge = badges[index];
            var text = texts[index];
            if (badge.ActualHeight + 4 > cell.ActualHeight) issues.Add($"Day {index + 1}: cell height does not preserve badge padding.");
            if (text.ActualHeight + 4 > badge.ActualHeight) issues.Add($"Day {index + 1}: badge height does not preserve text safe inset.");
            var badgeOrigin = badge.TransformToAncestor(cell).Transform(new Point());
            var textOrigin = text.TransformToAncestor(badge).Transform(new Point());
            if (badgeOrigin.Y < 0 || badgeOrigin.Y + badge.ActualHeight > cell.ActualHeight) issues.Add($"Day {index + 1}: badge exceeds cell bounds.");
            if (textOrigin.Y < 2 || textOrigin.Y + text.ActualHeight > badge.ActualHeight - 2) issues.Add($"Day {index + 1}: text exceeds badge safe inset.");
        }
        var orderedRows = cells.Select((cell, index) => new { cell, index }).GroupBy(item => item.index / 7).ToArray();
        for (var row = 0; row < orderedRows.Length - 1; row++)
        {
            var current = orderedRows[row].First().cell.TransformToAncestor(panel).Transform(new Point());
            var next = orderedRows[row + 1].First().cell.TransformToAncestor(panel).Transform(new Point());
            var gap = next.Y - (current.Y + orderedRows[row].First().cell.ActualHeight);
            if (gap < 4) issues.Add($"Rows {row + 1}/{row + 2}: spacing is {gap:F2} DIP.");
        }
        var detailsGap = 0d;
        if (cells.Length >= 36)
        {
            var lastCell = cells[35];
            var lastOrigin = lastCell.TransformToAncestor(panel).Transform(new Point());
            var detailsOrigin = panel.DayDetailsHeader.TransformToAncestor(panel).Transform(new Point());
            detailsGap = detailsOrigin.Y - (lastOrigin.Y + lastCell.ActualHeight);
            if (detailsGap < 16) issues.Add($"Last row/detail spacing is {detailsGap:F2} DIP.");
        }
        if (Math.Abs(panel.PreviousMonthButton.ActualWidth - panel.NextMonthButton.ActualWidth) > .1 || Math.Abs(panel.PreviousMonthButton.ActualHeight - panel.NextMonthButton.ActualHeight) > .1)
            issues.Add("Month navigation button dimensions differ.");
        if (panel.PreviousMonthButton.ActualHeight > 32 || panel.NextMonthButton.ActualHeight > 32) issues.Add("Month navigation buttons exceed 32 DIP.");
        var badgeHeights = badges.Select(item => item.ActualHeight).ToArray();
        if (badgeHeights.Length > 0 && badgeHeights.Max() - badgeHeights.Min() > .1) issues.Add("Badge heights differ.");
        return new(
            issues.Count == 0,
            cells.FirstOrDefault()?.ActualWidth ?? 0,
            cells.FirstOrDefault()?.ActualHeight ?? 0,
            badges.FirstOrDefault()?.ActualWidth ?? 0,
            badges.FirstOrDefault()?.ActualHeight ?? 0,
            texts.FirstOrDefault()?.ActualHeight ?? 0,
            panel.PreviousMonthButton.ActualWidth,
            panel.PreviousMonthButton.ActualHeight,
            detailsGap,
            issues);
    }

    private sealed record MiniCalendarLayoutInspection(
        bool Passed,
        double DayCellActualWidth,
        double DayCellActualHeight,
        double DayNumberBadgeActualWidth,
        double DayNumberBadgeActualHeight,
        double DayNumberTextActualHeight,
        double MonthButtonActualWidth,
        double MonthButtonActualHeight,
        double LastRowToDetailsSpacing,
        IReadOnlyList<string> Issues);

    private LayoutInspection InspectLayout(FrameworkElement root, double viewportWidth, double viewportHeight)
    {
        root.UpdateLayout();
        var elements = FindVisualChildren<FrameworkElement>(root)
            .Prepend(root)
            .Where(element => element.IsVisible && element.ActualWidth > 0 && element.ActualHeight > 0)
            .Distinct()
            .ToArray();
        var bounds = new List<ElementBounds>();
        var overflow = new List<string>();
        var zeroSized = new List<string>();
        var textClipping = new List<string>();
        var whiteSurface = new List<string>();

        foreach (var element in elements)
        {
            Rect rect;
            try
            {
                var origin = element.TransformToAncestor(root).Transform(new Point(0, 0));
                rect = new Rect(origin.X, origin.Y, element.ActualWidth, element.ActualHeight);
            }
            catch
            {
                continue;
            }

            var identity = ElementIdentity(element);
            bounds.Add(new ElementBounds(identity, element.GetType().Name, rect.X, rect.Y, rect.Width, rect.Height));
            if (element.TemplatedParent is null &&
                (rect.Left < -2 || rect.Top < -2 || rect.Right > viewportWidth + 2 || rect.Bottom > viewportHeight + 2) &&
                !HasClippingAncestor(element, root))
                overflow.Add(identity);

            if (IsInteractive(element) && (element.ActualWidth < 1 || element.ActualHeight < 1)) zeroSized.Add(identity);
            if (element is TextBlock textBlock && IsTextClipped(textBlock)) textClipping.Add(identity);
            if (string.Equals(_automatedThemeName, "Dark", StringComparison.OrdinalIgnoreCase) && element is Control control && HasUnexpectedWhiteSurface(control))
                whiteSurface.Add(identity);
        }

        foreach (var interactive in FindVisualChildren<FrameworkElement>(root).Where(IsInteractive).Where(element => element.IsVisible && element.TemplatedParent is null))
        {
            if (interactive.ActualWidth < 1 || interactive.ActualHeight < 1) zeroSized.Add(ElementIdentity(interactive));
        }

        var explicitInteractiveBounds = elements
            .Where(IsInteractive)
            .Where(element => element.TemplatedParent is null)
            .Select(element => (Element: element, Bounds: TryGetElementBounds(element, root)))
            .Where(item => item.Bounds is not null)
            .ToArray();
        var overlaps = new List<string>();
        for (var leftIndex = 0; leftIndex < explicitInteractiveBounds.Length; leftIndex++)
        for (var rightIndex = leftIndex + 1; rightIndex < explicitInteractiveBounds.Length; rightIndex++)
        {
            var left = explicitInteractiveBounds[leftIndex];
            var right = explicitInteractiveBounds[rightIndex];
            if (IsAncestor(left.Element, right.Element) || IsAncestor(right.Element, left.Element)) continue;
            var intersection = Rect.Intersect(left.Bounds!.Rect, right.Bounds!.Rect);
            if (!intersection.IsEmpty && intersection.Width > 2 && intersection.Height > 2 && !IsAllowedUtilityOverlay(left.Bounds, right.Bounds, intersection))
                overlaps.Add($"{left.Bounds.Identity} <> {right.Bounds.Identity}");
        }

        var focusTarget = elements.OfType<Button>().FirstOrDefault(button => button.IsEnabled && button.Focusable);
        var focusBounds = focusTarget is null ? null : TryGetElementBounds(focusTarget, root);
        var focusVisible = focusBounds is not null && focusBounds.Rect.IntersectsWith(new Rect(0, 0, viewportWidth, viewportHeight));
        var blocking = overflow.Distinct().Count() + overlaps.Distinct().Count() + zeroSized.Distinct().Count() + textClipping.Distinct().Count() + whiteSurface.Distinct().Count();
        return new LayoutInspection(
            bounds.Count,
            overflow.Distinct().ToArray(),
            overlaps.Distinct().ToArray(),
            zeroSized.Distinct().ToArray(),
            textClipping.Distinct().ToArray(),
            whiteSurface.Distinct().ToArray(),
            focusTarget is null ? string.Empty : ElementIdentity(focusTarget),
            focusVisible,
            blocking);
    }

    private ThemeInspection InspectThemeResources()
    {
        string[] requiredBrushes =
        [
            "WindowBackgroundBrush", "ContentBackgroundBrush", "SurfacePrimaryBrush", "SurfaceSecondaryBrush",
            "RaisedSurfaceBrush", "TextPrimaryBrush", "TextSecondaryBrush", "InputBackgroundBrush",
            "InputForegroundBrush", "InputBorderBrush", "DropdownBackgroundBrush", "TooltipBackgroundBrush",
            "ScrollBarTrackBrush", "ScrollBarThumbBrush", "MenuPopupBackgroundBrush", "MenuPopupBorderBrush"
        ];
        var missing = requiredBrushes.Where(key => Application.Current.TryFindResource(key) is not Brush).ToArray();
        var highContrastDictionaryExists = Application.Current.Resources.MergedDictionaries.Any(dictionary =>
            dictionary.Source?.OriginalString.Contains("HighContrast", StringComparison.OrdinalIgnoreCase) == true);
        var highContrastSourceExists = CanLoadResourceDictionary("Resources/DesignSystem/Theme.HighContrast.xaml");
        string[] controlTypes =
        [
            nameof(TextBox), nameof(PasswordBox), nameof(ComboBox), nameof(ComboBoxItem), nameof(CheckBox), nameof(RadioButton),
            nameof(ToggleButton), nameof(Slider), nameof(ScrollBar), nameof(ScrollViewer), nameof(DatePicker), nameof(DataGrid),
            nameof(DataGridColumnHeader), nameof(ContextMenu), nameof(MenuItem), nameof(Popup), nameof(TabControl), nameof(ToolTip), nameof(ProgressBar)
        ];
        var visibleTypes = FindVisualChildren<FrameworkElement>(RootGrid)
            .Where(element => element.IsVisible)
            .GroupBy(element => element.GetType().Name)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var renderedTypes = controlTypes.ToDictionary(type => type, type => visibleTypes.TryGetValue(type, out var count) ? count : 0);
        var controlRenderChecks = controlTypes.ToDictionary(type => type, VerifyControlStyleRender, StringComparer.OrdinalIgnoreCase);
        var primary = Application.Current.TryFindResource("TextPrimaryBrush") as SolidColorBrush;
        var background = Application.Current.TryFindResource("WindowBackgroundBrush") as SolidColorBrush;
        var contrast = primary is not null && background is not null ? ContrastRatio(primary.Color, background.Color) : 0;
        return new ThemeInspection(
            _automatedThemeName,
            missing,
            highContrastDictionaryExists || highContrastSourceExists,
            renderedTypes,
            controlRenderChecks,
            contrast,
            missing.Length == 0 && (highContrastDictionaryExists || highContrastSourceExists) && contrast >= 4.5 && controlRenderChecks.Values.All(value => value));
    }

    private static bool VerifyControlStyleRender(string typeName)
    {
        try
        {
            FrameworkElement element = typeName switch
            {
                nameof(TextBox) => new TextBox { Text = "DPI" },
                nameof(PasswordBox) => new PasswordBox { Password = "DPI" },
                nameof(ComboBox) => new ComboBox { ItemsSource = new[] { "DPI" }, SelectedIndex = 0 },
                nameof(ComboBoxItem) => new ComboBoxItem { Content = "DPI" },
                nameof(CheckBox) => new CheckBox { Content = "DPI", IsChecked = true },
                nameof(RadioButton) => new RadioButton { Content = "DPI", IsChecked = true },
                nameof(ToggleButton) => new ToggleButton { Content = "DPI", IsChecked = true },
                nameof(Slider) => new Slider { Value = 50 },
                nameof(ScrollBar) => new ScrollBar { Maximum = 100, Value = 25 },
                nameof(ScrollViewer) => new ScrollViewer { Content = new TextBlock { Text = "DPI" } },
                nameof(DatePicker) => new DatePicker { SelectedDate = DateTime.Today },
                nameof(DataGrid) => new DataGrid { ItemsSource = new[] { new { Value = "DPI" } }, AutoGenerateColumns = true },
                nameof(DataGridColumnHeader) => new DataGridColumnHeader { Content = "DPI" },
                nameof(ContextMenu) => new ContextMenu { Items = { new MenuItem { Header = "DPI" } } },
                nameof(MenuItem) => new MenuItem { Header = "DPI" },
                nameof(Popup) => new Popup { Child = new Border { Width = 120, Height = 40, Background = Brushes.Gray } },
                nameof(TabControl) => new TabControl { Items = { new TabItem { Header = "DPI", Content = "DPI" } }, SelectedIndex = 0 },
                nameof(ToolTip) => new ToolTip { Content = "DPI" },
                nameof(ProgressBar) => new ProgressBar { Maximum = 100, Value = 50 },
                _ => throw new InvalidOperationException(typeName)
            };
            var implicitStyle = Application.Current.TryFindResource(element.GetType()) as Style;
            if (implicitStyle is not null) element.Style = implicitStyle;
            element.Width = element is DataGrid ? 300 : 240;
            element.Height = element is DataGrid ? 100 : 44;
            element.Measure(new Size(element.Width, element.Height));
            element.Arrange(new Rect(0, 0, element.Width, element.Height));
            element.UpdateLayout();
            if (element is Control control) control.ApplyTemplate();
            var bitmap = new RenderTargetBitmap((int)element.Width, (int)element.Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(element);
            return bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0 &&
                   (implicitStyle is not null || element is System.Windows.Controls.Primitives.Popup or System.Windows.Controls.ContextMenu or System.Windows.Controls.MenuItem or System.Windows.Controls.ToolTip);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsInteractive(FrameworkElement element) =>
        element is ButtonBase or TextBoxBase or Selector or Slider or DatePicker;

    private static bool IsAncestor(DependencyObject ancestor, DependencyObject child)
    {
        for (DependencyObject? current = child; current is not null; current = VisualTreeHelper.GetParent(current))
            if (ReferenceEquals(current, ancestor)) return true;
        return false;
    }

    private static ElementBounds? TryGetElementBounds(FrameworkElement element, FrameworkElement root)
    {
        try
        {
            var origin = element.TransformToAncestor(root).Transform(new Point(0, 0));
            var visibleRect = new Rect(origin.X, origin.Y, element.ActualWidth, element.ActualHeight);
            for (DependencyObject? current = VisualTreeHelper.GetParent(element); current is FrameworkElement ancestor && !ReferenceEquals(current, root); current = VisualTreeHelper.GetParent(current))
            {
                if (ancestor is not ScrollViewer and not ScrollContentPresenter && ancestor.Clip is null && !ancestor.ClipToBounds) continue;
                var ancestorOrigin = ancestor.TransformToAncestor(root).Transform(new Point(0, 0));
                visibleRect.Intersect(new Rect(ancestorOrigin.X, ancestorOrigin.Y, ancestor.ActualWidth, ancestor.ActualHeight));
                if (visibleRect.IsEmpty) return null;
            }
            return new ElementBounds(ElementIdentity(element), element.GetType().Name, visibleRect.X, visibleRect.Y, visibleRect.Width, visibleRect.Height);
        }
        catch
        {
            return null;
        }
    }

    private static bool HasClippingAncestor(DependencyObject element, DependencyObject root)
    {
        for (DependencyObject? current = VisualTreeHelper.GetParent(element); current is not null && !ReferenceEquals(current, root); current = VisualTreeHelper.GetParent(current))
        {
            if (current is ScrollViewer or ScrollContentPresenter) return true;
            if (current is UIElement uiElement && (uiElement.Clip is not null || uiElement.ClipToBounds)) return true;
        }
        return false;
    }

    private static bool IsAllowedUtilityOverlay(ElementBounds left, ElementBounds right, Rect intersection)
    {
        if (left.Identity.Contains("关闭检查器", StringComparison.OrdinalIgnoreCase) ||
            right.Identity.Contains("关闭检查器", StringComparison.OrdinalIgnoreCase))
            return true;
        var leftArea = left.Width * left.Height;
        var rightArea = right.Width * right.Height;
        var smaller = leftArea <= rightArea ? left : right;
        var smallerArea = Math.Min(leftArea, rightArea);
        var largerArea = Math.Max(leftArea, rightArea);
        var semanticUtility = smaller.Identity.Contains("说明", StringComparison.OrdinalIgnoreCase) ||
                              smaller.Identity.Contains("帮助", StringComparison.OrdinalIgnoreCase) ||
                              smaller.Identity.Contains("更多", StringComparison.OrdinalIgnoreCase);
        return semanticUtility && largerArea >= smallerArea * 4 && intersection.Width * intersection.Height >= smallerArea * .85;
    }

    private static bool IsTextClipped(TextBlock textBlock)
    {
        if (string.IsNullOrWhiteSpace(textBlock.Text) || textBlock.TextTrimming != TextTrimming.None) return false;
        if (textBlock.ActualWidth <= 0 || textBlock.ActualHeight <= 0) return true;
        var formatted = new FormattedText(
            textBlock.Text,
            CultureInfo.CurrentUICulture,
            textBlock.FlowDirection,
            new Typeface(textBlock.FontFamily, textBlock.FontStyle, textBlock.FontWeight, textBlock.FontStretch),
            textBlock.FontSize,
            textBlock.Foreground,
            VisualTreeHelper.GetDpi(textBlock).PixelsPerDip);
        if (textBlock.TextWrapping == TextWrapping.NoWrap) return formatted.WidthIncludingTrailingWhitespace > textBlock.ActualWidth + 3;
        formatted.MaxTextWidth = Math.Max(1, textBlock.ActualWidth);
        return formatted.Height > textBlock.ActualHeight + 3;
    }

    private static bool HasUnexpectedWhiteSurface(Control control)
    {
        if (control is ButtonBase or CheckBox or RadioButton or ScrollViewer or ListBoxItem) return false;
        if (control.Background is not SolidColorBrush brush || brush.Opacity < .8 || brush.Color.A < 204) return false;
        var color = brush.Color;
        return color.R >= 245 && color.G >= 245 && color.B >= 245;
    }

    private static string ElementIdentity(FrameworkElement element)
    {
        var automationName = AutomationProperties.GetName(element);
        if (!string.IsNullOrWhiteSpace(automationName)) return automationName;
        if (!string.IsNullOrWhiteSpace(element.Name)) return element.Name;
        if (element is ContentControl content && content.Content is string value && !string.IsNullOrWhiteSpace(value)) return value;
        if (element is TextBlock text && !string.IsNullOrWhiteSpace(text.Text)) return text.Text.Length > 42 ? text.Text[..42] : text.Text;
        return element.GetType().Name;
    }

    private static double ContrastRatio(Color first, Color second)
    {
        static double Channel(byte value)
        {
            var normalized = value / 255d;
            return normalized <= .03928 ? normalized / 12.92 : Math.Pow((normalized + .055) / 1.055, 2.4);
        }
        static double Luminance(Color color) => .2126 * Channel(color.R) + .7152 * Channel(color.G) + .0722 * Channel(color.B);
        var left = Luminance(first);
        var right = Luminance(second);
        return (Math.Max(left, right) + .05) / (Math.Min(left, right) + .05);
    }

    private static bool CanLoadResourceDictionary(string relativePath)
    {
        try
        {
            _ = new ResourceDictionary { Source = new Uri(relativePath, UriKind.Relative) };
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ResolveSourceCommit()
    {
        var value = Environment.GetEnvironmentVariable("PIXEL_TART_SOURCE_COMMIT");
        return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
    }

    private void CloseAutomatedDpiOverlays()
    {
        if (_automatedContextMenu is not null)
        {
            _automatedContextMenu.IsOpen = false;
            _automatedContextMenu = null;
        }
        if (_automatedToolTip is not null)
        {
            _automatedToolTip.IsOpen = false;
            _automatedToolTip = null;
        }
        if (_automatedPopup is not null)
        {
            _automatedPopup.IsOpen = false;
            _automatedPopup = null;
        }
        if (_automatedAuxiliaryWindow is not null)
        {
            _automatedAuxiliaryWindow.Close();
            _automatedAuxiliaryWindow = null;
        }
    }

    private sealed record ElementBounds(string Identity, string Type, double X, double Y, double Width, double Height)
    {
        public Rect Rect => new(X, Y, Width, Height);
    }

    private sealed record LayoutInspection(
        int ElementCount,
        IReadOnlyList<string> Overflow,
        IReadOnlyList<string> Overlaps,
        IReadOnlyList<string> ZeroSizedInteractive,
        IReadOnlyList<string> TextClipping,
        IReadOnlyList<string> UnexpectedWhiteSurfaces,
        string FocusTarget,
        bool FocusVisible,
        int BlockingIssueCount);

    private sealed record ThemeInspection(
        string Theme,
        IReadOnlyList<string> MissingBrushResources,
        bool HighContrastResourceStructurePresent,
        IReadOnlyDictionary<string, int> RenderedControlTypes,
        IReadOnlyDictionary<string, bool> ControlStyleRenderChecks,
        double PrimaryTextContrastRatio,
        bool Passed);
}
#endif
