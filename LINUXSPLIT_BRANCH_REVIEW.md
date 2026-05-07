# LinuxSplit Branch Review

**Branch:** `upstream/LinuxSplit` (from NoGRo/Edi)  
**Reviewed:** 2026-05-06  
**Diverges from master at:** commit `d55fd09`

---

## Summary

The `LinuxSplit` branch is a significant architectural refactor aimed at cross-platform compatibility. It makes the **CLI and REST API layers fully portable to Linux**, but leaves the **GUI and audio subsystem as Windows-only**. It is best described as "Phase 1 complete, Phase 2 not started."

---

## What This Branch Accomplished

### Architecture Refactor
- Deleted the complex multi-layer Player system (`/Edi.Core/Players/` — ~700 lines removed)
- Introduced `DeviceManager.cs` and `DevicePlayer.cs` as cleaner, DI-friendly replacements
- Removed `DeviceCollector`, `DeviceConfiguration`, `RecorderDevice`, `RecorderProvider`
- Reorganized Funscript files (flattened subfolder structure)

### Cross-Platform Projects (work on Linux today)
| Project | Target | Status |
|---|---|---|
| `Edi.Core` | `net8.0` | Fully cross-platform library |
| `Edi.Console` (`Edi.Consola.csproj`) | `net8.0` | CLI works on Linux; AOT-enabled |
| `Edi.Mvc` → renamed `Edi.Rest` | `net8.0` | REST API works on Linux |

### Specific Fixes Made
- **OutputDir path** — now uses `Environment.SpecialFolder.LocalApplicationData + "/Edi"`, which resolves to `~/.local/share/Edi` on Linux
- **Explorer.exe button removed** — `btnOpenOutput_Click` that called `explorer.exe` is gone
- **Serial port** — no changes needed; `System.IO.Ports` works on Linux as-is
- **CLI completely rewritten** — modern `System.CommandLine` (v2.0.0-beta4) with subcommands: `play`, `stop`, `pause`, `resume`, `intensity`, `definitions`
- **Nullable enabled** in Edi.Core for better type safety

---

## What Is Still Missing / Incomplete

### Critical Blockers

#### 1. NAudio — Still Windows-Only
- `EStimProvider.cs` still directly instantiates `WaveOutEvent` (Windows Core Audio)
- No `IAudioOutput` abstraction or cross-platform alternative added
- On Linux, the EStim audio device will crash at runtime
- **Fix needed:** Abstract audio behind an interface; add a Linux implementation using LibVLCSharp or OpenAL

#### 2. WPF GUI — Still Windows-Only
- `Edi.Wpf.csproj` still targets `net8.0-windows7.0` with `<UseWPF>true</UseWPF>`
- Cannot compile or run on Linux at all
- **No Avalonia or any cross-platform UI was started**
- **Fix needed:** Port `Edi.Wpf` to Avalonia UI, or accept GUI-less Linux operation

#### 3. Windows Shell Calls Remain
- `MainWindow.xaml.cs` still contains:
  ```csharp
  Process.Start(new ProcessStartInfo("cmd", $"/c start http://localhost:5000/swagger/index.html"))
  ```
- **Fix needed:** Replace with `Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true })` — works cross-platform

### Minor Issues
- `paths.txt` in `Edi.Wpf/` contains hardcoded Windows game paths (`D:\`, `C:\Program Files`) — user config artifact, not a code blocker
- Commit messages mention "player don't works yet" — suggests audio/playback was known-broken at time of last commit

---

## Recommendation: Merge This Branch

The refactor is high quality and well-aligned with the Linux compatibility goal. Merging it into `master` (or your fork) gives you:
- A working headless Linux deployment path today (REST API + CLI)
- A cleaner architecture to build the remaining audio and GUI work on top of

### Remaining Work After Merge (updated from LINUX_COMPAT_PLAN.md)

| Task | Effort | Priority |
|---|---|---|
| Abstract NAudio behind `IAudioOutput` | Medium (~1 day) | High — blocks EStim on Linux |
| Add Linux audio backend (LibVLCSharp or OpenAL) | Medium (~1–2 days) | High |
| Fix `cmd /c start` URL opener in WPF | Trivial (2 lines) | Low — WPF won't run on Linux anyway |
| Port `Edi.Wpf` to Avalonia UI | Large (~1–2 weeks) | Medium — optional if CLI/REST is enough |
| Document Linux serial port config (`/dev/ttyUSB0`) | Trivial | Low |

---

## How to Check Out This Branch Locally

```bash
git fetch https://github.com/NoGRo/Edi LinuxSplit:LinuxSplit
git checkout LinuxSplit
```

Or to merge into your fork's master:

```bash
git checkout master
git merge LinuxSplit
```