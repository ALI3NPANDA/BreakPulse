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

    public TrayService(
        Action onShowHud,
        Action onShowSettings,
        Action onQuit)
    {
        _icon = new TaskbarIcon
        {
            ToolTipText = "BreakPulse",
            Icon = LoadIcon(),
            ContextMenu = new System.Windows.Controls.ContextMenu
            {
                ItemsSource = new System.Collections.ObjectModel.ObservableCollection<System.Windows.Controls.MenuItem>
                {
                    new System.Windows.Controls.MenuItem { Header = "Show HUD", Command = new RelayCommand(_ => onShowHud()) },
                    new System.Windows.Controls.MenuItem { Header = "Settings", Command = new RelayCommand(_ => onShowSettings()) },
                    new System.Windows.Controls.MenuItem { Header = "-" }, // Separator replacement
                    new System.Windows.Controls.MenuItem { Header = "Quit BreakPulse", Command = new RelayCommand(_ => onQuit()) }
                }
            }
        };
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
