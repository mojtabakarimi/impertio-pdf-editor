using System.Runtime.InteropServices;

namespace PdfViewer.Rendering;

/// <summary>
/// P/Invoke declarations for PDFium native text functions
/// </summary>
internal static class PdfiumNative
{
    private const string PdfiumDll = "pdfium.dll";

    // Text page functions
    [DllImport(PdfiumDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr FPDFText_LoadPage(IntPtr page);

    [DllImport(PdfiumDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FPDFText_ClosePage(IntPtr textPage);

    [DllImport(PdfiumDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int FPDFText_CountChars(IntPtr textPage);

    [DllImport(PdfiumDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int FPDFText_GetCharBox(IntPtr textPage, int index,
        out double left, out double right, out double bottom, out double top);

    // Text search functions
    [DllImport(PdfiumDll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    public static extern IntPtr FPDFText_FindStart(IntPtr textPage,
        [MarshalAs(UnmanagedType.LPWStr)] string findWhat,
        uint flags, int startIndex);

    [DllImport(PdfiumDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int FPDFText_FindNext(IntPtr findHandle);

    [DllImport(PdfiumDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int FPDFText_FindPrev(IntPtr findHandle);

    [DllImport(PdfiumDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FPDFText_FindClose(IntPtr findHandle);

    [DllImport(PdfiumDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int FPDFText_GetSchResultIndex(IntPtr findHandle);

    [DllImport(PdfiumDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int FPDFText_GetSchCount(IntPtr findHandle);

    // Get text within a rectangle
    [DllImport(PdfiumDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int FPDFText_GetBoundedText(IntPtr textPage,
        double left, double top, double right, double bottom,
        IntPtr buffer, int buflen);

    // Get character index at position
    [DllImport(PdfiumDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int FPDFText_GetCharIndexAtPos(IntPtr textPage,
        double x, double y, double xTolerance, double yTolerance);

    // Page functions (needed to get page handle)
    [DllImport(PdfiumDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr FPDF_LoadPage(IntPtr document, int pageIndex);

    [DllImport(PdfiumDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FPDF_ClosePage(IntPtr page);

    // Search flags
    public const uint FPDF_MATCHCASE = 0x00000001;
    public const uint FPDF_MATCHWHOLEWORD = 0x00000002;
}

/// <summary>
/// Represents a text match with its bounding rectangles
/// </summary>
public class TextMatchBounds
{
    public int PageNumber { get; set; }
    public int CharIndex { get; set; }
    public int CharCount { get; set; }
    public List<Rect> Rectangles { get; set; } = new();
}

/// <summary>
/// Simple rectangle structure for text bounds
/// </summary>
public struct Rect
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Right { get; set; }
    public double Bottom { get; set; }
    public double Width => Right - Left;
    public double Height => Bottom - Top;

    public Rect(double left, double top, double right, double bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }
}
