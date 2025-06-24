// CommandRecorder.cs -  recording commands and managing UI interactions with automatic detection features
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
        protected readonly WindowTrackingInfo windowTracker;
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

        // Configration of automatic detection
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
            windowTracker = new WindowTrackingInfo();
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

        private void OnWindowActivated(object sender, WindowActivatedEventArgs e)
        {
            throw new NotImplementedException();
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

                RecordingStateChanged?.Invoke(this, new RecordingStateChangedEventArgs
                {
                    IsRecording = true,
                    IsPaused = false,
                    SequenceName = sequenceName
                });

                System.Diagnostics.Debug.WriteLine("✅ Recording started successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error starting recording: {ex.Message}");
                throw;
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
                    windowTracker.StartTracking(primaryTarget, targetProcessName);
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

                RecordingStateChanged?.Invoke(this, new RecordingStateChangedEventArgs
                {
                    IsRecording = false,
                    IsPaused = false,
                    SequenceName = currentSequence?.Name ?? ""
                });

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
            RecordingStateChanged?.Invoke(this, new RecordingStateChangedEventArgs
            {
                IsRecording = true,
                IsPaused = true,
                SequenceName = currentSequence?.Name ?? ""
            });

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
            RecordingStateChanged?.Invoke(this, new RecordingStateChangedEventArgs
            {
                IsRecording = true,
                IsPaused = false,
                SequenceName = currentSequence?.Name ?? ""
            });

            System.Diagnostics.Debug.WriteLine("▶ Recording resumed");
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Overload pre WindowTrackingInfo
        /// </summary>
        private bool ShouldAutoSwitchToWindow(WindowTrackingInfo windowInfo)
        {
            return ShouldAutoSwitchToWindow(ConvertToWindowDetectionInfo(windowInfo));
        }

        private WindowTrackingInfo ConvertToWindowDetectionInfo(WindowTrackingInfo windowInfo)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Handler pre nové okno detekované window trackerom
        /// </summary>
        private void OnNewWindowDetected(object sender, NewWindowDetectedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🔍 Window tracker detected: {e.Description}");

                // Ak je automatické prepínanie zapnuté
                if(AutoSwitchToNewWindows && ShouldAutoSwitchToWindow(e.WindowInfo))
{
                    // Konvertuj WindowTrackingInfo na WindowDetectionInfo pred volaním
                    AutoSwitchToNewWindow(ConvertToWindowDetectionInfo(e.WindowInfo));
                }

                // Pridaj do sledovaných okien
                if (!trackedWindows.ContainsKey(e.WindowInfo.WindowHandle))
                {
                    trackedWindows[e.WindowInfo.WindowHandle] = e.Description;
                }

                // Trigger event pre UI
                WindowAutoDetected?.Invoke(this, new WindowAutoDetectedEventArgs
                {
                    WindowHandle = e.WindowInfo.WindowHandle,
                    Description = e.Description,
                    WindowInfo = ConvertToWindowTrackingInfo(e.WindowInfo)
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error handling new window detected: {ex.Message}");
            }
        }

        private void AutoSwitchToNewWindow(WindowTrackingInfo windowTrackingInfo)
        {
            throw new NotImplementedException();
        }

        private WindowTrackingInfo ConvertToWindowTrackingInfo(WindowTrackingInfo windowInfo)
        {
            throw new NotImplementedException();
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
                    command.UpdateFromElementInfo(elementInfo);
                }

                AddCommand(command);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error recording mouse click: {ex.Message}");
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
                CommandRecorded?.Invoke(this, new CommandRecordedEventArgs
                {
                    Command = command,
                    SequenceName = currentSequence?.Name ?? ""
                });

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
                        // UsageCount -> TotalUsage (podľa definície v Command.cs)
                        TotalUsage = 0,
                        FirstUsed = DateTime.Now,
                        LastUsed = DateTime.Now,
                        // Reliability vlastnosť neexistuje v ElementUsageStats
                        ElementType = command.Type.ToString(),
                        ControlType = command.ElementControlType ?? ""
                    };
                }

                var stats = elementStats[elementKey];
                // UsageCount -> TotalUsage
                stats.TotalUsage++;
                stats.LastUsed = DateTime.Now;

                // Použij IncrementUsage metódu z ElementUsageStats
                stats.IncrementUsage(command.Type);

                // Trigger event ostáva rovnaký
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

        #region Auto Window Detection Event Handlers

        /// <summary>
        /// Handler pre automaticky detekované nové okno
        /// </summary>
        private void OnAutoWindowDetected(object sender, AutoWindowDetectedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🪟 Auto-detected new window: {e.WindowInfo.Title}");
                System.Diagnostics.Debug.WriteLine($"   Type: {e.WindowInfo.WindowType}");
                System.Diagnostics.Debug.WriteLine($"   Process: {e.WindowInfo.ProcessName}");

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

                // Trigger rozšírený event
                WindowAutoDetected?.Invoke(this, new WindowAutoDetectedEventArgs
                {
                    WindowHandle = e.WindowInfo.WindowHandle,
                    Description = $"Auto-detected {e.WindowInfo.WindowType}: {e.WindowInfo.Title}",
                    WindowInfo = ConvertToWindowTrackingInfo(e.WindowInfo),
                    AutoSwitched = ShouldAutoSwitchToWindow(e.WindowInfo)
                });
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

                    // Ak sa zatvoril target window, pokús sa nájsť náhradu
                    if (e.WindowHandle == targetWindow && IsRecording)
                    {
                        HandleTargetWindowClosed();
                    }

                    // Odstráň z aktívnych kontextov po určitom čase
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
                    var context = windowContexts[e.WindowHandle];
                    var previousCount = context.UIElements.Count;
                    context.UIElements = e.NewElements;
                    context.LastUIUpdate = DateTime.Now;

                    System.Diagnostics.Debug.WriteLine($"🔄 UI elements changed in: {context.WindowTitle}");
                    System.Diagnostics.Debug.WriteLine($"   Previous: {previousCount}, New: {e.NewElements.Count}");

                    // Ak je povolené auto-update, aktualizuj existujúce príkazy
                    if (AutoUpdateExistingCommands && e.WindowHandle == targetWindow)
                    {
                        UpdateExistingCommandsWithNewElements(e.NewElements);
                    }

                    // Trigger event
                    UIElementsUpdated?.Invoke(this, new UIElementsUpdatedEventArgs
                    {
                        WindowHandle = e.WindowHandle,
                        PreviousElements = e.PreviousElements,
                        NewElements = e.NewElements,
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
                System.Diagnostics.Debug.WriteLine($"➕ New UI element detected: {e.Element.Name}");
                System.Diagnostics.Debug.WriteLine($"   Type: {e.Element.ControlType}");
                System.Diagnostics.Debug.WriteLine($"   Window: {GetWindowTitle(e.WindowHandle)}");

                // Aktualizuj kontext okna
                if (windowContexts.ContainsKey(e.WindowHandle))
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

        #region Window Management Logic

        /// <summary>
        /// Rozhodne či automaticky prepnúť na nové okno
        /// </summary>
        private bool ShouldAutoSwitchToWindow(WindowDetectionInfo windowInfo)
        {
            if (!AutoSwitchToNewWindows) return false;

            // Vždy prepni na dialógy a message boxy
            if (windowInfo.WindowType == WindowType.Dialog ||
                windowInfo.WindowType == WindowType.MessageBox)
                return true;

            // Prepni na modálne okná
            if (windowInfo.IsModal)
                return true;

            // Prepni ak je to okno z target procesu a je to významné okno
            if (!string.IsNullOrEmpty(targetProcessName) &&
                windowInfo.ProcessName.Equals(targetProcessName, StringComparison.OrdinalIgnoreCase))
            {
                // Prepni len ak nie je to hlavné okno (aby sa nepreskakoval medzi hlávnymi oknami)
                return windowInfo.WindowType != WindowType.MainWindow;
            }

            return false;
        }

        /// <summary>
        /// Automaticky prepne na nové okno
        /// </summary>
        private void AutoSwitchToNewWindow(WindowDetectionInfo windowInfo)
        {
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
        private void AddWindowToContext(WindowDetectionInfo windowInfo)
        {
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
        /// Spracuje aktiváciu okna počas nahrávania
        /// </summary>
        private void HandleWindowActivationDuringRecording(WindowContext context)
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
                    AddAutoSwitchCommand(new WindowDetectionInfo
                    {
                        WindowHandle = context.WindowHandle,
                        Title = context.WindowTitle,
                        WindowType = context.WindowType,
                        ProcessName = context.ProcessName
                    }, previousTarget);
                }
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
        /// Pridá automatický switch command
        /// </summary>
        private void AddAutoSwitchCommand(WindowDetectionInfo windowInfo, IntPtr previousWindow)
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
        /// Aktualizuje existujúce príkazy s novými elementami
        /// </summary>
        private void UpdateExistingCommandsWithNewElements(List<UIElementInfo> newElements)
        {
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
                        System.Diagnostics.Debug.WriteLine($"🔄 Updated {updatedCount} commands with better element matches");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error updating existing commands: {ex.Message}");
            }
        }

        /// <summary>
        /// Nájde lepší element match pre príkaz
        /// </summary>
        private UIElementInfo FindBetterElementMatch(Command command, List<UIElementInfo> availableElements)
        {
            try
            {
                // Ak príkaz už má dobrý AutomationId, netreba aktualizovať
                if (!string.IsNullOrEmpty(command.ElementId) && command.ElementId.Length > 3)
                    return null;

                // Hľadaj element na rovnakej pozícii alebo s podobným názvom
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
        private void UpdateCommandWithBetterElement(Command command, UIElementInfo betterElement)
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
                // Implementácia prediktívnej detekcie založenej na patterns
                System.Diagnostics.Debug.WriteLine("🔮 Predictive detection started");

                // Analyzuj existujúce príkazy pre patterns
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
                var winui3Commands = elementCommands.Where(c => c.ElementClass?.Contains("Microsoft.UI") == true).ToList();
                if (winui3Commands.Any())
                {
                    System.Diagnostics.Debug.WriteLine($"WinUI3 commands: {winui3Commands.Count}");
                }

                // Analyzuj spoľahlivosť identifikátorov
                var commandsWithStrongIds = elementCommands.Where(c => HasStrongIdentifier(c)).ToList();
                System.Diagnostics.Debug.WriteLine($"Commands with strong identifiers: {commandsWithStrongIds.Count}/{elementCommands.Count}");

                System.Diagnostics.Debug.WriteLine("=== ANALYSIS COMPLETE ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error analyzing recorded elements: {ex.Message}");
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

        /// <summary>
        /// Konvertuje WindowDetectionInfo na WindowTrackingInfo
        /// </summary>
        private WindowTrackingInfo ConvertToWindowTrackingInfo(WindowDetectionInfo detectionInfo)
        {
            return new WindowTrackingInfo
            {
                WindowHandle = detectionInfo.WindowHandle,
                Title = detectionInfo.Title,
                ProcessName = detectionInfo.ProcessName,
                WindowType = detectionInfo.WindowType,
                IsModal = detectionInfo.IsModal,
                DetectedAt = detectionInfo.DetectedAt,
                ClassName = detectionInfo.ClassName
            };
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

        internal void StartRecording(IntPtr targetWindowHandle)
        {
            throw new NotImplementedException();
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
    /// Informácie o detekovanom okne
    /// </summary>
    public class WindowDetectionInfo
    {
        public IntPtr WindowHandle { get; set; }
        public string Title { get; set; } = "";
        public string ProcessName { get; set; } = "";
        public WindowType WindowType { get; set; }
        public bool IsModal { get; set; }
        public DateTime DetectedAt { get; set; }
        public string ClassName { get; set; } = "";
    }

    ///// <summary>
    ///// Automatický detektor okien - zjednodušený
    ///// </summary>
    //public class AutoWindowDetector
    //{
    //    public bool EnableDialogDetection { get; set; } = true;
    //    public bool EnableMessageBoxDetection { get; set; } = true;
    //    public bool EnableChildWindowDetection { get; set; } = true;
    //    public bool EnableWinUI3Detection { get; set; } = true;
    //    public DetectionSensitivity DetectionSensitivity { get; set; } = DetectionSensitivity.Medium;

    //    public event EventHandler<AutoWindowDetectedEventArgs> NewWindowDetected;
    //    public event EventHandler<WindowActivatedEventArgs> WindowActivated;
    //    public event EventHandler<WindowClosedEventArgs> WindowClosed;

    //    private bool isDetecting = false;

    //    public void StartDetection(IntPtr primaryWindow, string targetProcess)
    //    {
    //        isDetecting = true;
    //        System.Diagnostics.Debug.WriteLine("🔍 AutoWindowDetector started");
    //    }

    //    public void StopDetection()
    //    {
    //        isDetecting = false;
    //        System.Diagnostics.Debug.WriteLine("🛑 AutoWindowDetector stopped");
    //    }
    //}

    ///// <summary>
    ///// Skener UI elementov - zjednodušený
    ///// </summary>
    //public class UIElementScanner
    //{
    //    public int ScanInterval { get; set; } = 750;
    //    public bool EnableDeepScanning { get; set; } = true;
    //    public bool EnableWinUI3ElementDetection { get; set; } = true;
    //    public int MaxElementsPerScan { get; set; } = 100;

    //    public event EventHandler<UIElementsChangedEventArgs> ElementsChanged;
    //    public event EventHandler<NewElementDetectedEventArgs> NewElementDetected;
    //    public event EventHandler<ElementDisappearedEventArgs> ElementDisappeared;

    //    private bool isScanning = false;

    //    public void StartScanning(IntPtr primaryWindow)
    //    {
    //        isScanning = true;
    //        System.Diagnostics.Debug.WriteLine("🔍 UIElementScanner started");
    //    }

    //    public void StopScanning()
    //    {
    //        isScanning = false;
    //        System.Diagnostics.Debug.WriteLine("🛑 UIElementScanner stopped");
    //    }

    //    public void AddWindowToScan(IntPtr windowHandle)
    //    {
    //        System.Diagnostics.Debug.WriteLine($"➕ Added window to scan: {windowHandle}");
    //    }

    //    public void SwitchPrimaryWindow(IntPtr newPrimaryWindow)
    //    {
    //        System.Diagnostics.Debug.WriteLine($"🔄 Switched primary scan window: {newPrimaryWindow}");
    //    }
    //}

    /// <summary>
    /// Citlivosť detekcie
    /// </summary>
    public enum DetectionSensitivity
    {
        Low,
        Medium,
        High,
        VeryHigh
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

    ///// <summary>
    ///// Event args pre zmenu stavu nahrávania
    ///// </summary>
    //public class RecordingStateChangedEventArgs : EventArgs
    //{
    //    public bool IsRecording { get; set; }
    //    public bool IsPaused { get; set; }
    //    public string SequenceName { get; set; } = "";
    //    public DateTime Timestamp { get; set; } = DateTime.Now;
    //}

    /// <summary>
    /// Event args pre nahranie príkazu
    /// </summary>
    public class CommandRecordedEventArgs : EventArgs
    {
        public Command Command { get; set; }
        public string SequenceName { get; set; } = "";
        public int CommandNumber { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

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

    /// <summary>
    /// Event args pre automaticky detekované okno
    /// </summary>
    public class AutoWindowDetectedEventArgs : EventArgs
    {
        public WindowDetectionInfo WindowInfo { get; set; }
        public string DetectionMethod { get; set; }
    }

    /// <summary>
    /// Event args pre nový UI element
    /// </summary>
    public class NewElementDetectedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public UIElementInfo Element { get; set; }
        public string DetectionMethod { get; set; }
    }

    /// <summary>
    /// Event args pre zmiznutý element
    /// </summary>
    public class ElementDisappearedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public string ElementIdentifier { get; set; }
        public DateTime DisappearedAt { get; set; }
    }

    ///// <summary>
    ///// Event args pre window auto detection
    ///// </summary>
    //public class WindowAutoDetectedEventArgs : EventArgs
    //{
    //    public IntPtr WindowHandle { get; set; }
    //    public string Description { get; set; }
    //    public WindowTrackingInfo WindowInfo { get; set; }
    //    public bool AutoSwitched { get; set; }
    //}

    /// <summary>
    /// Event args pre UI elementy changed
    /// </summary>
    public class UIElementsChangedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; }
        public List<UIElementInfo> NewElements { get; set; }
        public List<UIElementInfo> PreviousElements { get; set; }
    }

    /// <summary>
    /// Typ tlačidla myši
    /// </summary>
    public enum MouseButton
    {
        Left,
        Right,
        Middle
    }
    #endregion
}
