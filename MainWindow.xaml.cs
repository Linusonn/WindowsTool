using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using WindowsTool.Features;

namespace WindowsTool;

public partial class MainWindow : Window
{
    private readonly FeatureManager _manager = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2.5) };
    private bool _autoStartDone;

    public MainWindow()
    {
        InitializeComponent();

        _manager.Load();
        FeatureList.ItemsSource = _manager.Features;
        _manager.Features.CollectionChanged += (_, _) => UpdateCounts();

        _timer.Tick += async (_, _) =>
        {
            await _manager.RefreshAllAsync();
            UpdateCounts();
        };

        Loaded += async (_, _) =>
        {
            await _manager.RefreshAllAsync();
            UpdateCounts();
            _timer.Start();

            if (!_autoStartDone)
            {
                _autoStartDone = true;
                var autos = _manager.AutoStartFeatures.ToList();
                foreach (var f in autos)
                {
                    try { await f.StartAsync(); } catch { }
                }
            }
        };
    }

    private void UpdateCounts()
    {
        RunningCountText.Text = _manager.RunningCount.ToString();
        TotalCountText.Text = $"/ {_manager.TotalCount} 项功能";
        ConfigPathText.Text = _manager.ConfigPath;

        var running = _manager.Features.Where(f => f.State == FeatureState.Running)
                                       .Select(f => f.Name);
        var list = string.Join("、", running);
        StatusBarText.Text = _manager.RunningCount == 0
            ? "● 全部已停止"
            : $"● {list}";
    }

    // ---------- 窗口 ----------
    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            try { DragMove(); } catch { }
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // ---------- 操作 ----------
    private async void ToggleFeature_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not IFeature f) return;

        try
        {
            if (f.State is FeatureState.Running)
                await f.StopAsync();
            else
                await f.StartAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, f.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        UpdateCounts();
    }

    private async void StartAll_Click(object sender, RoutedEventArgs e)
    {
        StatusBarText.Text = "● 正在启动全部…";
        await _manager.StartAllAsync();
        UpdateCounts();
    }

    private async void StopAll_Click(object sender, RoutedEventArgs e)
    {
        StatusBarText.Text = "● 正在停止全部…";
        await _manager.StopAllAsync();
        UpdateCounts();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await _manager.RefreshAllAsync();
        UpdateCounts();
    }

    private void Config_Click(object sender, RoutedEventArgs e)
    {
        try { _manager.OpenConfigInEditor(); } catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "打不开配置文件", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
