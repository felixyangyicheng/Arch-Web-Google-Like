using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using MongoDB.Driver;
using UglyToad.PdfPig;
using Google_Like_Blazor.Utils;

namespace Google_Like_Blazor.Services
{
    public class FileService : IFileRepo
    {
        private readonly IMongoCollection<FileModel> _collection;
        private readonly ILogger<FileService> _logger;
        private readonly PdfTextCache _textCache;

        public FileService(
            IOptions<ConnectionStringModel> dbSettings,
            ILogger<FileService> logger,
            PdfTextCache textCache)
        {
            var mongoClient = new MongoClient(dbSettings.Value.ConnectionString);
            var mongoDatabase = mongoClient.GetDatabase(dbSettings.Value.DatabaseName);
            _collection = mongoDatabase.GetCollection<FileModel>(dbSettings.Value.ThreedCollectionName);
            _logger = logger;
            _textCache = textCache;
        }

        public FileService(IMongoCollection<FileModel> collection, PdfTextCache textCache)
        {
            _collection = collection;
            _logger = null!;
            _textCache = textCache;
        }

        // ── CRUD ──────────────────────────────────────────────────

        public async Task<bool> CreateAsync(FileModel obj)
        {
            await _collection.InsertOneAsync(obj);
            return true;
        }

        public async Task<List<FileModel>> GetAsync() =>
            await _collection.Find(_ => true).ToListAsync();

        public async Task<FileModel?> GetAsync(string id) =>
            await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();

        public async Task<FileModel?> GetByNameAsync(string name) =>
            await _collection.Find(x => x.FileName == name).FirstOrDefaultAsync();

        public async Task RemoveAsync(string id) =>
            await _collection.DeleteOneAsync(x => x.Id == id);

        public async Task<bool> UpdateAsync(string id, FileModel obj)
        {
            var result = await _collection.ReplaceOneAsync(x => x.Id == id, obj);
            // Invalidate text cache on update since content changed
            _textCache.Remove(id);
            return result.IsAcknowledged && result.ModifiedCount > 0;
        }

        // ── Search by file name ───────────────────────────────────

        public async Task<List<FileModel>> SearchByNameAsync(string name) =>
            await _collection.Find(x =>
                x.FileName.ToLowerInvariant().Contains(name.ToLowerInvariant()) &&
                x.Type.Contains("pdf")
            ).ToListAsync();

        public async Task<List<FileViewModel>> SearchInFileName(string keyword)
        {
            _logger.LogInformation("SearchInFileName: {Keyword}", keyword);
            var list = await _collection.Find(x =>
                x.FileName.ToLowerInvariant().Contains(keyword.ToLowerInvariant()) &&
                x.Type.Contains("pdf")
            ).ToListAsync();

            return list.Select(x => new FileViewModel
            {
                Id = x.Id,
                Content = x.Content,
                Type = x.Type,
                TextToPreview = string.Empty,
                FileName = x.FileName
            }).ToList();
        }

        // ── Full-text search strategies ───────────────────────────
        // ⚡ All methods use GetPagesText() which caches extracted text per file.
        //    First search parses the PDF; subsequent searches hit the cache.

        /// <summary>Sequential search — simplest, slowest for large collections.</summary>
        public async Task<List<FileViewModel>> SearchInContent(string keyword)
        {
            var result = new List<FileViewModel>();
            var list = await _collection.Find(x => x.Type.Contains("pdf")).ToListAsync();

            foreach (var item in list)
            {
                var sb = new StringBuilder();
                var pages = GetPagesText(item);
                for (int i = 0; i < pages.Length; i++)
                    SearchPage(pages[i], i + 1, keyword, sb);

                var vm = ToViewModel(item, sb.ToString());
                if (Matches(vm, keyword)) result.Add(vm);
            }
            return result;
        }

        /// <summary>Parallel across files using <c>Parallel.ForEachAsync</c>.</summary>
        public async Task<List<FileViewModel>> SearchInContentParelle(string keyword)
        {
            var result = new List<FileViewModel>();
            var list = await _collection.Find(x => x.Type.Contains("pdf")).ToListAsync();

            await Parallel.ForEachAsync(list, async (item, ct) =>
            {
                var sb = new StringBuilder();
                var pages = GetPagesText(item);
                for (int i = 0; i < pages.Length; i++)
                    SearchPage(pages[i], i + 1, keyword, sb);

                var vm = ToViewModel(item, sb.ToString());
                if (Matches(vm, keyword))
                    lock (result) { result.Add(vm); }
            });
            return result;
        }

        /// <summary>
        /// Nested parallelism: Parallel.ForEachAsync across files,
        /// then Parallel.ForEach across pages within each file.
        /// </summary>
        public async Task<List<FileViewModel>> SearchInContentParelleDeep2(string keyword)
        {
            var result = new List<FileViewModel>();
            var list = await _collection.Find(x => x.Type.Contains("pdf")).ToListAsync();

            await Parallel.ForEachAsync(list, async (item, ct) =>
            {
                var sb = new StringBuilder();
                var pages = GetPagesText(item);
                // Inner parallelism across pages (no async — Parallel.ForEach is safe)
                Parallel.For(0, pages.Length, i =>
                {
                    SearchPage(pages[i], i + 1, keyword, sb);
                });

                var vm = ToViewModel(item, sb.ToString());
                if (Matches(vm, keyword))
                    lock (result) { result.Add(vm); }
            });
            return result;
        }

        /// <summary>Task.WhenAll across files and across pages — most async-native strategy.</summary>
        public async Task<List<FileViewModel>> SearchInContentTask(string keyword)
        {
            var result = new List<FileViewModel>();
            var fileList = await _collection.Find(x => x.Type.Contains("pdf")).ToListAsync();

            var tasks = fileList.Select(async item =>
            {
                var sb = new StringBuilder();
                var pages = GetPagesText(item);
                var pageTasks = pages.Select((_, i) => Task.Run(() =>
                {
                    SearchPage(pages[i], i + 1, keyword, sb);
                }));
                await Task.WhenAll(pageTasks);

                var vm = ToViewModel(item, sb.ToString());
                if (Matches(vm, keyword))
                    lock (result) { result.Add(vm); }
            });
            await Task.WhenAll(tasks);
            return result;
        }

        /// <summary>
        /// Streaming search via <c>IAsyncEnumerable&lt;T&gt;</c>.
        /// Results are yielded one by one — the UI starts rendering immediately.
        /// </summary>
        public async IAsyncEnumerable<FileViewModel> SearchInContentAsyncEnum(
            string keyword,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var list = await _collection.Find(x => x.Type.Contains("pdf")).ToListAsync(ct);

            foreach (var item in list)
            {
                ct.ThrowIfCancellationRequested();
                var sb = new StringBuilder();
                var pages = GetPagesText(item);
                for (int i = 0; i < pages.Length; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    SearchPage(pages[i], i + 1, keyword, sb);
                }

                var vm = ToViewModel(item, sb.ToString());
                if (Matches(vm, keyword))
                    yield return vm;
            }
        }

        // ── Shared helpers ────────────────────────────────────────

        /// <summary>
        /// Get page texts for a file, from <see cref="PdfTextCache"/> if available,
        /// otherwise extract via PdfPig and store in cache.
        /// ⚡ Eliminates re-parsing PDFs — the single biggest search speedup.
        /// </summary>
        private string[] GetPagesText(FileModel item)
        {
            if (_textCache.TryGet(item.Id, out var cached))
                return cached;

            using var pdf = PdfDocument.Open(item.Content);
            var pages = pdf.GetPages().Select(p => p.Text).ToArray();
            _textCache.Set(item.Id, pages);
            return pages;
        }

        private static void SearchPage(string pageText, int pageNumber, string keyword, StringBuilder sb)
        {
            var r = new Regex(@"[^.!?;]*" + Regex.Escape(keyword) + @"[^.!?;]*",
                RegexOptions.IgnoreCase);
            var matches = r.Matches(pageText);
            foreach (Match m in matches)
            {
                var cleaned = Regex.Replace(m.Value, keyword,
                    $"<span class='keyword'>{keyword}</span>", RegexOptions.IgnoreCase);
                sb.Append("<p> [page ").Append(pageNumber)
                  .Append("] << ").Append(cleaned).Append(" >> </p>");
            }
        }

        private static FileViewModel ToViewModel(FileModel item, string preview) => new()
        {
            Id = item.Id,
            Content = item.Content,
            Type = item.Type,
            TextToPreview = preview,
            FileName = item.FileName
        };

        private static bool Matches(FileViewModel vm, string keyword) =>
            vm.TextToPreview.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            vm.FileName.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }
}
