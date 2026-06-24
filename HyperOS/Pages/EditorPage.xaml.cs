using System;
using System.IO;
using System.IO.IsolatedStorage;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Tasks;
using Windows.Phone.System.LockScreenExtensibility;

namespace HyperOS.Pages
{
    public partial class EditorPage : PhoneApplicationPage
    {
        private bool isLoading = true;
        private bool hasUnsavedChanges = false;

        // Current selections
        private string selectedTab = "Clock";
        private Border selectedHandle;

        // Element positions (pixels, 480x800 space)
        private double clockX, clockY;
        private double weatherX, weatherY;
        private double countdownX, countdownY;
        private bool hasSavedPositions;

        // Clock settings
        private int clockStyle = 0;
        private int clockSize = 2;
        private int clockColor = 0;
        private int clockBlend = 0;
        private int dateAlign = 1; // 0=Left, 1=Center, 2=Right

        // Widget settings
        private bool showWeather;
        private bool showCountdown;

        // Depth
        private bool useDepthEffect;
        private bool depthHourBehind = true;
        private bool depthColonBehind = true;
        private bool depthMinuteBehind = true;

        // Font & size arrays
        private static readonly string[] FontNames = {
            "MiSans Regular", "MiSans Bold", "MiSans Light", "Bebas Neue",
            "Playfair Display", "DM Serif", "Instrument Serif",
            "Montserrat", "Poppins", "Raleway Light", "Abril Fatface",
            "Segoe WP", "Segoe WP Black" };
        private static readonly FontFamily[] Fonts = {
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
            new FontFamily("Segoe WP Black") };
        private static readonly int[] SizeValues = { 80, 95, 105, 120, 140 };

        // Accent
        private static readonly SolidColorBrush AccentBrush =
            new SolidColorBrush(Color.FromArgb(0xFF, 0x3A, 0x7B, 0xF2));
        private static readonly SolidColorBrush SelectBrush =
            new SolidColorBrush(Color.FromArgb(0xAA, 0x00, 0xD4, 0xAA));
        private static readonly SolidColorBrush TransparentBrush =
            new SolidColorBrush(Colors.Transparent);
        private static readonly SolidColorBrush InactiveTabBg =
            new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF));

        // Which preset slot we are editing (-1 = none/direct)
        private int editingPreset = -1;

        // Snapshot of settings at editor open, for restoring on discard
        private System.Collections.Generic.Dictionary<string, object> settingsSnapshot;

        public EditorPage()
        {
            InitializeComponent();
        }

        #region Lifecycle

        private void EditorPage_Loaded(object sender, RoutedEventArgs e)
        {
            isLoading = true;
            LoadAllSettings();
            LoadPreviewImages();
            ApplyPreview();
            SelectTab("Clock");
            isLoading = false;
            TakeSettingsSnapshot();

            // If no saved positions, center elements after layout is computed
            if (!hasSavedPositions)
            {
                ClockHandle.LayoutUpdated += OnLayoutCenter;
            }
        }

        private void OnLayoutCenter(object sender, EventArgs e)
        {
            if (ClockHandle.ActualWidth <= 0) return; // Not yet rendered
            ClockHandle.LayoutUpdated -= OnLayoutCenter;
            CenterElements();
        }

        private void CenterElements()
        {
            // Center clock like the lock screen default (Center/Center, margin-bottom 40)
            clockX = (SCREEN_W - ClockHandle.ActualWidth) / 2.0;
            clockY = (SCREEN_H - ClockHandle.ActualHeight) / 2.0 + 8;
            ClockHandle.Margin = new Thickness(clockX, clockY, 0, 0);

            // Stack weather and countdown below clock, also centered
            if (WeatherHandle.Visibility == Visibility.Visible)
            {
                weatherX = (SCREEN_W - WeatherHandle.ActualWidth) / 2.0;
                weatherY = clockY + ClockHandle.ActualHeight + 8;
                WeatherHandle.Margin = new Thickness(weatherX, weatherY, 0, 0);
            }
            if (CountdownHandle.Visibility == Visibility.Visible)
            {
                double baseY = (WeatherHandle.Visibility == Visibility.Visible)
                    ? weatherY + WeatherHandle.ActualHeight + 4
                    : clockY + ClockHandle.ActualHeight + 8;
                countdownX = (SCREEN_W - CountdownHandle.ActualWidth) / 2.0;
                countdownY = baseY;
                CountdownHandle.Margin = new Thickness(countdownX, countdownY, 0, 0);
            }
        }

        protected override void OnNavigatedTo(System.Windows.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Restore unsaved positions from transient state (after tombstone)
            var state = Microsoft.Phone.Shell.PhoneApplicationService.Current.State;
            bool restoredFromState = false;
            double resClockX = 0, resClockY = 0;
            double resWeatherX = 0, resWeatherY = 0;
            double resCountdownX = 0, resCountdownY = 0;
            int resClockStyle = 0, resClockSize = 2, resClockColor = 0, resClockBlend = 0, resDateAlign = 1;
            bool resShowWeather = false, resShowCountdown = false;
            bool resDepth = false, resDepthH = true, resDepthC = true, resDepthM = true;
            if (state.ContainsKey("EdClockX"))
            {
                restoredFromState = true;
                resClockX = (double)state["EdClockX"];
                resClockY = (double)state["EdClockY"];
                resWeatherX = (double)state["EdWeatherX"];
                resWeatherY = (double)state["EdWeatherY"];
                resCountdownX = (double)state["EdCountdownX"];
                resCountdownY = (double)state["EdCountdownY"];
                resClockStyle = (int)state["EdClockStyle"];
                resClockSize = (int)state["EdClockSize"];
                resClockColor = (int)state["EdClockColor"];
                resClockBlend = (int)state["EdClockBlend"];
                resDateAlign = (int)state["EdDateAlign"];
                resShowWeather = (bool)state["EdShowWeather"];
                resShowCountdown = (bool)state["EdShowCountdown"];
                resDepth = (bool)state["EdDepth"];
                resDepthH = (bool)state["EdDepthH"];
                resDepthC = (bool)state["EdDepthC"];
                resDepthM = (bool)state["EdDepthM"];
                // Clean up
                string[] stateKeys = { "EdClockX","EdClockY","EdWeatherX","EdWeatherY","EdCountdownX","EdCountdownY",
                    "EdClockStyle","EdClockSize","EdClockColor","EdClockBlend","EdDateAlign",
                    "EdShowWeather","EdShowCountdown","EdDepth","EdDepthH","EdDepthC","EdDepthM" };
                foreach (var k in stateKeys) state.Remove(k);
            }

            // Check if we're editing a specific preset
            string presetStr;
            if (NavigationContext.QueryString.TryGetValue("preset", out presetStr))
            {
                int p;
                if (int.TryParse(presetStr, out p))
                    editingPreset = p;
            }

            if (!isLoading)
            {
                isLoading = true;

                // If editing a preset, load its saved settings first
                if (editingPreset >= 0)
                {
                    var s = IsolatedStorageSettings.ApplicationSettings;
                    foreach (var key in SetKeys)
                    {
                        string sk = "Set" + editingPreset + "_" + key;
                        if (s.Contains(sk)) s[key] = s[sk];
                    }
                    s.Save();

                    // Load per-preset wallpaper
                    try
                    {
                        using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                        {
                            string presetBg = "Background_" + editingPreset + ".jpg";
                            if (store.FileExists(presetBg))
                            {
                                // Copy preset wallpaper to Background.jpg for editor preview
                                if (store.FileExists("Background.jpg"))
                                    store.DeleteFile("Background.jpg");
                                store.CopyFile(presetBg, "Background.jpg");
                            }
                        }
                    }
                    catch { }
                }

                LoadAllSettings();
                LoadPreviewImages();

                // Override with restored state if returning from photo picker
                if (restoredFromState)
                {
                    clockX = resClockX; clockY = resClockY;
                    weatherX = resWeatherX; weatherY = resWeatherY;
                    countdownX = resCountdownX; countdownY = resCountdownY;
                    clockStyle = resClockStyle; clockSize = resClockSize;
                    clockColor = resClockColor; clockBlend = resClockBlend;
                    dateAlign = resDateAlign;
                    showWeather = resShowWeather; showCountdown = resShowCountdown;
                    useDepthEffect = resDepth;
                    depthHourBehind = resDepthH; depthColonBehind = resDepthC; depthMinuteBehind = resDepthM;
                    hasSavedPositions = true;
                }

                ApplyPreview();
                isLoading = false;

                if (!hasSavedPositions && ClockHandle.ActualWidth > 0)
                {
                    CenterElements();
                }
            }
        }

        protected override void OnBackKeyPress(System.ComponentModel.CancelEventArgs e)
        {
            if (UnsavedDialog.Visibility == Visibility.Visible)
            {
                // Dialog is showing, Cancel = close dialog, stay in editor
                e.Cancel = true;
                UnsavedDialog.Visibility = Visibility.Collapsed;
                return;
            }

            if (hasUnsavedChanges)
            {
                e.Cancel = true;
                UnsavedDialog.Visibility = Visibility.Visible;
                return;
            }
            base.OnBackKeyPress(e);
        }

        private void UnsavedDialog_Save(object sender, System.Windows.Input.GestureEventArgs e)
        {
            UnsavedDialog.Visibility = Visibility.Collapsed;
            SaveAndBack_Tap(null, null);
        }

        private void UnsavedDialog_DontSave(object sender, System.Windows.Input.GestureEventArgs e)
        {
            UnsavedDialog.Visibility = Visibility.Collapsed;
            hasUnsavedChanges = false;
            RestoreSettingsSnapshot();
            if (NavigationService.CanGoBack)
                NavigationService.GoBack();
        }

        private void UnsavedDialog_Cancel(object sender, System.Windows.Input.GestureEventArgs e)
        {
            UnsavedDialog.Visibility = Visibility.Collapsed;
        }

        #endregion

        #region Settings I/O

        private void LoadAllSettings()
        {
            var s = IsolatedStorageSettings.ApplicationSettings;

            clockStyle = Get(s, "ClockStyle", 0);
            clockSize = Get(s, "ClockSize", 2);
            clockColor = Get(s, "ClockColor", 0);
            clockBlend = Get(s, "ClockBlend", 0);
            dateAlign = Get(s, "DateAlign", 1);
            showWeather = Get(s, "ShowWeather", false);
            showCountdown = Get(s, "ShowCountdown", false);
            useDepthEffect = Get(s, "UseDepthEffect", false);
            depthHourBehind = Get(s, "DepthHourBehind", true);
            depthColonBehind = Get(s, "DepthColonBehind", true);
            depthMinuteBehind = Get(s, "DepthMinuteBehind", true);

            // Positions — check if user has saved free layout positions
            hasSavedPositions = s.Contains("ClockX");
            double defClockX, defClockY;
            ComputeDefaultClockPos(s, out defClockX, out defClockY);
            clockX = Get(s, "ClockX", defClockX);
            clockY = Get(s, "ClockY", defClockY);
            weatherX = Get(s, "WeatherX", defClockX);
            weatherY = Get(s, "WeatherY", defClockY + 155);
            countdownX = Get(s, "CountdownX", defClockX);
            countdownY = Get(s, "CountdownY", defClockY + 185);

            // Update UI controls
            FontLabel.Text = FontNames[Math.Min(clockStyle, FontNames.Length - 1)];
            UpdateSizeSelection();
            UpdateColorSelection();
            UpdateBlendSelection();
            UpdateDateAlignSelection();

            EdWeatherToggle.IsChecked = showWeather;
            EdWeatherCity.Text = Get<string>(s, "WeatherCity", "");
            EdCountdownToggle.IsChecked = showCountdown;
            EdCountdownName.Text = Get<string>(s, "CountdownName", "");
            if (s.Contains("CountdownTarget"))
                EdCountdownDate.Text = ((DateTime)s["CountdownTarget"]).ToString("yyyy-MM-dd");
            EdDepthToggle.IsChecked = useDepthEffect;
            EdDepthHour.IsChecked = depthHourBehind;
            EdDepthColon.IsChecked = depthColonBehind;
            EdDepthMinute.IsChecked = depthMinuteBehind;
            EdDepthLayers.Visibility = useDepthEffect ? Visibility.Visible : Visibility.Collapsed;

        }

        private void ComputeDefaultClockPos(IsolatedStorageSettings s, out double x, out double y)
        {
            int pos = Get(s, "ClockPosition", 1); // 0=Top,1=Center,2=Bottom
            int hAlign = Get(s, "ClockHAlign", 1); // 0=Left,1=Center,2=Right

            switch (pos)
            {
                case 0: y = 72; break;
                case 2: y = 460; break;
                default: y = 260; break;
            }
            switch (hAlign)
            {
                case 0: x = 24; break;
                case 2: x = 160; break;
                default: x = 70; break;
            }
        }

        private T Get<T>(IsolatedStorageSettings s, string key, T def)
        {
            if (s.Contains(key)) return (T)s[key];
            return def;
        }

        private void Save(string key, object val)
        {
            var s = IsolatedStorageSettings.ApplicationSettings;
            s[key] = val;
            s.Save();
            if (!isLoading) hasUnsavedChanges = true;
        }

        #endregion

        #region Preview

        private void LoadPreviewImages()
        {
            try
            {
                using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    if (store.FileExists("Background.jpg"))
                    {
                        using (var stream = store.OpenFile("Background.jpg",
                            FileMode.Open, FileAccess.Read))
                        {
                            var bmp = new BitmapImage();
                            bmp.SetSource(stream);
                            PreviewBgBrush.ImageSource = bmp;
                        }
                    }
                    if (useDepthEffect && store.FileExists("Foreground.png"))
                    {
                        using (var stream = store.OpenFile("Foreground.png",
                            FileMode.Open, FileAccess.Read))
                        {
                            var bmp = new BitmapImage();
                            bmp.SetSource(stream);
                            PreviewFgBrush.ImageSource = bmp;
                            PreviewFg.Visibility = Visibility.Visible;
                        }
                    }
                    else
                    {
                        PreviewFg.Visibility = Visibility.Collapsed;
                    }
                }
            }
            catch { }
        }

        private void ApplyPreview()
        {
            // Time
            var now = DateTime.Now;
            PHour.Text = now.Hour.ToString("D2");
            PMinute.Text = now.Minute.ToString("D2");
            PDay.Text = now.DayOfWeek.ToString();
            PDate.Text = now.ToString("MMMM d");

            // Font
            int fi = Math.Max(0, Math.Min(clockStyle, Fonts.Length - 1));
            PHour.FontFamily = Fonts[fi];
            PColon.FontFamily = Fonts[fi];
            PMinute.FontFamily = Fonts[fi];

            // Size
            int si = Math.Max(0, Math.Min(clockSize, SizeValues.Length - 1));
            int sz = SizeValues[si];
            PHour.FontSize = sz;
            PColon.FontSize = sz;
            PMinute.FontSize = sz;
            double pull = -sz * 0.16;
            PTimePanel.Margin = new Thickness(0, pull, 0, 0);

            // Color
            ApplyClockColor();

            // Date alignment
            ApplyDateAlign();

            // Positions
            ClockHandle.Margin = new Thickness(clockX, clockY, 0, 0);
            WeatherHandle.Margin = new Thickness(weatherX, weatherY, 0, 0);
            CountdownHandle.Margin = new Thickness(countdownX, countdownY, 0, 0);

            // Weather
            if (showWeather)
            {
                string cached = Get<string>(IsolatedStorageSettings.ApplicationSettings, "CachedWeather", "");
                PWeather.Text = string.IsNullOrEmpty(cached) ? "☀ 28°C" : cached;
                WeatherHandle.Visibility = Visibility.Visible;
            }
            else
            {
                WeatherHandle.Visibility = Visibility.Collapsed;
            }

            // Countdown
            if (showCountdown)
            {
                var s = IsolatedStorageSettings.ApplicationSettings;
                string name = Get<string>(s, "CountdownName", "Event");
                if (s.Contains("CountdownTarget"))
                {
                    var target = (DateTime)s["CountdownTarget"];
                    int days = (int)(target - DateTime.Today).TotalDays;
                    PCountdown.Text = "⏱ " + days + " days to " + name;
                }
                else
                {
                    PCountdown.Text = "⏱ " + name;
                }
                CountdownHandle.Visibility = Visibility.Visible;
            }
            else
            {
                CountdownHandle.Visibility = Visibility.Collapsed;
            }

            // Battery
            try
            {
                var bat = Windows.Phone.Devices.Power.Battery.GetDefault();
                PBattery.Text = "🔋 " + bat.RemainingChargePercent + "%";
            }
            catch
            {
                PBattery.Text = "🔋 --";
            }

            // Depth front layer: must run after layout pass
            Dispatcher.BeginInvoke(() => UpdateDepthFrontLayer());
        }

        private void UpdateDepthFrontLayer()
        {
            if (!useDepthEffect || PreviewFg.Visibility != Visibility.Visible)
            {
                FrontHour.Visibility = Visibility.Collapsed;
                FrontColon.Visibility = Visibility.Collapsed;
                FrontMinute.Visibility = Visibility.Collapsed;
                return;
            }

            // Hide originals that should be in front (they'll be replaced by front-layer copies)
            PHour.Opacity = depthHourBehind ? 1 : 0;
            PColon.Opacity = depthColonBehind ? 1 : 0;
            PMinute.Opacity = depthMinuteBehind ? 1 : 0;

            // Position front-layer copies using TransformToVisual
            PositionFrontText(FrontHour, PHour, !depthHourBehind);
            PositionFrontText(FrontColon, PColon, !depthColonBehind);
            PositionFrontText(FrontMinute, PMinute, !depthMinuteBehind);
        }

        private void PositionFrontText(TextBlock front, TextBlock source, bool show)
        {
            if (!show)
            {
                front.Visibility = Visibility.Collapsed;
                return;
            }
            try
            {
                var transform = source.TransformToVisual(PreviewArea);
                var pos = transform.Transform(new Point(0, 0));

                front.Text = source.Text;
                front.FontFamily = source.FontFamily;
                front.FontSize = source.FontSize;
                front.Foreground = source.Foreground;
                front.Visibility = Visibility.Visible;
                Canvas.SetLeft(front, pos.X);
                Canvas.SetTop(front, pos.Y);
            }
            catch
            {
                front.Visibility = Visibility.Collapsed;
            }
        }

        private void ApplyClockColor()
        {
            Brush brush;
            if (clockBlend > 0)
            {
                switch (clockBlend)
                {
                    case 1: brush = MakeGrad(Color.FromArgb(255, 255, 120, 50),
                                Color.FromArgb(255, 255, 60, 120)); break;  // Sunset
                    case 2: brush = MakeGrad(Color.FromArgb(255, 0, 180, 255),
                                Color.FromArgb(255, 0, 80, 200)); break;    // Ocean
                    case 3: brush = MakeGrad(Color.FromArgb(255, 0, 255, 150),
                                Color.FromArgb(255, 100, 0, 255)); break;   // Aurora
                    case 4: brush = MakeGrad(Color.FromArgb(255, 255, 0, 200),
                                Color.FromArgb(255, 0, 200, 255)); break;   // Neon
                    case 5: brush = MakeGrad(Color.FromArgb(255, 255, 105, 180),
                                Color.FromArgb(255, 148, 0, 211)); break;   // Rose
                    case 6: brush = MakeGrad(Color.FromArgb(255, 255, 50, 0),
                                Color.FromArgb(255, 255, 165, 0)); break;   // Fire
                    case 7: brush = MakeGrad(Color.FromArgb(255, 255, 255, 255),
                                Color.FromArgb(255, 173, 216, 230)); break; // Ice
                    case 8: brush = MakeGrad(Color.FromArgb(255, 50, 205, 50),
                                Color.FromArgb(255, 255, 255, 0)); break;   // Lime
                    case 9: brush = MakeGrad(Color.FromArgb(255, 75, 0, 130),
                                Color.FromArgb(255, 25, 25, 112)); break;   // Twilight
                    default: brush = new SolidColorBrush(Colors.White); break;
                }
            }
            else
            {
                switch (clockColor)
                {
                    case 1: brush = new SolidColorBrush(Color.FromArgb(255, 255, 215, 0)); break;   // Gold
                    case 2: brush = new SolidColorBrush(Color.FromArgb(255, 135, 206, 235)); break; // Sky Blue
                    case 3: brush = new SolidColorBrush(Color.FromArgb(255, 255, 182, 193)); break; // Pink
                    case 4: brush = new SolidColorBrush(Color.FromArgb(255, 255, 68, 68)); break;   // Red
                    case 5: brush = new SolidColorBrush(Color.FromArgb(255, 91, 255, 176)); break;  // Mint
                    case 6: brush = new SolidColorBrush(Color.FromArgb(255, 196, 167, 255)); break; // Lavender
                    case 7: brush = new SolidColorBrush(Color.FromArgb(255, 255, 140, 66)); break;  // Orange
                    case 8: brush = new SolidColorBrush(Color.FromArgb(255, 0, 229, 255)); break;   // Cyan
                    case 9: brush = new SolidColorBrush(Color.FromArgb(255, 160, 160, 176)); break; // Silver
                    default: brush = new SolidColorBrush(Colors.White); break;
                }
            }
            PHour.Foreground = brush;
            PColon.Foreground = brush;
            PMinute.Foreground = brush;
        }

        private LinearGradientBrush MakeGrad(Color from, Color to)
        {
            var lgb = new LinearGradientBrush();
            lgb.StartPoint = new Point(0, 0);
            lgb.EndPoint = new Point(0, 1);
            lgb.GradientStops.Add(new GradientStop { Color = from, Offset = 0 });
            lgb.GradientStops.Add(new GradientStop { Color = to, Offset = 1 });
            return lgb;
        }

        #endregion

        #region Drag & Drop

        private const double MAGNET = 12.0; // Magnetic attraction threshold (px)
        private const double SCREEN_W = 480.0;
        private const double SCREEN_H = 800.0;

        private void Element_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
        {
            var handle = (Border)sender;
            var ct = (CompositeTransform)handle.RenderTransform;
            ct.TranslateX += e.DeltaManipulation.Translation.X;
            ct.TranslateY += e.DeltaManipulation.Translation.Y;

            // Show live alignment guides while dragging
            double liveX = handle.Margin.Left + ct.TranslateX;
            double liveY = handle.Margin.Top + ct.TranslateY;
            double w = handle.ActualWidth;
            double h = handle.ActualHeight;
            ShowGuides(handle, liveX, liveY, w, h);

            e.Handled = true;
        }

        private void Element_ManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
        {
            var handle = (Border)sender;
            var ct = (CompositeTransform)handle.RenderTransform;

            // Compute new position (old margin + translation)
            double newX = handle.Margin.Left + ct.TranslateX;
            double newY = handle.Margin.Top + ct.TranslateY;

            // Magnetic snap to alignment points
            double w = handle.ActualWidth;
            double h = handle.ActualHeight;
            SnapToGuides(handle, ref newX, ref newY, w, h);

            // Clamp to screen bounds
            newX = Math.Max(0, Math.Min(SCREEN_W - w, newX));
            newY = Math.Max(0, Math.Min(SCREEN_H - h, newY));

            // Apply
            handle.Margin = new Thickness(newX, newY, 0, 0);
            ct.TranslateX = 0;
            ct.TranslateY = 0;

            // Hide guides
            GuideV.Visibility = Visibility.Collapsed;
            GuideH.Visibility = Visibility.Collapsed;

            // Update in-memory position (saved to settings only on Save tap)
            string tag = (string)handle.Tag;
            switch (tag)
            {
                case "Clock":    clockX = newX; clockY = newY; break;
                case "Weather":  weatherX = newX; weatherY = newY; break;
                case "Countdown": countdownX = newX; countdownY = newY; break;
            }
            hasUnsavedChanges = true;

            // Update depth front layer position after drag
            if (tag == "Clock")
                Dispatcher.BeginInvoke(() => UpdateDepthFrontLayer());

            e.Handled = true;
        }

        private void SnapToGuides(Border handle, ref double x, ref double y, double w, double h)
        {
            // ── Screen center guides ──
            double centerX = (SCREEN_W - w) / 2.0;   // X to center element horizontally
            double centerY = (SCREEN_H - h) / 2.0;   // Y to center element vertically

            if (Math.Abs(x - centerX) < MAGNET) x = centerX;
            if (Math.Abs(y - centerY) < MAGNET) y = centerY;

            // ── Left edge alignment (x=24, common padding) ──
            if (Math.Abs(x - 24) < MAGNET) x = 24;

            // ── Right edge alignment ──
            double rightEdge = SCREEN_W - 24 - w;
            if (Math.Abs(x - rightEdge) < MAGNET) x = rightEdge;

            // ── Align with other visible elements ──
            Border[] handles = { ClockHandle, WeatherHandle, CountdownHandle };
            foreach (var other in handles)
            {
                if (other == handle || other.Visibility != Visibility.Visible) continue;

                double ox = other.Margin.Left;
                double oy = other.Margin.Top;

                // Same X (left-align)
                if (Math.Abs(x - ox) < MAGNET) x = ox;

                // Same Y (top-align)
                if (Math.Abs(y - oy) < MAGNET) y = oy;

                // Stack below (align Y to bottom of other element)
                double belowY = oy + other.ActualHeight + 4;
                if (Math.Abs(y - belowY) < MAGNET) y = belowY;
            }
        }

        private void ShowGuides(Border handle, double x, double y, double w, double h)
        {
            double centerX = (SCREEN_W - w) / 2.0;
            double centerY = (SCREEN_H - h) / 2.0;
            bool showV = false, showH = false;
            double gx = 0, gy = 0;

            // Center guides
            if (Math.Abs(x - centerX) < MAGNET) { showV = true; gx = SCREEN_W / 2.0; }
            if (Math.Abs(y - centerY) < MAGNET) { showH = true; gy = SCREEN_H / 2.0; }

            // Left edge guide
            if (Math.Abs(x - 24) < MAGNET) { showV = true; gx = 24; }

            // Right edge guide
            double rightX = SCREEN_W - 24 - w;
            if (Math.Abs(x - rightX) < MAGNET) { showV = true; gx = SCREEN_W - 24; }

            // Element alignment guides
            Border[] handles = { ClockHandle, WeatherHandle, CountdownHandle };
            foreach (var other in handles)
            {
                if (other == handle || other.Visibility != Visibility.Visible) continue;
                double ox = other.Margin.Left;
                double oy = other.Margin.Top;

                if (Math.Abs(x - ox) < MAGNET) { showV = true; gx = ox; }
                if (Math.Abs(y - oy) < MAGNET) { showH = true; gy = oy; }
                double belowY = oy + other.ActualHeight + 4;
                if (Math.Abs(y - belowY) < MAGNET) { showH = true; gy = belowY; }
            }

            if (showV)
            {
                GuideV.Visibility = Visibility.Visible;
                GuideV.Margin = new Thickness(gx, 0, 0, 0);
            }
            else { GuideV.Visibility = Visibility.Collapsed; }

            if (showH)
            {
                GuideH.Visibility = Visibility.Visible;
                GuideH.Margin = new Thickness(0, gy, 0, 0);
            }
            else { GuideH.Visibility = Visibility.Collapsed; }
        }

        #endregion

        #region Element Selection

        private void Element_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            var handle = (Border)sender;
            string tag = (string)handle.Tag;
            SelectTab(tag);
            e.Handled = true;
        }

        private void SelectHandle(Border handle)
        {
            // Deselect previous
            if (selectedHandle != null)
                selectedHandle.BorderBrush = TransparentBrush;

            selectedHandle = handle;
            if (handle != null)
                handle.BorderBrush = SelectBrush;
        }

        private void SaveAndBack_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            // Save all current positions
            Save("ClockX", clockX);
            Save("ClockY", clockY);
            Save("WeatherX", weatherX);
            Save("WeatherY", weatherY);
            Save("CountdownX", countdownX);
            Save("CountdownY", countdownY);

            // Also save to preset slot if editing one
            if (editingPreset >= 0)
            {
                SaveSet(editingPreset);

                // Copy current Background.jpg to preset-specific file
                try
                {
                    using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                    {
                        if (store.FileExists("Background.jpg"))
                        {
                            string presetBg = "Background_" + editingPreset + ".jpg";
                            if (store.FileExists(presetBg))
                                store.DeleteFile(presetBg);
                            store.CopyFile("Background.jpg", presetBg);
                        }
                    }
                }
                catch { }
            }

            hasUnsavedChanges = false;

            // Navigate back
            if (NavigationService.CanGoBack)
                NavigationService.GoBack();
        }

        private void Background_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            SelectHandle(null);
        }

        #endregion

        #region Tab Navigation

        private void Tab_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            var border = (Border)sender;
            string tag = (string)border.Tag;
            SelectTab(tag);
        }

        private void SelectTab(string tab)
        {
            selectedTab = tab;

            // Update tab visuals
            SetTabActive(TabClock, TabClockText, tab == "Clock");
            SetTabActive(TabWeather, TabWeatherText, tab == "Weather");
            SetTabActive(TabCountdown, TabCountdownText, tab == "Countdown");
            SetTabActive(TabDisplay, TabDisplayText, tab == "Display");

            // Show corresponding properties
            ClockProps.Visibility = tab == "Clock" ? Visibility.Visible : Visibility.Collapsed;
            WeatherProps.Visibility = tab == "Weather" ? Visibility.Visible : Visibility.Collapsed;
            CountdownProps.Visibility = tab == "Countdown" ? Visibility.Visible : Visibility.Collapsed;
            DisplayProps.Visibility = tab == "Display" ? Visibility.Visible : Visibility.Collapsed;

            // Select corresponding handle on preview
            switch (tab)
            {
                case "Clock": SelectHandle(ClockHandle); break;
                case "Weather": SelectHandle(WeatherHandle); break;
                case "Countdown": SelectHandle(CountdownHandle); break;
                default: SelectHandle(null); break;
            }
        }

        private void SetTabActive(Border tab, TextBlock text, bool active)
        {
            tab.Background = active ? AccentBrush : InactiveTabBg;
            text.Foreground = active ?
                new SolidColorBrush(Colors.White) :
                new SolidColorBrush(Color.FromArgb(0xAA, 0xFF, 0xFF, 0xFF));
        }

        #endregion

        #region Clock Property Handlers

        private void FontPrev_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            clockStyle = (clockStyle - 1 + FontNames.Length) % FontNames.Length;
            Save("ClockStyle", clockStyle);
            FontLabel.Text = FontNames[clockStyle];
            int fi = Math.Max(0, Math.Min(clockStyle, Fonts.Length - 1));
            PHour.FontFamily = Fonts[fi];
            PColon.FontFamily = Fonts[fi];
            PMinute.FontFamily = Fonts[fi];
        }

        private void FontNext_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            clockStyle = (clockStyle + 1) % FontNames.Length;
            Save("ClockStyle", clockStyle);
            FontLabel.Text = FontNames[clockStyle];
            int fi = Math.Max(0, Math.Min(clockStyle, Fonts.Length - 1));
            PHour.FontFamily = Fonts[fi];
            PColon.FontFamily = Fonts[fi];
            PMinute.FontFamily = Fonts[fi];
        }

        private void Size_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            var border = (Border)sender;
            clockSize = int.Parse((string)border.Tag);
            Save("ClockSize", clockSize);
            UpdateSizeSelection();
            int sz = SizeValues[clockSize];
            PHour.FontSize = sz;
            PColon.FontSize = sz;
            PMinute.FontSize = sz;
            PTimePanel.Margin = new Thickness(0, -sz * 0.16, 0, 0);
        }

        private void UpdateSizeSelection()
        {
            Border[] pills = { SizeS, SizeM, SizeL, SizeXL, SizeXXL };
            for (int i = 0; i < pills.Length; i++)
            {
                pills[i].Background = (i == clockSize) ? AccentBrush : InactiveTabBg;
                ((TextBlock)pills[i].Child).Foreground = (i == clockSize) ?
                    new SolidColorBrush(Colors.White) :
                    new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF));
            }
        }

        private void Color_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            var el = (Ellipse)sender;
            clockColor = int.Parse((string)el.Tag);
            clockBlend = 0;
            Save("ClockColor", clockColor);
            Save("ClockBlend", 0);
            UpdateColorSelection();
            UpdateBlendSelection();
            ApplyClockColor();
        }

        private void UpdateColorSelection()
        {
            Ellipse[] circles = { ColorW, ColorG, ColorB, ColorP, ColorR, ColorMint, ColorLav, ColorOr, ColorCy, ColorSi };
            for (int i = 0; i < circles.Length; i++)
            {
                circles[i].Stroke = (i == clockColor && clockBlend == 0) ?
                    SelectBrush : TransparentBrush;
            }
        }

        private void Blend_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            var border = (Border)sender;
            clockBlend = int.Parse((string)border.Tag);
            Save("ClockBlend", clockBlend);
            UpdateBlendSelection();
            UpdateColorSelection();
            ApplyClockColor();
        }

        private void UpdateBlendSelection()
        {
            Border[] pills = { BlendNone, BlendSunset, BlendOcean, BlendAurora, BlendNeon, BlendRose, BlendFire, BlendIce, BlendLime, BlendTwilight };
            for (int i = 0; i < pills.Length; i++)
            {
                pills[i].Background = (i == clockBlend) ? AccentBrush : InactiveTabBg;
                ((TextBlock)pills[i].Child).Foreground = (i == clockBlend) ?
                    new SolidColorBrush(Colors.White) :
                    new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF));
            }
        }

        private void DateAlign_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            if (isLoading) return;
            var border = (Border)sender;
            dateAlign = int.Parse((string)border.Tag);
            Save("DateAlign", dateAlign);
            UpdateDateAlignSelection();
            ApplyDateAlign();
        }

        private void UpdateDateAlignSelection()
        {
            Border[] pills = { AlignLeft, AlignCenter, AlignRight };
            for (int i = 0; i < pills.Length; i++)
            {
                pills[i].Background = (i == dateAlign) ? AccentBrush : InactiveTabBg;
                ((TextBlock)pills[i].Child).Foreground = (i == dateAlign) ?
                    new SolidColorBrush(Colors.White) :
                    new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF));
            }
        }

        private void ApplyDateAlign()
        {
            switch (dateAlign)
            {
                case 0: PDatePanel.HorizontalAlignment = HorizontalAlignment.Left; break;
                case 2: PDatePanel.HorizontalAlignment = HorizontalAlignment.Right; break;
                default: PDatePanel.HorizontalAlignment = HorizontalAlignment.Center; break;
            }
        }

        #endregion

        #region Weather Handlers

        private void EdWeatherToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (isLoading) return;
            showWeather = EdWeatherToggle.IsChecked == true;
            Save("ShowWeather", showWeather);
            WeatherHandle.Visibility = showWeather ? Visibility.Visible : Visibility.Collapsed;
        }

        private void EdWeatherLocation_Click(object sender, RoutedEventArgs e)
        {
            string city = EdWeatherCity.Text.Trim();
            if (string.IsNullOrEmpty(city))
            {
                MessageBox.Show("Nhập tên thành phố.", "Thiếu thông tin", MessageBoxButton.OK);
                return;
            }
            WeatherStatus.Text = "⏳ Đang tìm...";
            // Geocode city name to lat/lon using open-meteo
            string url = string.Format(
                "https://geocoding-api.open-meteo.com/v1/search?name={0}&count=1&language=vi",
                Uri.EscapeDataString(city));
            var wc = new System.Net.WebClient();
            wc.DownloadStringCompleted += (s2, ev) =>
            {
                if (ev.Error != null)
                {
                    WeatherStatus.Text = "❌ Lỗi: " + ev.Error.Message;
                    return;
                }
                try
                {
                    string json = ev.Result;
                    // Parse lat/lon from {"results":[{"latitude":..., "longitude":..., "name":"..."}]}
                    double lat = ParseJsonDouble(json, "latitude");
                    double lon = ParseJsonDouble(json, "longitude");
                    string name = ParseJsonString(json, "name");
                    if (lat == 0 && lon == 0)
                    {
                        WeatherStatus.Text = "❌ Không tìm thấy: " + city;
                        return;
                    }
                    Save("WeatherLat", lat);
                    Save("WeatherLon", lon);
                    Save("WeatherCity", name);
                    EdWeatherCity.Text = name;
                    WeatherStatus.Text = "✅ " + name + " (" + lat.ToString("F2") + ", " + lon.ToString("F2") + ")";
                }
                catch { WeatherStatus.Text = "❌ Không tìm thấy: " + city; }
            };
            wc.DownloadStringAsync(new Uri(url));
        }

        private void EdWeatherGps_Click(object sender, RoutedEventArgs e)
        {
            WeatherStatus.Text = "📡 Đang lấy GPS...";
            var watcher = new System.Device.Location.GeoCoordinateWatcher(
                System.Device.Location.GeoPositionAccuracy.Default);
            watcher.StatusChanged += (s2, ev) =>
            {
                if (ev.Status == System.Device.Location.GeoPositionStatus.Disabled)
                {
                    Dispatcher.BeginInvoke(() =>
                        WeatherStatus.Text = "❌ GPS bị tắt. Bật Location trong Settings.");
                    watcher.Stop();
                }
            };
            watcher.PositionChanged += (s2, ev) =>
            {
                double lat = ev.Position.Location.Latitude;
                double lon = ev.Position.Location.Longitude;
                watcher.Stop();

                Dispatcher.BeginInvoke(() =>
                {
                    Save("WeatherLat", lat);
                    Save("WeatherLon", lon);
                    WeatherStatus.Text = "✅ GPS: " + lat.ToString("F4") + ", " + lon.ToString("F4");

                    // Reverse geocode to get city name
                    string url = string.Format(
                        "https://geocoding-api.open-meteo.com/v1/search?name={0},{1}&count=1&language=vi",
                        lat.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        lon.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    // Use nominatim for reverse geocoding
                    string revUrl = string.Format(
                        "https://nominatim.openstreetmap.org/reverse?format=json&lat={0}&lon={1}&accept-language=vi",
                        lat.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        lon.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    var wc = new System.Net.WebClient();
                    wc.Headers["User-Agent"] = "HyperOS/1.0";
                    wc.DownloadStringCompleted += (s3, ev3) =>
                    {
                        if (ev3.Error == null)
                        {
                            try
                            {
                                string city = ParseJsonString(ev3.Result, "city");
                                if (string.IsNullOrEmpty(city))
                                    city = ParseJsonString(ev3.Result, "town");
                                if (string.IsNullOrEmpty(city))
                                    city = ParseJsonString(ev3.Result, "state");
                                if (!string.IsNullOrEmpty(city))
                                {
                                    Save("WeatherCity", city);
                                    EdWeatherCity.Text = city;
                                    WeatherStatus.Text = "✅ " + city + " (" + lat.ToString("F2") + ", " + lon.ToString("F2") + ")";
                                }
                            }
                            catch { }
                        }
                    };
                    wc.DownloadStringAsync(new Uri(revUrl));
                });
            };
            watcher.Start();
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
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out val);
            return val;
        }

        private string ParseJsonString(string json, string key)
        {
            string search = "\"" + key + "\":\"";
            int idx = json.IndexOf(search);
            if (idx < 0) return "";
            idx += search.Length;
            int end = json.IndexOf("\"", idx);
            if (end < 0) return "";
            return json.Substring(idx, end - idx);
        }

        #endregion

        #region Countdown Handlers

        private void EdCountdownToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (isLoading) return;
            showCountdown = EdCountdownToggle.IsChecked == true;
            Save("ShowCountdown", showCountdown);
            CountdownHandle.Visibility = showCountdown ? Visibility.Visible : Visibility.Collapsed;
        }

        private void EdSaveCountdown_Click(object sender, RoutedEventArgs e)
        {
            Save("CountdownName", EdCountdownName.Text.Trim());
            DateTime target;
            if (DateTime.TryParse(EdCountdownDate.Text.Trim(), out target))
            {
                Save("CountdownTarget", target);
                string name = EdCountdownName.Text.Trim();
                int days = (int)(target - DateTime.Today).TotalDays;
                PCountdown.Text = "⏱ " + days + " days to " + name;
                MessageBox.Show("Countdown saved!", "Saved", MessageBoxButton.OK);
            }
            else
            {
                MessageBox.Show("Invalid date. Use yyyy-MM-dd", "Error", MessageBoxButton.OK);
            }
        }

        #endregion

        #region Display Handlers

        private void EdWallpaper_Click(object sender, RoutedEventArgs e)
        {
            // Save ALL current editor state to transient state before tombstoning
            var state = Microsoft.Phone.Shell.PhoneApplicationService.Current.State;
            state["EdClockX"] = clockX; state["EdClockY"] = clockY;
            state["EdWeatherX"] = weatherX; state["EdWeatherY"] = weatherY;
            state["EdCountdownX"] = countdownX; state["EdCountdownY"] = countdownY;
            state["EdClockStyle"] = clockStyle; state["EdClockSize"] = clockSize;
            state["EdClockColor"] = clockColor; state["EdClockBlend"] = clockBlend;
            state["EdDateAlign"] = dateAlign;
            state["EdShowWeather"] = showWeather; state["EdShowCountdown"] = showCountdown;
            state["EdDepth"] = useDepthEffect;
            state["EdDepthH"] = depthHourBehind; state["EdDepthC"] = depthColonBehind; state["EdDepthM"] = depthMinuteBehind;

            var chooser = new PhotoChooserTask();
            chooser.ShowCamera = true;
            chooser.Completed += (s, args) =>
            {
                if (args.TaskResult == TaskResult.OK && args.ChosenPhoto != null)
                {
                    try
                    {
                        var bmp = new BitmapImage();
                        bmp.SetSource(args.ChosenPhoto);
                        var wb = new WriteableBitmap(bmp);
                        using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                        using (var stream = store.CreateFile("Background.jpg"))
                        {
                            wb.SaveJpeg(stream, wb.PixelWidth, wb.PixelHeight, 0, 90);
                        }
                        PreviewBgBrush.ImageSource = bmp;
                        hasUnsavedChanges = true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButton.OK);
                    }
                }
            };
            chooser.Show();
        }

        private void EdDepthToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (isLoading) return;
            useDepthEffect = EdDepthToggle.IsChecked == true;
            Save("UseDepthEffect", useDepthEffect);
            EdDepthLayers.Visibility = useDepthEffect ? Visibility.Visible : Visibility.Collapsed;
            LoadPreviewImages();
        }

        private void EdForeground_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.FileTypeFilter.Add(".png");
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            picker.PickSingleFileAndContinue();
        }

        private void EdAutoExtract_Click(object sender, RoutedEventArgs e)
        {
            // Check if Background.jpg exists
            try
            {
                using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    if (!store.FileExists("Background.jpg"))
                    {
                        MessageBox.Show("Hãy chọn ảnh nền trước khi tách foreground.", "Chưa có ảnh nền", MessageBoxButton.OK);
                        return;
                    }
                }
            }
            catch { return; }

            EdAutoExtractBtn.IsEnabled = false;
            ExtractStatus.Text = "⏳ Đang tách vật thể...";

            // Read Background.jpg bytes
            byte[] imageBytes;
            try
            {
                using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                using (var stream = store.OpenFile("Background.jpg", FileMode.Open, FileAccess.Read))
                {
                    imageBytes = new byte[stream.Length];
                    stream.Read(imageBytes, 0, imageBytes.Length);
                }
            }
            catch (Exception ex)
            {
                ExtractStatus.Text = "❌ Lỗi đọc ảnh: " + ex.Message;
                EdAutoExtractBtn.IsEnabled = true;
                return;
            }

            // WP8.1 handles TLS 1.2 at OS level

            // Build multipart request
            string boundary = "----HyperOS" + DateTime.Now.Ticks.ToString("x");
            string apiKey = Get<string>(IsolatedStorageSettings.ApplicationSettings, "RemoveBgApiKey", "").Trim();
            if (string.IsNullOrEmpty(apiKey))
            {
                ExtractStatus.Text = "❌ Nhập API key trước!";
                EdAutoExtractBtn.IsEnabled = true;
                return;
            }

            var request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create("https://api.remove.bg/v1.0/removebg");
            request.Method = "POST";
            request.ContentType = "multipart/form-data; boundary=" + boundary;
            request.Headers["X-Api-Key"] = apiKey;

            request.BeginGetRequestStream(reqResult =>
            {
                try
                {
                    using (var reqStream = request.EndGetRequestStream(reqResult))
                    {
                        var encoding = System.Text.Encoding.UTF8;

                        // File part
                        string fileHeader = "--" + boundary + "\r\n" +
                            "Content-Disposition: form-data; name=\"image_file\"; filename=\"background.jpg\"\r\n" +
                            "Content-Type: image/jpeg\r\n\r\n";
                        byte[] headerBytes = encoding.GetBytes(fileHeader);
                        reqStream.Write(headerBytes, 0, headerBytes.Length);
                        reqStream.Write(imageBytes, 0, imageBytes.Length);

                        // Size param
                        string sizeParam = "\r\n--" + boundary + "\r\n" +
                            "Content-Disposition: form-data; name=\"size\"\r\n\r\nauto";

                        byte[] sizeBytes = encoding.GetBytes(sizeParam);
                        reqStream.Write(sizeBytes, 0, sizeBytes.Length);

                        // End boundary
                        byte[] endBytes = encoding.GetBytes("\r\n--" + boundary + "--\r\n");
                        reqStream.Write(endBytes, 0, endBytes.Length);
                    }

                    request.BeginGetResponse(resResult =>
                    {
                        try
                        {
                            using (var response = request.EndGetResponse(resResult))
                            using (var resStream = response.GetResponseStream())
                            using (var ms = new MemoryStream())
                            {
                                resStream.CopyTo(ms);
                                byte[] pngBytes = ms.ToArray();

                                // Save to IsolatedStorage
                                using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                                using (var file = store.CreateFile("Foreground.png"))
                                {
                                    file.Write(pngBytes, 0, pngBytes.Length);
                                }

                                // Update UI on dispatcher
                                Dispatcher.BeginInvoke(() =>
                                {
                                    ExtractStatus.Text = "✅ Tách thành công!";
                                    EdAutoExtractBtn.IsEnabled = true;
                                    hasUnsavedChanges = true;

                                    // Auto-enable depth effect
                                    if (!useDepthEffect)
                                    {
                                        useDepthEffect = true;
                                        Save("UseDepthEffect", true);
                                        EdDepthToggle.IsChecked = true;
                                        EdDepthLayers.Visibility = Visibility.Visible;
                                    }

                                    LoadPreviewImages();
                                });
                            }
                        }
                        catch (System.Net.WebException wex)
                        {
                            string errMsg = "Lỗi API";
                            try
                            {
                                if (wex.Response != null)
                                    using (var errStream = wex.Response.GetResponseStream())
                                    using (var reader = new StreamReader(errStream))
                                        errMsg = reader.ReadToEnd();
                            }
                            catch { }

                            Dispatcher.BeginInvoke(() =>
                            {
                                ExtractStatus.Text = "❌ " + errMsg;
                                EdAutoExtractBtn.IsEnabled = true;
                            });
                        }
                    }, null);
                }
                catch (Exception ex)
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        ExtractStatus.Text = "❌ " + ex.Message;
                        EdAutoExtractBtn.IsEnabled = true;
                    });
                }
            }, null);
        }

        private void EdDepthLayer_Changed(object sender, RoutedEventArgs e)
        {
            if (isLoading) return;
            depthHourBehind = EdDepthHour.IsChecked == true;
            depthColonBehind = EdDepthColon.IsChecked == true;
            depthMinuteBehind = EdDepthMinute.IsChecked == true;
            Save("DepthHourBehind", depthHourBehind);
            Save("DepthColonBehind", depthColonBehind);
            Save("DepthMinuteBehind", depthMinuteBehind);
            UpdateDepthFrontLayer();
        }

        #endregion

        #region My Sets

        // My Sets
        private static readonly string[] SetKeys = { "ClockStyle", "ClockPosition", "ClockHAlign",
            "ClockColor", "ClockBlend", "ClockSize",
            "ShowWeather", "ShowCountdown", "UseDepthEffect",
            "DepthHourBehind", "DepthColonBehind", "DepthMinuteBehind",
            "ClockX", "ClockY", "WeatherX", "WeatherY", "CountdownX", "CountdownY",
            "bIsAnimOn", "DateAlign", "CountdownName", "CountdownTarget", "OwnerInfo" };

        private void TakeSettingsSnapshot()
        {
            settingsSnapshot = new System.Collections.Generic.Dictionary<string, object>();
            var s = IsolatedStorageSettings.ApplicationSettings;
            foreach (var key in SetKeys)
            {
                if (s.Contains(key))
                    settingsSnapshot[key] = s[key];
            }
        }

        private void RestoreSettingsSnapshot()
        {
            if (settingsSnapshot == null) return;
            var s = IsolatedStorageSettings.ApplicationSettings;
            foreach (var key in SetKeys)
            {
                if (settingsSnapshot.ContainsKey(key))
                    s[key] = settingsSnapshot[key];
                else if (s.Contains(key))
                    s.Remove(key);
            }
            s.Save();
        }

        private void SaveSet(int n)
        {
            var s = IsolatedStorageSettings.ApplicationSettings;
            foreach (var key in SetKeys)
                if (s.Contains(key)) s["Set" + n + "_" + key] = s[key];
            s.Save();
            MessageBox.Show("Set " + n + " saved!", "My Sets", MessageBoxButton.OK);
        }

        private void LoadSet(int n)
        {
            var s = IsolatedStorageSettings.ApplicationSettings;
            bool found = false;
            foreach (var key in SetKeys)
            {
                string sk = "Set" + n + "_" + key;
                if (s.Contains(sk)) { s[key] = s[sk]; found = true; }
            }
            if (found)
            {
                s.Save();
                isLoading = true;
                LoadAllSettings();
                ApplyPreview();
                isLoading = false;
            }
            else
            {
                if (MessageBox.Show("No set saved. Save current?", "My Sets",
                    MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                    SaveSet(n);
            }
        }

        #endregion

        #region Navigation

        private void Back_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            if (hasUnsavedChanges)
            {
                UnsavedDialog.Visibility = Visibility.Visible;
                return;
            }
            if (NavigationService.CanGoBack)
                NavigationService.GoBack();
        }

        #endregion
    }
}
