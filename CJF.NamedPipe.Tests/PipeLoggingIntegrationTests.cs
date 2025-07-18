using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
using CJF.NamedPipe.Logging;

namespace CJF.NamedPipe.Tests;

/// <summary>PipeLogging 整合測試類別</summary>
public class PipeLoggingIntegrationTests : IDisposable
{
    private readonly IHost _host;
    private readonly IPipeLineProvider _pipeProvider;
    private readonly IPipeLoggerProvider _loggerProvider;
    private readonly ILogger<PipeLoggingIntegrationTests> _logger;
    private readonly PipeServer _server;
    private readonly PipeClient _client;

    public PipeLoggingIntegrationTests()
    {
        // 設置測試主機，同時包含 PipeLineService 和 PipeLogger
        var hostBuilder = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddPipeLineService(options =>
                {
                    // 使用較短的名稱以避免 macOS 上的路徑長度限制
                    var shortId = Guid.NewGuid().ToString("N")[..8];
                    options.CommandPipeName = $"LogCmd{shortId}";
                    options.StreamPipeName = $"LogStr{shortId}";
                }, TestCommandHandler, TestStreamHandler);
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddPipeLogger();
                logging.SetMinimumLevel(LogLevel.Trace);
            });

        _host = hostBuilder.Build();
        _pipeProvider = _host.Services.GetRequiredService<IPipeLineProvider>();
        _loggerProvider = _host.Services.GetRequiredService<IPipeLoggerProvider>();
        _logger = _host.Services.GetRequiredService<ILogger<PipeLoggingIntegrationTests>>();
        _server = _pipeProvider.CreateServer(TestCommandHandler, TestStreamHandler);
        _client = _pipeProvider.CreateClient();
    }

    /// <summary>測試日誌訊息透過命名管道傳送</summary>
    [Fact]
    public async Task Logger_ShouldSendMessagesViaPipe()
    {
        // Arrange
        var receivedLogMessages = new List<StreamMessage>();
        var logStreamGuid = "log-stream-handler";

        // 註冊日誌串流處理器
        await _loggerProvider.RegisterPipeStream(logStreamGuid, message =>
        {
            receivedLogMessages.Add(message);
            return Task.FromResult(true);
        });

        // Act
        _logger.LogTrace("追蹤訊息");
        _logger.LogDebug("除錯訊息");
        _logger.LogInformation("資訊訊息");
        _logger.LogWarning("警告訊息");
        _logger.LogError("錯誤訊息");
        _logger.LogCritical("嚴重錯誤訊息");

        // 等待異步處理完成
        await Task.Delay(200);

        // Assert
        Assert.Equal(6, receivedLogMessages.Count);
        
        Assert.Contains(receivedLogMessages, m => m.Content == "追蹤訊息" && m.Type == StreamMessageTypes.Trace);
        Assert.Contains(receivedLogMessages, m => m.Content == "除錯訊息" && m.Type == StreamMessageTypes.Debug);
        Assert.Contains(receivedLogMessages, m => m.Content == "資訊訊息" && m.Type == StreamMessageTypes.Info);
        Assert.Contains(receivedLogMessages, m => m.Content == "警告訊息" && m.Type == StreamMessageTypes.Warning);
        Assert.Contains(receivedLogMessages, m => m.Content == "錯誤訊息" && m.Type == StreamMessageTypes.Error);
        Assert.Contains(receivedLogMessages, m => m.Content == "嚴重錯誤訊息" && m.Type == StreamMessageTypes.Error);
    }

    /// <summary>測試在 PipeLineService 運行時記錄日誌</summary>
    [Fact]
    public async Task Logger_ShouldWorkWithRunningPipeService()
    {
        // Arrange
        var receivedLogMessages = new List<StreamMessage>();
        var logStreamGuid = "service-log-handler";

        await _loggerProvider.RegisterPipeStream(logStreamGuid, message =>
        {
            receivedLogMessages.Add(message);
            return Task.FromResult(true);
        });

        // 啟動 PipeLineService
        _server.Start();
        await Task.Delay(100);

        try
        {
            // Act - 在服務運行時記錄日誌
            _logger.LogInformation("服務已啟動");

            // 發送命令並記錄日誌
            var (result, message) = await _client.SendCommandAsync("test-log", "參數1");
            _logger.LogInformation($"命令執行結果: {result}, 訊息: {message}");

            // 等待異步處理完成
            await Task.Delay(200);

            // Assert
            Assert.True(receivedLogMessages.Count >= 2);
            Assert.Contains(receivedLogMessages, m => m.Content.Contains("服務已啟動"));
            Assert.Contains(receivedLogMessages, m => m.Content.Contains("命令執行結果"));
        }
        finally
        {
            await _server.StopAsync();
        }
    }

    /// <summary>測試多個日誌處理器同時工作</summary>
    [Fact]
    public async Task Logger_ShouldSupportMultipleHandlers()
    {
        // Arrange
        var consoleMessages = new List<StreamMessage>();
        var fileMessages = new List<StreamMessage>();
        var networkMessages = new List<StreamMessage>();

        var consoleGuid = "console-handler";
        var fileGuid = "file-handler";
        var networkGuid = "network-handler";

        await _loggerProvider.RegisterPipeStream(consoleGuid, message =>
        {
            consoleMessages.Add(message);
            return Task.FromResult(true);
        });

        await _loggerProvider.RegisterPipeStream(fileGuid, message =>
        {
            fileMessages.Add(message);
            return Task.FromResult(true);
        });

        await _loggerProvider.RegisterPipeStream(networkGuid, message =>
        {
            networkMessages.Add(message);
            return Task.FromResult(true);
        });

        // Act
        _logger.LogInformation("多處理器測試訊息");
        
        // 等待異步處理完成
        await Task.Delay(200);

        // Assert
        Assert.Single(consoleMessages);
        Assert.Single(fileMessages);
        Assert.Single(networkMessages);

        Assert.Equal("多處理器測試訊息", consoleMessages[0].Content);
        Assert.Equal("多處理器測試訊息", fileMessages[0].Content);
        Assert.Equal("多處理器測試訊息", networkMessages[0].Content);
    }

    /// <summary>測試日誌處理器的動態註冊和取消註冊</summary>
    [Fact]
    public async Task Logger_ShouldSupportDynamicHandlerManagement()
    {
        // Arrange
        var messages1 = new List<StreamMessage>();
        var messages2 = new List<StreamMessage>();
        var guid1 = "dynamic-handler-1";
        var guid2 = "dynamic-handler-2";

        // 註冊第一個處理器
        await _loggerProvider.RegisterPipeStream(guid1, message =>
        {
            messages1.Add(message);
            return Task.FromResult(true);
        });

        // Act 1: 只有第一個處理器
        _logger.LogInformation("第一階段訊息");
        await Task.Delay(100);

        // 註冊第二個處理器
        await _loggerProvider.RegisterPipeStream(guid2, message =>
        {
            messages2.Add(message);
            return Task.FromResult(true);
        });

        // Act 2: 兩個處理器都存在
        _logger.LogInformation("第二階段訊息");
        await Task.Delay(100);

        // 取消註冊第一個處理器
        await _loggerProvider.UnregisterPipeStream(guid1);

        // Act 3: 只有第二個處理器
        _logger.LogInformation("第三階段訊息");
        await Task.Delay(100);

        // Assert
        Assert.Equal(2, messages1.Count); // 第一和第二階段
        Assert.Equal(2, messages2.Count); // 第二和第三階段

        Assert.Equal("第一階段訊息", messages1[0].Content);
        Assert.Equal("第二階段訊息", messages1[1].Content);
        Assert.Equal("第二階段訊息", messages2[0].Content);
        Assert.Equal("第三階段訊息", messages2[1].Content);
    }

    /// <summary>測試異常處理和日誌記錄</summary>
    [Fact]
    public async Task Logger_ShouldHandleExceptionsCorrectly()
    {
        // Arrange
        var receivedLogMessages = new List<StreamMessage>();
        var logStreamGuid = "exception-log-handler";

        await _loggerProvider.RegisterPipeStream(logStreamGuid, message =>
        {
            receivedLogMessages.Add(message);
            return Task.FromResult(true);
        });

        // Act
        try
        {
            _logger.LogInformation("準備拋出例外");
            throw new InvalidOperationException("測試例外訊息");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "捕獲到例外: {ExceptionMessage}", ex.Message);
        }

        // 等待異步處理完成
        await Task.Delay(200);

        // Assert
        Assert.Equal(2, receivedLogMessages.Count);
        Assert.Contains(receivedLogMessages, m => m.Content.Contains("準備拋出例外"));
        Assert.Contains(receivedLogMessages, m => m.Content.Contains("捕獲到例外") && m.Type == StreamMessageTypes.Error);
    }

    /// <summary>測試高頻率日誌記錄的效能</summary>
    [Fact]
    public async Task Logger_ShouldHandleHighFrequencyLogging()
    {
        // Arrange
        var receivedLogMessages = new List<StreamMessage>();
        var logStreamGuid = "performance-handler";
        var messageCount = 100;

        await _loggerProvider.RegisterPipeStream(logStreamGuid, message =>
        {
            lock (receivedLogMessages)
            {
                receivedLogMessages.Add(message);
            }
            return Task.FromResult(true);
        });

        // Act
        var tasks = new List<Task>();
        for (int i = 0; i < messageCount; i++)
        {
            var messageIndex = i;
            tasks.Add(Task.Run(() => _logger.LogInformation($"高頻率訊息 #{messageIndex}")));
        }

        await Task.WhenAll(tasks);
        
        // 等待所有異步處理完成
        await Task.Delay(500);

        // Assert
        Assert.Equal(messageCount, receivedLogMessages.Count);
        
        // 驗證所有訊息都被正確接收
        for (int i = 0; i < messageCount; i++)
        {
            Assert.Contains(receivedLogMessages, m => m.Content.Contains($"高頻率訊息 #{i}"));
        }
    }

    /// <summary>測試命令處理器</summary>
    private static async Task<string> TestCommandHandler(string command, string[] args)
    {
        await Task.Delay(10); // 模擬處理時間
        return $"處理命令: {command}, 參數: {string.Join(", ", args)}";
    }

    /// <summary>測試串流命令處理器</summary>
    private static async Task TestStreamHandler(string command, string[] args, Func<StreamMessage, Task<bool>> streamWriter, CancellationToken cancellationToken)
    {
        await streamWriter(new StreamMessage($"開始處理串流命令: {command}", StreamMessageTypes.Info));
        await Task.Delay(50, cancellationToken);
        await streamWriter(new StreamMessage($"完成處理串流命令: {command}", StreamMessageTypes.Success, true));
    }

    public void Dispose()
    {
        _server?.StopAsync().Wait();
        _host?.Dispose();
    }
}
