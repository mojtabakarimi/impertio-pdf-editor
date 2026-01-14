using PdfViewer.Core.Models;

namespace PdfViewer.Core.Interfaces;

public interface IDocumentLoader
{
    Task<PdfDocument> LoadDocumentMetadataAsync(string filePath, CancellationToken cancellationToken = default);
    Task<bool> ValidatePdfFileAsync(string filePath, CancellationToken cancellationToken = default);
}
