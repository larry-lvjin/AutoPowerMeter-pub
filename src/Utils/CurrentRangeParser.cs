using System.Text.RegularExpressions;
using AutoPowerMeter.Models;

namespace AutoPowerMeter.Utils;

/// <summary>
/// 把 xlsx 里 "≤320uA±40uA" / "≤2mA" / "＜120mA" / "≤15.8mA±1.58mA" 这种字符串解析成 Limit。
/// </summary>
public static partial class CurrentRangeParser
{
    [GeneratedRegex(@"^\s*(?<op>[≤<＜≥>＞]=?|<=|>=)?\s*(?<val>[\d.]+)\s*(?<unit>uA|µA|mA|MA|UA)\s*(?:[±+＋])?\s*(?<tol>[\d.]+)?\s*(?<tolUnit>uA|µA|mA|MA|UA)?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LimitRegex();

    public static Limit Parse(string text)
    {
        // 实现细节未公开（算法核心）
        throw new NotImplementedException("该方法的实现不在开源范围内");
    }
}
