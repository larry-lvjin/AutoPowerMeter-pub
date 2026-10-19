using System.Diagnostics;
using System.IO;

namespace AutoPowerMeter.Phone;

/// <summary>按 bundle 找到当前 PID，再通过 DVT 终止并核验该 PID 已退出。</summary>
public static class DvtProcessController
{
    public static async Task KillAsync(PhoneSettings settings, string bundleId, CancellationToken ct,
        bool allowAlreadyStopped = false)
    {
        var cli = settings.DvtCliPath;
        if (!File.Exists(cli))
            throw new PhoneActionException($"DVT 工具不存在：{cli}");
        var pidText = await RunAsync(cli, settings.IosUdid,
            ["developer", "dvt", "process-id-for-bundle-id", "--userspace", bundleId], ct);
        if (allowAlreadyStopped && pidText.Trim() is "" or "0" or "None" or "null") return;
        if (!int.TryParse(pidText.Trim(), out var pid) || pid <= 0)
            throw new PhoneActionException($"{bundleId} 没有可终止的运行 PID：{pidText}");
        var running = await RunAsync(cli, settings.IosUdid,
            ["developer", "dvt", "is-running-pid", "--userspace", pid.ToString()], ct);
        if (!string.Equals(running.Trim(), "true", StringComparison.OrdinalIgnoreCase))
            throw new PhoneActionException($"{bundleId} PID {pid} 已不在运行，停止本项以重新确认场景");
        await RunAsync(cli, settings.IosUdid,
            ["developer", "dvt", "kill", "--userspace", pid.ToString()], ct);
        var after = await RunAsync(cli, settings.IosUdid,
            ["developer", "dvt", "is-running-pid", "--userspace", pid.ToString()], ct);
        if (!string.Equals(after.Trim(), "false", StringComparison.OrdinalIgnoreCase))
            throw new PhoneActionException($"DVT kill 后 {bundleId} 原 PID {pid} 仍在运行：{after}");
    }

    private static async Task<string> RunAsync(string cli, string udid, string[] args, CancellationToken ct)
    {
        var start = new ProcessStartInfo(cli)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var arg in args.Take(4)) start.ArgumentList.Add(arg);
        if (!string.IsNullOrWhiteSpace(udid))
        {
            start.ArgumentList.Add("--udid");
            start.ArgumentList.Add(udid);
        }
        foreach (var arg in args.Skip(4)) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new PhoneActionException("DVT 工具启动失败");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            var output = (await stdout.ConfigureAwait(false)).Trim();
            var error = (await stderr.ConfigureAwait(false)).Trim();
            if (process.ExitCode != 0)
                throw new PhoneActionException($"DVT 返回 {process.ExitCode}：{error} {output}");
            return output;
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }
    }
}
