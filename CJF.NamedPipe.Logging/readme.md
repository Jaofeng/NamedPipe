# CJF.NamedPipe.Logging

[![NuGet version](https://badge.fury.io/nu/CJF.NamedPipe.Logging.svg)](https://badge.fury.io/nu/CJF.NamedPipe.Logging)


一個基於 Microsoft.Extensions.Logging 的命名管道日誌記錄提供者，可將日誌訊息透過命名管道串流傳送到其他應用程式。

## 功能特色

- ✅ **Microsoft.Extensions.Logging 整合**: 完全相容 .NET 標準日誌記錄框架
- ✅ **命名管道串流**: 透過命名管道將日誌訊息即時傳送
- ✅ **多重處理器支援**: 支援註冊多個串流處理器
- ✅ **依賴注入支援**: 完全整合 Microsoft.Extensions.DependencyInjection
- ✅ **配置選項**: 支援 IOptions 模式進行配置
- ✅ **異步處理**: 支援異步日誌處理和串流傳送
- ✅ **自動清理**: 自動移除失效的串流處理器

## 快速開始

### 1. 安裝套件

```bash
dotnet add package CJF.NamedPipe.Logging
```

### 2. 註冊日誌提供者

```csharp
using CJF.NamedPipe.Logging;
using Microsoft.Extensions.Logging;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureLogging(logging =>
{
    // 基本註冊
    logging.AddPipeLogger();
    
    // 或者使用配置選項
    logging.AddPipeLogger(options =>
    {
        // 在此處配置選項（目前版本暫無特定選項）
    });
});

var host = builder.Build();
await host.RunAsync();
```

### 3. 使用日誌記錄

```csharp
public class MyService
{
    private readonly ILogger<MyService> _logger;

    public MyService(ILogger<MyService> logger)
    {
        _logger = logger;
    }

    public async Task DoWorkAsync()
    {
        _logger.LogInformation("開始執行工作");
        
        try
        {
            // 執行一些工作
            await Task.Delay(1000);
            _logger.LogInformation("工作執行成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "工作執行失敗");
        }
    }
}
```

### 4. 註冊串流處理器

```csharp
public class LogStreamService
{
    private readonly IPipeLoggerProvider _pipeLoggerProvider;

    public LogStreamService(IPipeLoggerProvider pipeLoggerProvider)
    {
        _pipeLoggerProvider = pipeLoggerProvider;
    }

    public async Task StartAsync()
    {
        // 註冊串流處理器
        var guid = Guid.NewGuid().ToString();
        await _pipeLoggerProvider.RegisterPipeStream(guid, HandleLogMessage);
    }

    private async Task<bool> HandleLogMessage(StreamMessage message)
    {
        try
        {
            // 處理日誌訊息
            Console.WriteLine($"[{message.Type}] {message.Content}");
            
            // 可以將訊息傳送到其他系統
            // await SendToExternalSystem(message);
            
            return true; // 返回 true 表示處理成功
        }
        catch (Exception ex)
        {
            Console.WriteLine($"處理日誌訊息時發生錯誤: {ex.Message}");
            return false; // 返回 false 會自動移除此處理器
        }
    }
}
```

## API 參考

### IPipeLoggerProvider 介面

```csharp
public interface IPipeLoggerProvider : ILoggerProvider
{
    /// <summary>取得 PipeLoggerOptions。</summary>
    PipeLoggerOptions Options { get; }
    
    /// <summary>取得分類名稱。</summary>
    string Category { get; }

    /// <summary>發送記錄條目到命名管道。</summary>
    /// <param name="logEntry">要發送的記錄條目。</param>
    Task SendLogEntry(LogEntry logEntry);

    /// <summary>註冊日誌串流處理器。</summary>
    /// <param name="guid">唯一識別符，用於識別串流處理器。</param>
    /// <param name="writer">處理串流訊息的委託。</param>
    /// <returns>如果成功註冊，則返回 true；如果已存在相同的處理器，則返回 false。</returns>
    Task<bool> RegisterPipeStream(string guid, Func<StreamMessage, Task<bool>> writer);

    /// <summary>取消註冊日誌串流處理器。</summary>
    /// <param name="guid">唯一識別符，用於識別串流處理器。</param>
    /// <returns>如果成功取消註冊，則返回 true；如果不存在相同的處理器，則返回 false。</returns>
    Task<bool> UnregisterPipeStream(string guid);

    /// <summary>檢查是否包含指定的日誌串流處理器。</summary>
    /// <param name="guid">唯一識別符，用於識別串流處理器。</param>
    /// <returns>如果包含指定的處理器，則返回 true；否則返回 false。</returns>
    bool Contains(string guid);
}
```

### LogEntry 類別

```csharp
[Serializable]
public class LogEntry
{
    /// <summary>記錄的時間戳。</summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>記錄的日誌級別。</summary>
    public LogLevel LogLevel { get; set; } = LogLevel.Information;
    
    /// <summary>記錄的分類名稱。</summary>
    public string Category { get; set; } = string.Empty;
    
    /// <summary>記錄的訊息內容。</summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>記錄的例外訊息。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Exception { get; set; }
}
```

## 日誌級別對應

PipeLogger 會將 Microsoft.Extensions.Logging 的日誌級別對應到 StreamMessage 類型：

| LogLevel | StreamMessageTypes |
|----------|-------------------|
| Trace | Trace |
| Debug | Debug |
| Information | Info |
| Warning | Warning |
| Error | Error |
| Critical | Error |

## 完整使用範例

### 主機應用程式

```csharp
using CJF.NamedPipe.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices(services =>
{
    services.AddSingleton<MyService>();
    services.AddSingleton<LogStreamService>();
});

builder.ConfigureLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
    logging.AddPipeLogger();
});

var host = builder.Build();

// 啟動日誌串流服務
var logStreamService = host.Services.GetRequiredService<LogStreamService>();
await logStreamService.StartAsync();

// 執行主要服務
var myService = host.Services.GetRequiredService<MyService>();
await myService.DoWorkAsync();

await host.RunAsync();
```

### 服務類別

```csharp
public class MyService
{
    private readonly ILogger<MyService> _logger;

    public MyService(ILogger<MyService> logger)
    {
        _logger = logger;
    }

    public async Task DoWorkAsync()
    {
        _logger.LogTrace("追蹤訊息");
        _logger.LogDebug("除錯訊息");
        _logger.LogInformation("資訊訊息");
        _logger.LogWarning("警告訊息");
        
        try
        {
            throw new InvalidOperationException("測試例外");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "發生錯誤");
        }
        
        _logger.LogCritical("嚴重錯誤訊息");
    }
}
```

### 日誌串流服務

```csharp
public class LogStreamService
{
    private readonly IPipeLoggerProvider _pipeLoggerProvider;
    private readonly List<string> _registeredGuids = new();

    public LogStreamService(IPipeLoggerProvider pipeLoggerProvider)
    {
        _pipeLoggerProvider = pipeLoggerProvider;
    }

    public async Task StartAsync()
    {
        // 註冊控制台輸出處理器
        var consoleGuid = "console-handler";
        if (await _pipeLoggerProvider.RegisterPipeStream(consoleGuid, HandleConsoleOutput))
        {
            _registeredGuids.Add(consoleGuid);
            Console.WriteLine($"已註冊控制台處理器: {consoleGuid}");
        }

        // 註冊檔案輸出處理器
        var fileGuid = "file-handler";
        if (await _pipeLoggerProvider.RegisterPipeStream(fileGuid, HandleFileOutput))
        {
            _registeredGuids.Add(fileGuid);
            Console.WriteLine($"已註冊檔案處理器: {fileGuid}");
        }
    }

    public async Task StopAsync()
    {
        foreach (var guid in _registeredGuids)
        {
            await _pipeLoggerProvider.UnregisterPipeStream(guid);
            Console.WriteLine($"已取消註冊處理器: {guid}");
        }
        _registeredGuids.Clear();
    }

    private async Task<bool> HandleConsoleOutput(StreamMessage message)
    {
        var color = message.Type switch
        {
            StreamMessageTypes.Error => ConsoleColor.Red,
            StreamMessageTypes.Warning => ConsoleColor.Yellow,
            StreamMessageTypes.Info => ConsoleColor.Green,
            StreamMessageTypes.Debug => ConsoleColor.Cyan,
            StreamMessageTypes.Trace => ConsoleColor.Gray,
            _ => ConsoleColor.White
        };

        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine($"[PIPE-{message.Type}] {message.Content}");
        Console.ForegroundColor = originalColor;

        return true;
    }

    private async Task<bool> HandleFileOutput(StreamMessage message)
    {
        try
        {
            var logFile = "pipe-logs.txt";
            var logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{message.Type}] {message.Content}";
            await File.AppendAllTextAsync(logFile, logLine + Environment.NewLine);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"寫入檔案時發生錯誤: {ex.Message}");
            return false;
        }
    }
}
```

## 最佳實踐

### 1. 處理器管理

```csharp
// 使用 using 語句確保資源釋放
public class LogStreamManager : IDisposable
{
    private readonly IPipeLoggerProvider _provider;
    private readonly List<string> _guids = new();

    public async Task RegisterHandlerAsync(string name, Func<StreamMessage, Task<bool>> handler)
    {
        var guid = $"{name}-{Guid.NewGuid()}";
        if (await _provider.RegisterPipeStream(guid, handler))
        {
            _guids.Add(guid);
        }
    }

    public void Dispose()
    {
        foreach (var guid in _guids)
        {
            _provider.UnregisterPipeStream(guid).Wait();
        }
    }
}
```

### 2. 錯誤處理

```csharp
private async Task<bool> SafeHandler(StreamMessage message)
{
    try
    {
        // 處理邏輯
        await ProcessMessage(message);
        return true;
    }
    catch (Exception ex)
    {
        // 記錄錯誤但不拋出例外
        Console.WriteLine($"處理訊息時發生錯誤: {ex.Message}");
        return false; // 返回 false 會自動移除此處理器
    }
}
```

### 3. 效能考量

```csharp
// 對於高頻率日誌，考慮使用批次處理
private readonly List<StreamMessage> _messageBuffer = new();
private readonly Timer _flushTimer;

private async Task<bool> BatchHandler(StreamMessage message)
{
    lock (_messageBuffer)
    {
        _messageBuffer.Add(message);
    }
    
    // 當緩衝區達到一定大小時立即處理
    if (_messageBuffer.Count >= 100)
    {
        await FlushMessages();
    }
    
    return true;
}
```

## 依賴項目

- Microsoft.Extensions.Logging (>= 9.0.7)
- CJF.NamedPipe (專案參考)

## 版本歷史

### v1.01.20 (2025-07-22)
- 🔧 **非同步優化**: 將 SendLogEntry 方法修改為非同步，改善日誌處理效能
- 🔧 **執行緒安全**: 使用 ConcurrentDictionary 來管理串流處理器，改善效能和執行緒安全性
- 🔧 **方法改善**: 將 SendLogEntry 方法的調用改為等待異步操作完成，確保日誌條目正確處理
- ✅ **配置擴展**: 新增可選的配置參數以擴展 AddPipeLogger 方法
- 🔧 **資源管理**: 在 Dispose 方法中新增日誌串流處理器的清理邏輯
- 🧪 **測試改善**: 改善測試中的非同步處理和驗證機制

### v1.00.10 (2025-07-18)
- 🎉 初始版本發布
- ✅ 支援 Microsoft.Extensions.Logging 整合
- ✅ 命名管道串流傳送
- ✅ 多重處理器支援

## 授權

MIT License

## 相關專案

- [CJF.NamedPipe](../CJF.NamedPipe/readme.md) - 核心命名管道通信庫
- [CJF.NamedPipe.Example](../CJF.NamedPipe.Example/USAGE.md) - 使用範例

## 貢獻

歡迎提交 Issue 和 Pull Request！

## 支援

如有問題，請在 GitHub 上提交 Issue。
