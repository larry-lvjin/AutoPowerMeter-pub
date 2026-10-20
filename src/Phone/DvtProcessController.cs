using System.Diagnostics;
using System.IO;

namespace AutoPowerMeter.Phone;

/// <summary>按 bundle 找到当前 PID，再通过 DVT 终止并核验该 PID 已退出。</summary>
public static class DvtProcessController
{
    public static async Task KillAsync(PhoneSettings settings, string bundleId, CancellationToken ct,
        bool allowAlreadyStopped = false)
    {
        // 实现细节未公开（算法核心）
        throw new NotImplementedException("该方法的实现不在开源范围内");
    }

    private static async Task<string> RunAsync(string cli, string udid, string[] args, CancellationToken ct)
    {
        // 实现细节未公开（算法核心）
        throw new NotImplementedException("该方法的实现不在开源范围内");
    }
}
