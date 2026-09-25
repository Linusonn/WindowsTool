using System.ComponentModel;

namespace WindowsTool.Features;

public enum FeatureState
{
    Unknown,
    Stopped,
    Starting,
    Running,
    Stopping,
    Error,
}

/// <summary>一个可启停的功能单元。</summary>
public interface IFeature : INotifyPropertyChanged
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    string Category { get; }

    FeatureState State { get; }
    /// <summary>状态旁的细节，比如 "PID 1234" / "端口 8188"。</summary>
    string StatusText { get; }

    bool CanStart { get; }
    bool CanStop { get; }
    bool IsBusy { get; }

    Task StartAsync();
    Task StopAsync();
    Task RefreshAsync();
}
