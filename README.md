# CJF.NamedPipe

[![NuGet version](https://badge.fury.io/nu/CJF.NamedPipe.svg)](https://badge.fury.io/nu/CJF.NamedPipe)
[![NuGet version](https://badge.fury.io/nu/CJF.NamedPipe.Logging.svg)](https://badge.fury.io/nu/CJF.NamedPipe.Logging)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

一個基於 ASP.NET Core 依賴注入 (DI) 模式的命名管道 (Named Pipe) 通信庫，支援一般命令和串流命令處理。

## 功能特色

- **ASP.NET Core DI 支援**: 完全整合 Microsoft.Extensions.DependencyInjection
- **雙管道架構**: 分別處理一般命令和串流命令，互不干擾
- **心跳機制**: 伺服端定期發送心跳訊息，提早偵測斷線狀況
- **配置選項**: 支援 IOptions 模式和 appsettings.json 配置
- **日誌記錄**: 整合 Microsoft.Extensions.Logging
- **後台服務**: 支援 IHostedService 模式
- **錯誤處理**: 完善的異常處理和超時機制
- **執行緒安全**: 使用 SemaphoreSlim 實現同步寫入

## 套件

| 套件 | 描述 |
|------|------|
| [CJF.NamedPipe](https://www.nuget.org/packages/CJF.NamedPipe) | 核心命名管道通信庫 |
| [CJF.NamedPipe.Logging](https://www.nuget.org/packages/CJF.NamedPipe.Logging) | 命名管道日誌記錄提供者 |

## 快速開始

### 安裝套件

```bash
dotnet add package CJF.NamedPipe
dotnet add package CJF.NamedPipe.Logging  # 可選
```

### 服務端

```csharp
using CJF.NamedPipe;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices(services =>
{
    services.AddPipeLineService(options =>
    {
        options.CommandPipeName = "MyApp.Command.Pipe";
        options.StreamPipeName = "MyApp.Stream.Pipe";
    }, CommandHandler, StreamHandler);
});

var host = builder.Build();
await host.RunAsync();

// 一般命令處理器
static async Task<string> CommandHandler(string command, string[] args)
{
    return command switch
    {
        "hello" => "Hello, World!",
        "time" => DateTime.Now.ToString(),
        "echo" => string.Join(" ", args),
        _ => $"未知命令: {command}"
    };
}

// 串流命令處理器
static async Task StreamHandler(string command, string[] args,
    Func<StreamMessage, Task<bool>> streamWriter, CancellationToken cancellationToken)
{
    await streamWriter(new StreamMessage($"開始處理: {command}", StreamMessageTypes.Info));

    for (int i = 1; i <= 5; i++)
    {
        await streamWriter(new StreamMessage($"進度: {i}/5", StreamMessageTypes.Info));
        await Task.Delay(1000, cancellationToken);
    }

    await streamWriter(new StreamMessage("處理完成", StreamMessageTypes.Success, true));
}
```

### 客戶端

```csharp
using CJF.NamedPipe;

var services = new ServiceCollection();
services.AddLogging();
services.Configure<PipeLineOptions>(options =>
{
    options.CommandPipeName = "MyApp.Command.Pipe";
    options.StreamPipeName = "MyApp.Stream.Pipe";
});
services.AddSingleton<IPipeLineProvider, PipeLineProvider>();

var provider = services.BuildServiceProvider().GetRequiredService<IPipeLineProvider>();
var client = provider.CreateClient();

// 發送一般命令
var (result, message) = await client.SendCommandAsync("hello");
Console.WriteLine($"結果: {result}, 訊息: {message}");

// 發送串流命令
await client.SendStreamCommandAsync("process", streamMessage =>
{
    Console.WriteLine($"[{streamMessage.Type}] {streamMessage.Content}");
}, "arg1", "arg2");
```

## 配置選項

### 使用程式碼配置

```csharp
services.AddPipeLineService(options =>
{
    options.CommandPipeName = "MyApp.Command.Pipe";  // 一般命令管道名稱
    options.StreamPipeName = "MyApp.Stream.Pipe";    // 串流命令管道名稱
    options.MaxClients = 10;                          // 最大客戶端數量 (-1 表示無限制)
    options.ConnectionTimeoutMs = 5000;               // 連接超時時間 (毫秒)
    options.ReadWriteTimeoutMs = 5000;                // 讀寫超時時間 (毫秒)
    options.HeartbeatIntervalMs = 30000;              // 心跳間隔時間 (毫秒，0 或負數停用)
}, CommandHandler, StreamHandler);
```

### 使用 appsettings.json 配置

```json
{
  "PipeLine": {
    "CommandPipeName": "MyApp.Command.Pipe",
    "StreamPipeName": "MyApp.Stream.Pipe",
    "MaxClients": 10,
    "ConnectionTimeoutMs": 5000,
    "ReadWriteTimeoutMs": 5000,
    "HeartbeatIntervalMs": 30000
  }
}
```

```csharp
builder.ConfigureServices((context, services) =>
{
    services.Configure<PipeLineOptions>(context.Configuration.GetSection("PipeLine"));
    services.AddPipeLineService(CommandHandler, StreamHandler);
});
```

## 錯誤處理

```csharp
var (result, message) = await client.SendCommandAsync("test");

switch (result)
{
    case PipeResults.Success:
        Console.WriteLine($"成功: {message}");
        break;
    case PipeResults.ServiceNotRunning:
        Console.WriteLine("服務未運行");
        break;
    case PipeResults.ConnectionError:
        Console.WriteLine("連接錯誤");
        break;
    case PipeResults.Timeout:
        Console.WriteLine("操作超時");
        break;
    case PipeResults.CommandError:
        Console.WriteLine($"命令錯誤: {message}");
        break;
}
```

## 日誌記錄擴展

CJF.NamedPipe.Logging 提供將日誌訊息透過命名管道串流傳送的功能：

```csharp
builder.ConfigureLogging(logging =>
{
    logging.AddPipeLogger();
});
```

詳細使用說明請參閱 [CJF.NamedPipe.Logging/readme.md](CJF.NamedPipe.Logging/readme.md)

## 專案結構

```
NamedPipe.sln
├── CJF.NamedPipe/           # 核心命名管道通信庫
├── CJF.NamedPipe.Logging/   # 命名管道日誌記錄提供者
├── CJF.NamedPipe.Tests/     # xUnit 測試專案
└── CJF.NamedPipe.Example/   # 使用範例專案
```

## 建置與測試

```bash
# 建置整個方案
dotnet build NamedPipe.sln

# 執行所有測試
dotnet test CJF.NamedPipe.Tests/CJF.NamedPipe.Tests.csproj

# 執行範例應用程式
cd CJF.NamedPipe.Example
dotnet run              # 啟動服務器
dotnet run -- client    # 啟動客戶端 (另一終端)
```

## 架構說明

### 核心組件

| 組件 | 描述 |
|------|------|
| `IPipeLineProvider` | 命名管道提供者介面 |
| `PipeLineProvider` | 命名管道提供者實作 |
| `PipeServer` | 命名管道服務器 |
| `PipeClient` | 命名管道客戶端 |
| `PipeLineService` | 後台服務實作 |
| `PipeLineOptions` | 配置選項類別 |

### 訊息類型

| 類型 | 描述 |
|------|------|
| `CommandMessage` | 命令訊息 |
| `StreamMessage` | 串流訊息 |
| `StreamMessageTypes` | 串流訊息類型 (Info, Success, Warning, Error, Debug, Trace, Heartbeat) |

## 授權

[MIT License](LICENSE)

## 貢獻

歡迎提交 Issue 和 Pull Request！

## 連結

- [GitHub Repository](https://github.com/Jaofeng/NamedPipe)
- [NuGet - CJF.NamedPipe](https://www.nuget.org/packages/CJF.NamedPipe)
- [NuGet - CJF.NamedPipe.Logging](https://www.nuget.org/packages/CJF.NamedPipe.Logging)
