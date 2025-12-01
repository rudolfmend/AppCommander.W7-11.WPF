using AppCommander.W7_11.WPF.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AppCommander.W7_11.WPF.Core.Managers
{
    /// <summary>
    /// Spravuje všetku funkcionalitu prehrávania sekvencií
    /// </summary>
    public class PlaybackManager
    {
        #region Win32 API

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

        #endregion

        #region Private Fields

        private readonly CommandPlayer _player;
        private readonly ObservableCollection<Command> _commands;
        private readonly ObservableCollection<UnifiedItem> _unifiedItems;
        private readonly Func<IntPtr> _getTargetWindowHandle;
        private readonly Func<string> _getCurrentSequenceName;
        private readonly Action<string> _updateStatus;
        private readonly Action _updateUI;
        private readonly Dispatcher _dispatcher;
        private Button _btnPause;
        private TextBox _txtRepeatCount;
        private CheckBox _chkInfiniteLoop;

        #endregion

        #region Constructor

        public PlaybackManager(
            CommandPlayer player,
            ObservableCollection<Command> commands,
            ObservableCollection<UnifiedItem> unifiedItems,
            Func<IntPtr> getTargetWindowHandle,
            Func<string> getCurrentSequenceName,
            Action<string> updateStatus,
            Action updateUI,
            Dispatcher dispatcher)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _unifiedItems = unifiedItems ?? throw new ArgumentNullException(nameof(unifiedItems));
            _getTargetWindowHandle = getTargetWindowHandle ?? throw new ArgumentNullException(nameof(getTargetWindowHandle));
            _getCurrentSequenceName = getCurrentSequenceName ?? throw new ArgumentNullException(nameof(getCurrentSequenceName));
            _updateStatus = updateStatus ?? throw new ArgumentNullException(nameof(updateStatus));
            _updateUI = updateUI ?? throw new ArgumentNullException(nameof(updateUI));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        #endregion

        #region Public Methods - UI Controls Setup

        /// <summary>
        /// Nastaví referencie na UI prvky
        /// </summary>
        public void SetUIControls(Button btnPause, TextBox txtRepeatCount, CheckBox chkInfiniteLoop)
        {
            _btnPause = btnPause;
            _txtRepeatCount = txtRepeatCount;
            _chkInfiniteLoop = chkInfiniteLoop;
        }

        #endregion

        #region Public Methods - Main Playback

        /// <summary>
        /// Spustí playback - hlavná metóda
        /// </summary>
        public void StartPlayback()
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                Debug.WriteLine($"Timestamp: {timestamp} | PlaybackManager.StartPlayback()");

                if (_player.IsPlaying)
                {
                    _updateStatus("Playback is already running");
                    return;
                }

                // Vždy používa _unifiedItems ak existujú
                if (_unifiedItems.Count > 0)
                {
                    Debug.WriteLine($"Using unified sequence for playback from MainCommandTable");
                    Debug.WriteLine($"_unifiedItems.Count = {_unifiedItems.Count}, _commands.Count = {_commands.Count}");

                    // KRITICKÁ OPRAVA: Vyčisti _commands pred playbackom aby sa použili iba _unifiedItems
                    if (_commands.Count > 0)
                    {
                        Debug.WriteLine("⚠️ WARNING: _commands collection is not empty - clearing it to use only _unifiedItems");
                        _commands.Clear();
                    }

                    PlayUnifiedSequence();
                }
                else if (_commands.Any())
                {
                    Debug.WriteLine("Using legacy commands for playback");
                    PlayLegacySequence();
                }
                else
                {
                    MessageBox.Show("No commands to play. Please record some commands first.",
                                   "No Commands", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error starting playback", ex);
            }
        }

        /// <summary>
        /// Testovací playback - prehrá len prvý príkaz
        /// </summary>
        public void TestPlayback()
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
                    TargetProcessName = GetProcessNameFromWindow(_getTargetWindowHandle()),
                    TargetWindowTitle = GetWindowTitle(_getTargetWindowHandle())
                };

                _player.TestPlayback(testSequence);
                _updateStatus("Test playback started with first command");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error starting test playback", ex);
            }
        }

        /// <summary>
        /// Prehrávanie bez vyhľadávania elementov (Direct mode)
        /// </summary>
        public void PlayWithoutElementSearch()
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
                        TargetProcessName = GetProcessNameFromWindow(_getTargetWindowHandle()),
                        TargetWindowTitle = GetWindowTitle(_getTargetWindowHandle())
                    };

                    _player.PlaySequence(sequence, 1);
                    _player.HighlightElements = originalHighlightSetting;

                    _updateStatus("Direct playback started (no element search)");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error starting direct playback", ex);
            }
        }

        /// <summary>
        /// Pozastaví/Obnoví playback
        /// </summary>
        public void TogglePause()
        {
            try
            {
                if (_player.IsPaused)
                {
                    _player.Resume();
                    if (_btnPause != null)
                        _btnPause.Content = "⏸ Pause";
                }
                else if (_player.IsPlaying)
                {
                    _player.Pause();
                    if (_btnPause != null)
                        _btnPause.Content = "▶ Resume";
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error pausing/resuming playback", ex);
            }
        }

        /// <summary>
        /// Zastaví playback
        /// </summary>
        public void StopPlayback()
        {
            try
            {
                _player.Stop();
                if (_btnPause != null)
                    _btnPause.Content = "⏸ Pause";
                _updateStatus("Playback stopped");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error stopping playback", ex);
            }
        }

        #endregion

        #region Private Methods - Playback Logic

        /// <summary>
        /// Prehrá unified sequence z MainCommandTable
        /// </summary>
        private void PlayUnifiedSequence()
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                Debug.WriteLine($"Timestamp: {timestamp} | UnifiedItems count: {_unifiedItems.Count} - PlayUnifiedSequence()");

                DiagnosePlaybackState();

                if (!_unifiedItems.Any())
                {
                    MessageBox.Show("No commands to play.", "No Commands",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Validácia príkazov
                var errors = new List<string>();
                foreach (var item in _unifiedItems)
                {
                    if (!item.IsValid(out string error))
                    {
                        errors.Add($"Step {item.StepNumber}: {error}");
                    }
                }

                if (errors.Any())
                {
                    var errorMessage = "Validation errors found:\n\n" +
                                      string.Join("\n", errors.Take(5)) +
                                      (errors.Count > 5 ? $"\n... and {errors.Count - 5} more errors." : "") +
                                      "\n\nDo you want to continue anyway?";

                    var result = MessageBox.Show(errorMessage, "Validation Errors",
                                                MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (result == MessageBoxResult.No)
                        return;
                }

                // Určenie počtu opakovaní
                int repeatCount = GetRepeatCount();

                // Vytvor UnifiedSequence s aktuálnymi _unifiedItems
                var currentSequence = new UnifiedSequence
                {
                    Name = _getCurrentSequenceName() ?? "Current Sequence",
                    Items = new List<UnifiedItem>(_unifiedItems),
                    TargetProcessName = GetProcessNameFromWindow(_getTargetWindowHandle()),
                    TargetWindowTitle = GetWindowTitle(_getTargetWindowHandle()),
                    Created = DateTime.Now,
                    LastModified = DateTime.Now
                };

                // Convert unified sequence to traditional format for playback
                var commandSequence = currentSequence.ToCommandSequence();
                commandSequence.Name = $"Playback_{DateTime.Now:HHmmss}";

                Debug.WriteLine($"=== PLAYBACK DEBUG ===");
                Debug.WriteLine($"UnifiedItems count: {_unifiedItems.Count}");
                Debug.WriteLine($"Converted commands count: {commandSequence.Commands.Count}");
                Debug.WriteLine($"Target process: {commandSequence.TargetProcessName}");
                Debug.WriteLine($"=====================");

                if (commandSequence.Commands == null || commandSequence.Commands.Count == 0)
                {
                    MessageBox.Show("Failed to convert commands for playback. Check debug output for details.",
                                   "Conversion Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                _player.PlaySequence(commandSequence, repeatCount);
                _updateStatus($"Starting playback ({repeatCount}x) - {commandSequence.Commands.Count} commands");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error starting unified playback", ex);
            }
        }

        /// <summary>
        /// Prehrá legacy sequence z _commands
        /// </summary>
        private void PlayLegacySequence()
        {
            try
            {
                // Validácia loop integrity
                var loopValidation = ValidateLoopIntegrity();
                if (!loopValidation.IsValid)
                {
                    var errorMessages = string.Join("\n", loopValidation.Errors);
                    var message = $"Loop validation warning:\n{errorMessages}\n\nDo you want to continue anyway?";
                    var result = MessageBox.Show(message, "Loop Integrity",
                                                MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (result != MessageBoxResult.Yes)
                        return;
                }

                int repeatCount = GetRepeatCount();

                var sequence = new CommandSequence
                {
                    Name = _getCurrentSequenceName() ?? "Legacy Sequence",
                    Commands = _commands.ToList(),
                    TargetProcessName = GetProcessNameFromWindow(_getTargetWindowHandle()),
                    TargetWindowTitle = GetWindowTitle(_getTargetWindowHandle())
                };

                _player.PlaySequence(sequence, repeatCount);
                _updateStatus($"Starting playback ({repeatCount}x) - {_commands.Count} commands");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error starting legacy playback", ex);
            }
        }

        #endregion

        #region Private Methods - Validation

        /// <summary>
        /// Validuje integritu loop príkazov
        /// </summary>
        private SequenceValidationResult ValidateLoopIntegrity()
        {
            var result = new SequenceValidationResult();

            var loopStarts = _commands.Count(c => c.Type == CommandType.LoopStart);
            var loopEnds = _commands.Count(c => c.Type == CommandType.LoopEnd);

            if (loopStarts != loopEnds)
            {
                result.AddError($"Loop mismatch: {loopStarts} loop starts, {loopEnds} loop ends");
            }

            if (loopStarts > 0 && result.IsValid)
            {
                Debug.WriteLine($"Loop validation passed: {loopStarts} complete loops detected");
            }

            return result;
        }

        #endregion

        #region Private Methods - Helpers

        /// <summary>
        /// Získa počet opakovaní z UI
        /// </summary>
        private int GetRepeatCount()
        {
            int repeatCount = 1;

            // Ak je infinite loop zaškrtnutý, použi int.MaxValue
            if (_chkInfiniteLoop?.IsChecked == true)
            {
                repeatCount = int.MaxValue;
                Debug.WriteLine("Infinite loop mode activated");
            }
            else if (_txtRepeatCount != null)
            {
                // Normálne parsovanie z textboxu
                if (!int.TryParse(_txtRepeatCount.Text, out repeatCount) || repeatCount < 1)
                {
                    repeatCount = 1;
                    _txtRepeatCount.Text = "1";
                }
            }

            return repeatCount;
        }

        /// <summary>
        /// Diagnostická metóda pre debugging playback problémov
        /// </summary>
        private void DiagnosePlaybackState()
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            Debug.WriteLine("=== PLAYBACK STATE DIAGNOSIS ===");
            Debug.WriteLine($"Timestamp: {timestamp}");

            Debug.WriteLine($"_unifiedItems.Count: {_unifiedItems?.Count ?? 0}");
            Debug.WriteLine($"_commands.Count: {_commands?.Count ?? 0}");
            Debug.WriteLine($"_targetWindowHandle: {_getTargetWindowHandle()}");
            Debug.WriteLine($"Target Process: {GetProcessNameFromWindow(_getTargetWindowHandle())}");
            Debug.WriteLine($"Target Window: {GetWindowTitle(_getTargetWindowHandle())}");

            if (_unifiedItems != null && _unifiedItems.Count > 0)
            {
                Debug.WriteLine("");
                Debug.WriteLine("First 3 unified items:");
                for (int i = 0; i < Math.Min(3, _unifiedItems.Count); i++)
                {
                    var item = _unifiedItems[i];
                    Debug.WriteLine($"  [{i}] Type={item.Type}, Name={item.Name}, Action={item.Action}");
                }
            }

            Debug.WriteLine("================================");
        }

        private string GetProcessNameFromWindow(IntPtr windowHandle)
        {
            try
            {
                if (windowHandle == IntPtr.Zero) return "Unknown";

                GetWindowThreadProcessId(windowHandle, out uint processId);
                using (var process = System.Diagnostics.Process.GetProcessById((int)processId))
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

        private void ShowErrorMessage(string title, Exception ex)
        {
            var message = $"{title}\n\nError: {ex.Message}";
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Debug.WriteLine($"{title}: {ex.Message}");
        }

        #endregion

        #region Event Handlers for CommandPlayer

        /// <summary>
        /// Handler pre OnCommandExecuted event z CommandPlayer
        /// </summary>
        public void OnCommandExecuted(object sender, CommandPlayer.CommandExecutedEventArgs e)
        {
            try
            {
                _dispatcher.Invoke(() =>
                {
                    var statusMsg = $"Executing {e.CommandIndex + 1}/{e.TotalCommands}: {GetCommandDescription(e.Command)}";
                    _updateStatus(statusMsg);

                    if (!e.Success)
                    {
                        Debug.WriteLine($"Command failed: {e.ErrorMessage}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling command executed: {ex.Message}");
            }
        }

        /// <summary>
        /// Handler pre OnPlaybackStateChanged event z CommandPlayer
        /// </summary>
        public void OnPlaybackStateChanged(object sender, CommandPlayer.PlaybackStateChangedEventArgs e)
        {
            try
            {
                _dispatcher.Invoke(() =>
                {
                    _updateUI();
                    _updateStatus($"Playback {e.State}: {e.SequenceName}");
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling playback state change: {ex.Message}");
            }
        }

        /// <summary>
        /// Handler pre OnPlaybackCompleted event z CommandPlayer
        /// </summary>
        public void OnPlaybackCompleted(object sender, CommandPlayer.PlaybackCompletedEventArgs e)
        {
            try
            {
                _dispatcher.Invoke(() =>
                {
                    _updateUI();
                    _updateStatus($"Playback completed: {e.Message}");

                    if (e.Success)
                    {
                        var message = $"Playback completed successfully!\n\nCommands executed: {e.CommandsExecuted}/{e.TotalCommands}";
                        Debug.WriteLine(message);
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling playback completed: {ex.Message}");
            }
        }

        /// <summary>
        /// Handler pre OnPlaybackError event z CommandPlayer
        /// </summary>
        public void OnPlaybackError(object sender, CommandPlayer.PlaybackErrorEventArgs e)
        {
            try
            {
                _dispatcher.Invoke(() =>
                {
                    _updateStatus($"Playback error: {e.ErrorMessage}");
                    var errorTitle = $"Error during playback at command {e.CommandIndex + 1}";
                    ShowErrorMessage(errorTitle, new Exception(e.ErrorMessage));
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling playback error: {ex.Message}");
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

        #endregion
    }
}
