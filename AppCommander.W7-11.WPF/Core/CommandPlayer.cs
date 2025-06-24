using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Automation;
using AppCommander.W7_11.WPF.Core;
using System.Runtime.InteropServices;

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

        public bool PreferElementIdentifiers { get; set; } = true;
        public bool EnableAdaptiveFinding { get; set; } = true;
        public int MaxElementSearchAttempts { get; set; } = 3;

        // Settings - zvýšené delays pre vyššiu spoľahlivosť
        public int DefaultDelayBetweenCommands { get; set; } = 300; // Zvýšené z 200ms na 300ms
        public bool StopOnError { get; set; } = true;
        public bool HighlightTargetElements { get; set; } = true;
        public IntPtr TargetWindow { get; set; } = IntPtr.Zero;
        public int WindowFocusDelay { get; set; } = 200; // Nový delay pre focus okna
        public int ElementSearchRetries { get; set; } = 3; // Počet pokusov pri hľadaní elementu

        public CommandPlayer()
        {
            actionSimulator = new ActionSimulator();
            loopStack = new Stack<LoopContext>();

            // Nastavenia pre ActionSimulator pre vyššiu spoľahlivosť
            actionSimulator.ClickDelay = 100; // Zvýšené z 50ms
            actionSimulator.KeyDelay = 100;   // Zvýšené z 50ms  
            actionSimulator.ActionDelay = 50; // Zvýšené z 10ms
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
                // Pokus o nájdenie cieľového okna s lepším error handlingom
                await FindAndValidateTargetWindow(sequence, targetWindow);

                cancellationTokenSource = new CancellationTokenSource();
                isPlaying = true;
                isPaused = false;

                NotifyPlaybackStateChanged(PlaybackState.Started);

                System.Diagnostics.Debug.WriteLine("Starting sequence execution");
                await ExecuteSequenceAsync(cancellationTokenSource.Token);

                NotifyPlaybackCompleted(true, "Sequence completed successfully");
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("Playback was cancelled");
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
                NotifyPlaybackStateChanged(PlaybackState.Stopped);
            }
        }

        /// <summary>
        /// Pre-execution analýza príkazov
        /// </summary>
        public void AnalyzeCommandsBeforeExecution(CommandSequence sequence)
        {
            if (sequence == null || TargetWindow == IntPtr.Zero) return;

            System.Diagnostics.Debug.WriteLine("\n=== PRE-EXECUTION ANALYSIS ===");
            System.Diagnostics.Debug.WriteLine($"Total commands: {sequence.Commands.Count}");

            var clickCommands = sequence.Commands.Where(c =>
                c.Type == CommandType.Click || c.Type == CommandType.DoubleClick ||
                c.Type == CommandType.RightClick || c.Type == CommandType.SetText).ToList();

            System.Diagnostics.Debug.WriteLine($"Commands requiring element finding: {clickCommands.Count}");

            if (EnableAdaptiveFinding && clickCommands.Any())
            {
                int foundCount = 0;
                int winui3Count = 0;

                foreach (var cmd in clickCommands)
                {
                    try
                    {
                        var searchResult = AdaptiveElementFinder.SmartFindElement(TargetWindow, cmd);
                        if (searchResult.IsSuccess)
                        {
                            foundCount++;
                            System.Diagnostics.Debug.WriteLine($"  ✓ {cmd.ElementName} -> found via {searchResult.SearchMethod}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"  ✗ {cmd.ElementName} -> {searchResult.ErrorMessage}");
                        }

                        if (cmd.IsWinUI3Element) winui3Count++;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"  ✗ {cmd.ElementName} -> Error: {ex.Message}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Elements found: {foundCount}/{clickCommands.Count}");
                if (winui3Count > 0)
                    System.Diagnostics.Debug.WriteLine($"WinUI3 elements: {winui3Count}");

                if (foundCount < clickCommands.Count)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️  Some elements may not be found during execution - will fallback to coordinates");
                }
            }

            System.Diagnostics.Debug.WriteLine("=== ANALYSIS COMPLETE ===\n");
        }

        private async Task FindAndValidateTargetWindow(CommandSequence sequence, IntPtr providedTargetWindow)
        {
            await Task.Run(() =>
            {
                // Adaptívne vyhľadanie cieľového okna s retry logikou
                if (sequence.AutoFindTarget && providedTargetWindow == IntPtr.Zero)
                {
                    System.Diagnostics.Debug.WriteLine("Attempting to find target window");
                    System.Diagnostics.Debug.WriteLine($"TargetProcessName: '{sequence.TargetProcessName}'");
                    System.Diagnostics.Debug.WriteLine($"TargetWindowTitle: '{sequence.TargetWindowTitle}'");

                    if (!string.IsNullOrEmpty(sequence.TargetProcessName))
                    {
                        // Pokus 1: Smart search
                        var searchResult = WindowTrackingInfo.SmartFindWindow(
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
                            // Pokus 2: Čakanie na aplikáciu
                            System.Diagnostics.Debug.WriteLine($"Window search failed, waiting for application: {sequence.TargetProcessName}");
                            NotifyPlaybackStateChanged(PlaybackState.Started,
                                $"Waiting for application: {sequence.TargetProcessName}");

                            TargetWindow = WindowTrackingInfo.WaitForApplication(
                                sequence.TargetProcessName,
                                sequence.MaxWaitTimeSeconds);

                            if (TargetWindow == IntPtr.Zero)
                            {
                                throw new InvalidOperationException(
                                    $"Target application '{sequence.TargetProcessName}' not found or not responding. " +
                                    $"Please ensure the application is running and try again.");
                            }
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
                    TargetWindow = providedTargetWindow;
                    System.Diagnostics.Debug.WriteLine($"Using provided target window: {providedTargetWindow}");
                }

                // Validácia target okna
                if (TargetWindow != IntPtr.Zero)
                {
                    if (!IsWindow(TargetWindow) || !IsWindowVisible(TargetWindow))
                    {
                        System.Diagnostics.Debug.WriteLine("Target window is not valid or visible, switching to global mode");
                        TargetWindow = IntPtr.Zero;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Target window validated successfully");

                        // Aktualizuj príkazy na základe aktuálneho stavu okna
                        System.Diagnostics.Debug.WriteLine("Updating commands for current window");
                        AdaptiveElementFinder.UpdateCommandsForCurrentWindow(TargetWindow, currentSequence.Commands);
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Operating in global mode - using stored coordinates");
                }
            });
        }

        public void StopPlayback()
        {
            if (!isPlaying)
                return;

            cancellationTokenSource?.Cancel();
            System.Diagnostics.Debug.WriteLine("Playback stop requested");
        }

        public void PausePlayback()
        {
            if (!isPlaying || isPaused)
                return;

            isPaused = true;
            NotifyPlaybackStateChanged(PlaybackState.Paused);
            System.Diagnostics.Debug.WriteLine("Playback paused");
        }

        public void ResumePlayback()
        {
            if (!isPlaying || !isPaused)
                return;

            isPaused = false;
            NotifyPlaybackStateChanged(PlaybackState.Resumed);
            System.Diagnostics.Debug.WriteLine("Playback resumed");
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
                    System.Diagnostics.Debug.WriteLine($"Executing command {currentCommandIndex + 1}/{currentSequence.Commands.Count}: {command}");

                    await ExecuteCommandAsync(command, cancellationToken);

                    // Default delay between commands
                    if (DefaultDelayBetweenCommands > 0)
                    {
                        await Task.Delay(DefaultDelayBetweenCommands, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error executing command {currentCommandIndex + 1}: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"Exception in ExecuteCommandAsync: {ex.Message}");
            }

            NotifyCommandExecuted(command, success, errorMessage);
        }

        private async Task<bool> ExecuteMouseCommand(Command command)
        {
            System.Diagnostics.Debug.WriteLine($"ExecuteMouseCommand: {command.ElementName} (Type: {command.Type})");

            // Ensure target window is focused if we have one
            await EnsureWindowFocused();

            UIElementInfo element = null;
            int x = 0, y = 0;
            string searchMethod = "None";

            // DÔLEŽITÉ: Najprv skús originálne súradnice, ak sú validné
            bool useOriginalCoordinates = false;
            if (command.ElementX > 0 && command.ElementY > 0 &&
                actionSimulator.IsPointOnScreen(command.ElementX, command.ElementY))
            {
                x = command.ElementX;
                y = command.ElementY;
                useOriginalCoordinates = true;
                searchMethod = "Original coordinates";
                System.Diagnostics.Debug.WriteLine($"Using original click coordinates ({x}, {y}) - screen position valid");
            }

            // Ak originálne súradnice nie sú dobré, skús nájsť element
            if (!useOriginalCoordinates && TargetWindow != IntPtr.Zero)
            {
                for (int retry = 0; retry < ElementSearchRetries; retry++)
                {
                    var searchResult = AdaptiveElementFinder.SmartFindElement(TargetWindow, command);

                    if (searchResult.IsSuccess && searchResult.Element != null)
                    {
                        element = searchResult.Element;
                        x = element.X;
                        y = element.Y;
                        searchMethod = searchResult.SearchMethod;

                        System.Diagnostics.Debug.WriteLine($"Element found via {searchMethod} at ({x}, {y}) on retry {retry + 1}");
                        break;
                    }
                    else if (retry < ElementSearchRetries - 1)
                    {
                        System.Diagnostics.Debug.WriteLine($"Element search failed on retry {retry + 1}, retrying...");
                        await Task.Delay(100);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Element search failed after {ElementSearchRetries} retries: {searchResult.ErrorMessage}");
                    }
                }
            }

            // Final fallback na uložené súradnice (ak nie sú už použité)
            if (!useOriginalCoordinates && element == null)
            {
                x = command.ElementX;
                y = command.ElementY;
                searchMethod = "Stored coordinates (fallback)";

                System.Diagnostics.Debug.WriteLine($"Using stored coordinates as final fallback ({x}, {y})");

                if (x <= 0 || y <= 0)
                {
                    var errorMsg = $"Could not find element '{command.ElementName}' and no valid coordinates stored. " +
                        $"Element search failed after {ElementSearchRetries} retries.";
                    System.Diagnostics.Debug.WriteLine($"ERROR: {errorMsg}");
                    throw new InvalidOperationException(errorMsg);
                }
            }

            // Validate coordinates are on screen
            if (!actionSimulator.IsPointOnScreen(x, y))
            {
                var errorMsg = $"Coordinates ({x}, {y}) are outside screen bounds. " +
                    $"Screen resolution may have changed since recording. " +
                    $"Original recording coordinates: ({command.ElementX}, {command.ElementY})";
                System.Diagnostics.Debug.WriteLine($"ERROR: {errorMsg}");
                throw new InvalidOperationException(errorMsg);
            }

            System.Diagnostics.Debug.WriteLine($"About to {command.Type} at ({x}, {y}) using {searchMethod}");

            // Highlight element if enabled
            if (HighlightTargetElements && element != null)
            {
                HighlightElement(element);
            }

            // Execute mouse action based on command type with improved retry logic
            for (int i = 0; i < command.RepeatCount; i++)
            {
                System.Diagnostics.Debug.WriteLine($"Executing {command.Type} #{i + 1}/{command.RepeatCount} at ({x}, {y})");

                try
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

                    System.Diagnostics.Debug.WriteLine($"Successfully executed {command.Type} #{i + 1} at ({x}, {y})");

                    if (i < command.RepeatCount - 1)
                        await Task.Delay(100); // Delay between repeats
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error executing {command.Type} #{i + 1}: {ex.Message}");
                    throw;
                }
            }

            return true;
        }

        private async Task<bool> ExecuteKeyCommand(Command command)
        {
            System.Diagnostics.Debug.WriteLine($"ExecuteKeyCommand: {command.Key} (Value: {command.Value})");

            // Ensure target window is focused if we have one
            await EnsureWindowFocused();

            for (int i = 0; i < command.RepeatCount; i++)
            {
                System.Diagnostics.Debug.WriteLine($"Sending key #{i + 1}/{command.RepeatCount}: {command.Key}");

                try
                {
                    // Add extra delay for each key to ensure it's processed
                    await Task.Delay(50);

                    actionSimulator.SendKey(command.Key);

                    await Task.Delay(150); // Increased delay after key to ensure processing

                    if (i < command.RepeatCount - 1)
                        await Task.Delay(100); // Additional delay between repeats
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error sending key {command.Key}: {ex.Message}");
                    throw;
                }
            }

            System.Diagnostics.Debug.WriteLine($"Successfully sent key: {command.Key}");
            return true;
        }

        private async Task<bool> ExecuteSetTextCommand(Command command)
        {
            System.Diagnostics.Debug.WriteLine($"ExecuteSetTextCommand: '{command.Value}' to element '{command.ElementName}'");

            // Ensure target window is focused
            await EnsureWindowFocused();

            UIElementInfo element = null;
            string searchMethod = "None";

            // Use adaptive element finder with retry logic
            if (TargetWindow != IntPtr.Zero)
            {
                for (int retry = 0; retry < ElementSearchRetries; retry++)
                {
                    var searchResult = AdaptiveElementFinder.SmartFindElement(TargetWindow, command);

                    if (searchResult.IsSuccess && searchResult.Element != null)
                    {
                        element = searchResult.Element;
                        searchMethod = searchResult.SearchMethod;
                        System.Diagnostics.Debug.WriteLine($"Text element found via {searchMethod} on retry {retry + 1}");
                        break;
                    }
                    else if (retry < ElementSearchRetries - 1)
                    {
                        System.Diagnostics.Debug.WriteLine($"Text element search failed on retry {retry + 1}, retrying...");
                        await Task.Delay(100);
                    }
                }
            }

            // Try to use automation pattern for text input
            if (element?.AutomationElement != null)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("Attempting to use UI Automation patterns for text input");

                    // Try ValuePattern first (for textboxes)
                    if (element.AutomationElement.TryGetCurrentPattern(ValuePattern.Pattern, out object valuePattern))
                    {
                        var value = valuePattern as ValuePattern;
                        if (value != null && !value.Current.IsReadOnly)
                        {
                            System.Diagnostics.Debug.WriteLine("Using ValuePattern.SetValue()");
                            value.SetValue(command.Value);
                            await Task.Delay(100); // Give time for the value to be set
                            return true;
                        }
                    }

                    // Try TextPattern as alternative
                    if (element.AutomationElement.TryGetCurrentPattern(TextPattern.Pattern, out object textPattern))
                    {
                        System.Diagnostics.Debug.WriteLine("Using TextPattern with focus and keyboard input");
                        element.AutomationElement.SetFocus();
                        await Task.Delay(200);

                        // Select all text and replace
                        actionSimulator.SendKeyCombo(Keys.Control, Keys.A);
                        await Task.Delay(100);
                        actionSimulator.SendText(command.Value);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"UI Automation failed: {ex.Message}, falling back to click and type");
                }
            }

            // Fallback: Click element and type text
            if (element != null)
            {
                System.Diagnostics.Debug.WriteLine($"Fallback: clicking element at ({element.X}, {element.Y}) and typing text");
                actionSimulator.ClickAt(element.X, element.Y);
                await Task.Delay(200);
            }
            else if (command.ElementX > 0 && command.ElementY > 0)
            {
                System.Diagnostics.Debug.WriteLine($"Fallback: clicking stored coordinates ({command.ElementX}, {command.ElementY}) and typing text");
                actionSimulator.ClickAt(command.ElementX, command.ElementY);
                await Task.Delay(200);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Cannot find text element '{command.ElementName}' and no coordinates stored.");
            }

            // Clear existing text and send new text
            System.Diagnostics.Debug.WriteLine("Clearing existing text and sending new text");
            actionSimulator.SendKeyCombo(Keys.Control, Keys.A);
            await Task.Delay(100);
            actionSimulator.SendText(command.Value);
            await Task.Delay(100);

            return true;
        }

        private async Task<bool> ExecuteWaitCommand(Command command, CancellationToken cancellationToken)
        {
            if (int.TryParse(command.Value, out int waitTime))
            {
                System.Diagnostics.Debug.WriteLine($"Waiting for {waitTime}ms");
                await Task.Delay(waitTime, cancellationToken);
                return true;
            }

            throw new ArgumentException($"Invalid wait time: {command.Value}");
        }

        private bool ExecuteLoopStart(Command command)
        {
            System.Diagnostics.Debug.WriteLine($"Starting loop: {command.ElementName} with {command.RepeatCount} iterations");

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

            System.Diagnostics.Debug.WriteLine($"Loop iteration {loopContext.CurrentIteration}/{loopContext.Iterations} completed");

            if (loopContext.CurrentIteration < loopContext.Iterations)
            {
                // Continue loop - jump back to start
                System.Diagnostics.Debug.WriteLine($"Jumping back to loop start at index {loopContext.StartIndex}");
                currentCommandIndex = loopContext.StartIndex;
            }
            else
            {
                // Loop completed - remove from stack
                System.Diagnostics.Debug.WriteLine($"Loop '{loopContext.LoopName}' completed");
                loopStack.Pop();
            }

            return true;
        }

        private async Task EnsureWindowFocused()
        {
            if (TargetWindow != IntPtr.Zero)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"Focusing target window: {TargetWindow}");
                    SetForegroundWindow(TargetWindow);
                    await Task.Delay(WindowFocusDelay); // Give time for focus to take effect
                    System.Diagnostics.Debug.WriteLine("Window focus completed");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Could not focus target window: {ex.Message}");
                }
            }
        }

        private void HighlightElement(UIElementInfo element)
        {
            // TODO: Implement element highlighting (e.g., draw red border around element)
            // This could use Graphics overlay or Windows API to draw a temporary border
            System.Diagnostics.Debug.WriteLine($"Highlighting element: {element.Name} at ({element.X}, {element.Y})");
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

        /// <summary>
        /// Spustí sequence s opakovaním alebo nekonečnou slučkou
        /// </summary>
        /// <param name="sequence">Command sequence na spustenie</param>
        /// <param name="targetWindow">Target window handle</param>
        /// <param name="repeatCount">Počet opakovaní (-1 = infinite loop)</param>
        /// <returns>Task</returns>
        public async Task PlaySequenceWithRepeatAsync(CommandSequence sequence, IntPtr targetWindow, int repeatCount)
        {
            System.Diagnostics.Debug.WriteLine("PlaySequenceWithRepeatAsync started");

            if (isPlaying)
                throw new InvalidOperationException("Already playing a sequence");

            if (sequence == null || sequence.Commands.Count == 0)
                throw new ArgumentException("Sequence is empty or null");

            bool isInfiniteLoop = repeatCount == -1;
            int actualRepeatCount = isInfiniteLoop ? int.MaxValue : Math.Max(1, repeatCount);

            System.Diagnostics.Debug.WriteLine($"Repeat playback: {(isInfiniteLoop ? "infinite loop" : $"{actualRepeatCount} iterations")}");
            System.Diagnostics.Debug.WriteLine($"Sequence has {sequence.Commands.Count} commands");

            currentSequence = sequence;
            int globalCommandIndex = 0;
            int executedIterations = 0;

            try
            {
                // Pokus o nájdenie cieľového okna
                await FindAndValidateTargetWindow(sequence, targetWindow);

                cancellationTokenSource = new CancellationTokenSource();
                isPlaying = true;
                isPaused = false;

                NotifyPlaybackStateChanged(PlaybackState.Started,
                    isInfiniteLoop ? "Starting infinite loop" : $"Starting {actualRepeatCount} iterations");

                // Hlavná slučka opakovaní
                for (int iteration = 0; iteration < actualRepeatCount && !cancellationTokenSource.Token.IsCancellationRequested; iteration++)
                {
                    executedIterations = iteration + 1;

                    System.Diagnostics.Debug.WriteLine($"=== ITERATION {executedIterations} {(isInfiniteLoop ? "(infinite)" : $"of {actualRepeatCount}")} ===");

                    try
                    {
                        // Reset command index pre každú iteráciu
                        currentCommandIndex = 0;
                        loopStack.Clear();

                        // Validácia target window pred každou iteráciou
                        if (TargetWindow != IntPtr.Zero && (!IsWindow(TargetWindow) || !IsWindowVisible(TargetWindow)))
                        {
                            System.Diagnostics.Debug.WriteLine("Target window lost, attempting to re-find...");
                            await FindAndValidateTargetWindow(sequence, IntPtr.Zero);

                            if (TargetWindow == IntPtr.Zero)
                            {
                                throw new InvalidOperationException($"Target window lost after iteration {executedIterations - 1}");
                            }
                        }

                        // Spustenie jednej iterácie
                        await ExecuteSequenceIterationAsync(cancellationTokenSource.Token, executedIterations, actualRepeatCount);

                        // Progress notification
                        NotifyPlaybackStateChanged(PlaybackState.Started,
                            isInfiniteLoop ? $"Completed iteration {executedIterations}" : $"Completed iteration {executedIterations}/{actualRepeatCount}");

                        // Pauza medzi iteráciami (okrem poslednej)
                        if (!isInfiniteLoop && iteration < actualRepeatCount - 1)
                        {
                            System.Diagnostics.Debug.WriteLine("Pausing between iterations...");
                            await Task.Delay(500, cancellationTokenSource.Token); // 500ms pauza medzi iteráciami
                        }
                        else if (isInfiniteLoop)
                        {
                            await Task.Delay(200, cancellationTokenSource.Token); // Kratšia pauza pre infinite loop
                        }

                        // Handle pause during inter-iteration delay
                        while (isPaused && !cancellationTokenSource.Token.IsCancellationRequested)
                        {
                            await Task.Delay(100, cancellationTokenSource.Token);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        System.Diagnostics.Debug.WriteLine($"Iteration {executedIterations} was cancelled");
                        break;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in iteration {executedIterations}: {ex.Message}");

                        // Pre infinite loop, loguj chybu ale pokračuj
                        if (isInfiniteLoop)
                        {
                            NotifyPlaybackError($"Error in iteration {executedIterations}: {ex.Message}", currentCommandIndex);

                            // Krátka pauza pred ďalšou iteráciou
                            await Task.Delay(1000, cancellationTokenSource.Token);
                            continue;
                        }
                        else
                        {
                            // Pre limited repeats, opýtaj sa užívateľa
                            NotifyPlaybackError($"Error in iteration {executedIterations}/{actualRepeatCount}: {ex.Message}", currentCommandIndex);

                            if (StopOnError)
                            {
                                throw new InvalidOperationException($"Stopped due to error in iteration {executedIterations}: {ex.Message}");
                            }

                            // Continue s ďalšou iteráciou ak StopOnError = false
                            await Task.Delay(500, cancellationTokenSource.Token);
                        }
                    }
                }

                string completionMessage = cancellationTokenSource.Token.IsCancellationRequested
                    ? $"Repeat playback stopped after {executedIterations} iterations"
                    : isInfiniteLoop
                        ? $"Infinite loop stopped after {executedIterations} iterations"
                        : $"Repeat playback completed: {executedIterations} iterations executed";

                NotifyPlaybackCompleted(true, completionMessage);
                System.Diagnostics.Debug.WriteLine(completionMessage);
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"Repeat playback was cancelled after {executedIterations} iterations");
                NotifyPlaybackCompleted(false, $"Playback was cancelled after {executedIterations} iterations");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in repeat playback: {ex.Message}");
                NotifyPlaybackError(ex.Message, currentCommandIndex);
                NotifyPlaybackCompleted(false, $"Repeat playback failed after {executedIterations} iterations: {ex.Message}");
            }
            finally
            {
                isPlaying = false;
                isPaused = false;
                cancellationTokenSource?.Dispose();
                cancellationTokenSource = null;
                NotifyPlaybackStateChanged(PlaybackState.Stopped);

                System.Diagnostics.Debug.WriteLine($"=== REPEAT PLAYBACK FINISHED ===");
                System.Diagnostics.Debug.WriteLine($"Total iterations executed: {executedIterations}");
            }
        }

        /// <summary>
        /// Vykonáva jednu iteráciu sequence (pomocná metóda pre repeat logic)
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="iterationNumber">Číslo aktuálnej iterácie</param>
        /// <param name="totalIterations">Celkový počet iterácií (int.MaxValue pre infinite)</param>
        /// <returns>Task</returns>
        private async Task ExecuteSequenceIterationAsync(CancellationToken cancellationToken, int iterationNumber, int totalIterations)
        {
            System.Diagnostics.Debug.WriteLine($"Starting sequence iteration {iterationNumber}");

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
                    System.Diagnostics.Debug.WriteLine($"[Iter {iterationNumber}] Executing command {currentCommandIndex + 1}/{currentSequence.Commands.Count}: {command}");

                    await ExecuteCommandAsync(command, cancellationToken);

                    // Default delay between commands
                    if (DefaultDelayBetweenCommands > 0)
                    {
                        await Task.Delay(DefaultDelayBetweenCommands, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Iter {iterationNumber}] Error executing command {currentCommandIndex + 1}: {ex.Message}");
                    NotifyCommandExecuted(command, false, $"Iteration {iterationNumber}: {ex.Message}");

                    if (StopOnError)
                        throw;
                }

                currentCommandIndex++;
            }

            System.Diagnostics.Debug.WriteLine($"Sequence iteration {iterationNumber} completed successfully");
        }

        /// <summary>
        /// Aktualizuje progress information pre repeat playback
        /// </summary>
        /// <param name="currentIteration">Aktuálna iterácia</param>
        /// <param name="totalIterations">Celkový počet iterácií</param>
        /// <param name="isInfinite">Či je to infinite loop</param>
        public void UpdateRepeatProgress(int currentIteration, int totalIterations, bool isInfinite)
        {
            string progressInfo = isInfinite
                ? $"Infinite loop - iteration {currentIteration}"
                : $"Progress: {currentIteration}/{totalIterations} iterations";

            NotifyPlaybackStateChanged(PlaybackState.Started, progressInfo);
        }

        internal void Pause()
        {
            throw new NotImplementedException();
        }

        internal void Resume()
        {
            throw new NotImplementedException();
        }

        internal void PlaySequence(CommandSequence sequence, int repeatCount)
        {
            throw new NotImplementedException();
        }

        internal void Stop()
        {
            throw new NotImplementedException();
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
