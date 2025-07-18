using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CJF.NamedPipe.Tests;

/// <summary>PipeLineService 測試類別</summary>
public class PipeLineServiceTests
{
    /// <summary>測試 PipeLineService 的 DI 註冊</summary>
    [Fact]
    public void AddPipeLineService_ShouldRegisterServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddPipeLineService(TestCommandHandler);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var provider = serviceProvider.GetService<IPipeLineProvider>();
        var commandHandler = serviceProvider.GetService<PipeCommandHandler>();
        var hostedService = serviceProvider.GetServices<IHostedService>()
            .OfType<PipeLineService>()
            .FirstOrDefault();

        Assert.NotNull(provider);
        Assert.NotNull(commandHandler);
        Assert.NotNull(hostedService);
    }

    /// <summary>測試 PipeLineService 的配置選項註冊</summary>
    [Fact]
    public void AddPipeLineService_WithOptions_ShouldConfigureOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddPipeLineService(options =>
        {
            options.CommandPipeName = "Custom.Command.Pipe";
            options.StreamPipeName = "Custom.Stream.Pipe";
            options.MaxClients = 10;
        }, TestCommandHandler);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<IPipeLineProvider>();

        Assert.Equal("Custom.Command.Pipe", provider.Options.CommandPipeName);
        Assert.Equal("Custom.Stream.Pipe", provider.Options.StreamPipeName);
        Assert.Equal(10, provider.Options.MaxClients);
    }

    /// <summary>測試 HostBuilder 擴展方法</summary>
    [Fact]
    public void UsePipeLineService_ShouldConfigureHost()
    {
        // Arrange
        var hostBuilder = Host.CreateDefaultBuilder();

        // Act
        hostBuilder.UsePipeLineService(options =>
        {
            options.CommandPipeName = "Host.Command.Pipe";
            options.StreamPipeName = "Host.Stream.Pipe";
        });

        // Assert
        var host = hostBuilder.Build();
        var provider = host.Services.GetService<IPipeLineProvider>();

        Assert.NotNull(provider);
        Assert.Equal("Host.Command.Pipe", provider.Options.CommandPipeName);
        Assert.Equal("Host.Stream.Pipe", provider.Options.StreamPipeName);
    }

    /// <summary>測試命令處理器</summary>
    private static async Task<string> TestCommandHandler(string command, string[] args)
    {
        return await Task.FromResult($"處理命令: {command}, 參數: {string.Join(", ", args)}");
    }

    /// <summary>測試串流命令處理器</summary>
    private static async Task TestStreamHandler(string command, string[] args, Func<StreamMessage, Task<bool>> streamWriter, CancellationToken cancellationToken)
    {
        await streamWriter(new StreamMessage($"開始處理串流命令: {command}", StreamMessageTypes.Info));
        await Task.Delay(100, cancellationToken);
        await streamWriter(new StreamMessage($"完成處理串流命令: {command}", StreamMessageTypes.Success, true));
    }
}
