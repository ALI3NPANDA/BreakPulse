using System.Windows;
using BreakPulse.Models;
using BreakPulse.Services;
using BreakPulse.Views;

namespace BreakPulse;

public partial class App : Application
{
    private TrayService? _tray;
    private TimerService? _timer;
    private TeamService? _team;
    private HudWindow? _hud;
    private AppSettings _settings = AppSettings.Load();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _team = new TeamService(_settings);
        _timer = new TimerService(_settings);
        _tray = new TrayService(OnShowHud, OnShowSettings, OnQuit);
        _timer.BreakDue += OnBreakDue;
        _timer.TickOccurred += OnTick;
        _timer.Start();

        // App runs silently in the background — the BreakOverlay appears when a break is due.
        // Users can still open the HUD manually via the tray icon → "Show HUD".
    }

    // ── HUD ──────────────────────────────────────────────────────────────────

    private void ShowHud()
    {
        if (_hud is { IsVisible: true }) return;
        _hud = new HudWindow(_settings, _timer!, OnShowSettings);
        _hud.Show();
    }

    private void OnShowHud()
    {
        Dispatcher.Invoke(ShowHud);
    }

    private void OnTick(TimeSpan remaining, double progress)
    {
        Dispatcher.Invoke(() =>
        {
            _hud?.UpdateProgress(remaining, progress);
            _tray?.UpdateStatus(remaining, _timer!.IsOnBreak);
        });
    }

    // ── Break alert ───────────────────────────────────────────────────────────

    private void OnBreakDue()
    {
        // Use BeginInvoke so the overlay opens AFTER the current DispatcherTimer tick
        // handler returns. Calling ShowDialog() from inside a tick (via Invoke) creates
        // a nested frame while the tick is still on the call stack, preventing subsequent
        // ticks from firing — which broke OnTimerTick in BreakOverlay.
        Dispatcher.BeginInvoke(() =>
        {
            // Enter break mode first so the overlay gets correct duration + IsLongBreak
            _timer!.StartBreak();
            var overlay = new BreakOverlay(_settings, _timer!);
            overlay.ShowDialog();
        });
    }

    // ── Settings ──────────────────────────────────────────────────────────────

    private void OnShowSettings()
    {
        Dispatcher.Invoke(() =>
        {
            var win = new SettingsWindow(_settings, _timer!, _team!);
            win.ShowDialog();
            // Settings are saved and applied inside SettingsWindow.Save_Click.
            // Re-apply here in case window was closed via OS X button after edits.
            _timer!.ApplySettings(_settings);
        });
    }

    // ── Quit ─────────────────────────────────────────────────────────────────

    private void OnQuit()
    {
        _timer?.Stop();
        _tray?.Dispose();
        Shutdown();
    }
}
