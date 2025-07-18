using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using CJF.NamedPipe.Logging;

namespace CJF.NamedPipe.Tests;

/// <summary>PipeLoggerProvider 測試類別</summary>
public class PipeLoggerProviderTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IPipeLoggerProvider _provider;

    public PipeLoggerProviderTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<PipeLoggerOptions>(options => { });
        services.AddSingleton<IPipeLoggerProvider, PipeLoggerProvider>();

        _serviceProvider = services.BuildServiceProvider();
        _provider = _serviceProvider.GetRequiredService<IPipeLoggerProvider>();
    }

    /// <summary>測試 PipeLoggerProvider 基本建立</summary>
    [Fact]
    public void PipeLoggerProvider_ShouldCreateSuccessfully()
    {
        // Assert
        Assert.NotNull(_provider);
        Assert.NotNull(_provider.Options);
        Assert.IsType<PipeLoggerProvider>(_provider);
    }

    /// <summary>測試建立 Logger</summary>
    [Fact]
    public void CreateLogger_ShouldReturnPipeLogger()
    {
        // Arrange
        var categoryName = "TestCategory";

        // Act
        var logger = _provider.CreateLogger(categoryName);

        // Assert
        Assert.NotNull(logger);
        Assert.IsType<PipeLogger>(logger);
        Assert.Equal(categoryName, _provider.Category);
    }

    /// <summary>測試建立多個不同分類的 Logger</summary>
    [Fact]
    public void CreateLogger_ShouldUpdateCategory()
    {
        // Arrange
        var category1 = "Category1";
        var category2 = "Category2";

        // Act
        var logger1 = _provider.CreateLogger(category1);
        var logger2 = _provider.CreateLogger(category2);

        // Assert
        Assert.NotNull(logger1);
        Assert.NotNull(logger2);
        Assert.Equal(category2, _provider.Category); // 應該是最後設定的分類
    }

    /// <summary>測試註冊串流處理器</summary>
    [Fact]
    public async Task RegisterPipeStream_ShouldSucceed()
    {
        // Arrange
        var guid = "test-handler-1";

        // Act
        var result = await _provider.RegisterPipeStream(guid, message =>
        {
            return Task.FromResult(true);
        });

        // Assert
        Assert.True(result);
        Assert.True(_provider.Contains(guid));
    }

    /// <summary>測試重複註冊相同 GUID 的處理器</summary>
    [Fact]
    public async Task RegisterPipeStream_ShouldFailForDuplicateGuid()
    {
        // Arrange
        var guid = "duplicate-handler";
        await _provider.RegisterPipeStream(guid, _ => Task.FromResult(true));

        // Act
        var result = await _provider.RegisterPipeStream(guid, _ => Task.FromResult(true));

        // Assert
        Assert.False(result);
        Assert.True(_provider.Contains(guid)); // 原來的處理器仍然存在
    }

    /// <summary>測試取消註冊處理器</summary>
    [Fact]
    public async Task UnregisterPipeStream_ShouldSucceed()
    {
        // Arrange
        var guid = "handler-to-remove";
        await _provider.RegisterPipeStream(guid, _ => Task.FromResult(true));

        // Act
        var result = await _provider.UnregisterPipeStream(guid);

        // Assert
        Assert.True(result);
        Assert.False(_provider.Contains(guid));
    }

    /// <summary>測試取消註冊不存在的處理器</summary>
    [Fact]
    public async Task UnregisterPipeStream_ShouldFailForNonExistentGuid()
    {
        // Arrange
        var guid = "non-existent-handler";

        // Act
        var result = await _provider.UnregisterPipeStream(guid);

        // Assert
        Assert.False(result);
    }

    /// <summary>測試 Contains 方法</summary>
    [Fact]
    public async Task Contains_ShouldReturnCorrectStatus()
    {
        // Arrange
        var existingGuid = "existing-handler";
        var nonExistingGuid = "non-existing-handler";
        
        await _provider.RegisterPipeStream(existingGuid, _ => Task.FromResult(true));

        // Act & Assert
        Assert.True(_provider.Contains(existingGuid));
        Assert.False(_provider.Contains(nonExistingGuid));
    }

    /// <summary>測試發送日誌條目到處理器</summary>
    [Fact]
    public async Task SendLogEntry_ShouldCallRegisteredHandlers()
    {
        // Arrange
        var guid1 = "handler-1";
        var guid2 = "handler-2";
        var receivedMessages1 = new List<StreamMessage>();
        var receivedMessages2 = new List<StreamMessage>();

        await _provider.RegisterPipeStream(guid1, message =>
        {
            receivedMessages1.Add(message);
            return Task.FromResult(true);
        });

        await _provider.RegisterPipeStream(guid2, message =>
        {
            receivedMessages2.Add(message);
            return Task.FromResult(true);
        });

        var logEntry = new LogEntry
        {
            Timestamp = DateTime.Now,
            LogLevel = LogLevel.Information,
            Category = "TestCategory",
            Message = "測試訊息",
            Exception = null
        };

        // Act
        _provider.SendLogEntry(logEntry);
        
        // 等待異步處理完成
        await Task.Delay(100);

        // Assert
        Assert.Single(receivedMessages1);
        Assert.Single(receivedMessages2);
        Assert.Equal("測試訊息", receivedMessages1[0].Content);
        Assert.Equal("測試訊息", receivedMessages2[0].Content);
        Assert.Equal(StreamMessageTypes.Info, receivedMessages1[0].Type);
        Assert.Equal(StreamMessageTypes.Info, receivedMessages2[0].Type);
    }

    /// <summary>測試不同日誌級別對應到正確的串流訊息類型</summary>
    [Theory]
    [InlineData(LogLevel.Trace, StreamMessageTypes.Trace)]
    [InlineData(LogLevel.Debug, StreamMessageTypes.Debug)]
    [InlineData(LogLevel.Information, StreamMessageTypes.Info)]
    [InlineData(LogLevel.Warning, StreamMessageTypes.Warning)]
    [InlineData(LogLevel.Error, StreamMessageTypes.Error)]
    [InlineData(LogLevel.Critical, StreamMessageTypes.Error)]
    public async Task SendLogEntry_ShouldMapLogLevelCorrectly(LogLevel logLevel, StreamMessageTypes expectedType)
    {
        // Arrange
        var guid = "level-test-handler";
        var receivedMessages = new List<StreamMessage>();

        await _provider.RegisterPipeStream(guid, message =>
        {
            receivedMessages.Add(message);
            return Task.FromResult(true);
        });

        var logEntry = new LogEntry
        {
            Timestamp = DateTime.Now,
            LogLevel = logLevel,
            Category = "TestCategory",
            Message = "測試訊息"
        };

        // Act
        _provider.SendLogEntry(logEntry);
        
        // 等待異步處理完成
        await Task.Delay(100);

        // Assert
        Assert.Single(receivedMessages);
        Assert.Equal(expectedType, receivedMessages[0].Type);
    }

    /// <summary>測試處理器返回 false 時自動移除</summary>
    [Fact]
    public async Task SendLogEntry_ShouldRemoveHandlerWhenReturnsFalse()
    {
        // Arrange
        var guid = "failing-handler";
        var callCount = 0;

        await _provider.RegisterPipeStream(guid, message =>
        {
            callCount++;
            return Task.FromResult(false); // 返回 false 表示處理失敗
        });

        var logEntry = new LogEntry
        {
            Timestamp = DateTime.Now,
            LogLevel = LogLevel.Information,
            Category = "TestCategory",
            Message = "第一條訊息"
        };

        // Act
        _provider.SendLogEntry(logEntry);
        await Task.Delay(100);

        // 發送第二條訊息
        logEntry.Message = "第二條訊息";
        _provider.SendLogEntry(logEntry);
        await Task.Delay(100);

        // Assert
        Assert.Equal(1, callCount); // 只應該被調用一次
        Assert.False(_provider.Contains(guid)); // 處理器應該被移除
    }

    /// <summary>測試處理器拋出例外時自動移除</summary>
    [Fact]
    public async Task SendLogEntry_ShouldRemoveHandlerWhenThrowsException()
    {
        // Arrange
        var guid = "exception-handler";
        var callCount = 0;

        await _provider.RegisterPipeStream(guid, message =>
        {
            callCount++;
            throw new InvalidOperationException("測試例外");
        });

        var logEntry = new LogEntry
        {
            Timestamp = DateTime.Now,
            LogLevel = LogLevel.Information,
            Category = "TestCategory",
            Message = "第一條訊息"
        };

        // Act
        _provider.SendLogEntry(logEntry);
        await Task.Delay(100);

        // 發送第二條訊息
        logEntry.Message = "第二條訊息";
        _provider.SendLogEntry(logEntry);
        await Task.Delay(100);

        // Assert
        Assert.Equal(1, callCount); // 只應該被調用一次
        Assert.False(_provider.Contains(guid)); // 處理器應該被移除
    }

    /// <summary>測試沒有註冊處理器時發送日誌條目</summary>
    [Fact]
    public void SendLogEntry_ShouldNotThrowWhenNoHandlers()
    {
        // Arrange
        var logEntry = new LogEntry
        {
            Timestamp = DateTime.Now,
            LogLevel = LogLevel.Information,
            Category = "TestCategory",
            Message = "測試訊息"
        };

        // Act & Assert - 不應該拋出例外
        _provider.SendLogEntry(logEntry);
    }

    /// <summary>測試 Dispose 後不會處理日誌條目</summary>
    [Fact]
    public async Task SendLogEntry_ShouldNotProcessAfterDispose()
    {
        // Arrange
        var guid = "dispose-test-handler";
        var receivedMessages = new List<StreamMessage>();

        await _provider.RegisterPipeStream(guid, message =>
        {
            receivedMessages.Add(message);
            return Task.FromResult(true);
        });

        var logEntry = new LogEntry
        {
            Timestamp = DateTime.Now,
            LogLevel = LogLevel.Information,
            Category = "TestCategory",
            Message = "測試訊息"
        };

        // Act
        _provider.Dispose();
        _provider.SendLogEntry(logEntry);
        
        // 等待異步處理完成
        await Task.Delay(100);

        // Assert
        Assert.Empty(receivedMessages); // 不應該收到任何訊息
    }

    /// <summary>測試多個處理器中部分失敗的情況</summary>
    [Fact]
    public async Task SendLogEntry_ShouldHandlePartialFailures()
    {
        // Arrange
        var successGuid = "success-handler";
        var failGuid = "fail-handler";
        var exceptionGuid = "exception-handler";
        
        var successMessages = new List<StreamMessage>();

        await _provider.RegisterPipeStream(successGuid, message =>
        {
            successMessages.Add(message);
            return Task.FromResult(true);
        });

        await _provider.RegisterPipeStream(failGuid, message =>
        {
            return Task.FromResult(false); // 返回失敗
        });

        await _provider.RegisterPipeStream(exceptionGuid, message =>
        {
            throw new InvalidOperationException("測試例外");
        });

        var logEntry = new LogEntry
        {
            Timestamp = DateTime.Now,
            LogLevel = LogLevel.Information,
            Category = "TestCategory",
            Message = "測試訊息"
        };

        // Act
        _provider.SendLogEntry(logEntry);
        await Task.Delay(100);

        // Assert
        Assert.Single(successMessages); // 成功的處理器應該收到訊息
        Assert.True(_provider.Contains(successGuid)); // 成功的處理器仍然存在
        Assert.False(_provider.Contains(failGuid)); // 失敗的處理器被移除
        Assert.False(_provider.Contains(exceptionGuid)); // 拋出例外的處理器被移除
    }

    public void Dispose()
    {
        _provider?.Dispose();
        (_serviceProvider as IDisposable)?.Dispose();
    }
}
