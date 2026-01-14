using PdfViewer.Core.Models;
using System.Collections.Generic;

namespace PdfViewer.Core.Interfaces;

public interface IPdfRenderService
{
    Task<IPdfDocument> LoadDocumentAsync(string filePath, CancellationToken cancellationToken = default);
    Task<byte[]> RenderPageAsync(IPdfDocument document, int pageNumber, RenderOptions options, CancellationToken cancellationToken = default);
    Task<byte[]> RenderThumbnailAsync(IPdfDocument document, int pageNumber, int thumbnailWidth, CancellationToken cancellationToken = default);
    void ClearCache();
    List<PdfBookmark> GetBookmarks(IPdfDocument document);
}
