using PdfViewer.Core.Interfaces;
using PdfViewer.Core.Models;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

namespace PdfViewer.Rendering;

public class PdfiumRenderService : IPdfRenderService
{
    private readonly PageCache _pageCache;

    public PdfiumRenderService()
    {
        _pageCache = new PageCache(50);
    }

    public async Task<Core.Interfaces.IPdfDocument> LoadDocumentAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var pdfDocument = PdfiumViewer.PdfDocument.Load(filePath);
            return (Core.Interfaces.IPdfDocument)new PdfiumDocument(pdfDocument, filePath);
        }, cancellationToken);
    }

    public async Task<byte[]> RenderPageAsync(Core.Interfaces.IPdfDocument document, int pageNumber, RenderOptions options, CancellationToken cancellationToken = default)
    {
        // Cache key includes ZoomFactor since we re-render at each zoom level for crisp quality
        var cacheKey = $"{document.FilePath}:{pageNumber}:{options.Dpi}:{options.ZoomFactor:F2}:{options.Rotation}";

        if (_pageCache.TryGet(cacheKey, out var cachedData))
        {
            return cachedData;
        }

        var data = await Task.Run(() =>
        {
            if (document is not PdfiumDocument pdfiumDoc)
                throw new ArgumentException("Invalid document type", nameof(document));

            var pdfDoc = pdfiumDoc.InternalDocument;
            var pageSize = pdfDoc.PageSizes[pageNumber];
            // Render at DPI * ZoomFactor for crisp quality at any zoom level (like Adobe/Foxit)
            var effectiveDpi = options.Dpi * options.ZoomFactor;
            var dpiScale = effectiveDpi / 72.0;
            var width = (int)(pageSize.Width * dpiScale);
            var height = (int)(pageSize.Height * dpiScale);

            Console.WriteLine($"[Render] Page {pageNumber}, BaseDPI={options.Dpi}, Zoom={options.ZoomFactor:F2}, EffectiveDPI={effectiveDpi:F0}, Size={width}x{height}");

            using var image = pdfDoc.Render(pageNumber, width, height, true);
            using var ms = new MemoryStream();
            image.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }, cancellationToken);

        _pageCache.Add(cacheKey, data);
        return data;
    }

    public async Task<byte[]> RenderThumbnailAsync(Core.Interfaces.IPdfDocument document, int pageNumber, int thumbnailWidth, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{document.FilePath}:thumb:{pageNumber}:{thumbnailWidth}";

        if (_pageCache.TryGet(cacheKey, out var cachedData))
        {
            return cachedData;
        }

        var data = await Task.Run(() =>
        {
            if (document is not PdfiumDocument pdfiumDoc)
                throw new ArgumentException("Invalid document type", nameof(document));

            var pdfDoc = pdfiumDoc.InternalDocument;
            var (width, height) = document.GetPageSize(pageNumber);
            var aspectRatio = width / height;
            var thumbnailHeight = (int)(thumbnailWidth / aspectRatio);

            using var image = pdfDoc.Render(pageNumber, thumbnailWidth, thumbnailHeight, true);
            using var ms = new MemoryStream();
            image.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }, cancellationToken);

        _pageCache.Add(cacheKey, data);
        return data;
    }

    public void ClearCache()
    {
        _pageCache.Clear();
    }

    public List<PdfBookmark> GetBookmarks(IPdfDocument document)
    {
        // PdfiumViewer doesn't expose bookmark API directly
        // Return empty list - bookmarks feature will show "No bookmarks" message
        // Future: Could use PDFium's native bookmark API via P/Invoke
        return new List<PdfBookmark>();
    }

    private class PdfiumDocument : Core.Interfaces.IPdfDocument
    {
        private PdfiumViewer.PdfDocument? _document;
        private bool _disposed;

        public PdfiumViewer.PdfDocument InternalDocument => _document ?? throw new ObjectDisposedException(nameof(PdfiumDocument));
        public int PageCount => InternalDocument.PageCount;
        public string FilePath { get; }

        public PdfiumDocument(PdfiumViewer.PdfDocument document, string filePath)
        {
            _document = document;
            FilePath = filePath;
        }

        public Task<byte[]> RenderPageAsync(int pageNumber, double dpi, double zoomFactor, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                var pageSize = InternalDocument.PageSizes[pageNumber];
                var dpiScale = dpi / 72.0 * zoomFactor;
                var width = (int)(pageSize.Width * dpiScale);
                var height = (int)(pageSize.Height * dpiScale);

                using var image = InternalDocument.Render(pageNumber, width, height, true);
                using var ms = new MemoryStream();
                image.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }, cancellationToken);
        }

        public (double width, double height) GetPageSize(int pageNumber)
        {
            var size = InternalDocument.PageSizes[pageNumber];
            return (size.Width, size.Height);
        }

        public Task<string> ExtractTextAsync(int pageNumber, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                try
                {
                    return InternalDocument.GetPdfText(pageNumber);
                }
                catch
                {
                    return string.Empty;
                }
            }, cancellationToken);
        }

        public void Close()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed) return;

            _document?.Dispose();
            _document = null;
            _disposed = true;

            GC.SuppressFinalize(this);
        }
    }
}
