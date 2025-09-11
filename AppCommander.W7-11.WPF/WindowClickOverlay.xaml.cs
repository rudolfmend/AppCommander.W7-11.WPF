using AppCommander.W7_11.WPF.Core;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace AppCommander.W7_11.WPF
{
    public partial class WindowClickOverlay : Window
    {
        #region Win32 API

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        #endregion

        #region Private Fields

        private DispatcherTimer _cursorUpdateTimer;
        private WindowTrackingInfo _currentHoverWindow;

        #endregion

        #region Constructor

        public WindowClickOverlay()
        {
            InitializeComponent();
            Initialize();
        }

        #endregion

        #region Private Methods

        private void Initialize()
        {
            // Nastav window properties
            this.Loaded += WindowClickOverlay_Loaded;
            this.KeyDown += WindowClickOverlay_KeyDown;

            // Timer pre aktualizáciu crosshair
            _cursorUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
            };
            _cursorUpdateTimer.Tick += UpdateCrosshair;
            _cursorUpdateTimer.Start();

            // Skry highlight rectangle na začiatku
            highlightRectangle.Visibility = Visibility.Collapsed;
        }

        private void WindowClickOverlay_Loaded(object sender, RoutedEventArgs e)
        {
            // Uisti sa, že je overlay na vrchole
            this.Topmost = true;
            this.Focus();
        }

        private void WindowClickOverlay_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.Close();
            }
        }

        private void UpdateCrosshair(object sender, EventArgs e)
        {
            try
            {
                if (GetCursorPos(out POINT cursorPos))
                {
                    // Konvertuj screen coordinates na window coordinates
                    var point = PointFromScreen(new Point(cursorPos.X, cursorPos.Y));

                    // Aktualizuj crosshair pozíciu
                    UpdateCrosshairPosition(point);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating crosshair: {ex.Message}");
            }
        }

        private void UpdateCrosshairPosition(Point position)
        {
            try
            {
                // Nastav crosshair lines
                horizontalLine.X1 = position.X - 50;
                horizontalLine.X2 = position.X + 50;
                horizontalLine.Y1 = position.Y;
                horizontalLine.Y2 = position.Y;

                verticalLine.X1 = position.X;
                verticalLine.X2 = position.X;
                verticalLine.Y1 = position.Y - 50;
                verticalLine.Y2 = position.Y + 50;

                // Nastav center dot
                Canvas.SetLeft(centerDot, position.X - 4);
                Canvas.SetTop(centerDot, position.Y - 4);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating crosshair position: {ex.Message}");
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Aktualizuje highlight pre aktuálne okno pod kurzorom
        /// </summary>
        public void UpdateHighlight(WindowTrackingInfo windowInfo)
        {
            try
            {
                _currentHoverWindow = windowInfo;

                if (windowInfo != null)
                {
                    // Aktualizuj info panel
                    txtCurrentProcess.Text = windowInfo.ProcessName;
                    txtCurrentTitle.Text = windowInfo.Title;
                    txtCurrentClass.Text = windowInfo.ClassName;

                    // Aktualizuj highlight rectangle
                    UpdateHighlightRectangle(windowInfo.WindowHandle);
                }
                else
                {
                    // Clear info
                    txtCurrentProcess.Text = "None";
                    txtCurrentTitle.Text = "-";
                    txtCurrentClass.Text = "-";

                    // Skry highlight
                    highlightRectangle.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating highlight: {ex.Message}");
            }
        }

        private void UpdateHighlightRectangle(IntPtr windowHandle)
        {
            try
            {
                if (GetWindowRect(windowHandle, out RECT windowRect))
                {
                    // Konvertuj screen coordinates na naše window coordinates
                    var topLeft = PointFromScreen(new Point(windowRect.Left, windowRect.Top));
                    var bottomRight = PointFromScreen(new Point(windowRect.Right, windowRect.Bottom));

                    // Nastav rectangle pozíciu a veľkosť
                    Canvas.SetLeft(highlightRectangle, topLeft.X);
                    Canvas.SetTop(highlightRectangle, topLeft.Y);
                    highlightRectangle.Width = bottomRight.X - topLeft.X;
                    highlightRectangle.Height = bottomRight.Y - topLeft.Y;

                    // Zobraz highlight
                    highlightRectangle.Visibility = Visibility.Visible;

                    // Animácia pre lepší vizuálny efekt
                    AnimateHighlight();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating highlight rectangle: {ex.Message}");
                highlightRectangle.Visibility = Visibility.Collapsed;
            }
        }

        private void AnimateHighlight()
        {
            try
            {
                // Jednoduchá pulsing animácia
                var animation = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = 0.3,
                    To = 0.8,
                    Duration = TimeSpan.FromMilliseconds(500),
                    AutoReverse = true,
                    RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
                };

                highlightRectangle.BeginAnimation(OpacityProperty, animation);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error animating highlight: {ex.Message}");
            }
        }

        #endregion

        #region Cleanup

        protected override void OnClosed(EventArgs e)
        {
            _cursorUpdateTimer?.Stop();
            _cursorUpdateTimer = null;
            base.OnClosed(e);
        }

        #endregion
    }
}
