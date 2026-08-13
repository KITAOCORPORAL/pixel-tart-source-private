using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.Services;

internal static class InputRoutingDiagnostics
{
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    [Conditional("DEBUG"), Conditional("INPUT_ROUTING_DIAGNOSTICS")]
    public static void RecordWindowMouse(
        UIElement root,
        MouseButtonEventArgs args,
        string eventName,
        string surface,
        string overlay,
        int? tutorialStep)
    {
        var point = args.GetPosition(root);
        var inputHit = root.InputHitTest(point) as DependencyObject;
        var visualHit = VisualTreeHelper.HitTest(root, point)?.VisualHit;
        Write(new
        {
            timestamp = DateTimeOffset.UtcNow,
            event_name = eventName,
            mouse_position = new { x = Math.Round(point.X, 1), y = Math.Round(point.Y, 1) },
            original_source = Describe(args.OriginalSource as DependencyObject),
            source = Describe(args.Source as DependencyObject),
            handled = args.Handled,
            input_hit_test = Describe(inputHit),
            visual_hit_test = Describe(visualHit),
            visual_parent_chain = ParentChain(inputHit),
            current_surface = SafeToken(surface),
            current_overlay = SafeToken(overlay),
            tutorial_step = tutorialStep
        });
    }

    [Conditional("DEBUG"), Conditional("INPUT_ROUTING_DIAGNOSTICS")]
    public static void RecordWindowKey(KeyEventArgs args, string surface, string overlay, int? tutorialStep) =>
        Write(new
        {
            timestamp = DateTimeOffset.UtcNow,
            event_name = "PreviewKeyDown",
            key = args.Key.ToString(),
            handled = args.Handled,
            original_source = Describe(args.OriginalSource as DependencyObject),
            source = Describe(args.Source as DependencyObject),
            current_surface = SafeToken(surface),
            current_overlay = SafeToken(overlay),
            tutorial_step = tutorialStep
        });

    [Conditional("DEBUG"), Conditional("INPUT_ROUTING_DIAGNOSTICS")]
    public static void RecordControlEvent(DependencyObject control, string eventName, object? originalSource, object? source, bool handled) =>
        Write(new
        {
            timestamp = DateTimeOffset.UtcNow,
            event_name = SafeToken(eventName),
            control = Describe(control),
            original_source = Describe(originalSource as DependencyObject),
            source = Describe(source as DependencyObject),
            handled
        });

    [Conditional("DEBUG"), Conditional("INPUT_ROUTING_DIAGNOSTICS")]
    public static void RecordShellEvent(string eventName, string surface, string overlay, int? tutorialStep) =>
        Write(new
        {
            timestamp = DateTimeOffset.UtcNow,
            event_name = SafeToken(eventName),
            current_surface = SafeToken(surface),
            current_overlay = SafeToken(overlay),
            tutorial_step = tutorialStep
        });

    private static object Describe(DependencyObject? element)
    {
        if (element is null) return new { type = "None", name = "", automation_id = "", z_index = 0, is_hit_test_visible = false };
        var framework = element as FrameworkElement;
        var uiElement = element as UIElement;
        return new
        {
            type = SafeToken(element.GetType().Name),
            name = SafeToken(framework?.Name),
            automation_id = SafeToken(AutomationProperties.GetAutomationId(element)),
            z_index = uiElement is null ? 0 : Panel.GetZIndex(uiElement),
            is_hit_test_visible = uiElement?.IsHitTestVisible ?? false
        };
    }

    private static IReadOnlyList<object> ParentChain(DependencyObject? element)
    {
        var result = new List<object>();
        for (var current = element; current is not null && result.Count < 16; current = Parent(current))
            result.Add(Describe(current));
        return result;
    }

    private static DependencyObject? Parent(DependencyObject element)
    {
        try
        {
            return VisualTreeHelper.GetParent(element) ?? LogicalTreeHelper.GetParent(element);
        }
        catch (InvalidOperationException)
        {
            return LogicalTreeHelper.GetParent(element);
        }
    }

    private static string SafeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var builder = new StringBuilder(Math.Min(value.Length, 64));
        foreach (var character in value.Take(64))
            builder.Append(char.IsLetterOrDigit(character) || character is '_' or '-' ? character : '_');
        return builder.ToString();
    }

    private static void Write(object value)
    {
        try
        {
            AppDataPaths.EnsureCreated();
            var line = JsonSerializer.Serialize(value, JsonOptions);
            lock (Gate)
            {
                using var stream = new FileStream(
                    Path.Combine(AppDataPaths.LogDirectory, "input-routing.jsonl"),
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.Read,
                    4096,
                    FileOptions.WriteThrough);
                using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                writer.WriteLine(line);
                writer.Flush();
                stream.Flush(true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
        }
    }
}
