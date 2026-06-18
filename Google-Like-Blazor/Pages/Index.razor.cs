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

        // ── Debounce + cancellation ───────────────────────────────
        private CancellationTokenSource? _searchCts;
        private Timer? _debounceTimer;
        private const int DebounceMs = 300; // wait 300ms after last keystroke

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                loading = true;
                var tempFiles = await _repositoryCache.GetFiles("tennis");
                files = tempFiles;
                FileCount = files.Count;
                loading = false;
                StateHasChanged();
            }
        }

        protected override async Task OnParametersSetAsync()
        {
            FileCount = await _repositoryCache.GetFilesCount();
            StateHasChanged();
            loading = true;
            files = await _file.SearchInFileName("tennis");
            StateHasChanged();
            loading = false;
            await base.OnParametersSetAsync();
        }

        public async Task SetValue(string keyword, List<FileViewModel> fileViewModels)
        {
            MemoryStorageUtility.Set(keyword, fileViewModels);
        }

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

        // ── Debounced search (used by the first SearchBarItem) ────

        protected void InputChanged(string searchWord)
        {
            // Cancel any pending debounce timer and in-flight search
            _debounceTimer?.Dispose();
            CancelSearch();
            SearchWord = searchWord;

            if (string.IsNullOrWhiteSpace(searchWord)) return;

            _debounceTimer = new Timer(async _ =>
            {
                await InvokeAsync(() => DoSearch(searchWord));
            }, null, DebounceMs, Timeout.Infinite);
        }

        // ── Immediate searches (benchmark bars — no debounce) ─────

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

        // ── Core search implementations ───────────────────────────

        private async Task DoSearch(string searchWord)
        {
            CancelSearch();
            _searchCts = new CancellationTokenSource();
            var ct = _searchCts.Token;

            try
            {
                FileCount = 0;
                elapsedMs = 0;
                var watch = System.Diagnostics.Stopwatch.StartNew();

                loading = true;
                StateHasChanged();

                // Phase 1: filename search (fast, always runs)
                files = await _file.SearchInFileName(searchWord);
                loading = false;
                StateHasChanged();

                loading = true;
                var tempFiles = await GetValueFromMemoryStorage(searchWord);
                files = tempFiles;
                FileCount = files.Count;
                StateHasChanged();

                // Phase 2: content search only if no filename matches
                if (FileCount < 1 && !ct.IsCancellationRequested)
                    files = await _file.SearchInContentParelle(searchWord);

                SearchWord = searchWord;
                loading = false;
                watch.Stop();
                elapsedMs = watch.ElapsedMilliseconds;
                StateHasChanged();
            }
            catch (OperationCanceledException) { /* search superseded — ignore */ }
        }

        private async Task DoParallelDeepSearch(string searchWord)
        {
            _searchCts = new CancellationTokenSource();
            try
            {
                FileCount = 0;
                elapsedMs = 0;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                loading = true;
                StateHasChanged();
                files = await _file.SearchInContentParelleDeep2(searchWord);
                FileCount = files.Count;
                SearchWord = searchWord;
                loading = false;
                watch.Stop();
                elapsedMs = watch.ElapsedMilliseconds;
                StateHasChanged();
            }
            catch (OperationCanceledException) { }
        }

        private async Task DoTaskWhenAllSearch(string searchWord)
        {
            _searchCts = new CancellationTokenSource();
            try
            {
                FileCount = 0;
                elapsedMs = 0;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                loading = true;
                StateHasChanged();
                files = await _file.SearchInContentTask(searchWord);
                FileCount = files.Count;
                SearchWord = searchWord;
                loading = false;
                watch.Stop();
                elapsedMs = watch.ElapsedMilliseconds;
                StateHasChanged();
            }
            catch (OperationCanceledException) { }
        }

        private async Task DoParallelSearch(string searchWord)
        {
            _searchCts = new CancellationTokenSource();
            try
            {
                FileCount = 0;
                elapsedMs = 0;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                loading = true;
                StateHasChanged();
                files = await _file.SearchInContentParelle(searchWord);
                FileCount = files.Count;
                SearchWord = searchWord;
                loading = false;
                watch.Stop();
                elapsedMs = watch.ElapsedMilliseconds;
                StateHasChanged();
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>Cancel any in-flight search.</summary>
        private void CancelSearch()
        {
            if (_searchCts != null)
            {
                _searchCts.Cancel();
                _searchCts.Dispose();
                _searchCts = null;
            }
        }

        protected async Task ShowFile(string Id)
        {
            // placeholder for future detail view
        }

        public void Dispose()
        {
            _debounceTimer?.Dispose();
            CancelSearch();
        }
    }
}
