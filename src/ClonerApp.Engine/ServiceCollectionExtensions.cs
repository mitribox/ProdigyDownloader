using ClonerApp.Core.Interfaces;
using ClonerApp.Engine.Crawling;
using ClonerApp.Engine.Downloading;
using ClonerApp.Engine.Filtering;
using ClonerApp.Engine.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace ClonerApp.Engine;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddClonerEngine(this IServiceCollection services)
    {
        services.AddSingleton<HtmlMediaExtractor>();
        services.AddSingleton<MediaFilter>();
        services.AddSingleton<PathBuilder>();
        services.AddSingleton<ICrawlEngine, CrawlEngine>();

        services.AddHttpClient("cloner", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "ClonerApp/1.0 (+https://localhost; media crawler)");
            client.DefaultRequestHeaders.Accept.ParseAdd("*/*");
        });

        services.AddTransient<SiteCrawler>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new SiteCrawler(
                factory.CreateClient("cloner"),
                sp.GetRequiredService<HtmlMediaExtractor>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SiteCrawler>>());
        });

        services.AddTransient<SitemapDiscoverer>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new SitemapDiscoverer(
                factory.CreateClient("cloner"),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SitemapDiscoverer>>());
        });

        services.AddTransient<MediaDownloader>();
        services.AddHostedService<ProjectSchedulerService>();

        return services;
    }
}
