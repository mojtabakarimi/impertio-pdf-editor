using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PdfViewer.Desktop.ViewModels;

public partial class ThumbnailViewModel : ObservableObject
{
    [ObservableProperty]
    private int _pageNumber;

    [ObservableProperty]
    private Bitmap? _thumbnailImage;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isLoading = true;
}
