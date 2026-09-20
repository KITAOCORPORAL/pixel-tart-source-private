using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;

namespace RAWSelectionAssistant.WpfTests;

internal static class PlanningProposalAcceptance
{
    public static async Task RunAsync(MainWindow window, MainViewModel main)
    {
        var output = Environment.GetEnvironmentVariable("PIXEL_TART_PROPOSAL_EVIDENCE");
        if (string.IsNullOrWhiteSpace(output)) return;
        Directory.CreateDirectory(output);
        var vm = main.PlanningPage!;
        main.NavigateCommand.Execute("Planning");
        var project = vm.ProjectId!.Value;
        // Upgrade only this explicitly isolated demo's one-pixel placeholders to visible fixtures.
        var oldPaths = vm.Shots.SelectMany(shot => shot.References).Select(reference => reference.ExternalReference).OfType<string>().Distinct().ToArray();
        for (var i = 0; i < oldPaths.Length; i++) DrawFixture(oldPaths[i], i + 30);
        vm.DocumentTitle = "肉与白骨｜欲望与死亡";
        vm.DocumentSubtitle = "观白骨如观美人，观美人如观白骨。";
        vm.DocumentBody = "肉身承载欲望，死亡让欲望最终沉默。\n\n> 在柔软与坚硬之间，寻找身体与空间的关系。";
        vm.ProposalStatus = "进行中";
        vm.ShootGoal = "以身体、石膏与织物建立安静的视觉对话。";
        vm.Keywords = "克制 / 肌理 / 留白 / 柔光";
        vm.MustCapture = "- 正面全身：身体与结构的对照\n- 手部近景：触感与重量\n- 侧面中景：空间与距离";
        vm.PlanningNotes = "本项目为隔离验收夹具；图片为合成构图参考，不是真实摄影作品。";
        vm.DocumentLocation = "杭州白棚"; vm.DocumentPeople = "模特：验收人物";
        Assert.IsTrue(await vm.FlushAsync());
        var fixture = Path.Combine(AppDataPaths.Root, "ProposalFixtures"); Directory.CreateDirectory(fixture);
        var brief = Path.Combine(fixture, "客户拍摄说明.txt");
        await File.WriteAllTextAsync(brief, "隔离验收文件：来源只被关联，不被移动或删除。");
        await vm.AddAttachmentsAsync([brief]);
        await vm.ShowBookingCommand.ExecuteAsync(null);
        vm.SelectedBooking = vm.AvailableBookings.Single(item => item.Id == PlanningHumanAcceptanceDemoSeeder.BookingId);
        await vm.LinkBookingCommand.ExecuteAsync(null);
        Assert.IsFalse(vm.IsBookingOpen);
        var editedShotId = vm.SelectedShot!.ShotId;
        vm.ShotNotes = "快速输入后立即修改状态，必须保留这些内容。";
        await vm.MarkShotCompletedCommand.ExecuteAsync(null);
        Assert.AreEqual("快速输入后立即修改状态，必须保留这些内容。", vm.SelectedShot.Notes);
        Assert.AreEqual(ProjectShotStatus.Completed, vm.SelectedShot.Status);
        var refs = new List<ProjectShotReference>();
        for (var i = 0; i < 24; i++)
        {
            var path = Path.Combine(fixture, $"reference-{i:00}.jpg"); DrawFixture(path, i);
            refs.Add(new(Guid.NewGuid(), ShotReferenceKind.General, ExternalReference: path, Title: new[] { "身体与石膏", "织物的呼吸", "温柔的阴影", "侧光结构", "妆发与质感", "空间中的距离" }[i % 6]));
        }
        await vm.AddDocumentReferencesAsync(refs);
        foreach (var reference in vm.AllProjectReferences.Where(item => refs.Any(source => source.ReferenceId == item.Id)).ToArray())
        {
            var index = refs.FindIndex(source => source.ReferenceId == reference.Id);
            await vm.UpdateReferenceAsync(reference, "分类", index < 3 ? "主视觉" : index < 8 ? "灯光" : index < 13 ? "造型" : "姿势");
            if (index < 3) await vm.UpdateReferenceAsync(vm.AllProjectReferences.Single(item => item.Id == reference.Id), "封面");
            if (index < 6) await vm.UpdateReferenceAsync(vm.AllProjectReferences.Single(item => item.Id == reference.Id), "情绪板");
            if (index is >= 8 and < 13) await vm.UpdateReferenceAsync(vm.AllProjectReferences.Single(item => item.Id == reference.Id), "分组", vm.StylingGroups[index % 3]);
        }
        // A real body edit followed immediately by a project switch must persist to the original identity.
        vm.DocumentBody += "\n\n**拍摄方法**：用距离表达情绪，让光线承担叙事。";
        var expected = vm.DocumentBody;
        await vm.ShowOverviewAsync(); await vm.OpenProjectSafelyAsync(project);
        Assert.AreEqual(expected, vm.DocumentBody);
        Assert.AreEqual("快速输入后立即修改状态，必须保留这些内容。", vm.Shots.Single(item => item.ShotId == editedShotId).Notes);
        Assert.HasCount(1, vm.Attachments);
        var persisted = await new PlanningProjectStore(Path.Combine(AppDataPaths.DataDirectory, "ProjectPlanning")).LoadAsync(project);
        Assert.AreEqual(expected, persisted.Document!.Body);
        Assert.IsNotEmpty(persisted.VisualLinks!);
        var recoveryStore = new PlanningProjectStore(Path.Combine(AppDataPaths.DataDirectory, "ProjectPlanning"));
        await recoveryStore.WriteDraftAsync(project, persisted.Document with { Body = expected + "\n恢复草稿验证" }, persisted.Summary!, shots: [vm.Shots[0] with { Notes = "镜头恢复草稿验证" }]);
        await vm.OpenProjectSafelyAsync(project);
        Assert.AreEqual(expected + "\n恢复草稿验证", vm.DocumentBody);
        Assert.AreEqual("镜头恢复草稿验证", vm.Shots[0].Notes);
        Assert.IsTrue(await vm.FlushAsync());
        Assert.IsNull(await recoveryStore.LoadDraftAsync(project));
        vm.DocumentBody = expected; Assert.IsTrue(await vm.FlushAsync());
        // Missing originals retain persistent derived preview.
        var offline = vm.AllProjectReferences.Single(item => item.Id == refs[23].ReferenceId);
        File.Move(refs[23].ExternalReference!, refs[23].ExternalReference! + ".offline");
        await vm.RefreshDocumentReferencesAsync();
        offline = vm.AllProjectReferences.Single(item => item.Id == offline.Id);
        Assert.IsFalse(offline.SourceAvailable); Assert.IsTrue(File.Exists(offline.PreviewPath));
        File.Move(refs[23].ExternalReference! + ".offline", refs[23].ExternalReference!);
        await vm.RefreshDocumentReferencesAsync();
        window.WindowState = WindowState.Normal; window.MinWidth = window.Width = 1920; window.MinHeight = window.Height = 1080;
        await Capture("01_planning_overview.png", overview: true);
        await vm.OpenProjectSafelyAsync(project);
        var names = new[] { "01_文字.png", "02_参考图.png", "03_情绪板.png", "04_镜头清单.png", "05_灯光图.png", "06_服化道.png", "07_文件.png" };
        for (var i = 0; i < PlanningCenterViewModel.ContentPages.Count; i++)
        { vm.ContentPage = PlanningCenterViewModel.ContentPages[i]; await Capture(names[i]); }
        vm.ContentPage = "文字"; vm.IsPreviewMode = true; await Capture("08_预览模式.png"); vm.IsPreviewMode = false;
        var englishNames = new[] { "02_planning_text.png", "03_reference_images.png", "04_moodboard.png", "05_shot_list.png", "06_lighting.png", "07_styling.png", "08_files.png" };
        for (var i = 0; i < names.Length; i++) File.Copy(Path.Combine(output, names[i]), Path.Combine(output, englishNames[i]), true);
        File.Copy(Path.Combine(output, "08_预览模式.png"), Path.Combine(output, "09_preview_mode.png"), true);
        File.Copy(Path.Combine(output, "01_文字.png"), Path.Combine(output, "10_planning_1080p.png"), true);
        var planningView = Find<PlanningCenterView>(window)!;
        var content = (System.Windows.Controls.StackPanel)planningView.FindName("DocumentContent");
        Assert.IsNull(Find<System.Windows.Controls.TextBox>(content), "Default body must be reading mode.");
        vm.IsDocumentEditing = true; window.UpdateLayout();
        Assert.IsNotNull(Find<System.Windows.Controls.TextBox>(content), "Editing must expose bound editors.");
        vm.IsDocumentEditing = false;
        vm.IsPreviewMode = true;
        var escape = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, 0, System.Windows.Input.Key.Escape) { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent };
        await planningView.HandleWorkspaceKeyAsync(escape);
        Assert.IsFalse(vm.IsPreviewMode); Assert.IsTrue(escape.Handled);
        PlanningProposalPdf.Export(vm, Path.Combine(output, "策划案-验收.pdf"));
        Assert.IsGreaterThan(10000L, new FileInfo(Path.Combine(output, "策划案-验收.pdf")).Length);
        var colorRef = vm.AllProjectReferences.Single(item => item.Id == refs[0].ReferenceId);
        await vm.UseReferenceForColorAsync(colorRef);
        var looks = await new ReferenceLookStore(Path.Combine(AppDataPaths.DataDirectory, "ProjectVisuals")).LoadAsync();
        Assert.IsTrue(looks.Looks.Any(look => look.ProjectId == project && look.ReferenceSources.Any(source => source.SourcePath == refs[0].ExternalReference)));
        var removeRef = vm.AllProjectReferences.Single(item => item.Id == refs[23].ReferenceId);
        await vm.UpdateReferenceAsync(removeRef, "移除");
        Assert.IsTrue(File.Exists(refs[23].ExternalReference));
        Assert.IsFalse(vm.AllProjectReferences.Any(item => item.Id == removeRef.Id));
        async Task Capture(string name, bool overview = false)
        {
            if (overview) await vm.ShowOverviewAsync();
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
            await Task.Delay(120);
            var view = Find<PlanningCenterView>(window)!; view.Render(); view.UpdateLayout();
            var scroll = (System.Windows.Controls.ScrollViewer)view.FindName("DocumentScroll"); scroll.ScrollToTop();
            window.UpdateLayout();
            Assert.AreEqual(1920d, window.ActualWidth); Assert.AreEqual(1080d, window.ActualHeight);
            Assert.IsGreaterThan(1400d, view.ActualWidth);
            var column = (System.Windows.Controls.ColumnDefinition)view.FindName("DocumentListColumn");
            Assert.AreEqual(vm.IsPreviewMode ? 0d : 280d, column.ActualWidth);
            var bitmap = new RenderTargetBitmap(1920, 1080, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(Path.Combine(output, name)); encoder.Save(stream);
        }
    }
    private static T? Find<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent is T found) return found;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) if (Find<T>(VisualTreeHelper.GetChild(parent, i)) is { } child) return child;
        return null;
    }
    private static void DrawFixture(string path, int index)
    {
        var width = index % 3 == 0 ? 1100 : 740; var height = index % 3 == 0 ? 740 : 980;
        var visual = new DrawingVisual();
        using (var draw = visual.RenderOpen())
        {
            var background = new LinearGradientBrush(Color.FromRgb(211, 199, 181), Color.FromRgb(64, 73, 74), 40 + index * 7);
            draw.DrawRectangle(background, null, new Rect(0, 0, width, height));
            draw.DrawEllipse(new SolidColorBrush(Color.FromArgb(100, 24, 30, 32)), null, new Point(width * .59, height * .83), width * .26, height * .04);
            draw.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(216, 204, 181)), null, new Rect(width * .48, height * .38, width * .19, height * .43), 46, 46);
            draw.DrawEllipse(new SolidColorBrush(Color.FromRgb(192, 163, 142)), null, new Point(width * .50, height * .30), width * .12, height * .12);
            draw.DrawRectangle(new SolidColorBrush(Color.FromArgb(80, 243, 234, 216)), null, new Rect(width * .13, 0, width * .12, height));
            draw.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(120, 244, 238, 220)), 2), new Point(0, height * .84), new Point(width, height * .84));
        }
        var image = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); image.Render(visual);
        var encoder = new JpegBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using var stream = File.Create(path); encoder.Save(stream);
    }
}
