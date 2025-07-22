using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CJF.NamedPipe;

/// <summary>命名管道後台服務。</summary>
/// <remarks>這個服務用於在背景中處理命名管道的相關操作。</remarks>
/// <remarks>初始化新的命名管道服務實例。</remarks>
/// <param name="pipeLineProvider">命名管道提供者</param>
/// <param name="logger">日誌記錄器</param>
/// <param name="commandHandler">命令處理器</param>
/// <param name="streamHandler">串流命令處理器</param>
/// <exception cref="ArgumentNullException">如果 <paramref name="pipeLineProvider"/> 為 null，則拋出此異常。</exception>
public class PipeLineService(IPipeLineProvider pipeLineProvider, ILogger<PipeLineService> logger, PipeCommandHandler? commandHandler = null, PipeStreamCommandHandler? streamHandler = null) : BackgroundService
{
    private readonly IPipeLineProvider _pipeLineProvider = pipeLineProvider ?? throw new ArgumentNullException(nameof(pipeLineProvider));
    private readonly ILogger<PipeLineService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly PipeCommandHandler? _commandHandler = commandHandler;
    private readonly PipeStreamCommandHandler? _streamHandler = streamHandler;
    private PipeServer? _pipeServer;

    /// <summary>執行服務</summary>
    /// <param name="stoppingToken">停止令牌</param>
    /// <returns>執行任務</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_commandHandler == null)
        {
            _logger.LogWarning("命令處理器未設定，服務將不會啟動");
            return;
        }

        try
        {
            _logger.LogInformation("正在啟動命名管道服務...");

            _pipeServer = _pipeLineProvider.CreateServer(_commandHandler, _streamHandler);
            await _pipeServer.StartAsync();

            _logger.LogInformation("命名管道服務已啟動");

            // 等待取消令牌
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("命名管道服務正在停止...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "命名管道服務執行時發生錯誤");
            throw;
        }
        finally
        {
            if (_pipeServer != null)
            {
                await _pipeServer.StopAsync();
                _logger.LogInformation("命名管道服務已停止");
            }
        }
    }

    /// <summary>停止服務</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>停止任務</returns>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("正在停止命名管道服務...");

        if (_pipeServer != null)
        {
            await _pipeServer.StopAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}


// <summary>擴展方法，用於註冊命名管道服務</summary>
/// <remarks>這些方法可以在 ASP.NET Core 應用程序中使用，以便輕鬆地添加和配置命名管道服務。</remarks>
public static class PipeLineServiceExtensions
{
    /// <summary>將命名管道服務添加到服務集合中</summary>
    /// <param name="services">服務集合</param>
    /// <returns>返回服務集合，以便可以鏈式調用</returns>
    /// <remarks>這個方法將 <see cref="IPipeLineProvider"/> 和 <see cref="PipeLineService"/> 添加到服務集合中。</remarks>
    public static IServiceCollection AddPipeLineService(this IServiceCollection services)
    {
        services.AddSingleton<IPipeLineProvider, PipeLineProvider>();
        services.AddSingleton<PipeLineService>();
        return services;
    }

    /// <summary>將命名管道服務添加到主機構建器中</summary>
    /// <param name="hostBuilder">主機構建器</param>
    /// <param name="configureOptions">配置選項的委託</param>
    /// <returns>返回主機構建器，以便可以鏈式調用</returns>
    /// <remarks>這個方法允許用戶配置管道選項，並將命名管道服務添加到主機中。</remarks>
    public static IHostBuilder UsePipeLineService(this IHostBuilder hostBuilder, Action<PipeLineOptions>? configureOptions = null)
    {
        hostBuilder.ConfigureServices((context, services) =>
        {
            if (configureOptions != null)
                services.Configure<PipeLineOptions>(configureOptions);
            else
                services.Configure<PipeLineOptions>(options => { });
            services.AddPipeLineService();
        });

        return hostBuilder;
    }

    /// <summary>將命名管道服務添加到服務集合中，並配置選項</summary>
    /// <param name="services">服務集合</param>
    /// <param name="configureOptions">配置選項的委託</param>
    /// <returns>返回服務集合，以便可以鏈式調用</returns>
    public static IServiceCollection AddPipeLineService(this IServiceCollection services, Action<PipeLineOptions>? configureOptions)
    {
        if (configureOptions != null)
            services.Configure<PipeLineOptions>(configureOptions);
        else
            services.Configure<PipeLineOptions>(options => { });
        return services.AddPipeLineService();
    }

    /// <summary>將命名管道服務添加到服務集合中，並註冊命令處理器</summary>
    /// <param name="services">服務集合</param>
    /// <param name="commandHandler">一般命令處理器</param>
    /// <param name="streamHandler">串流命令處理器</param>
    /// <returns>返回服務集合，以便可以鏈式調用</returns>
    public static IServiceCollection AddPipeLineService(this IServiceCollection services, PipeCommandHandler commandHandler, PipeStreamCommandHandler? streamHandler = null)
    {
        services.AddSingleton<IPipeLineProvider, PipeLineProvider>();
        services.AddSingleton(commandHandler);
        if (streamHandler != null)
            services.AddSingleton(streamHandler);
        services.AddHostedService<PipeLineService>();
        return services;
    }

    /// <summary>將命名管道服務添加到服務集合中，並配置選項和命令處理器</summary>
    /// <param name="services">服務集合</param>
    /// <param name="configureOptions">配置選項的委託</param>
    /// <param name="commandHandler">命令處理器</param>
    /// <param name="streamHandler">串流命令處理器</param>
    /// <returns>返回服務集合，以便可以鏈式調用</returns>
    public static IServiceCollection AddPipeLineService(this IServiceCollection services, Action<PipeLineOptions> configureOptions, PipeCommandHandler commandHandler, PipeStreamCommandHandler? streamHandler = null)
    {
        services.Configure<PipeLineOptions>(configureOptions);
        return services.AddPipeLineService(commandHandler, streamHandler);
    }

}
