namespace PdfViewer.Core.Models;

public class RenderOptions
{
    public double Dpi { get; set; } = 96.0; // Base screen DPI - ZoomFactor multiplies this for actual render DPI
    public double ZoomFactor { get; set; } = 1.0;
    public int Rotation { get; set; } = 0; // 0, 90, 180, 270
    public bool RenderAnnotations { get; set; } = true;
    public bool RenderFormFields { get; set; } = true;

    public RenderOptions Clone()
    {
        return new RenderOptions
        {
            Dpi = Dpi,
            ZoomFactor = ZoomFactor,
            Rotation = Rotation,
            RenderAnnotations = RenderAnnotations,
            RenderFormFields = RenderFormFields
        };
    }
}
