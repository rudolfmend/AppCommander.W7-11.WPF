using AppCommander.W7_11.WPF.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Forms;

namespace AppCommander.W7_11.WPF.Core
{
    public class CommandRecorder
    {
        private readonly GlobalHook globalHook;
        private CommandSequence currentSequence;
        private readonly Dictionary<string, ElementUsageStats> elementStats;
        private bool isRecording = false;
        private bool isPaused = false;
        private IntPtr targetWindow = IntPtr.Zero;
        private string targetProcessName = string.Empty;
        private int commandCounter = 1;

        // Events
        public event EventHandler<CommandRecordedEventArgs> CommandRecorded;
        public event EventHandler<RecordingStateChangedEventArgs> RecordingStateChanged;
        public event EventHandler<ElementUsageEventArgs> ElementUsageUpdated;

        public bool IsRecording => isRecording && !isPaused;
        public bool IsPaused => isPaused;
        public CommandSequence CurrentSequence => currentSequence;
        public Dictionary<string, ElementUsageStats> ElementStats => new Dictionary<string, ElementUsageStats>(elementStats);

        // WinUI3 debugging properties
        public bool EnableWinUI3Analysis { get; set; } = true;
        public bool EnableDetailedLogging { get; set; } = true;

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
            isPaused = false;

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

                System.Diagnostics.Debug.WriteLine($"Recording target: {targetProcessName} - {currentSequence.TargetWindowTitle}");
            }

            // Start global hooks
            globalHook.StartHooking();
            isRecording = true;

            RecordingStateChanged?.Invoke(this, new RecordingStateChangedEventArgs
            {
                IsRecording = true,
                IsPaused = false,
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
            isPaused = false;

            RecordingStateChanged?.Invoke(this, new RecordingStateChangedEventArgs
            {
                IsRecording = false,
                IsPaused = false,
                SequenceName = currentSequence?.Name ?? string.Empty,
                CommandCount = currentSequence?.Commands.Count ?? 0
            });
        }

        public void PauseRecording()
        {
            if (!isRecording || isPaused)
                return;

            isPaused = true;

            RecordingStateChanged?.Invoke(this, new RecordingStateChangedEventArgs
            {
                IsRecording = true,
                IsPaused = true,
                SequenceName = currentSequence?.Name ?? string.Empty
            });
        }

        public void ResumeRecording()
        {
            if (!isRecording || !isPaused)
                return;

            isPaused = false;

            RecordingStateChanged?.Invoke(this, new RecordingStateChangedEventArgs
            {
                IsRecording = true,
                IsPaused = false,
                SequenceName = currentSequence.Name
            });
        }

        private void OnKeyPressed(object sender, KeyPressedEventArgs e)
        {
            if (!isRecording || isPaused)
                return;

            // Filter to target window if specified
            if (targetWindow != IntPtr.Zero && e.WindowHandle != targetWindow)
                return;

            // Skip certain system keys
            if (ShouldSkipKey(e.Key))
                return;

            System.Diagnostics.Debug.WriteLine($"Recording key: {e.Key} in {e.ProcessName}");

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
            if (!isRecording || isPaused)
                return;

            if (targetWindow != IntPtr.Zero && e.WindowHandle != targetWindow)
                return;

            System.Diagnostics.Debug.WriteLine($"Recording mouse click at ({e.X}, {e.Y}) in {e.ProcessName}");

            // **POUŽITIE ROZŠÍRENEJ DETEKCIE S PODPOROU TABULIEK**
            var uiElement = UIElementDetector.GetElementAtPointEnhanced(e.X, e.Y);

            if (uiElement?.IsTableCell == true)
            {
                System.Diagnostics.Debug.WriteLine("=== TABLE CELL DETECTED DURING RECORDING ===");
                System.Diagnostics.Debug.WriteLine($"Table: {uiElement.TableName}");
                System.Diagnostics.Debug.WriteLine($"Cell: {uiElement.GetTableCellDisplayName()}");
                System.Diagnostics.Debug.WriteLine($"Position: Row {uiElement.TableRow}, Column {uiElement.TableColumn}");
                System.Diagnostics.Debug.WriteLine($"Identifier: {uiElement.TableCellIdentifier}");
                System.Diagnostics.Debug.WriteLine($"Content: '{uiElement.TableCellContent}'");
            }

            // **WinUI3 špecifická analýza**
            if (EnableWinUI3Analysis && uiElement?.ClassName == "Microsoft.UI.Content.DesktopChildSiteBridge")
            {
                System.Diagnostics.Debug.WriteLine("=== WinUI3 ELEMENT DETECTED ===");
                AnalyzeWinUI3Point(targetWindow, e.X, e.Y);
            }

            CommandType commandType = e.Button == System.Windows.Forms.MouseButtons.Left ? CommandType.Click : CommandType.RightClick;

            // **VYLEPŠENÁ TVORBA NÁZVU ELEMENTU S PODPOROU TABULIEK**
            string elementName = CreateMeaningfulElementNameEnhanced(uiElement, e.X, e.Y);

            // **Vytvor command s originálnymi súradnicami**
            var command = new Command(commandCounter++, elementName, commandType, e.X, e.Y)
            {
                MouseButton = e.Button,
                ElementX = e.X,  // Aktuálne súradnice
                ElementY = e.Y,  // Aktuálne súradnice
                TargetWindow = e.WindowTitle,
                TargetProcess = e.ProcessName
            };

            // **Aktualizuj z UIElementInfo s podporou tabuliek**
            if (uiElement != null)
            {
                command.UpdateFromElementInfoEnhanced(uiElement);

                if (EnableDetailedLogging)
                {
                    string logMessage = $"Element details: Name='{command.ElementName}', " +
                        $"Id='{command.ElementId}', Class='{command.ElementClass}', " +
                        $"Type='{command.ElementControlType}', Text='{command.ElementText}', " +
                        $"WinUI3={command.IsWinUI3Element}, ClickPos=({command.ElementX}, {command.ElementY})";

                    // Pridaj tabuľkové informácie do logu
                    if (uiElement.IsTableCell)
                    {
                        logMessage += $", TABLE: {uiElement.TableName}, Row: {uiElement.TableRow}, " +
                            $"Col: {uiElement.TableColumn}, CellId: '{uiElement.TableCellIdentifier}'";
                    }

                    System.Diagnostics.Debug.WriteLine(logMessage);
                }
            }

            AddCommand(command, uiElement);

            System.Diagnostics.Debug.WriteLine($"Command recorded: Step {command.StepNumber}: {command.Type} on {command.ElementName} (x{command.RepeatCount})");
        }

        /// <summary>
        /// Rozšírená metóda pre tvorbu názvu elementu s podporou tabuliek
        /// </summary>
        private string CreateMeaningfulElementNameEnhanced(UIElementInfo uiElement, int x, int y)
        {
            if (uiElement == null)
                return $"Click_at_{x}_{y}";

            // **ŠPECIALIZOVANÉ SPRACOVANIE PRE TABUĽKOVÉ BUNKY**
            if (uiElement.IsTableCell)
            {
                return uiElement.GetTableCellDisplayName();
            }

            // Fallback na štandardnú metódu
            return CreateMeaningfulElementName(uiElement, x, y);
        }

        /// <summary>
        /// **Vytvára zmysluplný názov elementu**
        /// </summary>
        private string CreateMeaningfulElementName(UIElementInfo uiElement, int x, int y)
        {
            if (uiElement == null)
                return $"Click_at_{x}_{y}";

            // Použij UIElementDetector logiku pre tvorbu názvu
            string elementName = "Unknown";

            // 1. Skutočný názov elementu
            if (!string.IsNullOrWhiteSpace(uiElement.Name) && !IsGenericElementName(uiElement.Name))
            {
                elementName = CleanElementName(uiElement.Name);
            }
            // 2. AutomationId
            else if (!string.IsNullOrWhiteSpace(uiElement.AutomationId) && !IsGenericId(uiElement.AutomationId))
            {
                elementName = $"AutoId_{CleanElementName(uiElement.AutomationId)}";
            }
            // 3. Text obsah
            else if (!string.IsNullOrWhiteSpace(uiElement.ElementText) && uiElement.ElementText.Length <= 20)
            {
                elementName = $"{uiElement.ControlType}_{CleanElementName(uiElement.ElementText)}";
            }
            // 4. Help text
            else if (!string.IsNullOrWhiteSpace(uiElement.HelpText) && uiElement.HelpText.Length <= 20)
            {
                elementName = $"{uiElement.ControlType}_{CleanElementName(uiElement.HelpText)}";
            }
            // 5. Fallback na typ a pozíciu
            else
            {
                string controlType = uiElement.ControlType ?? "Unknown";
                elementName = $"{controlType}_at_{x}_{y}";
            }

            return elementName;
        }

        private bool IsGenericElementName(string name)
        {
            var genericNames = new[]
            {
            "Microsoft.UI.Content.DesktopChildSiteBridge",
            "DesktopChildSiteBridge", "ContentPresenter", "Border",
            "Grid", "StackPanel", "Canvas", "UserControl"
        };

            return genericNames.Any(generic => name.IndexOf(generic, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private bool IsGenericId(string id)
        {
            return string.IsNullOrEmpty(id) || id.Length > 20 || id.All(char.IsDigit) || id.Contains("-") || id.Contains("{");
        }

        private string CleanElementName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "";

            return new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == ' ')
                                 .ToArray())
                                 .Replace(" ", "_")
                                 .Trim('_');
        }

        /// <summary>
        /// **Analyzuje nahraté WinUI3 elementy**
        /// </summary>
        public void AnalyzeRecordedWinUI3Elements()
        {
            if (currentSequence == null) return;

            var winui3Commands = currentSequence.Commands.Where(c => c.IsWinUI3Element).ToList();

            System.Diagnostics.Debug.WriteLine($"\n=== WINUI3 ANALYSIS ===");
            System.Diagnostics.Debug.WriteLine($"Total commands: {currentSequence.Commands.Count}");
            System.Diagnostics.Debug.WriteLine($"WinUI3 commands: {winui3Commands.Count}");

            if (winui3Commands.Any())
            {
                System.Diagnostics.Debug.WriteLine("\nWinUI3 Command Details:");
                foreach (var cmd in winui3Commands)
                {
                    System.Diagnostics.Debug.WriteLine($"Step {cmd.StepNumber}: {cmd.Type} on '{cmd.ElementName}'");
                    System.Diagnostics.Debug.WriteLine($"  Position: ({cmd.OriginalX}, {cmd.OriginalY}) -> ({cmd.ElementX}, {cmd.ElementY})");
                    System.Diagnostics.Debug.WriteLine($"  Identifier: {cmd.GetBestElementIdentifier()}");
                    if (!string.IsNullOrEmpty(cmd.ElementText))
                        System.Diagnostics.Debug.WriteLine($"  Text: '{cmd.ElementText}'");
                }

                // Identifikuj potenciálne problémy
                var problematicCommands = winui3Commands.Where(c =>
                    c.ElementName.Contains("Unknown") ||
                    c.ElementName.Contains("at_") ||
                    (string.IsNullOrEmpty(c.ElementId) && string.IsNullOrEmpty(c.ElementText))
                ).ToList();

                if (problematicCommands.Any())
                {
                    System.Diagnostics.Debug.WriteLine($"\n⚠️  Potentially problematic commands: {problematicCommands.Count}");
                    foreach (var cmd in problematicCommands)
                    {
                        System.Diagnostics.Debug.WriteLine($"  Step {cmd.StepNumber}: {cmd.ElementName} - may be unreliable");
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine("=== ANALYSIS COMPLETE ===\n");
        }

		/// <summary>
		/// Analyzuje WinUI3 point (náhrada za WinUI3DebugHelper)
		/// </summary>
		private void AnalyzeWinUI3Point(IntPtr targetWindow, int x, int y)
		{
			try
			{
				System.Diagnostics.Debug.WriteLine($"WinUI3 Analysis: Point ({x}, {y}) in window {targetWindow}");

				// Jednoduchá analýza - môžete rozšíriť neskôr
				var element = UIElementDetector.GetElementAtPoint(x, y);
				if (element != null && element.ClassName == "Microsoft.UI.Content.DesktopChildSiteBridge")
				{
					System.Diagnostics.Debug.WriteLine($"  WinUI3 Bridge Element: {element.Name}");
					System.Diagnostics.Debug.WriteLine($"  AutomationId: {element.AutomationId}");
					System.Diagnostics.Debug.WriteLine($"  Text: {element.ElementText}");
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"WinUI3 analysis error: {ex.Message}");
			}
		}

		/// <summary>
		/// Pridá command do sequence a aktualizuje štatistiky
		/// </summary>
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

			System.Diagnostics.Debug.WriteLine($"Command recorded: {command}");
		}

		/// <summary>
		/// Aktualizuje štatistiky použitia elementov
		/// </summary>
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

		/// <summary>
		/// Kontroluje či má byť klávesa preskočená
		/// </summary>
		private bool ShouldSkipKey(Keys key)
		{
			// Skip modifier keys, function keys, etc.
			var skipKeys = new[]
			{
		Keys.LWin, Keys.RWin, Keys.Apps,
		Keys.LControlKey, Keys.RControlKey, Keys.ControlKey,
		Keys.LShiftKey, Keys.RShiftKey, Keys.ShiftKey,
		Keys.LMenu, Keys.RMenu, Keys.Alt,
		Keys.CapsLock, Keys.NumLock, Keys.Scroll,
		Keys.PrintScreen, Keys.Pause,
		Keys.Insert, Keys.Delete, Keys.Home, Keys.End,
		Keys.PageUp, Keys.PageDown,
		Keys.F1, Keys.F2, Keys.F3, Keys.F4, Keys.F5, Keys.F6,
		Keys.F7, Keys.F8, Keys.F9, Keys.F10, Keys.F11, Keys.F12
	};

			return skipKeys.Contains(key);
		}

		/// <summary>
		/// Získa názov procesu z window handle
		/// </summary>
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

		/// <summary>
		/// Získa title okna z window handle
		/// </summary>
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

		/// <summary>
		/// Získa class name okna z window handle
		/// </summary>
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

		/// <summary>
		/// Uloží aktuálnu sequence do súboru
		/// </summary>
		public void SaveCurrentSequence(string filePath)
		{
			if (currentSequence == null)
				throw new InvalidOperationException("No sequence to save");

			currentSequence.SaveToFile(filePath);
		}

		/// <summary>
		/// Načíta sequence zo súboru
		/// </summary>
		public CommandSequence LoadSequence(string filePath)
		{
			return CommandSequence.LoadFromFile(filePath);
		}

		/// <summary>
		/// Pridá manuálny command
		/// </summary>
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

		/// <summary>
		/// Začne loop
		/// </summary>
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

		/// <summary>
		/// Ukončí loop
		/// </summary>
		public void EndLoop()
		{
			if (currentSequence == null)
				return;

			var command = new Command(commandCounter++, "LoopEnd", CommandType.LoopEnd);
			AddCommand(command, null);
		}

		/// <summary>
		/// Pridá wait command
		/// </summary>
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

		/// <summary>
		/// Destruktor - zastaví hooking
		/// </summary>
		~CommandRecorder()
		{
			globalHook?.StopHooking();
		}

		// Windows API import pre GetClassName
		[System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
		private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

		[System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
		private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

		[System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
		private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);
	}

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
