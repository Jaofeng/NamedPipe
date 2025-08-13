# CJF.NamedPipe

[![NuGet version](https://badge.fury.io/nu/CJF.NamedPipe.svg)](https://badge.fury.io/nu/CJF.NamedPipe)


一個基於 ASP.NET Core 依賴注入 (DI) 模式的命名管道 (Named Pipe) 通信庫，支援一般命令和串流命令處理。

## 功能特色

- ✅ **ASP.NET Core DI 支援**: 完全整合 Microsoft.Extensions.DependencyInjection
- ✅ **雙管道架構**: 分別處理一般命令和串流命令
- ✅ **配置選項**: 支援 IOptions 模式進行配置
- ✅ **日誌記錄**: 整合 Microsoft.Extensions.Logging
- ✅ **後台服務**: 支援 IHostedService 模式
- ✅ **錯誤處理**: 完善的異常處理和超時機制
- ✅ **測試支援**: 包含完整的單元測試和整合測試

## 快速開始

### 1. 安裝套件

```bash
dotnet add package CJF.NamedPipe
```

### 2. 註冊服務

```csharp
using CJF.NamedPipe;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices(services =>
{
    // 註冊命名管道服務
    services.AddPipeLineService(CommandHandler, StreamHandler);
    
    // 或者使用配置選項
    services.AddPipeLineService(options =>
    {
        options.CommandPipeName = "MyApp.Command.Pipe";
        options.StreamPipeName = "MyApp.Stream.Pipe";
        options.MaxClients = 10;
    }, CommandHandler, StreamHandler);
});

var host = builder.Build();
await host.RunAsync();
```

### 3. 實作命令處理器

```csharp
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
    
    // 模擬處理過程
    for (int i = 1; i <= 5; i++)
    {
        await streamWriter(new StreamMessage($"進度: {i}/5", StreamMessageTypes.Info));
        await Task.Delay(1000, cancellationToken);
    }
    
    await streamWriter(new StreamMessage("處理完成", StreamMessageTypes.Success, true));
}
```

### 4. 客戶端使用

```csharp
using CJF.NamedPipe;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddLogging();
services.Configure<PipeLineOptions>(options =>
{
    options.CommandPipeName = "MyApp.Command.Pipe";
    options.StreamPipeName = "MyApp.Stream.Pipe";
});
services.AddSingleton<IPipeLineProvider, PipeLineProvider>();

var serviceProvider = services.BuildServiceProvider();
var provider = serviceProvider.GetRequiredService<IPipeLineProvider>();
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

```csharp
public class PipeLineOptions
{
    /// <summary>一般命令管道名稱</summary>
    public string CommandPipeName { get; set; } = "CJF.NamedPipe.Command.Pipe";
    
    /// <summary>串流命令管道名稱</summary>
    public string StreamPipeName { get; set; } = "CJF.NamedPipe.Stream.Pipe";
    
    /// <summary>最大客戶端數量</summary>
    public int MaxClients { get; set; } = -1; // -1 表示無限制
    
    /// <summary>連接超時時間（毫秒）</summary>
    public int ConnectionTimeoutMs { get; set; } = 5000;
    
    /// <summary>讀寫超時時間（毫秒）</summary>
    public int ReadWriteTimeoutMs { get; set; } = 5000;
    
    /// <summary>服務標誌文件路徑</summary>
    public string ServiceFlagFilePath { get; set; } = "...";
}
```

## 使用 appsettings.json 配置

```json
{
  "PipeLine": {
    "CommandPipeName": "MyApp.Command.Pipe",
    "StreamPipeName": "MyApp.Stream.Pipe",
    "MaxClients": 10,
    "ConnectionTimeoutMs": 5000,
    "ReadWriteTimeoutMs": 5000
  }
}
```

```csharp
builder.ConfigureServices((context, services) =>
{
    // 從配置文件綁定選項
    services.Configure<PipeLineOptions>(context.Configuration.GetSection("PipeLine"));
    
    services.AddPipeLineService(CommandHandler, StreamHandler);
});
```

## 擴展方法

### IServiceCollection 擴展

```csharp
// 基本註冊
services.AddPipeLineService();

// 註冊命令處理器
services.AddPipeLineService(commandHandler, streamHandler);

// 配置選項 + 命令處理器
services.AddPipeLineService(options => 
{
    options.CommandPipeName = "Custom.Pipe";
}, commandHandler, streamHandler);
```

### IHostBuilder 擴展

```csharp
// 使用 HostBuilder
hostBuilder.UsePipeLineService(options =>
{
    options.CommandPipeName = "MyApp.Command.Pipe";
    options.StreamPipeName = "MyApp.Stream.Pipe";
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
    default:
        Console.WriteLine($"未知錯誤: {message}");
        break;
}
```

## 測試

專案包含完整的測試套件：

```bash
# 運行所有測試
dotnet test CJF.NamedPipe.Tests

# 運行特定測試
dotnet test CJF.NamedPipe.Tests --filter "TestMethod=PipeLineProvider_ShouldCreateClientAndServer"
```

## 示例專案

查看 `CJF.NamedPipe.Example` 專案以獲得完整的使用示例：

```bash
# 運行服務器
cd CJF.NamedPipe.Example
dotnet run

# 在另一個終端運行客戶端
dotnet run client
```

## 架構說明

### 核心組件

- **IPipeLineProvider**: 命名管道提供者介面
- **PipeLineProvider**: 命名管道提供者實作
- **PipeServer**: 命名管道服務器
- **PipeClient**: 命名管道客戶端
- **PipeLineService**: 後台服務實作
- **PipeLineOptions**: 配置選項類別

### 訊息類型

- **CommandMessage**: 命令訊息
- **StreamMessage**: 串流訊息
- **StreamMessageTypes**: 串流訊息類型枚舉

### 委託類型

- **PipeCommandHandler**: 一般命令處理器委託
- **PipeStreamCommandHandler**: 串流命令處理器委託

## 版本歷史

### v1.01.32 (2025-08-13)
- 🔧 **效能改善**: 修改 `PipeServer` 類別，將 `CancellationTokenSource` 改為可為 null，並在啟動和停止方法中新增取消邏輯，改善非同步處理的穩定性
- ✅ **新增方法**: 新增 `Start` 和 `Stop` 同步方法
- 🔧 **介面修改**: 修改 `StartAsync` 和 `StopAsync` 非同步方法傳入 `CancellationToken` 型態參數

### v1.01.25 (2025-08-01)
- 🔧 **程式碼清理**: 移除 `PipeLineOptions.ApplicationDataPath` 屬性

### v1.01.20 (2025-07-22)
- 🔧 **效能改善**: 將 `PipeServer` 的啟動方法修改為非同步，改善服務啟動效能
- 🔧 **架構重構**: 重構 `PipeServer` 類別，新增對 `IPipeLineProvider` 的依賴
- 🔧 **非同步優化**: 將啟動和停止服務器的方法修改為非同步
- ✅ **服務管理**: 新增 `CreateFlagFile` 和 `CleanFlagFile` 方法管理服務標誌文件
- 🛡️ **錯誤處理**: 新增異常處理以改善與服務通信的錯誤回報
- 🔧 **程式碼優化**: 將 `SendMessageAndHandleResponseAsync` 方法修改為 `static` 方法
- 🧹 **程式碼清理**: 移除不再使用的 `PipeConstants` 類別及其相關常量
- 📝 **配置更新**: 更新 `PipeLineOptions` 類別中的管道名稱和路徑設置
- 🧪 **測試改善**: 改善測試中的伺服器啟動方法為非同步，新增服務狀態和管道連接的驗證

### v1.00.10 (2025-07-18)
- 🎉 初始版本發布
- ✅ 支援 ASP.NET Core DI 模式
- ✅ 雙管道架構實作
- ✅ 完整的測試覆蓋
- ✅ 程式碼優化和重構
- ✅ 現代化的錯誤處理機制

## 授權

MIT License

## 貢獻

歡迎提交 Issue 和 Pull Request！

## 支援

如有問題，請在 GitHub 上提交 Issue。
