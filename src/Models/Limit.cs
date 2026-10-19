using AutoPowerMeter.Utils;

namespace AutoPowerMeter.Models;

/// <summary>
/// 通过判定规则。内部统一存 µA。
/// 示例：
///   "≤320µA±40µA" → Op=LE, ThresholdUa=320, ToleranceUa=40, OriginalInMicroAmps=true
///   "≤2mA"       → Op=LE, ThresholdUa=2000, ToleranceUa=0, OriginalInMicroAmps=false
///   "＜120mA"    → Op=Less, ThresholdUa=120000
/// </summary>
public sealed class Limit
{
    public LimitOp Op { get; set; } = LimitOp.LessOrEqual;

    /// <summary>阈值，统一 µA。</summary>
    public double ThresholdUa { get; set; }

    /// <summary>附加容差，统一 µA。上限类规则以 Threshold+Tolerance 作为唯一总上限。</summary>
    public double ToleranceUa { get; set; }

    /// <summary>原始 xlsx 是否用 µA 表达（用于结果回写时选择单位）。</summary>
    public bool OriginalInMicroAmps { get; set; }

    /// <summary>原始字符串，用于显示。</summary>
    public string OriginalText { get; set; } = "";

    public ResultStatus Evaluate(double measuredMa)
    {
        var measuredUa = UnitFormatter.MaToUa(measuredMa);
        // 功耗测试只按最终边界判定：不再设置 Warning 区间。
        // 例：≤340uA+40uA 的总上限为 380uA；≤380 为 Pass，>380 为 Fail。
        var ok = Op switch
        {
            LimitOp.LessOrEqual => measuredUa <= ThresholdUa + ToleranceUa,
            LimitOp.Less => measuredUa < ThresholdUa + ToleranceUa,
            LimitOp.GreaterOrEqual => measuredUa >= ThresholdUa,
            LimitOp.Greater => measuredUa > ThresholdUa,
            _ => false
        };
        return ok ? ResultStatus.Pass : ResultStatus.Fail;
    }
}
