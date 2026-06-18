namespace Google_Like_Blazor.Pages
{
    public partial class Index
    {
        [Inject] RepositoryCache _repositoryCache { get; set; } = null!;
        [Inject] IFileRepo _file { get; set; } = null!;
        [Inject] MemoryStorageUtility MemoryStorageUtility { get; set; } = null!;

        // ── State ─────────────────────────────────────────────────
        public bool loading { get; set; }
        public string SearchWord { get; set; } = string.Empty;
        public int FileCount { get; set; }
        public long elapsedMs { get; set; }
        public FileViewModel selectedItem { get; set; } = new();
        public List<FileViewModel> files { get; set; } = new();
        public string? errorMessage { get; set; }

        // ── Debounce + cancellation ───────────────────────────────
        private CancellationTokenSource? _searchCts;
        private Timer? _debounceTimer;
        private bool _initialized;
        private const int DebounceMs = 300;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender && !_initialized)
            {
                _initialized = true;
                loading = true;
                StateHasChanged();

                try
                {
                    FileCount = await _repositoryCache.GetFilesCount();
                    var tempFiles = await _repositoryCache.GetFiles("tennis");
                    files = tempFiles;
                }
                catch (Exception ex)
                {
                    errorMessage = $"Failed to load initial data: {ex.Message}";
                }
                finally
                {
                    loading = false;
                    StateHasChanged();
                }
            }
        }

        // ── OnParametersSetAsync is called on every render cycle — guard it ──

        protected override async Task OnParametersSetAsync()
        {
            if (!_initialized) return; // skip: OnAfterRenderAsync handles first load
            await base.OnParametersSetAsync();
        }

        public async Task SetValue(string keyword, List<FileViewModel> fileViewModels)
            => MemoryStorageUtility.Set(keyword, fileViewModels);

        public async Task<List<FileViewModel>> GetValueFromMemoryStorage(string keyword)
        {
            if (MemoryStorageUtility.Storage.TryGetValue(keyword, out var value))
                return (List<FileViewModel>)value;

            var p = await _repositoryCache.GetFiles(keyword);
            StateHasChanged();
            await SetValue(keyword, p);
            StateHasChanged();
            return p;
        }

        public void ClearAll() => MemoryStorageUtility.Storage.Clear();

        // ── Debounced main search ─────────────────────────────────

        protected void InputChanged(string searchWord)
        {
            _debounceTimer?.Dispose();
            CancelSearch();
            SearchWord = searchWord;

            if (string.IsNullOrWhiteSpace(searchWord))
            {
                files.Clear();
                FileCount = 0;
                return;
            }

            _debounceTimer = new Timer(async _ =>
            {
                await InvokeAsync(() => DoStreamingSearch(searchWord));
            }, null, DebounceMs, Timeout.Infinite);
        }

        // ── Immediate benchmark searches ──────────────────────────

        protected async Task InputChangedFor(string searchWord)
        {
            if (string.IsNullOrWhiteSpace(searchWord)) return;
            CancelSearch();
            await DoParallelDeepSearch(searchWord);
        }

        protected async Task InputChangedTaskWhenAll(string searchWord)
        {
            if (string.IsNullOrWhiteSpace(searchWord)) return;
            CancelSearch();
            await DoTaskWhenAllSearch(searchWord);
        }

        protected async Task InputChangedParalell(string searchWord)
        {
            if (string.IsNullOrWhiteSpace(searchWord)) return;
            CancelSearch();
            await DoParallelSearch(searchWord);
        }

        // ── Streaming search (IAsyncEnumerable — results appear as they arrive) ──

        private async Task DoStreamingSearch(string searchWord)
        {
            CancelSearch();
            _searchCts = new CancellationTokenSource();
            var ct = _searchCts.Token;

            try
            {
                FileCount = 0;
                elapsedMs = 0;
                errorMessage = null;
                var watch = System.Diagnostics.Stopwatch.StartNew();

                loading = true;
                files.Clear();
                StateHasChanged();

                // Phase 1: quick filename search
                var nameResults = await _file.SearchInFileName(searchWord);
                if (!ct.IsCancellationRequested)
                {
                    files = nameResults;
                    FileCount = files.Count;
                    StateHasChanged();
                }

                // Phase 2: streaming content search with IAsyncEnumerable
                if (!ct.IsCancellationRequested)
                {
                    await foreach (var vm in _file.SearchInContentAsyncEnum(searchWord, ct))
                    {
                        if (!files.Any(f => f.Id == vm.Id))
                        {
                            files.Add(vm);
                            FileCount = files.Count;
                            StateHasChanged(); // UI updates incrementally
                        }
                    }
                }

                SearchWord = searchWord;
                loading = false;
                watch.Stop();
                elapsedMs = watch.ElapsedMilliseconds;
                StateHasChanged();
            }
            catch (OperationCanceledException) { /* search superseded */ }
            catch (Exception ex)
            {
                errorMessage = $"Search failed: {ex.Message}";
                loading = false;
                StateHasChanged();
            }
        }

        private async Task DoParallelDeepSearch(string searchWord)
        {
            _searchCts = new CancellationTokenSource();
            try
            {
                FileCount = 0; elapsedMs = 0; errorMessage = null;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                loading = true; StateHasChanged();
                files = await _file.SearchInContentParelleDeep2(searchWord);
                FileCount = files.Count; SearchWord = searchWord;
                loading = false; watch.Stop();
                elapsedMs = watch.ElapsedMilliseconds; StateHasChanged();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { errorMessage = ex.Message; loading = false; StateHasChanged(); }
        }

        private async Task DoTaskWhenAllSearch(string searchWord)
        {
            _searchCts = new CancellationTokenSource();
            try
            {
                FileCount = 0; elapsedMs = 0; errorMessage = null;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                loading = true; StateHasChanged();
                files = await _file.SearchInContentTask(searchWord);
                FileCount = files.Count; SearchWord = searchWord;
                loading = false; watch.Stop();
                elapsedMs = watch.ElapsedMilliseconds; StateHasChanged();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { errorMessage = ex.Message; loading = false; StateHasChanged(); }
        }

        private async Task DoParallelSearch(string searchWord)
        {
            _searchCts = new CancellationTokenSource();
            try
            {
                FileCount = 0; elapsedMs = 0; errorMessage = null;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                loading = true; StateHasChanged();
                files = await _file.SearchInContentParelle(searchWord);
                FileCount = files.Count; SearchWord = searchWord;
                loading = false; watch.Stop();
                elapsedMs = watch.ElapsedMilliseconds; StateHasChanged();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { errorMessage = ex.Message; loading = false; StateHasChanged(); }
        }

        private void CancelSearch()
        {
            if (_searchCts != null)
            {
                _searchCts.Cancel();
                _searchCts.Dispose();
                _searchCts = null;
            }
        }

        protected async Task ShowFile(string Id) { }

        public void Dispose()
        {
            _debounceTimer?.Dispose();
            CancelSearch();
        }
    }
}
