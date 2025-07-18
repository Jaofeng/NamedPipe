using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace CJF.NamedPipe.Tests;

/// <summary>PipeLineProvider 測試類別</summary>
public class PipeLineProviderTests
{
    /// <summary>測試 PipeLineProvider 的基本功能</summary>
    [Fact]
    public void PipeLineProvider_ShouldCreateClientAndServer()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<PipeLineOptions>(options =>
        {
            options.CommandPipeName = "Test.Command.Pipe";
            options.StreamPipeName = "Test.Stream.Pipe";
        });
        services.AddSingleton<IPipeLineProvider, PipeLineProvider>();

        var serviceProvider = services.BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<IPipeLineProvider>();

        // Act
        var client = provider.CreateClient();
        var server = provider.CreateServer(TestCommandHandler);

        // Assert
        Assert.NotNull(client);
        Assert.NotNull(server);
        Assert.Equal("Test.Command.Pipe", provider.Options.CommandPipeName);
        Assert.Equal("Test.Stream.Pipe", provider.Options.StreamPipeName);
    }

    /// <summary>測試服務是否正在執行的檢查</summary>
    [Fact]
    public void IsServiceRunning_ShouldReturnFalse_WhenServiceNotRunning()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<PipeLineOptions>(options => { });
        services.AddSingleton<IPipeLineProvider, PipeLineProvider>();

        var serviceProvider = services.BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<IPipeLineProvider>();

        // Act
        var isRunning = provider.IsServiceRunning();

        // Assert
        Assert.False(isRunning);
    }

    /// <summary>測試命令處理器</summary>
    private static async Task<string> TestCommandHandler(string command, string[] args)
    {
        return await Task.FromResult($"處理命令: {command}, 參數: {string.Join(", ", args)}");
    }
}
