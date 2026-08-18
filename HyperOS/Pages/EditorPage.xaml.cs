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
using HyperOS.Helpers;

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
        private int clockLayout = 0; // 0=Horizontal, 1=Vertical, 2=Analog Minimal, 3=Analog Classic, 4=Analog Swiss
        private double clockOpacity = 1.0;
        private int clockHue = -1;
        private bool isUpdatingColorUI;

        // Widget settings
        private bool showWeather;
        private bool showCountdown;

        // Signature
        private bool showSignature = false;
        private string sigText = "";
        private int sigFontIndex = 0;
        private double sigSpacing = 0;
        private int sigAlign = 1;
        private int sigLayout = 0; // 0=Horizontal, 1=Vertical
        private int sigColor = 0;
        private int sigBlend = 0;
        private double sigOpacity = 1.0;
        private int sigHue = -1;
        private double signatureX = 24;
        private double signatureY = 120;

        // Depth
        private bool useDepthEffect;
        private bool depthHourBehind = true;
        private bool depthColonBehind = true;
        private bool depthMinuteBehind = true;

        // Filters
        private bool useMatte = false;
        private bool useRibbed = false;

        // Font & size arrays — redirect to ClockRenderer shared data (RAM: avoids duplicate static arrays)
        private static string[] FontNames { get { return ClockRenderer.FontNames; } }
        private static FontFamily[] Fonts { get { return ClockRenderer.Fonts; } }
        private static int[] SizeValues { get { return ClockRenderer.SizeValues; } }

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

        private PhotoChooserTask photoChooser;

        public EditorPage()
        {
            InitializeComponent();
            photoChooser = new PhotoChooserTask();
            photoChooser.ShowCamera = true;
            photoChooser.Completed += PhotoChooser_Completed;
        }

        private void PhotoChooser_Completed(object sender, PhotoResult args)
        {
            if (args.TaskResult == TaskResult.OK && args.ChosenPhoto != null)
            {
                try
                {
                    // Copy directly to avoid OOM and quality loss
                    using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                    using (var stream = store.CreateFile("Background.jpg"))
                    {
                        args.ChosenPhoto.Position = 0;
                        args.ChosenPhoto.CopyTo(stream);
                    }
                    
                    // Reload for preview
                    args.ChosenPhoto.Position = 0;
                    var bmp = new BitmapImage();
                    bmp.DecodePixelWidth = 480; // Optimize RAM for preview
                    bmp.SetSource(args.ChosenPhoto);
                    PreviewBgBrush.ImageSource = bmp;
                    hasUnsavedChanges = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButton.OK);
                }
            }
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
            int resClockLayout = 0;
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
                if (state.ContainsKey("EdClockLayout")) resClockLayout = (int)state["EdClockLayout"];
                resShowWeather = (bool)state["EdShowWeather"];
                resShowCountdown = (bool)state["EdShowCountdown"];
                resDepth = (bool)state["EdDepth"];
                resDepthH = (bool)state["EdDepthH"];
                resDepthC = (bool)state["EdDepthC"];
                resDepthM = (bool)state["EdDepthM"];
                // Clean up
                string[] stateKeys = { "EdClockX","EdClockY","EdWeatherX","EdWeatherY","EdCountdownX","EdCountdownY",
                    "EdClockStyle","EdClockSize","EdClockColor","EdClockBlend","EdDateAlign","EdClockLayout",
                    "EdShowWeather","EdShowCountdown","EdDepth","EdDepthH","EdDepthC","EdDepthM" };
                foreach (var k in stateKeys) state.Remove(k);
            }

            // Check if we're editing a specific preset (always, even on first load)
            string presetStr;
            if (NavigationContext.QueryString.TryGetValue("preset", out presetStr))
            {
                int p;
                if (int.TryParse(presetStr, out p))
                    editingPreset = p;
            }

            // Load preset wallpaper to Background.jpg BEFORE Loaded fires LoadPreviewImages
            if (e.NavigationMode == System.Windows.Navigation.NavigationMode.New && editingPreset >= 0)
            {
                var s = IsolatedStorageSettings.ApplicationSettings;

                // Copy Set{n}_ keys to global keys
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
                        string presetBgFile = "Background_" + editingPreset + ".jpg";
                        if (store.FileExists(presetBgFile))
                        {
                            // Copy preset wallpaper to Background.jpg for editor preview
                            if (store.FileExists("Background.jpg"))
                                store.DeleteFile("Background.jpg");
                            store.CopyFile(presetBgFile, "Background.jpg");
                        }
                        else
                        {
                            // No saved wallpaper yet — copy default from app resources
                            string defaultBg = s.Contains("Set" + editingPreset + "_BackgroundImage")
                                ? (string)s["Set" + editingPreset + "_BackgroundImage"]
                                : null;

                            if (!string.IsNullOrEmpty(defaultBg))
                            {
                                try
                                {
                                    // Strip leading '/' — GetResourceStream needs relative path without it
                                    string resPath = defaultBg.TrimStart('/');
                                    // Load from app resources and save to IsolatedStorage
                                    var sri = Application.GetResourceStream(new Uri(resPath, UriKind.Relative));
                                    if (sri != null && sri.Stream != null)
                                    {
                                        if (store.FileExists("Background.jpg"))
                                            store.DeleteFile("Background.jpg");
                                        using (var iso = store.OpenFile("Background.jpg", FileMode.Create, FileAccess.Write))
                                        {
                                            sri.Stream.CopyTo(iso);
                                        }
                                        sri.Stream.Dispose();
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }
                catch { }
            }

            if (!isLoading)
            {
                isLoading = true;

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
                    dateAlign = resDateAlign; clockLayout = resClockLayout;
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

        private async void UnsavedDialog_DontSave(object sender, System.Windows.Input.GestureEventArgs e)
        {
            UnsavedDialog.Visibility = Visibility.Collapsed;
            hasUnsavedChanges = false;
            RestoreSettingsSnapshot();
            await RestoreOriginalBackgroundAsync(true);
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
            clockOpacity = Get(s, "ClockOpacity", 1.0);
            clockHue = Get(s, "ClockHue", -1);
            dateAlign = Get(s, "DateAlign", 1);
            clockLayout = Get(s, "ClockLayout", 0);
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
            UpdateLayoutSelection();

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

            showSignature = Get(s, "ShowSignature", false);
            sigText = Get(s, "SignatureText", "LIFE");
            sigFontIndex = Get(s, "SignatureFont", 4); // Default to Playfair
            sigSpacing = Get(s, "SignatureSpacing", 500.0);
            sigAlign = Get(s, "SignatureAlign", 1); // Default to Top-Center
            sigLayout = Get(s, "SignatureLayout", 0);
            sigColor = Get(s, "SignatureColor", 0);
            sigBlend = Get(s, "SignatureBlend", 0);
            sigOpacity = Get(s, "SignatureOpacity", 1.0);
            sigHue = Get(s, "SignatureHue", -1);

            signatureX = Get(s, "SignatureX", defClockX);
            signatureY = Get(s, "SignatureY", defClockY + 220);

            EdSignatureToggle.IsChecked = showSignature;
            EdSignatureText.Text = sigText;
            SigFontLabel.Text = FontNames[Math.Min(sigFontIndex, FontNames.Length - 1)];
            EdSigSpacing.Value = sigSpacing;
            UpdateSigAlignSelection();
            UpdateSigLayoutSelection();
            // Unified color selections updated on demand
            EdDepthClock.IsChecked = depthHourBehind; // For analog: entire clock behind
            EdDepthLayers.Visibility = useDepthEffect ? Visibility.Visible : Visibility.Collapsed;
            UpdateDepthRowVisibility();

            useMatte = Get(s, "UseMatte", false);
            useRibbed = Get(s, "UseRibbed", false);
            if (MatteToggle != null) MatteToggle.IsChecked = useMatte;
            if (RibbedToggle != null) RibbedToggle.IsChecked = useRibbed;

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
                    string bgToLoad = (useMatte || useRibbed) && store.FileExists("Background_Filtered.jpg") ? "Background_Filtered.jpg" : "Background.jpg";
                    if (store.FileExists(bgToLoad))
                    {
                        using (var stream = store.OpenFile(bgToLoad,
                            FileMode.Open, FileAccess.Read))
                        {
                            var bmp = new BitmapImage();
                            bmp.DecodePixelWidth = 480; // RAM: match preview width
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
                            bmp.DecodePixelWidth = 480; // RAM: match preview width
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
            string hStr = now.Hour.ToString("D2");
            string mStr = now.Minute.ToString("D2");
            PHour.Text = hStr;
            PMinute.Text = mStr;
            PDay.Text = now.DayOfWeek.ToString();
            PDate.Text = now.ToString("MMMM d");

            bool isAnalog = clockLayout >= 2 && clockLayout <= 4;
            bool isVertical = clockLayout == 1;
            bool isRhombus = clockLayout == 5;
            bool isGiant = clockLayout == 6;

            // Update Rhombus digits
            PRhombusH1.Text = hStr[0].ToString();
            PRhombusH2.Text = hStr[1].ToString();
            PRhombusM1.Text = mStr[0].ToString();
            PRhombusM2.Text = mStr[1].ToString();

            // Show/hide digital vs analog vs rhombus
            if (isAnalog)
            {
                PTimePanel.Visibility = Visibility.Collapsed;
                PRhombusGrid.Visibility = Visibility.Collapsed;
                PAnalogClock.Visibility = Visibility.Visible;
            }
            else if (isRhombus)
            {
                PTimePanel.Visibility = Visibility.Collapsed;
                PAnalogClock.Visibility = Visibility.Collapsed;
                PRhombusGrid.Visibility = Visibility.Visible;
            }
            else
            {
                PTimePanel.Visibility = Visibility.Visible;
                PRhombusGrid.Visibility = Visibility.Collapsed;
                PAnalogClock.Visibility = Visibility.Collapsed;
            }

            // Vertical / Giant: stack vertically, hide colon
            PColon.Visibility = (isVertical || isGiant) ? Visibility.Collapsed : Visibility.Visible;

            if (isVertical)
            {
                PTimePanel.Orientation = System.Windows.Controls.Orientation.Vertical;
                PHour.HorizontalAlignment = HorizontalAlignment.Center;
                PMinute.HorizontalAlignment = HorizontalAlignment.Center;
            }
            else
            {
                PTimePanel.Orientation = System.Windows.Controls.Orientation.Horizontal;
                PHour.HorizontalAlignment = HorizontalAlignment.Left;
                PMinute.HorizontalAlignment = HorizontalAlignment.Left;
            }

            // Font (applies to digital modes only)
            int fi = Math.Max(0, Math.Min(clockStyle, Fonts.Length - 1));
            var font = Fonts[fi];
            PHour.FontFamily = font;
            PColon.FontFamily = font;
            PMinute.FontFamily = font;
            PRhombusH1.FontFamily = font;
            PRhombusH2.FontFamily = font;
            PRhombusM1.FontFamily = font;
            PRhombusM2.FontFamily = font;

            // Size
            int si = Math.Max(0, Math.Min(clockSize, SizeValues.Length - 1));
            int sz = SizeValues[si];
            
            double hourSz = sz;
            double minuteSz = sz;
            if (isGiant) { hourSz = sz * 1.6; minuteSz = sz * 1.6; }
            
            PHour.FontSize = hourSz;
            PColon.FontSize = sz;
            PMinute.FontSize = minuteSz;
            
            double rhombSz = sz * 1.2;
            PRhombusH1.FontSize = rhombSz;
            PRhombusH2.FontSize = rhombSz;
            PRhombusM1.FontSize = rhombSz;
            PRhombusM2.FontSize = rhombSz;

            if (isVertical)
            {
                PTimePanel.Margin = new Thickness(0, -sz * 0.22, 0, 0);
                PMinute.Margin = new Thickness(0, -sz * 0.35, 0, 0);
            }
            else if (isGiant)
            {
                PTimePanel.Margin = new Thickness(0, -sz * 0.25, 0, 0);
                PMinute.Margin = new Thickness(sz * 0.1, 0, 0, 0);
            }
            else
            {
                PTimePanel.Margin = new Thickness(0, -sz * 0.16, 0, 0);
                PMinute.Margin = new Thickness(0, 0, 0, 0);
            }

            if (isRhombus)
            {
                PRhombusGrid.Margin = new Thickness(0, -rhombSz * 0.15, 0, 0);
                PRhombusH1.Margin = new Thickness(0, 0, 0, -rhombSz * 0.1);
                PRhombusH2.Margin = new Thickness(0, 0, -rhombSz * 0.05, 0);
                PRhombusM1.Margin = new Thickness(-rhombSz * 0.05, 0, 0, 0);
                PRhombusM2.Margin = new Thickness(0, -rhombSz * 0.1, 0, 0);
            }

            // Color
            ApplyClockColor();

            if (isAnalog)
            {
                PAnalogClock.Children.Clear();
                // Scale analog clock based on size setting
                double diameter = sz * 1.6;
                PAnalogClock.Width = diameter;
                PAnalogClock.Height = diameter;
                DrawAnalogClock(PAnalogClock, diameter, now.Hour, now.Minute, clockLayout);
            }

            // Color options for analog clock are not restricted currently
            // Date alignment
            ApplyDateAlign();

            // Hide font section for analog, show for digital
            FontSection.Visibility = isAnalog ? Visibility.Collapsed : Visibility.Visible;

            // Positions
            ClockHandle.Margin = new Thickness(clockX, clockY, 0, 0);
            WeatherHandle.Margin = new Thickness(weatherX, weatherY, 0, 0);
            CountdownHandle.Margin = new Thickness(countdownX, countdownY, 0, 0);

            // Signature Preview
            SignatureHandle.Visibility = showSignature ? Visibility.Visible : Visibility.Collapsed;
            if (showSignature)
            {
                PSignature.Text = sigLayout == 1 ? string.Join("\n", sigText.ToCharArray()) : sigText;
                PSignature.FontFamily = Fonts[Math.Max(0, Math.Min(sigFontIndex, Fonts.Length - 1))];
                PSignature.CharacterSpacing = (int)sigSpacing;
                if (sigLayout == 1)
                {
                    PSignature.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
                    PSignature.LineHeight = 48 + (sigSpacing / 20.0);
                }
                else
                {
                    PSignature.LineHeight = 0;
                }
                SignatureHandle.Margin = new Thickness(signatureX, signatureY, 0, 0);
                ApplySignatureColor();
                PSignature.Opacity = sigOpacity;
            }

            ClockHandle.Opacity = clockOpacity;

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

        /// <summary>
        /// Draws an analog clock face on the given canvas.
        /// style: 2=Minimal (hands only), 3=Classic (12/3/6/9), 4=Swiss (tick marks)
        /// </summary>
        private void DrawAnalogClock(Canvas canvas, double diameter, int hour, int minute, int style)
        {
            Brush colorBrush = PHour.Foreground;
            ClockRenderer.DrawAnalogClock(canvas, diameter, hour, minute, style, colorBrush);
        }

        private void UpdateDepthFrontLayer()
        {
            if (!useDepthEffect || PreviewFg.Visibility != Visibility.Visible)
            {
                FrontHour.Visibility = Visibility.Collapsed;
                FrontColon.Visibility = Visibility.Collapsed;
                FrontMinute.Visibility = Visibility.Collapsed;
                FrontDate.Visibility = Visibility.Collapsed;
                FrontRhombusH1.Visibility = Visibility.Collapsed;
                FrontRhombusH2.Visibility = Visibility.Collapsed;
                FrontRhombusM1.Visibility = Visibility.Collapsed;
                FrontRhombusM2.Visibility = Visibility.Collapsed;
                FrontRhombusDot.Visibility = Visibility.Collapsed;
                // Restore originals
                PHour.Opacity = 1;
                PColon.Opacity = 1;
                PMinute.Opacity = 1;
                PDatePanel.Opacity = 1;
                PAnalogClock.Opacity = 1;
                PRhombusH1.Opacity = 1;
                PRhombusH2.Opacity = 1;
                PRhombusM1.Opacity = 1;
                PRhombusM2.Opacity = 1;
                PRhombusCenterDot.Opacity = 1;
                return;
            }

            bool isAnalog = clockLayout >= 2 && clockLayout <= 4;
            bool isRhombus = clockLayout == 5;

            if (isAnalog)
            {
                // Analog: entire clock behind foreground
                FrontHour.Visibility = Visibility.Collapsed;
                FrontColon.Visibility = Visibility.Collapsed;
                FrontMinute.Visibility = Visibility.Collapsed;
                FrontRhombusH1.Visibility = Visibility.Collapsed;
                FrontRhombusH2.Visibility = Visibility.Collapsed;
                FrontRhombusM1.Visibility = Visibility.Collapsed;
                FrontRhombusM2.Visibility = Visibility.Collapsed;
                FrontRhombusDot.Visibility = Visibility.Collapsed;
                PHour.Opacity = 1;
                PColon.Opacity = 1;
                PMinute.Opacity = 1;
                PAnalogClock.Opacity = 1; // analog stays behind foreground naturally
                PDatePanel.Opacity = 0; // Date always in front
            }
            else
            {
                PAnalogClock.Opacity = 1;
                bool isVertical = clockLayout == 1;

                // Hide originals that should be in front (they'll be replaced by front-layer copies)
                PHour.Opacity = depthHourBehind ? 1 : 0;
                PColon.Opacity = (isVertical || depthColonBehind) ? 1 : 0;
                PMinute.Opacity = depthMinuteBehind ? 1 : 0;
                
                PRhombusH1.Opacity = depthHourBehind ? 1 : 0;
                PRhombusH2.Opacity = depthHourBehind ? 1 : 0;
                PRhombusM1.Opacity = depthMinuteBehind ? 1 : 0;
                PRhombusM2.Opacity = depthMinuteBehind ? 1 : 0;
                PRhombusCenterDot.Opacity = depthColonBehind ? 1 : 0;

                PDatePanel.Opacity = 0; // Date always in front

                // Position front-layer copies using TransformToVisual
                if (isRhombus)
                {
                    FrontHour.Visibility = Visibility.Collapsed;
                    FrontColon.Visibility = Visibility.Collapsed;
                    FrontMinute.Visibility = Visibility.Collapsed;
                }
                else
                {
                    PositionFrontText(FrontHour, PHour, !depthHourBehind);
                    // Vertical and Giant: no colon — always hide FrontColon
                    if (isVertical || clockLayout == 6)
                        FrontColon.Visibility = Visibility.Collapsed;
                    else
                        PositionFrontText(FrontColon, PColon, !depthColonBehind);
                    PositionFrontText(FrontMinute, PMinute, !depthMinuteBehind);
                }
                
                if (isRhombus)
                {
                    PositionFrontText(FrontRhombusH1, PRhombusH1, !depthHourBehind);
                    PositionFrontText(FrontRhombusH2, PRhombusH2, !depthHourBehind);
                    PositionFrontText(FrontRhombusM1, PRhombusM1, !depthMinuteBehind);
                    PositionFrontText(FrontRhombusM2, PRhombusM2, !depthMinuteBehind);
                    
                    if (!depthColonBehind)
                    {
                        try
                        {
                            var transform = PRhombusCenterDot.TransformToVisual(PreviewArea);
                            var pos = transform.Transform(new Point(0, 0));
                            FrontRhombusDot.Visibility = Visibility.Visible;
                            Canvas.SetLeft(FrontRhombusDot, pos.X);
                            Canvas.SetTop(FrontRhombusDot, pos.Y);
                        }
                        catch { FrontRhombusDot.Visibility = Visibility.Collapsed; }
                    }
                    else
                    {
                        FrontRhombusDot.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    FrontRhombusH1.Visibility = Visibility.Collapsed;
                    FrontRhombusH2.Visibility = Visibility.Collapsed;
                    FrontRhombusM1.Visibility = Visibility.Collapsed;
                    FrontRhombusM2.Visibility = Visibility.Collapsed;
                    FrontRhombusDot.Visibility = Visibility.Collapsed;
                }
            }

            // Date always in front
            try
            {
                var dt = PDatePanel.TransformToVisual(PreviewArea);
                var dp = dt.Transform(new Point(0, 0));
                FrontDay.Text = PDay.Text;
                FrontDateText.Text = PDate.Text;
                FrontDate.Visibility = Visibility.Visible;
                Canvas.SetLeft(FrontDate, dp.X);
                Canvas.SetTop(FrontDate, dp.Y);
            }
            catch { FrontDate.Visibility = Visibility.Collapsed; }
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
            bool isAnalog = clockLayout >= 2 && clockLayout <= 4;
            
            if (isAnalog)
            {
                brush = new SolidColorBrush(Colors.White);
            }
            else if (clockHue >= 0)
            {
                brush = new SolidColorBrush(ColorFromHSV(clockHue, 1.0, 1.0));
            }
            else if (clockBlend > 0)
            {
                switch (clockBlend)
                {
                    case 1: brush = MakeGrad(Color.FromArgb(255, 255, 140, 50),
                                Color.FromArgb(255, 255, 80, 150)); break;  // Sunset
                    case 2: brush = MakeGrad(Color.FromArgb(255, 0, 210, 255),
                                Color.FromArgb(255, 58, 80, 200)); break;   // Ocean
                    case 3: brush = MakeGrad(Color.FromArgb(255, 80, 255, 120),
                                Color.FromArgb(255, 180, 80, 255)); break;  // Aurora
                    case 4: brush = MakeGrad(Color.FromArgb(255, 255, 0, 200),
                                Color.FromArgb(255, 0, 255, 255)); break;   // Neon
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
            PRhombusH1.Foreground = brush;
            PRhombusH2.Foreground = brush;
            PRhombusM1.Foreground = brush;
            PRhombusM2.Foreground = brush;
        }

        private LinearGradientBrush MakeGrad(Color from, Color to)
        {
            var lgb = new LinearGradientBrush();
            lgb.StartPoint = new Point(0, 0);
            lgb.EndPoint = new Point(1, 1);
            lgb.GradientStops.Add(new GradientStop { Color = from, Offset = 0 });
            lgb.GradientStops.Add(new GradientStop { Color = to, Offset = 1 });
            return lgb;
        }

        public static Color ColorFromHSV(double hue, double saturation, double value)
        {
            int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
            double f = hue / 60 - Math.Floor(hue / 60);

            value = value * 255;
            int v = Convert.ToInt32(value);
            int p = Convert.ToInt32(value * (1 - saturation));
            int q = Convert.ToInt32(value * (1 - f * saturation));
            int t = Convert.ToInt32(value * (1 - (1 - f) * saturation));

            if (hi == 0) return Color.FromArgb(255, (byte)v, (byte)t, (byte)p);
            else if (hi == 1) return Color.FromArgb(255, (byte)q, (byte)v, (byte)p);
            else if (hi == 2) return Color.FromArgb(255, (byte)p, (byte)v, (byte)t);
            else if (hi == 3) return Color.FromArgb(255, (byte)p, (byte)q, (byte)v);
            else if (hi == 4) return Color.FromArgb(255, (byte)t, (byte)p, (byte)v);
            else return Color.FromArgb(255, (byte)v, (byte)p, (byte)q);
        }

        private void ApplySignatureColor()
        {
            Brush brush;
            if (sigHue >= 0)
            {
                brush = new SolidColorBrush(ColorFromHSV(sigHue, 1.0, 1.0));
            }
            else if (sigBlend > 0)
            {
                switch (sigBlend)
                {
                    case 1: brush = MakeGrad(Color.FromArgb(255, 255, 140, 50), Color.FromArgb(255, 255, 80, 150)); break;  // Sunset
                    case 2: brush = MakeGrad(Color.FromArgb(255, 0, 210, 255), Color.FromArgb(255, 58, 80, 200)); break;   // Ocean
                    case 3: brush = MakeGrad(Color.FromArgb(255, 80, 255, 120), Color.FromArgb(255, 180, 80, 255)); break;  // Aurora
                    case 4: brush = MakeGrad(Color.FromArgb(255, 255, 0, 200), Color.FromArgb(255, 0, 255, 255)); break;   // Neon
                    case 5: brush = MakeGrad(Color.FromArgb(255, 255, 105, 180), Color.FromArgb(255, 148, 0, 211)); break;   // Rose
                    case 6: brush = MakeGrad(Color.FromArgb(255, 255, 50, 0), Color.FromArgb(255, 255, 165, 0)); break;   // Fire
                    case 7: brush = MakeGrad(Color.FromArgb(255, 255, 255, 255), Color.FromArgb(255, 173, 216, 230)); break; // Ice
                    case 8: brush = MakeGrad(Color.FromArgb(255, 50, 205, 50), Color.FromArgb(255, 255, 255, 0)); break;   // Lime
                    case 9: brush = MakeGrad(Color.FromArgb(255, 75, 0, 130), Color.FromArgb(255, 25, 25, 112)); break;   // Twilight
                    default: brush = new SolidColorBrush(Colors.White); break;
                }
            }
            else
            {
                switch (sigColor)
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
            PSignature.Foreground = brush;
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

            // Update depth front layer in real-time while dragging clock
            if ((string)handle.Tag == "Clock")
                UpdateDepthFrontLayer();

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
                case "Signature": signatureX = newX; signatureY = newY; break;
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
            Border[] handles = { ClockHandle, WeatherHandle, CountdownHandle, SignatureHandle };
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
            Border[] handles = { ClockHandle, WeatherHandle, CountdownHandle, SignatureHandle };
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
            PropPanel.Visibility = Visibility.Visible;
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
            Save("SignatureX", signatureX);
            Save("SignatureY", signatureY);

            // Save signature settings in case user didn't click "Save Signature"
            Save("ShowSignature", EdSignatureToggle.IsChecked == true);
            Save("SignatureText", EdSignatureText.Text);
            Save("SignatureFont", sigFontIndex);
            Save("SignatureSpacing", sigSpacing);
            Save("SignatureAlign", sigAlign);
            Save("SignatureLayout", sigLayout);
            Save("SignatureColor", sigColor);
            Save("SignatureBlend", sigBlend);

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
                        
                        if (store.FileExists("Background_Filtered.jpg"))
                        {
                            string presetFilteredBg = "Background_Filtered_" + editingPreset + ".jpg";
                            if (store.FileExists(presetFilteredBg))
                                store.DeleteFile(presetFilteredBg);
                            store.CopyFile("Background_Filtered.jpg", presetFilteredBg);
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
            PropPanel.Visibility = Visibility.Collapsed;
        }

        private bool toolbarHidden = false;

        private void PreviewArea_DoubleTap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            toolbarHidden = !toolbarHidden;

            if (toolbarHidden)
            {
                // Slide bottom toolbar down (out of view)
                var sbDown = new System.Windows.Media.Animation.Storyboard();
                var animBottom = new System.Windows.Media.Animation.DoubleAnimation
                {
                    To = 400, Duration = TimeSpan.FromMilliseconds(250),
                    EasingFunction = new System.Windows.Media.Animation.CubicEase
                    { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }
                };
                System.Windows.Media.Animation.Storyboard.SetTarget(animBottom, BottomToolbar);
                System.Windows.Media.Animation.Storyboard.SetTargetProperty(animBottom,
                    new PropertyPath("(UIElement.RenderTransform).(CompositeTransform.TranslateY)"));
                sbDown.Children.Add(animBottom);
                
                // Hide prop panel instantly
                PropPanel.Visibility = Visibility.Collapsed;

                // Fade header out
                var animHeader = new System.Windows.Media.Animation.DoubleAnimation
                {
                    To = 0, Duration = TimeSpan.FromMilliseconds(200)
                };
                System.Windows.Media.Animation.Storyboard.SetTarget(animHeader, HeaderBar);
                System.Windows.Media.Animation.Storyboard.SetTargetProperty(animHeader,
                    new PropertyPath("(UIElement.Opacity)"));
                sbDown.Children.Add(animHeader);

                sbDown.Completed += (s, ev) =>
                {
                    HeaderBar.IsHitTestVisible = false;
                    BottomToolbar.IsHitTestVisible = false;
                    PropPanel.IsHitTestVisible = false;
                };
                // Ensure transforms exist
                if (!(BottomToolbar.RenderTransform is CompositeTransform))
                    BottomToolbar.RenderTransform = new CompositeTransform();
                if (!(PropPanel.RenderTransform is CompositeTransform))
                    PropPanel.RenderTransform = new CompositeTransform();
                sbDown.Begin();
            }
            else
            {
                // Slide bottom toolbar up (into view)
                var sbUp = new System.Windows.Media.Animation.Storyboard();
                var animBottom = new System.Windows.Media.Animation.DoubleAnimation
                {
                    To = 0, Duration = TimeSpan.FromMilliseconds(300),
                    EasingFunction = new System.Windows.Media.Animation.CubicEase
                    { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                };
                System.Windows.Media.Animation.Storyboard.SetTarget(animBottom, BottomToolbar);
                System.Windows.Media.Animation.Storyboard.SetTargetProperty(animBottom,
                    new PropertyPath("(UIElement.RenderTransform).(CompositeTransform.TranslateY)"));
                sbUp.Children.Add(animBottom);

                // Slide prop panel up (into view)
                var animPanel = new System.Windows.Media.Animation.DoubleAnimation
                {
                    To = 0, Duration = TimeSpan.FromMilliseconds(300),
                    EasingFunction = new System.Windows.Media.Animation.CubicEase
                    { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                };
                System.Windows.Media.Animation.Storyboard.SetTarget(animPanel, PropPanel);
                System.Windows.Media.Animation.Storyboard.SetTargetProperty(animPanel,
                    new PropertyPath("(UIElement.RenderTransform).(CompositeTransform.TranslateY)"));
                sbUp.Children.Add(animPanel);

                // Fade header back in
                var animHeader = new System.Windows.Media.Animation.DoubleAnimation
                {
                    To = 1, Duration = TimeSpan.FromMilliseconds(200)
                };
                System.Windows.Media.Animation.Storyboard.SetTarget(animHeader, HeaderBar);
                System.Windows.Media.Animation.Storyboard.SetTargetProperty(animHeader,
                    new PropertyPath("(UIElement.Opacity)"));
                sbUp.Children.Add(animHeader);

                HeaderBar.IsHitTestVisible = true;
                BottomToolbar.IsHitTestVisible = true;
                PropPanel.IsHitTestVisible = true;
                sbUp.Begin();
            }

            e.Handled = true;
        }

        #endregion

        #region Tab Navigation

        // (Tab_Tap removed since bottom tabs are replaced by BottomToolbar)

        private void SelectTab(string tab)
        {
            selectedTab = tab;

            // Show corresponding properties
            if (ClockProps != null) ClockProps.Visibility = tab == "Clock" ? Visibility.Visible : Visibility.Collapsed;
            if (WeatherProps != null) WeatherProps.Visibility = tab == "Weather" ? Visibility.Visible : Visibility.Collapsed;
            if (CountdownProps != null) CountdownProps.Visibility = tab == "Countdown" ? Visibility.Visible : Visibility.Collapsed;
            if (SignatureProps != null) SignatureProps.Visibility = tab == "Signature" ? Visibility.Visible : Visibility.Collapsed;
            if (DisplayProps != null) DisplayProps.Visibility = tab == "Display" ? Visibility.Visible : Visibility.Collapsed;
            if (WidgetsProps != null) WidgetsProps.Visibility = tab == "Widgets" ? Visibility.Visible : Visibility.Collapsed;
            
            if (ColorProps != null) ColorProps.Visibility = tab == "Color" ? Visibility.Visible : Visibility.Collapsed;
            if (FilterProps != null) FilterProps.Visibility = tab == "Filter" ? Visibility.Visible : Visibility.Collapsed;
            if (DepthProps != null) DepthProps.Visibility = tab == "Depth" ? Visibility.Visible : Visibility.Collapsed;
            
            // Select corresponding handle on preview
            switch (tab)
            {
                case "Clock": SelectHandle(ClockHandle); break;
                case "Weather": SelectHandle(WeatherHandle); break;
                case "Countdown": SelectHandle(CountdownHandle); break;
                case "Signature": SelectHandle(SignatureHandle); break;
                default: SelectHandle(null); break;
            }
        }

        private void Toolbar_Widgets_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            PropPanel.Visibility = Visibility.Visible;
            SelectTab("Widgets");
        }

        private void Toolbar_Wallpaper_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            // Just simulate a double tap to trigger full screen preview?
            // Actually, we want to open a wallpaper picker. For now we just select Display tab.
            PropPanel.Visibility = Visibility.Visible;
            SelectTab("Display");
        }

        private void Toolbar_Filter_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            PropPanel.Visibility = Visibility.Visible;
            SelectTab("Filter");
        }

        private void Toolbar_Depth_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            PropPanel.Visibility = Visibility.Visible;
            SelectTab("Depth");
        }

        private void Toolbar_Color_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            PropPanel.Visibility = Visibility.Visible;
            SelectTab("Color");
            UpdateColorScopeSelection();
            UpdateColorSelection();
            UpdateBlendSelection();
        }

        #endregion

        #region Clock Property Handlers

        private void Layout_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            var border = (Border)sender;
            clockLayout = int.Parse((string)border.Tag);
            Save("ClockLayout", clockLayout);
            UpdateLayoutSelection();
            UpdateDepthRowVisibility();
            ApplyPreview();
        }

        private void UpdateLayoutSelection()
        {
            Border[] pills = { LayoutHoriz, LayoutVert, LayoutAnalog1, LayoutAnalog2, LayoutAnalog3, LayoutRhombus, LayoutGiant };
            for (int i = 0; i < pills.Length; i++)
                pills[i].Background = (i == clockLayout) ? AccentBrush : InactiveTabBg;
        }

        private void FontPrev_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            clockStyle = (clockStyle - 1 + FontNames.Length) % FontNames.Length;
            Save("ClockStyle", clockStyle);
            FontLabel.Text = FontNames[clockStyle];
            ApplyPreview();
        }

        private void FontNext_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            clockStyle = (clockStyle + 1) % FontNames.Length;
            Save("ClockStyle", clockStyle);
            FontLabel.Text = FontNames[clockStyle];
            ApplyPreview();
        }

        private void Size_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            var border = (Border)sender;
            clockSize = int.Parse((string)border.Tag);
            Save("ClockSize", clockSize);
            UpdateSizeSelection();
            ApplyPreview();
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

        private string colorScope = "Clock";

        private void ColorScope_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            var border = (Border)sender;
            colorScope = (string)border.Tag;
            UpdateColorScopeSelection();
            UpdateColorSelection();
            UpdateBlendSelection();
        }

        private void UpdateColorScopeSelection()
        {
            if (ColorScopeClock == null || ColorScopeSignature == null) return;
            ColorScopeClock.Background = colorScope == "Clock" ? AccentBrush : TransparentBrush;
            ((TextBlock)ColorScopeClock.Child).Foreground = colorScope == "Clock" ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF));

            ColorScopeSignature.Background = colorScope == "Signature" ? AccentBrush : TransparentBrush;
            ((TextBlock)ColorScopeSignature.Child).Foreground = colorScope == "Signature" ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF));
        }

        private void Color_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            var el = (Ellipse)sender;
            int c = int.Parse((string)el.Tag);
            if (colorScope == "Clock")
            {
                clockColor = c;
                clockBlend = 0;
                clockHue = -1;
                Save("ClockColor", clockColor);
                Save("ClockBlend", 0);
                Save("ClockHue", -1);
            }
            else
            {
                sigColor = c;
                sigBlend = 0;
                sigHue = -1;
                Save("SignatureColor", sigColor);
                Save("SignatureBlend", 0);
                Save("SignatureHue", -1);
            }
            UpdateColorSelection();
            UpdateBlendSelection();
            ApplyPreview();
        }

        private void UpdateColorSelection()
        {
            Ellipse[] circles = { ColorW, ColorG, ColorB, ColorP, ColorR, ColorMint, ColorLav, ColorOr, ColorCy, ColorSi };
            int curColor = colorScope == "Clock" ? clockColor : sigColor;
            int curBlend = colorScope == "Clock" ? clockBlend : sigBlend;
            int curHue = colorScope == "Clock" ? clockHue : sigHue;
            for (int i = 0; i < circles.Length; i++)
            {
                if (circles[i] != null)
                    circles[i].Stroke = (i == curColor && curBlend == 0 && curHue < 0) ? SelectBrush : TransparentBrush;
            }
            
            isUpdatingColorUI = true;
            EdColorOpacity.Value = colorScope == "Clock" ? clockOpacity : sigOpacity;
            EdColorHue.Value = curHue;
            isUpdatingColorUI = false;
        }

        private void Blend_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            var border = (Border)sender;
            int b = int.Parse((string)border.Tag);
            if (colorScope == "Clock")
            {
                clockBlend = b;
                clockHue = -1;
                Save("ClockBlend", clockBlend);
                Save("ClockHue", -1);
            }
            else
            {
                sigBlend = b;
                sigHue = -1;
                Save("SignatureBlend", sigBlend);
                Save("SignatureHue", -1);
            }
            UpdateBlendSelection();
            UpdateColorSelection();
            ApplyPreview();
        }

        private void UpdateBlendSelection()
        {
            Border[] pills = { BlendNone, BlendSunset, BlendOcean, BlendAurora, BlendNeon, BlendRose, BlendFire, BlendIce, BlendForest, BlendCyber };
            int curBlend = colorScope == "Clock" ? clockBlend : sigBlend;
            int curHue = colorScope == "Clock" ? clockHue : sigHue;
            for (int i = 0; i < pills.Length; i++)
            {
                if (pills[i] != null)
                {
                    pills[i].Background = (i == curBlend && curHue < 0) ? AccentBrush : InactiveTabBg;
                    ((TextBlock)pills[i].Child).Foreground = (i == curBlend && curHue < 0) ?
                        new SolidColorBrush(Colors.White) :
                        new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF));
                }
            }
        }

        private void EdColorOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isLoading) return;
            if (isUpdatingColorUI) return;
            if (colorScope == "Clock")
            {
                clockOpacity = e.NewValue;
                Save("ClockOpacity", clockOpacity);
            }
            else
            {
                sigOpacity = e.NewValue;
                Save("SignatureOpacity", sigOpacity);
            }
            ApplyPreview();
        }

        private void EdColorHue_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isLoading) return;
            if (isUpdatingColorUI) return;
            if (colorScope == "Clock")
            {
                clockHue = (int)e.NewValue;
                Save("ClockHue", clockHue);
            }
            else
            {
                sigHue = (int)e.NewValue;
                Save("SignatureHue", sigHue);
            }
            UpdateColorSelection();
            UpdateBlendSelection();
            ApplyPreview();
        }

        private void DateAlign_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            if (isLoading) return;
            var border = (Border)sender;
            dateAlign = int.Parse((string)border.Tag);
            Save("DateAlign", dateAlign);
            UpdateDateAlignSelection();
            ApplyDateAlign();
            Dispatcher.BeginInvoke(() => UpdateDepthFrontLayer());
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
            HorizontalAlignment ha = HorizontalAlignment.Center;
            switch (dateAlign)
            {
                case 0: ha = HorizontalAlignment.Left; break;
                case 2: ha = HorizontalAlignment.Right; break;
            }
            PDatePanel.HorizontalAlignment = ha;
            PTimePanel.HorizontalAlignment = ha;
            PAnalogClock.HorizontalAlignment = ha;
            PRhombusGrid.HorizontalAlignment = ha;
        }

        /// <summary>
        /// Auto-compute date alignment from clock's horizontal zone.
        /// Left third → 0 (Left), Middle third → 1 (Center), Right third → 2 (Right)
        /// </summary>
        private int ComputeAutoAlign(double x, double elementWidth)
        {
            double zoneThird = SCREEN_W / 3.0;
            double centerX = x + (elementWidth / 2.0);
            if (centerX < zoneThird) return 0;      // Left
            if (centerX > zoneThird * 2) return 2;   // Right
            return 1;                                 // Center
        }

        #endregion

        #region Signature Handlers

        private void EdSignatureToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (isLoading) return;
            showSignature = EdSignatureToggle.IsChecked == true;
            Save("ShowSignature", showSignature);
            SignatureHandle.Visibility = showSignature ? Visibility.Visible : Visibility.Collapsed;
            if (showSignature) ApplyPreview();
        }

        private void EdSaveSignature_Click(object sender, RoutedEventArgs e)
        {
            sigText = EdSignatureText.Text.Trim();
            Save("SignatureText", sigText);
            ApplyPreview();
        }

        private void SigFontPrev_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            sigFontIndex = (sigFontIndex - 1 + FontNames.Length) % FontNames.Length;
            Save("SignatureFont", sigFontIndex);
            SigFontLabel.Text = FontNames[sigFontIndex];
            ApplyPreview();
        }

        private void SigFontNext_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            sigFontIndex = (sigFontIndex + 1) % FontNames.Length;
            Save("SignatureFont", sigFontIndex);
            SigFontLabel.Text = FontNames[sigFontIndex];
            ApplyPreview();
        }

        private void EdSigSpacing_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isLoading) return;
            sigSpacing = e.NewValue;
            Save("SignatureSpacing", sigSpacing);
            ApplyPreview();
        }

        private void SigAlign_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            var border = (Border)sender;
            sigAlign = int.Parse((string)border.Tag);
            Save("SignatureAlign", sigAlign);
            UpdateSigAlignSelection();
            
            // Auto position immediately
            Dispatcher.BeginInvoke(() =>
            {
                double w = SignatureHandle.ActualWidth;
                double h = SignatureHandle.ActualHeight;
                if (w == 0 || h == 0) return;

                double px = 24, py = 120; // safe zones
                
                switch (sigAlign)
                {
                    case 0: signatureX = px; signatureY = py; break;
                    case 1: signatureX = (SCREEN_W - w) / 2.0; signatureY = py; break;
                    case 2: signatureX = SCREEN_W - w - px; signatureY = py; break;
                    case 3: signatureX = px; signatureY = SCREEN_H - h - py; break;
                    case 4: signatureX = (SCREEN_W - w) / 2.0; signatureY = SCREEN_H - h - py; break;
                    case 5: signatureX = SCREEN_W - w - px; signatureY = SCREEN_H - h - py; break;
                }
                SignatureHandle.Margin = new Thickness(signatureX, signatureY, 0, 0);
                Save("SignatureX", signatureX);
                Save("SignatureY", signatureY);
            });
        }

        private void SigLayout_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            var border = (Border)sender;
            sigLayout = int.Parse((string)border.Tag);
            Save("SignatureLayout", sigLayout);
            UpdateSigLayoutSelection();
            ApplyPreview();
            
            Dispatcher.BeginInvoke(() => {
                double w = SignatureHandle.ActualWidth;
                double h = SignatureHandle.ActualHeight;
                if (w > 0 && h > 0)
                {
                    double px = 24, py = 120;
                    switch (sigAlign)
                    {
                        case 0: signatureX = px; signatureY = py; break;
                        case 1: signatureX = (SCREEN_W - w) / 2.0; signatureY = py; break;
                        case 2: signatureX = SCREEN_W - w - px; signatureY = py; break;
                        case 3: signatureX = px; signatureY = SCREEN_H - h - py; break;
                        case 4: signatureX = (SCREEN_W - w) / 2.0; signatureY = SCREEN_H - h - py; break;
                        case 5: signatureX = SCREEN_W - w - px; signatureY = SCREEN_H - h - py; break;
                    }
                    SignatureHandle.Margin = new Thickness(signatureX, signatureY, 0, 0);
                    Save("SignatureX", signatureX);
                    Save("SignatureY", signatureY);
                }
            });
        }

        private void UpdateSigAlignSelection()
        {
            if (SigAlignTL == null) return;
            Border[] pills = { SigAlignTL, SigAlignTC, SigAlignTR, SigAlignBL, SigAlignBC, SigAlignBR };
            for (int i = 0; i < pills.Length; i++)
            {
                pills[i].Background = (i == sigAlign) ? AccentBrush : InactiveTabBg;
                ((TextBlock)pills[i].Child).Foreground = (i == sigAlign) ?
                    new SolidColorBrush(Colors.White) :
                    new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF));
            }
        }

        private void UpdateSigLayoutSelection()
        {
            if (SigLayoutHoriz == null) return;
            Border[] pills = { SigLayoutHoriz, SigLayoutVert };
            for (int i = 0; i < pills.Length; i++)
            {
                pills[i].Background = (i == sigLayout) ? AccentBrush : InactiveTabBg;
                ((TextBlock)pills[i].Child).Foreground = (i == sigLayout) ?
                    new SolidColorBrush(Colors.White) :
                    new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF));
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
            state["EdDateAlign"] = dateAlign; state["EdClockLayout"] = clockLayout;
            state["EdShowWeather"] = showWeather; state["EdShowCountdown"] = showCountdown;
            state["EdDepth"] = useDepthEffect;
            state["EdDepthH"] = depthHourBehind; state["EdDepthC"] = depthColonBehind; state["EdDepthM"] = depthMinuteBehind;

            photoChooser.Show();
        }

        private void EdDepthToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (isLoading) return;
            useDepthEffect = EdDepthToggle.IsChecked == true;
            Save("UseDepthEffect", useDepthEffect);
            EdDepthLayers.Visibility = useDepthEffect ? Visibility.Visible : Visibility.Collapsed;
            LoadPreviewImages();
            Dispatcher.BeginInvoke(() => UpdateDepthFrontLayer());
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

            bool isAnalog = clockLayout >= 2 && clockLayout <= 4;
            if (isAnalog)
            {
                // Analog: single "Clock" toggle controls all
                bool behind = EdDepthClock.IsChecked == true;
                depthHourBehind = behind;
                depthColonBehind = behind;
                depthMinuteBehind = behind;
            }
            else
            {
                depthHourBehind = EdDepthHour.IsChecked == true;
                depthColonBehind = (clockLayout == 1 || clockLayout == 6) ? depthHourBehind : (EdDepthColon.IsChecked == true);
                depthMinuteBehind = EdDepthMinute.IsChecked == true;
            }

            Save("DepthHourBehind", depthHourBehind);
            Save("DepthColonBehind", depthColonBehind);
            Save("DepthMinuteBehind", depthMinuteBehind);
            Dispatcher.BeginInvoke(() => UpdateDepthFrontLayer());
        }

        /// <summary>
        /// Shows/hides the appropriate depth layer rows based on clock layout.
        /// Analog → hide all layer rows (just toggle is enough). Vertical → Hour+Minute. Horizontal → all.
        /// </summary>
        private void UpdateDepthRowVisibility()
        {
            bool isAnalog = clockLayout >= 2 && clockLayout <= 4;
            bool isVertical = clockLayout == 1;
            bool isGiant = clockLayout == 6;

            // Analog: show single "Clock" toggle; Digital: show per-part toggles
            DepthRowClock.Visibility = isAnalog ? Visibility.Visible : Visibility.Collapsed;
            DepthRowHour.Visibility = isAnalog ? Visibility.Collapsed : Visibility.Visible;
            DepthRowColon.Visibility = (isAnalog || isVertical || isGiant) ? Visibility.Collapsed : Visibility.Visible;
            DepthRowMinute.Visibility = isAnalog ? Visibility.Collapsed : Visibility.Visible;

            // For analog, auto-set all behind when depth is on
            if (isAnalog && useDepthEffect)
            {
                depthHourBehind = true;
                depthColonBehind = true;
                depthMinuteBehind = true;
            }
        }

        #endregion

        #region My Sets

        // My Sets
        private static readonly string[] SetKeys = { "ClockStyle", "ClockPosition", "ClockHAlign",
            "ClockColor", "ClockBlend", "ClockOpacity", "ClockHue", "ClockSize", "ClockLayout",
            "ShowWeather", "ShowCountdown", "UseDepthEffect",
            "DepthHourBehind", "DepthColonBehind", "DepthMinuteBehind",
            "UseMatte", "UseRibbed",
            "ClockX", "ClockY", "WeatherX", "WeatherY", "CountdownX", "CountdownY",
            "bIsAnimOn", "DateAlign", "CountdownName", "CountdownTarget", "OwnerInfo",
            "ShowSignature", "SignatureX", "SignatureY", "SignatureText", "SignatureFont",
            "SignatureSpacing", "SignatureAlign", "SignatureColor", "SignatureBlend", "SignatureOpacity", "SignatureHue", "SignatureLayout" };

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

        private async void Back_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            if (hasUnsavedChanges)
            {
                UnsavedDialog.Visibility = Visibility.Visible;
                return;
            }
            await RestoreOriginalBackgroundAsync(true);
            if (NavigationService.CanGoBack)
                NavigationService.GoBack();
        }

        #endregion

        #region Filters

        private async void MatteToggle_Checked(object sender, RoutedEventArgs e)
        {
            await ApplyFilterAsync();
        }

        private async void MatteToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            await ApplyFilterAsync();
        }

        private async void RibbedToggle_Checked(object sender, RoutedEventArgs e)
        {
            await ApplyFilterAsync();
        }

        private async void RibbedToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            await ApplyFilterAsync();
        }

        private async System.Threading.Tasks.Task ApplyFilterAsync()
        {
            useMatte = MatteToggle?.IsChecked == true;
            useRibbed = RibbedToggle?.IsChecked == true;
            
            Save("UseMatte", useMatte);
            Save("UseRibbed", useRibbed);

            if (!useMatte && !useRibbed)
            {
                // Delete filtered background when disabled
                using (var store = System.IO.IsolatedStorage.IsolatedStorageFile.GetUserStoreForApplication())
                {
                    if (store.FileExists("Background_Filtered.jpg"))
                        store.DeleteFile("Background_Filtered.jpg");
                }
                LoadPreviewImages();
                return;
            }

            if (FilterProcessingText != null) FilterProcessingText.Visibility = Visibility.Visible;
            
            try
            {
                using (var store = System.IO.IsolatedStorage.IsolatedStorageFile.GetUserStoreForApplication())
                {
                    if (!store.FileExists("Background.jpg")) return;

                    // Load original
                    WriteableBitmap wb = null;
                    using (var stream = store.OpenFile("Background.jpg", System.IO.FileMode.Open, System.IO.FileAccess.Read))
                    {
                        var bmp = new BitmapImage();
                        bmp.SetSource(stream);
                        wb = new WriteableBitmap(bmp);
                    }

                    int w = wb.PixelWidth;
                    int h = wb.PixelHeight;
                    int[] srcPixels = wb.Pixels;
                    int[] destPixels = new int[srcPixels.Length];
                    Array.Copy(srcPixels, destPixels, srcPixels.Length);

                    // Process filter off UI thread
                    await System.Threading.Tasks.Task.Run(() =>
                    {
                        if (useMatte)
                            destPixels = HyperOS.Helpers.FilterHelper.ApplyBoxBlur(destPixels, w, h, 15, 2);
                        if (useRibbed)
                            destPixels = HyperOS.Helpers.FilterHelper.ApplyRibbedFilter(destPixels, w, h, 0);
                    });

                    if (destPixels != null)
                    {
                        WriteableBitmap filtered = new WriteableBitmap(w, h);
                        Array.Copy(destPixels, filtered.Pixels, destPixels.Length);

                        // Save back to Background_Filtered.jpg
                        if (store.FileExists("Background_Filtered.jpg")) store.DeleteFile("Background_Filtered.jpg");
                        using (var stream = store.OpenFile("Background_Filtered.jpg", System.IO.FileMode.Create, System.IO.FileAccess.Write))
                        {
                            filtered.SaveJpeg(stream, w, h, 0, 95);
                        }

                        // Reload Preview
                        LoadPreviewImages();
                    }
                }
            }
            catch { }
            
            if (FilterProcessingText != null) FilterProcessingText.Visibility = Visibility.Collapsed;
        }

        private async System.Threading.Tasks.Task RestoreOriginalBackgroundAsync(bool force = false)
        {
            await System.Threading.Tasks.Task.Yield();
            // Deprecated: No longer manipulating original file
        }

        #endregion

        #region AI Wallpaper & Settings Keys

        private bool isGeneratingAI = false;

        private void EdAIWallpaper_Click(object sender, RoutedEventArgs e)
        {
            if (isGeneratingAI)
            {
                MessageBox.Show("Vui lòng đợi ảnh trước tạo xong!", "Đang xử lý", MessageBoxButton.OK);
                return;
            }
            AIPromptTextBox.Text = "";
            AIPromptDialog.Visibility = Visibility.Visible;
        }

        private void AIPromptDialog_Cancel_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            AIPromptDialog.Visibility = Visibility.Collapsed;
        }

        private void AIPromptDialog_Cancel(object sender, System.Windows.Input.GestureEventArgs e)
        {
            AIPromptDialog.Visibility = Visibility.Collapsed;
        }

        private string aiSelectedStyle = "Anime";

        private void AIStyle_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            var border = sender as Border;
            if (border != null && border.Tag != null)
            {
                aiSelectedStyle = border.Tag.ToString();
                
                AIStyleAnime.Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
                AIStyle3D.Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
                AIStyleInk.Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
                AIStyleOil.Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
                AIStyleCustom.Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));

                ((TextBlock)AIStyleAnime.Child).Foreground = new SolidColorBrush(Colors.LightGray);
                ((TextBlock)AIStyle3D.Child).Foreground = new SolidColorBrush(Colors.LightGray);
                ((TextBlock)AIStyleInk.Child).Foreground = new SolidColorBrush(Colors.LightGray);
                ((TextBlock)AIStyleOil.Child).Foreground = new SolidColorBrush(Colors.LightGray);
                ((TextBlock)AIStyleCustom.Child).Foreground = new SolidColorBrush(Colors.LightGray);

                border.Background = new SolidColorBrush(Color.FromArgb(255, 58, 123, 242)); // #3A7BF2
                ((TextBlock)border.Child).Foreground = new SolidColorBrush(Colors.White);
            }
        }

        private async void AIPromptDialog_Generate(object sender, System.Windows.Input.GestureEventArgs e)
        {
            if (isGeneratingAI) return;
            string prompt = AIPromptTextBox.Text.Trim();
            if (string.IsNullOrEmpty(prompt)) prompt = aiSelectedStyle == "Custom" ? "beautiful landscape scenery" : aiSelectedStyle + " landscape scenery";

            isGeneratingAI = true;
            AIPromptDialog.Visibility = Visibility.Collapsed;
            if (FilterProcessingText != null)
            {
                FilterProcessingText.Text = "Đang tải ảnh từ Pollinations AI...";
                FilterProcessingText.Visibility = Visibility.Visible;
            }

            string fullPrompt = prompt;
            if (aiSelectedStyle != "Custom")
            {
                fullPrompt += ", in " + aiSelectedStyle + " style";
            }
            fullPrompt += ", highly detailed, 4k wallpaper, masterpiece";
            
            string encodedPrompt = System.Uri.EscapeDataString(fullPrompt);
            string imageUrl = $"https://image.pollinations.ai/prompt/{encodedPrompt}?width=1024&height=1024&nologo=true&seed={new Random().Next()}";

            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    var imgResponse = await client.GetAsync(imageUrl);
                    if (imgResponse.IsSuccessStatusCode)
                    {
                        byte[] imgBytes = await imgResponse.Content.ReadAsByteArrayAsync();

                        using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                        {
                            if (store.FileExists("Background_AI.jpg")) store.DeleteFile("Background_AI.jpg");
                            using (var file = store.CreateFile("Background_AI.jpg"))
                            {
                                file.Write(imgBytes, 0, imgBytes.Length);
                            }
                        }

                        if (FilterProcessingText != null) FilterProcessingText.Visibility = Visibility.Collapsed;
                        isGeneratingAI = false;
                        MessageBox.Show("Tạo ảnh AI thành công!", "Thành công", MessageBoxButton.OK);

                        using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                        {
                            if (store.FileExists("Background.jpg")) store.DeleteFile("Background.jpg");
                            store.CopyFile("Background_AI.jpg", "Background.jpg");
                        }

                        hasUnsavedChanges = true;
                        LoadPreviewImages();
                    }
                    else
                    {
                        if (FilterProcessingText != null) FilterProcessingText.Visibility = Visibility.Collapsed;
                        isGeneratingAI = false;
                        MessageBox.Show($"Lỗi từ server Pollinations ({(int)imgResponse.StatusCode}).", "Lỗi API", MessageBoxButton.OK);
                    }
                }
            }
            catch (Exception ex)
            {
                if (FilterProcessingText != null) FilterProcessingText.Visibility = Visibility.Collapsed;
                isGeneratingAI = false;
                MessageBox.Show("Lỗi kết nối: " + ex.Message, "Lỗi", MessageBoxButton.OK);
            }
        }

        #endregion

    }
}
