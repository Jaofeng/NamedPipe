using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CJF.NamedPipe.Logging;


#region Public Class : PipeLogger
/// <summary>命名管道記錄器。</summary>
public class PipeLogger([NotNull] PipeLoggerProvider provider) : ILogger
{
    private readonly LoggerExternalScopeProvider _ScopeProvider = new();
    private readonly PipeLoggerProvider _Provider = provider;

    /// <summary>開始一個作用域。</summary>
    /// <typeparam name="TState">作用域狀態的類型。</typeparam>
    /// <param name="state">作用域狀態。</param>
    /// <returns>返回一個可處理作用域的 IDisposable。</returns>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _ScopeProvider.Push(state);

    /// <summary>檢查指定的日誌級別是否啟用。</summary>
    /// <param name="logLevel">要檢查的日誌級別。</param>
    /// <returns>如果指定的日誌級別啟用，則返回 true；否則返回 false。</returns>
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    /// <summary>記錄一條日誌。</summary>
    /// <typeparam name="TState">日誌狀態的類型。</typeparam>
    /// <param name="logLevel">日誌級別。</param>
    /// <param name="eventId">事件 ID。</param>
    /// <param name="state">日誌狀態。</param>
    /// <param name="exception">異常信息。</param>
    /// <param name="formatter">格式化函數，用於將日誌狀態和異常轉換為字符串。</param>
    /// <remarks>如果日誌級別未啟用，則不會記錄任何內容。</remarks>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        var logEntry = new LogEntry
        {
            Timestamp = DateTime.Now,
            LogLevel = logLevel,
            Category = _Provider.Category,
            Message = message,
            Exception = exception?.ToString()
        };
        _Provider.SendLogEntry(logEntry).Wait(); // 等待異步操作完成
    }
}
#endregion

#region Public Static Class : PipeLoggerExtensions
/// <summary>擴充方法。</summary>
public static class PipeLoggerExtensions
{
    /// <summary>新增檔案記錄器。</summary>
    /// <param name="builder">日誌建構器。</param>
    /// <param name="configure">配置選項的委託。</param>
    /// <returns>返回日誌建構器。</returns>
    public static ILoggingBuilder AddPipeLogger(this ILoggingBuilder builder, Action<PipeLoggerOptions>? configure = null)
    {
        configure ??= options => { };
        builder.Services.Configure(configure);
        builder.Services.AddSingleton<IPipeLoggerProvider, PipeLoggerProvider>();
        builder.Services.AddSingleton<ILoggerProvider>(provider => provider.GetRequiredService<IPipeLoggerProvider>());
        return builder;
    }
}
#endregion

#region Class : LogEntry
/// <summary>表示一條日誌記錄的實體。</summary>
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
#endregion
