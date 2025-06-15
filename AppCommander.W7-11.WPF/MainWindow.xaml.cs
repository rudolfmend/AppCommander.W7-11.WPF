using AppCommander.W7_11.WPF.Core;
using Microsoft.Win32; // WPF dialógy
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommandType = AppCommander.W7_11.WPF.Core.CommandType;
using System.Diagnostics;

namespace AppCommander.W7_11.WPF
{
    public partial class MainWindow : Window
    {
        private readonly CommandRecorder recorder;
        private readonly CommandPlayer player;
        private readonly ObservableCollection<Command> commands;
        private readonly ObservableCollection<ElementUsageStats> elementStatsList;
        private readonly ActionSimulator actionSimulator;

        private IntPtr targetWindowHandle = IntPtr.Zero;
        private string currentFilePath = string.Empty;
        private bool hasUnsavedChanges = false;

        // **WinUI3 support properties**
        private WinUI3ApplicationAnalysis currentWinUI3Analysis;
        private bool isWinUI3Application = false;

        public MainWindow()
        {
            InitializeComponent();

            // **VYLEPŠENÁ inicializácia s WinUI3 podporou**
            recorder = new CommandRecorder();
            recorder.EnableWinUI3Analysis = true;
            recorder.EnableDetailedLogging = true;

            player = new CommandPlayer();
            player.PreferElementIdentifiers = true;  // **Preferuj elementy pred súradnicami**
            player.EnableAdaptiveFinding = true;     // **Povoľ adaptívne vyhľadávanie**
            player.MaxElementSearchAttempts = 3;     // **Max počet pokusov**

            commands = new ObservableCollection<Command>();
            elementStatsList = new ObservableCollection<ElementUsageStats>();
            actionSimulator = new ActionSimulator();

            // Setup data bindings
            dgCommands.ItemsSource = commands;
            lstElementStats.ItemsSource = elementStatsList;

            // Subscribe to events
            SubscribeToEvents();

            // Initialize UI state
            UpdateUI();
            System.Diagnostics.Debug.WriteLine("AppCommander initialized with WinUI3 support.");
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

            // Window events - LAMBDA RIEŠENIE
            this.Closing += (s, e) =>
            {
                try
                {
                    if (hasUnsavedChanges)
                    {
                        var result = MessageBox.Show("You have unsaved changes. Save before closing?",
                                                   "Unsaved Changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
                        {
                            SaveSequence_Click(this, new RoutedEventArgs());
                        }
                        else if (result == MessageBoxResult.Cancel)
                        {
                            e.Cancel = true;
                            return;
                        }
                    }

                    // Cleanup
                    if (recorder?.IsRecording == true)
                    {
                        recorder.StopRecording();
                    }

                    if (player?.IsPlaying == true)
                    {
                        player.StopPlayback();
                    }

                    System.Diagnostics.Debug.WriteLine("AppCommander shutting down...");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error during shutdown: {ex.Message}");
                }
            };
        }

        #region Recording Events

        private void OnCommandRecorded(object sender, CommandRecordedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                commands.Add(e.Command);
                hasUnsavedChanges = true;
                UpdateUI();

                // **Enhanced logging s WinUI3 info**
                string message = $"Recorded: {e.Command}";
                if (e.Command.IsWinUI3Element)
                {
                    message += " [WinUI3]";
                    if (e.Command.ElementConfidence > 0)
                    {
                        message += $" (confidence: {e.Command.ElementConfidence:F2})";
                    }
                }
                System.Diagnostics.Debug.WriteLine(message);
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
                System.Diagnostics.Debug.WriteLine($"Recording state changed: {(e.IsRecording ? "Started" : "Stopped")}");
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
                string message = $"{status} Step {e.CommandIndex + 1}: {e.Command.Type} on {e.Command.ElementName}";

                // **Pridaj method info ak je dostupný**
                if (!string.IsNullOrEmpty(e.Command.LastFoundMethod))
                {
                    message += $" [{e.Command.LastFoundMethod}]";
                }

                System.Diagnostics.Debug.WriteLine(message);

                if (!e.Success && !string.IsNullOrEmpty(e.ErrorMessage))
                {
                    System.Diagnostics.Debug.WriteLine($"   Error: {e.ErrorMessage}");
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

                System.Diagnostics.Debug.WriteLine($"Playback {e.State.ToString().ToLower()}");
            });
        }

        private void OnPlaybackCompleted(object sender, PlaybackCompletedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                string message = e.Success
                    ? $"✅ Sequence completed successfully. Executed {e.CommandsExecuted} commands."
                    : $"❌ Sequence stopped: {e.Message}";

                System.Diagnostics.Debug.WriteLine(message);

                if (!e.Success)
                {
                    MessageBox.Show(e.Message, "Playback Completed",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                }
            });
        }

        private void OnPlaybackError(object sender, PlaybackErrorEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                System.Diagnostics.Debug.WriteLine($"❌ PLAYBACK ERROR at step {e.CommandIndex + 1}:");
                System.Diagnostics.Debug.WriteLine($"   Error: {e.ErrorMessage}");

                if (e.Command != null)
                {
                    System.Diagnostics.Debug.WriteLine($"   Command: {e.Command.Type} on '{e.Command.ElementName}'");
                    System.Diagnostics.Debug.WriteLine($"   Position: ({e.Command.ElementX}, {e.Command.ElementY})");
                    System.Diagnostics.Debug.WriteLine($"   Element ID: '{e.Command.ElementId}'");
                    System.Diagnostics.Debug.WriteLine($"   Element Class: '{e.Command.ElementClass}'");

                    // **WinUI3 špecifické info**
                    if (e.Command.IsWinUI3Element)
                    {
                        System.Diagnostics.Debug.WriteLine($"   WinUI3 Element: Yes");
                        System.Diagnostics.Debug.WriteLine($"   Best Identifier: {e.Command.GetBestElementIdentifier()}");
                    }
                }

                string suggestions = GetErrorSuggestions(e.ErrorMessage);
                if (!string.IsNullOrEmpty(suggestions))
                {
                    System.Diagnostics.Debug.WriteLine($"   Suggestions: {suggestions}");
                }

                MessageBox.Show($"Error executing command at step {e.CommandIndex + 1}:\n\n{e.ErrorMessage}\n\n{suggestions}",
                               "Playback Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            });
        }

        private string GetErrorSuggestions(string errorMessage)
        {
            if (string.IsNullOrEmpty(errorMessage))
                return "";

            var lower = errorMessage.ToLower();

            if (lower.Contains("coordinates") && lower.Contains("outside"))
                return "Element may have moved. Try re-recording the sequence.";

            if (lower.Contains("could not find element"))
            {
                return isWinUI3Application ?
                    "WinUI3 element not found. UI may have changed or element needs better identifiers." :
                    "UI element not found. Ensure target application is in the same state as during recording.";
            }

            if (lower.Contains("window") && lower.Contains("not found"))
                return "Target application may not be running or window may be minimized.";

            if (lower.Contains("timeout") || lower.Contains("wait"))
                return "Operation timed out. Target application may be busy or unresponsive.";

            return "Check that target application is running and UI hasn't changed since recording.";
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

                System.Diagnostics.Debug.WriteLine($"Starting recording with sequence name: {sequenceName}");
                System.Diagnostics.Debug.WriteLine($"Target window handle: {targetWindowHandle}");

                // **Analyzuj target aplikáciu pred nahrávaním**
                if (targetWindowHandle != IntPtr.Zero)
                {
                    AnalyzeTargetApplication();
                }

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
                System.Diagnostics.Debug.WriteLine($"Started recording sequence: {sequenceName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start recording: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Error starting recording: {ex.Message}");
            }
        }

        private void StopRecording_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                recorder.StopRecording();

                // **Analyzuj nahraté WinUI3 elementy**
                if (recorder.CurrentSequence != null)
                {
                    recorder.AnalyzeRecordedWinUI3Elements();

                    // **Validuj sequence s WinUI3 podporou**
                    var validation = DebugTestHelper.ValidateSequenceWithWinUI3(recorder.CurrentSequence, targetWindowHandle);
                    LogValidationResults(validation);
                }

                System.Diagnostics.Debug.WriteLine("Recording stopped.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping recording: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"Error pausing/resuming recording: {ex.Message}");
            }
        }

        private async void PlaySequence_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== STARTING PLAYBACK ===");

                if (commands.Count == 0)
                {
                    MessageBox.Show("No commands to play. Please record a sequence first.",
                                   "No Commands", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"Found {commands.Count} commands to play");

                if (player.IsPaused)
                {
                    System.Diagnostics.Debug.WriteLine("Resuming paused playback");
                    player.ResumePlayback();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Creating new command sequence for playback");
                    var sequence = new CommandSequence(txtSequenceName.Text.Trim());

                    // **Pre-execution analýza**
                    if (targetWindowHandle != IntPtr.Zero)
                    {
                        player.AnalyzeCommandsBeforeExecution(sequence);

                        var validation = DebugTestHelper.ValidateSequenceWithWinUI3(sequence, targetWindowHandle);
                        if (!validation.IsValid)
                        {
                            var result = MessageBox.Show(
                                $"Validation found {validation.Errors.Count} errors and {validation.Warnings.Count} warnings.\n\n" +
                                "Continue with playback anyway?",
                                "Validation Issues",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Warning);

                            if (result == MessageBoxResult.No)
                            {
                                System.Diagnostics.Debug.WriteLine("Playback cancelled by user due to validation issues");
                                return;
                            }
                        }
                    }

                    // Detailné logovanie target info
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

                            System.Diagnostics.Debug.WriteLine($"Target window info extracted:");
                            System.Diagnostics.Debug.WriteLine($"  Process: '{targetInfo.ProcessName}'");
                            System.Diagnostics.Debug.WriteLine($"  Title: '{targetInfo.WindowTitle}'");
                            System.Diagnostics.Debug.WriteLine($"  Class: '{targetInfo.WindowClass}'");
                            System.Diagnostics.Debug.WriteLine($"  WinUI3 Application: {isWinUI3Application}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"WARNING: Could not extract target window info: {ex.Message}");
                        }
                    }

                    // Validate and log commands
                    System.Diagnostics.Debug.WriteLine("Command sequence details:");
                    for (int i = 0; i < commands.Count; i++)
                    {
                        var cmd = commands[i];
                        string cmdInfo = $"  {i + 1}. {cmd.Type} - '{cmd.ElementName}' at ({cmd.ElementX}, {cmd.ElementY})";

                        // **Pridaj WinUI3 info**
                        if (cmd.IsWinUI3Element)
                        {
                            cmdInfo += " [WinUI3]";
                            cmdInfo += $" ID:{cmd.GetBestElementIdentifier()}";
                        }

                        System.Diagnostics.Debug.WriteLine(cmdInfo);

                        // Validate command
                        if (cmd.Type == CommandType.Click || cmd.Type == CommandType.DoubleClick || cmd.Type == CommandType.RightClick)
                        {
                            if (cmd.ElementX <= 0 || cmd.ElementY <= 0)
                            {
                                System.Diagnostics.Debug.WriteLine($"    WARNING: Command {i + 1} has invalid coordinates");
                            }
                        }

                        sequence.AddCommand(cmd);
                    }

                    System.Diagnostics.Debug.WriteLine($"Sequence created successfully with {sequence.Commands.Count} commands");
                    System.Diagnostics.Debug.WriteLine("Starting playback...");
                    await player.PlaySequenceAsync(sequence, targetWindowHandle);
                    System.Diagnostics.Debug.WriteLine("Playback method completed");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"=== PLAYBACK ERROR ===");
                System.Diagnostics.Debug.WriteLine($"Error Type: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"Error Message: {ex.Message}");

                string userMessage = "Failed to play sequence.\n\n";
                userMessage += $"Error: {ex.Message}\n\n";
                userMessage += "Common solutions:\n";
                userMessage += "• Ensure target application is running\n";
                userMessage += "• Check that UI elements haven't changed\n";
                if (isWinUI3Application)
                {
                    userMessage += "• WinUI3 apps may need element re-identification\n";
                }
                userMessage += "• Try recording the sequence again\n";
                userMessage += "• Check Activity Log for detailed information";

                MessageBox.Show(userMessage, "Playback Error",
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
                System.Diagnostics.Debug.WriteLine($"Error pausing playback: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"Error stopping playback: {ex.Message}");
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
                    txtTarget.Text = $"{selected.ProcessName}: {selected.WindowTitle}";
                    statusTarget.Text = $"Target: {selected.ProcessName}";

                    System.Diagnostics.Debug.WriteLine($"Selected target: {selected.ProcessName} - {selected.WindowTitle} (Handle: 0x{selected.Handle.ToString("X8")})");

                    // **Analyzuj vybraté okno pre WinUI3**
                    AnalyzeTargetApplication();

                    UpdateUI();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error selecting target window: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Error selecting target: {ex.Message}");
            }
        }

        #endregion

        #region **WinUI3 Analysis Methods**

        /// <summary>
        /// Analyzuje target aplikáciu pre WinUI3 podporu
        /// </summary>
        private void AnalyzeTargetApplication()
        {
            try
            {
                if (targetWindowHandle == IntPtr.Zero)
                    return;

                System.Diagnostics.Debug.WriteLine("=== ANALYZING TARGET APPLICATION ===");

                // Základná analýza okna
                var targetInfo = ExtractWindowInfo(targetWindowHandle);
                System.Diagnostics.Debug.WriteLine($"Target: {targetInfo.ProcessName} - {targetInfo.WindowTitle}");
                System.Diagnostics.Debug.WriteLine($"Window Class: {targetInfo.WindowClass}");

                // **WinUI3 špecifická analýza**
                currentWinUI3Analysis = DebugTestHelper.AnalyzeWinUI3Application(targetWindowHandle);

                if (currentWinUI3Analysis.IsSuccessful)
                {
                    isWinUI3Application = currentWinUI3Analysis.BridgeCount > 0;

                    System.Diagnostics.Debug.WriteLine($"WinUI3 Analysis: {currentWinUI3Analysis.BridgeCount} bridges, {currentWinUI3Analysis.InteractiveElements.Count} interactive elements");

                    if (isWinUI3Application)
                    {
                        System.Diagnostics.Debug.WriteLine("✅ WinUI3 application detected - enhanced recording enabled");

                        // Update status
                        statusTarget.Text += " [WinUI3]";

                        // Log recommendations
                        foreach (var recommendation in currentWinUI3Analysis.Recommendations)
                        {
                            System.Diagnostics.Debug.WriteLine($"💡 {recommendation}");
                        }

                        // Log top interactive elements
                        var topElements = currentWinUI3Analysis.InteractiveElements.Take(5);
                        if (topElements.Any())
                        {
                            System.Diagnostics.Debug.WriteLine("📋 Top interactive elements:");
                            foreach (var element in topElements)
                            {
                                string id = !string.IsNullOrEmpty(element.AutomationId) ? $"ID:{element.AutomationId}" : "no-ID";
                                string text = !string.IsNullOrEmpty(element.Text) ? $"Text:'{element.Text}'" : "no-text";
                                System.Diagnostics.Debug.WriteLine($"   • {element.Name} ({element.ControlType}) [{id}] [{text}]");
                            }
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("📱 Standard Windows application detected");
                        isWinUI3Application = false;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Analysis failed: {currentWinUI3Analysis.ErrorMessage}");
                    isWinUI3Application = false;
                }

                System.Diagnostics.Debug.WriteLine("=== ANALYSIS COMPLETE ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Target analysis error: {ex.Message}");
                isWinUI3Application = false;
            }
        }

        /// <summary>
        /// Loguje validation výsledky
        /// </summary>
        private void LogValidationResults(AppCommander.W7_11.WPF.Core.ValidationResult validation)
        {
            System.Diagnostics.Debug.WriteLine($"=== VALIDATION RESULTS ===");
            System.Diagnostics.Debug.WriteLine($"Status: {(validation.IsValid ? "PASSED" : "FAILED")}");

            foreach (var error in validation.Errors)
            {
                System.Diagnostics.Debug.WriteLine($"✗ ERROR: {error}");
            }

            foreach (var warning in validation.Warnings)
            {
                System.Diagnostics.Debug.WriteLine($"⚠ WARNING: {warning}");
            }

            foreach (var info in validation.Info)
            {
                System.Diagnostics.Debug.WriteLine($"ℹ INFO: {info}");
            }

            if (validation.WinUI3CommandCount > 0)
            {
                System.Diagnostics.Debug.WriteLine($"WinUI3 commands: {validation.WinUI3CommandCount}");
            }

            if (validation.ElementsTotal > 0)
            {
                System.Diagnostics.Debug.WriteLine($"Element availability: {validation.ElementsFound}/{validation.ElementsTotal} found");
            }

            System.Diagnostics.Debug.WriteLine("=== VALIDATION COMPLETE ===");
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
            isWinUI3Application = false;
            currentWinUI3Analysis = null;
            UpdateUI();
            System.Diagnostics.Debug.WriteLine("New sequence created.");
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

                        // **Validuj načítanú sequence**
                        var validation = DebugTestHelper.ValidateSequenceWithWinUI3(sequence);
                        LogValidationResults(validation);

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
                                System.Diagnostics.Debug.WriteLine($"Auto-found target window: {sequence.TargetProcessName}");

                                // **Analyzuj nájdené okno**
                                AnalyzeTargetApplication();
                            }
                            else
                            {
                                targetWindowHandle = IntPtr.Zero;
                                txtTarget.Text += $" (Not Found)";
                                statusTarget.Text += " ✗";
                                System.Diagnostics.Debug.WriteLine($"Target application not running: {sequence.TargetProcessName}");
                            }
                        }

                        UpdateUI();
                        System.Diagnostics.Debug.WriteLine($"Loaded sequence: {sequence.Name} ({sequence.Commands.Count} commands)");

                        // **Log WinUI3 štatistiky**
                        var winui3Count = sequence.Commands.Count(c => c.IsWinUI3Element);
                        if (winui3Count > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"WinUI3 commands: {winui3Count}");
                        }
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
                    System.Diagnostics.Debug.WriteLine($"Error loading sequence: {ex.Message}");
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

                // Copy target information
                if (recorder.CurrentSequence != null)
                {
                    sequence.TargetProcessName = recorder.CurrentSequence.TargetProcessName;
                    sequence.TargetWindowTitle = recorder.CurrentSequence.TargetWindowTitle;
                    sequence.TargetWindowClass = recorder.CurrentSequence.TargetWindowClass;
                    sequence.TargetApplication = recorder.CurrentSequence.TargetApplication;
                    sequence.AutoFindTarget = recorder.CurrentSequence.AutoFindTarget;
                    sequence.MaxWaitTimeSeconds = recorder.CurrentSequence.MaxWaitTimeSeconds;
                }

                foreach (var cmd in commands)
                {
                    sequence.AddCommand(cmd);
                }

                sequence.SaveToFile(filePath);
                hasUnsavedChanges = false;
                System.Diagnostics.Debug.WriteLine($"Sequence saved to {filePath}");
                MessageBox.Show("Sequence saved successfully.", "Save Sequence",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving sequence: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Error saving sequence: {ex.Message}");
            }
        }

        private void QuickWinUI3Test_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (targetWindowHandle == IntPtr.Zero)
                {
                    MessageBox.Show("Please select a target window first.", "No Target Window",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                System.Diagnostics.Debug.WriteLine("=== QUICK WINUI3 TEST ===");

                var targetInfo = ExtractWindowInfo(targetWindowHandle);
                System.Diagnostics.Debug.WriteLine($"Testing: {targetInfo.ProcessName} - {targetInfo.WindowTitle}");

                // Test WinUI3 detection
                var analysis = DebugTestHelper.AnalyzeWinUI3Application(targetWindowHandle);

                if (analysis.IsSuccessful)
                {
                    System.Diagnostics.Debug.WriteLine($"📊 Analysis results:");
                    System.Diagnostics.Debug.WriteLine($"   Bridges: {analysis.BridgeCount}");
                    System.Diagnostics.Debug.WriteLine($"   Interactive elements: {analysis.InteractiveElements.Count}");

                    if (analysis.BridgeCount > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"🎯 WinUI3 application detected!");

                        // Test element finding if we have recorded commands
                        var winui3Commands = commands.Where(c => c.IsWinUI3Element).ToList();
                        if (winui3Commands.Any())
                        {
                            System.Diagnostics.Debug.WriteLine($"Testing {winui3Commands.Count} recorded WinUI3 commands:");
                            foreach (var cmd in winui3Commands.Take(3)) // Test first 3
                            {
                                var searchResult = AdaptiveElementFinder.SmartFindElement(targetWindowHandle, cmd);
                                string status = searchResult.IsSuccess ? "✓" : "✗";
                                System.Diagnostics.Debug.WriteLine($"  {status} {cmd.ElementName} -> {(searchResult.IsSuccess ? searchResult.SearchMethod : searchResult.ErrorMessage)}");
                            }
                        }

                        // Show top elements
                        var topElements = analysis.InteractiveElements.Take(5);
                        System.Diagnostics.Debug.WriteLine($"📋 Top elements:");
                        foreach (var element in topElements)
                        {
                            string id = !string.IsNullOrEmpty(element.AutomationId) ? $"ID:{element.AutomationId}" : "no-ID";
                            string text = !string.IsNullOrEmpty(element.Text) ? $"Text:'{element.Text}'" : "no-text";
                            System.Diagnostics.Debug.WriteLine($"   • {element.Name} ({element.ControlType}) [{id}] [{text}]");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"📱 Standard Windows application");
                    }

                    // Show recommendations
                    if (analysis.Recommendations.Any())
                    {
                        System.Diagnostics.Debug.WriteLine($"💡 Recommendations:");
                        foreach (var rec in analysis.Recommendations)
                        {
                            System.Diagnostics.Debug.WriteLine($"   • {rec}");
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Analysis failed: {analysis.ErrorMessage}");
                }

                System.Diagnostics.Debug.WriteLine($"=== TEST COMPLETE ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Test failed: {ex.Message}");
                MessageBox.Show($"WinUI3 test failed: {ex.Message}", "Test Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// **Bulk element update pre WinUI3**
        /// </summary>
        private void BulkUpdateWinUI3Elements_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (targetWindowHandle == IntPtr.Zero)
                {
                    MessageBox.Show("Please select a target window first.", "No Target Window",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var winui3Commands = commands.Where(c => c.IsWinUI3Element).ToList();
                if (!winui3Commands.Any())
                {
                    MessageBox.Show("No WinUI3 commands found to update.", "No WinUI3 Commands",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                System.Diagnostics.Debug.WriteLine("=== BULK WINUI3 ELEMENT UPDATE ===");
                System.Diagnostics.Debug.WriteLine($"Updating {winui3Commands.Count} WinUI3 commands...");

                int updatedCount = 0;
                int failedCount = 0;

                foreach (var cmd in winui3Commands)
                {
                    try
                    {
                        var searchResult = AdaptiveElementFinder.SmartFindElement(targetWindowHandle, cmd);
                        if (searchResult.IsSuccess && searchResult.Element != null)
                        {
                            // Update command with new element info
                            cmd.ElementX = searchResult.Element.X;
                            cmd.ElementY = searchResult.Element.Y;
                            cmd.ElementConfidence = searchResult.Confidence;
                            cmd.LastFoundMethod = searchResult.SearchMethod;

                            // Update element details if better info is available
                            if (string.IsNullOrEmpty(cmd.ElementId) && !string.IsNullOrEmpty(searchResult.Element.AutomationId))
                                cmd.ElementId = searchResult.Element.AutomationId;

                            if (string.IsNullOrEmpty(cmd.ElementText) && !string.IsNullOrEmpty(searchResult.Element.ElementText))
                                cmd.ElementText = searchResult.Element.ElementText;

                            updatedCount++;
                            System.Diagnostics.Debug.WriteLine($"  ✓ Updated: {cmd.ElementName} via {searchResult.SearchMethod}");
                        }
                        else
                        {
                            failedCount++;
                            System.Diagnostics.Debug.WriteLine($"  ✗ Failed: {cmd.ElementName} - {searchResult.ErrorMessage}");
                        }
                    }
                    catch (Exception ex)
                    {
                        failedCount++;
                        System.Diagnostics.Debug.WriteLine($"  ✗ Error updating {cmd.ElementName}: {ex.Message}");
                    }
                }

                hasUnsavedChanges = updatedCount > 0;
                UpdateUI();

                string resultMessage = $"Bulk update completed:\n\n";
                resultMessage += $"✓ Updated: {updatedCount}\n";
                resultMessage += $"✗ Failed: {failedCount}\n";
                resultMessage += $"Total: {winui3Commands.Count}";

                System.Diagnostics.Debug.WriteLine($"Bulk update results: {updatedCount} updated, {failedCount} failed");
                MessageBox.Show(resultMessage, "Bulk Update Results",
                               MessageBoxButton.OK,
                               updatedCount > 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Bulk update error: {ex.Message}");
                MessageBox.Show($"Bulk update failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// **Export comprehensive report**
        /// </summary>
        private void ExportComprehensiveReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (commands.Count == 0)
                {
                    MessageBox.Show("No commands to export.", "No Commands", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var dialog = new SaveFileDialog
                {
                    Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                    Title = "Export Comprehensive Report",
                    FileName = $"{txtSequenceName.Text}_comprehensive_report.txt"
                };

                if (dialog.ShowDialog() == true)
                {
                    var sequence = new CommandSequence(txtSequenceName.Text.Trim());
                    foreach (var cmd in commands)
                    {
                        sequence.AddCommand(cmd);
                    }

                    var report = new System.Text.StringBuilder();

                    // Header
                    report.AppendLine("=== APPCOMMANDER COMPREHENSIVE REPORT ===");
                    report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    report.AppendLine($"Sequence: {sequence.Name}");
                    report.AppendLine($"Commands: {sequence.Commands.Count}");
                    report.AppendLine("");

                    // Target info
                    if (targetWindowHandle != IntPtr.Zero)
                    {
                        var targetInfo = ExtractWindowInfo(targetWindowHandle);
                        report.AppendLine("=== TARGET APPLICATION ===");
                        report.AppendLine($"Process: {targetInfo.ProcessName}");
                        report.AppendLine($"Window Title: {targetInfo.WindowTitle}");
                        report.AppendLine($"Window Class: {targetInfo.WindowClass}");
                        report.AppendLine($"WinUI3 Application: {isWinUI3Application}");
                        report.AppendLine("");
                    }

                    // WinUI3 analysis
                    if (currentWinUI3Analysis != null && isWinUI3Application)
                    {
                        report.AppendLine("=== WINUI3 ANALYSIS ===");
                        report.AppendLine(DebugTestHelper.ExportWinUI3AnalysisReport(currentWinUI3Analysis));
                        report.AppendLine("");
                    }

                    // Validation results
                    var validation = DebugTestHelper.ValidateSequenceWithWinUI3(sequence, targetWindowHandle);
                    report.AppendLine("=== VALIDATION RESULTS ===");
                    report.AppendLine($"Status: {(validation.IsValid ? "PASSED" : "FAILED")}");
                    report.AppendLine($"Errors: {validation.Errors.Count}");
                    report.AppendLine($"Warnings: {validation.Warnings.Count}");

                    if (validation.Errors.Any())
                    {
                        report.AppendLine("\nErrors:");
                        foreach (var error in validation.Errors)
                            report.AppendLine($"  ✗ {error}");
                    }

                    if (validation.Warnings.Any())
                    {
                        report.AppendLine("\nWarnings:");
                        foreach (var warning in validation.Warnings)
                            report.AppendLine($"  ⚠ {warning}");
                    }

                    if (validation.Info.Any())
                    {
                        report.AppendLine("\nInformation:");
                        foreach (var info in validation.Info)
                            report.AppendLine($"  ℹ {info}");
                    }

                    report.AppendLine("");

                    // Command statistics
                    var winui3Commands = sequence.Commands.Where(c => c.IsWinUI3Element).ToList();
                    report.AppendLine("=== COMMAND STATISTICS ===");
                    report.AppendLine($"Total Commands: {sequence.Commands.Count}");
                    report.AppendLine($"WinUI3 Commands: {winui3Commands.Count}");

                    if (winui3Commands.Any())
                    {
                        var strongIds = winui3Commands.Count(c => !string.IsNullOrEmpty(c.ElementId) && !IsGenericId(c.ElementId));
                        var withText = winui3Commands.Count(c => !string.IsNullOrEmpty(c.ElementText));
                        var withConfidence = winui3Commands.Count(c => c.ElementConfidence > 0);

                        report.AppendLine($"  Strong AutomationIDs: {strongIds}");
                        report.AppendLine($"  With Text Content: {withText}");
                        report.AppendLine($"  With Confidence Score: {withConfidence}");

                        if (withConfidence > 0)
                        {
                            var avgConfidence = winui3Commands.Where(c => c.ElementConfidence > 0).Average(c => c.ElementConfidence);
                            report.AppendLine($"  Average Confidence: {avgConfidence:F2}");
                        }
                    }

                    var commandTypes = sequence.Commands.GroupBy(c => c.Type).OrderByDescending(g => g.Count());
                    report.AppendLine("\nCommand Types:");
                    foreach (var group in commandTypes)
                    {
                        report.AppendLine($"  {group.Key}: {group.Count()}");
                    }
                    report.AppendLine("");

                    // Detailed command list
                    report.AppendLine("=== DETAILED COMMAND LIST ===");
                    report.AppendLine(DebugTestHelper.ExportSequenceAsText(sequence));

                    // Element availability test
                    if (targetWindowHandle != IntPtr.Zero && winui3Commands.Any())
                    {
                        report.AppendLine("\n=== ELEMENT AVAILABILITY TEST ===");
                        int foundElements = 0;
                        foreach (var cmd in winui3Commands)
                        {
                            try
                            {
                                var searchResult = AdaptiveElementFinder.SmartFindElement(targetWindowHandle, cmd);
                                if (searchResult.IsSuccess)
                                {
                                    foundElements++;
                                    report.AppendLine($"✓ {cmd.ElementName} -> {searchResult.SearchMethod} (confidence: {searchResult.Confidence:F2})");
                                }
                                else
                                {
                                    report.AppendLine($"✗ {cmd.ElementName} -> {searchResult.ErrorMessage}");
                                }
                            }
                            catch (Exception ex)
                            {
                                report.AppendLine($"✗ {cmd.ElementName} -> Error: {ex.Message}");
                            }
                        }
                        report.AppendLine($"\nElement Availability: {foundElements}/{winui3Commands.Count} found");
                    }

                    // Recommendations
                    report.AppendLine("\n=== RECOMMENDATIONS ===");
                    if (currentWinUI3Analysis?.Recommendations?.Any() == true)
                    {
                        foreach (var rec in currentWinUI3Analysis.Recommendations)
                        {
                            report.AppendLine($"• {rec}");
                        }
                    }
                    else
                    {
                        report.AppendLine("• No specific recommendations available");
                    }

                    // Footer
                    report.AppendLine($"\n=== REPORT COMPLETE ===");
                    report.AppendLine($"Generated by AppCommander v1.1.0");
                    report.AppendLine($"Report file: {dialog.FileName}");

                    System.IO.File.WriteAllText(dialog.FileName, report.ToString());

                    System.Diagnostics.Debug.WriteLine($"Comprehensive report exported: {dialog.FileName}");
                    MessageBox.Show($"Comprehensive report exported successfully to:\n{dialog.FileName}",
                                   "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Ask if user wants to open the file
                    var openResult = MessageBox.Show("Would you like to open the report file?", "Open Report",
                                                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (openResult == MessageBoxResult.Yes)
                    {
                        System.Diagnostics.Process.Start("notepad.exe", dialog.FileName);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Export error: {ex.Message}");
                MessageBox.Show($"Export failed: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// **Live element finder (real-time testing)**
        /// </summary>
        private void LiveElementFinder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (targetWindowHandle == IntPtr.Zero)
                {
                    MessageBox.Show("Please select a target window first.", "No Target Window",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                ShowLiveElementFinderDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening live element finder: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// **Dialog pre live element finding**
        /// </summary>
        private void ShowLiveElementFinderDialog()
        {
            var finderWindow = new Window()
            {
                Title = "Live Element Finder",
                Width = 700,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Input panel
            var inputPanel = new StackPanel { Margin = new Thickness(10), Orientation = Orientation.Horizontal };
            inputPanel.Children.Add(new Label { Content = "Click coordinates (x, y):" });
            var coordTextBox = new TextBox { Width = 100, Margin = new Thickness(5, 0, 5, 0) };
            inputPanel.Children.Add(coordTextBox);
            var findButton = new Button { Content = "Find Element", Margin = new Thickness(5, 0, 5, 0) };
            inputPanel.Children.Add(findButton);
            var refreshButton = new Button { Content = "Refresh Analysis", Margin = new Thickness(5, 0, 0, 0) };
            inputPanel.Children.Add(refreshButton);

            Grid.SetRow(inputPanel, 0);
            mainGrid.Children.Add(inputPanel);

            // Results panel
            var resultsTextBox = new TextBox
            {
                IsReadOnly = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 10,
                Margin = new Thickness(10)
            };
            Grid.SetRow(resultsTextBox, 1);
            mainGrid.Children.Add(resultsTextBox);

            // Button panel
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10)
            };
            var closeButton = new Button { Content = "Close", Width = 75 };
            buttonPanel.Children.Add(closeButton);
            Grid.SetRow(buttonPanel, 2);
            mainGrid.Children.Add(buttonPanel);

            finderWindow.Content = mainGrid;

            // Initial analysis
            var analysis = DebugTestHelper.AnalyzeWinUI3Application(targetWindowHandle);
            var initialText = new System.Text.StringBuilder();
            initialText.AppendLine("=== LIVE ELEMENT FINDER ===");
            initialText.AppendLine($"Target: {ExtractWindowInfo(targetWindowHandle).ProcessName}");
            initialText.AppendLine($"WinUI3 Bridges: {analysis.BridgeCount}");
            initialText.AppendLine($"Interactive Elements: {analysis.InteractiveElements.Count}");
            initialText.AppendLine("");
            initialText.AppendLine("Enter coordinates (x,y) and click 'Find Element' to analyze specific points.");
            initialText.AppendLine("Example: 500,300");
            initialText.AppendLine("");

            resultsTextBox.Text = initialText.ToString();

            // Event handlers
            findButton.Click += (sender, e) =>
            {
                try
                {
                    var coords = coordTextBox.Text.Trim().Split(',');
                    if (coords.Length == 2 &&
                        int.TryParse(coords[0].Trim(), out int x) &&
                        int.TryParse(coords[1].Trim(), out int y))
                    {
                        var result = new System.Text.StringBuilder();
                        result.AppendLine($"=== ANALYZING POINT ({x}, {y}) ===");
                        result.AppendLine($"Timestamp: {DateTime.Now:HH:mm:ss}");
                        result.AppendLine("");

                        // Get element at point
                        var elementInfo = UIElementDetector.GetElementAtPoint(x, y);
                        if (elementInfo != null)
                        {
                            result.AppendLine("Element Found:");
                            result.AppendLine($"  Name: '{elementInfo.Name}'");
                            result.AppendLine($"  AutomationId: '{elementInfo.AutomationId}'");
                            result.AppendLine($"  Class: '{elementInfo.ClassName}'");
                            result.AppendLine($"  Type: '{elementInfo.ControlType}'");
                            result.AppendLine($"  Text: '{elementInfo.ElementText}'");
                            result.AppendLine($"  Position: ({elementInfo.X}, {elementInfo.Y})");
                            result.AppendLine($"  Enabled: {elementInfo.IsEnabled}, Visible: {elementInfo.IsVisible}");

                            if (elementInfo.ClassName == "Microsoft.UI.Content.DesktopChildSiteBridge")
                            {
                                result.AppendLine("  🎯 WinUI3 Element Detected!");

                                // Create a test command and see how well we can find it
                                var testCommand = new Command(1, elementInfo.Name, CommandType.Click, x, y);
                                testCommand.UpdateFromElementInfo(elementInfo);

                                result.AppendLine("");
                                result.AppendLine("Adaptive Finding Test:");
                                var searchResult = AdaptiveElementFinder.SmartFindElement(targetWindowHandle, testCommand);
                                if (searchResult.IsSuccess)
                                {
                                    result.AppendLine($"  ✓ Found via: {searchResult.SearchMethod}");
                                    result.AppendLine($"  ✓ Confidence: {searchResult.Confidence:F2}");
                                    result.AppendLine($"  ✓ Position: ({searchResult.Element.X}, {searchResult.Element.Y})");
                                }
                                else
                                {
                                    result.AppendLine($"  ✗ Search failed: {searchResult.ErrorMessage}");
                                }

                                result.AppendLine($"  Best Identifier: {testCommand.GetBestElementIdentifier()}");
                            }
                        }
                        else
                        {
                            result.AppendLine("No element found at this position.");
                        }

                        result.AppendLine("");
                        result.AppendLine(new string('-', 50));
                        result.AppendLine("");

                        resultsTextBox.Text += result.ToString();
                        resultsTextBox.ScrollToEnd();
                    }
                    else
                    {
                        MessageBox.Show("Please enter coordinates in format: x,y (example: 500,300)",
                                       "Invalid Format", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    resultsTextBox.Text += $"Error analyzing point: {ex.Message}\n\n";
                    resultsTextBox.ScrollToEnd();
                }
            };

            refreshButton.Click += (sender, e) =>
            {
                var refreshAnalysis = DebugTestHelper.AnalyzeWinUI3Application(targetWindowHandle);
                var refreshText = new System.Text.StringBuilder();
                refreshText.AppendLine("=== REFRESHED ANALYSIS ===");
                refreshText.AppendLine($"Timestamp: {DateTime.Now:HH:mm:ss}");
                refreshText.AppendLine($"WinUI3 Bridges: {refreshAnalysis.BridgeCount}");
                refreshText.AppendLine($"Interactive Elements: {refreshAnalysis.InteractiveElements.Count}");
                refreshText.AppendLine("");

                if (refreshAnalysis.InteractiveElements.Any())
                {
                    refreshText.AppendLine("Top 10 Interactive Elements:");
                    foreach (var element in refreshAnalysis.InteractiveElements.Take(10))
                    {
                        refreshText.AppendLine($"  • {element.Name} ({element.ControlType}) at ({element.Position.X}, {element.Position.Y})");
                    }
                }

                refreshText.AppendLine("");
                refreshText.AppendLine(new string('-', 50));
                refreshText.AppendLine("");

                resultsTextBox.Text += refreshText.ToString();
                resultsTextBox.ScrollToEnd();
            };

            coordTextBox.KeyDown += (sender, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    findButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
            };

            closeButton.Click += (sender, e) => finderWindow.Close();

            coordTextBox.Focus();
            finderWindow.ShowDialog();
        }

        // Helper methods for coordinate validation (already implemented above)
        private bool IsPointOnScreen(int x, int y)
        {
            try
            {
                var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
                int screenWidth = primaryScreen.Bounds.Width;
                int screenHeight = primaryScreen.Bounds.Height;

                bool isValid = x >= -100 && x <= (screenWidth + 100) && y >= -100 && y <= (screenHeight + 100);

                if (!isValid)
                {
                    foreach (var screen in System.Windows.Forms.Screen.AllScreens)
                    {
                        if (screen.Bounds.Contains(x, y))
                        {
                            return true;
                        }
                    }
                    return false;
                }

                return true;
            }
            catch
            {
                return true;
            }
        }
         
        private string GetScreenInfo()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Screen Information:");

                var primary = System.Windows.Forms.Screen.PrimaryScreen;
                sb.AppendLine($"Primary: {primary.Bounds.Width}x{primary.Bounds.Height} at ({primary.Bounds.X}, {primary.Bounds.Y})");

                foreach (var screen in System.Windows.Forms.Screen.AllScreens)
                {
                    if (!screen.Primary)
                    {
                        sb.AppendLine($"Secondary: {screen.Bounds.Width}x{screen.Bounds.Height} at ({screen.Bounds.X}, {screen.Bounds.Y})");
                    }
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error getting screen info: {ex.Message}";
            }
        }
        #endregion
        #region Update Methods Implementation

        /// <summary>
        /// Updates UI state based on current application state
        /// </summary>
        private void UpdateUI()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    // Update button states based on current state
                    bool hasCommands = commands?.Count > 0;
                    bool hasTarget = targetWindowHandle != IntPtr.Zero;
                    bool isRecording = recorder?.IsRecording ?? false;
                    bool isPlaying = player?.IsPlaying ?? false;

                    // Recording buttons
                    if (btnStartRecording != null)
                        btnStartRecording.IsEnabled = !isRecording && !isPlaying && hasTarget;
                    if (btnStopRecording != null)
                        btnStopRecording.IsEnabled = isRecording;
                    if (btnPauseRecording != null)
                        btnPauseRecording.IsEnabled = isRecording;

                    // Playback buttons
                    if (btnPlay != null)
                        btnPlay.IsEnabled = !isRecording && !isPlaying && hasCommands && hasTarget;
                    if (btnPause != null)
                        btnPause.IsEnabled = isPlaying;
                    if (btnStop != null)
                        btnStop.IsEnabled = isPlaying;

                    // Update window title s podporou .apc súborov
                    string title = "AppCommander - Automation Tool";

                    // Pridaj * ak sú unsaved changes
                    if (hasUnsavedChanges)
                        title += " *";

                    // Pridaj názov .apc súboru ak je otvorený
                    if (!string.IsNullOrEmpty(currentFilePath))
                    {
                        string fileName = Path.GetFileName(currentFilePath);
                        // Uisti sa že .apc súbory sú správne zobrazené
                        if (fileName.EndsWith(".apc", StringComparison.OrdinalIgnoreCase))
                        {
                            title += $" - {fileName}";
                        }
                        else
                        {
                            // Pre prípad že by bol iný typ súboru (neočakávaný)
                            title += $" - {fileName}";
                        }
                    }
                    else if (!string.IsNullOrEmpty(txtSequenceName?.Text) &&
                             txtSequenceName.Text != "New Sequence")
                    {
                        // Ak nie je súbor uložený, ale má názov, ukáž ho
                        title += $" - {txtSequenceName.Text} (unsaved)";
                    }

                    this.Title = title;

                    // Update status bar
                    UpdateStatusBar();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating UI: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates status bar with current information
        /// </summary>
        private void UpdateStatusBar()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    // Update commands count
                    if (statusCommands != null)
                    {
                        statusCommands.Text = $"Commands: {commands?.Count ?? 0}";
                    }

                    // Update recording status
                    if (statusRecording != null)
                    {
                        bool isRecording = recorder?.IsRecording ?? false;
                        statusRecording.Text = isRecording ? "Recording" : "Not Recording";
                    }

                    // Update target info
                    if (statusTarget != null)
                    {
                        if (targetWindowHandle != IntPtr.Zero)
                        {
                            var targetInfo = ExtractWindowInfo(targetWindowHandle);
                            statusTarget.Text = $"Target: {targetInfo.ProcessName}";
                            if (isWinUI3Application)
                            {
                                statusTarget.Text += " [WinUI3]";
                            }
                        }
                        else
                        {
                            statusTarget.Text = "No Target";
                        }
                    }

                    // Update status text
                    if (statusText != null)
                    {
                        if (recorder?.IsRecording == true)
                        {
                            statusText.Text = "Recording...";
                        }
                        else if (player?.IsPlaying == true)
                        {
                            statusText.Text = "Playing...";
                        }
                        else
                        {
                            statusText.Text = "Ready";
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating status bar: {ex.Message}");
            }
        }

        /// <summary>
        /// Extrahuje informácie o okne
        /// </summary>
        /// <param name="windowHandle">Handle okna</param>
        /// <returns>Informácie o okne</returns>
        private WindowInfo ExtractWindowInfo(IntPtr windowHandle)
        {
            try
            {
                var info = new WindowInfo();

                if (windowHandle == IntPtr.Zero)
                {
                    return new WindowInfo
                    {
                        WindowTitle = "Invalid Handle",
                        ProcessName = "Unknown",
                        WindowClass = "Unknown",
                        Handle = windowHandle,
                        ErrorMessage = "Invalid window handle"
                    };
                }

                // Get window title
                int titleLength = GetWindowTextLength(windowHandle);
                if (titleLength > 0)
                {
                    System.Text.StringBuilder title = new System.Text.StringBuilder(titleLength + 1);
                    GetWindowText(windowHandle, title, title.Capacity);
                    info.WindowTitle = title.ToString();
                }
                else
                {
                    info.WindowTitle = "No Title";
                }

                // Get window class
                System.Text.StringBuilder className = new System.Text.StringBuilder(256);
                GetClassName(windowHandle, className, className.Capacity);
                info.WindowClass = className.ToString();

                // Get process info
                GetWindowThreadProcessId(windowHandle, out uint processId);
                try
                {
                    var process = System.Diagnostics.Process.GetProcessById((int)processId);
                    info.ProcessName = process.ProcessName;
                    info.ProcessId = (int)processId;
                }
                catch
                {
                    info.ProcessName = "Unknown";
                    info.ProcessId = (int)processId;
                }

                info.Handle = windowHandle;
                return info;
            }
            catch (Exception ex)
            {
                return new WindowInfo
                {
                    WindowTitle = "Error",
                    ProcessName = "Unknown",
                    WindowClass = "Unknown",
                    Handle = windowHandle,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Kontroluje či je ID generické/slabé
        /// </summary>
        /// <param name="id">ID na kontrolu</param>
        /// <returns>True ak je ID generické</returns>
        private bool IsGenericId(string id)
        {
            if (string.IsNullOrEmpty(id)) return true;

            // Generic ID patterns
            var genericPatterns = new[] {
        "TextBox", "Button", "Grid", "Panel", "_", "Auto",
        "Unknown", "Element", "Control", "{", "}", "-"
    };

            // Ak je príliš dlhé alebo obsahuje len čísla
            if (id.Length > 20 || id.All(char.IsDigit))
                return true;

            return genericPatterns.Any(pattern => id.Contains(pattern));
        }

        /// <summary>
        /// Kontroluje či je meno elementu generické
        /// </summary>
        /// <param name="name">Meno na kontrolu</param>
        /// <returns>True ak je meno generické</returns>
        private bool IsGenericName(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;

            var genericNames = new[] {
        "Unknown", "pane_Unknown", "Element_at_", "Click_at_",
        "Microsoft.UI.Content", "DesktopChildSiteBridge", "Control"
    };

            return genericNames.Any(g => name.Contains(g)) || name.Length < 3;
        }

        // Windows API imports
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        #endregion

        // PRIDAJTE TIETO CHÝBAJÚCE EVENT HANDLERS DO MainWindow.xaml.cs

        #region Missing Menu Event Handlers

        /// <summary>
        /// Exit aplikácie
        /// </summary>
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// Window Selector - alias pre SelectTarget
        /// </summary>
        private void WindowSelector_Click(object sender, RoutedEventArgs e)
        {
            SelectTarget_Click(sender, e);
        }

        /// <summary>
        /// Element Inspector - alias pre LiveElementFinder
        /// </summary>
        private void ElementInspector_Click(object sender, RoutedEventArgs e)
        {
            LiveElementFinder_Click(sender, e);
        }

        /// <summary>
        /// Test Playback - alias pre QuickWinUI3Test
        /// </summary>
        private void TestPlayback_Click(object sender, RoutedEventArgs e)
        {
            QuickWinUI3Test_Click(sender, e);
        }

        /// <summary>
        /// Debug Coordinates pre vybraný command
        /// </summary>
        private void DebugCoordinates_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (commands.Count == 0)
                {
                    MessageBox.Show("No commands to debug.", "No Commands", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var selectedCommand = dgCommands.SelectedItem as Command;
                if (selectedCommand == null)
                {
                    MessageBox.Show("Please select a command to debug.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string debugInfo = $"Command Debug Information:\n\n";
                debugInfo += $"Step: {selectedCommand.StepNumber}\n";
                debugInfo += $"Type: {selectedCommand.Type}\n";
                debugInfo += $"Element: {selectedCommand.ElementName}\n";
                debugInfo += $"Current Position: ({selectedCommand.ElementX}, {selectedCommand.ElementY})\n";
                debugInfo += $"Original Position: ({selectedCommand.OriginalX}, {selectedCommand.OriginalY})\n";
                debugInfo += $"Element ID: {selectedCommand.ElementId}\n";
                debugInfo += $"Element Class: {selectedCommand.ElementClass}\n";
                debugInfo += $"Element Text: {selectedCommand.ElementText}\n";
                debugInfo += $"WinUI3 Element: {selectedCommand.IsWinUI3Element}\n";

                if (selectedCommand.IsWinUI3Element)
                {
                    debugInfo += $"Element Confidence: {selectedCommand.ElementConfidence:F2}\n";
                    debugInfo += $"Best Identifier: {selectedCommand.GetBestElementIdentifier()}\n";
                }

                // Pridaj screen info
                debugInfo += $"\nScreen Information:\n";
                debugInfo += GetScreenInfo();

                MessageBox.Show(debugInfo, "Command Debug Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Debug coordinates error: {ex.Message}");
                MessageBox.Show($"Debug error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Play Without Element Search - priame súradnice
        /// </summary>
        private void PlayWithoutElementSearch_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "This feature will play commands using exact coordinates without element searching.\n\n" +
                    "Warning: This may fail if the target application has moved or resized since recording.\n\n" +
                    "Continue?",
                    "Play Direct Mode",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    // Dočasne vypni element finding
                    bool originalPreferIds = player.PreferElementIdentifiers;
                    bool originalAdaptive = player.EnableAdaptiveFinding;

                    try
                    {
                        player.PreferElementIdentifiers = false;
                        player.EnableAdaptiveFinding = false;

                        System.Diagnostics.Debug.WriteLine("Playing sequence in direct coordinate mode");
                        PlaySequence_Click(sender, e);
                    }
                    finally
                    {
                        // Obnov originálne nastavenia
                        player.PreferElementIdentifiers = originalPreferIds;
                        player.EnableAdaptiveFinding = originalAdaptive;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Play direct error: {ex.Message}");
                MessageBox.Show($"Direct play error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Export Sequence For Debug - alias pre ExportComprehensiveReport
        /// </summary>
        private void ExportSequenceForDebug_Click(object sender, RoutedEventArgs e)
        {
            ExportComprehensiveReport_Click(sender, e);
        }

        #endregion

        #region Additional Missing Event Handlers (ak potrebujete)

        /// <summary>
        /// Settings dialog
        /// </summary>
        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Settings dialog not yet implemented.", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// About dialog
        /// </summary>
        private void About_Click(object sender, RoutedEventArgs e)
        {
            string aboutText = "AppCommander v1.0.0\n\n";
            aboutText += "Advanced automation tool for Windows applications\n";
            aboutText += "with enhanced WinUI3 support.\n\n";
            aboutText += "Features:\n";
            aboutText += "• Smart element detection\n";
            aboutText += "• WinUI3 application support\n";
            aboutText += "• Adaptive element finding\n";
            aboutText += "• Comprehensive sequence validation\n\n";
            aboutText += "Copyright © Rudolf Mendzezof 2025\n";
            aboutText += "All rights reserved.";

            MessageBox.Show(aboutText, "About AppCommander", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// User Guide
        /// </summary>
        private void UserGuide_Click(object sender, RoutedEventArgs e)
        {
            string guideText = "AppCommander Quick Start Guide:\n\n";
            guideText += "1. Select Target Window:\n";
            guideText += "   • Click 'Select Target Window'\n";
            guideText += "   • Choose the application you want to automate\n\n";
            guideText += "2. Record Sequence:\n";
            guideText += "   • Click 'Start Recording'\n";
            guideText += "   • Perform actions in the target application\n";
            guideText += "   • Click 'Stop Recording' when done\n\n";
            guideText += "3. Play Sequence:\n";
            guideText += "   • Ensure target application is open\n";
            guideText += "   • Click 'Play' to execute recorded actions\n\n";
            guideText += "4. Save/Load:\n";
            guideText += "   • Use File menu to save sequences\n";
            guideText += "   • Load previously saved sequences\n\n";
            guideText += "Tips:\n";
            guideText += "• WinUI3 applications are automatically detected\n";
            guideText += "• Use Tools menu for advanced features\n";
            guideText += "• Check Activity Log for detailed information";

            MessageBox.Show(guideText, "User Guide", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Edit Command context menu
        /// </summary>
        private void EditCommand_Click(object sender, RoutedEventArgs e)
        {
            var selectedCommand = dgCommands.SelectedItem as Command;
            if (selectedCommand == null)
            {
                MessageBox.Show("Please select a command to edit.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // TODO: Implement command editing dialog
            MessageBox.Show("Command editing dialog not yet implemented.", "Edit Command", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Delete Command context menu
        /// </summary>
        private void DeleteCommand_Click(object sender, RoutedEventArgs e)
        {
            var selectedCommand = dgCommands.SelectedItem as Command;
            if (selectedCommand == null)
            {
                MessageBox.Show("Please select a command to delete.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"Delete command: {selectedCommand.Type} on {selectedCommand.ElementName}?",
                                        "Delete Command", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                commands.Remove(selectedCommand);
                hasUnsavedChanges = true;
                UpdateUI();
                System.Diagnostics.Debug.WriteLine($"Deleted command: {selectedCommand.Type} on {selectedCommand.ElementName}");
            }
        }

        /// <summary>
        /// Add Wait Command context menu
        /// </summary>
        private void AddWaitCommand_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement add wait command dialog
            MessageBox.Show("Add wait command dialog not yet implemented.", "Add Wait Command", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Add Loop Start context menu
        /// </summary>
        private void AddLoopStart_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement add loop start
            MessageBox.Show("Add loop start not yet implemented.", "Add Loop Start", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Add Loop End context menu
        /// </summary>
        private void AddLoopEnd_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement add loop end
            MessageBox.Show("Add loop end not yet implemented.", "Add Loop End", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Vymaže obsah Activity Log
        /// </summary>
        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (txtLog != null)
                {
                    txtLog.Clear();
                    System.Diagnostics.Debug.WriteLine("Activity log cleared by user");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error clearing log: {ex.Message}");
            }
        }

        #endregion
    }

    /// <summary>
    /// Informácie o okne aplikácie
    /// </summary>
    public class WindowInfo
    {
        // txtSelectedClass.Text = selected.ClassName;
        public string ClassName { get; set; } = "";
        public string WindowTitle { get; set; } = "";
        public string ProcessName { get; set; } = "";
        public string WindowClass { get; set; } = "";
        public IntPtr Handle { get; set; } = IntPtr.Zero;
        public int ProcessId { get; set; } = 0;
        public string ErrorMessage { get; set; } = "";

        public bool IsValid => Handle != IntPtr.Zero && !string.IsNullOrEmpty(ProcessName);

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
                return $"Error: {ErrorMessage}";

            return $"{ProcessName}: {WindowTitle} (Class: {WindowClass})";
        }
    }
} 
