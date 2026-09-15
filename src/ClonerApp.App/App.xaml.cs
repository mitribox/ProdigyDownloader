using System.IO;
using System.Windows;
using ClonerApp.App.ViewModels;
using ClonerApp.App.Views;
using ClonerApp.Engine;
using ClonerApp.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace ClonerApp.App;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var splash = new SplashWindow();
        splash.Show();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(AppPaths.LogsDirectory, "prodigy-downloader-.log"),
                rollingInterval: RollingInterval.Day)
            .CreateLogger();

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(services =>
            {
                services.AddClonerInfrastructure();
                services.AddClonerEngine();

                services.AddSingleton<MainViewModel>();
                services.AddTransient<WizardViewModel>();
                services.AddTransient<ProjectDetailViewModel>();
                services.AddTransient<ProjectSettingsViewModel>();

                services.AddSingleton<MainWindow>();
                services.AddTransient<WizardWindow>();
                services.AddTransient<ProjectDetailWindow>();
                services.AddTransient<ProjectSettingsWindow>();
            })
            .Build();

        await _host.Services.EnsureDatabaseCreatedAsync();
        await _host.StartAsync();

        // Hold splash briefly for a polished start experience
        await Task.Delay(1800);

        var main = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = main;
        main.Show();
        splash.Close();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }

    public static T GetService<T>() where T : class =>
        ((App)Current)._host!.Services.GetRequiredService<T>();
}
