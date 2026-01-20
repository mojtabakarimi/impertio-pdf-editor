using Avalonia.Data.Converters;
using Avalonia.Media;
using PdfViewer.Desktop.ViewModels;
using System;
using System.Globalization;

namespace PdfViewer.Desktop.Converters;

public class HighlightPositionConverter : IValueConverter
{
    public static readonly HighlightPositionConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is HighlightRectViewModel vm)
        {
            return new TranslateTransform(vm.Left, vm.Top);
        }
        return new TranslateTransform(0, 0);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
