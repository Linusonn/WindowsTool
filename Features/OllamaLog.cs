using System.IO;

namespace WindowsTool.Features;

/// <summary>临时诊断日志：Ollama 依赖启停链路。写在发布目录下 wt_ollama.log。</summary>
public static class OllamaLog
{
    private static readonly object Lock = new();
    private static readonly string Path =
        System.IO.Path.Combine(AppContext.BaseDirectory, "wt_ollama.log");

    public static void Write(string msg)
    {
        try
        {
            lock (Lock)
                File.AppendAllText(Path, $"{DateTime.Now:MM-dd HH:mm:ss.fff} {msg}\r\n");
        }
        catch { }
    }
}
