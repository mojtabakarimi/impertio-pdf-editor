using PdfViewer.Core.Interfaces;
using PdfViewer.Core.Models;
using System.Collections.Generic;

namespace PdfViewer.Core.Services;

public class PdfDocumentService
{
    private readonly IPdfRenderService _renderService;
    private IPdfDocument? _currentDocument;
    private PdfDocument? _currentMetadata;

    public PdfDocument? CurrentDocument => _currentMetadata;
    public IPdfDocument? CurrentInternalDocument => _currentDocument;
    public bool IsDocumentLoaded => _currentDocument != null;

    public PdfDocumentService(IPdfRenderService renderService)
    {
        _renderService = renderService ?? throw new ArgumentNullException(nameof(renderService));
    }

    public async Task<PdfDocument> OpenDocumentAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be empty", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("PDF file not found", filePath);

        CloseDocument();

        _currentDocument = await _renderService.LoadDocumentAsync(filePath, cancellationToken);
        _currentMetadata = new PdfDocument(filePath)
        {
            PageCount = _currentDocument.PageCount
        };

        // OPTIMIZATION: Skip loading all page metadata upfront - load on-demand instead
        // This makes opening PDFs near-instant, especially for large documents
        // Page metadata will be loaded lazily when needed

        return _currentMetadata;
    }

    public async Task<byte[]> RenderPageAsync(int pageNumber, RenderOptions options, CancellationToken cancellationToken = default)
    {
        if (_currentDocument == null)
            throw new InvalidOperationException("No document is loaded");

        if (pageNumber < 0 || pageNumber >= _currentDocument.PageCount)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number is out of range");

        return await _renderService.RenderPageAsync(_currentDocument, pageNumber, options, cancellationToken);
    }

    public async Task<byte[]> RenderThumbnailAsync(int pageNumber, int thumbnailWidth, CancellationToken cancellationToken = default)
    {
        if (_currentDocument == null)
            throw new InvalidOperationException("No document is loaded");

        if (pageNumber < 0 || pageNumber >= _currentDocument.PageCount)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number is out of range");

        return await _renderService.RenderThumbnailAsync(_currentDocument, pageNumber, thumbnailWidth, cancellationToken);
    }

    public (double width, double height) GetPageSize(int pageNumber)
    {
        if (_currentDocument == null)
            throw new InvalidOperationException("No document is loaded");

        if (pageNumber < 0 || pageNumber >= _currentDocument.PageCount)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number is out of range");

        return _currentDocument.GetPageSize(pageNumber);
    }

    public void CloseDocument()
    {
        _currentDocument?.Dispose();
        _currentDocument = null;
        _currentMetadata = null;
    }

    public void ClearCache()
    {
        _renderService.ClearCache();
    }

    public List<PdfBookmark> GetBookmarks()
    {
        if (_currentDocument == null)
            return new List<PdfBookmark>();

        return _renderService.GetBookmarks(_currentDocument);
    }
}
