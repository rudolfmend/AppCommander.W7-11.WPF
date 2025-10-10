using AppCommander.W7_11.WPF.Core;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.VisualBasic;

public class SequenceSetItem : INotifyPropertyChanged
{
    private ObservableCollection<UnifiedItem> _unifiedItems;
    private UnifiedSequence _currentUnifiedSequence;
    private string _currentUnifiedSequenceFilePath;
    private bool _hasUnsavedUnifiedChanges;

    private int _stepNumber;
    private string _sequenceName;
    private int _repeatCount;
    private string _status;
    private DateTime _timestamp;
    private string _filePath;

    public int StepNumber
    {
        get { return _stepNumber; }
        set { _stepNumber = value; OnPropertyChanged("StepNumber"); }
    }

    public string SequenceName
    {
        get { return _sequenceName; }
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Sequence name cannot be empty");
            _sequenceName = value;
            OnPropertyChanged("SequenceName");
        }
    }

    public int RepeatCount
    {
        get { return _repeatCount; }
        set
        {
            if (value < 1)
                throw new ArgumentException("Repeat count must be at least 1");
            _repeatCount = value;
            OnPropertyChanged("RepeatCount");
        }
    }

    public string Status
    {
        get { return _status; }
        set { _status = value; OnPropertyChanged("Status"); }
    }

    public DateTime Timestamp
    {
        get { return _timestamp; }
        set { _timestamp = value; OnPropertyChanged("Timestamp"); }
    }

    public string FilePath
    {
        get { return _filePath; }
        set { _filePath = value; OnPropertyChanged("FilePath"); }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public SequenceSetItem()
    {
        _stepNumber = 1;
        _sequenceName = "";
        _repeatCount = 1;
        _status = "Ready";
        _timestamp = DateTime.Now;
        _filePath = "";
    }
}

// Trieda pre SequenceSet
public class SequenceSet
{
    public string Name { get; set; }
    public string Description { get; set; }
    public List<SequenceSetItem> Sequences { get; set; }
    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
    public string FilePath { get; set; }

    public SequenceSet()
    {
        Name = "";
        Description = "";
        Sequences = new List<SequenceSetItem>();
        Created = DateTime.Now;
        LastModified = DateTime.Now;
        FilePath = "";
    }

    public SequenceSet(string name) : this()
    {
        Name = name;
    }
}

namespace AppCommander.W7_11.WPF
{
    public partial class MainWindow : Window
    {
        #region Private Fields

        // Core components
        private WindowTracker _windowTracker;
        private CommandRecorder _recorder;
        private CommandPlayer _player;
        private AutomaticUIManager _automaticUIManager;

        // Sequence of commands
        private string _currentSequenceName = string.Empty;

        // Collections
        private ObservableCollection<Command> _commands;

        // State
        private IntPtr _targetWindowHandle = IntPtr.Zero;
        private string _currentFilePath = string.Empty;
        private bool _hasUnsavedChanges = false;
        private bool _isAutoTrackingEnabled = true;

        private ObservableCollection<SequenceSetItem> _sequenceSetItems;
        private SequenceSet _currentSequenceSet;
        private string _currentSequenceSetFilePath;
        private bool _hasUnsavedSequenceSetChanges;


        [DllImport("UxTheme.dll", SetLastError = true, EntryPoint = "#138")]
        private static extern bool ShouldSystemUseDarkMode();

        [DllImport("UxTheme.dll", SetLastError = true, EntryPoint = "#137")]
        private static extern bool ShouldAppsUseDarkMode();

        private void InitializeSequenceSet()
        {
            _sequenceSetItems = new ObservableCollection<SequenceSetItem>();
            _currentSequenceSet = new SequenceSet();
            _currentSequenceSetFilePath = string.Empty;
            _hasUnsavedSequenceSetChanges = false;
        }

        #endregion

        #region Constructor

        public MainWindow()
        {
            InitializeComponent();
            InitializeApplication();
        }

        private void InitializeUnifiedTable()
        {
            _unifiedItems = new ObservableCollection<UnifiedItem>();
            _currentUnifiedSequence = new UnifiedSequence();
            _currentUnifiedSequenceFilePath = string.Empty;
            _hasUnsavedUnifiedChanges = false;

            // Nastavte DataContext pre AppCommander_MainCommandTable
            if (AppCommander_MainCommandTable != null)
            {
                AppCommander_MainCommandTable.ItemsSource = _unifiedItems;
            }

            Debug.WriteLine("Unified table initialized");
        }

        private void InitializeApplication()
        {
            try
            {
                _windowTracker = new WindowTracker();
                _recorder = new CommandRecorder();
                _player = new CommandPlayer();
                _automaticUIManager = new AutomaticUIManager();

                // Initialize collections
                _commands = new ObservableCollection<Command>();

                InitializeUnifiedTable();

                InitializeWindowClickSelector();

                SubscribeToEvents();
                UpdateUI();
                UpdateUnsavedCommandsWarning();
                UpdateStatus("Application initialized - Ready to start");

                Debug.WriteLine("MainWindow initialized successfully");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error initializing application", ex);
            }
        }

        #endregion

        #region Event Subscriptions

        private void SubscribeToEvents()
        {
            try
            {
                // Recorder events
                _recorder.CommandRecorded += OnCommandRecorded;
                _recorder.RecordingStateChanged += OnRecordingStateChanged;

                // Player events
                _player.CommandExecuted += OnCommandExecuted;
                _player.PlaybackStateChanged += OnPlaybackStateChanged;
                _player.PlaybackCompleted += OnPlaybackCompleted;
                _player.PlaybackError += OnPlaybackError;

                // Window tracker events
                _windowTracker.NewWindowDetected += OnNewWindowDetected;
                _windowTracker.WindowActivated += OnWindowActivated;
                _windowTracker.WindowClosed += OnWindowClosed;

                // Automatic UI manager events
                _automaticUIManager.UIChangeDetected += OnUIChangeDetected;
                _automaticUIManager.NewWindowAppeared += OnNewWindowAppeared;

                // Window events
                this.Closing += MainWindow_Closing;

                Debug.WriteLine("Event subscriptions completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error subscribing to events: {0}", ex.Message));
            }
        }

        #endregion

        #region Core Event Handlers

        private void OnCommandRecorded(object sender, CommandRecordedEventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    _commands.Add(e.Command);
                    _hasUnsavedChanges = true;
                    UpdateUI();

                    string commandDescription = GetCommandDescription(e.Command);
                    UpdateStatus(string.Format("Command recorded: {0}", commandDescription));
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error handling command recorded: {0}", ex.Message));
            }
        }

        private string GetCommandDescription(Command command)
        {
            switch (command.Type)
            {
                case CommandType.LoopStart:
                    return string.Format("Loop Start (repeat {0}x)", command.RepeatCount);
                case CommandType.LoopEnd:
                    return "Loop End";
                case CommandType.Wait:
                    return string.Format("Wait {0}ms", command.Value);
                case CommandType.KeyPress:
                    return string.Format("Key Press: {0}", command.Key);
                case CommandType.Click:
                    return string.Format("Click on {0}", command.ElementName);
                case CommandType.SetText:
                    return string.Format("Set Text: '{0}' in {1}", command.Value, command.ElementName);
                default:
                    return string.Format("{0} - {1}", command.Type, command.ElementName);
            }
        }

        private void OnRecordingStateChanged(object sender, RecordingStateChangedEventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateRecordingStatus(e.IsRecording, e.IsPaused);
                    UpdateUI();

                    if (e.IsRecording)
                    {
                        var message = e.IsPaused ?
                            string.Format("Recording paused: {0}", e.SequenceName) :
                            string.Format("Recording started: {0}", e.SequenceName);
                        UpdateStatus(message);
                    }
                    else
                    {
                        UpdateStatus(string.Format("Recording stopped: {0}", e.SequenceName));

                        // **PRIDAJTE TENTO RIADOK:**
                        UpdateUnsavedCommandsWarning();
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error handling recording state changed: {0}", ex.Message));
            }
        }

        private void OnCommandExecuted(object sender, CommandPlayer.CommandExecutedEventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    var statusMsg = string.Format("Executing {0}/{1}: {2}",
                        e.CommandIndex + 1, e.TotalCommands, GetCommandDescription(e.Command));
                    UpdateStatus(statusMsg);

                    if (!e.Success)
                    {
                        Debug.WriteLine(string.Format("Command failed: {0}", e.ErrorMessage));
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error handling command executed: {0}", ex.Message));
            }
        }

        private void OnPlaybackStateChanged(object sender, CommandPlayer.PlaybackStateChangedEventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateUI();
                    UpdateStatus(string.Format("Playback {0}: {1}", e.State, e.SequenceName));
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error handling playback state change: {0}", ex.Message));
            }
        }

        private void OnPlaybackCompleted(object sender, CommandPlayer.PlaybackCompletedEventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateUI();
                    UpdateStatus(string.Format("Playback completed: {0}", e.Message));

                    if (e.Success)
                    {
                        var message = string.Format("Playback completed successfully!\n\nCommands executed: {0}/{1}",
                            e.CommandsExecuted, e.TotalCommands);
                        //MessageBox.Show(message, "Playback Completed",
                        //    MessageBoxButton.OK, MessageBoxImage.Information);
                        Debug.WriteLine($"{message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error handling playback completed: {0}", ex.Message));
            }
        }

        private void OnPlaybackError(object sender, CommandPlayer.PlaybackErrorEventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateStatus(string.Format("Playback error: {0}", e.ErrorMessage));
                    var errorTitle = string.Format("Error during playback at command {0}", e.CommandIndex + 1);
                    ShowErrorMessage(errorTitle, new Exception(e.ErrorMessage));
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error handling playback error: {0}", ex.Message));
            }
        }

        #endregion

        #region UI Update Fixes

        /// <summary>
        /// Resetuje UI po click selection - OPRAVENÉ
        /// </summary>
        private void ResetClickSelectionUI()
        {
            try
            {
                AppCommander_AppCommander_BtnSelectTargetByClick.Content = "🎯 Click to Select";
                AppCommander_AppCommander_BtnSelectTargetByClick.IsEnabled = true;
                AppCommander_BtnSelectTarget.IsEnabled = true;
                AppCommander_BtnRecording.IsEnabled = _targetWindowHandle != IntPtr.Zero;

                // Skryje selection indicator
                AppCommander_SelectionModeIndicator.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error resetting click selection UI: {ex.Message}");
            }
        }

        /// <summary>
        /// Aktualizuje target window information - OPRAVENÉ
        /// </summary>
        private void UpdateTargetWindowInfo(WindowTrackingInfo windowInfo)
        {
            try
            {
                if (windowInfo != null)
                {
                    var processName = windowInfo.ProcessName ?? "Unknown Process";
                    var title = windowInfo.Title ?? "Unknown Title";
                    AppCommander_LblTargetWindow.Text = $"{processName} - {title}";

                    // Update UI state
                    AppCommander_BtnRecording.IsEnabled = true;

                    AppCommander_LblTargetWindow.Text = $"{windowInfo.ProcessName} - {windowInfo.Title}";
                    AppCommander_TxtTargetProcess.Text = windowInfo.ProcessName;
                    _targetWindowHandle = windowInfo.WindowHandle;
                    Debug.WriteLine($"Target window updated: Handle=0x{_targetWindowHandle.ToInt64():X8}, Process={windowInfo.ProcessName}, Title={windowInfo.Title}");

                    UpdateStatus($"Target window set to: {windowInfo.ProcessName}");
                    AppCommander_BtnRecording.IsEnabled = true;
                    UpdateUI();
                }
                else
                {
                    AppCommander_LblTargetWindow.Text = "No target selected";
                    AppCommander_TxtTargetProcess.Text = "-";
                    AppCommander_BtnRecording.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating target window info: {ex.Message}");
                AppCommander_TxtTargetProcess.Text = "-";
                AppCommander_LblTargetWindow.Text = "Error loading target info";
                AppCommander_BtnRecording.IsEnabled = false;
            }
            finally
            {
                UpdateUI();
            }
        }

        /// <summary>
        /// Aktualizuje status labels - OPRAVENÉ
        /// </summary>
        private void UpdateStatusLabels(bool isRecording)
        {
            try
            {
                if (isRecording)
                {
                    // OPRAVA: TextBlock používa .Text namiesto .Content
                    AppCommander_LblAutoDetectionStatus.Text = "🟢 Auto-Detection Active";
                    AppCommander_LblUIRecordingStatus.Text = "🟢 UI Scanning Active";
                }
                else
                {
                    AppCommander_LblAutoDetectionStatus.Text = "🔴 Auto-Detection Inactive";
                    AppCommander_LblUIRecordingStatus.Text = "🔴 UI Scanning Inactive";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating status labels: {ex.Message}");
            }
        }

        #endregion

        #region Recording Methods - OPRAVENÉ

        private void StartNewRecording()
        {
            try
            {
                // Zabráň nahrávaniu počas playbacku
                if (_player != null && _player.IsPlaying)
                {
                    MessageBox.Show(
                        "Cannot start recording while playback is running.\n" +
                        "Please stop playback first.",
                        "Recording Not Available",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // KONTROLA: Musí byť vybraný target window
                if (_targetWindowHandle == IntPtr.Zero)
                {
                    MessageBox.Show("Please select a target window first.",
                                   "No Target Selected",
                                   MessageBoxButton.OK,
                                   MessageBoxImage.Warning);
                    return;
                }

                // KONTROLA: Target nesmie byť AppCommander
                string targetProcess = GetProcessNameFromWindow(_targetWindowHandle);
                if (targetProcess.Equals("AppCommander", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(
                        "You cannot record actions on AppCommander itself.\n" +
                        "Please select a different target application.",
                        "Invalid Target",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    // Resetuj target
                    _targetWindowHandle = IntPtr.Zero;
                    UpdateUI();
                    return;
                }

                var sequenceName = AppCommander_TxtSequenceName.Text;
                if (string.IsNullOrWhiteSpace(sequenceName))
                {
                    sequenceName = string.Format("Recording_{0:yyyyMMdd_HHmmss}", DateTime.Now);
                    AppCommander_TxtSequenceName.Text = sequenceName;
                }

                _recorder.StartRecording(sequenceName, _targetWindowHandle);
                _recorder.EnableRealTimeElementScanning = true;
                _recorder.AutoUpdateExistingCommands = true;
                _recorder.EnablePredictiveDetection = true;

                _windowTracker.StartTracking(targetProcess);
                _automaticUIManager.StartMonitoring(_targetWindowHandle, targetProcess);

                AppCommander_BtnRecording.Content = "⏹ Stop Recording";
                AppCommander_BtnRecording.Style = (Style)FindResource("DangerButton");

                UpdateStatusLabels(true);

                AppCommander_ProgressEnhancedRecording.Visibility = Visibility.Visible;
                AppCommander_ProgressEnhancedRecording.IsIndeterminate = true;

                UpdateStatus($"Recording started: {sequenceName}");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error starting recording", ex);
            }
        }

        private void StopCurrentRecording()
        {
            try
            {
                _recorder.StopRecording();
                _windowTracker.StopTracking();
                _automaticUIManager.StopMonitoring();

                AppCommander_BtnRecording.Content = "🔴 Start Recording";
                AppCommander_BtnRecording.Style = (Style)FindResource("DangerButton");

                UpdateStatusLabels(false);

                AppCommander_ProgressEnhancedRecording.Visibility = Visibility.Collapsed;
                AppCommander_ProgressEnhancedRecording.IsIndeterminate = false;

                UpdateStatus("Recording stopped");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error stopping recording", ex);
            }
        }

        #endregion

        #region Playback Controls - PODMIENEČNÉ kontroly

        private void PausePlayback_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_player.IsPaused)
                {
                    _player.Resume();
                    // OPRAVA: Podmienečná kontrola či AppCommander_BtnPause existuje
                    if (AppCommander_BtnPause != null)
                        AppCommander_BtnPause.Content = "⏸ Pause";
                }
                else if (_player.IsPlaying)
                {
                    _player.Pause();
                    if (AppCommander_BtnPause != null)
                        AppCommander_BtnPause.Content = "▶ Resume";
                }
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
                _player.Stop();
                // OPRAVA: Podmienečná kontrola
                if (AppCommander_BtnPause != null)
                    AppCommander_BtnPause.Content = "⏸ Pause";
                UpdateStatus("Playback stopped");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error stopping playback", ex);
            }
        }

        #endregion

        #region UI Update Method - OPRAVENÉ

        private void UpdateUI()
        {
            try
            {
                bool isRecording = _recorder != null && _recorder.IsRecording;
                bool isPlaying = _player != null && _player.IsPlaying;
                bool hasTargetWindow = _targetWindowHandle != IntPtr.Zero;

                // Recording button state
                AppCommander_BtnRecording.IsEnabled = hasTargetWindow || isRecording;

                AppCommander_BtnPlayCommands.IsEnabled = _commands.Any() && !isRecording && !isPlaying;

                if (AppCommander_BtnPlayCommands != null)
                    AppCommander_BtnPlayCommands.IsEnabled = _commands.Any() && !isRecording && !isPlaying;

                if (AppCommander_BtnPause != null)
                    AppCommander_BtnPause.IsEnabled = isPlaying;

                if (AppCommander_BtnStop != null)
                    AppCommander_BtnStop.IsEnabled = isPlaying;

                // Target selection buttons - disable počas recordingu
                AppCommander_AppCommander_BtnSelectTargetByClick.IsEnabled = !isRecording;
                AppCommander_BtnSelectTarget.IsEnabled = !isRecording;

                // Commands count
                var loopCount = _commands.Count(c => c.Type == CommandType.LoopStart);
                string commandText = loopCount > 0 ?
                    string.Format("Commands: {0} ({1} loops)", _commands.Count, loopCount) :
                    string.Format("Commands: {0}", _commands.Count);
                AppCommander_TxtCommandCount.Text = commandText;

                // Window title
                string title = "AppCommander";
                if (!string.IsNullOrEmpty(_currentFilePath))
                {
                    title += string.Format(" - {0}", Path.GetFileName(_currentFilePath));
                }
                if (_hasUnsavedChanges)
                {
                    title += " *";
                }
                this.Title = title;

                // Target process info - OPRAVA: ak existuje handle
                if (hasTargetWindow)
                {
                    var processName = GetProcessNameFromWindow(_targetWindowHandle);
                    var windowTitle = GetWindowTitle(_targetWindowHandle);
                    AppCommander_LblTargetWindow.Text = $"{processName} - {windowTitle}";
                    AppCommander_TxtTargetProcess.Text = processName;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error updating UI: {0}", ex.Message));
            }
        }

        #endregion

        #region Element Statistics - PODMIENEČNÉ

        private void RefreshElementStatistics()
        {
            try
            {
                // OPRAVA: podmienečná kontrola či AppCommander_LstElementStats existuje
                if (AppCommander_LstElementStats == null) return;

                AppCommander_LstElementStats.Items.Clear();

                var elementGroups = _commands
                    .Where(c => !string.IsNullOrEmpty(c.ElementName))
                    .GroupBy(c => c.ElementName)
                    .Select(g => new
                    {
                        ElementName = g.Key,
                        UsageCount = g.Count(),
                        LastUsed = g.Max(c => c.Timestamp)
                    })
                    .OrderByDescending(e => e.UsageCount)
                    .ToList();

                foreach (var element in elementGroups)
                {
                    AppCommander_LstElementStats.Items.Add(element);
                }

                UpdateStatus(string.Format("Element statistics refreshed: {0} unique elements", elementGroups.Count));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error refreshing element statistics: {0}", ex.Message));
            }
        }

        #endregion

        #region Auto Mode Toggle - PODMIENEČNÉ

        private void ToggleAutomaticMode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _isAutoTrackingEnabled = !_isAutoTrackingEnabled;

                // OPRAVA: podmienečná kontrola
                if (AppCommander_BtnToggleAutoMode != null)
                {
                    AppCommander_BtnToggleAutoMode.Content = _isAutoTrackingEnabled ? "🎯 Auto Mode ON" : "🎯 Auto Mode OFF";
                }

                var message = _isAutoTrackingEnabled ?
                    "Automatic mode enabled - New windows will be tracked automatically" :
                    "Automatic mode disabled - Manual window selection required";
                UpdateStatus(message);
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error toggling automatic mode", ex);
            }
        }

        #endregion

        #region Selection UI Updates - OPRAVENÉ

        private async void SelectTargetByClick_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_windowClickSelector.IsSelecting)
                {
                    _windowClickSelector.CancelSelection();
                    return;
                }

                // Zobraz selection indicator
                AppCommander_SelectionModeIndicator.Visibility = Visibility.Visible;
                AppCommander_TxtSelectionMode.Text = "Click Selection Active";

                // Zmeni tlačidlo na cancel mode
                AppCommander_AppCommander_BtnSelectTargetByClick.Content = "❌ Cancel Selection";
                AppCommander_AppCommander_BtnSelectTargetByClick.IsEnabled = true;

                // Disable ostatné controls počas výberu
                AppCommander_BtnSelectTarget.IsEnabled = false;
                AppCommander_BtnRecording.IsEnabled = false;

                UpdateStatus("Click selection mode activated. Click on any window to select it as target.");

                // Spusti async selection
                var selectedWindow = await _windowClickSelector.StartWindowSelectionAsync();

                if (selectedWindow != null)
                {
                    // Nastav vybrané okno ako target
                    _targetWindowHandle = selectedWindow.WindowHandle;
                    UpdateTargetWindowInfo(selectedWindow);

                    UpdateUI();
                    UpdateStatus(string.Format("Target selected by click: {0}", selectedWindow.ProcessName));

                    Debug.WriteLine(string.Format("Target window selected by click: Handle=0x{0:X8}, Process={1}, Title={2}",
                        _targetWindowHandle.ToInt64(), selectedWindow.ProcessName, selectedWindow.Title));
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error during click selection", ex);
            }
            finally
            {
                // Vráti UI do normálneho stavu
                ResetClickSelectionUI();
            }
        }

        #endregion

        #region Window Click Selection

        private WindowClickSelector _windowClickSelector;
        private ObservableCollection<UnifiedItem> _unifiedItems;
        private UnifiedSequence _currentUnifiedSequence;
        private string _currentUnifiedSequenceFilePath;
        private bool _hasUnsavedUnifiedChanges;
        private object AppCommander_TxtSequenceName_Copy;

        /// <summary>
        /// Inicializuje window click selector
        /// </summary>
        private void InitializeWindowClickSelector()
        {
            _windowClickSelector = new WindowClickSelector();

            // Subscribe to events
            _windowClickSelector.WindowSelected += OnWindowClickSelected;
            _windowClickSelector.SelectionCancelled += OnWindowClickSelectionCancelled;
            _windowClickSelector.StatusChanged += OnWindowClickStatusChanged;
        }

        /// <summary>
        /// Event handler pre úspešný výber okna
        /// </summary>
        private void OnWindowClickSelected(object sender, WindowSelectedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var windowInfo = e.SelectedWindow;

                    // Nastav target window
                    _targetWindowHandle = windowInfo.WindowHandle;
                    AppCommander_LblTargetWindow.Text = string.Format("{0} - {1}",
                        windowInfo.ProcessName, windowInfo.Title);

                    UpdateUI();
                    UpdateStatus(string.Format("Target window selected: {0} - {1}",
                        windowInfo.ProcessName, windowInfo.Title));

                    // Log successful selection
                    Debug.WriteLine(string.Format("Window selected by click: Process={0}, Title={1}, Handle=0x{2:X8}",
                        windowInfo.ProcessName, windowInfo.Title, windowInfo.WindowHandle.ToInt64()));
                }
                catch (Exception ex)
                {
                    ShowErrorMessage("Error processing window selection", ex);
                }
            }));
        }

        /// <summary>
        /// Event handler pre zrušenie výberu
        /// </summary>
        private void OnWindowClickSelectionCancelled(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateStatus("Window selection cancelled by user");
                ResetClickSelectionUI();
            }));
        }

        /// <summary>
        /// Event handler pre status zmeny počas výberu
        /// </summary>
        private void OnWindowClickStatusChanged(object sender, string status)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateStatus(status);
            }));
        }


        #endregion

        #region Updated Constructor and Cleanup

        private void InitializeComponents()
        {
            InitializeWindowClickSelector();
        }

        // Aktualizujte Dispose/Cleanup metódu
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // cleanup pre window click selector
                _windowClickSelector?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during cleanup: {ex.Message}");
            }
            finally
            {
                base.OnClosed(e);
            }
        }

        #endregion

        #region Updated SelectTarget_Click Method

        // Aktualizovaná verzia existujúcej metódy SelectTarget_Click
        private void SelectTarget_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Ak je click selection aktívny, zruš ho
                if (_windowClickSelector?.IsSelecting == true)
                {
                    _windowClickSelector.CancelSelection();
                    UpdateStatus("Click selection cancelled. Opening window selector dialog...");
                    System.Threading.Thread.Sleep(100); // Krátke čakanie
                }

                // Pokračuj s existujúcou funkcionalitou
                var dialog = new WindowSelectorDialog();
                if (dialog.ShowDialog() == true && dialog.SelectedWindow != null)
                {
                    _targetWindowHandle = dialog.SelectedWindow.WindowHandle;
                    AppCommander_LblTargetWindow.Text = string.Format("{0} - {1}",
                        dialog.SelectedWindow.ProcessName, dialog.SelectedWindow.Title);

                    UpdateUI();
                    UpdateStatus(string.Format("Target selected from dialog: {0}", dialog.SelectedWindow.ProcessName));

                    Debug.WriteLine(string.Format("Target window selected from dialog: Handle=0x{0:X8}, Process={1}, Title={2}",
                        _targetWindowHandle.ToInt64(), dialog.SelectedWindow.ProcessName, dialog.SelectedWindow.Title));
                }
                else
                {
                    Debug.WriteLine("No window selected or dialog cancelled");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error selecting target window", ex);
            }
            finally
            {
                // Uisti sa, že click selection UI je resetovaný
                ResetClickSelectionUI();
            }
        }

        #endregion

        #region Window Event Handlers

        private void OnNewWindowDetected(object sender, NewWindowDetectedEventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    if (_isAutoTrackingEnabled && _recorder.IsRecording)
                    {
                        Debug.WriteLine(string.Format("Auto-detected new window during recording: {0}", e.WindowTitle));

                        if (IsRelevantWindow(e))
                        {
                            _automaticUIManager.AddWindowToTracking(e.WindowHandle, WindowTrackingPriority.High);
                            UpdateStatus(string.Format("Auto-tracking new window: {0}", e.WindowTitle));
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error handling new window detected: {0}", ex.Message));
            }
        }

        private void OnWindowActivated(object sender, WindowActivatedEventArgs e)
        {
            try
            {
                if (_recorder.IsRecording && _isAutoTrackingEnabled)
                {
                    Debug.WriteLine(string.Format("Window activated during recording: {0}", e.WindowTitle));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error handling window activated: {0}", ex.Message));
            }
        }

        private void OnWindowClosed(object sender, WindowClosedEventArgs e)
        {
            try
            {
                if (_recorder.IsRecording)
                {
                    Debug.WriteLine(string.Format("Window closed during recording: {0}", e.WindowTitle));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error handling window closed: {0}", ex.Message));
            }
        }

        private void OnUIChangeDetected(object sender, UIChangeDetectedEventArgs e)
        {
            try
            {
                if (_recorder.IsRecording)
                {
                    Debug.WriteLine(string.Format("UI changes detected: {0} added, {1} removed",
                        e.Changes.AddedElements.Count, e.Changes.RemovedElements.Count));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error handling UI change detected: {0}", ex.Message));
            }
        }

        private void OnNewWindowAppeared(object sender, NewWindowAppearedEventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    if (_recorder.IsRecording && IsRelevantWindow(e))
                    {
                        UpdateStatus(string.Format("New window appeared: {0}", e.WindowTitle));
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error handling new window appeared: {0}", ex.Message));
            }
        }

        private bool IsRelevantWindow(NewWindowDetectedEventArgs e)
        {
            return e.WindowType == WindowType.Dialog ||
                   e.WindowType == WindowType.MessageBox ||
                   (!string.IsNullOrEmpty(_recorder.CurrentSequence?.TargetProcessName) &&
                    e.ProcessName.IndexOf(_recorder.CurrentSequence.TargetProcessName, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private bool IsRelevantWindow(NewWindowAppearedEventArgs e)
        {
            return e.WindowType == WindowType.Dialog ||
                   e.WindowType == WindowType.MessageBox ||
                   (!string.IsNullOrEmpty(_recorder.CurrentSequence?.TargetProcessName) &&
                    e.ProcessName.IndexOf(_recorder.CurrentSequence.TargetProcessName, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        #endregion

        #region Recording Controls



        /// <summary>
        /// Toggle medzi Start/Stop recording
        /// </summary>
        private void ToggleRecording_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_recorder.IsRecording)
                {
                    StopCurrentRecording();
                }
                else
                {
                    StartNewRecording();
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error toggling recording", ex);
            }
        }

        #endregion

        #region Enhanced Recording

        private void AutoRefreshAllUIElements_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_targetWindowHandle == IntPtr.Zero)
                {
                    MessageBox.Show("Please select a target window first.", "No Target Selected",
                                   MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var elements = AdaptiveElementFinder.GetAllInteractiveElements(_targetWindowHandle);
                UpdateStatus(string.Format("UI elements refreshed: {0} elements found", elements.Count));
                RefreshElementStatistics();
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error refreshing UI elements", ex);
            }
        }


        private void ShowAutomaticSystemStatus_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var statusInfo = string.Format(
                    "Automatic System Status\n\n" +
                    "Auto-Detection: {0}\n" +
                    "UI Scanning: {1}\n" +
                    "Window Tracking: {2}\n" +
                    "Target Window: {3}\n" +
                    "Recorded Commands: {4}\n" +
                    "Recording Status: {5}\n\n" +
                    "System Information:\n" +
                    "Windows Version: {6}\n" +
                    "Process: {7}",
                    _recorder.EnableRealTimeElementScanning ? "Active" : "Inactive",
                    _recorder.AutoUpdateExistingCommands ? "Enabled" : "Disabled",
                    _isAutoTrackingEnabled ? "Enabled" : "Disabled",
                    _targetWindowHandle != IntPtr.Zero ? GetWindowTitle(_targetWindowHandle) : "None",
                    _commands.Count,
                    _recorder.IsRecording ? "Recording" : "Stopped",
                    Environment.OSVersion.VersionString,
                    Process.GetCurrentProcess().ProcessName);

                MessageBox.Show(statusInfo, "System Status", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error showing system status", ex);
            }
        }

        #endregion

        #region Playback Controls

        private void PlaySequence_Click(object sender, RoutedEventArgs e)
        {
            StartPlayback_Click(sender, e);
        }

        private void TestPlayback_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_commands.Any())
                {
                    MessageBox.Show("No commands to test. Please record some commands first.",
                                   "No Commands", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var testSequence = new CommandSequence
                {
                    Name = "Test Playback",
                    Commands = new List<Command> { _commands.First() },
                    TargetProcessName = GetProcessNameFromWindow(_targetWindowHandle),
                    TargetWindowTitle = GetWindowTitle(_targetWindowHandle)
                };

                _player.TestPlayback(testSequence);
                UpdateStatus("Test playback started with first command");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error starting test playback", ex);
            }
        }

        private void PlayWithoutElementSearch_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_commands.Any())
                {
                    MessageBox.Show("No commands to play.", "No Commands",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show(
                    "Play without element search will use stored coordinates only.\n" +
                    "This may be less reliable but faster.\n\n" +
                    "Do you want to continue?",
                    "Play Direct Mode", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var originalHighlightSetting = _player.HighlightElements;
                    _player.HighlightElements = false;

                    var sequence = new CommandSequence
                    {
                        Name = "Direct Playback",
                        Commands = _commands.ToList(),
                        TargetProcessName = GetProcessNameFromWindow(_targetWindowHandle),
                        TargetWindowTitle = GetWindowTitle(_targetWindowHandle)
                    };

                    _player.PlaySequence(sequence, 1);
                    _player.HighlightElements = originalHighlightSetting;

                    UpdateStatus("Direct playback started (no element search)");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error starting direct playback", ex);
            }
        }
        
        private (bool IsValid, string Message) ValidateLoopIntegrity()
        {
            var loopStarts = _commands.Count(c => c.Type == CommandType.LoopStart);
            var loopEnds = _commands.Count(c => c.Type == CommandType.LoopEnd);

            if (loopStarts != loopEnds)
            {
                return (false, string.Format("Loop mismatch: {0} loop starts, {1} loop ends", loopStarts, loopEnds));
            }

            if (loopStarts > 0)
            {
                return (true, string.Format("Loop validation passed: {0} complete loops detected", loopStarts));
            }

            return (true, "No loops detected");
        }

        #endregion

        #region Menu Handlers - File

        /// <summary>
        /// Aktualizuje NewSequence_Click aby vytvorila unified sequence
        /// </summary>
        private void NewSequence_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Ak používame unified table, vytvor unified sequence
                if (_unifiedItems.Count > 0 || _commands.Count == 0)
                {
                    NewUnifiedSequence();
                }
                else
                {
                    // Fallback na starý systém
                    if (_hasUnsavedChanges)
                    {
                        var result = MessageBox.Show("You have unsaved changes. Do you want to save before creating a new sequence?",
                                                    "Unsaved Changes",
                                                    MessageBoxButton.YesNoCancel,
                                                    MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
                        {
                            SaveSequence_Click(sender, e);
                        }
                        else if (result == MessageBoxResult.Cancel)
                        {
                            return;
                        }
                    }

                    _commands.Clear();
                    _currentFilePath = string.Empty;
                    _hasUnsavedChanges = false;
                    UpdateUI();
                    UpdateStatus("New sequence created");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error creating new sequence", ex);
            }
        }

        // <summary>
        /// Aktualizuje metódu pre načítanie súborov aby podporovala unified sequences
        /// </summary>
        private void OpenSequence_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "All Supported Files|*.acc;*.json;*.uniseq|" +
                            "AppCommander Files (*.acc)|*.acc|" +
                            "JSON Files (*.json)|*.json|" +
                            "Unified Sequence Files (*.uniseq)|*.uniseq|" +
                            "All Files (*.*)|*.*",
                    DefaultExt = ".acc"
                };

                if (dialog.ShowDialog() == true)
                {
                    var extension = Path.GetExtension(dialog.FileName).ToLower();

                    if (extension == ".uniseq")
                    {
                        LoadUnifiedSequenceFromFile(dialog.FileName);
                    }
                    else
                    {
                        // Load as traditional sequence
                        LoadSequenceFromFile(dialog.FileName);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error opening sequence", ex);
            }
        }

        /// <summary>
        /// Aktualizuje metódu pre uloženie aby automaticky rozhodla medzi formátmi
        /// </summary>
        private void SaveSequence_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Ak máme unified items, uprednostníme unified formát
                if (_unifiedItems.Count > 0)
                {
                    if (string.IsNullOrEmpty(_currentUnifiedSequenceFilePath))
                    {
                        SaveAsSet_Click(sender, e);
                        return;
                    }
                    SaveUnifiedSequenceToFile(_currentUnifiedSequenceFilePath);
                }
                else
                {
                    // Fallback na starý systém
                    if (string.IsNullOrEmpty(_currentFilePath))
                    {
                        SaveSequenceAs_Click(sender, e);
                        return;
                    }
                    SaveSequenceToFile(_currentFilePath);
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error saving sequence", ex);
            }
        }

        /// <summary>
        /// Vyčistí unified table a vytvorí novú
        /// </summary>
        private void NewUnifiedSequence()
        {
            try
            {
                if (_hasUnsavedUnifiedChanges)
                {
                    var result = MessageBox.Show("You have unsaved changes in unified sequence. Do you want to save before creating new?",
                                                "Unsaved Changes",
                                                MessageBoxButton.YesNoCancel,
                                                MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        SaveAsSet_Click(null, null);
                    }
                    else if (result == MessageBoxResult.Cancel)
                    {
                        return;
                    }
                }

                _unifiedItems.Clear();
                _currentUnifiedSequence = new UnifiedSequence();
                _currentUnifiedSequenceFilePath = string.Empty;
                _hasUnsavedUnifiedChanges = false;

                UpdateMainWindowUI();
                UpdateStatus("New unified sequence created");
                UpdateUI();
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error creating new unified sequence", ex);
            }
        }

        private void UpdateMainWindowUI()
        {
            try
            {
                AppCommander_TxtSetCount.Text = string.Format("Unified Sequences: {0}", _unifiedItems.Count);
                AppCommander_TxtSequenceCount.Text = string.Format("Unified Items: {0}", _unifiedItems.Count);
                AppCommander_TxtCommandCount.Text = string.Format("Unified Items: {0}", _unifiedItems.Count);
                //unifiedSequenceName.Text = _currentUnifiedSequence?.Name ?? "Unnamed Sequence";
                //txtSequenceDescription.Text = _currentUnifiedSequence?.Description ?? string.Empty;

                //AppCommander_TxtSequenceName_Copy.Text = _currentUnifiedSequence?.Name ?? "Unnamed Sequence";
                if (AppCommander_TxtSequenceName_Copy is TextBox textBox)
                {
                    textBox.Text = _currentUnifiedSequence?.Name ?? "Unnamed Sequence";
                }
                //lstUnifiedItems.ItemsSource = _unifiedItems;
                UpdateUI();

                // Aktualizuje title okna
                string title = "AppCommander";
                if (!string.IsNullOrEmpty(_currentUnifiedSequenceFilePath))
                {
                    title += string.Format(" - {0}", Path.GetFileName(_currentUnifiedSequenceFilePath));
                }
                if (_hasUnsavedUnifiedChanges)
                {
                    title += " *";
                }
                this.Title = title;

                // Aktualizuje status bar
                AppCommander_TxtCommandCount.Text = string.Format("Unified Items: {0}", _unifiedItems.Count);

                // Aktualizuje enabled stav menu položiek
                AppCommander_MenuBar.IsEnabled = _unifiedItems.Count > 0;
                //menuItem.IsEnabled = _hasUnsavedUnifiedChanges && _unifiedItems.Count > 0;

                // Aktualizuje enabled stav playback tlačidiel
                AppCommander_BtnPlayCommands.IsEnabled = _unifiedItems.Count > 0 && !(_recorder?.IsRecording ?? false) && !(_player?.IsPlaying ?? false);
                AppCommander_BtnQuickReselect.IsEnabled = _unifiedItems.Count > 0 && !(_recorder?.IsRecording ?? false) && !(_player?.IsPlaying ?? false);
                AppCommander_BtnPause.IsEnabled = _player?.IsPlaying ?? false;
                AppCommander_BtnStop.IsEnabled = _player?.IsPlaying ?? false;

                // Aktualizuje enabled stav recording tlačidiel
                AppCommander_BtnRecording.IsEnabled = (_targetWindowHandle != IntPtr.Zero) || (_recorder?.IsRecording ?? false);
                AppCommander_AppCommander_BtnSelectTargetByClick.IsEnabled = !(_recorder?.IsRecording ?? false);
                AppCommander_BtnSelectTarget.IsEnabled = !(_recorder?.IsRecording ?? false);

                // Aktualizuje stavový riadok
                if (_recorder?.IsRecording ?? false)
                {
                    UpdateStatus("Recording in progress..."); 
                    UpdateUnsavedCommandsWarning();

                }
                else if (_player?.IsPlaying ?? false)
                {
                    UpdateStatus("Playback in progress...");
                    UpdateUnsavedCommandsWarning();
                }
                else
                {
                    UpdateStatus("Ready");
                    UpdateUnsavedCommandsWarning();
                }

                UpdateUnsavedCommandsWarning();

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in UpdateMainWindowUI: {ex.Message}");
                UpdateUnsavedCommandsWarning();
            }
        }

        private void SaveSequenceAs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "AppCommander Files (*.acc)|*.acc|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                    DefaultExt = ".acc",
                    FileName = string.Format("Sequence_{0:yyyyMMdd_HHmmss}.acc", DateTime.Now)
                };

                if (dialog.ShowDialog() == true)
                {
                    SaveSequenceToFile(dialog.FileName);
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error saving sequence", ex);
            }
        }

        #endregion

        #region Unified Table Playback Support

        /// <summary>
        /// Spustí playback z unified table
        /// </summary>
        private void PlayUnifiedSequence()
        {
            try
            {
                if (_unifiedItems.Count == 0)
                {
                    MessageBox.Show("No items to play. Please add commands or sequences first.",
                                  "No Items",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Information);
                    return;
                }

                // Validate unified sequence
                if (!_currentUnifiedSequence.IsValid(out List<string> errors))
                {
                    var errorMessage = "Validation errors found:\n" + string.Join("\n", errors.Take(5)) +
                                      (errors.Count > 5 ? $"\n... and {errors.Count - 5} more errors." : "") +
                                      "\n\nDo you want to continue anyway?";

                    var result = MessageBox.Show(errorMessage, "Validation Errors",
                                                MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (result == MessageBoxResult.No)
                        return;
                }

                int repeatCount = 1;
                if (!int.TryParse(AppCommander_TxtRepeatCount.Text, out repeatCount) || repeatCount < 1)
                {
                    repeatCount = 1;
                    AppCommander_TxtRepeatCount.Text = "1";
                }

                // Convert unified sequence to traditional format for playback
                var commandSequence = _currentUnifiedSequence.ToCommandSequence();
                commandSequence.Name = $"UnifiedPlayback_{DateTime.Now:HHmmss}";

                _player.PlaySequence(commandSequence, repeatCount);
                UpdateStatus($"Starting unified playback ({repeatCount}x) - {_unifiedItems.Count} items");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error starting unified playback", ex);
            }
        }

        /// <summary>
        /// Aktualizuje StartPlayback_Click aby podporovala unified table
        /// </summary>
        private void StartPlayback_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_player.IsPlaying)
                {
                    UpdateStatus("Playback is already running");
                    return;
                }

                // Prefer unified table if it has content
                if (_unifiedItems.Count > 0)
                {
                    PlayUnifiedSequence();
                }
                else if (_commands.Any())
                {
                    // Fallback to old system
                    var loopValidation = ValidateLoopIntegrity();
                    if (!loopValidation.IsValid)
                    {
                        var message = string.Format("Loop validation warning:\n{0}\n\nDo you want to continue anyway?",
                            loopValidation.Message);
                        var result = MessageBox.Show(message, "Loop Validation",
                            MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        if (result == MessageBoxResult.No)
                            return;
                    }

                    int repeatCount = 1;
                    if (!int.TryParse(AppCommander_TxtRepeatCount.Text, out repeatCount) || repeatCount < 1)
                    {
                        repeatCount = 1;
                        AppCommander_TxtRepeatCount.Text = "1";
                    }

                    var sequence = new CommandSequence
                    {
                        Name = string.Format("Playback_{0:HHmmss}", DateTime.Now),
                        Commands = _commands.ToList(),
                        TargetApplication = GetProcessNameFromWindow(_targetWindowHandle),
                        TargetProcessName = GetProcessNameFromWindow(_targetWindowHandle),
                        TargetWindowTitle = GetWindowTitle(_targetWindowHandle)
                    };

                    _player.PlaySequence(sequence, repeatCount);
                    UpdateStatus(string.Format("Starting playback ({0}x) - {1} commands", repeatCount, _commands.Count));
                }
                else
                {
                    MessageBox.Show("No commands or items to play. Please record some commands or add sequences first.",
                                  "No Content",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error starting playback", ex);
            }
        }

        #endregion

        #region Updated Window Closing

        /// <summary>
        /// Aktualizuje MainWindow_Closing aby kontrolovala aj unified changes
        /// </summary>
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                bool hasUnsavedChanges = _hasUnsavedChanges || _hasUnsavedUnifiedChanges || _hasUnsavedSequenceSetChanges;

                if (hasUnsavedChanges)
                {
                    var changes = new List<string>();
                    if (_hasUnsavedChanges) changes.Add("recorded commands");
                    if (_hasUnsavedUnifiedChanges) changes.Add("unified sequence");
                    if (_hasUnsavedSequenceSetChanges) changes.Add("sequence set");

                    var changesList = string.Join(", ", changes);
                    var result = MessageBox.Show($"You have unsaved changes in: {changesList}.\n\nDo you want to save before closing?",
                                                "Unsaved Changes",
                                                MessageBoxButton.YesNoCancel,
                                                MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        // Save the most relevant format
                        if (_hasUnsavedUnifiedChanges)
                        {
                            SaveAsSet_Click(null, null);
                        }
                        else if (_hasUnsavedChanges)
                        {
                            SaveSequence_Click(null, null);
                        }
                        else if (_hasUnsavedSequenceSetChanges)
                        {
                            BtnSaveSetAs_Click(null, null);
                        }
                    }
                    else if (result == MessageBoxResult.Cancel)
                    {
                        e.Cancel = true;
                        return;
                    }
                }

                // Cleanup
                if (_recorder != null) _recorder.Dispose();
                if (_player != null) _player.Dispose();
                if (_windowTracker != null) _windowTracker.Dispose();
                if (_automaticUIManager != null) _automaticUIManager.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error during window closing: {0}", ex.Message));
            }
        }

        /// <summary>
        /// Uloží unified sekvenciu ako nový súbor (.uniseq)
        /// </summary>
        private void SaveAsSet_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Kontrola, či existujú unified items
                if (_unifiedItems == null || _unifiedItems.Count == 0)
                {
                    MessageBox.Show("Cannot save empty sequence. Please add commands or sequences first.",
                                    "Empty Sequence",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
                    return;
                }

                // Validácia sekvencie
                if (!_currentUnifiedSequence.IsValid(out List<string> errors))
                {
                    var errorMessage = "Validation warnings found:\n" +
                                      string.Join("\n", errors.Take(5)) +
                                      (errors.Count > 5 ? $"\n... and {errors.Count - 5} more errors." : "") +
                                      "\n\nDo you want to save anyway?";

                    var validationResult = MessageBox.Show(errorMessage, "Validation Warnings",
                                                MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (validationResult == MessageBoxResult.No)
                        return;
                }

                // Získanie názvu sekvencie z textboxu
                string defaultName = "UnifiedSequence_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                if (AppCommander_TxtSequenceName_Copy is TextBox textBox && !string.IsNullOrEmpty(textBox.Text))
                {
                    defaultName = textBox.Text;
                }

                // SaveFileDialog
                var dialog = new SaveFileDialog
                {
                    Filter = "Unified Sequence Files (*.uniseq)|*.uniseq|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                    DefaultExt = ".uniseq",
                    Title = "Save Unified Sequence As",
                    FileName = defaultName
                };

                if (dialog.ShowDialog() == true)
                {
                    SaveUnifiedSequenceToFile(dialog.FileName);
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error saving unified sequence", ex);
            }
        }

        /// <summary>
        /// Uloží unified sekvenciu do súboru
        /// </summary>
        private void SaveUnifiedSequenceToFile(string filePath)
        {
            try
            {
                if (_unifiedItems == null || _unifiedItems.Count == 0)
                {
                    MessageBox.Show("Cannot save empty sequence.",
                                    "Empty Sequence",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
                    return;
                }

                // Aktualizácia _currentUnifiedSequence
                _currentUnifiedSequence.Name = (AppCommander_TxtSequenceName_Copy is TextBox textBox && !string.IsNullOrEmpty(textBox.Text)) ?
                                                textBox.Text :
                                                Path.GetFileNameWithoutExtension(filePath);

                _currentUnifiedSequence.Description = $"Unified sequence created on {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                _currentUnifiedSequence.Items = _unifiedItems.ToList();
                _currentUnifiedSequence.LastModified = DateTime.Now;
                _currentUnifiedSequence.FilePath = filePath;

                // Ak je to prvé uloženie, nastav Created
                if (_currentUnifiedSequence.Created == default(DateTime))
                {
                    _currentUnifiedSequence.Created = DateTime.Now;
                }

                // Serializácia do JSON
                var json = JsonConvert.SerializeObject(_currentUnifiedSequence, Formatting.Indented);
                File.WriteAllText(filePath, json);

                // Aktualizácia stavu
                _currentUnifiedSequenceFilePath = filePath;
                _hasUnsavedUnifiedChanges = false;

                UpdateMainWindowUI();
                UpdateStatus($"Unified sequence saved: {Path.GetFileName(filePath)} ({_unifiedItems.Count} items)");

                Debug.WriteLine($"Unified sequence saved to: {filePath}");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error saving unified sequence to file", ex);
            }
        }

        /// <summary>
        /// Načíta unified sekvenciu zo súboru
        /// </summary>
        private void LoadUnifiedSequenceFromFile(string filePath)
        {
            try
            {
                // Kontrola existencie súboru
                if (!File.Exists(filePath))
                {
                    MessageBox.Show($"File '{filePath}' does not exist.",
                                    "File Not Found",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                    return;
                }

                // Kontrola neuložených zmien
                if (_hasUnsavedUnifiedChanges)
                {
                    var result = MessageBox.Show(
                        "You have unsaved changes. Do you want to save before loading a new sequence?",
                        "Unsaved Changes",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        SaveAsSet_Click(null, null);
                    }
                    else if (result == MessageBoxResult.Cancel)
                    {
                        return;
                    }
                }

                // Načítanie a deserializácia JSON súboru
                var json = File.ReadAllText(filePath);
                var unifiedSequence = JsonConvert.DeserializeObject<UnifiedSequence>(json);

                if (unifiedSequence == null)
                {
                    MessageBox.Show("Invalid unified sequence file format.",
                                    "Invalid File",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                    return;
                }

                // Validácia Items
                if (unifiedSequence.Items == null)
                {
                    unifiedSequence.Items = new List<UnifiedItem>();
                }

                // Vyčistenie a naplnenie _unifiedItems
                _unifiedItems.Clear();

                foreach (var item in unifiedSequence.Items)
                {
                    // Validácia položiek
                    if (item.Type == UnifiedItem.ItemType.SequenceReference)
                    {
                        // Kontrola, či referencovaný súbor existuje
                        if (!string.IsNullOrEmpty(item.FilePath) && !File.Exists(item.FilePath))
                        {
                            item.Status = "File Missing";
                            Debug.WriteLine($"Warning: Referenced sequence file not found: {item.FilePath}");
                        }
                    }

                    _unifiedItems.Add(item);
                }

                // Prepočítanie step numbers
                for (int i = 0; i < _unifiedItems.Count; i++)
                {
                    _unifiedItems[i].StepNumber = i + 1;
                }

                // Aktualizácia stavu
                _currentUnifiedSequence = unifiedSequence;
                _currentUnifiedSequenceFilePath = filePath;
                _hasUnsavedUnifiedChanges = false;

                // Aktualizácia názvu v textboxe
                if (AppCommander_TxtSequenceName_Copy is TextBox textBox)
                {
                    textBox.Text = unifiedSequence.Name ?? Path.GetFileNameWithoutExtension(filePath);
                }

                UpdateMainWindowUI();
                UpdateStatus($"Unified sequence loaded: {Path.GetFileName(filePath)} ({_unifiedItems.Count} items)");

                Debug.WriteLine($"Unified sequence loaded from: {filePath}");
                Debug.WriteLine($"  Name: {unifiedSequence.Name}");
                Debug.WriteLine($"  Items: {_unifiedItems.Count}");
                Debug.WriteLine($"  Created: {unifiedSequence.Created}");
                Debug.WriteLine($"  Last Modified: {unifiedSequence.LastModified}");
            }
            catch (JsonException ex)
            {
                MessageBox.Show($"Error parsing unified sequence file:\n\n{ex.Message}",
                                "Invalid JSON",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                Debug.WriteLine($"JSON parse error: {ex.Message}");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error loading unified sequence", ex);
            }
        }

        #endregion

        #region Updated Unified Table Methods

        /// <summary>
        /// Presúva položku vyššie v unified tabuľke
        /// </summary>
        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = AppCommander_MainCommandTable.SelectedItem as UnifiedItem;
                if (selectedItem == null || selectedItem.StepNumber <= 1)
                {
                    UpdateStatus("Cannot move item up - select an item that is not first");
                    return;
                }

                var currentIndex = _unifiedItems.IndexOf(selectedItem);
                if (currentIndex > 0)
                {
                    _unifiedItems.Move(currentIndex, currentIndex - 1);
                    RecalculateStepNumbers();
                    _hasUnsavedUnifiedChanges = true;

                    // Keep selection on moved item
                    AppCommander_MainCommandTable.SelectedItem = selectedItem;
                    UpdateStatus($"Moved '{selectedItem.Name}' up");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error moving item up", ex);
            }
        }

        /// <summary>
        /// Presúva položku nižšie v unified tabuľke
        /// </summary>
        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = AppCommander_MainCommandTable.SelectedItem as UnifiedItem;
                if (selectedItem == null || selectedItem.StepNumber >= _unifiedItems.Count)
                {
                    UpdateStatus("Cannot move item down - select an item that is not last");
                    return;
                }

                var currentIndex = _unifiedItems.IndexOf(selectedItem);
                if (currentIndex < _unifiedItems.Count - 1)
                {
                    _unifiedItems.Move(currentIndex, currentIndex + 1);
                    RecalculateStepNumbers();
                    _hasUnsavedUnifiedChanges = true;

                    // Keep selection on moved item
                    AppCommander_MainCommandTable.SelectedItem = selectedItem;
                    UpdateStatus($"Moved '{selectedItem.Name}' down");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error moving item down", ex);
            }
        }

        private void RecalculateStepNumbers()
        {
            for (int i = 0; i < _unifiedItems.Count; i++)
            {
                _unifiedItems[i].StepNumber = i + 1;
            }
        }

        /// <summary>
        /// Pridá príkazy z tabuľky AppCommander_MainCommandTable do unified tabuľky
        /// </summary>
        private void AddFromCommands_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_commands.Count == 0)
                {
                    MessageBox.Show("No recorded commands to add. Please record some commands first.",
                                   "No Commands",
                                   MessageBoxButton.OK,
                                   MessageBoxImage.Information);
                    return;
                }

                // Check if user wants to save commands first
                if (_hasUnsavedChanges)
                {
                    var result = MessageBox.Show(
                        "You have unsaved recorded commands. It's recommended to save them as a sequence first.\n\n" +
                        "Do you want to save them as a sequence and then add to the unified list?",
                        "Unsaved Commands",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        SaveSequenceAs_Click(sender, e);
                        // If save was successful, the commands are now saved, continue with adding
                    }
                    else if (result == MessageBoxResult.Cancel)
                    {
                        return;
                    }
                    // If No, continue with adding unsaved commands
                }

                // Uložíme index odkiaľ začíname pridávať, aby sme vedeli ktoré položky zmazať
                int startIndex = _unifiedItems.Count;

                // Convert commands to unified items
                int addedCount = 0;
                foreach (var command in _commands)
                {
                    var unifiedItem = UnifiedItem.FromCommand(command, _unifiedItems.Count + 1);
                    _unifiedItems.Add(unifiedItem);
                    addedCount++;
                }

                _hasUnsavedUnifiedChanges = true;
                RecalculateStepNumbers();
                UpdateStatus($"Added {addedCount} commands to unified sequence");
                UpdateUnsavedCommandsWarning();

                // LOGIKA: Ponúkni zmazanie len ak užívateľ zaznamenal DRUHÚ sadu príkazov
                // Kontrolujeme či unified items už obsahovali nejaké príkazy pred týmto pridaním
                bool hadPreviousCommands = startIndex > 0;

                if (hadPreviousCommands)
                {
                    // Optionally clear the original commands after adding
                    // PRVÝ MESSAGE BOX - informačný
                    var clearResult = MessageBox.Show(
                        $"Successfully added {addedCount} commands to the unified list.\n\n" +
                        "Do you want to delete the previous list of recorded commands?\n\n" +
                        "⚠️ Note: This will remove commands from the recording area,\n" +
                        "but they will remain in the main table (unified sequence).",
                        "Clear Original Commands",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (clearResult == MessageBoxResult.Yes)
                    {
                        // DRUHÝ MESSAGE BOX - bezpečnostné potvrdenie
                        var confirmResult = MessageBox.Show(
                            "⚠️ CONFIRMATION REQUIRED ⚠️\n\n" +
                            $"Are you absolutely sure you want to clear {_commands.Count} recorded commands?\n\n" +
                            "This action will:\n" +
                            "• Clear the original recording list\n" +
                            "• Keep the commands in the unified sequence (main table)\n" +
                            "• Cannot be undone\n\n" +
                            "Do you want to proceed?",
                            "Confirm Clear Commands",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (confirmResult == MessageBoxResult.Yes)
                        {
                            // Vymazanie príkazov
                            _commands.Clear();
                            _hasUnsavedChanges = false;

                            // Aktualizácia UI
                            UpdateUI();

                            UpdateStatus($"✓ Recorded commands cleared. {addedCount} commands remain in unified sequence.");

                            MessageBox.Show(
                                $"✓ Original commands cleared successfully!\n\n" +
                                $"{addedCount} commands are still available in the main unified sequence.",
                                "Commands Cleared",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                        else
                        {
                            UpdateStatus("Clear operation cancelled by user");
                        }
                    }
                }
                else
                {
                    // Ak toto bola prvá sada príkazov, len informuj užívateľa
                    UpdateStatus($"First set of {addedCount} commands added. Record more commands to enable clear option.");
                }

                UpdateUnsavedCommandsWarning();
                Debug.WriteLine("called method UpdateUnsavedCommandsWarning() in method AddFromCommands_Click()");

            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error adding commands to unified list", ex);
            }
        }

        /// <summary>
        /// Pridá sekvenciu zo súboru do unified tabuľky
        /// </summary>
        private void AddSequence_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    Filter = "AppCommander Files (*.acc)|*.acc|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                    Title = "Select Sequence File to Add",
                    Multiselect = false
                };

                if (dialog.ShowDialog() == true)
                {
                    var filePath = dialog.FileName;
                    var fileName = Path.GetFileNameWithoutExtension(filePath);

                    // Skontroluj či už sekvencia nie je pridaná
                    if (_unifiedItems.Any(item =>
                        item.Type == UnifiedItem.ItemType.SequenceReference &&
                        item.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
                    {
                        MessageBox.Show($"Sequence '{fileName}' is already added to the unified list.",
                                       "Duplicate Sequence",
                                       MessageBoxButton.OK,
                                       MessageBoxImage.Warning);
                        return;
                    }

                    // Validácia súboru
                    if (!File.Exists(filePath))
                    {
                        MessageBox.Show($"File '{filePath}' does not exist.",
                                       "File Not Found",
                                       MessageBoxButton.OK,
                                       MessageBoxImage.Error);
                        return;
                    }

                    // Validácia obsahu súboru
                    if (!ValidateSequenceFile(filePath))
                    {
                        MessageBox.Show($"File '{fileName}' is not a valid sequence file.",
                                       "Invalid File",
                                       MessageBoxButton.OK,
                                       MessageBoxImage.Error);
                        return;
                    }

                    // Vytvorenie nového UnifiedItem
                    var unifiedItem = UnifiedItem.FromSequenceFile(filePath, _unifiedItems.Count + 1);
                    _unifiedItems.Add(unifiedItem);

                    _hasUnsavedUnifiedChanges = true;
                    RecalculateStepNumbers();

                    UpdateStatus($"Sequence '{fileName}' added to unified list");
                    Debug.WriteLine($"Added sequence to unified list: {fileName} from {filePath}");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error adding sequence to unified list", ex);
            }
        }


        /// <summary>
        /// Tlačidlo Edit v action buttons - Hlavná editácia - otvorí plné okno
        /// </summary>
        private void EditItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = AppCommander_MainCommandTable.SelectedItem as UnifiedItem;
                OpenSmartEditor(selectedItem);
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error opening editor", ex);
            }
        }

        /// <summary>
        /// Edituje vybranú položku v unified tabuľke - Rýchla editácia 
        /// </summary>
        private void QuickEditItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = AppCommander_MainCommandTable.SelectedItem as UnifiedItem;
                if (selectedItem == null)
                {
                    MessageBox.Show("Please select an item to edit.",
                                   "No Selection",
                                   MessageBoxButton.OK,
                                   MessageBoxImage.Information);
                    return;
                }

                bool wasModified = false;

                if (selectedItem.Type == UnifiedItem.ItemType.SequenceReference)
                {
                    // Edit repeat count for sequences
                    var newRepeatCount = ShowInputDialog(
                        $"Enter repeat count for sequence '{selectedItem.Name}':",
                        "Edit Sequence Repeat Count",
                        selectedItem.RepeatCount.ToString());

                    if (!string.IsNullOrEmpty(newRepeatCount) &&
                        int.TryParse(newRepeatCount, out int count) && count > 0)
                    {
                        selectedItem.RepeatCount = count;
                        selectedItem.Status = "Modified";
                        wasModified = true;
                        UpdateStatus($"Sequence '{selectedItem.Name}' repeat count updated to {count}");
                    }
                    else if (!string.IsNullOrEmpty(newRepeatCount))
                    {
                        MessageBox.Show("Please enter a valid positive number.",
                                       "Invalid Input",
                                       MessageBoxButton.OK,
                                       MessageBoxImage.Warning);
                    }
                }
                else if (selectedItem.Type == UnifiedItem.ItemType.Command ||
                         selectedItem.Type == UnifiedItem.ItemType.Wait)
                {
                    // Edit command value
                    var newValue = ShowInputDialog(
                        $"Edit value for '{selectedItem.Name}':",
                        "Edit Command Value",
                        selectedItem.Value);

                    if (!string.IsNullOrEmpty(newValue) && newValue != selectedItem.Value)
                    {
                        selectedItem.Value = newValue;
                        selectedItem.Status = "Modified";
                        wasModified = true;
                        UpdateStatus($"Command '{selectedItem.Name}' value updated");
                    }
                }
                else if (selectedItem.Type == UnifiedItem.ItemType.LoopStart)
                {
                    // Edit loop repeat count
                    var newRepeatCount = ShowInputDialog(
                        $"Enter repeat count for loop:",
                        "Edit Loop Repeat Count",
                        selectedItem.RepeatCount.ToString());

                    if (!string.IsNullOrEmpty(newRepeatCount) &&
                        int.TryParse(newRepeatCount, out int count) && count > 0)
                    {
                        selectedItem.RepeatCount = count;
                        selectedItem.Value = count.ToString();
                        selectedItem.Status = "Modified";
                        wasModified = true;
                        UpdateStatus($"Loop repeat count updated to {count}");
                    }
                }

                if (wasModified)
                {
                    _hasUnsavedUnifiedChanges = true;
                    AppCommander_MainCommandTable.Items.Refresh();
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error editing unified item", ex);
            }
        }

        /// <summary>
        /// Odstráni vybranú položku z unified tabuľky
        /// </summary>
        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = AppCommander_MainCommandTable.SelectedItem as UnifiedItem;
                if (selectedItem == null)
                {
                    MessageBox.Show("Please select an item to delete.",
                                   "No Selection",
                                   MessageBoxButton.OK,
                                   MessageBoxImage.Information);
                    return;
                }

                var message = $"Are you sure you want to delete this item?\n\n" +
                             $"Type: {selectedItem.TypeDisplay}\n" +
                             $"Name: {selectedItem.Name}";

                var result = MessageBox.Show(message,
                                            "Confirm Delete",
                                            MessageBoxButton.YesNo,
                                            MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var itemName = selectedItem.Name;
                    _unifiedItems.Remove(selectedItem);
                    RecalculateStepNumbers();
                    _hasUnsavedUnifiedChanges = true;

                    UpdateStatus($"Deleted item: {itemName}");
                }

                UpdateUnsavedCommandsWarning();
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error deleting unified item", ex);
            }
        }

        #endregion

        #region Menu Handlers - Commands

        private void AddWaitCommand_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_recorder.IsRecording)
                {
                    MessageBox.Show("Please start recording first.", "Not Recording",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string input = ShowInputDialog("Enter wait time in milliseconds:", "Add Wait Command", "1000");

                if (!string.IsNullOrEmpty(input) && int.TryParse(input, out int waitTime) && waitTime > 0)
                {
                    _recorder.AddWaitCommand(waitTime);
                    UpdateStatus(string.Format("Wait command added: {0}ms", waitTime));
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error adding wait command", ex);
            }
        }

        private void AddLoopStart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_recorder.IsRecording)
                {
                    MessageBox.Show("Please start recording first.", "Not Recording",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string input = ShowInputDialog("Enter number of repetitions:", "Add Loop Start", "2");

                if (!string.IsNullOrEmpty(input) && int.TryParse(input, out int repeatCount) && repeatCount > 0)
                {
                    var loopCommand = new Command
                    {
                        StepNumber = _commands.Count + 1,
                        Type = CommandType.LoopStart,
                        ElementName = "Loop Start",
                        Value = repeatCount.ToString(),
                        RepeatCount = repeatCount,
                        IsLoopStart = true,
                        Timestamp = DateTime.Now
                    };

                    _commands.Add(loopCommand);
                    _hasUnsavedChanges = true;
                    UpdateUI();
                    UpdateStatus(string.Format("Loop start added: repeat {0}x", repeatCount));
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error adding loop start", ex);
            }
        }

        private void AddLoopEnd_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_recorder.IsRecording)
                {
                    MessageBox.Show("Please start recording first.", "Not Recording",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var lastLoopStart = _commands.LastOrDefault(c => c.Type == CommandType.LoopStart);
                var loopEndsCount = _commands.Count(c => c.Type == CommandType.LoopEnd);
                var loopStartsCount = _commands.Count(c => c.Type == CommandType.LoopStart);

                if (loopStartsCount <= loopEndsCount)
                {
                    MessageBox.Show("No open loop found. Please add a Loop Start first.", "No Open Loop",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var loopEndCommand = new Command
                {
                    StepNumber = _commands.Count + 1,
                    Type = CommandType.LoopEnd,
                    ElementName = "Loop End",
                    Value = lastLoopStart != null ? lastLoopStart.Value : "1",
                    IsLoopEnd = true,
                    Timestamp = DateTime.Now
                };

                _commands.Add(loopEndCommand);
                _hasUnsavedChanges = true;
                UpdateUI();
                UpdateStatus("Loop end added");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error adding loop end", ex);
            }
        }

        private void OpenEditCommandWindow(UnifiedItem item)
        {
            try
            {
                var editWindow = new EditCommandWindow();

                if (editWindow.AppCommander_TxtStepNumber != null) editWindow.AppCommander_TxtStepNumber.Text = item.StepNumber.ToString();
                if (editWindow.AppCommander_TxtType != null) editWindow.AppCommander_TxtType.Text = item.TypeDisplay;
                if (editWindow.AppCommander_TxtName != null) editWindow.AppCommander_TxtName.Text = item.Name;
                if (editWindow.AppCommander_TxtAction != null) editWindow.AppCommander_TxtAction.Text = item.Action;
                if (editWindow.AppCommander_TxtValue != null) editWindow.AppCommander_TxtValue.Text = item.Value;
                if (editWindow.AppCommander_TxtRepeatCount != null) editWindow.AppCommander_TxtRepeatCount.Text = item.RepeatCount.ToString();
                if (editWindow.AppCommander_TxtStatus != null) editWindow.AppCommander_TxtStatus.Text = item.Status;
                if (editWindow.AppCommander_TxtTimestamp != null) editWindow.AppCommander_TxtTimestamp.Text = item.Timestamp.ToString("G");
                if (editWindow.AppCommander_TxtFilePath != null) editWindow.AppCommander_TxtFilePath.Text = item.FilePath;
                if (editWindow.AppCommander_TxtElementX != null) editWindow.AppCommander_TxtElementX.Text = item.ElementX?.ToString() ?? "";
                if (editWindow.AppCommander_TxtElementY != null) editWindow.AppCommander_TxtElementY.Text = item.ElementY?.ToString() ?? "";
                if (editWindow.AppCommander_TxtElementId != null) editWindow.AppCommander_TxtElementId.Text = item.ElementId;
                if (editWindow.AppCommander_TxtClassName != null) editWindow.AppCommander_TxtClassName.Text = item.ClassName;

                editWindow.Owner = this;

                bool? result = editWindow.ShowDialog();

                // If changes were saved, update the UnifiedItem from the dialog fields
                if (result == true && editWindow.WasSaved)
                {
                    item.Name = editWindow.AppCommander_TxtName?.Text ?? item.Name;
                    item.Action = editWindow.AppCommander_TxtAction?.Text ?? item.Action;
                    item.Value = editWindow.AppCommander_TxtValue?.Text ?? item.Value;
                    if (int.TryParse(editWindow.AppCommander_TxtRepeatCount?.Text, out int repeatCount))
                        item.RepeatCount = repeatCount;
                    item.Status = editWindow.AppCommander_TxtStatus?.Text ?? item.Status;
                    if (DateTime.TryParse(editWindow.AppCommander_TxtTimestamp?.Text, out DateTime timestamp))
                        item.Timestamp = timestamp;
                    item.FilePath = editWindow.AppCommander_TxtFilePath?.Text ?? item.FilePath;
                    if (int.TryParse(editWindow.AppCommander_TxtElementX?.Text, out int elementX))
                        item.ElementX = elementX;
                    if (int.TryParse(editWindow.AppCommander_TxtElementY?.Text, out int elementY))
                        item.ElementY = elementY;
                    item.ElementId = editWindow.AppCommander_TxtElementId?.Text ?? item.ElementId;
                    item.ClassName = editWindow.AppCommander_TxtClassName?.Text ?? item.ClassName;

                    _hasUnsavedUnifiedChanges = true;
                    AppCommander_MainCommandTable.Items.Refresh();
                    
                    string.Format("Command '{0}' updated", item.Name);
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error editing command", ex);
            }
        }

        /// <summary>
        /// Menu Commands → Edit Selected Command
        /// </summary>
        private void EditCommand_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = AppCommander_MainCommandTable.SelectedItem as UnifiedItem;
                OpenSmartEditor(selectedItem);
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error opening editor", ex);
            }
        }

        private void DeleteCommand_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedCommand = AppCommander_MainCommandTable.SelectedItem as Command;
                if (selectedCommand == null)
                {
                    MessageBox.Show("Please select a command to delete.", "No Selection",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var message = string.Format("Are you sure you want to delete this command?\n\n{0}: {1}",
                    selectedCommand.Type, selectedCommand.ElementName);
                var result = MessageBox.Show(message, "Confirm Delete",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _commands.Remove(selectedCommand);
                    _hasUnsavedChanges = true;
                    UpdateUI();
                    UpdateStatus("Command deleted");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error deleting command", ex);
            }
        }

        #endregion

        #region Menu Handlers - Tools & Help

        private void WindowSelector_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new WindowSelectorDialog();
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error opening window selector", ex);
            }
        }

        private void UserGuide_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MessageBox.Show("AppCommander User Guide\n\n" +
                               "Basic Usage:\n" +
                               "1. Click 'Select Target' to choose application window\n" +
                               "2. Click 'Start Recording' and perform actions\n" +
                               "3. Click 'Stop Recording' when finished\n" +
                               "4. Click 'Play' to replay recorded actions\n\n" +
                               "Advanced Features:\n" +
                               "• Use 'Enhanced Recording' for better element detection\n" +
                               "• Add loops using Commands menu\n" +
                               "• Set repeat count for multiple playbacks\n" +
                               "• Use Element Inspector to analyze UI elements\n\n" +
                               "For detailed documentation, visit project repository.",
                               "User Guide", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error showing user guide", ex);
            }
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateStatus("Log cleared");
                Debug.WriteLine("Log cleared by user");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error clearing log", ex);
            }
        }

        private void DebugCoordinates_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MessageBox.Show("Debug Coordinates Tool\n\n" +
                               "This tool helps debug coordinate issues:\n" +
                               "• Shows current mouse position\n" +
                               "• Displays screen resolution\n" +
                               "• Tests coordinate conversion\n\n" +
                               "Move mouse and press F12 to capture coordinates.",
                               "Debug Coordinates", MessageBoxButton.OK, MessageBoxImage.Information);

                UpdateStatus("Debug coordinates tool activated - Press F12 to capture mouse position");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error activating debug coordinates", ex);
            }
        }

        private void ElementInspector_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_targetWindowHandle == IntPtr.Zero)
                {
                    MessageBox.Show("Please select a target window first.", "No Target Selected",
                                   MessageBoxButton.OK, MessageBoxImage.Warning);
                    SelectTarget_Click(sender, e);
                    return;
                }

                string processName = GetProcessNameFromWindow(_targetWindowHandle);
                string windowTitle = GetWindowTitle(_targetWindowHandle);

                int elementCount = 0;
                try
                {
                    var elements = AdaptiveElementFinder.GetAllInteractiveElements(_targetWindowHandle);
                    elementCount = elements.Count;
                }
                catch
                {
                    elementCount = 0;
                }

                string inspectorInfo = string.Format(
                    "Element Inspector\n\n" +
                    "Target Window Information:\n" +
                    "Process: {0}\n" +
                    "Title: {1}\n" +
                    "Handle: 0x{2:X8}\n" +
                    "Interactive Elements Found: {3}\n\n" +
                    "Features:\n" +
                    "• UI element detection and analysis\n" +
                    "• Element property inspection\n" +
                    "• Automation identifier discovery\n" +
                    "• Coordinate mapping\n\n" +
                    "This feature is available and functional.",
                    processName,
                    windowTitle,
                    _targetWindowHandle.ToInt64(),
                    elementCount);

                MessageBox.Show(inspectorInfo, "Element Inspector",
                               MessageBoxButton.OK, MessageBoxImage.Information);

                UpdateStatus(string.Format("Element inspector used - Found {0} elements", elementCount));
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error opening element inspector", ex);
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string currentSettings = string.Format(
                    "AppCommander Settings\n\n" +
                    "Recording Configuration:\n" +
                    "• Default delay: {0}ms\n" +
                    "• Auto-tracking: {1}\n" +
                    "• Element scanning: {2}\n" +
                    "• Predictive detection: {3}\n\n" +
                    "Playback Configuration:\n" +
                    "• Stop on error: {4}\n" +
                    "• Highlight elements: {5}\n\n" +
                    "System Information:\n" +
                    "• Windows: {6}\n" +
                    "• .NET Framework: {7}\n" +
                    "• Commands recorded: {8}\n\n" +
                    "Note: Settings modification will be available in future versions.\n" +
                    "Current configuration uses optimized defaults for Windows 7-11.",
                    _player != null ? _player.DefaultDelayBetweenCommands : 100,
                    _isAutoTrackingEnabled ? "Enabled" : "Disabled",
                    _recorder != null && _recorder.EnableRealTimeElementScanning ? "Active" : "Inactive",
                    _recorder != null && _recorder.EnablePredictiveDetection ? "Enabled" : "Disabled",
                    _player != null ? _player.StopOnError.ToString() : "False",
                    _player != null ? _player.HighlightElements.ToString() : "True",
                    Environment.OSVersion.VersionString,
                    Environment.Version.ToString(),
                    _commands.Count);

                MessageBox.Show(currentSettings, "Settings",
                               MessageBoxButton.OK, MessageBoxImage.Information);

                UpdateStatus("Settings information displayed");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error showing settings", ex);
            }
        }

        private void ExportAutoDetectedData_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_commands.Any())
                {
                    MessageBox.Show("No commands to export.", "No Data",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var dialog = new SaveFileDialog
                {
                    Filter = "Text Files (*.txt)|*.txt|CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                    DefaultExt = ".txt",
                    FileName = string.Format("AutoDetected_Data_{0:yyyyMMdd_HHmmss}.txt", DateTime.Now)
                };

                if (dialog.ShowDialog() == true)
                {
                    var exportText = GenerateAutoDetectedDataReport();
                    File.WriteAllText(dialog.FileName, exportText);

                    UpdateStatus(string.Format("Auto-detected data exported: {0}", Path.GetFileName(dialog.FileName)));
                    MessageBox.Show("Data exported successfully!", "Export Complete",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error exporting auto-detected data", ex);
            }
        }

        private void ExportSequenceForDebug_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_commands.Any())
                {
                    MessageBox.Show("No commands to export.", "No Data",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var dialog = new SaveFileDialog
                {
                    Filter = "Text Files (*.txt)|*.txt|Debug Files (*.debug)|*.debug|All Files (*.*)|*.*",
                    DefaultExt = ".txt",
                    FileName = string.Format("Debug_Export_{0:yyyyMMdd_HHmmss}.txt", DateTime.Now)
                };

                if (dialog.ShowDialog() == true)
                {
                    var sequence = new CommandSequence
                    {
                        Name = "Debug Export",
                        Commands = _commands.ToList(),
                        TargetProcessName = GetProcessNameFromWindow(_targetWindowHandle),
                        TargetWindowTitle = GetWindowTitle(_targetWindowHandle)
                    };

                    var debugReport = DebugTestHelper.ExportSequenceAsText(sequence);
                    File.WriteAllText(dialog.FileName, debugReport);

                    UpdateStatus(string.Format("Debug info exported: {0}", Path.GetFileName(dialog.FileName)));
                    MessageBox.Show("Debug information exported successfully!", "Export Complete",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error exporting debug info", ex);
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.Close();
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error closing application", ex);
            }
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("AppCommander WPF - Version 1.0\n\n" +
                          "A powerful tool for automating Windows applications.\n" +
                          "Features:\n" +
                          "• Record and playback UI interactions\n" +
                          "• Smart element detection by name\n" +
                          "• Table cell support\n" +
                          "• Loop commands with repeat functionality\n" +
                          "• Automatic window tracking\n" +
                          "• Enhanced WinUI3 support\n\n" +
                          "Developed by Rudolf Mendzezof\n" +
                          "Enhanced with advanced automation features",
                          "About AppCommander", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Loop Controls

        private bool _isUpdatingRepeatCount = false; // Flag to prevent recursion

        private void InfiniteLoop_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_isUpdatingRepeatCount) return; // Prevent recursion

                _isUpdatingRepeatCount = true;

                AppCommander_TxtRepeatCount.IsEnabled = false;
                AppCommander_TxtRepeatCount.Text = "∞";
                UpdateStatus("Infinite loop enabled");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error enabling infinite loop", ex);
            }
            finally
            {
                _isUpdatingRepeatCount = false;
            }
        }

        private void InfiniteLoop_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_isUpdatingRepeatCount) return; // Prevent recursion

                _isUpdatingRepeatCount = true;

                AppCommander_TxtRepeatCount.IsEnabled = true;
                AppCommander_TxtRepeatCount.Text = "1";
                UpdateStatus("Infinite loop disabled");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error disabling infinite loop", ex);
            }
            finally
            {
                _isUpdatingRepeatCount = false;
            }
        }

        private void AppCommander_TxtRepeatCount_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingRepeatCount) return; // Prevent recursion during programmatic changes

                // Your existing TextChanged logic here (if any)
                // Ale NIKDY nevolajte UpdateUI() odtiaľto!
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in AppCommander_TxtRepeatCount_TextChanged: {ex.Message}");
            }
        }

        #endregion

        #region File Operations

        private void LoadSequenceFromFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    MessageBox.Show("File not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var json = File.ReadAllText(filePath);
                var sequence = JsonConvert.DeserializeObject<CommandSequence>(json);

                if (sequence != null && sequence.Commands != null)
                {
                    _commands.Clear();
                    foreach (var command in sequence.Commands)
                    {
                        _commands.Add(command);
                    }

                    _currentFilePath = filePath;
                    _hasUnsavedChanges = false;
                    UpdateUI();

                    var loopInfo = GetSequenceLoopInfo(sequence);
                    var statusMsg = string.Format("Sequence loaded: {0} ({1} commands{2})",
                        Path.GetFileName(filePath), _commands.Count, loopInfo);
                    UpdateStatus(statusMsg);
                }
                else
                {
                    MessageBox.Show("Invalid file format.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error loading sequence", ex);
            }
        }

        private string GetSequenceLoopInfo(CommandSequence sequence)
        {
            var loopStarts = sequence.Commands.Count(c => c.Type == CommandType.LoopStart);
            if (loopStarts > 0)
            {
                return string.Format(", {0} loops", loopStarts);
            }
            return "";
        }

        private void SaveSequenceToFile(string filePath)
        {
            try
            {
                var sequence = new CommandSequence
                {
                    Name = Path.GetFileNameWithoutExtension(filePath),
                    Commands = _commands.ToList(),
                    TargetApplication = GetProcessNameFromWindow(_targetWindowHandle),
                    TargetProcessName = GetProcessNameFromWindow(_targetWindowHandle),
                    TargetWindowTitle = GetWindowTitle(_targetWindowHandle),
                    Created = DateTime.Now,
                    LastModified = DateTime.Now
                };

                var json = JsonConvert.SerializeObject(sequence, Formatting.Indented);
                File.WriteAllText(filePath, json);

                _currentFilePath = filePath;
                _hasUnsavedChanges = false;
                UpdateUI();
                UpdateStatus(string.Format("Sequence saved: {0}", Path.GetFileName(filePath)));
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error saving sequence", ex);
            }
        }

        #endregion

        #region UI Update

        private void UpdateStatus(string message)
        {
            try
            {
                AppCommander_AppCommander_TxtStatusBar.Text = string.Format("{0:HH:mm:ss} - {1}", DateTime.Now, message);
                Debug.WriteLine(string.Format("Status: {0}", message));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error updating status: {0}", ex.Message));
            }
        }

        private void UpdateRecordingStatus(bool isRecording, bool isPaused)
        {
            try
            {
                if (isRecording)
                {
                    AppCommander_LblStatusBarRecording.Text = isPaused ? "Recording Paused" : "Recording";
                }
                else
                {
                    AppCommander_LblStatusBarRecording.Text = "Not Recording";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error updating recording status: {0}", ex.Message));
            }
        }

        #endregion

        #region Helper Methods 

        private string GenerateAutoDetectedDataReport()
        {
            var report = new System.Text.StringBuilder();

            report.AppendLine("AUTO-DETECTED DATA REPORT");
            report.AppendLine(string.Format("Generated: {0:yyyy-MM-dd HH:mm:ss}", DateTime.Now));
            report.AppendLine(string.Format("Target: {0}", GetWindowTitle(_targetWindowHandle)));
            report.AppendLine(string.Format("Commands: {0}", _commands.Count));
            report.AppendLine("");

            var commandGroups = _commands.GroupBy(c => c.Type).ToList();

            report.AppendLine("COMMAND SUMMARY:");
            foreach (var group in commandGroups)
            {
                report.AppendLine(string.Format("  {0}: {1} commands", group.Key, group.Count()));
            }
            report.AppendLine("");

            report.AppendLine("DETAILED COMMANDS:");
            for (int i = 0; i < _commands.Count; i++)
            {
                var cmd = _commands[i];
                report.AppendLine(string.Format("{0:D3}. {1} - {2}", i + 1, cmd.Type, cmd.ElementName));

                if (!string.IsNullOrEmpty(cmd.ElementId))
                    report.AppendLine(string.Format("     ID: {0}", cmd.ElementId));

                if (cmd.ElementX > 0 && cmd.ElementY > 0)
                    report.AppendLine(string.Format("     Position: ({0}, {1})", cmd.ElementX, cmd.ElementY));

                if (!string.IsNullOrEmpty(cmd.Value))
                    report.AppendLine(string.Format("     Value: {0}", cmd.Value));

                report.AppendLine("");
            }

            return report.ToString();
        }

        private void ShowErrorMessage(string title, Exception ex)
        {
            var message = string.Format("{0}\n\nError: {1}", title, ex.Message);
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Debug.WriteLine(string.Format("{0}: {1}", title, ex.Message));
        }

        private string GetProcessNameFromWindow(IntPtr windowHandle)
        {
            try
            {
                if (windowHandle == IntPtr.Zero) return "Unknown";

                GetWindowThreadProcessId(windowHandle, out uint processId);
                using (var process = Process.GetProcessById((int)processId))
                {
                    return process.ProcessName;
                }
            }
            catch
            {
                return "Unknown";
            }
        }

        private string GetWindowTitle(IntPtr windowHandle)
        {
            try
            {
                if (windowHandle == IntPtr.Zero) return "Unknown";

                var sb = new System.Text.StringBuilder(256);
                GetWindowText(windowHandle, sb, sb.Capacity);
                return sb.ToString();
            }
            catch
            {
                return "Unknown";
            }
        }

        private string ShowInputDialog(string prompt, string title, string defaultValue = "")
        {
            var dialog = new Window
            {
                Title = title,
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var stackPanel = new StackPanel { Margin = new Thickness(10) };
            var label = new TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 10) };
            var textBox = new TextBox { Text = defaultValue };

            stackPanel.Children.Add(label);
            stackPanel.Children.Add(textBox);
            grid.Children.Add(stackPanel);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10)
            };

            var okButton = new Button { Content = "OK", Width = 75, Margin = new Thickness(0, 0, 10, 0) };
            var cancelButton = new Button { Content = "Cancel", Width = 75 };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            Grid.SetRow(buttonPanel, 1);
            grid.Children.Add(buttonPanel);

            dialog.Content = grid;

            string result = null;
            okButton.Click += (s, e) => { result = textBox.Text; dialog.DialogResult = true; };
            cancelButton.Click += (s, e) => { dialog.DialogResult = false; };
            textBox.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) { result = textBox.Text; dialog.DialogResult = true; } };

            textBox.Focus();
            textBox.SelectAll();

            //return dialog.ShowDialog() == true ? result : null;
            return dialog.ShowDialog() == true ? result : defaultValue;
        }

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

        #endregion
    
        // Trieda pre SequenceSet item v DataGrid
        #region Sequence Set Methods

        /// <summary>
        /// Upraví vybranú sekvenciu v sete
        /// </summary>
        private void EditSequence_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = AppCommander_MainCommandTable.SelectedItem as SequenceSetItem;
                if (selectedItem == null)
                {
                    MessageBox.Show("Please select a sequence to edit.",
                                    "No Selection",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                    return;
                }

                // Jednoduchý edit dialog pre RepeatCount
                var input = Microsoft.VisualBasic.Interaction.InputBox(
                    $"Enter repeat count for sequence '{selectedItem.SequenceName}':",
                    "Edit Sequence",
                    selectedItem.RepeatCount.ToString());

                if (!string.IsNullOrEmpty(input))
                {
                    if (int.TryParse(input, out int repeatCount) && repeatCount > 0)
                    {
                        selectedItem.RepeatCount = repeatCount;
                        selectedItem.Status = "Modified";
                        _hasUnsavedSequenceSetChanges = true;

                        UpdateSequenceSetUI();
                        UpdateStatus($"Sequence '{selectedItem.SequenceName}' repeat count updated to {repeatCount}");
                    }
                    else
                    {
                        MessageBox.Show("Please enter a valid positive number.",
                                        "Invalid Input",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error editing sequence", ex);
            }
        }

        /// <summary>
        /// Odstráni vybranú sekvenciu zo setu
        /// </summary>
        private void RemoveSequence_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = AppCommander_MainCommandTable.SelectedItem as SequenceSetItem;
                if (selectedItem == null)
                {
                    MessageBox.Show("Please select a sequence to remove.",
                                    "No Selection",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show(
                    $"Are you sure you want to remove sequence '{selectedItem.SequenceName}' from the set?\n\n" +
                    "Note: This will only remove it from the current set, the original file will not be deleted.",
                    "Confirm Remove",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var sequenceName = selectedItem.SequenceName;
                    _sequenceSetItems.Remove(selectedItem);

                    // Aktualizuj step numbers
                    for (int i = 0; i < _sequenceSetItems.Count; i++)
                    {
                        _sequenceSetItems[i].StepNumber = i + 1;
                    }

                    _hasUnsavedSequenceSetChanges = true;
                    UpdateSequenceSetUI();
                    UpdateStatus($"Sequence '{sequenceName}' removed from set");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error removing sequence", ex);
            }
        }

        /// <summary>
        /// Uloží set sekvencií - rýchle uloženie
        /// </summary>
        private void BtnSaveSetAs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(_currentSequenceSetFilePath))
                {
                    SaveSetAs_Click(sender, e);
                    return;
                }

                SaveSetToFile(_currentSequenceSetFilePath);
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error saving sequence set", ex);
            }
        }

        /// <summary>
        /// Uloží set sekvencií do súboru
        /// </summary>
        private void SaveSetToFile(string filePath)
        {
            try
            {
                if (_sequenceSetItems.Count == 0)
                {
                    MessageBox.Show("Cannot save empty sequence set. Please add at least one sequence.",
                                    "Empty Set",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
                    return;
                }

                // Vytvorenie SequenceSet objektu
                var sequenceSet = new SequenceSet
                {
                    Name = (AppCommander_TxtSequenceName_Copy is TextBox textBox && !string.IsNullOrEmpty(textBox.Text)) ?
                            textBox.Text :
                            Path.GetFileNameWithoutExtension(filePath),

                    Description = $"Sequence set created on {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    Sequences = _sequenceSetItems.ToList(),
                    Created = _currentSequenceSet?.Created ?? DateTime.Now,
                    LastModified = DateTime.Now,
                    FilePath = filePath
                };

                // Serializácia do JSON
                var json = JsonConvert.SerializeObject(sequenceSet, Formatting.Indented);
                File.WriteAllText(filePath, json);

                // Aktualizácia stavu
                _currentSequenceSet = sequenceSet;
                _currentSequenceSetFilePath = filePath;
                _hasUnsavedSequenceSetChanges = false;

                UpdateSequenceSetUI();
                UpdateStatus($"Sequence set saved: {Path.GetFileName(filePath)} ({_sequenceSetItems.Count} sequences)");

                Debug.WriteLine($"Sequence set saved to: {filePath}");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error saving sequence set to file", ex);
            }
        }

        /// <summary>
        /// Uloží set sekvencií ako nový súbor
        /// </summary>
        private void SaveSetAs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_sequenceSetItems.Count == 0)
                {
                    MessageBox.Show("Cannot save empty sequence set. Please add at least one sequence.",
                                    "Empty Set",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
                    return;
                }

                var dialog = new SaveFileDialog
                {
                    Filter = "Sequence Set Files (*.acset)|*.acset|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                    DefaultExt = ".acset",
                    Title = "Save Sequence Set As",
                    //FileName = !string.IsNullOrEmpty(AppCommander_TxtSequenceName_Copy?.Text) ?
                    //            AppCommander_TxtSequenceName_Copy.Text :
                    //            "SequenceSet_" + DateTime.Now.ToString("yyyyMMdd_HHmmss")

                    FileName = (AppCommander_TxtSequenceName_Copy is TextBox textBox && !string.IsNullOrEmpty(textBox.Text)) ?
                                textBox.Text :
                                "SequenceSet_" + DateTime.Now.ToString("yyyyMMdd_HHmmss")
                };

                if (dialog.ShowDialog() == true)
                {
                    SaveSetToFile(dialog.FileName);
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error saving sequence set as", ex);
            }
        }

        #endregion

        #region Unsaved Commands Warning Management

        /// <summary>
        /// Aktualizuje warning položku v unified tabuľke
        /// </summary>
        private void UpdateUnsavedCommandsWarning()
        {
            try
            {
                // Skontrolovať či sú nahrané príkazy, ktoré nie sú v unified tabuľke
                bool hasUnsavedCommands = _commands != null && _commands.Count > 0;

                // Nájsť existujúcu warning položku
                var existingWarning = _unifiedItems?.FirstOrDefault(
                    item => item.Type == UnifiedItem.ItemType.LiveRecording &&
                            item.Name == "⚠️ Unsaved Command Set");

                if (hasUnsavedCommands)
                {
                    // Ak nie je warning položka, vytvor ju
                    if (existingWarning == null && _unifiedItems != null)
                    {
                        var warningItem = new UnifiedItem(UnifiedItem.ItemType.LiveRecording)
                        {
                            StepNumber = 1,
                            Name = "⚠️ Unsaved Command Set",
                            Action = "Click to edit or add to sequence",
                            Value = $"{_commands.Count} command(s) recorded",
                            RepeatCount = 1,
                            Status = "⚠️ Warning",
                            Timestamp = DateTime.Now,
                            IsLiveRecording = true,
                            LiveSequenceReference = _recorder?.GetCurrentSequence
                        };

                        // Pridať na začiatok tabuľky
                        _unifiedItems.Insert(0, warningItem);
                        RecalculateStepNumbers();

                        Debug.WriteLine($"Warning item added: {_commands.Count} unsaved commands");
                    }
                    else if (existingWarning != null)
                    {
                        // Aktualizovať existujúcu warning položku
                        existingWarning.Value = $"{_commands.Count} command(s) recorded";
                        existingWarning.Timestamp = DateTime.Now;
                        AppCommander_MainCommandTable?.Items.Refresh();
                    }
                }
                else
                {
                    // Ak nie sú unsaved príkazy, odstrániť warning položku
                    if (existingWarning != null && _unifiedItems != null)
                    {
                        _unifiedItems.Remove(existingWarning);
                        RecalculateStepNumbers();
                        AppCommander_MainCommandTable?.Items.Refresh();

                        Debug.WriteLine("Warning item removed - no unsaved commands");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating unsaved commands warning: {ex.Message}");
            }
        }

        /// <summary>
        /// Handler pre kliknutie na warning položku
        /// </summary>
        private void HandleWarningItemClick(UnifiedItem warningItem)
        {
            try
            {
                if (warningItem == null || warningItem.Type != UnifiedItem.ItemType.LiveRecording)
                    return;

                // Zobraz dialog s možnosťami
                var result = MessageBox.Show(
                    $"You have {_commands.Count} recorded commands that are not yet added to the main sequence.\n\n" +
                    "What would you like to do?\n\n" +
                    "• YES - Open editor to review and edit commands\n" +
                    "• NO - Add commands to main sequence immediately\n" +
                    "• CANCEL - Do nothing",
                    "Unsaved Commands",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Otvorí Sequence Editor pre nahrané príkazy
                    OpenEditorForRecordedCommands();
                }
                else if (result == MessageBoxResult.No)
                {
                    // Pridá príkazy do AppCommander_MainCommandTable tabuľky
                    AddFromCommands_Click(null, null);
                }
                // Cancel - nič sa nestane
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error handling warning item click", ex);
            }
        }

        /// <summary>
        /// Otvorí editor pre nahrané príkazy
        /// </summary>
        private void OpenEditorForRecordedCommands()
        {
            try
            {
                if (_commands == null || _commands.Count == 0)
                {
                    MessageBox.Show(
                        "No recorded commands to edit.",
                        "No Commands",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                // Konvertuj príkazy na UnifiedItems
                var itemsToEdit = new List<UnifiedItem>();
                for (int i = 0; i < _commands.Count; i++)
                {
                    var unifiedItem = UnifiedItem.FromCommand(_commands[i], i + 1);
                    itemsToEdit.Add(unifiedItem);
                }

                // Otvor Sequence Editor
                var editorWindow = new SequenceEditorWindow(itemsToEdit, "Recorded Commands")
                {
                    Owner = this
                };

                bool? dialogResult = editorWindow.ShowDialog();

                if (dialogResult == true && editorWindow.WasSaved)
                {
                    // Užívateľ uložil zmeny - aktualizuj _commands
                    _commands.Clear();

                    foreach (var item in editorWindow.EditedItems)
                    {
                        if (item.Type != UnifiedItem.ItemType.SequenceReference)
                        {
                            var command = item.ToCommand();
                            command.StepNumber = _commands.Count + 1;
                            _commands.Add(command);
                        }
                    }

                    UpdateUI();
                    UpdateUnsavedCommandsWarning();
                    UpdateStatus($"Recorded commands updated - {_commands.Count} commands");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error opening editor for recorded commands", ex);
            }
        }

        #endregion

        #region Helper Methods for Sequence Set

        /// <summary>
        /// Validuje či je súbor validná sekvencia
        /// </summary>
        private bool ValidateSequenceFile(string filePath)
        {
            try
            {
                var content = File.ReadAllText(filePath);

                // Pokus o deserializáciu ako CommandSequence
                var sequence = JsonConvert.DeserializeObject<CommandSequence>(content);

                return sequence != null && sequence.Commands != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Aktualizuje UI pre sequence set
        /// </summary>
        private void UpdateSequenceSetUI()
        {
            try
            {
                AppCommander_TxtSequenceCount = this.FindName("AppCommander_TxtSequenceCount") as TextBlock;
                // Aktualizuj počet sekvencií
                if (AppCommander_TxtSequenceCount != null)
                {
                    AppCommander_TxtSequenceCount.Text = $"{_sequenceSetItems.Count} sequences";
                }

                // Aktualizuj title bar ak je potrebné
                var hasChanges = _hasUnsavedSequenceSetChanges ? "*" : "";
                var setName = !string.IsNullOrEmpty(_currentSequenceSetFilePath) ?
                                Path.GetFileNameWithoutExtension(_currentSequenceSetFilePath) :
                                "Untitled Set";

                // Môžete aktualizovať window title ak chcete
                // this.Title = $"AppCommander - {setName}{hasChanges}";

                // Refresh DataGrid
                if (AppCommander_MainCommandTable != null)
                {
                    AppCommander_MainCommandTable.Items.Refresh();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating sequence set UI: {ex.Message}");
            }
        }

        /// <summary>
        /// Načíta sequence set zo súboru
        /// </summary>
        private void LoadSequenceSetFromFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    MessageBox.Show($"File '{filePath}' does not exist.",
                                    "File Not Found",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                    return;
                }

                var json = File.ReadAllText(filePath);
                var sequenceSet = JsonConvert.DeserializeObject<SequenceSet>(json);

                if (sequenceSet != null && sequenceSet.Sequences != null)
                {
                    _sequenceSetItems.Clear();

                    foreach (var sequence in sequenceSet.Sequences)
                    {
                        // Validuj že súbor stále existuje
                        if (File.Exists(sequence.FilePath))
                        {
                            _sequenceSetItems.Add(sequence);
                        }
                        else
                        {
                            Debug.WriteLine($"Warning: Sequence file not found: {sequence.FilePath}");
                            // Pridaj aj tak, ale označ ako chýbajúci
                            sequence.Status = "File Missing";
                            _sequenceSetItems.Add(sequence);
                        }
                    }

                    _currentSequenceSet = sequenceSet;
                    _currentSequenceSetFilePath = filePath;
                    _hasUnsavedSequenceSetChanges = false;

                    UpdateSequenceSetUI();
                    UpdateStatus($"Sequence set loaded: {Path.GetFileName(filePath)} ({_sequenceSetItems.Count} sequences)");
                }
                else
                {
                    MessageBox.Show("Invalid sequence set file format.",
                                    "Invalid File",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error loading sequence set", ex);
            }
        }
        #endregion

        #region Theme Selection - Simplified

        // Pre RadioButton riešenie
        private void ThemeRadio_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                var radioButton = sender as RadioButton;
                if (radioButton?.Tag is string theme)
                {
                    SetAppTheme(theme);
                    UpdateStatus($"{theme} theme enabled");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error changing theme", ex);
            }
        }

        // Pre ComboBox riešenie
        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var comboBox = sender as ComboBox;
                var selectedItem = comboBox?.SelectedItem as ComboBoxItem;

                if (selectedItem?.Tag is string theme)
                {
                    SetAppTheme(theme);
                    UpdateStatus($"{selectedItem.Content} selected");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error changing theme", ex);
            }
        }

        // Upravená SetAppTheme bez potreby UpdateThemeCheckBoxes
        private void SetAppTheme(string theme)
        {
            try
            {
                ResourceDictionary themeDict = new ResourceDictionary();
                Debug.WriteLine("SetAppTheme");
                switch (theme)
                {
                    case "Light":
                        themeDict.Source = new Uri("Themes/LightTheme.xaml", UriKind.Relative);
                        break;
                    case "Dark":
                        themeDict.Source = new Uri("Themes/DarkTheme.xaml", UriKind.Relative);
                        break;
                    case "HighContrast":
                        themeDict.Source = new Uri("Themes/HighContrastTheme.xaml", UriKind.Relative);
                        break;
                    case "System":
                    default:
                        var isSystemDark = IsSystemInDarkMode();
                        themeDict.Source = isSystemDark ?
                            new Uri("Themes/DarkTheme.xaml", UriKind.Relative) :
                            new Uri("Themes/LightTheme.xaml", UriKind.Relative);
                        break;
                }

                // Odstráň existujúce theme dictionaries
                var existingDictionaries = Application.Current.Resources.MergedDictionaries
                    .Where(d => d.Source != null &&
                               (d.Source.OriginalString.Contains("LightTheme.xaml") ||
                                d.Source.OriginalString.Contains("DarkTheme.xaml") ||
                                d.Source.OriginalString.Contains("HighContrastTheme.xaml")))
                    .ToList();

                foreach (var dict in existingDictionaries)
                {
                    Application.Current.Resources.MergedDictionaries.Remove(dict);
                }

                // Pridaj nový theme dictionary
                Application.Current.Resources.MergedDictionaries.Add(themeDict);

                Debug.WriteLine($"Theme changed to: {theme}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying theme '{theme}': {ex.Message}");
                // Fallback handling...
            }
        }

        /// <summary>
        /// Zistí či je systém v dark mode pomocou Windows Registry
        /// </summary>
        /// <returns>True ak je dark mode aktívny, false ak light mode</returns>
        private bool IsSystemInDarkMode()
        {
            try
            {
                // Skús najprv registry metódu (najspoľahlivejšia)
                const string registryKeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
                const string appsValueName = "AppsUseLightTheme";
                const string systemValueName = "SystemUsesLightTheme";

                // Skontroluj apps theme preference
                var appsValue = Registry.GetValue(registryKeyPath, appsValueName, null);
                if (appsValue is int appsTheme)
                {
                    Debug.WriteLine($"Apps theme from registry: {(appsTheme == 0 ? "Dark" : "Light")}");
                    return appsTheme == 0; // 0 = dark, 1 = light
                }

                // Fallback na system theme
                var systemValue = Registry.GetValue(registryKeyPath, systemValueName, null);
                if (systemValue is int sysTheme)
                {
                    Debug.WriteLine($"System theme from registry: {(sysTheme == 0 ? "Dark" : "Light")}");
                    return sysTheme == 0;
                }

                // Ak registry values neexistujú, skús Win32 API
                return TryWin32DarkModeDetection();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Registry dark mode detection failed: {ex.Message}");
                return TryWin32DarkModeDetection();
            }
        }

        private bool TryWin32DarkModeDetection()
        {
            try
            {
                // Skús použiť UxTheme API
                bool shouldUseDark = ShouldAppsUseDarkMode();
                Debug.WriteLine($"Win32 API dark mode detection: {shouldUseDark}");
                return shouldUseDark;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Win32 API dark mode detection failed: {ex.Message}");

                // Posledný fallback - skús high contrast mode
                return DetectHighContrastMode();
            }
        }

        private bool DetectHighContrastMode()
        {
            try
            {
                const string hcKeyPath = @"HKEY_CURRENT_USER\Control Panel\Accessibility\HighContrast";
                const string flagsValueName = "Flags";

                var hcValue = Registry.GetValue(hcKeyPath, flagsValueName, null);
                if (hcValue is string hcFlags)
                {
                    // High contrast flag "1" znamená že je aktívny
                    bool isHighContrast = hcFlags.Contains("1");
                    if (isHighContrast)
                    {
                        Debug.WriteLine("High contrast mode detected, treating as dark theme");
                        return true; // High contrast často používa tmavé farby
                    }
                }

                Debug.WriteLine("No theme detection successful, defaulting to light mode");
                return false; // Default light mode
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"High contrast detection failed: {ex.Message}");
                return false; // Ultimate fallback - light mode
            }
        }

        private bool IsSystemInDarkModeExtended()
        {
            try
            {
                // 1. Skontroluj Windows verziu
                var osVersion = Environment.OSVersion.Version;
                if (osVersion.Major < 10)
                {
                    Debug.WriteLine("Windows version < 10, no dark mode support");
                    return false; // Windows 8.1 a staršie nemajú dark mode
                }

                // 2. Pre Windows 10 build 1809+ (17763+)
                if (osVersion.Major == 10 && osVersion.Build >= 17763)
                {
                    return IsSystemInDarkMode(); // Použij plnú detekciu
                }

                // 3. Pre staršie Windows 10 buildy
                if (osVersion.Major == 10)
                {
                    // Len registry-based detection
                    const string registryKeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
                    var appsValue = Registry.GetValue(registryKeyPath, "AppsUseLightTheme", null);

                    if (appsValue is int theme)
                    {
                        return theme == 0;
                    }
                }

                return false; // Default light mode
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Extended dark mode detection failed: {ex.Message}");
                return false;
            }
        }

        private void LogThemeDetectionInfo()
        {
            try
            {
                Debug.WriteLine("=== Theme Detection Debug Info ===");
                Debug.WriteLine($"OS Version: {Environment.OSVersion.VersionString}");
                Debug.WriteLine($"OS Build: {Environment.OSVersion.Version.Build}");

                const string regPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
                var appsValue = Registry.GetValue(regPath, "AppsUseLightTheme", "Not Found");
                var systemValue = Registry.GetValue(regPath, "SystemUsesLightTheme", "Not Found");

                Debug.WriteLine($"Registry AppsUseLightTheme: {appsValue}");
                Debug.WriteLine($"Registry SystemUsesLightTheme: {systemValue}");
                Debug.WriteLine($"Final Detection Result: {(IsSystemInDarkMode() ? "Dark" : "Light")}");
                Debug.WriteLine("=================================");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Theme detection logging failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Alternatívna metóda pomocou UxTheme.dll (Windows 10 build 17763+)
        /// </summary>
        /// <returns>True ak dark mode, false ak light mode</returns>
        private bool IsSystemInDarkModeAdvanced()
        {
            try
            {
                // Use the class-level ShouldSystemUseDarkMode() extern method
                return ShouldSystemUseDarkMode();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Advanced dark mode detection failed: {ex.Message}");
                // Fallback na registry metódu
                return IsSystemInDarkMode();
            }
        }

        /// <summary>
        /// Komplexná detekcia system theme s fallback možnosťami
        /// </summary>
        /// <returns>True ak dark mode, false ak light mode</returns>
        private bool DetectSystemTheme()
        {
            try
            {
                // Metóda 1: Registry check (najpouživanejšia)
                const string registryKeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

                // Skontroluj apps theme
                var appsValue = Registry.GetValue(registryKeyPath, "AppsUseLightTheme", null);

                // Skontroluj system theme  
                var systemValue = Registry.GetValue(registryKeyPath, "SystemUsesLightTheme", null);

                // Preferuj apps theme, ak existuje
                if (appsValue is int appsTheme)
                {
                    Debug.WriteLine($"Apps theme detected: {(appsTheme == 0 ? "Dark" : "Light")}");
                    return appsTheme == 0; // 0 = dark, 1 = light
                }

                // Fallback na system theme
                if (systemValue is int sysTheme)
                {
                    Debug.WriteLine($"System theme detected: {(sysTheme == 0 ? "Dark" : "Light")}");
                    return sysTheme == 0;
                }

                // Ak nič neexistuje, skús high contrast mode
                var highContrast = Registry.GetValue(
                    @"HKEY_CURRENT_USER\Control Panel\Accessibility\HighContrast",
                    "Flags",
                    null);

                if (highContrast is string hcFlags && hcFlags.Contains("1"))
                {
                    Debug.WriteLine("High contrast mode detected, treating as dark");
                    return true; // High contrast často používa tmavé farby
                }

                Debug.WriteLine("No theme registry values found, defaulting to light mode");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in DetectSystemTheme: {ex.Message}");
                return false; // Default light mode
            }
        }

        // Pre minimálne theme menu (tretia verzia) handler:

        private void ThemeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var menuItem = sender as MenuItem;
                if (menuItem?.Tag is string theme)
                {
                    // Unchecked všetky theme menu items
                    var parentMenu = menuItem.Parent as MenuItem;
                    if (parentMenu != null)
                    {
                        foreach (var item in parentMenu.Items)
                        {
                            // Kontrola či je item MenuItem (nie Separator)
                            if (item is MenuItem mi && mi.Tag is string && mi.IsCheckable)
                            {
                                mi.IsChecked = false;
                            }
                        }
                    }

                    // Check current theme
                    menuItem.IsChecked = true;

                    // Apply theme
                    SetAppTheme(theme);
                    UpdateStatus($"{menuItem.Header} selected");
                    DebugCurrentTheme(); 
                }
            }
            catch (Exception ex)
            {
                DebugCurrentTheme();
                ShowErrorMessage("Error changing theme", ex);
            }
        }

        public static void DebugCurrentTheme()
        {
            try
            {
                //foreach (var dict in Application.Current.Resources.MergedDictionaries)
                var panelBrush = Application.Current.Resources["PanelBackgroundBrush"] as SolidColorBrush;
                if (panelBrush != null)
                {
                    Debug.WriteLine("=== Current Theme Debug Info ===");
                    Debug.WriteLine(string.Format("Current PanelBackgroundBrush color: {0}", panelBrush.Color));

                    //Debug.WriteLine($"System in dark mode: {IsSystemInDarkMode()}");
                    Debug.WriteLine("===============================");
                }
                else
                {
                    Debug.WriteLine("PanelBackgroundBrush not found!");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Debug theme error: {0}", ex.Message));
            }
        }
        #endregion

        #region Context Menu Handlers

        private void ContextMenu_OpenSequenceEditor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_unifiedItems == null || _unifiedItems.Count == 0)
                {
                    MessageBox.Show(
                        "No commands to edit. Please record or load some commands first.",
                        "No Commands",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                string sequenceName = !string.IsNullOrEmpty(_currentSequenceName)
                    ? _currentSequenceName
                    : "Current Sequence";

                var editorWindow = new SequenceEditorWindow(
                    _unifiedItems.ToList(),
                    sequenceName)
                {
                    Owner = this
                };

                bool? result = editorWindow.ShowDialog();

                if (result == true && editorWindow.WasSaved)
                {
                    _unifiedItems.Clear();

                    foreach (var item in editorWindow.EditedItems)
                    {
                        _unifiedItems.Add(item);
                    }

                    _hasUnsavedUnifiedChanges = true;
                    UpdateUI();
                    UpdateStatus($"Sequence updated - {_unifiedItems.Count} commands");

                    if (AppCommander_MainCommandTable != null)
                    {
                        AppCommander_MainCommandTable.Items.Refresh();
                    }
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error opening sequence editor", ex);
            }
        }

        /// <summary>
        /// Context menu - Edit Command
        /// </summary>
        private void ContextMenu_Edit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = AppCommander_MainCommandTable.SelectedItem as UnifiedItem;

                // **Špecializovaná obsluha pre warning položku**
                if (selectedItem?.Type == UnifiedItem.ItemType.LiveRecording &&
                    selectedItem.Name == "⚠️ Unsaved Command Set")
                {
                    HandleWarningItemClick(selectedItem);
                    return;
                }

                OpenSmartEditor(selectedItem);
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error opening editor", ex);
            }
        }

        private void RefreshWarnings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateUnsavedCommandsWarning();
                UpdateStatus("Warnings refreshed");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error refreshing warnings", ex);
            }
        }

        private void ContextMenu_Delete_Click(object sender, RoutedEventArgs e)
        {
            // Použite existujúci DeleteCommand_Click alebo vytvorte nový
            DeleteCommand_Click(sender, e);
        }

        private void ContextMenu_Copy_Click(object sender, RoutedEventArgs e)
        {
            if (AppCommander_MainCommandTable.SelectedItem is UnifiedItem selectedItem)
            {
                // Skopírovať detaily do schránky
                var details = $"Krok: {selectedItem.StepNumber}\n" +
                             $"Typ: {selectedItem.TypeDisplay}\n" +
                             $"Názov: {selectedItem.Name}\n" +
                             $"Akcia: {selectedItem.Action}\n" +
                             $"Hodnota: {selectedItem.Value}";

                Clipboard.SetText(details);
                UpdateStatus("Command details copied to clipboard");
            }
        }

        private void ContextMenu_Duplicate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (AppCommander_MainCommandTable.SelectedItem is UnifiedItem selectedItem)
                {
                    // Vytvoriť kópiu
                    var duplicate = new UnifiedItem
                    {
                        StepNumber = _unifiedItems.Count + 1,
                        Type = selectedItem.Type,
                        Name = selectedItem.Name + " (kópia)",
                        Action = selectedItem.Action,
                        Value = selectedItem.Value,
                        RepeatCount = selectedItem.RepeatCount,
                        Status = "Ready",
                        Timestamp = DateTime.Now,
                        FilePath = selectedItem.FilePath,
                        ElementX = selectedItem.ElementX,
                        ElementY = selectedItem.ElementY,
                        ElementId = selectedItem.ElementId,
                        ClassName = selectedItem.ClassName
                    };

                    _unifiedItems.Add(duplicate);
                    _hasUnsavedUnifiedChanges = true;
                    RecalculateStepNumbers();
                    UpdateStatus($"Command duplicated: {duplicate.Name}");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error duplicating command", ex);
            }
        }
        #endregion

        #region Advanced Execution Features

        #endregion

        #region Command Duplication and Templates

        /// <summary>
        /// Prenumeruje všetky príkazy
        /// </summary>
        private void RenumberCommands()
        {
            for (int i = 0; i < _commands.Count; i++)
            {
                _commands[i].StepNumber = i + 1;
            }
        }

        #endregion

        #region Keyboard Shortcuts Support

        /// <summary>
        /// Spracovanie klávesových skratiek v okne
        /// </summary>
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                // Ctrl + E = Edit selected command
                if (e.Key == Key.E && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    if (AppCommander_MainCommandTable.SelectedItem != null)
                    {
                        EditCommand_Click(sender, e);
                        e.Handled = true;
                    }
                }
                // Ctrl + D = Duplicate command
                else if (e.Key == Key.D && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    if (AppCommander_MainCommandTable.SelectedItem != null)
                    {
                        DuplicateCommand_Click(sender, e);
                        e.Handled = true;
                    }
                }
                // F5 = Execute sequence
                else if (e.Key == Key.F5)
                {
                    ExecuteModifiedSequence_Click(sender, e);
                    e.Handled = true;
                }
                // Ctrl + T = Test command
                else if (e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    if (AppCommander_MainCommandTable.SelectedItem != null)
                    {
                        TestCommand_Click(sender, e);
                        e.Handled = true;
                    }
                }
                // Delete = Delete command
                else if (e.Key == Key.Delete)
                {
                    if (AppCommander_MainCommandTable.SelectedItem != null)
                    {
                        DeleteCommand_Click(sender, e);
                        e.Handled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling keyboard shortcut: {ex.Message}");
            }
        }

        #endregion

        #region Execute Modified Sequence 

        /// <summary>
        /// Spustí aktuálnu (upravenú) sekvenciu príkazov
        /// </summary>
        private async void ExecuteModifiedSequence_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Kontrola, či existujú príkazy
                if (_unifiedItems == null || _unifiedItems.Count == 0)
                {
                    MessageBox.Show(
                        "No commands to execute. Please load or record a sequence first.",
                        "No Commands",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Upozornenie na neuložené zmeny
                if (_hasUnsavedUnifiedChanges)
                {
                    var result = MessageBox.Show(
                        "You have unsaved changes in your sequence.\n\n" +
                        "Do you want to continue executing without saving?\n\n" +
                        "Click 'Yes' to execute anyway\n" +
                        "Click 'No' to save first\n" +
                        "Click 'Cancel' to abort",
                        "Unsaved Changes",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Cancel)
                    {
                        return;
                    }
                    else if (result == MessageBoxResult.No)
                    {
                        // Pokus o uloženie
                        SaveSequence_Click(sender, e);
                        if (_hasUnsavedUnifiedChanges) // Ak sa neuložilo (cancel), neexekuovať
                        {
                            return;
                        }
                    }
                }

                // Potvrdenie spustenia
                var confirmResult = MessageBox.Show(
                    string.Format("Execute sequence with {0} commands?\n\n" +
                    "The automation will start in 3 seconds.\n" +
                    "Press ESC to stop execution at any time.", _unifiedItems.Count),
                    "Execute Sequence",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Question);

                if (confirmResult != MessageBoxResult.OK)
                {
                    return;
                }

                // Deaktivácia UI počas exekúcie
                SetUIEnabled(false);
                UpdateStatus("Preparing to execute sequence...");

                // 3 sekundové odpočítavanie
                for (int i = 3; i > 0; i--)
                {
                    UpdateStatus(string.Format("Starting in {0}...", i));
                    await Task.Delay(1000);
                }

                UpdateStatus("Executing sequence...");

                // Vytvorenie CommandSequence z unified items
                var sequence = _currentUnifiedSequence.ToCommandSequence();
                sequence.Name = _currentSequenceName ?? "Modified Sequence";

                // Spustenie sekvencie
                await ExecuteSequenceAsync(sequence);

                UpdateStatus("Sequence execution completed");

                MessageBox.Show(
                    "Sequence execution completed successfully!",
                    "Execution Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error executing sequence", ex);
            }
            finally
            {
                SetUIEnabled(true);
            }
        }

        /// <summary>
        /// Asynchronné vykonanie sekvencie príkazov
        /// </summary>
        private async Task ExecuteSequenceAsync(CommandSequence sequence)
        {
            try
            {
                if (_player == null)
                {
                    _player = new CommandPlayer();
                }

                // Získanie rýchlosti z ExecutionSpeedControl (ak existuje)
                double speedMultiplier = 1.0;

                // Pokus o nájdenie ExecutionSpeedCtrl - podmienečne
                var speedControl = this.FindName("ExecutionSpeedCtrl");
                if (speedControl != null)
                {
                    // Ak existuje a má property SpeedMultiplier
                    var speedProperty = speedControl.GetType().GetProperty("SpeedMultiplier");
                    if (speedProperty != null)
                    {
                        var speedValue = speedProperty.GetValue(speedControl);
                        if (speedValue != null && speedValue is double)
                        {
                            speedMultiplier = (double)speedValue;
                        }
                    }
                }

                // Spustenie sekvencie
                await Task.Run(() =>
                {
                    _player.PlaySequence(sequence, 1);
                });
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Sequence execution failed: {0}", ex.Message), ex);
            }
        }

        /// <summary>
        /// Nastaví stav UI elementov (enable/disable)
        /// </summary>
        private void SetUIEnabled(bool enabled)
        {
            try
            {
                // Recording buttons 
                if (AppCommander_BtnRecording != null)
                    AppCommander_BtnRecording.IsEnabled = enabled || !enabled;

                // Ak existuje ButtonStartRecording
                var btnStartRec = this.FindName("ButtonStartRecording") as Button;
                if (btnStartRec != null)
                    btnStartRec.IsEnabled = enabled;

                // Ak existuje ButtonStopRecording  
                var AppCommander_BtnStopRec = this.FindName("ButtonStopRecording") as Button;
                if (AppCommander_BtnStopRec != null)
                    AppCommander_BtnStopRec.IsEnabled = !enabled;

                // Save/Load buttons - rôzne možné názvy
                var btnSaveSeq = this.FindName("BtnSaveSequence") as Button;
                if (btnSaveSeq != null)
                    btnSaveSeq.IsEnabled = enabled;

                var btnLoadSeq = this.FindName("BtnLoadSequence") as Button;
                if (btnLoadSeq != null)
                    btnLoadSeq.IsEnabled = enabled;

                // Playback buttons
                if (AppCommander_BtnPlayCommands != null)
                    AppCommander_BtnPlayCommands.IsEnabled = enabled;

                if (AppCommander_BtnPause != null)
                    AppCommander_BtnPause.IsEnabled = !enabled;

                if (AppCommander_BtnStop != null)
                    AppCommander_BtnStop.IsEnabled = !enabled;

                // Command table
                if (AppCommander_MainCommandTable != null)
                    AppCommander_MainCommandTable.IsEnabled = enabled;

                // Selection buttons
                if (AppCommander_AppCommander_BtnSelectTargetByClick != null)
                    AppCommander_AppCommander_BtnSelectTargetByClick.IsEnabled = enabled;

                if (AppCommander_BtnSelectTarget != null)
                    AppCommander_BtnSelectTarget.IsEnabled = enabled;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("Error setting UI state: {0}", ex.Message));
            }
        }

        #endregion

        #region Advanced Execution Features 

        /// <summary>
        /// Spustí sekvenciu od vybraného príkazu
        /// </summary>
        private async void ExecuteFromSelected_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = AppCommander_MainCommandTable.SelectedItem as UnifiedItem;
                if (selectedItem == null)
                {
                    MessageBox.Show(
                        "Please select a command to start execution from.",
                        "No Selection",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                var startIndex = AppCommander_MainCommandTable.SelectedIndex;

                if (startIndex < 0 || startIndex >= _unifiedItems.Count)
                {
                    MessageBox.Show(
                        "Invalid command selection.",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                var confirmResult = MessageBox.Show(
                    string.Format("Execute sequence starting from command #{0}?\n\n" +
                    "Command: {1}\n" +
                    "Remaining commands: {2}\n\n" +
                    "Execution will start in 3 seconds.\n" +
                    "Press ESC to stop at any time.",
                    startIndex + 1, selectedItem.Name, _unifiedItems.Count - startIndex),
                    "Execute From Selected",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Question);

                if (confirmResult != MessageBoxResult.OK)
                {
                    return;
                }

                // Deaktivácia UI
                SetUIEnabled(false);

                // Odpočítavanie
                for (int i = 3; i > 0; i--)
                {
                    UpdateStatus(string.Format("Starting in {0}...", i));
                    await Task.Delay(1000);
                }

                UpdateStatus(string.Format("Executing from command #{0}...", startIndex + 1));

                // Vytvorenie čiastkovej sekvencie
                var partialItems = new List<UnifiedItem>();
                for (int i = startIndex; i < _unifiedItems.Count; i++)
                {
                    partialItems.Add(_unifiedItems[i]);
                }

                var partialSequence = new UnifiedSequence
                {
                    Name = string.Format("{0} (from #{1})",
                        _currentSequenceName ?? "Sequence", startIndex + 1),
                    Items = partialItems,
                    Created = DateTime.Now,
                    LastModified = DateTime.Now
                };

                // Konverzia na CommandSequence
                var commandSequence = partialSequence.ToCommandSequence();

                // Spustenie
                await ExecuteSequenceAsync(commandSequence);

                UpdateStatus("Partial sequence execution completed");

                MessageBox.Show(
                    string.Format("Executed {0} commands successfully!", partialItems.Count),
                    "Execution Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error executing from selected command", ex);
            }
            finally
            {
                SetUIEnabled(true);
            }
        }

        /// <summary>
        /// Spustí iba vybraný príkaz (single step execution)
        /// </summary>
        private async void ExecuteSingleCommand_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = AppCommander_MainCommandTable.SelectedItem as UnifiedItem;
                if (selectedItem == null)
                {
                    MessageBox.Show(
                        "Please select a command to execute.",
                        "No Selection",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                var confirmResult = MessageBox.Show(
                    string.Format("Execute single command?\n\n" +
                    "Command: {0}\n" +
                    "Action: {1}\n" +
                    "Value: {2}",
                    selectedItem.Name, selectedItem.Action, selectedItem.Value),
                    "Execute Single Command",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Question);

                if (confirmResult != MessageBoxResult.OK)
                {
                    return;
                }

                SetUIEnabled(false);
                UpdateStatus(string.Format("Executing command: {0}...", selectedItem.Name));

                // Vytvorenie sekvencie s jediným príkazom
                var singleCommandSequence = new CommandSequence
                {
                    Name = "Single Command",
                    Commands = new List<Command> { selectedItem.ToCommand() },
                    Created = DateTime.Now,
                    LastModified = DateTime.Now
                };

                await ExecuteSequenceAsync(singleCommandSequence);

                UpdateStatus("Single command executed");

                MessageBox.Show(
                    "Command executed successfully!",
                    "Execution Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error executing single command", ex);
            }
            finally
            {
                SetUIEnabled(true);
            }
        }

        /// <summary>
        /// Testuje príkaz bez jeho spustenia (dry run)
        /// </summary>
        private void TestCommand_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = AppCommander_MainCommandTable.SelectedItem as UnifiedItem;
                if (selectedItem == null)
                {
                    MessageBox.Show(
                        "Please select a command to test.",
                        "No Selection",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                // Validácia príkazu
                string errorMessage;
                if (!selectedItem.IsValid(out errorMessage))
                {
                    MessageBox.Show(
                        string.Format("Command validation failed:\n\n{0}", errorMessage),
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                // Zobrazenie detailov príkazu
                var details = new System.Text.StringBuilder();
                details.AppendLine("✅ Command is valid");
                details.AppendLine();
                details.AppendLine(string.Format("Step: {0}", selectedItem.StepNumber));
                details.AppendLine(string.Format("Type: {0}", selectedItem.TypeDisplay));
                details.AppendLine(string.Format("Name: {0}", selectedItem.Name));
                details.AppendLine(string.Format("Action: {0}", selectedItem.Action));
                details.AppendLine(string.Format("Value: {0}", selectedItem.Value));
                details.AppendLine(string.Format("Repeat: {0}x", selectedItem.RepeatCount));

                if (selectedItem.ElementX.HasValue && selectedItem.ElementY.HasValue)
                {
                    details.AppendLine(string.Format("Position: ({0}, {1})",
                        selectedItem.ElementX, selectedItem.ElementY));
                }

                // Dodatočné info podľa typu
                switch (selectedItem.Type)
                {
                    case UnifiedItem.ItemType.LoopStart:
                        details.AppendLine();
                        details.AppendLine(string.Format("⚠️ This will repeat next commands {0} times",
                            selectedItem.RepeatCount));
                        break;
                    case UnifiedItem.ItemType.Wait:
                        details.AppendLine();
                        details.AppendLine(string.Format("⏱ This will pause execution for {0}",
                            selectedItem.Value));
                        break;
                    case UnifiedItem.ItemType.Command:
                        if (selectedItem.ElementX.HasValue && selectedItem.ElementY.HasValue)
                        {
                            var screen = System.Windows.SystemParameters.PrimaryScreenWidth;
                            var screenHeight = System.Windows.SystemParameters.PrimaryScreenHeight;

                            if (selectedItem.ElementX > screen || selectedItem.ElementY > screenHeight)
                            {
                                details.AppendLine();
                                details.AppendLine("⚠️ WARNING: Coordinates are outside screen bounds!");
                                details.AppendLine(string.Format("   Screen: {0}x{1}", screen, screenHeight));
                            }
                        }
                        break;
                }

                MessageBox.Show(
                    details.ToString(),
                    "Command Test Results",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error testing command", ex);
            }
        }

        #endregion

        #region Command Duplication 

        /// <summary>
        /// Duplikuje vybraný príkaz
        /// </summary>
        private void DuplicateCommand_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = AppCommander_MainCommandTable.SelectedItem as UnifiedItem;
                if (selectedItem == null)
                {
                    MessageBox.Show(
                        "Please select a command to duplicate.",
                        "No Selection",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                var duplicatedItem = new UnifiedItem(selectedItem.Type)
                {
                    StepNumber = _unifiedItems.Count + 1,
                    Name = selectedItem.Name + " (Copy)",
                    Action = selectedItem.Action,
                    Value = selectedItem.Value,
                    RepeatCount = selectedItem.RepeatCount,
                    Status = "Duplicated",
                    Timestamp = DateTime.Now,
                    FilePath = selectedItem.FilePath,
                    ElementX = selectedItem.ElementX,
                    ElementY = selectedItem.ElementY,
                    ElementId = selectedItem.ElementId,
                    ClassName = selectedItem.ClassName
                };

                // Vložiť hneď za vybraný príkaz
                var insertIndex = AppCommander_MainCommandTable.SelectedIndex + 1;

                // Pridať do unified items
                if (insertIndex < _unifiedItems.Count)
                {
                    _unifiedItems.Insert(insertIndex, duplicatedItem);
                }
                else
                {
                    _unifiedItems.Add(duplicatedItem);
                }

                // Prenumerovať položky
                RecalculateStepNumbers();

                _hasUnsavedUnifiedChanges = true;
                UpdateUI();

                // Vybrať nový príkaz
                AppCommander_MainCommandTable.SelectedIndex = insertIndex;

                UpdateStatus(string.Format("Command duplicated: {0}", selectedItem.Name));

                MessageBox.Show(
                    string.Format("Command '{0}' was duplicated successfully.", selectedItem.Name),
                    "Command Duplicated",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error duplicating command", ex);
            }
        }

        /// <summary>
        /// Vytvorí šablónu z vybraného príkazu
        /// </summary>
        private void CreateTemplate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = AppCommander_MainCommandTable.SelectedItem as UnifiedItem;
                if (selectedItem == null)
                {
                    MessageBox.Show(
                        "Please select a command to create template from.",
                        "No Selection",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                string templateName = ShowInputDialog(
                    "Enter template name:",
                    "Create Command Template",
                    string.Format("{0}_Template", selectedItem.Name));

                if (string.IsNullOrWhiteSpace(templateName))
                {
                    return;
                }

                // Vytvorenie šablóny (uloženie do súboru)
                var templatePath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "AppCommander",
                    "Templates",
                    string.Format("{0}.json", templateName));

                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(templatePath));

                var templateCommand = selectedItem.ToCommand();
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(templateCommand,
                    Newtonsoft.Json.Formatting.Indented);
                System.IO.File.WriteAllText(templatePath, json);

                UpdateStatus(string.Format("Template created: {0}", templateName));

                MessageBox.Show(
                    string.Format("Template '{0}' created successfully!\n\nLocation: {1}",
                        templateName, templatePath),
                    "Template Created",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error creating template", ex);
            }
        }

        #endregion

        #region Batch Edit 

        /// <summary>
        /// Hromadná editácia označených príkazov
        /// </summary>
        private void BatchEditCommands_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItems = AppCommander_MainCommandTable.SelectedItems.Cast<UnifiedItem>().ToList();

                if (!selectedItems.Any())
                {
                    MessageBox.Show(
                        "Please select one or more commands to edit.",
                        "No Selection",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                string input = ShowInputDialog(
                    string.Format("Enter multiplier for repeat count ({0} commands selected):", selectedItems.Count),
                    "Batch Edit",
                    "2");

                if (string.IsNullOrEmpty(input))
                {
                    return;
                }

                int multiplier;
                if (!int.TryParse(input, out multiplier))
                {
                    MessageBox.Show(
                        "Please enter a valid number.",
                        "Invalid Input",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Aplikovanie zmeny na všetky vybrané príkazy
                foreach (var item in selectedItems)
                {
                    item.RepeatCount *= multiplier;
                }

                _hasUnsavedUnifiedChanges = true;
                AppCommander_MainCommandTable.Items.Refresh();

                UpdateStatus(string.Format("Batch updated {0} commands", selectedItems.Count));

                MessageBox.Show(
                    string.Format("Successfully updated {0} commands.", selectedItems.Count),
                    "Batch Edit Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error in batch edit", ex);
            }
        }

        #endregion

        #region Sequence Editor Integration

        /// <summary>
        /// Otvorí Sequence Editor pre všetky príkazy alebo konkrétnu sekvenciu
        /// </summary>
        private void OpenSequenceEditor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Skontroluj či je vybraná konkrétna sekvencia na editáciu
                var selectedItem = AppCommander_MainCommandTable.SelectedItem as UnifiedItem;

                if (selectedItem != null && selectedItem.Type == UnifiedItem.ItemType.SequenceReference)
                {
                    // ═══════════════════════════════════════════════════════════
                    //  USE CASE: Edituj príkazy KONKRÉTNEJ sekvencie zo súboru

                    var editor = new SequenceEditorWindow(
                        new[] { selectedItem },
                        selectedItem.Name
                    )
                    {
                        Owner = this
                    };

                    if (editor.ShowDialog() == true)
                    {
                        // Zmeny boli uložené priamo do súboru sekvencie
                        UpdateStatus($"✅ Sequence '{selectedItem.Name}' updated successfully");

                        // Voliteľne: Refresh sequence info ak chceš aktualizovať AppCommander_MainCommandTable
                        // (napríklad ak sa zmenil počet príkazov v sekvencii)
                    }
                }
                else
                {
                    // ═══════════════════════════════════════════════════════════
                    //  USE CASE: Edituj VŠETKY príkazy v AppCommander_MainCommandTable   

                    if (_unifiedItems == null || _unifiedItems.Count == 0) 
                    {
                        MessageBox.Show(
                            "No commands to edit. Please record or load some commands first.",
                            "No Commands",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        return;
                    }

                    // Otvor Sequence Editor Window pre všetky príkazy
                    var editorWindow = new SequenceEditorWindow(_unifiedItems, _currentSequenceName)
                    {
                        Owner = this
                    };

                    bool? result = editorWindow.ShowDialog();

                    // Ak boli uložené zmeny, aktualizuj _unifiedItems
                    if (result == true && editorWindow.WasSaved)
                    {
                        _unifiedItems.Clear();
                        foreach (var item in editorWindow.EditedItems)
                        {
                            _unifiedItems.Add(item);
                        }

                        _hasUnsavedUnifiedChanges = true;
                        UpdateUI();
                        UpdateStatus($"Sequence updated - {_unifiedItems.Count} commands");
                    }
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error opening sequence editor", ex);
            }
        }

        /// <summary>
        /// Alternatívna metóda - otvorí editor pre vybranú časť sekvencie
        /// </summary>
        private void OpenSequenceEditorForSelection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (AppCommander_MainCommandTable.SelectedItems.Count == 0)
                {
                    MessageBox.Show(
                        "Please select commands to edit.",
                        "No Selection",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                // Vytvor zoznam vybraných items
                var selectedItems = new List<UnifiedItem>();
                foreach (UnifiedItem item in AppCommander_MainCommandTable.SelectedItems)
                {
                    selectedItems.Add(item);
                }

                var editorWindow = new SequenceEditorWindow(selectedItems, "Selected Commands")
                {
                    Owner = this
                };

                bool? result = editorWindow.ShowDialog();

                if (result == true && editorWindow.WasSaved)
                {
                    // Aktualizuj vybrané items v hlavnom zozname
                    var editedItems = editorWindow.EditedItems;
                    int editedIndex = 0;

                    foreach (UnifiedItem selectedItem in AppCommander_MainCommandTable.SelectedItems.Cast<UnifiedItem>().ToList())
                    {
                        if (editedIndex < editedItems.Count)
                        {
                            var editedItem = editedItems[editedIndex];
                            var index = _unifiedItems.IndexOf(selectedItem);

                            if (index >= 0)
                            {
                                _unifiedItems[index] = editedItem;
                            }
                            editedIndex++;
                        }
                    }

                    _hasUnsavedUnifiedChanges = true;
                    RecalculateStepNumbers();
                    UpdateUI();
                    UpdateStatus("Selected commands updated");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error editing selection", ex);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // INTELIGENTNÝ HANDLER - Rozhodne ktoré okno otvoriť
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Inteligentne otvorí správne okno na editáciu podľa typu vybraného item-u
        /// </summary>
        private void OpenSmartEditor(UnifiedItem selectedItem)
        {
            if (selectedItem == null)
            {
                MessageBox.Show(
                    "Please select an item to edit.",
                    "No Selection",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            try
            {
                if (selectedItem.Type == UnifiedItem.ItemType.SequenceReference)
                {
                    // ═══════════════════════════════════════════════════════════
                    // SEKVENCIA → Otvor SequenceEditorWindow
                    // ═══════════════════════════════════════════════════════════

                    var sequenceEditor = new SequenceEditorWindow(
                        new[] { selectedItem },
                        selectedItem.Name
                    )
                    {
                        Owner = this
                    };

                    if (sequenceEditor.ShowDialog() == true)
                    {
                        UpdateStatus($"✅ Sequence '{selectedItem.Name}' updated successfully");
                    }
                }
                else
                {
                    // ═══════════════════════════════════════════════════════════
                    // PRÍKAZ → Otvor EditCommandWindow
                    // ═══════════════════════════════════════════════════════════

                    var commandEditor = new EditCommandWindow(selectedItem)
                    {
                        Owner = this
                    };

                    if (commandEditor.ShowDialog() == true && commandEditor.WasSaved)
                    {
                        // Aktualizuj item v tabuľke s editovanými údajmi
                        commandEditor.UpdateUnifiedItem(selectedItem);

                        _hasUnsavedUnifiedChanges = true;
                        UpdateUI();
                        UpdateStatus($"✅ Command '{selectedItem.Name}' updated");
                    }
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error opening editor", ex);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // EVENT HANDLERY - Všetky používajú OpenSmartEditor
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Double-click na riadok v tabuľke
        /// </summary>
        private void AppCommander_MainCommandTable_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var selectedItem = AppCommander_MainCommandTable.SelectedItem as UnifiedItem;
                if (selectedItem == null)
                    return;

                // **Špecializovaná obsluha pre warning položku**
                if (selectedItem.Type == UnifiedItem.ItemType.LiveRecording &&
                    selectedItem.Name == "⚠️ Unsaved Command Set")
                {
                    HandleWarningItemClick(selectedItem);
                    return;
                }

                // logika pre ostatné položky
                OpenSmartEditor(selectedItem);
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error handling double click", ex);
            }
        }

        #endregion
    }
}
