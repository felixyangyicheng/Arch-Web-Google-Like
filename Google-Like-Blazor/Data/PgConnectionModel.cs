namespace Google_Like_Blazor.Data;

/// <summary>
/// Configuration POCO for the PostgreSQL connection, mirrored from <c>PostgresDatabase</c> section in appsettings.json.
/// </summary>
public class PgConnectionModel
{
    public string ConnectionString { get; set; } = null!;
    public string DatabaseName { get; set; } = null!;
    public string TableName { get; set; } = "files";
}
