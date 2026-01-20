using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace PdfViewer.Desktop.Converters;

public class HighlightColorConverter : IValueConverter
{
    public static readonly HighlightColorConverter Instance = new();

    // Yellow for normal matches, orange for current match
    private static readonly Color NormalMatchColor = Color.FromRgb(255, 255, 0); // Yellow
    private static readonly Color CurrentMatchColor = Color.FromRgb(255, 165, 0); // Orange

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isCurrentMatch)
        {
            return isCurrentMatch ? CurrentMatchColor : NormalMatchColor;
        }
        return NormalMatchColor;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
