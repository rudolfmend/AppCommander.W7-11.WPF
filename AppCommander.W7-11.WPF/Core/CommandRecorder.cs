using AppCommander.W7_11.WPF.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Forms;

namespace AppCommander.W7_11.WPF.Core
{
    public class CommandRecorder
    {
        private readonly GlobalHook globalHook;
        private readonly WindowTracker windowTracker; // NOVÉ
        private CommandSequence currentSequence;
        private readonly Dictionary<string, ElementUsageStats> elementStats;
        private readonly Dictionary<IntPtr, string> trackedWindows; // Sledované okná a ich názvy
        private bool isRecording = false;
        private bool isPaused = false;
        private IntPtr targetWindow = IntPtr.Zero;
        private string targetProcessName = string.Empty;
        private int commandCounter = 1;

        // Events
        public event EventHandler<CommandRecordedEventArgs> CommandRecorded;
        public event EventHandler<RecordingStateChangedEventArgs> RecordingStateChanged;
        public event EventHandler<ElementUsageEventArgs> ElementUsageUpdated;
        public event EventHandler<WindowAutoDetectedEventArgs> WindowAutoDetected; // NOVÉ

        public bool IsRecording => isRecording && !isPaused;
        public bool IsPaused => isPaused;
        public CommandSequence CurrentSequence => currentSequence;
        public Dictionary<string, ElementUsageStats> ElementStats => new Dictionary<string, ElementUsageStats>(elementStats);

        // Nastavenia pre automatickú detekciu okien
        public bool AutoDetectNewWindows { get; set; } = true;
        public bool AutoSwitchToNewWindows { get; set; } = true; // Automaticky prepnúť na nové okno
        public bool LogWindowChanges { get; set; } = true;

        // WinUI3 debugging properties
        public bool EnableWinUI3Analysis { get; set; } = true;
        public bool EnableDetailedLogging { get; set; } = true;

        public CommandRecorder()
        {
            globalHook = new GlobalHook();
            windowTracker = new WindowTracker(); // NOVÉ
            elementStats = new Dictionary<string, ElementUsageStats>();
            trackedWindows = new Dictionary<IntPtr, string>(); // NOVÉ

            // Subscribe to hook events
            globalHook.KeyPressed += OnKeyPressed;
            globalHook.MouseClicked += OnMouseClicked;

            // Subscribe to window tracker events - NOVÉ
            windowTracker.NewWindowDetected += OnNewWindowDetected;
            windowTracker.WindowActivated += OnWindowActivated;
            windowTracker.WindowClosed += OnWindowClosed;
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
            trackedWindows.Clear(); // NOVÉ
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

                // Pridaj primary target do sledovaných okien
                trackedWindows[targetWindow] = $"{targetProcessName} - {currentSequence.TargetWindowTitle}";

                System.Diagnostics.Debug.WriteLine($"Recording target: {targetProcessName} - {currentSequence.TargetWindowTitle}");
            }

            // NOVÉ - Spusti window tracking ak je povolené
            if (AutoDetectNewWindows)
            {
                windowTracker.TrackOnlyTargetProcess = !string.IsNullOrEmpty(targetProcessName);
                windowTracker.StartTracking(targetWindow, targetProcessName);
                System.Diagnostics.Debug.WriteLine("Window tracking started for automatic detection");
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

            // NOVÉ - Zastaví window tracking
            if (AutoDetectNewWindows)
            {
                windowTracker.StopTracking();
                System.Diagnostics.Debug.WriteLine("Window tracking stopped");
            }

            isRecording = false;
            isPaused = false;

            // NOVÉ - Log sledovaných okien
            if (LogWindowChanges && trackedWindows.Count > 1)
            {
                System.Diagnostics.Debug.WriteLine($"=== RECORDING SESSION SUMMARY ===");
                System.Diagnostics.Debug.WriteLine($"Primary target: {trackedWindows.FirstOrDefault().Value}");
                System.Diagnostics.Debug.WriteLine($"Additional windows detected: {trackedWindows.Count - 1}");
                foreach (var window in trackedWindows.Skip(1))
                {
                    System.Diagnostics.Debug.WriteLine($"  - {window.Value}");
                }
            }

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

        // NOVÉ - Event handlers pre WindowTracker
        private void OnNewWindowDetected(object sender, NewWindowDetectedEventArgs e)
        {
            if (!isRecording || isPaused) return;

            try
            {
                var windowInfo = e.WindowInfo;
                string windowDescription = $"{windowInfo.ProcessName} - {windowInfo.Title}";

                // Pridaj do sledovaných okien
                if (!trackedWindows.ContainsKey(e.WindowHandle))
                {
                    trackedWindows[e.WindowHandle] = windowDescription;
                }

                System.Diagnostics.Debug.WriteLine($"=== NEW WINDOW AUTO-DETECTED ===");
                System.Diagnostics.Debug.WriteLine($"Window: {windowDescription}");
                System.Diagnostics.Debug.WriteLine($"Type: {windowInfo.WindowType}");
                System.Diagnostics.Debug.WriteLine($"Method: {e.DetectionMethod}");

                // Automaticky prepni na nové okno ak je to povolené
                if (AutoSwitchToNewWindows && ShouldSwitchToWindow(windowInfo))
                {
                    SwitchToNewWindow(e.WindowHandle, windowDescription);
                }

                // Trigger event pre UI
                WindowAutoDetected?.Invoke(this, new WindowAutoDetectedEventArgs
                {
                    WindowHandle = e.WindowHandle,
                    WindowInfo = windowInfo,
                    Description = windowDescription,
                    AutoSwitched = AutoSwitchToNewWindows && ShouldSwitchToWindow(windowInfo)
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling new window detection: {ex.Message}");
            }
        }

        private void OnWindowActivated(object sender, WindowActivatedEventArgs e)
        {
            if (!isRecording || isPaused) return;

            // Log aktivácie okna ak je sledované
            if (trackedWindows.ContainsKey(e.WindowHandle))
            {
                System.Diagnostics.Debug.WriteLine($"Window activated: {trackedWindows[e.WindowHandle]}");

                // Automaticky prepni target ak je to povolené
                if (AutoSwitchToNewWindows && e.WindowHandle != targetWindow)
                {
                    SwitchToNewWindow(e.WindowHandle, trackedWindows[e.WindowHandle]);
                }
            }
        }

        private void OnWindowClosed(object sender, WindowClosedEventArgs e)
        {
            if (!isRecording) return;

            if (trackedWindows.ContainsKey(e.WindowHandle))
            {
                string windowDescription = trackedWindows[e.WindowHandle];
                System.Diagnostics.Debug.WriteLine($"Tracked window closed: {windowDescription}");

                // Odstráň zo sledovaných okien
                trackedWindows.Remove(e.WindowHandle);

                // Ak sa zatvoril target window, prepni na primary target
                if (e.WindowHandle == targetWindow && trackedWindows.Any())
                {
                    var primaryTarget = trackedWindows.First();
                    SwitchToNewWindow(primaryTarget.Key, primaryTarget.Value);
                    System.Diagnostics.Debug.WriteLine($"Target window closed, switched back to: {primaryTarget.Value}");
                }
            }
        }

        /// <summary>
        /// Rozhodne či prepnúť na nové okno automaticky
        /// </summary>
        private bool ShouldSwitchToWindow(WindowTrackingInfo windowInfo)
        {
            // Vždy prepni na dialógy a message boxy
            if (windowInfo.WindowType == WindowType.Dialog ||
                windowInfo.WindowType == WindowType.MessageBox)
                return true;

            // Prepni na modálne okná
            if (windowInfo.IsModal)
                return true;

            // Prepni ak je to okno z target procesu
            if (!string.IsNullOrEmpty(targetProcessName) &&
                windowInfo.ProcessName.Equals(targetProcessName, StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        /// <summary>
        /// Prepne na nové okno ako target
        /// </summary>
        private void SwitchToNewWindow(IntPtr newWindowHandle, string description)
        {
            try
            {
                var oldTarget = targetWindow;
                targetWindow = newWindowHandle;

                System.Diagnostics.Debug.WriteLine($"Auto-switched target window from {GetWindowTitleFromHandle(oldTarget)} to {description}");

                // Pridaj comment command do sekvencie
                AddWindowSwitchCommand(description);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error switching to new window: {ex.Message}");
            }
        }

        /// <summary>
        /// Pridá comment command o prepnutí okna
        /// </summary>
        private void AddWindowSwitchCommand(string windowDescription)
        {
            try
            {
                var switchCommand = new Command(commandCounter++, $"Window_Switch", CommandType.Wait)
                {
                    Value = "0", // 0ms wait, len marker
                    TargetWindow = windowDescription,
                    TargetProcess = targetProcessName,
                    ElementX = -1,
                    ElementY = -1
                };

                // Špeciálne označenie pre window switch
                switchCommand.ElementClass = "WindowSwitch";
                switchCommand.ElementControlType = "AutoDetected";

                AddCommand(switchCommand, null);

                System.Diagnostics.Debug.WriteLine($"Added window switch marker: {windowDescription}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding window switch command: {ex.Message}");
            }
        }

        /// <summary>
        /// Manuálne pridanie okna do trackingu
        /// </summary>
        public void AddWindowToTracking(IntPtr windowHandle, string description = "")
        {
            if (windowHandle == IntPtr.Zero) return;

            try
            {
                if (string.IsNullOrEmpty(description))
                {
                    description = $"{GetProcessNameFromWindow(windowHandle)} - {GetWindowTitleFromHandle(windowHandle)}";
                }

                trackedWindows[windowHandle] = description;

                if (isRecording && AutoDetectNewWindows)
                {
                    windowTracker.AddWindow(windowHandle, description);
                }

                System.Diagnostics.Debug.WriteLine($"Manually added window to tracking: {description}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding window to tracking: {ex.Message}");
            }
        }

        /// <summary>
        /// Získa zoznam sledovaných okien
        /// </summary>
        public Dictionary<IntPtr, string> GetTrackedWindows()
        {
            return new Dictionary<IntPtr, string>(trackedWindows);
        }

        /// <summary>
        /// Prepne target window manuálne
        /// </summary>
        public void SwitchTargetWindow(IntPtr newTargetWindow)
        {
            if (newTargetWindow == IntPtr.Zero || newTargetWindow == targetWindow)
                return;

            try
            {
                var oldTarget = targetWindow;
                targetWindow = newTargetWindow;

                string newDescription = trackedWindows.ContainsKey(newTargetWindow)
                    ? trackedWindows[newTargetWindow]
                    : $"{GetProcessNameFromWindow(newTargetWindow)} - {GetWindowTitleFromHandle(newTargetWindow)}";

                // Pridaj do trackingu ak tam nie je
                if (!trackedWindows.ContainsKey(newTargetWindow))
                {
                    trackedWindows[newTargetWindow] = newDescription;
                }

                System.Diagnostics.Debug.WriteLine($"Manually switched target from {GetWindowTitleFromHandle(oldTarget)} to {newDescription}");

                if (isRecording)
                {
                    AddWindowSwitchCommand(newDescription);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error switching target window: {ex.Message}");
            }
        }

        private void OnKeyPressed(object sender, KeyPressedEventArgs e)
        {
            if (!isRecording || isPaused)
                return;

            // ROZŠÍRENÉ - Kontroluj či klik prišiel z aktuálneho target window alebo sledovaného okna
            bool isFromTrackedWindow = false;
            if (targetWindow != IntPtr.Zero && e.WindowHandle == targetWindow)
            {
                isFromTrackedWindow = true;
            }
            else if (trackedWindows.ContainsKey(e.WindowHandle))
            {
                // Automaticky prepni target ak je autoswitch povolený
                if (AutoSwitchToNewWindows)
                {
                    SwitchToNewWindow(e.WindowHandle, trackedWindows[e.WindowHandle]);
                }
                isFromTrackedWindow = true;
            }

            // Ak nie je z trackovaného okna a je strict mode, ignoruj
            if (!isFromTrackedWindow && AutoDetectNewWindows && !string.IsNullOrEmpty(targetProcessName))
            {
                System.Diagnostics.Debug.WriteLine($"Ignoring key press from untracked window: {e.ProcessName}");
                return;
            }

            // Skip certain system keys
            if (ShouldSkipKey(e.Key))
                return;

            System.Diagnostics.Debug.WriteLine($"Recording key: {e.Key} in {e.ProcessName} (Window: {trackedWindows.ContainsKey(e.WindowHandle)})");

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

            // ROZŠÍRENÉ - Kontroluj či klik prišiel z aktuálneho target window alebo sledovaného okna
            bool isFromTrackedWindow = false;
            string currentWindowDescription = "";

            if (targetWindow != IntPtr.Zero && e.WindowHandle == targetWindow)
            {
                isFromTrackedWindow = true;
                currentWindowDescription = trackedWindows.ContainsKey(targetWindow) ? trackedWindows[targetWindow] : "Current Target";
            }
            else if (trackedWindows.ContainsKey(e.WindowHandle))
            {
                isFromTrackedWindow = true;
                currentWindowDescription = trackedWindows[e.WindowHandle];

                // Automaticky prepni target ak je autoswitch povolený
                if (AutoSwitchToNewWindows && e.WindowHandle != targetWindow)
                {
                    SwitchToNewWindow(e.WindowHandle, currentWindowDescription);
                }
            }

            // Ak nie je z trackovaného okna, skús automaticky detekovať
            if (!isFromTrackedWindow && AutoDetectNewWindows)
            {
                // Skús pridať okno do trackingu
                string newWindowDesc = $"{e.ProcessName} - {e.WindowTitle}";

                // Pridaj iba ak je z target procesu alebo ak nie je strict mode
                if (string.IsNullOrEmpty(targetProcessName) ||
                    e.ProcessName.Equals(targetProcessName, StringComparison.OrdinalIgnoreCase))
                {
                    AddWindowToTracking(e.WindowHandle, newWindowDesc);
                    currentWindowDescription = newWindowDesc;
                    isFromTrackedWindow = true;

                    if (AutoSwitchToNewWindows)
                    {
                        SwitchToNewWindow(e.WindowHandle, newWindowDesc);
                    }

                    System.Diagnostics.Debug.WriteLine($"Auto-added new window to tracking: {newWindowDesc}");
                }
            }

            if (!isFromTrackedWindow)
            {
                System.Diagnostics.Debug.WriteLine($"Ignoring click from untracked window: {e.ProcessName} - {e.WindowTitle}");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"Recording mouse click at ({e.X}, {e.Y}) in {currentWindowDescription}");

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
                TargetWindow = currentWindowDescription, // Použij aktuálny popis okna
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
                        $"WinUI3={command.IsWinUI3Element}, ClickPos=({command.ElementX}, {command.ElementY}), " +
                        $"Window='{currentWindowDescription}'";

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

            System.Diagnostics.Debug.WriteLine($"Command recorded: Step {command.StepNumber}: {command.Type} on {command.ElementName} in {currentWindowDescription}");
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
            windowTracker?.StopTracking();
        }

        // Windows API import pre GetClassName
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);
    }

    // NOVÉ - Event argument classes pre window tracking
    public class WindowAutoDetectedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public WindowTrackingInfo WindowInfo { get; set; }
        public string Description { get; set; } = "";
        public bool AutoSwitched { get; set; }
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
    }a názov procesu z window handle
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
    } 
}
