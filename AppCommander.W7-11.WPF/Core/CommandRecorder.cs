using AppCommander.W7_11.WPF.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace AppCommander.W7_11.WPF.Core
{
    public class CommandRecorder
    {
        private readonly GlobalHook globalHook;
        private CommandSequence currentSequence;
        private readonly Dictionary<string, ElementUsageStats> elementStats;
        private bool isRecording = false;
        private IntPtr targetWindow = IntPtr.Zero;
        private string targetProcessName = string.Empty;
        private int commandCounter = 1;

        // Events
        public event EventHandler<CommandRecordedEventArgs> CommandRecorded;
        public event EventHandler<RecordingStateChangedEventArgs> RecordingStateChanged;
        public event EventHandler<ElementUsageEventArgs> ElementUsageUpdated;

        public bool IsRecording => isRecording;
        public CommandSequence CurrentSequence => currentSequence;
        public Dictionary<string, ElementUsageStats> ElementStats => new Dictionary<string, ElementUsageStats>(elementStats);

        public CommandRecorder()
        {
            globalHook = new GlobalHook();
            elementStats = new Dictionary<string, ElementUsageStats>();

            // Subscribe to hook events
            globalHook.KeyPressed += OnKeyPressed;
            globalHook.MouseClicked += OnMouseClicked;
        }

        public void StartRecording(string sequenceName, IntPtr targetWindowHandle = default(IntPtr))
        {
            if (isRecording)
                return;

            // Initialize new sequence
            currentSequence = new CommandSequence(sequenceName);
            targetWindow = targetWindowHandle;
            commandCounter = 1;
            elementStats.Clear();

            // Get target process name and window info if window handle provided
            if (targetWindow != IntPtr.Zero)
            {
                targetProcessName = GetProcessNameFromWindow(targetWindow);
                currentSequence.TargetApplication = targetProcessName;
                currentSequence.TargetProcessName = targetProcessName;
                currentSequence.TargetWindowTitle = GetWindowTitleFromHandle(targetWindow);
                currentSequence.TargetWindowClass = GetWindowClassFromHandle(targetWindow);
                currentSequence.AutoFindTarget = true;
                currentSequence.MaxWaitTimeSeconds = 30;
            }

            // Start global hooks
            globalHook.StartHooking();
            isRecording = true;

            RecordingStateChanged?.Invoke(this, new RecordingStateChangedEventArgs
            {
                IsRecording = true,
                SequenceName = sequenceName,
                TargetWindow = targetWindow
            });
        }

        public void StopRecording()
        {
            if (!isRecording)
                return;

            // Stop global hooks
            globalHook.StopHooking();
            isRecording = false;

            RecordingStateChanged?.Invoke(this, new RecordingStateChangedEventArgs
            {
                IsRecording = false,
                SequenceName = currentSequence?.Name ?? string.Empty,
                CommandCount = currentSequence?.Commands.Count ?? 0
            });
        }

        public void PauseRecording()
        {
            if (!isRecording)
                return;

            globalHook.StopHooking();
            isRecording = false;

            RecordingStateChanged?.Invoke(this, new RecordingStateChangedEventArgs
            {
                IsRecording = false,
                IsPaused = true,
                SequenceName = currentSequence?.Name ?? string.Empty
            });
        }

        public void ResumeRecording()
        {
            if (isRecording || currentSequence == null)
                return;

            globalHook.StartHooking();
            isRecording = true;

            RecordingStateChanged?.Invoke(this, new RecordingStateChangedEventArgs
            {
                IsRecording = true,
                IsPaused = false,
                SequenceName = currentSequence.Name
            });
        }

        private void OnKeyPressed(object sender, KeyPressedEventArgs e)
        {
            if (!isRecording)
                return;

            // Filter to target window if specified
            if (targetWindow != IntPtr.Zero && e.WindowHandle != targetWindow)
                return;

            // Skip certain system keys
            if (ShouldSkipKey(e.Key))
                return;

            // Create command for key press
            var command = new Command(commandCounter++, $"Key_{e.Key}", CommandType.KeyPress)
            {
                Key = e.Key,
                Value = e.Key.ToString(),
                TargetWindow = e.WindowTitle,
                TargetProcess = e.ProcessName,
                ElementX = -1, // Key presses don't have specific coordinates
                ElementY = -1
            };

            AddCommand(command, null);
        }

        private void OnMouseClicked(object sender, MouseClickedEventArgs e)
        {
            if (!isRecording)
                return;

            // Filter to target window if specified
            if (targetWindow != IntPtr.Zero && e.WindowHandle != targetWindow)
                return;

            // Determine command type based on mouse button
            CommandType commandType = e.Button == MouseButtons.Left ? CommandType.Click : CommandType.RightClick;

            // Create element name
            string elementName = "Unknown";
            if (e.UIElement != null)
            {
                elementName = !string.IsNullOrEmpty(e.UIElement.Name)
                    ? e.UIElement.Name
                    : e.UIElement.GetUniqueIdentifier();
            }

            // Create command for mouse click
            var command = new Command(commandCounter++, elementName, commandType)
            {
                MouseButton = e.Button,
                ElementX = e.X,
                ElementY = e.Y,
                TargetWindow = e.WindowTitle,
                TargetProcess = e.ProcessName
            };

            // Fill element details if available
            if (e.UIElement != null)
            {
                command.ElementId = e.UIElement.AutomationId;
                command.ElementClass = e.UIElement.ClassName;
                command.ElementControlType = e.UIElement.ControlType;
            }

            AddCommand(command, e.UIElement);
        }

        private void AddCommand(Command command, UIElementInfo uiElement)
        {
            // Add to sequence
            currentSequence.AddCommand(command);

            // Update element statistics
            UpdateElementStats(command, uiElement);

            // Notify listeners
            CommandRecorded?.Invoke(this, new CommandRecordedEventArgs
            {
                Command = command,
                UIElement = uiElement,
                TotalCommands = currentSequence.Commands.Count
            });
        }

        private void UpdateElementStats(Command command, UIElementInfo uiElement)
        {
            string elementKey = command.ElementName;

            if (!elementStats.ContainsKey(elementKey))
            {
                elementStats[elementKey] = new ElementUsageStats
                {
                    ElementName = command.ElementName,
                    ElementType = command.ElementClass,
                    ControlType = command.ElementControlType
                };
            }

            var stats = elementStats[elementKey];
            stats.IncrementUsage(command.Type);

            ElementUsageUpdated?.Invoke(this, new ElementUsageEventArgs
            {
                ElementName = elementKey,
                Stats = stats
            });
        }

        private bool ShouldSkipKey(Keys key)
        {
            // Skip modifier keys, function keys, etc.
            var skipKeys = new[]
            {
                Keys.LWin, Keys.RWin, Keys.Apps,
                Keys.LControlKey, Keys.RControlKey, Keys.ControlKey,
                Keys.LShiftKey, Keys.RShiftKey, Keys.ShiftKey,
                Keys.LMenu, Keys.RMenu, Keys.Alt, // LMenu/RMenu sú správne názvy pre Alt klávesy
                Keys.CapsLock, Keys.NumLock, Keys.Scroll,
                Keys.PrintScreen, Keys.Pause,
                Keys.Insert, Keys.Delete, Keys.Home, Keys.End,
                Keys.PageUp, Keys.PageDown,
                Keys.F1, Keys.F2, Keys.F3, Keys.F4, Keys.F5, Keys.F6,
                Keys.F7, Keys.F8, Keys.F9, Keys.F10, Keys.F11, Keys.F12
            };

            return skipKeys.Contains(key);
        }

        private string GetProcessNameFromWindow(IntPtr hWnd)
        {
            try
            {
                GetWindowThreadProcessId(hWnd, out uint processId);
                using (var process = System.Diagnostics.Process.GetProcessById((int)processId))
                {
                    return process.ProcessName;
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        private string GetWindowTitleFromHandle(IntPtr hWnd)
        {
            try
            {
                const int nChars = 256;
                var buffer = new System.Text.StringBuilder(nChars);

                if (GetWindowText(hWnd, buffer, nChars) > 0)
                    return buffer.ToString();

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private string GetWindowClassFromHandle(IntPtr hWnd)
        {
            try
            {
                var buffer = new System.Text.StringBuilder(256);
                GetClassName(hWnd, buffer, buffer.Capacity);
                return buffer.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        public void SaveCurrentSequence(string filePath)
        {
            if (currentSequence == null)
                throw new InvalidOperationException("No sequence to save");

            currentSequence.SaveToFile(filePath);
        }

        public CommandSequence LoadSequence(string filePath)
        {
            return CommandSequence.LoadFromFile(filePath);
        }

        public void AddManualCommand(CommandType type, string elementName, string value = "", int repeatCount = 1)
        {
            if (currentSequence == null)
                return;

            var command = new Command(commandCounter++, elementName, type)
            {
                Value = value,
                RepeatCount = repeatCount,
                TargetWindow = "Manual",
                TargetProcess = "Manual"
            };

            AddCommand(command, null);
        }

        public void StartLoop(string loopName, int iterations = 1)
        {
            if (currentSequence == null)
                return;

            var command = new Command(commandCounter++, loopName, CommandType.Loop)
            {
                RepeatCount = iterations,
                IsLoopStart = true,
                Value = iterations.ToString()
            };

            AddCommand(command, null);
        }

        public void EndLoop()
        {
            if (currentSequence == null)
                return;

            var command = new Command(commandCounter++, "LoopEnd", CommandType.LoopEnd);
            AddCommand(command, null);
        }

        public void AddWaitCommand(int milliseconds)
        {
            if (currentSequence == null)
                return;

            var command = new Command(commandCounter++, $"Wait_{milliseconds}ms", CommandType.Wait)
            {
                Value = milliseconds.ToString(),
                RepeatCount = 1
            };

            AddCommand(command, null);
        }

        // Windows API
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        ~CommandRecorder()
        {
            globalHook?.StopHooking();
        }
    }

    // Event argument classes
    public class CommandRecordedEventArgs : EventArgs
    {
        public Command Command { get; set; }
        public UIElementInfo UIElement { get; set; }
        public int TotalCommands { get; set; }
    }

    public class RecordingStateChangedEventArgs : EventArgs
    {
        public bool IsRecording { get; set; }
        public bool IsPaused { get; set; }
        public string SequenceName { get; set; } = string.Empty;
        public IntPtr TargetWindow { get; set; }
        public int CommandCount { get; set; }
    }

    public class ElementUsageEventArgs : EventArgs
    {
        public string ElementName { get; set; } = string.Empty;
        public ElementUsageStats Stats { get; set; }
    }
}
