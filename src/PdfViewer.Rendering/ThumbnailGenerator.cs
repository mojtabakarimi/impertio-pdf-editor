using PdfViewer.Core.Interfaces;

namespace PdfViewer.Rendering;

public class ThumbnailGenerator
{
    private readonly IPdfRenderService _renderService;
    private readonly int _defaultThumbnailWidth;

    public ThumbnailGenerator(IPdfRenderService renderService, int defaultThumbnailWidth = 150)
    {
        _renderService = renderService ?? throw new ArgumentNullException(nameof(renderService));
        _defaultThumbnailWidth = defaultThumbnailWidth;
    }

    public async Task<byte[]> GenerateThumbnailAsync(IPdfDocument document, int pageNumber, CancellationToken cancellationToken = default)
    {
        return await _renderService.RenderThumbnailAsync(document, pageNumber, _defaultThumbnailWidth, cancellationToken);
    }

    public async Task<Dictionary<int, byte[]>> GenerateThumbnailsAsync(IPdfDocument document, int startPage, int endPage, CancellationToken cancellationToken = default)
    {
        var thumbnails = new Dictionary<int, byte[]>();
        var tasks = new List<Task<(int pageNumber, byte[] data)>>();

        for (int i = startPage; i <= endPage && i < document.PageCount; i++)
        {
            int pageNumber = i;
            var task = Task.Run(async () =>
            {
                var thumbnail = await GenerateThumbnailAsync(document, pageNumber, cancellationToken);
                return (pageNumber, thumbnail);
            }, cancellationToken);

            tasks.Add(task);
        }

        var results = await Task.WhenAll(tasks);

        foreach (var (pageNumber, data) in results)
        {
            thumbnails[pageNumber] = data;
        }

        return thumbnails;
    }
}
