<div align="center">

# AutoPowerMeter

**One test pipeline for device actions, phone control, and current capture**

PLAUD power-consumption test orchestrator · Windows desktop application

<p>
  <img src="https://img.shields.io/badge/Platform-Windows-0078D4" alt="Windows">
  <img src="https://img.shields.io/badge/.NET-10-512BD4" alt=".NET 10">
  <img src="https://img.shields.io/badge/UI-WPF%20%2F%20MVVM-8866CC" alt="WPF / MVVM">
  <img src="https://img.shields.io/badge/Instrument-100K12%2B%2B-149C80" alt="100K12++">
</p>

<p><a href="README.md">简体中文</a> · <strong>English</strong></p>

</div>

---

## Overview

AutoPowerMeter brings **Excel test sequencing, the PLAUD internal App HTTP API, iPhone system actions, and programmable power instrumentation** into one desktop application. Select a suite, check the cases to run, and start the test: the application executes actions, captures current, and writes reports automatically.

This is not a general-purpose phone testing framework, and it does not ask an AI model to choose actions at runtime. Chinese operation descriptions in Excel are compiled into deterministic steps. Phone control uses HTTP, WebDriverAgent (WDA), iOS Shortcuts, and DVT tools.

> [!IMPORTANT]
> **Automation support is not proof of complete on-device validation.** The current dashboard primarily supports Link iOS; Android support is limited to an HTTP USB-forwarding helper. Device actions require a PLAUD internal build with the debugging API enabled, not an assumed capability of the App Store version. The current executor does not provide uniform page-tree/device-state checks before and after every action or before capture. HTTP business actions, DVT process termination, and some Find My scripts have their own checks, but these do not validate the entire measurement scenario.

## Features

| Capability | Current implementation |
| --- | --- |
| Excel-driven sequencing | Workbook order, transition steps, limit parsing, and result reuse. Unknown operations, duplicate IDs, or unparseable Delta limits prevent plan loading. |
| Four Link iOS suites | Deltas / Delta × full / basic power consumption. Device variants share actions and use separate reference limits. |
| Automatic connection | Discover serial/VISA instruments, prepare a WDA session and USB HTTP relay, and read the current device SN from App `/state`. |
| App business control | Connection, battery modes, recording start/pause/resume/stop, software marks, automatic-transfer and Find My switches, disconnection, and unbinding. Actual support depends on the App and firmware. |
| iPhone system control | Shortcut navigation/locking/Bluetooth actions, native WDA actions and recording playback, and DVT termination of the original PID identified by Bundle ID. |
| Find My scenarios | Binding playback, sound playback, stop-during-playback, and item removal; scripts must match the target item and actual system UI. |
| Continuous capture | 100K12++ v6 serial protocol. Current Link iOS plans request 10 kSa/s and use the arithmetic mean of the measurement window. |
| FAIL retest | One additional 60-second capture after an initial FAIL, without replaying actions or scheduling another 60-second scenario wait. The retest determines the result. |
| Automatic artifacts | Save each result immediately to a separate Excel report. Persist full-case waveforms, markers, and JSON metadata for final FAIL results. |
| Batch controls | Select all or individual cases, pause/resume, skip the current case, stop the batch, and export reports/logs. |

## Architecture

```text
Excel cases / reference limits / report templates
                       │
             WPF dashboard + test engine
                       │
          ┌────────────┼─────────────┐
          ▼            ▼             ▼
      App HTTP     Phone control   Power / capture
      Modes/record  WDA/Shortcuts   100K12++ serial
      Connect/unbind DVT/Python    Legacy VISA / Mock
          └────────────┼─────────────┘
                       ▼
           Excel reports · logs · FAIL waveforms
```

The dashboard shows the suite selector, checked queue, active case, live current, capture countdown, and logs. **The application UI is currently primarily Chinese.** Bilingual README files do not imply an English application UI.

## Prerequisites

### Windows and instrumentation

- A Windows desktop environment; publishing examples below target `win-x64`.
- **.NET 10 SDK** for source builds. Ordinary build outputs require the .NET 10 Windows Desktop Runtime; a self-contained publish includes the runtime.
- A 100K12++ instrument and its serial driver. Resources use a format such as `SERIAL::COM3::115200`; verify the port and baud rate on your bench.
- Legacy Agilent 66319D support requires a vendor VISA runtime. Its polling path is not equivalent to the 100K12++ high-rate streaming path.

### iPhone and App

- An iPhone connected over USB, with Trust This Computer and Developer Mode enabled; the required Apple USB drivers must be installed on Windows.
- An installed, correctly signed WDA Runner, and a signed-in PLAUD internal App with the HTTP debugging service enabled.
- A device already bound to the App. Prepare the Find My, recording, and mode state required by the first checked case. **Skipping earlier cases does not automatically execute unchecked cases to reconstruct their scenarios.**
- `ios.exe` and `wintun.dll` under `tools/go-ios`; the main project copies these into its output resources.
- `tidevice.exe` available on `PATH` for the App HTTP USB relay.
- An installed `pymobiledevice3` executable configured through `DvtCliPath` for DVT process control. Find My scripts additionally require `python.exe` in the same directory as that executable.
- Windows administrator consent may be required to establish the iOS tunnel. The application cannot bypass USB trust, Developer Mode, or signing requirements.

For example, prepare an environment appropriate for your machine:

```powershell
py -m venv .venv-ios
.\.venv-ios\Scripts\python.exe -m pip install tidevice pymobiledevice3
$env:PATH = "$PWD\.venv-ios\Scripts;$env:PATH"
```

Set `DvtCliPath` to the **absolute path** of `pymobiledevice3.exe` in that environment. These dependencies are not version-pinned by the project; revalidate compatibility with your target iOS after upgrades. Use a modern Python version; the included scripts use Python 3.10+ syntax.

## Build and publish

Run from the project root:

```powershell
dotnet restore .\src\AutoPowerMeter.csproj
dotnet build .\src\AutoPowerMeter.csproj -c Release

# Open the desktop application; this does not automatically start tests
.\src\bin\Release\net10.0-windows\AutoPowerMeter.exe
```

Use the self-contained ReadyToRun single-file profile:

```powershell
dotnet publish .\src\AutoPowerMeter.csproj -p:PublishProfile=SingleFile
```

The output is `artifacts/single-file-win-x64/AutoPowerMeter.exe`. It bundles the .NET runtime, native libraries, templates, go-ios, recordings, and WDA Python scripts, which extract into the .NET temporary cache at runtime. Distribute only this EXE. Satellite resources are limited to Simplified Chinese and English; unsupported WPF assembly trimming remains disabled.

**Single-file packaging does not make hardware control work on every Windows computer.** The target is Windows x64 supported by .NET 10. Apple/serial/VISA drivers, signed WDA, `tidevice`, and the DVT/Python environment still require preparation. ReadyToRun is precompilation, not obfuscation or anti-decompilation.

Editable single-file settings are stored in `%LOCALAPPDATA%\AutoPowerMeter\settings`, independently of the extraction cache. The first launch copies the default phone configuration; existing settings are not overwritten. Ordinary folder builds retain their existing resource configuration behavior.

The [GitHub Actions release configuration](<.github/workflows/release.yml>) now explicitly targets the main project and Windows x64, enables ReadyToRun, and bundles native libraries and all content for extraction. Existing private/public repository publishing behavior is retained.

## Quick start

1. **Prepare the bench.** Check wiring, polarity, voltage, device state, and the target phone. Prefer a single connected iPhone and only the intended instrument to avoid unintended automatic selection.
2. **Check runtime settings.** After the first single-file launch, edit `%LOCALAPPDATA%\AutoPowerMeter\settings\tap_map.json`. For folder builds, edit the output resource copy of the [phone/action configuration](<src/Resources/tap_map.json>). Set the DVT path and specify `IosUdid` when multiple phones are connected.
3. **Create the Shortcuts.** With the default navigation settings, create the exact names below on the iPhone and run each manually once to handle permissions.
4. **Open the application and choose a suite.** The default is `Deltas Link iOS 全功耗`. Ensure the reference limits match the tested device variant.
5. **Choose an output location.** The `FAIL 波形目录` field controls logs, Excel reports, and FAIL waveforms together. It defaults to `D:\AutoPowerWave` and must be writable.
6. **Start with one low-risk case.** Prepare its initial scenario. Do not use unbinding, item removal, or Bluetooth-off actions as your first trial.
7. **Click `开始测试` (Start test).** The application connects the instrument, prepares WDA/HTTP forwarding, and reads the SN. If no Token is available, a first-run prompt accepts one and caches it in Windows Credential Manager.
8. **Observe actions and reports.** Confirm the phone and device actually reach the intended scenario before expanding the batch.

### Default Shortcuts

Keep these Chinese names unless you update the corresponding configuration fields:

| Exact name | Shortcut action |
| --- | --- |
| `APM-回桌面` | Go to Home Screen |
| `APM-打开PLAUD` | Open App → PLAUD |
| `APM-打开查找` | Open App → Find My |
| `APM-PLAUD灭屏` | Open PLAUD, then Lock Screen |
| `APM-查找灭屏` | Open Find My, then Lock Screen |
| `APM-关闭蓝牙` | Set Bluetooth to off |
| `APM-打开蓝牙` | Set Bluetooth to on |

Navigation currently invokes Shortcuts directly through WDA `/url`; **automatic fallback or per-action page validation is not guaranteed**. Set `UseShortcutsForNavigation` to `false` to use native navigation/locking. Explicit one-way Bluetooth actions in Excel still call their Shortcuts regardless of this setting or the legacy `EnableBluetoothAutomation` switch.

### Keyboard controls

| Shortcut | Action |
| --- | --- |
| `Ctrl + R` | Run checked cases |
| `Ctrl + P` | Pause/resume |
| `Esc` | Stop the batch or cancel connection preparation |

Pause takes effect **between cases**; it does not freeze an ongoing action or capture. Skip and stop cancel execution; **they do not restore phone/device state or guarantee power output is turned off**.

## Configuration

For single-file builds, edit `%LOCALAPPDATA%\AutoPowerMeter\settings\tap_map.json`; for folder builds, edit the output configuration copy. Editing the [source configuration resource](<src/Resources/tap_map.json>) requires rebuilding/publishing. Relative recording paths still resolve against the runtime resource directory. Do not put Tokens in JSON.

| Setting | Purpose / default |
| --- | --- |
| `Settings.WdaBaseUrl` | WDA endpoint: `http://127.0.0.1:8100`. |
| `Settings.AppApiBaseUrl` | App HTTP endpoint: `http://127.0.0.1:8090`. |
| `Settings.PlaudBundleId` / `FindMyBundleId` | `ai.plaud.ios.plaud` / `com.apple.findmy`. |
| `Settings.IosUdid` | Explicit iPhone target. When USB selection is needed, an empty value permits automatic selection only if exactly one phone is available. |
| `Settings.AndroidSerial` | Target serial for the Android HTTP-forwarding helper. |
| `Settings.DvtCliPath` | Absolute path to `pymobiledevice3.exe`. The class default points to a developer machine and must be adjusted. |
| `Settings.RecordingDirectory` | `Resources/recordings`; revalidate actions after changing phones or iOS versions. |
| `Settings.UseShortcutsForNavigation` | `true` in the supplied resource; controls navigation/locking. |
| `PLAUD_HTTP_DEBUG_TOKEN` | The main App checks process, user, and machine environment variables, then Windows Credential Manager. Environment settings override cached credentials. |
| `AUTO_POWER_WAVEFORM_DIR` | Overrides the output directory at startup; otherwise the saved selection or default is used. |

The iOS HTTP relay reuses only forwarding owned by this application. If the preferred local port is occupied, it tries `18090–18099` instead of taking over another process. Remote WDA must be prepared separately and cannot be blindly paired with local USB HTTP forwarding to an unidentified phone.

## Suites and measurement rules

The current [case and template directory](<Docs/测试用例及模板/>) supplies these dashboard options:

| Suite | Current automatic cases | Actions / limits |
| --- | --- | --- |
| Deltas Link iOS 全功耗 | 89 | Full-power sequence, Deltas limits |
| Delta Link iOS 全功耗 | 89 | Same full-power actions, Delta limits |
| Deltas Link iOS 基本功耗 | 25 | Basic-power sequence, Deltas limits |
| Delta Link iOS 基本功耗 | 25 | Same basic-power actions, Delta limits |

Counts reflect the current workbook loading and synthetic validation, not successful on-device power measurements for every case. After updating workbooks, use the actual loaded counts. The operation language is not an arbitrary natural-language DSL: wording changes require checking the [sequence compiler](<src/TestRunner/AuthoritativeDeltaPlan.cs>).

Current Link iOS timing:

```text
Execute the checked case and its attached transitions
  → Fixed 60-second wait after the final action (includes instrument preparation)
  → Capture for 60 seconds and compute the measurement-window arithmetic mean
  → Evaluate the selected limit
  → Initial FAIL: one additional 60-second capture; retest determines the result
  → Save the report; persist waveforms only for a final FAIL
```

- Statistical stability gating is disabled, and pulses are not automatically removed. The 10 kSa/s setting is a requested stream rate, not a throughput guarantee for every instrument/USB environment.
- Retesting does not replay actions or schedule another 60-second scenario wait, but the driver's own stream preparation still applies; it is not a promise of a gapless next capture.
- Added tolerance is included in an upper bound. For example, `≤340uA+40uA` means PASS at `≤380uA`, with no separate WARNING band.
- Combined reports retain Deltas/Delta limits in G/H, write measured values in I, and write status in J. Measurements remain numeric cells with unit formatting.
- Reuse rows explicitly reference their source case and are not remeasured. Unsupported or fixture-dependent cases retain manual-test markers instead of fabricated values.
- An incomplete retest is reported as ERROR; the initial FAIL value is not substituted for a completed final retest.

## Output artifacts

Logs are saved from startup. Each GUI run creates an Excel report with a timestamp and unique identifier instead of overwriting the source template. Results are saved immediately after each case.

For a final FAIL, the time-named output subdirectory contains:

| File | Contents |
| --- | --- |
| `*-100ms-csv.csv` | Minimum/mean/maximum current for 100 ms windows, UTF-8 BOM. Not a point-by-point raw CSV. |
| `*-1ms-COM*.mpa` | 1 ms mean/extrema records in the instrument-compatible waveform format. |
| `*.markers.txt` | Case description, action times, initial/retest windows, and measurement boundaries. |
| `*.json` | Values, status, sample rate, timing, waveform indexes, and other metadata. |

The 100K12++ driver buffers available data from the first case action and attempts to preserve the full case through final retest evaluation. PASS, skipped, and ERROR results do not use the normal FAIL waveform persistence path. Waveform times are estimated from host time and sample indexes—**not hardware timestamps**. Check logs for stream or file errors.

Changing the output directory copies the current log and report, preserves old files, and does not move previously written waveforms. See [Output directory behavior](<Docs/统一输出目录.md>) (Chinese).

## Safety and known limitations

> [!WARNING]
> Tests make real changes: recording, mode switching, Bluetooth changes, App termination, Find My removal, and App unbinding/data clearing. Use only authorized test phones and devices, and back up data first. Stopping a test is not an emergency hardware stop; use a physical bench power cutoff when needed.

- **Electrical protection:** Both the default voltage and software maximum are **4.0 V**. The 100K12++ v6 driver does not implement software constant-voltage current limiting or manual range selection. A `1 A` configuration value does not mean current limiting is active. Voltage readback is a command-confirmed setting, not an independent DMM measurement. Software guards do not replace hardware protection.
- **No uniform scenario gate:** Existing supervision helpers do not imply they are called on the current execution path. There is no additional uniform pre-capture state check; verify initial scenarios and actual outcomes on the bench.
- **Find My target:** The three Python actions invoked by C# currently use the item name `plaud`. Some control matches depend on Chinese system text. Revalidate scripts after changing UI language, item names, or iOS; generic multilingual/multi-item support is not promised.
- **Removal-script Token:** The script reads the process environment variable `PLAUD_HTTP_DEBUG_TOKEN`. C# currently does not inject the GUI/credential-resolved Token into that child process. For removal cases, ensure the launch process already has the correct variable; GUI caching alone does not guarantee script authentication.
- **Removal-script relay address:** The main HTTP client can use a fallback local port, but the removal script still receives the configured phone address. For removal cases, prefer a free configured port to avoid a mismatch between `8090` and the actual relay.
- **Non-automated conditions:** Physical posture, charging connections, OTA, shipping/FTEST modes, and synchronized startup-peak capture are not complete automation features merely because related models exist.
- **Data and privacy:** Main-application Token caching does not write secrets to configuration or logs, but research scripts may contain built-in debug Tokens. Review these before publishing source. Reports, logs, recordings, and page trees may contain SNs, paths, or user data; redact before sharing.

## Development and validation

The following entries do not require a real phone or power instrument:

```powershell
# Four suites, limit columns, and report writing with synthetic measurements
dotnet run --project .\src\Tools\ScenarioValidation\ScenarioValidation.csproj `
  -c Release -- --dual-ios-only

# Automatic SN reading and preserved selections with mocked HTTP
dotnet run --project .\src\Tools\ScenarioValidation\ScenarioValidation.csproj `
  -c Release -- --auto-sn-only

# Simulated connection, forwarding, cancellation, and voltage boundaries
dotnet run --project .\src\Tools\RuntimeValidation\RuntimeValidation.csproj -c Release
```

During this documentation review, the main Release build (`--no-restore`) and all three entries above passed. **No physical power output, phone business actions, or full power-consumption run was performed.** Synthetic results are not measured power reports.

The [legacy Validation entry](<Validation/Program.cs>), other ScenarioValidation branches, and [MPA compatibility validation](<src/Tools/MpaValidation/Program.cs>) contain older assumptions or external file dependencies. Do not claim that all validation modes pass. RuntimeValidation flags such as `--live`, `--live-phone`, and `--supervise-t161` access real devices; supervision branches can execute sound actions and are not offline tests.

### Auxiliary tools and research

- [PlanCompiler](<src/Tools/PlanCompiler/Program.cs>): workbook inspection, plan compilation, and report smoke tools. Some subcommands write files or change USB forwarding.
- [HTTP research scripts](<research/scripts/README.txt>): standalone checks for modes, recording marks, transfer, Find My, disconnection, unbinding, and scanning. The unbind script actually clears data.
- [RecPlay](<research/RecPlay/README.md>): research tools for WDA startup, recording/playback, and C# integration; not a second production entry point. Its measurement marker does not itself capture current.
- [BatchRunner](<BatchRunner/Program.cs>) / [HardwareProbe](<HardwareProbe/Program.cs>): specialized hardware batch/probe tools with developer-bench defaults. Read the source before execution; even a mode-smoke run is not an offline test. Exiting does not guarantee instrument output is disabled.

## Project layout

```text
AutoPowerMeter/
├─ src/                     Main WPF application
│  ├─ AppApi/               PLAUD HTTP client and models
│  ├─ Phone/                WDA, USB runtime, Shortcuts, DVT, recordings
│  ├─ Instruments/          100K12++, VISA, Mock, and capture
│  ├─ Excel/                Workbook reading and report writing
│  ├─ TestRunner/           Sequence parsing, state machine, artifacts
│  ├─ ViewModels/ / Views/  MVVM UI
│  ├─ Resources/            Configuration, recordings, WDA Python scripts
│  └─ Tools/                Specialized compiler/validation tools
├─ Docs/                    Plans, templates, protocols, historical records
├─ research/                HTTP scripts and RecPlay research
├─ tools/                   go-ios, Wintun, and supporting resources
├─ Validation/              Early validation project
├─ BatchRunner/             Specialized batch runner
├─ HardwareProbe/           Instrument probe
└─ history/                 Session indexes, snapshots, validation artifacts
```

## Further documentation

These supporting documents are currently Chinese:

- [Automatic connection and standalone operation](<Docs/自动连接说明.md>)
- [iPhone Shortcuts integration](<Docs/iPhone快捷指令接入.md>)
- [Unified output directory](<Docs/统一输出目录.md>)
- [Early implementation plan](<Docs/全自动功耗测试实施方案-v1.md>)
- [Capture and waveform review records](<Docs/采样稳态与波形核对.md>)
- [Historical HTTP/WDA supervision validation](<Docs/HTTP-WDA行为监督验证-20260913.md>)

Stage-specific documents explain design and historical verification. Some describe older case counts, navigation fallback, state gates, or waveform policies. For current operation, use the code, current workbooks, and the explicit limitations in this README.

## Contributing and licensing

Contributions of reproducible issues, protocol evidence, redacted logs, and documentation improvements are welcome. For control-chain changes, record phone/iOS/App/firmware/instrument versions and distinguish simulations, read-only device checks, and real tests with side effects.

No project-level `LICENSE` is currently provided at the project root. This README does not assign MIT or another open-source license. Confirm authorization before use or redistribution; bundled third-party components such as go-ios and Wintun remain subject to their own licenses.
