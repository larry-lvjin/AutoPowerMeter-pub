using System.IO;
using System.Text.Json;

namespace AutoPowerMeter.Utils;

/// <summary>Explicit offline packaging check; never creates phone or instrument connections.</summary>
internal static class PackagingSmokeCheck
{
    internal static int Run(string reportPath)
    {
        var required = new[]
        {
            "Resources/go-ios/ios.exe", "Resources/go-ios/wintun.dll", "Resources/tap_map.json",
            "Resources/wda-scripts/WDA-移除查找.py",
            "Resources/wda-scripts/WDA-查找播放声音.py",
            "Resources/wda-scripts/WDA-查找播放声音过程中停止播放.py",
            "Resources/recordings/绑定FindMy-iOS26.json",
            "Docs/测试用例及模板/Deltas + Delta 全功耗  测试用例.xlsx",
            "Docs/测试用例及模板/Deltas + Delta 全功耗  测试报告模板.xlsx",
            "Docs/测试用例及模板/Deltas + Delta 基本功耗  测试用例.xlsx",
            "Docs/测试用例及模板/Deltas + Delta 基本功耗  测试报告模板.xlsx"
        };
        var missing = required.Where(relative => !File.Exists(Path.Combine(AppContext.BaseDirectory, relative))).ToArray();
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new
        {
            Passed = missing.Length == 0,
            Missing = missing,
            ResourceDirectory = AppContext.BaseDirectory,
            ExecutablePath = Environment.ProcessPath,
            SettingsDirectory = RuntimeSettingsStorage.DirectoryPath,
            HardwareAccessed = false
        }, new JsonSerializerOptions { WriteIndented = true }));
        return missing.Length == 0 ? 0 : 1;
    }
}
