using AppCommander.W7_11.WPF.Core;
using Microsoft.Win32; // WPF dialógy
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommandType = AppCommander.W7_11.WPF.Core.CommandType;

namespace AppCommander.W7_11.WPF
{
    public partial class MainWindow : Window
    {
        private readonly CommandRecorder recorder;
        private readonly CommandPlayer player;
        private readonly ObservableCollection<Command> commands;
        private readonly ObservableCollection<ElementUsageStats> elementStatsList;

        private IntPtr targetWindowHandle = IntPtr.Zero;
        private string currentFilePath = string.Empty;
        private bool hasUnsavedChanges = false;

        public MainWindow()
        {
            InitializeComponent();

            // Initialize components
            recorder = new CommandRecorder();
            player = new CommandPlayer();
            commands = new ObservableCollection<Command>();
            elementStatsList = new ObservableCollection<ElementUsageStats>();

            // Setup data bindings
            dgCommands.ItemsSource = commands;
            lstElementStats.ItemsSource = elementStatsList;

            // Subscribe to events
            SubscribeToEvents();

            // Initialize UI state
            UpdateUI();
            LogMessage("AppCommander initialized and ready.");
        }

        private void SubscribeToEvents()
        {
            // Recorder events
            recorder.CommandRecorded += OnCommandRecorded;
            recorder.RecordingStateChanged += OnRecordingStateChanged;
            recorder.ElementUsageUpdated += OnElementUsageUpdated;

            // Player events
            player.CommandExecuted += OnCommandExecuted;
            player.PlaybackStateChanged += OnPlaybackStateChanged;
            player.PlaybackError += OnPlaybackError;
            player.PlaybackCompleted += OnPlaybackCompleted;

            // Window events
            this.Closing += MainWindow_Closing;
        }

        #region Recording Events

        private void OnCommandRecorded(object sender, CommandRecordedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                commands.Add(e.Command);
                hasUnsavedChanges = true;
                UpdateUI();
                LogMessage($"Recorded: {e.Command}");
            });
        }

        private void OnRecordingStateChanged(object sender, RecordingStateChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (e.IsRecording)
                {
                    txtStatus.Text = e.IsPaused ? "Recording Paused" : "Recording...";
                    statusRecording.Text = e.IsPaused ? "Paused" : "Recording";
                    btnStartRecording.IsEnabled = false;
                    btnStopRecording.IsEnabled = true;
                    btnPauseRecording.IsEnabled = !e.IsPaused;
                    btnPauseRecording.Content = e.IsPaused ? "⏵ Resume" : "⏸ Pause";
                }
                else
                {
                    txtStatus.Text = "Ready";
                    statusRecording.Text = "Not Recording";
                    btnStartRecording.IsEnabled = true;
                    btnStopRecording.IsEnabled = false;
                    btnPauseRecording.IsEnabled = false;
                    btnPauseRecording.Content = "⏸ Pause";
                }

                UpdateStatusBar();
                LogMessage($"Recording state changed: {(e.IsRecording ? "Started" : "Stopped")}");
            });
        }

        private void OnElementUsageUpdated(object sender, ElementUsageEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                var existing = elementStatsList.FirstOrDefault(x => x.ElementName == e.ElementName);
                if (existing != null)
                {
                    elementStatsList.Remove(existing);
                }
                elementStatsList.Add(e.Stats);
            });
        }

        #endregion

        #region Playback Events

        private void OnCommandExecuted(object sender, CommandExecutedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                // Update progress
                progressPlayback.Value = (double)e.CommandIndex / e.TotalCommands * 100;

                // Highlight current command in grid
                if (e.CommandIndex < commands.Count)
                {
                    dgCommands.SelectedIndex = e.CommandIndex;
                    dgCommands.ScrollIntoView(commands[e.CommandIndex]);
                }

                string status = e.Success ? "✓" : "✗";
                LogMessage($"{status} Step {e.CommandIndex + 1}: {e.Command.Type} on {e.Command.ElementName}");

                if (!e.Success && !string.IsNullOrEmpty(e.ErrorMessage))
                {
                    LogMessage($"   Error: {e.ErrorMessage}");
                }
            });
        }

        private void OnPlaybackStateChanged(object sender, PlaybackStateChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                switch (e.State)
                {
                    case PlaybackState.Started:
                        txtStatus.Text = "Playing...";
                        btnPlay.IsEnabled = false;
                        btnPause.IsEnabled = true;
                        btnStop.IsEnabled = true;
                        progressPlayback.Visibility = Visibility.Visible;
                        progressPlayback.Value = 0;
                        break;

                    case PlaybackState.Paused:
                        txtStatus.Text = "Playback Paused";
                        btnPlay.Content = "▶ Resume";
                        btnPlay.IsEnabled = true;
                        btnPause.IsEnabled = false;
                        break;

                    case PlaybackState.Resumed:
                        txtStatus.Text = "Playing...";
                        btnPlay.Content = "▶ Play";
                        btnPlay.IsEnabled = false;
                        btnPause.IsEnabled = true;
                        break;

                    case PlaybackState.Stopped:
                        txtStatus.Text = "Ready";
                        btnPlay.Content = "▶ Play";
                        btnPlay.IsEnabled = true;
                        btnPause.IsEnabled = false;
                        btnStop.IsEnabled = false;
                        progressPlayback.Visibility = Visibility.Collapsed;
                        dgCommands.SelectedIndex = -1;
                        break;
                }

                LogMessage($"Playback {e.State.ToString().ToLower()}");
            });
        }

        private void OnPlaybackError(object sender, PlaybackErrorEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                LogMessage($"❌ Playback error at step {e.CommandIndex + 1}: {e.ErrorMessage}");
                MessageBox.Show($"Error executing command at step {e.CommandIndex + 1}:\n\n{e.ErrorMessage}",
                               "Playback Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            });
        }

        private void OnPlaybackCompleted(object sender, PlaybackCompletedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                string message = e.Success
                    ? $"✅ Sequence completed successfully. Executed {e.CommandsExecuted} commands."
                    : $"❌ Sequence stopped: {e.Message}";

                LogMessage(message);

                if (!e.Success)
                {
                    MessageBox.Show(e.Message, "Playback Completed",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                }
            });
        }

        #endregion

        #region Button Event Handlers

        private void StartRecording_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string sequenceName = string.IsNullOrWhiteSpace(txtSequenceName.Text)
                    ? "New Sequence"
                    : txtSequenceName.Text.Trim();

                LogMessage($"Starting recording with sequence name: {sequenceName}");
                LogMessage($"Target window handle: {targetWindowHandle}");

                // Clear existing commands if starting new recording
                if (commands.Count > 0)
                {
                    var result = MessageBox.Show(
                        "Starting a new recording will clear the current command sequence. Continue?",
                        "Clear Current Sequence",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.No)
                        return;

                    commands.Clear();
                    elementStatsList.Clear();
                }

                recorder.StartRecording(sequenceName, targetWindowHandle);
                LogMessage($"Started recording sequence: {sequenceName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start recording: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
                LogMessage($"Error starting recording: {ex.Message}");
            }
        }

        private void StopRecording_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                recorder.StopRecording();
                LogMessage("Recording stopped.");
            }
            catch (Exception ex)
            {
                LogMessage($"Error stopping recording: {ex.Message}");
            }
        }

        private void PauseRecording_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (recorder.IsRecording)
                {
                    recorder.PauseRecording();
                }
                else
                {
                    recorder.ResumeRecording();
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error pausing/resuming recording: {ex.Message}");
            }
        }

        private async void PlaySequence_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogMessage("PlaySequence_Click started");

                if (commands.Count == 0)
                {
                    MessageBox.Show("No commands to play. Please record a sequence first.",
                                   "No Commands", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                LogMessage($"Found {commands.Count} commands to play");

                if (player.IsPaused)
                {
                    LogMessage("Resuming paused playback");
                    player.ResumePlayback();
                }
                else
                {
                    LogMessage("Creating new command sequence");
                    var sequence = new CommandSequence(txtSequenceName.Text.Trim());

                    // Copy target info if we have it
                    if (targetWindowHandle != IntPtr.Zero)
                    {
                        try
                        {
                            var targetInfo = ExtractWindowInfo(targetWindowHandle);
                            sequence.TargetProcessName = targetInfo.ProcessName;
                            sequence.TargetWindowTitle = targetInfo.WindowTitle;
                            sequence.TargetWindowClass = targetInfo.WindowClass;
                            sequence.TargetApplication = targetInfo.ProcessName;
                            sequence.AutoFindTarget = true;
                            sequence.MaxWaitTimeSeconds = 30;

                            LogMessage($"Using target: {targetInfo.ProcessName} - {targetInfo.WindowTitle}");
                        }
                        catch (Exception ex)
                        {
                            LogMessage($"Could not extract target window info: {ex.Message}");
                        }
                    }

                    LogMessage("Adding commands to sequence");
                    foreach (var cmd in commands)
                    {
                        sequence.AddCommand(cmd);
                    }

                    LogMessage($"Sequence created with {sequence.Commands.Count} commands");
                    LogMessage($"Target window handle: {targetWindowHandle}");
                    LogMessage($"Target process: '{sequence.TargetProcessName}'");

                    LogMessage("Starting playback...");
                    await player.PlaySequenceAsync(sequence, targetWindowHandle);
                    LogMessage("Playback completed successfully");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"DETAILED ERROR: {ex.GetType().Name}: {ex.Message}");
                LogMessage($"Stack trace: {ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    LogMessage($"Inner exception: {ex.InnerException.Message}");
                }

                MessageBox.Show($"Failed to play sequence: {ex.Message}\n\nSee Activity Log for details.", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PausePlayback_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                player.PausePlayback();
            }
            catch (Exception ex)
            {
                LogMessage($"Error pausing playback: {ex.Message}");
            }
        }

        private void StopPlayback_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                player.StopPlayback();
            }
            catch (Exception ex)
            {
                LogMessage($"Error stopping playback: {ex.Message}");
            }
        }

        private void SelectTarget_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new WindowSelectorDialog
                {
                    Owner = this
                };

                if (dialog.ShowDialog() == true && dialog.SelectedWindow != null)
                {
                    var selected = dialog.SelectedWindow;
                    targetWindowHandle = selected.Handle;

                    // Update UI to show selected target
                    txtTarget.Text = $"{selected.ProcessName}: {selected.Title}";
                    statusTarget.Text = $"Target: {selected.ProcessName}";

                    LogMessage($"Selected target: {selected.ProcessName} - {selected.Title} (Handle: 0x{selected.Handle.ToString("X8")})");

                    UpdateUI();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error selecting target window: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
                LogMessage($"Error selecting target: {ex.Message}");
            }
        }

        #endregion

        #region Menu Event Handlers

        private void NewSequence_Click(object sender, RoutedEventArgs e)
        {
            if (hasUnsavedChanges)
            {
                var result = MessageBox.Show("You have unsaved changes. Save current sequence?",
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

            commands.Clear();
            elementStatsList.Clear();
            txtSequenceName.Text = "New Sequence";
            currentFilePath = string.Empty;
            hasUnsavedChanges = false;
            UpdateUI();
            LogMessage("New sequence created.");
        }

        private void OpenSequence_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "AppCommander Files (*.apc)|*.apc|All Files (*.*)|*.*",
                Title = "Open Command Sequence"
            };

            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                try
                {
                    var sequence = CommandSequence.LoadFromFile(dialog.FileName);
                    if (sequence != null)
                    {
                        commands.Clear();
                        foreach (var cmd in sequence.Commands)
                        {
                            commands.Add(cmd);
                        }

                        txtSequenceName.Text = sequence.Name;
                        currentFilePath = dialog.FileName;
                        hasUnsavedChanges = false;

                        // Update target info display if available
                        if (!string.IsNullOrEmpty(sequence.TargetProcessName))
                        {
                            txtTarget.Text = $"{sequence.TargetProcessName}: {sequence.TargetWindowTitle}";
                            statusTarget.Text = $"Target: {sequence.TargetProcessName}";

                            // Try to find the target window automatically
                            var searchResult = WindowFinder.SmartFindWindow(
                                sequence.TargetProcessName,
                                sequence.TargetWindowTitle,
                                sequence.TargetWindowClass);

                            if (searchResult.IsValid)
                            {
                                targetWindowHandle = searchResult.Handle;
                                txtTarget.Text += $" (Found)";
                                statusTarget.Text += " ✓";
                                LogMessage($"Auto-found target window: {sequence.TargetProcessName} (Handle: 0x{searchResult.Handle.ToString("X8")})");
                            }
                            else
                            {
                                targetWindowHandle = IntPtr.Zero;
                                txtTarget.Text += $" (Not Found)";
                                statusTarget.Text += " ✗";
                                LogMessage($"Target application not running: {sequence.TargetProcessName}");
                            }
                        }
                        else
                        {
                            txtTarget.Text = "No target specified";
                            statusTarget.Text = "No Target";
                            targetWindowHandle = IntPtr.Zero;
                        }

                        UpdateUI();
                        LogMessage($"Loaded sequence: {sequence.Name} ({sequence.Commands.Count} commands)");
                        LogMessage($"Target info: Process='{sequence.TargetProcessName}', Title='{sequence.TargetWindowTitle}'");
                    }
                    else
                    {
                        MessageBox.Show("Failed to load sequence file.", "Error",
                                       MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading sequence: {ex.Message}", "Error",
                                   MessageBoxButton.OK, MessageBoxImage.Error);
                    LogMessage($"Error loading sequence: {ex.Message}");
                }
            }
        }

        private void SaveSequence_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentFilePath))
            {
                SaveSequenceAs_Click(sender, e);
            }
            else
            {
                SaveSequenceToFile(currentFilePath);
            }
        }

        private void SaveSequenceAs_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "AppCommander Files (*.apc)|*.apc|All Files (*.*)|*.*",
                Title = "Save Command Sequence",
                FileName = txtSequenceName.Text + ".apc"
            };

            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                SaveSequenceToFile(dialog.FileName);
                currentFilePath = dialog.FileName;
            }
        }

        private void SaveSequenceToFile(string filePath)
        {
            try
            {
                var sequence = new CommandSequence(txtSequenceName.Text.Trim())
                {
                    Description = $"Created with AppCommander on {DateTime.Now:yyyy-MM-dd HH:mm:ss}"
                };

                // Copy target information from recorder if available
                if (recorder.CurrentSequence != null)
                {
                    sequence.TargetProcessName = recorder.CurrentSequence.TargetProcessName;
                    sequence.TargetWindowTitle = recorder.CurrentSequence.TargetWindowTitle;
                    sequence.TargetWindowClass = recorder.CurrentSequence.TargetWindowClass;
                    sequence.TargetApplication = recorder.CurrentSequence.TargetApplication;
                    sequence.AutoFindTarget = recorder.CurrentSequence.AutoFindTarget;
                    sequence.MaxWaitTimeSeconds = recorder.CurrentSequence.MaxWaitTimeSeconds;
                }
                else if (targetWindowHandle != IntPtr.Zero)
                {
                    // Fallback: extract info from current target window
                    try
                    {
                        var targetInfo = ExtractWindowInfo(targetWindowHandle);
                        sequence.TargetProcessName = targetInfo.ProcessName;
                        sequence.TargetWindowTitle = targetInfo.WindowTitle;
                        sequence.TargetWindowClass = targetInfo.WindowClass;
                        sequence.TargetApplication = targetInfo.ProcessName;
                        sequence.AutoFindTarget = true;
                        sequence.MaxWaitTimeSeconds = 30;

                        LogMessage($"Extracted target info: {targetInfo.ProcessName} - {targetInfo.WindowTitle}");
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Could not extract target window info: {ex.Message}");
                    }
                }

                foreach (var cmd in commands)
                {
                    sequence.AddCommand(cmd);
                }

                sequence.SaveToFile(filePath);
                hasUnsavedChanges = false;
                UpdateUI();
                LogMessage($"Sequence saved: {filePath}");
                LogMessage($"Target info saved: Process='{sequence.TargetProcessName}', Title='{sequence.TargetWindowTitle}'");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving sequence: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
                LogMessage($"Error saving sequence: {ex.Message}");
            }
        }

        private (string ProcessName, string WindowTitle, string WindowClass) ExtractWindowInfo(IntPtr handle)
        {
            string processName = "";
            string windowTitle = "";
            string windowClass = "";

            try
            {
                // Get process name
                GetWindowThreadProcessId(handle, out uint processId);
                using (var process = System.Diagnostics.Process.GetProcessById((int)processId))
                {
                    processName = process.ProcessName;
                }

                // Get window title
                var titleBuffer = new System.Text.StringBuilder(256);
                GetWindowText(handle, titleBuffer, titleBuffer.Capacity);
                windowTitle = titleBuffer.ToString();

                // Get window class
                var classBuffer = new System.Text.StringBuilder(256);
                GetClassName(handle, classBuffer, classBuffer.Capacity);
                windowClass = classBuffer.ToString();
            }
            catch (Exception ex)
            {
                LogMessage($"Error extracting window info: {ex.Message}");
            }

            return (processName, windowTitle, windowClass);
        }

        // Windows API
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void WindowSelector_Click(object sender, RoutedEventArgs e)
        {
            SelectTarget_Click(sender, e);
        }

        private void ElementInspector_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Element inspector tool will be implemented.", "Coming Soon",
                           MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Settings dialog will be implemented.", "Coming Soon",
                           MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            var aboutText = $@"AppCommander v1.0.0

Automation tool for interacting with text fields and buttons in other applications.

Features:
• Record keyboard and mouse actions
• Play back recorded sequences
• Support for loops and delays
• Universal command file format
• Windows 7-11 compatibility

© 2025 Rudolf Mendzezof
Licensed under Apache License 2.0";

            MessageBox.Show(aboutText, "About AppCommander",
                           MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void UserGuide_Click(object sender, RoutedEventArgs e)
        {
            var guideText = @"Quick Start Guide:

1. RECORDING:
   - Click 'Select Target Window' to choose application
   - Click 'Start Recording' 
   - Perform actions in target application
   - Click 'Stop Recording' when done

2. PLAYBACK:
   - Click 'Play' to execute recorded sequence
   - Use 'Pause' and 'Stop' to control playback

3. EDITING:
   - Right-click commands to edit or delete
   - Add loops and delays from context menu

4. SAVING:
   - Use File > Save to store sequences
   - Files use .apc extension

For detailed documentation, visit the project repository.";

            MessageBox.Show(guideText, "User Guide",
                           MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Context Menu Event Handlers

        private void EditCommand_Click(object sender, RoutedEventArgs e)
        {
            var selectedCommand = dgCommands.SelectedItem as Command;
            if (selectedCommand != null)
            {
                MessageBox.Show("Command editing dialog will be implemented.", "Coming Soon",
                               MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void DeleteCommand_Click(object sender, RoutedEventArgs e)
        {
            var selectedCommand = dgCommands.SelectedItem as Command;
            if (selectedCommand != null)
            {
                var result = MessageBox.Show($"Delete command: {selectedCommand}?", "Confirm Delete",
                                           MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    commands.Remove(selectedCommand);
                    hasUnsavedChanges = true;
                    UpdateUI();
                    LogMessage($"Deleted command: {selectedCommand}");
                }
            }
        }

        private void AddWaitCommand_Click(object sender, RoutedEventArgs e)
        {
            string input = ShowInputDialog("Enter wait time in milliseconds:", "Add Wait Command", "1000");

            if (!string.IsNullOrEmpty(input) && int.TryParse(input, out int waitTime))
            {
                var waitCommand = new Command(GetNextStepNumber(), $"Wait_{waitTime}ms", CommandType.Wait)
                {
                    Value = waitTime.ToString(),
                    RepeatCount = 1
                };

                commands.Add(waitCommand);
                hasUnsavedChanges = true;
                UpdateUI();
                LogMessage($"Added wait command: {waitTime}ms");
            }
        }

        private void AddLoopStart_Click(object sender, RoutedEventArgs e)
        {
            string input = ShowInputDialog("Enter number of iterations:", "Add Loop Start", "3");

            if (!string.IsNullOrEmpty(input) && int.TryParse(input, out int iterations))
            {
                var loopCommand = new Command(GetNextStepNumber(), $"Loop_{iterations}x", CommandType.Loop)
                {
                    RepeatCount = iterations,
                    IsLoopStart = true,
                    Value = iterations.ToString()
                };

                commands.Add(loopCommand);
                hasUnsavedChanges = true;
                UpdateUI();
                LogMessage($"Added loop start: {iterations} iterations");
            }
        }

        private void AddLoopEnd_Click(object sender, RoutedEventArgs e)
        {
            var loopEndCommand = new Command(GetNextStepNumber(), "LoopEnd", CommandType.LoopEnd);
            commands.Add(loopEndCommand);
            hasUnsavedChanges = true;
            UpdateUI();
            LogMessage("Added loop end command");
        }

        #endregion

        #region Other Event Handlers

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            txtLog.Clear();
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (hasUnsavedChanges)
            {
                var result = MessageBox.Show("You have unsaved changes. Save before closing?",
                                           "Unsaved Changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    SaveSequence_Click(this, new RoutedEventArgs());
                    if (hasUnsavedChanges)
                    {
                        e.Cancel = true;
                        return;
                    }
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
            }

            recorder?.StopRecording();
            player?.StopPlayback();
        }

        #endregion

        #region Helper Methods

        private void UpdateUI()
        {
            string title = "AppCommander - Automation Tool";
            if (!string.IsNullOrEmpty(txtSequenceName.Text))
            {
                title += $" - {txtSequenceName.Text}";
                if (hasUnsavedChanges)
                    title += "*";
            }
            this.Title = title;

            UpdateStatusBar();
            btnPlay.IsEnabled = commands.Count > 0 && !player.IsPlaying;
        }

        private void UpdateStatusBar()
        {
            statusCommands.Text = $"Commands: {commands.Count}";
            statusRecording.Text = recorder.IsRecording ? "Recording" : "Not Recording";
            statusTarget.Text = targetWindowHandle != IntPtr.Zero ? "Target Selected" : "No Target";
        }

        private void LogMessage(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string logEntry = $"[{timestamp}] {message}\n";

            txtLog.AppendText(logEntry);
            txtLog.ScrollToEnd();
        }

        private int GetNextStepNumber()
        {
            return commands.Count > 0 ? commands.Max(c => c.StepNumber) + 1 : 1;
        }

        private string ShowInputDialog(string question, string title, string defaultValue = "")
        {
            var inputDialog = new Window()
            {
                Title = title,
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var stackPanel = new StackPanel { Margin = new Thickness(10) };

            var label = new Label { Content = question };
            stackPanel.Children.Add(label);

            var textBox = new TextBox { Text = defaultValue, Margin = new Thickness(0, 5, 0, 10) };
            stackPanel.Children.Add(textBox);

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            var okButton = new Button { Content = "OK", Width = 75, Margin = new Thickness(0, 0, 10, 0) };
            var cancelButton = new Button { Content = "Cancel", Width = 75 };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            stackPanel.Children.Add(buttonPanel);

            inputDialog.Content = stackPanel;

            string result = null;
            okButton.Click += (sender, e) => { result = textBox.Text; inputDialog.DialogResult = true; };
            cancelButton.Click += (sender, e) => { inputDialog.DialogResult = false; };

            textBox.KeyDown += (sender, e) => { if (e.Key == Key.Enter) { result = textBox.Text; inputDialog.DialogResult = true; } };

            textBox.Focus();
            textBox.SelectAll();

            return inputDialog.ShowDialog() == true ? result : null;
        }

        #endregion
    }
}
