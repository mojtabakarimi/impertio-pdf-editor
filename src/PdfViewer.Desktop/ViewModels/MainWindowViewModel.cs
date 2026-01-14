using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfViewer.Core.Interfaces;
using PdfViewer.Core.Models;
using PdfViewer.Core.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace PdfViewer.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly PdfDocumentService _documentService;
    private readonly ITextSearchService _textSearchService;
    private readonly IPrintService _printService;

    [ObservableProperty]
    private string _documentTitle = "PDF Viewer";

    [ObservableProperty]
    private int _currentPageNumber = 1;

    [ObservableProperty]
    private int _totalPages = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayScaleFactor))]
    private double _zoomFactor = 1.0;

    [ObservableProperty]
    private Bitmap? _currentPageImage;

    [ObservableProperty]
    private bool _isDocumentLoaded;

    [ObservableProperty]
    private string _statusText = "No document loaded";

    [ObservableProperty]
    private int _rotationAngle = 0;

    [ObservableProperty]
    private ZoomMode _zoomMode = ZoomMode.Manual;

    [ObservableProperty]
    private double _viewportWidth = 800;

    [ObservableProperty]
    private double _viewportHeight = 600;

    [ObservableProperty]
    private bool _isSidebarVisible = true;

    [ObservableProperty]
    private ObservableCollection<ThumbnailViewModel> _thumbnails = new();

    [ObservableProperty]
    private ObservableCollection<BookmarkViewModel> _bookmarks = new();

    [ObservableProperty]
    private bool _hasBookmarks;

    [ObservableProperty]
    private bool _isThumbnailsTabSelected = true;

    [ObservableProperty]
    private bool _isBookmarksTabSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSinglePageMode))]
    [NotifyPropertyChangedFor(nameof(IsContinuousMode))]
    [NotifyPropertyChangedFor(nameof(IsTwoPageMode))]
    private ViewMode _viewMode = ViewMode.Continuous;

    public bool IsSinglePageMode => ViewMode == ViewMode.SinglePage;
    public bool IsContinuousMode => ViewMode == ViewMode.Continuous;
    public bool IsTwoPageMode => ViewMode == ViewMode.TwoPage;

    [ObservableProperty]
    private ObservableCollection<PageViewModel> _pages = new();

    [ObservableProperty]
    private bool _isSearchPanelVisible;

    [ObservableProperty]
    private SearchViewModel? _searchViewModel;

    // Full Screen Mode
    [ObservableProperty]
    private bool _isFullScreen;

    [ObservableProperty]
    private bool _isMenuBarVisible = true;

    [ObservableProperty]
    private bool _isRibbonVisible = true;

    [ObservableProperty]
    private bool _isStatusBarVisible = true;

    // Tool Mode
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSelectToolSelected))]
    [NotifyPropertyChangedFor(nameof(IsHandToolSelected))]
    private ToolMode _currentTool = ToolMode.Hand;

    public bool IsSelectToolSelected => CurrentTool == ToolMode.Select;
    public bool IsHandToolSelected => CurrentTool == ToolMode.Hand;

    // Page History (Back/Forward navigation)
    private readonly Stack<int> _pageHistoryBack = new();
    private readonly Stack<int> _pageHistoryForward = new();
    private bool _isNavigatingHistory = false;

    // Recent Files
    [ObservableProperty]
    private ObservableCollection<string> _recentFiles = new();
    private const int MaxRecentFiles = 10;

    // Loading Progress
    [ObservableProperty]
    private double _loadingProgress = 0;

    [ObservableProperty]
    private bool _isLoading;

    // Second page for Two-Page View
    [ObservableProperty]
    private Bitmap? _secondPageImage;

    // Current file path for document properties
    private string? _currentFilePath;

    [ObservableProperty]
    private string _fileSizeText = "";

    private PdfDocument? _currentDocument;
    private RenderOptions _renderOptions;

    public MainWindowViewModel(PdfDocumentService documentService, ITextSearchService textSearchService, IPrintService printService)
    {
        _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
        _textSearchService = textSearchService ?? throw new ArgumentNullException(nameof(textSearchService));
        _printService = printService ?? throw new ArgumentNullException(nameof(printService));
        _renderOptions = new RenderOptions();
        _searchViewModel = new SearchViewModel(textSearchService, NavigateToPageFromSearch);
    }

    private async Task NavigateToPageFromSearch(int pageNumber)
    {
        await NavigateToPage(pageNumber);
    }

    [RelayCommand]
    private async Task OpenDocument()
    {
        try
        {
            StatusText = "Opening document...";

            // This will be called from the View with the file path
            // For now, we'll wait for the view to call OpenDocumentWithPath
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }

    public async Task OpenDocumentWithPath(string filePath)
    {
        try
        {
            IsLoading = true;
            LoadingProgress = 0;
            StatusText = "Loading document...";

            // Reset rotation for new document
            RotationAngle = 0;
            _renderOptions.Rotation = 0;

            // Close search panel and clear results
            IsSearchPanelVisible = false;
            SearchViewModel?.Clear();

            // Clear page history for new document
            _pageHistoryBack.Clear();
            _pageHistoryForward.Clear();

            // Clear render cache and current page image for new document
            _documentService.ClearCache();
            CurrentPageImage = null;
            Pages.Clear();

            LoadingProgress = 20;
            _currentDocument = await _documentService.OpenDocumentAsync(filePath);
            _currentFilePath = filePath;

            // Calculate file size
            var fileInfo = new FileInfo(filePath);
            FileSizeText = FormatFileSize(fileInfo.Length);

            LoadingProgress = 50;
            TotalPages = _currentDocument.PageCount;
            IsDocumentLoaded = true;
            DocumentTitle = $"PDF Viewer - {_currentDocument.FileName}";

            // Set document for text search and printing
            _textSearchService.SetDocument(_documentService.CurrentInternalDocument);
            _printService.SetDocument(_documentService.CurrentInternalDocument);

            // Add to recent files
            AddToRecentFiles(filePath);

            LoadingProgress = 70;
            // Initialize thumbnails
            await InitializeThumbnailsAsync();

            LoadingProgress = 80;
            // Load bookmarks/outline
            LoadBookmarks();

            LoadingProgress = 90;

            // Initialize pages based on current view mode
            if (IsContinuousMode)
            {
                await InitializeContinuousPagesAsync();
            }
            else
            {
                await NavigateToPage(1);
            }

            LoadingProgress = 100;
            IsLoading = false;
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading document: {ex.Message}";
            IsDocumentLoaded = false;
            IsLoading = false;
        }
    }

    // ========== SAVE COMMANDS ==========

    // Event to request save file dialog from the View
    public event Func<string?, Task<string?>>? SaveFileRequested;

    [RelayCommand(CanExecute = nameof(IsDocumentLoaded))]
    private async Task Save()
    {
        if (!IsDocumentLoaded || string.IsNullOrEmpty(_currentFilePath))
            return;

        try
        {
            StatusText = "Saving document...";
            // For a viewer without editing, save just copies the file
            // When editing features are added, this will save changes
            StatusText = $"Document saved: {Path.GetFileName(_currentFilePath)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error saving: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(IsDocumentLoaded))]
    private async Task SaveAs()
    {
        if (!IsDocumentLoaded || string.IsNullOrEmpty(_currentFilePath))
            return;

        try
        {
            // Request save path from View
            var savePath = await (SaveFileRequested?.Invoke(_currentFilePath) ?? Task.FromResult<string?>(null));

            if (string.IsNullOrEmpty(savePath))
                return;

            StatusText = "Saving document...";

            // Copy the file to the new location
            File.Copy(_currentFilePath, savePath, true);

            StatusText = $"Document saved as: {Path.GetFileName(savePath)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error saving: {ex.Message}";
        }
    }

    public async Task SaveAsWithPath(string savePath)
    {
        if (!IsDocumentLoaded || string.IsNullOrEmpty(_currentFilePath))
            return;

        try
        {
            StatusText = "Saving document...";

            // Copy the file to the new location
            File.Copy(_currentFilePath, savePath, true);

            StatusText = $"Document saved as: {Path.GetFileName(savePath)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error saving: {ex.Message}";
        }
    }

    private async Task InitializeThumbnailsAsync()
    {
        Thumbnails.Clear();

        // Create placeholder thumbnails for all pages
        for (int i = 1; i <= TotalPages; i++)
        {
            Thumbnails.Add(new ThumbnailViewModel
            {
                PageNumber = i,
                IsLoading = true,
                IsSelected = i == 1
            });
        }

        // Load thumbnails in background
        _ = LoadThumbnailsAsync();
    }

    private async Task LoadThumbnailsAsync()
    {
        const int batchSize = 10;

        for (int i = 0; i < TotalPages; i += batchSize)
        {
            var tasks = new System.Collections.Generic.List<Task>();

            for (int j = i; j < Math.Min(i + batchSize, TotalPages); j++)
            {
                int pageIndex = j;
                tasks.Add(LoadThumbnailAsync(pageIndex));
            }

            await Task.WhenAll(tasks);
        }
    }

    private async Task LoadThumbnailAsync(int pageIndex)
    {
        try
        {
            var thumbnailData = await _documentService.RenderThumbnailAsync(pageIndex, 150);

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (pageIndex < Thumbnails.Count)
                {
                    using var ms = new MemoryStream(thumbnailData);
                    Thumbnails[pageIndex].ThumbnailImage = new Bitmap(ms);
                    Thumbnails[pageIndex].IsLoading = false;
                }
            });
        }
        catch
        {
            // Failed to load thumbnail, leave as placeholder
            if (pageIndex < Thumbnails.Count)
            {
                Thumbnails[pageIndex].IsLoading = false;
            }
        }
    }

    [RelayCommand]
    private async Task NavigateFromThumbnail(ThumbnailViewModel? thumbnail)
    {
        if (thumbnail != null)
        {
            await NavigateToPage(thumbnail.PageNumber);
        }
    }

    [RelayCommand]
    private async Task NavigateFromBookmark(BookmarkViewModel? bookmark)
    {
        if (bookmark != null && bookmark.PageNumber > 0)
        {
            await NavigateToPage(bookmark.PageNumber);
        }
    }

    private void LoadBookmarks()
    {
        Bookmarks.Clear();
        HasBookmarks = false;

        // Try to get bookmarks from the document
        try
        {
            var bookmarkList = _documentService.GetBookmarks();
            if (bookmarkList != null && bookmarkList.Count > 0)
            {
                foreach (var bookmark in bookmarkList)
                {
                    Bookmarks.Add(ConvertToBookmarkViewModel(bookmark));
                }
                HasBookmarks = true;
            }
        }
        catch
        {
            // Bookmarks not supported or not available
            HasBookmarks = false;
        }
    }

    private BookmarkViewModel ConvertToBookmarkViewModel(PdfViewer.Core.Models.PdfBookmark bookmark)
    {
        var vm = new BookmarkViewModel
        {
            Title = bookmark.Title,
            PageNumber = bookmark.PageNumber
        };

        foreach (var child in bookmark.Children)
        {
            vm.Children.Add(ConvertToBookmarkViewModel(child));
        }

        return vm;
    }

    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarVisible = !IsSidebarVisible;
    }

    [RelayCommand]
    private void ToggleSearchPanel()
    {
        IsSearchPanelVisible = !IsSearchPanelVisible;
    }

    [RelayCommand]
    private void CloseSearchPanel()
    {
        IsSearchPanelVisible = false;
        SearchViewModel?.Clear();
    }

    [RelayCommand]
    private async Task Print()
    {
        if (!IsDocumentLoaded) return;

        try
        {
            StatusText = "Printing...";

            var options = new PrintOptions
            {
                StartPage = 1,
                EndPage = TotalPages,
                Copies = 1,
                Scaling = PrintScaling.FitToPage
            };

            var success = await _printService.PrintAsync(options);

            StatusText = success
                ? $"Page {CurrentPageNumber} of {TotalPages} | Print job sent"
                : $"Page {CurrentPageNumber} of {TotalPages} | Print failed";
        }
        catch (Exception ex)
        {
            StatusText = $"Print error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SetSinglePageMode()
    {
        if (ViewMode == ViewMode.SinglePage) return;

        ViewMode = ViewMode.SinglePage;
        Pages.Clear();
        await RenderCurrentPage();
    }

    [RelayCommand]
    private async Task SetContinuousMode()
    {
        if (ViewMode == ViewMode.Continuous) return;

        ViewMode = ViewMode.Continuous;
        await InitializeContinuousPagesAsync();
    }

    private async Task InitializeContinuousPagesAsync()
    {
        Pages.Clear();

        // Create page view models for all pages
        for (int i = 1; i <= TotalPages; i++)
        {
            var pageVm = new PageViewModel
            {
                PageNumber = i,
                IsLoading = true,
                IsVisible = false
            };

            // Get page dimensions
            try
            {
                var (width, height) = _documentService.GetPageSize(i - 1);
                pageVm.Width = width * ZoomFactor;
                pageVm.Height = height * ZoomFactor;
            }
            catch
            {
                pageVm.Width = 612; // Default letter width
                pageVm.Height = 792; // Default letter height
            }

            Pages.Add(pageVm);
        }

        // Load visible pages (start with first few)
        await LoadVisiblePagesAsync(0, Math.Min(5, TotalPages));
    }

    private async Task LoadVisiblePagesAsync(int startIndex, int count)
    {
        var tasks = new System.Collections.Generic.List<Task>();

        for (int i = startIndex; i < Math.Min(startIndex + count, TotalPages); i++)
        {
            int pageIndex = i;
            tasks.Add(LoadPageAsync(pageIndex));
        }

        await Task.WhenAll(tasks);
    }

    private async Task LoadPageAsync(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= Pages.Count) return;

        var pageVm = Pages[pageIndex];
        if (pageVm.PageImage != null) return; // Already loaded

        try
        {
            var pageData = await _documentService.RenderPageAsync(pageIndex, _renderOptions);

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (pageIndex < Pages.Count)
                {
                    using var ms = new MemoryStream(pageData);
                    Pages[pageIndex].PageImage = new Bitmap(ms);
                    Pages[pageIndex].IsLoading = false;
                }
            });
        }
        catch
        {
            if (pageIndex < Pages.Count)
            {
                Pages[pageIndex].IsLoading = false;
            }
        }
    }

    public async Task OnContinuousScrollChanged(int firstVisiblePage, int lastVisiblePage)
    {
        if (ViewMode != ViewMode.Continuous) return;

        // Update current page number based on most visible page
        if (firstVisiblePage >= 1 && firstVisiblePage <= TotalPages)
        {
            CurrentPageNumber = firstVisiblePage;
            UpdateThumbnailSelection();
        }

        // Load visible pages plus buffer
        int buffer = 2;
        int startIndex = Math.Max(0, firstVisiblePage - buffer - 1);
        int endIndex = Math.Min(TotalPages - 1, lastVisiblePage + buffer - 1);

        await LoadVisiblePagesAsync(startIndex, endIndex - startIndex + 1);
    }

    private void UpdateThumbnailSelection()
    {
        foreach (var thumbnail in Thumbnails)
        {
            thumbnail.IsSelected = thumbnail.PageNumber == CurrentPageNumber;
        }
    }

    [RelayCommand(CanExecute = nameof(CanNavigatePrevious))]
    private async Task PreviousPage()
    {
        if (CanNavigatePrevious())
        {
            await NavigateToPage(CurrentPageNumber - 1);
        }
    }

    [RelayCommand(CanExecute = nameof(CanNavigateNext))]
    private async Task NextPage()
    {
        if (CanNavigateNext())
        {
            await NavigateToPage(CurrentPageNumber + 1);
        }
    }

    [RelayCommand]
    private async Task ZoomIn()
    {
        ZoomMode = ZoomMode.Manual;
        ZoomFactor = Math.Min(ZoomFactor + 0.25, 5.0);
        _renderOptions.ZoomFactor = ZoomFactor;
        _documentService.ClearCache();
        await RenderCurrentPage();
    }

    [RelayCommand]
    private async Task ZoomOut()
    {
        ZoomMode = ZoomMode.Manual;
        ZoomFactor = Math.Max(ZoomFactor - 0.25, 0.25);
        _renderOptions.ZoomFactor = ZoomFactor;
        _documentService.ClearCache();
        await RenderCurrentPage();
    }

    [RelayCommand]
    private async Task ResetZoom()
    {
        ZoomMode = ZoomMode.Manual;
        ZoomFactor = 1.0;
        _renderOptions.ZoomFactor = ZoomFactor;
        _documentService.ClearCache();
        await RenderCurrentPage();
    }

    [RelayCommand]
    private async Task RotateClockwise()
    {
        RotationAngle = (RotationAngle + 90) % 360;
        _renderOptions.Rotation = RotationAngle;
        await RenderCurrentPage();
    }

    [RelayCommand]
    private async Task RotateCounterClockwise()
    {
        RotationAngle = (RotationAngle - 90 + 360) % 360;
        _renderOptions.Rotation = RotationAngle;
        await RenderCurrentPage();
    }

    [RelayCommand]
    private async Task SetFitWidth()
    {
        ZoomMode = ZoomMode.FitWidth;
        await CalculateAndApplyZoom();
    }

    [RelayCommand]
    private async Task SetFitPage()
    {
        ZoomMode = ZoomMode.FitPage;
        await CalculateAndApplyZoom();
    }

    [RelayCommand]
    private async Task SetActualSize()
    {
        ZoomMode = ZoomMode.ActualSize;
        ZoomFactor = 1.0;
        _renderOptions.ZoomFactor = ZoomFactor;
        await RenderCurrentPage();
    }

    public async Task UpdateViewportSize(double width, double height)
    {
        ViewportWidth = width;
        ViewportHeight = height;

        if (IsDocumentLoaded && ZoomMode != ZoomMode.Manual)
        {
            await CalculateAndApplyZoom();
        }
    }

    private async Task CalculateAndApplyZoom()
    {
        if (!IsDocumentLoaded)
            return;

        try
        {
            var pageIndex = CurrentPageNumber - 1;
            var (pageWidth, pageHeight) = _documentService.GetPageSize(pageIndex);

            // Account for rotation - swap dimensions if rotated 90 or 270 degrees
            if (RotationAngle == 90 || RotationAngle == 270)
            {
                (pageWidth, pageHeight) = (pageHeight, pageWidth);
            }

            // Account for margins (40px on each side in the XAML)
            var availableWidth = ViewportWidth - 80;
            var availableHeight = ViewportHeight - 80;

            if (availableWidth <= 0 || availableHeight <= 0)
                return;

            ZoomFactor = ZoomMode switch
            {
                ZoomMode.FitWidth => availableWidth / pageWidth,
                ZoomMode.FitPage => Math.Min(availableWidth / pageWidth, availableHeight / pageHeight),
                ZoomMode.ActualSize => 1.0,
                _ => ZoomFactor
            };

            // Clamp zoom factor to reasonable bounds
            ZoomFactor = Math.Clamp(ZoomFactor, 0.1, 10.0);
            _renderOptions.ZoomFactor = ZoomFactor;
            await RenderCurrentPage();
        }
        catch
        {
            // If page size retrieval fails, fall back to manual mode
            ZoomMode = ZoomMode.Manual;
        }
    }

    private bool CanNavigatePrevious() => IsDocumentLoaded && CurrentPageNumber > 1;
    private bool CanNavigateNext() => IsDocumentLoaded && CurrentPageNumber < TotalPages;

    private async Task NavigateToPage(int pageNumber)
    {
        if (!IsDocumentLoaded || pageNumber < 1 || pageNumber > TotalPages)
            return;

        // Track page history
        if (CurrentPageNumber != pageNumber)
        {
            AddToPageHistory(CurrentPageNumber);
        }

        CurrentPageNumber = pageNumber;
        UpdateThumbnailSelection();
        await RenderCurrentPage();

        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    private async Task RenderCurrentPage()
    {
        if (!IsDocumentLoaded)
            return;

        var pageIndex = CurrentPageNumber - 1; // Convert from 1-based (UI) to 0-based (internal)

        try
        {
            StatusText = $"Loading page {CurrentPageNumber} of {TotalPages}...";

            // Render first page at current zoom level
            var pageData = await _documentService.RenderPageAsync(pageIndex, _renderOptions);

            using (var ms = new MemoryStream(pageData))
            {
                CurrentPageImage = new Bitmap(ms);
            }

            // Render second page for Two-Page mode
            if (IsTwoPageMode && pageIndex + 1 < TotalPages)
            {
                var secondPageData = await _documentService.RenderPageAsync(pageIndex + 1, _renderOptions);
                using (var ms = new MemoryStream(secondPageData))
                {
                    SecondPageImage = new Bitmap(ms);
                }
            }
            else
            {
                SecondPageImage = null;
            }

            StatusText = $"Page {CurrentPageNumber} of {TotalPages} | Zoom: {(int)(ZoomFactor * 100)}%";
        }
        catch (Exception ex)
        {
            StatusText = $"Error rendering page: {ex.Message}";
        }
    }

    // ========== NAVIGATION COMMANDS ==========

    [RelayCommand]
    private async Task FirstPage()
    {
        if (IsDocumentLoaded && CurrentPageNumber != 1)
        {
            await NavigateToPage(1);
        }
    }

    [RelayCommand]
    private async Task LastPage()
    {
        if (IsDocumentLoaded && CurrentPageNumber != TotalPages)
        {
            await NavigateToPage(TotalPages);
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private async Task GoBack()
    {
        if (_pageHistoryBack.Count > 0)
        {
            _isNavigatingHistory = true;
            _pageHistoryForward.Push(CurrentPageNumber);
            var page = _pageHistoryBack.Pop();
            await NavigateToPage(page);
            _isNavigatingHistory = false;
            GoBackCommand.NotifyCanExecuteChanged();
            GoForwardCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private async Task GoForward()
    {
        if (_pageHistoryForward.Count > 0)
        {
            _isNavigatingHistory = true;
            _pageHistoryBack.Push(CurrentPageNumber);
            var page = _pageHistoryForward.Pop();
            await NavigateToPage(page);
            _isNavigatingHistory = false;
            GoBackCommand.NotifyCanExecuteChanged();
            GoForwardCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanGoBack() => _pageHistoryBack.Count > 0;
    private bool CanGoForward() => _pageHistoryForward.Count > 0;

    public async Task GoToPage(int pageNumber)
    {
        if (IsDocumentLoaded && pageNumber >= 1 && pageNumber <= TotalPages)
        {
            await NavigateToPage(pageNumber);
        }
    }

    private void AddToPageHistory(int fromPage)
    {
        if (!_isNavigatingHistory && fromPage != CurrentPageNumber)
        {
            _pageHistoryBack.Push(fromPage);
            _pageHistoryForward.Clear();
            GoBackCommand.NotifyCanExecuteChanged();
            GoForwardCommand.NotifyCanExecuteChanged();
        }
    }

    // ========== VIEW MODE COMMANDS ==========

    [RelayCommand]
    private async Task SetTwoPageMode()
    {
        if (ViewMode == ViewMode.TwoPage) return;

        ViewMode = ViewMode.TwoPage;
        Pages.Clear();
        await RenderCurrentPage();
    }

    [RelayCommand]
    private void ToggleFullScreen()
    {
        IsFullScreen = !IsFullScreen;
        if (IsFullScreen)
        {
            IsMenuBarVisible = false;
            IsRibbonVisible = false;
            IsStatusBarVisible = false;
            IsSidebarVisible = false;
        }
        else
        {
            IsMenuBarVisible = true;
            IsRibbonVisible = true;
            IsStatusBarVisible = true;
            IsSidebarVisible = true;
        }
    }

    [RelayCommand]
    private void ExitFullScreen()
    {
        if (IsFullScreen)
        {
            ToggleFullScreen();
        }
    }

    // For launching presentation mode from the View
    public PdfDocumentService GetDocumentService() => _documentService;

    [RelayCommand]
    private void ToggleMenuBar()
    {
        IsMenuBarVisible = !IsMenuBarVisible;
    }

    [RelayCommand]
    private void ToggleRibbon()
    {
        IsRibbonVisible = !IsRibbonVisible;
    }

    [RelayCommand]
    private void ToggleStatusBar()
    {
        IsStatusBarVisible = !IsStatusBarVisible;
    }

    // ========== TOOL COMMANDS ==========

    [RelayCommand]
    private void SetSelectTool()
    {
        CurrentTool = ToolMode.Select;
        StatusText = $"Page {CurrentPageNumber} of {TotalPages} | Select Tool";
    }

    [RelayCommand]
    private void SetHandTool()
    {
        CurrentTool = ToolMode.Hand;
        StatusText = $"Page {CurrentPageNumber} of {TotalPages} | Hand Tool (Pan)";
    }

    [RelayCommand]
    private void SetZoomTool()
    {
        CurrentTool = ToolMode.Zoom;
        StatusText = $"Page {CurrentPageNumber} of {TotalPages} | Zoom Tool";
    }

    // ========== ZOOM COMMANDS ==========

    [RelayCommand]
    private async Task ZoomToPreset(string? percentStr)
    {
        if (int.TryParse(percentStr, out int percent))
        {
            await ZoomToPercent(percent);
        }
    }

    public async Task ZoomToPercent(int percent)
    {
        ZoomMode = ZoomMode.Manual;
        ZoomFactor = percent / 100.0;
        ZoomFactor = Math.Clamp(ZoomFactor, 0.25, 5.0);
        _renderOptions.ZoomFactor = ZoomFactor;
        _documentService.ClearCache();
        await RenderCurrentPage();
    }

    private System.Threading.CancellationTokenSource? _zoomRenderCts;

    public async Task MouseWheelZoom(bool zoomIn)
    {
        ZoomMode = ZoomMode.Manual;
        var delta = zoomIn ? 0.1 : -0.1;
        ZoomFactor = Math.Clamp(ZoomFactor + delta, 0.25, 5.0);

        // Update status immediately
        StatusText = $"Page {CurrentPageNumber} of {TotalPages} | Zoom: {(int)(ZoomFactor * 100)}%";

        // Debounce high-quality render - cancel previous pending render
        _zoomRenderCts?.Cancel();
        _zoomRenderCts = new System.Threading.CancellationTokenSource();
        var token = _zoomRenderCts.Token;

        // Wait for user to stop zooming, then render at high quality
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300, token); // Wait 300ms after last zoom action
                if (!token.IsCancellationRequested)
                {
                    _renderOptions.ZoomFactor = ZoomFactor;
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        await RenderCurrentPage();
                    });
                }
            }
            catch (TaskCanceledException) { }
        });
    }

    // Computed property for zoom percentage display
    public int ZoomPercentage => (int)(ZoomFactor * 100);

    // Display scale factor - always 1.0, PDF is re-rendered at correct DPI for each zoom level
    // This gives crisp text/graphics at any zoom (like Adobe/Foxit)
    public double DisplayScaleFactor => 1.0;

    // ========== RECENT FILES ==========

    public void AddToRecentFiles(string filePath)
    {
        // Remove if already exists
        if (RecentFiles.Contains(filePath))
        {
            RecentFiles.Remove(filePath);
        }

        // Add to beginning
        RecentFiles.Insert(0, filePath);

        // Trim to max
        while (RecentFiles.Count > MaxRecentFiles)
        {
            RecentFiles.RemoveAt(RecentFiles.Count - 1);
        }
    }

    [RelayCommand]
    private async Task OpenRecentFile(string? filePath)
    {
        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        {
            await OpenDocumentWithPath(filePath);
        }
    }

    // ========== DOCUMENT INFO ==========

    public string? CurrentFilePath => _currentFilePath;

    public (string title, string author, string subject, int pageCount, long fileSize) GetDocumentProperties()
    {
        if (!IsDocumentLoaded || _currentDocument == null)
            return ("", "", "", 0, 0);

        long fileSize = 0;
        if (!string.IsNullOrEmpty(_currentFilePath) && File.Exists(_currentFilePath))
        {
            fileSize = new FileInfo(_currentFilePath).Length;
        }

        return (
            _currentDocument.Title ?? "",
            _currentDocument.Author ?? "",
            "",
            TotalPages,
            fileSize
        );
    }

    public (double width, double height) GetFirstPageSize()
    {
        if (!IsDocumentLoaded)
            return (612.0, 792.0); // Default letter size

        try
        {
            return _documentService.GetPageSize(0);
        }
        catch
        {
            return (612.0, 792.0);
        }
    }

    // ========== AUTO SCROLL NAVIGATION ==========
    // Note: Mouse wheel page navigation is now handled in MainWindow.axaml.cs
    // This event is kept for potential future use
    public event Action<bool>? ScrollResetRequested;

    // ========== ESCAPE KEY HANDLING ==========

    public void HandleEscapeKey()
    {
        if (IsFullScreen)
        {
            ToggleFullScreen();
        }
        else if (IsSearchPanelVisible)
        {
            CloseSearchPanel();
        }
    }

    // ========== HELPER METHODS ==========

    private static string FormatFileSize(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int suffixIndex = 0;
        double size = bytes;

        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        return $"{size:F1} {suffixes[suffixIndex]}";
    }
}
