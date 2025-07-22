using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CJF.NamedPipe.Logging;

#region Public Sealed Class : PipeLoggerProvider
/// <summary>命名通道記錄器的提供者。</summary>
/// <remarks>建立 PipeLoggingProvider 新的執行個體。</remarks>
/// <param name="opts">PipeLoggerOptions</param>
[ProviderAlias("PipeLogger")]
public sealed class PipeLoggerProvider(IOptions<PipeLoggerOptions> opts) : IPipeLoggerProvider
{
    /// <summary>命名管道客戶端。</summary>
    // private NamedPipeClientStream? _PipeClient;
    private readonly ConcurrentDictionary<string, Func<StreamMessage, Task<bool>>> _StreamHandlers = [];
    /// <summary>用於同步存取的鎖。</summary>
    private readonly object _lock = new();

    /// <summary>是否已釋放資源。</summary>
    private bool _disposed = false;


    /// <summary>取得 PipeLoggerOptions。</summary>
    public PipeLoggerOptions Options { get; private set; } = opts.Value;
    /// <summary>取得分類名稱。</summary>
    public string Category { get; private set; } = string.Empty;

    /// <summary>發送記錄條目到命名管道。</summary>
    /// <param name="logEntry">要發送的記錄條目。</param>
    public async Task SendLogEntry(LogEntry logEntry)
    {
        if (_disposed) return;
        var guids = _StreamHandlers.Keys.ToArray();
        if (guids.Length == 0) return;
        var streamMessage = new StreamMessage
        {
            Content = logEntry.Message,
            Type = logEntry.LogLevel switch
            {
                LogLevel.Trace => StreamMessageTypes.Trace,
                LogLevel.Debug => StreamMessageTypes.Debug,
                LogLevel.Information => StreamMessageTypes.Info,
                LogLevel.Warning => StreamMessageTypes.Warning,
                LogLevel.Error => StreamMessageTypes.Error,
                LogLevel.Critical => StreamMessageTypes.Error,
                _ => StreamMessageTypes.Info
            },
        };
        foreach (var guid in guids)
        {
            try
            {
                if (!_StreamHandlers.TryGetValue(guid, out var handler) || handler is null)
                    continue;
                if (await handler!(streamMessage) is not true)
                    _StreamHandlers.TryRemove(guid, out _);
            }
            catch
            {
                _StreamHandlers.TryRemove(guid, out _);
            }
        }
    }

    /// <summary>建立 ILogger 新的執行個體。</summary>
    /// <param name="categoryName">分類名稱</param>
    public ILogger CreateLogger(string categoryName)
    {
        Category = categoryName;
        return new PipeLogger(this);
    }
    /// <summary>釋放資源。</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>註冊日誌串流處理器。</summary>
    /// <param name="guid">唯一識別符，用於識別串流處理器。</param>
    /// <param name="writer">處理串流訊息的委託。</param>
    /// <returns>如果成功註冊，則返回 true；如果已存在相同的處理器，則返回 false。</returns>
    public Task<bool> RegisterPipeStream(string guid, Func<StreamMessage, Task<bool>> writer)
    {
        // 檢查是否已經註冊過相同的處理器
        if (_StreamHandlers.ContainsKey(guid))
            return Task.FromResult(false);
        lock (_lock)
        {
            _StreamHandlers.TryAdd(guid, writer);
        }
        return Task.FromResult(true);
    }

    /// <summary>取消註冊日誌串流處理器。</summary>
    /// <param name="guid">唯一識別符，用於識別串流處理器。</param>
    /// <returns>如果成功取消註冊，則返回 true；如果不存在相同的處理器，則返回 false。</returns>
    public Task<bool> UnregisterPipeStream(string guid)
    {
        // 檢查是否存在相同的處理器
        if (!_StreamHandlers.ContainsKey(guid))
            return Task.FromResult(false);
        // 移除處理器
        lock (_lock)
        {
            _StreamHandlers.TryRemove(guid, out _);
        }
        // Console.WriteLine($"已取消註冊日誌串流處理器: {guid}");
        return Task.FromResult(true);
    }

    /// <summary>檢查是否包含指定的日誌串流處理器。</summary>
    /// <param name="guid">唯一識別符，用於識別串流處理器。</param>
    /// <returns>如果包含指定的處理器，則返回 true；否則返回 false。</returns>
    public bool Contains(string guid) => _StreamHandlers.ContainsKey(guid);
}
#endregion
