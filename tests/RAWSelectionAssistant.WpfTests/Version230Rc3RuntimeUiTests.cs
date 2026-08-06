using System.Globalization;
using System.IO;
using System.Xml.Linq;
using RAWSelectionAssistant.Converters;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class Version230Rc3RuntimeUiTests
{
    [TestMethod]
    [DataRow("工作日历视图")][DataRow("工作日历排序")][DataRow("日期跳转")][DataRow("月")][DataRow("周")][DataRow("日")]
    [DataRow("拍摄状态筛选")][DataRow("拍摄类型筛选")][DataRow("搜索排期")][DataRow("可折叠状态图例")]
    [DataRow("未拍摄")][DataRow("已拍摄")][DataRow("待发送选片 / 待选片 / 已选片")][DataRow("待精修 / 已精修 / 待交付")][DataRow("已交付")]
    [DataRow("时间冲突")][DataRow("天气风险")][DataRow("关闭档期")][DataRow("Width=\"65*\"")][DataRow("Width=\"35*\"")]
    [DataRow("GridSplitter")][DataRow("当前周期拍摄列表")][DataRow("当前周期总览")][DataRow("StatusToTextConverter")][DataRow("DaySchedulePanel")]
    public void ProfessionalCalendar_ContainsRequiredRuntimeSurface(string token) => Contains("src/RAWSelectionAssistant/Views/WorkCalendarView.xaml", token);

    [TestMethod]
    [DataRow("摄影收支")][DataRow("金额信息仅保存在本机")][DataRow("＋ 收入")][DataRow("＋ 支出")][DataRow("导出 CSV")]
    [DataRow("本月收入")][DataRow("本月支出")][DataRow("本月净现金流")][DataRow("待收款")][DataRow("待付款")][DataRow("项目预计利润")]
    [DataRow("按收入支出筛选")][DataRow("按支付状态筛选")][DataRow("按分类筛选")][DataRow("按项目筛选")][DataRow("按拍摄任务筛选")]
    [DataRow("本月收支记录")][DataRow("付款方或收款方")][DataRow("支付方式")][DataRow("附件（仅关联原位置）")]
    [DataRow("移除引用")][DataRow("不会删除电脑中的原文件")][DataRow("保存收支记录")][DataRow("搜索词不会写入日志")]
    public void FinancePage_ContainsRequiredLocalAccountingSurface(string token) => Contains("src/RAWSelectionAssistant/Views/FinanceView.xaml", token);

    [TestMethod]
    [DataRow("本地摄影资料关联")][DataRow("默认仅关联原位置")][DataRow("不把文件写入数据库")][DataRow("不会自动复制")]
    [DataRow("文件夹不会被扫描")][DataRow("支持一次添加多个文件")][DataRow("文档分类")][DataRow("复制到项目资料目录并关联")]
    [DataRow("预览")][DataRow("打开所在位置")][DataRow("重新定位")][DataRow("移除关联")][DataRow("检查全部关联文件")]
    [DataRow("PreviewDisplayWidth")][DataRow("PreviewDisplayHeight")][DataRow("搜索文本预览")][DataRow("只读文本预览")]
    [DataRow("安全文件卡片")][DataRow("Office 文件不执行宏")][DataRow("未知格式仅显示元数据")][DataRow("重试保存关联")]
    [DataRow("安全撤销本次复制")][DataRow("保留文件但放弃关联")][DataRow("VerticalScrollBarVisibility=\"Auto\"")]
    public void DocumentPanel_ContainsSafePreviewAndRecoverySurface(string token) => Contains("src/RAWSelectionAssistant/Views/BookingDocumentsPanel.xaml", token);

    [TestMethod]
    [DataRow("1 基础信息")][DataRow("2 时间与准备")][DataRow("3 策划资料")][DataRow("4 联系人、工作人员与收支")]
    [DataRow("客户或模特姓名/代号")][DataRow("手机")][DataRow("微信")][DataRow("邮箱")][DataRow("其他联系方式")]
    [DataRow("联系人备注")][DataRow("主要联系人")][DataRow("工作人员姓名或代号")][DataRow("到场时间")][DataRow("工作人员备注")]
    [DataRow("上移工作人员")][DataRow("下移工作人员")][DataRow("拍摄总金额")][DataRow("定金")][DataRow("已收金额")]
    [DataRow("保存草稿")][DataRow("保存排期")]
    public void BookingEditor_ContainsFourStepPeopleAndMoneyWorkflow(string token) => Contains("src/RAWSelectionAssistant/Views/ShootBookingEditorView.xaml", token);

    [TestMethod]
    [DataRow("只监看所选文件夹顶层")][DataRow("不会递归扫描子文件夹")][DataRow("CompactMoreButton")][DataRow("更多联机监看工具")]
    [DataRow("照片浏览器")][DataRow("自动最新")][DataRow("客户监看")][DataRow("检查器")][DataRow("任务中心")][DataRow("全屏监看")]
    [DataRow("缩略图会按需加载")][DataRow("选择一张照片后才能进入全屏监看")][DataRow("ShowTetherEmptyState")]
    [DataRow("选择看守文件夹")][DataRow("开始联机会话")][DataRow("HasSelection")][DataRow("IsInspectorCollapsed")]
    public void TetherRuntime_ContainsCompactSafeUserFacingStates(string token) => Contains("src/RAWSelectionAssistant/Views/TetherCaptureView.xaml", token);

    [TestMethod]
    [DataRow("CalendarItemStyle")][DataRow("CalendarDayButtonStyle")][DataRow("DatePickerTextBox")][DataRow("PART_Popup")]
    [DataRow("PART_PreviousButton")][DataRow("PART_NextButton")][DataRow("PART_HeaderButton")][DataRow("PART_Calendar")]
    [DataRow("DropdownBackgroundBrush")][DataRow("TextPrimaryBrush")][DataRow("AccentBrush")][DataRow("InputBorderBrush")]
    public void DatePickerTheme_ContainsCompleteRuntimeTemplate(string token) => Contains("src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Inputs.xaml", token);

    [TestMethod]
    public void ComboBoxTheme_PreservesDisplayMemberPathAtRuntime() =>
        Contains(
            "src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Inputs.xaml",
            "Content=\"{TemplateBinding SelectionBoxItem}\"",
            "ContentTemplate=\"{TemplateBinding SelectionBoxItemTemplate}\"",
            "ContentTemplateSelector=\"{TemplateBinding ItemTemplateSelector}\"",
            "ContentStringFormat=\"{TemplateBinding SelectionBoxItemStringFormat}\"");

    [TestMethod]
    [DataRow(ShootBookingStatus.Tentative, "待确定")][DataRow(ShootBookingStatus.Confirmed, "已确认")][DataRow(ShootBookingStatus.Preparing, "准备中")]
    [DataRow(ShootBookingStatus.Shooting, "拍摄中")][DataRow(ShootBookingStatus.Completed, "已拍摄")][DataRow(ShootBookingStatus.AwaitingSelectionDelivery, "待发送选片")]
    [DataRow(ShootBookingStatus.AwaitingSelection, "待选片")][DataRow(ShootBookingStatus.Selected, "已选片")][DataRow(ShootBookingStatus.AwaitingRetouch, "待精修")]
    [DataRow(ShootBookingStatus.Retouched, "已精修")][DataRow(ShootBookingStatus.AwaitingDelivery, "待交付")][DataRow(ShootBookingStatus.Delivered, "已交付")]
    [DataRow(ShootBookingStatus.Cancelled, "已取消")][DataRow(ShootBookingStatus.Postponed, "已延期")]
    public void BookingStatusConverter_NeverExposesInternalEnum(ShootBookingStatus status, string expected)
    {
        var actual = new StatusToTextConverter().Convert(status, typeof(string), null!, CultureInfo.GetCultureInfo("zh-CN"));
        Assert.AreEqual(expected, actual);
        Assert.AreNotEqual(status.ToString(), actual);
    }

    [TestMethod]
    public void RuntimeDialogs_AreThemedAndNoDirectMessageBoxRemains()
    {
        var source = string.Join('\n', Directory.GetFiles(Path.Combine(Root(), "src", "RAWSelectionAssistant"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.IsFalse(source.Contains("MessageBox.Show", StringComparison.Ordinal));
        Contains("src/RAWSelectionAssistant/Views/ThemedMessageDialog.xaml", "DynamicResource", "AutomationProperties.Name", "PrimaryButton", "SecondaryButton");
    }

    [TestMethod]
    public void UpgradeTutorialButtons_CloseSafelyInModalAndRuntimeReviewWindows()
    {
        Contains("src/RAWSelectionAssistant/Views/UpgradeTutorialWindow.xaml.cs", "public bool Accepted", "Accepted = true", "Accepted = false", "Close();");
        var source = File.ReadAllText(Path.Combine(Root(), "src", "RAWSelectionAssistant", "Views", "UpgradeTutorialWindow.xaml.cs"));
        Assert.IsFalse(source.Contains("DialogResult", StringComparison.Ordinal));
        Contains("src/RAWSelectionAssistant/MainWindow.xaml.cs", "offer.ShowDialog();", "offer.Accepted");
    }

    [TestMethod]
    public void ReadOnlyRuntimeValues_AreNeverBoundBackToViewModels()
    {
        Contains("src/RAWSelectionAssistant/Views/BookingDocumentsPanel.xaml", "PreviewText, Mode=OneWay", "PreviewTextWrapping, Mode=OneWay");
        Contains("src/RAWSelectionAssistant/Views/ShootBookingEditorView.xaml", "BalanceLabel, Mode=OneWay", "BalanceText, Mode=OneWay", "MoneyWarningText, Mode=OneWay");
    }

    [TestMethod]
    public void Rc3ViewsRemainValidXaml()
    {
        foreach (var relative in new[] { "Views/WorkCalendarView.xaml", "Views/FinanceView.xaml", "Views/BookingDocumentsPanel.xaml", "Views/ShootBookingEditorView.xaml", "Views/TetherCaptureView.xaml", "Views/ThemedMessageDialog.xaml" })
            XDocument.Parse(File.ReadAllText(Path.Combine(Root(), "src", "RAWSelectionAssistant", relative.Replace('/', Path.DirectorySeparatorChar))));
    }

    private static void Contains(string relative, params string[] tokens)
    {
        var text = File.ReadAllText(Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar)));
        foreach (var token in tokens) StringAssert.Contains(text, token);
    }

    private static string Root() { for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent) if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName; throw new DirectoryNotFoundException(); }
}
