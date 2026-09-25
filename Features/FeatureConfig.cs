using System.Text.Json.Serialization;

namespace WindowsTool.Features;

/// <summary>features.json 里的单条功能定义。</summary>
public class FeatureConfig
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("category")] public string Category { get; set; } = "其它";
    /// <summary>随程序启动一起拉起。</summary>
    [JsonPropertyName("autoStart")] public bool AutoStart { get; set; }

    [JsonPropertyName("start")] public StartSpec? Start { get; set; }
    [JsonPropertyName("stop")] public StopSpec? Stop { get; set; }
    [JsonPropertyName("detect")] public DetectSpec? Detect { get; set; }
}

public class StartSpec
{
    /// <summary>可执行文件（exe/bat/cmd），可以是全路径，也可以是 PATH 里的名字。</summary>
    [JsonPropertyName("file")] public string File { get; set; } = "";
    [JsonPropertyName("args")] public string? Args { get; set; }
    [JsonPropertyName("workingDirectory")] public string? WorkingDirectory { get; set; }
    /// <summary>需要管理员权限启动（会弹 UAC）。</summary>
    [JsonPropertyName("runAsAdmin")] public bool RunAsAdmin { get; set; }
    /// <summary>用 ShellExecute 启动（某些程序如 UWP/快捷方式需要）。</summary>
    [JsonPropertyName("useShellExecute")] public bool UseShellExecute { get; set; }
}

public class StopSpec
{
    /// <summary>优先按记录下来的 PID 结束进程。</summary>
    [JsonPropertyName("killByPid")] public bool KillByPid { get; set; } = true;
    /// <summary>兜底：按进程名结束（不带 .exe）。</summary>
    [JsonPropertyName("killProcessNames")] public List<string>? KillProcessNames { get; set; }
    /// <summary>停止前先执行的命令（例如 docker stop）。</summary>
    [JsonPropertyName("command")] public string? Command { get; set; }
    [JsonPropertyName("commandArgs")] public string? CommandArgs { get; set; }
}

public class DetectSpec
{
    /// <summary>进程名（不带 .exe）出现即视为运行中。</summary>
    [JsonPropertyName("processNames")] public List<string>? ProcessNames { get; set; }
    /// <summary>本地端口在监听即视为运行中。</summary>
    [JsonPropertyName("port")] public int? Port { get; set; }
    /// <summary>需要跑起来的进程名（配合 stop.killProcessNames 更精确）。</summary>
    [JsonPropertyName("requireProcessNames")] public List<string>? RequireProcessNames { get; set; }
    /// <summary>true = 必须同时满足所有条件；false(默认) = 任一满足。</summary>
    [JsonPropertyName("matchAll")] public bool MatchAll { get; set; }
}

/// <summary>功能清单根对象。</summary>
public class FeatureFile
{
    [JsonPropertyName("features")] public List<FeatureConfig> Features { get; set; } = new();
}
