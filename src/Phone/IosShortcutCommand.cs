namespace AutoPowerMeter.Phone;

/// <summary>Apple Shortcuts URL scheme；由 WDA /session/{id}/url 交给 iOS 解析。</summary>
public static class IosShortcutCommand
{
    public static string BuildUrl(string shortcutName)
    {
        if (string.IsNullOrWhiteSpace(shortcutName))
            throw new ArgumentException("快捷指令名称不能为空", nameof(shortcutName));
        return "shortcuts://run-shortcut?name=" + Uri.EscapeDataString(shortcutName.Trim());
    }
}
