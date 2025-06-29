using AppCommander.W7_11.WPF.Core;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;

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

                Debug.WriteLine("✅ MainWindow initialized successfully");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error initializing application", ex);
            }
        }

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

                // Window tracker events - OPRAVENÉ
                _windowTracker.NewWindowDetected += OnNewWindowDetected;
                _windowTracker.WindowActivated += OnWindowActivated;
                _windowTracker.WindowClosed += OnWindowClosed;

                // Automatic UI manager events
                _automaticUIManager.UIChangeDetected += OnUIChangeDetected;
                _automaticUIManager.NewWindowAppeared += OnNewWindowAppeared;

                // Window events
                this.Closing += MainWindow_Closing;

                Debug.WriteLine("📡 Event subscriptions completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error subscribing to events: {ex.Message}");
            }
        }

        #endregion

        #region Event Handlers

        private void OnCommandRecorded(object sender, CommandRecordedEventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    _commands.Add(e.Command);
                    _hasUnsavedChanges = true;
                    UpdateUI();

                    // ROZŠÍRENÉ: Zobraz informácie o type príkazu
                    string commandDescription = GetCommandDescription(e.Command);
                    UpdateStatus($"Command recorded: {commandDescription}");
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error handling command recorded: {ex.Message}");
            }
        }

        private string GetCommandDescription(Command command)
        {
            switch (command.Type)
            {
                case CommandType.LoopStart:
                    return $"Loop Start (repeat {command.RepeatCount}x)";
                case CommandType.LoopEnd:
                    return "Loop End";
                case CommandType.Wait:
                    return $"Wait {command.Value}ms";
                case CommandType.KeyPress:
                    return $"Key Press: {command.Key}";
                case CommandType.Click:
                    return $"Click on {command.ElementName}";
                case CommandType.SetText:
                    return $"Set Text: '{command.Value}' in {command.ElementName}";
                default:
                    return $"{command.Type} - {command.ElementName}";
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
                        UpdateStatus(e.IsPaused ? "Recording paused" : $"Recording: {e.SequenceName}");
                    }
                    else
                    {
                        UpdateStatus("Recording stopped");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error handling recording state change: {ex.Message}");
            }
        }



        private void OnCommandExecuted(object sender, CommandPlayer.CommandExecutedEventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateStatus($"Executing {e.CommandIndex + 1}/{e.TotalCommands}: {GetCommandDescription(e.Command)}");

                    if (!e.Success)
                    {
                        Debug.WriteLine($"⚠️ Command failed: {e.ErrorMessage}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error handling command executed: {ex.Message}");
            }
        }

        private void OnPlaybackStateChanged(object sender, CommandPlayer.PlaybackStateChangedEventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateUI();
                    UpdateStatus($"Playback {e.State}: {e.SequenceName}");
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error handling playback state change: {ex.Message}");
            }
        }

        private void OnPlaybackCompleted(object sender, CommandPlayer.PlaybackCompletedEventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateUI();
                    UpdateStatus($"Playback completed: {e.Message}");

                    if (e.Success)
                    {
                        MessageBox.Show($"Playback completed successfully!\n\nCommands executed: {e.CommandsExecuted}/{e.TotalCommands}",
                                        "Playback Completed", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error handling playback completed: {ex.Message}");
            }
        }

        private void OnPlaybackError(object sender, CommandPlayer.PlaybackErrorEventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateStatus($"Playback error: {e.ErrorMessage}");
                    ShowErrorMessage($"Error during playback at command {e.CommandIndex + 1}", new Exception(e.ErrorMessage));
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error handling playback error: {ex.Message}");
            }
        }


        // NOVÉ: Window tracking event handlers
        private void OnNewWindowDetected(object sender, NewWindowDetectedEventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    if (_isAutoTrackingEnabled && _recorder.IsRecording)
                    {
                        Debug.WriteLine($"🆕 Auto-detected new window during recording: {e.WindowTitle}");

                        // Automaticky pridaj okno do sledovania ak je relevantné
                        if (IsRelevantWindow(e))
                        {
                            _automaticUIManager.AddWindowToTracking(e.WindowHandle, WindowTrackingPriority.High);
                            UpdateStatus($"Auto-tracking new window: {e.WindowTitle}");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error handling new window detected: {ex.Message}");
            }
        }

        private void OnWindowActivated(object sender, WindowActivatedEventArgs e)
        {
            try
            {
                if (_recorder.IsRecording && _isAutoTrackingEnabled)
                {
                    Debug.WriteLine($"🎯 Window activated during recording: {e.WindowTitle}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error handling window activated: {ex.Message}");
            }
        }

        private void OnWindowClosed(object sender, WindowClosedEventArgs e)
        {
            try
            {
                if (_recorder.IsRecording)
                {
                    Debug.WriteLine($"🗑️ Window closed during recording: {e.WindowTitle}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error handling window closed: {ex.Message}");
            }
        }

        private void OnUIChangeDetected(object sender, UIChangeDetectedEventArgs e)
        {
            try
            {
                if (_recorder.IsRecording)
                {
                    Debug.WriteLine($"🔄 UI changes detected: {e.Changes.AddedElements.Count} added, {e.Changes.RemovedElements.Count} removed");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error handling UI change detected: {ex.Message}");
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
                        UpdateStatus($"New window appeared: {e.WindowTitle}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error handling new window appeared: {ex.Message}");
            }
        }

        private bool IsRelevantWindow(NewWindowDetectedEventArgs e)
        {
            // Relevantné sú dialógy, message boxy a okná z target procesu
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
                _recorder?.Dispose();
                _player?.Dispose();
                _windowTracker?.Dispose();
                _automaticUIManager?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error during window closing: {ex.Message}");
            }
        }

        #endregion

        #region Button Click Handlers - Recording

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

                var sequenceName = $"Recording_{DateTime.Now:yyyyMMdd_HHmmss}";

                // Štart recording
                _recorder.StartRecording(sequenceName, _targetWindowHandle);

                // Štart window tracking pre automatickú detekciu nových okien
                string targetProcess = GetProcessNameFromWindow(_targetWindowHandle);
                _windowTracker.StartTracking(targetProcess);

                // Štart automatic UI monitoring
                _automaticUIManager.StartMonitoring(_targetWindowHandle, targetProcess);

                UpdateStatus($"Recording started: {sequenceName} (Target: {targetProcess})");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error starting recording", ex);
            }
        }

        private void SelectTarget_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new WindowSelectorDialog();
                if (dialog.ShowDialog() == true && dialog.SelectedWindow != null)
                {
                    _targetWindowHandle = dialog.SelectedWindow.WindowHandle;
                    txtTargetInfo.Text = $"{dialog.SelectedWindow.ProcessName} - {dialog.SelectedWindow.Title}";

                    UpdateUI();

                    UpdateStatus($"Target selected: {dialog.SelectedWindow.ProcessName}");

                    // Debug informácie
                    System.Diagnostics.Debug.WriteLine($"✅ Target window selected:");
                    System.Diagnostics.Debug.WriteLine($"   Handle: 0x{_targetWindowHandle:X8}");
                    System.Diagnostics.Debug.WriteLine($"   Process: {dialog.SelectedWindow.ProcessName}");
                    System.Diagnostics.Debug.WriteLine($"   Title: {dialog.SelectedWindow.Title}");
                    System.Diagnostics.Debug.WriteLine($"   IsZero: {_targetWindowHandle == IntPtr.Zero}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("❌ No window selected or dialog cancelled");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error selecting target window", ex);
            }
        }

        #endregion

        #region Button Click Handlers - Playback

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

                // ROZŠÍRENÉ: Kontrola loop integrity pred playback
                var loopValidation = ValidateLoopIntegrity();
                if (!loopValidation.IsValid)
                {
                    var result = MessageBox.Show($"Loop validation warning:\n{loopValidation.Message}\n\nDo you want to continue anyway?",
                                               "Loop Validation", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (result == MessageBoxResult.No)
                        return;
                }

                // Get repeat count
                int repeatCount = 1;
                if (!int.TryParse(txtRepeatCount.Text, out repeatCount) || repeatCount < 1)
                {
                    repeatCount = 1;
                    txtRepeatCount.Text = "1";
                }

                // Create sequence
                var sequence = new CommandSequence
                {
                    Name = $"Playback_{DateTime.Now:HHmmss}",
                    Commands = _commands.ToList(),
                    TargetApplication = GetProcessNameFromWindow(_targetWindowHandle),
                    TargetProcessName = GetProcessNameFromWindow(_targetWindowHandle),
                    TargetWindowTitle = GetWindowTitle(_targetWindowHandle)
                };

                _player.PlaySequence(sequence, repeatCount);
                UpdateStatus($"Starting playback ({repeatCount}x) - {_commands.Count} commands");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error starting playback", ex);
            }
        }

        private (bool IsValid, string Message) ValidateLoopIntegrity()
        {
            var loopStarts = _commands.Where(c => c.Type == CommandType.LoopStart).Count();
            var loopEnds = _commands.Where(c => c.Type == CommandType.LoopEnd).Count();

            if (loopStarts != loopEnds)
            {
                return (false, $"Loop mismatch: {loopStarts} loop starts, {loopEnds} loop ends");
            }

            if (loopStarts > 0)
            {
                return (true, $"Loop validation passed: {loopStarts} complete loops detected");
            }

            return (true, "No loops detected");
        }

        private void PausePlayback_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_player.IsPaused)
                {
                    _player.Resume();
                    btnPause.Content = "⏸ Pause";
                }
                else if (_player.IsPlaying)
                {
                    _player.Pause();
                    btnPause.Content = "▶ Resume";
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
                btnPause.Content = "⏸ Pause";
                UpdateStatus("Playback stopped");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error stopping playback", ex);
            }
        }

        #endregion

        #region Menu Handlers - File (ZACHOVANÁ FUNKCIONALITA)

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
                    FileName = $"Sequence_{DateTime.Now:yyyyMMdd_HHmmss}.acc"
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

        #region Menu Handlers - Commands (ROZŠÍRENÉ LOOP FUNKCIE)

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
                    UpdateStatus($"Wait command added: {waitTime}ms");
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
                    // Vytvor loop start command s repeat count
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
                    UpdateStatus($"Loop start added: repeat {repeatCount}x");
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

                // Skontroluj či existuje otvorený loop
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
                    Value = lastLoopStart?.Value ?? "1",
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

                    // Špeciálne spracovanie pre loop commands
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

                var result = MessageBox.Show($"Are you sure you want to delete this command?\n\n{selectedCommand.Type}: {selectedCommand.ElementName}",
                                           "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);

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

        private void ClearCommands_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_commands.Any())
                {
                    MessageBox.Show("No commands to clear.", "No Commands",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show($"Are you sure you want to delete all {_commands.Count} commands?",
                                           "Confirm Clear All", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _commands.Clear();
                    _hasUnsavedChanges = true;
                    UpdateUI();
                    UpdateStatus("All commands cleared");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error clearing commands", ex);
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

        #region File Operations (ZACHOVANÁ FUNKCIONALITA)

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

                if (sequence?.Commands != null)
                {
                    _commands.Clear();
                    foreach (var command in sequence.Commands)
                    {
                        _commands.Add(command);
                    }

                    _currentFilePath = filePath;
                    _hasUnsavedChanges = false;
                    UpdateUI();

                    // ROZŠÍRENÉ: Zobraz informácie o načítanej sequence
                    var loopInfo = GetSequenceLoopInfo(sequence);
                    UpdateStatus($"Sequence loaded: {Path.GetFileName(filePath)} ({_commands.Count} commands{loopInfo})");
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
            var loopEnds = sequence.Commands.Count(c => c.Type == CommandType.LoopEnd);

            if (loopStarts > 0)
            {
                return $", {loopStarts} loops";
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
                UpdateStatus($"Sequence saved: {Path.GetFileName(filePath)}");
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
                // Update button states
                bool isRecording = _recorder?.IsRecording == true;
                bool isPlaying = _player?.IsPlaying == true;

                btnRecord.Content = isRecording ? "⏹ Stop Recording" : "🔴 Record";

                // PRIDANÉ: Debug informácie
                bool hasTargetWindow = _targetWindowHandle != IntPtr.Zero;
                bool shouldEnableRecord = hasTargetWindow || isRecording;

                System.Diagnostics.Debug.WriteLine($"=== UpdateUI Debug ===");
                System.Diagnostics.Debug.WriteLine($"Target Handle: 0x{_targetWindowHandle:X8}");
                System.Diagnostics.Debug.WriteLine($"Has Target Window: {hasTargetWindow}");
                System.Diagnostics.Debug.WriteLine($"Is Recording: {isRecording}");
                System.Diagnostics.Debug.WriteLine($"Should Enable Record: {shouldEnableRecord}");

                btnRecord.IsEnabled = shouldEnableRecord;

                System.Diagnostics.Debug.WriteLine($"btnRecord.IsEnabled set to: {btnRecord.IsEnabled}");
                System.Diagnostics.Debug.WriteLine($"====================");

                btnPlay.IsEnabled = _commands.Any() && !isRecording && !isPlaying;
                btnPause.IsEnabled = isPlaying;
                btnStop.IsEnabled = isPlaying;

                // Update command count with loop info
                var loopCount = _commands.Count(c => c.Type == CommandType.LoopStart);
                string commandText = loopCount > 0 ?
                    $"Commands: {_commands.Count} ({loopCount} loops)" :
                    $"Commands: {_commands.Count}";
                lblCommandCount.Text = commandText;

                // Update window title
                string title = "AppCommander";
                if (!string.IsNullOrEmpty(_currentFilePath))
                {
                    title += $" - {Path.GetFileName(_currentFilePath)}";
                }
                if (_hasUnsavedChanges)
                {
                    title += " *";
                }
                this.Title = title;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error updating UI: {ex.Message}");
            }
        }

        private void UpdateStatus(string message)
        {
            try
            {
                lblStatus.Text = $"{DateTime.Now:HH:mm:ss} - {message}";
                Debug.WriteLine($"📊 Status: {message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error updating status: {ex.Message}");
            }
        }

        private void UpdateRecordingStatus(bool isRecording, bool isPaused)
        {
            try
            {
                if (isRecording)
                {
                    lblRecordingStatus.Text = isPaused ? "⏸️ Recording Paused" : "🔴 Recording";
                }
                else
                {
                    lblRecordingStatus.Text = "⚫ Not Recording";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error updating recording status: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

        private void ShowErrorMessage(string title, Exception ex)
        {
            var message = $"{title}\n\nError: {ex.Message}";
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Debug.WriteLine($"❌ {title}: {ex.Message}");
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

        // Simple input dialog helper
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
