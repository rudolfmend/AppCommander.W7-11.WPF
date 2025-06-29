using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AppCommander.W7_11.WPF.Core
{
    public class CommandPlayer
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        private readonly ActionSimulator actionSimulator;
        private CommandSequence currentSequence;
        private bool isPlaying = false;
        private bool isPaused = false;
        private CancellationTokenSource cancellationTokenSource;
        private int currentCommandIndex = 0;
        private readonly Stack<LoopContext> loopStack;

        // Configuration
        public int DefaultDelayBetweenCommands { get; set; } = 100; // ms
        public bool StopOnError { get; set; } = false;
        public bool HighlightElements { get; set; } = true;

        // Events
        public event EventHandler<CommandExecutedEventArgs> CommandExecuted;
        public event EventHandler<PlaybackStateChangedEventArgs> PlaybackStateChanged;
        public event EventHandler<PlaybackErrorEventArgs> PlaybackError;
        public event EventHandler<PlaybackCompletedEventArgs> PlaybackCompleted;

        // Properties
        public bool IsPlaying => isPlaying;
        public bool IsPaused => isPaused;
        public CommandSequence CurrentSequence => currentSequence;
        public int CurrentCommandIndex => currentCommandIndex;
        public int TotalCommands => currentSequence?.Commands.Count ?? 0;

        public CommandPlayer()
        {
            actionSimulator = new ActionSimulator();
            loopStack = new Stack<LoopContext>();
            System.Diagnostics.Debug.WriteLine("🎬 CommandPlayer initialized");
        }

        #region Public Methods - OPRAVENÉ IMPLEMENTÁCIE

        internal void Pause()
        {
            try
            {
                if (!isPlaying || isPaused)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ Cannot pause - not playing or already paused");
                    return;
                }

                isPaused = true;
                NotifyPlaybackStateChanged(PlaybackState.Paused);
                System.Diagnostics.Debug.WriteLine("⏸️ Playback paused");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error pausing playback: {ex.Message}");
                NotifyPlaybackError($"Error pausing playback: {ex.Message}", currentCommandIndex);
            }
        }

        internal void Resume()
        {
            try
            {
                if (!isPlaying || !isPaused)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ Cannot resume - not playing or not paused");
                    return;
                }

                isPaused = false;
                NotifyPlaybackStateChanged(PlaybackState.Resumed);
                System.Diagnostics.Debug.WriteLine("▶️ Playback resumed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error resuming playback: {ex.Message}");
                NotifyPlaybackError($"Error resuming playback: {ex.Message}", currentCommandIndex);
            }
        }

        internal void PlaySequence(CommandSequence sequence, int repeatCount = 1)
        {
            try
            {
                if (isPlaying)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ Already playing a sequence");
                    return;
                }

                if (sequence == null || !sequence.Commands.Any())
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ No sequence or empty sequence provided");
                    return;
                }

                currentSequence = sequence;
                currentCommandIndex = 0;
                isPlaying = true;
                isPaused = false;
                loopStack.Clear();

                cancellationTokenSource = new CancellationTokenSource();

                System.Diagnostics.Debug.WriteLine($"🎬 Starting playback: {sequence.Name} (Repeat: {repeatCount}x)");
                NotifyPlaybackStateChanged(PlaybackState.Started);

                // Start playback asynchronously
                Task.Run(async () =>
                {
                    try
                    {
                        for (int iteration = 0; iteration < repeatCount; iteration++)
                        {
                            if (cancellationTokenSource.Token.IsCancellationRequested)
                                break;

                            System.Diagnostics.Debug.WriteLine($"🔄 Starting iteration {iteration + 1}/{repeatCount}");

                            currentCommandIndex = 0;
                            await ExecuteSequenceAsync(cancellationTokenSource.Token);

                            if (iteration < repeatCount - 1)
                            {
                                await Task.Delay(500, cancellationTokenSource.Token); // Delay between iterations
                            }
                        }

                        CompletePlayback(true, $"Sequence completed successfully ({repeatCount} iterations)");
                    }
                    catch (OperationCanceledException)
                    {
                        CompletePlayback(false, "Playback was cancelled");
                    }
                    catch (Exception ex)
                    {
                        CompletePlayback(false, $"Playback failed: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error starting playback: {ex.Message}");
                NotifyPlaybackError($"Error starting playback: {ex.Message}", 0);
            }
        }

        internal void Stop()
        {
            try
            {
                if (!isPlaying)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ Not currently playing");
                    return;
                }

                cancellationTokenSource?.Cancel();
                isPlaying = false;
                isPaused = false;
                currentCommandIndex = 0;
                loopStack.Clear();

                NotifyPlaybackStateChanged(PlaybackState.Stopped);
                System.Diagnostics.Debug.WriteLine("⏹️ Playback stopped");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error stopping playback: {ex.Message}");
                NotifyPlaybackError($"Error stopping playback: {ex.Message}", currentCommandIndex);
            }
        }

        internal void TestPlayback(CommandSequence sequence)
        {
            try
            {
                if (sequence == null || !sequence.Commands.Any())
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ No sequence provided for testing");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"🧪 Testing playback for sequence: {sequence.Name}");

                // Validate commands
                var validationResult = ValidateSequence(sequence);
                if (!validationResult.IsValid)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Sequence validation failed:");
                    foreach (var error in validationResult.Errors)
                    {
                        System.Diagnostics.Debug.WriteLine($"  - {error}");
                    }
                    return;
                }

                // Test with first command only
                if (sequence.Commands.Any())
                {
                    var testSequence = new CommandSequence
                    {
                        Name = $"Test_{sequence.Name}",
                        Commands = new List<Command> { sequence.Commands.First() },
                        TargetProcessName = sequence.TargetProcessName,
                        TargetWindowTitle = sequence.TargetWindowTitle
                    };

                    PlaySequence(testSequence, 1);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error testing playback: {ex.Message}");
                NotifyPlaybackError($"Error testing playback: {ex.Message}", 0);
            }
        }

        #endregion

        #region Private Methods

        private async Task ExecuteSequenceAsync(CancellationToken cancellationToken)
        {
            while (currentCommandIndex < currentSequence.Commands.Count)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Handle pause
                while (isPaused)
                {
                    await Task.Delay(50, cancellationToken);
                }

                var command = currentSequence.Commands[currentCommandIndex];

                try
                {
                    System.Diagnostics.Debug.WriteLine($"Executing command {currentCommandIndex + 1}/{currentSequence.Commands.Count}: {command.Type} - {command.ElementName}");

                    await ExecuteCommandAsync(command, cancellationToken);

                    // Default delay between commands
                    if (DefaultDelayBetweenCommands > 0)
                    {
                        await Task.Delay(DefaultDelayBetweenCommands, cancellationToken);
                    }

                    NotifyCommandExecuted(command, true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Error executing command {currentCommandIndex + 1}: {ex.Message}");
                    NotifyCommandExecuted(command, false, ex.Message);

                    if (StopOnError)
                        throw;
                }

                currentCommandIndex++;
            }
        }

        private async Task ExecuteCommandAsync(Command command, CancellationToken cancellationToken)
        {
            try
            {
                // Focus target window if specified
                if (!string.IsNullOrEmpty(command.TargetWindow))
                {
                    await FocusTargetWindow(command);
                }

                switch (command.Type)
                {
                    case CommandType.Click:
                    case CommandType.DoubleClick:
                    case CommandType.RightClick:
                        await ExecuteClickCommand(command, cancellationToken);
                        break;

                    case CommandType.TypeText:
                        await ExecuteTypeCommand(command, cancellationToken);
                        break;

                    case CommandType.KeyPress:
                        await ExecuteKeyPressCommand(command, cancellationToken);
                        break;

                    case CommandType.Wait:
                        await ExecuteWaitCommand(command, cancellationToken);
                        break;

                    case CommandType.LoopStart:
                        ExecuteLoopStart(command);
                        break;

                    case CommandType.LoopEnd:
                        await ExecuteLoopEnd(command, cancellationToken);
                        break;

                    default:
                        System.Diagnostics.Debug.WriteLine($"⚠️ Unknown command type: {command.Type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error in ExecuteCommandAsync: {ex.Message}");
                throw;
            }
        }

        private async Task ExecuteClickCommand(Command command, CancellationToken cancellationToken)
        {
            try
            {
                UIElementInfo targetElement = null;

                // Try to find element by table cell identifier first
                if (command.IsTableCommand && !string.IsNullOrEmpty(command.TableCellIdentifier))
                {
                    IntPtr targetHandle = FindTargetWindow(command);
                    if (targetHandle != IntPtr.Zero)
                    {
                        targetElement = UIElementDetector.FindTableCellByIdentifier(
                            targetHandle,
                            command.TableCellIdentifier);

                        if (targetElement != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"✅ Found table cell: {command.TableCellIdentifier}");
                        }
                    }
                }

                // Fallback to element name search
                if (targetElement == null && !string.IsNullOrEmpty(command.ElementName))
                {
                    IntPtr targetHandle = FindTargetWindow(command);
                    if (targetHandle != IntPtr.Zero)
                    {
                        targetElement = UIElementDetector.FindElementByName(command.ElementName, targetHandle);
                    }
                }

                // Final fallback to coordinates
                if (targetElement == null)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Element not found, using coordinates: {command.ElementX}, {command.ElementY}");

                    // Execute click at coordinates using ActionSimulator
                    switch (command.Type)
                    {
                        case CommandType.Click:
                            actionSimulator.ClickAt(command.ElementX, command.ElementY);
                            break;
                        case CommandType.DoubleClick:
                            actionSimulator.DoubleClickAt(command.ElementX, command.ElementY);
                            break;
                        case CommandType.RightClick:
                            actionSimulator.RightClickAt(command.ElementX, command.ElementY);
                            break;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"✅ Found element: {targetElement.Name}");

                    // Execute click on element using coordinates
                    int clickX = targetElement.X;
                    int clickY = targetElement.Y;

                    switch (command.Type)
                    {
                        case CommandType.Click:
                            actionSimulator.ClickAt(clickX, clickY);
                            break;
                        case CommandType.DoubleClick:
                            actionSimulator.DoubleClickAt(clickX, clickY);
                            break;
                        case CommandType.RightClick:
                            actionSimulator.RightClickAt(clickX, clickY);
                            break;
                    }
                }

                await Task.Delay(50, cancellationToken); // Small delay after click
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to execute click command: {ex.Message}");
            }
        }

        private async Task ExecuteTypeCommand(Command command, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrEmpty(command.Value))
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ No text to type");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"⌨️ Typing text: {command.Value}");

                // Simple text typing using SendKeys
                foreach (char c in command.Value)
                {
                    SendKeys.SendWait(c.ToString());
                    await Task.Delay(10, cancellationToken); // Small delay between characters
                }

                await Task.Delay(100, cancellationToken); // Delay after typing
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to execute type command: {ex.Message}");
            }
        }

        private async Task ExecuteKeyPressCommand(Command command, CancellationToken cancellationToken)
        {
            try
            {
                if (command.KeyCode > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"🔑 Pressing key: {(Keys)command.KeyCode}");

                    // Simple key press using SendKeys
                    string keyToSend = GetSendKeysString((Keys)command.KeyCode);
                    if (!string.IsNullOrEmpty(keyToSend))
                    {
                        SendKeys.SendWait(keyToSend);
                    }
                }
                else if (!string.IsNullOrEmpty(command.Value))
                {
                    if (Enum.TryParse<Keys>(command.Value, out Keys key))
                    {
                        System.Diagnostics.Debug.WriteLine($"🔑 Pressing key: {key}");
                        string keyToSend = GetSendKeysString(key);
                        if (!string.IsNullOrEmpty(keyToSend))
                        {
                            SendKeys.SendWait(keyToSend);
                        }
                    }
                }

                await Task.Delay(50, cancellationToken); // Small delay after key press
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to execute key press command: {ex.Message}");
            }
        }

        private async Task ExecuteWaitCommand(Command command, CancellationToken cancellationToken)
        {
            try
            {
                if (int.TryParse(command.Value, out int waitTime))
                {
                    System.Diagnostics.Debug.WriteLine($"⏱️ Waiting {waitTime}ms");
                    await Task.Delay(waitTime, cancellationToken);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ Invalid wait time, using default 1000ms");
                    await Task.Delay(1000, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to execute wait command: {ex.Message}");
            }
        }

        private void ExecuteLoopStart(Command command)
        {
            try
            {
                int iterations = 1;
                if (int.TryParse(command.Value, out int parseResult))
                {
                    iterations = Math.Max(1, parseResult);
                }

                var loopContext = new LoopContext
                {
                    StartIndex = currentCommandIndex,
                    Iterations = iterations,
                    CurrentIteration = 0,
                    LoopName = command.ElementName
                };

                loopStack.Push(loopContext);
                System.Diagnostics.Debug.WriteLine($"🔄 Loop start: {iterations} iterations");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to execute loop start: {ex.Message}");
            }
        }

        private async Task ExecuteLoopEnd(Command command, CancellationToken cancellationToken)
        {
            try
            {
                if (loopStack.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ Loop end without matching loop start");
                    return;
                }

                var loopContext = loopStack.Peek();
                loopContext.CurrentIteration++;

                System.Diagnostics.Debug.WriteLine($"🔄 Loop iteration {loopContext.CurrentIteration}/{loopContext.Iterations}");

                if (loopContext.CurrentIteration < loopContext.Iterations)
                {
                    // Continue loop - jump back to start
                    currentCommandIndex = loopContext.StartIndex;
                    await Task.Delay(100, cancellationToken); // Small delay between loop iterations
                }
                else
                {
                    // Loop completed
                    loopStack.Pop();
                    System.Diagnostics.Debug.WriteLine($"✅ Loop completed: {loopContext.LoopName}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to execute loop end: {ex.Message}");
            }
        }

        private async Task FocusTargetWindow(Command command)
        {
            try
            {
                IntPtr targetHandle = FindTargetWindow(command);

                if (targetHandle != IntPtr.Zero)
                {
                    if (IsWindow(targetHandle) && IsWindowVisible(targetHandle))
                    {
                        SetForegroundWindow(targetHandle);
                        await Task.Delay(200); // Give window time to focus
                        System.Diagnostics.Debug.WriteLine("🎯 Window focus completed");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("⚠️ Target window is not valid or visible");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ No target window handle available");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Could not focus target window: {ex.Message}");
            }
        }

        private IntPtr FindTargetWindow(Command command)
        {
            try
            {
                // Simple approach - try to find window by process name
                if (!string.IsNullOrEmpty(currentSequence.TargetProcessName))
                {
                    var processes = System.Diagnostics.Process.GetProcessesByName(currentSequence.TargetProcessName);
                    foreach (var process in processes)
                    {
                        if (process.MainWindowHandle != IntPtr.Zero)
                        {
                            return process.MainWindowHandle;
                        }
                    }
                }

                return IntPtr.Zero;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error finding target window: {ex.Message}");
                return IntPtr.Zero;
            }
        }

        private string GetSendKeysString(Keys key)
        {
            // Convert Keys enum to SendKeys string format
            switch (key)
            {
                case Keys.Enter: return "{ENTER}";
                case Keys.Tab: return "{TAB}";
                case Keys.Escape: return "{ESC}";
                case Keys.Space: return " ";
                case Keys.Back: return "{BACKSPACE}";
                case Keys.Delete: return "{DELETE}";
                case Keys.Home: return "{HOME}";
                case Keys.End: return "{END}";
                case Keys.PageUp: return "{PGUP}";
                case Keys.PageDown: return "{PGDN}";
                case Keys.Up: return "{UP}";
                case Keys.Down: return "{DOWN}";
                case Keys.Left: return "{LEFT}";
                case Keys.Right: return "{RIGHT}";
                case Keys.F1: return "{F1}";
                case Keys.F2: return "{F2}";
                case Keys.F3: return "{F3}";
                case Keys.F4: return "{F4}";
                case Keys.F5: return "{F5}";
                case Keys.F6: return "{F6}";
                case Keys.F7: return "{F7}";
                case Keys.F8: return "{F8}";
                case Keys.F9: return "{F9}";
                case Keys.F10: return "{F10}";
                case Keys.F11: return "{F11}";
                case Keys.F12: return "{F12}";
                default:
                    // For regular characters, just return the character
                    if (key >= Keys.A && key <= Keys.Z)
                        return key.ToString().ToLower();
                    if (key >= Keys.D0 && key <= Keys.D9)
                        return ((int)(key - Keys.D0)).ToString();
                    return string.Empty;
            }
        }

        private ValidationResult ValidateSequence(CommandSequence sequence)
        {
            // Use existing ValidationResult from DebugTestHelper
            IntPtr targetHandle = FindTargetWindow(sequence.Commands.FirstOrDefault() ?? new Command());
            return DebugTestHelper.ValidateSequenceWithWinUI3(sequence, targetHandle);
        }

        private void CompletePlayback(bool success, string message)
        {
            try
            {
                isPlaying = false;
                isPaused = false;
                loopStack.Clear();

                PlaybackCompleted?.Invoke(this, new PlaybackCompletedEventArgs
                {
                    Success = success,
                    Message = message,
                    CommandsExecuted = currentCommandIndex,
                    TotalCommands = TotalCommands
                });

                NotifyPlaybackStateChanged(PlaybackState.Stopped, message);
                System.Diagnostics.Debug.WriteLine($"🏁 Playback completed: {message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error completing playback: {ex.Message}");
            }
        }

        private void NotifyCommandExecuted(Command command, bool success, string error = "")
        {
            try
            {
                CommandExecuted?.Invoke(this, new CommandExecutedEventArgs
                {
                    Command = command,
                    Success = success,
                    ErrorMessage = error,
                    CommandIndex = currentCommandIndex,
                    TotalCommands = TotalCommands
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error notifying command executed: {ex.Message}");
            }
        }

        private void NotifyPlaybackStateChanged(PlaybackState state, string additionalInfo = "")
        {
            try
            {
                PlaybackStateChanged?.Invoke(this, new PlaybackStateChangedEventArgs
                {
                    State = state,
                    CurrentIndex = currentCommandIndex,
                    TotalCommands = TotalCommands,
                    SequenceName = currentSequence?.Name ?? string.Empty,
                    AdditionalInfo = additionalInfo
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error notifying playback state change: {ex.Message}");
            }
        }

        private void NotifyPlaybackError(string error, int commandIndex)
        {
            try
            {
                PlaybackError?.Invoke(this, new PlaybackErrorEventArgs
                {
                    ErrorMessage = error,
                    CommandIndex = commandIndex,
                    Command = commandIndex < currentSequence?.Commands.Count ? currentSequence.Commands[commandIndex] : null
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error notifying playback error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            try
            {
                Stop();
                cancellationTokenSource?.Dispose();
                System.Diagnostics.Debug.WriteLine("🧹 CommandPlayer disposed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error disposing CommandPlayer: {ex.Message}");
            }
        }

        #endregion
    

        #region Helper Classes and Enums

        private void HighlightPosition(int x, int y)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🎯 Highlighting position: ({x}, {y})");
                // TODO: Implement visual highlighting if needed
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Error highlighting position: {ex.Message}");
            }
        }

        private void HighlightElement(UIElementInfo element)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🎯 Highlighting element: {element.Name} at ({element.X}, {element.Y})");
                // TODO: Implement visual highlighting if needed
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Error highlighting element: {ex.Message}");
            }
        }


        public enum PlaybackState
        {
            Started,
            Stopped,
            Paused,
            Resumed
        }

        // Event argument classes
        public class CommandExecutedEventArgs : EventArgs
        {
            public Command Command { get; set; }
            public bool Success { get; set; }
            public string ErrorMessage { get; set; } = string.Empty;
            public int CommandIndex { get; set; }
            public int TotalCommands { get; set; }
        }

        public class PlaybackStateChangedEventArgs : EventArgs
        {
            public PlaybackState State { get; set; }
            public int CurrentIndex { get; set; }
            public int TotalCommands { get; set; }
            public string SequenceName { get; set; } = string.Empty;
            public string AdditionalInfo { get; set; } = string.Empty;
        }

        public class PlaybackErrorEventArgs : EventArgs
        {
            public string ErrorMessage { get; set; } = string.Empty;
            public int CommandIndex { get; set; }
            public Command Command { get; set; }
        }

        public class PlaybackCompletedEventArgs : EventArgs
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public int CommandsExecuted { get; set; }
            public int TotalCommands { get; set; }
        }
    }
    #endregion
}
