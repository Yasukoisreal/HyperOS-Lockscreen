using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MC = System.Windows.Media.Color;

namespace HyperOS.Helpers
{
    /// <summary>
    /// Shared clock rendering utilities used by EditorPage, LockScreen, and MySetsPage.
    /// All clock drawing logic lives here so changes propagate everywhere.
    /// </summary>
    public static class ClockRenderer
    {
        #region Shared Data

        public static readonly FontFamily[] Fonts = {
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
            new FontFamily("/Assets/Fonts/PlayfairDisplay-Italic.ttf#Playfair Display"),
            new FontFamily("/Assets/Fonts/BodoniModa-Regular.ttf#Bodoni Moda"),
            new FontFamily("/Assets/Fonts/BodoniModa-Italic.ttf#Bodoni Moda"),
            new FontFamily("Segoe WP"),
            new FontFamily("Segoe WP Black"),
        };

        public static readonly string[] FontNames = {
            "MiSans Regular", "MiSans Demibold", "MiSans Light",
            "Bebas Neue", "Playfair Display", "DM Serif Display",
            "Instrument Serif", "Montserrat Bold", "Poppins SemiBold",
            "Raleway Light", "Abril Fatface", "Playfair Display Italic",
            "Bodoni Moda", "Bodoni Moda Italic",
            "Segoe WP", "Segoe WP Black"
        };

        public static readonly int[] SizeValues = { 80, 95, 105, 120, 140 };

        #endregion

        #region Analog Clock Drawing

        /// <summary>
        /// Draws an analog clock face on the given canvas.
        /// style: 2=Minimal (hands only), 3=Classic (12/3/6/9), 4=Swiss (tick marks)
        /// </summary>
        public static void DrawAnalogClock(Canvas canvas, double diameter, int hour, int minute, int style, Brush clockBrush)
        {
            double cx = diameter / 2;
            double cy = diameter / 2;
            double r = diameter / 2 - 4;

            // Prevent memory leak on WP8.1: reuse existing elements if already drawn
            if (canvas.Children.Count > 0)
            {
                Rectangle hHand = null, mHand = null;
                foreach (UIElement el in canvas.Children)
                {
                    var fe = el as FrameworkElement;
                    if (fe != null)
                    {
                        if ((string)fe.Tag == "HourHand") hHand = el as Rectangle;
                        else if ((string)fe.Tag == "MinuteHand") mHand = el as Rectangle;
                    }
                }
                if (hHand != null && mHand != null)
                {
                    double haRot = ((hour % 12) + minute / 60.0) * 30 + 180;
                    var ht = (TransformGroup)hHand.RenderTransform;
                    if (ht.Children.Count > 0)
                    {
                        var hr = ht.Children[0] as RotateTransform;
                        if (hr != null) hr.Angle = haRot;
                    }

                    double maRot = minute * 6 + 180;
                    var mt = (TransformGroup)mHand.RenderTransform;
                    if (mt.Children.Count > 0)
                    {
                        var mr = mt.Children[0] as RotateTransform;
                        if (mr != null) mr.Angle = maRot;
                    }
                }
                return;
            }

            canvas.Children.Clear();

            Brush shapeBrush = clockBrush;
            Brush textBrush = clockBrush;

            // Outer circle
            var circle = new Ellipse
            {
                Width = diameter - 8,
                Height = diameter - 8,
                Stroke = shapeBrush,
                StrokeThickness = 2,
                Fill = new SolidColorBrush(MC.FromArgb(20, 255, 255, 255)),
                Opacity = 0.6
            };
            Canvas.SetLeft(circle, 4);
            Canvas.SetTop(circle, 4);
            canvas.Children.Add(circle);

            // Face decoration
            if (style == 3) // Classic — 12, 3, 6, 9
            {
                string[] nums = { "12", "3", "6", "9" };
                double[] angles = { -90, 0, 90, 180 };
                for (int i = 0; i < 4; i++)
                {
                    double a = angles[i] * Math.PI / 180;
                    double nr = r * 0.78;
                    var tb = new TextBlock
                    {
                        Text = nums[i],
                        FontSize = diameter * 0.1,
                        Foreground = textBrush,
                        FontFamily = new FontFamily("/Assets/Fonts/MiSans-Regular.ttf#MiSans"),
                        Opacity = 0.8
                    };
                    double tw = nums[i].Length * diameter * 0.05;
                    double th = diameter * 0.1;
                    Canvas.SetLeft(tb, cx + nr * Math.Cos(a) - tw / 2);
                    Canvas.SetTop(tb, cy + nr * Math.Sin(a) - th / 2);
                    canvas.Children.Add(tb);
                }
            }
            else if (style == 4) // Swiss — tick marks
            {
                for (int i = 0; i < 12; i++)
                {
                    double tickAngle = i * 30 + 180;
                    double r1 = r * 0.85;
                    double r2 = (i % 3 == 0) ? r * 0.7 : r * 0.78;
                    double thick = (i % 3 == 0) ? 2.5 : 1.2;
                    double tickLen = r1 - r2;
                    
                    var tgTick = new TransformGroup();
                    tgTick.Children.Add(new TranslateTransform { Y = r2 });
                    tgTick.Children.Add(new RotateTransform { Angle = tickAngle, CenterX = thick / 2, CenterY = 0 });

                    var tick = new Rectangle
                    {
                        Width = thick,
                        Height = tickLen,
                        Fill = shapeBrush,
                        RenderTransform = tgTick,
                        Opacity = 0.8
                    };
                    Canvas.SetLeft(tick, cx - thick / 2);
                    Canvas.SetTop(tick, cy);
                    canvas.Children.Add(tick);
                }
            }
            // style == 2: Minimal — no decorations

            // Hour hand
            double haRotate = ((hour % 12) + minute / 60.0) * 30 + 180;
            double hourLen = r * 0.5;
            var tgHour = new TransformGroup();
            tgHour.Children.Add(new RotateTransform { Angle = haRotate, CenterX = 2, CenterY = 0 });
            var hourHand = new Rectangle
            {
                Tag = "HourHand",
                Width = 4,
                Height = hourLen,
                Fill = shapeBrush,
                RadiusX = 2, RadiusY = 2,
                RenderTransform = tgHour
            };
            Canvas.SetLeft(hourHand, cx - 2);
            Canvas.SetTop(hourHand, cy);
            canvas.Children.Add(hourHand);

            // Minute hand
            double maRotate = minute * 6 + 180;
            double minLen = r * 0.72;
            var tgMin = new TransformGroup();
            tgMin.Children.Add(new RotateTransform { Angle = maRotate, CenterX = 1.5, CenterY = 0 });
            var minHand = new Rectangle
            {
                Tag = "MinuteHand",
                Width = 3,
                Height = minLen,
                Fill = shapeBrush,
                RadiusX = 1.5, RadiusY = 1.5,
                RenderTransform = tgMin
            };
            Canvas.SetLeft(minHand, cx - 1.5);
            Canvas.SetTop(minHand, cy);
            canvas.Children.Add(minHand);

            // Center dot
            var dot = new Ellipse { Width = 8, Height = 8, Fill = clockBrush };
            Canvas.SetLeft(dot, cx - 4);
            Canvas.SetTop(dot, cy - 4);
            canvas.Children.Add(dot);
        }

        #endregion

        #region Card Preview Building

        /// <summary>
        /// Builds a complete clock preview StackPanel for card display (My Sets).
        /// Used by both MySetsPage and LockScreen overlay.
        /// </summary>
        /// <param name="clockLayout">0=Horiz, 1=Vert, 2=Minimal, 3=Classic, 4=Swiss</param>
        /// <param name="clockStyle">Font index</param>
        /// <param name="clockSize">Size index</param>
        /// <param name="clockX">X position (-1 = center)</param>
        /// <param name="clockY">Y position (-1 = center)</param>
        /// <param name="cardW">Card width</param>
        /// <param name="cardH">Card height</param>
        /// <param name="hourBrush">Brush for hour</param>
        /// <param name="colonBrush">Brush for colon</param>
        /// <param name="minuteBrush">Brush for minute</param>
        /// <param name="dateBrush">Brush for date text</param>
        public static Grid BuildCardPreview(int clockLayout, int clockStyle, int sizeIdx,
            double clockX, double clockY, int dateAlign, double cardW, double cardH,
            Brush hourBrush, Brush colonBrush, Brush minuteBrush, Brush dateBrush, int presetIndex = -1)
        {
            int fi = Math.Max(0, Math.Min(clockStyle, Fonts.Length - 1));
            int si = Math.Max(0, Math.Min(sizeIdx, SizeValues.Length - 1));
            int sz = SizeValues[si];

            // Scale font for card
            double scale = cardW / 480.0;
            sz = Math.Max(16, (int)(sz * scale));

            var containerGrid = new Grid { Width = cardW, Height = cardH };
            var stack = new StackPanel();
            if (clockX >= 0 && clockY >= 0)
            {
                double scaleX = cardW / 480.0;
                double scaleY = cardH / 800.0;
                stack.HorizontalAlignment = HorizontalAlignment.Left;
                stack.VerticalAlignment = VerticalAlignment.Top;
                stack.Margin = new Thickness(clockX * scaleX, clockY * scaleY, 0, 0);
            }
            else
            {
                stack.VerticalAlignment = VerticalAlignment.Center;
                stack.HorizontalAlignment = HorizontalAlignment.Center;
                stack.Margin = new Thickness(0, -10, 0, 0);
            }
            stack.IsHitTestVisible = false;

            // Draw Signature (Behind layer only, which means when we're drawing the date/hour that are behind)
            // To simplify, we draw it if dateBrush is not transparent
            var scb = dateBrush as SolidColorBrush;
            if (scb != null && scb.Color.A > 0)
            {
                var s = System.IO.IsolatedStorage.IsolatedStorageSettings.ApplicationSettings;
                string pfx = presetIndex >= 0 ? ("Set" + presetIndex + "_") : "";
                
                bool showSig = s.Contains(pfx + "ShowSignature") && (bool)s[pfx + "ShowSignature"];
                if (showSig)
                {
                    string sigText = s.Contains(pfx + "SignatureText") ? (string)s[pfx + "SignatureText"] : "";
                    if (!string.IsNullOrWhiteSpace(sigText))
                    {
                        int fontIdx = s.Contains(pfx + "SignatureFont") ? (int)s[pfx + "SignatureFont"] : 0;
                        double sigSpacing = s.Contains(pfx + "SignatureSpacing") ? (double)s[pfx + "SignatureSpacing"] : 0;
                        int sigAlign = s.Contains(pfx + "SignatureAlign") ? (int)s[pfx + "SignatureAlign"] : 1;
                        int sigLayout = s.Contains(pfx + "SignatureLayout") ? (int)s[pfx + "SignatureLayout"] : 0;
                        double sigX = s.Contains(pfx + "SignatureX") ? (double)s[pfx + "SignatureX"] : -1;
                        double sigY = s.Contains(pfx + "SignatureY") ? (double)s[pfx + "SignatureY"] : -1;
                        int sigColorIdx = s.Contains(pfx + "SignatureColor") ? (int)s[pfx + "SignatureColor"] : 0;
                        int sigBlend = s.Contains(pfx + "SignatureBlend") ? (int)s[pfx + "SignatureBlend"] : 0;

                        var sigFont = GetFont(fontIdx);
                        MC sigColor = ResolveClockColor(sigColorIdx, sigBlend);
                        var sigBrush = new SolidColorBrush(sigColor);

                        var sigBlock = new TextBlock
                        {
                            Text = sigLayout == 1 ? string.Join("\n", sigText.ToCharArray()) : sigText,
                            FontFamily = sigFont,
                            FontSize = Math.Max(10, 48 * scale),
                            Foreground = sigBrush,
                            CharacterSpacing = (int)sigSpacing
                        };
                        if (sigLayout == 1)
                        {
                            sigBlock.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
                            sigBlock.LineHeight = (48 + (sigSpacing / 20.0)) * scale;
                        }
                        else
                        {
                            sigBlock.LineHeight = 0;
                        }

                        if (sigX >= 0 && sigY >= 0)
                        {
                            sigBlock.HorizontalAlignment = HorizontalAlignment.Left;
                            sigBlock.VerticalAlignment = VerticalAlignment.Top;
                            sigBlock.Margin = new Thickness(sigX * scale, sigY * (cardH / 800.0), 0, 0);
                        }
                        else
                        {
                            sigBlock.HorizontalAlignment = HorizontalAlignment.Center;
                            sigBlock.VerticalAlignment = VerticalAlignment.Top;
                            sigBlock.Margin = new Thickness(0, 720 * (cardH / 800.0), 0, 0);
                        }
                        
                        switch (sigAlign)
                        {
                            case 0: sigBlock.TextAlignment = TextAlignment.Left; break;
                            case 2: sigBlock.TextAlignment = TextAlignment.Right; break;
                            default: sigBlock.TextAlignment = TextAlignment.Center; break;
                        }

                        containerGrid.Children.Add(sigBlock);
                    }
                }
            }

            // Date alignment from preset
            HorizontalAlignment dateHAlign;
            switch (dateAlign)
            {
                case 0: dateHAlign = HorizontalAlignment.Left; break;
                case 2: dateHAlign = HorizontalAlignment.Right; break;
                default: dateHAlign = HorizontalAlignment.Center; break;
            }
            stack.Children.Add(new TextBlock
            {
                Text = DateTime.Now.DayOfWeek.ToString() + " \u00b7 " + DateTime.Now.ToString("MMMM d"),
                FontFamily = new FontFamily("/Assets/Fonts/MiSans-Regular.ttf#MiSans"),
                FontSize = Math.Max(8, 20 * scale),
                Foreground = dateBrush,
                HorizontalAlignment = dateHAlign,
                Margin = new Thickness(0, 0, 0, 2)
            });

            // Time
            if (clockLayout >= 2 && clockLayout <= 4)
            {
                // Analog clock
                double diameter = sz * 1.6;
                var analogCanvas = new Canvas
                {
                    Width = diameter,
                    Height = diameter,
                    HorizontalAlignment = dateHAlign
                };
                DrawAnalogClock(analogCanvas, diameter, DateTime.Now.Hour, DateTime.Now.Minute, clockLayout, hourBrush);
                stack.Children.Add(analogCanvas);
            }
            else if (clockLayout == 5)
            {
                // Rhombus digital
                var timeGrid = new Grid
                {
                    HorizontalAlignment = dateHAlign,
                    Margin = new Thickness(0, -sz * 0.15, 0, 0)
                };
                timeGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                timeGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                timeGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                timeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                timeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                timeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                string hStr = DateTime.Now.ToString("HH");
                string mStr = DateTime.Now.ToString("mm");
                double rhombSz = sz * 1.2;

                var h1 = new TextBlock { Text = hStr[0].ToString(), FontFamily = Fonts[fi], FontSize = rhombSz, Foreground = hourBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, -rhombSz * 0.1) };
                Grid.SetRow(h1, 0); Grid.SetColumn(h1, 1);
                
                var h2 = new TextBlock { Text = hStr[1].ToString(), FontFamily = Fonts[fi], FontSize = rhombSz, Foreground = hourBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, -rhombSz * 0.05, 0) };
                Grid.SetRow(h2, 1); Grid.SetColumn(h2, 0);

                var m1 = new TextBlock { Text = mStr[0].ToString(), FontFamily = Fonts[fi], FontSize = rhombSz, Foreground = minuteBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(-rhombSz * 0.05, 0, 0, 0) };
                Grid.SetRow(m1, 1); Grid.SetColumn(m1, 2);

                var m2 = new TextBlock { Text = mStr[1].ToString(), FontFamily = Fonts[fi], FontSize = rhombSz, Foreground = minuteBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, -rhombSz * 0.1, 0, 0) };
                Grid.SetRow(m2, 2); Grid.SetColumn(m2, 1);

                var dot = new System.Windows.Shapes.Ellipse { Width = Math.Max(4, 12 * scale), Height = Math.Max(4, 12 * scale), Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 220, 50, 50)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetRow(dot, 1); Grid.SetColumn(dot, 1);

                timeGrid.Children.Add(h1);
                timeGrid.Children.Add(h2);
                timeGrid.Children.Add(m1);
                timeGrid.Children.Add(m2);
                timeGrid.Children.Add(dot);

                stack.Children.Add(timeGrid);
            }
            else if (clockLayout == 6)
            {
                // Giant digital
                var timeP = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = dateHAlign,
                    Margin = new Thickness(0, -sz * 0.25, 0, 0)
                };
                timeP.Children.Add(new TextBlock { Text = DateTime.Now.ToString("HH"), FontFamily = Fonts[fi], FontSize = sz * 1.6, Foreground = hourBrush });
                timeP.Children.Add(new TextBlock { Text = DateTime.Now.ToString("mm"), FontFamily = Fonts[fi], FontSize = sz * 1.6, Foreground = minuteBrush, Margin = new Thickness(sz * 0.1, 0, 0, 0) });
                stack.Children.Add(timeP);
            }
            else if (clockLayout == 1)
            {
                // Vertical digital
                var timeP = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = dateHAlign,
                    Margin = new Thickness(0, -sz * 0.22, 0, 0)
                };
                timeP.Children.Add(new TextBlock
                {
                    Text = DateTime.Now.ToString("HH"),
                    FontFamily = Fonts[fi],
                    FontSize = sz,
                    Foreground = hourBrush,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                timeP.Children.Add(new TextBlock
                {
                    Text = DateTime.Now.ToString("mm"),
                    FontFamily = Fonts[fi],
                    FontSize = sz,
                    Foreground = minuteBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, -sz * 0.35, 0, 0)
                });
                stack.Children.Add(timeP);
            }
            else
            {
                // Horizontal digital
                var timeP = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = dateHAlign,
                    Margin = new Thickness(0, -sz * 0.16, 0, 0)
                };
                timeP.Children.Add(new TextBlock { Text = DateTime.Now.ToString("HH"), FontFamily = Fonts[fi], FontSize = sz, Foreground = hourBrush });
                timeP.Children.Add(new TextBlock { Text = ":", FontFamily = Fonts[fi], FontSize = sz, Foreground = colonBrush, Margin = new Thickness(0, -sz * 0.08, 0, 0) });
                timeP.Children.Add(new TextBlock { Text = DateTime.Now.ToString("mm"), FontFamily = Fonts[fi], FontSize = sz, Foreground = minuteBrush });
                stack.Children.Add(timeP);
            }
            containerGrid.Children.Add(stack);
            return containerGrid;
        }

        #endregion

        #region Color Resolution

        /// <summary>
        /// Resolves clock color from color index and blend index.
        /// </summary>
        public static MC ResolveClockColor(int colorIdx, int blendIdx)
        {
            if (blendIdx > 0)
            {
                switch (blendIdx)
                {
                    case 1: return MC.FromArgb(255, 255, 120, 50);   // Sunset
                    case 2: return MC.FromArgb(255, 0, 180, 255);    // Ocean
                    case 3: return MC.FromArgb(255, 0, 255, 150);    // Aurora
                    case 4: return MC.FromArgb(255, 255, 0, 200);    // Neon
                    case 5: return MC.FromArgb(255, 255, 105, 180);  // Rose
                    case 6: return MC.FromArgb(255, 255, 50, 0);     // Fire
                    case 7: return Colors.White;                      // Ice
                    case 8: return MC.FromArgb(255, 50, 205, 50);    // Lime
                    case 9: return MC.FromArgb(255, 75, 0, 130);     // Twilight
                    default: return Colors.White;
                }
            }
            switch (colorIdx)
            {
                case 1: return MC.FromArgb(255, 255, 215, 0);   // Gold
                case 2: return MC.FromArgb(255, 135, 206, 235); // Sky Blue
                case 3: return MC.FromArgb(255, 255, 182, 193); // Pink
                case 4: return MC.FromArgb(255, 255, 68, 68);   // Red
                case 5: return MC.FromArgb(255, 91, 255, 176);  // Mint
                case 6: return MC.FromArgb(255, 196, 167, 255); // Lavender
                case 7: return MC.FromArgb(255, 255, 140, 66);  // Orange
                case 8: return MC.FromArgb(255, 0, 229, 255);   // Cyan
                case 9: return MC.FromArgb(255, 160, 160, 176); // Silver
                default: return Colors.White;
            }
        }

        #endregion

        #region Helper

        /// <summary>Safe font index lookup</summary>
        public static FontFamily GetFont(int index)
        {
            return Fonts[Math.Max(0, Math.Min(index, Fonts.Length - 1))];
        }

        /// <summary>Safe size lookup</summary>
        public static int GetSize(int index)
        {
            int i = Math.Max(0, Math.Min(index, SizeValues.Length - 1));
            return SizeValues[i];
        }

        #endregion
    }
}
