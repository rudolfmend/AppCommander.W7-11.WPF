using AppCommander.W7_11.WPF.Core;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using AppCommander.W7_11.WPF.Core.Managers;

public class SequenceSetItem : INotifyPropertyChanged
{
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
        #region Fields - MEMBER DEFINITIONS 

        // Core components
        private CommandRecorder _recorder;   
        private WindowTracker _windowTracker;      
        private AutomaticUIManager _automaticUIManager; 
        internal RecordingManager _recordingManager;
        internal CommandPlayer _player;
        internal WindowClickOverlay _overlay;
        internal ThemeManager _themeManager;
        internal PlaybackManager _playbackManager;
        internal SequenceManager _sequenceManager;
        internal FileManager _fileManager;
        internal TableOperationsManager _tableOperationsManager;
        internal LoopControlsManager _loopControlsManager;

        internal bool _isSidePanelVisible = false;  // Side panel state (visible/hidden) 

        // Sequence of commands
        internal string _currentSequenceName = string.Empty;

        // Collections
        private ObservableCollection<Command> _commands;

        // State
        private IntPtr _targetWindowHandle = IntPtr.Zero;
        private bool _isAutoTrackingEnabled = true;

        private ObservableCollection<SequenceSetItem> _sequenceSetItems;
        private SequenceSet _currentSequenceSet;
        private string _currentSequenceSetFilePath;
        private bool _hasUnsavedSequenceSetChanges;

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
            InitializeWindowClickSelector();
            InitializeSidePanel();

            // PRIDAJ: Nastavenie UI controls pre PlaybackManager
            // Toto musí byť až PO InitializeComponent()
            if (_playbackManager != null)
            {
                _playbackManager.SetUIControls(
                    AppCommander_BtnPause,
                    AppCommander_TxtRepeatCount,
                    AppCommander_ChkInfiniteLoop
                );
            }

            this.AllowDrop = true;
            this.PreviewDragEnter += MainWindow_PreviewDragEnter;
            this.PreviewDragLeave += MainWindow_PreviewDragLeave;
            this.PreviewDrop += MainWindow_PreviewDrop;
        }

        private void InitializeUnifiedTable()
        {
            _unifiedItems = new ObservableCollection<UnifiedItem>();

            if (AppCommander_MainCommandTable != null)
            {
                AppCommander_MainCommandTable.ItemsSource = _unifiedItems;

                // Inicializuj TableOperationsManager s rozšírenými callbacks
                _tableOperationsManager = new TableOperationsManager(
                    _unifiedItems,
                    AppCommander_MainCommandTable,
                    UpdateStatus,
                    UpdateUI,
                    // Callback pre nastavenie HasUnsavedUnifiedChanges
                    (value) => { if (_sequenceManager != null) _sequenceManager.HasUnsavedUnifiedChanges = value; },
                    // Callback pre UpdateUnsavedCommandsWarning
                    () => _sequenceManager?.UpdateUnsavedCommandsWarning()
                );
            }

            Debug.WriteLine("AppCommander_MainCommandTable table initialized");
        }

        private void InitializeApplication()
        {
            try
            {
                _windowTracker = new WindowTracker();
                _recorder = new CommandRecorder();
                _player = new CommandPlayer();
                _automaticUIManager = new AutomaticUIManager();
                _recordingManager = new RecordingManager(
                    _recorder,
                    _windowTracker,
                    _automaticUIManager);
                _themeManager = new ThemeManager();

                _commands = new ObservableCollection<Command>();
                InitializeUnifiedTable();
                InitializeSequenceSet();

                // Inicializácia PlaybackManager
                _playbackManager = new PlaybackManager(
                    _player,
                    _commands,
                    _unifiedItems,
                    () => _targetWindowHandle,
                    () => _currentSequenceName,
                    UpdateStatus,
                    UpdateUI,
                    this.Dispatcher
                );

                // Inicializácia SequenceManager
                _sequenceManager = new SequenceManager(
                    _commands,
                    _unifiedItems,
                    () => _targetWindowHandle,
                    () => _currentSequenceName,
                    UpdateStatus,
                    UpdateUI,
                    GetProcessNameFromWindow,
                    GetWindowTitle
                );

                // Inicializácia FileManager
                _fileManager = new FileManager(
                    UpdateStatus,
                    UpdateUI
                );

                // Inicializácia LoopControlsManager
                _loopControlsManager = new LoopControlsManager(
                    AppCommander_TxtRepeatCount,
                    AppCommander_ChkInfiniteLoop,
                    UpdateStatus
                );

                _loopControlsManager.AttachEventHandlers
                    (
                        onEnterPressed: () => AppCommander_BtnPlayCommands.RaiseEvent
                        (
                            new RoutedEventArgs
                            (
                                Button.ClickEvent
                            )
                        )
                    );

                SubscribeToEvents();
                UpdateUI();
                UpdateStatus("Application initialized - Ready to start");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error initializing application", ex);
            }
        }

        #endregion

        #region Drag-and-Drop Handlers

        private void MainWindow_PreviewDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                DropOverlay.Visibility = Visibility.Visible;
                e.Effects = DragDropEffects.Copy;
            }
            e.Handled = true;
        }

        private void MainWindow_PreviewDragLeave(object sender, DragEventArgs e)
        {
            Point pos = e.GetPosition(this);
            if (pos.X < 0 || pos.Y < 0 || pos.X > this.ActualWidth || pos.Y > this.ActualHeight)
            {
                DropOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void MainWindow_PreviewDrop(object sender, DragEventArgs e)
        {
            DropOverlay.Visibility = Visibility.Collapsed;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                // Použije FileManager
                var result = _fileManager.ProcessDroppedFiles(files);

                if (result.Success)
                {
                    // Spracuj sequence súbory
                    foreach (var file in result.SequenceFiles)
                    {
                        _sequenceManager.LoadSequenceFile(file);
                    }

                    // Spracuj output súbory (Excel/CSV) - set as target
                    if (result.OutputFiles.Any())
                    {
                        var outputFile = result.OutputFiles.First();
                        var confirmResult = MessageBox.Show(
                            $"Set '{Path.GetFileName(outputFile)}' as target output file?\n\n" +
                            "This file will be used to store extracted data from documents.",
                            "Set Target Output File",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (confirmResult == MessageBoxResult.Yes)
                        {
                            _fileManager.SetTargetOutputFile(outputFile, confirmOverwrite: false);
                        }

                        // Ak je viac output súborov, upozorni
                        if (result.OutputFiles.Count > 1)
                        {
                            MessageBox.Show(
                                $"Note: Only the first file was set as target.\n" +
                                $"Other {result.OutputFiles.Count - 1} file(s) were ignored.",
                                "Multiple Output Files",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                    }

                    // Spracuj input súbory (PDF/Images) - add to queue
                    if (result.InputFiles.Any())
                    {
                        var addedCount = _fileManager.AddSourceFiles(result.InputFiles);

                        if (addedCount > 0)
                        {
                            MessageBox.Show(
                                $"Added {addedCount} file(s) to processing queue.\n\n" +
                                "Click 'Process Documents' to extract data.",
                                "Files Added",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                    }

                    // Upozorni na nepodporované súbory
                    if (result.UnsupportedFiles.Any())
                    {
                        MessageBox.Show(
                            $"Warning: {result.UnsupportedFiles.Count} unsupported file(s) were ignored.\n\n" +
                            $"Supported formats:\n" +
                            $"• Sequences: .acc, .uniseq, .acset\n" +
                            $"• Output: .xlsx, .csv, .xls\n" +
                            $"• Input: .pdf, .jpg, .png, .txt, .xml, .json",
                            "Unsupported Files",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }

                    UpdateStatus(result.Message);
                }
                else
                {
                    UpdateStatus($"Drop failed: {result.Message}");
                }
            }

            e.Handled = true;
        }

        #endregion

        #region Event Subscriptions

        private void SubscribeToEvents()
        {
            try
            {
                // Recorder events
                _recordingManager.CommandRecorded += OnCommandRecorded;
                _recordingManager.RecordingStateChanged += OnRecordingStateChanged;

                // ZMEŇ: Player events - použi PlaybackManager handlery
                _player.CommandExecuted += _playbackManager.OnCommandExecuted;
                _player.PlaybackStateChanged += _playbackManager.OnPlaybackStateChanged;
                _player.PlaybackCompleted += _playbackManager.OnPlaybackCompleted;
                _player.PlaybackError += _playbackManager.OnPlaybackError;

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
                Debug.WriteLine($"Error subscribing to events: {ex.Message}");
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
                    _sequenceManager.HasUnsavedChanges = true;
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
                string Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                Debug.WriteLine($"Recording state changed: IsRecording={e.IsRecording}, IsPaused={e.IsPaused}, SequenceName={e.SequenceName}, Time={Timestamp}");

                Dispatcher.Invoke(() =>
                {
                    if (e.IsRecording)
                    {
                        string message = e.IsPaused ?
                            string.Format("Recording paused: {0}", e.SequenceName) :
                            string.Format("Recording started: {0}", e.SequenceName);
                        UpdateStatus(message);
                    }
                    else
                    {
                        UpdateStatus(string.Format("Recording stopped: {0}", e.SequenceName));

                        // Aktualizuj _sequenceManager.CurrentUnifiedSequence s nahratými príkazmi
                        if (_unifiedItems.Count > 0)
                        {
                            _sequenceManager.CurrentUnifiedSequence = new UnifiedSequence
                            {
                                Name = e.SequenceName ?? "Recorded Sequence",
                                Items = new List<UnifiedItem>(_unifiedItems),
                                TargetProcessName = GetProcessNameFromWindow(_targetWindowHandle),
                                TargetWindowTitle = GetWindowTitle(_targetWindowHandle),
                                Created = DateTime.Now,
                                LastModified = DateTime.Now
                            };

                            Debug.WriteLine($"Updated _sequenceManager.CurrentUnifiedSequence with {_unifiedItems.Count} items");
                        }

                        _sequenceManager.UpdateUnsavedCommandsWarning();
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error handling recording state changed: {0}", ex.Message));
            }
        }

        #endregion

        #region Recording Methods 

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

                // Validácia target window
                if (_targetWindowHandle == IntPtr.Zero)
                {
                    MessageBox.Show(
                        "Please select a target window first.",
                        "No Target Selected",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Získaj target process name
                string targetProcess = GetProcessNameFromWindow(_targetWindowHandle);

                // Spusti nahrávanie cez RecordingManager
                bool success = _recordingManager.StartRecording(_targetWindowHandle, targetProcess);

                if (success)
                {
                    // Aktualizuj UI
                    AppCommander_BtnRecording.Content = "⏹ Stop Recording";
                    UpdateStatusLabels(true);
                    AppCommander_ProgressEnhancedRecording.Visibility = Visibility.Visible;
                    AppCommander_ProgressEnhancedRecording.IsIndeterminate = true;
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error starting recording", ex);
            }
        }

        private async void StartStopToggleRecording_Click(object sender, RoutedEventArgs e)
        {
            // ════════════════════════════════════════
            System.Diagnostics.Debug.WriteLine("════════════════════════════════════════");
            System.Diagnostics.Debug.WriteLine("🔴 START/STOP RECORDING BUTTON CLICKED");
            System.Diagnostics.Debug.WriteLine($"   _recorder is null: {_recorder == null}");
            System.Diagnostics.Debug.WriteLine($"   _recordingManager.IsRecording: {_recorder?.IsRecording ?? false}");
            System.Diagnostics.Debug.WriteLine($"   _targetWindowHandle: 0x{_targetWindowHandle:X}");
            System.Diagnostics.Debug.WriteLine("════════════════════════════════════════");
            // ════════════════════════════════════════

            try
            {
                // Ak už nahrávame, zastav nahrávanie
                if (_recorder != null && _recordingManager.IsRecording)
                {
                    StopCurrentRecording();
                    return;
                }

                // Ak nemáme vybraný target, automaticky spusti výber okna
                if (_targetWindowHandle == IntPtr.Zero)
                {
                    await StartRecordingWithAutoSelection();
                }
                else
                {
                    // Máme target, začni nahrávanie priamo
                    StartNewRecording();
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error toggling recording", ex);
            }
        }

        // nové ↓
        /// <summary>
        /// Spustí nahrávanie s automatickým výberom okna
        /// </summary>
        private async Task StartRecordingWithAutoSelection()
        {
            try
            {
                // Informuj používateľa
                UpdateStatus("Select target window by clicking on it...");

                // Zmena UI pre indikáciu výberu
                var originalButtonContent = AppCommander_BtnRecording.Content;
                AppCommander_BtnRecording.Content = "⏳ Selecting Window...";

                // Zobraz selection indicator ak existuje
                if (AppCommander_SelectionModeIndicator != null)
                {
                    AppCommander_SelectionModeIndicator.Visibility = Visibility.Visible;
                    AppCommander_TxtSelectionMode.Text = "Click on target window";
                }

                // Spusti window selection
                var selectedWindow = await _windowClickSelector.StartWindowSelectionAsync();

                if (selectedWindow != null)
                {
                    // Úspešný výber - nastav target
                    _targetWindowHandle = selectedWindow.WindowHandle;

                    // Overenie, že nevyberáme AppCommander
                    if (selectedWindow.ProcessName.Equals("AppCommander", StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show(
                            "Cannot record actions on Senaro itself.\n" +
                            "Please select a different application.",
                            "Invalid Target",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);

                        // Reset target
                        _targetWindowHandle = IntPtr.Zero;
                        UpdateTargetWindowInfo(null);
                    }
                    else
                    {
                        // Nastav target window info
                        UpdateTargetWindowInfo(selectedWindow);

                        // Automaticky začni nahrávanie
                        UpdateStatus($"Target selected: {selectedWindow.ProcessName}. Starting recording...");

                        // Malé oneskorenie pre lepší UX
                        await Task.Delay(500);

                        // Začni nahrávanie
                        StartNewRecording();
                    }
                }
                else
                {
                    // Výber bol zrušený
                    UpdateStatus("Window selection cancelled. Recording not started.");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error during auto window selection", ex);
            }
            finally
            {
                // Reset UI
                AppCommander_BtnRecording.IsEnabled = true;

                // Skry selection indicator
                if (AppCommander_SelectionModeIndicator != null)
                {
                    AppCommander_SelectionModeIndicator.Visibility = Visibility.Collapsed;
                }

                // Update button podľa stavu
                UpdateRecordingButton();
            }
        }

        /// <summary>
        /// Rýchle nahrávanie - kombinuje výber okna a nahrávanie
        /// </summary>
        private async void QuickRecording_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Ak už nahrávame, zastav
                if (_recordingManager.IsRecording)
                {
                    StopCurrentRecording();
                    return;
                }

                // Vždy začni s výberom nového okna
                await StartRecordingWithAutoSelection();
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error in quick recording", ex);
            }
        }

        /// <summary>
        /// Aktualizuje Recording button podľa stavu
        /// </summary>
        private void UpdateRecordingButton()
        {
            try
            {
                if (_recordingManager.IsRecording)
                {
                    AppCommander_BtnRecording.Content = "⏹ Stop Recording";
                }
                else
                {
                    if (_targetWindowHandle == IntPtr.Zero)
                    {
                        AppCommander_BtnRecording.Content = "🎯 Select & Record";
                    }
                    else
                    {
                        AppCommander_BtnRecording.Content = "🔴 Start Recording";
                    }
                }
                // Style zostane DangerButton z XAML - netreba meniť
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating recording button: {ex.Message}");
            }
        }

        /// <summary>
        /// Upravená metóda SelectTargetByClick_Click pre kompatibilitu
        /// </summary>
        private async void SelectTargetByClick_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Ak je výber aktívny, zruš ho
                if (_windowClickSelector.IsSelecting)
                {
                    _windowClickSelector.CancelSelection();
                    ResetClickSelectionUI();
                    return;
                }

                // Spusti výber
                var selectedWindow = await SelectWindowByClick();

                if (selectedWindow != null)
                {
                    _targetWindowHandle = selectedWindow.WindowHandle;
                    UpdateTargetWindowInfo(selectedWindow);
                    UpdateUI();

                    // Ak chceme, môžeme automaticky začať nahrávanie
                    if (AppCommander_ChkAutoStartRecording?.IsChecked == true)
                    {
                        StartNewRecording();
                    }
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error during window selection", ex);
            }
        }

        /// <summary>
        /// Pomocná metóda pre výber okna kliknutím
        /// </summary>
        private async Task<WindowTrackingInfo> SelectWindowByClick()
        {
            try
            {
                // UI indikácia výberu
                AppCommander_SelectionModeIndicator.Visibility = Visibility.Visible;
                AppCommander_TxtSelectionMode.Text = "Click Selection Active";

                // Zmena tlačidla
                //AppCommander_AppCommander_BtnSelectTargetByClick.Content = "❌ Cancel Selection";
                AppCommander_BtnSelectTarget.IsEnabled = false;

                UpdateStatus("Click on any window to select it as target...");

                // Spusti výber
                var result = await _windowClickSelector.StartWindowSelectionAsync();

                return result;
            }
            finally
            {
                ResetClickSelectionUI();
            }
        }

        /// <summary>
        /// Keyboard shortcut handler pre rýchle nahrávanie
        /// </summary>
        private async void Window_KeyDown(object sender, KeyEventArgs e)
        {
            await Task.Yield(); // Ensures the method is asynchronous and suppresses CS1998

            try
            {
                // F9 - Toggle recording s auto-selection
                if (e.Key == Key.F9)
                {
                    e.Handled = true;
                    StartStopToggleRecording_Click(null, null);
                }
                // Ctrl+R - Quick recording (vždy nový výber)
                else if (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    e.Handled = true;
                    QuickRecording_Click(null, null);
                }
                // Escape - Stop recording ak beží
                else if (e.Key == Key.Escape && _recordingManager.IsRecording)
                {
                    e.Handled = true;
                    StopCurrentRecording();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in keyboard handler: {ex.Message}");
            }
        }

        //private async void Window_KeyDown(object sender, KeyEventArgs e)
        //{
        //    try
        //    {
        //        // F9 - Toggle recording s auto-selection
        //        if (e.Key == Key.F9)
        //        {
        //            e.Handled = true;
        //            StartStopToggleRecording_Click(null, null);
        //        }
        //        // Ctrl+R - Quick recording (vždy nový výber)
        //        else if (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.Control)
        //        {
        //            e.Handled = true;
        //             QuickRecording_Click(null, null);
        //        }
        //        // Escape - Stop recording ak beží
        //        else if (e.Key == Key.Escape && _recordingManager.IsRecording)
        //        {
        //            e.Handled = true;
        //            StopCurrentRecording();
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine($"Error in keyboard handler: {ex.Message}");
        //    }
        //}

        #endregion

        #region Helper Methods

        /// <summary>
        /// Overí či je možné začať nahrávanie
        /// </summary>
        private bool CanStartRecording()
        {
            // Nemôžeme nahrávať počas prehrávania
            if (_player?.IsPlaying == true)
            {
                return false;
            }

            // Pre auto-selection nepotrebujeme target vopred
            return true;
        }

        /// <summary>
        /// Zobrazí tooltip s informáciami o rýchlom nahrávaní
        /// </summary>
        private void ShowQuickRecordingTooltip()
        {
            var tooltip = new ToolTip
            {
                Content = "Click to select target window and start recording immediately\n" +
                         "Shortcut: F9 or Ctrl+R",
                PlacementTarget = AppCommander_BtnRecording,
                Placement = PlacementMode.Bottom,
                IsOpen = true
            };

            // Auto-hide po 3 sekundách
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (s, e) =>
            {
                tooltip.IsOpen = false;
                timer.Stop();
            };
            timer.Start();
        }

        private void StopCurrentRecording()
        {
            try
            {
                _recordingManager.StopRecording();
                UpdateRecordingButton();
                UpdateStatusLabels(false);
                AppCommander_ProgressEnhancedRecording.Visibility = Visibility.Collapsed;
                AppCommander_ProgressEnhancedRecording.IsIndeterminate = false;
                UpdateStatus("Recording stopped");

                if (_commands != null && _commands.Count > 0)
                {
                    int commandCount = _commands.Count;

                    // Automatické uloženie bez MessageBoxu
                    var defaultFileName = $"Sequence_{DateTime.Now:yyyyMMdd_HHmmss}.acc";
                    var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    var appCommanderPath = Path.Combine(documentsPath, "Senaro", "Sequences");

                    Directory.CreateDirectory(appCommanderPath);
                    var filePath = Path.Combine(appCommanderPath, defaultFileName);

                    _sequenceManager.SaveSequenceToFile(filePath);
                    _sequenceManager.OnSequenceSavedSuccessfully(filePath);

                    ShowToast(
                        "Sequence Saved",
                        $"File: {defaultFileName}\nLocation: {appCommanderPath}\nCommands: {commandCount}",
                        3000);
                }

                _sequenceManager.UpdateUnsavedCommandsWarning();
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error stopping recording", ex);
            }
        }

        //  toast notifikácie
        private void ShowToast(string v1, string v2, int v3)
        {
            MessageBox.Show(v2, v1, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region UI Update Method - 

        private void UpdateUI()
        {
            try
            {
                bool isRecording = _recorder != null && _recordingManager.IsRecording;
                bool isPlaying = _player != null && _player.IsPlaying;
                bool hasTargetWindow = _targetWindowHandle != IntPtr.Zero;

                // Select button state 
                if (AppCommander_BtnSelectTarget != null)
                    AppCommander_BtnSelectTarget.IsEnabled = !isRecording;

                // Recording button state - vždy aktívne (umožní výber okna)
                if (AppCommander_BtnRecording != null)
                    AppCommander_BtnRecording.IsEnabled = true;

                // Playback buttons
                if (AppCommander_BtnPlayCommands != null)
                    AppCommander_BtnPlayCommands.IsEnabled = (_commands.Any() || _unifiedItems.Count > 0) && !isRecording && !isPlaying;

                if (AppCommander_BtnPause != null)
                    AppCommander_BtnPause.IsEnabled = isPlaying;

                if (AppCommander_BtnStop != null)
                    AppCommander_BtnStop.IsEnabled = isPlaying;

                // Commands count
                var loopCount = _commands.Count(c => c.Type == CommandType.LoopStart);
                string commandText = loopCount > 0
                    ? string.Format("{0} commands ({1} loop{2})", _commands.Count, loopCount, loopCount != 1 ? "s" : "")
                    : string.Format("{0} command{1}", _commands.Count, _commands.Count != 1 ? "s" : "");

                if (AppCommander_TxtCommandsCount != null)
                    AppCommander_TxtCommandsCount.Text = commandText;

                // Recording status v side paneli (NOVÝ)
                if (AppCommander_LblUIRecordingStatus != null)
                {
                    AppCommander_LblUIRecordingStatus.Text = isRecording ? "Active" : "Inactive";
                    AppCommander_LblUIRecordingStatus.Foreground = isRecording
                        ? new SolidColorBrush(Color.FromRgb(198, 40, 40))  // Červená
                        : new SolidColorBrush(Color.FromRgb(96, 94, 92));   // Sivá
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error updating UI: {0}", ex.Message));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void SetUIState(bool enabled)
        {
            try
            {
                // Recording button
                if (AppCommander_BtnRecording != null)
                    AppCommander_BtnRecording.IsEnabled = enabled;

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
                if (AppCommander_BtnSelectTarget != null)
                    AppCommander_BtnSelectTarget.IsEnabled = enabled;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error setting UI state: {0}", ex.Message));
            }
        }

        #endregion

        #region Element Statistics - PODMIENEČNÉ

        private void RefreshElementStatistics()
        {
            try
            {
                // podmienečná kontrola či AppCommander_LstElementStats existuje
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

        #region Auto Mode Toggle - Conditional Check

        private void ToggleAutomaticMode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _isAutoTrackingEnabled = !_isAutoTrackingEnabled;

                // podmienečná kontrola
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

        #region Selection UI Updates - 


        #endregion

        #region UI Behavior Enhancements 

        /// <summary>
        /// Resetuje UI po click selection 
        /// </summary>
        private void ResetClickSelectionUI()
        {
            try
            {
                //AppCommander_AppCommander_BtnSelectTargetByClick.Content = "🎯 Click to Select";
                //AppCommander_AppCommander_BtnSelectTargetByClick.IsEnabled = true;
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
        /// Aktualizuje target window information 
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
                    // Tlačidlo zostane kliknuteľné ak sú načítané príkazy (.acc súbor)
                    AppCommander_BtnRecording.IsEnabled = _unifiedItems.Count > 0;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating target window info: {ex.Message}");
                AppCommander_TxtTargetProcess.Text = "-";
                AppCommander_LblTargetWindow.Text = "Error loading target info";
                // Tlačidlo zostane kliknuteľné ak sú načítané príkazy (.acc súbor)
                AppCommander_BtnRecording.IsEnabled = _unifiedItems.Count > 0;
            }
            finally
            {
                UpdateUI();
            }
        }

        /// <summary>
        /// Aktualizuje status labels 
        /// </summary>
        private void UpdateStatusLabels(bool isRecording)
        {
            try
            {
                if (isRecording)
                {
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

        /// <summary>
        /// Toggle zobrazenie/skrytie bočného panelu
        /// </summary>
        private void ToggleSidePanel_Click(object sender, RoutedEventArgs e)
        {
            ToggleSidePanel(!_isSidePanelVisible);
        }

        /// <summary>
        /// Zobrazí alebo skryje bočný panel s animáciou
        /// </summary>
        private void ToggleSidePanel(bool show)
        {
            _isSidePanelVisible = show;

            if (show)
            {
                // Zobraz panel najprv
                AppCommander_SidePanel.Visibility = Visibility.Visible;

                // Animuj šírku stĺpca z 0 na 320
                var animation = new GridLengthAnimation
                {
                    From = new GridLength(0, GridUnitType.Pixel),
                    To = new GridLength(320, GridUnitType.Pixel),
                    Duration = TimeSpan.FromMilliseconds(250),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                SidePanelColumn.BeginAnimation(ColumnDefinition.WidthProperty, animation);

                // Zmení ikonu - šípka doprava (na skrytie panelu)
                UpdateToggleIcon("M8.59 16.59L13.17 12 8.59 7.41 10 6l6 6-6 6-1.41-1.41z", "Hide side panel");
            }
            else
            {
                // Animuj šírku stĺpca z 320 na 0
                var animation = new GridLengthAnimation
                {
                    From = new GridLength(320, GridUnitType.Pixel),
                    To = new GridLength(0, GridUnitType.Pixel),
                    Duration = TimeSpan.FromMilliseconds(250),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };

                animation.Completed += (s, e) =>
                {
                    // Po dokončení animácie skry panel úplne
                    AppCommander_SidePanel.Visibility = Visibility.Collapsed;
                };

                SidePanelColumn.BeginAnimation(ColumnDefinition.WidthProperty, animation);

                // Zmení ikonu - šípka doľava (na zobrazenie panelu)
                UpdateToggleIcon("M15.41 16.59L10.83 12l4.58-4.59L14 6l-6 6 6 6 1.41-1.41z", "Show side panel");
            }
        }

        /// <summary>      
        /// Aktualizuje ikonu a tooltip toggle tlačidla
        /// </summary>
        private void UpdateToggleIcon(string pathData, string tooltip)
        {
            if (AppCommander_BtnToggleSidePanel == null) return;

            try
            {
                // Získaj Template obsah
                var template = AppCommander_BtnToggleSidePanel.Template;
                if (template == null) return;

                // Najdi Path element v template
                var path = template.FindName("ToggleIconPath", AppCommander_BtnToggleSidePanel) as System.Windows.Shapes.Path;

                if (path != null)
                {
                    // Animuj fade out
                    var fadeOut = new DoubleAnimation
                    {
                        From = 1.0,
                        To = 0.0,
                        Duration = TimeSpan.FromMilliseconds(100)
                    };

                    fadeOut.Completed += (s, e) =>
                    {
                        // Zmeň ikonu
                        path.Data = Geometry.Parse(pathData);

                        // Animuj fade in
                        var fadeIn = new DoubleAnimation
                        {
                            From = 0.0,
                            To = 1.0,
                            Duration = TimeSpan.FromMilliseconds(100)
                        };
                        path.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                    };

                    path.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                }
                else
                {
                    Debug.WriteLine("ToggleIconPath not found in template");
                }

                // Aktualizuj tooltip
                AppCommander_BtnToggleSidePanel.ToolTip = tooltip;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating toggle icon: {ex.Message}");
            }
        }

        /// <summary>
        /// Inicializácia side panelu
        /// </summary>
        private void InitializeSidePanel()
        {
            // Panel skrytý na začiatku
            _isSidePanelVisible = false;
            AppCommander_SidePanel.Visibility = Visibility.Collapsed;
            SidePanelColumn.Width = new GridLength(0);

            // Nastav počiatočnú ikonu - šípka doľava (zobraziť panel)
            UpdateToggleIcon("M15.41 16.59L10.83 12l4.58-4.59L14 6l-6 6 6 6 1.41-1.41z", "Show side panel");
        }

        #endregion

        #region Window Click Selection

        private WindowClickSelector _windowClickSelector;
        private ObservableCollection<UnifiedItem> _unifiedItems;

        private void AppCommander_BtnRecording_Click(object sender, RoutedEventArgs e)
        {
            // Ak ešte nie je vybraté cieľové okno, spustíme výber kliknutím
            if (_targetWindowHandle == IntPtr.Zero) // if (_recorder.TargetWindowHandle == IntPtr.Zero) -public IntPtr TargetWindowHandle in class WindowContext in CommandRecorder.cs
            {
                StartWindowClickSelection();
                return;
            }

            // Inak štandardne spustíme nahrávanie   
            _ = StartRecordingWithAutoSelection(); // Await the async method
        }

        private async void StartWindowClickSelection()
        {
            if (_windowClickSelector == null)
                _windowClickSelector = new WindowClickSelector();

            _windowClickSelector.WindowSelected += OnWindowClickSelected;

            await _windowClickSelector.StartWindowSelectionAsync();
        }

        private void UpdateTargetWindowDisplay(IntPtr handle)
        {
            var windowInfo = _windowTracker.CreateWindowTrackingInfo(handle);
            if (windowInfo != null)
            {
                // Aktualizuj label/textbox s názvom okna
                AppCommander_TxtTarget.Text = windowInfo.Title;
            }
        }

        private void GetWindowInfo(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("GetWindowInfo method not implemented yet.");
            MessageBox.Show("GetWindowInfo method not implemented yet.");
        }

        public event EventHandler<IntPtr> WindowSelected;

        public void StartSelection()
        {
            _overlay = new WindowClickOverlay();

            // _overlay.WindowClicked += OnWindowClicked;

            // Replace this block in MainWindow.xaml.cs

            //_overlay.MouseDown += (s, args) =>
            //{
            //    // may to check which mouse button was pressed, e.g. left click
            //    if (args.LeftButton == MouseButtonState.Pressed)
            //    {                
            //        // For example, if WindowClickOverlay has a property TargetWindowHandle:

            //        IntPtr windowHandle = WindowClickOverlay.UpdateHighlightRectangle(); // <-- Replace with actual property/method- in WindowClickOverlay to get the handle.
            //                                                     // need to implement logic to get the window handle from the overlay.
            //        OnWindowClicked(_overlay, windowHandle);
            //    }
            //};

            // Replace this block in MainWindow.xaml.cs

            _overlay.MouseDown += (s, args) =>
            {
                // may to check which mouse button was pressed, e.g. left click
                if (args.LeftButton == MouseButtonState.Pressed)
                {
                    // must pass a window handle to UpdateHighlightRectangle(IntPtr)
                    // If have a target window handle, use it here. For demonstration, let's assume you have a variable 'targetWindowHandle' (IntPtr)
                    // If not, you need to obtain the handle of the window under the mouse cursor.

                    // Example: Use _targetWindowHandle if available
                    IntPtr windowHandle = _targetWindowHandle;

                    // If you need to get the handle under the mouse, you must implement that logic.
                    // For now, this will fix the CS7036 error by passing a valid IntPtr.

                    _overlay.UpdateHighlightRectangle(windowHandle); // Pass the required argument

                    OnWindowClicked(_overlay, windowHandle);
                }
            };

            _overlay.Show();
        }

        private void OnWindowClicked(object sender, IntPtr handle)
        {
            _overlay?.Close();
            WindowSelected?.Invoke(this, handle);
        }

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

        #region SelectTarget_Click Method

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
                    if (_isAutoTrackingEnabled && _recordingManager.IsRecording)
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
                if (_recordingManager.IsRecording && _isAutoTrackingEnabled)
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
                if (_recordingManager.IsRecording)
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
                if (_recordingManager.IsRecording)
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
                    if (_recordingManager.IsRecording && IsRelevantWindow(e))
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
                   (!string.IsNullOrEmpty(_recordingManager.CurrentSequence?.TargetProcessName) &&
                    e.ProcessName.IndexOf(_recordingManager.CurrentSequence.TargetProcessName, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private bool IsRelevantWindow(NewWindowAppearedEventArgs e)
        {
            return e.WindowType == WindowType.Dialog ||
                   e.WindowType == WindowType.MessageBox ||
                   (!string.IsNullOrEmpty(_recordingManager.CurrentSequence?.TargetProcessName) &&
                    e.ProcessName.IndexOf(_recordingManager.CurrentSequence.TargetProcessName, StringComparison.OrdinalIgnoreCase) >= 0);
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
                if (_recordingManager.IsRecording)
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

        #region Playback Click Handlers

        private void PlaySequence_Click(object sender, RoutedEventArgs e)
        {
            _playbackManager?.StartPlayback();
        }

        private void PausePlayback_Click(object sender, RoutedEventArgs e)
        {
            _playbackManager?.TogglePause();
        }

        private void StopPlayback_Click(object sender, RoutedEventArgs e)
        {
            _playbackManager?.StopPlayback();
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
                    _recordingManager.EnableRealTimeElementScanning ? "Active" : "Inactive",
                    _recordingManager.AutoUpdateExistingCommands ? "Enabled" : "Disabled",
                    _isAutoTrackingEnabled ? "Enabled" : "Disabled",
                    _targetWindowHandle != IntPtr.Zero ? GetWindowTitle(_targetWindowHandle) : "None",
                    _commands.Count,
                    _recordingManager.IsRecording ? "Recording" : "Stopped",
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

        #region Menu Handlers - File

        /// <summary>
        /// Aktualizuje NewSequence_Click aby vytvorila unified sequence
        /// </summary>
        private void NewSequence_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_unifiedItems.Count > 0 || _commands.Count == 0)
                {
                    _sequenceManager.NewUnifiedSequence();
                }
                else
                {
                    // Fallback na starý systém
                    if (_sequenceManager.HasUnsavedChanges)
                    {
                        var result = MessageBox.Show(
                            "You have unsaved changes. Do you want to save before creating a new sequence?",
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
                    _sequenceManager.HasUnsavedChanges = false;
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
                var filePath = _fileManager.OpenSequenceDialog();

                if (!string.IsNullOrEmpty(filePath))
                {
                    var extension = Path.GetExtension(filePath).ToLower();

                    if (extension == ".uniseq")
                    {
                        _sequenceManager.LoadUnifiedSequenceFromFile(filePath);
                        UpdateMainWindowUI();
                    }
                    else if (extension == ".acc" || extension == ".json")
                    {
                        var fileName = Path.GetFileNameWithoutExtension(filePath);

                        if (!_sequenceManager.ValidateSequenceFile(filePath))
                        {
                            MessageBox.Show($"File '{fileName}' is not a valid sequence file.",
                                           "Invalid File",
                                           MessageBoxButton.OK,
                                           MessageBoxImage.Error);
                            return;
                        }

                        if (_unifiedItems.Any(item =>
                            item.Type == UnifiedItem.ItemType.SequenceReference &&
                            item.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
                        {
                            MessageBox.Show($"Sequence '{fileName}' is already in the list.",
                                           "Duplicate Sequence",
                                           MessageBoxButton.OK,
                                           MessageBoxImage.Warning);
                            return;
                        }

                        var unifiedItem = UnifiedItem.FromSequenceFile(filePath, _unifiedItems.Count + 1);
                        _unifiedItems.Add(unifiedItem);

                        _sequenceManager.HasUnsavedUnifiedChanges = true;
                        RecalculateStepNumbers();
                        UpdateMainWindowUI();
                        UpdateStatus($"Sequence '{fileName}' added to list");
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
                if (_unifiedItems.Count > 0)
                {
                    if (string.IsNullOrEmpty(_sequenceManager.CurrentUnifiedSequenceFilePath))
                    {
                        SaveAsSet_Click(sender, e);
                        return;
                    }
                    _sequenceManager.SaveUnifiedSequenceToFile(_sequenceManager.CurrentUnifiedSequenceFilePath);
                }
                else
                {
                    if (string.IsNullOrEmpty(_sequenceManager.CurrentFilePath))
                    {
                        SaveSequenceAs_Click(sender, e);
                        return;
                    }
                    _sequenceManager.SaveSequenceToFile(_sequenceManager.CurrentFilePath);
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error saving sequence", ex);
            }
        }

        private void UpdateMainWindowUI()
        {
            try
            {
                AppCommander_TxtSetCount.Text = string.Format("Unified Sequences: {0}", _unifiedItems.Count);
                AppCommander_TxtSequenceCount.Text = string.Format("Unified Items: {0}", _unifiedItems.Count);
                AppCommander_TxtCommandsCount.Text = string.Format("Unified Items: {0}", _unifiedItems.Count);

                UpdateUI();

                // Aktualizuje title okna
                string title = "Senaro";
                if (!string.IsNullOrEmpty(_sequenceManager.CurrentUnifiedSequenceFilePath))
                {
                    title += string.Format(" - {0}", Path.GetFileName(_sequenceManager.CurrentUnifiedSequenceFilePath));
                }
                if (_sequenceManager.HasUnsavedUnifiedChanges)
                {
                    title += " *";
                }
                this.Title = title;

                // Aktualizuje status bar
                AppCommander_TxtCommandsCount.Text = string.Format("Unified Items: {0}", _unifiedItems.Count);

                // Aktualizuje enabled stav menu položiek
                AppCommander_MenuBar.IsEnabled = _unifiedItems.Count > 0;
                //menuItem.IsEnabled = _sequenceManager.HasUnsavedUnifiedChanges && _unifiedItems.Count > 0;

                // Aktualizuje enabled stav playback tlačidiel
                AppCommander_BtnPlayCommands.IsEnabled = _unifiedItems.Count > 0 && !(_recorder?.IsRecording ?? false) && !(_player?.IsPlaying ?? false);
                AppCommander_BtnQuickReselect.IsEnabled = _unifiedItems.Count > 0 && !(_recorder?.IsRecording ?? false) && !(_player?.IsPlaying ?? false);
                AppCommander_BtnPause.IsEnabled = _player?.IsPlaying ?? false;
                AppCommander_BtnStop.IsEnabled = _player?.IsPlaying ?? false;

                // Aktualizuje enabled stav recording tlačidiel
                // Tlačidlo je kliknuteľné ak je vybraté cieľové okno, alebo prebieha nahrávanie, alebo sú načítané príkazy (.acc súbor)
                AppCommander_BtnRecording.IsEnabled = (_targetWindowHandle != IntPtr.Zero) || (_recorder?.IsRecording ?? false) || (_unifiedItems.Count > 0);
                //AppCommander_AppCommander_BtnSelectTargetByClick.IsEnabled = !(_recorder?.IsRecording ?? false);
                AppCommander_BtnSelectTarget.IsEnabled = !(_recorder?.IsRecording ?? false);

                // Aktualizuje stavový riadok
                if (_recorder?.IsRecording ?? false)
                {
                    UpdateStatus("Recording in progress...");
                    _sequenceManager.UpdateUnsavedCommandsWarning();

                }
                else if (_player?.IsPlaying ?? false)
                {
                    UpdateStatus("Playback in progress...");
                    _sequenceManager.UpdateUnsavedCommandsWarning();
                }
                else
                {
                    UpdateStatus("Ready");
                    _sequenceManager.UpdateUnsavedCommandsWarning();
                }

                _sequenceManager.UpdateUnsavedCommandsWarning();

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in UpdateMainWindowUI: {ex.Message}");
                _sequenceManager.UpdateUnsavedCommandsWarning();
            }
        }

        private void SaveSequenceAs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var filePath = _fileManager.SaveSequenceAsDialog();

                if (!string.IsNullOrEmpty(filePath))
                {
                    _sequenceManager.SaveSequenceToFile(filePath);
                    _sequenceManager.OnSequenceSavedSuccessfully(filePath);
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error saving sequence", ex);
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
                bool hasUnsavedChanges = _sequenceManager.HasUnsavedChanges || _sequenceManager.HasUnsavedUnifiedChanges || _hasUnsavedSequenceSetChanges;

                if (hasUnsavedChanges)
                {
                    var changes = new List<string>();
                    if (_sequenceManager.HasUnsavedChanges) changes.Add("recorded commands");
                    if (_sequenceManager.HasUnsavedUnifiedChanges) changes.Add("unified sequence");
                    if (_hasUnsavedSequenceSetChanges) changes.Add("sequence set");

                    var changesList = string.Join(", ", changes);
                    var result = MessageBox.Show($"You have unsaved changes in: {changesList}.\n\nDo you want to save before closing?",
                                                "Unsaved Changes",
                                                MessageBoxButton.YesNoCancel,
                                                MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        // Save the most relevant format
                        if (_sequenceManager.HasUnsavedUnifiedChanges)
                        {
                            SaveAsSet_Click(null, null);
                        }
                        else if (_sequenceManager.HasUnsavedChanges)
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
                if (_recordingManager != null) _recordingManager.Dispose();
                if (_player != null) _player.Dispose();
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
                if (_unifiedItems == null || _unifiedItems.Count == 0)
                {
                    MessageBox.Show("Cannot save empty sequence. Please add commands or sequences first.",
                                    "Empty Sequence",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
                    return;
                }

                if (!_sequenceManager.CurrentUnifiedSequence.IsValid(out List<string> errors))
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

                string defaultName = _sequenceManager.CurrentUnifiedSequence?.Name ??
                     "UnifiedSequence_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");

                var filePath = _fileManager.SaveUnifiedSequenceAsDialog(defaultName);

                if (!string.IsNullOrEmpty(filePath))
                {
                    _sequenceManager.SaveUnifiedSequenceToFile(filePath);
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error saving unified sequence", ex);
            }
        }

        #endregion

        #region Updated AppCommander_MainCommandTable Table Methods

        /// <summary>
        /// Presúva položku vyššie v MainCommandTable tabuľke
        /// </summary>
        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _tableOperationsManager.MoveUp();
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error moving item up", ex);
            }
        }

        /// <summary>
        /// Presúva položku nižšie v MainCommandTable tabuľke
        /// </summary>
        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _tableOperationsManager.MoveDown();
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error moving item down", ex);
            }
        }

        private void RecalculateStepNumbers()
        {
            _tableOperationsManager?.RecalculateStepNumbers();
        }

        /// <summary>
        /// 
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
                if (_sequenceManager.HasUnsavedChanges)
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

                _sequenceManager.HasUnsavedUnifiedChanges = true;
                RecalculateStepNumbers();
                UpdateStatus($"Added {addedCount} commands to unified sequence");
                _sequenceManager.UpdateUnsavedCommandsWarning();

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
                            _sequenceManager.HasUnsavedChanges = false;

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

                _sequenceManager.UpdateUnsavedCommandsWarning();
                Debug.WriteLine("called method _sequenceManager.UpdateUnsavedCommandsWarning() in method AddFromCommands_Click()");

            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error adding commands to unified list", ex);
            }
        }

        /// <summary>
        /// Pridá sekvenciu zo súboru do AppCommander_MainCommandTable tabuľky
        /// </summary>
        private void AddSequence_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    Filter = "Senaro Files (*.acc)|*.acc|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
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
                    if (!_sequenceManager.ValidateSequenceFile(filePath))
                    {
                        MessageBox.Show($"File '{fileName}' is not a valid sequence file.",
                                       "Invalid File",
                                       MessageBoxButton.OK,
                                       MessageBoxImage.Error);
                        return;
                    }

                    // Vytvorenie ho UnifiedItem
                    var unifiedItem = UnifiedItem.FromSequenceFile(filePath, _unifiedItems.Count + 1);
                    _unifiedItems.Add(unifiedItem);

                    _sequenceManager.HasUnsavedUnifiedChanges = true;
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
        /// Edituje vybranú položku v AppCommander_MainCommandTable tabuľke - Rýchla editácia 
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
                    _sequenceManager.HasUnsavedUnifiedChanges = true;
                    AppCommander_MainCommandTable.Items.Refresh();
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error editing unified item", ex);
            }
        }

        /// <summary>
        /// Odstráni vybranú položku z AppCommander_MainCommandTable tabuľky
        /// </summary>
        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _tableOperationsManager.DeleteSelected();
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
                if (!_recordingManager.IsRecording)
                {
                    MessageBox.Show("Please start recording first.", "Not Recording",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string input = ShowInputDialog("Enter wait time in milliseconds:", "Add Wait Command", "1000");

                if (!string.IsNullOrEmpty(input) && int.TryParse(input, out int waitTime) && waitTime > 0)
                {
                    _recordingManager.AddWaitCommand(waitTime);
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
                if (!_recordingManager.IsRecording)
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
                    _sequenceManager.HasUnsavedChanges = true;
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
                if (!_recordingManager.IsRecording)
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
                _sequenceManager.HasUnsavedChanges = true;
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

                    _sequenceManager.HasUnsavedUnifiedChanges = true;
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
                    _sequenceManager.HasUnsavedChanges = true;
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
                var userGuideWindow = new UserGuideWindow();
                userGuideWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error opening user guide", ex);
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
                    "Senaro Settings\n\n" +
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
                    _recorder != null && _recordingManager.EnableRealTimeElementScanning ? "Active" : "Inactive",
                    _recorder != null && _recordingManager.EnablePredictiveDetection ? "Enabled" : "Disabled",
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
            try
            {
                var aboutWindow = new AboutWindow();
                aboutWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error opening about window", ex);
            }
        }

        private void PrivacyPolicy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var privacyWindow = new PrivacyPolicyWindow();
                privacyWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error opening privacy policy window", ex);
            }
        }

        #endregion

        #region Loop Controls

        private void InfiniteLoop_Checked(object sender, RoutedEventArgs e)
            => _loopControlsManager.InfiniteLoop_Checked(sender, e);

        private void InfiniteLoop_Unchecked(object sender, RoutedEventArgs e)
            => _loopControlsManager.InfiniteLoop_Unchecked(sender, e);

        private void AppCommander_TxtRepeatCount_KeyDown(object sender, KeyEventArgs e)
            => _loopControlsManager.RepeatCount_KeyDown(sender, e,
                () => AppCommander_BtnPlayCommands.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));

        private void AppCommander_TxtRepeatCount_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
            => _loopControlsManager.RepeatCount_PreviewMouseWheel(sender, e);

        private void AppCommander_TxtRepeatCount_PreviewKeyDown(object sender, KeyEventArgs e)
            => _loopControlsManager.RepeatCount_PreviewKeyDown(sender, e);

        private void AppCommander_TxtRepeatCount_PreviewTextInput(object sender, TextCompositionEventArgs e)
            => _loopControlsManager.RepeatCount_PreviewTextInput(sender, e);

        private void AppCommander_TxtRepeatCount_LostFocus(object sender, RoutedEventArgs e)
            => _loopControlsManager.RepeatCount_LostFocus(sender, e);

        #endregion

        #region Mouse Wheel Support for Repeat Count

        /// <summary>
        /// Handler pre klávesy šípok na TextBox AppCommander_TxtRepeatCount
        /// </summary>
        //private void AppCommander_TxtRepeatCount_PreviewKeyDown(object sender, KeyEventArgs e)
        //{
        //    try
        //    {
        //        // Kontrola či je infinite loop zapnutý
        //        if (AppCommander_ChkInfiniteLoop?.IsChecked == true)
        //        {
        //            if (e.Key == Key.Up || e.Key == Key.Down)
        //            {
        //                e.Handled = true;
        //            }
        //            return;
        //        }

        //        var textBox = sender as TextBox;
        //        if (textBox == null || !textBox.IsEnabled)
        //        {
        //            return;
        //        }

        //        // Spracovanie šípok hore/dole
        //        if (e.Key == Key.Up || e.Key == Key.Down)
        //        {
        //            int currentValue;
        //            if (!int.TryParse(textBox.Text, out currentValue))
        //            {
        //                currentValue = 1;
        //            }

        //            int delta = e.Key == Key.Up ? 1 : -1;

        //            // Modifikátory pre väčšie kroky
        //            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        //            {
        //                delta *= 10;
        //            }
        //            else if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
        //            {
        //                delta *= 5;
        //            }

        //            int newValue = currentValue + delta;
        //            newValue = Math.Max(1, Math.Min(9999, newValue));

        //            textBox.Text = newValue.ToString();
        //            textBox.SelectAll();

        //            e.Handled = true;
        //            UpdateStatus($"Repeat count changed to {newValue}");
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        ShowErrorMessage("Error handling key press", ex);
        //    }
        //}

        #endregion

        #region File Operations

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

                typeof(SequenceManager)
                    .GetField("_currentFilePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.SetValue(_sequenceManager, filePath);

                _sequenceManager.HasUnsavedChanges = false;
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
                    Name = _currentSequenceSet?.Name ?? Path.GetFileNameWithoutExtension(filePath),

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

                    FileName = _currentSequenceSet?.Name ?? "SequenceSet_" + DateTime.Now.ToString("yyyyMMdd_HHmmss")
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

        #region Theme Selection

        private void ThemeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var menuItem = sender as MenuItem;
                if (menuItem?.Tag is string theme)
                {
                    _themeManager.SetTheme(theme);
                    UpdateThemeMenuChecks(theme);
                    UpdateStatus($"{theme} theme applied");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error changing theme", ex);
            }
        }

        /// <summary>
        /// Aktualizuje checkmarky v Theme menu
        /// </summary>
        private void UpdateThemeMenuChecks(string selectedTheme)
        {
            try
            {
                // Zruš check všetkým
                if (MenuItemThemeSystem != null)
                    MenuItemThemeSystem.IsChecked = false;
                if (MenuItemThemeLight != null)
                    MenuItemThemeLight.IsChecked = false;
                if (MenuItemThemeDark != null)
                    MenuItemThemeDark.IsChecked = false;
                if (MenuItemThemeHighContrast != null)
                    MenuItemThemeHighContrast.IsChecked = false;

                // Nastav check len aktívnej téme
                switch (selectedTheme)
                {
                    case "System":
                        if (MenuItemThemeSystem != null)
                            MenuItemThemeSystem.IsChecked = true;
                        break;
                    case "Light":
                        if (MenuItemThemeLight != null)
                            MenuItemThemeLight.IsChecked = true;
                        break;
                    case "Dark":
                        if (MenuItemThemeDark != null)
                            MenuItemThemeDark.IsChecked = true;
                        break;
                    case "HighContrast":
                        if (MenuItemThemeHighContrast != null)
                            MenuItemThemeHighContrast.IsChecked = true;
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating theme menu checks: {ex.Message}");
            }
        }

        #endregion

        #region Unsaved Commands Warning Management

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
                    _sequenceManager.UpdateUnsavedCommandsWarning();
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

                    _sequenceManager.HasUnsavedUnifiedChanges = true;
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
                _sequenceManager.UpdateUnsavedCommandsWarning();
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
            _tableOperationsManager.CopyToClipboard();
        }

        private void ContextMenu_Duplicate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _tableOperationsManager.ContextMenu_Duplicate(sender, e);
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
                if (_sequenceManager.HasUnsavedUnifiedChanges)
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
                        if (_sequenceManager.HasUnsavedUnifiedChanges) // Ak sa neuložilo (cancel), neexekuovať
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
                var sequence = _sequenceManager.CurrentUnifiedSequence.ToCommandSequence();
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
                //if (AppCommander_AppCommander_BtnSelectTargetByClick != null)
                //    AppCommander_AppCommander_BtnSelectTargetByClick.IsEnabled = enabled;

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
                _tableOperationsManager.DuplicateSelected();
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

                _sequenceManager.HasUnsavedUnifiedChanges = true;
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

                        _sequenceManager.HasUnsavedUnifiedChanges = true;
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

                    _sequenceManager.HasUnsavedUnifiedChanges = true;
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
                        //commandEditor.UpdateUnifiedItem(selectedItem);

                        _sequenceManager.HasUnsavedUnifiedChanges = true;
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

        #region GridLengthAnimation Helper

        /// <summary>
        /// Custom animácia pre GridLength s podporou EasingFunction
        /// Potrebná pre animáciu šírky stĺpca side panelu
        /// </summary>
        public class GridLengthAnimation : AnimationTimeline
        {
            // Dependency Properties
            public static readonly DependencyProperty FromProperty =
                DependencyProperty.Register("From", typeof(GridLength), typeof(GridLengthAnimation));

            public static readonly DependencyProperty ToProperty =
                DependencyProperty.Register("To", typeof(GridLength), typeof(GridLengthAnimation));

            public static readonly DependencyProperty EasingFunctionProperty =
                DependencyProperty.Register("EasingFunction", typeof(IEasingFunction), typeof(GridLengthAnimation));

            // Properties
            public GridLength From
            {
                get => (GridLength)GetValue(FromProperty);
                set => SetValue(FromProperty, value);
            }

            public GridLength To
            {
                get => (GridLength)GetValue(ToProperty);
                set => SetValue(ToProperty, value);
            }

            public IEasingFunction EasingFunction
            {
                get => (IEasingFunction)GetValue(EasingFunctionProperty);
                set => SetValue(EasingFunctionProperty, value);
            }

            public override Type TargetPropertyType => typeof(GridLength);

            protected override Freezable CreateInstanceCore() => new GridLengthAnimation();

            public override object GetCurrentValue(object defaultOriginValue,
                                                   object defaultDestinationValue,
                                                   AnimationClock animationClock)
            {
                if (animationClock.CurrentProgress == null)
                    return From;

                double fromValue = From.Value;
                double toValue = To.Value;
                double progress = animationClock.CurrentProgress.Value;

                // Aplikuj easing funkciu ak existuje
                if (EasingFunction != null)
                {
                    progress = EasingFunction.Ease(progress);
                }

                double currentValue = fromValue + (toValue - fromValue) * progress;
                return new GridLength(currentValue, GridUnitType.Pixel);
            }
        }
        #endregion

        #region DOCUMENT PROCESSING UI

        /// <summary>
        /// Tlačidlo "Set Target Output File"
        /// </summary>
        private void SetTargetOutputFile_Click(object sender, RoutedEventArgs e)
        {
            _fileManager.SelectTargetOutputFileDialog();
        }

        /// <summary>
        /// Tlačidlo "Add Documents to Queue"
        /// </summary>
        private void AddDocumentsToQueue_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Title = "Select Documents to Process",
                    Filter = "All Supported|*.pdf;*.jpg;*.jpeg;*.png;*.txt;*.xml;*.json|" +
                            "PDF Files (*.pdf)|*.pdf|" +
                            "Images (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png|" +
                            "Text Files (*.txt)|*.txt|" +
                            "Data Files (*.xml;*.json)|*.xml;*.json|" +
                            "All Files (*.*)|*.*",
                    Multiselect = true
                };

                if (dialog.ShowDialog() == true)
                {
                    var addedCount = _fileManager.AddSourceFiles(dialog.FileNames);

                    if (addedCount > 0)
                    {
                        MessageBox.Show(
                            $"Added {addedCount} file(s) to processing queue.",
                            "Files Added",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error adding documents", ex);
            }
        }

        /// <summary>
        /// Tlačidlo "Clear Queue"
        /// </summary>
        private void ClearDocumentQueue_Click(object sender, RoutedEventArgs e)
        {
            if (_fileManager.SourceFileCount > 0)
            {
                var result = MessageBox.Show(
                    $"Clear all {_fileManager.SourceFileCount} file(s) from queue?",
                    "Clear Queue",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _fileManager.ClearSourceFilesQueue();
                }
            }
        }

        /// <summary>
        /// Tlačidlo "Process Documents"
        /// </summary>
        private void ProcessDocuments_Click(object sender, RoutedEventArgs e)  
        {
            try
            {
                if (!_fileManager.HasTargetFile)
                {
                    MessageBox.Show(
                        "Please set a target output file first.\n\n" +
                        "Drag & drop an Excel/CSV file or click 'Set Target Output File'.",
                        "No Target File",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (_fileManager.SourceFileCount == 0)
                {
                    MessageBox.Show(
                        "No documents in queue.\n\n" +
                        "Drag & drop documents or click 'Add Documents'.",
                        "Empty Queue",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                // TODO: Implementácia document processing logiky
                MessageBox.Show(
                    $"Document processing will be implemented soon.\n\n" +
                    $"Target: {Path.GetFileName(_fileManager.TargetOutputFile)}\n" +
                    $"Documents: {_fileManager.SourceFileCount}",
                    "Processing",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                UpdateStatus($"Processing {_fileManager.SourceFileCount} documents...");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error processing documents", ex);
            }
        }  

        #endregion 
    }
}
