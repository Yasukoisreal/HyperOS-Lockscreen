using System;
using System.Collections.Generic;
using System.IO.IsolatedStorage;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Phone.Controls;
using Microsoft.Xna.Framework;
using MC = System.Windows.Media.Color;
using Windows.Phone.System;
using Microsoft.Phone.Info;
using System.Net;
using System.Device.Location;
using System.Windows.Shapes;

namespace HyperOS.Pages
{
    public partial class LockScreen : PhoneApplicationPage
    {
        // Timers
        private DispatcherTimer timer;
        private DispatcherTimer batteryTimer;

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
        private int dateAlign = 1;      // 0=Left, 1=Center, 2=Right

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
        private bool depthHourBehind = true;
        private bool depthColonBehind = true;
        private bool depthMinuteBehind = true;

        // Free positions (from Editor)
        private bool hasFreeLayout = false;
        private double clockFreeX, clockFreeY;
        private double weatherFreeX, weatherFreeY;
        private double countdownFreeX, countdownFreeY;

        // Font families for My Sets card preview
        private static readonly FontFamily[] ClockFontFamilies = {
            new FontFamily("/Assets/Fonts/MiSans-Regular.ttf#MiSans"),
            new FontFamily("/Assets/Fonts/MiSans-Demibold.ttf#MiSans"),
            new FontFamily("/Assets/Fonts/MiSans-Light.ttf#MiSans"),
            new FontFamily("/Assets/Fonts/BebasNeue-Regular.ttf#Bebas Neue"),
            new FontFamily("/Assets/Fonts/PlayfairDisplay-Regular.ttf#Playfair Display"),
            new FontFamily("/Assets/Fonts/DMSerifDisplay-Regular.ttf#DM Serif Display"),
            new FontFamily("/Assets/Fonts/InstrumentSerif-Regular.ttf#Instrument Serif"),
            new FontFamily("/Assets/Fonts/Montserrat-Bold.ttf#Montserrat"),
            new FontFamily("/Assets/Fonts/Poppins-SemiBold.ttf#Poppins"),
            new FontFamily("/Assets/Fonts/Raleway-Light.ttf#Raleway"),
            new FontFamily("/Assets/Fonts/AbrilFatface-Regular.ttf#Abril Fatface"),
            new FontFamily("Segoe WP"),
            new FontFamily("Segoe WP Black"),
        };

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
            ApplyFreePositions();
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

            // Weather timer (every 30 min)
            weatherTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
            weatherTimer.Tick += (s, a) => FetchWeather();
            weatherTimer.Start();
            if (showWeather)
            {
                LoadCachedWeather(); // Show cached data immediately
                FetchWeather();      // Then refresh from API
            }

            // Load depth effect BEFORE animations so animation knows which parts to skip
            LoadForeground();
            ApplyDepthLayers();
            UpdateCountdown();

            // Play animations on first load (must be after ApplyDepthLayers)
            PlayEntryAnimations();

            isFirstLoad = false;
        }

        protected override void OnNavigatedTo(System.Windows.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (!isFirstLoad)
            {
                // Only do a full reload when user navigates BACK from EditorPage/Settings.
                // Skip reload on OS resume (Reset) — the page is already in memory.
                if (e.NavigationMode == System.Windows.Navigation.NavigationMode.Back)
                {
                    // User changed settings, reload everything
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
                    UpdateCountdown();
                    if (showWeather) FetchWeather();
                    LoadForeground();
                    ApplyDepthLayers();
                    ApplyFreePositions();

                    // Replay animations (user just came back from settings)
                    PlayEntryAnimations();
                }

                // Always reset swipe overlay & security grids on any resume
                var t = (CompositeTransform)OverlayInformationPanel.RenderTransform;
                t.TranslateY = 0;
                OverlayInformationPanel.Opacity = 1;
                var tb = (CompositeTransform)BehindForegroundGrid.RenderTransform;
                tb.TranslateY = 0;
                BehindForegroundGrid.Opacity = 1;

                PassGrid.Visibility = Visibility.Collapsed;
                PatternGrid.Visibility = Visibility.Collapsed;
                RecoverGrid.Visibility = Visibility.Collapsed;
            }
        }

        private void PlayEntryAnimations()
        {
            if (bIsAnimOn)
            {
                try
                {
                    // Build clock animation in code to respect depth layers.
                    // Only animate parts that are NOT behind the foreground.
                    var sb = new Storyboard();
                    int delayMs = 0;

                    // Hour
                    if (!useDepthEffect || !depthHourBehind)
                    {
                        AddFadeSlide(sb, HourPart, delayMs, 600);
                    }
                    delayMs += 100;

                    // Colon
                    if (!useDepthEffect || !depthColonBehind)
                    {
                        AddFadeSlide(sb, ColonPart, delayMs, 600);
                    }
                    delayMs += 100;

                    // Minute
                    if (!useDepthEffect || !depthMinuteBehind)
                    {
                        AddFadeSlide(sb, MinutePart, delayMs, 600);
                    }

                    if (sb.Children.Count > 0)
                        sb.Begin();

                    ((Storyboard)Resources["DayAnim"]).Begin();
                }
                catch { }
            }
        }

        private void AddFadeSlide(Storyboard sb, UIElement target, int delayMs, int durationMs)
        {
            // Opacity: 0 → 1
            var opAnim = new DoubleAnimationUsingKeyFrames();
            Storyboard.SetTarget(opAnim, target);
            Storyboard.SetTargetProperty(opAnim, new PropertyPath("(UIElement.Opacity)"));
            opAnim.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.Zero, Value = 0 });
            if (delayMs > 0)
                opAnim.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(delayMs), Value = 0 });
            opAnim.KeyFrames.Add(new EasingDoubleKeyFrame
            {
                KeyTime = TimeSpan.FromMilliseconds(delayMs + durationMs),
                Value = 1,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
            sb.Children.Add(opAnim);

            // TranslateY: 30 → 0
            var trAnim = new DoubleAnimationUsingKeyFrames();
            Storyboard.SetTarget(trAnim, target);
            Storyboard.SetTargetProperty(trAnim,
                new PropertyPath("(UIElement.RenderTransform).(CompositeTransform.TranslateY)"));
            trAnim.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.Zero, Value = 30 });
            if (delayMs > 0)
                trAnim.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(delayMs), Value = 30 });
            trAnim.KeyFrames.Add(new EasingDoubleKeyFrame
            {
                KeyTime = TimeSpan.FromMilliseconds(delayMs + durationMs),
                Value = 0,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
            sb.Children.Add(trAnim);
        }

        #region Time & Date

        private void UpdateTime()
        {
            string newTime = DateTime.Now.ToString("HH:mm");
            if (newTime != lastTimeText)
            {
                lastTimeText = newTime;
                string[] parts = newTime.Split(':');
                string h = parts[0];
                string m = parts.Length > 1 ? parts[1] : "00";
                // Front parts
                HourPart.Text = h;
                MinutePart.Text = m;
                // Behind parts (for depth effect)
                HourPartBehind.Text = h;
                MinutePartBehind.Text = m;

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
                // Sync behind layer
                var tb = (CompositeTransform)BehindForegroundGrid.RenderTransform;
                tb.TranslateY = newY;
                // Fade opacity based on swipe distance
                double opacity = Math.Max(0, 1 + newY / 500);
                OverlayInformationPanel.Opacity = opacity;
                BehindForegroundGrid.Opacity = opacity;
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
                var tb2 = (CompositeTransform)BehindForegroundGrid.RenderTransform;
                tb2.TranslateY = 0;
                BehindForegroundGrid.Opacity = 1;
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
            if (s.Contains("sPassword"))
                UserPassword = (string)s["sPassword"];
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
            if (s.Contains("DateAlign"))
                dateAlign = (int)s["DateAlign"];

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
            if (s.Contains("DepthHourBehind"))
                depthHourBehind = (bool)s["DepthHourBehind"];
            if (s.Contains("DepthColonBehind"))
                depthColonBehind = (bool)s["DepthColonBehind"];
            if (s.Contains("DepthMinuteBehind"))
                depthMinuteBehind = (bool)s["DepthMinuteBehind"];

            // Free layout positions (from Editor)
            hasFreeLayout = s.Contains("ClockX");
            if (hasFreeLayout)
            {
                clockFreeX = (double)s["ClockX"];
                clockFreeY = (double)s["ClockY"];
                weatherFreeX = s.Contains("WeatherX") ? (double)s["WeatherX"] : clockFreeX;
                weatherFreeY = s.Contains("WeatherY") ? (double)s["WeatherY"] : clockFreeY + 155;
                countdownFreeX = s.Contains("CountdownX") ? (double)s["CountdownX"] : clockFreeX;
                countdownFreeY = s.Contains("CountdownY") ? (double)s["CountdownY"] : clockFreeY + 185;
            }
        }

        private void SetClockFont(FontFamily ff)
        {
            HourPart.FontFamily = ff;
            ColonPart.FontFamily = ff;
            MinutePart.FontFamily = ff;
            HourPartBehind.FontFamily = ff;
            ColonPartBehind.FontFamily = ff;
            MinutePartBehind.FontFamily = ff;
        }

        private void ApplyClockStyle()
        {
            switch (clockStyle)
            {
                case 0: SetClockFont(new FontFamily("/Assets/Fonts/MiSans-Regular.ttf#MiSans")); break;
                case 1: SetClockFont(new FontFamily("/Assets/Fonts/MiSans-Demibold.ttf#MiSans")); break;
                case 2: SetClockFont(new FontFamily("/Assets/Fonts/MiSans-Light.ttf#MiSans")); break;
                case 3: SetClockFont(new FontFamily("/Assets/Fonts/BebasNeue-Regular.ttf#Bebas Neue")); break;
                case 4: SetClockFont(new FontFamily("/Assets/Fonts/PlayfairDisplay-Regular.ttf#Playfair Display")); break;
                case 5: SetClockFont(new FontFamily("/Assets/Fonts/DMSerifDisplay-Regular.ttf#DM Serif Display")); break;
                case 6: SetClockFont(new FontFamily("/Assets/Fonts/InstrumentSerif-Regular.ttf#Instrument Serif")); break;
                case 7: SetClockFont(new FontFamily("/Assets/Fonts/Montserrat-Bold.ttf#Montserrat")); break;
                case 8: SetClockFont(new FontFamily("/Assets/Fonts/Poppins-SemiBold.ttf#Poppins")); break;
                case 9: SetClockFont(new FontFamily("/Assets/Fonts/Raleway-Light.ttf#Raleway")); break;
                case 10: SetClockFont(new FontFamily("/Assets/Fonts/AbrilFatface-Regular.ttf#Abril Fatface")); break;
                case 11: SetClockFont(new FontFamily("Segoe WP")); break;
                case 12: SetClockFont(new FontFamily("Segoe WP Black")); break;
                default: SetClockFont(new FontFamily("/Assets/Fonts/MiSans-Regular.ttf#MiSans")); break;
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
                    TimePanel.HorizontalAlignment = HorizontalAlignment.Left;
                    BehindTimePanel.HorizontalAlignment = HorizontalAlignment.Left;
                    break;
                case 2: // Right
                    ClockPanel.HorizontalAlignment = HorizontalAlignment.Right;
                    ClockPanel.Margin = new Thickness(0, ClockPanel.Margin.Top, 24, ClockPanel.Margin.Bottom);
                    DateInfoPanel.HorizontalAlignment = HorizontalAlignment.Right;
                    TimePanel.HorizontalAlignment = HorizontalAlignment.Right;
                    BehindTimePanel.HorizontalAlignment = HorizontalAlignment.Right;
                    break;
                default: // Center
                    ClockPanel.HorizontalAlignment = HorizontalAlignment.Center;
                    DateInfoPanel.HorizontalAlignment = HorizontalAlignment.Center;
                    TimePanel.HorizontalAlignment = HorizontalAlignment.Center;
                    BehindTimePanel.HorizontalAlignment = HorizontalAlignment.Center;
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
                    case 5: brush = MakeGradient(C(255, 105, 180), C(148, 0, 211));   break; // Rose
                    case 6: brush = MakeGradient(C(255, 50, 0),    C(255, 165, 0));   break; // Fire
                    case 7: brush = MakeGradient(C(255, 255, 255), C(173, 216, 230)); break; // Ice
                    case 8: brush = MakeGradient(C(50, 205, 50),   C(255, 255, 0));   break; // Lime
                    case 9: brush = MakeGradient(C(75, 0, 130),    C(25, 25, 112));   break; // Twilight
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
                    case 5: brush = new SolidColorBrush(C(91, 255, 176)); break;  // Mint
                    case 6: brush = new SolidColorBrush(C(196, 167, 255)); break; // Lavender
                    case 7: brush = new SolidColorBrush(C(255, 140, 66)); break;  // Orange
                    case 8: brush = new SolidColorBrush(C(0, 229, 255)); break;   // Cyan
                    case 9: brush = new SolidColorBrush(C(160, 160, 176)); break; // Silver
                    default: brush = new SolidColorBrush(Colors.White); break;
                }
            }
            HourPart.Foreground = brush;
            ColonPart.Foreground = brush;
            MinutePart.Foreground = brush;
            HourPartBehind.Foreground = brush;
            ColonPartBehind.Foreground = brush;
            MinutePartBehind.Foreground = brush;
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
            HourPart.FontSize = sz;
            ColonPart.FontSize = sz;
            MinutePart.FontSize = sz;
            HourPartBehind.FontSize = sz;
            ColonPartBehind.FontSize = sz;
            MinutePartBehind.FontSize = sz;
            // Pull clock closer to date
            double pull = -sz * 0.16;
            TimePanel.Margin = new Thickness(0, pull, 0, 0);
            BehindTimePanel.Margin = new Thickness(0, pull, 0, 0);
        }

        private void ApplyDepthLayers()
        {
            if (!useDepthEffect)
            {
                // No depth — all front, hide behind layer
                BehindForegroundGrid.Visibility = Visibility.Collapsed;
                HourPart.Opacity = 1;
                ColonPart.Opacity = 1;
                MinutePart.Opacity = 1;
                return;
            }

            // Sync behind panel position with front panel
            BehindClockPanel.VerticalAlignment = ClockPanel.VerticalAlignment;
            BehindClockPanel.Margin = ClockPanel.Margin;
            BehindClockPanel.HorizontalAlignment = ClockPanel.HorizontalAlignment;

            bool anyBehind = depthHourBehind || depthColonBehind || depthMinuteBehind;
            BehindForegroundGrid.Visibility = anyBehind ? Visibility.Visible : Visibility.Collapsed;

            // Hour
            HourPart.Opacity = depthHourBehind ? 0 : 1;
            HourPartBehind.Opacity = depthHourBehind ? 1 : 0;

            // Colon
            ColonPart.Opacity = depthColonBehind ? 0 : 1;
            ColonPartBehind.Opacity = depthColonBehind ? 1 : 0;

            // Minute
            MinutePart.Opacity = depthMinuteBehind ? 0 : 1;
            MinutePartBehind.Opacity = depthMinuteBehind ? 1 : 0;
        }

        private void ApplyFreePositions()
        {
            if (hasFreeLayout)
            {
                // ── Free layout from Editor ──
                // Editor's ClockHandle has Padding=4 + Border=1.5 = 5.5px offset
                // that the lock screen panels don't have. Compensate here.
                const double PAD = 5.5;

                ClockPanel.VerticalAlignment = VerticalAlignment.Top;
                ClockPanel.HorizontalAlignment = HorizontalAlignment.Left;
                ClockPanel.Margin = new Thickness(clockFreeX + PAD, clockFreeY + PAD, 0, 0);

                // Force child elements to Left alignment (free positioning is absolute)
                // Then apply user's date alignment preference
                TimePanel.HorizontalAlignment = HorizontalAlignment.Left;
                BehindTimePanel.HorizontalAlignment = HorizontalAlignment.Left;
                switch (dateAlign)
                {
                    case 0: DateInfoPanel.HorizontalAlignment = HorizontalAlignment.Left; break;
                    case 2: DateInfoPanel.HorizontalAlignment = HorizontalAlignment.Right; break;
                    default: DateInfoPanel.HorizontalAlignment = HorizontalAlignment.Center; break;
                }

                BehindClockPanel.VerticalAlignment = VerticalAlignment.Top;
                BehindClockPanel.HorizontalAlignment = HorizontalAlignment.Left;
                BehindClockPanel.Margin = new Thickness(clockFreeX + PAD, clockFreeY + PAD, 0, 0);

                if (WeatherText.Visibility == Visibility.Visible)
                    WeatherText.Margin = new Thickness(weatherFreeX + PAD, weatherFreeY, 0, 0);

                if (CountdownText.Visibility == Visibility.Visible)
                    CountdownText.Margin = new Thickness(countdownFreeX + PAD, countdownFreeY, 0, 0);

                if (OwnerInfoText.Visibility == Visibility.Visible)
                {
                    double ownerY = countdownFreeY + 30;
                    if (CountdownText.Visibility != Visibility.Visible)
                        ownerY = weatherFreeY + 30;
                    OwnerInfoText.Margin = new Thickness(clockFreeX, ownerY, 0, 0);
                }
            }
            else
            {
                // ── Legacy layout: position widgets relative to clock ──
                // Match the clock's horizontal alignment for all widgets
                HorizontalAlignment ha = ClockPanel.HorizontalAlignment;
                WeatherText.HorizontalAlignment = ha;
                CountdownText.HorizontalAlignment = ha;
                OwnerInfoText.HorizontalAlignment = ha;

                // Vertical: position widgets at the same vertical as clock, offset down
                // We use the clock panel's SizeChanged to compute offset
                PositionLegacyWidgets();
                ClockPanel.SizeChanged -= ClockPanel_SizeChanged;
                ClockPanel.SizeChanged += ClockPanel_SizeChanged;
            }
        }

        private void ClockPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            PositionLegacyWidgets();
        }

        private void PositionLegacyWidgets()
        {
            // Compute the bottom edge of the clock in the Grid
            double clockBottom = ClockPanel.Margin.Top + ClockPanel.ActualHeight;

            // If clock is centered/bottom-aligned, estimate position
            if (ClockPanel.VerticalAlignment == VerticalAlignment.Center)
            {
                double pageH = 800; // WP8.1 standard WVGA
                clockBottom = (pageH + ClockPanel.ActualHeight) / 2.0 - 20;
            }
            else if (ClockPanel.VerticalAlignment == VerticalAlignment.Bottom)
            {
                clockBottom = 800 - 40;
            }
            else
            {
                clockBottom = ClockPanel.Margin.Top + ClockPanel.ActualHeight;
            }

            // Position weather below clock
            if (WeatherText.Visibility == Visibility.Visible)
            {
                WeatherText.VerticalAlignment = VerticalAlignment.Top;
                WeatherText.Margin = new Thickness(
                    WeatherText.Margin.Left, clockBottom + 6, 0, 0);
                clockBottom += 28;
            }

            if (CountdownText.Visibility == Visibility.Visible)
            {
                CountdownText.VerticalAlignment = VerticalAlignment.Top;
                CountdownText.Margin = new Thickness(
                    CountdownText.Margin.Left, clockBottom + 4, 0, 0);
                clockBottom += 24;
            }

            if (OwnerInfoText.Visibility == Visibility.Visible)
            {
                OwnerInfoText.VerticalAlignment = VerticalAlignment.Top;
                OwnerInfoText.Margin = new Thickness(
                    OwnerInfoText.Margin.Left, clockBottom + 12, 0, 0);
            }
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
                        }
                    }
                    else
                    {
                        // No custom wallpaper — use default
                        BackgroundBrush.ImageSource = new BitmapImage(
                            new Uri("/Assets/BlurBackground.jpg", UriKind.Relative));
                    }
                }
                backgroundLoaded = true;
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

        #region Long Press → My Sets (Inline Overlay)

        private class MSPreset
        {
            public string Name, Subtitle;
            public int ClockStyle, ClockSize, ClockColor, ClockBlend, DateAlign;
            public MC PreviewBg, PreviewClockColor;
            public double ClockX = -1, ClockY = -1;
            public bool UseDepthEffect;
            public bool DepthHourBehind = true, DepthColonBehind = true, DepthMinuteBehind = true;
        }

        private static readonly List<MSPreset> msPresets = new List<MSPreset>
        {
            new MSPreset { Name="Classic", Subtitle="What's classic never goes out of style.", ClockStyle=0, ClockSize=2, ClockColor=0, ClockBlend=0, DateAlign=1, PreviewBg=MC.FromArgb(255,60,60,80), PreviewClockColor=MC.FromArgb(255,255,255,255) },
            new MSPreset { Name="Bold", Subtitle="Make a statement with bold typography.", ClockStyle=1, ClockSize=4, ClockColor=0, ClockBlend=1, DateAlign=1, PreviewBg=MC.FromArgb(255,40,20,60), PreviewClockColor=MC.FromArgb(255,255,120,80) },
            new MSPreset { Name="Elegant", Subtitle="Refined beauty in every detail.", ClockStyle=4, ClockSize=3, ClockColor=1, ClockBlend=0, DateAlign=1, PreviewBg=MC.FromArgb(255,30,30,30), PreviewClockColor=MC.FromArgb(255,255,215,0) },
            new MSPreset { Name="Neon", Subtitle="Electrify your screen.", ClockStyle=3, ClockSize=3, ClockColor=8, ClockBlend=4, DateAlign=0, PreviewBg=MC.FromArgb(255,10,10,30), PreviewClockColor=MC.FromArgb(255,0,229,255) },
            new MSPreset { Name="Magazine", Subtitle="Turn your lock screen into a cover.", ClockStyle=6, ClockSize=2, ClockColor=0, ClockBlend=0, DateAlign=1, PreviewBg=MC.FromArgb(255,180,120,80), PreviewClockColor=MC.FromArgb(255,255,255,255) },
            new MSPreset { Name="Minimal", Subtitle="Less is more.", ClockStyle=9, ClockSize=1, ClockColor=9, ClockBlend=0, DateAlign=1, PreviewBg=MC.FromArgb(255,20,20,25), PreviewClockColor=MC.FromArgb(255,160,160,176) },
            new MSPreset { Name="Serif", Subtitle="Timeless serif elegance.", ClockStyle=5, ClockSize=3, ClockColor=0, ClockBlend=0, DateAlign=1, PreviewBg=MC.FromArgb(255,50,40,60), PreviewClockColor=MC.FromArgb(255,255,255,255) },
            new MSPreset { Name="Display", Subtitle="Time is important. Make it count.", ClockStyle=10, ClockSize=4, ClockColor=0, ClockBlend=5, DateAlign=1, PreviewBg=MC.FromArgb(255,20,10,10), PreviewClockColor=MC.FromArgb(255,255,105,180) },
            new MSPreset { Name="Fire", Subtitle="Feel the heat.", ClockStyle=1, ClockSize=3, ClockColor=4, ClockBlend=6, DateAlign=0, PreviewBg=MC.FromArgb(255,40,10,5), PreviewClockColor=MC.FromArgb(255,255,80,0) },
            new MSPreset { Name="Ocean", Subtitle="Calm waves, deep blue.", ClockStyle=7, ClockSize=2, ClockColor=2, ClockBlend=2, DateAlign=1, PreviewBg=MC.FromArgb(255,10,30,60), PreviewClockColor=MC.FromArgb(255,0,180,255) },
            new MSPreset { Name="Aurora", Subtitle="Northern lights on your screen.", ClockStyle=2, ClockSize=2, ClockColor=5, ClockBlend=3, DateAlign=1, PreviewBg=MC.FromArgb(255,10,20,30), PreviewClockColor=MC.FromArgb(255,80,255,120) },
            new MSPreset { Name="Poppins", Subtitle="Modern geometric beauty.", ClockStyle=8, ClockSize=3, ClockColor=3, ClockBlend=0, DateAlign=1, PreviewBg=MC.FromArgb(255,60,40,50), PreviewClockColor=MC.FromArgb(255,255,182,193) },
            new MSPreset { Name="Twilight", Subtitle="Between day and night.", ClockStyle=4, ClockSize=2, ClockColor=6, ClockBlend=9, DateAlign=1, PreviewBg=MC.FromArgb(255,25,10,50), PreviewClockColor=MC.FromArgb(255,148,100,255) },
            new MSPreset { Name="Ice", Subtitle="Cool and crisp.", ClockStyle=9, ClockSize=2, ClockColor=0, ClockBlend=7, DateAlign=1, PreviewBg=MC.FromArgb(255,200,220,240), PreviewClockColor=MC.FromArgb(255,255,255,255) },
            new MSPreset { Name="Lime", Subtitle="Fresh and vibrant energy.", ClockStyle=7, ClockSize=3, ClockColor=5, ClockBlend=8, DateAlign=0, PreviewBg=MC.FromArgb(255,15,40,15), PreviewClockColor=MC.FromArgb(255,100,255,100) },
        };

        private const double MS_CW = 200, MS_CH = 360, MS_GAP = 16, MS_STEP = 216, MS_SW = 480;
        private int msCurrentIndex;
        private double msOffsetX, msTotalDragX;
        private List<Border> msCards = new List<Border>();
        private List<Ellipse> msDots = new List<Ellipse>();
        private Dictionary<int, BitmapImage> msWallpapers = new Dictionary<int, BitmapImage>();
        private BitmapImage msForeground;

        private void LayoutRoot_Hold(object sender, System.Windows.Input.GestureEventArgs e)
        {
            ShowMySetsOverlay();
        }

        private void ShowMySetsOverlay()
        {
            // Load per-preset wallpapers
            msWallpapers.Clear();
            try
            {
                using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    for (int i = 0; i < msPresets.Count; i++)
                    {
                        string file = "Background_" + i + ".jpg";
                        if (store.FileExists(file))
                        {
                            using (var stream = store.OpenFile(file, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                            {
                                var bmp = new BitmapImage();
                                bmp.SetSource(stream);
                                msWallpapers[i] = bmp;
                            }
                        }
                    }
                }
            }
            catch { }

            // Read saved preset overrides
            var s = IsolatedStorageSettings.ApplicationSettings;

            // First preset (Classic) reads live/active settings
            var first = msPresets[0];
            if (s.Contains("ClockStyle")) try { first.ClockStyle = (int)s["ClockStyle"]; } catch { }
            if (s.Contains("ClockSize")) try { first.ClockSize = (int)s["ClockSize"]; } catch { }
            if (s.Contains("ClockColor")) try { first.ClockColor = (int)s["ClockColor"]; } catch { }
            if (s.Contains("ClockBlend")) try { first.ClockBlend = (int)s["ClockBlend"]; } catch { }
            if (s.Contains("ClockX")) try { first.ClockX = (double)s["ClockX"]; } catch { }
            if (s.Contains("ClockY")) try { first.ClockY = (double)s["ClockY"]; } catch { }
            if (s.Contains("UseDepthEffect")) try { first.UseDepthEffect = (bool)s["UseDepthEffect"]; } catch { }
            if (s.Contains("DepthHourBehind")) try { first.DepthHourBehind = (bool)s["DepthHourBehind"]; } catch { }
            if (s.Contains("DepthColonBehind")) try { first.DepthColonBehind = (bool)s["DepthColonBehind"]; } catch { }
            if (s.Contains("DepthMinuteBehind")) try { first.DepthMinuteBehind = (bool)s["DepthMinuteBehind"]; } catch { }

            for (int i = 1; i < msPresets.Count; i++)
            {
                string pfx = "Set" + i + "_";
                if (s.Contains(pfx + "ClockStyle"))
                {
                    var p = msPresets[i];
                    try { p.ClockStyle = (int)s[pfx + "ClockStyle"]; } catch { }
                    if (s.Contains(pfx + "ClockSize")) try { p.ClockSize = (int)s[pfx + "ClockSize"]; } catch { }
                    if (s.Contains(pfx + "ClockColor")) try { p.ClockColor = (int)s[pfx + "ClockColor"]; } catch { }
                    if (s.Contains(pfx + "ClockBlend")) try { p.ClockBlend = (int)s[pfx + "ClockBlend"]; } catch { }
                    if (s.Contains(pfx + "ClockX")) try { p.ClockX = (double)s[pfx + "ClockX"]; } catch { }
                    if (s.Contains(pfx + "ClockY")) try { p.ClockY = (double)s[pfx + "ClockY"]; } catch { }
                    if (s.Contains(pfx + "UseDepthEffect")) try { p.UseDepthEffect = (bool)s[pfx + "UseDepthEffect"]; } catch { }
                    if (s.Contains(pfx + "DepthHourBehind")) try { p.DepthHourBehind = (bool)s[pfx + "DepthHourBehind"]; } catch { }
                    if (s.Contains(pfx + "DepthColonBehind")) try { p.DepthColonBehind = (bool)s[pfx + "DepthColonBehind"]; } catch { }
                    if (s.Contains(pfx + "DepthMinuteBehind")) try { p.DepthMinuteBehind = (bool)s[pfx + "DepthMinuteBehind"]; } catch { }
                }
            }

            // Load foreground image for depth
            msForeground = null;
            try
            {
                using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    if (store.FileExists("Foreground.png"))
                        using (var stream = store.OpenFile("Foreground.png", System.IO.FileMode.Open, System.IO.FileAccess.Read))
                        {
                            var bmp = new BitmapImage();
                            bmp.SetSource(stream);
                            msForeground = bmp;
                        }
                }
            }
            catch { }

            // Build cards
            MySetsCarousel.Children.Clear();
            msCards.Clear();
            MySetsDotsPanel.Children.Clear();
            msDots.Clear();

            for (int i = 0; i < msPresets.Count; i++)
            {
                var card = BuildMSCard(msPresets[i], i);
                msCards.Add(card);
                MySetsCarousel.Children.Add(card);
                Canvas.SetTop(card, 30);

                var dot = new Ellipse { Width = 6, Height = 6, Fill = new SolidColorBrush(MC.FromArgb(80, 255, 255, 255)), Margin = new Thickness(3, 0, 3, 0) };
                msDots.Add(dot);
                MySetsDotsPanel.Children.Add(dot);
            }

            // Start at the currently active preset
            int startIdx = 0;
            var settings = IsolatedStorageSettings.ApplicationSettings;
            if (settings.Contains("ActivePresetIndex"))
                try { startIdx = (int)settings["ActivePresetIndex"]; } catch { }
            startIdx = Math.Max(0, Math.Min(msPresets.Count - 1, startIdx));

            msCurrentIndex = startIdx;
            msOffsetX = 0;
            MSGoToIndex(startIdx, false);
            MySetsOverlay.Visibility = Visibility.Visible;
        }

        private Border BuildMSCard(MSPreset preset, int index)
        {
            Brush bg;
            BitmapImage wp;
            if (msWallpapers.TryGetValue(index, out wp))
                bg = new ImageBrush { ImageSource = wp, Stretch = Stretch.UniformToFill };
            else
                bg = new SolidColorBrush(preset.PreviewBg);

            var card = new Border
            {
                Width = MS_CW, Height = MS_CH, CornerRadius = new CornerRadius(24),
                Background = bg, Tag = index,
                RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(),
            };

            var inner = new Grid { Width = MS_CW, Height = MS_CH };
            inner.Clip = new RectangleGeometry
            {
                Rect = new Rect(0, 0, MS_CW, MS_CH),
                RadiusX = 24, RadiusY = 24
            };
            card.Child = inner;

            // Gradient
            inner.Children.Add(new System.Windows.Shapes.Rectangle
            {
                VerticalAlignment = VerticalAlignment.Bottom, Height = 160, IsHitTestVisible = false,
                Fill = new LinearGradientBrush
                {
                    StartPoint = new System.Windows.Point(0, 0), EndPoint = new System.Windows.Point(0, 1),
                    GradientStops = { new GradientStop { Color = MC.FromArgb(0, 0, 0, 0), Offset = 0 }, new GradientStop { Color = MC.FromArgb(140, 0, 0, 0), Offset = 1 } }
                }
            });

            // Clock setup
            int fi = Math.Min(preset.ClockStyle, ClockFontFamilies.Length - 1);
            int[] sizes = { 36, 42, 48, 54, 64 };
            int sz = sizes[Math.Min(preset.ClockSize, sizes.Length - 1)];
            var brush = new SolidColorBrush(preset.PreviewClockColor);
            var transBrush = new SolidColorBrush(Colors.Transparent);
            bool hasDepth = preset.UseDepthEffect && msForeground != null;

            // --- BEHIND LAYER (or full layer if no depth) ---
            var behindStack = BuildMSClockStack(preset, fi, sz,
                hasDepth ? (preset.DepthHourBehind ? brush : transBrush) : brush,
                hasDepth ? (preset.DepthColonBehind ? brush : transBrush) : brush,
                hasDepth ? (preset.DepthMinuteBehind ? brush : transBrush) : brush,
                hasDepth ? transBrush : new SolidColorBrush(MC.FromArgb(180, 255, 255, 255)));
            inner.Children.Add(behindStack);

            // --- FOREGROUND OVERLAY ---
            if (hasDepth)
            {
                inner.Children.Add(new Border
                {
                    Background = new ImageBrush { ImageSource = msForeground, Stretch = Stretch.UniformToFill },
                    IsHitTestVisible = false
                });

                // --- FRONT LAYER ---
                var frontStack = BuildMSClockStack(preset, fi, sz,
                    preset.DepthHourBehind ? transBrush : brush,
                    preset.DepthColonBehind ? transBrush : brush,
                    preset.DepthMinuteBehind ? transBrush : brush,
                    new SolidColorBrush(MC.FromArgb(180, 255, 255, 255)));
                inner.Children.Add(frontStack);
            }

            // Border frame
            inner.Children.Add(new Border { BorderBrush = new SolidColorBrush(MC.FromArgb(50, 255, 255, 255)), BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(24), IsHitTestVisible = false });

            return card;
        }

        private StackPanel BuildMSClockStack(MSPreset preset, int fi, int sz,
            Brush hourBrush, Brush colonBrush, Brush minuteBrush, Brush dateBrush)
        {
            var stack = new StackPanel();
            if (preset.ClockX >= 0 && preset.ClockY >= 0)
            {
                stack.HorizontalAlignment = HorizontalAlignment.Left;
                stack.VerticalAlignment = VerticalAlignment.Top;
                stack.Margin = new Thickness(preset.ClockX * (MS_CW / 480.0), preset.ClockY * (MS_CH / 800.0), 0, 0);
            }
            else
            {
                stack.VerticalAlignment = VerticalAlignment.Center;
                stack.HorizontalAlignment = HorizontalAlignment.Center;
                stack.Margin = new Thickness(0, -10, 0, 0);
            }
            stack.IsHitTestVisible = false;

            stack.Children.Add(new TextBlock
            {
                Text = DateTime.Now.DayOfWeek.ToString() + "  ·  " + DateTime.Now.ToString("MMMM d"),
                FontFamily = new FontFamily("/Assets/Fonts/MiSans-Regular.ttf#MiSans"),
                FontSize = 10, Foreground = dateBrush,
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 2)
            });

            var timeP = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, -sz * 0.16, 0, 0) };
            timeP.Children.Add(new TextBlock { Text = DateTime.Now.ToString("HH"), FontFamily = ClockFontFamilies[fi], FontSize = sz, Foreground = hourBrush });
            timeP.Children.Add(new TextBlock { Text = ":", FontFamily = ClockFontFamilies[fi], FontSize = sz, Foreground = colonBrush, Margin = new Thickness(0, -sz * 0.08, 0, 0) });
            timeP.Children.Add(new TextBlock { Text = DateTime.Now.ToString("mm"), FontFamily = ClockFontFamilies[fi], FontSize = sz, Foreground = minuteBrush });
            stack.Children.Add(timeP);

            return stack;
        }

        // Drag & snap
        private void MySets_ManipulationDelta(object sender, System.Windows.Input.ManipulationDeltaEventArgs e)
        {
            msOffsetX += e.DeltaManipulation.Translation.X;
            msTotalDragX += Math.Abs(e.DeltaManipulation.Translation.X);
            msOffsetX = Math.Max(-(msPresets.Count - 1) * MS_STEP - 40, Math.Min(40, msOffsetX));
            MSLayoutCards();
            e.Handled = true;
        }

        private void MySets_ManipulationCompleted(object sender, System.Windows.Input.ManipulationCompletedEventArgs e)
        {
            if (msTotalDragX < 15)
            {
                var src = e.OriginalSource as UIElement;
                if (src != null)
                {
                    try
                    {
                        var pt = src.TransformToVisual(Application.Current.RootVisual).Transform(e.ManipulationOrigin);
                        double cardL = (MS_SW - MS_CW) / 2.0, cardR = (MS_SW + MS_CW) / 2.0;
                        if (pt.X > cardR) { msTotalDragX = 0; MSGoToIndex(msCurrentIndex + 1, true); e.Handled = true; return; }
                        if (pt.X < cardL) { msTotalDragX = 0; MSGoToIndex(msCurrentIndex - 1, true); e.Handled = true; return; }
                    }
                    catch { }
                }
            }
            msTotalDragX = 0;
            int idx = (int)Math.Round(-msOffsetX / MS_STEP);
            idx = Math.Max(0, Math.Min(msPresets.Count - 1, idx));
            MSGoToIndex(idx, true);
            e.Handled = true;
        }

        private void MSGoToIndex(int index, bool animate)
        {
            msCurrentIndex = Math.Max(0, Math.Min(msPresets.Count - 1, index));
            double target = -msCurrentIndex * MS_STEP;

            if (animate)
            {
                int steps = 12; double startOff = msOffsetX; int step = 0;
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                timer.Tick += (s2, ev2) =>
                {
                    step++;
                    double t = 1 - Math.Pow(1 - (double)step / steps, 3);
                    msOffsetX = startOff + (target - startOff) * t;
                    MSLayoutCards();
                    if (step >= steps) { timer.Stop(); msOffsetX = target; MSLayoutCards(); }
                };
                timer.Start();
            }
            else { msOffsetX = target; MSLayoutCards(); }

            MySetsTitle.Text = msPresets[msCurrentIndex].Name;
            MySetsSubtitle.Text = msPresets[msCurrentIndex].Subtitle;
            for (int i = 0; i < msDots.Count; i++)
            {
                msDots[i].Fill = new SolidColorBrush(i == msCurrentIndex ? MC.FromArgb(255, 255, 255, 255) : MC.FromArgb(80, 255, 255, 255));
                msDots[i].Width = i == msCurrentIndex ? 8 : 6;
                msDots[i].Height = i == msCurrentIndex ? 8 : 6;
            }
        }

        private void MSLayoutCards()
        {
            double cx = MS_SW / 2.0;
            for (int i = 0; i < msCards.Count; i++)
            {
                double left = cx - MS_CW / 2.0 + msOffsetX + i * MS_STEP;
                Canvas.SetLeft(msCards[i], left);
                double dist = Math.Abs(left + MS_CW / 2.0 - cx);
                double t2 = Math.Min(1.0, dist / MS_STEP);
                var ct = (ScaleTransform)msCards[i].RenderTransform;
                ct.ScaleX = 1.0 - 0.15 * t2; ct.ScaleY = 1.0 - 0.15 * t2;
                msCards[i].Opacity = 1.0 - 0.5 * t2;
            }
        }

        // Apply selected preset
        private void MySets_Apply_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            var preset = msPresets[msCurrentIndex];
            var s = IsolatedStorageSettings.ApplicationSettings;

            string pfx = "Set" + msCurrentIndex + "_";
            bool hasSavedSlot = s.Contains(pfx + "ClockStyle");

            if (hasSavedSlot)
            {
                // Copy all saved preset slot settings to global keys
                string[] keys = { "ClockStyle", "ClockPosition", "ClockHAlign", "ClockColor", "ClockBlend", "ClockSize",
                    "ShowWeather", "ShowCountdown", "UseDepthEffect", "DepthHourBehind", "DepthColonBehind", "DepthMinuteBehind",
                    "ClockX", "ClockY", "WeatherX", "WeatherY", "CountdownX", "CountdownY",
                    "bIsAnimOn", "DateAlign", "CountdownName", "CountdownTarget", "OwnerInfo" };
                foreach (var key in keys)
                {
                    string sk = pfx + key;
                    if (s.Contains(sk)) s[key] = s[sk];
                }
            }
            else
            {
                // Apply preset defaults — reset everything
                s["ClockStyle"] = preset.ClockStyle;
                s["ClockSize"] = preset.ClockSize;
                s["ClockColor"] = preset.ClockColor;
                s["ClockBlend"] = preset.ClockBlend;
                s["DateAlign"] = preset.DateAlign;
                s["ClockPosition"] = 1;   // Center
                s["ClockHAlign"] = 1;     // Center

                // Remove free layout positions so it uses default center
                string[] posKeys = { "ClockX", "ClockY", "WeatherX", "WeatherY", "CountdownX", "CountdownY" };
                foreach (var pk in posKeys)
                    if (s.Contains(pk)) s.Remove(pk);
            }

            // Handle wallpaper
            try
            {
                using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    string presetBg = "Background_" + msCurrentIndex + ".jpg";
                    if (store.FileExists(presetBg))
                    {
                        // Copy preset wallpaper to global
                        if (store.FileExists("Background.jpg"))
                            store.DeleteFile("Background.jpg");
                        store.CopyFile(presetBg, "Background.jpg");
                    }
                    else
                    {
                        // No custom wallpaper for this preset — remove old one
                        if (store.FileExists("Background.jpg"))
                            store.DeleteFile("Background.jpg");
                    }
                }
            }
            catch { }

            s["ActivePresetIndex"] = msCurrentIndex;
            s.Save();
            MySetsOverlay.Visibility = Visibility.Collapsed;

            // Reload lock screen with new settings
            LoadSettings();
            backgroundLoaded = false;
            LoadBackground();
            ApplyClockStyle();
            ApplyClockPosition();
            ApplyClockHAlign();
            ApplyClockColor();
            ApplyClockSize();
            ApplyFreePositions();
            lastTimeText = "";
            UpdateTime();
            LoadForeground();
            ApplyDepthLayers();
            UpdateCountdown();
        }

        private void MySets_Close_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            MySetsOverlay.Visibility = Visibility.Collapsed;
        }

        #endregion
    }
}
