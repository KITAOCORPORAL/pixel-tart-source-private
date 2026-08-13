using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Automation;

namespace PixelTart.UiAutomationValidation;

internal static partial class Program
{
    private const string Protocol = "pixel-tart-ui-automation-validation/v1";

    [STAThread]
    private static int Main(string[] args)
    {
        ParsedCommand? parsed = null;
        try
        {
            parsed = ParsedCommand.Parse(args);
            if (parsed.Command is "help" or "--help" or "-h")
            {
                Console.WriteLine(Usage.Text);
                return 0;
            }

            var result = Execute(parsed);
            ReportWriter.Write(parsed, result.Payload);
            return result.ExitCode;
        }
        catch (UsageException exception)
        {
            var payload = Failure(
                parsed?.Command ?? "invalid",
                "invalid_arguments",
                exception.Message);
            ReportWriter.Write(parsed, payload);
            return 2;
        }
        catch (TargetRejectedException exception)
        {
            var payload = Failure(
                parsed?.Command ?? "unknown",
                "target_rejected",
                exception.Message);
            ReportWriter.Write(parsed, payload);
            return 5;
        }
        catch (Exception exception)
        {
            var payload = Failure(
                parsed?.Command ?? "unknown",
                "unexpected_error",
                $"{exception.GetType().Name}: {exception.Message}");
            ReportWriter.Write(parsed, payload);
            return 1;
        }
    }

    private static CommandResult Execute(ParsedCommand command) => command.Command switch
    {
        "launch" => Launch(command),
        "inspect-id" => Inspect(command, SelectorKind.AutomationId),
        "inspect-name" => Inspect(command, SelectorKind.AutomationName),
        "invoke-id" => Invoke(command, SelectorKind.AutomationId),
        "invoke-name" => Invoke(command, SelectorKind.AutomationName),
        "click-id" => ClickByAutomationId(command),
        "list-buttons" => ListButtons(command),
        "press-escape" => PressEscape(command),
        _ => throw new UsageException($"Unknown command '{command.Command}'.")
    };

    private static CommandResult Launch(ParsedCommand command)
    {
        var executablePath = Path.GetFullPath(command.Required("exe"));
        var acceptanceRoot = Path.GetFullPath(command.Required("acceptance-root"));
        var timeout = command.Timeout();

        if (!File.Exists(executablePath))
        {
            return FailedCommand("launch", "executable_not_found", "The acceptance executable does not exist.", 4);
        }

        if (!Path.GetFileNameWithoutExtension(executablePath)
                .EndsWith(".Acceptance", StringComparison.OrdinalIgnoreCase))
        {
            throw new TargetRejectedException("Only an executable whose name ends in '.Acceptance.exe' may be launched.");
        }

        IsolationGuard.Validate(acceptanceRoot);
        var localAppData = Path.Combine(acceptanceRoot, "LocalAppData");
        var roamingAppData = Path.Combine(acceptanceRoot, "RoamingAppData");
        var temp = Path.Combine(acceptanceRoot, "Temp");
        Directory.CreateDirectory(localAppData);
        Directory.CreateDirectory(roamingAppData);
        Directory.CreateDirectory(temp);

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath)!,
            UseShellExecute = false
        };
        startInfo.Environment["PIXEL_TART_ACCEPTANCE_ROOT"] = acceptanceRoot;
        startInfo.Environment["LOCALAPPDATA"] = localAppData;
        startInfo.Environment["APPDATA"] = roamingAppData;
        startInfo.Environment["TEMP"] = temp;
        startInfo.Environment["TMP"] = temp;
        foreach (var argument in command.All("argument"))
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The acceptance process could not be started.");

        var target = TargetWindow.WaitFor(process.Id, timeout);
        if (target is null)
        {
            var exited = process.HasExited;
            return new CommandResult(4, Envelope("launch", false, new
            {
                error = new
                {
                    code = exited ? "process_exited_before_window" : "window_timeout",
                    message = exited
                        ? "The acceptance process exited before exposing a top-level window."
                        : "The acceptance process did not expose a top-level window before the timeout."
                },
                target = TargetSummary.ForProcess(process.Id, Path.GetFileName(executablePath)),
                isolation = IsolationSummary.Create(acceptanceRoot)
            }));
        }

        target.EnsureAllowed();
        var waitAutomationId = command.Optional("wait-automation-id");
        object? readyControl = null;
        if (!string.IsNullOrWhiteSpace(waitAutomationId))
        {
            var match = UiaQuery.FindSingle(target, SelectorKind.AutomationId, waitAutomationId, timeout);
            if (!match.Success)
            {
                return new CommandResult(3, Envelope("launch", false, new
                {
                    error = match.Error,
                    target = target.Summary(),
                    isolation = IsolationSummary.Create(acceptanceRoot),
                    selector_type = SelectorKind.AutomationId.JsonName(),
                    selector = Safe.Text(waitAutomationId),
                    match_count = match.MatchCount
                }));
            }

            readyControl = UiaSnapshot.From(match.Element!);
        }

        return new CommandResult(0, Envelope("launch", true, new
        {
            target = target.Summary(),
            isolation = IsolationSummary.Create(acceptanceRoot),
            ready_control = readyControl
        }));
    }

    private static CommandResult Inspect(ParsedCommand command, SelectorKind selectorKind)
    {
        var target = Target(command);
        var selector = command.Required(selectorKind.OptionName());
        var match = UiaQuery.FindSingle(target, selectorKind, selector, command.Timeout());
        if (!match.Success)
        {
            return MatchFailure("inspect", target, selectorKind, selector, match);
        }

        return new CommandResult(0, Envelope("inspect", true, new
        {
            target = target.Summary(),
            selector_type = selectorKind.JsonName(),
            diagnostic_only = selectorKind == SelectorKind.AutomationName,
            selector = Safe.Text(selector),
            match_count = 1,
            control = UiaSnapshot.From(match.Element!)
        }));
    }

    private static CommandResult Invoke(ParsedCommand command, SelectorKind selectorKind)
    {
        var target = Target(command);
        var selector = command.Required(selectorKind.OptionName());
        var match = UiaQuery.FindSingle(target, selectorKind, selector, command.Timeout());
        if (!match.Success)
        {
            return MatchFailure("invoke", target, selectorKind, selector, match);
        }

        var before = UiaSnapshot.From(match.Element!);
        if (!match.Element!.TryGetCurrentPattern(InvokePattern.Pattern, out var rawPattern) ||
            rawPattern is not InvokePattern invokePattern)
        {
            return new CommandResult(4, Envelope("invoke", false, new
            {
                error = new { code = "invoke_pattern_unavailable", message = "The selected control does not expose InvokePattern." },
                target = target.Summary(),
                selector_type = selectorKind.JsonName(),
                diagnostic_only = selectorKind == SelectorKind.AutomationName,
                selector = Safe.Text(selector),
                control = before
            }));
        }

        invokePattern.Invoke();
        return new CommandResult(0, Envelope("invoke", true, new
        {
            target = target.Summary(),
            selector_type = selectorKind.JsonName(),
            diagnostic_only = selectorKind == SelectorKind.AutomationName,
            selector = Safe.Text(selector),
            invoked = true,
            control = before
        }));
    }

    private static CommandResult ListButtons(ParsedCommand command)
    {
        var target = Target(command);
        var buttons = UiaQuery.VisibleButtons(target)
            .Select(UiaSnapshot.From)
            .OrderBy(static item => item.BoundingRectangle.Y)
            .ThenBy(static item => item.BoundingRectangle.X)
            .ToArray();

        return new CommandResult(0, Envelope("list-buttons", true, new
        {
            target = target.Summary(),
            selector_type = "visible_button_dump",
            diagnostic_only = true,
            count = buttons.Length,
            buttons
        }));
    }

    private static CommandResult ClickByAutomationId(ParsedCommand command)
    {
        var target = Target(command);
        var targetSummary = target.Summary();
        var automationId = command.Required("automation-id");
        var match = UiaQuery.FindSingle(target, SelectorKind.AutomationId, automationId, command.Timeout());
        if (!match.Success)
        {
            return MatchFailure("click-id", target, SelectorKind.AutomationId, automationId, match);
        }

        var pre = UiaClickSnapshot.From(match.Element!);
        var center = pre.Center();
        if (!pre.IsEnabled || pre.IsOffscreen || !center.IsValid)
        {
            return new CommandResult(4, Envelope("click-id", false, new
            {
                error = new
                {
                    code = "control_not_clickable",
                    message = "The selected control must be enabled, on-screen, and have a valid bounding rectangle."
                },
                target = targetSummary,
                selector_type = SelectorKind.AutomationId.JsonName(),
                automation_id = Safe.Text(automationId),
                center,
                pre,
                uia_element_from_point = (object?)null,
                send_input_success = false,
                target_disappeared = false
            }));
        }

        var dispatch = WindowsInput.SendMouseClick(target, center);
        if (!dispatch.Success)
        {
            return new CommandResult(4, Envelope("click-id", false, new
            {
                error = new { code = dispatch.ErrorCode, message = dispatch.ErrorMessage },
                target = targetSummary,
                selector_type = SelectorKind.AutomationId.JsonName(),
                automation_id = Safe.Text(automationId),
                center,
                pre,
                uia_element_from_point = dispatch.ElementFromPoint,
                send_input_success = false,
                target_disappeared = false
            }));
        }

        var post = PostInputProbe.Observe(
            target,
            automationId,
            TimeSpan.FromMilliseconds(Math.Clamp(command.Timeout().TotalMilliseconds, 100, 2000)));
        return new CommandResult(0, Envelope("click-id", true, new
        {
            target = targetSummary,
            selector_type = SelectorKind.AutomationId.JsonName(),
            automation_id = Safe.Text(automationId),
            center,
            pre,
            uia_element_from_point = dispatch.ElementFromPoint,
            send_input_success = true,
            target_disappeared = post.TargetDisappeared,
            target_check_timed_out = post.TargetCheckTimedOut,
            window_still_exists = post.WindowStillExists,
            window_responsive = post.WindowResponsive
        }));
    }

    private static CommandResult PressEscape(ParsedCommand command)
    {
        var target = Target(command);
        var targetSummary = target.Summary();
        var dispatch = WindowsInput.SendEscape(target);
        if (!dispatch.Success)
        {
            return new CommandResult(4, Envelope("press-escape", false, new
            {
                error = new { code = dispatch.ErrorCode, message = dispatch.ErrorMessage },
                target = targetSummary,
                input = new { key = "Escape", method = "SetForegroundWindow+SendInput", dispatched = false }
            }));
        }

        return new CommandResult(0, Envelope("press-escape", true, new
        {
            target = targetSummary,
            input = new
            {
                key = "Escape",
                method = "SetForegroundWindow+SendInput",
                dispatched = true,
                foreground_confirmed_before_send = true
            }
        }));
    }

    private static TargetWindow Target(ParsedCommand command)
    {
        var pid = command.RequiredInt("pid");
        var target = TargetWindow.WaitFor(pid, command.Timeout())
            ?? throw new TargetRejectedException("No visible top-level window was found for the requested process.");
        target.EnsureAllowed();
        return target;
    }

    private static CommandResult MatchFailure(
        string operation,
        TargetWindow target,
        SelectorKind selectorKind,
        string selector,
        UiaMatch match) => new(3, Envelope(operation, false, new
        {
            error = match.Error,
            target = target.Summary(),
            selector_type = selectorKind.JsonName(),
            diagnostic_only = selectorKind == SelectorKind.AutomationName,
            selector = Safe.Text(selector),
            match_count = match.MatchCount
        }));

    private static CommandResult FailedCommand(string command, string code, string message, int exitCode) =>
        new(exitCode, Failure(command, code, message));

    private static object Failure(string command, string code, string message) => Envelope(command, false, new
    {
        error = new { code, message = Safe.Text(message) }
    });

    private static object Envelope(string command, bool success, object details) => new
    {
        protocol = Protocol,
        generated_at = DateTimeOffset.UtcNow,
        command,
        success,
        details
    };
}

internal sealed record CommandResult(int ExitCode, object Payload);

internal enum SelectorKind
{
    AutomationId,
    AutomationName
}

internal static class SelectorKindExtensions
{
    public static string OptionName(this SelectorKind kind) => kind switch
    {
        SelectorKind.AutomationId => "automation-id",
        SelectorKind.AutomationName => "name",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public static string JsonName(this SelectorKind kind) => kind switch
    {
        SelectorKind.AutomationId => "automation_id",
        SelectorKind.AutomationName => "automation_name",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}

internal sealed class ParsedCommand
{
    private readonly Dictionary<string, List<string>> _options;

    private ParsedCommand(string command, Dictionary<string, List<string>> options)
    {
        Command = command;
        _options = options;
    }

    public string Command { get; }

    public static ParsedCommand Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            throw new UsageException("A command is required. Use 'help' for examples.");
        }

        var options = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < args.Count; index++)
        {
            var token = args[index];
            if (!token.StartsWith("--", StringComparison.Ordinal) || token.Length == 2)
            {
                throw new UsageException($"Unexpected argument '{token}'. Options must use --name value.");
            }

            var separator = token.IndexOf('=');
            string name;
            string value;
            if (separator > 2)
            {
                name = token[2..separator];
                value = token[(separator + 1)..];
            }
            else
            {
                name = token[2..];
                if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    value = "true";
                }
                else
                {
                    value = args[++index];
                }
            }

            if (!options.TryGetValue(name, out var values))
            {
                values = [];
                options.Add(name, values);
            }
            values.Add(value);
        }

        return new ParsedCommand(args[0].ToLowerInvariant(), options);
    }

    public string Required(string name)
    {
        var value = Optional(name);
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
        {
            throw new UsageException($"--{name} requires a value.");
        }
        return value;
    }

    public int RequiredInt(string name)
    {
        var value = Required(name);
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result) || result <= 0)
        {
            throw new UsageException($"--{name} must be a positive integer.");
        }
        return result;
    }

    public string? Optional(string name) =>
        _options.TryGetValue(name, out var values) && values.Count > 0 ? values[^1] : null;

    public IReadOnlyList<string> All(string name) =>
        _options.TryGetValue(name, out var values) ? values : [];

    public TimeSpan Timeout()
    {
        var raw = Optional("timeout-ms") ?? "15000";
        if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var milliseconds) ||
            milliseconds is < 100 or > 120_000)
        {
            throw new UsageException("--timeout-ms must be between 100 and 120000.");
        }
        return TimeSpan.FromMilliseconds(milliseconds);
    }
}

internal sealed class UsageException(string message) : Exception(message);

internal sealed class TargetRejectedException(string message) : Exception(message);

internal static class IsolationGuard
{
    public static void Validate(string acceptanceRoot)
    {
        var normalized = EnsureTrailingSeparator(Path.GetFullPath(acceptanceRoot));
        var forbidden = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        }
        .Where(static path => !string.IsNullOrWhiteSpace(path))
        .Select(path => EnsureTrailingSeparator(Path.GetFullPath(path)));

        if (Path.GetPathRoot(normalized)?.Equals(normalized, StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new TargetRejectedException("The acceptance root cannot be a drive root.");
        }

        foreach (var path in forbidden)
        {
            if (normalized.Equals(path, StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith(path, StringComparison.OrdinalIgnoreCase))
            {
                throw new TargetRejectedException("The acceptance root must not be inside a real user-data folder.");
            }
        }
    }

    private static string EnsureTrailingSeparator(string path) =>
        path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
}

internal sealed record IsolationSummary(
    string Mode,
    string RootToken,
    bool LocalAppDataRedirected,
    bool AppDataRedirected,
    bool TempRedirected)
{
    public static IsolationSummary Create(string root)
    {
        var normalized = Path.GetFullPath(root).ToUpperInvariant();
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return new IsolationSummary(
            "explicit_acceptance_root",
            Convert.ToHexString(digest)[..12],
            true,
            true,
            true);
    }
}

internal static class ReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static void Write(ParsedCommand? command, object payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var outputPath = command?.Optional("output");
        if (!string.IsNullOrWhiteSpace(outputPath) && !string.Equals(outputPath, "true", StringComparison.OrdinalIgnoreCase))
        {
            var fullPath = Path.GetFullPath(outputPath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(fullPath, json, new UTF8Encoding(false));
        }
        Console.WriteLine(json);
    }
}

internal static partial class Safe
{
    [GeneratedRegex(@"(?i)(?:[a-z]:\\|\\\\)[^\r\n\""']+")]
    private static partial Regex AbsolutePathRegex();

    [GeneratedRegex(@"(?i)\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(?<!\d)(?:\+?\d[\d\s().-]{6,}\d)(?!\d)")]
    private static partial Regex PhoneRegex();

    public static string Text(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var result = new string(value.Where(static character => !char.IsControl(character) || character is '\t').ToArray());
        result = AbsolutePathRegex().Replace(result, "[redacted-path]");
        result = EmailRegex().Replace(result, "[redacted-email]");
        result = PhoneRegex().Replace(result, "[redacted-number]");
        result = result.Trim();
        return result.Length <= 160 ? result : result[..157] + "...";
    }
}

internal static class Usage
{
    public const string Text = """
Pixel Tart UI Automation Validation

Commands:
  launch --exe <KitaoPhotoSelector.Acceptance.exe> --acceptance-root <isolated-root>
         [--argument <value>] [--wait-automation-id <id>] [--timeout-ms 15000] [--output <json>]
  inspect-id --pid <pid> --automation-id <id> [--timeout-ms 15000] [--output <json>]
  inspect-name --pid <pid> --name <exact-name> [--timeout-ms 15000] [--output <json>]
  invoke-id --pid <pid> --automation-id <id> [--timeout-ms 15000] [--output <json>]
  invoke-name --pid <pid> --name <exact-name> [--timeout-ms 15000] [--output <json>]
  click-id --pid <pid> --automation-id <id> [--timeout-ms 15000] [--output <json>]
  list-buttons --pid <pid> [--timeout-ms 15000] [--output <json>]
  press-escape --pid <pid> [--timeout-ms 15000] [--output <json>]

AutomationName selection is diagnostic-only. Formal revalidation must use AutomationId.
The tool rejects browser targets and never launches non-Acceptance executables.
""";
}
