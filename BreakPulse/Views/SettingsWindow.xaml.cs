using System.Net.Http;
using System.Windows;
using BreakPulse.Models;
using BreakPulse.Services;

namespace BreakPulse.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _s;
    private readonly TimerService _timer;
    private readonly TeamService  _team;

    public SettingsWindow(AppSettings settings, TimerService timer, TeamService team)
    {
        InitializeComponent();
        _s     = settings;
        _timer = timer;
        _team  = team;
        LoadFromSettings();
    }

    // ── Load ──────────────────────────────────────────────────────────────

    private void LoadFromSettings()
    {
        // Timer tab
        SessionSlider.Value     = _s.SessionMinutes;
        BreakSlider.Value       = _s.BreakMinutes;
        LongBreakSlider.Value   = _s.SessionsBeforeLong;
        LongBreakDurSlider.Value= _s.LongBreakMinutes;
        PreWarnSlider.Value     = _s.PreWarningMinutes;

        SkipMeetingsToggle.IsChecked = _s.SkipDuringMeetings;
        IdleToggle.IsChecked         = _s.DetectIdle;
        AdaptiveToggle.IsChecked     = _s.AdaptiveBreaks;
        AutoResumeToggle.IsChecked   = _s.AutoResume;

        // Alerts tab
        AlertModeCombo.SelectedIndex = (int)_s.NotificationMode;
        BreakMessageBox.Text         = _s.BreakMessage;
        SoundToggle.IsChecked        = _s.PlaySound;
        ShortcutToggle.IsChecked     = _s.RequireShortcut;
        BlockScreenToggle.IsChecked  = _s.BlockScreenOnBreak;

        // Display tab
        PositionCombo.SelectedIndex  = (int)_s.Position;
        SizeSlider.Value             = _s.HudSize;
        OpacitySlider.Value          = (int)(_s.IdleOpacity * 100);
        ArcToggle.IsChecked          = _s.ShowProgressArc;
        DotToggle.IsChecked          = _s.CollapseToDot;
        TopMostToggle.IsChecked      = _s.AlwaysOnTop;

        // Team tab
        TelemetryToggle.IsChecked = _s.TeamTelemetryEnabled;
        ApiEndpointBox.Text       = _s.TeamApiEndpoint;
        DisplayNameBox.Text       = _s.UserDisplayName;
        TeamNameBox.Text          = _s.TeamName;
        AnonToggle.IsChecked      = _s.AnonymizeTelemetry;
        ManagerToggle.IsChecked   = _s.ManagerDashboardEnabled;
        SlackToggle.IsChecked     = _s.PushToSlack;

        // Update value labels
        UpdateLabels();
    }

    private void UpdateLabels()
    {
        SessionVal.Text      = $"{(int)SessionSlider.Value} min";
        BreakVal.Text        = $"{(int)BreakSlider.Value} min";
        LongBreakVal.Text    = $"{(int)LongBreakSlider.Value} sessions";
        LongBreakDurVal.Text = $"{(int)LongBreakDurSlider.Value} min";
        PreWarnVal.Text      = $"{(int)PreWarnSlider.Value} min";
        SizeVal.Text         = $"{(int)SizeSlider.Value} px";
        OpacityVal.Text      = $"{(int)OpacitySlider.Value}%";
    }

    // ── Slider handlers ───────────────────────────────────────────────────

    private void SessionSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
        => SessionVal.Text = $"{(int)SessionSlider.Value} min";

    private void BreakSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
        => BreakVal.Text = $"{(int)BreakSlider.Value} min";

    private void LongBreakSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
        => LongBreakVal.Text = $"{(int)LongBreakSlider.Value} sessions";

    private void LongBreakDurSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
        => LongBreakDurVal.Text = $"{(int)LongBreakDurSlider.Value} min";

    private void PreWarnSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
        => PreWarnVal.Text = $"{(int)PreWarnSlider.Value} min";

    private void SizeSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
        => SizeVal.Text = $"{(int)SizeSlider.Value} px";

    private void OpacitySlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
        => OpacityVal.Text = $"{(int)OpacitySlider.Value}%";

    // ── Team ─────────────────────────────────────────────────────────────

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        ConnTestResult.Text = "Testing…";
        try
        {
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var resp = await http.GetAsync(ApiEndpointBox.Text.TrimEnd('/') + "/health");
            ConnTestResult.Text = resp.IsSuccessStatusCode
                ? "✓ Connected"
                : $"✗ Server returned {(int)resp.StatusCode}";
        }
        catch (Exception ex)
        {
            ConnTestResult.Text = $"✗ {ex.Message}";
        }
    }

    // ── Save / Cancel ─────────────────────────────────────────────────────

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        // Timer
        _s.SessionMinutes      = (int)SessionSlider.Value;
        _s.BreakMinutes        = (int)BreakSlider.Value;
        _s.SessionsBeforeLong  = (int)LongBreakSlider.Value;
        _s.LongBreakMinutes    = (int)LongBreakDurSlider.Value;
        _s.PreWarningMinutes   = (int)PreWarnSlider.Value;
        _s.SkipDuringMeetings  = SkipMeetingsToggle.IsChecked == true;
        _s.DetectIdle          = IdleToggle.IsChecked == true;
        _s.AdaptiveBreaks      = AdaptiveToggle.IsChecked == true;
        _s.AutoResume          = AutoResumeToggle.IsChecked == true;

        // Alerts
        _s.NotificationMode  = (AlertMode)AlertModeCombo.SelectedIndex;
        _s.BreakMessage      = BreakMessageBox.Text;
        _s.PlaySound         = SoundToggle.IsChecked == true;
        _s.RequireShortcut   = ShortcutToggle.IsChecked == true;
        _s.BlockScreenOnBreak= BlockScreenToggle.IsChecked == true;

        // Display
        _s.Position         = (HudPosition)PositionCombo.SelectedIndex;
        _s.HudSize          = (int)SizeSlider.Value;
        _s.IdleOpacity      = OpacitySlider.Value / 100.0;
        _s.ShowProgressArc  = ArcToggle.IsChecked == true;
        _s.CollapseToDot    = DotToggle.IsChecked == true;
        _s.AlwaysOnTop      = TopMostToggle.IsChecked == true;

        // Team
        _s.TeamTelemetryEnabled     = TelemetryToggle.IsChecked == true;
        _s.TeamApiEndpoint          = ApiEndpointBox.Text;
        _s.UserDisplayName          = DisplayNameBox.Text;
        _s.TeamName                 = TeamNameBox.Text;
        _s.AnonymizeTelemetry       = AnonToggle.IsChecked == true;
        _s.ManagerDashboardEnabled  = ManagerToggle.IsChecked == true;
        _s.PushToSlack              = SlackToggle.IsChecked == true;

        // Persist & apply live
        _s.Save();
        _timer.ApplySettings(_s);

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
