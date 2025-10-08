using AppCommander.W7_11.WPF.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Forms;

namespace AppCommander.W7_11.WPF.Core
{
    public class CommandRecorder
    {
        #region Properties

        // Basic properties
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

        // 
        private CommandExecutionManager executionManager;
        private ExecutionSpeedControl speedControl;

        // Advanced properties for automatic detection
        private readonly AutoWindowDetector autoWindowDetector;
        private readonly UIElementScanner uiElementScanner;
        private readonly Dictionary<IntPtr, WindowContext> windowContexts;

        // Configuration of automatic detection
        public bool EnableRealTimeElementScanning { get; set; } = true;
        public bool AutoUpdateExistingCommands { get; set; } = true;
        public int ElementScanInterval { get; set; } = 750; // ms
        public bool EnablePredictiveDetection { get; set; } = true;

        // Settings for window management - automatic detection and switching
        public bool AutoDetectNewWindows { get; set; } = true;
        public bool AutoSwitchToNewWindows { get; set; } = true;
        public bool LogWindowChanges { get; set;  } = true;

        // WinUI3 debugging properties
        public bool EnableWinUI3Analysis { get; set; } = true;
        public bool EnableDetailedLogging { get; set; } = true;

        private bool isShiftPressed = false;
        private DateTime lastShiftPressTime = DateTime.MinValue;
        private Command pendingShiftCommand = null;
        private const int SHIFT_COMBINATION_TIMEOUT = 100; // ms

        #endregion

        #region AppCommander UI Blacklist

        /// <summary>
        /// Blacklist všetkých UI elementov AppCommander
        /// </summary>
        private static readonly HashSet<string> AppCommanderUIElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // === HLAVNÉ OVLÁDACIE TLAČIDLÁ ===
            "AppCommander_BtnRecording",              // Start/Stop Recording tlačidlo
            "AppCommander_BtnSelectTarget",           // Browse target selection
            "AppCommander_AppCommander_BtnSelectTargetByClick",    // Click to select target
            "AppCommander_BtnPlayCommands",           // Play button
            "AppCommander_BtnPause",                  // Pause playback
            "AppCommander_BtnStop",                   // Stop playback
    
            // === AKČNÉ TLAČIDLÁ ===
            "AppCommander_BtnRefreshStats",           // Refresh statistics
            "ButtonStartRecording",      // Alternative start recording
            "ButtonStopRecording",       // Alternative stop recording
            "BtnSaveSequence",           // Save sequence button
            "BtnLoadSequence",           // Load sequence button
    
            // === TEXTBOXY A INPUTY ===
            "AppCommander_TxtRepeatCount",            // Repeat count input
            "AppCommander_TxtSequenceName",           // Sequence name input
            "AppCommander_TxtSequenceName_Copy",      // Copy of sequence name
    
            // === CHECKBOXY ===
            "AppCommander_ChkInfiniteLoop",           // Infinite loop checkbox
    
            // === LABELS A TEXTBLOCKY ===
            "AppCommander_TxtCommandCount",           // Command count display
            "AppCommander_AppCommander_TxtStatusBar",              // Status bar text
            "AppCommander_TxtTarget",                 // Target window info
            "AppCommander_LblStatusBarRecording",     // Recording status label
            "AppCommander_TxtSetCount",               // Sets count display
            "AppCommander_TxtSequenceCount",          // Sequences count display
            "AppCommander_TxtCommandsCount",          // Commands count display
            "AppCommander_LblTargetWindow",           // Target window label
    
            // === TABUĽKY A ZOZNAMY ===
            "AppCommander_MainCommandTable",          // Main command DataGrid
            "AppCommander_LstElementStats",           // Element statistics list
            "dgWindows",                 // Windows list (WindowSelectorDialog)
    
            // === PROGRESS INDIKÁTORY ===
            "AppCommander_ProgressEnhancedRecording", // Recording progress bar
    
            // === INÉ UI ELEMENTY ===
            "AppCommander_SelectionModeIndicator",    // Selection mode indicator
            "AppCommander_TxtSelectionMode",          // Selection mode text
            "AppCommander_MenuBar",                   // Main menu bar
    
            // === AUTOMATION PROPERTIES (môžu byť použité ako identifikátory) ===
            "RecordButton",              // AutomationId for recording button
            "PlayButton",                // AutomationId for play button
    
            // === OKNÁ A DIALÓGY ===
            "WindowSelectorDialog",      // Window selector dialog
            "EditCommandWindow",         // Edit command window
            "AppCommander",              // Main window title/class
    
            // === DODATOČNÉ BEZPEČNOSTNÉ POLOŽKY ===
            // Ak má element text content ktorý obsahuje tieto frázy
            "Start Recording",
            "Stop Recording",
            "Click to Select",
            "Cancel Selection",
            "Play",
            "Pause",
            "Resume",
            "Refresh Stats",
            "Element Inspector",
            "Settings",
            "Debug Info"
        };

        #endregion

        #region Events

        public event EventHandler<CommandRecordedEventArgs> CommandRecorded;
        public event EventHandler<RecordingStateChangedEventArgs> RecordingStateChanged;
        public event EventHandler<ElementUsageEventArgs> ElementUsageUpdated;
        public event EventHandler<WindowAutoDetectedEventArgs> WindowAutoDetected;

        // **Rozšírené eventy**
        public event EventHandler<WindowContextChangedEventArgs> WindowContextChanged;
        public event EventHandler<UIElementsUpdatedEventArgs> UIElementsUpdated;
        public event EventHandler<LiveRecordingStartedEventArgs> LiveRecordingStarted;

        #endregion

        #region Public Properties

        public bool IsRecording => isRecording && !isPaused;
        public bool IsPaused => isPaused;
        public CommandSequence CurrentSequence => currentSequence;
        public Dictionary<string, ElementUsageStats> ElementStats => new Dictionary<string, ElementUsageStats>(elementStats);

        public EventHandler<ExecutionSpeedChangedEventArgs> OnExecutionSpeedChanged { get; private set; }
        public EventHandler<string> OnExecutionSpeedAdjusted { get; private set; }
        public EventHandler<CommandExecutionInfo> OnCommandStateChanged { get; private set; }

        #endregion

        #region Constructor

        public CommandRecorder()
        {
            globalHook = new GlobalHook();
            windowTracker = new WindowTracker();
            elementStats = new Dictionary<string, ElementUsageStats>();
            trackedWindows = new Dictionary<IntPtr, string>();

            // **Inicializácia rozšírených komponentov**
            autoWindowDetector = new AutoWindowDetector();
            uiElementScanner = new UIElementScanner();
            windowContexts = new Dictionary<IntPtr, WindowContext>();

            // Subscribe to hook events
            globalHook.KeyPressed += OnKeyPressed;
            globalHook.MouseClicked += OnMouseClicked;

            // Subscribe to window tracker events
            windowTracker.NewWindowDetected += OnNewWindowDetected;
            windowTracker.WindowActivated += OnWindowActivated;
            windowTracker.WindowClosed += OnWindowClosed;

            // **Konfigurácia automatickej detekcie**
            ConfigureAutomaticDetection();

            System.Diagnostics.Debug.WriteLine("🚀 CommandRecorder initialized with automatic detection");
        }

        private void InitializeExecutionManager()
        {
            executionManager = new CommandExecutionManager(windowTracker, automaticUIManager);

            // Nastavenie event handlerov
            executionManager.CommandStateChanged += OnCommandStateChanged;
            executionManager.ExecutionSpeedAdjusted += OnExecutionSpeedAdjusted;

            // Vytvorenie UI kontroly
            speedControl = new ExecutionSpeedControl();
            speedControl.Initialize(executionManager);
            speedControl.ExecutionSpeedChanged += OnExecutionSpeedChanged;
        }

        #endregion

        #region Configuration

        /// <summary>
        /// Konfiguruje automatickú detekciu
        /// </summary>
        private void ConfigureAutomaticDetection()
        {
            // Konfigurácia auto window detector
            autoWindowDetector.EnableDialogDetection = true;
            autoWindowDetector.EnableMessageBoxDetection = true;
            autoWindowDetector.EnableChildWindowDetection = true;
            autoWindowDetector.EnableWinUI3Detection = true;
            autoWindowDetector.DetectionSensitivity = DetectionSensitivity.High;

            // Konfigurácia UI element scanner
            uiElementScanner.ScanInterval = ElementScanInterval;
            uiElementScanner.EnableDeepScanning = true;
            uiElementScanner.EnableWinUI3ElementDetection = true;
            uiElementScanner.MaxElementsPerScan = 100;

            // Pripojenie eventov
            autoWindowDetector.NewWindowDetected += OnAutoWindowDetected;
            autoWindowDetector.WindowActivated += OnAutoWindowActivated;
            autoWindowDetector.WindowClosed += OnAutoWindowClosed;

            uiElementScanner.ElementsChanged += OnUIElementsChanged;
            uiElementScanner.NewElementDetected += OnNewElementDetected;
            uiElementScanner.ElementDisappeared += OnElementDisappeared;
        }

        #endregion

        #region ExecutionSpeedControl - event handler

        ///// <summary>
        ///// Event handler pre zmeny stavu príkazov
        ///// </summary>
        //private void OnCommandStateChanged(object sender, CommandExecutionInfo executionInfo)
        //{
        //    System.Diagnostics.Debug.WriteLine($"🔄 Command {executionInfo.StepNumber}: {executionInfo.State}");

        //    // Aktualizuj UI indikátor
        //    UpdateExecutionStatusUI(executionInfo);

        //    // Ak je command failed, pridaj do logu
        //    if (executionInfo.State == CommandExecutionState.Failed)
        //    {
        //        LogExecutionFailure(executionInfo);
        //    }
        //}

        ///// <summary>
        ///// Event handler pre automatické prispôsobenie rýchlosti
        ///// </summary>
        //private void OnExecutionSpeedAdjusted(object sender, string reason)
        //{
        //    System.Diagnostics.Debug.WriteLine($"⚡ Execution speed adjusted: {reason}");

        //    // Aktualizuj UI
        //    if (speedControl != null)
        //    {
        //        // Update usages of 'speedControl' to use the correct type (no cast needed)
        //        // Example: In OnExecutionSpeedAdjusted, this line is now valid:
        //        speedControl.UpdateSpeedIndicator(reason);
        //    }
        //}

        //// Add this method to fix CS0103: The name 'OnExecutionSpeedChanged' does not exist in the current context

        ///// <summary>
        ///// Event handler for manual execution speed changes
        ///// </summary>
        //private void OnExecutionSpeedChanged(object sender, ExecutionSpeedChangedEventArgs e)
        //{
        //    System.Diagnostics.Debug.WriteLine($"🎛️ Speed manually changed to: {e.Speed}");

        //    // Save settings
        //    SaveExecutionSettings(e.Settings);
        //}

        /// <summary>
        /// Vykonaj sequence s kontrolou rýchlosti
        /// </summary>
        public async Task<bool> ExecuteSequenceWithSpeedControl(CommandSequence sequence,
            CancellationToken cancellationToken = default)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🚀 Starting sequence execution with {sequence.Commands.Count} commands");

                bool allCommandsSucceeded = true;
                int successfulCommands = 0;

                foreach (var command in sequence.Commands)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        System.Diagnostics.Debug.WriteLine("⏹️ Execution cancelled by user");
                        break;
                    }

                    System.Diagnostics.Debug.WriteLine($"▶️ Executing command {command.StepNumber}: {command.Type}");

                    // Použije execution manager pre riadené vykonávanie
                    bool success = await executionManager.ExecuteCommandWithWait(command, cancellationToken);

                    if (success)
                    {
                        successfulCommands++;
                    }
                    else
                    {
                        allCommandsSucceeded = false;
                        System.Diagnostics.Debug.WriteLine($"❌ Command {command.StepNumber} failed");

                        // Rozhodnutie či pokračovať alebo prerušiť
                        if (!ShouldContinueAfterFailure(command, sequence))
                        {
                            break;
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"✅ Sequence execution completed: {successfulCommands}/{sequence.Commands.Count} successful");

                return allCommandsSucceeded;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Sequence execution error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Rozhodne či pokračovať po neúspešnom príkaze
        /// </summary>
        private bool ShouldContinueAfterFailure(Command failedCommand, CommandSequence sequence)
        {
            // Pre kritické príkazy (napr. login) nepokračuj
            if (failedCommand.Type == CommandType.SetText &&
                (failedCommand.ElementName?.ToLower().Contains("password") == true ||
                 failedCommand.ElementName?.ToLower().Contains("login") == true))
            {
                return false;
            }

            // Pre ostatné príkazy pokračuj
            return true;
        }

        ///// <summary>
        ///// Aktualizuj UI status
        ///// </summary>
        //private void UpdateExecutionStatusUI(CommandExecutionInfo executionInfo)
        //{
        //    // Dispatch na UI thread ak je potrebné
        //    Application.Current?.Dispatcher.InvokeAsync(() =>
        //    {
        //        // Aktualizuj progress bar alebo status label
        //        if (speedControl != null)
        //        {
        //            speedControl.UpdateExecutionStatus(executionInfo);
        //        }
        //    });
        //}

        /// <summary>
        /// Zaloguj chybu vykonania
        /// </summary>
        private void LogExecutionFailure(CommandExecutionInfo executionInfo)
        {
            var logEntry = new
            {
                Timestamp = DateTime.Now,
                StepNumber = executionInfo.StepNumber,
                State = executionInfo.State,
                Duration = executionInfo.Duration,
                ErrorMessage = executionInfo.ErrorMessage
            };

            // Pridaj do execution log
            executionLog.Add(logEntry);

            // Možnosť exportu do súboru pre debugging
            if (executionLog.Count % 10 == 0) // každých 10 chýb
            {
                ExportExecutionLog();
            }
        }

        private List<object> executionLog = new List<object>();
        private AutomaticUIManager automaticUIManager;

        /// <summary>;
        /// Export execution log pre analýzu
        /// </summary>
        private void ExportExecutionLog()
        {
            try
            {
                var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CommandRecorder", "ExecutionLogs");

                Directory.CreateDirectory(logPath);

                var fileName = $"execution_log_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                var filePath = Path.Combine(logPath, fileName);

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(executionLog, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(filePath, json);

                System.Diagnostics.Debug.WriteLine($"📄 Execution log exported: {filePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Failed to export execution log: {ex.Message}");
            }
        }

        /// <summary>
        /// Ulož nastavenia execution managera
        /// </summary>
        private void SaveExecutionSettings(ExecutionSettings settings)
        {
            try
            {
                var settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CommandRecorder", "execution_settings.json");

                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(settings, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(settingsPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Failed to save execution settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Načítaj nastavenia execution managera
        /// </summary>
        private ExecutionSettings LoadExecutionSettings()
        {
            try
            {
                var settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CommandRecorder", "execution_settings.json");

                if (File.Exists(settingsPath))
                {
                    var json = File.ReadAllText(settingsPath);
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<ExecutionSettings>(json);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Failed to load execution settings: {ex.Message}");
            }

            // Vráť default nastavenia
            return new ExecutionSettings();
        }

        /// <summary>
        /// Získaj execution speed control pre integráciu do hlavného UI
        /// </summary>
        public ExecutionSpeedControl GetExecutionSpeedControl()
        {
            return speedControl;
        }

        /// <summary>
        /// Testovacie metódy pre debugging
        /// </summary>
        public async Task TestExecutionSpeed()
        {
            var testCommands = new List<Command>
        {
            new Command { StepNumber = 1, Type = CommandType.Click, ElementName = "TestButton1" },
            new Command { StepNumber = 2, Type = CommandType.SetText, ElementName = "TestTextBox", Value = "Test" },
            new Command { StepNumber = 3, Type = CommandType.Click, ElementName = "TestButton2" }
        };

            var sequence = new CommandSequence { Commands = testCommands };

            System.Diagnostics.Debug.WriteLine("🧪 Starting execution speed test");
            var startTime = DateTime.Now;

            await ExecuteSequenceWithSpeedControl(sequence);

            var totalTime = DateTime.Now.Subtract(startTime);
            System.Diagnostics.Debug.WriteLine($"🧪 Test completed in {totalTime.TotalSeconds:F2} seconds");
        }

        #endregion // ExecutionSpeedControl

        #region Recording Methods

        /// <summary>
        /// Spustí nahrávanie s ochranou proti nekonečnej slučke
        /// </summary>
        public virtual void StartRecording(string sequenceName, IntPtr targetWindowHandle = default(IntPtr))
        {
            // NOVÁ KONTROLA: Zabráň spusteniu nahrávania, ak už beží
            if (isRecording)
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Cannot start recording - already recording!");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine(string.Format("Starting recording: {0}", sequenceName));

                // Initialize new sequence
                currentSequence = new CommandSequence(sequenceName);
                targetWindow = targetWindowHandle;
                commandCounter = 1;
                elementStats.Clear();
                trackedWindows.Clear();
                isPaused = false;

                // Get target process name and window info if window handle provided
                if (targetWindow != IntPtr.Zero)
                {
                    targetProcessName = GetProcessNameFromWindow(targetWindow);
                    currentSequence.TargetApplication = targetProcessName;
                    currentSequence.TargetProcessName = targetProcessName;
                    currentSequence.TargetWindowTitle = GetWindowTitleFromHandle(targetWindow);
                    currentSequence.TargetWindowClass = GetWindowClassFromHandle(targetWindow);

                    // **NOVÁ KONTROLA: Zabráň nahrávaniu na samého seba**
                    if (targetProcessName.Equals("AppCommander", StringComparison.OrdinalIgnoreCase))
                    {
                        System.Diagnostics.Debug.WriteLine("🚫 Cannot record on AppCommander itself!");

                        // Upozornenie používateľa (použite vhodný spôsob v kontexte)
                        System.Diagnostics.Debug.WriteLine("⚠️ User tried to record on AppCommander - operation cancelled");
                        return;
                    }

                    // Track this window
                    if (!trackedWindows.ContainsKey(targetWindow))
                    {
                        trackedWindows[targetWindow] = GetWindowTitle(targetWindow);
                    }

                    System.Diagnostics.Debug.WriteLine(string.Format(
                        "Recording target: {0} - {1}",
                        targetProcessName,
                        currentSequence.TargetWindowTitle ?? ""));
                }

                // Start hook and tracking
                globalHook.StartHooking();
                isRecording = true;

                // Raise event - OPRAVENÉ: používa správny konštruktor
                RecordingStateChanged?.Invoke(this, new RecordingStateChangedEventArgs(
                    isRecording: true,
                    isPaused: false,
                    sequenceName: sequenceName
                ));

                System.Diagnostics.Debug.WriteLine("✅ Recording started successfully");
            }
            catch (Exception ex)
            {
                isRecording = false;
                System.Diagnostics.Debug.WriteLine($"❌ Error starting recording: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Overload pre spätnosť
        /// </summary>
        public void StartRecording(IntPtr targetWindowHandle)
        {
            StartRecording(string.Format("Recording_{0:yyyyMMdd_HHmmss}", DateTime.Now), targetWindowHandle);
        }

        private void AnalyzeCommandPatterns()
        {
            try
            {
                if (currentSequence != null && currentSequence.Commands != null && currentSequence.Commands.Count >= 3)
                {
                    // .NET 4.8 kompatibilný spôsob namiesto TakeLast(3)
                    var commandCount = currentSequence.Commands.Count;
                    var recentCommands = new List<Command>();
                    for (int i = Math.Max(0, commandCount - 3); i < commandCount; i++)
                    {
                        recentCommands.Add(currentSequence.Commands[i]);
                    }

                    // Detekuje opakujúce sa akcie
                    var allClicks = true;
                    foreach (var cmd in recentCommands)
                    {
                        if (cmd.Type != CommandType.Click)
                        {
                            allClicks = false;
                            break;
                        }
                    }

                    if (allClicks)
                    {
                        System.Diagnostics.Debug.WriteLine("Pattern detected: Multiple clicks sequence");
                    }

                    // Detekuje form filling pattern
                    var hasSetText = false;
                    var hasClick = false;
                    foreach (var cmd in recentCommands)
                    {
                        if (cmd.Type == CommandType.SetText) hasSetText = true;
                        if (cmd.Type == CommandType.Click) hasClick = true;
                    }

                    if (hasSetText && hasClick)
                    {
                        System.Diagnostics.Debug.WriteLine("Pattern detected: Form filling workflow");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("Error analyzing command patterns: {0}", ex.Message));
            }
        }

        /// <summary>
        /// Spustí automatické služby
        /// </summary>
        private void StartAutomaticServices(IntPtr primaryTarget)
        {
            try
            {
                // Spusti automatickú detekciu okien
                if (AutoDetectNewWindows)
                {
                    windowTracker.TrackOnlyTargetProcess = !string.IsNullOrEmpty(targetProcessName);
                    windowTracker.StartTracking(targetProcessName);
                    autoWindowDetector.StartDetection(primaryTarget, targetProcessName);
                    System.Diagnostics.Debug.WriteLine("Window tracking started for automatic detection");
                }

                // Spusti skenovanie UI elementov ak je povolené
                if (EnableRealTimeElementScanning)
                {
                    uiElementScanner.StartScanning(primaryTarget);
                }

                // Spusti prediktívnu detekciu
                if (EnablePredictiveDetection)
                {
                    StartPredictiveDetection();
                }

                System.Diagnostics.Debug.WriteLine("🔍 Automatic services started");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error starting automatic services: {ex.Message}");
            }
        }

        /// <summary>
        /// Vytvorí počiatočný kontext okna
        /// </summary>
        private void CreateInitialWindowContext(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero) return;

            try
            {
                var context = new WindowContext
                {
                    WindowHandle = windowHandle,
                    WindowTitle = GetWindowTitle(windowHandle),
                    ProcessName = GetProcessName(windowHandle),
                    WindowType = DetermineWindowType(windowHandle),
                    CreatedAt = DateTime.Now,
                    UIElements = ScanUIElements(windowHandle),
                    IsActive = true
                };

                windowContexts[windowHandle] = context;

                System.Diagnostics.Debug.WriteLine($"📋 Created window context: {context.WindowTitle}");
                System.Diagnostics.Debug.WriteLine($"   UI Elements: {context.UIElements.Count}");

                // Trigger event
                WindowContextChanged?.Invoke(this, new WindowContextChangedEventArgs
                {
                    WindowHandle = windowHandle,
                    Context = context,
                    ChangeType = ContextChangeType.Created
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error creating window context: {ex.Message}");
            }
        }

        /// <summary>
        /// Zastaví nahrávanie
        /// </summary>
        public virtual void StopRecording()
        {
            if (!isRecording)
                return;

            try
            {
                // Zastaví automatické služby
                StopAutomaticServices();

                // Stop hooks
                globalHook.StopHooking();
                windowTracker.StopTracking();

                isRecording = false;
                isPaused = false;

                RecordingStateChanged?.Invoke(this, new RecordingStateChangedEventArgs(false, false, currentSequence?.Name ?? ""));

                // **Analyzuj nahraté elementy**
                if (currentSequence != null && EnableWinUI3Analysis)
                {
                    AnalyzeRecordedElements();
                }

                System.Diagnostics.Debug.WriteLine("🛑 Recording stopped");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error stopping recording: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Zastaví automatické služby
        /// </summary>
        private void StopAutomaticServices()
        {
            try
            {
                autoWindowDetector?.StopDetection();
                uiElementScanner?.StopScanning();

                // Vyčisti contexts
                foreach (var context in windowContexts.Values)
                {
                    context.IsActive = false;
                }

                System.Diagnostics.Debug.WriteLine("🧹 Automatic services stopped and cleaned up");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error stopping automatic services: {ex.Message}");
            }
        }

        /// <summary>
        /// Pozastaví nahrávanie
        /// </summary>
        public void PauseRecording()
        {
            if (!isRecording || isPaused)
                return;

            isPaused = true;
            RecordingStateChanged?.Invoke(this, new RecordingStateChangedEventArgs(true, true, currentSequence?.Name ?? ""));

            System.Diagnostics.Debug.WriteLine("⏸ Recording paused");
        }

        /// <summary>
        /// Obnoví nahrávanie
        /// </summary>
        public void ResumeRecording()
        {
            if (!isRecording || !isPaused)
                return;

            isPaused = false;
            RecordingStateChanged?.Invoke(this, new RecordingStateChangedEventArgs(true, false, currentSequence?.Name ?? ""));

            System.Diagnostics.Debug.WriteLine("▶ Recording resumed");
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handler pre nové okno detekované window trackerom
        /// </summary>
        private void OnNewWindowDetected(object sender, NewWindowDetectedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🔍 Window tracker detected: {e.WindowInfo?.Title ?? "Unknown"}");

                // Ak je automatické prepínanie zapnuté
                if (AutoSwitchToNewWindows && ShouldAutoSwitchToWindow(e.WindowInfo))
                {
                    AutoSwitchToNewWindow(e.WindowInfo);
                }

                // Pridaj do sledovaných okien
                if (e.WindowInfo != null && !trackedWindows.ContainsKey(e.WindowInfo.WindowHandle))
                {
                    trackedWindows[e.WindowInfo.WindowHandle] = e.WindowInfo.Title;
                }

                // Trigger event pre UI
                if (WindowAutoDetected != null)
                {
                    var windowHandle = (e.WindowInfo != null) ? e.WindowInfo.WindowHandle : IntPtr.Zero;
                    var title = (e.WindowInfo != null) ? e.WindowInfo.Title : "Unknown";
                    var processName = (e.WindowInfo != null) ? e.WindowInfo.ProcessName : null;
                    var windowType = (e.WindowInfo != null) ? e.WindowInfo.WindowType : WindowType.MainWindow;

                    var eventArgs = new WindowAutoDetectedEventArgs(
                        windowHandle,
                        string.Format("Auto-detected: {0}", title),
                        title,
                        processName,
                        windowType);

                    WindowAutoDetected(this, eventArgs);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error handling new window detected: {ex.Message}");
            }
        }

        /// <summary>
        /// Handler pre aktivované okno
        /// </summary>
        private void OnWindowActivated(object sender, WindowActivatedEventArgs e)
        {
            try
            {
                if (windowContexts.ContainsKey(e.WindowHandle))
                {
                    var context = windowContexts[e.WindowHandle];
                    context.IsActive = true;
                    context.LastActivated = DateTime.Now;

                    System.Diagnostics.Debug.WriteLine($"🎯 Window activated: {context.WindowTitle}");

                    // Ak je nahrávanie aktívne a toto nie je target window, možno prepni
                    if (IsRecording && e.WindowHandle != targetWindow)
                    {
                        HandleWindowActivationDuringRecording(context);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error handling window activation: {ex.Message}");
            }
        }

        /// <summary>
        /// Handler pre zatvorené okno
        /// </summary>
        private void OnWindowClosed(object sender, WindowClosedEventArgs e)
        {
            try
            {
                // Odstráň zo sledovaných okien
                if (trackedWindows.ContainsKey(e.WindowHandle))
                {
                    var description = trackedWindows[e.WindowHandle];
                    trackedWindows.Remove(e.WindowHandle);
                    System.Diagnostics.Debug.WriteLine($"Window closed and removed from tracking: {description}");
                }

                // Aktualizuj context
                if (windowContexts.ContainsKey(e.WindowHandle))
                {
                    var context = windowContexts[e.WindowHandle];
                    context.IsActive = false;
                    context.ClosedAt = DateTime.Now;

                    // Ak sa zatvoril target window, pokús sa nájsť náhradu
                    if (e.WindowHandle == targetWindow && IsRecording)
                    {
                        HandleTargetWindowClosed();
                    }

                    ScheduleContextCleanup(e.WindowHandle);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error handling window closed: {ex.Message}");
            }
        }

        /// <summary>
        /// Handler pre stlačenie klávesy s rozšíreným mapovaním
        /// </summary>
        private void OnKeyPressed(object sender, KeyPressedEventArgs e)
        {
            if (!IsRecording) return;

            try
            {
                // **ROZŠÍRENÉ MAPOVANIE: Spracuj SHIFT kombinacie**
                var processedKey = ProcessKeyWithShiftSupport(e.Key);

                // Ak sa klávesa spracovala ako súčasť SHIFT kombinacie, preskočíme ju
                if (processedKey == null) return;

                var command = new Command(commandCounter++, "Key_Press", CommandType.KeyPress, 0, 0)
                {
                    Value = processedKey.ToString(),
                    Key = processedKey.Value,
                    TargetWindow = GetWindowTitle(targetWindow),
                    TargetProcess = targetProcessName,
                    Timestamp = DateTime.Now
                };

                AddCommand(command);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error recording key press: {ex.Message}");
            }
        }

        /// <summary>
        /// Spracuje klávesy s podporou SHIFT kombinácií
        /// </summary>
        private Keys? ProcessKeyWithShiftSupport(Keys originalKey)
        {
            // Najprv mapuj NumPad klávesy
            var mappedKey = MapNumPadToDigits(originalKey);

            // Spracuj SHIFT klávesy
            if (mappedKey == Keys.LShiftKey || mappedKey == Keys.RShiftKey)
            {
                HandleShiftKeyPress();
                return null; // Nenahrávamc samostatnú SHIFT udalosť
            }

            // Ak je SHIFT stlačený a toto je písmeno/číslica, kombinuj ich
            if (isShiftPressed && IsShiftCombinable(mappedKey))
            {
                var combinedKey = CombineWithShift(mappedKey);
                ResetShiftState();
                return combinedKey;
            }

            // Ak prešiel SHIFT timeout, resetuj stav
            if (isShiftPressed && (DateTime.Now - lastShiftPressTime).TotalMilliseconds > SHIFT_COMBINATION_TIMEOUT)
            {
                ResetShiftState();
            }

            return mappedKey;
        }

        /// <summary>
        /// Spracuje stlačenie SHIFT klávesy
        /// </summary>
        private void HandleShiftKeyPress()
        {
            isShiftPressed = true;
            lastShiftPressTime = DateTime.Now;

            System.Diagnostics.Debug.WriteLine("🔄 SHIFT pressed - waiting for combination");
        }

        /// <summary>
        /// Kontroluje, či sa klávesa dá kombinovať so SHIFT
        /// </summary>
        private bool IsShiftCombinable(Keys key)
        {
            return (key >= Keys.A && key <= Keys.Z) ||        // Písmená
                   (key >= Keys.D0 && key <= Keys.D9) ||      // Číslice
                   IsShiftSymbolKey(key);                      // Symboly
        }

        /// <summary>
        /// Kontroluje, či je klávesa symbol ktorý sa mení so SHIFT
        /// </summary>
        private bool IsShiftSymbolKey(Keys key)
        {
            var shiftSymbols = new[] {
        Keys.OemMinus,      // - / _
        Keys.Oemplus,       // = / +
        Keys.OemOpenBrackets,   // [ / {
        Keys.Oem6,          // ] / }
        Keys.Oem5,          // \ / |
        Keys.Oem1,          // ; / :
        Keys.Oem7,          // ' / "
        Keys.Oemcomma,      // , / <
        Keys.OemPeriod,     // . / >
        Keys.OemQuestion,   // / / ?
        Keys.Oemtilde       // ` / ~
    };

            return shiftSymbols.Contains(key);
        }

        /// <summary>
        /// Kombinuje klávesy so SHIFT
        /// </summary>
        private Keys CombineWithShift(Keys key)
        {
            System.Diagnostics.Debug.WriteLine($"🔄 Combining SHIFT + {key}");

            // Písmená: malé → veľké
            if (key >= Keys.A && key <= Keys.Z)
            {
                // V .NET Keys enum sú písmená už veľké, ale označíme že je to SHIFT kombinácia
                return key | Keys.Shift;
            }

            // Číslice so SHIFT → symboly
            switch (key)
            {
                case Keys.D1: return Keys.D1 | Keys.Shift;  // 1 → !
                case Keys.D2: return Keys.D2 | Keys.Shift;  // 2 → @
                case Keys.D3: return Keys.D3 | Keys.Shift;  // 3 → #
                case Keys.D4: return Keys.D4 | Keys.Shift;  // 4 → $
                case Keys.D5: return Keys.D5 | Keys.Shift;  // 5 → %
                case Keys.D6: return Keys.D6 | Keys.Shift;  // 6 → ^
                case Keys.D7: return Keys.D7 | Keys.Shift;  // 7 → &
                case Keys.D8: return Keys.D8 | Keys.Shift;  // 8 → *
                case Keys.D9: return Keys.D9 | Keys.Shift;  // 9 → (
                case Keys.D0: return Keys.D0 | Keys.Shift;  // 0 → )

                // Symboly so SHIFT
                case Keys.OemMinus: return Keys.OemMinus | Keys.Shift;      // - → _
                case Keys.Oemplus: return Keys.Oemplus | Keys.Shift;       // = → +
                case Keys.OemOpenBrackets: return Keys.OemOpenBrackets | Keys.Shift; // [ → {
                case Keys.Oem6: return Keys.Oem6 | Keys.Shift;             // ] → }
                case Keys.Oem5: return Keys.Oem5 | Keys.Shift;             // \ → |
                case Keys.Oem1: return Keys.Oem1 | Keys.Shift;             // ; → :
                case Keys.Oem7: return Keys.Oem7 | Keys.Shift;             // ' → "
                case Keys.Oemcomma: return Keys.Oemcomma | Keys.Shift;     // , → <
                case Keys.OemPeriod: return Keys.OemPeriod | Keys.Shift;   // . → >
                case Keys.OemQuestion: return Keys.OemQuestion | Keys.Shift; // / → ?
                case Keys.Oemtilde: return Keys.Oemtilde | Keys.Shift;     // ` → ~

                default:
                    return key;
            }
        }

        /// <summary>
        /// Resetuje stav SHIFT
        /// </summary>
        private void ResetShiftState()
        {
            isShiftPressed = false;
            lastShiftPressTime = DateTime.MinValue;
            pendingShiftCommand = null;
        }

        /// <summary>
        /// Mapuje NumPad klávesy na hlavné číselné klávesy (pôvodná funkcia)
        /// </summary>
        private Keys MapNumPadToDigits(Keys originalKey)
        {
            switch (originalKey)
            {
                case Keys.NumPad0: return Keys.D0;
                case Keys.NumPad1: return Keys.D1;
                case Keys.NumPad2: return Keys.D2;
                case Keys.NumPad3: return Keys.D3;
                case Keys.NumPad4: return Keys.D4;
                case Keys.NumPad5: return Keys.D5;
                case Keys.NumPad6: return Keys.D6;
                case Keys.NumPad7: return Keys.D7;
                case Keys.NumPad8: return Keys.D8;
                case Keys.NumPad9: return Keys.D9;

                // Pre ostatné klávesy vráť pôvodný kód
                default:
                    return originalKey;
            }
        }

        /// <summary>
        /// Konvertuje Keys hodnotu s SHIFT na string reprezentáciu
        /// </summary>
        private string GetShiftKeyDisplayString(Keys key)
        {
            // Ak obsahuje SHIFT flag
            if ((key & Keys.Shift) == Keys.Shift)
            {
                var baseKey = key & ~Keys.Shift; // Odstráň SHIFT flag

                // Písmená zostanú veľké
                if (baseKey >= Keys.A && baseKey <= Keys.Z)
                {
                    return baseKey.ToString(); // Už veľké písmeno
                }

                // Číslice so SHIFT → symboly
                switch (baseKey)
                {
                    case Keys.D1: return "!";
                    case Keys.D2: return "@";
                    case Keys.D3: return "#";
                    case Keys.D4: return "$";
                    case Keys.D5: return "%";
                    case Keys.D6: return "^";
                    case Keys.D7: return "&";
                    case Keys.D8: return "*";
                    case Keys.D9: return "(";
                    case Keys.D0: return ")";
                    default: return key.ToString();
                }
            }

            return key.ToString();
        }

        /// <summary>
        /// Handler pre klik myšou + ignoruje kliky na AppCommander UI
        /// </summary>
        private void OnMouseClicked(object sender, MouseClickedEventArgs e)
        {
            if (!IsRecording) return;

            try
            {
                // FILTER 1: Kontrola ProcessName
                if (e.ProcessName != null &&
                    e.ProcessName.Equals("AppCommander", StringComparison.OrdinalIgnoreCase))
                {
                    System.Diagnostics.Debug.WriteLine($"🚫 Ignoring click on AppCommander UI: {e.ProcessName}");
                    return;
                }

                // FILTER 2: Kontrola Window Handle
                if (IsAppCommanderWindow(e.WindowHandle))
                {
                    System.Diagnostics.Debug.WriteLine($"🚫 Ignoring click on AppCommander window");
                    return;
                }

                // Zisti UI element na pozícii kliknutia
                var elementInfo = UIElementDetector.GetElementAtPoint(e.X, e.Y);

                // FILTER 3: Kontrola UI Blacklistu (nová vrstva)
                if (elementInfo != null && IsAppCommanderUIElement(elementInfo))
                {
                    System.Diagnostics.Debug.WriteLine($"🚫 Ignoring click - element is on AppCommander blacklist");
                    return;
                }

                var command = new Command(commandCounter++,
                    elementInfo?.Name ?? $"Click_at_{e.X}_{e.Y}",
                    CommandType.Click, e.X, e.Y)
                {
                    TargetWindow = GetWindowTitle(targetWindow),
                    TargetProcess = targetProcessName,
                    Timestamp = DateTime.Now
                };

                // Aktualizuj command s element info ak je dostupný
                if (elementInfo != null)
                {
                    command.UpdateFromElementInfoEnhanced(elementInfo);
                }

                AddCommand(command);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error recording mouse click: {ex.Message}");
            }
        }

        /// <summary>
        /// Pomocná metóda - skontroluje, či window handle patrí AppCommander
        /// </summary>
        private bool IsAppCommanderWindow(IntPtr windowHandle)
        {
            try
            {
                uint processId;
                GetWindowThreadProcessId(windowHandle, out processId);

                using (var process = Process.GetProcessById((int)processId))
                {
                    return process.ProcessName.Equals("AppCommander", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                return false;
            }
        }


        #endregion

        #region Auto Window Detection Event Handlers

        /// <summary>
        /// Handler pre automaticky detekované nové okno
        /// </summary>
        private void OnAutoWindowDetected(object sender, WindowAutoDetectedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🪟 Auto-detected new window: {e.WindowInfo?.Title ?? "Unknown"}");

                // Rozhodnie či automaticky prepnúť na toto okno
                if (ShouldAutoSwitchToWindow(e.WindowInfo))
                {
                    AutoSwitchToNewWindow(e.WindowInfo);
                }
                else
                {
                    // Len pridaj do kontextu bez prepnutia
                    AddWindowToContext(e.WindowInfo);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error handling auto window detection: {ex.Message}");
            }
        }

        /// <summary>
        /// Handler pre aktivované okno
        /// </summary>
        private void OnAutoWindowActivated(object sender, WindowActivatedEventArgs e)
        {
            try
            {
                if (windowContexts.ContainsKey(e.WindowHandle))
                {
                    var context = windowContexts[e.WindowHandle];
                    context.IsActive = true;
                    context.LastActivated = DateTime.Now;

                    System.Diagnostics.Debug.WriteLine($"🎯 Window activated: {context.WindowTitle}");

                    if (IsRecording && e.WindowHandle != targetWindow)
                    {
                        HandleWindowActivationDuringRecording(context);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error handling window activation: {ex.Message}");
            }
        }

        /// <summary>
        /// Handler pre zatvorené okno
        /// </summary>
        private void OnAutoWindowClosed(object sender, WindowClosedEventArgs e)
        {
            try
            {
                if (windowContexts.ContainsKey(e.WindowHandle))
                {
                    var context = windowContexts[e.WindowHandle];
                    context.IsActive = false;
                    context.ClosedAt = DateTime.Now;

                    System.Diagnostics.Debug.WriteLine($"🗑️ Window closed: {context.WindowTitle}");

                    if (e.WindowHandle == targetWindow && IsRecording)
                    {
                        HandleTargetWindowClosed();
                    }

                    ScheduleContextCleanup(e.WindowHandle);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error handling window close: {ex.Message}");
            }
        }

        /// <summary>
        /// Handler pre zmenu UI elementov
        /// </summary>
        private void OnUIElementsChanged(object sender, UIElementsChangedEventArgs e)
        {
            try
            {
                if (windowContexts.ContainsKey(e.WindowHandle))
                {
                    var context = windowContexts[e.WindowHandle]; // PRIDANÉ - definícia context
                    var previousCount = (context.UIElements != null) ? context.UIElements.Count : 0; // PRIDANÉ - definícia previousCount

                    // Konvertuj nové elementy
                    var newElements = new List<UIElementInfo>();
                    if (e.NewElements != null)
                    {
                        foreach (var snapshot in e.NewElements)
                        {
                            newElements.Add(new UIElementInfo
                            {
                                Name = snapshot.Name,
                                AutomationId = snapshot.AutomationId,
                                ClassName = snapshot.ClassName,
                                ControlType = snapshot.ControlType,
                                X = snapshot.X,
                                Y = snapshot.Y,
                                IsEnabled = snapshot.IsEnabled,
                                IsVisible = snapshot.IsVisible,
                                ElementText = snapshot.Text
                            });
                        }
                    }

                    // Aktualizuj kontext
                    context.UIElements = newElements;
                    context.LastUIUpdate = DateTime.Now;

                    System.Diagnostics.Debug.WriteLine(string.Format("UI elements changed in: {0}", context.WindowTitle));
                    System.Diagnostics.Debug.WriteLine(string.Format("   Previous: {0}, New: {1}", previousCount, context.UIElements.Count));

                    // If auto-update is enabled, update existing commands
                    if (AutoUpdateExistingCommands && e.WindowHandle == targetWindow)
                    {
                        UpdateExistingCommandsWithNewElements(e.NewElements);
                    }

                    // Trigger event - konvertuj predchádzajúce elementy
                    var previousElements = new List<UIElementInfo>();
                    if (e.PreviousElements != null)
                    {
                        foreach (var snapshot in e.PreviousElements)
                        {
                            previousElements.Add(new UIElementInfo
                            {
                                Name = snapshot.Name,
                                AutomationId = snapshot.AutomationId,
                                ClassName = snapshot.ClassName,
                                ControlType = snapshot.ControlType,
                                X = snapshot.X,
                                Y = snapshot.Y,
                                IsEnabled = snapshot.IsEnabled,
                                IsVisible = snapshot.IsVisible,
                                ElementText = snapshot.Text
                            });
                        }
                    }

                    if (UIElementsUpdated != null)
                    {
                        UIElementsUpdated(this, new UIElementsUpdatedEventArgs
                        {
                            WindowHandle = e.WindowHandle,
                            PreviousElements = previousElements,
                            NewElements = newElements, 
                            Context = context
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("Error handling UI elements change: {0}", ex.Message));
            }
        }

        /// <summary>
        /// Handler pre nový UI element
        /// </summary>
        private void OnNewElementDetected(object sender, NewElementDetectedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"➕ New UI element detected: {e.Element?.Name ?? "Unknown"}");

                // Aktualizuj kontext okna
                if (e.Element != null && windowContexts.ContainsKey(e.WindowHandle))
                {
                    var context = windowContexts[e.WindowHandle];

                    // Konvertuj UIElementSnapshot na UIElementInfo
                    var elementInfo = new UIElementInfo
                    {
                        Name = e.Element.Name,
                        AutomationId = e.Element.AutomationId,
                        ClassName = e.Element.ClassName,
                        ControlType = e.Element.ControlType,
                        X = e.Element.X,
                        Y = e.Element.Y,
                        IsEnabled = e.Element.IsEnabled,
                        IsVisible = e.Element.IsVisible,
                        ElementText = e.Element.Text
                    };

                    if (!context.UIElements.Any(el => el.AutomationId == elementInfo.AutomationId &&
                                                      el.Name == elementInfo.Name))
                    {
                        context.UIElements.Add(elementInfo); // Pridaj skonvertovaný element
                        context.LastUIUpdate = DateTime.Now;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error handling new element: {ex.Message}");
            }
        }

        /// <summary>
        /// Handler pre zmiznutý UI element
        /// </summary>
        private void OnElementDisappeared(object sender, ElementDisappearedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"➖ UI element disappeared: {e.ElementIdentifier}");

                // Aktualizuj kontext okna
                if (windowContexts.ContainsKey(e.WindowHandle))
                {
                    var context = windowContexts[e.WindowHandle];
                    context.UIElements.RemoveAll(el =>
                        el.AutomationId == e.ElementIdentifier ||
                        el.Name == e.ElementIdentifier);
                    context.LastUIUpdate = DateTime.Now;
                }

                // Označuje príkazy ktoré používajú tento element ako potenciálne problematické
                MarkCommandsWithMissingElement(e.ElementIdentifier);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error handling element disappearance: {ex.Message}");
            }
        }

        #endregion

        #region Command Management

        /// <summary>
        /// Pridá command do sekvencie
        /// </summary>
        protected void AddCommand(Command command)
        {
            try
            {
                currentSequence?.AddCommand(command);

                // Update element statistics
                UpdateElementUsage(command);

                // Trigger event
                CommandRecorded?.Invoke(this, new CommandRecordedEventArgs(command));

                System.Diagnostics.Debug.WriteLine($"📝 Recorded: {command.Type} - {command.ElementName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error adding command: {ex.Message}");
            }
        }

        /// <summary>
        /// Aktualizuje štatistiky použitia elementu
        /// </summary>
        private void UpdateElementUsage(Command command)
        {
            try
            {
                string elementKey = !string.IsNullOrEmpty(command.ElementId)
                    ? command.ElementId
                    : command.ElementName;

                if (string.IsNullOrEmpty(elementKey)) return;

                if (!elementStats.ContainsKey(elementKey))
                {
                    elementStats[elementKey] = new ElementUsageStats
                    {
                        ElementName = command.ElementName,
                        TotalUsage = 0,
                        FirstUsed = DateTime.Now,
                        LastUsed = DateTime.Now,
                        ElementType = command.Type.ToString(),
                        ControlType = command.ElementControlType ?? ""
                    };
                }

                var stats = elementStats[elementKey];
                stats.IncrementUsage(command.Type);

                // Trigger event
                ElementUsageUpdated?.Invoke(this, new ElementUsageEventArgs
                {
                    ElementName = elementKey,
                    Stats = stats
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error updating element usage: {ex.Message}");
            }
        }



        #endregion

        #region Window Management Logic

        /// <summary>
        /// Rozhodne či automaticky prepnúť na nové okno
        /// </summary>
        private bool ShouldAutoSwitchToWindow(WindowTrackingInfo windowInfo)
        {
            if (!AutoSwitchToNewWindows || windowInfo == null) return false;

            // Vždy prepni na dialógy a message boxy
            if (windowInfo.WindowType == WindowType.Dialog ||
                windowInfo.WindowType == WindowType.MessageBox)
                return true;

            // Prepni ak je to okno z target procesu a je to významné okno
            if (!string.IsNullOrEmpty(targetProcessName) &&
                windowInfo.ProcessName.Equals(targetProcessName, StringComparison.OrdinalIgnoreCase))
            {
                // Prepni len ak nie je to hlavné okno
                return windowInfo.WindowType != WindowType.MainWindow;
            }

            return false;
        }

        /// <summary>
        /// Automaticky prepne na nové okno
        /// </summary>
        private void AutoSwitchToNewWindow(WindowTrackingInfo windowInfo)
        {
            if (windowInfo == null) return;

            try
            {
                var previousTarget = targetWindow;
                targetWindow = windowInfo.WindowHandle;

                System.Diagnostics.Debug.WriteLine($"🔄 Auto-switched from {GetWindowTitle(previousTarget)} to {windowInfo.Title}");

                // Vytvor kontext pre nové okno
                CreateInitialWindowContext(windowInfo.WindowHandle);

                // Pridaj command pre switch ak je potrebný
                if (IsRecording)
                {
                    AddAutoSwitchCommand(windowInfo, previousTarget);
                }

                // Spusti skenovanie pre nové okno
                if (EnableRealTimeElementScanning)
                {
                    uiElementScanner.AddWindowToScan(windowInfo.WindowHandle);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error auto-switching window: {ex.Message}");
            }
        }

        /// <summary>
        /// Pridá okno do kontextu bez prepnutia
        /// </summary>
        private void AddWindowToContext(WindowTrackingInfo windowInfo)
        {
            if (windowInfo == null) return;

            try
            {
                if (!windowContexts.ContainsKey(windowInfo.WindowHandle))
                {
                    CreateInitialWindowContext(windowInfo.WindowHandle);

                    // Pridaj do skeneru ak je povolené
                    if (EnableRealTimeElementScanning)
                    {
                        uiElementScanner.AddWindowToScan(windowInfo.WindowHandle);
                    }

                    System.Diagnostics.Debug.WriteLine($"📝 Added window to context: {windowInfo.Title}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error adding window to context: {ex.Message}");
            }
        }

        /// <summary>
        /// Spracuje aktiváciu okna počas nahrávania - DOKONČENÁ METÓDA
        /// </summary>
        private void HandleWindowActivationDuringRecording(WindowContext context)
        {
            try
            {
                // Ak je to dialog alebo message box, automaticky prepni
                if (context.WindowType == WindowType.Dialog ||
                    context.WindowType == WindowType.MessageBox)
                {
                    var previousTarget = targetWindow;
                    targetWindow = context.WindowHandle;

                    System.Diagnostics.Debug.WriteLine($"🎯 Auto-switched to activated {context.WindowType}: {context.WindowTitle}");

                    if (IsRecording)
                    {
                        AddAutoSwitchCommand(context, previousTarget);
                    }
                }
                else
                {
                    // Pre ostatné typy okien len loguj aktiváciu
                    System.Diagnostics.Debug.WriteLine($"📋 Window activated during recording: {context.WindowTitle} ({context.WindowType})");

                    // Môžeme pridať možnosť manuálneho prepnutia neskôr
                    if (LogWindowChanges)
                    {
                        AddWindowActivationNote(context);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error handling window activation during recording: {ex.Message}");
            }
        }

        /// <summary>
        /// Spracuje zatvorenie target okna
        /// </summary>
        private void HandleTargetWindowClosed()
        {
            try
            {
                // Nájdi náhradné okno z rovnakého procesu
                var replacementWindow = windowContexts.Values
                    .Where(ctx => ctx.IsActive &&
                                  ctx.ProcessName.Equals(targetProcessName, StringComparison.OrdinalIgnoreCase) &&
                                  ctx.WindowType == WindowType.MainWindow)
                    .OrderByDescending(ctx => ctx.LastActivated)
                    .FirstOrDefault();

                if (replacementWindow != null)
                {
                    targetWindow = replacementWindow.WindowHandle;
                    System.Diagnostics.Debug.WriteLine($"🔄 Target window replaced with: {replacementWindow.WindowTitle}");

                    if (IsRecording)
                    {
                        AddWindowSwitchCommand($"Returned to {replacementWindow.WindowTitle}",
                                             replacementWindow.WindowHandle);
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ No replacement window found for closed target");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error handling target window close: {ex.Message}");
            }
        }

        #endregion

        #region Command Enhancement Methods

        /// <summary>
        /// Pridá automatický switch command - WindowTrackingInfo verzia
        /// </summary>
        private void AddAutoSwitchCommand(WindowTrackingInfo windowInfo, IntPtr previousWindow)
        {
            try
            {
                var switchCommand = new Command
                {
                    StepNumber = commandCounter++,
                    ElementName = $"AutoSwitch_To_{windowInfo.WindowType}",
                    Type = CommandType.Wait,
                    Value = "300", // 300ms čakanie na stabilizáciu okna
                    TargetWindow = windowInfo.Title,
                    TargetProcess = windowInfo.ProcessName,
                    ElementClass = "AutoWindowSwitch",
                    ElementControlType = windowInfo.WindowType.ToString(),
                    Timestamp = DateTime.Now,
                    ElementX = -1,
                    ElementY = -1
                };

                AddCommand(switchCommand);

                System.Diagnostics.Debug.WriteLine($"➕ Added auto-switch command: {switchCommand.ElementName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error adding auto-switch command: {ex.Message}");
            }
        }

        /// <summary>
        /// Pridá automatický switch command - WindowContext verzia
        /// </summary>
        private void AddAutoSwitchCommand(WindowContext context, IntPtr previousWindow)
        {
            try
            {
                var switchCommand = new Command
                {
                    StepNumber = commandCounter++,
                    ElementName = $"AutoSwitch_To_{context.WindowType}",
                    Type = CommandType.Wait,
                    Value = "300",
                    TargetWindow = context.WindowTitle,
                    TargetProcess = context.ProcessName,
                    ElementClass = "AutoWindowSwitch",
                    ElementControlType = context.WindowType.ToString(),
                    Timestamp = DateTime.Now,
                    ElementX = -1,
                    ElementY = -1
                };

                AddCommand(switchCommand);

                System.Diagnostics.Debug.WriteLine($"➕ Added auto-switch command: {switchCommand.ElementName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error adding auto-switch command: {ex.Message}");
            }
        }

        /// <summary>
        /// Pridá poznámku o aktivácii okna
        /// </summary>
        private void AddWindowActivationNote(WindowContext context)
        {
            try
            {
                var noteCommand = new Command
                {
                    StepNumber = commandCounter++,
                    ElementName = $"WindowActivated_{context.WindowType}",
                    Type = CommandType.Wait,
                    Value = "0", // Žiadne čakanie, len poznámka
                    TargetWindow = context.WindowTitle,
                    TargetProcess = context.ProcessName,
                    ElementClass = "WindowActivationNote",
                    ElementControlType = context.WindowType.ToString(),
                    Timestamp = DateTime.Now,
                    ElementX = -1,
                    ElementY = -1
                };

                AddCommand(noteCommand);

                System.Diagnostics.Debug.WriteLine($"📝 Added window activation note: {context.WindowTitle}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error adding window activation note: {ex.Message}");
            }
        }

        /// <summary>
        /// Aktualizuje existujúce príkazy s novými elementami
        /// </summary>
        private void UpdateExistingCommandsWithNewElements(List<UIElementSnapshot> newElements)
        {
            if (newElements == null) return;

            try
            {
                var commandsToUpdate = currentSequence?.Commands?.Where(cmd =>
                    cmd.Type == CommandType.Click ||
                    cmd.Type == CommandType.SetText ||
                    cmd.Type == CommandType.DoubleClick ||
                    cmd.Type == CommandType.RightClick).ToList();

                if (commandsToUpdate?.Any() == true)
                {
                    int updatedCount = 0;

                    foreach (var command in commandsToUpdate)
                    {
                        var betterElement = FindBetterElementMatch(command, newElements);
                        if (betterElement != null)
                        {
                            UpdateCommandWithBetterElement(command, betterElement);
                            updatedCount++;
                        }
                    }

                    if (updatedCount > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"Updated {updatedCount} commands with better element matches");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating existing commands: {ex.Message}");
            }
        }

        /// <summary>
        /// Nájde lepší element match pre príkaz
        /// </summary>
        private UIElementSnapshot FindBetterElementMatch(Command command, List<UIElementSnapshot> availableElements)
        {
            try
            {
                // Ak príkaz už má dobrý AutomationId, netreba aktualizovať
                if (!string.IsNullOrEmpty(command.ElementId) && command.ElementId.Length > 3)
                    return null;

                // Hľadaj element na rovnakej pozícii
                var candidates = availableElements.Where(el =>
                    Math.Abs(el.X - command.ElementX) < 10 &&
                    Math.Abs(el.Y - command.ElementY) < 10).ToList();

                if (!candidates.Any())
                {
                    // Rozšír hľadanie na väčšiu oblasť
                    candidates = availableElements.Where(el =>
                        Math.Abs(el.X - command.ElementX) < 50 &&
                        Math.Abs(el.Y - command.ElementY) < 50).ToList();
                }

                // Vráť najlepší kandidát s AutomationId
                return candidates.FirstOrDefault(el => !string.IsNullOrEmpty(el.AutomationId));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error finding better element match: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Aktualizuje príkaz s lepším elementom
        /// </summary>
        private void UpdateCommandWithBetterElement(Command command, UIElementSnapshot betterElement)
        {
            try
            {
                var oldElementId = command.ElementId;

                command.ElementId = betterElement.AutomationId;
                command.ElementName = betterElement.Name;
                command.ElementClass = betterElement.ClassName;
                command.ElementControlType = betterElement.ControlType;
                command.ElementX = betterElement.X;
                command.ElementY = betterElement.Y;

                System.Diagnostics.Debug.WriteLine($"🔄 Updated command element: '{oldElementId}' -> '{betterElement.AutomationId}'");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error updating command with better element: {ex.Message}");
            }
        }

        /// <summary>
        /// Označuje príkazy s chýbajúcim elementom
        /// </summary>
        private void MarkCommandsWithMissingElement(string elementIdentifier)
        {
            try
            {
                var affectedCommands = currentSequence?.Commands?.Where(cmd =>
                    cmd.ElementId == elementIdentifier ||
                    cmd.ElementName == elementIdentifier).ToList();

                if (affectedCommands?.Any() == true)
                {
                    foreach (var command in affectedCommands)
                    {
                        // Pridaj warning flag
                        command.ElementClass += "_MISSING";
                    }

                    System.Diagnostics.Debug.WriteLine($"⚠️ Marked {affectedCommands.Count} commands with missing element: {elementIdentifier}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error marking commands with missing element: {ex.Message}");
            }
        }

        /// <summary>
        /// Pridá window switch command
        /// </summary>
        protected void AddWindowSwitchCommand(string description, IntPtr windowHandle)
        {
            try
            {
                var switchCommand = new Command(commandCounter++,
                    "Window_Switch", CommandType.Wait, -1, -1)
                {
                    Value = "500", // 500ms wait
                    TargetWindow = description,
                    TargetProcess = GetProcessNameFromWindow(windowHandle),
                    ElementClass = "WindowSwitch",
                    ElementControlType = "AutoDetected",
                    Timestamp = DateTime.Now
                };

                AddCommand(switchCommand);
                System.Diagnostics.Debug.WriteLine($"➕ Added window switch command: {description}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error adding window switch command: {ex.Message}");
            }
        }

        #endregion

        #region Predictive Detection

        /// <summary>
        /// Spustí prediktívnu detekciu
        /// </summary>
        private void StartPredictiveDetection()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("🔮 Predictive detection started");
                AnalyzeCommandPatterns();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error starting predictive detection: {ex.Message}");
            }
        }

        #endregion

        #region Analysis Methods

        /// <summary>
        /// Analyzuje nahraté elementy
        /// </summary>
        public void AnalyzeRecordedElements()
        {
            try
            {
                if (currentSequence?.Commands == null || !currentSequence.Commands.Any())
                    return;

                System.Diagnostics.Debug.WriteLine("=== ANALYZING RECORDED ELEMENTS ===");

                var elementCommands = currentSequence.Commands.Where(c =>
                    c.Type == CommandType.Click ||
                    c.Type == CommandType.SetText ||
                    c.Type == CommandType.DoubleClick ||
                    c.Type == CommandType.RightClick).ToList();

                System.Diagnostics.Debug.WriteLine($"Found {elementCommands.Count} element-based commands");

                // Analyzuje WinUI3 elementy
                var winui3Commands = elementCommands.Where(c => c.IsWinUI3Element).ToList();
                if (winui3Commands.Any())
                {
                    System.Diagnostics.Debug.WriteLine($"WinUI3 commands: {winui3Commands.Count}");
                    var avgConfidence = winui3Commands.Average(c => c.ElementConfidence);
                    System.Diagnostics.Debug.WriteLine($"Average WinUI3 confidence: {avgConfidence:F2}");
                }

                // Analyzuje table commands
                var tableCommands = elementCommands.Where(c => c.IsTableCommand).ToList();
                if (tableCommands.Any())
                {
                    System.Diagnostics.Debug.WriteLine($"Table commands: {tableCommands.Count}");
                    var tableNames = tableCommands.Select(c => c.TableName).Distinct().ToList();
                    System.Diagnostics.Debug.WriteLine($"Tables used: {string.Join(", ", tableNames)}");
                }

                // Analyzuje spoľahlivosť identifikátorov
                var commandsWithStrongIds = elementCommands.Where(c => HasStrongIdentifier(c)).ToList();
                System.Diagnostics.Debug.WriteLine($"Commands with strong identifiers: {commandsWithStrongIds.Count}/{elementCommands.Count}");

                // Analyzuje command patterns
                AnalyzeCommandSequencePatterns(elementCommands);

                System.Diagnostics.Debug.WriteLine("=== ANALYSIS COMPLETE ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error analyzing recorded elements: {ex.Message}");
            }
        }

        /// <summary>
        /// Analyzuje patterns v sekvencii príkazov
        /// </summary>
        private void AnalyzeCommandSequencePatterns(List<Command> commands)
        {
            try
            {
                // Detekuje opakujúce sa patterns
                var clickSequences = 0;
                var formFillSequences = 0;

                for (int i = 0; i < commands.Count - 2; i++)
                {
                    var sequence = commands.Skip(i).Take(3).ToList();

                    if (sequence.All(c => c.Type == CommandType.Click))
                    {
                        clickSequences++;
                    }

                    if (sequence.Any(c => c.Type == CommandType.SetText) &&
                        sequence.Any(c => c.Type == CommandType.Click))
                    {
                        formFillSequences++;
                    }
                }

                if (clickSequences > 0)
                    System.Diagnostics.Debug.WriteLine($"Found {clickSequences} click sequence patterns");

                if (formFillSequences > 0)
                    System.Diagnostics.Debug.WriteLine($"Found {formFillSequences} form filling patterns");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error analyzing sequence patterns: {ex.Message}");
            }
        }

        /// <summary>
        /// Kontroluje či má príkaz silný identifikátor
        /// </summary>
        private bool HasStrongIdentifier(Command command)
        {
            return !string.IsNullOrEmpty(command.ElementId) &&
                   command.ElementId.Length > 3 &&
                   !command.ElementId.Contains("Unknown");
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Aktualizuje target window
        /// </summary>
        public void UpdateTargetWindow(IntPtr newTargetWindow)
        {
            try
            {
                var previousTarget = targetWindow;
                targetWindow = newTargetWindow;

                // Aktualizuj target process name
                targetProcessName = GetProcessNameFromWindow(newTargetWindow);

                // Aktualizuj skener
                if (EnableRealTimeElementScanning)
                {
                    uiElementScanner.SwitchPrimaryWindow(newTargetWindow);
                }

                System.Diagnostics.Debug.WriteLine($"🔄 Target window updated: {GetWindowTitle(newTargetWindow)}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error updating target window: {ex.Message}");
            }
        }

        /// <summary>
        /// Získa sledované okná
        /// </summary>
        public Dictionary<IntPtr, string> GetTrackedWindows()
        {
            return new Dictionary<IntPtr, string>(trackedWindows);
        }

        /// <summary>
        /// Pridá okno do sledovania
        /// </summary>
        public void AddWindowToTracking(IntPtr windowHandle, string description)
        {
            try
            {
                if (!trackedWindows.ContainsKey(windowHandle))
                {
                    trackedWindows[windowHandle] = description;
                    System.Diagnostics.Debug.WriteLine($"➕ Added window to tracking: {description}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error adding window to tracking: {ex.Message}");
            }
        }

        /// <summary>
        /// Prepne target window
        /// </summary>
        public void SwitchTargetWindow(IntPtr newTargetWindow)
        {
            try
            {
                if (trackedWindows.ContainsKey(newTargetWindow))
                {
                    UpdateTargetWindow(newTargetWindow);

                    if (IsRecording)
                    {
                        AddWindowSwitchCommand($"Switched to {trackedWindows[newTargetWindow]}", newTargetWindow);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error switching target window: {ex.Message}");
            }
        }

        /// <summary>
        /// Naplánuje cleanup kontextu
        /// </summary>
        private void ScheduleContextCleanup(IntPtr windowHandle)
        {
            Task.Delay(TimeSpan.FromMinutes(5)).ContinueWith(_ => {
                try
                {
                    if (windowContexts.ContainsKey(windowHandle))
                    {
                        var context = windowContexts[windowHandle];
                        if (!context.IsActive)
                        {
                            windowContexts.Remove(windowHandle);
                            System.Diagnostics.Debug.WriteLine($"🧹 Cleaned up context for: {context.WindowTitle}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Error in context cleanup: {ex.Message}");
                }
            });
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Skenuje UI elementy okna
        /// </summary>
        private List<UIElementInfo> ScanUIElements(IntPtr windowHandle)
        {
            try
            {
                return AdaptiveElementFinder.GetAllInteractiveElements(windowHandle);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error scanning UI elements: {ex.Message}");
                return new List<UIElementInfo>();
            }
        }

        //private List<UIElementInfo> ScanWindowElements(IntPtr windowHandle)
        //{
        //    var elements = new List<UIElementInfo>();

        //    try
        //    {
        //        AutomationElement window = AutomationElement.FromHandle(windowHandle);
        //        if (window == null) return elements;

        //        // Použite cache request pre lepší performance
        //        CacheRequest cacheRequest = new CacheRequest();
        //        cacheRequest.Add(AutomationElement.NameProperty);
        //        cacheRequest.Add(AutomationElement.AutomationIdProperty);
        //        cacheRequest.Add(AutomationElement.ClassNameProperty);
        //        cacheRequest.Add(AutomationElement.ControlTypeProperty);
        //        cacheRequest.Add(AutomationElement.BoundingRectangleProperty);
        //        cacheRequest.TreeScope = TreeScope.Element | TreeScope.Descendants;

        //        using (cacheRequest.Activate())
        //        {
        //            window = AutomationElement.FromHandle(windowHandle);
        //            var allElements = window.FindAll(TreeScope.Descendants, Condition.TrueCondition);

        //            foreach (AutomationElement element in allElements)
        //            {
        //                try
        //                {
        //                    var elementInfo = UIElementDetector.ConvertToUIElementInfo(element);
        //                    if (elementInfo != null)
        //                    {
        //                        elements.Add(elementInfo);
        //                    }
        //                }
        //                catch (ElementNotAvailableException)
        //                {
        //                    // Element sa zmenil počas skenovania - skip
        //                    continue;
        //                }
        //                catch (System.Runtime.InteropServices.COMException)
        //                {
        //                    // COM error - skip
        //                    continue;
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        System.Diagnostics.Debug.WriteLine($"Error scanning window elements: {ex.Message}");
        //    }

        //    return elements;
        //}

        /// <summary>
        /// Získa title okna
        /// </summary>
        private string GetWindowTitle(IntPtr windowHandle)
        {
            try
            {
                if (windowHandle == IntPtr.Zero)
                    return "No Window";

                var sb = new System.Text.StringBuilder(256);
                GetWindowText(windowHandle, sb, sb.Capacity);
                return sb.ToString();
            }
            catch
            {
                return "Unknown Window";
            }
        }

        /// <summary>
        /// Získa meno procesu z okna
        /// </summary>
        private string GetProcessNameFromWindow(IntPtr windowHandle)
        {
            try
            {
                if (windowHandle == IntPtr.Zero)
                    return "Unknown Process";

                GetWindowThreadProcessId(windowHandle, out uint processId);
                using (var process = Process.GetProcessById((int)processId))
                {
                    return process.ProcessName;
                }
            }
            catch
            {
                return "Unknown Process";
            }
        }

        /// <summary>
        /// Získa meno procesu
        /// </summary>
        private string GetProcessName(IntPtr windowHandle)
        {
            return GetProcessNameFromWindow(windowHandle);
        }

        /// <summary>
        /// Získa title z handle
        /// </summary>
        private string GetWindowTitleFromHandle(IntPtr windowHandle)
        {
            return GetWindowTitle(windowHandle);
        }

        /// <summary>
        /// Získa class z handle
        /// </summary>
        private string GetWindowClassFromHandle(IntPtr windowHandle)
        {
            try
            {
                if (windowHandle == IntPtr.Zero)
                    return "";

                var sb = new System.Text.StringBuilder(256);
                GetClassName(windowHandle, sb, sb.Capacity);
                return sb.ToString();
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Určí typ okna
        /// </summary>
        private WindowType DetermineWindowType(IntPtr windowHandle)
        {
            try
            {
                string className = GetWindowClassFromHandle(windowHandle);
                string title = GetWindowTitle(windowHandle);

                // MessageBox detection
                if (className.Contains("MessageBox") || className == "#32770")
                {
                    if (title.Contains("Error") || title.Contains("Warning") ||
                        title.Contains("Information") || title.Contains("Confirm"))
                        return WindowType.MessageBox;
                }

                // Dialog detection
                if (className.Contains("Dialog") || className == "#32770")
                    return WindowType.Dialog;

                // Child window detection
                IntPtr parent = GetParent(windowHandle);
                if (parent != IntPtr.Zero)
                    return WindowType.ChildWindow;

                return WindowType.MainWindow;
            }
            catch
            {
                return WindowType.MainWindow;
            }
        }

        #endregion

        #region Win32 API Imports

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Dispose pattern implementation
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (isRecording)
                {
                    StopRecording();
                }

                //globalHook?.Dispose();
                globalHook?.StopHooking();
                windowTracker?.Dispose();
                autoWindowDetector?.Dispose();
                uiElementScanner?.Dispose();

                System.Diagnostics.Debug.WriteLine("🧹 CommandRecorder disposed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error disposing CommandRecorder: {ex.Message}");
            }
        }

        public void AddWaitCommand(int waitTime)
        {
            if (!IsRecording)
                return;

            var waitCommand = new Command(commandCounter++, "Wait_Command", CommandType.Wait, 0, 0)
            {
                Value = waitTime.ToString(),
                TargetWindow = GetWindowTitle(targetWindow),
                TargetProcess = targetProcessName,
                Timestamp = DateTime.Now,
                ElementName = string.Format("Wait_{0}ms", waitTime)
            };

            AddCommand(waitCommand);
        }

        #endregion

        #region Logic for check - whether the application is recording itself
        /// <summary>
        /// Skontroluje, či UI element patrí AppCommander blacklistu
        /// </summary>
        private bool IsAppCommanderUIElement(UIElementInfo elementInfo)
        {
            if (elementInfo == null)
                return false;

            try
            {
                // Kontrola 1: AutomationId
                if (!string.IsNullOrEmpty(elementInfo.AutomationId) &&
                    AppCommanderUIElements.Contains(elementInfo.AutomationId))
                {
                    System.Diagnostics.Debug.WriteLine($"🚫 Blacklist match: AutomationId = {elementInfo.AutomationId}");
                    return true;
                }

                // Kontrola 2: Name
                if (!string.IsNullOrEmpty(elementInfo.Name) &&
                    AppCommanderUIElements.Contains(elementInfo.Name))
                {
                    System.Diagnostics.Debug.WriteLine($"🚫 Blacklist match: Name = {elementInfo.Name}");
                    return true;
                }

                // 'elementInfo.ElementText' in IsAppCommanderUIElem
                // Kontrola 3: Text content
                if (!string.IsNullOrEmpty(elementInfo.ElementText))
                {
                    foreach (var blacklistedText in AppCommanderUIElements)
                    {
                        if (elementInfo.ElementText.Contains(blacklistedText))
                        {
                            System.Diagnostics.Debug.WriteLine($"🚫 Blacklist match: Text contains '{blacklistedText}'");
                            return true;
                        }
                    }
                }

                // Kontrola 4: ClassName obsahuje "AppCommander"
                if (!string.IsNullOrEmpty(elementInfo.ClassName) &&
                    elementInfo.ClassName.Contains("AppCommander"))
                {
                    System.Diagnostics.Debug.WriteLine($"🚫 Blacklist match: ClassName = {elementInfo.ClassName}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Error checking blacklist: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Pomocná metóda - skontroluje element aj podľa jeho textu
        /// VOLITEĽNÉ: Môžete použiť pre extra kontrolu
        /// </summary>
        private bool ElementContainsBlacklistedText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            var blacklistedPhrases = new[]
            {
                "Start Recording",
                "Stop Recording",
                "▶    Play",
                "Pause",
                "Resume",
                "Click to Select",
                "Cancel Selection"
            };

            return blacklistedPhrases.Any(phrase =>
                text.IndexOf(phrase, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        #endregion
    }

    #region Supporting Classes - Len tie ktoré nie sú definované inde

    /// <summary>
    /// Kontext okna s rozšírenými informáciami
    /// </summary>
    public class WindowContext
    {
        public IntPtr WindowHandle { get; set; }
        public string WindowTitle { get; set; } = "";
        public string ProcessName { get; set; } = "";
        public WindowType WindowType { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastActivated { get; set; }
        public DateTime? ClosedAt { get; set; }
        public DateTime LastUIUpdate { get; set; }
        public bool IsActive { get; set; }
        public List<UIElementInfo> UIElements { get; set; } = new List<UIElementInfo>();
    }

    /// <summary>
    /// Typ zmeny kontextu
    /// </summary>
    public enum ContextChangeType
    {
        Created,
        Updated,
        Activated,
        Closed
    }

    #endregion

    #region Event Args Classes - Len tie ktoré nie sú definované inde

    /// <summary>
    /// Event args pre aktualizáciu použitia elementu
    /// </summary>
    public class ElementUsageEventArgs : EventArgs
    {
        public string ElementName { get; set; } = "";
        public ElementUsageStats Stats { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Event args pre zmenu kontextu okna
    /// </summary>
    public class WindowContextChangedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public WindowContext Context { get; set; }
        public ContextChangeType ChangeType { get; set; }
    }

    /// <summary>
    /// Event args pre aktualizáciu UI elementov
    /// </summary>
    public class UIElementsUpdatedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public List<UIElementInfo> PreviousElements { get; set; }
        public List<UIElementInfo> NewElements { get; set; }
        public WindowContext Context { get; set; }
    }
    #endregion // Event Args Classes - Len tie ktoré nie sú definované inde
}
