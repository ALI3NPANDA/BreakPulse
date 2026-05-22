using System.Drawing;
using BreakPulse.Models;
using Hardcodet.Wpf.TaskbarNotification;

namespace BreakPulse.Services;

/// <summary>
/// Manages the Windows system tray icon and right-click context menu.
/// BreakPulse lives here when minimised — no taskbar entry needed.
/// </summary>
public class TrayService : IDisposable
{
    private readonly TaskbarIcon _icon;
    private readonly System.Windows.Controls.MenuItem _statusItem;

    public TrayService(
        Action onShowHud,
        Action onShowSettings,
        Action onQuit)
    {
        _statusItem = new System.Windows.Controls.MenuItem
        {
            Header = "⏱  --:-- until break",
            //Command = new RelayCommand(_ => onShowHud())
        };
        _icon = new TaskbarIcon
        {
            ToolTipText = "BreakPulse",
            Icon = LoadIcon(),
            ContextMenu = new System.Windows.Controls.ContextMenu
            {
                ItemsSource = new System.Collections.ObjectModel.ObservableCollection<System.Windows.Controls.MenuItem>
                {
                    _statusItem,
                    new()
                        { Header = "Settings", Command = new RelayCommand(_ => onShowSettings()) },
                    new()
                        { Header = "Quit BreakPulse", Command = new RelayCommand(_ => onQuit()) },
                },
            },
        };
    }

    /// <summary>Called on every timer tick to keep the tray item header up to date.</summary>
    public void UpdateStatus(TimeSpan remaining, bool isOnBreak)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            _statusItem.Header = isOnBreak
                ? $"🟢  {remaining:mm\\:ss} break remaining"
                : $"⏱  {remaining:mm\\:ss} until break";
        });
    }

    private static Icon LoadIcon()
    {
        var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.FillEllipse(new SolidBrush(Color.FromArgb(108, 99, 255)), 2, 2, 28, 28);
        g.DrawEllipse(new Pen(Color.White, 1.5f), 2, 2, 28, 28);
        return Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        _icon?.Dispose();
    }
}
