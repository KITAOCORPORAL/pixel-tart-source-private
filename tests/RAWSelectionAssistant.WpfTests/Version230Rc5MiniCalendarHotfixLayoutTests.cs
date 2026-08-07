using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class Version230Rc5MiniCalendarHotfixLayoutTests
{
    [TestMethod]
    [DataRow(280, 384)]
    [DataRow(280, 420)]
    [DataRow(260, 384)]
    [DataRow(320, 420)]
    public Task MiniCalendar_ActualLayoutPreservesTextInsetsRowsAndDetailsGap(int width, int height) => RunSta(() =>
    {
        var app = EnsureApplication(out var ownsApplication);
        try
        {
            var panel = CreatePanel(width, height);
            var cells = Children<Border>(panel).Where(item => item.Name == "CalendarDayCell").ToArray();
            var badges = Children<Border>(panel).Where(item => item.Name == "DayNumberBadge").ToArray();
            var texts = Children<TextBlock>(panel).Where(item => item.Name == "DayNumberText").ToArray();
            var detailsHeader = Children<Grid>(panel).Single(item => item.Name == "DayDetailsHeader");
            var previousButton = Children<Button>(panel).Single(item => item.Name == "PreviousMonthButton");
            var nextButton = Children<Button>(panel).Single(item => item.Name == "NextMonthButton");

            Assert.HasCount(42, cells);
            Assert.HasCount(42, badges);
            Assert.HasCount(42, texts);
            Assert.AreEqual(22d, badges[0].ActualHeight, .1);
            Assert.IsGreaterThanOrEqualTo(32d, cells[0].ActualHeight);
            Assert.IsGreaterThanOrEqualTo(texts[0].ActualHeight + 4, badges[0].ActualHeight);
            Assert.IsGreaterThanOrEqualTo(badges[0].ActualHeight + 10, cells[0].ActualHeight);

            for (var index = 0; index < 42; index++)
            {
                var badgeOrigin = badges[index].TransformToAncestor(cells[index]).Transform(new Point());
                var textOrigin = texts[index].TransformToAncestor(badges[index]).Transform(new Point());
                Assert.IsGreaterThanOrEqualTo(0d, badgeOrigin.Y);
                Assert.IsLessThanOrEqualTo(cells[index].ActualHeight, badgeOrigin.Y + badges[index].ActualHeight);
                Assert.IsGreaterThanOrEqualTo(2d, textOrigin.Y);
                Assert.IsLessThanOrEqualTo(badges[index].ActualHeight - 2, textOrigin.Y + texts[index].ActualHeight);
                Assert.AreEqual(badges[0].ActualHeight, badges[index].ActualHeight, .1);
            }

            for (var row = 0; row < 5; row++)
            {
                var current = cells[row * 7];
                var next = cells[(row + 1) * 7];
                var currentOrigin = current.TransformToAncestor(panel).Transform(new Point());
                var nextOrigin = next.TransformToAncestor(panel).Transform(new Point());
                Assert.IsGreaterThanOrEqualTo(4d, nextOrigin.Y - (currentOrigin.Y + current.ActualHeight));
            }

            var lastCell = cells[35];
            var lastOrigin = lastCell.TransformToAncestor(panel).Transform(new Point());
            var detailsOrigin = detailsHeader.TransformToAncestor(panel).Transform(new Point());
            Assert.IsGreaterThanOrEqualTo(16d, detailsOrigin.Y - (lastOrigin.Y + lastCell.ActualHeight));
            Assert.AreEqual(previousButton.ActualWidth, nextButton.ActualWidth, .1);
            Assert.AreEqual(previousButton.ActualHeight, nextButton.ActualHeight, .1);
            Assert.IsLessThanOrEqualTo(32d, previousButton.ActualHeight);
        }
        finally
        {
            if (ownsApplication) app.Shutdown();
        }
        return Task.CompletedTask;
    });

    [TestMethod]
    [DataRow(100)]
    [DataRow(125)]
    [DataRow(150)]
    [DataRow(175)]
    [DataRow(200)]
    public Task MiniCalendar_LogicalMetricsRemainStableAcrossSupportedDpi(int dpiPercent) => RunSta(() =>
    {
        var app = EnsureApplication(out var ownsApplication);
        try
        {
            var panel = CreatePanel(280, 420);
            var cells = Children<Border>(panel).Where(item => item.Name == "CalendarDayCell").ToArray();
            var badges = Children<Border>(panel).Where(item => item.Name == "DayNumberBadge").ToArray();
            var texts = Children<TextBlock>(panel).Where(item => item.Name == "DayNumberText").ToArray();
            Assert.IsTrue(new[] { 100, 125, 150, 175, 200 }.Contains(dpiPercent));
            Assert.IsTrue(cells.All(item => item.ActualHeight >= 32));
            Assert.IsTrue(badges.All(item => Math.Abs(item.ActualHeight - 22) < .1));
            Assert.IsTrue(texts.All(item => item.ActualHeight + 4 <= 22));
        }
        finally
        {
            if (ownsApplication) app.Shutdown();
        }
        return Task.CompletedTask;
    });

    [TestMethod]
    public void MiniCalendar_HotfixDoesNotUseCompressionTransformsOrNegativeMargins()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "src", "RAWSelectionAssistant", "Views", "WorkbenchCalendarPanel.xaml"));
        Assert.IsFalse(xaml.Contains("ScaleTransform", StringComparison.Ordinal));
        Assert.IsFalse(xaml.Contains("TranslateTransform", StringComparison.Ordinal));
        Assert.IsFalse(xaml.Contains("Margin=\"-", StringComparison.Ordinal));
        StringAssert.Contains(xaml, "x:Name=\"PreviousMonthButton\"");
        StringAssert.Contains(xaml, "x:Name=\"NextMonthButton\"");
        StringAssert.Contains(xaml, "Width=\"30\" Height=\"30\" MinWidth=\"30\" MinHeight=\"30\"");
    }

    private static WorkbenchCalendarPanel CreatePanel(double width, double height)
    {
        var calendar = new WorkCalendarViewModel(new StubBookingService(), new StubProjectRepository());
        var selectedDate = DateTime.Today.AddDays(1);
        calendar.Month.Configure(DateTime.Today, CreateStatusBookings(), selectedDate);
        calendar.DaySchedule.Configure(selectedDate, [], null);
        var panel = new WorkbenchCalendarPanel { DataContext = calendar, Width = width, Height = height };
        panel.Measure(new Size(width, height));
        panel.Arrange(new Rect(0, 0, width, height));
        panel.UpdateLayout();
        return panel;
    }

    private static IReadOnlyList<ShootBookingSummary> CreateStatusBookings()
    {
        var statuses = new[]
        {
            ShootBookingStatus.Confirmed,
            ShootBookingStatus.Completed,
            ShootBookingStatus.AwaitingDelivery,
            ShootBookingStatus.Delivered
        };
        return statuses.Select((status, index) =>
        {
            var start = new DateTimeOffset(DateTime.Today.AddDays(index).AddHours(9), TimeZoneInfo.Local.GetUtcOffset(DateTime.Today.AddDays(index))).ToUniversalTime();
            return new ShootBookingSummary(Guid.NewGuid(), null, $"Layout {index}", "Isolated", start, start.AddHours(1), TimeZoneInfo.Local.Id, false, status, "Studio", "Portrait", false, false);
        }).ToArray();
    }

    private static App EnsureApplication(out bool ownsApplication)
    {
        ownsApplication = Application.Current is null;
        if (Application.Current is App current) return current;
        var app = new App();
        app.InitializeComponent();
        return app;
    }

    private static IEnumerable<T> Children<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) yield return match;
            foreach (var descendant in Children<T>(child)) yield return descendant;
        }
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

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }

    private sealed class StubProjectRepository : IProjectRepository
    {
        public Task UpsertAsync(PhotoProjectRecord project, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<PhotoProjectRecord>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PhotoProjectRecord>>([]);
    }

    private sealed class StubBookingService : IShootBookingService
    {
        public Task<BookingSaveResult> SaveAsync(ShootBookingDraft draft, BookingConflictResolution conflictResolution = BookingConflictResolution.None, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ShootBooking?> GetAsync(Guid id, bool includeArchived = false, CancellationToken cancellationToken = default) => Task.FromResult<ShootBooking?>(null);
        public Task<IReadOnlyList<ShootRequirementItem>> GetRequirementsAsync(Guid bookingId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ShootRequirementItem>>([]);
        public Task<IReadOnlyList<ShootBookingSummary>> QueryCurrentViewAsync(ShootBookingQuery query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ShootBookingSummary>>([]);
        public Task<ShootBookingPage> SearchAllUnarchivedAsync(ShootBookingSearchRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new ShootBookingPage([], null));
        public Task<ShootBookingPage> SearchArchivedAsync(ShootBookingSearchRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new ShootBookingPage([], null));
        public Task<bool> CompleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ArchiveAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> RestoreAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
