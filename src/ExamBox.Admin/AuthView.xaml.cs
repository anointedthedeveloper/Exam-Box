using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ExamBox.Models;
using ExamBox.Services;

namespace ExamBox.Admin;

public partial class AuthView : UserControl
{
    private readonly bool _setup;
    private bool _busy;
    private int _slide;
    private readonly DispatcherTimer _slideTimer = new() { Interval = TimeSpan.FromSeconds(4.5) };
    private readonly DispatcherTimer _lockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private DateTime? _lockedUntil;

    public AuthView()
    {
        InitializeComponent();
        _setup = !App.Auth.HasAdmin();
        Heading.Text = _setup ? "Welcome to ExamBox" : "Administrator sign in";
        SubHeading.Text = _setup ? "Create your administrator account." : "Sign in to manage students and exams.";
        ConfirmPanel.Visibility = _setup ? Visibility.Visible : Visibility.Collapsed;
        SubmitText.Text = _setup ? "Create account" : "Sign in";
        StrengthPanel.Visibility = _setup ? Visibility.Visible : Visibility.Collapsed;
        ForgotLink.Visibility = _setup ? Visibility.Collapsed : Visibility.Visible;
        VersionText.Text = "v" + (System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "1.0.0");
        RenderPortal();
        UpdateHints();

        if (!_setup && App.Settings.RememberedUser != null)
        {
            Username.Text = App.Settings.RememberedUser;
            RememberMe.IsChecked = true;
        }

        _slideTimer.Tick += (_, _) => NextSlide();
        _lockTimer.Tick += (_, _) => TickLock();
        Loaded += (_, _) =>
        {
            App.Portal.Changed += RenderPortal;
            _slideTimer.Start();
            (string.IsNullOrEmpty(Username.Text) ? Username : (Control)Password).Focus();
        };
        Unloaded += (_, _) => { _slideTimer.Stop(); _lockTimer.Stop(); App.Portal.Changed -= RenderPortal; };
    }

    // ---------- slideshow ----------
    private FrameworkElement[] SlideItems => new FrameworkElement[] { Slide0, Slide1, Slide2, Slide3 };
    private Border[] Dots => new[] { Dot0, Dot1, Dot2, Dot3 };
    private TranslateTransform[] Shifts => new[] { Shift0, Shift1, Shift2, Shift3 };

    private void NextSlide()
    {
        var from = _slide;
        _slide = (_slide + 1) % 4;
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
        var ms = TimeSpan.FromMilliseconds(550);

        SlideItems[from].BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, ms) { EasingFunction = ease });
        Shifts[from].BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, -60, ms) { EasingFunction = ease });
        SlideItems[_slide].BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, ms) { EasingFunction = ease });
        Shifts[_slide].BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(60, 0, ms) { EasingFunction = ease });

        for (var i = 0; i < 4; i++)
        {
            var on = i == _slide;
            Dots[i].BeginAnimation(WidthProperty, new DoubleAnimation(on ? 28 : 9, TimeSpan.FromMilliseconds(300)));
            Dots[i].Background = on ? Brushes.White : new SolidColorBrush(Color.FromArgb(0x59, 255, 255, 255));
        }
    }

    // ---------- small form helpers ----------
    private void UpdateHints()
    {
        UserHint.Visibility = Username.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        PassHint.Visibility = PasswordText.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        ConfirmHint.Visibility = Confirm.Password.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_setup) { UpdateStrength(); UpdateMatch(); }
    }

    private void Field_Changed(object sender, TextChangedEventArgs e) { if (IsLoaded || sender != null) UpdateHints(); }
    private void Password_Changed(object sender, RoutedEventArgs e) => UpdateHints();
    private void Confirm_Changed(object sender, RoutedEventArgs e) => UpdateHints();

    private void Caps_Check(object sender, RoutedEventArgs e) => CapsHint.Visibility = System.Windows.Input.Keyboard.IsKeyToggled(System.Windows.Input.Key.CapsLock) ? Visibility.Visible : Visibility.Collapsed;
    private void Caps_Key(object sender, System.Windows.Input.KeyEventArgs e) => Caps_Check(sender, e);

    /// <summary>Guidance only: any non-empty password is accepted.</summary>
    private void UpdateStrength()
    {
        var pw = PasswordText;
        var score = pw.Length == 0 ? 0 : (pw.Length >= 8 ? 1 : 0) + (pw.Length >= 12 ? 1 : 0)
            + (pw.Any(char.IsUpper) && pw.Any(char.IsLower) ? 1 : 0) + (pw.Any(char.IsDigit) ? 1 : 0) + (pw.Any(c => !char.IsLetterOrDigit(c)) ? 1 : 0);
        var level = pw.Length == 0 ? 0 : score <= 1 ? 1 : score <= 3 ? 2 : 3;
        var color = level switch { 1 => "ErrBrush", 2 => "WarnBrush", _ => "OkBrush" };
        var segs = new[] { Seg0, Seg1, Seg2 };
        for (var i = 0; i < 3; i++) segs[i].Background = (Brush)FindResource(i < level ? color : "LineBrush");
        StrengthText.Text = level switch
        {
            0 => "Choose any password you'll remember. Longer is safer.",
            1 => "Weak — still allowed, but easy to guess.",
            2 => "Okay — adding length or symbols makes it stronger.",
            _ => "Strong password."
        };
    }

    private void UpdateMatch()
    {
        if (Confirm.Password.Length == 0) { MatchText.Visibility = Visibility.Collapsed; return; }
        var ok = Confirm.Password == PasswordText;
        MatchText.Text = ok ? "✓  Passwords match" : "✕  Passwords do not match yet";
        MatchText.Foreground = (Brush)FindResource(ok ? "OkBrush" : "ErrBrush");
        MatchText.Visibility = Visibility.Visible;
    }

    private void RenderPortal()
    {
        var running = App.Portal.Running;
        PortalDot.Fill = (Brush)FindResource(running ? "OkBrush" : "ErrBrush");
        PortalText.Text = running ? $"Student portal running · {PortalHost.ReachableUrls(App.Portal.Port).First()}" : "Student portal stopped";
    }

    private void Forgot_Click(object sender, RoutedEventArgs e) =>
        Ui.Info("Passwords are stored securely and can't be viewed, but you can set a new administrator password.\n\n" +
                "1. Close ExamBox.\n2. Open Command Prompt in the folder that contains ExamBox.exe.\n3. Run:\n\n      ExamBox.exe --reset-admin YourNewPassword\n\n" +
                "Your students and exams are not affected.");

    // ---------- password visibility ----------
    private string PasswordText => ShowPw.IsChecked == true ? PasswordVisible.Text : Password.Password;

    private void ShowPw_Changed(object sender, RoutedEventArgs e)
    {
        ShowPw.ToolTip = ShowPw.IsChecked == true ? "Hide password" : "Show password";
        if (ShowPw.IsChecked == true)
        {
            PasswordVisible.Text = Password.Password;
            Password.Visibility = Visibility.Collapsed;
            PasswordVisible.Visibility = Visibility.Visible;
            PasswordVisible.Focus();
            PasswordVisible.CaretIndex = PasswordVisible.Text.Length;
        }
        else
        {
            Password.Password = PasswordVisible.Text;
            PasswordVisible.Visibility = Visibility.Collapsed;
            Password.Visibility = Visibility.Visible;
            Password.Focus();
        }
    }

    // ---------- messages ----------
    private void ShowError(string message, bool warning = false)
    {
        ErrorText.Text = (warning ? "⚠  " : "") + message;
        ErrorBox.Background = (Brush)FindResource(warning ? "WarnBgBrush" : "ErrBgBrush");
        ErrorText.Foreground = (Brush)FindResource(warning ? "WarnBrush" : "ErrBrush");
        ErrorBox.Visibility = Visibility.Visible;
    }

    private void SetBusy(bool on)
    {
        _busy = on;
        Busy.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        Submit.IsEnabled = !on && _lockedUntil == null;
        Username.IsEnabled = Password.IsEnabled = PasswordVisible.IsEnabled = Confirm.IsEnabled = !on;
        SubmitText.Text = on ? (_setup ? "Creating account…" : "Signing in…") : (_setup ? "Create account" : "Sign in");
    }

    // ---------- lock countdown ----------
    private void StartLock(DateTime untilUtc)
    {
        _lockedUntil = untilUtc;
        Submit.IsEnabled = false;
        _lockTimer.Start();
        TickLock();
    }

    private void TickLock()
    {
        if (_lockedUntil == null) return;
        var left = _lockedUntil.Value - DateTime.UtcNow;
        if (left <= TimeSpan.Zero)
        {
            _lockedUntil = null;
            _lockTimer.Stop();
            ErrorBox.Visibility = Visibility.Collapsed;
            Submit.IsEnabled = !_busy;
            return;
        }
        ShowError($"Too many failed attempts. Locked — try again in {(int)left.TotalMinutes}:{left.Seconds:00}.");
    }

    private async void Submit_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _lockedUntil != null) return;
        ErrorBox.Visibility = Visibility.Collapsed;
        var username = Username.Text.Trim();
        var password = PasswordText;

        if (_setup && password != Confirm.Password) { ShowError("Passwords do not match."); return; }

        SetBusy(true);
        AuthResult r;
        try
        {
            // Password hashing is deliberately slow; keep the window responsive (and the spinner moving).
            r = await Task.Run(() =>
            {
                if (_setup)
                {
                    var created = App.Auth.CreateAdmin(username, password);
                    if (!created.Ok) return new AuthResult(null, created.Error);
                }
                return App.Auth.Authenticate(username, password, UserRole.Admin);
            });
        }
        catch (Exception ex)
        {
            SetBusy(false);
            ShowError("Something went wrong: " + ex.Message);
            return;
        }
        SetBusy(false);

        if (r.User == null)
        {
            if (r.LockedUntil != null) StartLock(r.LockedUntil.Value);
            else ShowError(r.Error ?? "Sign-in failed.", warning: r.AttemptsLeft is > 0);
            Password.Clear(); PasswordVisible.Clear();
            (_setup ? Username : (Control)Password).Focus();
            return;
        }

        // "Remember me" only remembers the username; the password is never stored.
        App.Settings.RememberedUser = RememberMe.IsChecked == true ? r.User.Username : null;
        App.Settings.Save();
        MainWindow.Current.SignedIn(r.User);
    }
}
