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
        var limit = new Limit { OriginalText = text ?? "" };
        if (string.IsNullOrWhiteSpace(text)) return limit;

        var m = LimitRegex().Match(text.Trim());
        if (!m.Success) return limit;

        limit.Op = m.Groups["op"].Value switch
        {
            "≥" or ">=" or "＞=" => LimitOp.GreaterOrEqual,
            ">" or "＞" => LimitOp.Greater,
            "<" or "＜" => LimitOp.Less,
            _ => LimitOp.LessOrEqual
        };

        var val = double.Parse(m.Groups["val"].Value, System.Globalization.CultureInfo.InvariantCulture);
        var unit = m.Groups["unit"].Value;
        var isUa = unit.Equals("uA", StringComparison.OrdinalIgnoreCase) || unit == "µA" || unit.Equals("UA", StringComparison.OrdinalIgnoreCase);
        limit.OriginalInMicroAmps = isUa;
        limit.ThresholdUa = isUa ? val : val * 1000.0;

        if (m.Groups["tol"].Success)
        {
            var tol = double.Parse(m.Groups["tol"].Value, System.Globalization.CultureInfo.InvariantCulture);
            var tolUnit = m.Groups["tolUnit"].Value;
            var tolIsUa = string.IsNullOrEmpty(tolUnit)
                ? isUa
                : (tolUnit.Equals("uA", StringComparison.OrdinalIgnoreCase) || tolUnit == "µA" || tolUnit.Equals("UA", StringComparison.OrdinalIgnoreCase));
            limit.ToleranceUa = tolIsUa ? tol : tol * 1000.0;
        }

        return limit;
    }
}
