namespace PdfViewer.Core.Interfaces;

public interface ITextSearchService
{
    Task<IReadOnlyList<SearchResult>> SearchAsync(string query, SearchOptions options, CancellationToken ct = default);
    Task<string> ExtractPageTextAsync(int pageNumber, CancellationToken ct = default);
    void SetDocument(IPdfDocument? document);
}

public class SearchResult
{
    public int PageNumber { get; set; }
    public int StartIndex { get; set; }
    public int Length { get; set; }
    public string ContextSnippet { get; set; } = string.Empty;
    public string MatchedText { get; set; } = string.Empty;

    // Text position info for highlighting (if available)
    public List<TextBounds> Bounds { get; set; } = new();
}

public class TextBounds
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Right { get; set; }
    public double Bottom { get; set; }
}

public class SearchOptions
{
    public bool MatchCase { get; set; }
    public bool WholeWord { get; set; }
    public int StartPage { get; set; } = 0;
    public int EndPage { get; set; } = -1; // -1 means all pages
}
