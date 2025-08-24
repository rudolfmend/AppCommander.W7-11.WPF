using AppCommander.W7_11.WPF.Core;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CommandType = AppCommander.W7_11.WPF.Core.CommandType;
using WindowState = AppCommander.W7_11.WPF.Core.WindowState;

namespace AppCommander.W7_11.WPF
{
    public partial class MainWindow : Window
    {
        #region Private Fields

        // Core components
        private WindowTracker _windowTracker;
        private CommandRecorder _recorder;
        private CommandPlayer _player;
        private ActionSimulator _actionSimulator;

        // Collections
        private ObservableCollection<Command> _commands;
        private ObservableCollection<ElementUsageStats> _elementStatsList;

        // State
        private IntPtr _targetWindowHandle = IntPtr.Zero;
        private string _currentFilePath = string.Empty;
        private bool _hasUnsavedChanges = false;

        // Auto-detection system
        private AutomaticUIManager _automaticUIManager;
        private DispatcherTimer _windowScanTimer;
        private Dictionary<IntPtr, WindowTrackingData> _activeWindows;
        private bool _isAutoDetectionEnabled = true;
        private bool _isRecordingUIElements = false;

        // WinUI3 support
        private WinUI3ApplicationAnalysis _currentWinUI3Analysis;
        private bool _isWinUI3Application = false;

        #endregion

        #region Constructor and Initialization

        public MainWindow()
        {
            InitializeComponent();
            InitializeApplicationComponents();
            System.Diagnostics.Debug.WriteLine("AppCommander initialized with Automatic Window Detection");
        }

        private void InitializeApplicationComponents()
        {
            InitializeCoreComponents();
            InitializeCollections();
            InitializeAutoDetectionSystem();
            InitializeWindowTracker();
            SubscribeToEvents();
            UpdateUI();
        }

        private void InitializeCoreComponents()
        {
            _recorder = new CommandRecorder
            {
                EnableWinUI3Analysis = true,
                EnableDetailedLogging = true,
                AutoDetectNewWindows = true,
                AutoSwitchToNewWindows = true
            };

            _player = new CommandPlayer
            {
                PreferElementIdentifiers = true,
                EnableAdaptiveFinding = true,
                MaxElementSearchAttempts = 3
            };

            _actionSimulator = new ActionSimulator();
        }

        private void InitializeCollections()
        {
            _commands = new ObservableCollection<Command>();
            _elementStatsList = new ObservableCollection<ElementUsageStats>();

            dgCommands.ItemsSource = _commands;
            lstElementStats.ItemsSource = _elementStatsList;
        }

        private void InitializeAutoDetectionSystem()
        {
            _automaticUIManager = new AutomaticUIManager();
            _activeWindows = new Dictionary<IntPtr, WindowTrackingData>();

            _windowScanTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _windowScanTimer.Tick += OnWindowScanTimerTick;

            SetupAutoDetectionEvents();
            StartAutoDetectionIfEnabled();
        }

        private void InitializeWindowTracker()
        {
            _windowTracker = new WindowTracker
            {
                MonitoringIntervalMs = 500,
                TrackOnlyTargetProcess = true
            };

            _windowTracker.NewWindowDetected += OnNewWindowDetected;
            _windowTracker.WindowClosed += OnWindowClosed;
            _windowTracker.WindowActivated += OnWindowActivated;
        }

        private void SetupAutoDetectionEvents()
        {
            _automaticUIManager.UIChangeDetected += OnAutomaticUIChangeDetected;
            _automaticUIManager.NewWindowAppeared += OnAutomaticNewWindowDetected;
            _automaticUIManager.WindowClosed += OnAutomaticWindowClosed;
            _recorder.WindowAutoDetected += OnWindowAutoDetected;
        }

        private void StartAutoDetectionIfEnabled()
        {
            if (_isAutoDetectionEnabled)
            {
                _automaticUIManager.StartMonitoring();
                _windowScanTimer.Start();
                System.Diagnostics.Debug.WriteLine("🔍 Automatic window detection started");
                UpdateStatusMessage("Auto-detection: ON");
            }
        }

        #endregion

        #region Recording Operations

        private void StartRecording_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateTargetWindow()) return;

                string sequenceName = GetSequenceName();

                if (HandleExistingRecording()) return;

                StartRecordingWithAutoDetection(sequenceName);
                System.Diagnostics.Debug.WriteLine("✅ Recording with auto-detection started");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Failed to start recording", ex);
            }
        }

        private bool ValidateTargetWindow()
        {
            if (_targetWindowHandle == IntPtr.Zero)
            {
                MessageBox.Show("Please select a target window first.", "No Target Window",
                               MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
            return true;
        }

        private string GetSequenceName()
        {
            string sequenceName = txtSequenceName?.Text; 
            return string.IsNullOrWhiteSpace(sequenceName)
                ? $"Recording_{DateTime.Now:yyyyMMdd_HHmmss}"
                : sequenceName.Trim();
        }

        private bool HandleExistingRecording()
        {
            if (_recorder.IsRecording)
            {
                var result = MessageBox.Show(
                    "Recording is already in progress. Stop current recording and start new one?",
                    "Recording in Progress",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                    return true;

                _recorder.StopRecording();
            }
            return false;
        }

        private void StartRecordingWithAutoDetection(string sequenceName)
        {
            try
            {
                HandleExistingCommands();
                StartAutomaticUIMonitoring();
                ConfigureAndStartRecorder(sequenceName);
                UpdateRecordingUI(true);
                LogRecordingInfo();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error in StartRecordingWithAutoDetection: {ex.Message}");
                throw;
            }
        }

        private void HandleExistingCommands()
        {
            if (_commands.Count > 0)
            {
                var result = MessageBox.Show(
                    "Clear existing commands and start fresh recording?",
                    "Clear Commands",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _commands.Clear();
                    _elementStatsList.Clear();
                }
            }
        }

        private void StartAutomaticUIMonitoring()
        {
            if (_automaticUIManager != null)
            {
                _automaticUIManager.StartMonitoring(_targetWindowHandle, GetProcessNameFromWindow(_targetWindowHandle));
                _isRecordingUIElements = true;
            }
        }

        private void ConfigureAndStartRecorder(string sequenceName)
        {
            _recorder.AutoDetectNewWindows = true;
            _recorder.AutoSwitchToNewWindows = true;
            _recorder.LogWindowChanges = true;
            _recorder.StartRecording(sequenceName, _targetWindowHandle);
        }

        private void StopRecording_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _recorder?.StopRecording();
                _isRecordingUIElements = false;
                UpdateRecordingUI(false);
                UpdateStatusMessage("Recording stopped");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error stopping recording", ex);
            }
        }

        #endregion

        #region Playback Operations

        private void PlaySequence_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_commands.Count == 0)
                {
                    MessageBox.Show("No commands to play.", "Empty Sequence",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var sequence = new CommandSequence { Commands = _commands.ToList() };
                int repeatCount = GetRepeatCount();

                _player?.PlaySequence(sequence, repeatCount);
                UpdateUI();
                UpdateStatusMessage("Playback started");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error starting playback", ex);
            }
        }

        private int GetRepeatCount()
        {
            if (chkInfiniteLoop.IsChecked == true)
                return -1; // Infinite

            return int.TryParse(txtRepeatCount.Text, out int count) && count > 0 ? count : 1;
        }

        private void PausePlayback_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_player?.IsPaused == true)
                {
                    _player.Resume();
                    btnPause.Content = "⏸ Pause";
                    UpdateStatusMessage("Playback resumed");
                }
                else
                {
                    _player?.Pause();
                    btnPause.Content = "▶ Resume";
                    UpdateStatusMessage("Playback paused");
                }
                UpdateUI();
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error pausing/resuming playback", ex);
            }
        }

        private void StopPlayback_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _player?.Stop();
                UpdateUI();
                UpdateStatusMessage("Playback stopped");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error stopping playback", ex);
            }
        }

        #endregion

        #region Auto-Detection System

        private void ToggleAutomaticMode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _isAutoDetectionEnabled = !_isAutoDetectionEnabled;

                if (_isAutoDetectionEnabled)
                    EnableAutomaticMode();
                else
                    DisableAutomaticMode();

                UpdateRecordingUI(_recorder?.IsRecording ?? false);
                UpdateToggleButton(sender as Button);
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error toggling automatic mode", ex);
            }
        }

        private void EnableAutomaticMode()
        {
            if (_automaticUIManager != null && !_automaticUIManager.IsMonitoringActive)
            {
                var targetProcess = GetProcessNameFromWindow(_targetWindowHandle);
                _automaticUIManager.StartMonitoring(_targetWindowHandle, targetProcess);
            }

            if (!_windowScanTimer.IsEnabled)
                _windowScanTimer.Start();

            LogToUI("🟢 Automatic mode: ENABLED");
            System.Diagnostics.Debug.WriteLine("🟢 Automatic mode enabled");
        }

        private void DisableAutomaticMode()
        {
            _automaticUIManager?.StopMonitoring();
            _windowScanTimer.Stop();

            LogToUI("🔴 Automatic mode: DISABLED");
            System.Diagnostics.Debug.WriteLine("🔴 Automatic mode disabled");
        }

        private void UpdateToggleButton(Button button)
        {
            if (button != null)
            {
                button.Content = _isAutoDetectionEnabled ? "Disable Auto Mode" : "Enable Auto Mode";
            }
        }

        private void AutoRefreshAllUIElements_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("🔄 Starting auto-refresh of all UI elements");

                if (_automaticUIManager?.IsMonitoringActive == true)
                {
                    _automaticUIManager.ForceUIRefresh();
                    LogToUI("Auto-refreshed all tracked windows");
                }
                else if (_targetWindowHandle != IntPtr.Zero)
                {
                    RefreshTargetWindowElements();
                }
                else
                {
                    MessageBox.Show("No target window selected for refresh.", "No Target",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                }

                System.Diagnostics.Debug.WriteLine("✅ Auto-refresh completed");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error during auto-refresh", ex);
            }
        }

        #endregion

        #region Event Handlers

        private void OnWindowScanTimerTick(object sender, EventArgs e)
        {
            if (!_isAutoDetectionEnabled || _recorder?.IsRecording != true)
                return;

            try
            {
                ScanForNewWindows();

                if (_isRecordingUIElements)
                    RefreshUIElementsForActiveWindows();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Window scan error: {ex.Message}");
            }
        }

        private void OnAutomaticUIChangeDetected(object sender, UIChangeDetectedEventArgs e)
        {
            try
            {
                Dispatcher.InvokeAsync(() =>
                {
                    System.Diagnostics.Debug.WriteLine($"🔄 UI changes detected in: {e.WindowState.Title}");

                    if (e.WindowHandle == _targetWindowHandle)
                        UpdateElementStatisticsFromChanges(e.Changes);

                    LogUIChanges(e);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error handling automatic UI change: {ex.Message}");
            }
        }

        private void OnAutomaticNewWindowDetected(object sender, NewWindowAppearedEventArgs e)
        {
            try
            {
                Dispatcher.InvokeAsync(() =>
                {
                    System.Diagnostics.Debug.WriteLine($"🪟 New window auto-detected: {e.WindowTitle} ({e.WindowType})");

                    if (_recorder?.IsRecording == true && ShouldAutoSwitchToNewWindow(e))
                        HandleAutomaticWindowSwitch(e);
                    else
                        LogToUI($"Detected: {e.WindowType} - {e.WindowTitle}");
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error handling automatic new window: {ex.Message}");
            }
        }

        private void OnAutomaticWindowClosed(object sender, WindowClosedEventArgs e)
        {
            try
            {
                Dispatcher.InvokeAsync(() =>
                {
                    System.Diagnostics.Debug.WriteLine($"🗑️ Tracked window closed: {e.WindowTrackingInfo}");
                    HandleWindowClosure(e);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error handling automatic window closed: {ex.Message}");
            }
        }

        private void OnNewWindowDetected(object sender, NewWindowDetectedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                System.Diagnostics.Debug.WriteLine($"Nové okno detekované: {e.WindowInfo.Title}");
                // Handle new window detection
            });
        }

        private void OnWindowClosed(object sender, WindowClosedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                System.Diagnostics.Debug.WriteLine($"Okno zatvorené: {e.WindowInfo.Title}");
                // Handle window closure
            });
        }

        private void OnWindowActivated(object sender, WindowActivatedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                var windowTitle = e.WindowInfo?.Title ?? e.WindowTitle ?? "Unknown Window";
                System.Diagnostics.Debug.WriteLine($"Okno aktivované: {windowTitle}");
                // Handle window activation
            });
        }

        private void OnWindowAutoDetected(object sender, WindowAutoDetectedEventArgs e)
        {
            Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"🔍 Window auto-detected: {e.Description}");
                    ProcessAutoDetectedWindow(e);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Error handling window auto-detection: {ex.Message}");
                }
            });
        }

        #endregion

        #region UI Updates

        private void UpdateRecordingUI(bool isRecording)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateStatusDisplay(isRecording);
                    UpdateAutoDetectionIndicators();
                    UpdateProgressBar(isRecording);
                    UpdateUI();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error updating recording UI: {ex.Message}");
            }
        }

        private void UpdateStatusDisplay(bool isRecording)
        {
            if (txtStatus != null)
            {
                txtStatus.Text = isRecording
                    ? "Recording (Auto-detect ON)"
                    : "Ready";
            }
        }

        private void UpdateAutoDetectionIndicators()
        {
            if (lblAutoDetectionStatus != null)
            {
                lblAutoDetectionStatus.Content = _isAutoDetectionEnabled
                    ? "🟢 Auto-Detection Active"
                    : "🔴 Auto-Detection Inactive";
            }

            if (lblUIRecordingStatus != null)
            {
                lblUIRecordingStatus.Content = _isRecordingUIElements
                    ? "🟢 UI Scanning Active"
                    : "🔴 UI Scanning Inactive";
            }
        }

        private void UpdateProgressBar(bool isRecording)
        {
            if (progressEnhancedRecording != null)
            {
                progressEnhancedRecording.IsIndeterminate = isRecording;
                progressEnhancedRecording.Visibility = isRecording ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void UpdateStatusMessage(string message)
        {
            try
            {
                if (txtStatus != null)
                    txtStatus.Text = message;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error updating status: {ex.Message}");
            }
        }

        private void LogToUI(string message)
        {
            try
            {
                Dispatcher.InvokeAsync(() => {
                    UpdateStatusMessage(message);
                    System.Diagnostics.Debug.WriteLine($"📝 UI Log: {message}");
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error logging to UI: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

        private void ShowErrorMessage(string title, Exception ex)
        {
            MessageBox.Show($"{title}: {ex.Message}", "Error",
                           MessageBoxButton.OK, MessageBoxImage.Error);
            System.Diagnostics.Debug.WriteLine($"❌ {title}: {ex.Message}");
        }

        private void LogRecordingInfo()
        {
            try
            {
                var targetInfo = GetWindowInfo(_targetWindowHandle);
                var processName = GetProcessNameFromWindow(_targetWindowHandle);

                LogToUI("=== RECORDING STARTED ===");
                LogToUI($"Target: {targetInfo}");
                LogToUI($"Process: {processName}");
                LogToUI($"Auto-Detection: {(_isAutoDetectionEnabled ? "Enabled" : "Disabled")}");
                LogToUI($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error logging recording info: {ex.Message}");
            }
        }

        private string GetWindowInfo(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero)
                return "No window selected";

            try
            {
                var title = GetWindowTitle(windowHandle);
                var processName = GetProcessNameFromWindow(windowHandle);
                return $"{processName} - {title}";
            }
            catch
            {
                return "Unknown window";
            }
        }

        private string GetWindowTitle(IntPtr windowHandle)
        {
            try
            {
                var sb = new System.Text.StringBuilder(256);
                GetWindowText(windowHandle, sb, sb.Capacity);
                return sb.ToString();
            }
            catch
            {
                return "Unknown Window";
            }
        }

        private string GetProcessNameFromWindow(IntPtr windowHandle)
        {
            try
            {
                GetWindowThreadProcessId(windowHandle, out uint processId);
                using (var process = Process.GetProcessById((int)processId))
                {
                    return process.ProcessName;
                }
            }
            catch
            {
                return "Unknown Process";
            }
        }

        #endregion

        #region Placeholder Methods (To be implemented based on your specific needs)

        private void SubscribeToEvents() { /* Implementation needed */ }
        private void UpdateUI() { /* Implementation needed */ }
        private void ScanForNewWindows() { /* Implementation needed */ }
        private void RefreshUIElementsForActiveWindows() { /* Implementation needed */ }
        private void RefreshTargetWindowElements() { /* Implementation needed */ }
        private void UpdateElementStatisticsFromChanges(object changes) { /* Implementation needed */ }
        private void LogUIChanges(UIChangeDetectedEventArgs e) { /* Implementation needed */ }
        private bool ShouldAutoSwitchToNewWindow(NewWindowAppearedEventArgs e) { return false; /* Implementation needed */ }
        private void HandleAutomaticWindowSwitch(NewWindowAppearedEventArgs e) { /* Implementation needed */ }
        private void HandleWindowClosure(WindowClosedEventArgs e) { /* Implementation needed */ }
        private void ProcessAutoDetectedWindow(WindowAutoDetectedEventArgs e) { /* Implementation needed */ }

        #endregion

        #region Win32 API

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        #endregion

        #region Cleanup

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                _windowTracker?.StopTracking();
                _windowTracker?.Dispose();
                _automaticUIManager?.StopMonitoring();
                _windowScanTimer?.Stop();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during cleanup: {ex.Message}");
            }

            base.OnClosed(e);
        }

        #endregion
    }

    #region Supporting Data Classes

    public class WindowTrackingData
    {
        public IntPtr WindowHandle { get; set; }
        public string Title { get; set; } = "";
        public string ProcessName { get; set; } = "";
        public WindowType WindowType { get; set; }
        public bool IsModal { get; set; }
        public DateTime DetectedAt { get; set; }
        public List<UIElementInfo> UIElements { get; set; } = new List<UIElementInfo>();
        public bool IsActive { get; set; } = true;
    }

    #endregion
}