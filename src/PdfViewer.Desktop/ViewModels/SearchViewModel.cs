using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfViewer.Core.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace PdfViewer.Desktop.ViewModels;

public partial class SearchViewModel : ObservableObject
{
    private readonly ITextSearchService _searchService;
    private readonly Func<int, Task> _navigateToPage;
    private CancellationTokenSource? _searchCts;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _matchCase;

    [ObservableProperty]
    private bool _wholeWord;

    [ObservableProperty]
    private ObservableCollection<SearchResult> _results = new();

    [ObservableProperty]
    private int _currentResultIndex = -1;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _isSearching;

    public SearchViewModel(ITextSearchService searchService, Func<int, Task> navigateToPage)
    {
        _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
        _navigateToPage = navigateToPage ?? throw new ArgumentNullException(nameof(navigateToPage));
    }

    [RelayCommand]
    private async Task Search()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            Results.Clear();
            CurrentResultIndex = -1;
            StatusText = string.Empty;
            return;
        }

        // Cancel previous search
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();

        try
        {
            IsSearching = true;
            StatusText = "Searching...";
            Results.Clear();
            CurrentResultIndex = -1;

            var options = new SearchOptions
            {
                MatchCase = MatchCase,
                WholeWord = WholeWord
            };

            var searchResults = await _searchService.SearchAsync(SearchQuery, options, _searchCts.Token);

            Results.Clear();
            foreach (var result in searchResults)
            {
                Results.Add(result);
            }

            if (Results.Count > 0)
            {
                CurrentResultIndex = 0;
                StatusText = $"1 of {Results.Count} matches";
                await GoToCurrentResult();
            }
            else
            {
                StatusText = "No matches found";
            }
        }
        catch (OperationCanceledException)
        {
            // Search was cancelled, ignore
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    private async Task NextResult()
    {
        if (Results.Count == 0) return;

        CurrentResultIndex = (CurrentResultIndex + 1) % Results.Count;
        StatusText = $"{CurrentResultIndex + 1} of {Results.Count} matches";
        await GoToCurrentResult();
    }

    [RelayCommand]
    private async Task PreviousResult()
    {
        if (Results.Count == 0) return;

        CurrentResultIndex = CurrentResultIndex <= 0 ? Results.Count - 1 : CurrentResultIndex - 1;
        StatusText = $"{CurrentResultIndex + 1} of {Results.Count} matches";
        await GoToCurrentResult();
    }

    private async Task GoToCurrentResult()
    {
        if (CurrentResultIndex >= 0 && CurrentResultIndex < Results.Count)
        {
            var result = Results[CurrentResultIndex];
            await _navigateToPage(result.PageNumber);
        }
    }

    public void Clear()
    {
        SearchQuery = string.Empty;
        Results.Clear();
        CurrentResultIndex = -1;
        StatusText = string.Empty;
        _searchCts?.Cancel();
    }
}
