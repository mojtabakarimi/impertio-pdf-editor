namespace PdfViewer.Core.Models;

public class PdfDocument
{
    public string FilePath { get; set; }
    public string FileName { get; set; }
    public int PageCount { get; set; }
    public List<PdfPage> Pages { get; set; }
    public string Title { get; set; }
    public string Author { get; set; }
    public string Subject { get; set; }
    public DateTime? CreationDate { get; set; }

    public PdfDocument(string filePath)
    {
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
        Pages = new List<PdfPage>();
        Title = string.Empty;
        Author = string.Empty;
        Subject = string.Empty;
    }

    public PdfPage? GetPage(int pageNumber)
    {
        return Pages.FirstOrDefault(p => p.PageNumber == pageNumber);
    }
}
