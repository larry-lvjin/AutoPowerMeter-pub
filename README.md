<div align="center">

# AutoPowerMeter

**把设备动作、手机控制与电流采样串成一条测试流水线**

PLAUD 功耗自动化总控 · Windows 桌面应用

<p>
  <img src="https://img.shields.io/badge/Platform-Windows-0078D4" alt="Windows">
  <img src="https://img.shields.io/badge/.NET-10-512BD4" alt=".NET 10">
  <img src="https://img.shields.io/badge/UI-WPF%20%2F%20MVVM-8866CC" alt="WPF / MVVM">
  <img src="https://img.shields.io/badge/Instrument-100K12%2B%2B-149C80" alt="100K12++">
</p>

<p><strong>简体中文</strong> · <a href="README.en.md">English</a></p>

</div>

---

## 简介

AutoPowerMeter 面向 PLAUD 设备的功耗台架测试，将 **Excel 用例编排、PLAUD 内测 App HTTP、iPhone 系统操作和程控电源** 集成在一个桌面程序中：选择方案、勾选用例、开始测试，程序执行动作、采集电流并自动生成报告。

它不是通用手机测试框架，也不依靠 AI 在运行时决定动作。当前编排由中文 Excel 操作描述解析为确定的步骤，手机控制使用 HTTP、WebDriverAgent（WDA）、iOS 快捷指令及 DVT 工具。

> [!IMPORTANT]
> **自动化能力不等于全场景实机验收。** 当前主界面以 Link iOS 为主；Android 仅提供 HTTP USB 转发辅助。设备动作依赖支持调试接口的 PLAUD 内测包，普通商店版本不能据此假定可用。当前执行器没有统一的动作前后 / 采样前页面树与设备状态验收；HTTP 业务动作、DVT 杀进程和部分 Find My 脚本各自有检查，但不能替代整条场景的台架核对。

## 功能一览

| 能力 | 当前实现 |
| --- | --- |
| Excel 驱动编排 | 按所选表格顺序执行，支持过场、参考标准解析和结果复用；未知动作、重复编号或无法解析的 Delta 标准会阻止计划加载。 |
| 四套 Link iOS 方案 | Deltas / Delta × 全功耗 / 基本功耗；两种设备共用动作编排，分别读取参考标准。 |
| 自动连接 | 发现串口 / VISA 电源，准备 WDA 会话与 USB HTTP relay，从 App `/state` 自动读取本轮样机 SN。 |
| App 业务控制 | 连接、模式切换、录音开始 / 暂停 / 恢复 / 停止、软件标记、无感传输开关、Find My 开关、断连及解绑。实际支持范围由 App 与固件决定。 |
| iPhone 系统控制 | 快捷指令导航 / 锁屏 / 蓝牙开关，WDA 系统操作及录制回放，DVT 按 Bundle ID 找到并终止原 PID。 |
| Find My 场景 | 绑定录制回放、播放声音、播放中停止及移除物品；需匹配目标物品和实际系统页面。 |
| 连续电流采集 | 100K12++ v6 串口协议，当前 Link iOS 计划请求 10 kSa/s，按正式窗口计算算术平均值。 |
| FAIL 复测 | 首测 FAIL 后仅复测一次 60 秒，不重放动作、不再次安排 60 秒场景等待，以复测值判定。 |
| 自动留档 | 每项结果即时保存到独立 Excel 报告；最终 FAIL 保存完整用例波形、时间标记与 JSON 元数据。 |
| 批次控制 | 全选 / 自由勾选、暂停 / 继续、跳过当前、终止批次、导出报告与日志。 |

## 系统组成

```text
Excel 用例 / 参考标准 / 报告模板
               │
        WPF 界面与测试引擎
               │
       ┌───────┼─────────┐
       ▼       ▼         ▼
  App HTTP   手机控制    电源 / 采样
  模式/录音   WDA/快捷指令  100K12++ 串口
  连接/解绑   DVT/Python   历史 VISA / Mock
       └───────┼─────────┘
               ▼
      Excel 报告 · 日志 · FAIL 波形
```

主界面集中显示测试方案、勾选队列、当前项目、实时电流、测量倒计时和运行日志。程序界面目前以中文为主；本项目提供双语 README，不代表应用已有英文 UI。

## 环境准备

### Windows 与仪器

- Windows 桌面环境；本文发布示例使用 `win-x64`。
- 从源码构建需要 **.NET 10 SDK**；运行普通构建产物需要 .NET 10 Windows Desktop Runtime，自包含发布无需另装该运行时。
- 100K12++ 及对应串口驱动；资源格式如 `SERIAL::COM3::115200`，请按台架核实串口与波特率。
- 历史 Agilent 66319D 支持走 VISA，需要厂商 VISA 运行环境；它不等价于 100K12++ 的高速连续采集能力。

### iPhone 与 App

- iPhone 已通过 USB 连接、信任此电脑、启用开发者模式，Windows 已安装所需 Apple USB 驱动。
- 已安装并正确签名 WDA Runner，PLAUD 内测 App 已登录，并启用 HTTP 调试服务。
- 样机已与 App 绑定；Find My、录音状态和模式满足首个勾选用例的前提。**跳选不会自动执行未勾选项来补齐前置场景。**
- `tools/go-ios` 中需有 `ios.exe` 和 `wintun.dll`；主工程将它们复制到输出目录。
- 安装 `tidevice`，保证 `tidevice.exe` 在 `PATH`，用于 App HTTP USB relay。
- 安装 `pymobiledevice3`，在配置中填写 `DvtCliPath`，用于 DVT 进程控制；Find My Python 脚本还要求该可执行文件所在目录有 `python.exe`。
- 初次建立 iOS 隧道可能需要 Windows 管理员授权。软件不能绕过 USB 信任、开发者模式或签名要求。

工具环境可按实际安装位置准备，例如：

```powershell
py -m venv .venv-ios
.\.venv-ios\Scripts\python.exe -m pip install tidevice pymobiledevice3
$env:PATH = "$PWD\.venv-ios\Scripts;$env:PATH"
```

随后将 `DvtCliPath` 设置为该虚拟环境中 `pymobiledevice3.exe` 的**绝对路径**。上述依赖未在项目中锁定版本，升级后需重新验证与目标 iOS 的兼容性。请使用现代 Python 版本，随附脚本使用 Python 3.10+ 语法。

## 构建与发布

在项目根目录执行：

```powershell
dotnet restore .\src\AutoPowerMeter.csproj
dotnet build .\src\AutoPowerMeter.csproj -c Release

# 运行桌面程序；仅打开界面，不会自动开始测试
.\src\bin\Release\net10.0-windows\AutoPowerMeter.exe
```

推荐使用自包含 ReadyToRun 单文件发布配置：

```powershell
dotnet publish .\src\AutoPowerMeter.csproj -p:PublishProfile=SingleFile
```

产物为 `artifacts/single-file-win-x64/AutoPowerMeter.exe`。该 EXE 包含 .NET 运行时、原生库、模板、go-ios、录制文件及 WDA Python 脚本，运行时自动解压到 .NET 临时缓存；分发时只需复制该 EXE。仅保留简体中文 / 英文卫星资源，WPF 不使用不受支持的程序集裁剪。

**单文件不代表任意 Windows 上都能直接控制硬件。** 发布目标为受 .NET 10 支持的 Windows x64；Apple/串口/VISA 驱动、签名 WDA、`tidevice`、DVT / Python 环境仍需准备。ReadyToRun 是预编译优化，不是代码混淆或防反编译。

单文件版可编辑配置和输出目录设置保存在 `%LOCALAPPDATA%\AutoPowerMeter\settings`，不随临时缓存清理丢失；首次运行复制默认手机配置，已有配置不自动覆盖。普通文件夹构建仍使用原来的程序资源配置。

[GitHub Actions 发布配置](<.github/workflows/release.yml>)已明确主工程与 Windows x64，并启用 ReadyToRun、原生库及所有内容的单文件解压打包；私有 / 公开仓库发布行为保持原有配置。

## 快速开始

1. **准备台架。** 核对接线、极性、电压、样机状态和目标手机；尽量只连接一台 iPhone 和目标测试电源，避免自动选择错误设备。
2. **检查运行配置。** 单文件版首次启动后编辑 `%LOCALAPPDATA%\AutoPowerMeter\settings\tap_map.json`；普通文件夹版编辑程序资源目录中的 [点位与手机配置](<src/Resources/tap_map.json>) 对应副本。填写 DVT 工具路径；多手机时明确填写 `IosUdid`。
3. **创建快捷指令。** 使用默认导航配置时，在 iPhone 创建下表中的同名快捷指令，分别手动运行一次以处理授权。
4. **启动程序并选择方案。** 默认是 `Deltas Link iOS 全功耗`；确认设备版本与参考标准一致。
5. **选择保存位置。** “FAIL 波形目录”实际上同时控制运行日志、Excel 报告和 FAIL 波形，默认 `D:\AutoPowerWave`；目录必须可写。
6. **先勾选一项低风险用例。** 确认其初始场景；不要先用解绑、移除或蓝牙关闭动作试运行。
7. **点击“开始测试”。** 程序连接电源、准备 WDA / HTTP 转发并读取 SN；首次无 Token 时弹窗输入，本机缓存到 Windows 凭据管理器。
8. **观察动作与报告。** 检查手机和样机是否真的到达预期场景，单项验证通过后再扩大批次。

### 默认快捷指令

| 名称（需保留中文） | iPhone 中的动作 |
| --- | --- |
| `APM-回桌面` | 前往主屏幕 |
| `APM-打开PLAUD` | 打开 App → PLAUD |
| `APM-打开查找` | 打开 App → 查找 |
| `APM-PLAUD灭屏` | 先打开 PLAUD，再锁定屏幕 |
| `APM-查找灭屏` | 先打开查找，再锁定屏幕 |
| `APM-关闭蓝牙` | 将蓝牙设为关闭 |
| `APM-打开蓝牙` | 将蓝牙设为打开 |

当前导航直接通过 WDA `/url` 调用快捷指令，**不承诺失败后自动回退或逐项页面验收**。将 `UseShortcutsForNavigation` 设为 `false` 可改用原生导航 / 锁屏；Excel 中明确的单向蓝牙开关仍会调用对应快捷指令，不受该设置及旧 `EnableBluetoothAutomation` 开关影响。

### 操作快捷键

| 快捷键 | 操作 |
| --- | --- |
| `Ctrl + R` | 开始勾选用例 |
| `Ctrl + P` | 暂停 / 继续 |
| `Esc` | 终止批次或取消连接准备 |

暂停在**用例之间**生效，不冻结正在进行的动作或测量。跳过当前与终止批次是取消操作，**不等于恢复手机、样机或切断电源输出**。

## 配置说明

单文件版编辑 `%LOCALAPPDATA%\AutoPowerMeter\settings\tap_map.json`；普通文件夹版编辑输出目录里的配置副本。修改源码 [配置资源](<src/Resources/tap_map.json>) 后需重新构建 / 发布。相对录制资源路径仍按运行资源目录解析，不要把 Token 写入 JSON。

| 配置 | 用途 / 默认值 |
| --- | --- |
| `Settings.WdaBaseUrl` | WDA 地址，默认 `http://127.0.0.1:8100`。 |
| `Settings.AppApiBaseUrl` | App HTTP 地址，默认 `http://127.0.0.1:8090`。 |
| `Settings.PlaudBundleId` / `FindMyBundleId` | 默认 `ai.plaud.ios.plaud` / `com.apple.findmy`。 |
| `Settings.IosUdid` | 多 iPhone 时指定目标；留空只在需要选择 USB 设备且恰有一台时自动选择。 |
| `Settings.AndroidSerial` | Android HTTP 转发的目标序列号。 |
| `Settings.DvtCliPath` | `pymobiledevice3.exe` 绝对路径；类默认值是开发机路径，必须按本机修改。 |
| `Settings.RecordingDirectory` | 默认 `Resources/recordings`；更换手机 / iOS 后需重新验证录制动作。 |
| `Settings.UseShortcutsForNavigation` | 当前资源默认 `true`；用于导航 / 锁屏。 |
| `PLAUD_HTTP_DEBUG_TOKEN` | 主程序读取进程、用户、系统环境变量，再读取 Windows 凭据缓存；可用环境变量覆盖。 |
| `AUTO_POWER_WAVEFORM_DIR` | 启动时覆盖输出目录；否则使用已保存选择或默认目录。 |

iOS HTTP relay 只复用本程序拥有的转发；默认本机端口被其他程序占用时会尝试 `18090–18099`，不接管外部进程。远程 WDA 需自行准备，不能与不确定身份的本机 USB HTTP 转发混用。

## 测试方案与测量规则

当前 [测试用例与模板目录](<Docs/测试用例及模板/>) 提供四种界面选项：

| 方案 | 当前自动项数 | 动作 / 标准 |
| --- | --- | --- |
| Deltas Link iOS 全功耗 | 89 | 全功耗编排，Deltas 标准 |
| Delta Link iOS 全功耗 | 89 | 相同全功耗动作，Delta 标准 |
| Deltas Link iOS 基本功耗 | 25 | 基本功耗编排，Deltas 标准 |
| Delta Link iOS 基本功耗 | 25 | 相同基本功耗动作，Delta 标准 |

数量是当前表格加载与模拟验证结果，不代表全部项目均通过实机功耗测试；更换表格后以实际加载结果为准。表格不是任意自然语言 DSL：改变操作措辞需要同步检查 [编排解析器](<src/TestRunner/AuthoritativeDeltaPlan.cs>)。

当前 Link iOS 时序：

```text
执行当前项及其附带过场
    → 最后动作完成后固定等待 60 秒（含仪器管线准备）
    → 连续采样 60 秒，计算正式窗口算术平均值
    → 按所选标准判定
    → 首测 FAIL：仅再采样一次 60 秒，以复测值判定
    → 保存报告；最终 FAIL 才持久化波形
```

- 当前不启用统计稳态门控，不自动剔除脉冲；10 kSa/s 是计划请求值，不是任意 USB / 仪器环境的吞吐保证。
- 复测不重放动作、不再安排 60 秒场景等待，但仍经过驱动自身的采样管线准备，不保证无间隔立即开窗。
- 上限标准中的附加容差计入总上限。例如 `≤340uA+40uA` 按 `≤380uA` 判 PASS / FAIL，没有独立 WARNING 区间。
- 新双标准报告在 G / H 列保留 Deltas / Delta 标准，实测值写 I 列，结论写 J 列。结果以数值保存并设置单位格式。
- 结果复用行明确指向来源用例，不再重复测量；未支持或需工装的项目保留人工测试标记，不制造数值。
- 首测 FAIL 后复测未完成会记录 ERROR，不把首测值冒充最终复测结果。

## 输出文件

日志从启动开始保存；每轮 Excel 报告带时间戳和唯一编号，正常 GUI 流程不覆盖原始模板。每项结束立即保存，无需等整轮完成。

最终 FAIL 会在输出目录的时间子目录中保存：

| 文件 | 内容 |
| --- | --- |
| `*-100ms-csv.csv` | 100 ms 窗口的最小 / 平均 / 最大电流，UTF-8 BOM。不是逐点原始 CSV。 |
| `*-1ms-COM*.mpa` | 1 ms 平均及极值波形记录，用于兼容仪器波形格式。 |
| `*.markers.txt` | 用例描述、动作时间、首测 / 复测窗口与取值边界。 |
| `*.json` | 判定值、状态、采样率、时间与波形索引等元数据。 |

100K12++ 从用例第一步起缓存可收到的波形，最终 FAIL 尽量保存至复测判定完成；PASS / 跳过 / ERROR 不走常规 FAIL 波形保存路径。波形时间由主机时间与采样索引估算，**不是硬件时间戳**，断流或文件错误仍需查看日志。

切换目录会复制当前日志和当前报告，旧文件保留，已写波形不迁移。详细行为见 [统一输出目录](<Docs/统一输出目录.md>)。

## 安全与已知边界

> [!WARNING]
> 程序会真实改变设备状态，包括录音、模式切换、蓝牙开关、杀 App、Find My 移除以及 App 解绑 / 清数据。只在有授权的测试手机与样机上执行，并提前备份数据。终止测试不是硬件急停；需要急停时使用台架的物理断电措施。

- **电气保护：** 当前默认与软件硬上限均为 **4.0 V**。100K12++ v6 不支持软件恒压限流或手动量程；界面 / 接口的 `1 A` 配置不表示限流已实施。电压回读是命令确认后的配置值，不是万用表实测。
- **缺少统一场景验收：** 现有监督辅助代码不意味着当前执行路径每步都运行它；采样前没有额外统一状态门控。用户需核实初始场景与实际动作结果。
- **Find My 目标：** 当前 C# 调用的三个 Python 动作脚本指定物品名 `plaud`；部分控件匹配依赖中文系统文案。换语言、重命名物品或更换 iOS 后需检查脚本，不能宣称通用多语言 / 多物品支持。
- **移除脚本的 Token：** 该脚本读取进程环境 `PLAUD_HTTP_DEBUG_TOKEN`，当前 C# 不把弹窗 / 凭据缓存中的 Token 注入子进程。包含移除操作时请确保启动进程环境已有正确变量，单靠 GUI 缓存不能保证脚本鉴权。
- **移除脚本的 relay 地址：** 主 HTTP 客户端可使用备用本机端口，但该脚本仍从手机配置取得地址。包含移除操作时优先保证配置端口空闲，避免默认 `8090` 与实际 relay 不一致。
- **非自动范围：** 物理姿态、充电接线、OTA、船运 / FTEST、上电峰值同步采样等，不因主程序有相关模型就视为已完成自动化。
- **数据与隐私：** 主程序 Token 缓存不写入配置或日志，但研究脚本可能含内置调试 Token；发布源码前需审查。报告、日志、录制文件和页面树也可能包含 SN、路径或用户数据，应脱敏后分享。

## 开发与验证

以下命令不需要真实手机或电源，适合验证当前方案和连接逻辑：

```powershell
# 四套方案、标准列及报告写入（合成数据）
dotnet run --project .\src\Tools\ScenarioValidation\ScenarioValidation.csproj `
  -c Release -- --dual-ios-only

# 自动 SN 读取及勾选保留（模拟 HTTP）
dotnet run --project .\src\Tools\ScenarioValidation\ScenarioValidation.csproj `
  -c Release -- --auto-sn-only

# 连接、端口转发、取消和电压边界（模拟）
dotnet run --project .\src\Tools\RuntimeValidation\RuntimeValidation.csproj -c Release
```

本次文档核查中，主工程 Release 构建（`--no-restore`）与上述三个验证入口均通过。**没有执行真实电源输出、手机业务动作或全量功耗测试。** 模拟结果不是实测功耗报告。

[旧 Validation 入口](<Validation/Program.cs>)、ScenarioValidation 的其他分支及 [MPA 格式验证](<src/Tools/MpaValidation/Program.cs>) 包含历史断言或外部文件依赖，不应笼统声称全量测试通过。RuntimeValidation 的 `--live` / `--live-phone` / `--supervise-t161` 等参数会接触真机；尤其监督分支可真实执行声音动作，不属于离线测试。

### 辅助工具与研究代码

- [PlanCompiler](<src/Tools/PlanCompiler/Program.cs>)：表格检查、计划编译与报告冒烟工具；部分子命令会写文件或操作端口转发。
- [HTTP 验证脚本说明](<research/scripts/README.txt>)：独立核查模式、录音标记、传输、Find My、断连、解绑与扫描；解绑脚本会真实清数据。
- [RecPlay](<research/RecPlay/README.md>)：WDA 启动、录制、回放及 C# 集成研究工具；并非主程序的第二套生产入口，其测量标记本身不采集电流。
- [BatchRunner](<BatchRunner/Program.cs>) / [HardwareProbe](<HardwareProbe/Program.cs>)：专项跑批与硬件探测工具，含开发台架默认值，执行前必须阅读源码；模式冒烟也不是离线测试，退出不保证仪器输出关闭。

## 项目结构

```text
AutoPowerMeter/
├─ src/                     WPF 主程序
│  ├─ AppApi/               PLAUD HTTP 客户端与模型
│  ├─ Phone/                WDA、USB 运行时、快捷指令、DVT、录制回放
│  ├─ Instruments/          100K12++、VISA、Mock 与采样
│  ├─ Excel/                表格读取与报告写入
│  ├─ TestRunner/           编排解析、状态机、测量与波形输出
│  ├─ ViewModels/ / Views/  MVVM 界面
│  ├─ Resources/            配置、录制与 WDA Python 脚本
│  └─ Tools/                专项编译 / 验证工具
├─ Docs/                    方案、模板、协议与历史验证文档
├─ research/                HTTP 脚本与 RecPlay 研究
├─ tools/                   go-ios、Wintun 等辅助资源
├─ Validation/              早期验证工程
├─ BatchRunner/             专项批次执行
├─ HardwareProbe/           硬件探测
└─ history/                 会话索引、阶段快照与验证产物
```

## 更多文档

- [自动连接与独立运行](<Docs/自动连接说明.md>)
- [iPhone 快捷指令接入](<Docs/iPhone快捷指令接入.md>)
- [统一输出目录](<Docs/统一输出目录.md>)
- [早期全自动功耗测试实施方案](<Docs/全自动功耗测试实施方案-v1.md>)
- [采样与波形核对记录](<Docs/采样稳态与波形核对.md>)
- [HTTP / WDA 行为监督历史验证](<Docs/HTTP-WDA行为监督验证-20260913.md>)

以上阶段文档用于了解设计与历史验证；部分内容描述旧版用例数量、导航回退、状态验收或波形策略。使用当前程序时以代码、当前表格和本 README 中明确的边界为准。

## 贡献与许可

欢迎提交问题复现、协议证据、脱敏日志与文档改进。修改控制链路时请说明手机 / iOS / App / 固件 / 仪器版本，并区分模拟验证、只读真机检查与有副作用的实机测试。

当前项目根目录未提供项目级 `LICENSE`，本 README 不擅自声明 MIT 或其他开源授权。使用或分发前请确认项目授权；附带 go-ios、Wintun 等第三方组件各自遵循其许可。
