using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using BreakPulse.Models;
using BreakPulse.Services;

namespace BreakPulse.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _s;
    private readonly TimerService _timer;
    private readonly TeamService _team;

    // 10 exercise-slot text boxes, built once in BuildExerciseSlots()
    private readonly List<TextBox> _exerciseBoxes = new();

    // Page references for navigation
    private readonly ScrollViewer[] _pages;

    public SettingsWindow(AppSettings settings, TimerService timer, TeamService team)
    {
        InitializeComponent();
        _s = settings;
        _timer = timer;
        _team = team;
        _pages = new[] { TimerPage, AlertsPage, DisplayPage};
        BuildExerciseSlots();
        LoadFromSettings();
    }

    // ── Title bar drag ───────────────────────────────────────────────────────

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
            DragMove();
    }

    // ── Navigation ───────────────────────────────────────────────────────────

    private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_pages == null) return;
        int idx = NavList.SelectedIndex;
        for (var i = 0; i < _pages.Length; i++)
        {
            _pages[i].Visibility = i == idx ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    // ── Color picker handlers ─────────────────────────────────────────────────

    private void BgColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(BgColorBox.Text);
            BgColorPreview.Background = new SolidColorBrush(color);
        }
        catch
        { /* invalid color — ignore */
        }
    }

    private void AccentColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(AccentColorBox.Text);
            AccentColorPreview.Background = new SolidColorBrush(color);
        }
        catch
        { /* invalid color — ignore */
        }
    }

    private void WaveColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(WaveColorBox.Text);
            WaveColorPreview.Background = new SolidColorBrush(color);
        }
        catch
        { /* invalid color — ignore */
        }
    }

    private void BlockColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(BlockColorBox.Text);
            BlockColorPreview.Background = new SolidColorBrush(color);
        }
        catch
        { /* invalid color — ignore */
        }
    }

    private void BlockOpacitySlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
    {
        BlockOpacityVal.Text = $"{(int)BlockOpacitySlider.Value}%";
    }

    // ── Exercise list UI ──────────────────────────────────────────────────────

    private void BuildExerciseSlots()
    {
        for (var i = 0; i < 10; i++)
        {
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 6), LastChildFill = true };
            var label = new TextBlock
            {
                Text = $"{i + 1}.",
                Foreground = new SolidColorBrush(Color.FromArgb(0x88, 0xFF, 0xFF, 0xFF)),
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
                FontSize = 13,
                Width = 26,
                VerticalAlignment = VerticalAlignment.Center,
            };
            DockPanel.SetDock(label, Dock.Left);
            var box = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)),
                CaretBrush = new SolidColorBrush(Color.FromRgb(0x60, 0xCD, 0xFF)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x25, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 7, 10, 7),
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = false,
            };
            _exerciseBoxes.Add(box);
            row.Children.Add(label);
            row.Children.Add(box);
            ExerciseListPanel.Children.Add(row);
        }
    }

    // ── Load ──────────────────────────────────────────────────────────────

    private void LoadFromSettings()
    {
        // Timer tab
        SessionSlider.Value = _s.SessionMinutes;
        BreakSlider.Value = _s.BreakMinutes;
        LongBreakSlider.Value = _s.SessionsBeforeLong;
        LongBreakDurSlider.Value = _s.LongBreakMinutes;
        PreWarnSlider.Value = _s.PreWarningMinutes;
        SkipMeetingsToggle.IsChecked = _s.SkipDuringMeetings;
        IdleToggle.IsChecked = _s.DetectIdle;
        AutoResumeToggle.IsChecked = _s.AutoResume;
        LaunchOnStartupToggle.IsChecked = _s.LaunchOnStartup;

        // Alerts tab
        //SoundToggle.IsChecked = _s.PlaySound;
        ShortcutToggle.IsChecked = _s.RequireShortcut;
        BlockScreenToggle.IsChecked = _s.BlockScreenOnBreak;

        // Exercise slots
        List<string> exercises = _s.Exercises;
        for (var i = 0; i < _exerciseBoxes.Count; i++)
        {
            _exerciseBoxes[i].Text = i < exercises.Count ? exercises[i] : "";
        }

        // Appearance tab
        BgColorBox.Text = _s.OverlayBackgroundColor;
        AccentColorBox.Text = _s.ParticleAccentColor;
        WaveColorBox.Text = _s.ParticleWaveColor;
        BlockColorBox.Text = _s.BlockScreenColor;
        BlockOpacitySlider.Value = _s.BlockScreenOpacity * 100;
        //ArcToggle.IsChecked = _s.ShowProgressArc;
        ShowCountdownToggle.IsChecked = _s.ShowCountdown;
        ShowExerciseToggle.IsChecked = _s.ShowExercise;
        TopMostToggle.IsChecked = _s.AlwaysOnTop;
        HighQualityAnimToggle.IsChecked = _s.UseHighQualityAnimation;

        // Team tab
        // TelemetryToggle.IsChecked = _s.TeamTelemetryEnabled;
        // ApiEndpointBox.Text = _s.TeamApiEndpoint;
        // DisplayNameBox.Text = _s.UserDisplayName;
        // TeamNameBox.Text = _s.TeamName;
        // AnonToggle.IsChecked = _s.AnonymizeTelemetry;
        // ManagerToggle.IsChecked = _s.ManagerDashboardEnabled;
        // SlackToggle.IsChecked = _s.PushToSlack;

        // Update value labels
        UpdateLabels();
    }

    private void UpdateLabels()
    {
        SessionVal.Text = $"{(int)SessionSlider.Value} min";
        BreakVal.Text = $"{(int)BreakSlider.Value} min";
        LongBreakVal.Text = $"{(int)LongBreakSlider.Value} sessions";
        LongBreakDurVal.Text = $"{(int)LongBreakDurSlider.Value} min";
        PreWarnVal.Text = $"{(int)PreWarnSlider.Value} min";
    }

    // ── Slider handlers ───────────────────────────────────────────────────

    private void SessionSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
    {
        SessionVal.Text = $"{(int)SessionSlider.Value} min";
    }

    private void BreakSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
    {
        BreakVal.Text = $"{(int)BreakSlider.Value} min";
    }

    private void LongBreakSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
    {
        LongBreakVal.Text = $"{(int)LongBreakSlider.Value} sessions";
    }

    private void LongBreakDurSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
    {
        LongBreakDurVal.Text = $"{(int)LongBreakDurSlider.Value} min";
    }

    private void PreWarnSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
    {
        PreWarnVal.Text = $"{(int)PreWarnSlider.Value} min";
    }

    // ── Team ─────────────────────────────────────────────────────────────

    // private async void TestConnection_Click(object sender, RoutedEventArgs e)
    // {
    //     ConnTestResult.Text = "Testing…";
    //     try
    //     {
    //         var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    //         var resp = await http.GetAsync(ApiEndpointBox.Text.TrimEnd('/') + "/health");
    //         ConnTestResult.Text = resp.IsSuccessStatusCode
    //             ? "✓ Connected"
    //             : $"✗ Server returned {(int)resp.StatusCode}";
    //     }
    //     catch (Exception ex)
    //     {
    //         ConnTestResult.Text = $"✗ {ex.Message}";
    //     }
    // }

    // ── Save / Cancel ─────────────────────────────────────────────────────

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        // Timer
        _s.SessionMinutes = (int)SessionSlider.Value;
        _s.BreakMinutes = (int)BreakSlider.Value;
        _s.SessionsBeforeLong = (int)LongBreakSlider.Value;
        _s.LongBreakMinutes = (int)LongBreakDurSlider.Value;
        _s.PreWarningMinutes = (int)PreWarnSlider.Value;
         _s.SkipDuringMeetings = SkipMeetingsToggle.IsChecked == true;
         _s.DetectIdle = IdleToggle.IsChecked == true;
         _s.AutoResume = AutoResumeToggle.IsChecked == true;
         _s.LaunchOnStartup = LaunchOnStartupToggle.IsChecked == true;

        // Alerts
        //_s.PlaySound = SoundToggle.IsChecked == true;
        _s.RequireShortcut = ShortcutToggle.IsChecked == true;
        _s.BlockScreenOnBreak = BlockScreenToggle.IsChecked == true;

        // Exercise list — pad / trim to exactly 10 slots
        _s.Exercises = _exerciseBoxes.Select(b => b.Text.Trim()).ToList();
        while (_s.Exercises.Count < 10) _s.Exercises.Add("");

        // Appearance
        _s.OverlayBackgroundColor = BgColorBox.Text.Trim();
        _s.ParticleAccentColor = AccentColorBox.Text.Trim();
        _s.ParticleWaveColor = WaveColorBox.Text.Trim();
        _s.BlockScreenColor = BlockColorBox.Text.Trim();
        _s.BlockScreenOpacity = BlockOpacitySlider.Value / 100.0;
        //_s.ShowProgressArc = ArcToggle.IsChecked == true;
        _s.ShowCountdown = ShowCountdownToggle.IsChecked == true;
        _s.ShowExercise = ShowExerciseToggle.IsChecked == true;
        _s.AlwaysOnTop = TopMostToggle.IsChecked == true;
        _s.UseHighQualityAnimation = HighQualityAnimToggle.IsChecked == true;

        // Team
        // _s.TeamTelemetryEnabled = TelemetryToggle.IsChecked == true;
        // _s.TeamApiEndpoint = ApiEndpointBox.Text;
        // _s.UserDisplayName = DisplayNameBox.Text;
        // _s.TeamName = TeamNameBox.Text;
        // _s.AnonymizeTelemetry = AnonToggle.IsChecked == true;
        // _s.ManagerDashboardEnabled = ManagerToggle.IsChecked == true;
        // _s.PushToSlack = SlackToggle.IsChecked == true;

        // Apply Windows startup registry entry
        StartupService.SetLaunchOnStartup(_s.LaunchOnStartup);

        // Persist & apply live
        _s.Save();
        _timer.ApplySettings(_s);
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
