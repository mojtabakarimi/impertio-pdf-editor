using CommunityToolkit.Mvvm.ComponentModel;

namespace PdfViewer.Desktop.ViewModels;

public partial class HighlightRectViewModel : ObservableObject
{
    [ObservableProperty]
    private double _left;

    [ObservableProperty]
    private double _top;

    [ObservableProperty]
    private double _width;

    [ObservableProperty]
    private double _height;

    [ObservableProperty]
    private bool _isCurrentMatch;
}
