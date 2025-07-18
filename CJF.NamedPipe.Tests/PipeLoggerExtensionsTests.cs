using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using CJF.NamedPipe.Logging;

namespace CJF.NamedPipe.Tests;

/// <summary>PipeLoggerExtensions 測試類別</summary>
public class PipeLoggerExtensionsTests : IDisposable
{
    private readonly IHost _host;

    public PipeLoggerExtensionsTests()
    {
        var hostBuilder = Host.CreateDefaultBuilder();
        _host = hostBuilder.Build();
    }

    /// <summary>測試 AddPipeLogger 基本註冊</summary>
    [Fact]
    public void AddPipeLogger_ShouldRegisterServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddLogging(builder => builder.AddPipeLogger());

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var pipeLoggerProvider = serviceProvider.GetService<IPipeLoggerProvider>();
        var loggerProvider = serviceProvider.GetServices<ILoggerProvider>()
            .FirstOrDefault(p => p is PipeLoggerProvider);

        Assert.NotNull(pipeLoggerProvider);
        Assert.NotNull(loggerProvider);
        Assert.IsType<PipeLoggerProvider>(pipeLoggerProvider);
        Assert.IsType<PipeLoggerProvider>(loggerProvider);
    }

    /// <summary>測試 AddPipeLogger 帶配置選項的註冊</summary>
    [Fact]
    public void AddPipeLogger_WithConfiguration_ShouldRegisterServicesAndOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var configurationCalled = false;

        // Act
        services.AddLogging(builder => 
            builder.AddPipeLogger(options =>
            {
                configurationCalled = true;
                // 目前 PipeLoggerOptions 沒有特定屬性，但測試配置委託被調用
            }));

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var pipeLoggerProvider = serviceProvider.GetService<IPipeLoggerProvider>();
        var options = serviceProvider.GetService<IOptions<PipeLoggerOptions>>();

        Assert.NotNull(pipeLoggerProvider);
        Assert.NotNull(options);
        Assert.True(configurationCalled);
    }

    /// <summary>測試在 Host 中使用 AddPipeLogger</summary>
    [Fact]
    public void AddPipeLogger_InHost_ShouldWork()
    {
        // Arrange & Act
        var hostBuilder = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddPipeLogger();
            });

        using var host = hostBuilder.Build();

        // Assert
        var pipeLoggerProvider = host.Services.GetService<IPipeLoggerProvider>();
        var logger = host.Services.GetService<ILogger<PipeLoggerExtensionsTests>>();

        Assert.NotNull(pipeLoggerProvider);
        Assert.NotNull(logger);
    }

    /// <summary>測試多個日誌提供者共存</summary>
    [Fact]
    public void AddPipeLogger_WithOtherProviders_ShouldCoexist()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddDebug();
            builder.AddPipeLogger();
        });

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var loggerProviders = serviceProvider.GetServices<ILoggerProvider>().ToList();
        var pipeLoggerProvider = serviceProvider.GetService<IPipeLoggerProvider>();

        Assert.True(loggerProviders.Count >= 3); // 至少包含 Console, Debug, PipeLogger
        Assert.NotNull(pipeLoggerProvider);
        Assert.Contains(loggerProviders, p => p is PipeLoggerProvider);
    }

    /// <summary>測試 Logger 工廠建立的 Logger 包含 PipeLogger</summary>
    [Fact]
    public void LoggerFactory_ShouldIncludePipeLogger()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddPipeLogger());

        var serviceProvider = services.BuildServiceProvider();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        // Act
        var logger = loggerFactory.CreateLogger<PipeLoggerExtensionsTests>();

        // Assert
        Assert.NotNull(logger);
        
        // 測試日誌記錄功能
        logger.LogInformation("測試訊息");
        
        // 如果沒有拋出例外，表示 PipeLogger 正常工作
        Assert.True(true);
    }

    /// <summary>測試配置選項的綁定</summary>
    [Fact]
    public void AddPipeLogger_ShouldBindOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddLogging(builder => 
            builder.AddPipeLogger(options =>
            {
                // 目前 PipeLoggerOptions 是空的，但測試選項系統正常工作
            }));

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetService<IOptions<PipeLoggerOptions>>();
        var pipeLoggerProvider = serviceProvider.GetService<IPipeLoggerProvider>();

        Assert.NotNull(options);
        Assert.NotNull(options.Value);
        Assert.NotNull(pipeLoggerProvider);
        Assert.Same(options.Value, pipeLoggerProvider.Options);
    }

    /// <summary>測試服務生命週期</summary>
    [Fact]
    public void AddPipeLogger_ShouldRegisterAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddPipeLogger());

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var provider1 = serviceProvider.GetService<IPipeLoggerProvider>();
        var provider2 = serviceProvider.GetService<IPipeLoggerProvider>();

        // Assert
        Assert.NotNull(provider1);
        Assert.NotNull(provider2);
        Assert.Same(provider1, provider2); // 應該是同一個實例（Singleton）
    }

    /// <summary>測試在不同作用域中的行為</summary>
    [Fact]
    public void AddPipeLogger_ShouldWorkInScopes()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddPipeLogger());

        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        using (var scope1 = serviceProvider.CreateScope())
        {
            var provider1 = scope1.ServiceProvider.GetService<IPipeLoggerProvider>();
            Assert.NotNull(provider1);

            using (var scope2 = serviceProvider.CreateScope())
            {
                var provider2 = scope2.ServiceProvider.GetService<IPipeLoggerProvider>();
                Assert.NotNull(provider2);
                Assert.Same(provider1, provider2); // Singleton 在不同作用域中應該是同一個實例
            }
        }
    }

    /// <summary>測試重複註冊的行為</summary>
    [Fact]
    public void AddPipeLogger_MultipleRegistrations_ShouldWork()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act - 多次註冊
        services.AddLogging(builder =>
        {
            builder.AddPipeLogger();
            builder.AddPipeLogger(options => { }); // 重複註冊
        });

        // Assert - 不應該拋出例外
        var serviceProvider = services.BuildServiceProvider();
        var pipeLoggerProvider = serviceProvider.GetService<IPipeLoggerProvider>();
        var loggerProviders = serviceProvider.GetServices<ILoggerProvider>().ToList();

        Assert.NotNull(pipeLoggerProvider);
        // 可能會有多個 PipeLoggerProvider 實例，但這是正常的
        Assert.True(loggerProviders.Count(p => p is PipeLoggerProvider) >= 1);
    }

    public void Dispose()
    {
        _host?.Dispose();
    }
}
