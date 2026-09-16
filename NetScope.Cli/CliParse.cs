namespace NetScope.Cli;

/// <summary>Shared argument parsing for CLI commands — presentation only.</summary>
internal static class CliParse
{
    public const int MinHours = 1;
    public const int MaxHours = 8760;

    public static bool TryInt(string? value, string name, out int result)
    {
        if (int.TryParse(value, out result))
            return true;

        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"Invalid {name}: {value}");
        Console.ResetColor();
        result = 0;
        return false;
    }

    public static bool TryLong(string? value, string name, out long result)
    {
        if (long.TryParse(value, out result))
            return true;

        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"Invalid {name}: {value}");
        Console.ResetColor();
        result = 0;
        return false;
    }

    public static int ClampHours(int hours) => Math.Clamp(hours, MinHours, MaxHours);
}
