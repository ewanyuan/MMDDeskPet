using System.Globalization;

namespace MMDDeskPet;

internal sealed class LaunchOptions
{
    internal string? ModelPath { get; private set; }

    internal string? MotionPath { get; private set; }

    internal string? LibraryFolder { get; private set; }

    internal bool SmokeTest { get; private set; }

    internal double? ExitAfterSeconds { get; private set; }

    internal bool SelfTest { get; private set; }

    internal bool ExpressionAudit { get; private set; }

    internal static bool TryParse(IReadOnlyList<string> args, out LaunchOptions options, out string? error)
    {
        options = new LaunchOptions();
        error = null;
        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (HasName(argument, "--model"))
            {
                if (!TryTakeValue(args, argument, "--model", ref index, out var value, out error))
                {
                    return false;
                }

                options.ModelPath = value;
            }
            else if (HasName(argument, "--motion"))
            {
                if (!TryTakeValue(args, argument, "--motion", ref index, out var value, out error))
                {
                    return false;
                }

                options.MotionPath = value;
            }
            else if (HasName(argument, "--library"))
            {
                if (!TryTakeValue(args, argument, "--library", ref index, out var value, out error))
                {
                    return false;
                }

                options.LibraryFolder = value;
            }
            else if (HasName(argument, "--exit-after"))
            {
                if (!TryTakeValue(args, argument, "--exit-after", ref index, out var value, out error))
                {
                    return false;
                }

                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
                    || seconds <= 0
                    || double.IsNaN(seconds)
                    || double.IsInfinity(seconds))
                {
                    error = "--exit-after requires a positive number of seconds.";
                    return false;
                }

                options.ExitAfterSeconds = seconds;
            }
            else if (argument.Equals("--smoke-test", StringComparison.OrdinalIgnoreCase))
            {
                options.SmokeTest = true;
            }
            else if (argument.Equals("--mmd-runtime-self-test", StringComparison.OrdinalIgnoreCase))
            {
                options.SelfTest = true;
            }
            else if (argument.Equals("--mmd-expression-audit", StringComparison.OrdinalIgnoreCase))
            {
                options.ExpressionAudit = true;
            }
        }

        return true;
    }

    private static bool HasName(string argument, string name)
    {
        return argument.Equals(name, StringComparison.OrdinalIgnoreCase)
            || argument.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryTakeValue(
        IReadOnlyList<string> args,
        string argument,
        string name,
        ref int index,
        out string value,
        out string? error)
    {
        var prefix = name + "=";
        if (argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = argument[prefix.Length..];
            if (string.IsNullOrWhiteSpace(value))
            {
                error = name + " requires a value.";
                return false;
            }

            error = null;
            return true;
        }

        if (index + 1 >= args.Count || args[index + 1].StartsWith('-'))
        {
            value = string.Empty;
            error = name + " requires a value.";
            return false;
        }

        index++;
        value = args[index];
        error = null;
        return true;
    }
}
