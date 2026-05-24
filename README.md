# BreakPulse — Windows Break Reminder

A floating circular HUD that lives above all your windows and reminds you to take breaks. BreakPulse sits above your applications and gently prompts you with exercise tips when it's time to rest.

Built with WPF + .NET 9, designed for JetBrains Rider.

**Version:** 1.0.0 | **Released:** May 24, 2026

---

## ✨ Features

### Core
- **Floating circular HUD** with visual break countdown (always on top)
- **Customizable session timer** (default: 60 minutes, configurable 1-240 min)
- **Full-screen break overlay** with exercise suggestions
- **System tray integration** (minimize to tray, no taskbar entry)
- **Idle detection** using Win32 API — respects your active work time
- **Dark theme** with customizable accent color and appearance

### Settings Panel (3 tabs)
- **⏱ Timer** — Session & break length, long breaks, pre-warning, meeting detection, idle detection, auto-resume, startup
- **🔔 Alerts** — Customize 10 exercise tips, sound alerts, keyboard shortcut dismissal, full-screen break blocking
- **🎨 Appearance** — Overlay/block colors (with hex picker), progress arc, countdown timer, exercise hints, always-on-top

---

## 🏗️ Project structure

```
BreakPulse/
├── Models/
│   ├── AppSettings.cs      — All user preferences, persisted to %AppData%\BreakPulse\settings.json
│   └── TeamMember.cs       — Data model for enterprise telemetry
├── Services/
│   ├── TimerService.cs     — Session countdown, idle detection (Win32), break cycling
│   └── TrayService.cs      — System tray icon + context menu (no taskbar entry)
├── Views/
│   ├── Styles.xaml         — Shared dark theme: colors, buttons, toggles, sliders
│   ├── HudWindow.xaml/.cs  — Circular floating window, arc progress, always-on-top
│   ├── BreakOverlay.xaml/.cs — Full-screen dim overlay with countdown + exercise tips
│   └── SettingsWindow.xaml/.cs — Tabbed settings panel (⏱ Timer / 🔔 Alerts / 🎨 Appearance)
├── App.xaml/.cs            — Bootstrap: tray, timer, HUD, wires all events
└── BreakPulse.csproj
```

---

## 📋 System Requirements

- **OS:** Windows 10 or 11
- **.NET Runtime:** .NET 9 or higher
- **.NET SDK:** 9.0 or higher (for development)
- **IDE:** JetBrains Rider 2024.1+ (recommended for development)

---

## 🚀 Installation & Usage

### For Users

1. Download and extract `BreakPulse.exe` (published single-file executable)
2. Run the application — the HUD appears centered on your screen
3. Adjust settings via right-click context menu or tray icon
4. (Optional) Add to Windows Startup for automatic launch on boot

### For Developers

Getting started in Rider:

1. Install .NET 9 SDK — https://dotnet.microsoft.com/download
2. Open `BreakPulse.sln` in Rider
3. Rider will restore NuGet packages automatically
4. Press **Run** (Shift+F10) — the HUD appears centered on screen
5. Right-click the HUD or its tray icon for the context menu
6. Double-click the tray icon to bring the HUD back if you close it

---

## 🎨 Adding a Custom App Icon

Drop a 256×256 `.ico` file at `BreakPulse/Assets/icon.ico`.
The project already references it — Rider will pick it up on next build.
Until then a programmatic purple circle is used as a fallback.

---

## 📦 Build & Publish

### Run in Debug Mode

```powershell
dotnet run
```

### Publish as a Single Executable

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Output: `bin\Release\net9.0-windows\win-x64\publish\BreakPulse.exe`

---

## ⚙️ Customisation Guide

All core settings are available in the Settings window. Here's how to customize:

### Via UI (Settings Window)

| What                         | Where                                   |
|------------------------------|-----------------------------------------|
| Session timer length         | Timer tab → "Work session" slider (1–240 min) |
| Break duration               | Timer tab → "Break duration" slider (1–30 min) |
| Long break interval          | Timer tab → "Long break after X sessions" |
| Exercise tips                | Alerts tab → Edit 10 customizable slots |
| Sound alert                  | Alerts tab → Toggle "Play a sound on break" |
| Block screen during break    | Alerts tab → Toggle "Block screen during break" |
| Overlay background color     | Appearance tab → Color picker (hex values) |
| Particle accent color        | Appearance tab → Color picker |
| Particle wave color          | Appearance tab → Color picker |
| Block screen overlay color   | Appearance tab → Color picker |
| Progress arc display         | Appearance tab → Toggle "Show progress arc" |
| Countdown timer display      | Appearance tab → Toggle "Show countdown timer" |
| Exercise suggestion display  | Appearance tab → Toggle "Show exercise suggestion" |
| Always on top                | Appearance tab → Toggle "Always on top" |
| Skip during meetings         | Timer tab → Toggle "Skip during meetings" |
| Detect idle time             | Timer tab → Toggle "Detect idle time" |
| Launch on Windows startup    | Timer tab → Toggle "Launch on Windows startup" |

### Via Code (Advanced)

| What                         | Where                                   |
|------------------------------|-----------------------------------------|
| Add a new HUD position       | `HudPosition` enum + `HudWindow.PlaceOnScreen()` |
| Swap arc for a progress bar  | Replace `Canvas` in `HudWindow.xaml`    |
| Custom meeting detection     | Implement `ICalendarProvider`, pass to `TimerService` |

---

## 🔧 Add to Windows Startup

After publishing, create a shortcut to `BreakPulse.exe` and place it in:

```
%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup
```

Or add a registry key:

```
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
"BreakPulse" = "C:\path\to\BreakPulse.exe"
```

---

## 📝 Known Notes & Limitations

- **Break overlay is full-screen** and cannot be dismissed early (by design)
- **Idle detection** via Win32 API — Windows-specific, not cross-platform
- **Meeting detection** checks for Teams/Zoom process names; custom calendar integration available via `ICalendarProvider`
- **Settings are persisted** to `%AppData%\BreakPulse\settings.json`

---

## 💬 Feedback & Contributing

This is the initial release. If you encounter issues or have feature suggestions, feel free to open an issue or submit a pull request.

---

## 📄 License

See [LICENSE](LICENSE) for details.

---

**BreakPulse** — Take control of your break schedule. 🎯
