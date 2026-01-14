using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace PdfViewer.Desktop.ViewModels;

public partial class BookmarkViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private int _pageNumber;

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private ObservableCollection<BookmarkViewModel> _children = new();

    public bool HasChildren => Children.Count > 0;
}
