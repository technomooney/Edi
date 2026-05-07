# Branch Changes: `linux-port-merged`

This document describes every change made in this branch relative to `master`, why each
change was made, and what the original programmer should consider before merging.

Branch: `linux-port-merged`  
Base: `master`  
Commits: 16 (see `git log master..linux-port-merged`)

---

## Overview

The two goals of this branch are:

1. **Make `Edi.Core` build and run on Linux** (and any non-Windows OS). The only
   Windows-specific dependency in Core is NAudio, used solely by the EStim device.
   All other NAudio imports in Core were stale/unused and have been removed.

2. **Add `Edi.Avalonia`** — a new GUI project that is a functional port of `Edi.Wpf`
   using [Avalonia UI 11.2](https://avaloniaui.net/). Avalonia targets `net9.0` (not
   `net9.0-windows7.0`) and runs on Windows, Linux, and macOS with the same binary.
   The WPF project is unchanged and still works on Windows.

These goals required a handful of small fixes to Core that are arguably improvements
regardless of platform (null guards, auto-discovery, layout fixes).

---

## Summary

| # | Area | Type | Safe to merge? |
|---|------|------|----------------|
| 1 | New `Edi.Avalonia` project | Additive | Yes — WPF is untouched |
| 2 | Conditional NAudio/LibVLC package refs | Core change | Yes — Windows build unchanged |
| 3 | `IAudioOutput` abstraction | Core change | Yes — interface is thin and correct |
| 4 | Remove stale `using NAudio.*` | Cleanup | Yes — imports were unused |
| 5 | Fix backslash path separator | Bug fix | Yes — strictly more correct |
| 6 | Cross-platform `EdiConfig.json` defaults | Config | Yes — doesn't overwrite user configs |
| 7 | `GamesConfig` auto-discovery | Feature | Review — `Games` key shape changed |
| 8 | `ApiBuilder` null guard | Bug fix | Yes — prevents crash on fresh install |
| 9 | `Edi.cs` fallback to auto-discovered game | Behaviour change | Review — priority order changed |
| 10 | Rescan button + layout fix | Feature + bug fix | Yes |
| 11 | Async window close fix | Bug fix | Yes — same fix needed in WPF too |
| 12 | `net8.0` → `net9.0` bump | Version bump | Review — consider LTS implications |
| 13 | `Edi.sln` add Avalonia project | Additive | Yes |
| 14 | `Edi.Wpf.csproj` cross-compilation support | Build fix | Yes — no effect on native Windows build |
| 15 | Cross-compile conditions in Core/Avalonia | Correctness fix | Yes — native builds unchanged |
| 16 | WPF `MainWindow` `GamesInfo` → `GetAll()` | Bug fix | Yes — compile error without this |

Items marked **Review** have small behaviour changes worth a second look before merging
to mainline. Everything else is either additive (new project, new files) or a
straightforward bug fix with no downside.

Detailed notes on each change follow below.

---

## Change 1 — New project: `Edi.Avalonia`

**Files added:** entire `Edi.Avalonia/` directory  
**Commit:** `30e7b70` + subsequent fixup commits

### What
A new C# project that replicates the functionality of `Edi.Wpf` using Avalonia UI
instead of WPF. It shares `Edi.Core` completely unchanged.

Key files:
| File | Purpose |
|------|---------|
| `Edi.Avalonia.csproj` | Targets `net9.0` (cross-platform, no Windows TFM). NAudio included conditionally on Windows only. |
| `Program.cs` | Entry point. `[STAThread]`, single-instance `Mutex`, `IHost` for DI/logging, then Avalonia desktop lifetime. |
| `App.axaml` / `App.axaml.cs` | Avalonia application root — equivalent to WPF's `App.xaml`. Uses `FluentTheme`. |
| `Forms/MainWindow.axaml` + `.cs` | Port of `Edi.Wpf/MainWindow`. Same controls, rewritten layout. |
| `Forms/SimulateGame.axaml` + `.cs` | Port of `Edi.Wpf/SimulateGame`. |

### Why
`Edi.Wpf` depends on `net9.0-windows7.0` and WPF, both of which are Windows-only at
the SDK level — they cannot even be compiled on Linux. Avalonia UI is a WPF-lineage
framework (originally forked from WPF's design) that compiles and runs natively on
Windows, Linux (X11/Wayland), and macOS using the same C# and XAML-like markup.

### Avalonia vs WPF — what a WPF developer needs to know

This is the most foreign part of the branch for someone who has not used Avalonia before.
The differences are deliberate design choices, not bugs, and the two frameworks are
close enough that the port was straightforward. Key points:

**File extension: `.axaml` not `.xaml`**  
Avalonia uses `.axaml` (Avalonia XAML) so that IDEs can distinguish it from WPF XAML
and apply the correct IntelliSense/designer. The syntax inside the file is essentially
the same as WPF XAML. The XML namespace root is `https://github.com/avaloniaui` instead
of `http://schemas.microsoft.com/winfx/2006/xaml/presentation`.

**No XAML designer (yet)**  
Avalonia has a previewer that works in Rider and VS Code via the Avalonia extension, but
it is not as mature as the WPF designer. The UI was built and tested at runtime. The
`Design.IsDesignMode` guard in `MainWindow`'s constructor prevents a NullReferenceException
when the previewer instantiates the window without running the full DI host.

**Data binding is nearly identical to WPF**  
`{Binding Path}`, `INotifyPropertyChanged`, `ObservableCollection<T>`, converters —
all work the same way. `DataContext` is set the same way. The only notable difference:
Avalonia uses compiled bindings (`{CompiledBinding}`) for performance, but classic
`{Binding}` still works and is what this port uses.

**`IValueConverter` works the same way**  
The `BoolToReadyIconConverter` at the bottom of `MainWindow.axaml.cs` is a standard
`IValueConverter` — same interface, same `Convert`/`ConvertBack` contract as WPF.

**Layout panels are the same, with one important difference**  
`DockPanel`, `StackPanel`, `Grid`, `ScrollViewer` all exist in Avalonia with the same
semantics as WPF. However, Avalonia does **not** support absolute pixel positioning
(`Canvas` with `Left`/`Top` attached properties works, but `Margin`-based absolute
positioning like WPF's `StackPanel` with fixed margins does not give the same results
cross-platform because DPI and font metrics differ). The Avalonia port uses proper
docking and grid layouts throughout instead of the fixed-margin approach in the WPF
version.

**`Content` vs `Header` on `TabItem`**  
WPF `TabItem` uses `Header` for the tab label. Avalonia uses the same — no change here.

**Event handler signatures are slightly different**  
WPF uses `RoutedEventArgs`; Avalonia has its own event arg types. For example:
- WPF closing: `CancelEventArgs` → Avalonia: `WindowClosingEventArgs`
- WPF selection changed: `SelectionChangedEventArgs` → same name, different namespace
- WPF range base: `RangeBaseValueChangedEventArgs` → same name, Avalonia namespace

These are cosmetic differences — the handler bodies are identical.

**Threading model is the same**  
Avalonia uses `Dispatcher.UIThread.Post()`/`InvokeAsync()` exactly like WPF's
`Application.Current.Dispatcher`. The calls in `MainWindow.axaml.cs` are a direct
translation.

**`OutputType=WinExe` is cross-platform in .NET 9**  
The `.csproj` uses `<OutputType>WinExe</OutputType>`. On Windows this means "no console
window" (Win32 `SUBSYSTEM:WINDOWS`). On Linux, .NET ignores the Win32 subsystem flag
entirely — the binary works fine. This was left as `WinExe` intentionally so that on
Windows the app launches without a console window, matching the WPF experience.

**`UsePlatformDetect()` selects the right backend automatically**  
`AppBuilder.Configure<App>().UsePlatformDetect()` picks:
- Windows → Win32 + Direct2D rendering (same quality as WPF's DirectX path)
- Linux → X11 or Wayland depending on the session
- macOS → Cocoa/Metal

No runtime `if (Windows) {...}` branching is needed in application code for UI concerns.

### What the original programmer should check
- **UI parity on Windows**: The Avalonia layout is not pixel-for-pixel identical to WPF.
  Spot-check all tabs and controls on a Windows machine to confirm behaviour matches.
  Particular attention to the device DataGrid, audio device selector, and COM port
  selector — these are the most interaction-heavy controls.
- **Playback button labels**: Icon/emoji characters (▶ ⏸ ■) were replaced with text
  labels (`Play`, `Pause`, `Stop`) because emoji rendering in Avalonia on some Linux
  fonts produced replacement boxes. These can be restored or improved with Avalonia's
  `SymbolIcon` control or a bundled icon font (e.g. Segoe MDL2 on Windows).
- **No Repack button**: The `Repack` button was removed because `IEdi` in `master` no
  longer exposes a `Repack()` method. If it needs to be surfaced again it needs to be
  re-added to `IEdi` first.
- **No Repack button**: The `Repack` button was removed from the Avalonia UI because
  `IEdi` no longer exposes a `Repack()` method (this was already the case in `master`).
  If Repack needs to be surfaced again, it needs to be re-added to `IEdi` and wired up.

---

## Change 2 — `Edi.Core.csproj`: platform-conditional package references

**File:** `Edi.Core/Edi.Core.csproj`  
**Commit:** `7dba50e`

### What
```xml
<!-- Before -->
<PackageReference Include="NAudio" Version="2.2.1" />

<!-- After -->
<PackageReference Include="NAudio" Version="2.2.1" Condition="$([MSBuild]::IsOSPlatform('Windows'))" />
<PackageReference Include="LibVLCSharp" Version="3.9.0" Condition="!$([MSBuild]::IsOSPlatform('Windows'))" />
```

Also added a compile-time constant that is defined only when building on Windows:

```xml
<PropertyGroup Condition="$([MSBuild]::IsOSPlatform('Windows'))">
  <DefineConstants>$(DefineConstants);WINDOWS_BUILD</DefineConstants>
</PropertyGroup>
```

### Why
NAudio is a Windows-only library — it uses Win32 `waveOut*` APIs and will not restore
or compile on Linux. The `Condition` attribute on `PackageReference` is an MSBuild
feature that evaluates at restore/build time (not at runtime), so Linux builds never
even try to download NAudio.

The `WINDOWS_BUILD` constant gates the `#if` blocks in C# source files (see Change 3).
This approach (compile-time selection) was chosen over `RuntimeInformation.IsOSPlatform()`
(runtime selection) because the two audio libraries have completely different native
dependencies — trying to load LibVLCSharp on Windows or NAudio on Linux would throw
immediately at runtime. Compile-time selection is cleaner: the wrong implementation
is never compiled into the binary at all.

`LibVLCSharp` is the .NET wrapper for `libvlc`, the library behind VLC media player.
It supports MP3 playback on Linux (and Windows/macOS) and is distributed as a NuGet
package that bundles its own native binaries on Windows and macOS. On Linux it uses
the system-installed `libvlc`.

### What the original programmer should check
- **Windows builds are unchanged**: NAudio is still restored and compiled in exactly
  as before. The `Condition` is only false on non-Windows.
- **Linux prerequisite**: `LibVLCSharp` on Linux requires the system `libvlc` library.
  On Debian/Ubuntu: `sudo apt install libvlc-dev`. On Arch: `sudo pacman -S vlc`.
  Without this the `LibVlcAudioOutput` constructor throws a `VLCException` at runtime
  when the EStim device is initialised. The app will still start — just the EStim audio
  device won't work if libvlc is missing. Worth documenting in the README.

---

## Change 3 — EStim audio abstraction (`IAudioOutput`)

**Files:**
- `Edi.Core/Device/EStim/IAudioOutput.cs` *(new)*
- `Edi.Core/Device/EStim/NAudioOutput.cs` *(new)*
- `Edi.Core/Device/EStim/LibVlcAudioOutput.cs` *(new)*
- `Edi.Core/Device/EStim/EStimDevice.cs` *(changed)*
- `Edi.Core/Device/EStim/EStimProvider.cs` *(changed)*

**Commit:** `7dba50e`

### What — before
`EStimDevice` held a `WaveOutEvent` (NAudio type) directly as a field and called
NAudio APIs throughout its methods. This meant the entire `Edi.Core` assembly had
a hard dependency on NAudio just to support one device type.

### What — after
A thin interface isolates the audio concern:

```csharp
public interface IAudioOutput : IDisposable
{
    int DeviceNumber { get; }
    float Volume { get; set; }
    void Load(string path, long startMs);  // prepare a file at a seek position
    void Play();
    void Pause();
    void Stop();
}
```

Two implementations, each gated by a compile-time constant so only one is ever compiled
into a given binary:

**`NAudioOutput`** — Windows only, entire file inside `#if WINDOWS_BUILD`:
- Wraps `WaveOutEvent` (NAudio's WAVEOUT API wrapper) for output device selection.
- Wraps `Mp3FileReader` for decoding. One reader is cached per file path to avoid
  reopening on repeated `Load()` calls (NAudio requires re-initialising `WaveOutEvent`
  after seeking, which the cache avoids).

**`LibVlcAudioOutput`** — Non-Windows only, entire file inside `#if !WINDOWS_BUILD`:
- Wraps LibVLCSharp's `MediaPlayer`.
- VLC does not support mid-stream seek-then-play the same way NAudio does, so `Load()`
  stores the path and start time and `Play()` constructs a new `Media` object with
  VLC's `:start-time` option (in seconds, floating point).

`EStimProvider` selects the implementation at compile time:

```csharp
private static IAudioOutput CreateAudioOutput(int deviceNumber)
{
#if WINDOWS_BUILD
    return new NAudioOutput(deviceNumber);
#else
    return new LibVlcAudioOutput(deviceNumber);
#endif
}
```

`EStimDevice` is now completely platform-agnostic — it calls `IAudioOutput` methods
and has no NAudio references.

### Why
The EStim device plays MP3 audio files through a selected sound card to drive EStim
hardware. NAudio is the natural choice on Windows (thin wrapper over OS APIs, low
latency). LibVLCSharp is the most capable cross-platform audio library available in
.NET — it handles codec decoding, device selection, and playback without any OS-specific
code. The interface pattern is the standard way to swap implementations; the compile-time
guard (rather than a runtime strategy pattern) ensures neither library is loaded on the
wrong platform.

### What the original programmer should check
- **Seek accuracy**: `NAudioOutput` seeks by setting `Mp3FileReader.CurrentTime`
  precisely to a `TimeSpan`. `LibVlcAudioOutput` passes `:start-time` to VLC as a
  floating-point seconds value. VLC may have coarser frame-alignment on seek depending
  on the MP3's bit rate and frame size. If EStim pulse timing is sensitive to sub-100ms
  accuracy, the Linux implementation should be measured against the Windows one.
- **Audio device selection on Linux**: The `deviceNumber` parameter is used by
  `NAudioOutput` to pick among Windows `waveOut` devices. LibVLCSharp does not expose
  the same numeric device index concept — `LibVlcAudioOutput` currently ignores
  `deviceNumber` and uses VLC's default output device. A future improvement would be
  to enumerate ALSA/PulseAudio/PipeWire devices and pass the correct sink name to VLC.
- **Mp3FileReader caching in `NAudioOutput`**: One reader is kept open per unique file
  path. With a large EStim audio library this could accumulate open file handles. The
  old code did not cache (it reopened on each call), so this is a behavioural change
  that trades file handles for lower latency. Worth noting if users report handle leaks.

---

## Change 4 — Remove stale NAudio `using` statements across Core

**Files:** `AutoBlowProvider.cs`, `HandyProvider.cs`, `AudioGallery.cs`,
`AudioRepository.cs`, `IndexRepository.cs`, `DevicePlayer.cs`,
`MultiChannelPlayer.cs`, `Edi.cs`, `Repacker.cs`  
**Commit:** `7dba50e`

### What
Removed `using NAudio.*` directives from files that imported NAudio but did not
actually call any NAudio API. One case (`AudioRepository.cs`) had 31 lines of
NAudio-related code that was already dead (the methods were never called from Core).
Those lines were deleted.

### Why
On Linux, NAudio is not compiled in. A `using NAudio.Wave` statement causes a
compiler error even if the symbol is never used. These were clearly left over from
earlier refactoring.

### What the original programmer should check
- `AudioRepository.cs` had some deleted lines. Confirm nothing depended on that code.
  In `master` at the time of branching those methods were already unreachable from
  the public API.

---

## Change 5 — Fix hardcoded backslash path separator in `DefinitionRepository`

**File:** `Edi.Core/Gallery/Definition/DefinitionRepository.cs`  
**Commit:** `039217c`

### What
The original code used string manipulation with `\\` to strip path prefixes and
split file paths:

```csharp
// Before
var removePathBase = GalleryDir.FullName.EndsWith("\\") ? ... + "\\";
var pathSplit = file.FullName.Replace(removePathBase, "").Split('\\');
```

Replaced with `Path.Combine()` and `Path.DirectorySeparatorChar`-aware APIs.

### Why
On Linux, `\` is a valid filename character, not a directory separator. The old code
silently failed to strip the path prefix, so every gallery name included the full
absolute path. This caused gallery lookup to fail (no names matched).

### What the original programmer should check
- This is a genuine cross-platform bug that also affects Windows in edge cases (e.g.,
  network paths with mixed separators). The fix is safe and strictly correct.

---

## Change 6 — `EdiConfig.json` cross-platform defaults

**Files:** `Edi.Wpf/EdiConfig.json`, `EdiConfig.json` (repo root)  
**Commits:** `039217c`, `30e7b70`, `d25c5ce`, `e0d1f02`

### What
The original config had hardcoded absolute Windows paths as defaults:

```json
// Before (Edi.Wpf/EdiConfig.json)
"GalleryPath": "D:\\Games\\Edi Gallery\\",
"GamesInfo": [{ "Name": "...", "Path": "D:\\..." }]
```

Replaced with relative paths and the new `GalleryRootPath` key:

```json
// After
"GalleryRootPath": "./Gallery"
```

### Why
Absolute `D:\` paths don't exist on Linux or on another developer's Windows machine.
They cause startup crashes on fresh installs. Relative paths anchored to the
executable directory work everywhere.

### What the original programmer should check
- Existing users who have customised `EdiConfig.json` are unaffected — their file
  won't be overwritten by this change.
- If the WPF app's config migration (loading old config JSON) tries to deserialise
  the old `GamesInfo` array key, it will just get the default empty list — no crash.

---

## Change 7 — `GamesConfig`: auto-discover games from a root folder

**File:** `Edi.Core/Gallery/GamesConfig.cs`  
**Commits:** `91dde52`, `d25c5ce`, `51b927b`, `9d224d4`

### What
Previously `GamesConfig` held a manually-maintained `ObservableCollection<GameInfo>`
that the user had to populate by hand in `EdiConfig.json`. Now:

- `GalleryRootPath` — a single folder. Any immediate subfolder containing
  `Definitions.csv` or `Definitions_auto.csv` is automatically treated as a game.
- `GetAll()` — returns the cached game dict, or runs a scan if the cache is empty.
- `Rescan()` — walks the disk, repopulates the cache, triggers
  `INotifyPropertyChanged` so `ConfigurationManager` auto-saves.
- `ResolvedRootPath` — resolves relative paths against `AppContext.BaseDirectory` so
  `./Gallery` works regardless of the process working directory (`dotnet run` vs
  double-clicking the exe).
- `SelectedGameinfo` and `Games` dict are kept for backwards compatibility with the
  WPF UI and the `SelectGame` REST API endpoint.

### Why
Manually listing every game in a config file is tedious and error-prone. The
conventions for what counts as a game folder already exist (`Definitions.csv`). This
makes the common case (one root folder, many game subfolders) zero-config.

### What the original programmer should check
- The WPF `MainWindow.xaml` still binds to `GamesInfo` (`ObservableCollection`). That
  binding will need to be updated if the WPF UI is to show the auto-discovered list.
  Currently on WPF, the game selector works via the existing `SelectedGameinfo` path
  in `IEdi`. The Avalonia UI uses `GamesConfig.GetAll()` directly.
- `Games` is now a `Dictionary<string, string>` (name → path) rather than
  `ObservableCollection<GameInfo>`. If anything in WPF or the REST API was
  deserialising `Games` as `GameInfo` objects from JSON, that key shape has changed.

---

## Change 8 — `ApiBuilder`: null guard for missing gallery path

**File:** `Edi.Core/Services/ApiBuilder.cs`  
**Commit:** `30e7b70`

### What
```csharp
// Before — crashes if GalleryPath is null or empty
var galleryPath = new DirectoryInfo(config.Get<GalleryConfig>().GalleryPath).FullName;
app.UseStaticFiles(...);

// After — skips static file hosting gracefully
var galleryPath = config.Get<GalleryConfig>().GalleryPath;
if (!string.IsNullOrEmpty(galleryPath) && Directory.Exists(galleryPath))
{
    app.UseStaticFiles(...);
}
```

### Why
On a fresh install (or Linux where there's no pre-configured gallery path), `GalleryPath`
is empty. The original code threw `ArgumentNullException` constructing `DirectoryInfo`,
crashing the entire app before the UI appeared. The fix lets the app start and serve the
REST API even without a gallery configured; static assets are just not available until
the user sets a path and restarts.

### What the original programmer should check
- This is a genuine crash bug on any fresh Windows install too (not just Linux).
- The `/Edi/Assets` endpoint will return 404 until a valid gallery path is set.
  Clients should handle that gracefully.

---

## Change 9 — `Edi.cs`: fallback to auto-discovered game when path is null

**File:** `Edi.Core/Services/Edi.cs`  
**Commit:** `e0d1f02`

### What
```csharp
// Before
return ConfigurationManager.Get<GalleryConfig>()?.GalleryPath ?? "./";

// After
return ConfigurationManager.Get<GamesConfig>()?.GetAll().Values.FirstOrDefault()
    ?? ConfigurationManager.Get<GalleryConfig>()?.GalleryPath
    ?? "./";
```

### Why
`Edi.Init()` is called on startup with no arguments. If `GalleryPath` is empty (fresh
install), it was passing `"./"` to all gallery repos, which would scan the executable
directory — finding nothing and silently loading an empty gallery. With auto-discovery,
the first game found in `GalleryRootPath` is used automatically instead.

### What the original programmer should check
- Priority order is now: `GamesConfig` → `GalleryConfig.GalleryPath` → `./`. This is
  a behaviour change for users who have `GalleryPath` set but no `GalleryRootPath`.
  Their existing config will continue to work via the `GalleryConfig` fallback.

---

## Change 10 — `MainWindow` (Avalonia): Rescan button and game selector fixes

**Files:** `Edi.Avalonia/Forms/MainWindow.axaml`, `MainWindow.axaml.cs`,
`Edi.Core/Gallery/GamesConfig.cs`  
**Commits:** `0a0b53c`, `9d224d4`

### What
- A **Rescan** button sits next to the game `ComboBox`. Clicking it calls
  `GamesConfig.Rescan()`, repopulates the dropdown, and auto-saves the config.
- The game selector row uses a two-column `Grid` (fixed widths) instead of a
  `StackPanel`, so long game names are truncated with ellipsis rather than overflowing
  and pushing the right-side controls off-screen.
- `btnRescan_Click` **unsubscribes** `Game_SelectionChanged` before swapping
  `ItemsSource`, then resubscribes after. This prevents the `SelectionChanged` event
  from firing on `SelectedIndex = 0` and triggering `edi.Init()` with whatever game
  happens to be first in the newly scanned list.

### Why
Without the event unsubscribe, clicking Rescan could send unexpected commands to
connected devices (OSR movement, EStim output) because `edi.Init()` reloads the
gallery and the player picks up from the first track.

---

## Change 11 — Fix window close not awaiting device shutdown

**File:** `Edi.Avalonia/Forms/MainWindow.axaml.cs`  
**Commit:** `b3ea2ef`

### What
```csharp
// Before — fire-and-forget, window closes before Pause() completes
private async void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
{
    await edi.Player.Pause();
    Close();
}

// After — cancel the close, await Pause(), then close for real
private bool _isClosing;
private async void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
{
    if (_isClosing) return;
    e.Cancel = true;
    _isClosing = true;
    await edi.Player.Pause();
    await Task.Delay(500);
    Closing -= MainWindow_Closing;
    Close();
}
```

### Why
`async void` event handlers are fire-and-forget — the runtime does not await them.
Avalonia proceeds with closing immediately, disposing the window and tearing down the
host before `Pause()` completes. This left OSR and EStim devices in a playing state
after the UI exited. The fix cancels the OS close, awaits the shutdown sequence, then
issues a real `Close()`.

### What the original programmer should check
- The same pattern applies to `Edi.Wpf/Forms/MainWindow.xaml.cs` if WPF has the same
  bug (it likely does — WPF also doesn't await `async void` closing handlers).

---

## Change 12 — .NET version bump: `net8.0` → `net9.0`

**Files:** `Edi.Console/Edi.Consola.csproj`, `Edi.Core/Edi.Core.csproj`,
`Edi.Mvc/Edi.Mvc.csproj`, `Edi.Wpf/Edi.Wpf.csproj`  
**Commit:** `30e7b70`

### What
`<TargetFramework>` changed from `net8.0` (or `net8.0-windows7.0` for WPF) to `net9.0`
(or `net9.0-windows7.0` for WPF).

### Why
The Linux dev machine only had .NET 9 SDK installed. All projects must target the same
runtime for `dotnet build` to succeed. .NET 9 is the current stable release (November
2024) and is a supported upgrade path from .NET 8.

### What the original programmer should check
- .NET 9 is not an LTS release (that's .NET 8 and the upcoming .NET 10). If the
  project is distributed as self-contained, the runtime version is bundled and this is
  not a user concern. If it runs on the user's installed runtime, users will need .NET 9.
- There are no breaking API changes between .NET 8 and .NET 9 that affect this
  codebase. The bump is mechanical.

---

## Change 13 — `Edi.sln`: added `Edi.Avalonia` project

**File:** `Edi.sln`  
**Commit:** `30e7b70`

### What
Added the `Edi.Avalonia` project to the solution file so it appears in Visual Studio /
Rider alongside the existing projects.

---

## Change 14 — `Edi.Wpf.csproj`: enable cross-compilation from Linux

**File:** `Edi.Wpf/Edi.Wpf.csproj`  
**Commit:** `d61064c`

### What
Four additions to the WPF project file:

```xml
<EnableWindowsTargeting>true</EnableWindowsTargeting>
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
<DefineConstants>$(DefineConstants);WINDOWS_BUILD</DefineConstants>
```
```xml
<PackageReference Include="NAudio" Version="2.2.1" />
```

### Why
Without these, `dotnet build Edi.Wpf` fails with two separate errors on Linux:

- **NETSDK1100** (`EnableWindowsTargeting` missing): The SDK refuses to download Windows
  reference assemblies on a non-Windows build machine, so no WPF or `Microsoft.Windows`
  types can be resolved — not even by the C# compiler.
- **NETSDK1082** (`RuntimeIdentifier` missing): `SelfContained=true` without a RID
  defaults to `linux-x64`. There is no WPF runtime pack for `linux-x64`, so the restore
  step fails.

`WINDOWS_BUILD` is defined unconditionally here because WPF is a Windows-only project —
the condition is always true. Without it, `#if WINDOWS_BUILD` guards in referenced code
would silently evaluate as false during a Linux cross-compile, producing incorrect results.

NAudio was being pulled in transitively from `Edi.Core` (which has a conditional
`PackageReference`). That transitive reference disappears on Linux because `Edi.Core`'s
condition uses `IsOSPlatform('Windows')` (false on Linux). Making the WPF dependency
explicit is correct regardless of platform.

### What the original programmer should check
- `RuntimeIdentifier=win-x64` means `dotnet build` from Linux always produces a
  `win-x64` binary. If the original project previously produced an `Any CPU` or
  `win-x86` output, the publish profile should be checked. The `RuntimeIdentifier` here
  matches the existing `SelfContained=true` + `PublishSingleFile=true` configuration
  which already implies a single-RID output.
- `EnableWindowsTargeting` is a .NET 7+ feature. It has no effect on Windows builds.

---

## Change 15 — `Edi.Core`/`Edi.Avalonia` conditions: support cross-compilation

**Files:** `Edi.Core/Edi.Core.csproj`, `Edi.Avalonia/Edi.Avalonia.csproj`  
**Commit:** `d61064c`

### What
All three `WINDOWS_BUILD` conditions and the NAudio/LibVLCSharp conditions were updated
from:

```xml
Condition="$([MSBuild]::IsOSPlatform('Windows'))"
```

to:

```xml
Condition="$([MSBuild]::IsOSPlatform('Windows')) or $(RuntimeIdentifier.StartsWith('win'))"
```

(and the inverse conditions for LibVLCSharp updated to match.)

### Why
`IsOSPlatform('Windows')` checks the OS the *build is running on*, not the OS being
*targeted*. When cross-compiling on Linux with `RuntimeIdentifier=win-x64` (e.g.
`dotnet publish -r win-x64` to produce a Windows release build), the condition was
`false` even though the output was a Windows binary. This had two incorrect effects:

1. **Wrong audio backend**: LibVLCSharp would be compiled into the Windows binary instead
   of NAudio. The resulting exe would crash at startup on Windows because libvlc is not
   present on a typical Windows machine.
2. **`WINDOWS_BUILD` not defined**: All `#if WINDOWS_BUILD` guards would evaluate as
   false in a cross-compiled Windows binary, causing the same LibVLC/NAudio selection
   issue at the `EStimProvider` factory.

The `RuntimeIdentifier.StartsWith('win')` addition means the correct backend is always
selected based on the target platform, regardless of which OS the build is running on.

### What the original programmer should check
- On a native Windows build (no RID specified), `RuntimeIdentifier` is empty so
  `StartsWith('win')` is false — behaviour is identical to before.
- On a native Linux build targeting Linux, both conditions are false — LibVLCSharp is
  selected as before.
- Only the cross-compile case (Linux → Windows) is affected, and it now correctly selects
  NAudio.

---

## Change 16 — `Edi.Wpf/MainWindow.xaml.cs`: fix `GamesInfo` references broken by `GamesConfig` refactor

**File:** `Edi.Wpf/Forms/MainWindow.xaml.cs`  
**Commit:** `4499c1a`

### What
Two call sites in the browse-for-game dialog handler referenced `GamesConfig.GamesInfo`
(the old `ObservableCollection<GameInfo>`), which was removed in Change 7:

```csharp
// Before
var game = new GameInfo(configPath, configPath);
if (!gamesConfig.GamesInfo.Any(x => x.Path == configPath))
    gamesConfig.GamesInfo.Add(game);

// After
var game = new GameInfo(configPath, configPath);
if (!gamesConfig.GetAll().ContainsValue(configPath))
    gamesConfig.Games[configPath] = configPath;
```

### Why
This was a compile error (`CS1061`) introduced by Change 7's removal of `GamesInfo`.
The WPF project would not build at all without this fix. The new code uses `GetAll()`
for the existence check (reads from cache or scans) and directly assigns to `Games` to
persist the manually-browsed entry alongside the auto-discovered ones.

### What the original programmer should check
- The old code added a `GameInfo(configPath, configPath)` — using the full file path as
  both the `Name` and the `Path`. The new code preserves this: `Games[configPath] =
  configPath` gives the same name = path result. The display in the WPF ComboBox will
  show the full path as the game name, which was always the case with this browse path.
  If a cleaner name is desired, `Path.GetFileNameWithoutExtension(configPath)` could be
  used as the key instead.

