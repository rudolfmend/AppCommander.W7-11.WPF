// CommandRecorder.cs - Kompletná opravená verzia bez konfliktov
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using AppCommander.W7_11.WPF.Core;
using System.Threading.Tasks;

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
        public bool LogWindowChanges { get; set; } = true;

        // WinUI3 debugging properties
        public bool EnableWinUI3Analysis { get; set; } = true;
        public bool EnableDetailedLogging { get; set; } = true;

        #endregion

        #region Events

        public event EventHandler<CommandRecordedEventArgs> CommandRecorded;
        public event EventHandler<RecordingStateChangedEventArgs> RecordingStateChanged;
        public event EventHandler<ElementUsageEventArgs> ElementUsageUpdated;
        public event EventHandler<WindowAutoDetectedEventArgs> WindowAutoDetected;

        // **Rozšírené eventy**
        public event EventHandler<WindowContextChangedEventArgs> WindowContextChanged;
        public event EventHandler<UIElementsUpdatedEventArgs> UIElementsUpdated;

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

        #region Recording Methods

        /// <summary>
        /// Spustí nahrávanie s automatickou detekciou
        /// </summary>
        public virtual void StartRecording(string sequenceName, IntPtr targetWindowHandle = default(IntPtr))
        {
            if (isRecording)
                return;

            try
            {
                System.Diagnostics.Debug.WriteLine($"🎬 Starting recording: {sequenceName}");

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
                    currentSequence.AutoFindTarget = true;
                    currentSequence.MaxWaitTimeSeconds = 30;

                    // Pridaj primary target do sledovaných okien
                    trackedWindows[targetWindow] = $"{targetProcessName} - {currentSequence.TargetWindowTitle}";

                    System.Diagnostics.Debug.WriteLine($"Recording target: {targetProcessName} - {currentSequence.TargetWindowTitle}");
                }

                // **Spusti automatické služby**
                StartAutomaticServices(targetWindowHandle);

                // **Vytvor počiatočný kontext okna**
                CreateInitialWindowContext(targetWindowHandle);

                // Start global hooks
                globalHook.StartHooking();
                isRecording = true;

                RecordingStateChanged?.Invoke(this, new RecordingStateChangedEventArgs(true, false, sequenceName));

                System.Diagnostics.Debug.WriteLine("✅ Recording started successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error starting recording: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Overload pre spätnosť
        /// </summary>
        public void StartRecording(IntPtr targetWindowHandle)
        {
            StartRecording($"Recording_{DateTime.Now:yyyyMMdd_HHmmss}", targetWindowHandle);
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
                WindowAutoDetected?.Invoke(this, new WindowAutoDetectedEventArgs(
                    e.WindowInfo?.WindowHandle ?? IntPtr.Zero,
                    $"Auto-detected: {e.WindowInfo?.Title ?? "Unknown"}",
                    e.WindowInfo?.Title,
                    e.WindowInfo?.ProcessName,
                    e.WindowInfo?.WindowType ?? WindowType.MainWindow)
                {
                    WindowInfo = e.WindowInfo
                });
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
        /// Handler pre stlačenie klávesy
        /// </summary>
        private void OnKeyPressed(object sender, KeyPressedEventArgs e)
        {
            if (!IsRecording) return;

            try
            {
                var command = new Command(commandCounter++, "Key_Press", CommandType.KeyPress, 0, 0)
                {
                    Value = e.Key.ToString(),
                    Key = e.Key,
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
        /// Handler pre klik myšou
        /// </summary>
        private void OnMouseClicked(object sender, MouseClickedEventArgs e)
        {
            if (!IsRecording) return;

            try
            {
                // Zisti UI element na pozícii kliknutia
                var elementInfo = UIElementDetector.GetElementAtPoint(e.X, e.Y);

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
                    // Namiesto duplicitnej konverzie:
                    var convertedPrevious = e.PreviousElements?.Select(snapshot => new UIElementInfo { /* konverzia */ }).ToList();
                    var convertedNew = context.UIElements; // už konvertované vyššie

                    UIElementsUpdated?.Invoke(this, new UIElementsUpdatedEventArgs
                    {
                        WindowHandle = e.WindowHandle,
                        PreviousElements = convertedPrevious,
                        NewElements = convertedNew,
                        Context = context
                    });

                    context.LastUIUpdate = DateTime.Now;

                    System.Diagnostics.Debug.WriteLine($"🔄 UI elements changed in: {context.WindowTitle}");
                    System.Diagnostics.Debug.WriteLine($"   Previous: {previousCount}, New: {context.UIElements.Count}");

                    // If auto-update is enabled, update existing commands
                    if (AutoUpdateExistingCommands && e.WindowHandle == targetWindow)
                    {
                        UpdateExistingCommandsWithNewElements(e.NewElements);
                    }

                    // Trigger event
                    UIElementsUpdated?.Invoke(this, new UIElementsUpdatedEventArgs
                    {
                        WindowHandle = e.WindowHandle,
                        PreviousElements = e.PreviousElements?.Select(snapshot => new UIElementInfo
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
                        }).ToList(),
                        NewElements = e.NewElements?.Select(snapshot => new UIElementInfo
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
                        }).ToList(),
                        Context = context
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error handling UI elements change: {ex.Message}");
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
                    if (!context.UIElements.Any(el => el.AutomationId == e.Element.AutomationId &&
                                                      el.Name == e.Element.Name))
                    {
                        context.UIElements.Add(e.Element);
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

        /// <summary>
        /// Analyzuje patterns v príkazoch
        /// </summary>
        private void AnalyzeCommandPatterns()
        {
            // Implementácia analýzy patterns
            // Napríklad: detekcia opakujúcich sa sekvencií, common UI flows, atď.
            try
            {
                if (currentSequence?.Commands?.Count >= 3)
                {
                    // Analyzuj posledné 3 príkazy pre patterns

                    //var recentCommands = currentSequence.Commands.TakeLast(3).ToList();
                    var recentCommands = currentSequence.Commands.OrderByDescending(c => c.Timestamp).Take(3).ToList();

                    // Detekuj opakujúce sa akcie
                    if (recentCommands.All(c => c.Type == CommandType.Click))
                    {
                        System.Diagnostics.Debug.WriteLine("🔮 Pattern detected: Multiple clicks sequence");
                    }

                    // Detekuj form filling pattern
                    if (recentCommands.Any(c => c.Type == CommandType.SetText) &&
                        recentCommands.Any(c => c.Type == CommandType.Click))
                    {
                        System.Diagnostics.Debug.WriteLine("🔮 Pattern detected: Form filling workflow");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error analyzing command patterns: {ex.Message}");
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

                // Analyzuj WinUI3 elementy
                var winui3Commands = elementCommands.Where(c => c.IsWinUI3Element).ToList();
                if (winui3Commands.Any())
                {
                    System.Diagnostics.Debug.WriteLine($"WinUI3 commands: {winui3Commands.Count}");
                    var avgConfidence = winui3Commands.Average(c => c.ElementConfidence);
                    System.Diagnostics.Debug.WriteLine($"Average WinUI3 confidence: {avgConfidence:F2}");
                }

                // Analyzuj table commands
                var tableCommands = elementCommands.Where(c => c.IsTableCommand).ToList();
                if (tableCommands.Any())
                {
                    System.Diagnostics.Debug.WriteLine($"Table commands: {tableCommands.Count}");
                    var tableNames = tableCommands.Select(c => c.TableName).Distinct().ToList();
                    System.Diagnostics.Debug.WriteLine($"Tables used: {string.Join(", ", tableNames)}");
                }

                // Analyzuj spoľahlivosť identifikátorov
                var commandsWithStrongIds = elementCommands.Where(c => HasStrongIdentifier(c)).ToList();
                System.Diagnostics.Debug.WriteLine($"Commands with strong identifiers: {commandsWithStrongIds.Count}/{elementCommands.Count}");

                // Analyzuj command patterns
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
                // Detekuj opakujúce sa patterns
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
                ElementName = $"Wait_{waitTime}ms"
            };

            AddCommand(waitCommand);
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

    ///// <summary>
    ///// Event args pre UI elementy changed
    ///// </summary>
    //public class UIElementsChangedEventArgs : EventArgs
    //{
    //    public IntPtr WindowHandle { get; set; }
    //    public List<UIElementInfo> NewElements { get; set; }
    //    public List<UIElementInfo> PreviousElements { get; set; }
    //}

    ///// <summary>
    ///// Event args pre nový UI element
    ///// </summary>
    //public class NewElementDetectedEventArgs : EventArgs
    //{
    //    public IntPtr WindowHandle { get; set; }
    //    public UIElementInfo Element { get; set; }
    //    public string DetectionMethod { get; set; }
    //}

    ///// <summary>
    ///// Event args pre key press
    ///// </summary>
    //public class KeyPressedEventArgs : EventArgs
    //{
    //    public System.Windows.Forms.Keys Key { get; set; }
    //    public DateTime Timestamp { get; set; } = DateTime.Now;
    //}

    ///// <summary>
    ///// Event args pre mouse click
    ///// </summary>
    //public class MouseClickedEventArgs : EventArgs
    //{
    //    public int X { get; set; }
    //    public int Y { get; set; }
    //    public System.Windows.Forms.MouseButtons Button { get; set; }
    //    public DateTime Timestamp { get; set; } = DateTime.Now;
    //}

    #endregion
}
