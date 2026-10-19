using AutoPowerMeter.AppApi;
using AutoPowerMeter.Phone;
using AutoPowerMeter.Views;
using Microsoft.Extensions.Logging;

namespace AutoPowerMeter.ViewModels;

/// <summary>
/// 手机自动化运行时状态。WDA、HTTP 地址与 Token 在开始测试时统一加载和验证。
/// </summary>
public sealed class PhoneViewModel : ObservableObject
{
    private readonly ILogger<PhoneViewModel> _logger;
    private IPhoneController? _phone;

    public PhoneViewModel()
    {
        _logger = ServiceLocator.Current.LoggerFactory.CreateLogger<PhoneViewModel>();
        ServiceLocator.Current.IosRuntime.Progress += message =>
            System.Windows.Application.Current.Dispatcher.Invoke(() => StatusText = message);
    }

    public IPhoneController? Controller => _phone;

    private string _wdaUrl = "http://127.0.0.1:8100";
    public string WdaUrl { get => _wdaUrl; private set => SetProperty(ref _wdaUrl, value); }

    private string _appApiUrl = "http://127.0.0.1:8090";
    public string AppApiUrl { get => _appApiUrl; private set => SetProperty(ref _appApiUrl, value); }

    private string _appApiToken = "";
    public string AppApiToken { get => _appApiToken; private set => SetProperty(ref _appApiToken, value); }

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        private set => SetProperty(ref _isConnected, value);
    }

    private string _statusText = "等待开始测试";
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public async Task EnsureAutomationReadyAsync(CancellationToken cancellationToken = default)
    {
        LoadRuntimeSettings();
        if (string.IsNullOrWhiteSpace(AppApiToken))
        {
            var dialog = new TokenPromptDialog
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            if (dialog.ShowDialog() != true)
                throw new OperationCanceledException("已取消 HTTP Token 配置");
            AppApiToken = dialog.Token;
        }
        if (!DebugTokenStore.Save(AppApiToken))
            _logger.LogWarning("Token本轮已读取，但Windows凭据缓存未保存；未输出Token内容");

        // 每次开始均复核真实服务并重建会话；旧SessionId/IsConnected不能证明重启后的连接还在。
        if (_phone is not null) await _phone.DisconnectAsync().ConfigureAwait(true);
        _phone = null;
        IsConnected = false;
        try
        {
            StatusText = $"正在连接 WDA {WdaUrl}";
            var phone = ServiceLocator.Current.CreateWdaPhone();
            await phone.ConnectAsync(cancellationToken).ConfigureAwait(true);
            _phone = phone;
            IsConnected = phone.IsConnected;
            OnPropertyChanged(nameof(Controller));
        }
        catch
        {
            IsConnected = false;
            throw;
        }

        if (!IsConnected)
            throw new InvalidOperationException("WDA 未建立有效会话");

        // 只建立执行指令所需的 WDA 会话和 HTTP 端口转发；不探测页面或 APP 状态。
        await ServiceLocator.Current.IosRuntime.EnsureReadyAsync(_phone.Settings, cancellationToken)
            .ConfigureAwait(true);
        AppApiUrl = (await ServiceLocator.Current.IosRuntime.EnsureHttpRelayAsync(
            _phone.Settings, cancellationToken).ConfigureAwait(true)).ToString().TrimEnd('/');
        StatusText = "WDA 会话与 HTTP 转发已配置";
        _logger.LogInformation("WDA 会话与 HTTP 转发已配置；按用例指令执行，不做启动前页面/状态检查");
    }

    public AppDebugClient CreateAppClient()
        => new(AppApiUrl, AppApiToken, ServiceLocator.Current.LoggerFactory.CreateLogger<AppDebugClient>());

    private void LoadRuntimeSettings()
    {
        var tapMap = TapMap.LoadOrDefault(ServiceLocator.Current.TapMapPath);
        WdaUrl = tapMap.Settings.WdaBaseUrl;
        AppApiUrl = tapMap.Settings.AppApiBaseUrl;
        AppApiToken = DebugTokenStore.Resolve();
    }
}
