using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PdfViewer.Core.Models;
using PdfViewer.Desktop.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PdfViewer.Desktop.Views;

public partial class MainWindow : Window
{
    private ScrollViewer? _documentScrollViewer;
    private ScrollViewer? _thumbnailScrollViewer;
    private MenuItem? _recentFilesMenu;
    private TextBox? _pageNumberTextBox;
    private bool _isPanning = false;
    private Point _panStartPoint;
    private Vector _panStartOffset;
    private int _lastScrolledToPage = 0;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _documentScrollViewer = this.FindControl<ScrollViewer>("DocumentScrollViewer");
        if (_documentScrollViewer != null)
        {
            _documentScrollViewer.SizeChanged += OnDocumentViewerSizeChanged;
            // Use tunneling (Preview) to capture wheel events before ScrollViewer handles them
            _documentScrollViewer.AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            _documentScrollViewer.PointerPressed += OnDocumentPointerPressed;
            _documentScrollViewer.PointerMoved += OnDocumentPointerMoved;
            _documentScrollViewer.PointerReleased += OnDocumentPointerReleased;
            _documentScrollViewer.PointerEntered += OnDocumentPointerEntered;
            _documentScrollViewer.PointerExited += OnDocumentPointerExited;
            _documentScrollViewer.ScrollChanged += OnDocumentScrollChanged;
            // Initialize viewport size
            UpdateViewportSize();
        }

        // Setup Recent Files menu
        _recentFilesMenu = this.FindControl<MenuItem>("RecentFilesMenu");
        if (_recentFilesMenu != null)
        {
            _recentFilesMenu.SubmenuOpened += OnRecentFilesMenuOpened;
        }

        // Setup Page Number TextBox for Enter key handling
        _pageNumberTextBox = this.FindControl<TextBox>("PageNumberTextBox");
        if (_pageNumberTextBox != null)
        {
            _pageNumberTextBox.KeyDown += OnPageNumberKeyDown;
        }

        // Setup Thumbnail ScrollViewer for auto-scrolling
        _thumbnailScrollViewer = this.FindControl<ScrollViewer>("ThumbnailScrollViewer");
    }

    private void OnRecentFilesMenuOpened(object? sender, RoutedEventArgs e)
    {
        if (_recentFilesMenu == null || DataContext is not MainWindowViewModel viewModel)
            return;

        _recentFilesMenu.Items.Clear();

        if (viewModel.RecentFiles.Count == 0)
        {
            var emptyItem = new MenuItem { Header = "(No recent files)", IsEnabled = false };
            _recentFilesMenu.Items.Add(emptyItem);
            return;
        }

        foreach (var filePath in viewModel.RecentFiles)
        {
            var menuItem = new MenuItem
            {
                Header = System.IO.Path.GetFileName(filePath),
                Tag = filePath
            };
            menuItem.Click += OnRecentFileClick;
            _recentFilesMenu.Items.Add(menuItem);
        }
    }

    private async void OnRecentFileClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string filePath)
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                await viewModel.OpenDocumentWithPath(filePath);
            }
        }
    }

    private async void OnPageNumberKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (DataContext is MainWindowViewModel viewModel && _pageNumberTextBox != null)
            {
                if (int.TryParse(_pageNumberTextBox.Text, out int pageNum))
                {
                    await viewModel.GoToPage(pageNum);
                    // Move focus away from TextBox
                    _documentScrollViewer?.Focus();
                }
            }
            e.Handled = true;
        }
    }

    private void OnDocumentViewerSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateViewportSize();
    }

    private async void OnDocumentScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel) return;
        if (_documentScrollViewer == null) return;
        if (!viewModel.IsContinuousMode) return;

        // Calculate which pages are visible based on scroll position
        var scrollOffset = _documentScrollViewer.Offset.Y;
        var viewportHeight = _documentScrollViewer.Viewport.Height;

        // Estimate page height (use average or first page height)
        double pageHeight = 800; // Default estimate
        if (viewModel.Pages.Count > 0)
        {
            pageHeight = viewModel.Pages[0].Height + 20; // Add gap between pages
        }

        // Calculate first and last visible pages (1-based)
        int firstVisiblePage = Math.Max(1, (int)(scrollOffset / pageHeight) + 1);
        int lastVisiblePage = Math.Min(viewModel.TotalPages, (int)((scrollOffset + viewportHeight) / pageHeight) + 2);

        await viewModel.OnContinuousScrollChanged(firstVisiblePage, lastVisiblePage);

        // Scroll thumbnail panel to show current page (avoid excessive scrolling)
        if (_thumbnailScrollViewer != null && firstVisiblePage != _lastScrolledToPage)
        {
            _lastScrolledToPage = firstVisiblePage;
            ScrollThumbnailToPage(firstVisiblePage);
        }
    }

    private void ScrollThumbnailToPage(int pageNumber)
    {
        if (_thumbnailScrollViewer == null) return;
        if (pageNumber < 1) return;

        // Each thumbnail is approximately 180 pixels tall (150 image + margin + padding + label)
        const double thumbnailHeight = 190;
        var targetOffset = (pageNumber - 1) * thumbnailHeight;

        // Center the thumbnail in the viewport if possible
        var viewportHeight = _thumbnailScrollViewer.Viewport.Height;
        var centeredOffset = Math.Max(0, targetOffset - (viewportHeight / 2) + (thumbnailHeight / 2));

        _thumbnailScrollViewer.Offset = new Vector(0, centeredOffset);
    }

    private async void UpdateViewportSize()
    {
        if (_documentScrollViewer != null && DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.UpdateViewportSize(_documentScrollViewer.Bounds.Width, _documentScrollViewer.Bounds.Height);
        }
    }

    // ========== KEYBOARD NAVIGATION ==========

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel) return;

        // Handle Escape key
        if (e.Key == Key.Escape)
        {
            viewModel.HandleEscapeKey();
            e.Handled = true;
            return;
        }

        // Handle navigation keys (only when not typing in a text box)
        if (e.Source is TextBox) return;

        switch (e.Key)
        {
            case Key.Left:
            case Key.PageUp:
                if (viewModel.IsDocumentLoaded && viewModel.CurrentPageNumber > 1)
                {
                    await viewModel.GoToPage(viewModel.CurrentPageNumber - 1);
                    e.Handled = true;
                }
                break;

            case Key.Right:
            case Key.PageDown:
            case Key.Space:
                if (viewModel.IsDocumentLoaded && viewModel.CurrentPageNumber < viewModel.TotalPages)
                {
                    await viewModel.GoToPage(viewModel.CurrentPageNumber + 1);
                    e.Handled = true;
                }
                break;

            case Key.Home:
                if (viewModel.IsDocumentLoaded)
                {
                    await viewModel.GoToPage(1);
                    e.Handled = true;
                }
                break;

            case Key.End:
                if (viewModel.IsDocumentLoaded)
                {
                    await viewModel.GoToPage(viewModel.TotalPages);
                    e.Handled = true;
                }
                break;

            case Key.F11:
                viewModel.ToggleFullScreenCommand.Execute(null);
                UpdateWindowState(viewModel.IsFullScreen);
                e.Handled = true;
                break;

            case Key.G:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                {
                    await ShowGoToPageDialog(viewModel);
                    e.Handled = true;
                }
                break;

            case Key.Back:
            case Key.BrowserBack:
                if (viewModel.GoBackCommand.CanExecute(null))
                {
                    await viewModel.GoBackCommand.ExecuteAsync(null);
                    e.Handled = true;
                }
                break;

            case Key.BrowserForward:
                if (viewModel.GoForwardCommand.CanExecute(null))
                {
                    await viewModel.GoForwardCommand.ExecuteAsync(null);
                    e.Handled = true;
                }
                break;
        }
    }

    private void UpdateWindowState(bool isFullScreen)
    {
        if (isFullScreen)
        {
            WindowState = WindowState.FullScreen;
        }
        else
        {
            WindowState = WindowState.Normal;
        }
    }

    private async Task ShowGoToPageDialog(MainWindowViewModel viewModel)
    {
        if (!viewModel.IsDocumentLoaded) return;

        // Simple input dialog for page number
        var dialog = new Window
        {
            Title = "Go To Page",
            Width = 300,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var textBox = new TextBox
        {
            Watermark = $"Enter page number (1-{viewModel.TotalPages})",
            Margin = new Thickness(20, 20, 20, 10)
        };

        var okButton = new Button
        {
            Content = "Go",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 20),
            Padding = new Thickness(30, 8)
        };

        okButton.Click += async (s, e) =>
        {
            if (int.TryParse(textBox.Text, out int pageNum))
            {
                await viewModel.GoToPage(pageNum);
            }
            dialog.Close();
        };

        var panel = new StackPanel();
        panel.Children.Add(textBox);
        panel.Children.Add(okButton);
        dialog.Content = panel;

        await dialog.ShowDialog(this);
    }

    // ========== MOUSE WHEEL ZOOM & PAGE NAVIGATION ==========

    private DateTime _lastWheelNavigate = DateTime.MinValue;
    private const int WheelNavigateCooldownMs = 400;

    private async void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel) return;
        if (_documentScrollViewer == null) return;

        // Ctrl+Scroll = Zoom
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            var zoomIn = e.Delta.Y > 0;
            await viewModel.MouseWheelZoom(zoomIn);
            e.Handled = true;
            return;
        }

        // In Single Page mode: navigate pages when at scroll boundaries
        if (viewModel.ViewMode == PdfViewer.Core.Models.ViewMode.SinglePage)
        {
            var atTop = _documentScrollViewer.Offset.Y <= 1;
            var atBottom = _documentScrollViewer.Offset.Y >=
                Math.Max(0, _documentScrollViewer.Extent.Height - _documentScrollViewer.Viewport.Height - 1);

            var scrollingUp = e.Delta.Y > 0;
            var scrollingDown = e.Delta.Y < 0;

            // Check cooldown
            if ((DateTime.Now - _lastWheelNavigate).TotalMilliseconds < WheelNavigateCooldownMs)
                return;

            // At top and scrolling up = go to previous page
            if (atTop && scrollingUp && viewModel.CurrentPageNumber > 1)
            {
                _lastWheelNavigate = DateTime.Now;
                await viewModel.GoToPage(viewModel.CurrentPageNumber - 1);
                // Scroll to bottom of previous page
                await Task.Delay(100);
                var maxScroll = Math.Max(0, _documentScrollViewer.Extent.Height - _documentScrollViewer.Viewport.Height);
                _documentScrollViewer.Offset = new Vector(0, maxScroll);
                e.Handled = true;
                return;
            }

            // At bottom and scrolling down = go to next page
            if (atBottom && scrollingDown && viewModel.CurrentPageNumber < viewModel.TotalPages)
            {
                _lastWheelNavigate = DateTime.Now;
                await viewModel.GoToPage(viewModel.CurrentPageNumber + 1);
                // Scroll to top of next page
                await Task.Delay(100);
                _documentScrollViewer.Offset = new Vector(0, 0);
                e.Handled = true;
                return;
            }
        }
    }

    // ========== HAND TOOL (PAN MODE) ==========

    private void OnDocumentPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel) return;
        if (_documentScrollViewer == null) return;

        if (viewModel.CurrentTool == ToolMode.Hand)
        {
            _isPanning = true;
            _panStartPoint = e.GetPosition(_documentScrollViewer);
            _panStartOffset = _documentScrollViewer.Offset;
            // Closed/grabbing hand cursor when dragging
            _documentScrollViewer.Cursor = new Cursor(StandardCursorType.SizeAll);
            e.Handled = true;
        }
    }

    private void OnDocumentPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_documentScrollViewer == null) return;

        // Update cursor based on tool mode when not panning
        if (!_isPanning && DataContext is MainWindowViewModel viewModel)
        {
            UpdateDocumentCursor(viewModel.CurrentTool);
        }

        if (!_isPanning) return;

        var currentPoint = e.GetPosition(_documentScrollViewer);
        var delta = _panStartPoint - currentPoint;
        _documentScrollViewer.Offset = new Vector(
            _panStartOffset.X + delta.X,
            _panStartOffset.Y + delta.Y
        );
        e.Handled = true;
    }

    private void OnDocumentPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            if (_documentScrollViewer != null && DataContext is MainWindowViewModel viewModel)
            {
                // Return to open hand cursor
                UpdateDocumentCursor(viewModel.CurrentTool);
            }
            e.Handled = true;
        }
    }

    private void OnDocumentPointerEntered(object? sender, PointerEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            UpdateDocumentCursor(viewModel.CurrentTool);
        }
    }

    private void OnDocumentPointerExited(object? sender, PointerEventArgs e)
    {
        if (_documentScrollViewer != null && !_isPanning)
        {
            _documentScrollViewer.Cursor = Cursor.Default;
        }
    }

    private void UpdateDocumentCursor(ToolMode tool)
    {
        if (_documentScrollViewer == null) return;

        _documentScrollViewer.Cursor = tool switch
        {
            ToolMode.Hand => new Cursor(StandardCursorType.Hand),
            ToolMode.Select => new Cursor(StandardCursorType.Ibeam),
            _ => Cursor.Default
        };
    }

    // ========== DRAG & DROP ==========

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.None;

        var data = e.Data;
        if (data.Contains(DataFormats.Files))
        {
            var files = data.GetFiles();
            if (files != null && files.Any(f => f.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)))
            {
                e.DragEffects = DragDropEffects.Copy;
            }
        }
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel) return;

        var data = e.Data;
        if (data.Contains(DataFormats.Files))
        {
            var files = data.GetFiles();
            var pdfFile = files?.FirstOrDefault(f => f.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));

            if (pdfFile != null)
            {
                var path = pdfFile.Path.LocalPath;
                if (!string.IsNullOrEmpty(path))
                {
                    await viewModel.OpenDocumentWithPath(path);
                }
            }
        }
    }

    // ========== FILE OPERATIONS ==========

    private async void OpenDocument_Click(object? sender, RoutedEventArgs e)
    {
        await OpenPdfFile();
    }

    private async void SaveAs_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || !viewModel.IsDocumentLoaded)
            return;

        var savePath = await ShowSaveFileDialog(viewModel.CurrentFilePath);
        if (!string.IsNullOrEmpty(savePath))
        {
            await viewModel.SaveAsWithPath(savePath);
        }
    }

    private async Task<string?> ShowSaveFileDialog(string? defaultPath)
    {
        var storageProvider = StorageProvider;

        var defaultFileName = !string.IsNullOrEmpty(defaultPath)
            ? Path.GetFileName(defaultPath)
            : "document.pdf";

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save PDF As",
            SuggestedFileName = defaultFileName,
            DefaultExtension = "pdf",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("PDF Files")
                {
                    Patterns = new[] { "*.pdf" }
                }
            }
        });

        return file?.Path.LocalPath;
    }

    private async Task OpenPdfFile()
    {
        var file = await ShowOpenFileDialog();
        if (file != null && DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.OpenDocumentWithPath(file);
        }
    }

    private async Task<string?> ShowOpenFileDialog()
    {
        var storageProvider = StorageProvider;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open PDF File",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("PDF Files")
                {
                    Patterns = new[] { "*.pdf" }
                },
                FilePickerFileTypes.All
            }
        });

        return files?.FirstOrDefault()?.Path.LocalPath;
    }

    private async void ShowProperties_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || !viewModel.IsDocumentLoaded)
            return;

        var props = viewModel.GetDocumentProperties();
        var (pageWidth, pageHeight) = viewModel.CurrentFilePath != null
            ? GetFirstPageSize(viewModel)
            : (612.0, 792.0); // Default letter size

        var dialog = new DocumentPropertiesDialog();
        dialog.SetProperties(
            viewModel.CurrentFilePath ?? "",
            props.title,
            props.author,
            props.subject,
            props.pageCount,
            pageWidth,
            pageHeight,
            null // Creation date not available from PdfiumViewer
        );

        await dialog.ShowDialog(this);
    }

    private (double width, double height) GetFirstPageSize(MainWindowViewModel viewModel)
    {
        return viewModel.GetFirstPageSize();
    }

    private void StartPresentation_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || !viewModel.IsDocumentLoaded)
            return;

        var presentationWindow = new PresentationWindow(
            viewModel.GetDocumentService(),
            viewModel.TotalPages,
            viewModel.CurrentPageNumber
        );

        presentationWindow.Show();
    }

    private void Exit_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
