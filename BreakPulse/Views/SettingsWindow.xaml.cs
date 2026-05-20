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
    private readonly TeamService  _team;

    // 10 exercise-slot text boxes, built once in BuildExerciseSlots()
    private readonly List<TextBox> _exerciseBoxes = new();

    // Page references for navigation
    private readonly ScrollViewer[] _pages;

    // ── Appearance: gradient theme presets ───────────────────────────────────
    private static readonly (string Name, string Inner, string Outer)[] _themes =
    {
        ("Plasma",   "#6C63FF", "#00E5A0"),
        ("Ocean",    "#0EA5E9", "#06B6D4"),
        ("Sunset",   "#F97316", "#EC4899"),
        ("Midnight", "#1E40AF", "#7C3AED"),
        ("Forest",   "#16A34A", "#0D9488"),
        ("Sakura",   "#EC4899", "#F43F5E"),
        ("Ember",    "#F59E0B", "#EF4444"),
        ("Arctic",   "#60CDFF", "#818CF8"),
    };

    private readonly List<Border> _swatchIndicators = new();
    private int _selectedThemeIdx = 0;

    public SettingsWindow(AppSettings settings, TimerService timer, TeamService team)
    {
        InitializeComponent();
        _s     = settings;
        _timer = timer;
        _team  = team;

        _pages = new[] { TimerPage, AlertsPage, DisplayPage, TeamPage };

        BuildThemeSwatches();
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

        var idx = NavList.SelectedIndex;
        for (var i = 0; i < _pages.Length; i++)
            _pages[i].Visibility = i == idx ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Theme swatches ────────────────────────────────────────────────────────

    private void BuildThemeSwatches()
    {
        ThemeSwatchPanel.Children.Clear();
        _swatchIndicators.Clear();

        for (var i = 0; i < _themes.Length; i++)
        {
            var (name, inner, outer) = _themes[i];
            var idx = i;

            var innerColor = (Color)ColorConverter.ConvertFromString(inner);
            var outerColor = (Color)ColorConverter.ConvertFromString(outer);

            var gradient = new RadialGradientBrush();
            gradient.GradientStops.Add(new GradientStop(innerColor, 0.2));
            gradient.GradientStops.Add(new GradientStop(outerColor, 1.0));

            // Selection ring overlay
            var ring = new Border
            {
                BorderBrush     = new SolidColorBrush(Color.FromRgb(0x60, 0xCD, 0xFF)),
                BorderThickness = new Thickness(2.5),
                CornerRadius    = new CornerRadius(9),
                Visibility      = Visibility.Collapsed,
            };
            _swatchIndicators.Add(ring);

            // Name label with shadow for legibility on any gradient
            var label = new TextBlock
            {
                Text                = name,
                FontFamily          = new FontFamily("Segoe UI Variable, Segoe UI"),
                FontSize            = 11,
                FontWeight          = FontWeights.SemiBold,
                Foreground          = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Bottom,
                Margin              = new Thickness(0, 0, 0, 7),
                Effect              = new DropShadowEffect { BlurRadius = 6, ShadowDepth = 0, Opacity = 0.9 },
            };

            var inner2 = new Grid();
            inner2.Children.Add(ring);
            inner2.Children.Add(label);

            var swatch = new Border
            {
                Width        = 104,
                Height       = 68,
                CornerRadius = new CornerRadius(8),
                Margin       = new Thickness(0, 0, 8, 8),
                Background   = gradient,
                Cursor       = Cursors.Hand,
                Child        = inner2,
            };
            swatch.MouseLeftButtonDown += (_, _) => SelectTheme(idx);
            ThemeSwatchPanel.Children.Add(swatch);
        }
    }

    private void SelectTheme(int idx)
    {
        _selectedThemeIdx = idx;
        for (var i = 0; i < _swatchIndicators.Count; i++)
            _swatchIndicators[i].Visibility = i == idx ? Visibility.Visible : Visibility.Collapsed;
    }

    private int FindThemeIndex(string innerHex)
    {
        for (var i = 0; i < _themes.Length; i++)
            if (string.Equals(_themes[i].Inner, innerHex, StringComparison.OrdinalIgnoreCase))
                return i;
        return 0;
    }

    // ── Exercise list UI ──────────────────────────────────────────────────────

    private void BuildExerciseSlots()
    {
        for (var i = 0; i < 10; i++)
        {
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 6), LastChildFill = true };

            var label = new TextBlock
            {
                Text              = $"{i + 1}.",
                Foreground        = new SolidColorBrush(Color.FromArgb(0x88, 0xFF, 0xFF, 0xFF)),
                FontFamily        = new FontFamily("Segoe UI Variable, Segoe UI"),
                FontSize          = 13,
                Width             = 26,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(label, Dock.Left);

            var box = new TextBox
            {
                Background      = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30)),
                Foreground      = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)),
                CaretBrush      = new SolidColorBrush(Color.FromRgb(0x60, 0xCD, 0xFF)),
                BorderBrush     = new SolidColorBrush(Color.FromArgb(0x25, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                Padding         = new Thickness(10, 7, 10, 7),
                FontFamily      = new FontFamily("Segoe UI Variable, Segoe UI"),
                FontSize        = 13,
                TextWrapping    = TextWrapping.Wrap,
                AcceptsReturn   = false
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
        SoundToggle.IsChecked        = _s.PlaySound;
        ShortcutToggle.IsChecked     = _s.RequireShortcut;
        BlockScreenToggle.IsChecked  = _s.BlockScreenOnBreak;

        // Exercise slots
        var exercises = _s.Exercises;
        for (var i = 0; i < _exerciseBoxes.Count; i++)
            _exerciseBoxes[i].Text = i < exercises.Count ? exercises[i] : "";

        // Appearance tab
        var themeIdx = FindThemeIndex(_s.GradientInnerColor);
        SelectTheme(themeIdx);
        ArcToggle.IsChecked          = _s.ShowProgressArc;
        ArcThicknessSlider.Value     = _s.ArcThickness;
        ShowCountdownToggle.IsChecked = _s.ShowCountdown;
        ShowExerciseToggle.IsChecked  = _s.ShowExercise;
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
        SessionVal.Text          = $"{(int)SessionSlider.Value} min";
        BreakVal.Text            = $"{(int)BreakSlider.Value} min";
        LongBreakVal.Text        = $"{(int)LongBreakSlider.Value} sessions";
        LongBreakDurVal.Text     = $"{(int)LongBreakDurSlider.Value} min";
        PreWarnVal.Text          = $"{(int)PreWarnSlider.Value} min";
        ArcThicknessVal.Text     = $"{(int)ArcThicknessSlider.Value} px";
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

    private void ArcThicknessSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
        => ArcThicknessVal.Text = $"{(int)ArcThicknessSlider.Value} px";

    // Old slider handlers no longer used (SizeSlider / OpacitySlider removed):

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
        _s.NotificationMode   = (AlertMode)AlertModeCombo.SelectedIndex;
        _s.PlaySound          = SoundToggle.IsChecked == true;
        _s.RequireShortcut    = ShortcutToggle.IsChecked == true;
        _s.BlockScreenOnBreak = BlockScreenToggle.IsChecked == true;

        // Exercise list — pad / trim to exactly 10 slots
        _s.Exercises = _exerciseBoxes.Select(b => b.Text.Trim()).ToList();
        while (_s.Exercises.Count < 10) _s.Exercises.Add("");

        // Appearance
        if (_selectedThemeIdx >= 0 && _selectedThemeIdx < _themes.Length)
        {
            _s.GradientInnerColor = _themes[_selectedThemeIdx].Inner;
            _s.GradientOuterColor = _themes[_selectedThemeIdx].Outer;
        }
        _s.ShowProgressArc  = ArcToggle.IsChecked == true;
        _s.ArcThickness     = ArcThicknessSlider.Value;
        _s.ShowCountdown    = ShowCountdownToggle.IsChecked == true;
        _s.ShowExercise     = ShowExerciseToggle.IsChecked == true;
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
