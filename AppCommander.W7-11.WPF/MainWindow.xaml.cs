using AppCommander.W7_11.WPF.Core;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

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

        // Collections
        private ObservableCollection<Command> _commands;

        // State
        private IntPtr _targetWindowHandle = IntPtr.Zero;
        private string _currentFilePath = string.Empty;
        private bool _hasUnsavedChanges = false;
        private bool _isAutoTrackingEnabled = true;

        #endregion

        #region Constructor

        public MainWindow()
        {
            InitializeComponent();
            InitializeApplication();
        }

        private void InitializeApplication()
        {
            try
            {
                // Initialize core components
                _windowTracker = new WindowTracker();
                _recorder = new CommandRecorder();
                _player = new CommandPlayer();
                _automaticUIManager = new AutomaticUIManager();

                // Initialize collections
                _commands = new ObservableCollection<Command>();
                dgCommands.ItemsSource = _commands;

                // Subscribe to events
                SubscribeToEvents();

                // Update UI
                UpdateUI();
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
                        var message = e.IsPaused ? "Recording paused" : string.Format("Recording: {0}", e.SequenceName);
                        UpdateStatus(message);
                    }
                    else
                    {
                        UpdateStatus("Recording stopped");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error handling recording state change: {0}", ex.Message));
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

        #region Window Click Selection

        private WindowClickSelector _windowClickSelector;

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
        /// Handler pre výber okna pomocou kliknutia
        /// </summary>
        private async void SelectTargetByClick_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_windowClickSelector.IsSelecting)
                {
                    _windowClickSelector.CancelSelection();
                    return;
                }

                // Zmeni tlačidlo na cancel mode
                btnSelectTargetByClick.Content = "❌ Cancel Selection";
                btnSelectTargetByClick.IsEnabled = true;

                // Disable ostatné controls počas výberu
                btnSelectTarget.IsEnabled = false;
                btnStartRecording.IsEnabled = false;

                UpdateStatus("Click selection mode activated. Click on any window to select it as target.");

                // Spusti async selection
                var selectedWindow = await _windowClickSelector.StartWindowSelectionAsync();

                if (selectedWindow != null)
                {
                    // Nastav vybrané okno ako target
                    _targetWindowHandle = selectedWindow.WindowHandle;
                    lblTargetWindow.Content = string.Format("{0} - {1}",
                        selectedWindow.ProcessName, selectedWindow.Title);

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
                    lblTargetWindow.Content = string.Format("{0} - {1}",
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

        /// <summary>
        /// Resetuje UI po click selection
        /// </summary>
        private void ResetClickSelectionUI()
        {
            try
            {
                btnSelectTargetByClick.Content = "👆 Click to Select";
                btnSelectTargetByClick.IsEnabled = true;
                btnSelectTarget.IsEnabled = true;
                btnStartRecording.IsEnabled = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error resetting click selection UI: {ex.Message}");
            }
        }

        #endregion

        #region Updated Constructor and Cleanup

        // Pridajte toto do existujúceho konstruktora MainWindow
        private void InitializeComponents()
        {
            // Existujúci inicializačný kód...

            // Pridajte toto na koniec
            InitializeWindowClickSelector();
        }

        // Aktualizujte Dispose/Cleanup metódu
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // Existujúci cleanup kód...

                // Pridajte cleanup pre window click selector
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
                    lblTargetWindow.Content = string.Format("{0} - {1}",
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

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                if (_hasUnsavedChanges)
                {
                    var result = MessageBox.Show("You have unsaved changes. Do you want to save before closing?",
                                                "Unsaved Changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        SaveSequence_Click(null, null);
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

        #endregion

        #region Recording Controls

        private void StartRecording_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_recorder.IsRecording)
                {
                    _recorder.StopRecording();
                    _windowTracker.StopTracking();
                    _automaticUIManager.StopMonitoring();
                    return;
                }

                if (_targetWindowHandle == IntPtr.Zero)
                {
                    MessageBox.Show("Please select a target window first.", "No Target Selected",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    SelectTarget_Click(sender, e);
                    return;
                }

                var sequenceName = string.Format("Recording_{0:yyyyMMdd_HHmmss}", DateTime.Now);

                _recorder.StartRecording(sequenceName, _targetWindowHandle);

                string targetProcess = GetProcessNameFromWindow(_targetWindowHandle);
                _windowTracker.StartTracking(targetProcess);
                _automaticUIManager.StartMonitoring(_targetWindowHandle, targetProcess);

                UpdateStatus(string.Format("Recording started: {0} (Target: {1})", sequenceName, targetProcess));
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error starting recording", ex);
            }
        }

        //private void SelectTarget_Click(object sender, RoutedEventArgs e)
        //{
        //    try
        //    {
        //        var dialog = new WindowSelectorDialog();
        //        if (dialog.ShowDialog() == true && dialog.SelectedWindow != null)
        //        {
        //            _targetWindowHandle = dialog.SelectedWindow.WindowHandle;
        //            lblTargetWindow.Content = string.Format("{0} - {1}",
        //                dialog.SelectedWindow.ProcessName, dialog.SelectedWindow.Title);

        //            UpdateUI();
        //            UpdateStatus(string.Format("Target selected: {0}", dialog.SelectedWindow.ProcessName));

        //            Debug.WriteLine(string.Format("Target window selected: Handle=0x{0:X8}, Process={1}, Title={2}",
        //                _targetWindowHandle.ToInt64(), dialog.SelectedWindow.ProcessName, dialog.SelectedWindow.Title));
        //        }
        //        else
        //        {
        //            Debug.WriteLine("No window selected or dialog cancelled");
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        ShowErrorMessage("Error selecting target window", ex);
        //    }
        //}

        private void StopRecording_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_recorder.IsRecording)
                {
                    _recorder.StopRecording();
                    _windowTracker.StopTracking();
                    _automaticUIManager.StopMonitoring();

                    lblAutoDetectionStatus.Content = "Auto-Detection Inactive";
                    lblUIRecordingStatus.Content = "UI Scanning Inactive";
                    progressEnhancedRecording.Visibility = Visibility.Collapsed;
                    progressEnhancedRecording.IsIndeterminate = false;

                    UpdateStatus("Recording stopped");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error stopping recording", ex);
            }
        }

        private void PauseRecording_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_recorder.IsRecording)
                {
                    if (_recorder.IsPaused)
                    {
                        _recorder.ResumeRecording();
                        btnPauseRecording.Content = "Pause";
                        UpdateStatus("Recording resumed");
                    }
                    else
                    {
                        _recorder.PauseRecording();
                        btnPauseRecording.Content = "Resume";
                        UpdateStatus("Recording paused");
                    }
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error pausing/resuming recording", ex);
            }
        }

        #endregion

        #region Enhanced Recording

        private void StartEnhancedRecordingWithAutoDetection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_recorder.IsRecording)
                {
                    MessageBox.Show("Recording is already in progress. Please stop current recording first.",
                                   "Recording In Progress", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (_targetWindowHandle == IntPtr.Zero)
                {
                    MessageBox.Show("Please select a target window first.", "No Target Selected",
                                   MessageBoxButton.OK, MessageBoxImage.Warning);
                    SelectTarget_Click(sender, e);
                    return;
                }

                var sequenceName = txtSequenceName.Text;
                if (string.IsNullOrWhiteSpace(sequenceName))
                {
                    sequenceName = string.Format("Enhanced_Recording_{0:yyyyMMdd_HHmmss}", DateTime.Now);
                    txtSequenceName.Text = sequenceName;
                }

                _recorder.StartRecording(sequenceName, _targetWindowHandle);
                _recorder.EnableRealTimeElementScanning = true;
                _recorder.AutoUpdateExistingCommands = true;
                _recorder.EnablePredictiveDetection = true;

                string targetProcess = GetProcessNameFromWindow(_targetWindowHandle);
                _windowTracker.StartTracking(targetProcess);
                _automaticUIManager.StartMonitoring(_targetWindowHandle, targetProcess);

                lblAutoDetectionStatus.Content = "Auto-Detection Active";
                lblUIRecordingStatus.Content = "UI Scanning Active";
                progressEnhancedRecording.Visibility = Visibility.Visible;
                progressEnhancedRecording.IsIndeterminate = true;

                UpdateStatus(string.Format("Enhanced recording started: {0} (Target: {1})", sequenceName, targetProcess));
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error starting enhanced recording", ex);
            }
        }

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

        private void ToggleAutomaticMode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _isAutoTrackingEnabled = !_isAutoTrackingEnabled;
                btnToggleAutoMode.Content = _isAutoTrackingEnabled ? "Auto Mode ON" : "Auto Mode OFF";

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

        private void StartPlayback_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_player.IsPlaying)
                {
                    UpdateStatus("Playback is already running");
                    return;
                }

                if (!_commands.Any())
                {
                    MessageBox.Show("No commands to play. Please record some commands first.",
                                  "No Commands", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

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
                if (!int.TryParse(txtRepeatCount.Text, out repeatCount) || repeatCount < 1)
                {
                    repeatCount = 1;
                    txtRepeatCount.Text = "1";
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
            catch (Exception ex)
            {
                ShowErrorMessage("Error starting playback", ex);
            }
        }

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

        private void PausePlayback_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_player.IsPaused)
                {
                    _player.Resume();
                    btnPause.Content = "Pause";
                }
                else if (_player.IsPlaying)
                {
                    _player.Pause();
                    btnPause.Content = "Resume";
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
                btnPause.Content = "Pause";
                UpdateStatus("Playback stopped");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error stopping playback", ex);
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

        private void NewSequence_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_hasUnsavedChanges)
                {
                    var result = MessageBox.Show("You have unsaved changes. Do you want to save before creating a new sequence?",
                                                "Unsaved Changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

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
            catch (Exception ex)
            {
                ShowErrorMessage("Error creating new sequence", ex);
            }
        }

        private void OpenSequence_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "AppCommander Files (*.acc)|*.acc|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                    DefaultExt = ".acc"
                };

                if (dialog.ShowDialog() == true)
                {
                    LoadSequenceFromFile(dialog.FileName);
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error opening sequence", ex);
            }
        }

        private void SaveSequence_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(_currentFilePath))
                {
                    SaveSequenceAs_Click(sender, e);
                    return;
                }

                SaveSequenceToFile(_currentFilePath);
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error saving sequence", ex);
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

        private void EditCommand_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedCommand = dgCommands.SelectedItem as Command;
                if (selectedCommand == null)
                {
                    MessageBox.Show("Please select a command to edit.", "No Selection",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string newValue = ShowInputDialog("Edit command value:", "Edit Command", selectedCommand.Value);

                if (!string.IsNullOrEmpty(newValue))
                {
                    selectedCommand.Value = newValue;

                    if (selectedCommand.Type == CommandType.LoopStart && int.TryParse(newValue, out int repeatCount))
                    {
                        selectedCommand.RepeatCount = repeatCount;
                    }

                    _hasUnsavedChanges = true;
                    dgCommands.Items.Refresh();
                    UpdateStatus("Command edited");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error editing command", ex);
            }
        }

        private void DeleteCommand_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedCommand = dgCommands.SelectedItem as Command;
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

        private void InfiniteLoop_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                txtRepeatCount.IsEnabled = false;
                txtRepeatCount.Text = "∞";
                UpdateStatus("Infinite loop enabled");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error enabling infinite loop", ex);
            }
        }

        private void InfiniteLoop_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                txtRepeatCount.IsEnabled = true;
                txtRepeatCount.Text = "1";
                UpdateStatus("Infinite loop disabled");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error disabling infinite loop", ex);
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

        #region UI Updates

        private void UpdateUI()
        {
            try
            {
                bool isRecording = _recorder != null && _recorder.IsRecording;
                bool isPlaying = _player != null && _player.IsPlaying;

                btnStartRecording.Content = isRecording ? "Stop Recording" : "Record";
                bool hasTargetWindow = _targetWindowHandle != IntPtr.Zero;
                bool shouldEnableRecord = hasTargetWindow || isRecording;

                btnStartRecording.IsEnabled = shouldEnableRecord;
                btnPlay.IsEnabled = _commands.Any() && !isRecording && !isPlaying;
                btnPause.IsEnabled = isPlaying;
                btnStop.IsEnabled = isPlaying;

                var loopCount = _commands.Count(c => c.Type == CommandType.LoopStart);
                string commandText = loopCount > 0 ?
                    string.Format("Commands: {0} ({1} loops)", _commands.Count, loopCount) :
                    string.Format("Commands: {0}", _commands.Count);
                txtCommandCount.Text = commandText;

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
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error updating UI: {0}", ex.Message));
            }
        }

        private void UpdateStatus(string message)
        {
            try
            {
                txtStatusBar.Text = string.Format("{0:HH:mm:ss} - {1}", DateTime.Now, message);
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
                    lblStatusBarRecording.Text = isPaused ? "Recording Paused" : "Recording";
                }
                else
                {
                    lblStatusBarRecording.Text = "Not Recording";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error updating recording status: {0}", ex.Message));
            }
        }

        #endregion

        #region Helper Methods

        private void RefreshElementStatistics()
        {
            try
            {
                lstElementStats.Items.Clear();

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
                    lstElementStats.Items.Add(element);
                }

                UpdateStatus(string.Format("Element statistics refreshed: {0} unique elements", elementGroups.Count));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error refreshing element statistics: {0}", ex.Message));
            }
        }

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

            return dialog.ShowDialog() == true ? result : null;
        }

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

        #endregion
    }
}
