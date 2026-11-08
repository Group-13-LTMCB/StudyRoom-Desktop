using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using StudyRoom.Application.Engines;
using StudyRoom.Application.Helpers;
using StudyRoom.Application.Interfaces;
using StudyRoom.Application.Services;
using StudyRoom.Core.Interfaces;
using StudyRoom.Infrastructure.Auth;
using StudyRoom.Infrastructure.Network;
using StudyRoom.Infrastructure.OS;
using StudyRoom.Infrastructure.Security;
using StudyRoom.Infrastructure.Storage;
using StudyRoom.UI.ViewModels;
using Xunit;

namespace StudyRoom.UI.Tests;

public class MemoryBenchmarkTests
{
    private sealed class TestMemoryOptimizer : IMemoryOptimizer
    {
        public int CallCount { get; private set; }
        public void OptimizeMemory() => CallCount++;
    }

    [Fact]
    public void MemoryOptimizer_PerformOptimization_ExecutesWithoutException()
    {
        // Act & Assert
        var exception = Record.Exception(() => MemoryOptimizer.PerformOptimization());
        Assert.Null(exception);
    }

    [Fact]
    public void MemoryOptimizer_ServiceInstance_ImplementsIMemoryOptimizer()
    {
        // Arrange
        IMemoryOptimizer optimizer = new MemoryOptimizer();

        // Act & Assert
        var exception = Record.Exception(() => optimizer.OptimizeMemory());
        Assert.Null(exception);
    }

    [Fact]
    public void MainWindow_WhenMinimized_CallsMemoryOptimizer()
    {
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                var testOptimizer = new TestMemoryOptimizer();
                var dashboardVm = new DashboardViewModel(new MockDashboardService());

                var window = new MainWindow(
                    dashboardVm,
                    hotKeyService: null,
                    floatingBubbleView: null,
                    memoryOptimizer: testOptimizer);

                window.Show();
                window.WindowState = WindowState.Minimized;
                Assert.Equal(1, testOptimizer.CallCount);

                window.Close();
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(threadEx);
    }

    [Fact]
    public async Task AppServicesAndLifecycle_MemoryBenchmark_RemainsUnderBudget()
    {
        // Arrange: Build full DI service provider mimicking App.xaml.cs
        var services = new ServiceCollection();

        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
        services.AddSingleton<IPomodoroTimerService, LocalPomodoroTimerService>();
        services.AddSingleton<IPomodoroEngine, PomodoroEngine>();
        services.AddSingleton<IFractionalIndexingHelper, FractionalIndexingHelper>();
        services.AddSingleton<IRateLimiter>(_ => new TokenBucketRateLimiter(5, 1));
        services.AddSingleton<IDashboardService, MockDashboardService>();
        services.AddSingleton<ISecureStorageService, DpapiSecureStorageService>();
        services.AddSingleton<IAuthService, CognitoAuthService>();
        services.AddSingleton<IWebSocketClient, WebSocketClient>();
        services.AddSingleton<SqliteLocalCacheRepository>();
        services.AddSingleton<ILocalCacheRepository>(sp => sp.GetRequiredService<SqliteLocalCacheRepository>());
        services.AddSingleton<IKanbanService, KanbanService>();
        services.AddSingleton<IHotKeyService, Win32HotKeyService>();
        services.AddSingleton<IMemoryOptimizer, MemoryOptimizer>();

        services.AddTransient<MainViewModel>();
        services.AddTransient<KanbanBoardViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<FlashcardViewModel>();
        services.AddSingleton<FloatingBubbleViewModel>();

        using var provider = services.BuildServiceProvider();

        // Act: Resolve and exercise viewmodels
        var dashboard = provider.GetRequiredService<DashboardViewModel>();
        await dashboard.LoadDataCommand.ExecuteAsync(null);

        var kanban = provider.GetRequiredService<KanbanBoardViewModel>();
        kanban.NewCardTitle = "Benchmark card";
        await kanban.SaveNewCardCommand.ExecuteAsync(null);

        var mainVm = provider.GetRequiredService<MainViewModel>();
        mainVm.StartCommand.Execute(null);

        var bubble = provider.GetRequiredService<FloatingBubbleViewModel>();
        bubble.InputText = "Hello from benchmark test";
        bubble.SendMessageCommand.Execute(null);

        // Optimize memory
        var optimizer = provider.GetRequiredService<IMemoryOptimizer>();
        optimizer.OptimizeMemory();

        // Measure managed heap memory
        long managedHeapBytes = GC.GetTotalMemory(forceFullCollection: true);
        long managedHeapMb = managedHeapBytes / (1024 * 1024);

        // Assert: Managed heap footprint after GC compaction should be well below 40MB
        Assert.True(managedHeapMb < 40, $"Expected managed heap < 40MB, but was {managedHeapMb}MB ({managedHeapBytes} bytes)");
    }
}
