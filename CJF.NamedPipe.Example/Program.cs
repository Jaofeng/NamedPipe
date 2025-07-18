using CJF.NamedPipe;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CJF.NamedPipe.Example;

/// <summary>示例應用程序主類</summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("CJF.NamedPipe 示例應用程序");
        Console.WriteLine("==========================");

        if (args.Length > 0 && args[0] == "client")
        {
            await RunClientAsync();
        }
        else
        {
            await RunServerAsync();
        }
    }

    /// <summary>運行服務器模式</summary>
    private static async Task RunServerAsync()
    {
        Console.WriteLine("啟動命名管道服務器...");

        var host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                // 從配置文件綁定 PipeLineOptions
                services.Configure<PipeLineOptions>(context.Configuration.GetSection("PipeLine"));

                // 註冊命名管道服務
                services.AddPipeLineService(CommandHandler, StreamHandler);
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            })
            .Build();

        Console.WriteLine("服務器已啟動，按 Ctrl+C 停止服務器");
        Console.WriteLine("您可以在另一個終端中運行 'dotnet run client' 來測試客戶端");

        await host.RunAsync();
    }

    /// <summary>運行客戶端模式</summary>
    private static async Task RunClientAsync()
    {
        Console.WriteLine("啟動命名管道客戶端...");

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.Configure<PipeLineOptions>(options =>
        {
            options.CommandPipeName = "CJF.Example.Command.Pipe";
            options.StreamPipeName = "CJF.Example.Stream.Pipe";
        });
        services.AddSingleton<IPipeLineProvider, PipeLineProvider>();

        var serviceProvider = services.BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<IPipeLineProvider>();
        var client = provider.CreateClient();

        Console.WriteLine("等待服務器啟動...");
        if (!await client.WaitForServiceAsync(10000))
        {
            Console.WriteLine("無法連接到服務器，請確保服務器正在運行");
            return;
        }

        Console.WriteLine("已連接到服務器！");

        while (true)
        {
            Console.WriteLine("\n選擇操作:");
            Console.WriteLine("1. 發送一般命令");
            Console.WriteLine("2. 發送串流命令");
            Console.WriteLine("3. 測試連接");
            Console.WriteLine("4. 退出");
            Console.Write("請輸入選項 (1-4): ");

            var choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    await TestNormalCommand(client);
                    break;
                case "2":
                    await TestStreamCommand(client);
                    break;
                case "3":
                    await TestConnection(client);
                    break;
                case "4":
                    Console.WriteLine("再見！");
                    return;
                default:
                    Console.WriteLine("無效選項，請重新選擇");
                    break;
            }
        }
    }

    /// <summary>測試一般命令</summary>
    private static async Task TestNormalCommand(PipeClient client)
    {
        Console.Write("請輸入命令: ");
        var command = Console.ReadLine() ?? "test";
        
        Console.Write("請輸入參數 (用空格分隔): ");
        var argsInput = Console.ReadLine() ?? "";
        var args = string.IsNullOrWhiteSpace(argsInput) ? Array.Empty<string>() : argsInput.Split(' ');

        var (result, message) = await client.SendCommandAsync(command, args);
        
        Console.WriteLine($"結果: {result}");
        Console.WriteLine($"回應: {message}");
    }

    /// <summary>測試串流命令</summary>
    private static async Task TestStreamCommand(PipeClient client)
    {
        Console.Write("請輸入串流命令: ");
        var command = Console.ReadLine() ?? "stream-test";
        
        Console.Write("請輸入參數 (用空格分隔): ");
        var argsInput = Console.ReadLine() ?? "";
        var args = string.IsNullOrWhiteSpace(argsInput) ? Array.Empty<string>() : argsInput.Split(' ');

        Console.WriteLine("開始接收串流訊息...");

        var (result, message) = await client.SendStreamCommandAsync(command, streamMessage =>
        {
            var typeColor = streamMessage.Type switch
            {
                StreamMessageTypes.Info => ConsoleColor.White,
                StreamMessageTypes.Warning => ConsoleColor.Yellow,
                StreamMessageTypes.Error => ConsoleColor.Red,
                StreamMessageTypes.Success => ConsoleColor.Green,
                _ => ConsoleColor.Gray
            };

            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = typeColor;
            Console.WriteLine($"[{streamMessage.Type}] {streamMessage.Content}");
            Console.ForegroundColor = originalColor;
        }, args);

        Console.WriteLine($"\n串流完成 - 結果: {result}");
        Console.WriteLine($"最終訊息: {message}");
    }

    /// <summary>測試連接</summary>
    private static async Task TestConnection(PipeClient client)
    {
        Console.WriteLine("測試連接中...");
        var canConnect = await client.TestPipeConnection();
        Console.WriteLine($"連接狀態: {(canConnect ? "成功" : "失敗")}");
    }

    /// <summary>命令處理器</summary>
    private static async Task<string> CommandHandler(string command, string[] args)
    {
        Console.WriteLine($"[服務器] 收到命令: {command}, 參數: [{string.Join(", ", args)}]");
        
        return command.ToLower() switch
        {
            "hello" => "Hello, World!",
            "time" => $"當前時間: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            "echo" => $"回音: {string.Join(" ", args)}",
            "add" when args.Length >= 2 && int.TryParse(args[0], out var a) && int.TryParse(args[1], out var b) 
                => $"計算結果: {a} + {b} = {a + b}",
            "error" => throw new InvalidOperationException("這是一個測試錯誤"),
            _ => await Task.FromResult($"處理命令: {command}, 參數: [{string.Join(", ", args)}]")
        };
    }

    /// <summary>串流命令處理器</summary>
    private static async Task StreamHandler(string command, string[] args, Func<StreamMessage, Task<bool>> streamWriter, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[服務器] 收到串流命令: {command}, 參數: [{string.Join(", ", args)}]");

        await streamWriter(new StreamMessage($"開始處理串流命令: {command}", StreamMessageTypes.Info));

        switch (command.ToLower())
        {
            case "countdown":
                var count = args.Length > 0 && int.TryParse(args[0], out var c) ? c : 5;
                for (int i = count; i > 0; i--)
                {
                    await streamWriter(new StreamMessage($"倒數: {i}", StreamMessageTypes.Info));
                    await Task.Delay(1000, cancellationToken);
                }
                await streamWriter(new StreamMessage("倒數完成！", StreamMessageTypes.Success, true));
                break;

            case "progress":
                var steps = args.Length > 0 && int.TryParse(args[0], out var s) ? s : 10;
                for (int i = 1; i <= steps; i++)
                {
                    var percentage = (i * 100) / steps;
                    await streamWriter(new StreamMessage($"進度: {percentage}% ({i}/{steps})", StreamMessageTypes.Info));
                    await Task.Delay(500, cancellationToken);
                }
                await streamWriter(new StreamMessage("處理完成！", StreamMessageTypes.Success, true));
                break;

            case "error-test":
                await streamWriter(new StreamMessage("即將發生錯誤...", StreamMessageTypes.Warning));
                await Task.Delay(1000, cancellationToken);
                await streamWriter(new StreamMessage("發生錯誤！", StreamMessageTypes.Error, true));
                break;

            default:
                for (int i = 1; i <= 3; i++)
                {
                    await streamWriter(new StreamMessage($"處理步驟 {i}/3", StreamMessageTypes.Info));
                    await Task.Delay(1000, cancellationToken);
                }
                await streamWriter(new StreamMessage($"完成處理串流命令: {command}", StreamMessageTypes.Success, true));
                break;
        }
    }
}
