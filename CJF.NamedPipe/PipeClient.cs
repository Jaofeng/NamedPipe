using System.IO.Pipes;
using System.Text;

namespace CJF.NamedPipe;

/// <summary>命名管道回覆狀態常量</summary>
public enum PipeResults
{
    /// <summary>成功</summary>
    Success,
    /// <summary>失敗</summary>
    Failure,
    /// <summary>服務未運行</summary>
    ServiceNotRunning,
    /// <summary>連接錯誤</summary>
    ConnectionError,
    /// <summary>命令錯誤</summary>
    CommandError,
    /// <summary>未知命令</summary>
    UnknownCommand,
    /// <summary>超時</summary>
    Timeout
}

/// <summary>用於與服務進行命名管道通信的客戶端</summary>
public class PipeClient
{
    private readonly IPipeLineProvider _Provider;

    /// <summary>一般命令管道名稱。</summary>
    public string CommandPipeName => _Provider.Options.CommandPipeName;
    /// <summary>串流命令管道名稱。</summary>
    public string StreamPipeName => _Provider.Options.StreamPipeName;


    /// <summary>初始化新的命名管道客戶端實例</summary>
    /// <param name="provider">命名管道提供者</param>
    internal PipeClient(IPipeLineProvider provider)
    {
        _Provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    /// <summary>發送命令到服務並獲取響應</summary>
    /// <param name="command">要發送的命令</param>
    /// <param name="args">命令參數</param>
    /// <returns>服務的響應</returns>
    public async Task<(PipeResults Result, string Message)> SendCommandAsync(string command, params string[] args)
    {
        return await SendCommandAsync(command, false, null, args);
    }

    /// <summary>發送串流命令到服務並持續接收回應</summary>
    /// <param name="command">要發送的命令</param>
    /// <param name="streamHandler">串流訊息處理器</param>
    /// <param name="args">命令參數</param>
    /// <returns>服務的最終響應</returns>
    public async Task<(PipeResults Result, string Message)> SendStreamCommandAsync(string command, Action<StreamMessage> streamHandler, params string[] args)
    {
        return await SendCommandAsync(command, true, streamHandler, args);
    }

    /// <summary>發送命令到服務並獲取響應（內部方法）</summary>
    /// <param name="command">要發送的命令</param>
    /// <param name="isStreaming">是否為串流命令</param>
    /// <param name="streamHandler">串流訊息處理器</param>
    /// <param name="args">命令參數</param>
    /// <returns>服務的響應</returns>
    private async Task<(PipeResults Result, string Message)> SendCommandAsync(string command, bool isStreaming, Action<StreamMessage>? streamHandler, params string[] args)
    {
        // 檢查服務是否正在執行
        if (!_Provider.IsServiceRunning())
            return (PipeResults.ServiceNotRunning, "服務未執行。請先啟動服務。");

        try
        {
            // 根據指令類型選擇不同的管道
            var pipeName = isStreaming ? StreamPipeName : CommandPipeName;
            var connectionTimeout = _Provider.Options.ConnectionTimeoutMs;
            var readWriteTimeout = _Provider.Options.ReadWriteTimeoutMs;

            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
            
            // 嘗試連接到服務，使用配置的超時時間
            using var connectionCts = new CancellationTokenSource(connectionTimeout);
            try
            {
                await client.ConnectAsync(connectionCts.Token);
            }
            catch (OperationCanceledException) when (connectionCts.IsCancellationRequested)
            {
                return (PipeResults.Timeout, "連接超時，請確保服務正在執行。");
            }

            if (!client.IsConnected)
                return (PipeResults.ConnectionError, "無法連接到服務。");

            // 創建並發送命令消息
            var message = new CommandMessage
            {
                Command = command,
                Arguments = args,
                IsStreaming = isStreaming
            };

            return await SendMessageAndHandleResponseAsync(client, message, isStreaming, streamHandler, readWriteTimeout);
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    /// <summary>發送訊息並處理回應</summary>
    /// <param name="client">命名管道客戶端</param>
    /// <param name="message">要發送的命令消息</param>
    /// <param name="isStreaming">是否為串流命令</param>
    /// <param name="streamHandler">串流訊息處理器</param>
    /// <param name="readWriteTimeout">讀寫超時時間</param>
    /// <returns>服務的響應</returns>
    private static async Task<(PipeResults Result, string Message)> SendMessageAndHandleResponseAsync(
        NamedPipeClientStream client, 
        CommandMessage message, 
        bool isStreaming, 
        Action<StreamMessage>? streamHandler, 
        int readWriteTimeout)
    {
        string json = message.Serialize();
        byte[] buffer = Encoding.UTF8.GetBytes(json);
        
        using var writeCts = new CancellationTokenSource(readWriteTimeout);
        try
        {
            // 寫入消息長度和內容
            await client.WriteAsync(BitConverter.GetBytes(buffer.Length).AsMemory(0, 4), writeCts.Token);
            await client.WriteAsync(buffer, writeCts.Token);
            await client.FlushAsync(writeCts.Token);
        }
        catch (OperationCanceledException) when (writeCts.IsCancellationRequested)
        {
            return (PipeResults.Timeout, "寫入命令超時。");
        }
        catch (IOException ex)
        {
            return (PipeResults.ConnectionError, $"寫入命令時發生錯誤: {ex.Message}");
        }

        // 處理回應
        if (isStreaming && streamHandler != null)
        {
            return await HandleStreamResponseAsync(client, streamHandler);
        }
        else
        {
            return await HandleNormalResponseAsync(client, readWriteTimeout);
        }
    }

    /// <summary>處理異常並返回適當的結果</summary>
    private static (PipeResults Result, string Message) HandleException(Exception ex)
    {
        return ex switch
        {
            OperationCanceledException oce when oce.CancellationToken.IsCancellationRequested 
                => (PipeResults.Timeout, "操作被取消。"),
            OperationCanceledException 
                => (PipeResults.Timeout, "連接超時，請確保服務正在執行。"),
            TimeoutException 
                => (PipeResults.Timeout, "連接超時，請確保服務正在執行。"),
            IOException 
                => (PipeResults.ConnectionError, "與服務通信時發生錯誤，請確保服務正在運行。"),
            UnauthorizedAccessException 
                => (PipeResults.ConnectionError, "無法訪問命名管道，請檢查權限。"),
            _ => (PipeResults.Failure, $"與服務通信時發生錯誤: {ex.Message}")
        };
    }


    /// <summary>等待服務啟動，直到超時或服務可用。</summary>
    /// <param name="timeoutMilliseconds">等待的超時時間，默認為3000毫秒。</param>
    /// <returns>如果服務在超時前可用，則返回 true；否則返回 false。</returns>
    public async Task<bool> WaitForServiceAsync(int timeoutMilliseconds = 3000)
    {
        int elapsed = 0;
        while (elapsed < timeoutMilliseconds)
        {
            if (await TestPipeConnection())
                return true;
            await Task.Delay(100);
            elapsed += 100;
        }
        return false;
    }

    /// <summary>測試命名管道連接是否可用。</summary>
    /// <returns>如果連接成功，則返回 true；否則返回 false。</returns>
    public async Task<bool> TestPipeConnection()
    {
        try
        {
            // 測試一般命令管道
            using var commandClient = new NamedPipeClientStream(".", CommandPipeName, PipeDirection.InOut);
            using var cts1 = new CancellationTokenSource(500);
            await commandClient.ConnectAsync(cts1.Token);

            // 測試串流命令管道
            using var streamClient = new NamedPipeClientStream(".", StreamPipeName, PipeDirection.InOut);
            using var cts2 = new CancellationTokenSource(500);
            await streamClient.ConnectAsync(cts2.Token);

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>處理一般回應</summary>
    /// <param name="client">命名管道客戶端</param>
    /// <param name="readWriteTimeout">讀寫超時時間，默認為5000毫秒</param>
    /// <returns>服務的響應</returns>
    private static async Task<(PipeResults Result, string Message)> HandleNormalResponseAsync(NamedPipeClientStream client, int readWriteTimeout = 5000)
    {
        // 讀取響應長度
        byte[] lengthBuffer = new byte[4];
        // 使用 CancellationTokenSource 設置超時
        using var cts = new CancellationTokenSource(readWriteTimeout);
        try
        {
            await client.ReadExactlyAsync(lengthBuffer, cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            return (PipeResults.Timeout, "\x1B[91m讀取響應超時。\x1B[39m");
        }
        int responseLength = BitConverter.ToInt32(lengthBuffer, 0);

        // 讀取響應內容
        byte[] responseBuffer = new byte[responseLength];
        cts.CancelAfter(5000); // 設置超時為5秒
        try
        {
            await client.ReadExactlyAsync(responseBuffer, cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            return (PipeResults.Timeout, "\x1B[91m讀取響應超時。\x1B[39m");
        }
        var responseMessage = Encoding.UTF8.GetString(responseBuffer);

        if (string.IsNullOrWhiteSpace(responseMessage))
        {
            return (PipeResults.Failure, "\x1B[91m服務返回空響應。\x1B[39m");
        }
        else if (responseMessage.StartsWith("Fail: "))
        {
            // 如果響應以 "Fail:" 開頭，則視為命令錯誤
            return (PipeResults.Failure, responseMessage["Fail: ".Length..].Trim());
        }
        else if (responseMessage.StartsWith("Error: "))
        {
            // 如果響應以 "Error:" 開頭，則視為命令錯誤
            return (PipeResults.CommandError, responseMessage["Error: ".Length..].Trim());
        }
        else if (responseMessage.StartsWith("UnknownCommand: "))
        {
            // 如果響應以 "UnknownCommand:" 開頭，則視為未知命令
            return (PipeResults.UnknownCommand, responseMessage["UnknownCommand: ".Length..].Trim());
        }
        else
        {
            // 正常響應，返回成功和解碼的消息
            return (PipeResults.Success, responseMessage);
        }
    }

    /// <summary>處理串流回應</summary>
    private static async Task<(PipeResults Result, string Message)> HandleStreamResponseAsync(NamedPipeClientStream client, Action<StreamMessage> streamHandler)
    {
        try
        {
            string lastMessage = string.Empty;
            while (true)
            {
                // 讀取訊息長度
                byte[] lengthBuffer = new byte[4];
                await client.ReadExactlyAsync(lengthBuffer);
                int messageLength = BitConverter.ToInt32(lengthBuffer, 0);

                // 讀取訊息內容
                byte[] messageBuffer = new byte[messageLength];
                await client.ReadExactlyAsync(messageBuffer);
                string json = Encoding.UTF8.GetString(messageBuffer);

                // 解析串流訊息
                var streamMessage = StreamMessage.Deserialize(json);
                if (streamMessage == null)
                    return (PipeResults.Failure, "無效的串流訊息格式。");

                // 處理串流訊息
                streamHandler(streamMessage);

                // 如果是結束訊息，退出循環
                if (streamMessage.IsFinished)
                {
                    lastMessage = streamMessage.Content;
                    break;
                }
            }

            return (PipeResults.Success, lastMessage);
        }
        catch (Exception ex)
        {
            return (PipeResults.Failure, $"處理串流回應時發生錯誤: {ex.Message}");
        }
    }
}
