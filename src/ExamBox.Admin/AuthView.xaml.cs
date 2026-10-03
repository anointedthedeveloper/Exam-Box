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
    private bool _autoTried, _busy;
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

        if (!_setup && App.Settings.RememberedUser != null)
        {
            Username.Text = App.Settings.RememberedUser;
            RememberMe.IsChecked = true;
        }

        _slideTimer.Tick += (_, _) => NextSlide();
        _lockTimer.Tick += (_, _) => TickLock();
        Loaded += (_, _) =>
        {
            _slideTimer.Start();
            if (!_setup && !_autoTried && TryAutoSignIn()) return;
            (string.IsNullOrEmpty(Username.Text) ? Username : (Control)Password).Focus();
        };
        Unloaded += (_, _) => { _slideTimer.Stop(); _lockTimer.Stop(); };
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

    // ---------- password visibility ----------
    private string PasswordText => ShowPw.IsChecked == true ? PasswordVisible.Text : Password.Password;

    private void ShowPw_Changed(object sender, RoutedEventArgs e)
    {
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

    // ---------- remember me ----------
    /// <summary>"Remember me": sign in automatically with the DPAPI-protected password from the last launch.</summary>
    private bool TryAutoSignIn()
    {
        _autoTried = true;
        var user = App.Settings.RememberedUser;
        var pw = Secrets.Unprotect(App.Settings.RememberedSecret);
        if (user == null || pw == null) return false;
        var r = App.Auth.Authenticate(user, pw, UserRole.Admin);
        if (r.User == null)
        {
            App.Settings.RememberedSecret = null;   // stored credentials no longer work: fall back to the form
            App.Settings.Save();
            return false;
        }
        MainWindow.Current.SignedIn(r.User);
        return true;
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

        if (RememberMe.IsChecked == true)
        {
            App.Settings.RememberedUser = r.User.Username;
            App.Settings.RememberedSecret = Secrets.Protect(password);
        }
        else
        {
            App.Settings.RememberedUser = null;
            App.Settings.RememberedSecret = null;
        }
        App.Settings.Save();
        MainWindow.Current.SignedIn(r.User);
    }
}
