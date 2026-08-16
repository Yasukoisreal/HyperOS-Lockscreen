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
                Line hHand = null, mHand = null;
                foreach (UIElement el in canvas.Children)
                {
                    var fe = el as FrameworkElement;
                    if (fe != null)
                    {
                        if ((string)fe.Tag == "HourHand") hHand = el as Line;
                        else if ((string)fe.Tag == "MinuteHand") mHand = el as Line;
                    }
                }
                if (hHand != null && mHand != null)
                {
                    double ha = (((hour % 12) + minute / 60.0) * 30 - 90) * Math.PI / 180;
                    hHand.X2 = cx + (r * 0.5) * Math.Cos(ha);
                    hHand.Y2 = cy + (r * 0.5) * Math.Sin(ha);

                    double ma = (minute * 6 - 90) * Math.PI / 180;
                    mHand.X2 = cx + (r * 0.72) * Math.Cos(ma);
                    mHand.Y2 = cy + (r * 0.72) * Math.Sin(ma);
                }
                return;
            }

            canvas.Children.Clear();

            Brush shapeBrush = clockBrush;
            Brush textBrush = clockBrush;

            if (clockBrush is LinearGradientBrush)
            {
                var oldLg = (LinearGradientBrush)clockBrush;
                var newLg = new LinearGradientBrush();
                newLg.MappingMode = BrushMappingMode.Absolute;
                newLg.StartPoint = new Point(0, 0);
                newLg.EndPoint = new Point(diameter, diameter);
                foreach (var s in oldLg.GradientStops)
                    newLg.GradientStops.Add(new GradientStop { Color = s.Color, Offset = s.Offset });
                shapeBrush = newLg;
            }

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
                    double a = (i * 30 - 90) * Math.PI / 180;
                    double r1 = r * 0.85;
                    double r2 = (i % 3 == 0) ? r * 0.7 : r * 0.78;
                    double thick = (i % 3 == 0) ? 2.5 : 1.2;
                    var tick = new Line
                    {
                        X1 = cx + r1 * Math.Cos(a), Y1 = cy + r1 * Math.Sin(a),
                        X2 = cx + r2 * Math.Cos(a), Y2 = cy + r2 * Math.Sin(a),
                        Stroke = shapeBrush,
                        StrokeThickness = thick,
                        Opacity = 0.8
                    };
                    canvas.Children.Add(tick);
                }
            }
            // style == 2: Minimal — no decorations

            // Hour hand
            double hourAngle = ((hour % 12) + minute / 60.0) * 30 - 90;
            double ha2 = hourAngle * Math.PI / 180;
            double hourLen = r * 0.5;
            var hourHand = new Line
            {
                Tag = "HourHand",
                X1 = cx, Y1 = cy,
                X2 = cx + hourLen * Math.Cos(ha2),
                Y2 = cy + hourLen * Math.Sin(ha2),
                Stroke = shapeBrush,
                StrokeThickness = 4,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            canvas.Children.Add(hourHand);

            // Minute hand
            double minAngle = minute * 6 - 90;
            double ma2 = minAngle * Math.PI / 180;
            double minLen = r * 0.72;
            var minHand = new Line
            {
                Tag = "MinuteHand",
                X1 = cx, Y1 = cy,
                X2 = cx + minLen * Math.Cos(ma2),
                Y2 = cy + minLen * Math.Sin(ma2),
                Stroke = shapeBrush,
                StrokeThickness = 3,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
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
        public static StackPanel BuildCardPreview(
            int clockLayout, int clockStyle, int clockSize,
            double clockX, double clockY, int dateAlign,
            double cardW, double cardH,
            Brush hourBrush, Brush colonBrush, Brush minuteBrush, Brush dateBrush)
        {
            int fi = Math.Max(0, Math.Min(clockStyle, Fonts.Length - 1));
            int si = Math.Max(0, Math.Min(clockSize, SizeValues.Length - 1));
            int sz = SizeValues[si];

            // Scale font for card
            double scale = cardW / 480.0;
            sz = Math.Max(16, (int)(sz * scale));

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
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                DrawAnalogClock(analogCanvas, diameter, DateTime.Now.Hour, DateTime.Now.Minute, clockLayout, hourBrush);
                stack.Children.Add(analogCanvas);
            }
            else if (clockLayout == 5)
            {
                // Rhombus digital
                var timeGrid = new Grid
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
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
                    HorizontalAlignment = HorizontalAlignment.Center,
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
                    HorizontalAlignment = HorizontalAlignment.Center,
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
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, -sz * 0.16, 0, 0)
                };
                timeP.Children.Add(new TextBlock { Text = DateTime.Now.ToString("HH"), FontFamily = Fonts[fi], FontSize = sz, Foreground = hourBrush });
                timeP.Children.Add(new TextBlock { Text = ":", FontFamily = Fonts[fi], FontSize = sz, Foreground = colonBrush, Margin = new Thickness(0, -sz * 0.08, 0, 0) });
                timeP.Children.Add(new TextBlock { Text = DateTime.Now.ToString("mm"), FontFamily = Fonts[fi], FontSize = sz, Foreground = minuteBrush });
                stack.Children.Add(timeP);
            }

            return stack;
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
