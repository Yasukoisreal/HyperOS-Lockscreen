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
using HyperOS.Helpers;
using Microsoft.Phone.Controls;

namespace HyperOS.Pages
{
    public partial class MySetsPage : PhoneApplicationPage
    {
        #region Preset Definition

        private class Preset
        {
            public string Category { get; set; }
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
            public int ClockLayout { get; set; } // 0=Horiz, 1=Vert, 2=Minimal, 3=Classic, 4=Swiss, 5=Rhombus, 6=Giant
            public string BackgroundImage { get; set; } // e.g. "Assets/Pictures/classic02.jpg"

            // Signature
            public bool ShowSignature { get; set; }
            public string SignatureText { get; set; }
            public int SignatureFont { get; set; }
            public double SignatureSpacing { get; set; }
            public int SignatureAlign { get; set; }
            public int SignatureColor { get; set; }
            public int SignatureBlend { get; set; }
            public double SignatureX { get; set; }
            public double SignatureY { get; set; }

            public Preset() { ClockX = -1; ClockY = -1; DepthHourBehind = true; DepthColonBehind = true; DepthMinuteBehind = true; SignatureX = -1; SignatureY = -1; }
        }

        private static readonly List<Preset> Presets = new List<Preset>
        {
            // CLASSIC
            new Preset { Category="Classic", Name="Classic", Subtitle="What's classic never goes out of style.", ClockStyle=0, ClockSize=2, ClockColor=0, ClockBlend=0, DateAlign=0, ClockX=30, ClockY=100, PreviewBg=Color.FromArgb(255,60,60,80), PreviewClockColor=Color.FromArgb(255,255,255,255), BackgroundImage="/Assets/Pictures/classic02.jpg" },
            new Preset { Category="Classic", Name="Ice", Subtitle="Cool and crisp.", ClockStyle=9, ClockSize=2, ClockColor=0, ClockBlend=0, DateAlign=0, ClockX=20, ClockY=50, PreviewBg=Color.FromArgb(255,120,160,200), PreviewClockColor=Color.FromArgb(255,255,255,255), BackgroundImage="/Assets/Pictures/classic03.jpg" },
            new Preset { Category="Classic", Name="Ocean", Subtitle="Calm waves, deep blue.", ClockStyle=0, ClockSize=2, ClockColor=0, ClockBlend=0, DateAlign=0, ClockLayout=3, ClockX=30, ClockY=50, PreviewBg=Color.FromArgb(255,20,100,120), PreviewClockColor=Color.FromArgb(255,255,255,255), BackgroundImage="/Assets/Pictures/AI Static 3.jpg" },
            new Preset { Category="Classic", Name="Analog", Subtitle="Classic analog elegance.", ClockStyle=0, ClockSize=2, ClockColor=0, ClockBlend=0, DateAlign=1, ClockLayout=2, PreviewBg=Color.FromArgb(255,20,20,30), PreviewClockColor=Color.FromArgb(255,255,255,255), BackgroundImage="/Assets/Pictures/AI Static 4.jpg" },
            new Preset { Category="Classic", Name="Classic Clock", Subtitle="Numbers on the dial.", ClockStyle=0, ClockSize=2, ClockColor=0, ClockBlend=0, DateAlign=1, ClockLayout=3, PreviewBg=Color.FromArgb(255,40,30,20), PreviewClockColor=Color.FromArgb(255,255,255,255), BackgroundImage="/Assets/Pictures/magazine01.jpg" },
            new Preset { Category="Classic", Name="Swiss", Subtitle="Precision Swiss design.", ClockStyle=0, ClockSize=2, ClockColor=0, ClockBlend=0, DateAlign=1, ClockLayout=4, PreviewBg=Color.FromArgb(255,10,10,15), PreviewClockColor=Color.FromArgb(255,255,255,255), BackgroundImage="/Assets/Pictures/magazine02.jpg" },

            // RHOMBUS
            new Preset { Category="Rhombus", Name="Floral", Subtitle="Beauty in nature.", ClockStyle=11, ClockSize=3, ClockColor=0, ClockBlend=0, DateAlign=1, ClockLayout=5, ClockX=160, ClockY=100, PreviewBg=Color.FromArgb(255,20,20,20), PreviewClockColor=Color.FromArgb(255,255,255,255), BackgroundImage="/Assets/Pictures/10062799164539040.jpg" },
            new Preset { Category="Rhombus", Name="Blossom", Subtitle="Soft and elegant.", ClockStyle=13, ClockSize=3, ClockColor=0, ClockBlend=0, DateAlign=1, ClockLayout=5, ClockX=160, ClockY=100, PreviewBg=Color.FromArgb(255,40,30,30), PreviewClockColor=Color.FromArgb(255,255,255,255), BackgroundImage="/Assets/Pictures/10696117861097790.jpg" },
            new Preset { Category="Rhombus", Name="Architecture", Subtitle="Structured heights.", ClockStyle=11, ClockSize=3, ClockColor=0, ClockBlend=0, DateAlign=1, ClockLayout=5, ClockX=160, ClockY=100, PreviewBg=Color.FromArgb(255,10,30,40), PreviewClockColor=Color.FromArgb(255,255,255,255), BackgroundImage="/Assets/Pictures/Tall Building Wallpaper.jpg" },
            new Preset { Category="Rhombus", Name="Hyper", Subtitle="Flowing gradient.", ClockStyle=13, ClockSize=3, ClockColor=0, ClockBlend=0, DateAlign=1, ClockLayout=5, ClockX=160, ClockY=100, PreviewBg=Color.FromArgb(255,20,10,40), PreviewClockColor=Color.FromArgb(255,255,255,255), BackgroundImage="/Assets/Pictures/Xiaomi HyperOS Wallpapers.jpg" },

            // MAGAZINE
            new Preset { Category="Magazine", Name="Magazine", Subtitle="Turn your lock screen into a cover.", ClockStyle=6, ClockSize=2, ClockColor=0, ClockBlend=0, DateAlign=0, ClockX=25, ClockY=580, PreviewBg=Color.FromArgb(255,180,140,80), PreviewClockColor=Color.FromArgb(255,255,255,255), BackgroundImage="/Assets/Pictures/magazine01.jpg" },
            new Preset { Category="Magazine", Name="Bold", Subtitle="Make a statement with bold typography.", ClockStyle=3, ClockSize=4, ClockColor=1, ClockBlend=0, DateAlign=0, ClockLayout=1, ClockX=20, ClockY=480, PreviewBg=Color.FromArgb(255,120,20,20), PreviewClockColor=Color.FromArgb(255,255,215,0), BackgroundImage="/Assets/Pictures/east07.jpg" },
            new Preset { Category="Magazine", Name="Elegant", Subtitle="Refined beauty in every detail.", ClockStyle=4, ClockSize=3, ClockColor=1, ClockBlend=0, DateAlign=1, ClockX=220, ClockY=80, PreviewBg=Color.FromArgb(255,15,15,12), PreviewClockColor=Color.FromArgb(255,255,215,0), BackgroundImage="/Assets/Pictures/east05.jpg" },
            new Preset { Category="Magazine", Name="Neon", Subtitle="Electrify your screen.", ClockStyle=0, ClockSize=3, ClockColor=5, ClockBlend=4, DateAlign=0, ClockLayout=1, ClockX=20, ClockY=450, PreviewBg=Color.FromArgb(255,15,50,40), PreviewClockColor=Color.FromArgb(255,80,255,150), BackgroundImage="/Assets/Pictures/east06.jpg" },
            new Preset { Category="Magazine", Name="Minimal", Subtitle="Less is more.", ClockStyle=9, ClockSize=0, ClockColor=0, ClockBlend=0, DateAlign=0, ClockX=30, ClockY=60, PreviewBg=Color.FromArgb(255,60,60,60), PreviewClockColor=Color.FromArgb(255,255,255,255), BackgroundImage="/Assets/Pictures/magazine05.jpg" },
            new Preset { Category="Magazine", Name="Serif", Subtitle="Timeless serif elegance.", ClockStyle=5, ClockSize=3, ClockColor=0, ClockBlend=0, DateAlign=1, ClockLayout=1, PreviewBg=Color.FromArgb(255,20,60,70), PreviewClockColor=Color.FromArgb(255,255,255,255), BackgroundImage="/Assets/Pictures/east02.jpg" },
            new Preset { Category="Magazine", Name="Display", Subtitle="Time is important. Make it count.", ClockStyle=10, ClockSize=4, ClockColor=7, ClockBlend=0, DateAlign=1, ClockX=180, ClockY=600, PreviewBg=Color.FromArgb(255,40,40,120), PreviewClockColor=Color.FromArgb(255,255,140,66), BackgroundImage="/Assets/Pictures/magazine06.jpg" },
            new Preset { Category="Magazine", Name="Fire", Subtitle="Feel the heat.", ClockStyle=7, ClockSize=3, ClockColor=1, ClockBlend=0, DateAlign=0, ClockX=20, ClockY=520, PreviewBg=Color.FromArgb(255,120,30,20), PreviewClockColor=Color.FromArgb(255,255,215,0), BackgroundImage="/Assets/Pictures/east01.jpg" },
            new Preset { Category="Magazine", Name="Aurora", Subtitle="Northern lights on your screen.", ClockStyle=2, ClockSize=2, ClockColor=6, ClockBlend=0, DateAlign=1, PreviewBg=Color.FromArgb(255,15,30,50), PreviewClockColor=Color.FromArgb(255,196,167,255), BackgroundImage="/Assets/Pictures/AI Static 4.jpg" },
            new Preset { Category="Magazine", Name="Poppins", Subtitle="Modern geometric beauty.", ClockStyle=8, ClockSize=1, ClockColor=0, ClockBlend=0, DateAlign=1, ClockX=230, ClockY=40, PreviewBg=Color.FromArgb(255,80,120,90), PreviewClockColor=Color.FromArgb(255,255,255,255), BackgroundImage="/Assets/Pictures/magazine04.jpg" },
            new Preset { Category="Magazine", Name="Twilight", Subtitle="Between day and night.", ClockStyle=10, ClockSize=3, ClockColor=7, ClockBlend=1, DateAlign=0, ClockLayout=1, ClockX=20, ClockY=500, PreviewBg=Color.FromArgb(255,15,20,50), PreviewClockColor=Color.FromArgb(255,255,180,80), BackgroundImage="/Assets/Pictures/magazine02.jpg" },
            new Preset { Category="Magazine", Name="Lime", Subtitle="Fresh and vibrant energy.", ClockStyle=4, ClockSize=2, ClockColor=1, ClockBlend=0, DateAlign=1, ClockX=200, ClockY=580, PreviewBg=Color.FromArgb(255,50,55,60), PreviewClockColor=Color.FromArgb(255,255,215,0), BackgroundImage="/Assets/Pictures/magazine03.jpg" },
            new Preset { Category="Magazine", Name="Vertical", Subtitle="Time stacked vertically.", ClockStyle=3, ClockSize=3, ClockColor=2, ClockBlend=0, DateAlign=0, ClockLayout=1, ClockX=20, ClockY=500, PreviewBg=Color.FromArgb(255,130,180,230), PreviewClockColor=Color.FromArgb(255,135,206,235), BackgroundImage="/Assets/Pictures/magazine07.jpg" },
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

        // Cached dot brushes (CPU: avoids allocating per swipe)
        private static readonly SolidColorBrush DotActive = new SolidColorBrush(Colors.White);
        private static readonly SolidColorBrush DotInactive = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));

        private double offsetX; // current horizontal offset (positive = first card visible)

        // Fonts/sizes now in ClockRenderer shared class
        private Dictionary<int, BitmapImage> presetWallpapers = new Dictionary<int, BitmapImage>();
        private BitmapImage globalForeground;

        public MySetsPage()
        {
            InitializeComponent();
        }

        private bool firstLoad = true;

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (!firstLoad) return; // OnNavigatedTo handles subsequent loads
            firstLoad = false;

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
                            bmp.DecodePixelWidth = 220; // RAM: thumbnail size for cards
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
            // Don't preload — lazy-load in CreateCard with DecodePixelWidth to prevent OOM
            presetWallpapers.Clear();
        }

        // Original defaults — used to reset Presets before reading saved overrides
        private static readonly List<Preset> OriginalDefaults = new List<Preset>();
        private static bool defaultsCaptured = false;

        /// <summary>
        /// Capture the original preset defaults on first access so we can always
        /// reset before reading saved overrides (prevents static mutation).
        /// </summary>
        private void CaptureOriginalDefaults()
        {
            if (defaultsCaptured) return;
            for (int i = 0; i < Presets.Count; i++)
            {
                var src = Presets[i];
                OriginalDefaults.Add(new Preset
                {
                    Name = src.Name, Subtitle = src.Subtitle,
                    ClockStyle = src.ClockStyle, ClockSize = src.ClockSize,
                    ClockColor = src.ClockColor, ClockBlend = src.ClockBlend,
                    DateAlign = src.DateAlign, ClockLayout = src.ClockLayout,
                    ClockX = src.ClockX, ClockY = src.ClockY,
                    UseDepthEffect = src.UseDepthEffect,
                    DepthHourBehind = src.DepthHourBehind,
                    DepthColonBehind = src.DepthColonBehind,
                    DepthMinuteBehind = src.DepthMinuteBehind,
                    PreviewBg = src.PreviewBg,
                    PreviewClockColor = src.PreviewClockColor,
                    BackgroundImage = src.BackgroundImage
                });
            }
            defaultsCaptured = true;
        }

        /// <summary>
        /// Read saved preset settings from IsolatedStorage and override defaults.
        /// Always resets to original defaults first to prevent static mutation.
        /// </summary>
        private void ReadSavedPresets()
        {
            CaptureOriginalDefaults();

            var s = IsolatedStorageSettings.ApplicationSettings;

            // Reset all presets to original defaults first
            for (int i = 0; i < Presets.Count && i < OriginalDefaults.Count; i++)
            {
                var orig = OriginalDefaults[i];
                var p = Presets[i];
                p.ClockStyle = orig.ClockStyle; p.ClockSize = orig.ClockSize;
                p.ClockColor = orig.ClockColor; p.ClockBlend = orig.ClockBlend;
                p.DateAlign = orig.DateAlign; p.ClockLayout = orig.ClockLayout;
                p.ClockX = orig.ClockX; p.ClockY = orig.ClockY;
                p.UseDepthEffect = orig.UseDepthEffect;
                p.DepthHourBehind = orig.DepthHourBehind;
                p.DepthColonBehind = orig.DepthColonBehind;
                p.DepthMinuteBehind = orig.DepthMinuteBehind;
                p.PreviewClockColor = orig.PreviewClockColor;
                p.ShowSignature = orig.ShowSignature;
                p.SignatureText = orig.SignatureText;
                p.SignatureFont = orig.SignatureFont;
                p.SignatureSpacing = orig.SignatureSpacing;
                p.SignatureAlign = orig.SignatureAlign;
                p.SignatureColor = orig.SignatureColor;
                p.SignatureBlend = orig.SignatureBlend;
                p.SignatureX = orig.SignatureX;
                p.SignatureY = orig.SignatureY;
            }

            // Now apply saved overrides
            for (int i = 0; i < Presets.Count; i++)
            {
                string prefix = "Set" + i + "_";
                if (s.Contains(prefix + "ClockStyle") || s.Contains(prefix + "ShowSignature"))
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
                    p.ClockLayout = GetSetting(s, prefix + "ClockLayout", p.ClockLayout);
                    p.PreviewClockColor = ClockRenderer.ResolveClockColor(p.ClockColor, p.ClockBlend);
                    
                    p.ShowSignature = GetSetting(s, prefix + "ShowSignature", p.ShowSignature);
                    p.SignatureText = GetSetting(s, prefix + "SignatureText", p.SignatureText);
                    p.SignatureFont = GetSetting(s, prefix + "SignatureFont", p.SignatureFont);
                    p.SignatureSpacing = GetSetting(s, prefix + "SignatureSpacing", p.SignatureSpacing);
                    p.SignatureAlign = GetSetting(s, prefix + "SignatureAlign", p.SignatureAlign);
                    p.SignatureColor = GetSetting(s, prefix + "SignatureColor", p.SignatureColor);
                    p.SignatureBlend = GetSetting(s, prefix + "SignatureBlend", p.SignatureBlend);
                    p.SignatureX = GetSetting(s, prefix + "SignatureX", p.SignatureX);
                    p.SignatureY = GetSetting(s, prefix + "SignatureY", p.SignatureY);
                }
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
            Brush bg = new SolidColorBrush(preset.PreviewBg); // fallback

            // Lazy-load wallpaper with small decode size to prevent OOM
            try
            {
                using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    string savedFile = "Background_" + index + ".jpg";
                    if (store.FileExists(savedFile))
                    {
                        using (var stream = store.OpenFile(savedFile,
                            System.IO.FileMode.Open, System.IO.FileAccess.Read))
                        {
                            var bmp = new BitmapImage();
                            bmp.DecodePixelWidth = 220; // RAM: thumbnail only
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
                Width = CARD_W,
                Height = CARD_H,
                CornerRadius = new CornerRadius(24),
                Background = bg,
                Tag = index,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(),
            };

            var inner = new Grid { Width = CARD_W, Height = CARD_H };
            inner.Clip = new RectangleGeometry
            {
                Rect = new Rect(0, 0, CARD_W, CARD_H),
                RadiusX = 24, RadiusY = 24
            };
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
            var brush = new SolidColorBrush(preset.PreviewClockColor);
            var transBrush = new SolidColorBrush(Colors.Transparent);
            bool hasDepth = preset.UseDepthEffect && globalForeground != null;

            // --- BEHIND LAYER (or full layer if no depth) ---
            var behindStack = ClockRenderer.BuildCardPreview(
                preset.ClockLayout, preset.ClockStyle, preset.ClockSize,
                preset.ClockX, preset.ClockY, preset.DateAlign, CARD_W, CARD_H,
                hasDepth ? (preset.DepthHourBehind ? brush : transBrush) : brush,
                hasDepth ? (preset.DepthColonBehind ? brush : transBrush) : brush,
                hasDepth ? (preset.DepthMinuteBehind ? brush : transBrush) : brush,
                hasDepth ? transBrush : new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)), index);
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
                var frontStack = ClockRenderer.BuildCardPreview(
                    preset.ClockLayout, preset.ClockStyle, preset.ClockSize,
                    preset.ClockX, preset.ClockY, preset.DateAlign, CARD_W, CARD_H,
                    preset.DepthHourBehind ? transBrush : brush,
                    preset.DepthColonBehind ? transBrush : brush,
                    preset.DepthMinuteBehind ? transBrush : brush,
                    new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)), index);
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


        // Clock rendering now handled by ClockRenderer.BuildCardPreview()


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
            SetCategory.Text = (Presets[currentIndex].Category ?? "").ToUpper();
            SetTitle.Text = Presets[currentIndex].Name;
            SetSubtitle.Text = Presets[currentIndex].Subtitle;

            // Update dots (CPU: reuse cached brushes)
            for (int i = 0; i < dots.Count; i++)
            {
                bool active = i == currentIndex;
                dots[i].Fill = active ? DotActive : DotInactive;
                dots[i].Width = active ? 8 : 6;
                dots[i].Height = active ? 8 : 6;
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

        private void SyncPresetToGlobal(int index)
        {
            var s = IsolatedStorageSettings.ApplicationSettings;
            s["ActivePresetIndex"] = index;

            var preset = Presets[index];
            string px = "Set" + index + "_";

            // If this preset hasn't been saved yet, initialize its default keys
            if (!s.Contains(px + "ClockStyle"))
            {
                s[px + "ClockStyle"] = preset.ClockStyle;
                s[px + "ClockSize"] = preset.ClockSize;
                s[px + "ClockColor"] = preset.ClockColor;
                s[px + "ClockBlend"] = preset.ClockBlend;
                s[px + "DateAlign"] = preset.DateAlign;
                s[px + "ClockLayout"] = preset.ClockLayout;
                // Include position and depth keys (BUG 1 fix)
                if (preset.ClockX >= 0) s[px + "ClockX"] = preset.ClockX;
                if (preset.ClockY >= 0) s[px + "ClockY"] = preset.ClockY;
                s[px + "UseDepthEffect"] = preset.UseDepthEffect;
                s[px + "DepthHourBehind"] = preset.DepthHourBehind;
                s[px + "DepthColonBehind"] = preset.DepthColonBehind;
                s[px + "DepthMinuteBehind"] = preset.DepthMinuteBehind;
            }

            // Sync ALL Set{n}_ keys to global keys so EditorPage and LockScreen read correctly
            string[] keys = { "ClockStyle", "ClockSize", "ClockColor", "ClockBlend", "DateAlign", "ClockLayout",
                "ClockX", "ClockY", "UseDepthEffect", "DepthHourBehind", "DepthColonBehind", "DepthMinuteBehind",
                "ShowWeather", "ShowCountdown", "WeatherX", "WeatherY", "CountdownX", "CountdownY",
                "ShowSignature", "SignatureText", "SignatureFont", "SignatureSpacing", "SignatureAlign", "SignatureColor", "SignatureBlend", "SignatureX", "SignatureY" };
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
            // Ensure background image is passed to Editor/LockScreen
            try
            {
                using (var store = System.IO.IsolatedStorage.IsolatedStorageFile.GetUserStoreForApplication())
                {
                    string presetBg = "Background_" + index + ".jpg";
                    if (store.FileExists(presetBg))
                    {
                        // User has a custom wallpaper saved for this preset
                        if (store.FileExists("Background.jpg"))
                            store.DeleteFile("Background.jpg");
                        store.CopyFile(presetBg, "Background.jpg");
                        
                        s[px + "BackgroundImage"] = null; // Clear fallback string
                        if (s.Contains("BackgroundImage")) s.Remove("BackgroundImage");
                    }
                    else
                    {
                        // No custom wallpaper - copy default from app resources
                        string defaultBg = (preset.BackgroundImage ?? "").TrimStart('/');
                        if (!string.IsNullOrEmpty(defaultBg))
                        {
                            try
                            {
                                var sri = System.Windows.Application.GetResourceStream(new Uri(defaultBg, UriKind.Relative));
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
                        }
                        else
                        {
                            // No wallpaper at all
                            if (store.FileExists("Background.jpg"))
                                store.DeleteFile("Background.jpg");
                            if (s.Contains(px + "BackgroundImage")) s.Remove(px + "BackgroundImage");
                            if (s.Contains("BackgroundImage")) s.Remove("BackgroundImage");
                        }
                    }
                }
            }
            catch { }
            
            s.Save();
        }

        private void Customise_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            SyncPresetToGlobal(currentIndex);

            // Navigate to editor with preset index
            NavigationService.Navigate(
                new Uri("/Pages/EditorPage.xaml?preset=" + currentIndex, UriKind.Relative));
        }

        private void Settings_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            NavigationService.Navigate(
                new Uri("/Pages/SettingsPage.xaml", UriKind.Relative));
        }

        private void Exit_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            if (NavigationService.CanGoBack)
                NavigationService.GoBack();
            else
                System.Windows.Application.Current.Terminate();
        }

        private void Apply_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            SyncPresetToGlobal(currentIndex);
            System.Windows.MessageBox.Show("Preset đã được áp dụng thành công cho màn hình khóa!", "Thành công", System.Windows.MessageBoxButton.OK);

            if (NavigationService.CanGoBack)
                NavigationService.GoBack();
            else
                System.Windows.Application.Current.Terminate();
        }

        #endregion

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Only rebuild cards when returning from editor (Back), not on initial load
            if (cards.Count > 0 && e.NavigationMode == NavigationMode.Back)
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
