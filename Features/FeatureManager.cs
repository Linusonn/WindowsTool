using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace WindowsTool.Features;

public class FeatureManager
{
    public ObservableCollection<IFeature> Features { get; } = new();
    public string PidDir { get; }
    public string ConfigPath { get; private set; } = "";

    public FeatureManager()
    {
        PidDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WindowsTool", "pids");
        Directory.CreateDirectory(PidDir);
    }

    public int TotalCount => Features.Count;
    public int RunningCount => Features.Count(f => f.State == FeatureState.Running);

    public void Load(string? path = null)
    {
        ConfigPath = path ?? Path.Combine(AppContext.BaseDirectory, "features.json");
        Features.Clear();

        if (!File.Exists(ConfigPath))
        {
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(
                new FeatureFile { Features = new List<FeatureConfig>() },
                new JsonSerializerOptions { WriteIndented = true }));
        }

        var json = File.ReadAllText(ConfigPath);
        var opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        var file = JsonSerializer.Deserialize<FeatureFile>(json, opts) ?? new FeatureFile();

        foreach (var cfg in file.Features)
            Features.Add(new ProcessFeature(cfg, PidDir));
    }

    /// <summary>重新读盘（改完 features.json 后调）。</summary>
    public void Reload() => Load(ConfigPath);

    public async Task RefreshAllAsync()
    {
        var tasks = Features.Select(f => f.RefreshAsync()).ToArray();
        await Task.WhenAll(tasks);
    }

    public async Task StartAllAsync()
    {
        foreach (var f in Features.Where(f => f.State != FeatureState.Running))
        {
            try { await f.StartAsync(); } catch { }
        }
    }

    public async Task StopAllAsync()
    {
        foreach (var f in Features.Where(f => f.State == FeatureState.Running || f.State == FeatureState.Unknown))
        {
            try { await f.StopAsync(); } catch { }
        }
    }

    public IEnumerable<IFeature> AutoStartFeatures => Features.Where(f => IsAutoStart(f));

    private bool IsAutoStart(IFeature f)
    {
        try
        {
            var json = File.ReadAllText(ConfigPath);
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
            var file = JsonSerializer.Deserialize<FeatureFile>(json, opts);
            return file?.Features.FirstOrDefault(c => c.Id == f.Id)?.AutoStart ?? false;
        }
        catch { return false; }
    }

    public void OpenConfigInEditor()
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ConfigPath) { UseShellExecute = true }); }
        catch { }
    }
}
