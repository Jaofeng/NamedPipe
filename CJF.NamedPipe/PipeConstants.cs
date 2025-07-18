namespace CJF.NamedPipe;

/// <summary>定義管道通信的常量</summary>
public static class PipeConstants
{
    /// <summary>一般命令的命名管道名稱</summary>
    public const string CommandPipeName = "CJF.NamedPipe.Command.Pipe";

    /// <summary>串流命令的命名管道名稱</summary>
    public const string StreamPipeName = "CJF.NamedPipe.Stream.Pipe";

    /// <summary>應用程式資料檔案路徑</summary>
    public static string ApplicationDataPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CJF.NamedPipe");

    /// <summary>服務執行的標誌文件路徑</summary>
    public static string ServiceFlagFilePath => Path.Combine(ApplicationDataPath, "CJF.NamedPipe.flag");

    /// <summary>確保服務標誌文件的目錄存在</summary>
    public static void EnsureServiceFlagDirectoryExists()
    {
        string directory = Path.GetDirectoryName(ServiceFlagFilePath)!;
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
    }
}
