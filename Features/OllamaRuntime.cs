using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace WindowsTool.Features;

/// <summary>
/// Ollama 的按需生命周期：有功能需要它才拉起，最后一个需要它的功能停掉就收走，
/// 平时不常驻、不占显存。服务端额外带 OLLAMA_KEEP_ALIVE=120s，闲置两分钟自己把模型卸掉。
/// </summary>
public static class OllamaRuntime
{
    public const int Port = 11434;

    private static readonly string[] KillNames = { "ollama", "ollama app" };

    public static bool IsRunning() => PortOpen(Port);

    public static string? ResolveExe()
    {
        var local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "Ollama", "ollama.exe");
        if (File.Exists(local)) return local;

        string? env = Environment.GetEnvironmentVariable("OLLAMA_EXE");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env)) return env;

        return null;
    }

    /// <summary>确保 Ollama 在跑；起不来返回 false（最常见原因是 11434 落在 Hyper-V/Docker 的排除端口区）。</summary>
    public static async Task<bool> EnsureStartedAsync()
    {
        if (IsRunning()) return true;

        var exe = ResolveExe();
        if (exe == null) return false;

        try
        {
            var psi = new ProcessStartInfo(exe, "serve")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = Path.GetDirectoryName(exe)!,
            };
            psi.Environment["OLLAMA_KEEP_ALIVE"] = "120s";
            psi.Environment["OLLAMA_HOST"] = "127.0.0.1:11434";
            Process.Start(psi);
        }
        catch { return false; }

        for (var i = 0; i < 40; i++)
        {
            await Task.Delay(300);
            if (IsRunning()) return true;
        }
        return false;
    }

    /// <summary>卸掉已加载模型再收进程，显存立刻还回去。</summary>
    public static void Stop()
    {
        UnloadModels();
        foreach (var name in KillNames)
        {
            var ps = Process.GetProcessesByName(name);
            OllamaLog.Write($"kill {name}: {ps.Length} procs");
            foreach (var p in ps)
            {
                try { p.Kill(entireProcessTree: true); OllamaLog.Write($"killed {name} pid={p.Id}"); }
                catch (Exception ex) { OllamaLog.Write($"kill fail {name} pid={p.Id}: {ex.Message}"); }
            }
        }
    }

    private static void UnloadModels()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var json = http.GetStringAsync($"http://127.0.0.1:{Port}/api/ps").GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("models", out var models)) return;

            foreach (var m in models.EnumerateArray())
            {
                var name = m.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (string.IsNullOrEmpty(name)) continue;
                var body = new StringContent(
                    JsonSerializer.Serialize(new { model = name, keep_alive = "0s" }),
                    Encoding.UTF8, "application/json");
                try { http.PostAsync($"http://127.0.0.1:{Port}/api/generate", body).Wait(2000); } catch { }
            }
        }
        catch { }
    }

    private static bool PortOpen(int port)
    {
        try
        {
            using var c = new System.Net.Sockets.TcpClient();
            var t = c.ConnectAsync("127.0.0.1", port);
            return t.Wait(300) && c.Connected;
        }
        catch { return false; }
    }
}
