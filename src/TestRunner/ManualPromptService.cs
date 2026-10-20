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
        // 实现细节未公开（算法核心）
        throw new NotImplementedException("该方法的实现不在开源范围内");
    }
}
