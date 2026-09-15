using Microsoft.EntityFrameworkCore;

namespace ClonerApp.Infrastructure.Data;

public static class SchemaUpgrader
{
    public static async Task UpgradeAsync(ClonerDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);

        var existing = await GetColumnsAsync(db, "Projects", cancellationToken);

        await AddColumnIfMissingAsync(db, existing, "Projects", "UrlInputMode", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await AddColumnIfMissingAsync(db, existing, "Projects", "UrlPrefix", "TEXT NULL", cancellationToken);
        await AddColumnIfMissingAsync(db, existing, "Projects", "PageFrom", "INTEGER NULL", cancellationToken);
        await AddColumnIfMissingAsync(db, existing, "Projects", "PageTo", "INTEGER NULL", cancellationToken);
        await AddColumnIfMissingAsync(db, existing, "Projects", "UrlRegex", "TEXT NULL", cancellationToken);
        await AddColumnIfMissingAsync(db, existing, "Projects", "ScanWithinStartingFolder", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await AddColumnIfMissingAsync(db, existing, "Projects", "IgnoreHomePage", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await AddColumnIfMissingAsync(db, existing, "Projects", "AlwaysScanImageLinks", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
    }

    private static async Task<HashSet<string>> GetColumnsAsync(
        ClonerDbContext db,
        string table,
        CancellationToken cancellationToken)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        if (command.Connection!.State != System.Data.ConnectionState.Open)
            await command.Connection.OpenAsync(cancellationToken);

        command.CommandText = $"PRAGMA table_info('{table}');";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    private static async Task AddColumnIfMissingAsync(
        ClonerDbContext db,
        HashSet<string> existing,
        string table,
        string column,
        string sqlType,
        CancellationToken cancellationToken)
    {
        if (existing.Contains(column))
            return;

        await db.Database.ExecuteSqlRawAsync(
            string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "ALTER TABLE {0} ADD COLUMN {1} {2};", table, column, sqlType),
            cancellationToken);
        existing.Add(column);
    }
}
