using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using StudyRoom.Application.Engines;
using StudyRoom.Application.Helpers;
using StudyRoom.Application.Interfaces;
using StudyRoom.Application.Services;
using StudyRoom.Core.Interfaces;
using StudyRoom.Infrastructure.Security;
using StudyRoom.Infrastructure.Storage;
using StudyRoom.UI.ViewModels;

namespace StudyRoom.UI;

public partial class App : System.Windows.Application
{
    private readonly ServiceProvider _serviceProvider;

    public App()
    {
        var services = new ServiceCollection();

        // Core & Application Engines
        services.AddSingleton<IPomodoroTimerService, LocalPomodoroTimerService>();
        services.AddSingleton<IPomodoroEngine, PomodoroEngine>();
        services.AddSingleton<IFractionalIndexingHelper, FractionalIndexingHelper>();
        services.AddSingleton<IRateLimiter>(_ => new TokenBucketRateLimiter(5, 1));

        // Dashboard Services
        services.AddSingleton<IDashboardService, MockDashboardService>();

        // Infrastructure Services
        services.AddSingleton<ISecureStorageService, DpapiSecureStorageService>();
        services.AddSingleton<SqliteLocalCacheRepository>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<DashboardViewModel>();

        // Views
        services.AddTransient<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider.Dispose();
        base.OnExit(e);
    }
}
