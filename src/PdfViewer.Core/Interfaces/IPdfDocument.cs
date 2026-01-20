namespace PdfViewer.Core.Interfaces;

public interface IPdfDocument : IDisposable
{
    int PageCount { get; }
    string FilePath { get; }

    Task<byte[]> RenderPageAsync(int pageNumber, double dpi, double zoomFactor, CancellationToken cancellationToken = default);
    (double width, double height) GetPageSize(int pageNumber);
    Task<string> ExtractTextAsync(int pageNumber, CancellationToken cancellationToken = default);
    void Close();

    /// <summary>
    /// Search for text on a page and return bounding rectangles for highlights
    /// </summary>
    List<HighlightRect> FindTextBounds(int pageNumber, string searchText, bool matchCase = false, bool wholeWord = false);
}

/// <summary>
/// Rectangle for highlighting text on PDF page (in PDF coordinates - origin at bottom-left)
/// </summary>
public class HighlightRect
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Right { get; set; }
    public double Bottom { get; set; }
    public double Width => Right - Left;
    public double Height => Top - Bottom; // PDF coordinates: top > bottom

    public HighlightRect(double left, double top, double right, double bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }
}
