using PdfViewer.Core.Interfaces;
using System.Drawing.Printing;

namespace PdfViewer.Rendering;

public class PdfiumPrintService : IPrintService
{
    private IPdfDocument? _document;

    public void SetDocument(IPdfDocument? document)
    {
        _document = document;
    }

    public Task<IReadOnlyList<PrinterInfo>> GetAvailablePrintersAsync()
    {
        return Task.Run(() =>
        {
            var printers = new List<PrinterInfo>();
            var defaultPrinter = new PrinterSettings().PrinterName;

            foreach (string printerName in PrinterSettings.InstalledPrinters)
            {
                printers.Add(new PrinterInfo
                {
                    Name = printerName,
                    IsDefault = printerName == defaultPrinter,
                    IsAvailable = true
                });
            }

            return (IReadOnlyList<PrinterInfo>)printers;
        });
    }

    public PrinterInfo? GetDefaultPrinter()
    {
        var settings = new PrinterSettings();
        return new PrinterInfo
        {
            Name = settings.PrinterName,
            IsDefault = true,
            IsAvailable = true
        };
    }

    public async Task<bool> PrintAsync(PrintOptions options, CancellationToken ct = default)
    {
        if (_document == null)
            return false;

        return await Task.Run(() =>
        {
            try
            {
                using var printDoc = new PrintDocument();

                var printerSettings = new PrinterSettings
                {
                    PrinterName = options.PrinterName ?? new PrinterSettings().PrinterName,
                    Copies = (short)options.Copies,
                    Collate = options.Collate
                };
                printDoc.PrinterSettings = printerSettings;

                int currentPage = options.StartPage - 1; // 0-indexed
                int endPage = options.EndPage > 0 ? options.EndPage - 1 : (_document.PageCount - 1);

                printDoc.PrintPage += (sender, e) =>
                {
                    if (_document == null || e.Graphics == null)
                    {
                        e.HasMorePages = false;
                        return;
                    }

                    // Render the current page at high quality for printing
                    var pageData = _document.RenderPageAsync(currentPage, 300, 1.0).GetAwaiter().GetResult();
                    using var ms = new MemoryStream(pageData);
                    using var image = System.Drawing.Image.FromStream(ms);

                    // Calculate scaling to fit page
                    var printArea = e.MarginBounds;
                    var scaleX = (float)printArea.Width / image.Width;
                    var scaleY = (float)printArea.Height / image.Height;
                    var scale = Math.Min(scaleX, scaleY);

                    if (options.Scaling == PrintScaling.None)
                        scale = 1.0f;
                    else if (options.Scaling == PrintScaling.ShrinkToPage && scale > 1.0f)
                        scale = 1.0f;

                    var width = image.Width * scale;
                    var height = image.Height * scale;
                    var x = printArea.X + (printArea.Width - width) / 2;
                    var y = printArea.Y + (printArea.Height - height) / 2;

                    e.Graphics.DrawImage(image, x, y, width, height);

                    currentPage++;
                    e.HasMorePages = currentPage <= endPage;
                };

                printDoc.Print();
                return true;
            }
            catch
            {
                return false;
            }
        }, ct);
    }
}
