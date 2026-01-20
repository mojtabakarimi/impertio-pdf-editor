using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfViewer.Core.Interfaces;
using PdfViewer.Core.Models;
using PdfViewer.Core.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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

    // Search highlighting for current page
    [ObservableProperty]
    private int _currentPageMatchCount;

    [ObservableProperty]
    private string _currentPageMatchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<HighlightRectViewModel> _highlightRects = new();

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
        UpdateCurrentPageMatchInfo();
    }

    private void UpdateCurrentPageMatchInfo()
    {
        if (SearchViewModel == null || SearchViewModel.Results.Count == 0)
        {
            CurrentPageMatchCount = 0;
            CurrentPageMatchText = string.Empty;
            HighlightRects.Clear();
            return;
        }

        CurrentPageMatchCount = SearchViewModel.GetResultCountForPage(CurrentPageNumber);
        if (CurrentPageMatchCount > 0)
        {
            var currentResultOnPage = SearchViewModel.Results
                .Select((r, i) => new { Result = r, Index = i })
                .Where(x => x.Result.PageNumber == CurrentPageNumber)
                .ToList();

            var selectedIndex = SearchViewModel.CurrentResultIndex;
            var matchOnPage = currentResultOnPage.FindIndex(x => x.Index == selectedIndex);

            if (matchOnPage >= 0)
            {
                CurrentPageMatchText = $"Match {matchOnPage + 1} of {CurrentPageMatchCount} on this page";
            }
            else
            {
                CurrentPageMatchText = $"{CurrentPageMatchCount} match{(CurrentPageMatchCount > 1 ? "es" : "")} on this page";
            }

            // Update highlight rectangles for current page
            UpdateHighlightRects(matchOnPage);
        }
        else
        {
            CurrentPageMatchText = string.Empty;
            HighlightRects.Clear();
        }
    }

    private void UpdateHighlightRects(int currentMatchIndex)
    {
        HighlightRects.Clear();

        try
        {
            var document = _documentService.CurrentInternalDocument;
            if (document == null || SearchViewModel == null || string.IsNullOrEmpty(SearchViewModel.SearchQuery))
            {
                Console.WriteLine("[Highlight] No document or search query");
                return;
            }

            // Get text bounds from PDFium
            var pageIndex = CurrentPageNumber - 1;
            Console.WriteLine($"[Highlight] Finding bounds for '{SearchViewModel.SearchQuery}' on page {CurrentPageNumber}");
            var bounds = document.FindTextBounds(pageIndex, SearchViewModel.SearchQuery,
                SearchViewModel.MatchCase, SearchViewModel.WholeWord);

            Console.WriteLine($"[Highlight] Found {bounds.Count} bounds");

            if (bounds.Count == 0)
                return;

            // Get page size for coordinate transformation
            var (pageWidth, pageHeight) = _documentService.GetPageSize(pageIndex);
            Console.WriteLine($"[Highlight] Page size: {pageWidth} x {pageHeight}");

            // Convert PDF coordinates to screen coordinates
            // PDF coordinates: origin at bottom-left, Y increases upward
            // Screen coordinates: origin at top-left, Y increases downward
            // The rendered image is at ZoomFactor * DPI scale
            var dpiScale = 96.0 / 72.0; // Default DPI 96, PDF points at 72 DPI
            var scale = ZoomFactor * dpiScale;
            Console.WriteLine($"[Highlight] Scale: {scale} (Zoom: {ZoomFactor}, DPI Scale: {dpiScale})");

            int matchIndex = 0;
            foreach (var rect in bounds)
            {
                // Transform PDF coordinates to screen coordinates
                var screenLeft = rect.Left * scale;
                var screenTop = (pageHeight - rect.Top) * scale; // Flip Y axis
                var screenWidth = rect.Width * scale;
                var screenHeight = rect.Height * scale;

                Console.WriteLine($"[Highlight] Match {matchIndex}: PDF({rect.Left:F1},{rect.Top:F1},{rect.Width:F1},{rect.Height:F1}) -> Screen({screenLeft:F1},{screenTop:F1},{screenWidth:F1},{screenHeight:F1})");

                var highlightVm = new HighlightRectViewModel
                {
                    Left = screenLeft,
                    Top = screenTop,
                    Width = screenWidth,
                    Height = screenHeight,
                    IsCurrentMatch = matchIndex == currentMatchIndex
                };

                HighlightRects.Add(highlightVm);
                matchIndex++;
            }

            Console.WriteLine($"[Highlight] Added {HighlightRects.Count} highlight rectangles");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating highlight rects: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
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

            // Reset zoom factors for new document
            _renderedZoomFactor = ZoomFactor;
            VisualZoomFactor = ZoomFactor;
            OnPropertyChanged(nameof(DisplayScaleFactor));

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

        // Remember current page before switching
        var currentPage = CurrentPageNumber;

        ViewMode = ViewMode.Continuous;
        await InitializeContinuousPagesAsync();

        // Scroll to the page we were on
        if (currentPage > 0 && currentPage <= TotalPages)
        {
            ScrollToPageRequested?.Invoke(currentPage);
        }
    }

    private async Task InitializeContinuousPagesAsync()
    {
        Pages.Clear();

        // DPI scale factor - must match the render DPI
        var dpiScale = _renderOptions.Dpi / 72.0;

        // Create page view models for all pages
        for (int i = 1; i <= TotalPages; i++)
        {
            var pageVm = new PageViewModel
            {
                PageNumber = i,
                IsLoading = true,
                IsVisible = false
            };

            // Get page dimensions - scale to match rendered image size
            try
            {
                var (width, height) = _documentService.GetPageSize(i - 1);
                // Container size must match rendered image size exactly for crisp display
                pageVm.Width = width * ZoomFactor * dpiScale;
                pageVm.Height = height * ZoomFactor * dpiScale;
            }
            catch
            {
                pageVm.Width = 612 * dpiScale; // Default letter width
                pageVm.Height = 792 * dpiScale; // Default letter height
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

        Console.WriteLine($"[LoadPage] Page {pageIndex}: PageImage is {(pageVm.PageImage == null ? "NULL" : "NOT NULL")}, IsLoading={pageVm.IsLoading}");

        if (pageVm.PageImage != null) return; // Already loaded

        try
        {
            Console.WriteLine($"[LoadPage] Rendering page {pageIndex} with ZoomFactor={_renderOptions.ZoomFactor}");
            var pageData = await _documentService.RenderPageAsync(pageIndex, _renderOptions);
            Console.WriteLine($"[LoadPage] Page {pageIndex} rendered, data size: {pageData.Length} bytes");

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (pageIndex < Pages.Count)
                {
                    using var ms = new MemoryStream(pageData);
                    var bitmap = new Bitmap(ms);
                    Console.WriteLine($"[LoadPage] Page {pageIndex} bitmap created: {bitmap.PixelSize.Width}x{bitmap.PixelSize.Height}");
                    Pages[pageIndex].PageImage = bitmap;
                    Pages[pageIndex].IsLoading = false;
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LoadPage] Error loading page {pageIndex}: {ex.Message}");
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

    // Event to request thumbnail panel scroll
    public event Action<int>? ScrollThumbnailToPageRequested;

    private void UpdateThumbnailSelection()
    {
        foreach (var thumbnail in Thumbnails)
        {
            thumbnail.IsSelected = thumbnail.PageNumber == CurrentPageNumber;
        }

        // Request thumbnail panel to scroll to show current page
        ScrollThumbnailToPageRequested?.Invoke(CurrentPageNumber);
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
        Console.WriteLine($"[ZoomIn] Before: ZoomFactor={ZoomFactor}");
        ZoomMode = ZoomMode.Manual;
        ZoomFactor = Math.Min(ZoomFactor + 0.25, 5.0);
        _renderOptions.ZoomFactor = ZoomFactor;
        Console.WriteLine($"[ZoomIn] After: ZoomFactor={ZoomFactor}, RenderOptions.ZoomFactor={_renderOptions.ZoomFactor}");
        _documentService.ClearCache();
        Console.WriteLine("[ZoomIn] Cache cleared, rendering...");
        await RenderCurrentPage();
        Console.WriteLine("[ZoomIn] Render complete");
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
        _documentService.ClearCache();
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
            _documentService.ClearCache();
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

    // Event for requesting scroll to a specific page in continuous mode
    public event Action<int>? ScrollToPageRequested;

    // Event for zoom operations - View should preserve scroll position
    public event Func<(int pageNumber, double relativePosition)>? GetCurrentScrollPosition;
    public event Action<int, double>? RestoreScrollPosition;

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

        // In continuous mode, request scroll to the page
        if (IsContinuousMode)
        {
            ScrollToPageRequested?.Invoke(pageNumber);
        }

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

            // Handle Continuous mode - update all page sizes and re-render visible pages
            if (IsContinuousMode)
            {
                await UpdateContinuousPagesForZoom();
                StatusText = $"Page {CurrentPageNumber} of {TotalPages} | Zoom: {(int)(ZoomFactor * 100)}%";
                return;
            }

            // Render first page at current zoom level (Single Page and Two-Page modes)
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

            // Update search match info for current page
            UpdateCurrentPageMatchInfo();
        }
        catch (Exception ex)
        {
            StatusText = $"Error rendering page: {ex.Message}";
        }
    }

    private async Task UpdateContinuousPagesForZoom()
    {
        Console.WriteLine($"[Zoom] ========== ZOOM UPDATE START ==========");
        Console.WriteLine($"[Zoom] ZoomFactor: {ZoomFactor}, RenderOptions.ZoomFactor: {_renderOptions.ZoomFactor}");
        Console.WriteLine($"[Zoom] RenderOptions.Dpi: {_renderOptions.Dpi}");

        // Get current scroll position before changing page sizes
        var scrollInfo = GetCurrentScrollPosition?.Invoke() ?? (CurrentPageNumber, 0.0);
        Console.WriteLine($"[Zoom] Preserving scroll position: Page {scrollInfo.pageNumber}, RelativePos {scrollInfo.relativePosition:F2}");

        // DPI scale factor - must match the render DPI
        var dpiScale = _renderOptions.Dpi / 72.0;
        Console.WriteLine($"[Zoom] DPI Scale: {dpiScale}");

        // Update dimensions for all pages
        for (int i = 0; i < Pages.Count; i++)
        {
            var pageVm = Pages[i];
            try
            {
                var (width, height) = _documentService.GetPageSize(i);
                var newWidth = width * ZoomFactor * dpiScale;
                var newHeight = height * ZoomFactor * dpiScale;

                Console.WriteLine($"[Zoom] Page {i}: Old size ({pageVm.Width:F0}x{pageVm.Height:F0}) -> New size ({newWidth:F0}x{newHeight:F0})");

                // Container size must match rendered image size exactly for crisp display
                pageVm.Width = newWidth;
                pageVm.Height = newHeight;

                // Clear the cached image so it gets re-rendered
                Console.WriteLine($"[Zoom] Page {i}: Clearing PageImage (was {(pageVm.PageImage == null ? "NULL" : "NOT NULL")})");
                pageVm.PageImage = null;
                pageVm.IsLoading = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Zoom] Page {i}: Error - {ex.Message}");
                pageVm.Width = 612 * ZoomFactor * dpiScale;
                pageVm.Height = 792 * ZoomFactor * dpiScale;
            }
        }

        // Restore scroll position to the same page after sizes changed
        RestoreScrollPosition?.Invoke(scrollInfo.pageNumber, scrollInfo.relativePosition);

        // Re-render visible pages (around current page)
        int startIndex = Math.Max(0, scrollInfo.pageNumber - 3);
        int endIndex = Math.Min(TotalPages - 1, scrollInfo.pageNumber + 3);

        Console.WriteLine($"[Zoom] Re-rendering pages {startIndex} to {endIndex}");
        await LoadVisiblePagesAsync(startIndex, endIndex - startIndex + 1);
        Console.WriteLine($"[Zoom] ========== ZOOM UPDATE END ==========");
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
        var oldZoom = ZoomFactor;
        var newZoom = Math.Clamp(ZoomFactor + delta, 0.25, 5.0);

        ZoomFactor = newZoom;

        // Update status immediately
        StatusText = $"Page {CurrentPageNumber} of {TotalPages} | Zoom: {(int)(ZoomFactor * 100)}%";

        // For continuous mode: resize page containers immediately (images will stretch)
        if (IsContinuousMode && Pages.Count > 0)
        {
            var dpiScale = _renderOptions.Dpi / 72.0;
            var zoomRatio = newZoom / oldZoom;

            for (int i = 0; i < Pages.Count; i++)
            {
                // Scale container sizes by zoom ratio (keeps existing image, just stretches it)
                Pages[i].Width *= zoomRatio;
                Pages[i].Height *= zoomRatio;
            }
        }

        // Debounce high-quality render - cancel previous pending render
        _zoomRenderCts?.Cancel();
        _zoomRenderCts = new System.Threading.CancellationTokenSource();
        var token = _zoomRenderCts.Token;

        // Wait for user to stop zooming, then render at high quality
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(400, token); // Wait 400ms after last zoom action
                if (!token.IsCancellationRequested)
                {
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        _renderOptions.ZoomFactor = ZoomFactor;
                        _documentService.ClearCache();
                        await RenderCurrentPageHighQuality();
                    });
                }
            }
            catch (TaskCanceledException) { }
        });
    }

    // Event to save/restore scroll position during high-quality render
    public event Func<(double x, double y)>? SaveScrollPosition;
    public event Action<double, double>? RestoreScrollPositionAfterRender;

    // High-quality render after zoom settles - updates _renderedZoomFactor when done
    private async Task RenderCurrentPageHighQuality()
    {
        if (!IsDocumentLoaded) return;

        try
        {
            // Save current scroll position (scaled)
            var savedPos = SaveScrollPosition?.Invoke() ?? (0, 0);
            var oldScale = DisplayScaleFactor;

            if (IsContinuousMode)
            {
                await UpdateContinuousPagesForZoomHighQuality();
            }
            else
            {
                await RenderCurrentPage();
            }

            // Update rendered zoom factor - this resets DisplayScaleFactor to 1.0
            _renderedZoomFactor = ZoomFactor;
            VisualZoomFactor = ZoomFactor;
            OnPropertyChanged(nameof(DisplayScaleFactor));

            // Restore scroll position (adjust for scale change: oldScale -> 1.0)
            // Position was in scaled coords, now content is at 1:1, so divide by oldScale
            if (oldScale > 0)
            {
                RestoreScrollPositionAfterRender?.Invoke(savedPos.x / oldScale, savedPos.y / oldScale);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Zoom] High quality render error: {ex.Message}");
        }
    }

    // High-quality render for continuous mode - doesn't change page sizes, just re-renders images
    private async Task UpdateContinuousPagesForZoomHighQuality()
    {
        Console.WriteLine($"[ZoomHQ] Rendering at ZoomFactor={ZoomFactor}");

        // Only clear and reload visible pages to minimize disruption
        int startIndex = Math.Max(0, CurrentPageNumber - 3);
        int endIndex = Math.Min(TotalPages - 1, CurrentPageNumber + 3);

        // Clear only visible pages
        for (int i = startIndex; i <= endIndex; i++)
        {
            if (i < Pages.Count)
            {
                Pages[i].PageImage = null;
                Pages[i].IsLoading = true;
            }
        }

        // Re-render visible pages
        await LoadVisiblePagesAsync(startIndex, endIndex - startIndex + 1);

        Console.WriteLine($"[ZoomHQ] Render complete");
    }

    // Computed property for zoom percentage display
    public int ZoomPercentage => (int)(ZoomFactor * 100);

    // Visual scale factor for instant zoom feedback (transform-based)
    // This changes immediately during zoom for smooth visual feedback
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayScaleFactor))]
    private double _visualZoomFactor = 1.0;

    // The zoom level at which pages were last rendered
    private double _renderedZoomFactor = 1.0;

    // Display scale factor - ratio between visual zoom and rendered zoom
    // Used by ScaleTransform for instant zoom feedback
    public double DisplayScaleFactor => _renderedZoomFactor > 0 ? VisualZoomFactor / _renderedZoomFactor : 1.0;

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
