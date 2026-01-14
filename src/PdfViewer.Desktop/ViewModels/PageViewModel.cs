using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PdfViewer.Desktop.ViewModels;

public partial class PageViewModel : ObservableObject
{
    [ObservableProperty]
    private int _pageNumber;

    [ObservableProperty]
    private Bitmap? _pageImage;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private double _width;

    [ObservableProperty]
    private double _height;
}
