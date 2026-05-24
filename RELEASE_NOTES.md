# BreakPulse Release Notes

## v1.0.0 — Initial Release

**May 24, 2026**

### What's New

BreakPulse is a floating circular HUD application for Windows that reminds you to take regular breaks. It sits above your applications and gently prompts you with exercise tips when it's time to rest.

### Features

✨ **Core**
- Floating circular HUD with visual break countdown (always on top)
- Customizable session timer (default: 25 minutes)
- Full-screen break overlay with exercise suggestions
- System tray integration (minimize to tray, no taskbar entry)
- Idle detection using Win32 API — respects your active work time
- Dark theme with customizable accent color

🛠️ **Settings Panel**
- Timer configuration (session length, break duration)
- Alert customization
- Display position and theme options
- Meeting detection — auto-skip breaks during Teams/Zoom calls

👥 **Enterprise Support**
- Optional team telemetry via REST API
- Heartbeat tracking with activity status
- Anonymization mode for privacy

### System Requirements

- **OS:** Windows 10 or 11
- **.NET Runtime:** .NET 9 or higher

### Installation & Usage

1. Download and extract `BreakPulse.exe` (published single-file executable)
2. Run the application — the HUD appears centered on your screen
3. Adjust settings via right-click context menu
4. (Optional) Add to Windows Startup for automatic launch on boot

### Documentation

- Full setup guide: See [README.md](README.md)
- Customization options: See the Customisation Guide section in README.md
- Enterprise team setup: See Enterprise / Team telemetry section in README.md

### Known Notes

- Break overlay is full-screen and cannot be dismissed early (by design)
- Team telemetry requires a custom REST endpoint — sample data is displayed by default
- Idle detection is Windows-specific and not available on other platforms

### Build & Development

For developers using JetBrains Rider:

```powershell
# Restore packages and run
dotnet run

# Publish as single executable
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

### Feedback & Contributing

This is the initial release. If you encounter issues or have feature suggestions, feel free to open an issue or submit a pull request.

---

**BreakPulse** — Take control of your break schedule. 🎯

