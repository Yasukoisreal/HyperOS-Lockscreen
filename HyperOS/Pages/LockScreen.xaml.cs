using System;
using System.Collections.Generic;
using System.IO;
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
using HyperOS.Helpers;
using System.Net;
using System.Device.Location;
using System.Windows.Shapes;
using Microsoft.Devices;

namespace HyperOS.Pages
{
    public partial class LockScreen : PhoneApplicationPage
    {
        // Timers
        private DispatcherTimer timer;
        private DispatcherTimer minuteSyncTimer;
        private DispatcherTimer batteryTimer;
        private DispatcherTimer msAnimTimer;
        private bool isUnlockingStarted = false;

        // Settings flags
        private bool bIsAnimOn = true;
        private int clockStyle = 0;
        private int clockPosition = 1; // 0=Top, 1=Center, 2=Bottom
        private int clockHAlign = 1;    // 0=Left, 1=Center, 2=Right
        private int clockColor = 0;     // 0=White, 1=Gold, 2=SkyBlue, 3=Pink, 4=Red
        private int clockBlend = 0;     // 0=None, 1=Sunset, 2=Ocean, 3=Aurora, 4=Neon
        private int clockSize = 2;      // 0=S..4=XXL (default 2=L)
        private double clockOpacity = 1.0;
        private int clockHue = -1;
        private int dateAlign = 1;      // 0=Left, 1=Center, 2=Right
        private int clockLayout = 0;    // 0=Horiz, 1=Vert, 2=Analog Minimal, 3=Classic, 4=Swiss

        // Cached resources (CPU optimization)
        private static readonly SolidColorBrush ChargingBrush = new SolidColorBrush(
            System.Windows.Media.Color.FromArgb(0xAA, 0xFF, 0xCC, 0x00));
        private static readonly SolidColorBrush NormalBatteryBrush = new SolidColorBrush(
            System.Windows.Media.Color.FromArgb(0xAA, 0xFF, 0xFF, 0xFF));
        private static readonly SolidColorBrush BadgeCounterBgBrush = new SolidColorBrush(
            System.Windows.Media.Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
        private static readonly SolidColorBrush WhiteBrush = new SolidColorBrush(
            System.Windows.Media.Colors.White);
        private Windows.Phone.Devices.Power.Battery cachedBattery;
        private string lastTimeText = "";
        private string cachedCarrier = null;
        private DateTime lastCarrierCheck = DateTime.MinValue;
        private string lastBadgesSignature = null;
        private bool isFirstLoad = true;
        private bool backgroundLoaded = false;

        // Weather
        private DispatcherTimer weatherTimer;
        private bool showWeather = false;
        private double weatherLat = 0;
        private double weatherLon = 0;
        private bool weatherRequestInFlight = false;
        private DateTime lastWeatherFetchUtc = DateTime.MinValue;
        private static readonly TimeSpan WeatherMinRefreshInterval = TimeSpan.FromMinutes(5);

        // Countdown
        private bool showCountdown = false;
        private DateTime countdownTarget;
        private string countdownName = "";

        // System Badges & Alarm
        private bool showBadges = true;

        // Depth effect
        private bool useDepthEffect = false;
        private bool depthHourBehind = true;
        private bool depthColonBehind = true;
        private bool depthMinuteBehind = true;

        // Background filters
        private bool useMatte = false;
        private bool useRibbed = false;

        // Free positions (from Editor)
        private bool hasFreeLayout = false;
        private double clockFreeX, clockFreeY;
        private double weatherFreeX, weatherFreeY;
        private double countdownFreeX, countdownFreeY;

        // Signature
        private bool showSignature = false;
        private string sigText = "";
        private int sigFontIndex = 0;
        private double sigSpacing = 0;
        private int sigAlign = 1;
        private int sigLayout = 0; // 0=Horizontal, 1=Vertical
        private int sigColorIdx = 0;
        private int sigBlend = 0;
        private double sigOpacity = 1.0;
        private int sigHue = -1;
        private double signatureX = 0;
        private double signatureY = 0;

        // Quick Camera Viewfinder
        private Microsoft.Devices.PhotoCamera photoCamera;
        private Microsoft.Devices.CameraType currentCameraType = Microsoft.Devices.CameraType.Primary;
        private int currentFlashIndex = 0; // 0=Auto, 1=On, 2=Off
        private bool isCameraCapturing = false;

        // Font families now shared from ClockRenderer.Fonts

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
            ApplyFreePositions();
            ApplySignatureStyle();
            UpdateTime();

            // Cache battery reference once
            try { cachedBattery = Windows.Phone.Devices.Power.Battery.GetDefault(); } catch { }

            // Main clock timer (synchronized minute-aligned delay)
            StartClockTimer();

            // Hardware Start key hook via reflection
            StartButtonHelper.RegisterStartKey(OnStartKeyPressed);

            // Hook frame Obscured / Unobscured lifecycle for battery preservation
            var frame = Application.Current.RootVisual as PhoneApplicationFrame;
            if (frame != null)
            {
                frame.Obscured -= RootFrame_Obscured;
                frame.Obscured += RootFrame_Obscured;
                frame.Unobscured -= RootFrame_Unobscured;
                frame.Unobscured += RootFrame_Unobscured;
            }

            // Battery timer
            batteryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            batteryTimer.Tick += batteryTimer_Tick;
            batteryTimer.Start();
            UpdateBattery();
            UpdateLockScreenSnapshot();

            // Weather timer (every 30 min) — only start if weather is enabled
            weatherTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
            weatherTimer.Tick += (s, a) => FetchWeather();
            if (showWeather)
            {
                weatherTimer.Start();
                LoadCachedWeather(); // Show cached data immediately
                FetchWeather(true);  // Then refresh from API
            }

            // Load depth effect BEFORE animations so animation knows which parts to skip
            LoadForeground();
            ApplyDepthLayers();
            UpdateCountdown();

            // Flashlight state sync
            FlashlightHelper.StateChanged -= FlashlightHelper_StateChanged;
            FlashlightHelper.StateChanged += FlashlightHelper_StateChanged;
            UpdateFlashlightVisual(FlashlightHelper.IsOn);

            // Play animations on first load (must be after ApplyDepthLayers)
            PlayEntryAnimations();

            isFirstLoad = false;
        }

        protected override void OnNavigatedFrom(System.Windows.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            // AGENTS.md: Always set ImageBrush.ImageSource = null to free memory immediately
            BackgroundBrush.ImageSource = null;
            ForegroundBrush.ImageSource = null;
            backgroundLoaded = false; // Force reload next time
            
            if (msForeground != null)
                msForeground = null;
            
            // Stop timers to save battery while screen is off
            StopClockTimer();
            if (batteryTimer != null) batteryTimer.Stop();
            if (weatherTimer != null) weatherTimer.Stop();
            if (msAnimTimer != null) { msAnimTimer.Stop(); msAnimTimer = null; }

            StartButtonHelper.UnregisterStartKey(OnStartKeyPressed);
            FlashlightHelper.StateChanged -= FlashlightHelper_StateChanged;
            FlashlightHelper.TurnOff();
            UpdateFlashlightVisual(false);
            CloseCameraOverlay(true);
        }

        protected override void OnNavigatedTo(System.Windows.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (!isFirstLoad)
            {
                bool isBackNavigation =
                    e.NavigationMode == System.Windows.Navigation.NavigationMode.Back;

                // Only do a full reload when user navigates BACK from EditorPage/Settings.
                // Skip reload on OS resume (Reset) — the page is already in memory.
                if (isBackNavigation)
                {
                    // User changed settings, reload everything
                    LoadSettings();
                    backgroundLoaded = false;
                    LoadBackground();
                    ApplyClockStyle();
                    ApplyClockPosition();
                    ApplyClockHAlign();
                    ApplyClockColor();
                    lastTimeText = ""; // Force time refresh
                    UpdateTime();
                    UpdateBattery();
                    UpdateCountdown();
                    UpdateLockScreenSnapshot();
                    if (showWeather)
                    {
                        if (!weatherTimer.IsEnabled) weatherTimer.Start();
                        FetchWeather(true);
                    }
                    else
                    {
                        weatherTimer.Stop();
                        WeatherText.Visibility = Visibility.Collapsed;
                    }
                    LoadForeground();
                    ApplyDepthLayers();
                    ApplyFreePositions();
                    ApplySignatureStyle();

                    // Replay animations (user just came back from settings)
                    PlayEntryAnimations();
                }

                EnsureTimersRunning();
                if (!isBackNavigation)
                {
                    LoadBackground();
                    LoadForeground();
                    lastTimeText = "";
                    UpdateTime();
                    UpdateBattery();
                    UpdateCountdown();
                    UpdateLockScreenSnapshot();
                    if (showWeather)
                    {
                        LoadCachedWeather();
                        FetchWeather();
                    }
                }

                // Always reset swipe overlay & security grids on any resume
                var t = (CompositeTransform)OverlayInformationPanel.RenderTransform;
                t.TranslateY = 0;
                OverlayInformationPanel.Opacity = 1;
                var tb = (CompositeTransform)BehindForegroundGrid.RenderTransform;
                tb.TranslateY = 0;
                BehindForegroundGrid.Opacity = 1;

                FlashlightHelper.StateChanged -= FlashlightHelper_StateChanged;
                FlashlightHelper.StateChanged += FlashlightHelper_StateChanged;
                UpdateFlashlightVisual(FlashlightHelper.IsOn);
            }
        }

        private void EnsureTimersRunning()
        {
            if (timer == null || !timer.IsEnabled) StartClockTimer();
            if (batteryTimer != null && !batteryTimer.IsEnabled) batteryTimer.Start();

            if (weatherTimer != null)
            {
                if (showWeather)
                {
                    if (!weatherTimer.IsEnabled) weatherTimer.Start();
                }
                else if (weatherTimer.IsEnabled)
                {
                    weatherTimer.Stop();
                }
            }
        }

        private void StartClockTimer()
        {
            StopClockTimer();
            UpdateTime();

            DateTime now = DateTime.Now;
            int msUntilNextMinute = (60 - now.Second) * 1000 + (1000 - now.Millisecond);
            if (msUntilNextMinute <= 0) msUntilNextMinute = 1000;

            minuteSyncTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(msUntilNextMinute) };
            minuteSyncTimer.Tick += (s, e) =>
            {
                if (minuteSyncTimer != null)
                {
                    minuteSyncTimer.Stop();
                    minuteSyncTimer = null;
                }
                UpdateTime();

                timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
                timer.Tick += (st, et) => UpdateTime();
                timer.Start();
            };
            minuteSyncTimer.Start();
        }

        private void StopClockTimer()
        {
            if (minuteSyncTimer != null)
            {
                minuteSyncTimer.Stop();
                minuteSyncTimer = null;
            }
            if (timer != null)
            {
                timer.Stop();
                timer = null;
            }
        }

        private void RootFrame_Obscured(object sender, Microsoft.Phone.Controls.ObscuredEventArgs e)
        {
            StopClockTimer();
            if (batteryTimer != null) batteryTimer.Stop();
            if (weatherTimer != null) weatherTimer.Stop();
            FlashlightHelper.TurnOff();
            CloseCameraOverlay(true);

            try
            {
                if (currentPanelStory != null)
                {
                    currentPanelStory.Stop();
                    currentPanelStory = null;
                }
                if (currentWidgetsStory != null)
                {
                    currentWidgetsStory.Stop();
                    currentWidgetsStory = null;
                }
                var tp = (CompositeTransform)LockScreenPanel.RenderTransform;
                if (tp != null) tp.TranslateY = 0;
                var tw = (CompositeTransform)OverlayInformationPanel.RenderTransform;
                if (tw != null) tw.TranslateY = 0;
                var tb = (CompositeTransform)BehindForegroundGrid.RenderTransform;
                if (tb != null) tb.TranslateY = 0;
                OverlayInformationPanel.Opacity = 1;
                BehindForegroundGrid.Opacity = 1;

                ExtensibilityHelper.EndUnlock();
                isUnlockingStarted = false;
            }
            catch { }
        }

        private void RootFrame_Unobscured(object sender, EventArgs e)
        {
            StartClockTimer();
            if (batteryTimer != null) batteryTimer.Start();
            if (weatherTimer != null && showWeather) weatherTimer.Start();
            UpdateBattery();
            UpdateLockScreenSnapshot();
        }

        private void OnStartKeyPressed(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                AnimateBackToNormal(forced: true);
            });
        }

        private void PlayEntryAnimations()
        {
            if (bIsAnimOn)
            {
                try
                {
                    // Build clock animation in code to respect depth layers.
                    var sb = new Storyboard();

                    if (clockLayout >= 2 && clockLayout <= 4)
                    {
                        // Analog clock — animate the whole canvas
                        if (useDepthEffect)
                            AddFadeSlide(sb, AnalogClockCanvasBehind, 0, 450);
                        else
                            AddFadeSlide(sb, AnalogClockCanvas, 0, 450);
                    }
                    else if (clockLayout == 5)
                    {
                        // Rhombus layout
                        AddFadeSlide(sb, RhombusGrid, 0, 450);
                        if (useDepthEffect)
                            AddFadeSlide(sb, RhombusGridBehind, 0, 450);
                    }
                    else if (clockLayout == 7)
                    {
                        // Stacked layout
                        if (useDepthEffect)
                            AddFadeSlide(sb, StackedGridBehind, 0, 450);
                        else
                            AddFadeSlide(sb, StackedGrid, 0, 450);
                    }
                    else if (clockLayout == 8)
                    {
                        // Upward layout
                        if (useDepthEffect)
                            AddFadeSlide(sb, UpwardGridBehind, 0, 450);
                        else
                            AddFadeSlide(sb, UpwardGrid, 0, 450);
                    }
                    else
                    {
                        int delayMs = 0;

                        // Hour
                        if (useDepthEffect && depthHourBehind)
                            AddFadeSlide(sb, HourPartBehind, delayMs, 450);
                        else
                            AddFadeSlide(sb, HourPart, delayMs, 450);
                        delayMs += 100;

                        // Colon (skip for vertical and giant)
                        if (clockLayout != 1 && clockLayout != 6)
                        {
                            if (useDepthEffect && depthColonBehind)
                                AddFadeSlide(sb, ColonPartBehind, delayMs, 450);
                            else
                                AddFadeSlide(sb, ColonPart, delayMs, 450);
                            delayMs += 100;
                        }

                        // Minute
                        if (useDepthEffect && depthMinuteBehind)
                            AddFadeSlide(sb, MinutePartBehind, delayMs, 450);
                        else
                            AddFadeSlide(sb, MinutePart, delayMs, 450);
                    }

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

                // Redraw analog clock if active (only layouts 2, 3, 4)
                if (clockLayout >= 2 && clockLayout <= 4)
                {
                    var now = DateTime.Now;
                    double diameter = AnalogClockCanvas.Width;
                    DrawAnalogClock(AnalogClockCanvas, diameter, now.Hour, now.Minute, clockLayout);
                    DrawAnalogClock(AnalogClockCanvasBehind, diameter, now.Hour, now.Minute, clockLayout);
                }
                
                // Update Rhombus
                if (clockLayout == 5)
                {
                    RhombusH1.Text = h[0].ToString(); RhombusH1Behind.Text = h[0].ToString();
                    RhombusH2.Text = h.Length > 1 ? h[1].ToString() : ""; RhombusH2Behind.Text = RhombusH2.Text;
                    RhombusM1.Text = m[0].ToString(); RhombusM1Behind.Text = m[0].ToString();
                    RhombusM2.Text = m.Length > 1 ? m[1].ToString() : ""; RhombusM2Behind.Text = RhombusM2.Text;
                }
                
                // Update Stacked/Upward time
                if (clockLayout == 7)
                {
                    StackedTime.Text = newTime; StackedTimeBehind.Text = newTime;
                }
                else if (clockLayout == 8)
                {
                    UpwardTime.Text = newTime; UpwardTimeBehind.Text = newTime;
                }

                string newDay = DateTime.Now.ToString("dddd");
                string newDate = DateTime.Now.ToString("MMMM d");
                if (DayPanel.Text != newDay)
                {
                    DayPanel.Text = newDay;
                    DatePanel.Text = newDate;
                    UpdateCountdown(); // Refresh once per day
                }
                
                // Stacked layout date/day/carrier must be updated regardless of whether DayPanel changed,
                // to prevent initialization bugs if the day happens to match the XAML default (Saturday).
                if (clockLayout == 7 || clockLayout == 8)
                {
                    var now = DateTime.Now;
                    string stackDateStr = now.Day + "/" + now.Month;
                    string stackDayStr = now.ToString("ddd").ToUpper();
                    if (cachedCarrier == null || (DateTime.Now - lastCarrierCheck).TotalMinutes >= 15)
                    {
                        try
                        {
                            cachedCarrier = Microsoft.Phone.Net.NetworkInformation.DeviceNetworkInformation.CellularMobileOperator;
                        }
                        catch { }
                        if (string.IsNullOrWhiteSpace(cachedCarrier))
                            cachedCarrier = "No Service";
                        lastCarrierCheck = DateTime.Now;
                    }
                    string carrier = cachedCarrier;

                    if (clockLayout == 7)
                    {
                        if (StackedDate.Text != stackDateStr)
                        {
                            StackedDate.Text = stackDateStr;
                            StackedDateBehind.Text = stackDateStr;
                        }
                        if (StackedDay.Text != stackDayStr)
                        {
                            StackedDay.Text = stackDayStr;
                            StackedDayBehind.Text = stackDayStr;
                        }
                        if (StackedCarrier.Text != carrier)
                        {
                            StackedCarrier.Text = carrier;
                            StackedCarrierBehind.Text = carrier;
                        }
                    }
                    else
                    {
                        if (UpwardDate.Text != stackDateStr)
                        {
                            UpwardDate.Text = stackDateStr;
                            UpwardDateBehind.Text = stackDateStr;
                        }
                        if (UpwardDay.Text != stackDayStr)
                        {
                            UpwardDay.Text = stackDayStr;
                            UpwardDayBehind.Text = stackDayStr;
                        }
                    }
                }
            }
        }

        #endregion

        #region Swipe to Unlock

        private Storyboard currentPanelStory;
        private Storyboard currentWidgetsStory;

        private void AnimatePanelTo(double targetY, EventHandler completedHandler = null, double durationMs = 280)
        {
            if (currentPanelStory != null)
            {
                try { currentPanelStory.Stop(); } catch { }
                currentPanelStory = null;
            }

            var t = (CompositeTransform)LockScreenPanel.RenderTransform;
            if (t == null) return;

            if (!bIsAnimOn || durationMs <= 0)
            {
                t.TranslateY = targetY;
                if (completedHandler != null) completedHandler(this, EventArgs.Empty);
                return;
            }

            currentPanelStory = new Storyboard();
            var da = new DoubleAnimation
            {
                To = targetY,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = new CubicEase { EasingMode = (targetY < -400) ? EasingMode.EaseIn : EasingMode.EaseOut }
            };
            Storyboard.SetTarget(da, t);
            Storyboard.SetTargetProperty(da, new PropertyPath("TranslateY"));
            currentPanelStory.Children.Add(da);

            currentPanelStory.Completed += (s, e) =>
            {
                currentPanelStory = null;
                t.TranslateY = targetY;
                if (completedHandler != null)
                {
                    completedHandler(s, e);
                }
            };

            currentPanelStory.Begin();
        }

        private void AnimateWidgetsOpacity(double targetOpacity, double durationMs = 200)
        {
            if (!bIsAnimOn || durationMs <= 0)
            {
                OverlayInformationPanel.Opacity = targetOpacity;
                BehindForegroundGrid.Opacity = targetOpacity;
                return;
            }

            var sb = new Storyboard();
            var animOp = new DoubleAnimation
            {
                To = targetOpacity,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(animOp, OverlayInformationPanel);
            Storyboard.SetTargetProperty(animOp, new PropertyPath("Opacity"));

            var animOpb = new DoubleAnimation
            {
                To = targetOpacity,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(animOpb, BehindForegroundGrid);
            Storyboard.SetTargetProperty(animOpb, new PropertyPath("Opacity"));

            sb.Children.Add(animOp);
            sb.Children.Add(animOpb);
            sb.Begin();
        }

        private void SnapWidgetsBack()
        {
            ExtensibilityHelper.EndUnlock();
            isUnlockingStarted = false;

            if (currentWidgetsStory != null)
            {
                try { currentWidgetsStory.Stop(); } catch { }
                currentWidgetsStory = null;
            }

            var t = (CompositeTransform)OverlayInformationPanel.RenderTransform;
            var tb = (CompositeTransform)BehindForegroundGrid.RenderTransform;
            if (t == null) return;

            if (!bIsAnimOn)
            {
                t.TranslateY = 0;
                if (tb != null) tb.TranslateY = 0;
                OverlayInformationPanel.Opacity = 1;
                BehindForegroundGrid.Opacity = 1;
                return;
            }

            currentWidgetsStory = new Storyboard();

            var animY = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(animY, t);
            Storyboard.SetTargetProperty(animY, new PropertyPath("TranslateY"));

            var animOp = new DoubleAnimation
            {
                To = 1,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(animOp, OverlayInformationPanel);
            Storyboard.SetTargetProperty(animOp, new PropertyPath("Opacity"));

            currentWidgetsStory.Children.Add(animY);
            currentWidgetsStory.Children.Add(animOp);

            if (tb != null)
            {
                var animYb = new DoubleAnimation
                {
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(250),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(animYb, tb);
                Storyboard.SetTargetProperty(animYb, new PropertyPath("TranslateY"));

                var animOpb = new DoubleAnimation
                {
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(250),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(animOpb, BehindForegroundGrid);
                Storyboard.SetTargetProperty(animOpb, new PropertyPath("Opacity"));

                currentWidgetsStory.Children.Add(animYb);
                currentWidgetsStory.Children.Add(animOpb);
            }

            currentWidgetsStory.Completed += (s, e) =>
            {
                currentWidgetsStory = null;
                t.TranslateY = 0;
                if (tb != null) tb.TranslateY = 0;
                OverlayInformationPanel.Opacity = 1;
                BehindForegroundGrid.Opacity = 1;
            };

            currentWidgetsStory.Begin();
        }

        private void AnimateBackToNormal(bool forced = false)
        {
            var panelTransform = (CompositeTransform)LockScreenPanel.RenderTransform;
            if (panelTransform == null) return;
            if (!forced && panelTransform.TranslateY == 0) return;

            ExtensibilityHelper.EndUnlock();
            isUnlockingStarted = false;

            if (currentWidgetsStory != null)
            {
                try { currentWidgetsStory.Stop(); } catch { }
                currentWidgetsStory = null;
            }

            var t = (CompositeTransform)OverlayInformationPanel.RenderTransform;
            var tb = (CompositeTransform)BehindForegroundGrid.RenderTransform;
            if (t != null) t.TranslateY = 0;
            if (tb != null) tb.TranslateY = 0;
            OverlayInformationPanel.Opacity = 1;
            BehindForegroundGrid.Opacity = 1;

            if (panelTransform.TranslateY != 0)
            {
                AnimatePanelTo(0, null, 260);
            }
        }

        private void LockScreenPanel_ManipulationStarted(
            object sender, ManipulationStartedEventArgs e)
        {
            if (MySetsOverlay.Visibility == Visibility.Visible) return;

            if (currentPanelStory != null)
            {
                try { currentPanelStory.Stop(); } catch { }
                currentPanelStory = null;
            }
            if (currentWidgetsStory != null)
            {
                try { currentWidgetsStory.Stop(); } catch { }
                currentWidgetsStory = null;
            }

            var panelTransform = (CompositeTransform)LockScreenPanel.RenderTransform;
            bool isPanelSlidUp = (panelTransform != null && panelTransform.TranslateY < -20);

            if (!isPanelSlidUp && !isUnlockingStarted)
            {
                isUnlockingStarted = true;
                ExtensibilityHelper.BeginUnlock();
            }
        }

        private void LockScreenPanel_ManipulationDelta(
            object sender, ManipulationDeltaEventArgs e)
        {
            if (MySetsOverlay.Visibility == Visibility.Visible) return;

            double pinpadHeight = ExtensibilityHelper.GetPinpadHeightDips();
            var panelTransform = (CompositeTransform)LockScreenPanel.RenderTransform;
            bool isPanelSlidUp = (panelTransform != null && panelTransform.TranslateY < -20);

            // If PIN pad is already open, dragging down moves the wallpaper panel back towards 0
            if (isPanelSlidUp)
            {
                double newPanelY = panelTransform.TranslateY + e.DeltaManipulation.Translation.Y;
                if (newPanelY > 0) newPanelY = 0;
                if (newPanelY < -pinpadHeight) newPanelY = -pinpadHeight;
                panelTransform.TranslateY = newPanelY;
                return;
            }

            // Normal state: wallpaper stays static! Only widgets slide up and fade!
            if (!isUnlockingStarted)
            {
                isUnlockingStarted = true;
                ExtensibilityHelper.BeginUnlock();
            }

            var t = (CompositeTransform)OverlayInformationPanel.RenderTransform;
            var tb = (CompositeTransform)BehindForegroundGrid.RenderTransform;

            double newY = t.TranslateY + e.DeltaManipulation.Translation.Y;
            if (newY > 0) newY = 0;

            t.TranslateY = newY;
            if (tb != null) tb.TranslateY = newY;

            double opacity = Math.Max(0, 1.0 + newY / 400.0);
            OverlayInformationPanel.Opacity = opacity;
            if (BehindForegroundGrid != null) BehindForegroundGrid.Opacity = opacity;
        }

        private void LockScreenPanel_ManipulationCompleted(
            object sender, ManipulationCompletedEventArgs e)
        {
            if (MySetsOverlay.Visibility == Visibility.Visible) return;

            double pinpadHeight = ExtensibilityHelper.GetPinpadHeightDips();
            bool isPinLocked = pinpadHeight > 0;
            var panelTransform = (CompositeTransform)LockScreenPanel.RenderTransform;
            bool isPanelSlidUp = (panelTransform != null && panelTransform.TranslateY < -20);

            // If PIN pad was already open and user was dragging it
            if (isPanelSlidUp)
            {
                bool isFlickDown = e.FinalVelocities.LinearVelocity.Y > 1000.0;
                bool isFlickUp = e.FinalVelocities.LinearVelocity.Y < -1000.0;
                bool shouldClosePin = (panelTransform.TranslateY > -pinpadHeight * 0.6 || isFlickDown) && !isFlickUp;

                if (shouldClosePin)
                {
                    AnimateBackToNormal();
                }
                else
                {
                    AnimatePanelTo(-pinpadHeight, null, 200);
                }
                return;
            }

            // Normal state: user just released their finger after swiping widgets up
            var t = (CompositeTransform)OverlayInformationPanel.RenderTransform;
            double currentY = t.TranslateY;
            bool flickUp = e.FinalVelocities.LinearVelocity.Y < -1200.0;
            bool flickDown = e.FinalVelocities.LinearVelocity.Y > 1200.0;

            bool shouldProceed = (currentY <= -180.0 || flickUp) && !flickDown;

            if (shouldProceed)
            {
                // USER FINISHED SWIPE: NOW AND ONLY NOW does the wallpaper slide up!
                if (isPinLocked)
                {
                    // Animate wallpaper up to reveal the native PIN pad underneath!
                    AnimatePanelTo(-pinpadHeight, null, 280);
                    AnimateWidgetsOpacity(0, 200);
                }
                else
                {
                    // No PIN: animate wallpaper all the way off screen and unlock
                    AnimatePanelTo(-800.0, (s, args) => DoActualUnlock(), 300);
                    AnimateWidgetsOpacity(0, 200);
                }
            }
            else
            {
                // Swipe cancelled / not far enough: snap widgets back down, wallpaper stays at 0
                SnapWidgetsBack();
            }
        }

        #endregion

        #region Unlock Methods

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

        private async void FlashlightShortcut_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            try { Microsoft.Devices.VibrateController.Default.Start(TimeSpan.FromMilliseconds(25)); } catch { }
            await FlashlightHelper.ToggleAsync();
        }

        private void FlashlightHelper_StateChanged(bool isOn)
        {
            Dispatcher.BeginInvoke(() => UpdateFlashlightVisual(isOn));
        }

        private void UpdateFlashlightVisual(bool isOn)
        {
            try
            {
                if (FlashlightButton == null) return;
                if (isOn)
                {
                    FlashlightButton.Background = new SolidColorBrush(Colors.White);
                    if (FlashlightHead != null)
                    {
                        FlashlightHead.Stroke = new SolidColorBrush(Colors.Black);
                        FlashlightHead.Fill = new SolidColorBrush(Colors.Black);
                    }
                    if (FlashlightBody != null)
                    {
                        FlashlightBody.Stroke = new SolidColorBrush(Colors.Black);
                        FlashlightBody.Fill = new SolidColorBrush(Colors.Black);
                    }
                    if (FlashlightBeam != null)
                    {
                        FlashlightBeam.Fill = new SolidColorBrush(Colors.White);
                    }
                }
                else
                {
                    FlashlightButton.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(34, 255, 255, 255));
                    if (FlashlightHead != null)
                    {
                        FlashlightHead.Stroke = new SolidColorBrush(Colors.White);
                        FlashlightHead.Fill = new SolidColorBrush(Colors.Transparent);
                    }
                    if (FlashlightBody != null)
                    {
                        FlashlightBody.Stroke = new SolidColorBrush(Colors.White);
                        FlashlightBody.Fill = new SolidColorBrush(Colors.Transparent);
                    }
                    if (FlashlightBeam != null)
                    {
                        FlashlightBeam.Fill = new SolidColorBrush(Colors.White);
                    }
                }
            }
            catch { }
        }

        private void CameraShortcut_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            try { Microsoft.Devices.VibrateController.Default.Start(TimeSpan.FromMilliseconds(25)); } catch { }
            OpenQuickCamera();
        }

        private void OpenQuickCamera()
        {
            if (CameraOverlay == null || CameraOverlay.Visibility == Visibility.Visible) return;

            FlashlightHelper.TurnOff();
            CameraOverlay.Visibility = Visibility.Visible;
            CameraOverlayTransform.TranslateX = 480;

            var sb = new Storyboard();
            var anim = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(anim, CameraOverlayTransform);
            Storyboard.SetTargetProperty(anim, new PropertyPath("TranslateX"));
            sb.Children.Add(anim);
            sb.Begin();

            InitializeCamera();
        }

        private void InitializeCamera()
        {
            try
            {
                DisposeCamera();
                photoCamera = new Microsoft.Devices.PhotoCamera(currentCameraType);
                photoCamera.CaptureImageAvailable += PhotoCamera_CaptureImageAvailable;
                photoCamera.CaptureThumbnailAvailable += PhotoCamera_CaptureThumbnailAvailable;
                photoCamera.Initialized += PhotoCamera_Initialized;
                CameraVideoBrush.SetSource(photoCamera);
            }
            catch { }
        }

        private void PhotoCamera_Initialized(object sender, Microsoft.Devices.CameraOperationCompletedEventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (photoCamera != null)
                {
                    try
                    {
                        if (CameraViewfinderTransform != null)
                        {
                            CameraViewfinderTransform.Rotation = photoCamera.Orientation;
                        }
                        ApplyFlashMode();
                    }
                    catch { }
                }
            });
        }

        private void PhotoCamera_CaptureImageAvailable(object sender, Microsoft.Devices.ContentReadyEventArgs e)
        {
            try
            {
                using (var ml = new Microsoft.Xna.Framework.Media.MediaLibrary())
                {
                    string fileName = string.Format("HyperOS_{0:yyyyMMdd_HHmmss}.jpg", DateTime.Now);
                    ml.SavePictureToCameraRoll(fileName, e.ImageStream);
                }
            }
            catch { }
            finally
            {
                isCameraCapturing = false;
            }
        }

        private void PhotoCamera_CaptureThumbnailAvailable(object sender, Microsoft.Devices.ContentReadyEventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    var bmp = new System.Windows.Media.Imaging.BitmapImage();
                    bmp.SetSource(e.ImageStream);
                    CameraThumbnailImage.Source = bmp;
                }
                catch { }
            });
        }

        private void CameraShutter_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            if (photoCamera == null || isCameraCapturing) return;
            isCameraCapturing = true;

            try { Microsoft.Devices.VibrateController.Default.Start(TimeSpan.FromMilliseconds(40)); } catch { }
            PlayCameraFlashEffect();

            try
            {
                photoCamera.CaptureImage();
            }
            catch
            {
                isCameraCapturing = false;
            }
        }

        private void PlayCameraFlashEffect()
        {
            try
            {
                CameraFlashEffect.Opacity = 0.85;
                var sb = new Storyboard();
                var anim = new DoubleAnimation
                {
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(200)
                };
                Storyboard.SetTarget(anim, CameraFlashEffect);
                Storyboard.SetTargetProperty(anim, new PropertyPath("Opacity"));
                sb.Children.Add(anim);
                sb.Begin();
            }
            catch { }
        }

        private void CameraViewfinder_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            if (photoCamera != null && photoCamera.IsFocusSupported)
            {
                try
                {
                    var pos = e.GetPosition(CameraOverlay);
                    Canvas.SetLeft(CameraFocusRing, pos.X - 30);
                    Canvas.SetTop(CameraFocusRing, pos.Y - 30);
                    CameraFocusRing.Visibility = Visibility.Visible;
                    photoCamera.Focus();
                }
                catch { }
            }
        }

        private void CameraSwitch_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            if (Microsoft.Devices.PhotoCamera.IsCameraTypeSupported(Microsoft.Devices.CameraType.FrontFacing))
            {
                currentCameraType = (currentCameraType == Microsoft.Devices.CameraType.Primary)
                    ? Microsoft.Devices.CameraType.FrontFacing
                    : Microsoft.Devices.CameraType.Primary;
                InitializeCamera();
            }
        }

        private void CameraFlash_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            currentFlashIndex = (currentFlashIndex + 1) % 3;
            ApplyFlashMode();
        }

        private void ApplyFlashMode()
        {
            if (photoCamera == null) return;
            try
            {
                switch (currentFlashIndex)
                {
                    case 0:
                        if (photoCamera.IsFlashModeSupported(Microsoft.Devices.FlashMode.Auto))
                            photoCamera.FlashMode = Microsoft.Devices.FlashMode.Auto;
                        CameraFlashIcon.Text = "⚡A";
                        break;
                    case 1:
                        if (photoCamera.IsFlashModeSupported(Microsoft.Devices.FlashMode.On))
                            photoCamera.FlashMode = Microsoft.Devices.FlashMode.On;
                        CameraFlashIcon.Text = "⚡ON";
                        break;
                    case 2:
                        if (photoCamera.IsFlashModeSupported(Microsoft.Devices.FlashMode.Off))
                            photoCamera.FlashMode = Microsoft.Devices.FlashMode.Off;
                        CameraFlashIcon.Text = "⚡OFF";
                        break;
                }
            }
            catch { }
        }

        private void CameraClose_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            CloseCameraOverlay();
        }

        private void CloseCameraOverlay(bool immediate = false)
        {
            if (CameraOverlay == null || CameraOverlay.Visibility == Visibility.Collapsed) return;

            if (immediate)
            {
                CameraOverlay.Visibility = Visibility.Collapsed;
                DisposeCamera();
                return;
            }

            var sb = new Storyboard();
            var anim = new DoubleAnimation
            {
                To = 480,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CircleEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(anim, CameraOverlayTransform);
            Storyboard.SetTargetProperty(anim, new PropertyPath("TranslateX"));
            sb.Children.Add(anim);
            sb.Completed += (s, e) =>
            {
                CameraOverlay.Visibility = Visibility.Collapsed;
                DisposeCamera();
            };
            sb.Begin();
        }

        private void DisposeCamera()
        {
            if (photoCamera != null)
            {
                try
                {
                    photoCamera.CaptureImageAvailable -= PhotoCamera_CaptureImageAvailable;
                    photoCamera.CaptureThumbnailAvailable -= PhotoCamera_CaptureThumbnailAvailable;
                    photoCamera.Initialized -= PhotoCamera_Initialized;
                    photoCamera.Dispose();
                }
                catch { }
                photoCamera = null;
            }
            if (CameraFocusRing != null) CameraFocusRing.Visibility = Visibility.Collapsed;
        }

        #endregion

        #region Battery

        private void batteryTimer_Tick(object sender, EventArgs e)
        {
            UpdateBattery();
            UpdateLockScreenSnapshot();
        }

        private void UpdateBattery()
        {
            if (TopStatusBarPanel == null || TopStatusBarPanel.Visibility != Visibility.Visible)
                return;

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

        #region LockScreen Bridge (Badges & Alarm)

        private void UpdateLockScreenSnapshot()
        {
            if (!showBadges)
            {
                AlarmStatusIcon.Visibility = Visibility.Collapsed;
                AlarmDateIcon.Visibility = Visibility.Collapsed;
                NotificationContainer.Visibility = Visibility.Collapsed;
                lastBadgesSignature = null;
                return;
            }

            try
            {
                var snapshot = LockScreenBridgeHelper.GetSnapshot();
                if (snapshot == null) return;

                // Build signature to avoid rebuilding visual tree if unchanged
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.Append(snapshot.HasAlarm).Append('|').Append(snapshot.DetailedNotificationText).Append('|');
                if (snapshot.Badges != null)
                {
                    sb.Append(snapshot.Badges.Count);
                    for (int i = 0; i < snapshot.Badges.Count; i++)
                    {
                        var b = snapshot.Badges[i];
                        if (b != null)
                        {
                            sb.Append(':').Append(b.Counter);
                        }
                    }
                }
                string sig = sb.ToString();
                if (sig == lastBadgesSignature)
                    return;
                lastBadgesSignature = sig;

                // 1. Alarm indicators
                if (snapshot.HasAlarm)
                {
                    AlarmStatusIcon.Visibility = Visibility.Visible;
                    AlarmDateIcon.Visibility = Visibility.Visible;
                }
                else
                {
                    AlarmStatusIcon.Visibility = Visibility.Collapsed;
                    AlarmDateIcon.Visibility = Visibility.Collapsed;
                }

                // 2. Detailed notification card
                bool hasDetailedText = !string.IsNullOrEmpty(snapshot.DetailedNotificationText);
                if (hasDetailedText)
                {
                    DetailedNotificationText.Text = snapshot.DetailedNotificationText;
                    DetailedNotificationCard.Visibility = Visibility.Visible;
                }
                else
                {
                    DetailedNotificationCard.Visibility = Visibility.Collapsed;
                }

                // 3. Quick status badges
                BadgesPanel.Children.Clear();
                if (snapshot.HasBadges)
                {
                    foreach (var badge in snapshot.Badges)
                    {
                        if (badge == null || badge.Icon == null) continue;

                        var badgeItemPanel = new StackPanel
                        {
                            Orientation = System.Windows.Controls.Orientation.Horizontal,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(5, 0, 5, 0)
                        };

                        var img = new Image
                        {
                            Width = 22,
                            Height = 22,
                            Stretch = Stretch.Uniform,
                            VerticalAlignment = VerticalAlignment.Center,
                            Source = badge.Icon
                        };
                        badgeItemPanel.Children.Add(img);

                        if (badge.HasCounter)
                        {
                            var countBorder = new Border
                            {
                                Background = BadgeCounterBgBrush,
                                CornerRadius = new CornerRadius(7),
                                Padding = new Thickness(4, 0, 4, 1),
                                Margin = new Thickness(4, 0, 0, 0),
                                VerticalAlignment = VerticalAlignment.Center
                            };
                            var countText = new TextBlock
                            {
                                Text = badge.Counter,
                                FontFamily = ClockRenderer.GetFont(1), // MiSans Demibold
                                FontSize = 11,
                                Foreground = WhiteBrush,
                                VerticalAlignment = VerticalAlignment.Center
                            };
                            countBorder.Child = countText;
                            badgeItemPanel.Children.Add(countBorder);
                        }

                        BadgesPanel.Children.Add(badgeItemPanel);
                    }
                    BadgesTray.Visibility = Visibility.Visible;
                }
                else
                {
                    BadgesTray.Visibility = Visibility.Collapsed;
                }

                // Show container if either detailed text or badges are visible
                if (hasDetailedText || snapshot.HasBadges)
                {
                    NotificationContainer.Visibility = Visibility.Visible;
                }
                else
                {
                    NotificationContainer.Visibility = Visibility.Collapsed;
                }
            }
            catch
            {
            }
        }

        #endregion

        #region Settings

        private void LoadSettings()
        {
            var s = IsolatedStorageSettings.ApplicationSettings;

            if (s.Contains("bIsAnimOn"))
                bIsAnimOn = (bool)s["bIsAnimOn"];
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
            if (s.Contains("ClockLayout"))
                clockLayout = (int)s["ClockLayout"];
            clockOpacity = s.Contains("ClockOpacity") ? (double)s["ClockOpacity"] : 1.0;
            clockHue = s.Contains("ClockHue") ? (int)s["ClockHue"] : -1;

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
            else
            {
                OwnerInfoText.Visibility = Visibility.Collapsed;
            }

            // Weather
            showWeather = s.Contains("ShowWeather") ? (bool)s["ShowWeather"] : false;
            if (s.Contains("WeatherLat"))
                weatherLat = (double)s["WeatherLat"];
            if (s.Contains("WeatherLon"))
                weatherLon = (double)s["WeatherLon"];

            // Countdown
            showCountdown = s.Contains("ShowCountdown") ? (bool)s["ShowCountdown"] : false;

            // Background filters
            useMatte = s.Contains("UseMatte") ? (bool)s["UseMatte"] : false;
            useRibbed = s.Contains("UseRibbed") ? (bool)s["UseRibbed"] : false;
            if (s.Contains("CountdownTarget"))
                countdownTarget = (DateTime)s["CountdownTarget"];
            if (s.Contains("CountdownName"))
                countdownName = (string)s["CountdownName"];

            // System Badges & Alarm
            showBadges = s.Contains("ShowBadges") ? (bool)s["ShowBadges"] : true;


            // Depth effect
            useDepthEffect = s.Contains("UseDepthEffect") ? (bool)s["UseDepthEffect"] : false;
            depthHourBehind = s.Contains("DepthHourBehind") ? (bool)s["DepthHourBehind"] : true;
            depthColonBehind = s.Contains("DepthColonBehind") ? (bool)s["DepthColonBehind"] : true;
            depthMinuteBehind = s.Contains("DepthMinuteBehind") ? (bool)s["DepthMinuteBehind"] : true;

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

            // Signature
            showSignature = s.Contains("ShowSignature") ? (bool)s["ShowSignature"] : false;
            sigText = s.Contains("SignatureText") ? (string)s["SignatureText"] : "";
            sigFontIndex = s.Contains("SignatureFont") ? (int)s["SignatureFont"] : 0;
            sigSpacing = s.Contains("SignatureSpacing") ? (double)s["SignatureSpacing"] : 0;
            sigAlign = s.Contains("SignatureAlign") ? (int)s["SignatureAlign"] : 1;
            sigLayout = s.Contains("SignatureLayout") ? (int)s["SignatureLayout"] : 0;
            sigColorIdx = s.Contains("SignatureColor") ? (int)s["SignatureColor"] : 0;
            sigBlend = s.Contains("SignatureBlend") ? (int)s["SignatureBlend"] : 0;
            sigOpacity = s.Contains("SignatureOpacity") ? (double)s["SignatureOpacity"] : 1.0;
            sigHue = s.Contains("SignatureHue") ? (int)s["SignatureHue"] : -1;
            signatureX = s.Contains("SignatureX") ? (double)s["SignatureX"] : 30;
            signatureY = s.Contains("SignatureY") ? (double)s["SignatureY"] : 650;
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
            // Font
            var ff = ClockRenderer.GetFont(clockStyle);
            SetClockFont(ff);
            RhombusH1.FontFamily = ff; RhombusH2.FontFamily = ff;
            RhombusM1.FontFamily = ff; RhombusM2.FontFamily = ff;
            RhombusH1Behind.FontFamily = ff; RhombusH2Behind.FontFamily = ff;
            RhombusM1Behind.FontFamily = ff; RhombusM2Behind.FontFamily = ff;
            StackedTime.FontFamily = ff; StackedTimeBehind.FontFamily = ff;
            StackedDate.FontFamily = ff; StackedDateBehind.FontFamily = ff;
            StackedWeather.FontFamily = ff; StackedWeatherBehind.FontFamily = ff;

            bool isAnalog = clockLayout >= 2 && clockLayout <= 4;
            bool isVertical = clockLayout == 1;
            bool isRhombus = clockLayout == 5;
            bool isGiant = clockLayout == 6;
            bool isStacked = clockLayout == 7;
            bool isUpward = clockLayout == 8;
            
            UpwardTime.FontFamily = ff; UpwardTimeBehind.FontFamily = ff;
            UpwardDate.FontFamily = ff; UpwardDateBehind.FontFamily = ff;
            UpwardDay.FontFamily = ff; UpwardDayBehind.FontFamily = ff;

            // Show/hide digital vs analog vs rhombus vs stacked vs upward
            if (isAnalog)
            {
                TimePanel.Visibility = Visibility.Collapsed;
                BehindTimePanel.Visibility = Visibility.Collapsed;
                RhombusGrid.Visibility = Visibility.Collapsed;
                RhombusGridBehind.Visibility = Visibility.Collapsed;
                StackedGrid.Visibility = Visibility.Collapsed;
                StackedGridBehind.Visibility = Visibility.Collapsed;
                UpwardGrid.Visibility = Visibility.Collapsed;
                UpwardGridBehind.Visibility = Visibility.Collapsed;
                AnalogClockCanvas.Visibility = Visibility.Visible;
                // AnalogClockCanvasBehind is managed by ApplyDepthLayers
            }
            else if (isRhombus)
            {
                TimePanel.Visibility = Visibility.Collapsed;
                BehindTimePanel.Visibility = Visibility.Collapsed;
                AnalogClockCanvas.Visibility = Visibility.Collapsed;
                AnalogClockCanvasBehind.Visibility = Visibility.Collapsed;
                StackedGrid.Visibility = Visibility.Collapsed;
                StackedGridBehind.Visibility = Visibility.Collapsed;
                UpwardGrid.Visibility = Visibility.Collapsed;
                UpwardGridBehind.Visibility = Visibility.Collapsed;
                RhombusGrid.Visibility = Visibility.Visible;
                RhombusGridBehind.Visibility = Visibility.Visible;
            }
            else if (isStacked)
            {
                TimePanel.Visibility = Visibility.Collapsed;
                BehindTimePanel.Visibility = Visibility.Collapsed;
                AnalogClockCanvas.Visibility = Visibility.Collapsed;
                AnalogClockCanvasBehind.Visibility = Visibility.Collapsed;
                RhombusGrid.Visibility = Visibility.Collapsed;
                RhombusGridBehind.Visibility = Visibility.Collapsed;
                UpwardGrid.Visibility = Visibility.Collapsed;
                UpwardGridBehind.Visibility = Visibility.Collapsed;
                StackedGrid.Visibility = Visibility.Visible;
                // StackedGridBehind is managed by ApplyDepthLayers
            }
            else if (isUpward)
            {
                TimePanel.Visibility = Visibility.Collapsed;
                BehindTimePanel.Visibility = Visibility.Collapsed;
                AnalogClockCanvas.Visibility = Visibility.Collapsed;
                AnalogClockCanvasBehind.Visibility = Visibility.Collapsed;
                RhombusGrid.Visibility = Visibility.Collapsed;
                RhombusGridBehind.Visibility = Visibility.Collapsed;
                StackedGrid.Visibility = Visibility.Collapsed;
                StackedGridBehind.Visibility = Visibility.Collapsed;
                UpwardGrid.Visibility = Visibility.Visible;
                // UpwardGridBehind is managed by ApplyDepthLayers
            }
            else
            {
                TimePanel.Visibility = Visibility.Visible;
                BehindTimePanel.Visibility = Visibility.Visible;
                RhombusGrid.Visibility = Visibility.Collapsed;
                RhombusGridBehind.Visibility = Visibility.Collapsed;
                StackedGrid.Visibility = Visibility.Collapsed;
                StackedGridBehind.Visibility = Visibility.Collapsed;
                UpwardGrid.Visibility = Visibility.Collapsed;
                UpwardGridBehind.Visibility = Visibility.Collapsed;
                AnalogClockCanvas.Visibility = Visibility.Collapsed;
                AnalogClockCanvasBehind.Visibility = Visibility.Collapsed;
            }

            // Vertical / Giant: stack vertically, hide colon
            ColonPart.Visibility = (isVertical || isGiant) ? Visibility.Collapsed : Visibility.Visible;
            ColonPartBehind.Visibility = (isVertical || isGiant) ? Visibility.Collapsed : Visibility.Visible;
            
            // Date info panel hidden if stacked/upward (it has its own)
            DateInfoPanel.Visibility = (isStacked || isUpward) ? Visibility.Collapsed : Visibility.Visible;

            if (isVertical)
            {
                TimePanel.Orientation = System.Windows.Controls.Orientation.Vertical;
                BehindTimePanel.Orientation = System.Windows.Controls.Orientation.Vertical;
                HourPart.HorizontalAlignment = HorizontalAlignment.Center;
                MinutePart.HorizontalAlignment = HorizontalAlignment.Center;
                HourPartBehind.HorizontalAlignment = HorizontalAlignment.Center;
                MinutePartBehind.HorizontalAlignment = HorizontalAlignment.Center;
            }
            else
            {
                TimePanel.Orientation = System.Windows.Controls.Orientation.Horizontal;
                BehindTimePanel.Orientation = System.Windows.Controls.Orientation.Horizontal;
                HourPart.HorizontalAlignment = HorizontalAlignment.Left;
                MinutePart.HorizontalAlignment = HorizontalAlignment.Left;
                HourPartBehind.HorizontalAlignment = HorizontalAlignment.Left;
                MinutePartBehind.HorizontalAlignment = HorizontalAlignment.Left;
            }

            // Draw analog clock if needed
            if (isAnalog)
            {
                var now = DateTime.Now;
                int sz = SizeValues[Math.Max(0, Math.Min(clockSize, SizeValues.Length - 1))];
                double diameter = sz * 1.6;
                AnalogClockCanvas.Width = diameter;
                AnalogClockCanvas.Height = diameter;
                AnalogClockCanvasBehind.Width = diameter;
                AnalogClockCanvasBehind.Height = diameter;
                AnalogClockCanvas.Children.Clear();
                AnalogClockCanvasBehind.Children.Clear();
                DrawAnalogClock(AnalogClockCanvas, diameter, now.Hour, now.Minute, clockLayout);
                DrawAnalogClock(AnalogClockCanvasBehind, diameter, now.Hour, now.Minute, clockLayout);
            }

            // Apply size
            int sizeIndex = Math.Max(0, Math.Min(clockSize, SizeValues.Length - 1));
            int baseSize = SizeValues[sizeIndex];

            double hourSz = baseSize;
            double minuteSz = baseSize;
            if (isGiant) { hourSz = baseSize * 1.6; minuteSz = baseSize * 1.6; }
            
            HourPart.FontSize = hourSz;
            HourPartBehind.FontSize = hourSz;
            ColonPart.FontSize = baseSize;
            ColonPartBehind.FontSize = baseSize;
            MinutePart.FontSize = minuteSz;
            MinutePartBehind.FontSize = minuteSz;
            
            double rhombSz = baseSize * 1.2;
            RhombusH1.FontSize = rhombSz; RhombusH2.FontSize = rhombSz;
            RhombusM1.FontSize = rhombSz; RhombusM2.FontSize = rhombSz;
            RhombusH1Behind.FontSize = rhombSz; RhombusH2Behind.FontSize = rhombSz;
            RhombusM1Behind.FontSize = rhombSz; RhombusM2Behind.FontSize = rhombSz;

            if (isVertical)
            {
                TimePanel.Margin = new Thickness(0, -baseSize * 0.22, 0, 0);
                BehindTimePanel.Margin = new Thickness(0, -baseSize * 0.22, 0, 0);
                MinutePart.Margin = new Thickness(0, -baseSize * 0.35, 0, 0);
                MinutePartBehind.Margin = new Thickness(0, -baseSize * 0.35, 0, 0);
            }
            else if (isGiant)
            {
                TimePanel.Margin = new Thickness(0, -baseSize * 0.25, 0, 0);
                BehindTimePanel.Margin = new Thickness(0, -baseSize * 0.25, 0, 0);
                MinutePart.Margin = new Thickness(baseSize * 0.1, 0, 0, 0);
                MinutePartBehind.Margin = new Thickness(baseSize * 0.1, 0, 0, 0);
            }
            else
            {
                TimePanel.Margin = new Thickness(0, -baseSize * 0.16, 0, 0);
                BehindTimePanel.Margin = new Thickness(0, -baseSize * 0.16, 0, 0);
                MinutePart.Margin = new Thickness(0, 0, 0, 0);
                MinutePartBehind.Margin = new Thickness(0, 0, 0, 0);
            }
            
            if (isRhombus)
            {
                RhombusGrid.Margin = new Thickness(0, -rhombSz * 0.15, 0, 0);
                RhombusGridBehind.Margin = new Thickness(0, -rhombSz * 0.15, 0, 0);
                
                var h1M = new Thickness(0, 0, 0, -rhombSz * 0.1);
                var h2M = new Thickness(0, 0, -rhombSz * 0.05, 0);
                var m1M = new Thickness(-rhombSz * 0.05, 0, 0, 0);
                var m2M = new Thickness(0, -rhombSz * 0.1, 0, 0);
                
                RhombusH1.Margin = h1M; RhombusH1Behind.Margin = h1M;
                RhombusH2.Margin = h2M; RhombusH2Behind.Margin = h2M;
                RhombusM1.Margin = m1M; RhombusM1Behind.Margin = m1M;
                RhombusM2.Margin = m2M; RhombusM2Behind.Margin = m2M;
            }
            
            if (isStacked)
            {
                double stackSz = baseSize;
                StackedTime.FontSize = stackSz * 0.72; StackedTimeBehind.FontSize = stackSz * 0.72;
                StackedDate.FontSize = stackSz * 0.72; StackedDateBehind.FontSize = stackSz * 0.72;
                StackedWeather.FontSize = stackSz * 0.72; StackedWeatherBehind.FontSize = stackSz * 0.72;
                StackedCarrier.FontSize = stackSz * 0.20; StackedCarrierBehind.FontSize = stackSz * 0.20;
                StackedDay.FontSize = stackSz * 0.22; StackedDayBehind.FontSize = stackSz * 0.22;
                
                var topMargin = new Thickness(0, (int)(-stackSz * 0.20), 0, 0);
                var timeMargin = new Thickness((int)(-stackSz * 0.04), 0, 0, 0);
                StackedTime.Margin = timeMargin; 
                StackedTimeBehind.Margin = timeMargin;
                
                StackedDatePanel.Margin = topMargin;
                StackedDatePanelBehind.Margin = topMargin;
                
                var dayMargin = new Thickness((int)(stackSz * 0.08), (int)(stackSz * 0.12), 0, 0);
                StackedDay.Margin = dayMargin; StackedDayBehind.Margin = dayMargin;
                
                StackedWeather.Margin = topMargin; 
                StackedWeatherBehind.Margin = topMargin;
            }
            if (isUpward)
            {
                double upSz = baseSize;
                UpwardTime.FontSize = upSz * 1.0; UpwardTimeBehind.FontSize = upSz * 1.0;
                UpwardDate.FontSize = upSz * 0.44; UpwardDateBehind.FontSize = upSz * 0.44;
                UpwardDay.FontSize = upSz * 0.24; UpwardDayBehind.FontSize = upSz * 0.24;
                
                var tMargin = new Thickness(0, (int)(-upSz * 0.25), 0, 0);
                var dMargin = new Thickness(0, (int)(-upSz * 0.15), 0, 0);
                
                UpwardTime.Margin = tMargin; UpwardTimeBehind.Margin = tMargin;
                UpwardDay.Margin = dMargin; UpwardDayBehind.Margin = dMargin;
            }

            // Time & Date format
            var dt = DateTime.Now;
            string hStr = dt.Hour.ToString("D2");
            string mStr = dt.Minute.ToString("D2");
            
            HourPart.Text = hStr;
            HourPartBehind.Text = hStr;
            MinutePart.Text = mStr;
            MinutePartBehind.Text = mStr;
            
            RhombusH1.Text = hStr[0].ToString(); RhombusH1Behind.Text = hStr[0].ToString();
            RhombusH2.Text = hStr[1].ToString(); RhombusH2Behind.Text = hStr[1].ToString();
            RhombusM1.Text = mStr[0].ToString(); RhombusM1Behind.Text = mStr[0].ToString();
            RhombusM2.Text = mStr[1].ToString(); RhombusM2Behind.Text = mStr[1].ToString();
            
            if (isStacked || isUpward)
            {
                string timeStr = hStr + ":" + mStr;
                string dateStr = dt.Day + "/" + dt.Month;
                string dayStr = dt.ToString("ddd").ToUpper();
                var s2 = IsolatedStorageSettings.ApplicationSettings;
                string cached = s2.Contains("CachedWeather") ? (string)s2["CachedWeather"] : "28° ☁";
                string weatherStr = cached;
                
                string carrier = "";
                try
                {
                    carrier = Microsoft.Phone.Net.NetworkInformation.DeviceNetworkInformation.CellularMobileOperator;
                }
                catch { }
                if (string.IsNullOrWhiteSpace(carrier))
                    carrier = "No Service";
                
                if (isStacked)
                {
                    StackedTime.Text = timeStr; StackedTimeBehind.Text = timeStr;
                    StackedDate.Text = dateStr; StackedDateBehind.Text = dateStr;
                    StackedDay.Text = dayStr; StackedDayBehind.Text = dayStr;
                    StackedWeather.Text = weatherStr; StackedWeatherBehind.Text = weatherStr;
                    StackedCarrier.Text = carrier; StackedCarrierBehind.Text = carrier;
                }
                else
                {
                    UpwardTime.Text = timeStr; UpwardTimeBehind.Text = timeStr;
                    UpwardDate.Text = dateStr; UpwardDateBehind.Text = dateStr;
                    UpwardDay.Text = dayStr; UpwardDayBehind.Text = dayStr;
                }
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
            HorizontalAlignment ha = HorizontalAlignment.Center;
            switch (clockHAlign)
            {
                case 0: // Left
                    ha = HorizontalAlignment.Left;
                    ClockPanel.HorizontalAlignment = ha;
                    ClockPanel.Margin = new Thickness(24, ClockPanel.Margin.Top, 0, ClockPanel.Margin.Bottom);
                    break;
                case 2: // Right
                    ha = HorizontalAlignment.Right;
                    ClockPanel.HorizontalAlignment = ha;
                    ClockPanel.Margin = new Thickness(0, ClockPanel.Margin.Top, 24, ClockPanel.Margin.Bottom);
                    break;
                default: // Center
                    ha = HorizontalAlignment.Center;
                    ClockPanel.HorizontalAlignment = ha;
                    break;
            }
            
            DateInfoPanel.HorizontalAlignment = ha;
            TimePanel.HorizontalAlignment = ha;
            BehindTimePanel.HorizontalAlignment = ha;
            StackedGrid.HorizontalAlignment = ha;
            StackedGridBehind.HorizontalAlignment = ha;
            
            TextAlignment ta = TextAlignment.Center;
            if (clockHAlign == 0) ta = TextAlignment.Left;
            else if (clockHAlign == 2) ta = TextAlignment.Right;
            
            StackedCarrier.TextAlignment = ta;
            StackedTime.TextAlignment = ta;
            StackedWeather.TextAlignment = ta;
            
            StackedCarrierBehind.TextAlignment = ta;
            StackedTimeBehind.TextAlignment = ta;
            StackedWeatherBehind.TextAlignment = ta;
            
            StackedCarrier.HorizontalAlignment = HorizontalAlignment.Stretch;
            StackedTime.HorizontalAlignment = HorizontalAlignment.Stretch;
            StackedDatePanel.HorizontalAlignment = ha;
            StackedWeather.HorizontalAlignment = HorizontalAlignment.Stretch;
            
            StackedCarrierBehind.HorizontalAlignment = HorizontalAlignment.Stretch;
            StackedTimeBehind.HorizontalAlignment = HorizontalAlignment.Stretch;
            StackedDatePanelBehind.HorizontalAlignment = ha;
            StackedWeatherBehind.HorizontalAlignment = HorizontalAlignment.Stretch;
            
            UpwardGrid.HorizontalAlignment = ha;
            UpwardGridBehind.HorizontalAlignment = ha;
            
            UpwardTime.TextAlignment = ta;
            UpwardDate.TextAlignment = ta;
            UpwardDay.TextAlignment = ta;
            
            UpwardTimeBehind.TextAlignment = ta;
            UpwardDateBehind.TextAlignment = ta;
            UpwardDayBehind.TextAlignment = ta;
            
            UpwardTime.HorizontalAlignment = HorizontalAlignment.Stretch;
            UpwardDate.HorizontalAlignment = HorizontalAlignment.Stretch;
            UpwardDay.HorizontalAlignment = HorizontalAlignment.Stretch;
            
            UpwardTimeBehind.HorizontalAlignment = HorizontalAlignment.Stretch;
            UpwardDateBehind.HorizontalAlignment = HorizontalAlignment.Stretch;
            UpwardDayBehind.HorizontalAlignment = HorizontalAlignment.Stretch;
        }

        // Helper to avoid XNA vs Media.Color ambiguity
        private static System.Windows.Media.Color C(byte r, byte g, byte b)
        {
            return System.Windows.Media.Color.FromArgb(255, r, g, b);
        }

        private void ApplyClockColor()
        {
            Brush brush;
            bool isAnalog = clockLayout >= 2 && clockLayout <= 4;
            
            if (isAnalog)
            {
                brush = new SolidColorBrush(Colors.White);
            }
            else if (clockHue >= 0)
            {
                brush = new SolidColorBrush(ClockRenderer.ColorFromHSV(clockHue, 1.0, 1.0));
            }
            else if (clockBlend > 0)
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
                    case 2: brush = new SolidColorBrush(C(135, 206, 235)); break; // Sky Blue
                    case 3: brush = new SolidColorBrush(C(255, 182, 193)); break; // Pink
                    case 4: brush = new SolidColorBrush(C(255, 68, 68)); break;   // Red
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
            
            RhombusH1.Foreground = brush; RhombusH2.Foreground = brush;
            RhombusM1.Foreground = brush; RhombusM2.Foreground = brush;
            RhombusH1Behind.Foreground = brush; RhombusH2Behind.Foreground = brush;
            RhombusM1Behind.Foreground = brush; RhombusM2Behind.Foreground = brush;
            
            StackedTime.Foreground = brush; StackedTimeBehind.Foreground = brush;
            StackedDate.Foreground = brush; StackedDateBehind.Foreground = brush;
            StackedWeather.Foreground = brush; StackedWeatherBehind.Foreground = brush;

            UpwardTime.Foreground = brush; UpwardTimeBehind.Foreground = brush;
            UpwardDate.Foreground = brush; UpwardDateBehind.Foreground = brush;
            UpwardDay.Foreground = brush; UpwardDayBehind.Foreground = brush;

            // Apply clock opacity from Editor settings
            ClockPanel.Opacity = clockOpacity;
            BehindClockPanel.Opacity = clockOpacity;
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

        private static int[] SizeValues { get { return ClockRenderer.SizeValues; } }


        /// <summary>
        /// Draws an analog clock face on the given canvas.
        /// style: 2=Minimal (hands only), 3=Classic (12/3/6/9), 4=Swiss (tick marks)
        /// </summary>
        private void DrawAnalogClock(Canvas canvas, double diameter, int hour, int minute, int style)
        {
            Brush clockBrush = HourPart.Foreground;
            ClockRenderer.DrawAnalogClock(canvas, diameter, hour, minute, style, clockBrush);
        }



        private void ApplyDepthLayers()
        {
            bool isAnalog = clockLayout >= 2 && clockLayout <= 4;

            if (!useDepthEffect)
            {
                // No depth — all front, hide behind layer
                BehindForegroundGrid.Visibility = Visibility.Collapsed;
                HourPart.Opacity = 1;
                ColonPart.Opacity = 1;
                MinutePart.Opacity = 1;
                AnalogClockCanvas.Opacity = 1;
                AnalogClockCanvasBehind.Visibility = Visibility.Collapsed;
                RhombusH1.Opacity = 1;
                RhombusH2.Opacity = 1;
                RhombusM1.Opacity = 1;
                RhombusM2.Opacity = 1;
                RhombusDot.Opacity = 1;
                StackedGrid.Opacity = 1;
                StackedGridBehind.Visibility = Visibility.Collapsed;
                UpwardGrid.Opacity = 1;
                UpwardGridBehind.Visibility = Visibility.Collapsed;
                return;
            }

            // Sync behind panel position with front panel
            BehindClockPanel.VerticalAlignment = ClockPanel.VerticalAlignment;
            BehindClockPanel.Margin = ClockPanel.Margin;
            BehindClockPanel.HorizontalAlignment = ClockPanel.HorizontalAlignment;

            bool isStacked = clockLayout == 7;
            bool isUpward = clockLayout == 8;

            if (isAnalog || isStacked || isUpward)
            {
                // Analog/Stacked/Upward: treat entire clock as behind
                BehindForegroundGrid.Visibility = Visibility.Visible;
                AnalogClockCanvas.Opacity = 0;
                AnalogClockCanvasBehind.Visibility = Visibility.Visible;
                HourPart.Opacity = 0;
                ColonPart.Opacity = 0;
                MinutePart.Opacity = 0;
                StackedGrid.Opacity = 0;
                StackedGridBehind.Visibility = Visibility.Visible;
                UpwardGrid.Opacity = 0;
                UpwardGridBehind.Visibility = Visibility.Visible;
            }
            else
            {
                bool anyBehind = depthHourBehind || depthColonBehind || depthMinuteBehind;
                BehindForegroundGrid.Visibility = anyBehind ? Visibility.Visible : Visibility.Collapsed;
                AnalogClockCanvasBehind.Visibility = Visibility.Collapsed;
                AnalogClockCanvas.Opacity = 1;
                StackedGridBehind.Visibility = Visibility.Collapsed;
                StackedGrid.Opacity = 1;
                UpwardGridBehind.Visibility = Visibility.Collapsed;
                UpwardGrid.Opacity = 1;

                double hOp = depthHourBehind ? 0 : 1;
                double hOpB = depthHourBehind ? 1 : 0;
                double cOp = depthColonBehind ? 0 : 1;
                double cOpB = depthColonBehind ? 1 : 0;
                double mOp = depthMinuteBehind ? 0 : 1;
                double mOpB = depthMinuteBehind ? 1 : 0;

                // Hour
                HourPart.Opacity = hOp;
                HourPartBehind.Opacity = hOpB;
                RhombusH1.Opacity = hOp;
                RhombusH2.Opacity = hOp;
                RhombusH1Behind.Opacity = hOpB;
                RhombusH2Behind.Opacity = hOpB;

                // Colon
                ColonPart.Opacity = cOp;
                ColonPartBehind.Opacity = cOpB;
                RhombusDot.Opacity = cOp;
                RhombusDotBehind.Opacity = cOpB;

                // Minute
                MinutePart.Opacity = mOp;
                MinutePartBehind.Opacity = mOpB;
                RhombusM1.Opacity = mOp;
                RhombusM2.Opacity = mOp;
                RhombusM1Behind.Opacity = mOpB;
                RhombusM2Behind.Opacity = mOpB;
            }
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
                // Use dateAlign auto-computed by Editor (based on clock center)
                HorizontalAlignment childAlign = HorizontalAlignment.Center;
                switch (dateAlign)
                {
                    case 0: childAlign = HorizontalAlignment.Left; break;
                    case 2: childAlign = HorizontalAlignment.Right; break;
                }
                
                TimePanel.HorizontalAlignment = childAlign;
                BehindTimePanel.HorizontalAlignment = childAlign;
                DateInfoPanel.HorizontalAlignment = childAlign;
                StackedGrid.HorizontalAlignment = childAlign;
                StackedGridBehind.HorizontalAlignment = childAlign;
                
                TextAlignment ta = TextAlignment.Center;
                if (childAlign == HorizontalAlignment.Left) ta = TextAlignment.Left;
                else if (childAlign == HorizontalAlignment.Right) ta = TextAlignment.Right;
                
                StackedCarrier.TextAlignment = ta;
                StackedTime.TextAlignment = ta;
                StackedWeather.TextAlignment = ta;
                
                StackedCarrierBehind.TextAlignment = ta;
                StackedTimeBehind.TextAlignment = ta;
                StackedWeatherBehind.TextAlignment = ta;
                
                StackedCarrier.HorizontalAlignment = HorizontalAlignment.Stretch;
                StackedTime.HorizontalAlignment = HorizontalAlignment.Stretch;
                StackedDatePanel.HorizontalAlignment = childAlign;
                StackedWeather.HorizontalAlignment = HorizontalAlignment.Stretch;
                
                StackedCarrierBehind.HorizontalAlignment = HorizontalAlignment.Stretch;
                StackedTimeBehind.HorizontalAlignment = HorizontalAlignment.Stretch;
                StackedDatePanelBehind.HorizontalAlignment = childAlign;
                StackedWeatherBehind.HorizontalAlignment = HorizontalAlignment.Stretch;

                BehindClockPanel.VerticalAlignment = VerticalAlignment.Top;
                BehindClockPanel.HorizontalAlignment = HorizontalAlignment.Left;
                BehindClockPanel.Margin = new Thickness(clockFreeX + PAD, clockFreeY + PAD, 0, 0);

                WeatherText.Margin = new Thickness(weatherFreeX + PAD, weatherFreeY + PAD, 0, 0);
                CountdownText.Margin = new Thickness(countdownFreeX + PAD, countdownFreeY + PAD, 0, 0);

                if (OwnerInfoText.Visibility == Visibility.Visible)
                {
                    double ownerY = showCountdown ? countdownFreeY + 30 : (showWeather ? weatherFreeY + 30 : clockFreeY + 155 + 30);
                    OwnerInfoText.Margin = new Thickness(clockFreeX + PAD, ownerY + PAD, 0, 0);
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

        private void ApplySignatureStyle()
        {
            MainSignatureText.Visibility = showSignature ? Visibility.Visible : Visibility.Collapsed;
            if (!showSignature || string.IsNullOrEmpty(sigText)) return;

            MainSignatureText.Text = sigLayout == 1 ? string.Join("\n", sigText.ToCharArray()) : sigText;
            var fontNames = new string[] {
                "MiSans", "MiSans", "MiSans", "Bebas Neue", "Playfair Display",
                "DM Serif Display", "Instrument Serif", "Montserrat", "Poppins",
                "Raleway", "Abril Fatface", "Playfair Display", "Bodoni Moda",
                "Bodoni Moda", "Segoe WP", "Segoe WP Black"
            };
            int fIdx = Math.Max(0, Math.Min(sigFontIndex, fontNames.Length - 1));
            var ff = new FontFamily("/Assets/Fonts/" + fontNames[fIdx].Replace(" ", "") + "-Regular.ttf#" + fontNames[fIdx]);
            if (fIdx == 1) ff = new FontFamily("/Assets/Fonts/MiSans-Demibold.ttf#MiSans");
            if (fIdx == 2) ff = new FontFamily("/Assets/Fonts/MiSans-Light.ttf#MiSans");
            if (fIdx == 11) ff = new FontFamily("/Assets/Fonts/PlayfairDisplay-Italic.ttf#Playfair Display");
            if (fIdx == 13) ff = new FontFamily("/Assets/Fonts/BodoniModa-Italic.ttf#Bodoni Moda");
            if (fIdx == 14) ff = new FontFamily("Segoe WP");
            if (fIdx == 15) ff = new FontFamily("Segoe WP Black");

            MainSignatureText.FontFamily = ff;
            MainSignatureText.FontSize = 48;
            MainSignatureText.CharacterSpacing = (int)sigSpacing;
            if (sigLayout == 1)
            {
                MainSignatureText.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
                MainSignatureText.LineHeight = 48 + (sigSpacing / 20.0);
            }
            else
            {
                MainSignatureText.LineHeight = 0;
            }
            
            Brush brush;
            if (sigHue >= 0)
            {
                brush = new SolidColorBrush(ClockRenderer.ColorFromHSV(sigHue, 1.0, 1.0));
            }
            else if (sigBlend > 0)
            {
                switch (sigBlend)
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
                switch (sigColorIdx)
                {
                    case 1: brush = new SolidColorBrush(C(255, 215, 0)); break;   // Gold
                    case 2: brush = new SolidColorBrush(C(135, 206, 235)); break; // Sky Blue
                    case 3: brush = new SolidColorBrush(C(255, 182, 193)); break; // Pink
                    case 4: brush = new SolidColorBrush(C(255, 68, 68)); break;   // Red
                    case 5: brush = new SolidColorBrush(C(91, 255, 176)); break;  // Mint
                    case 6: brush = new SolidColorBrush(C(196, 167, 255)); break; // Lavender
                    case 7: brush = new SolidColorBrush(C(255, 140, 66)); break;  // Orange
                    case 8: brush = new SolidColorBrush(C(0, 229, 255)); break;   // Cyan
                    case 9: brush = new SolidColorBrush(C(160, 160, 176)); break; // Silver
                    default: brush = new SolidColorBrush(Colors.White); break;
                }
            }
            MainSignatureText.Foreground = brush;

            MainSignatureText.Opacity = sigOpacity;
            MainSignatureText.Margin = new Thickness(signatureX + 5.5, signatureY + 5.5, 0, 0);
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
            if (showWeather)
            {
                WeatherText.VerticalAlignment = VerticalAlignment.Top;
                WeatherText.Margin = new Thickness(
                    WeatherText.Margin.Left, clockBottom + 6, 0, 0);
                clockBottom += 28;
            }

            if (showCountdown)
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
                    string bgToLoad = (useMatte || useRibbed) && store.FileExists("Background_Filtered.jpg") ? "Background_Filtered.jpg" : "Background.jpg";
                    if (store.FileExists(bgToLoad))
                    {
                        using (var stream = store.OpenFile(bgToLoad,
                            System.IO.FileMode.Open, System.IO.FileAccess.Read))
                        {
                            var bitmap = new BitmapImage();
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

        private void FetchWeather(bool force = false)
        {
            if (!showWeather || (weatherLat == 0 && weatherLon == 0))
            {
                WeatherText.Visibility = Visibility.Collapsed;
                return;
            }

            // Show cached weather immediately while fetching new data
            LoadCachedWeather();

            if (weatherRequestInFlight) return;
            if (!force && lastWeatherFetchUtc != DateTime.MinValue
                && (DateTime.UtcNow - lastWeatherFetchUtc) < WeatherMinRefreshInterval)
            {
                return;
            }

            try
            {
                weatherRequestInFlight = true;
                lastWeatherFetchUtc = DateTime.UtcNow;

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
                                
                                if (clockLayout == 7)
                                {
                                    StackedWeather.Text = WeatherText.Text;
                                    StackedWeatherBehind.Text = WeatherText.Text;
                                }


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
                    finally
                    {
                        weatherRequestInFlight = false;
                    }
                }, null);
            }
            catch
            {
                weatherRequestInFlight = false;
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
                    
                    if (clockLayout == 7)
                    {
                        StackedWeather.Text = WeatherText.Text;
                        StackedWeatherBehind.Text = WeatherText.Text;
                    }

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
            if (CameraOverlay != null && CameraOverlay.Visibility == Visibility.Visible)
            {
                e.Cancel = true;
                CloseCameraOverlay();
                return;
            }

            if (MySetsOverlay.Visibility == Visibility.Visible)
            {
                e.Cancel = true;
                MySetsOverlay.Visibility = Visibility.Collapsed;
                return;
            }

            var t = (CompositeTransform)LockScreenPanel.RenderTransform;
            if (t != null && t.TranslateY != 0)
            {
                e.Cancel = true;
                AnimateBackToNormal();
                return;
            }

            // Block back only when app is currently acting as lock screen.
            try
            {
                e.Cancel = SystemProtection.ScreenLocked;
            }
            catch
            {
                e.Cancel = true;
            }
        }

        #endregion

        #region Long Press → My Sets (Inline Overlay)

        private class MSPreset
        {
            public string Name, Subtitle, Category;
            public int ClockStyle, ClockSize, ClockColor, ClockBlend, DateAlign;
            public int ClockLayout; // 0=Horiz, 1=Vert, 2=Minimal, 3=Classic, 4=Swiss, 5=Rhombus, 6=Giant
            public MC PreviewBg, PreviewClockColor;
            public double ClockX = -1, ClockY = -1;
            public bool UseDepthEffect;
            public bool DepthHourBehind = true, DepthColonBehind = true, DepthMinuteBehind = true;
            public bool UseMatte, UseRibbed;
            public int ClockHue = -1;
            public int SignatureHue = -1;
            public string BackgroundImage; // e.g. "Assets/Pictures/classic02.jpg"
        }

        private static readonly List<MSPreset> msPresetsOriginal = new List<MSPreset>
        {
            // CLASSIC
            new MSPreset { Category="Classic", Name="Classic", Subtitle="What's classic never goes out of style.", ClockStyle=0, ClockSize=2, ClockColor=0, ClockBlend=0, DateAlign=0, ClockX=30, ClockY=100, PreviewBg=MC.FromArgb(255,60,60,80), PreviewClockColor=MC.FromArgb(255,255,255,255), BackgroundImage="/Assets/Pictures/classic02.jpg" },
            new MSPreset { Category="Classic", Name="Ice", Subtitle="Cool and crisp.", ClockStyle=9, ClockSize=2, ClockColor=0, ClockBlend=0, DateAlign=0, ClockX=20, ClockY=50, PreviewBg=MC.FromArgb(255,120,160,200), PreviewClockColor=MC.FromArgb(255,255,255,255), BackgroundImage="/Assets/Pictures/classic03.jpg" },
            new MSPreset { Category="Classic", Name="Ocean", Subtitle="Calm waves, deep blue.", ClockStyle=0, ClockSize=2, ClockColor=0, ClockBlend=0, DateAlign=0, ClockLayout=3, ClockX=30, ClockY=50, PreviewBg=MC.FromArgb(255,20,100,120), PreviewClockColor=MC.FromArgb(255,255,255,255), BackgroundImage="/Assets/Pictures/AI Static 3.jpg" },
            new MSPreset { Category="Classic", Name="Analog", Subtitle="Classic analog elegance.", ClockStyle=0, ClockSize=2, ClockColor=0, ClockBlend=0, DateAlign=1, ClockLayout=2, PreviewBg=MC.FromArgb(255,20,20,30), PreviewClockColor=MC.FromArgb(255,255,255,255) },
            new MSPreset { Category="Classic", Name="Classic Clock", Subtitle="Numbers on the dial.", ClockStyle=0, ClockSize=2, ClockColor=1, ClockBlend=0, DateAlign=1, ClockLayout=3, PreviewBg=MC.FromArgb(255,40,30,20), PreviewClockColor=MC.FromArgb(255,255,215,0) },
            new MSPreset { Category="Classic", Name="Swiss", Subtitle="Precision Swiss design.", ClockStyle=0, ClockSize=2, ClockColor=0, ClockBlend=0, DateAlign=1, ClockLayout=4, PreviewBg=MC.FromArgb(255,10,10,15), PreviewClockColor=MC.FromArgb(255,255,255,255) },

            // RHOMBUS
            new MSPreset { Category="Rhombus", Name="Floral", Subtitle="Beauty in nature.", ClockStyle=11, ClockSize=3, ClockColor=0, ClockBlend=0, DateAlign=1, ClockLayout=5, ClockX=160, ClockY=100, PreviewBg=MC.FromArgb(255,20,20,20), PreviewClockColor=MC.FromArgb(255,255,255,255), BackgroundImage="/Assets/Pictures/10062799164539040.jpg" },
            new MSPreset { Category="Rhombus", Name="Blossom", Subtitle="Soft and elegant.", ClockStyle=13, ClockSize=3, ClockColor=0, ClockBlend=0, DateAlign=1, ClockLayout=5, ClockX=160, ClockY=100, PreviewBg=MC.FromArgb(255,40,30,30), PreviewClockColor=MC.FromArgb(255,255,255,255), BackgroundImage="/Assets/Pictures/10696117861097790.jpg" },
            new MSPreset { Category="Rhombus", Name="Architecture", Subtitle="Structured heights.", ClockStyle=11, ClockSize=3, ClockColor=0, ClockBlend=0, DateAlign=1, ClockLayout=5, ClockX=160, ClockY=100, PreviewBg=MC.FromArgb(255,10,30,40), PreviewClockColor=MC.FromArgb(255,255,255,255), BackgroundImage="/Assets/Pictures/Tall Building Wallpaper.jpg" },
            new MSPreset { Category="Rhombus", Name="Hyper", Subtitle="Flowing gradient.", ClockStyle=13, ClockSize=3, ClockColor=0, ClockBlend=0, DateAlign=1, ClockLayout=5, ClockX=160, ClockY=100, PreviewBg=MC.FromArgb(255,20,10,40), PreviewClockColor=MC.FromArgb(255,255,255,255), BackgroundImage="/Assets/Pictures/Xiaomi HyperOS Wallpapers.jpg" },

            // MAGAZINE
            new MSPreset { Category="Magazine", Name="Magazine", Subtitle="Turn your lock screen into a cover.", ClockStyle=6, ClockSize=2, ClockColor=0, ClockBlend=0, DateAlign=0, ClockX=25, ClockY=580, PreviewBg=MC.FromArgb(255,180,140,80), PreviewClockColor=MC.FromArgb(255,255,255,255), BackgroundImage="/Assets/Pictures/magazine01.jpg" },
            new MSPreset { Category="Magazine", Name="Bold", Subtitle="Make a statement with bold typography.", ClockStyle=3, ClockSize=4, ClockColor=1, ClockBlend=0, DateAlign=0, ClockLayout=1, ClockX=20, ClockY=480, PreviewBg=MC.FromArgb(255,120,20,20), PreviewClockColor=MC.FromArgb(255,255,215,0), BackgroundImage="/Assets/Pictures/east07.jpg" },
            new MSPreset { Category="Magazine", Name="Elegant", Subtitle="Refined beauty in every detail.", ClockStyle=4, ClockSize=3, ClockColor=1, ClockBlend=0, DateAlign=1, ClockX=220, ClockY=80, PreviewBg=MC.FromArgb(255,15,15,12), PreviewClockColor=MC.FromArgb(255,255,215,0), BackgroundImage="/Assets/Pictures/east05.jpg" },
            new MSPreset { Category="Magazine", Name="Neon", Subtitle="Electrify your screen.", ClockStyle=0, ClockSize=3, ClockColor=5, ClockBlend=3, DateAlign=1, ClockLayout=4, PreviewBg=MC.FromArgb(255,15,50,40), PreviewClockColor=MC.FromArgb(255,80,255,150), BackgroundImage="/Assets/Pictures/east06.jpg" },
            new MSPreset { Category="Magazine", Name="Minimal", Subtitle="Less is more.", ClockStyle=9, ClockSize=0, ClockColor=0, ClockBlend=0, DateAlign=0, ClockX=30, ClockY=60, PreviewBg=MC.FromArgb(255,60,60,60), PreviewClockColor=MC.FromArgb(255,255,255,255), BackgroundImage="/Assets/Pictures/magazine05.jpg" },
            new MSPreset { Category="Magazine", Name="Serif", Subtitle="Timeless serif elegance.", ClockStyle=5, ClockSize=3, ClockColor=0, ClockBlend=0, DateAlign=1, ClockLayout=1, PreviewBg=MC.FromArgb(255,20,60,70), PreviewClockColor=MC.FromArgb(255,255,255,255), BackgroundImage="/Assets/Pictures/east02.jpg" },
            new MSPreset { Category="Magazine", Name="Display", Subtitle="Time is important. Make it count.", ClockStyle=10, ClockSize=4, ClockColor=7, ClockBlend=0, DateAlign=1, ClockX=180, ClockY=600, PreviewBg=MC.FromArgb(255,40,40,120), PreviewClockColor=MC.FromArgb(255,255,140,66), BackgroundImage="/Assets/Pictures/magazine06.jpg" },
            new MSPreset { Category="Magazine", Name="Fire", Subtitle="Feel the heat.", ClockStyle=7, ClockSize=3, ClockColor=1, ClockBlend=0, DateAlign=0, ClockX=20, ClockY=520, PreviewBg=MC.FromArgb(255,120,30,20), PreviewClockColor=MC.FromArgb(255,255,215,0), BackgroundImage="/Assets/Pictures/east01.jpg" },
            new MSPreset { Category="Magazine", Name="Aurora", Subtitle="Northern lights on your screen.", ClockStyle=2, ClockSize=2, ClockColor=6, ClockBlend=0, DateAlign=1, PreviewBg=MC.FromArgb(255,15,30,50), PreviewClockColor=MC.FromArgb(255,196,167,255), BackgroundImage="/Assets/Pictures/AI Static 4.jpg" },
            new MSPreset { Category="Magazine", Name="Poppins", Subtitle="Modern geometric beauty.", ClockStyle=8, ClockSize=1, ClockColor=0, ClockBlend=0, DateAlign=1, ClockX=230, ClockY=40, PreviewBg=MC.FromArgb(255,80,120,90), PreviewClockColor=MC.FromArgb(255,255,255,255), BackgroundImage="/Assets/Pictures/magazine04.jpg" },
            new MSPreset { Category="Magazine", Name="Twilight", Subtitle="Between day and night.", ClockStyle=10, ClockSize=3, ClockColor=7, ClockBlend=1, DateAlign=0, ClockLayout=1, ClockX=20, ClockY=500, PreviewBg=MC.FromArgb(255,15,20,50), PreviewClockColor=MC.FromArgb(255,255,180,80), BackgroundImage="/Assets/Pictures/magazine02.jpg" },
            new MSPreset { Category="Magazine", Name="Lime", Subtitle="Fresh and vibrant energy.", ClockStyle=4, ClockSize=2, ClockColor=1, ClockBlend=0, DateAlign=1, ClockX=200, ClockY=580, PreviewBg=MC.FromArgb(255,50,55,60), PreviewClockColor=MC.FromArgb(255,255,215,0), BackgroundImage="/Assets/Pictures/magazine03.jpg" },
            new MSPreset { Category="Magazine", Name="Vertical", Subtitle="Time stacked vertically.", ClockStyle=3, ClockSize=3, ClockColor=2, ClockBlend=0, DateAlign=0, ClockLayout=1, ClockX=20, ClockY=500, PreviewBg=MC.FromArgb(255,130,180,230), PreviewClockColor=MC.FromArgb(255,135,206,235), BackgroundImage="/Assets/Pictures/magazine07.jpg" },
        };

        // Working copy — rebuilt from originals each time to prevent static mutation
        private List<MSPreset> msPresets = new List<MSPreset>();

        private const double MS_CW = 200, MS_CH = 360, MS_GAP = 16, MS_STEP = 216, MS_SW = 480;
        private int msCurrentIndex;
        private double msOffsetX, msTotalDragX;
        private List<Border> msCards = new List<Border>();
        private List<Ellipse> msDots = new List<Ellipse>();
        private Dictionary<int, BitmapImage> msWallpapers = new Dictionary<int, BitmapImage>();
        private BitmapImage msForeground;
        // Cached dot brushes (CPU: avoids allocating per swipe)
        private static readonly SolidColorBrush msDotActive = new SolidColorBrush(MC.FromArgb(255, 255, 255, 255));
        private static readonly SolidColorBrush msDotInactive = new SolidColorBrush(MC.FromArgb(80, 255, 255, 255));

        private void LayoutRoot_Hold(object sender, System.Windows.Input.GestureEventArgs e)
        {
            // Guard: don't open MySets if overlay is already open or if user is mid-swipe
            if (MySetsOverlay.Visibility == Visibility.Visible)
                return;

            var t = (CompositeTransform)LockScreenPanel.RenderTransform;
            if (t != null && Math.Abs(t.TranslateY) > 20) return; // mid-swipe or at PIN pad

            ShowMySetsOverlay();
        }

        private void ShowMySetsOverlay()
        {
            // Rebuild working presets from originals (prevents static mutation — BUG 3 fix)
            msPresets.Clear();
            for (int i = 0; i < msPresetsOriginal.Count; i++)
            {
                var o = msPresetsOriginal[i];
                msPresets.Add(new MSPreset
                {
                    Name = o.Name, Subtitle = o.Subtitle, Category = o.Category,
                    ClockStyle = o.ClockStyle, ClockSize = o.ClockSize,
                    ClockColor = o.ClockColor, ClockBlend = o.ClockBlend,
                    ClockHue = o.ClockHue, SignatureHue = o.SignatureHue,
                    DateAlign = o.DateAlign, ClockLayout = o.ClockLayout,
                    ClockX = o.ClockX, ClockY = o.ClockY,
                    UseDepthEffect = o.UseDepthEffect,
                    DepthHourBehind = o.DepthHourBehind,
                    DepthColonBehind = o.DepthColonBehind,
                    DepthMinuteBehind = o.DepthMinuteBehind,
                    UseMatte = o.UseMatte,
                    UseRibbed = o.UseRibbed,
                    PreviewBg = o.PreviewBg, PreviewClockColor = o.PreviewClockColor,
                    BackgroundImage = o.BackgroundImage
                });
            }

            // Don't preload all wallpapers — use lazy loading in BuildMSCard to avoid OOM
            msWallpapers.Clear();

            // Read saved preset overrides
            var s = IsolatedStorageSettings.ApplicationSettings;

            // First preset (Classic) reads live/active settings
            var first = msPresets[0];
            if (s.Contains("ClockStyle")) try { first.ClockStyle = (int)s["ClockStyle"]; } catch { }
            if (s.Contains("ClockSize")) try { first.ClockSize = (int)s["ClockSize"]; } catch { }
            if (s.Contains("ClockColor")) try { first.ClockColor = (int)s["ClockColor"]; } catch { }
            if (s.Contains("ClockBlend")) try { first.ClockBlend = (int)s["ClockBlend"]; } catch { }
            if (s.Contains("ClockHue")) try { first.ClockHue = (int)s["ClockHue"]; } catch { }
            if (s.Contains("ClockLayout")) try { first.ClockLayout = (int)s["ClockLayout"]; } catch { }
            if (s.Contains("ClockX")) try { first.ClockX = (double)s["ClockX"]; } catch { }
            if (s.Contains("ClockY")) try { first.ClockY = (double)s["ClockY"]; } catch { }
            if (s.Contains("UseDepthEffect")) try { first.UseDepthEffect = (bool)s["UseDepthEffect"]; } catch { }
            if (s.Contains("DepthHourBehind")) try { first.DepthHourBehind = (bool)s["DepthHourBehind"]; } catch { }
            if (s.Contains("DepthColonBehind")) try { first.DepthColonBehind = (bool)s["DepthColonBehind"]; } catch { }
            if (s.Contains("DepthMinuteBehind")) try { first.DepthMinuteBehind = (bool)s["DepthMinuteBehind"]; } catch { }
            if (s.Contains("UseMatte")) try { first.UseMatte = (bool)s["UseMatte"]; } catch { }
            if (s.Contains("UseRibbed")) try { first.UseRibbed = (bool)s["UseRibbed"]; } catch { }
            first.PreviewClockColor = ClockRenderer.ResolveClockColor(first.ClockColor, first.ClockBlend, first.ClockHue);

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
                    if (s.Contains(pfx + "ClockHue")) try { p.ClockHue = (int)s[pfx + "ClockHue"]; } catch { }
                    if (s.Contains(pfx + "ClockLayout")) try { p.ClockLayout = (int)s[pfx + "ClockLayout"]; } catch { }
                    if (s.Contains(pfx + "ClockX")) try { p.ClockX = (double)s[pfx + "ClockX"]; } catch { }
                    if (s.Contains(pfx + "ClockY")) try { p.ClockY = (double)s[pfx + "ClockY"]; } catch { }
                    if (s.Contains(pfx + "UseDepthEffect")) try { p.UseDepthEffect = (bool)s[pfx + "UseDepthEffect"]; } catch { }
                    if (s.Contains(pfx + "DepthHourBehind")) try { p.DepthHourBehind = (bool)s[pfx + "DepthHourBehind"]; } catch { }
                    if (s.Contains(pfx + "DepthColonBehind")) try { p.DepthColonBehind = (bool)s[pfx + "DepthColonBehind"]; } catch { }
                    if (s.Contains(pfx + "DepthMinuteBehind")) try { p.DepthMinuteBehind = (bool)s[pfx + "DepthMinuteBehind"]; } catch { }
                    if (s.Contains(pfx + "UseMatte")) try { p.UseMatte = (bool)s[pfx + "UseMatte"]; } catch { }
                    if (s.Contains(pfx + "UseRibbed")) try { p.UseRibbed = (bool)s[pfx + "UseRibbed"]; } catch { }
                    p.PreviewClockColor = ClockRenderer.ResolveClockColor(p.ClockColor, p.ClockBlend, p.ClockHue);
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
                            bmp.DecodePixelWidth = 220; // Thumbnail size for cards
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
            Brush bg = new SolidColorBrush(preset.PreviewBg); // fallback

            // Lazy-load wallpaper with small decode size to prevent OOM
            try
            {
                using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    string savedFile = (preset.UseMatte || preset.UseRibbed) && store.FileExists("Background_Filtered_" + index + ".jpg")
                        ? "Background_Filtered_" + index + ".jpg"
                        : "Background_" + index + ".jpg";

                    if (store.FileExists(savedFile))
                    {
                        using (var stream = store.OpenFile(savedFile, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                        {
                            var bmp = new BitmapImage();
                            bmp.DecodePixelWidth = 220; // Thumbnail only
                            bmp.SetSource(stream);
                            bg = new ImageBrush { ImageSource = bmp, Stretch = Stretch.UniformToFill };
                        }
                    }
                    else if (!string.IsNullOrEmpty(preset.BackgroundImage))
                    {
                        var bmp = new BitmapImage();
                        bmp.DecodePixelWidth = 220;
                        bmp.UriSource = new Uri(preset.BackgroundImage, UriKind.Relative);
                        bg = new ImageBrush { ImageSource = bmp, Stretch = Stretch.UniformToFill };
                    }
                }
            }
            catch { }

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



            // Clock setup
            var brush = new SolidColorBrush(preset.PreviewClockColor);
            var transBrush = new SolidColorBrush(Colors.Transparent);
            bool hasDepth = preset.UseDepthEffect && msForeground != null;

            // Date brush logic: Stacked/Upward layout shares the clock color, others use translucent white
            var baseDateBrush = (preset.ClockLayout == 7 || preset.ClockLayout == 8) ? brush : new SolidColorBrush(MC.FromArgb(180, 255, 255, 255));
            bool isFullBehind = preset.ClockLayout == 7 || preset.ClockLayout == 8 || (preset.ClockLayout >= 2 && preset.ClockLayout <= 4);

            // --- BEHIND LAYER (or full layer if no depth) ---
            var behindDateBrush = hasDepth ? (isFullBehind ? baseDateBrush : transBrush) : baseDateBrush;

            var behindStack = ClockRenderer.BuildCardPreview(
                preset.ClockLayout, preset.ClockStyle, preset.ClockSize,
                preset.ClockX, preset.ClockY, preset.DateAlign, MS_CW, MS_CH,
                hasDepth ? (preset.DepthHourBehind || isFullBehind ? brush : transBrush) : brush,
                hasDepth ? (preset.DepthColonBehind || isFullBehind ? brush : transBrush) : brush,
                hasDepth ? (preset.DepthMinuteBehind || isFullBehind ? brush : transBrush) : brush,
                behindDateBrush, index);
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
                var frontDateBrush = isFullBehind ? transBrush : baseDateBrush;
                var frontStack = ClockRenderer.BuildCardPreview(
                    preset.ClockLayout, preset.ClockStyle, preset.ClockSize,
                    preset.ClockX, preset.ClockY, preset.DateAlign, MS_CW, MS_CH,
                    (preset.DepthHourBehind || isFullBehind) ? transBrush : brush,
                    (preset.DepthColonBehind || isFullBehind) ? transBrush : brush,
                    (preset.DepthMinuteBehind || isFullBehind) ? transBrush : brush,
                    frontDateBrush, index);
                inner.Children.Add(frontStack);
            }

            // Border frame
            inner.Children.Add(new Border { BorderBrush = new SolidColorBrush(MC.FromArgb(50, 255, 255, 255)), BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(24), IsHitTestVisible = false });

            return card;
        }


        // Card clock rendering now handled by ClockRenderer.BuildCardPreview()



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
            if (msAnimTimer != null)
            {
                msAnimTimer.Stop();
                msAnimTimer = null;
            }

            msCurrentIndex = Math.Max(0, Math.Min(msPresets.Count - 1, index));
            double target = -msCurrentIndex * MS_STEP;

            if (animate)
            {
                int steps = 12; double startOff = msOffsetX; int step = 0;
                msAnimTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                msAnimTimer.Tick += (s2, ev2) =>
                {
                    step++;
                    double t = 1 - Math.Pow(1 - (double)step / steps, 3);
                    msOffsetX = startOff + (target - startOff) * t;
                    MSLayoutCards();
                    if (step >= steps)
                    {
                        if (msAnimTimer != null)
                        {
                            msAnimTimer.Stop();
                            msAnimTimer = null;
                        }
                        msOffsetX = target;
                        MSLayoutCards();
                    }
                };
                msAnimTimer.Start();
            }
            else { msOffsetX = target; MSLayoutCards(); }

            MySetsCategory.Text = (msPresets[msCurrentIndex].Category ?? "").ToUpper();
            MySetsTitle.Text = msPresets[msCurrentIndex].Name;
            MySetsSubtitle.Text = msPresets[msCurrentIndex].Subtitle;
            for (int i = 0; i < msDots.Count; i++)
            {
                bool active = i == msCurrentIndex;
                msDots[i].Fill = active ? msDotActive : msDotInactive;
                msDots[i].Width = active ? 8 : 6;
                msDots[i].Height = active ? 8 : 6;
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
            s["ActivePresetIndex"] = msCurrentIndex;

            string px = "Set" + msCurrentIndex + "_";

            // If this preset hasn't been saved yet, initialize its default keys
            if (!s.Contains(px + "ClockStyle"))
            {
                s[px + "ClockStyle"] = preset.ClockStyle;
                s[px + "ClockSize"] = preset.ClockSize;
                s[px + "ClockColor"] = preset.ClockColor;
                s[px + "ClockBlend"] = preset.ClockBlend;
                s[px + "ClockHue"] = preset.ClockHue;
                s[px + "SignatureHue"] = preset.SignatureHue;
                s[px + "DateAlign"] = preset.DateAlign;
                s[px + "ClockLayout"] = preset.ClockLayout;
                if (preset.ClockX >= 0) s[px + "ClockX"] = preset.ClockX;
                if (preset.ClockY >= 0) s[px + "ClockY"] = preset.ClockY;
                s[px + "UseDepthEffect"] = preset.UseDepthEffect;
                s[px + "DepthHourBehind"] = preset.DepthHourBehind;
                s[px + "DepthColonBehind"] = preset.DepthColonBehind;
                s[px + "DepthMinuteBehind"] = preset.DepthMinuteBehind;
            }

            // Sync ALL Set{n}_ keys to global keys so EditorPage and LockScreen read correctly
            string[] keys = { "ClockStyle", "ClockSize", "ClockColor", "ClockBlend", "ClockOpacity", "ClockHue", "DateAlign", "ClockLayout",
                "ClockX", "ClockY", "UseDepthEffect", "DepthHourBehind", "DepthColonBehind", "DepthMinuteBehind",
                "UseMatte", "UseRibbed",
                "ShowWeather", "ShowCountdown", "WeatherX", "WeatherY", "CountdownX", "CountdownY",
                "ShowSignature", "SignatureText", "SignatureFont", "SignatureSpacing", "SignatureAlign", "SignatureColor", "SignatureBlend", "SignatureOpacity", "SignatureHue", "SignatureX", "SignatureY" };
            foreach (var key in keys)
            {
                if (s.Contains(px + key))
                    s[key] = s[px + key];
                else
                {
                    // Remove global keys if preset doesn't have them (fallback to default)
                    if (s.Contains(key)) s.Remove(key);
                }
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

                        // Also copy filtered background if it exists
                        string presetFilteredBg = "Background_Filtered_" + msCurrentIndex + ".jpg";
                        if (store.FileExists("Background_Filtered.jpg"))
                            store.DeleteFile("Background_Filtered.jpg");
                        if (store.FileExists(presetFilteredBg))
                            store.CopyFile(presetFilteredBg, "Background_Filtered.jpg");

                        s[px + "BackgroundImage"] = null; // Clear fallback string
                        if (s.Contains("BackgroundImage")) s.Remove("BackgroundImage");
                    }
                    else
                    {
                        // No saved wallpaper — copy default from app resources
                        string defaultBg = (preset.BackgroundImage ?? "").TrimStart('/');
                        if (!string.IsNullOrEmpty(defaultBg))
                        {
                            try
                            {
                                var sri = Application.GetResourceStream(new Uri(defaultBg, UriKind.Relative));
                                if (sri != null && sri.Stream != null)
                                {
                                    if (store.FileExists("Background.jpg"))
                                        store.DeleteFile("Background.jpg");
                                    using (var iso = store.OpenFile("Background.jpg",
                                        System.IO.FileMode.Create, System.IO.FileAccess.Write))
                                    {
                                        sri.Stream.CopyTo(iso);
                                    }
                                    sri.Stream.Dispose();
                                }
                            }
                            catch { }

                            // Also save the string for Editor fallback
                            s[px + "BackgroundImage"] = preset.BackgroundImage;
                            s["BackgroundImage"] = preset.BackgroundImage;

                            // Built-in preset means no custom filters
                            if (store.FileExists("Background_Filtered.jpg"))
                                store.DeleteFile("Background_Filtered.jpg");
                        }
                        else
                        {
                            // Truly no wallpaper (analog presets) — remove old one
                            if (store.FileExists("Background.jpg"))
                                store.DeleteFile("Background.jpg");
                            if (store.FileExists("Background_Filtered.jpg"))
                                store.DeleteFile("Background_Filtered.jpg");
                            if (s.Contains(px + "BackgroundImage")) s.Remove(px + "BackgroundImage");
                            if (s.Contains("BackgroundImage")) s.Remove("BackgroundImage");
                        }
                    }
                }
            }
            catch { }

            if (msAnimTimer != null)
            {
                msAnimTimer.Stop();
                msAnimTimer = null;
            }

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
            lastTimeText = "";
            UpdateTime();
            UpdateBattery();
            UpdateCountdown();
            if (showWeather)
            {
                if (!weatherTimer.IsEnabled) weatherTimer.Start();
                FetchWeather(true);
            }
            else
            {
                weatherTimer.Stop();
                WeatherText.Visibility = Visibility.Collapsed;
            }
            LoadForeground();
            ApplyDepthLayers();
            ApplyFreePositions();
            ApplySignatureStyle();
        }

        private void MySets_Close_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            if (msAnimTimer != null)
            {
                msAnimTimer.Stop();
                msAnimTimer = null;
            }
            MySetsOverlay.Visibility = Visibility.Collapsed;
        }

        #endregion
    }
}
