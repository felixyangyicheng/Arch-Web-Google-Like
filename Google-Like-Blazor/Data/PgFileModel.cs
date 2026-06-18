namespace Google_Like_Blazor.Data;

/// <summary>
/// PostgreSQL entity for file storage.
/// Mirrors FileModel but targets a relational table instead of a MongoDB document.
/// </summary>
public class PgFileModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FileName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
}
