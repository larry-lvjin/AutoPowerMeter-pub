using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AutoPowerMeter.ViewModels;

namespace AutoPowerMeter.Views;

public partial class AutomationDashboardView : UserControl
{
    private MainViewModel? _mainViewModel;

    public AutomationDashboardView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>窗口增加的竖向空间全部分配给运行日志，队列仍可用分隔条手动调整。</summary>
    public void AddLogViewportHeight(double logicalHeightDelta)
    {
        if (!double.IsFinite(logicalHeightDelta) || Math.Abs(logicalHeightDelta) < 0.01) return;
        var current = LogRow.Height.IsAbsolute ? LogRow.Height.Value : LogRow.ActualHeight;
        LogRow.Height = new GridLength(Math.Max(LogRow.MinHeight, current + logicalHeightDelta));
    }

    private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox box || FindParent<DataGrid>(box)?.DataContext is not TestPlanViewModel plan)
            return;
        plan.SelectAll = plan.VisibleCases.Any(item => !item.IsSelected);
    }

    private static T? FindParent<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match) return match;
            source = source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        }
        return null;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _mainViewModel = Window.GetWindow(this)?.DataContext as MainViewModel;
        if (_mainViewModel is not null)
            _mainViewModel.Logs.CollectionChanged += Logs_OnCollectionChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_mainViewModel is not null)
            _mainViewModel.Logs.CollectionChanged -= Logs_OnCollectionChanged;
        _mainViewModel = null;
    }

    private void Logs_OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (RunLogList.Items.Count > 0)
            RunLogList.ScrollIntoView(RunLogList.Items[^1]);
    }
}
