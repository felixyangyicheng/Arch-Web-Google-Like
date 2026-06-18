using Google_Like_Blazor.Data;

namespace Google_Like_Blazor.Services;

/// <summary>
/// PostgreSQL-backed file repository.
/// Provides the same conceptual contract as IFileRepo but against PostgreSQL.
/// Both implementations coexist; callers pick the one they need via DI.
/// </summary>
public interface IPgFileRepo
{
    Task<bool> CreateAsync(PgFileModel obj);
    Task<bool> UpsertAsync(PgFileModel obj);
    Task<List<PgFileModel>> GetAsync();
    Task<PgFileModel?> GetAsync(string id);
    Task<PgFileModel?> GetByNameAsync(string name);
    Task RemoveAsync(string id);
    Task<bool> UpdateAsync(string id, PgFileModel obj);
    Task<List<PgFileModel>> SearchByNameAsync(string name);

    // Full-text search over PDF content extracted on-the-fly (same PdfPig pipeline)
    Task<List<FileViewModel>> SearchInContent(string keyword);
    Task<List<FileViewModel>> SearchInContentParallel(string keyword);
    Task<List<FileViewModel>> SearchInContentParallelDeep(string keyword);
    Task<List<FileViewModel>> SearchInContentTaskWhenAll(string keyword);
    IAsyncEnumerable<FileViewModel> SearchInContentAsyncEnum(string keyword);
    Task<List<FileViewModel>> SearchInFileName(string keyword);
}
