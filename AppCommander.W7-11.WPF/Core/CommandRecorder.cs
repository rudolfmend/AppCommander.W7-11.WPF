using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using AppCommander.W7_11.WPF.Core;

namespace AppCommander.W7_11.WPF.Core
{
    public class CommandRecorder
    {
        #region Properties

        protected readonly GlobalHook globalHook;
        protected readonly WindowTracker windowTracker;
        protected CommandSequence currentSequence;
        protected readonly Dictionary<string, ElementUsageStats> elementStats;
        protected readonly Dictionary<IntPtr, string> trackedWindows;
        protected bool isRecording = false;
        protected bool isPaused = false;
        protected IntPtr targetWindow = IntPtr.Zero;
        protected string targetProcessName = string.Empty;
        protected int commandCounter = 1;

        // Loop management
        private readonly Stack<LoopContext> loopStack;
        private bool infiniteLoopEnabled = false;

        // Configuration
        public bool AutoDetectNewWindows { get; set; } = true;
        public bool LogWindowChanges { get; set; } = true;

        #endregion

        #region Events

        public event EventHandler<CommandRecordedEventArgs> CommandRecorded;
        public event EventHandler<RecordingStateChangedEventArgs> RecordingStateChanged;

        #endregion

        #region Public Properties

        public bool IsRecording => isRecording && !isPaused;
        public bool IsPaused => isPaused;
        public CommandSequence CurrentSequence => currentSequence;
        public Dictionary<string, ElementUsageStats> ElementStats => new Dictionary<string, ElementUsageStats>(elementStats);

        #endregion

        #region Constructor

        public CommandRecorder()
        {
            globalHook = new GlobalHook();
            windowTracker = new WindowTracker();
            elementStats = new Dictionary<string, ElementUsageStats>();
            trackedWindows = new Dictionary<IntPtr, string>();
            loopStack = new Stack<LoopContext>();

            // Subscribe to hook events
            globalHook.KeyPressed += OnKeyPressed;
            globalHook.MouseClicked += OnMouseClicked;

            // Subscribe to window tracker events
            windowTracker.NewWindowDetected += OnNewWindowDetected;
            windowTracker.WindowActivated += OnWindowActivated;
            windowTracker.WindowClosed += OnWindowClosed;

            Debug.WriteLine("🚀 CommandRecorder initialized");
        }

        #endregion

        #region Recording Control

        public void StartRecording(string sequenceName, IntPtr targetWindowHandle = default(IntPtr))
        {
            try
            {
                if (isRecording)
                {
                    Debug.WriteLine("⚠️ Recording already in progress");
                    return;
                }

                targetWindow = targetWindowHandle;
                targetProcessName = GetProcessNameFromWindow(targetWindowHandle);

                currentSequence = new CommandSequence
                {
                    Name = sequenceName,
                    TargetProcessName = targetProcessName,
                    TargetWindowTitle = GetWindowTitle(targetWindowHandle),
                    Created = DateTime.Now,
                    LastModified = DateTime.Now
                };

                commandCounter = 1;
                loopStack.Clear();

                // Start window tracking if enabled
                if (AutoDetectNewWindows)
                {
                    windowTracker.StartTracking(targetProcessName);
                }

                // Start global hooks
                globalHook.StartHooking();
                isRecording = true;

                RecordingStateChanged?.Invoke(this, new RecordingStateChangedEventArgs(true, false, sequenceName));

                Debug.WriteLine($"✅ Recording started: {sequenceName} | Target: {targetProcessName}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error starting recording: {ex.Message}");
                throw;
            }
        }

        public void StopRecording()
        {
            try
            {
                if (!isRecording)
                    return;

                globalHook.StopHooking();
                windowTracker.StopTracking();

                isRecording = false;
                isPaused = false;

                RecordingStateChanged?.Invoke(this, new RecordingStateChangedEventArgs(false, false, currentSequence?.Name ?? ""));

                Debug.WriteLine($"⏹️ Recording stopped. Commands recorded: {currentSequence?.Commands.Count ?? 0}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error stopping recording: {ex.Message}");
                throw;
            }
        }

        public void PauseRecording()
        {
            if (isRecording && !isPaused)
            {
                isPaused = true;
                RecordingStateChanged?.Invoke(this, new RecordingStateChangedEventArgs(true, true, currentSequence?.Name ?? ""));
                Debug.WriteLine("⏸️ Recording paused");
            }
        }

        public void ResumeRecording()
        {
            if (isRecording && isPaused)
            {
                isPaused = false;
                RecordingStateChanged?.Invoke(this, new RecordingStateChangedEventArgs(true, false, currentSequence?.Name ?? ""));
                Debug.WriteLine("▶️ Recording resumed");
            }
        }

        #endregion

        #region Loop Commands - OPRAVENÉ IMPLEMENTÁCIE

        internal void AddLoopStart()
        {
            try
            {
                if (!isRecording)
                {
                    Debug.WriteLine("⚠️ Cannot add loop start - not recording");
                    return;
                }

                var loopCommand = new Command
                {
                    StepNumber = commandCounter++,
                    Type = CommandType.LoopStart,
                    ElementName = "Loop Start",
                    Value = "1", // Default repeat count
                    IsLoopStart = true,
                    Timestamp = DateTime.Now,
                    TargetWindow = currentSequence.TargetWindowTitle,
                    TargetProcess = targetProcessName
                };

                currentSequence.Commands.Add(loopCommand);

                // Add to loop stack
                loopStack.Push(new LoopContext
                {
                    StartIndex = currentSequence.Commands.Count - 1,
                    Iterations = 1,
                    CurrentIteration = 0,
                    LoopName = $"Loop_{loopStack.Count + 1}"
                });

                CommandRecorded?.Invoke(this, new CommandRecordedEventArgs(loopCommand));
                Debug.WriteLine($"🔄 Loop start added at index {loopCommand.StepNumber}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error adding loop start: {ex.Message}");
            }
        }

        internal void AddLoopEnd()
        {
            try
            {
                if (!isRecording)
                {
                    Debug.WriteLine("⚠️ Cannot add loop end - not recording");
                    return;
                }

                if (loopStack.Count == 0)
                {
                    Debug.WriteLine("⚠️ No matching loop start found");
                    return;
                }

                var loopContext = loopStack.Pop();

                var loopEndCommand = new Command
                {
                    StepNumber = commandCounter++,
                    Type = CommandType.LoopEnd,
                    ElementName = "Loop End",
                    Value = loopContext.LoopName,
                    Timestamp = DateTime.Now,
                    TargetWindow = currentSequence.TargetWindowTitle,
                    TargetProcess = targetProcessName
                };

                currentSequence.Commands.Add(loopEndCommand);

                // Mark commands inside loop
                var startIndex = loopContext.StartIndex;
                for (int i = startIndex + 1; i < currentSequence.Commands.Count - 1; i++)
                {
                    currentSequence.Commands[i].IsLoopCommand = true;
                }

                CommandRecorded?.Invoke(this, new CommandRecordedEventArgs(loopEndCommand));
                Debug.WriteLine($"🔄 Loop end added. Loop contains {currentSequence.Commands.Count - startIndex - 2} commands");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error adding loop end: {ex.Message}");
            }
        }

        internal void AddWaitCommand(int waitTime)
        {
            try
            {
                if (!isRecording)
                {
                    Debug.WriteLine("⚠️ Cannot add wait command - not recording");
                    return;
                }

                var waitCommand = new Command
                {
                    StepNumber = commandCounter++,
                    Type = CommandType.Wait,
                    ElementName = "Wait",
                    Value = waitTime.ToString(),
                    RepeatCount = 1,
                    Timestamp = DateTime.Now,
                    TargetWindow = currentSequence.TargetWindowTitle,
                    TargetProcess = targetProcessName
                };

                currentSequence.Commands.Add(waitCommand);
                CommandRecorded?.Invoke(this, new CommandRecordedEventArgs(waitCommand));
                Debug.WriteLine($"⏱️ Wait command added: {waitTime}ms");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error adding wait command: {ex.Message}");
            }
        }

        internal void SetInfiniteLoop(bool enabled)
        {
            infiniteLoopEnabled = enabled;
            Debug.WriteLine($"🔄 Infinite loop {(enabled ? "enabled" : "disabled")}");
        }

        #endregion

        #region Utility Commands - OPRAVENÉ IMPLEMENTÁCIE

        internal void RemoveLoopStart()
        {
            try
            {
                if (currentSequence?.Commands == null) return;

                var lastLoopStart = currentSequence.Commands.LastOrDefault(c => c.Type == CommandType.LoopStart);
                if (lastLoopStart != null)
                {
                    currentSequence.Commands.Remove(lastLoopStart);

                    // Remove from loop stack if it was the last one
                    if (loopStack.Count > 0)
                    {
                        loopStack.Pop();
                    }

                    Debug.WriteLine("🗑️ Last loop start removed");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error removing loop start: {ex.Message}");
            }
        }

        internal void RemoveLoopEnd()
        {
            try
            {
                if (currentSequence?.Commands == null) return;

                var lastLoopEnd = currentSequence.Commands.LastOrDefault(c => c.Type == CommandType.LoopEnd);
                if (lastLoopEnd != null)
                {
                    currentSequence.Commands.Remove(lastLoopEnd);
                    Debug.WriteLine("🗑️ Last loop end removed");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error removing loop end: {ex.Message}");
            }
        }

        internal void ClearLog()
        {
            try
            {
                currentSequence?.Commands.Clear();
                elementStats.Clear();
                commandCounter = 1;
                loopStack.Clear();
                Debug.WriteLine("🧹 Command log cleared");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error clearing log: {ex.Message}");
            }
        }

        internal void DebugCoordinates()
        {
            try
            {
                Debug.WriteLine("=== DEBUG COORDINATES ===");
                var position = System.Windows.Forms.Cursor.Position;
                Debug.WriteLine($"Current mouse position: {position.X}, {position.Y}");

                var element = UIElementDetector.GetElementAtPointEnhanced(position.X, position.Y);
                if (element != null)
                {
                    Debug.WriteLine($"Element at position: {element.Name}");
                    Debug.WriteLine($"AutomationId: {element.AutomationId}");
                    Debug.WriteLine($"ControlType: {element.ControlType}");
                    Debug.WriteLine($"ClassName: {element.ClassName}");
                }
                else
                {
                    Debug.WriteLine("No element found at current position");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error in debug coordinates: {ex.Message}");
            }
        }

        internal void StartElementInspector()
        {
            try
            {
                if (!isRecording)
                {
                    Debug.WriteLine("⚠️ Cannot start element inspector - not recording");
                    return;
                }

                Debug.WriteLine("🔍 Element inspector mode activated");
                Debug.WriteLine("Click on any UI element to inspect it...");

                // Inspector mode will be handled by the mouse click events
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error starting element inspector: {ex.Message}");
            }
        }

        #endregion

        #region Event Handlers

        private void OnKeyPressed(object sender, KeyPressedEventArgs e)
        {
            if (!IsRecording) return;

            try
            {
                var command = new Command
                {
                    StepNumber = commandCounter++,
                    Type = CommandType.KeyPress,
                    ElementName = "Keyboard",
                    Value = e.Key.ToString(),
                    KeyCode = (int)e.Key,
                    Timestamp = DateTime.Now,
                    TargetWindow = currentSequence.TargetWindowTitle,
                    TargetProcess = targetProcessName
                };

                currentSequence.Commands.Add(command);
                CommandRecorded?.Invoke(this, new CommandRecordedEventArgs(command));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error recording key press: {ex.Message}");
            }
        }

        private void OnMouseClicked(object sender, MouseClickedEventArgs e)
        {
            if (!IsRecording) return;

            try
            {
                // Get element at click position
                var element = UIElementDetector.GetElementAtPointEnhanced(e.X, e.Y);

                var command = new Command
                {
                    StepNumber = commandCounter++,
                    Type = e.Button == System.Windows.Forms.MouseButtons.Left ? CommandType.Click : CommandType.RightClick,
                    ElementName = element?.Name ?? "Unknown Element",
                    ElementX = e.X,
                    ElementY = e.Y,
                    OriginalX = e.X,
                    OriginalY = e.Y,
                    MouseButton = e.Button,
                    Timestamp = DateTime.Now,
                    TargetWindow = currentSequence.TargetWindowTitle,
                    TargetProcess = targetProcessName
                };

                // Fill element details
                if (element != null)
                {
                    command.ElementId = element.AutomationId;
                    command.ElementClass = element.ClassName;
                    command.ElementControlType = element.ControlType;
                    command.ElementText = element.ElementText;

                    // Check if it's a table cell
                    if (element.IsTableCell)
                    {
                        command.IsTableCommand = true;
                        command.TableCellIdentifier = element.TableCellIdentifier;
                        command.TableName = element.TableName;
                        command.TableRow = element.TableRow;
                        command.TableColumn = element.TableColumn;
                    }
                }

                currentSequence.Commands.Add(command);
                UpdateElementStats(element);
                CommandRecorded?.Invoke(this, new CommandRecordedEventArgs(command));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error recording mouse click: {ex.Message}");
            }
        }

        private void OnNewWindowDetected(object sender, NewWindowDetectedEventArgs e)
        {
            if (LogWindowChanges)
            {
                Debug.WriteLine($"🪟 New window detected: {e.WindowTitle} ({e.ProcessName})");
            }
        }

        private void OnWindowActivated(object sender, WindowActivatedEventArgs e)
        {
            if (LogWindowChanges)
            {
                Debug.WriteLine($"🎯 Window activated: {e.WindowTitle}");
            }
        }

        private void OnWindowClosed(object sender, WindowClosedEventArgs e)
        {
            if (LogWindowChanges)
            {
                Debug.WriteLine($"❌ Window closed: {e.WindowTitle}");
            }
        }

        #endregion

        #region Helper Methods

        private void UpdateElementStats(UIElementInfo element)
        {
            if (element == null) return;

            try
            {
                var key = GetElementIdentifier(element);
                if (elementStats.ContainsKey(key))
                {
                    elementStats[key].UsageCount++;
                    elementStats[key].LastUsed = DateTime.Now;
                }
                else
                {
                    elementStats[key] = new ElementUsageStats
                    {
                        ElementName = element.Name,
                        UsageCount = 1,
                        FirstUsed = DateTime.Now,
                        LastUsed = DateTime.Now
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error updating element stats: {ex.Message}");
            }
        }

        private string GetElementIdentifier(UIElementInfo element)
        {
            // Simple element identifier - priority: AutomationId > Name > Text > Position
            if (!string.IsNullOrEmpty(element.AutomationId))
                return $"AutomationId:{element.AutomationId}";

            if (!string.IsNullOrEmpty(element.Name))
                return $"Name:{element.Name}";

            if (!string.IsNullOrEmpty(element.ElementText))
                return $"Text:{element.ElementText}";

            return $"Position:{element.X},{element.Y}";
        }

        private string GetProcessNameFromWindow(IntPtr windowHandle)
        {
            try
            {
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
                var sb = new System.Text.StringBuilder(256);
                GetWindowText(windowHandle, sb, sb.Capacity);
                return sb.ToString();
            }
            catch
            {
                return "Unknown Window";
            }
        }

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

        #endregion

        #region Dispose

        public void Dispose()
        {
            try
            {
                if (isRecording)
                {
                    StopRecording();
                }

                globalHook?.StopHooking();
                windowTracker?.Dispose();

                Debug.WriteLine("🧹 CommandRecorder disposed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error disposing CommandRecorder: {ex.Message}");
            }
        }

        #endregion
    }

    #region Helper Classes

    internal class LoopContext
    {
        public int StartIndex { get; set; }
        public int Iterations { get; set; }
        public int CurrentIteration { get; set; }
        public string LoopName { get; set; }
    }

    #endregion
}
