using Docnet.Core;
using Docnet.Core.Models;
using System.Text.RegularExpressions;

Console.WriteLine("PDF Text Extraction Test");
Console.WriteLine(new string('=', 70));

var testFolder = args.Length > 0 ? args[0] : @"D:\Repos\(Impertio)\csharp-pdf-viewer\test pdf";

var pdfFiles = Directory.GetFiles(testFolder, "*.pdf");
Console.WriteLine($"Found {pdfFiles.Length} PDF files in: {testFolder}");
Console.WriteLine();

using var docnetLib = DocLib.Instance;

foreach (var pdfPath in pdfFiles)
{
    var fileName = Path.GetFileName(pdfPath);
    Console.WriteLine(new string('=', 70));
    Console.WriteLine($"FILE: {fileName}");
    Console.WriteLine(new string('-', 70));

    try
    {
        using var doc = docnetLib.GetDocReader(pdfPath, new PageDimensions(1, 1));
        var pageCount = doc.GetPageCount();
        Console.WriteLine($"Pages: {pageCount}");

        // Sample text from first, middle, and last pages
        var pagesToTest = new List<int> { 0 };
        if (pageCount > 2) pagesToTest.Add(pageCount / 2);
        if (pageCount > 1) pagesToTest.Add(pageCount - 1);

        Console.WriteLine();
        Console.WriteLine("Text Samples:");
        foreach (var pageIdx in pagesToTest)
        {
            using var page = doc.GetPageReader(pageIdx);
            var text = page.GetText() ?? "";
            var cleanText = text.Replace("\n", " ").Replace("\r", "").Trim();
            var preview = cleanText.Length > 80 ? cleanText.Substring(0, 80) + "..." : cleanText;
            Console.WriteLine($"  Page {pageIdx + 1,3}: {text.Length,5} chars | \"{preview}\"");
        }

        // Search test
        Console.WriteLine();
        Console.WriteLine("Search Results:");
        var searchTerms = new[] { "the", "and", "page", "1" };

        foreach (var term in searchTerms)
        {
            int totalMatches = 0;
            var pagesWithMatches = new List<int>();

            for (int i = 0; i < pageCount; i++)
            {
                using var page = doc.GetPageReader(i);
                var text = page.GetText() ?? "";
                var count = Regex.Matches(text, $@"\b{Regex.Escape(term)}\b", RegexOptions.IgnoreCase).Count;
                if (count > 0)
                {
                    totalMatches += count;
                    pagesWithMatches.Add(i + 1);
                }
            }

            Console.Write($"  '{term,-6}': {totalMatches,4} matches on {pagesWithMatches.Count,3}/{pageCount} pages");
            if (pagesWithMatches.Count > 0 && pagesWithMatches.Count <= 6)
                Console.Write($" [{string.Join(",", pagesWithMatches)}]");
            else if (pagesWithMatches.Count > 6)
                Console.Write($" [{string.Join(",", pagesWithMatches.Take(3))}...{string.Join(",", pagesWithMatches.TakeLast(3))}]");
            Console.WriteLine();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR: {ex.Message}");
    }

    Console.WriteLine();
}

Console.WriteLine("Test complete!");
