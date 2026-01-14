namespace PdfViewer.Core.Models;

public class PdfPage
{
    public int PageNumber { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public int Rotation { get; set; }

    public PdfPage(int pageNumber, double width, double height, int rotation = 0)
    {
        PageNumber = pageNumber;
        Width = width;
        Height = height;
        Rotation = rotation;
    }

    public double GetAspectRatio() => Width / Height;
}
