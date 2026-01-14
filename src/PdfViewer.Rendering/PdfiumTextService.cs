using PdfViewer.Core.Interfaces;
using System.Text.RegularExpressions;

namespace PdfViewer.Rendering;

public class PdfiumTextService : ITextSearchService
{
    private IPdfDocument? _document;

    public void SetDocument(IPdfDocument? document)
    {
        _document = document;
    }

    public async Task<string> ExtractPageTextAsync(int pageNumber, CancellationToken ct = default)
    {
        if (_document == null)
            return string.Empty;

        return await _document.ExtractTextAsync(pageNumber, ct);
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query, SearchOptions options, CancellationToken ct = default)
    {
        if (_document == null || string.IsNullOrWhiteSpace(query))
            return Array.Empty<SearchResult>();

        var results = new List<SearchResult>();
        var startPage = options.StartPage;
        var endPage = options.EndPage < 0 ? _document.PageCount - 1 : options.EndPage;

        // Build regex pattern
        var pattern = options.WholeWord
            ? $@"\b{Regex.Escape(query)}\b"
            : Regex.Escape(query);

        var regexOptions = options.MatchCase
            ? RegexOptions.None
            : RegexOptions.IgnoreCase;

        var regex = new Regex(pattern, regexOptions);

        for (int pageIndex = startPage; pageIndex <= endPage; pageIndex++)
        {
            ct.ThrowIfCancellationRequested();

            var pageText = await _document.ExtractTextAsync(pageIndex, ct);
            if (string.IsNullOrEmpty(pageText))
                continue;

            var matches = regex.Matches(pageText);
            foreach (Match match in matches)
            {
                // Create context snippet (50 chars before and after)
                var snippetStart = Math.Max(0, match.Index - 50);
                var snippetEnd = Math.Min(pageText.Length, match.Index + match.Length + 50);
                var snippet = pageText.Substring(snippetStart, snippetEnd - snippetStart);

                // Add ellipsis if truncated
                if (snippetStart > 0) snippet = "..." + snippet;
                if (snippetEnd < pageText.Length) snippet = snippet + "...";

                results.Add(new SearchResult
                {
                    PageNumber = pageIndex + 1, // 1-based for display
                    StartIndex = match.Index,
                    Length = match.Length,
                    ContextSnippet = snippet.Replace("\n", " ").Replace("\r", "").Trim()
                });
            }
        }

        return results;
    }
}
