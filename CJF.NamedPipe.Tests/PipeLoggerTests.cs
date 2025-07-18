using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using CJF.NamedPipe.Logging;

namespace CJF.NamedPipe.Tests;

/// <summary>PipeLogger 測試類別</summary>
public class PipeLoggerTests : IDisposable
{
    private readonly IHost _host;
    private readonly IPipeLoggerProvider _pipeLoggerProvider;
    private readonly ILogger<PipeLoggerTests> _logger;

    public PipeLoggerTests()
    {
        // 設置測試主機
        var hostBuilder = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging(builder =>
                {
                    builder.ClearProviders();
                    builder.AddPipeLogger();
                    builder.SetMinimumLevel(LogLevel.Trace); // 設定最低日誌級別為 Trace
                });
            });

        _host = hostBuilder.Build();
        _pipeLoggerProvider = _host.Services.GetRequiredService<IPipeLoggerProvider>();
        _logger = _host.Services.GetRequiredService<ILogger<PipeLoggerTests>>();
    }

    /// <summary>測試 PipeLogger 基本功能</summary>
    [Fact]
    public void PipeLogger_ShouldCreateLogger()
    {
        // Arrange & Act
        var logger = _pipeLoggerProvider.CreateLogger("TestCategory");

        // Assert
        Assert.NotNull(logger);
        Assert.IsType<PipeLogger>(logger);
        Assert.Equal("TestCategory", _pipeLoggerProvider.Category);
    }

    /// <summary>測試日誌級別檢查</summary>
    [Theory]
    [InlineData(LogLevel.Trace, true)]
    [InlineData(LogLevel.Debug, true)]
    [InlineData(LogLevel.Information, true)]
    [InlineData(LogLevel.Warning, true)]
    [InlineData(LogLevel.Error, true)]
    [InlineData(LogLevel.Critical, true)]
    [InlineData(LogLevel.None, false)]
    public void IsEnabled_ShouldReturnCorrectValue(LogLevel logLevel, bool expected)
    {
        // Arrange
        var logger = _pipeLoggerProvider.CreateLogger("TestCategory");

        // Act
        var result = logger.IsEnabled(logLevel);

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>測試串流處理器註冊</summary>
    [Fact]
    public async Task RegisterPipeStream_ShouldRegisterHandler()
    {
        // Arrange
        var guid = Guid.NewGuid().ToString();
        var receivedMessages = new List<StreamMessage>();

        // Act
        var result = await _pipeLoggerProvider.RegisterPipeStream(guid, message =>
        {
            receivedMessages.Add(message);
            return Task.FromResult(true);
        });

        // Assert
        Assert.True(result);
        Assert.True(_pipeLoggerProvider.Contains(guid));
    }

    /// <summary>測試重複註冊串流處理器</summary>
    [Fact]
    public async Task RegisterPipeStream_ShouldReturnFalse_WhenAlreadyRegistered()
    {
        // Arrange
        var guid = Guid.NewGuid().ToString();
        await _pipeLoggerProvider.RegisterPipeStream(guid, _ => Task.FromResult(true));

        // Act
        var result = await _pipeLoggerProvider.RegisterPipeStream(guid, _ => Task.FromResult(true));

        // Assert
        Assert.False(result);
    }

    /// <summary>測試取消註冊串流處理器</summary>
    [Fact]
    public async Task UnregisterPipeStream_ShouldRemoveHandler()
    {
        // Arrange
        var guid = Guid.NewGuid().ToString();
        await _pipeLoggerProvider.RegisterPipeStream(guid, _ => Task.FromResult(true));

        // Act
        var result = await _pipeLoggerProvider.UnregisterPipeStream(guid);

        // Assert
        Assert.True(result);
        Assert.False(_pipeLoggerProvider.Contains(guid));
    }

    /// <summary>測試取消註冊不存在的串流處理器</summary>
    [Fact]
    public async Task UnregisterPipeStream_ShouldReturnFalse_WhenNotExists()
    {
        // Arrange
        var guid = Guid.NewGuid().ToString();

        // Act
        var result = await _pipeLoggerProvider.UnregisterPipeStream(guid);

        // Assert
        Assert.False(result);
    }

    /// <summary>測試日誌訊息傳送到串流處理器</summary>
    [Fact]
    public async Task Log_ShouldSendToStreamHandlers()
    {
        // Arrange
        var guid = Guid.NewGuid().ToString();
        var receivedMessages = new List<StreamMessage>();
        
        await _pipeLoggerProvider.RegisterPipeStream(guid, message =>
        {
            receivedMessages.Add(message);
            return Task.FromResult(true);
        });

        // Act
        _logger.LogInformation("測試訊息");
        
        // 等待異步處理完成
        await Task.Delay(100);

        // Assert
        Assert.Single(receivedMessages);
        Assert.Equal("測試訊息", receivedMessages[0].Content);
        Assert.Equal(StreamMessageTypes.Info, receivedMessages[0].Type);
    }

    /// <summary>測試不同日誌級別對應到正確的串流訊息類型</summary>
    [Theory]
    [InlineData(LogLevel.Trace, StreamMessageTypes.Trace)]
    [InlineData(LogLevel.Debug, StreamMessageTypes.Debug)]
    [InlineData(LogLevel.Information, StreamMessageTypes.Info)]
    [InlineData(LogLevel.Warning, StreamMessageTypes.Warning)]
    [InlineData(LogLevel.Error, StreamMessageTypes.Error)]
    [InlineData(LogLevel.Critical, StreamMessageTypes.Error)]
    public async Task Log_ShouldMapLogLevelToStreamMessageType(LogLevel logLevel, StreamMessageTypes expectedType)
    {
        // Arrange
        var guid = Guid.NewGuid().ToString();
        var receivedMessages = new List<StreamMessage>();
        
        await _pipeLoggerProvider.RegisterPipeStream(guid, message =>
        {
            receivedMessages.Add(message);
            return Task.FromResult(true);
        });

        // Act
        _logger.Log(logLevel, "測試訊息");
        
        // 等待異步處理完成
        await Task.Delay(100);

        // Assert
        Assert.Single(receivedMessages);
        Assert.Equal(expectedType, receivedMessages[0].Type);
    }

    /// <summary>測試多個串流處理器同時接收訊息</summary>
    [Fact]
    public async Task Log_ShouldSendToMultipleHandlers()
    {
        // Arrange
        var guid1 = Guid.NewGuid().ToString();
        var guid2 = Guid.NewGuid().ToString();
        var receivedMessages1 = new List<StreamMessage>();
        var receivedMessages2 = new List<StreamMessage>();
        
        await _pipeLoggerProvider.RegisterPipeStream(guid1, message =>
        {
            receivedMessages1.Add(message);
            return Task.FromResult(true);
        });
        
        await _pipeLoggerProvider.RegisterPipeStream(guid2, message =>
        {
            receivedMessages2.Add(message);
            return Task.FromResult(true);
        });

        // Act
        _logger.LogInformation("測試訊息");
        
        // 等待異步處理完成
        await Task.Delay(100);

        // Assert
        Assert.Single(receivedMessages1);
        Assert.Single(receivedMessages2);
        Assert.Equal("測試訊息", receivedMessages1[0].Content);
        Assert.Equal("測試訊息", receivedMessages2[0].Content);
    }

    /// <summary>測試處理器返回 false 時自動移除</summary>
    [Fact]
    public async Task Log_ShouldRemoveHandler_WhenReturnsFalse()
    {
        // Arrange
        var guid = Guid.NewGuid().ToString();
        var callCount = 0;
        
        await _pipeLoggerProvider.RegisterPipeStream(guid, message =>
        {
            callCount++;
            return Task.FromResult(false); // 返回 false 表示處理失敗
        });

        // Act
        _logger.LogInformation("第一條訊息");
        await Task.Delay(100);
        
        _logger.LogInformation("第二條訊息");
        await Task.Delay(100);

        // Assert
        Assert.Equal(1, callCount); // 只應該被調用一次
        Assert.False(_pipeLoggerProvider.Contains(guid)); // 處理器應該被移除
    }

    /// <summary>測試處理器拋出例外時自動移除</summary>
    [Fact]
    public async Task Log_ShouldRemoveHandler_WhenThrowsException()
    {
        // Arrange
        var guid = Guid.NewGuid().ToString();
        var callCount = 0;
        
        await _pipeLoggerProvider.RegisterPipeStream(guid, message =>
        {
            callCount++;
            throw new InvalidOperationException("測試例外");
        });

        // Act
        _logger.LogInformation("第一條訊息");
        await Task.Delay(100);
        
        _logger.LogInformation("第二條訊息");
        await Task.Delay(100);

        // Assert
        Assert.Equal(1, callCount); // 只應該被調用一次
        Assert.False(_pipeLoggerProvider.Contains(guid)); // 處理器應該被移除
    }

    /// <summary>測試 LogLevel.None 不會發送訊息</summary>
    [Fact]
    public async Task Log_ShouldNotSend_WhenLogLevelIsNone()
    {
        // Arrange
        var guid = Guid.NewGuid().ToString();
        var receivedMessages = new List<StreamMessage>();
        
        await _pipeLoggerProvider.RegisterPipeStream(guid, message =>
        {
            receivedMessages.Add(message);
            return Task.FromResult(true);
        });

        // Act
        _logger.Log(LogLevel.None, "這條訊息不應該被發送");
        
        // 等待異步處理完成
        await Task.Delay(100);

        // Assert
        Assert.Empty(receivedMessages);
    }

    /// <summary>測試作用域功能</summary>
    [Fact]
    public void BeginScope_ShouldReturnDisposable()
    {
        // Arrange
        var logger = _pipeLoggerProvider.CreateLogger("TestCategory");

        // Act
        using var scope = logger.BeginScope("測試作用域");

        // Assert
        Assert.NotNull(scope);
    }

    public void Dispose()
    {
        _host?.Dispose();
    }
}
