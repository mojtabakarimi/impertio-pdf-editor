namespace PdfViewer.Core.Interfaces;

public interface IPrintService
{
    Task<bool> PrintAsync(PrintOptions options, CancellationToken ct = default);
    Task<IReadOnlyList<PrinterInfo>> GetAvailablePrintersAsync();
    PrinterInfo? GetDefaultPrinter();
    void SetDocument(IPdfDocument? document);
}

public class PrintOptions
{
    public string? PrinterName { get; set; }
    public int StartPage { get; set; } = 1;
    public int EndPage { get; set; } = -1; // -1 means all pages
    public int Copies { get; set; } = 1;
    public bool Collate { get; set; } = true;
    public PrintOrientation Orientation { get; set; } = PrintOrientation.Auto;
    public PrintScaling Scaling { get; set; } = PrintScaling.FitToPage;
}

public enum PrintOrientation
{
    Auto,
    Portrait,
    Landscape
}

public enum PrintScaling
{
    None,
    FitToPage,
    ShrinkToPage
}

public class PrinterInfo
{
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsAvailable { get; set; }
}
