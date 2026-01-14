using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PdfViewer.Core.Services;
using PdfViewer.Core.Models;
using System;
using System.IO;
using System.Threading.Tasks;

namespace PdfViewer.Desktop.Views;

public partial class PresentationWindow : Window
{
    private readonly PdfDocumentService _documentService;
    private readonly int _totalPages;
    private int _currentPage = 1;
    private readonly RenderOptions _renderOptions;
    private Image? _pageImage;
    private TextBlock? _pageCounter;
    private Border? _instructionsPanel;
    private DispatcherTimer? _hideInstructionsTimer;

    public PresentationWindow(PdfDocumentService documentService, int totalPages, int startPage = 1)
    {
        InitializeComponent();

        _documentService = documentService;
        _totalPages = totalPages;
        _currentPage = Math.Clamp(startPage, 1, totalPages);
        _renderOptions = new RenderOptions { Dpi = 150, ZoomFactor = 1.0 };

        _pageImage = this.FindControl<Image>("PageImage");
        _pageCounter = this.FindControl<TextBlock>("PageCounter");
        _instructionsPanel = this.FindControl<Border>("InstructionsPanel");

        KeyDown += OnKeyDown;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await LoadCurrentPage();

        // Hide instructions after 5 seconds
        _hideInstructionsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _hideInstructionsTimer.Tick += (s, e) =>
        {
            if (_instructionsPanel != null)
            {
                _instructionsPanel.IsVisible = false;
            }
            _hideInstructionsTimer?.Stop();
        };
        _hideInstructionsTimer.Start();
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Close();
                e.Handled = true;
                break;

            case Key.Right:
            case Key.Down:
            case Key.Space:
            case Key.PageDown:
                await NextPage();
                e.Handled = true;
                break;

            case Key.Left:
            case Key.Up:
            case Key.PageUp:
                await PreviousPage();
                e.Handled = true;
                break;

            case Key.Home:
                await GoToPage(1);
                e.Handled = true;
                break;

            case Key.End:
                await GoToPage(_totalPages);
                e.Handled = true;
                break;
        }
    }

    private async Task NextPage()
    {
        if (_currentPage < _totalPages)
        {
            _currentPage++;
            await LoadCurrentPage();
        }
    }

    private async Task PreviousPage()
    {
        if (_currentPage > 1)
        {
            _currentPage--;
            await LoadCurrentPage();
        }
    }

    private async Task GoToPage(int page)
    {
        if (page >= 1 && page <= _totalPages && page != _currentPage)
        {
            _currentPage = page;
            await LoadCurrentPage();
        }
    }

    private async Task LoadCurrentPage()
    {
        try
        {
            // Update page counter
            if (_pageCounter != null)
            {
                _pageCounter.Text = $"Page {_currentPage} of {_totalPages}";
            }

            // Render page at high quality for presentation
            var pageData = await _documentService.RenderPageAsync(_currentPage - 1, _renderOptions);

            if (_pageImage != null)
            {
                using var ms = new MemoryStream(pageData);
                _pageImage.Source = new Bitmap(ms);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading presentation page: {ex.Message}");
        }
    }
}
