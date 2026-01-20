using PdfViewer.Core.Interfaces;
using PdfViewer.Core.Models;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;

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

        public List<HighlightRect> FindTextBounds(int pageNumber, string searchText, bool matchCase = false, bool wholeWord = false)
        {
            var results = new List<HighlightRect>();

            if (string.IsNullOrEmpty(searchText) || _document == null)
                return results;

            try
            {
                // Get the native document handle via reflection
                var docField = _document.GetType().GetField("_document", BindingFlags.NonPublic | BindingFlags.Instance);
                if (docField == null)
                    docField = _document.GetType().GetField("_file", BindingFlags.NonPublic | BindingFlags.Instance);

                if (docField == null)
                    return results;

                var fileObj = docField.GetValue(_document);
                if (fileObj == null)
                    return results;

                // Get the document handle
                var handleProp = fileObj.GetType().GetProperty("Document", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (handleProp == null)
                    return results;

                var docHandle = (IntPtr?)handleProp.GetValue(fileObj);
                if (docHandle == null || docHandle == IntPtr.Zero)
                    return results;

                // Load the page
                var pageHandle = PdfiumNative.FPDF_LoadPage(docHandle.Value, pageNumber);
                if (pageHandle == IntPtr.Zero)
                    return results;

                try
                {
                    // Load the text page
                    var textPage = PdfiumNative.FPDFText_LoadPage(pageHandle);
                    if (textPage == IntPtr.Zero)
                        return results;

                    try
                    {
                        // Build search flags
                        uint flags = 0;
                        if (matchCase) flags |= PdfiumNative.FPDF_MATCHCASE;
                        if (wholeWord) flags |= PdfiumNative.FPDF_MATCHWHOLEWORD;

                        // Start the search
                        var findHandle = PdfiumNative.FPDFText_FindStart(textPage, searchText, flags, 0);
                        if (findHandle == IntPtr.Zero)
                            return results;

                        try
                        {
                            // Get page size for coordinate conversion
                            var pageSize = _document.PageSizes[pageNumber];

                            // Find all matches
                            while (PdfiumNative.FPDFText_FindNext(findHandle) != 0)
                            {
                                int charIndex = PdfiumNative.FPDFText_GetSchResultIndex(findHandle);
                                int charCount = PdfiumNative.FPDFText_GetSchCount(findHandle);

                                if (charCount <= 0)
                                    continue;

                                // Get bounding boxes for each character and merge them
                                double minLeft = double.MaxValue;
                                double maxRight = double.MinValue;
                                double minBottom = double.MaxValue;
                                double maxTop = double.MinValue;

                                for (int i = 0; i < charCount; i++)
                                {
                                    double left, right, bottom, top;
                                    if (PdfiumNative.FPDFText_GetCharBox(textPage, charIndex + i,
                                        out left, out right, out bottom, out top) != 0)
                                    {
                                        if (left < minLeft) minLeft = left;
                                        if (right > maxRight) maxRight = right;
                                        if (bottom < minBottom) minBottom = bottom;
                                        if (top > maxTop) maxTop = top;
                                    }
                                }

                                if (minLeft != double.MaxValue)
                                {
                                    results.Add(new HighlightRect(minLeft, maxTop, maxRight, minBottom));
                                }
                            }
                        }
                        finally
                        {
                            PdfiumNative.FPDFText_FindClose(findHandle);
                        }
                    }
                    finally
                    {
                        PdfiumNative.FPDFText_ClosePage(textPage);
                    }
                }
                finally
                {
                    PdfiumNative.FPDF_ClosePage(pageHandle);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error finding text bounds: {ex.Message}");
            }

            return results;
        }
    }
}
