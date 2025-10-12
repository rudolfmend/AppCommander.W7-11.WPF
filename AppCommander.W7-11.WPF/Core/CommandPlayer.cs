using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AppCommander.W7_11.WPF.Core
{
    /// <summary>
    /// CommandPlayer pre .NET Framework 4.8 kompatibilitu (Windows 7-11)
    /// </summary>
    public class CommandPlayer : IDisposable
    {
        #region Win32 API Imports

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("dwmapi.dll")]
        private static extern int DwmIsCompositionEnabled(out bool enabled);

        #endregion

        #region Private Fields

        private readonly ActionSimulator actionSimulator;
        private readonly Stack<LoopContext> loopStack;
        private CommandSequence currentSequence;
        private CancellationTokenSource cancellationTokenSource;

        private bool isPlaying = false;
        private bool isPaused = false;
        private int currentCommandIndex = 0;
        private bool disposed = false;

        #endregion

        #region Public Properties

        public int DefaultDelayBetweenCommands { get; set; } = 100;
        public bool StopOnError { get; set; } = false;
        public bool HighlightElements { get; set; } = true;

        public bool IsPlaying
        {
            get { return isPlaying; }
        }

        public bool IsPaused
        {
            get { return isPaused; }
        }

        public CommandSequence CurrentSequence
        {
            get { return currentSequence; }
        }

        public int CurrentCommandIndex
        {
            get { return currentCommandIndex; }
        }

        public int TotalCommands
        {
            get { return currentSequence != null && currentSequence.Commands != null ? currentSequence.Commands.Count : 0; }
        }

        #endregion

        #region Events

        public event EventHandler<CommandExecutedEventArgs> CommandExecuted;
        public event EventHandler<PlaybackStateChangedEventArgs> PlaybackStateChanged;
        public event EventHandler<PlaybackErrorEventArgs> PlaybackError;
        public event EventHandler<PlaybackCompletedEventArgs> PlaybackCompleted;

        #endregion

        #region Constructor

        public CommandPlayer()
        {
            actionSimulator = new ActionSimulator();
            loopStack = new Stack<LoopContext>();

            // Optimalizácia pre Windows 7
            OptimizeForCurrentWindows();

            System.Diagnostics.Debug.WriteLine("CommandPlayer initialized for Windows 7-11");
        }

        #endregion

        #region Public Methods

        public void PlaySequence(CommandSequence sequence, int repeatCount = 1)
        {
            if (disposed)
                throw new ObjectDisposedException("CommandPlayer");

            try
            {
                if (isPlaying)
                {
                    System.Diagnostics.Debug.WriteLine("Already playing a sequence");
                    return;
                }

                if (sequence == null || sequence.Commands == null || sequence.Commands.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("No sequence or empty sequence provided");
                    return;
                }

                currentSequence = sequence;
                currentCommandIndex = 0;
                isPlaying = true;
                isPaused = false;
                loopStack.Clear();

                cancellationTokenSource = new CancellationTokenSource();

                var iterationsText = repeatCount > 1 ? string.Format(" (Repeat: {0}x)", repeatCount) : "";
                System.Diagnostics.Debug.WriteLine("Starting playback: " + sequence.Name + iterationsText);

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

                            System.Diagnostics.Debug.WriteLine(string.Format("Starting iteration {0}/{1}", iteration + 1, repeatCount));

                            currentCommandIndex = 0;
                            await ExecuteSequenceAsync(cancellationTokenSource.Token);

                            if (iteration < repeatCount - 1)
                            {
                                await Task.Delay(500, cancellationTokenSource.Token);
                            }
                        }

                        var completionMessage = string.Format("Sequence completed successfully ({0} iterations)", repeatCount);
                        CompletePlayback(true, completionMessage);
                    }
                    catch (OperationCanceledException)
                    {
                        CompletePlayback(false, "Playback was cancelled");
                    }
                    catch (Exception ex)
                    {
                        CompletePlayback(false, "Playback failed: " + ex.Message);
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error starting playback: " + ex.Message);
                NotifyPlaybackError("Error starting playback: " + ex.Message, 0);
            }
        }

        public void Pause()
        {
            if (disposed)
                return;

            try
            {
                if (!isPlaying || isPaused)
                {
                    System.Diagnostics.Debug.WriteLine("Cannot pause - not playing or already paused");
                    return;
                }

                isPaused = true;
                NotifyPlaybackStateChanged(PlaybackState.Paused);
                System.Diagnostics.Debug.WriteLine("Playback paused");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error pausing playback: " + ex.Message);
                NotifyPlaybackError("Error pausing playback: " + ex.Message, currentCommandIndex);
            }
        }

        public void Resume()
        {
            if (disposed)
                return;

            try
            {
                if (!isPlaying || !isPaused)
                {
                    System.Diagnostics.Debug.WriteLine("Cannot resume - not playing or not paused");
                    return;
                }

                isPaused = false;
                NotifyPlaybackStateChanged(PlaybackState.Resumed);
                System.Diagnostics.Debug.WriteLine("Playback resumed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error resuming playback: " + ex.Message);
                NotifyPlaybackError("Error resuming playback: " + ex.Message, currentCommandIndex);
            }
        }

        public void Stop()
        {
            if (disposed)
                return;

            try
            {
                if (!isPlaying)
                {
                    System.Diagnostics.Debug.WriteLine("Not currently playing");
                    return;
                }

                if (cancellationTokenSource != null)
                    cancellationTokenSource.Cancel();

                isPlaying = false;
                isPaused = false;
                currentCommandIndex = 0;
                loopStack.Clear();

                NotifyPlaybackStateChanged(PlaybackState.Stopped);
                System.Diagnostics.Debug.WriteLine("Playback stopped");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error stopping playback: " + ex.Message);
                NotifyPlaybackError("Error stopping playback: " + ex.Message, currentCommandIndex);
            }
        }

        public void TestPlayback(CommandSequence sequence)
        {
            if (disposed)
                return;

            try
            {
                if (sequence == null || sequence.Commands == null || sequence.Commands.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("No sequence provided for testing");
                    return;
                }

                System.Diagnostics.Debug.WriteLine("Testing playback for sequence: " + sequence.Name);

                // Validate commands
                var validationResult = ValidateSequence(sequence);
                if (!validationResult.IsValid)
                {
                    System.Diagnostics.Debug.WriteLine("Sequence validation failed:");
                    if (validationResult.Errors != null)
                    {
                        foreach (var error in validationResult.Errors)
                        {
                            System.Diagnostics.Debug.WriteLine("  - " + error);
                        }
                    }
                    return;
                }

                // Test with first command only
                if (sequence.Commands.Count > 0)
                {
                    var firstCommand = sequence.Commands[0];
                    var testCommands = new List<Command> { firstCommand };

                    var testSequence = new CommandSequence
                    {
                        Name = "Test_" + sequence.Name,
                        Commands = testCommands,
                        TargetProcessName = sequence.TargetProcessName,
                        TargetWindowTitle = sequence.TargetWindowTitle
                    };

                    PlaySequence(testSequence, 1);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error testing playback: " + ex.Message);
                NotifyPlaybackError("Error testing playback: " + ex.Message, 0);
            }
        }

        #endregion

        #region Private Execution Methods

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
                    var commandInfo = string.Format("Executing command {0}/{1}: {2} - {3}",
                        currentCommandIndex + 1,
                        currentSequence.Commands.Count,
                        command.Type,
                        command.ElementName);
                    System.Diagnostics.Debug.WriteLine(commandInfo);

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
                    var errorMsg = string.Format("Error executing command {0}: {1}", currentCommandIndex + 1, ex.Message);
                    System.Diagnostics.Debug.WriteLine(errorMsg);
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
                Debug.WriteLine("private async Task ExecuteCommandAsync(Command command, CancellationToken cancellationToken) - Starting execution of command: " + command.ToString());

                // Focus target window if specified
                if (!string.IsNullOrEmpty(command.TargetWindow))
                {
                    await FocusTargetWindow(command, cancellationToken); // PRIDANÝ cancellationToken
                }

                Debug.WriteLine("private async Task ExecuteCommandAsync(Command command, CancellationToken cancellationToken) - Command details: " + command.ToString());

                switch (command.Type)
                {
                    case CommandType.Click:
                    case CommandType.DoubleClick:
                    case CommandType.RightClick:
                        await ExecuteClickCommand(command, cancellationToken);
                        break;

                    case CommandType.TypeText:
                    case CommandType.SetText:
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
                        System.Diagnostics.Debug.WriteLine("Unknown command type: " + command.Type);
                        break;
                }

                Debug.WriteLine("private async Task ExecuteCommandAsync(Command command, CancellationToken cancellationToken) - Command executed successfully.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error in ExecuteCommandAsync: " + ex.Message);
                throw;
            }
        }

        private async Task ExecuteClickCommand(Command command, CancellationToken cancellationToken)
        {
            try
            {
                UIElementInfo targetElement = null;

                Debug.WriteLine("private async Task ExecuteClickCommand(Command command, CancellationToken cancellationToken) - Command details: " + command.ToString());
                // + timeout pre hľadanie elementu
                var findElementTask = Task.Run(() =>
                {
                    // Try to find element by table cell identifier first
                    if (command.IsTableCommand && !string.IsNullOrEmpty(command.TableCellIdentifier))
                    {
                        IntPtr targetHandle = FindTargetWindow(command);
                        if (targetHandle != IntPtr.Zero)
                        {
                            var element = UIElementDetector.FindTableCellByIdentifier(
                                targetHandle,
                                command.TableCellIdentifier);

                            if (element != null)
                            {
                                System.Diagnostics.Debug.WriteLine("Found table cell: " + command.TableCellIdentifier);
                                return element;
                            }
                        }
                    }

                    // Fallback to element name search
                    if (!string.IsNullOrEmpty(command.ElementName))
                    {
                        IntPtr targetHandle = FindTargetWindow(command);
                        if (targetHandle != IntPtr.Zero)
                        {
                            return UIElementDetector.FindElementByName(command.ElementName, targetHandle);
                        }
                    }

                    return null;
                }, cancellationToken);

                // Čakaj maximálne 5 sekúnd na nájdenie elementu
                var timeoutTask = Task.Delay(5000, cancellationToken);
                var completedTask = await Task.WhenAny(findElementTask, timeoutTask);

                if (completedTask == findElementTask)
                {
                    targetElement = await findElementTask;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Element search timed out after 5 seconds");
                }

                // Execute click
                if (targetElement == null)
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("Element not found, using coordinates: {0}, {1}", command.ElementX, command.ElementY));
                    ExecuteClickAtCoordinates(command.Type, command.ElementX, command.ElementY);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Found element: " + targetElement.Name);
                    ExecuteClickAtCoordinates(command.Type, targetElement.X, targetElement.Y);

                    if (HighlightElements)
                    {
                        HighlightElement(targetElement);
                    }
                }
                Debug.WriteLine("private async Task ExecuteClickCommand(Command command, CancellationToken cancellationToken) - Click executed.");

                await Task.Delay(50, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to execute click command: " + ex.Message);
            }
        }

        private void ExecuteClickAtCoordinates(CommandType clickType, int x, int y)
        {
            switch (clickType)
            {
                case CommandType.Click:
                    actionSimulator.ClickAt(x, y);
                    break;
                case CommandType.DoubleClick:
                    actionSimulator.DoubleClickAt(x, y);
                    break;
                case CommandType.RightClick:
                    actionSimulator.RightClickAt(x, y);
                    break;
            }
        }

        private async Task ExecuteTypeCommand(Command command, CancellationToken cancellationToken)
        {
            try
            {
                Debug.WriteLine("private async Task ExecuteTypeCommand(Command command, CancellationToken cancellationToken) - Command details: " + command.ToString());

                if (string.IsNullOrEmpty(command.Value))
                {
                    System.Diagnostics.Debug.WriteLine("No text to type");
                    return;
                }

                System.Diagnostics.Debug.WriteLine("Typing text: " + command.Value);

                // Ak je potrebné nájsť element, použite timeout
                if (!string.IsNullOrEmpty(command.ElementName))
                {
                    var findElementTask = Task.Run(() =>
                    {
                        IntPtr targetHandle = FindTargetWindow(command);
                        if (targetHandle != IntPtr.Zero)
                        {
                            return UIElementDetector.FindElementByName(command.ElementName, targetHandle);
                        }
                        return null;
                    }, cancellationToken);

                    var timeoutTask = Task.Delay(5000, cancellationToken);
                    var completedTask = await Task.WhenAny(findElementTask, timeoutTask);

                    UIElementInfo targetElement = null;
                    if (completedTask == findElementTask)
                    {
                        targetElement = await findElementTask;
                    }

                    if (targetElement != null)
                    {
                        // Kliknite na element aby bol fokusovaný
                        ExecuteClickAtCoordinates(CommandType.Click, targetElement.X, targetElement.Y);
                        await Task.Delay(100, cancellationToken);
                    }
                }

                // Simple text typing using SendKeys
                foreach (char c in command.Value)
                {
                    SendKeys.SendWait(c.ToString());
                    await Task.Delay(10, cancellationToken);
                }
                Debug.WriteLine("private async Task ExecuteTypeCommand(Command command, CancellationToken cancellationToken) - Text typed.");

                await Task.Delay(100, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to execute type command: " + ex.Message);
            }
        }

        private async Task ExecuteKeyPressCommand(Command command, CancellationToken cancellationToken)
        {
            try
            {
                string keyToSend = null;

                if (command.KeyCode > 0)
                {
                    var key = (Keys)command.KeyCode;
                    System.Diagnostics.Debug.WriteLine($"Pressing key: {key} (Code: {command.KeyCode})");
                    keyToSend = GetSendKeysString(key);
                }
                else if (!string.IsNullOrEmpty(command.Value))
                {
                    // Spracuj SHIFT kombinacie z Value stringu
                    keyToSend = ParseKeyValueWithShift(command.Value);

                    // Ak sa nepodarilo spracovať ako SHIFT kombinaciu, skús štandardne
                    if (string.IsNullOrEmpty(keyToSend))
                    {
                        Keys key;
                        if (Enum.TryParse<Keys>(command.Value, out key))
                        {
                            System.Diagnostics.Debug.WriteLine("Pressing key: " + key);
                            keyToSend = GetSendKeysString(key);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(keyToSend))
                {
                    System.Diagnostics.Debug.WriteLine($"Sending keys: {keyToSend}");
                    SendKeys.SendWait(keyToSend);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Could not process key command: {command.Value} (KeyCode: {command.KeyCode})");
                }

                await Task.Delay(50, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to execute key press command: " + ex.Message);
            }
        }

        /// <summary>
        /// Spracuje SHIFT kombinacie z Value stringu
        /// </summary>
        private string ParseKeyValueWithShift(string value)
        {
            if (string.IsNullOrEmpty(value) || !value.Contains("Shift"))
                return null;

            try
            {
                // Formát: "B, Shift" alebo "D7, Shift"
                var parts = value.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2 || parts[1] != "Shift")
                    return null;

                var baseKeyStr = parts[0];

                // Pre písmená: SHIFT+písmeno = veľké písmeno
                if (baseKeyStr.Length == 1 && char.IsLetter(baseKeyStr[0]))
                {
                    return "+" + baseKeyStr.ToUpper(); // SendKeys formát: +A pre SHIFT+A
                }

                // Pre číslice: SHIFT+číslica = symbol
                if (baseKeyStr.StartsWith("D") && baseKeyStr.Length == 2)
                {
                    var digit = baseKeyStr[1];
                    return "+" + digit; // SendKeys formát: +1 pre SHIFT+1 (!)
                }

                System.Diagnostics.Debug.WriteLine($"Unknown SHIFT combination: {value}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing SHIFT value '{value}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Rozšírená metóda pre mapovanie klávesov s SHIFT podporou
        /// </summary>
        private string GetSendKeysString(Keys key)
        {
            // Spracuj SHIFT flag v Keys hodnote
            if ((key & Keys.Shift) == Keys.Shift)
            {
                var baseKey = key & ~Keys.Shift; // Odstráň SHIFT flag
                return ProcessShiftKey(baseKey);
            }

            // Štandardné klávesy bez SHIFT
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
                    // Písmená (malé)
                    if (key >= Keys.A && key <= Keys.Z)
                        return key.ToString().ToLower();
                    // Číslice
                    if (key >= Keys.D0 && key <= Keys.D9)
                        return ((int)(key - Keys.D0)).ToString();
                    return string.Empty;
            }
        }

        /// <summary>
        /// Spracuje klávesy so SHIFT flagom
        /// </summary>
        private string ProcessShiftKey(Keys baseKey)
        {
            System.Diagnostics.Debug.WriteLine($"Processing SHIFT + {baseKey}");

            // Písmená so SHIFT = veľké písmená
            if (baseKey >= Keys.A && baseKey <= Keys.Z)
            {
                return "+" + baseKey.ToString(); // SendKeys: +A pre SHIFT+A
            }

            // Číslice so SHIFT = symboly
            if (baseKey >= Keys.D0 && baseKey <= Keys.D9)
            {
                var digit = (int)(baseKey - Keys.D0);
                return "+" + digit.ToString(); // SendKeys: +1 pre SHIFT+1
            }

            // Špeciálne symboly so SHIFT
            switch (baseKey)
            {
                case Keys.OemMinus: return "_";        // SHIFT+- = _
                case Keys.Oemplus: return "+";         // SHIFT+= = +
                case Keys.OemOpenBrackets: return "{"; // SHIFT+[ = {
                case Keys.Oem6: return "}";            // SHIFT+] = }
                case Keys.Oem5: return "|";            // SHIFT+\ = |
                case Keys.Oem1: return ":";            // SHIFT+; = :
                case Keys.Oem7: return "\"";           // SHIFT+' = "
                case Keys.Oemcomma: return "<";        // SHIFT+, = <
                case Keys.OemPeriod: return ">";       // SHIFT+. = >
                case Keys.OemQuestion: return "?";     // SHIFT+/ = ?
                case Keys.Oemtilde: return "~";        // SHIFT+` = ~
                default:
                    System.Diagnostics.Debug.WriteLine($"Unhandled SHIFT+{baseKey}");
                    return string.Empty;
            }
        }

        private async Task ExecuteWaitCommand(Command command, CancellationToken cancellationToken)
        {
            try
            {
                int waitTime;
                if (int.TryParse(command.Value, out waitTime))
                {
                    System.Diagnostics.Debug.WriteLine("Waiting " + waitTime + "ms");
                    await Task.Delay(waitTime, cancellationToken);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Invalid wait time, using default 1000ms");
                    await Task.Delay(1000, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to execute wait command: " + ex.Message);
            }
        }

        private void ExecuteLoopStart(Command command)
        {
            try
            {
                int iterations = 1;
                int parseResult;
                if (int.TryParse(command.Value, out parseResult))
                {
                    iterations = Math.Max(1, parseResult);
                }

                var loopContext = new LoopContext
                {
                    StartIndex = currentCommandIndex,
                    Iterations = iterations,
                    CurrentIteration = 0,
                    LoopName = command.ElementName ?? "UnnamedLoop"
                };

                loopStack.Push(loopContext);
                System.Diagnostics.Debug.WriteLine("Loop start: " + iterations + " iterations");
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to execute loop start: " + ex.Message);
            }
        }

        private async Task ExecuteLoopEnd(Command command, CancellationToken cancellationToken)
        {
            try
            {
                if (loopStack.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("Loop end without matching loop start");
                    return;
                }

                var loopContext = loopStack.Peek();
                loopContext.CurrentIteration++;

                System.Diagnostics.Debug.WriteLine(string.Format("Loop iteration {0}/{1}", loopContext.CurrentIteration, loopContext.Iterations));

                if (loopContext.CurrentIteration < loopContext.Iterations)
                {
                    // Continue loop - jump back to start
                    currentCommandIndex = loopContext.StartIndex;
                    await Task.Delay(100, cancellationToken);
                }
                else
                {
                    // Loop completed
                    loopStack.Pop();
                    System.Diagnostics.Debug.WriteLine("Loop completed: " + loopContext.LoopName);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to execute loop end: " + ex.Message);
            }
        }

        #endregion

        #region Helper Methods

        private async Task FocusTargetWindow(Command command, CancellationToken cancellationToken)
        {
            try
            {
                Debug.WriteLine("Focusing target window: " + command.TargetWindow);

                // Použite Task.Run s timeout pre FindTargetWindow
                var findWindowTask = Task.Run(() => FindTargetWindow(command), cancellationToken);
                var timeoutTask = Task.Delay(2000, cancellationToken);

                var completedTask = await Task.WhenAny(findWindowTask, timeoutTask);

                IntPtr targetHandle = IntPtr.Zero;
                if (completedTask == findWindowTask)
                {
                    targetHandle = await findWindowTask;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("FindTargetWindow timed out after 2 seconds");
                    return;
                }

                if (targetHandle != IntPtr.Zero)
                {
                    if (IsValidWindowForVersion(targetHandle) && IsWindowVisible(targetHandle))
                    {
                        SetForegroundWindow(targetHandle);
                        await Task.Delay(200, cancellationToken);
                        System.Diagnostics.Debug.WriteLine("Window focus completed");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Target window is not valid or visible");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("No target window handle available");
                }

                Debug.WriteLine("private async Task FocusTargetWindow(Command command, CancellationToken cancellationToken) - Focus attempt finished.");

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Could not focus target window: " + ex.Message);
            }
        }

        private IntPtr FindTargetWindow(Command command)
        {
            try
            {
                if (currentSequence != null && !string.IsNullOrEmpty(currentSequence.TargetProcessName))
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
                System.Diagnostics.Debug.WriteLine("Error finding target window: " + ex.Message);
                return IntPtr.Zero;
            }
        }

        private SequenceValidationResult ValidateSequence(CommandSequence sequence)
        {
            try
            {
                var firstCommand = (sequence.Commands != null && sequence.Commands.Count > 0) ? sequence.Commands[0] : new Command();
                IntPtr targetHandle = FindTargetWindow(firstCommand);
                return DebugTestHelper.ValidateSequenceWithWinUI3(sequence, targetHandle);
            }
            catch (Exception ex)
            {
                // Vytvor ValidationResult a pridaj chybu pomocou metódy AddError
                var result = new SequenceValidationResult();
                result.AddError("Validation error: " + ex.Message);
                return result;
            }
        }

        private bool IsValidWindowForVersion(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero)
                return false;

            if (!IsWindow(windowHandle))
                return false;

            // Dodatočné kontroly pre Windows 7
            if (!IsWindowsVersionCompatible())
            {
                System.Diagnostics.Debug.WriteLine("Warning: Running on unsupported Windows version");
                return false;
            }

            return true;
        }

        private bool IsWindowsVersionCompatible()
        {
            var version = Environment.OSVersion.Version;
            // Windows 7 = 6.1, Windows 8 = 6.2, Windows 10/11 = 10.0+
            return version.Major >= 6 && (version.Major > 6 || version.Minor >= 1);
        }

        private void OptimizeForCurrentWindows()
        {
            try
            {
                // Windows 7 má pomalší UI Automation
                var version = Environment.OSVersion.Version;
                if (version.Major == 6 && version.Minor == 1)
                {
                    DefaultDelayBetweenCommands = Math.Max(DefaultDelayBetweenCommands, 200); // Min 200ms pre Windows 7
                    System.Diagnostics.Debug.WriteLine("Optimized for Windows 7");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error optimizing for Windows version: " + ex.Message);
            }
        }

        private void HighlightPosition(int x, int y)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine(string.Format("Highlighting position: ({0}, {1})", x, y));
                // TODO: Implement visual highlighting if needed
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error highlighting position: " + ex.Message);
            }
        }

        private void HighlightElement(UIElementInfo element)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine(string.Format("Highlighting element: {0} at ({1}, {2})", element.Name, element.X, element.Y));
                // TODO: Implement visual highlighting if needed
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error highlighting element: " + ex.Message);
            }
        }

        #endregion

        #region Event Notification Methods

        private void CompletePlayback(bool success, string message)
        {
            try
            {
                isPlaying = false;
                isPaused = false;
                loopStack.Clear();

                SafeInvokeEvent(PlaybackCompleted, new PlaybackCompletedEventArgs
                {
                    Success = success,
                    Message = message,
                    CommandsExecuted = currentCommandIndex,
                    TotalCommands = TotalCommands
                });

                NotifyPlaybackStateChanged(PlaybackState.Stopped, message);
                System.Diagnostics.Debug.WriteLine("Playback completed: " + message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error completing playback: " + ex.Message);
            }
        }

        private void NotifyCommandExecuted(Command command, bool success, string error = "")
        {
            try
            {
                SafeInvokeEvent(CommandExecuted, new CommandExecutedEventArgs
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
                System.Diagnostics.Debug.WriteLine("Error notifying command executed: " + ex.Message);
            }
        }

        private void NotifyPlaybackStateChanged(PlaybackState state, string additionalInfo = "")
        {
            try
            {
                var sequenceName = (currentSequence != null) ? currentSequence.Name : string.Empty;

                SafeInvokeEvent(PlaybackStateChanged, new PlaybackStateChangedEventArgs
                {
                    State = state,
                    CurrentIndex = currentCommandIndex,
                    TotalCommands = TotalCommands,
                    SequenceName = sequenceName,
                    AdditionalInfo = additionalInfo
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error notifying playback state change: " + ex.Message);
            }
        }

        private void NotifyPlaybackError(string error, int commandIndex)
        {
            try
            {
                Command command = null;
                if (currentSequence != null && currentSequence.Commands != null &&
                    commandIndex >= 0 && commandIndex < currentSequence.Commands.Count)
                {
                    command = currentSequence.Commands[commandIndex];
                }

                SafeInvokeEvent(PlaybackError, new PlaybackErrorEventArgs
                {
                    ErrorMessage = error,
                    CommandIndex = commandIndex,
                    Command = command
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error notifying playback error: " + ex.Message);
            }
        }

        private void SafeInvokeEvent<T>(EventHandler<T> eventHandler, T eventArgs) where T : EventArgs
        {
            try
            {
                if (eventHandler != null)
                    eventHandler(this, eventArgs);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error invoking event: " + ex.Message);
            }
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    try
                    {
                        Stop();

                        if (cancellationTokenSource != null)
                        {
                            cancellationTokenSource.Dispose();
                            cancellationTokenSource = null;
                        }

                        if (loopStack != null)
                            loopStack.Clear();

                        System.Diagnostics.Debug.WriteLine("CommandPlayer disposed");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Error disposing CommandPlayer: " + ex.Message);
                    }
                }

                disposed = true;
            }
        }

        #endregion

        #region Nested Classes

        public class LoopContext
        {
            public int StartIndex { get; set; }
            public int Iterations { get; set; }
            public int CurrentIteration { get; set; }
            public string LoopName { get; set; } = string.Empty;
        }

        public enum PlaybackState
        {
            Started,
            Stopped,
            Paused,
            Resumed
        }

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

        #endregion
    }
}
