using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Automation;

namespace AppCommander.W7_11.WPF.Core
{
    public class CommandPlayer
    {
        private readonly ActionSimulator actionSimulator;
        private CommandSequence currentSequence;
        private bool isPlaying = false;
        private bool isPaused = false;
        private CancellationTokenSource cancellationTokenSource;
        private int currentCommandIndex = 0;
        private readonly Stack<LoopContext> loopStack;

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

        // Settings
        public int DefaultDelayBetweenCommands { get; set; } = 100; // ms
        public bool StopOnError { get; set; } = true;
        public bool HighlightTargetElements { get; set; } = true;
        public IntPtr TargetWindow { get; set; } = IntPtr.Zero;

        public CommandPlayer()
        {
            actionSimulator = new ActionSimulator();
            loopStack = new Stack<LoopContext>();
        }

        public async Task PlaySequenceAsync(CommandSequence sequence, IntPtr targetWindow = default(IntPtr))
        {
            System.Diagnostics.Debug.WriteLine("PlaySequenceAsync started");

            if (isPlaying)
                throw new InvalidOperationException("Already playing a sequence");

            if (sequence == null || sequence.Commands.Count == 0)
                throw new ArgumentException("Sequence is empty or null");

            System.Diagnostics.Debug.WriteLine($"Sequence has {sequence.Commands.Count} commands");

            currentSequence = sequence;
            currentCommandIndex = 0;
            loopStack.Clear();

            try
            {
                // Adaptívne vyhľadanie cieľového okna
                if (sequence.AutoFindTarget && targetWindow == IntPtr.Zero)
                {
                    System.Diagnostics.Debug.WriteLine("Attempting to find target window");
                    System.Diagnostics.Debug.WriteLine($"TargetProcessName: '{sequence.TargetProcessName}'");
                    System.Diagnostics.Debug.WriteLine($"TargetWindowTitle: '{sequence.TargetWindowTitle}'");

                    if (!string.IsNullOrEmpty(sequence.TargetProcessName))
                    {
                        var searchResult = WindowFinder.SmartFindWindow(
                            sequence.TargetProcessName,
                            sequence.TargetWindowTitle,
                            sequence.TargetWindowClass);

                        if (searchResult.IsValid)
                        {
                            TargetWindow = searchResult.Handle;
                            System.Diagnostics.Debug.WriteLine($"Found target window via {searchResult.MatchMethod}");
                            NotifyPlaybackStateChanged(PlaybackState.Started,
                                $"Found target window via {searchResult.MatchMethod}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Window search failed, waiting for application: {sequence.TargetProcessName}");
                            NotifyPlaybackStateChanged(PlaybackState.Started,
                                $"Waiting for application: {sequence.TargetProcessName}");

                            TargetWindow = WindowFinder.WaitForApplication(
                                sequence.TargetProcessName,
                                sequence.MaxWaitTimeSeconds);
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("No target process name specified - using global mode");
                        TargetWindow = IntPtr.Zero; // Global mode
                    }
                }
                else
                {
                    TargetWindow = targetWindow;
                    System.Diagnostics.Debug.WriteLine($"Using provided target window: {targetWindow}");
                }

                // Aktualizuj príkazy na základe aktuálneho stavu okna
                if (TargetWindow != IntPtr.Zero)
                {
                    System.Diagnostics.Debug.WriteLine("Updating commands for current window");
                    AdaptiveElementFinder.UpdateCommandsForCurrentWindow(TargetWindow, currentSequence.Commands);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("No target window - using global coordinates");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in window finding: {ex.Message}");
                // Pre testovanie - pokračuj aj bez target okna v global mode
                TargetWindow = IntPtr.Zero;
                System.Diagnostics.Debug.WriteLine("Continuing in global mode");
            }

            cancellationTokenSource = new CancellationTokenSource();
            isPlaying = true;
            isPaused = false;

            NotifyPlaybackStateChanged(PlaybackState.Started);

            try
            {
                System.Diagnostics.Debug.WriteLine("Starting sequence execution");
                await ExecuteSequenceAsync(cancellationTokenSource.Token);

                NotifyPlaybackCompleted(true, "Sequence completed successfully");
            }
            catch (OperationCanceledException)
            {
                NotifyPlaybackCompleted(false, "Playback was cancelled");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in sequence execution: {ex.Message}");
                NotifyPlaybackError(ex.Message, currentCommandIndex);

                if (StopOnError)
                {
                    NotifyPlaybackCompleted(false, $"Stopped due to error: {ex.Message}");
                }
            }
            finally
            {
                isPlaying = false;
                isPaused = false;
                cancellationTokenSource?.Dispose();
                cancellationTokenSource = null;
            }
        }

        public void StopPlayback()
        {
            if (!isPlaying)
                return;

            cancellationTokenSource?.Cancel();
            NotifyPlaybackStateChanged(PlaybackState.Stopped);
        }

        public void PausePlayback()
        {
            if (!isPlaying || isPaused)
                return;

            isPaused = true;
            NotifyPlaybackStateChanged(PlaybackState.Paused);
        }

        public void ResumePlayback()
        {
            if (!isPlaying || !isPaused)
                return;

            isPaused = false;
            NotifyPlaybackStateChanged(PlaybackState.Resumed);
        }

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
                    await ExecuteCommandAsync(command, cancellationToken);

                    // Default delay between commands
                    if (DefaultDelayBetweenCommands > 0)
                    {
                        await Task.Delay(DefaultDelayBetweenCommands, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    NotifyCommandExecuted(command, false, ex.Message);

                    if (StopOnError)
                        throw;
                }

                currentCommandIndex++;
            }
        }

        private async Task ExecuteCommandAsync(Command command, CancellationToken cancellationToken)
        {
            bool success = false;
            string errorMessage = string.Empty;

            try
            {
                switch (command.Type)
                {
                    case CommandType.Click:
                    case CommandType.DoubleClick:
                    case CommandType.RightClick:
                    case CommandType.MouseClick:
                        success = await ExecuteMouseCommand(command);
                        break;

                    case CommandType.KeyPress:
                        success = await ExecuteKeyCommand(command);
                        break;

                    case CommandType.SetText:
                        success = await ExecuteSetTextCommand(command);
                        break;

                    case CommandType.Wait:
                        success = await ExecuteWaitCommand(command, cancellationToken);
                        break;

                    case CommandType.Loop:
                        success = ExecuteLoopStart(command);
                        break;

                    case CommandType.LoopEnd:
                        success = ExecuteLoopEnd(command);
                        break;

                    default:
                        throw new NotSupportedException($"Command type {command.Type} is not supported");
                }
            }
            catch (Exception ex)
            {
                success = false;
                errorMessage = ex.Message;
            }

            NotifyCommandExecuted(command, success, errorMessage);
        }

        private async Task<bool> ExecuteMouseCommand(Command command)
        {
            IntPtr targetHandle = TargetWindow != IntPtr.Zero ? TargetWindow : IntPtr.Zero;

            // Použij adaptívne vyhľadávanie prvkov
            var searchResult = AdaptiveElementFinder.SmartFindElement(targetHandle, command);

            UIElementInfo element = null;
            int x, y;

            if (searchResult.IsSuccess && searchResult.Element != null)
            {
                element = searchResult.Element;
                x = element.X;
                y = element.Y;

                // Oznám úspešné nájdenie
                NotifyCommandExecuted(command, true,
                    $"Element found via {searchResult.SearchMethod} (confidence: {searchResult.Confidence:P0})");
            }
            else
            {
                // Fallback na uložené súradnice
                x = command.ElementX;
                y = command.ElementY;

                if (x <= 0 || y <= 0)
                {
                    throw new InvalidOperationException(
                        $"Could not find element '{command.ElementName}' and no valid coordinates stored. " +
                        $"Search error: {searchResult.ErrorMessage}");
                }

                // Varovanie o použití starých súradníc
                NotifyCommandExecuted(command, true,
                    $"Using stored coordinates - element search failed: {searchResult.ErrorMessage}");
            }

            // Highlight element if enabled
            if (HighlightTargetElements && element != null)
            {
                HighlightElement(element);
            }

            // Execute mouse action based on command type
            for (int i = 0; i < command.RepeatCount; i++)
            {
                switch (command.Type)
                {
                    case CommandType.Click:
                    case CommandType.MouseClick:
                        actionSimulator.ClickAt(x, y);
                        break;
                    case CommandType.DoubleClick:
                        actionSimulator.DoubleClickAt(x, y);
                        break;
                    case CommandType.RightClick:
                        actionSimulator.RightClickAt(x, y);
                        break;
                }

                if (i < command.RepeatCount - 1)
                    await Task.Delay(50); // Small delay between repeats
            }

            return true;
        }

        private async Task<bool> ExecuteKeyCommand(Command command)
        {
            for (int i = 0; i < command.RepeatCount; i++)
            {
                actionSimulator.SendKey(command.Key);

                if (i < command.RepeatCount - 1)
                    await Task.Delay(50); // Small delay between repeats
            }

            return true;
        }

        private async Task<bool> ExecuteSetTextCommand(Command command)
        {
            // Use adaptive element finder
            IntPtr targetHandle = TargetWindow != IntPtr.Zero ? TargetWindow : IntPtr.Zero;
            var searchResult = AdaptiveElementFinder.SmartFindElement(targetHandle, command);

            UIElementInfo element = null;

            if (searchResult.IsSuccess && searchResult.Element != null)
            {
                element = searchResult.Element;

                // Try to use automation pattern for text input
                if (element.AutomationElement != null)
                {
                    try
                    {
                        // Try ValuePattern first (for textboxes)
                        if (element.AutomationElement.TryGetCurrentPattern(ValuePattern.Pattern, out object valuePattern))
                        {
                            var value = valuePattern as ValuePattern;
                            if (value != null && !value.Current.IsReadOnly)
                            {
                                value.SetValue(command.Value);
                                return true;
                            }
                        }

                        // Try TextPattern as alternative
                        if (element.AutomationElement.TryGetCurrentPattern(TextPattern.Pattern, out object textPattern))
                        {
                            var text = textPattern as TextPattern;
                            if (text != null)
                            {
                                // Focus the element first
                                element.AutomationElement.SetFocus();
                                await Task.Delay(100);

                                // Select all text and replace
                                actionSimulator.SendKeyCombo(Keys.Control, Keys.A);
                                await Task.Delay(50);
                                actionSimulator.SendText(command.Value);
                                return true;
                            }
                        }
                    }
                    catch
                    {
                        // Fall back to click and type
                    }
                }

                // Click to focus the element first
                actionSimulator.ClickAt(element.X, element.Y);
                await Task.Delay(100);
            }
            else
            {
                // Fallback: use stored coordinates
                if (command.ElementX > 0 && command.ElementY > 0)
                {
                    actionSimulator.ClickAt(command.ElementX, command.ElementY);
                    await Task.Delay(100);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Cannot find text element '{command.ElementName}' and no coordinates stored. " +
                        $"Search error: {searchResult.ErrorMessage}");
                }
            }

            // Clear existing text and send new text
            actionSimulator.SendKeyCombo(Keys.Control, Keys.A);
            await Task.Delay(50);
            actionSimulator.SendText(command.Value);

            return true;
        }

        private async Task<bool> ExecuteWaitCommand(Command command, CancellationToken cancellationToken)
        {
            if (int.TryParse(command.Value, out int waitTime))
            {
                await Task.Delay(waitTime, cancellationToken);
                return true;
            }

            throw new ArgumentException($"Invalid wait time: {command.Value}");
        }

        private bool ExecuteLoopStart(Command command)
        {
            var loopContext = new LoopContext
            {
                StartIndex = currentCommandIndex,
                Iterations = command.RepeatCount,
                CurrentIteration = 0,
                LoopName = command.ElementName
            };

            loopStack.Push(loopContext);
            return true;
        }

        private bool ExecuteLoopEnd(Command command)
        {
            if (loopStack.Count == 0)
                throw new InvalidOperationException("LoopEnd without corresponding Loop start");

            var loopContext = loopStack.Peek();
            loopContext.CurrentIteration++;

            if (loopContext.CurrentIteration < loopContext.Iterations)
            {
                // Continue loop - jump back to start
                currentCommandIndex = loopContext.StartIndex;
            }
            else
            {
                // Loop completed - remove from stack
                loopStack.Pop();
            }

            return true;
        }

        private void HighlightElement(UIElementInfo element)
        {
            // TODO: Implement element highlighting (e.g., draw red border around element)
            // This could use Graphics overlay or Windows API to draw a temporary border
        }

        private void NotifyCommandExecuted(Command command, bool success, string error = "")
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

        private void NotifyPlaybackStateChanged(PlaybackState state, string additionalInfo = "")
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

        private void NotifyPlaybackError(string error, int commandIndex)
        {
            PlaybackError?.Invoke(this, new PlaybackErrorEventArgs
            {
                ErrorMessage = error,
                CommandIndex = commandIndex,
                Command = commandIndex < currentSequence.Commands.Count ? currentSequence.Commands[commandIndex] : null
            });
        }

        private void NotifyPlaybackCompleted(bool success, string message)
        {
            PlaybackCompleted?.Invoke(this, new PlaybackCompletedEventArgs
            {
                Success = success,
                Message = message,
                CommandsExecuted = currentCommandIndex,
                TotalCommands = TotalCommands
            });
        }
    }

    // Helper classes
    internal class LoopContext
    {
        public int StartIndex { get; set; }
        public int Iterations { get; set; }
        public int CurrentIteration { get; set; }
        public string LoopName { get; set; }
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
