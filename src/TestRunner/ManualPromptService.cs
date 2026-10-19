using System.Windows;

namespace AutoPowerMeter.TestRunner;

/// <summary>
/// 在任意线程发起人工确认弹窗。Marshal 到 UI 线程显示模态对话框，
/// 调用方 await 返回 PromptChoice。
/// </summary>
public sealed class ManualPromptService
{
    /// <summary>展示模态弹窗并等待用户选择。</summary>
    public Task<PromptChoice> ShowAsync(string title, string body, string? hint = null, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<PromptChoice>(TaskCreationOptions.RunContinuationsAsynchronously);
        var app = Application.Current ?? throw new InvalidOperationException("no WPF Application");
        app.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                var dlg = new ManualPromptDialog(title, body, hint)
                {
                    Owner = app.MainWindow
                };
                ct.Register(() =>
                {
                    if (dlg.IsLoaded && dlg.IsVisible) dlg.Close();
                });
                dlg.ShowDialog();
                tcs.TrySetResult(dlg.Choice);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        return tcs.Task;
    }
}
