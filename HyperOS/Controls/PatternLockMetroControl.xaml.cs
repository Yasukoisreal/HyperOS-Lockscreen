using System;
using System.Collections.Generic;
using System.IO.IsolatedStorage;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace HyperOS.Controls
{
    public partial class PatternLockMetroControl : UserControl
    {
        // Events
        public event EventHandler PatternMatchSuccess;
        public event EventHandler PatternMatchUnsuccess;
        public event EventHandler RegistrationSuccess;

        // Properties
        public bool IsRegisterationMode { get; set; }
        public int Tries { get; set; }

        // Internal state
        private readonly Point[] dotCenters;
        private readonly Ellipse[] dots;
        private readonly Ellipse[] rings;
        private readonly bool[] dotVisited;
        private readonly List<int> currentPattern;
        private string patternToMatch = "";
        private bool isDrawing;
        private Line currentLine;

        // Colors — HyperOS style
        private static readonly SolidColorBrush dotNormal = new SolidColorBrush(
            Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF));  // subtle white
        private static readonly SolidColorBrush dotActive = new SolidColorBrush(
            Color.FromArgb(0xFF, 0x40, 0x9E, 0xFF));  // blue accent
        private static readonly SolidColorBrush ringActive = new SolidColorBrush(
            Color.FromArgb(0x44, 0x40, 0x9E, 0xFF));  // soft blue ring
        private static readonly SolidColorBrush dotSuccess = new SolidColorBrush(
            Color.FromArgb(0xFF, 0x4C, 0xAF, 0x50));  // green
        private static readonly SolidColorBrush ringSuccess = new SolidColorBrush(
            Color.FromArgb(0x44, 0x4C, 0xAF, 0x50));  // soft green ring
        private static readonly SolidColorBrush dotError = new SolidColorBrush(
            Color.FromArgb(0xFF, 0xFF, 0x4B, 0x4B));  // red
        private static readonly SolidColorBrush ringError = new SolidColorBrush(
            Color.FromArgb(0x44, 0xFF, 0x4B, 0x4B));  // soft red ring
        private static readonly SolidColorBrush lineBrush = new SolidColorBrush(
            Color.FromArgb(0x88, 0x40, 0x9E, 0xFF));  // blue line

        public PatternLockMetroControl()
        {
            InitializeComponent();
            Tries = 5;

            dots = new Ellipse[]
            {
                Dot0, Dot1, Dot2,
                Dot3, Dot4, Dot5,
                Dot6, Dot7, Dot8
            };

            rings = new Ellipse[]
            {
                Ring0, Ring1, Ring2,
                Ring3, Ring4, Ring5,
                Ring6, Ring7, Ring8
            };

            // Dot center positions for 320x320 canvas (60px margin, 100px spacing)
            dotCenters = new Point[]
            {
                new Point(60, 60),   new Point(160, 60),  new Point(260, 60),
                new Point(60, 160),  new Point(160, 160), new Point(260, 160),
                new Point(60, 260),  new Point(160, 260), new Point(260, 260)
            };

            dotVisited = new bool[9];
            currentPattern = new List<int>();

            // Load saved pattern
            LoadPattern();
        }

        private void LoadPattern()
        {
            try
            {
                var s = IsolatedStorageSettings.ApplicationSettings;
                if (s.Contains("AppPatternToMatch"))
                    patternToMatch = (string)s["AppPatternToMatch"];
            }
            catch { }
        }

        private void UserControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isDrawing = true;
            currentPattern.Clear();
            ClearLines();
            ResetDots();

            Point pos = e.GetPosition(PatternCanvas);
            CheckDotHit(pos);

            PatternCanvas.CaptureMouse();
        }

        private void UserControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isDrawing) return;

            Point pos = e.GetPosition(PatternCanvas);
            CheckDotHit(pos);

            // Update current tracking line
            if (currentPattern.Count > 0)
            {
                int lastDot = currentPattern[currentPattern.Count - 1];
                if (currentLine != null)
                    PatternCanvas.Children.Remove(currentLine);

                currentLine = new Line
                {
                    X1 = dotCenters[lastDot].X,
                    Y1 = dotCenters[lastDot].Y,
                    X2 = pos.X,
                    Y2 = pos.Y,
                    Stroke = lineBrush,
                    StrokeThickness = 3,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };
                PatternCanvas.Children.Add(currentLine);
            }
        }

        private void UserControl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDrawing = false;
            PatternCanvas.ReleaseMouseCapture();

            // Remove tracking line
            if (currentLine != null)
            {
                PatternCanvas.Children.Remove(currentLine);
                currentLine = null;
            }

            // Check pattern
            if (currentPattern.Count >= 3)
            {
                string pattern = string.Join(",", currentPattern);

                if (IsRegisterationMode)
                {
                    // Registration mode — save pattern
                    var s = IsolatedStorageSettings.ApplicationSettings;
                    s["AppPatternToMatch"] = pattern;
                    s.Save();
                    patternToMatch = pattern;

                    HighlightDotsSuccess();
                    if (RegistrationSuccess != null)
                        RegistrationSuccess(this, EventArgs.Empty);
                }
                else
                {
                    // Match mode
                    if (pattern == patternToMatch)
                    {
                        HighlightDotsSuccess();
                        if (PatternMatchSuccess != null)
                            PatternMatchSuccess(this, EventArgs.Empty);
                    }
                    else
                    {
                        HighlightDotsError();
                        Tries--;
                        if (PatternMatchUnsuccess != null)
                            PatternMatchUnsuccess(this, EventArgs.Empty);
                    }
                }
            }
            else
            {
                // Not enough dots
                ResetDots();
                ClearLines();
            }
        }

        private void CheckDotHit(Point pos)
        {
            double threshold = 40;
            for (int i = 0; i < 9; i++)
            {
                if (dotVisited[i]) continue;

                double dx = pos.X - dotCenters[i].X;
                double dy = pos.Y - dotCenters[i].Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                if (dist < threshold)
                {
                    dotVisited[i] = true;
                    // Activate dot + ring
                    dots[i].Fill = dotActive;
                    dots[i].Width = 20;
                    dots[i].Height = 20;
                    Canvas.SetLeft(dots[i], dotCenters[i].X - 10);
                    Canvas.SetTop(dots[i], dotCenters[i].Y - 10);
                    rings[i].Stroke = ringActive;
                    rings[i].Fill = new SolidColorBrush(
                        Color.FromArgb(0x11, 0x40, 0x9E, 0xFF));

                    // Draw line from previous dot
                    if (currentPattern.Count > 0)
                    {
                        int prevDot = currentPattern[currentPattern.Count - 1];
                        DrawLine(dotCenters[prevDot], dotCenters[i]);
                    }

                    currentPattern.Add(i);
                    break;
                }
            }
        }

        private void DrawLine(Point from, Point to)
        {
            var line = new Line
            {
                X1 = from.X,
                Y1 = from.Y,
                X2 = to.X,
                Y2 = to.Y,
                Stroke = lineBrush,
                StrokeThickness = 3,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Tag = "pattern_line"
            };
            PatternCanvas.Children.Add(line);
        }

        private void ClearLines()
        {
            var toRemove = new List<UIElement>();
            foreach (UIElement child in PatternCanvas.Children)
            {
                if (child is Line)
                    toRemove.Add(child);
            }
            foreach (var item in toRemove)
                PatternCanvas.Children.Remove(item);
        }

        private void ResetDots()
        {
            for (int i = 0; i < 9; i++)
            {
                dotVisited[i] = false;
                dots[i].Fill = dotNormal;
                dots[i].Width = 16;
                dots[i].Height = 16;
                Canvas.SetLeft(dots[i], dotCenters[i].X - 8);
                Canvas.SetTop(dots[i], dotCenters[i].Y - 8);
                dots[i].Stroke = new SolidColorBrush(Colors.Transparent);
                rings[i].Stroke = new SolidColorBrush(Colors.Transparent);
                rings[i].Fill = new SolidColorBrush(Colors.Transparent);
            }
        }

        private void HighlightDotsSuccess()
        {
            foreach (int idx in currentPattern)
            {
                dots[idx].Fill = dotSuccess;
                rings[idx].Stroke = ringSuccess;
                rings[idx].Fill = new SolidColorBrush(
                    Color.FromArgb(0x11, 0x4C, 0xAF, 0x50));
            }
            // Update lines to green
            foreach (UIElement child in PatternCanvas.Children)
            {
                var line = child as Line;
                if (line != null)
                    line.Stroke = new SolidColorBrush(
                        Color.FromArgb(0x88, 0x4C, 0xAF, 0x50));
            }
        }

        private void HighlightDotsError()
        {
            foreach (int idx in currentPattern)
            {
                dots[idx].Fill = dotError;
                rings[idx].Stroke = ringError;
                rings[idx].Fill = new SolidColorBrush(
                    Color.FromArgb(0x11, 0xFF, 0x4B, 0x4B));
            }
            // Update lines to red
            foreach (UIElement child in PatternCanvas.Children)
            {
                var line = child as Line;
                if (line != null)
                    line.Stroke = new SolidColorBrush(
                        Color.FromArgb(0x88, 0xFF, 0x4B, 0x4B));
            }

            // Reset after delay
            var resetTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(800)
            };
            resetTimer.Tick += (s, a) =>
            {
                resetTimer.Stop();
                ResetDots();
                ClearLines();
            };
            resetTimer.Start();
        }
    }
}
