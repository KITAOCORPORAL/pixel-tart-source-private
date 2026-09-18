using System.Reflection;

namespace RAWSelectionAssistant.Services;

public static class StartupDiagnostics
{
    private static string InformationalVersion => typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
    public static string ProductSourceSha => InformationalVersion.Split('+').ElementAtOrDefault(1) ?? "unversioned";
    public static string BuildId => "2.3.0-dev." + ProductSourceSha[..Math.Min(7, ProductSourceSha.Length)];
    public static string ErrorCode(string stage) => "PT-START-" + stage[..2].PadLeft(4, '0');
    public static string ErrorSummary(string stage) => $"软件启动失败，已记录诊断信息。\n错误编号：{ErrorCode(stage)}\n原因：{(stage.StartsWith("07", StringComparison.Ordinal) ? "主窗口加载失败" : "启动初始化未完成")}\n版本：{BuildId}";
}
