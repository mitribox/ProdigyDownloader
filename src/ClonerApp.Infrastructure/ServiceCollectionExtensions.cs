using ClonerApp.Core.Interfaces;
using ClonerApp.Infrastructure.Data;
using ClonerApp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClonerApp.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddClonerInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<ClonerDbContext>(options =>
            options.UseSqlite($"Data Source={AppPaths.DatabasePath}"));

        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IAssetRepository, AssetRepository>();
        services.AddScoped<IRunRepository, RunRepository>();

        return services;
    }

    public static async Task EnsureDatabaseCreatedAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClonerDbContext>();
        await SchemaUpgrader.UpgradeAsync(db);
    }
}
