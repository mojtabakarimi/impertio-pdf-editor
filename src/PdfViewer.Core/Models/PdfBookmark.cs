using System.Collections.Generic;

namespace PdfViewer.Core.Models;

public class PdfBookmark
{
    public string Title { get; set; } = string.Empty;
    public int PageNumber { get; set; }
    public List<PdfBookmark> Children { get; set; } = new();
}
