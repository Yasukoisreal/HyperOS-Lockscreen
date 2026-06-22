using System;
using System.IO.IsolatedStorage;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Phone.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Media;
using Windows.Phone.System;
using Microsoft.Phone.Info;
using System.Net;
using System.Device.Location;

namespace HyperOS.Pages
{
    public partial class LockScreen : PhoneApplicationPage
    {
        // Timers
        private DispatcherTimer timer;
        private DispatcherTimer batteryTimer;
        private DispatcherTimer phyTimer;

        // Settings flags
        private bool bIsPasswordEnabled;
        private bool bIsPatternOn;
        private bool bIsAnimOn = true;
        private int clockStyle = 0;
        private int clockPosition = 1; // 0=Top, 1=Center, 2=Bottom
        private int clockHAlign = 1;    // 0=Left, 1=Center, 2=Right
        private int clockColor = 0;     // 0=White, 1=Gold, 2=SkyBlue, 3=Pink, 4=Red
        private int clockBlend = 0;     // 0=None, 1=Sunset, 2=Ocean, 3=Aurora, 4=Neon
        private int clockSize = 2;      // 0=S..4=XXL (default 2=L)

        // PIN
        private string passwordText = "";
        private string UserPassword = "";
        private int passwordTries = 5;

        // Pattern
        private int patternTries = 5;

        // Swipe threshold
        private double yToUnlock = 100;

        // Cached resources (CPU optimization)
        private static readonly SolidColorBrush FilledBrush = new SolidColorBrush(Colors.White);
        private static readonly SolidColorBrush EmptyBrush = new SolidColorBrush(Colors.Transparent);
        private static readonly SolidColorBrush ChargingBrush = new SolidColorBrush(
            System.Windows.Media.Color.FromArgb(0xAA, 0xFF, 0xCC, 0x00));
        private static readonly SolidColorBrush NormalBatteryBrush = new SolidColorBrush(
            System.Windows.Media.Color.FromArgb(0xAA, 0xFF, 0xFF, 0xFF));
        private Windows.Phone.Devices.Power.Battery cachedBattery;
        private string lastTimeText = "";
        private bool isFirstLoad = true;
        private bool backgroundLoaded = false;

        // Weather
        private DispatcherTimer weatherTimer;
        private bool showWeather = false;
        private double weatherLat = 0;
        private double weatherLon = 0;

        // Countdown
        private bool showCountdown = false;
        private DateTime countdownTarget;
        private string countdownName = "";

        // Depth effect
        private bool useDepthEffect = false;

        public LockScreen()
        {
            InitializeComponent();
        }

        private void PhoneApplicationPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (!isFirstLoad) return; // Guard: Loaded can fire multiple times

            LoadSettings();
            LoadBackground();
            ApplyClockStyle();
            ApplyClockPosition();
            ApplyClockHAlign();
            ApplyClockColor();
            ApplyClockSize();
            UpdateTime();

            // Cache battery reference once
            try { cachedBattery = Windows.Phone.Devices.Power.Battery.GetDefault(); } catch { }

            // Main clock timer (one-time setup)
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (s, a) => UpdateTime();
            timer.Start();

            // Battery timer
            batteryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            batteryTimer.Tick += batteryTimer_Tick;
            batteryTimer.Start();
            UpdateBattery();

            // XNA FrameworkDispatcher timer (required for MediaPlayer)
            phyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            phyTimer.Tick += (s, a) => { try { FrameworkDispatcher.Update(); } catch { } };
            phyTimer.Start();

            // Setup music events (one-time, unsubscribe first to prevent leaks)
            try
            {
                MediaPlayer.ActiveSongChanged -= MediaPlayer_ActiveSongChanged;
                MediaPlayer.MediaStateChanged -= MediaPlayer_MediaStateChanged;
                MediaPlayer.ActiveSongChanged += MediaPlayer_ActiveSongChanged;
                MediaPlayer.MediaStateChanged += MediaPlayer_MediaStateChanged;
                UpdateMusicInfo();
            }
            catch { }

            // Play animations on first load
            PlayEntryAnimations();

            // Weather timer (every 30 min)
            weatherTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
            weatherTimer.Tick += (s, a) => FetchWeather();
            weatherTimer.Start();
            if (showWeather)
            {
                LoadCachedWeather(); // Show cached data immediately
                FetchWeather();      // Then refresh from API
            }

            // Load extras
            LoadForeground();
            UpdateCountdown();

            isFirstLoad = false;
        }

        protected override void OnNavigatedTo(System.Windows.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (!isFirstLoad)
            {
                // Reload settings (user may have changed clock style etc.)
                LoadSettings();
                backgroundLoaded = false;
                LoadBackground();
                ApplyClockStyle();
                ApplyClockPosition();
                ApplyClockHAlign();
                ApplyClockColor();
                ApplyClockSize();
                lastTimeText = ""; // Force time refresh
                UpdateTime();
                UpdateBattery();
                UpdateMusicInfo();
                UpdateCountdown();
                if (showWeather) FetchWeather();
                LoadForeground();

                // Reset swipe overlay
                var t = (CompositeTransform)OverlayInformationPanel.RenderTransform;
                t.TranslateY = 0;
                OverlayInformationPanel.Opacity = 1;

                // Hide security grids
                PassGrid.Visibility = Visibility.Collapsed;
                PatternGrid.Visibility = Visibility.Collapsed;
                RecoverGrid.Visibility = Visibility.Collapsed;

                // Replay animations
                PlayEntryAnimations();
            }
        }

        private void PlayEntryAnimations()
        {
            if (bIsAnimOn)
            {
                try
                {
                    ((Storyboard)Resources["TimeAnim"]).Begin();
                    ((Storyboard)Resources["DayAnim"]).Begin();
                }
                catch { }
            }
        }

        #region Time & Date

        private void UpdateTime()
        {
            string newTime = DateTime.Now.ToString("HH:mm");
            if (newTime != lastTimeText)
            {
                lastTimeText = newTime;
                HourText.Text = newTime;
                string newDay = DateTime.Now.ToString("dddd");
                string newDate = DateTime.Now.ToString("MMMM d");
                if (DayPanel.Text != newDay)
                {
                    DayPanel.Text = newDay;
                    DatePanel.Text = newDate;
                    UpdateCountdown(); // Refresh once per day
                }
            }
        }

        #endregion

        #region Swipe to Unlock

        private void OverlayInformationPanel_ManipulationStarted(
            object sender, ManipulationStartedEventArgs e)
        {
            // Record start point
        }

        private void OverlayInformationPanel_ManipulationDelta(
            object sender, ManipulationDeltaEventArgs e)
        {
            var t = (CompositeTransform)OverlayInformationPanel.RenderTransform;
            double newY = t.TranslateY + e.DeltaManipulation.Translation.Y;
            if (newY <= 0)
            {
                t.TranslateY = newY;
                // Fade opacity based on swipe distance
                double opacity = Math.Max(0, 1 + newY / 500);
                OverlayInformationPanel.Opacity = opacity;
            }
        }

        private void OverlayInformationPanel_ManipulationCompleted(
            object sender, ManipulationCompletedEventArgs e)
        {
            var t = (CompositeTransform)OverlayInformationPanel.RenderTransform;
            if (Math.Abs(t.TranslateY) > yToUnlock)
            {
                if (!bIsPasswordEnabled && !bIsPatternOn)
                {
                    // No security — unlock directly
                    RequestScreenUnlock();
                }
                else
                {
                    // Show PIN/Pattern
                    VisualStateManager.GoToState(this, "PassEnter", true);
                    ShowUnlockMethod();
                }
            }
            else
            {
                // Snap back
                t.TranslateY = 0;
                OverlayInformationPanel.Opacity = 1;
            }
        }

        #endregion

        #region Unlock Methods

        private void UnlockButton_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            ShowUnlockMethod();
        }

        private void ShowUnlockMethod()
        {
            if (bIsPasswordEnabled)
            {
                PassGrid.Visibility = Visibility.Visible;
                passwordText = "";
                UpdatePassCodeInd();
                Inc_Pass.Visibility = Visibility.Collapsed;
                try { ((Storyboard)Resources["PassAnim"]).Begin(); } catch { }
            }
            else if (bIsPatternOn)
            {
                PatternGrid.Visibility = Visibility.Visible;
                PatternErrorText.Visibility = Visibility.Collapsed;
                try { ((Storyboard)Resources["PatternGridAnim"]).Begin(); } catch { }
            }
            else
            {
                // No security — unlock directly
                RequestScreenUnlock();
            }
        }

        private void RequestScreenUnlock()
        {
            // Play unlock animation first if animations enabled
            if (bIsAnimOn)
            {
                try { UnlockAnim.Begin(); return; } catch { }
            }
            DoActualUnlock();
        }

        private void UnlockAnim_Completed(object sender, EventArgs e)
        {
            DoActualUnlock();
        }

        private void DoActualUnlock()
        {
            try
            {
                if (SystemProtection.ScreenLocked)
                {
                    SystemProtection.RequestScreenUnlock();
                }
            }
            catch { }
        }

        #endregion

        #region Quick Shortcuts

        private void FlashlightShortcut_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            // Unlock screen — WP8.1 doesn't allow direct flashlight access from lock
            RequestScreenUnlock();
        }

        private void CameraShortcut_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            // Unlock screen — WP8.1 doesn't allow direct camera launch from lock
            RequestScreenUnlock();
        }

        #endregion

        #region PIN Pad

        private void AddDigit(string digit)
        {
            if (passwordText.Length < 4)
            {
                passwordText += digit;
                UpdatePassCodeInd();

                // Auto-submit when 4 digits entered
                if (passwordText.Length == 4)
                {
                    CheckPassword();
                }
            }
        }

        private void _0_Click(object sender, RoutedEventArgs e) { AddDigit("0"); }
        private void _1_Click(object sender, RoutedEventArgs e) { AddDigit("1"); }
        private void _2_Click(object sender, RoutedEventArgs e) { AddDigit("2"); }
        private void _3_Click(object sender, RoutedEventArgs e) { AddDigit("3"); }
        private void _4_Click(object sender, RoutedEventArgs e) { AddDigit("4"); }
        private void _5_Click(object sender, RoutedEventArgs e) { AddDigit("5"); }
        private void _6_Click(object sender, RoutedEventArgs e) { AddDigit("6"); }
        private void _7_Click(object sender, RoutedEventArgs e) { AddDigit("7"); }
        private void _8_Click(object sender, RoutedEventArgs e) { AddDigit("8"); }
        private void _9_Click(object sender, RoutedEventArgs e) { AddDigit("9"); }

        private void OK_Button_Click(object sender, RoutedEventArgs e)
        {
            // Backspace — delete last digit
            if (passwordText.Length > 0)
            {
                passwordText = passwordText.Substring(0, passwordText.Length - 1);
                UpdatePassCodeInd();
            }
        }

        private void PassBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            // Hidden textbox mirror for PIN
        }

        private void CheckPassword()
        {
            if (passwordText == UserPassword)
            {
                // Correct PIN
                try { ((Storyboard)Resources["PassAnimR"]).Begin(); } catch { }
                RequestScreenUnlock();
            }
            else
            {
                // Wrong PIN
                passwordTries--;
                passwordText = "";
                UpdatePassCodeInd();
                Inc_Pass.Visibility = Visibility.Visible;

                if (passwordTries <= 0)
                {
                    PassGrid.Visibility = Visibility.Collapsed;
                    ShowRecovery();
                }
            }
        }

        private void UpdatePassCodeInd()
        {
            Char1.Background = passwordText.Length >= 1 ? FilledBrush : EmptyBrush;
            Char2.Background = passwordText.Length >= 2 ? FilledBrush : EmptyBrush;
            Char3.Background = passwordText.Length >= 3 ? FilledBrush : EmptyBrush;
            Char4.Background = passwordText.Length >= 4 ? FilledBrush : EmptyBrush;
        }

        #endregion

        #region Pattern Lock

        private void PatternLockMetroControl_PatternMatchSuccess(object sender, EventArgs e)
        {
            try { ((Storyboard)Resources["PatternGridAnimR"]).Begin(); } catch { }
            RequestScreenUnlock();
        }

        private void pattLoc_PatternMatchUnsuccess(object sender, EventArgs e)
        {
            patternTries--;
            PatternErrorText.Visibility = Visibility.Visible;

            if (patternTries <= 0)
            {
                CaptionTextNoTries.Visibility = Visibility.Visible;
                PatternGrid.Visibility = Visibility.Collapsed;
                ShowRecovery();
            }
        }

        #endregion

        #region Recovery

        private void ShowRecovery()
        {
            RecoverGrid.Visibility = Visibility.Visible;
            try { ((Storyboard)Resources["RecoverGridAnim"]).Begin(); } catch { }
        }

        private void RecoverButton_Click(object sender, RoutedEventArgs e)
        {
            // Reset password/pattern and unlock
            var settings = IsolatedStorageSettings.ApplicationSettings;
            settings["bIsPasswordEnabled"] = false;
            settings["bIsPatternOn"] = false;
            settings.Save();

            bIsPasswordEnabled = false;
            bIsPatternOn = false;
            passwordTries = 5;
            patternTries = 5;

            try { ((Storyboard)Resources["RecoverGridAnimR"]).Begin(); } catch { }
            RecoverGrid.Visibility = Visibility.Collapsed;
            RequestScreenUnlock();
        }

        #endregion

        #region Music

        private void PlayPrev(object sender, RoutedEventArgs e)
        {
            try
            {
                FrameworkDispatcher.Update();
                MediaPlayer.MovePrevious();
            }
            catch { }
        }

        private void PlayPause(object sender, RoutedEventArgs e)
        {
            try
            {
                FrameworkDispatcher.Update();
                if (MediaPlayer.State == MediaState.Playing)
                    MediaPlayer.Pause();
                else
                    MediaPlayer.Resume();
            }
            catch { }
        }

        private void PlayNext(object sender, RoutedEventArgs e)
        {
            try
            {
                FrameworkDispatcher.Update();
                MediaPlayer.MoveNext();
            }
            catch { }
        }

        private void MediaPlayer_ActiveSongChanged(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                UpdateMusicInfo();
                try
                {
                    ((Storyboard)Resources["SongAnim"]).Begin();
                    ((Storyboard)Resources["ArtistAnim"]).Begin();
                }
                catch { }
            });
        }

        private void MediaPlayer_MediaStateChanged(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                PlayPauseIcon.Text = MediaPlayer.State == MediaState.Playing ? "⏸" : "▶";
            });
        }

        private void UpdateMusicInfo()
        {
            try
            {
                FrameworkDispatcher.Update();
                if (MediaPlayer.Queue.ActiveSong != null)
                {
                    SongName.Text = MediaPlayer.Queue.ActiveSong.Name ?? "Unknown";
                    Artist.Text = MediaPlayer.Queue.ActiveSong.Artist.Name ?? "";
                    PlayPauseIcon.Text = MediaPlayer.State == MediaState.Playing ? "⏸" : "▶";
                    PlayPanel.Visibility = Visibility.Visible;
                    MusicControlPanel.Visibility = Visibility.Visible;
                }
            }
            catch { }
        }

        private void PlaySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Seek functionality - optional
        }

        #endregion

        #region Battery

        private void batteryTimer_Tick(object sender, EventArgs e)
        {
            UpdateBattery();
        }

        private void UpdateBattery()
        {
            try
            {
                if (cachedBattery == null)
                    cachedBattery = Windows.Phone.Devices.Power.Battery.GetDefault();
                int level = cachedBattery.RemainingChargePercent;
                BatteryText.Text = level + "%";
                BatteryText.Visibility = Visibility.Visible;

                if (level <= 15)
                {
                    BatteryLowIcon.Visibility = Visibility.Visible;
                    FlashBattery.Begin();
                }
                else
                {
                    BatteryLowIcon.Visibility = Visibility.Collapsed;
                    FlashBattery.Stop();
                }

                // Charging detection
                try
                {
                    bool isCharging = DeviceStatus.PowerSource == PowerSource.External;
                    if (isCharging)
                    {
                        ChargingIcon.Visibility = Visibility.Visible;
                        ChargingPulse.Begin();
                        BatteryText.Foreground = ChargingBrush;
                    }
                    else
                    {
                        ChargingIcon.Visibility = Visibility.Collapsed;
                        ChargingPulse.Stop();
                        BatteryText.Foreground = NormalBatteryBrush;
                    }
                }
                catch { }
            }
            catch { }
        }

        #endregion

        #region Settings

        private void LoadSettings()
        {
            var s = IsolatedStorageSettings.ApplicationSettings;

            if (s.Contains("bIsPasswordEnabled"))
                bIsPasswordEnabled = (bool)s["bIsPasswordEnabled"];
            if (s.Contains("bIsPatternOn"))
                bIsPatternOn = (bool)s["bIsPatternOn"];
            if (s.Contains("bIsAnimOn"))
                bIsAnimOn = (bool)s["bIsAnimOn"];
            if (s.Contains("UserPassword"))
                UserPassword = (string)s["UserPassword"];
            if (s.Contains("ClockStyle"))
                clockStyle = (int)s["ClockStyle"];
            if (s.Contains("ClockPosition"))
                clockPosition = (int)s["ClockPosition"];
            if (s.Contains("ClockHAlign"))
                clockHAlign = (int)s["ClockHAlign"];
            if (s.Contains("ClockColor"))
                clockColor = (int)s["ClockColor"];
            if (s.Contains("ClockBlend"))
                clockBlend = (int)s["ClockBlend"];
            if (s.Contains("ClockSize"))
                clockSize = (int)s["ClockSize"];

            // Owner info
            if (s.Contains("OwnerInfo"))
            {
                string info = (string)s["OwnerInfo"];
                if (!string.IsNullOrEmpty(info))
                {
                    OwnerInfoText.Text = info;
                    OwnerInfoText.Visibility = Visibility.Visible;
                }
                else
                {
                    OwnerInfoText.Visibility = Visibility.Collapsed;
                }
            }

            // Weather
            if (s.Contains("ShowWeather"))
                showWeather = (bool)s["ShowWeather"];
            if (s.Contains("WeatherLat"))
                weatherLat = (double)s["WeatherLat"];
            if (s.Contains("WeatherLon"))
                weatherLon = (double)s["WeatherLon"];

            // Countdown
            if (s.Contains("ShowCountdown"))
                showCountdown = (bool)s["ShowCountdown"];
            if (s.Contains("CountdownTarget"))
                countdownTarget = (DateTime)s["CountdownTarget"];
            if (s.Contains("CountdownName"))
                countdownName = (string)s["CountdownName"];


            // Depth effect
            if (s.Contains("UseDepthEffect"))
                useDepthEffect = (bool)s["UseDepthEffect"];
        }

        private void ApplyClockStyle()
        {
            switch (clockStyle)
            {
                case 0: HourText.FontFamily = new FontFamily("/Assets/Fonts/MiSans-Regular.ttf#MiSans"); break;
                case 1: HourText.FontFamily = new FontFamily("/Assets/Fonts/MiSans-Demibold.ttf#MiSans"); break;
                case 2: HourText.FontFamily = new FontFamily("/Assets/Fonts/BebasNeue-Regular.ttf#Bebas Neue"); break;
                case 3: HourText.FontFamily = new FontFamily("Segoe WP"); break;
                case 4: HourText.FontFamily = new FontFamily("Segoe WP Black"); break;
                default: HourText.FontFamily = new FontFamily("/Assets/Fonts/MiSans-Demibold.ttf#MiSans"); break;
            }
        }

        private void ApplyClockPosition()
        {
            switch (clockPosition)
            {
                case 0: // Top
                    ClockPanel.VerticalAlignment = VerticalAlignment.Top;
                    ClockPanel.Margin = new Thickness(0, 80, 0, 0);
                    break;
                case 2: // Bottom
                    ClockPanel.VerticalAlignment = VerticalAlignment.Bottom;
                    ClockPanel.Margin = new Thickness(0, 0, 0, 140);
                    break;
                default: // Center
                    ClockPanel.VerticalAlignment = VerticalAlignment.Center;
                    ClockPanel.Margin = new Thickness(0, 0, 0, 40);
                    break;
            }
        }

        private void ApplyClockHAlign()
        {
            switch (clockHAlign)
            {
                case 0: // Left
                    ClockPanel.HorizontalAlignment = HorizontalAlignment.Left;
                    ClockPanel.Margin = new Thickness(24, ClockPanel.Margin.Top, 0, ClockPanel.Margin.Bottom);
                    DateInfoPanel.HorizontalAlignment = HorizontalAlignment.Left;
                    HourText.HorizontalAlignment = HorizontalAlignment.Left;
                    break;
                case 2: // Right
                    ClockPanel.HorizontalAlignment = HorizontalAlignment.Right;
                    ClockPanel.Margin = new Thickness(0, ClockPanel.Margin.Top, 24, ClockPanel.Margin.Bottom);
                    DateInfoPanel.HorizontalAlignment = HorizontalAlignment.Right;
                    HourText.HorizontalAlignment = HorizontalAlignment.Right;
                    break;
                default: // Center
                    ClockPanel.HorizontalAlignment = HorizontalAlignment.Center;
                    DateInfoPanel.HorizontalAlignment = HorizontalAlignment.Center;
                    HourText.HorizontalAlignment = HorizontalAlignment.Center;
                    break;
            }
        }

        // Helper to avoid XNA vs Media.Color ambiguity
        private static System.Windows.Media.Color C(byte r, byte g, byte b)
        {
            return System.Windows.Media.Color.FromArgb(255, r, g, b);
        }

        private void ApplyClockColor()
        {
            Brush brush;
            if (clockBlend > 0)
            {
                switch (clockBlend)
                {
                    case 1: brush = MakeGradient(C(255, 140, 50),  C(255, 80, 150));  break; // Sunset
                    case 2: brush = MakeGradient(C(0, 210, 255),   C(58, 80, 200));   break; // Ocean
                    case 3: brush = MakeGradient(C(80, 255, 120),  C(180, 80, 255));  break; // Aurora
                    case 4: brush = MakeGradient(C(255, 0, 200),   C(0, 255, 255));   break; // Neon
                    default: brush = new SolidColorBrush(Colors.White); break;
                }
            }
            else
            {
                switch (clockColor)
                {
                    case 1: brush = new SolidColorBrush(C(255, 215, 0)); break;   // Gold
                    case 2: brush = new SolidColorBrush(C(135, 206, 250)); break; // Sky Blue
                    case 3: brush = new SolidColorBrush(C(255, 182, 193)); break; // Pink
                    case 4: brush = new SolidColorBrush(C(255, 99, 99)); break;   // Red
                    default: brush = new SolidColorBrush(Colors.White); break;
                }
            }
            HourText.Foreground = brush;
        }

        private LinearGradientBrush MakeGradient(System.Windows.Media.Color from, System.Windows.Media.Color to)
        {
            var lg = new LinearGradientBrush();
            lg.StartPoint = new System.Windows.Point(0, 0);
            lg.EndPoint = new System.Windows.Point(1, 1);
            lg.GradientStops.Add(new GradientStop { Color = from, Offset = 0 });
            lg.GradientStops.Add(new GradientStop { Color = to, Offset = 1 });
            return lg;
        }

        private static readonly int[] SizeValues = { 80, 95, 105, 120, 140 };

        private void ApplyClockSize()
        {
            int idx = clockSize;
            if (idx < 0 || idx >= SizeValues.Length) idx = 2;
            int sz = SizeValues[idx];
            HourText.FontSize = sz;
            // Pull clock closer to date — less pull at larger sizes to avoid overlap
            double pull = -sz * 0.16;  // ~-13 at S(80), ~-17 at L(105), ~-22 at XXL(140)
            HourText.Margin = new Thickness(0, pull, 0, 0);
        }

        private void LoadBackground()
        {
            if (backgroundLoaded) return;
            try
            {
                using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    if (store.FileExists("Background.jpg"))
                    {
                        using (var stream = store.OpenFile("Background.jpg",
                            System.IO.FileMode.Open, System.IO.FileAccess.Read))
                        {
                            var bitmap = new BitmapImage();
                            bitmap.DecodePixelWidth = 480;
                            bitmap.SetSource(stream);
                            BackgroundBrush.ImageSource = bitmap;
                            backgroundLoaded = true;
                        }
                    }
                }
            }
            catch { }
        }

        #endregion

        #region Weather

        private void FetchWeather()
        {
            if (!showWeather || (weatherLat == 0 && weatherLon == 0))
            {
                WeatherText.Visibility = Visibility.Collapsed;
                return;
            }

            // Show cached weather immediately while fetching new data
            LoadCachedWeather();

            try
            {
                // Use HTTP (not HTTPS) — WP8.1 has TLS compatibility issues
                string url = string.Format(
                    "http://api.open-meteo.com/v1/forecast?latitude={0}&longitude={1}&current_weather=true",
                    weatherLat.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    weatherLon.ToString(System.Globalization.CultureInfo.InvariantCulture));

                var request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
                request.Method = "GET";

                request.BeginGetResponse(ar =>
                {
                    try
                    {
                        var response = request.EndGetResponse(ar);
                        using (var stream = response.GetResponseStream())
                        using (var reader = new System.IO.StreamReader(stream))
                        {
                            string json = reader.ReadToEnd();
                            // Extract "current_weather":{...} sub-object to avoid
                            // matching "temperature":"°C" in current_weather_units
                            string cw = json;
                            int cwIdx = json.IndexOf("\"current_weather\":{");
                            if (cwIdx >= 0)
                            {
                                int braceStart = json.IndexOf('{', cwIdx + 18);
                                int braceEnd = json.IndexOf('}', braceStart);
                                if (braceStart >= 0 && braceEnd > braceStart)
                                    cw = json.Substring(braceStart, braceEnd - braceStart + 1);
                            }
                            double temp = ParseJsonDouble(cw, "temperature");
                            int code = (int)ParseJsonDouble(cw, "weathercode");
                            string icon = GetWeatherIcon(code);

                            Dispatcher.BeginInvoke(() =>
                            {
                                WeatherText.Text = icon + " " + temp.ToString("0") + "°C";
                                WeatherText.Visibility = Visibility.Visible;

                                // Cache to storage
                                var s = IsolatedStorageSettings.ApplicationSettings;
                                s["CachedWeather"] = WeatherText.Text;
                                s.Save();
                            });
                        }
                    }
                    catch
                    {
                        Dispatcher.BeginInvoke(() => LoadCachedWeather());
                    }
                }, null);
            }
            catch
            {
                LoadCachedWeather();
            }
        }

        private void LoadCachedWeather()
        {
            try
            {
                var s = IsolatedStorageSettings.ApplicationSettings;
                if (s.Contains("CachedWeather"))
                {
                    WeatherText.Text = (string)s["CachedWeather"];
                    WeatherText.Visibility = Visibility.Visible;
                }
            }
            catch { }
        }

        private double ParseJsonDouble(string json, string key)
        {
            string search = "\"" + key + "\":";
            int idx = json.IndexOf(search);
            if (idx < 0) return 0;
            idx += search.Length;
            int end = idx;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '.' || json[end] == '-'))
                end++;
            double val;
            double.TryParse(json.Substring(idx, end - idx),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out val);
            return val;
        }

        private string GetWeatherIcon(int code)
        {
            if (code == 0) return "\u2600"; // ☀
            if (code <= 3) return "\u26C5"; // ⛅
            if (code <= 48) return "\u2601"; // ☁ fog
            if (code <= 57) return "\u2602"; // ☂ drizzle
            if (code <= 67) return "\u2614"; // ☔ rain
            if (code <= 77) return "\u2744"; // ❄ snow
            if (code <= 82) return "\u2614"; // ☔ showers
            return "\u26A1"; // ⚡ thunderstorm
        }

        #endregion

        #region Countdown

        private void UpdateCountdown()
        {
            if (!showCountdown || countdownTarget == DateTime.MinValue)
            {
                CountdownText.Visibility = Visibility.Collapsed;
                return;
            }

            TimeSpan diff = countdownTarget.Date - DateTime.Now.Date;
            if (diff.TotalDays < 0)
            {
                CountdownText.Text = countdownName + " \u2014 \u0110\u00E3 qua";
            }
            else if (diff.TotalDays == 0)
            {
                CountdownText.Text = countdownName + " \u2014 H\u00F4m nay!";
            }
            else
            {
                CountdownText.Text = countdownName + " \u2014 C\u00F2n " + (int)diff.TotalDays + " ng\u00E0y";
            }
            CountdownText.Visibility = Visibility.Visible;
        }

        #endregion

        #region Depth Effect

        private void LoadForeground()
        {
            if (!useDepthEffect)
            {
                ForegroundOverlay.Visibility = Visibility.Collapsed;
                return;
            }

            try
            {
                using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    if (store.FileExists("Foreground.png"))
                    {
                        using (var stream = store.OpenFile("Foreground.png",
                            System.IO.FileMode.Open, System.IO.FileAccess.Read))
                        {
                            var bitmap = new BitmapImage();
                            bitmap.DecodePixelWidth = 480; // RAM optimization
                            bitmap.SetSource(stream);
                            ForegroundBrush.ImageSource = bitmap;
                            ForegroundOverlay.Visibility = Visibility.Visible;
                        }
                    }
                    else
                    {
                        ForegroundOverlay.Visibility = Visibility.Collapsed;
                    }
                }
            }
            catch
            {
                ForegroundOverlay.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        #region Navigation

        private void PhoneApplicationPage_BackKeyPress(
            object sender, System.ComponentModel.CancelEventArgs e)
        {
            // If PIN or Pattern grid is visible, go back to main lock screen
            if (PassGrid.Visibility == Visibility.Visible)
            {
                e.Cancel = true;
                PassGrid.Visibility = Visibility.Collapsed;
                passwordText = "";
                UpdatePassCodeInd();
                VisualStateManager.GoToState(this, "PassClose", true);

                // Reset overlay
                var t = (CompositeTransform)OverlayInformationPanel.RenderTransform;
                t.TranslateY = 0;
                OverlayInformationPanel.Opacity = 1;
                return;
            }

            if (PatternGrid.Visibility == Visibility.Visible)
            {
                e.Cancel = true;
                PatternGrid.Visibility = Visibility.Collapsed;
                VisualStateManager.GoToState(this, "PassClose", true);

                var t = (CompositeTransform)OverlayInformationPanel.RenderTransform;
                t.TranslateY = 0;
                OverlayInformationPanel.Opacity = 1;
                return;
            }

            if (RecoverGrid.Visibility == Visibility.Visible)
            {
                e.Cancel = true;
                RecoverGrid.Visibility = Visibility.Collapsed;
                VisualStateManager.GoToState(this, "PassClose", true);

                var t = (CompositeTransform)OverlayInformationPanel.RenderTransform;
                t.TranslateY = 0;
                OverlayInformationPanel.Opacity = 1;
                return;
            }

            // Block back on lock screen — cannot exit
            e.Cancel = true;
        }

        #endregion
    }
}
