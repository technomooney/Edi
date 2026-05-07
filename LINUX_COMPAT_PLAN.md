# Linux Compatibility Plan for Edi

## Current State

Edi is a multi-project .NET 8 solution with four projects:
- **Edi.Wpf** — WPF desktop GUI (Windows-only)
- **Edi.Core** — Core library (mostly portable, some Windows deps)
- **Edi.Mvc** — ASP.NET Core REST API (nearly portable today)
- **Edi.Console** — CLI app (nearly portable today)

The biggest blockers are the WPF UI framework and the NAudio audio library, both of which are Windows-only.

---

## Blockers by Severity

### Critical (won't compile/run at all on Linux)

| Issue | Location | Notes |
|---|---|---|
| WPF framework (`net8.0-windows7.0`, `<UseWPF>true`) | `Edi.Wpf/Edi.Wpf.csproj` | Entire GUI project |
| All XAML UI files | `Edi.Wpf/Forms/*.xaml` | WPF-only markup |
| NAudio (`WaveOutEvent`, `CoreAudioApi`, `WaveOut.DeviceCount`) | `Edi.Core/Device/EStim/EStimProvider.cs`, `Edi.Wpf/Forms/MainWindow.xaml.cs:177` | Windows audio API |

### High (compiles but breaks core features)

| Issue | Location | Fix |
|---|---|---|
| `Process.Start("cmd", "/c start ...")` | `MainWindow.xaml.cs:298` | Use `xdg-open` on Linux |
| `Process.Start("explorer.exe", ...)` | `MainWindow.xaml.cs:379` | Use `xdg-open` on Linux |
| `ApartmentState.STA` threading | `Program.cs:48` | WPF-specific; remove with WPF |

### Medium (works but may behave unexpectedly)

| Issue | Location | Notes |
|---|---|---|
| `Environment.SpecialFolder.LocalApplicationData` | `Edi.Core/Services/Edi.cs:133` | .NET maps this to `~/.local/share` on Linux — likely fine |
| Named `Mutex` for single-instance | `Program.cs:8` | Works on Linux; fine to keep |
| `SerialPort` COM ports | `Edi.Core/Device/OSR/Connection/SerialConnection.cs` | On Linux, ports are `/dev/ttyUSB0` etc. — `System.IO.Ports` does work on Linux, but user must configure port name |

---

## Recommended Approach: Two-Phase Plan

### Phase 1 — Make Edi.Core + Edi.Mvc + Edi.Console portable (low effort, high value)

These three projects run headlessly and are close to cross-platform already. They expose the REST API and CLI surface, which means a Linux user could control Edi via the web UI or CLI even without a native GUI.

**Steps:**

1. **Remove NAudio from Edi.Core** or wrap it behind an interface so it can be stubbed/replaced.
   - Define `IAudioOutput` interface in Edi.Core.
   - Move NAudio-specific implementation into a `Edi.Audio.NAudio` project (Windows-only).
   - Add a no-op or ALSA/PipeWire implementation for Linux.
   - Candidate library for Linux audio: **LibVLCSharp** (cross-platform) or **OpenTK.Audio.OpenAL** (OpenAL).

2. **Fix serial port path handling** in `SerialConnection.cs`.
   - No code change needed — just document that Linux users set port to `/dev/ttyUSB0` (or similar) instead of `COM3`.
   - Optionally add runtime port discovery using `SerialPort.GetPortNames()` — this already works on Linux.

3. **Fix `OutputDir`** in `Edi.Core/Services/Edi.cs`.
   - `LocalApplicationData` maps to `~/.local/share` on Linux — verify this is acceptable, no code change likely needed.

4. **Remove `net8.0-windows` TFM restriction** from `Edi.Core.csproj` and `Edi.Mvc.csproj` if present.
   - Change to `net8.0` and add `[SupportedOSPlatform("windows")]` attributes on any remaining Windows-only APIs.

5. **Test `Edi.Mvc`** builds and runs headlessly on Linux — this is likely 80% done already.

**Estimated effort:** 1–3 days

---

### Phase 2 — Add a cross-platform GUI (medium effort)

Replace or supplement the WPF GUI with one that runs on Linux.

**Option A: Avalonia UI (recommended)**
- Avalonia is a WPF-compatible XAML framework that runs on Linux, macOS, and Windows.
- XAML syntax is very similar to WPF — many `.xaml` files can be ported with minor changes.
- Strong community, active development, good WPF migration guides.
- Add a new `Edi.Avalonia` project alongside `Edi.Wpf` so Windows users keep their existing experience until the port is proven.

**Option B: MAUI**
- Microsoft's official cross-platform framework. Linux support is community-driven and not officially supported — not recommended.

**Option C: Web UI only (skip native GUI)**
- Keep `Edi.Mvc` as the backend and build or use an existing web frontend.
- Lowest migration cost; works on any OS via browser.
- Already partially in place (Swagger UI at `/swagger`).

**Recommended:** Option A (Avalonia) for a native-feeling Linux GUI, with Option C as the fallback if time is limited.

**Estimated effort for Option A:** 1–2 weeks of porting XAML + code-behind.

---

## File Checklist

### Edi.Core (Phase 1)
- [ ] `Edi.Core/Edi.Core.csproj` — remove any `windows` TFM restriction
- [ ] `Edi.Core/Services/Edi.cs:133` — verify `LocalApplicationData` path is acceptable on Linux
- [ ] `Edi.Core/Device/EStim/EStimProvider.cs` — extract NAudio behind `IAudioOutput` interface
- [ ] `Edi.Core/Device/OSR/Connection/SerialConnection.cs` — document Linux port naming; no code change needed
- [ ] `Edi.Core/Device/Handy/HandyProvider.cs` — check for any Windows deps
- [ ] `Edi.Core/Device/AutoBlow/AutoBlowProvider.cs` — check for any Windows deps

### Edi.Mvc (Phase 1)
- [ ] Verify it builds with `net8.0` (not `net8.0-windows`)
- [ ] Run on Linux and smoke test REST endpoints

### Edi.Console (Phase 1)
- [ ] Verify it builds and runs on Linux

### Edi.Wpf (Phase 2 — new Avalonia project)
- [ ] `MainWindow.xaml` / `MainWindow.xaml.cs` — port to Avalonia
- [ ] `SimulateGame.xaml` — port to Avalonia
- [ ] `Program.cs` — remove `ApartmentState.STA`, replace Mutex pattern for Avalonia
- [ ] `MainWindow.xaml.cs:298` — replace `cmd /c start` with `Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true })`
- [ ] `MainWindow.xaml.cs:379` — replace `explorer.exe` with `xdg-open` (Linux) / `open` (macOS)
- [ ] `MainWindow.xaml.cs:177` — replace NAudio `WaveOut.DeviceCount` with cross-platform audio API

---

## Dependencies to Add/Replace

| Current | Replace With | Reason |
|---|---|---|
| NAudio (Windows) | LibVLCSharp or OpenTK.Audio.OpenAL | Cross-platform audio |
| WPF (`net8.0-windows`) | Avalonia UI | Cross-platform XAML GUI |
| `cmd /c start <url>` | `UseShellExecute = true` | Cross-platform URL open |
| `explorer.exe <path>` | `xdg-open <path>` on Linux | Cross-platform folder open |

---

## Quick Wins (can be done right now)

1. Fix the `cmd /c start` URL opener and `explorer.exe` folder opener to use `UseShellExecute = true` — these are 2-line fixes that make those features cross-platform today, even in the WPF project.
2. Verify `Edi.Mvc` and `Edi.Console` build on Linux without changes — they may already work.
3. Add `<TargetFramework>net8.0</TargetFramework>` to `Edi.Core` if not already, so it isn't accidentally locked to Windows.