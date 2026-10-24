using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Windows;

namespace WindowsTool.Features;

/// <summary>
/// 通用"进程型"功能：按配置启动一个可执行文件/脚本，按 PID 或进程名结束它，
/// 状态用 PID / 进程名 / 端口三种方式探测。
/// </summary>
public class ProcessFeature : IFeature
{
    private readonly FeatureConfig _cfg;
    private readonly string _pidDir;
    private FeatureState _state = FeatureState.Unknown;
    private string _statusText = "未知";
    private bool _busy;
    private string? _lastError;

    public ProcessFeature(FeatureConfig cfg, string pidDir)
    {
        _cfg = cfg;
        _pidDir = pidDir;
    }

    public string Id => _cfg.Id;
    public string Name => _cfg.Name;
    public string Description => _cfg.Description;
    public string Category => string.IsNullOrWhiteSpace(_cfg.Category) ? "其它" : _cfg.Category;
    public FeatureState State => _state;
    public string StatusText => _statusText;
    public bool IsBusy => _busy;
    public bool CanStart => !_busy && _state != FeatureState.Running && _state != FeatureState.Starting;
    public bool CanStop => !_busy && (_state == FeatureState.Running || _state == FeatureState.Error || _state == FeatureState.Unknown);
    public bool RequiresOllama => _cfg.RequiresOllama;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    private void Set(FeatureState st, string text)
    {
        void apply()
        {
            _state = st;
            _statusText = text;
            Raise(nameof(State));
            Raise(nameof(StatusText));
            Raise(nameof(CanStart));
            Raise(nameof(CanStop));
            Raise(nameof(IsBusy));
        }

        var app = Application.Current;
        if (app?.Dispatcher != null && !app.Dispatcher.CheckAccess())
            app.Dispatcher.Invoke(apply);
        else
            apply();
    }

    // ---------------- PID 记录 ----------------
    private string PidFile => Path.Combine(_pidDir, $"{_cfg.Id}.pid");

    private void SavePid(int pid)
    {
        try { Directory.CreateDirectory(_pidDir); File.WriteAllText(PidFile, pid.ToString()); }
        catch { /* 记不下来也不致命 */ }
    }

    private int LoadPid()
    {
        try
        {
            if (File.Exists(PidFile) && int.TryParse(File.ReadAllText(PidFile).Trim(), out var pid))
                return pid;
        }
        catch { }
        return -1;
    }

    private void ClearPid()
    {
        try { if (File.Exists(PidFile)) File.Delete(PidFile); } catch { }
    }

    private static bool IsAlive(int pid)
    {
        try { using var p = Process.GetProcessById(pid); return !p.HasExited; }
        catch { return false; }
    }

    // ---------------- 启停 ----------------
    public async Task StartAsync()
    {
        if (_cfg.Start == null || string.IsNullOrWhiteSpace(_cfg.Start.File))
        {
            Set(FeatureState.Error, "未配置启动命令");
            return;
        }
        if (_state == FeatureState.Running) return;

        _busy = true; Raise(nameof(IsBusy)); Raise(nameof(CanStart)); Raise(nameof(CanStop));
        Set(FeatureState.Starting, "启动中…");

        // 依赖 Ollama 的功能：先把 Ollama 拉起来，起不来就别装成功
        OllamaLog.Write($"start {_cfg.Id} requiresOllama={_cfg.RequiresOllama}");
        if (_cfg.RequiresOllama)
        {
            Set(FeatureState.Starting, "启动 Ollama…");
            var ok = await OllamaRuntime.EnsureStartedAsync();
            OllamaLog.Write($"ensure-ollama for {_cfg.Id} => {ok}");
            if (!ok)
            {
                _busy = false; Raise(nameof(IsBusy)); Raise(nameof(CanStart)); Raise(nameof(CanStop));
                Set(FeatureState.Error, "Ollama 起不来：11434 可能被 Hyper-V/Docker 占走");
                return;
            }
        }

        try
        {
            var spec = _cfg.Start;
            var psi = new ProcessStartInfo
            {
                FileName = spec.File,
                Arguments = spec.Args ?? "",
                UseShellExecute = spec.UseShellExecute,
                CreateNoWindow = !spec.UseShellExecute,
                WindowStyle = spec.UseShellExecute ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden,
            };
            if (!string.IsNullOrWhiteSpace(spec.WorkingDirectory) && Directory.Exists(spec.WorkingDirectory))
                psi.WorkingDirectory = spec.WorkingDirectory;

            if (spec.Env is { Count: > 0 })
                foreach (var kv in spec.Env)
                    psi.Environment[kv.Key] = kv.Value;

            if (spec.RunAsAdmin)
            {
                psi.UseShellExecute = true;
                psi.Verb = "runas";
            }

            var proc = Process.Start(psi);
            if (proc != null) SavePid(proc.Id);
            _lastError = null;

            // 给程序一点启动时间再判定状态
            await Task.Delay(1500);
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            Set(FeatureState.Error, $"启动失败：{Short(ex.Message)}");
        }
        finally
        {
            _busy = false; Raise(nameof(IsBusy)); Raise(nameof(CanStart)); Raise(nameof(CanStop));
        }
        await RefreshAsync();
    }

    public async Task StopAsync()
    {
        OllamaLog.Write($"stop {_cfg.Id} requiresOllama={_cfg.RequiresOllama}");
        _busy = true; Raise(nameof(IsBusy)); Raise(nameof(CanStart)); Raise(nameof(CanStop));
        Set(FeatureState.Stopping, "停止中…");
        try
        {
            var stop = _cfg.Stop;

            // 1) 自定义停止命令（docker stop 之类）
            if (stop != null && !string.IsNullOrWhiteSpace(stop.Command))
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = stop.Command,
                        Arguments = stop.CommandArgs ?? "",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    };
                    using var cp = Process.Start(psi);
                    if (cp != null) await Task.WhenAny(cp.WaitForExitAsync(), Task.Delay(15000));
                }
                catch (Exception ex) { _lastError = ex.Message; }
            }

            // 2) 按记录的 PID 杀
            var killed = false;
            if (stop?.KillByPid != false)
            {
                var pid = LoadPid();
                if (pid > 0)
                {
                    if (IsAlive(pid)) { TryKill(pid); killed = true; }
                    ClearPid();
                }
            }

            // 3) 兜底按进程名杀
            if (stop?.KillProcessNames is { Count: > 0 })
            {
                foreach (var raw in stop.KillProcessNames)
                {
                    var name = raw.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                        ? raw[..^4] : raw;
                    foreach (var p in Process.GetProcessesByName(name))
                    {
                        try { p.Kill(entireProcessTree: true); killed = true; } catch { }
                    }
                }
            }

            if (!killed && _state == FeatureState.Stopped)
            {
                // 本来就没在跑
            }
            await Task.Delay(700);

            // 依赖 Ollama 的功能停掉后，如果没人再需要它，就把 Ollama 收走（省显存）
            if (_cfg.RequiresOllama) await OllamaCoordinator.ReleaseAsync(this);
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
        }
        finally
        {
            _busy = false; Raise(nameof(IsBusy)); Raise(nameof(CanStart)); Raise(nameof(CanStop));
        }
        await RefreshAsync();
    }

    private static void TryKill(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            p.Kill(entireProcessTree: true);
        }
        catch { }
    }

    // ---------------- 状态探测 ----------------
    public async Task RefreshAsync()
    {
        await Task.Run(() =>
        {
            var det = _cfg.Detect;
            if (det == null)
            {
                Set(FeatureState.Unknown, "未配置探测方式");
                return;
            }

            var hits = new List<string>();
            var total = 0;

            // PID
            if (_cfg.Stop?.KillByPid != false)
            {
                total++;
                var pid = LoadPid();
                if (pid > 0)
                {
                    if (IsAlive(pid)) hits.Add($"PID {pid}");
                    else ClearPid();
                }
            }

            // 进程名
            if (det.ProcessNames is { Count: > 0 })
            {
                total++;
                foreach (var raw in det.ProcessNames)
                {
                    var name = raw.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? raw[..^4] : raw;
                    try
                    {
                        var ps = Process.GetProcessesByName(name);
                        if (ps.Length > 0) { hits.Add($"{name}×{ps.Length}"); break; }
                    }
                    catch { }
                }
            }

            // 端口
            if (det.Port is int port and > 0)
            {
                total++;
                if (IsPortOpen(port)) hits.Add($"端口 {port}");
            }

            var running = det.MatchAll ? hits.Count == total && total > 0 : hits.Count > 0;
            var detail = hits.Count > 0 ? string.Join(" · ", hits) : "已停止";
            Set(running ? FeatureState.Running : FeatureState.Stopped, detail);
        });
    }

    private static bool IsPortOpen(int port)
    {
        try
        {
            using var c = new TcpClient();
            var task = c.ConnectAsync("127.0.0.1", port);
            return task.Wait(300) && c.Connected;
        }
        catch { return false; }
    }

    private static string Short(string s) => s.Length > 60 ? s[..60] + "…" : s;
}
