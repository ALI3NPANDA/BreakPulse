# BreakPulse — Windows Break Reminder

A floating circular HUD that lives above all your windows and reminds you to take breaks.
Built with WPF + .NET 9, designed for JetBrains Rider.

---

## Project structure

```
BreakPulse/
├── Models/
│   ├── AppSettings.cs      — All user preferences, persisted to %AppData%\BreakPulse\settings.json
│   └── TeamMember.cs       — Data model for enterprise telemetry
├── Services/
│   ├── TimerService.cs     — Session countdown, idle detection (Win32), break cycling
│   ├── TrayService.cs      — System tray icon + context menu (no taskbar entry)
│   └── TeamService.cs      — REST API client for team heartbeats and status polling
├── Views/
│   ├── Styles.xaml         — Shared dark theme: colors, buttons, toggles, sliders
│   ├── HudWindow.xaml/.cs  — Circular floating window, arc progress, always-on-top
│   ├── BreakOverlay.xaml/.cs — Full-screen dim overlay with countdown + exercise tips
│   └── SettingsWindow.xaml/.cs — Tabbed settings panel (Timer / Alerts / Display / Team)
├── App.xaml/.cs            — Bootstrap: tray, timer, HUD, wires all events
└── BreakPulse.csproj
```

---

## Prerequisites

- .NET 9 SDK — https://dotnet.microsoft.com/download
- JetBrains Rider 2024.1+
- Windows 10 or 11 (WPF + Win32 idle detection)

---

## Getting started in Rider

1. Open `BreakPulse.sln` in Rider
2. Rider will restore NuGet packages automatically
3. Press **Run** (Shift+F10) — the HUD appears centered on screen
4. Right-click the HUD or its tray icon for the context menu
5. Double-click the tray icon to bring the HUD back if you close it

---

## Adding a real app icon

Drop a 256×256 `.ico` file at `BreakPulse/Assets/icon.ico`.
The project already references it — Rider will pick it up on next build.
Until then a programmatic purple circle is used as a fallback.

---

## Enterprise / Team telemetry

The Team tab wires to a REST microservice you host. Expected contract:

```
POST {endpoint}/heartbeat
Body: { userId, displayName, teamName, sessionSeconds, onBreak, timestamp }

GET  {endpoint}/team
Returns: [ { id, displayName, initials, sessionTime, status, breaksTaken }, … ]

GET  {endpoint}/health
Returns: 200 OK (used for "Test connection" button)
```

`sessionSeconds` is posted once per minute via `TeamService.SendHeartbeatAsync()`.
When `AnonymizeTelemetry` is on, `userId` is a SHA-256 hash of the machine name and
`displayName` is sent as "Anonymous".

Without a real endpoint, the Team tab shows built-in sample data so you can
design the manager dashboard UI first.

---

## Customisation guide

| What                         | Where                                   |
|------------------------------|-----------------------------------------|
| Default session length       | `AppSettings.SessionMinutes` default    |
| Break messages               | `BreakOverlay.cs` → `Exercises[]` array |
| Accent color                 | `Styles.xaml` → `AccentColor`           |
| Add a new HUD position       | `HudPosition` enum + `HudWindow.PlaceOnScreen()` |
| Swap arc for a progress bar  | Replace `Canvas` in `HudWindow.xaml`    |
| Play a sound on break        | Add `MediaPlayer` call in `App.OnBreakDue()` |
| Calendar integration (skip)  | Implement `ICalendarProvider` and inject into `TimerService` |

---

## Publish as a single .exe

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Output: `bin\Release\net9.0-windows\win-x64\publish\BreakPulse.exe`

---

## Add to Windows startup

After publishing, create a shortcut to `BreakPulse.exe` and place it in:

```
%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup
```

Or add a registry key:

```
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
"BreakPulse" = "C:\path\to\BreakPulse.exe"
```
