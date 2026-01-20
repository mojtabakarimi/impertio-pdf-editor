#r "src/PdfViewer.Rendering/bin/Debug/net10.0/PdfViewer.Core.dll"
#r "src/PdfViewer.Rendering/bin/Debug/net10.0/PdfViewer.Rendering.dll"
#r "src/PdfViewer.Rendering/bin/Debug/net10.0/PdfiumViewer.dll"

using PdfViewer.Rendering;
using PdfViewer.Core.Interfaces;
using System;
using System.IO;
using System.Threading.Tasks;

// Get the first PDF in a common location or ask for path
var testPdfPath = args.Length > 0 ? args[0] : null;

if (string.IsNullOrEmpty(testPdfPath))
{
    Console.WriteLine("Usage: dotnet script SearchTest.csx <path-to-pdf>");
    return;
}

Console.WriteLine($"Testing PDF: {testPdfPath}");
Console.WriteLine(new string('=', 60));

var renderService = new PdfiumRenderService();
var document = await renderService.LoadDocumentAsync(testPdfPath);

Console.WriteLine($"Total pages: {document.PageCount}");
Console.WriteLine();

// Extract text from first 10 pages and last 10 pages
Console.WriteLine("=== Text extraction sample ===");

var pagesToCheck = new List<int>();
for (int i = 0; i < Math.Min(5, document.PageCount); i++) pagesToCheck.Add(i);
for (int i = Math.Max(5, document.PageCount - 5); i < document.PageCount; i++) pagesToCheck.Add(i);

foreach (var pageNum in pagesToCheck.Distinct())
{
    var text = await document.ExtractTextAsync(pageNum);
    var preview = text.Length > 100 ? text.Substring(0, 100).Replace("\n", " ") + "..." : text.Replace("\n", " ");
    Console.WriteLine($"Page {pageNum + 1}: {text.Length} chars - \"{preview}\"");
}

Console.WriteLine();
Console.WriteLine("=== Search test ===");

var textService = new PdfiumTextService();
textService.SetDocument(document);

var searchTerms = new[] { "the", "this", "and", "page" };

foreach (var term in searchTerms)
{
    var results = await textService.SearchAsync(term, new SearchOptions(), default);
    var pages = results.Select(r => r.PageNumber).Distinct().OrderBy(p => p).ToList();
    Console.WriteLine($"'{term}': {results.Count} matches on {pages.Count} pages");
    if (pages.Count > 0 && pages.Count <= 10)
    {
        Console.WriteLine($"  Pages: {string.Join(", ", pages)}");
    }
    else if (pages.Count > 10)
    {
        Console.WriteLine($"  Pages: {string.Join(", ", pages.Take(5))}...{string.Join(", ", pages.TakeLast(5))}");
    }
}

document.Dispose();
Console.WriteLine("\nDone!");
