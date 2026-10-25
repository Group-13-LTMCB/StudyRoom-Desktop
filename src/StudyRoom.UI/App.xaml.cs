using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using StudyRoom.Application.Interfaces;
using StudyRoom.Application.Services;
using StudyRoom.UI.ViewModels;

namespace StudyRoom.UI;

public partial class App : System.Windows.Application
{
    private readonly ServiceProvider _serviceProvider;

    public App()
    {
        var services = new ServiceCollection();

        // Application Services
        services.AddSingleton<IPomodoroTimerService, LocalPomodoroTimerService>();

        // ViewModels
        services.AddTransient<MainViewModel>();

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
