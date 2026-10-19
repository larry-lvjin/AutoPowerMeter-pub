using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Text;

namespace AutoPowerMeter.Phone;

/// <summary>仅管理 Android 的 App HTTP USB 转发；iPhone 全量用例仍由 WDA/go-ios 运行。</summary>
public sealed class AndroidHttpForwarder(IAndroidForwardHost host)
{
    public async Task<string> EnsureAsync(int port = 8090, string? preferredSerial = null,
        bool repair = false, CancellationToken ct = default)
    {
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        var serial = SelectDevice(await host.RunAsync(["devices"], ct).ConfigureAwait(false), preferredSerial);
        var local = $"tcp:{port}";
        var mappings = ParseMappings(await host.RunAsync(["forward", "--list"], ct).ConfigureAwait(false));
        var existing = mappings.Where(m => m.Local == local).ToArray();
        if (existing.Any(m => m.Serial != serial || m.Remote != local))
            throw new InvalidOperationException($"本机 {port} 已转发到另一设备或端口；请先检查 adb forward --list");
        if (existing.Length > 1)
            throw new InvalidOperationException($"本机 {port} 有多条 adb 映射，拒绝猜测目标手机");
        if (existing.Length == 1 && !repair) return serial;
        if (existing.Length == 0 && host.PortInUse(port))
            throw new InvalidOperationException($"本机 {port} 被非 adb 进程占用，拒绝覆盖 iOS 或其他 HTTP 服务");

        if (existing.Length == 1)
            await host.RunAsync(["-s", serial, "forward", "--remove", local], ct).ConfigureAwait(false);
        await host.RunAsync(["-s", serial, "forward", local, local], ct).ConfigureAwait(false);
        var after = ParseMappings(await host.RunAsync(["forward", "--list"], ct).ConfigureAwait(false));
        if (!after.Any(m => m.Serial == serial && m.Local == local && m.Remote == local))
            throw new IOException($"adb 未确认 {serial} 的 {port} USB 转发");
        return serial;
    }

    public static string SelectDevice(string output, string? preferred)
    {
        var devices = output.Replace("\r", "").Split('\n')
            .Select(line => line.Split('\t', 2))
            .Where(parts => parts.Length == 2 && parts[1].Split(' ', 2)[0] == "device")
            .Select(parts => parts[0].Trim()).Where(serial => serial.Length > 0).ToArray();
        if (!string.IsNullOrWhiteSpace(preferred))
            return devices.Contains(preferred, StringComparer.Ordinal)
                ? preferred : throw new InvalidOperationException($"指定 Android 设备 {preferred} 未通过 adb 连接");
        return devices.Length switch
        {
            1 => devices[0],
            0 => throw new InvalidOperationException("未发现已授权的 Android USB 设备；请检查 adb devices"),
            _ => throw new InvalidOperationException("连接了多台 Android 设备；请指定 serial，避免转发到错误手机")
        };
    }

    public static IReadOnlyList<(string Serial, string Local, string Remote)> ParseMappings(string output)
        => output.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length >= 3)
            .Select(parts => (Serial: parts[0], Local: parts[1], Remote: parts[2])).ToArray();
}

public interface IAndroidForwardHost
{
    Task<string> RunAsync(string[] arguments, CancellationToken ct);
    bool PortInUse(int port);
}

public sealed class AdbForwardHost : IAndroidForwardHost
{
    public async Task<string> RunAsync(string[] arguments, CancellationToken ct)
    {
        var info = new ProcessStartInfo("adb.exe")
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new IOException("adb.exe 未能启动");
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(TimeSpan.FromSeconds(20));
        var output = process.StandardOutput.ReadToEndAsync(budget.Token);
        var error = process.StandardError.ReadToEndAsync(budget.Token);
        try
        {
            await process.WaitForExitAsync(budget.Token).ConfigureAwait(false);
            var stdout = await output.ConfigureAwait(false);
            var stderr = await error.ConfigureAwait(false);
            if (process.ExitCode != 0) throw new IOException($"adb {string.Join(' ', arguments)} 失败：{stderr.Trim()}");
            return stdout;
        }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
            ct.ThrowIfCancellationRequested();
            throw new TimeoutException("adb 命令 20 秒内未完成");
        }
    }

    public bool PortInUse(int port) => IPGlobalProperties.GetIPGlobalProperties()
        .GetActiveTcpListeners().Any(endpoint => endpoint.Port == port);
}
