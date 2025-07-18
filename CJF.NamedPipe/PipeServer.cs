using System.IO.Pipes;
using System.Text;
using Microsoft.Extensions.Logging;

namespace CJF.NamedPipe;

/// <summary>用於與客戶端進行命名管道通信的服務端</summary>
public class PipeServer
{
    private readonly PipeCommandHandler _CommandHandler;
    private readonly PipeStreamCommandHandler? _StreamHandler;
    private readonly PipeLineOptions _Options;
    private readonly ILogger<PipeServer>? _Logger;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private Task? _CommandServerTask;
    private Task? _StreamServerTask;
    private bool _IsRunning;

    /// <summary>初始化管道服務器</summary>
    /// <param name="commandHandler">處理命令的回調</param>
    /// <param name="streamHandler">處理串流命令的回調</param>
    /// <param name="options">管道配置選項</param>
    /// <param name="logger">日誌記錄器</param>
    public PipeServer(PipeCommandHandler commandHandler, PipeStreamCommandHandler? streamHandler = null, PipeLineOptions? options = null, ILogger<PipeServer>? logger = null)
    {
        _CommandHandler = commandHandler ?? throw new ArgumentNullException(nameof(commandHandler));
        _StreamHandler = streamHandler;
        _Options = options ?? new PipeLineOptions();
        _Logger = logger;
        EnsureServiceFlagDirectoryExists();
    }

    /// <summary>啟動服務器</summary>
    public void Start()
    {
        if (_IsRunning)
            return;

        _IsRunning = true;

        // 創建標誌文件表示服務正在執行
        EnsureServiceFlagDirectoryExists();
        File.WriteAllText(_Options.ServiceFlagFilePath, DateTime.Now.ToString());
        _Logger?.LogInformation("命名管道服務已啟動，標誌文件: {FlagFile}", _Options.ServiceFlagFilePath);

        // 啟動一般命令監聽任務
        _CommandServerTask = Task.Run(() => ListenForCommandClientsAsync());

        // 啟動串流命令監聽任務
        _StreamServerTask = Task.Run(() => ListenForStreamClientsAsync());
    }

    /// <summary>停止服務器</summary>
    public async Task StopAsync()
    {
        if (!_IsRunning)
            return;

        _IsRunning = false;
        _cancellationTokenSource.Cancel();

        // 等待服務器任務完成
        var tasks = new List<Task>();
        if (_CommandServerTask != null)
            tasks.Add(_CommandServerTask);
        if (_StreamServerTask != null)
            tasks.Add(_StreamServerTask);

        if (tasks.Count > 0)
        {
            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                // 預期的取消異常，忽略
            }
            catch
            {
                // 忽略其他異常
            }
        }

        // 刪除標誌文件
        try
        {
            if (File.Exists(_Options.ServiceFlagFilePath))
            {
                File.Delete(_Options.ServiceFlagFilePath);
                _Logger?.LogInformation("已刪除服務標誌文件: {FlagFile}", _Options.ServiceFlagFilePath);
            }
        }
        catch (Exception ex)
        {
            _Logger?.LogWarning(ex, "刪除服務標誌文件時發生錯誤: {FlagFile}", _Options.ServiceFlagFilePath);
        }
    }

    /// <summary>監聽一般命令客戶端連接</summary>
    private async Task ListenForCommandClientsAsync()
    {
        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                var server = new NamedPipeServerStream(
                    _Options.CommandPipeName,
                    PipeDirection.InOut,
                    _Options.MaxClients == -1 ? NamedPipeServerStream.MaxAllowedServerInstances : _Options.MaxClients,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                // 等待客戶端連接
                await server.WaitForConnectionAsync(_cancellationTokenSource.Token);

                // 處理一般命令請求（同步處理以確保服務器流不會過早關閉）
                await HandleCommandRequestAsync(server);

                // 處理完成後關閉服務器流
                server.Dispose();
            }
            catch (OperationCanceledException)
            {
                // 預期的取消異常，退出循環
                break;
            }
            catch (Exception ex)
            {
                _Logger?.LogError(ex, "處理一般命令客戶端連接時發生錯誤");
                // 短暫延遲以避免在錯誤情況下的快速循環
                try
                {
                    await Task.Delay(1000, _cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    /// <summary>監聽串流命令客戶端連接</summary>
    private async Task ListenForStreamClientsAsync()
    {
        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
            var server = new NamedPipeServerStream(
                _Options.StreamPipeName,
                PipeDirection.InOut,
                _Options.MaxClients == -1 ? NamedPipeServerStream.MaxAllowedServerInstances : _Options.MaxClients,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

                // 等待客戶端連接
                await server.WaitForConnectionAsync(_cancellationTokenSource.Token);

                // 處理串流命令請求（異步處理，因為串流可能需要長時間運行）
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await HandleStreamRequestAsync(server);
                    }
                    finally
                    {
                        // 確保服務器流在處理完成後被關閉
                        server.Dispose();
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // 預期的取消異常，退出循環
                break;
            }
            catch (Exception ex)
            {
                _Logger?.LogError(ex, "處理串流命令客戶端連接時發生錯誤");
                // 短暫延遲以避免在錯誤情況下的快速循環
                try
                {
                    await Task.Delay(1000, _cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    /// <summary>處理一般命令請求</summary>
    /// <remarks>讀取消息長度和內容，解析命令並執行</remarks>
    /// <param name="server">命名管道服務器流</param>
    /// <returns>處理結果</returns>
    private async Task HandleCommandRequestAsync(NamedPipeServerStream server)
    {
        try
        {
            // 檢查連接是否仍然有效
            if (!server.IsConnected)
            {
                _Logger?.LogDebug("客戶端連接已斷開，跳過處理");
                return;
            }

            // 讀取並驗證消息
            var message = await ReadAndValidateMessageAsync(server);
            if (message == null)
                return;

            // 處理一般命令
            string response = await _CommandHandler(message.Command, message.Arguments);
            await SendResponseAsync(server, response);
        }
        catch (EndOfStreamException)
        {
            // 客戶端提前關閉連接，這是正常情況（如連接測試）
            _Logger?.LogDebug("客戶端提前關閉連接");
        }
        catch (IOException ex) when (IsConnectionInterrupted(ex))
        {
            // 管道連接中斷，這是正常情況
            _Logger?.LogDebug("管道連接中斷: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            _Logger?.LogError(ex, "處理一般命令請求時發生錯誤");
        }
    }

    /// <summary>處理串流命令請求</summary>
    /// <remarks>讀取消息長度和內容，解析命令並執行</remarks>
    /// <param name="server">命名管道服務器流</param>
    /// <returns>處理結果</returns>
    private async Task HandleStreamRequestAsync(NamedPipeServerStream server)
    {
        try
        {
            // 檢查連接是否仍然有效
            if (!server.IsConnected)
            {
                _Logger?.LogDebug("客戶端連接已斷開，跳過處理");
                return;
            }

            // 讀取並驗證消息
            var message = await ReadAndValidateMessageAsync(server);
            if (message == null)
                return;

            // 處理串流命令
            if (_StreamHandler != null)
                await HandleStreamCommandAsync(server, message);
            else
                await SendStreamErrorAsync(server, "串流命令處理器未設定");
        }
        catch (EndOfStreamException)
        {
            // 客戶端提前關閉連接，這是正常情況（如連接測試）
            _Logger?.LogDebug("客戶端提前關閉連接");
        }
        catch (IOException ex) when (IsConnectionInterrupted(ex))
        {
            // 管道連接中斷，這是正常情況
            _Logger?.LogDebug("管道連接中斷: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            _Logger?.LogError(ex, "處理串流命令請求時發生錯誤");
        }
    }

    /// <summary>處理串流命令</summary>
    /// <remarks>使用串流寫入器發送數據</remarks>
    /// <param name="server">命名管道服務器流</param>
    /// <param name="message">命令消息</param>
    /// <returns>處理結果</returns>
    private async Task HandleStreamCommandAsync(NamedPipeServerStream server, CommandMessage message)
    {
        var cancellationToken = _cancellationTokenSource.Token;

        // 創建串流寫入器
        async Task<bool> StreamWriter(StreamMessage streamMessage)
        {
            try
            {
                string json = streamMessage.Serialize();
                byte[] buffer = Encoding.UTF8.GetBytes(json);

                // 寫入訊息長度和內容
                await server.WriteAsync(BitConverter.GetBytes(buffer.Length).AsMemory(0, 4), cancellationToken);
                await server.WriteAsync(buffer, cancellationToken);
                await server.FlushAsync(cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                _Logger?.LogError(ex, "串流寫入時發生錯誤");
                return false;
            }
        }

        try
        {
            // 執行串流命令
            await _StreamHandler!(message.Command, message.Arguments, StreamWriter, cancellationToken);
        }
        catch (Exception ex)
        {
            // 發送錯誤訊息
            await StreamWriter(new StreamMessage { Content = $"執行命令時發生錯誤: {ex.Message}", Type = StreamMessageTypes.Error, IsFinished = true });
        }
    }

    /// <summary>發送一般回應</summary>
    /// <remarks>將回應轉換為字節並發送給客戶端</remarks>
    /// <param name="server">命名管道服務器流</param>
    /// <param name="response">回應內容</param>
    private async Task SendResponseAsync(NamedPipeServerStream server, string response)
    {
        try
        {
            byte[] responseBuffer = Encoding.UTF8.GetBytes(response);
            await server.WriteAsync(BitConverter.GetBytes(responseBuffer.Length).AsMemory(0, 4));
            await server.WriteAsync(responseBuffer);
            await server.FlushAsync();
        }
        catch (Exception ex)
        {
            _Logger?.LogError(ex, "發送回應時發生錯誤");
        }
    }

    /// <summary>發送串流錯誤訊息</summary>
    /// <remarks>將錯誤訊息轉換為串流格式並發送給客戶端</remarks>
    /// <param name="server">命名管道服務器流</param>
    /// <param name="errorMessage">錯誤訊息</param>
    private async Task SendStreamErrorAsync(NamedPipeServerStream server, string errorMessage)
    {
        try
        {
            var streamMessage = new StreamMessage
            {
                Content = errorMessage,
                Type = StreamMessageTypes.Error,
                IsFinished = true
            };

            string json = streamMessage.Serialize();
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            await server.WriteAsync(BitConverter.GetBytes(buffer.Length).AsMemory(0, 4));
            await server.WriteAsync(buffer);
            await server.FlushAsync();
        }
        catch (Exception ex)
        {
            _Logger?.LogError(ex, "發送串流錯誤訊息時發生錯誤");
        }
    }
    /// <summary>讀取並驗證消息</summary>
    /// <param name="server">命名管道服務器流</param>
    /// <returns>解析後的命令消息，如果無效則返回 null</returns>
    private async Task<CommandMessage?> ReadAndValidateMessageAsync(NamedPipeServerStream server)
    {
        // 讀取消息長度
        byte[] lengthBuffer = new byte[4];
        int bytesRead = await server.ReadAsync(lengthBuffer.AsMemory(0, 4));
        
        // 如果沒有讀取到足夠的數據，可能是連接測試
        if (bytesRead < 4)
        {
            _Logger?.LogDebug("收到不完整的數據，可能是連接測試");
            return null;
        }

        int messageLength = BitConverter.ToInt32(lengthBuffer, 0);
        
        // 驗證消息長度的合理性
        if (messageLength <= 0 || messageLength > 1024 * 1024) // 限制為 1MB
        {
            _Logger?.LogWarning("收到無效的消息長度: {Length}", messageLength);
            return null;
        }

        // 讀取消息內容
        byte[] messageBuffer = new byte[messageLength];
        await server.ReadExactlyAsync(messageBuffer);
        string json = Encoding.UTF8.GetString(messageBuffer);

        // 解析命令消息
        var message = CommandMessage.Deserialize(json);
        if (message == null)
        {
            _Logger?.LogWarning("收到無效的命令格式");
        }

        return message;
    }

    /// <summary>檢查是否為連接中斷異常</summary>
    /// <param name="ex">IO 異常</param>
    /// <returns>如果是連接中斷異常則返回 true</returns>
    private static bool IsConnectionInterrupted(IOException ex)
    {
        return ex.Message.Contains("Broken pipe") || 
               ex.Message.Contains("Connection reset") ||
               ex.Message.Contains("管道已結束");
    }

    /// <summary>確保服務標誌文件的目錄存在</summary>
    private void EnsureServiceFlagDirectoryExists()
    {
        string directory = Path.GetDirectoryName(_Options.ServiceFlagFilePath)!;
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
    }
}
