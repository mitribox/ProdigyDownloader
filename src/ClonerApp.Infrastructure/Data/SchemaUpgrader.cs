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
        await AddColumnIfMissingAsync(db, existing, "Projects", "ExcludeRulesJson", "TEXT NULL", cancellationToken);
        await AddColumnIfMissingAsync(db, existing, "Projects", "CrawlEntireSite", "INTEGER NOT NULL DEFAULT 0", cancellationToken);

        await EnsureCrawledPagesTableAsync(db, cancellationToken);
    }

    private static async Task EnsureCrawledPagesTableAsync(ClonerDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "CrawledPages" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_CrawledPages" PRIMARY KEY,
                "ProjectId" TEXT NOT NULL,
                "Url" TEXT NOT NULL,
                "ETag" TEXT NULL,
                "LastModified" TEXT NULL,
                "ContentHash" TEXT NULL,
                "LastSeenAtUtc" TEXT NOT NULL,
                CONSTRAINT "FK_CrawledPages_Projects_ProjectId" FOREIGN KEY ("ProjectId") REFERENCES "Projects" ("Id") ON DELETE CASCADE
            );
            """,
            cancellationToken);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_CrawledPages_ProjectId_Url"
            ON "CrawledPages" ("ProjectId", "Url");
            """,
            cancellationToken);
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
