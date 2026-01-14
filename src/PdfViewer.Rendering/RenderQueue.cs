using System.Collections.Concurrent;

namespace PdfViewer.Rendering;

public class RenderQueue
{
    private readonly ConcurrentQueue<RenderTask> _queue = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private Task? _processingTask;
    private bool _isRunning;

    public event EventHandler<RenderCompletedEventArgs>? RenderCompleted;

    public void Start()
    {
        if (_isRunning) return;

        _isRunning = true;
        _processingTask = Task.Run(ProcessQueueAsync);
    }

    public void Stop()
    {
        _isRunning = false;
        _cancellationTokenSource.Cancel();
        _processingTask?.Wait(TimeSpan.FromSeconds(5));
    }

    public void Enqueue(RenderTask task)
    {
        _queue.Enqueue(task);
    }

    public void Clear()
    {
        while (_queue.TryDequeue(out _)) { }
    }

    private async Task ProcessQueueAsync()
    {
        while (_isRunning && !_cancellationTokenSource.Token.IsCancellationRequested)
        {
            if (_queue.TryDequeue(out var task))
            {
                try
                {
                    await _semaphore.WaitAsync(_cancellationTokenSource.Token);
                    try
                    {
                        var result = await task.RenderFunc();
                        RenderCompleted?.Invoke(this, new RenderCompletedEventArgs
                        {
                            PageNumber = task.PageNumber,
                            Data = result,
                            Success = true
                        });
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }
                catch (Exception ex)
                {
                    RenderCompleted?.Invoke(this, new RenderCompletedEventArgs
                    {
                        PageNumber = task.PageNumber,
                        Success = false,
                        Error = ex
                    });
                }
            }
            else
            {
                await Task.Delay(100, _cancellationTokenSource.Token);
            }
        }
    }
}

public class RenderTask
{
    public required int PageNumber { get; set; }
    public required Func<Task<byte[]>> RenderFunc { get; set; }
}

public class RenderCompletedEventArgs : EventArgs
{
    public int PageNumber { get; set; }
    public byte[]? Data { get; set; }
    public bool Success { get; set; }
    public Exception? Error { get; set; }
}
