using System;
using System.Collections.Generic;
using System.IO.IsolatedStorage;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Phone.Controls;

namespace HyperOS.Pages
{
    public partial class MySetsPage : PhoneApplicationPage
    {
        #region Preset Definition

        private class Preset
        {
            public string Name { get; set; }
            public string Subtitle { get; set; }
            public int ClockStyle { get; set; }
            public int ClockSize { get; set; }
            public int ClockColor { get; set; }
            public int ClockBlend { get; set; }
            public int DateAlign { get; set; }
            public Color PreviewBg { get; set; }
            public Color PreviewClockColor { get; set; }
            public double ClockX { get; set; }  // -1 = center
            public double ClockY { get; set; }  // -1 = center
            public bool UseDepthEffect { get; set; }
            public bool DepthHourBehind { get; set; }
            public bool DepthColonBehind { get; set; }
            public bool DepthMinuteBehind { get; set; }

            public Preset() { ClockX = -1; ClockY = -1; DepthHourBehind = true; DepthColonBehind = true; DepthMinuteBehind = true; }
        }

        private static readonly List<Preset> Presets = new List<Preset>
        {
            new Preset { Name = "Classic", Subtitle = "What's classic never goes out of style.",
                ClockStyle = 0, ClockSize = 2, ClockColor = 0, ClockBlend = 0, DateAlign = 1,
                PreviewBg = Color.FromArgb(255, 60, 60, 80), PreviewClockColor = Colors.White },
            new Preset { Name = "Bold", Subtitle = "Make a statement with bold typography.",
                ClockStyle = 1, ClockSize = 4, ClockColor = 0, ClockBlend = 1, DateAlign = 1,
                PreviewBg = Color.FromArgb(255, 40, 20, 60), PreviewClockColor = Color.FromArgb(255, 255, 120, 80) },
            new Preset { Name = "Elegant", Subtitle = "Refined beauty in every detail.",
                ClockStyle = 4, ClockSize = 3, ClockColor = 1, ClockBlend = 0, DateAlign = 1,
                PreviewBg = Color.FromArgb(255, 30, 30, 30), PreviewClockColor = Color.FromArgb(255, 255, 215, 0) },
            new Preset { Name = "Neon", Subtitle = "Electrify your screen.",
                ClockStyle = 3, ClockSize = 3, ClockColor = 8, ClockBlend = 4, DateAlign = 0,
                PreviewBg = Color.FromArgb(255, 10, 10, 30), PreviewClockColor = Color.FromArgb(255, 0, 229, 255) },
            new Preset { Name = "Magazine", Subtitle = "Turn your lock screen into a cover.",
                ClockStyle = 6, ClockSize = 2, ClockColor = 0, ClockBlend = 0, DateAlign = 1,
                PreviewBg = Color.FromArgb(255, 180, 120, 80), PreviewClockColor = Colors.White },
            new Preset { Name = "Minimal", Subtitle = "Less is more.",
                ClockStyle = 9, ClockSize = 1, ClockColor = 9, ClockBlend = 0, DateAlign = 1,
                PreviewBg = Color.FromArgb(255, 20, 20, 25), PreviewClockColor = Color.FromArgb(255, 160, 160, 176) },
            new Preset { Name = "Serif", Subtitle = "Timeless serif elegance.",
                ClockStyle = 5, ClockSize = 3, ClockColor = 0, ClockBlend = 0, DateAlign = 1,
                PreviewBg = Color.FromArgb(255, 50, 40, 60), PreviewClockColor = Colors.White },
            new Preset { Name = "Display", Subtitle = "Time is important. Make it count.",
                ClockStyle = 10, ClockSize = 4, ClockColor = 0, ClockBlend = 5, DateAlign = 1,
                PreviewBg = Color.FromArgb(255, 20, 10, 10), PreviewClockColor = Color.FromArgb(255, 255, 105, 180) },
            new Preset { Name = "Fire", Subtitle = "Feel the heat.",
                ClockStyle = 1, ClockSize = 3, ClockColor = 4, ClockBlend = 6, DateAlign = 0,
                PreviewBg = Color.FromArgb(255, 40, 10, 5), PreviewClockColor = Color.FromArgb(255, 255, 80, 0) },
            new Preset { Name = "Ocean", Subtitle = "Calm waves, deep blue.",
                ClockStyle = 7, ClockSize = 2, ClockColor = 2, ClockBlend = 2, DateAlign = 1,
                PreviewBg = Color.FromArgb(255, 10, 30, 60), PreviewClockColor = Color.FromArgb(255, 0, 180, 255) },
            new Preset { Name = "Aurora", Subtitle = "Northern lights on your screen.",
                ClockStyle = 2, ClockSize = 2, ClockColor = 5, ClockBlend = 3, DateAlign = 1,
                PreviewBg = Color.FromArgb(255, 10, 20, 30), PreviewClockColor = Color.FromArgb(255, 80, 255, 120) },
            new Preset { Name = "Poppins", Subtitle = "Modern geometric beauty.",
                ClockStyle = 8, ClockSize = 3, ClockColor = 3, ClockBlend = 0, DateAlign = 1,
                PreviewBg = Color.FromArgb(255, 60, 40, 50), PreviewClockColor = Color.FromArgb(255, 255, 182, 193) },
            new Preset { Name = "Twilight", Subtitle = "Between day and night.",
                ClockStyle = 4, ClockSize = 2, ClockColor = 6, ClockBlend = 9, DateAlign = 1,
                PreviewBg = Color.FromArgb(255, 25, 10, 50), PreviewClockColor = Color.FromArgb(255, 148, 100, 255) },
            new Preset { Name = "Ice", Subtitle = "Cool and crisp.",
                ClockStyle = 9, ClockSize = 2, ClockColor = 0, ClockBlend = 7, DateAlign = 1,
                PreviewBg = Color.FromArgb(255, 200, 220, 240), PreviewClockColor = Colors.White },
            new Preset { Name = "Lime", Subtitle = "Fresh and vibrant energy.",
                ClockStyle = 7, ClockSize = 3, ClockColor = 5, ClockBlend = 8, DateAlign = 0,
                PreviewBg = Color.FromArgb(255, 15, 40, 15), PreviewClockColor = Color.FromArgb(255, 100, 255, 100) },
        };

        #endregion

        private int currentIndex = 0;
        private const double CARD_W = 200;
        private const double CARD_H = 360;
        private const double CARD_GAP = 16;
        private const double CARD_STEP = CARD_W + CARD_GAP;
        private const double SCREEN_W = 480.0;

        private List<Border> cards = new List<Border>();
        private List<Ellipse> dots = new List<Ellipse>();

        private double offsetX; // current horizontal offset (positive = first card visible)

        private static readonly FontFamily[] PreviewFonts = {
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
        private Dictionary<int, BitmapImage> presetWallpapers = new Dictionary<int, BitmapImage>();
        private BitmapImage globalForeground;

        public MySetsPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadPresetWallpapers();
            LoadForegroundImage();
            ReadSavedPresets();
            BuildCards();
            GoToIndex(0, false);
        }

        private void LoadForegroundImage()
        {
            globalForeground = null;
            try
            {
                using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    if (store.FileExists("Foreground.png"))
                    {
                        using (var stream = store.OpenFile("Foreground.png",
                            System.IO.FileMode.Open, System.IO.FileAccess.Read))
                        {
                            var bmp = new BitmapImage();
                            bmp.SetSource(stream);
                            globalForeground = bmp;
                        }
                    }
                }
            }
            catch { }
        }

        private void LoadPresetWallpapers()
        {
            presetWallpapers.Clear();

            try
            {
                using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    // Only load per-preset wallpapers — no fallback to shared Background.jpg
                    for (int i = 0; i < Presets.Count; i++)
                    {
                        string file = "Background_" + i + ".jpg";
                        if (store.FileExists(file))
                        {
                            using (var stream = store.OpenFile(file,
                                System.IO.FileMode.Open, System.IO.FileAccess.Read))
                            {
                                var bmp = new BitmapImage();
                                bmp.SetSource(stream);
                                presetWallpapers[i] = bmp;
                            }
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Read saved preset settings from IsolatedStorage and override defaults.
        /// </summary>
        private void ReadSavedPresets()
        {
            var s = IsolatedStorageSettings.ApplicationSettings;

            // First preset (Classic) reflects the live/active settings
            var first = Presets[0];
            first.ClockStyle = GetSetting(s, "ClockStyle", first.ClockStyle);
            first.ClockSize = GetSetting(s, "ClockSize", first.ClockSize);
            first.ClockColor = GetSetting(s, "ClockColor", first.ClockColor);
            first.ClockBlend = GetSetting(s, "ClockBlend", first.ClockBlend);
            first.DateAlign = GetSetting(s, "DateAlign", first.DateAlign);
            first.ClockX = GetSetting(s, "ClockX", first.ClockX);
            first.ClockY = GetSetting(s, "ClockY", first.ClockY);
            first.UseDepthEffect = GetSetting(s, "UseDepthEffect", false);
            first.DepthHourBehind = GetSetting(s, "DepthHourBehind", true);
            first.DepthColonBehind = GetSetting(s, "DepthColonBehind", true);
            first.DepthMinuteBehind = GetSetting(s, "DepthMinuteBehind", true);
            first.PreviewClockColor = ResolveClockColor(first.ClockColor, first.ClockBlend);

            // Other presets use their own saved values
            for (int i = 1; i < Presets.Count; i++)
            {
                string prefix = "Set" + i + "_";
                if (s.Contains(prefix + "ClockStyle"))
                {
                    var p = Presets[i];
                    p.ClockStyle = GetSetting(s, prefix + "ClockStyle", p.ClockStyle);
                    p.ClockSize = GetSetting(s, prefix + "ClockSize", p.ClockSize);
                    p.ClockColor = GetSetting(s, prefix + "ClockColor", p.ClockColor);
                    p.ClockBlend = GetSetting(s, prefix + "ClockBlend", p.ClockBlend);
                    p.DateAlign = GetSetting(s, prefix + "DateAlign", p.DateAlign);
                    p.ClockX = GetSetting(s, prefix + "ClockX", p.ClockX);
                    p.ClockY = GetSetting(s, prefix + "ClockY", p.ClockY);
                    p.UseDepthEffect = GetSetting(s, prefix + "UseDepthEffect", false);
                    p.DepthHourBehind = GetSetting(s, prefix + "DepthHourBehind", true);
                    p.DepthColonBehind = GetSetting(s, prefix + "DepthColonBehind", true);
                    p.DepthMinuteBehind = GetSetting(s, prefix + "DepthMinuteBehind", true);
                    p.PreviewClockColor = ResolveClockColor(p.ClockColor, p.ClockBlend);
                }
            }
        }

        private static Color ResolveClockColor(int colorIdx, int blendIdx)
        {
            if (blendIdx > 0)
            {
                switch (blendIdx)
                {
                    case 1: return Color.FromArgb(255, 255, 120, 50);   // Sunset
                    case 2: return Color.FromArgb(255, 0, 180, 255);    // Ocean
                    case 3: return Color.FromArgb(255, 0, 255, 150);    // Aurora
                    case 4: return Color.FromArgb(255, 255, 0, 200);    // Neon
                    case 5: return Color.FromArgb(255, 255, 105, 180);  // Rose
                    case 6: return Color.FromArgb(255, 255, 50, 0);     // Fire
                    case 7: return Colors.White;                         // Ice
                    case 8: return Color.FromArgb(255, 50, 205, 50);    // Lime
                    case 9: return Color.FromArgb(255, 75, 0, 130);     // Twilight
                    default: return Colors.White;
                }
            }
            switch (colorIdx)
            {
                case 1: return Color.FromArgb(255, 255, 215, 0);   // Gold
                case 2: return Color.FromArgb(255, 135, 206, 235); // Sky Blue
                case 3: return Color.FromArgb(255, 255, 182, 193); // Pink
                case 4: return Color.FromArgb(255, 255, 68, 68);   // Red
                case 5: return Color.FromArgb(255, 91, 255, 176);  // Mint
                case 6: return Color.FromArgb(255, 196, 167, 255); // Lavender
                case 7: return Color.FromArgb(255, 255, 140, 66);  // Orange
                case 8: return Color.FromArgb(255, 0, 229, 255);   // Cyan
                case 9: return Color.FromArgb(255, 160, 160, 176); // Silver
                default: return Colors.White;
            }
        }

        private static T GetSetting<T>(IsolatedStorageSettings s, string key, T def)
        {
            if (s.Contains(key))
            {
                try { return (T)s[key]; }
                catch { }
            }
            return def;
        }

        #region Build Cards

        private void BuildCards()
        {
            CarouselCanvas.Children.Clear();
            cards.Clear();
            DotsPanel.Children.Clear();
            dots.Clear();

            for (int i = 0; i < Presets.Count; i++)
            {
                var preset = Presets[i];
                var card = CreateCard(preset, i);
                cards.Add(card);
                CarouselCanvas.Children.Add(card);

                // Canvas.Top centers card vertically in the canvas area
                Canvas.SetTop(card, 30);

                // Dot
                var dot = new Ellipse
                {
                    Width = 6, Height = 6,
                    Fill = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                    Margin = new Thickness(3, 0, 3, 0)
                };
                dots.Add(dot);
                DotsPanel.Children.Add(dot);
            }
        }

        private Border CreateCard(Preset preset, int index)
        {
            Brush bg;
            BitmapImage wp;
            if (presetWallpapers.TryGetValue(index, out wp))
                bg = new ImageBrush { ImageSource = wp, Stretch = Stretch.UniformToFill };
            else
                bg = new SolidColorBrush(preset.PreviewBg);

            var card = new Border
            {
                Width = CARD_W,
                Height = CARD_H,
                CornerRadius = new CornerRadius(24),
                Background = bg,
                Tag = index,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(),
            };

            var inner = new Grid { Width = CARD_W, Height = CARD_H };
            card.Child = inner;

            // Gradient overlay
            inner.Children.Add(new Rectangle
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                Height = 160,
                IsHitTestVisible = false,
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1),
                    GradientStops = {
                        new GradientStop { Color = Color.FromArgb(0, 0, 0, 0), Offset = 0 },
                        new GradientStop { Color = Color.FromArgb(140, 0, 0, 0), Offset = 1 }
                    }
                }
            });

            // Clock setup
            int fi = Math.Min(preset.ClockStyle, PreviewFonts.Length - 1);
            int[] sizes = { 36, 42, 48, 54, 64 };
            int sz = sizes[Math.Min(preset.ClockSize, sizes.Length - 1)];
            var brush = new SolidColorBrush(preset.PreviewClockColor);
            var transBrush = new SolidColorBrush(Colors.Transparent);
            bool hasDepth = preset.UseDepthEffect && globalForeground != null;

            // --- BEHIND LAYER (or full layer if no depth) ---
            var behindStack = BuildClockStack(preset, fi, sz,
                hasDepth ? (preset.DepthHourBehind ? brush : transBrush) : brush,
                hasDepth ? (preset.DepthColonBehind ? brush : transBrush) : brush,
                hasDepth ? (preset.DepthMinuteBehind ? brush : transBrush) : brush,
                hasDepth ? transBrush : new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)));
            inner.Children.Add(behindStack);

            // --- FOREGROUND OVERLAY ---
            if (hasDepth)
            {
                inner.Children.Add(new Border
                {
                    Background = new ImageBrush { ImageSource = globalForeground, Stretch = Stretch.UniformToFill },
                    IsHitTestVisible = false
                });

                // --- FRONT LAYER (parts NOT behind) ---
                var frontStack = BuildClockStack(preset, fi, sz,
                    preset.DepthHourBehind ? transBrush : brush,
                    preset.DepthColonBehind ? transBrush : brush,
                    preset.DepthMinuteBehind ? transBrush : brush,
                    new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)));
                inner.Children.Add(frontStack);
            }

            // Frame border
            inner.Children.Add(new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(24),
                IsHitTestVisible = false
            });

            return card;
        }

        private StackPanel BuildClockStack(Preset preset, int fi, int sz,
            Brush hourBrush, Brush colonBrush, Brush minuteBrush, Brush dateBrush)
        {
            var stack = new StackPanel();
            if (preset.ClockX >= 0 && preset.ClockY >= 0)
            {
                double scaleX = CARD_W / 480.0;
                double scaleY = CARD_H / 800.0;
                stack.HorizontalAlignment = HorizontalAlignment.Left;
                stack.VerticalAlignment = VerticalAlignment.Top;
                stack.Margin = new Thickness(preset.ClockX * scaleX, preset.ClockY * scaleY, 0, 0);
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
                Text = DateTime.Now.ToString("ddd  ·  MMM dd"),
                FontFamily = new FontFamily("/Assets/Fonts/MiSans-Regular.ttf#MiSans"),
                FontSize = 10, Foreground = dateBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 2)
            });

            var timePanel = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            timePanel.Children.Add(new TextBlock { Text = DateTime.Now.ToString("HH"), FontFamily = PreviewFonts[fi], FontSize = sz, Foreground = hourBrush });
            timePanel.Children.Add(new TextBlock { Text = ":", FontFamily = PreviewFonts[fi], FontSize = sz, Foreground = colonBrush, Margin = new Thickness(0, -sz * 0.08, 0, 0) });
            timePanel.Children.Add(new TextBlock { Text = DateTime.Now.ToString("mm"), FontFamily = PreviewFonts[fi], FontSize = sz, Foreground = minuteBrush });
            stack.Children.Add(timePanel);

            return stack;
        }

        #endregion

        #region Drag & Snap

        private double totalDragX;

        private void Carousel_ManipulationDelta(object sender, System.Windows.Input.ManipulationDeltaEventArgs e)
        {
            offsetX += e.DeltaManipulation.Translation.X;
            totalDragX += Math.Abs(e.DeltaManipulation.Translation.X);

            // Clamp with slight rubber-band
            double maxOff = 40;
            double minOff = -(Presets.Count - 1) * CARD_STEP - 40;
            offsetX = Math.Max(minOff, Math.Min(maxOff, offsetX));

            LayoutCards();
            e.Handled = true;
        }

        private void Carousel_ManipulationCompleted(object sender, System.Windows.Input.ManipulationCompletedEventArgs e)
        {
            if (totalDragX < 15)
            {
                // This was a tap, not a swipe
                // Use the original touch position to determine left/center/right
                var origin = e.ManipulationOrigin;

                // Transform origin to screen coordinates via the source element
                var src = e.OriginalSource as UIElement;
                if (src != null)
                {
                    try
                    {
                        var transform = src.TransformToVisual(Application.Current.RootVisual);
                        var screenPt = transform.Transform(origin);
                        double tapX = screenPt.X;
                        double cardLeft = (SCREEN_W - CARD_W) / 2.0;
                        double cardRight = (SCREEN_W + CARD_W) / 2.0;

                        if (tapX > cardRight)
                        {
                            // Tapped right side → next card
                            totalDragX = 0;
                            GoToIndex(currentIndex + 1, true);
                            e.Handled = true;
                            return;
                        }
                        else if (tapX < cardLeft)
                        {
                            // Tapped left side → previous card
                            totalDragX = 0;
                            GoToIndex(currentIndex - 1, true);
                            e.Handled = true;
                            return;
                        }
                    }
                    catch { }
                }
            }

            totalDragX = 0;

            // Snap to nearest
            int idx = (int)Math.Round(-offsetX / CARD_STEP);
            idx = Math.Max(0, Math.Min(Presets.Count - 1, idx));
            GoToIndex(idx, true);
            e.Handled = true;
        }

        private void GoToIndex(int index, bool animate)
        {
            currentIndex = Math.Max(0, Math.Min(Presets.Count - 1, index));
            double targetOff = -currentIndex * CARD_STEP;

            if (animate)
            {
                // Smooth animate offsetX → targetOff
                int steps = 12;
                double startOff = offsetX;
                var timer = new System.Windows.Threading.DispatcherTimer();
                timer.Interval = TimeSpan.FromMilliseconds(16);
                int step = 0;
                timer.Tick += (s, ev) =>
                {
                    step++;
                    double t = (double)step / steps;
                    t = 1 - Math.Pow(1 - t, 3); // ease-out cubic
                    offsetX = startOff + (targetOff - startOff) * t;
                    LayoutCards();
                    if (step >= steps)
                    {
                        timer.Stop();
                        offsetX = targetOff;
                        LayoutCards();
                    }
                };
                timer.Start();
            }
            else
            {
                offsetX = targetOff;
                LayoutCards();
            }

            // Update title
            SetTitle.Text = Presets[currentIndex].Name;
            SetSubtitle.Text = Presets[currentIndex].Subtitle;

            // Update dots
            for (int i = 0; i < dots.Count; i++)
            {
                dots[i].Fill = new SolidColorBrush(
                    i == currentIndex ? Colors.White : Color.FromArgb(80, 255, 255, 255));
                dots[i].Width = i == currentIndex ? 8 : 6;
                dots[i].Height = i == currentIndex ? 8 : 6;
            }
        }

        /// <summary>
        /// Position all cards on the Canvas based on offsetX.
        /// Center of screen = 240. Card i's center = i * CARD_STEP + CARD_W/2.
        /// Canvas.Left = screenCenter - cardW/2 + offsetX + i * CARD_STEP
        /// </summary>
        private void LayoutCards()
        {
            double centerX = SCREEN_W / 2.0;

            for (int i = 0; i < cards.Count; i++)
            {
                double left = centerX - CARD_W / 2.0 + offsetX + i * CARD_STEP;
                Canvas.SetLeft(cards[i], left);

                // Scale & opacity based on distance from center
                double dist = Math.Abs(left + CARD_W / 2.0 - centerX);
                double maxDist = CARD_STEP;
                double t = Math.Min(1.0, dist / maxDist); // 0=centered, 1=far

                double scale = 1.0 - 0.15 * t;   // 1.0 → 0.85
                double opacity = 1.0 - 0.5 * t;   // 1.0 → 0.5

                var ct = (ScaleTransform)cards[i].RenderTransform;
                ct.ScaleX = scale;
                ct.ScaleY = scale;
                cards[i].Opacity = opacity;
            }
        }

        #endregion

        #region Actions

        private void Customise_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            // Apply defaults for this preset first
            var preset = Presets[currentIndex];
            var s = IsolatedStorageSettings.ApplicationSettings;
            s["ClockStyle"] = preset.ClockStyle;
            s["ClockSize"] = preset.ClockSize;
            s["ClockColor"] = preset.ClockColor;
            s["ClockBlend"] = preset.ClockBlend;
            s["DateAlign"] = preset.DateAlign;
            s.Save();

            // Navigate to editor with preset index
            NavigationService.Navigate(
                new Uri("/Pages/EditorPage.xaml?preset=" + currentIndex, UriKind.Relative));
        }

        private void Settings_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            NavigationService.Navigate(
                new Uri("/Pages/SettingsPage.xaml", UriKind.Relative));
        }

        #endregion

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Rebuild cards to reflect any saved changes
            if (cards.Count > 0)
            {
                int savedIdx = currentIndex;
                LoadPresetWallpapers();
                ReadSavedPresets();
                BuildCards();
                GoToIndex(savedIdx, false);
            }

            while (NavigationService.CanGoBack)
                NavigationService.RemoveBackEntry();
        }
    }
}
