
using Microsoft.Extensions.Logging;

namespace CJF.NamedPipe.Logging;

#region Public Interface : IPipeLoggerProvider
/// <summary>命名管道記錄器提供者介面。</summary>
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
#endregion
