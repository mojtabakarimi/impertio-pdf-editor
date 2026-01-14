namespace PdfViewer.Core.Interfaces;

public interface IPdfDocument : IDisposable
{
    int PageCount { get; }
    string FilePath { get; }

    Task<byte[]> RenderPageAsync(int pageNumber, double dpi, double zoomFactor, CancellationToken cancellationToken = default);
    (double width, double height) GetPageSize(int pageNumber);
    Task<string> ExtractTextAsync(int pageNumber, CancellationToken cancellationToken = default);
    void Close();
}
