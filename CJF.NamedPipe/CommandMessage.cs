using System.Text.Json;
using System.Text.Json.Serialization;

namespace CJF.NamedPipe;

/// <summary>表示在CLI和服務之間傳遞的命令消息</summary>
public class CommandMessage
{
    /// <summary>命令類型</summary>
    [JsonPropertyName("command")]
    public string Command { get; set; } = string.Empty;

    /// <summary>命令參數</summary>
    [JsonPropertyName("args")]
    public string[] Arguments { get; set; } = [];

    /// <summary>是否為串流命令</summary>
    [JsonPropertyName("streaming")]
    public bool IsStreaming { get; set; } = false;

    /// <summary>將消息序列化為JSON字符串</summary>
    /// <returns>序列化後的JSON字符串</returns>
    public string Serialize() => JsonSerializer.Serialize(this);

    /// <summary>從JSON字符串反序列化消息</summary>
    /// <param name="json">要反序列化的JSON字符串</param>
    /// <returns>反序列化後的CommandMessage對象，或null如果反序列化失敗</returns>
    public static CommandMessage? Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<CommandMessage>(json);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>表示串流消息的類型</summary>
public enum StreamMessageTypes
{
    /// <summary>追蹤訊息</summary>
    Trace,
    /// <summary>除錯訊息</summary>
    Debug,
    /// <summary>資訊訊息</summary>
    Info,
    /// <summary>警告訊息</summary>
    Warning,
    /// <summary>錯誤訊息</summary>
    Error,
    /// <summary>成功訊息</summary>
    Success
}

/// <summary>表示串流回應訊息</summary>
public class StreamMessage
{
    /// <summary>訊息內容</summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>是否為結束訊息</summary>
    /// <remarks>預設為 <see langword="false"/></remarks>
    [JsonPropertyName("finished")]
    public bool IsFinished { get; set; } = false;

    /// <summary>訊息類型 (Info, Warning, Error, Success)</summary>
    /// <remarks>默認為 <see cref="StreamMessageTypes.Info"/></remarks>
    [JsonPropertyName("type")]
    public StreamMessageTypes Type { get; set; } = StreamMessageTypes.Info;
    /// <summary>默認構造函數</summary>
    public StreamMessage() { }
    /// <summary>帶參數的構造函數</summary>
    /// <param name="content">訊息內容</param>
    /// <param name="type">訊息類型，默認為 <see cref="StreamMessageTypes.Info"/></param>
    /// <param name="isFinished">是否為結束訊息，預設為 <see langword="false"/></param>
    public StreamMessage(string content, StreamMessageTypes type = StreamMessageTypes.Info, bool isFinished = false)
    {
        Content = content;
        Type = type;
        IsFinished = isFinished;
    }

    /// <summary>將消息序列化為JSON字符串</summary>
    /// <returns>序列化後的JSON字符串</returns>
    public string Serialize() => JsonSerializer.Serialize(this);

    /// <summary>從JSON字符串反序列化消息</summary>
    /// <param name="json">要反序列化的JSON字符串</param>
    /// <returns>反序列化後的StreamMessage對象，或null如果反序列化失敗</returns>
    public static StreamMessage? Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<StreamMessage>(json);
        }
        catch
        {
            return null;
        }
    }
}
