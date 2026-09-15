using ClonerApp.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClonerApp.Engine;

public sealed class ProjectSchedulerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICrawlEngine _engine;
    private readonly ILogger<ProjectSchedulerService> _logger;

    public ProjectSchedulerService(
        IServiceScopeFactory scopeFactory,
        ICrawlEngine engine,
        ILogger<ProjectSchedulerService> logger)
    {
        _scopeFactory = scopeFactory;
        _engine = engine;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var projects = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
                var due = await projects.GetDueProjectsAsync(DateTime.UtcNow, stoppingToken);

                foreach (var project in due)
                {
                    if (_engine.IsRunning(project.Id))
                        continue;

                    _logger.LogInformation("Starting scheduled run for {Project}", project.Name);
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _engine.RunProjectAsync(project.Id, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Scheduled run failed for {ProjectId}", project.Id);
                        }
                    }, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduler loop error");
            }
        }
    }
}
