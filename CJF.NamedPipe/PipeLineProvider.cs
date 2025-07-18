using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CJF.NamedPipe;

/// <summary>命名管道(Pipeline)提供者介面。</summary>
public interface IPipeLineProvider
{
    /// <summary>獲取或設置管道配置選項。</summary>
    PipeLineOptions Options { get; }

    /// <summary>檢查服務是否正在執行。</summary>
    /// <remarks>
    /// 這個方法通過檢查服務的標誌文件來確定服務是否正在運行。
    /// 如果服務正在運行，則返回 true；否則返回 false。
    /// </remarks>
    bool IsServiceRunning();

    /// <summary>創建一個新的管道客戶端實例。</summary>
    /// <returns>返回一個新的 <see cref="PipeClient"/> 實例。</returns>
    PipeClient CreateClient();

    /// <summary>創建一個新的管道服務器實例。</summary>
    /// <param name="commandHandler">命令處理器</param>
    /// <param name="streamHandler">串流命令處理器</param>
    /// <returns>返回一個新的 <see cref="PipeServer"/> 實例。</returns>
    PipeServer CreateServer(PipeCommandHandler commandHandler, PipeStreamCommandHandler? streamHandler = null);
}

/// <summary>命名管道(Pipeline)提供者。</summary>
/// <remarks>此提供者用於處理命名管道相關的操作。</remarks>
[ProviderAlias("PipeLine")]
public class PipeLineProvider : IPipeLineProvider
{
    private readonly PipeLineOptions _opt;
    private readonly ILogger<PipeLineProvider> _logger;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>獲取管道配置選項</summary>
    public PipeLineOptions Options => _opt;

    /// <summary>初始化新的命名管道提供者實例。</summary>
    /// <param name="options">管道配置選項</param>
    /// <param name="logger">日誌記錄器</param>
    /// <param name="loggerFactory">日誌工廠</param>
    public PipeLineProvider(IOptions<PipeLineOptions> options, ILogger<PipeLineProvider> logger, ILoggerFactory loggerFactory)
    {
        _opt = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        EnsureServiceFlagDirectoryExists();
    }

    /// <summary>檢查服務是否正在執行。</summary>
    /// <remarks>
    /// 這個方法通過檢查服務的標誌文件來確定服務是否正在運行。
    /// 如果服務正在運行，則返回 true；否則返回 false。
    /// </remarks>
    public bool IsServiceRunning()
    {
        return File.Exists(_opt.ServiceFlagFilePath) && File.ReadAllText(_opt.ServiceFlagFilePath) != string.Empty;
    }

    /// <summary>創建一個新的管道客戶端實例。</summary>
    /// <returns>返回一個新的 <see cref="PipeClient"/> 實例。</returns>
    public PipeClient CreateClient() => new PipeClient(this);

    /// <summary>創建一個新的管道服務器實例。</summary>
    /// <param name="commandHandler">命令處理器</param>
    /// <param name="streamHandler">串流命令處理器</param>
    /// <returns>返回一個新的 <see cref="PipeServer"/> 實例。</returns>
    public PipeServer CreateServer(PipeCommandHandler commandHandler, PipeStreamCommandHandler? streamHandler = null)
    {
        var serverLogger = _loggerFactory.CreateLogger<PipeServer>();
        return new PipeServer(commandHandler, streamHandler, _opt, serverLogger);
    }

    /// <summary>確保服務標誌文件的目錄存在</summary>
    private void EnsureServiceFlagDirectoryExists()
    {
        string directory = Path.GetDirectoryName(_opt.ServiceFlagFilePath)!;
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
    }
}
