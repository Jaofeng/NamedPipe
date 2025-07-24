using Microsoft.Extensions.Options;

namespace CJF.NamedPipe;

/// <summary>用於處理一般命令的委託</summary>
/// <param name="command">命令</param>
/// <param name="args">參數</param>
/// <returns>處理結果</returns>
public delegate Task<string> PipeCommandHandler(string command, string[] args);

/// <summary>用於處理串流命令的委託</summary>
/// <param name="command">命令</param>
/// <param name="args">參數</param>
/// <param name="streamWriter">串流寫入器</param>
/// <param name="cancellationToken">取消令牌</param>
/// <returns>處理結果</returns>
public delegate Task PipeStreamCommandHandler(string command, string[] args, Func<StreamMessage, Task<bool>> streamWriter, CancellationToken cancellationToken);

/// <summary>命名管道(NamedPipe)的配置選項。</summary>
public sealed class PipeLineOptions
{
    /// <summary>獲取或設置一般命令管道名稱。</summary>
    public string CommandPipeName { get; set; } = $"{AppDomain.CurrentDomain.FriendlyName}.Command.Pipe";

    /// <summary>獲取或設置串流命令管道名稱。</summary>
    public string StreamPipeName { get; set; } = $"{AppDomain.CurrentDomain.FriendlyName}.Stream.Pipe";

    /// <summary>獲取或設置管道的最大客戶端數量。</summary>
    public int MaxClients { get; set; } = -1;

    /// <summary>獲取或設置服務執行的標誌文件路徑。</summary>
    public string ServiceFlagFilePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppDomain.CurrentDomain.FriendlyName, AppDomain.CurrentDomain.FriendlyName + ".flag");

    /// <summary>獲取或設置連接超時時間（毫秒）。</summary>
    public int ConnectionTimeoutMs { get; set; } = 5000;

    /// <summary>獲取或設置讀寫超時時間（毫秒）。</summary>
    public int ReadWriteTimeoutMs { get; set; } = 5000;
}
