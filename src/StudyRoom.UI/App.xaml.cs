using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using StudyRoom.Application.Engines;
using StudyRoom.Application.Helpers;
using StudyRoom.Application.Interfaces;
using StudyRoom.Application.Services;
using StudyRoom.Core.Interfaces;
using StudyRoom.UI.Services;
using StudyRoom.Infrastructure.Auth;
using StudyRoom.Infrastructure.Configuration;
using StudyRoom.Infrastructure.Network;
using StudyRoom.Infrastructure.OS;
using StudyRoom.Infrastructure.Security;
using StudyRoom.Infrastructure.Storage;
using StudyRoom.UI.ViewModels;
using StudyRoom.UI.Views;

namespace StudyRoom.UI;

public partial class App : System.Windows.Application
{
    private readonly ServiceProvider _serviceProvider;

    public App()
    {
        var services = new ServiceCollection();

        // Application Configuration (AWS Endpoints & Cognito)
        var appConfig = AppConfiguration.Load();
        services.AddSingleton(appConfig);

        // Core & Application Engines
        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
        services.AddSingleton<IPomodoroTimerService, LocalPomodoroTimerService>();
        services.AddSingleton<IPomodoroEngine, PomodoroEngine>();
        services.AddSingleton<IFractionalIndexingHelper, FractionalIndexingHelper>();
        services.AddSingleton<IRateLimiter>(_ => new TokenBucketRateLimiter(5, 1));

        // Dashboard Services
        services.AddSingleton<IDashboardService, MockDashboardService>();

        // Infrastructure Services
        services.AddSingleton<ISecureStorageService, DpapiSecureStorageService>();
        services.AddSingleton<IAuthService>(sp =>
        {
            var config = sp.GetRequiredService<AppConfiguration>();
            var region = Amazon.RegionEndpoint.GetBySystemName(config.CognitoRegion);
            return new CognitoAuthService(clientId: config.CognitoClientId, region: region);
        });
        services.AddSingleton<IWebSocketClient, WebSocketClient>();
        services.AddSingleton<SqliteLocalCacheRepository>();
        services.AddSingleton<ILocalCacheRepository>(sp => sp.GetRequiredService<SqliteLocalCacheRepository>());
        services.AddSingleton<IKanbanService, KanbanService>();
        services.AddSingleton<IHotKeyService, Win32HotKeyService>();
        services.AddSingleton<IMemoryOptimizer, MemoryOptimizer>();
        services.AddSingleton<IAudioService, WpfAudioService>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<KanbanBoardViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<FlashcardViewModel>();
        services.AddSingleton<FloatingBubbleViewModel>();

        // Views
        services.AddTransient<MainWindow>();
        services.AddTransient<LoginView>();
        services.AddTransient<FlashcardDialog>();
        services.AddSingleton<FloatingBubbleView>();

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
