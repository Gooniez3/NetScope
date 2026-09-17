using System.Globalization;
using Avalonia.Data.Converters;

namespace NetScope.App.Converters;

/// <summary>
/// Sizes a DataGrid to its header plus rows so short lists do not stretch into empty floor.
/// </summary>
public sealed class RowListHeightConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var count = value is int i ? i : 0;
        if (count <= 0)
            return 0d;

        const double header = 32;
        const double row = 28;
        var cap = 200d;
        if (parameter is string text && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            cap = parsed;

        return Math.Min(header + (count * row) + 8, cap);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
