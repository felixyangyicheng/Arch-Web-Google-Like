using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Google_Like_Blazor.Data;
using Google_Like_Blazor.Utils;
using Npgsql;

namespace Google_Like_Blazor.Services;

/// <summary>
/// PostgreSQL implementation of <see cref="IPgFileRepo"/>.
/// Stores files in a <c>files</c> table and leverages PostgreSQL 18 full-text
/// search features (tsvector, ts_rank, websearch_to_tsquery) for ranked search.
/// </summary>
public class PgFileService : IPgFileRepo
{
    private readonly string _connectionString;
    private readonly ILogger<PgFileService> _logger;
    private readonly PdfTextCache _textCache;
    private const string TableName = "files";
    private const string TextSearchConfig = "french";

    public PgFileService(
        IOptions<PgConnectionModel> pgSettings,
        ILogger<PgFileService> logger,
        PdfTextCache textCache)
    {
        _connectionString = pgSettings.Value.ConnectionString;
        _logger = logger;
        _textCache = textCache;
        EnsureSchemaCreated();
    }

    // ── Schema bootstrap (PostgreSQL 18 features) ──────────────────

    private void EnsureSchemaCreated()
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        // pg_trgm extension enables GIN-accelerated ILIKE/LIKE for fast filename search
        using var extCmd = conn.CreateCommand();
        extCmd.CommandText = "CREATE EXTENSION IF NOT EXISTS pg_trgm";
        extCmd.ExecuteNonQuery();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            CREATE TABLE IF NOT EXISTS {TableName} (
                Id            TEXT PRIMARY KEY,
                FileName      TEXT NOT NULL,
                Type          TEXT NOT NULL DEFAULT 'application/pdf',
                Content       BYTEA NOT NULL,
                ExtractedText TEXT NOT NULL DEFAULT '',
                -- tsvector auto-generated from ExtractedText + FileName (PG 18 feature)
                SearchVector  TSVECTOR GENERATED ALWAYS AS
                    (to_tsvector('{TextSearchConfig}', coalesce(ExtractedText, '') || ' ' || coalesce(FileName, ''))) STORED,
                UploadedAt    TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );

            -- GIN index over the tsvector for full-text search ranking
            CREATE INDEX IF NOT EXISTS idx_files_search
                ON {TableName} USING GIN (SearchVector);

            -- pg_trgm GIN index accelerates ILIKE/contains on FileName
            CREATE INDEX IF NOT EXISTS idx_files_filename_trgm
                ON {TableName} USING GIN (FileName gin_trgm_ops);

            -- Index for recent-uploads ordering
            CREATE INDEX IF NOT EXISTS idx_files_uploaded
                ON {TableName} (UploadedAt DESC);
            """;
        cmd.ExecuteNonQuery();
    }

    // ── CRUD ──────────────────────────────────────────────────────

    public async Task<bool> CreateAsync(PgFileModel obj)
    {
        // Pre-extract PDF text at upload time → populates ExtractedText + tsvector
        var extracted = ExtractAndCacheText(obj);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO {TableName} (Id, FileName, Type, Content, ExtractedText)
            VALUES (@id, @fn, @type, @content, @extracted)
            """;
        cmd.Parameters.AddWithValue("id", obj.Id);
        cmd.Parameters.AddWithValue("fn", obj.FileName);
        cmd.Parameters.AddWithValue("type", obj.Type);
        cmd.Parameters.AddWithValue("content", obj.Content);
        cmd.Parameters.AddWithValue("extracted", extracted);
        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    /// <summary>
    /// Upsert using PostgreSQL MERGE (PG 15+). If a file with the same FileName
    /// exists, update it; otherwise insert a new one. Simpler and faster than
    /// read-then-create-or-update.
    /// </summary>
    public async Task<bool> UpsertAsync(PgFileModel obj)
    {
        var extracted = ExtractAndCacheText(obj);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            MERGE INTO {TableName} AS target
            USING (VALUES (@id, @fn, @type, @content, @extracted))
                AS source (Id, FileName, Type, Content, ExtractedText)
            ON target.FileName = source.FileName
            WHEN MATCHED THEN
                UPDATE SET Content = source.Content,
                           Type = source.Type,
                           ExtractedText = source.ExtractedText,
                           UploadedAt = NOW()
            WHEN NOT MATCHED THEN
                INSERT (Id, FileName, Type, Content, ExtractedText)
                VALUES (source.Id, source.FileName, source.Type,
                        source.Content, source.ExtractedText);
            """;
        cmd.Parameters.AddWithValue("id", obj.Id);
        cmd.Parameters.AddWithValue("fn", obj.FileName);
        cmd.Parameters.AddWithValue("type", obj.Type);
        cmd.Parameters.AddWithValue("content", obj.Content);
        cmd.Parameters.AddWithValue("extracted", extracted);
        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<List<PgFileModel>> GetAsync()
        => await QueryToListAsync($"SELECT Id, FileName, Type, Content FROM {TableName} ORDER BY UploadedAt DESC");

    public async Task<PgFileModel?> GetAsync(string id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT Id, FileName, Type, Content FROM {TableName} WHERE Id = @id";
        cmd.Parameters.AddWithValue("id", id);
        await using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? Map(reader) : null;
    }

    public async Task<PgFileModel?> GetByNameAsync(string name)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT Id, FileName, Type, Content FROM {TableName} WHERE FileName = @name";
        cmd.Parameters.AddWithValue("name", name);
        await using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? Map(reader) : null;
    }

    public async Task RemoveAsync(string id)
    {
        _textCache.Remove(id);
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM {TableName} WHERE Id = @id";
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> UpdateAsync(string id, PgFileModel obj)
    {
        _textCache.Remove(id);
        var extracted = ExtractAndCacheText(obj);
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE {TableName} SET FileName=@fn, Type=@type, Content=@content, ExtractedText=@extracted WHERE Id=@id";
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("fn", obj.FileName);
        cmd.Parameters.AddWithValue("type", obj.Type);
        cmd.Parameters.AddWithValue("content", obj.Content);
        cmd.Parameters.AddWithValue("extracted", extracted);
        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    // ── Filename search (pg_trgm accelerated) ─────────────────────

    public async Task<List<PgFileModel>> SearchByNameAsync(string name)
        => await QueryToListAsync(
            $"SELECT Id, FileName, Type, Content FROM {TableName} WHERE FileName ILIKE @name AND Type LIKE '%pdf%' ORDER BY UploadedAt DESC",
            ("name", $"%{name}%"));

    public async Task<List<FileViewModel>> SearchInFileName(string keyword)
    {
        var list = await SearchByNameAsync(keyword);
        return list.Select(x => new FileViewModel
        {
            Id = x.Id, Content = x.Content,
            Type = x.Type, TextToPreview = string.Empty, FileName = x.FileName
        }).ToList();
    }

    // ── Full-text search (PG 18 tsvector engine) ──────────────────

    /// <summary>
    /// PostgreSQL tsvector full-text search with ranking.
    /// Uses <c>websearch_to_tsquery</c> for Google-like search syntax.
    /// Much faster than fetching all files and doing C# regex.
    /// </summary>
    public async Task<List<FileViewModel>> SearchInContent(string keyword)
    {
        var raw = await FullTextSearchAsync(keyword);
        return HighlightResults(raw, keyword);
    }

    public async Task<List<FileViewModel>> SearchInContentParallel(string keyword)
    {
        // PG does the heavy lifting server-side; parallelism here is redundant but kept for API parity
        return await SearchInContent(keyword);
    }

    public async Task<List<FileViewModel>> SearchInContentParallelDeep(string keyword)
        => await SearchInContent(keyword);

    public async Task<List<FileViewModel>> SearchInContentTaskWhenAll(string keyword)
        => await SearchInContent(keyword);

    public async IAsyncEnumerable<FileViewModel> SearchInContentAsyncEnum(
        string keyword,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var raw = await FullTextSearchAsync(keyword, ct);
        foreach (var vm in HighlightResults(raw, keyword))
        {
            ct.ThrowIfCancellationRequested();
            yield return vm;
        }
    }

    // ── Private helpers ───────────────────────────────────────────

    /// <summary>
    /// Extract text from a PDF via PdfPig and store in PdfTextCache.
    /// Returns the extracted text to be stored in the ExtractedText column.
    /// </summary>
    private string ExtractAndCacheText(PgFileModel obj)
    {
        if (string.IsNullOrEmpty(obj.Type) || !obj.Type.Contains("pdf"))
            return string.Empty;

        // Already cached? Use it.
        if (_textCache.TryGet(obj.Id, out var pages))
            return string.Join("\n", pages);

        try
        {
            using var pdf = UglyToad.PdfPig.PdfDocument.Open(obj.Content);
            pages = pdf.GetPages().Select(p => p.Text).ToArray();
            _textCache.Set(obj.Id, pages);
            return string.Join("\n", pages);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract text from {FileName}", obj.FileName);
            return string.Empty;
        }
    }

    /// <summary>
    /// Execute a PG full-text search query and return raw file models.
    /// </summary>
    private async Task<List<FileViewModel>> FullTextSearchAsync(string keyword,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();

        // websearch_to_tsquery supports Google-style syntax: quotes for phrases,
        // OR for alternatives, - for exclusion.
        cmd.CommandText = $"""
            SELECT Id, FileName, Type, Content, ExtractedText,
                   ts_rank(SearchVector, websearch_to_tsquery('{TextSearchConfig}', @kw)) AS rank
            FROM {TableName}
            WHERE SearchVector @@ websearch_to_tsquery('{TextSearchConfig}', @kw)
               OR FileName ILIKE '%' || @kw || '%'
            ORDER BY rank DESC, UploadedAt DESC
            LIMIT 200
            """;
        cmd.Parameters.AddWithValue("kw", keyword);

        var result = new List<FileViewModel>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new FileViewModel
            {
                Id = reader.GetString(0),
                FileName = reader.GetString(1),
                Type = reader.GetString(2),
                Content = (byte[])reader[3],
                TextToPreview = string.Empty // populated by HighlightResults
            });
        }
        return result;
    }

    /// <summary>
    /// Apply C# regex highlighting over the pre-extracted text.
    /// The PG tsquery finds matching rows; C# regex creates the HTML snippets.
    /// </summary>
    private List<FileViewModel> HighlightResults(List<FileViewModel> items, string keyword)
    {
        foreach (var item in items)
        {
            var sb = new StringBuilder();
            string text;

            // Try PdfTextCache first, fall back to PdfPig
            if (_textCache.TryGet(item.Id, out var pages))
            {
                for (int i = 0; i < pages.Length; i++)
                    SearchPageHighlight(pages[i], i + 1, keyword, sb);
            }
            else if (item.Content is { Length: > 0 })
            {
                try
                {
                    using var pdf = UglyToad.PdfPig.PdfDocument.Open(item.Content);
                    var pgs = pdf.GetPages().Select(p => p.Text).ToArray();
                    _textCache.Set(item.Id, pgs);
                    for (int i = 0; i < pgs.Length; i++)
                        SearchPageHighlight(pgs[i], i + 1, keyword, sb);
                }
                catch { /* not a valid PDF */ }
            }

            item.TextToPreview = sb.ToString();
        }

        return items.Where(vm =>
            vm.TextToPreview.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            vm.FileName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
        ).ToList();
    }

    private static void SearchPageHighlight(string pageText, int pageNumber, string keyword, StringBuilder sb)
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

    private async Task<List<PgFileModel>> QueryToListAsync(string sql,
        params (string Name, object Value)[] parameters)
    {
        var result = new List<PgFileModel>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add(Map(reader));
        return result;
    }

    private static PgFileModel Map(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetString(0),
        FileName = reader.GetString(1),
        Type = reader.GetString(2),
        Content = (byte[])reader[3]
    };
}
